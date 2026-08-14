using FieldOps.Application.Abstractions.Persistence;
using FieldOps.Application.Abstractions.Outbox;
using FieldOps.Application.Common.Exceptions;
using FieldOps.Application.Common.Geography;
using FieldOps.Application.Visits.Models;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;

namespace FieldOps.Application.Visits;

public class VisitService : IVisitService
{
    private const double MaximumStartDistanceMeters = 200d;

    private readonly IVisitRepository _visitRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxWriter _outboxWriter;

    public VisitService(
        IVisitRepository visitRepository,
        IEmployeeRepository employeeRepository,
        IStoreRepository storeRepository,
        IUnitOfWork unitOfWork,
        IOutboxWriter outboxWriter)
    {
        _visitRepository = visitRepository;
        _employeeRepository = employeeRepository;
        _storeRepository = storeRepository;
        _unitOfWork = unitOfWork;
        _outboxWriter = outboxWriter;
    }

    public async Task<VisitDetailDto> CreateAsync(
        CreateVisitInput input,
        CancellationToken cancellationToken = default)
    {
        if (input.EmployeeId <= 0)
        {
            throw new ApplicationValidationException(nameof(input.EmployeeId), "EmployeeId must be greater than zero.");
        }

        if (input.StoreId <= 0)
        {
            throw new ApplicationValidationException(nameof(input.StoreId), "StoreId must be greater than zero.");
        }

        if (!await _employeeRepository.ExistsAsync(input.EmployeeId, cancellationToken))
        {
            throw new EmployeeNotFoundException(input.EmployeeId);
        }

        if (!await _storeRepository.ExistsAsync(input.StoreId, cancellationToken))
        {
            throw new StoreNotFoundException(input.StoreId);
        }

        // Aynı use case içindeki Add ve SaveChanges, Application'ın tek commit sınırını açık tutar.
        var visit = new Visit(input.EmployeeId, input.StoreId, input.PlannedDate, DateTime.UtcNow);
        _visitRepository.Add(visit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var detail = await _visitRepository.GetDetailAsync(visit.Id, cancellationToken);

        return detail ?? throw new InvalidOperationException("The newly created visit could not be retrieved.");
    }

    public async Task<VisitDetailDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var detail = await _visitRepository.GetDetailAsync(id, cancellationToken);

        return detail ?? throw new VisitNotFoundException(id);
    }

    public async Task<VisitDetailDto> StartAsync(
        long id,
        StartVisitInput input,
        CancellationToken cancellationToken = default)
    {
        ValidateStartCoordinates(input);

        var visit = await _visitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new VisitNotFoundException(id);

        // Saf Domain guard'ı, durum çatışmasını mesafe kontrolünden önce ve mutasyon yapmadan önceliklendirir.
        visit.EnsureCanStart();

        var storeCoordinates = await _storeRepository.GetCoordinatesAsync(visit.StoreId, cancellationToken)
            ?? throw new StoreNotFoundException(visit.StoreId);

        var distanceMeters = HaversineDistanceCalculator.CalculateMeters(
            storeCoordinates.Latitude,
            storeCoordinates.Longitude,
            input.Latitude,
            input.Longitude);

        if (distanceMeters > MaximumStartDistanceMeters)
        {
            throw new VisitTooFarFromStoreException(id, distanceMeters, MaximumStartDistanceMeters);
        }

        visit.Start(DateTime.UtcNow, input.Latitude, input.Longitude);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var detail = await _visitRepository.GetDetailAsync(visit.Id, cancellationToken);

        return detail ?? throw new InvalidOperationException("The newly started visit could not be retrieved.");
    }

    public async Task<VisitDetailDto> CompleteAsync(
        long id,
        CompleteVisitInput input,
        CancellationToken cancellationToken = default)
    {
        var visit = await _visitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new VisitNotFoundException(id);

        if (visit.Status == VisitStatus.Completed)
        {
            // Retry, gerçek bir durum geçişi değildir; ilk tamamlamanın zamanı, notu ve Version'ı korunur.
            var existingDetail = await _visitRepository.GetDetailAsync(visit.Id, cancellationToken);

            return existingDetail
                ?? throw new InvalidOperationException("The completed visit could not be retrieved.");
        }

        var completionTime = DateTime.UtcNow;
        // PostgreSQL timestamptz mikro-saniye hassasiyetindedir; tek zamanı bu hassasiyete indirgemek
        // Visit'teki değer ile immutable JSON payload'ın veritabanından okunduğunda birebir aynı kalmasını sağlar.
        completionTime = completionTime.AddTicks(-(completionTime.Ticks % TimeSpan.TicksPerMicrosecond));

        visit.Complete(completionTime, input.Notes);
        _outboxWriter.AddVisitCompleted(
            visit.Id,
            visit.EmployeeId,
            visit.StoreId,
            completionTime);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Tracked entity başarısız denemenin değerlerini hâlâ taşıyabilir; karar yalnızca taze DB projeksiyonuyla verilir.
            var currentDetail = await _visitRepository.GetDetailAsync(visit.Id, cancellationToken);

            if (currentDetail?.Status == VisitStatus.Completed)
            {
                return currentDetail;
            }

            throw;
        }

        var detail = await _visitRepository.GetDetailAsync(visit.Id, cancellationToken);

        return detail ?? throw new InvalidOperationException("The newly completed visit could not be retrieved.");
    }

    public async Task<VisitDetailDto> CancelAsync(
        long id,
        CancelVisitInput input,
        CancellationToken cancellationToken = default)
    {
        if (input.Version <= 0)
        {
            throw new ApplicationValidationException(nameof(input.Version), "Version must be greater than zero.");
        }

        var visit = await _visitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new VisitNotFoundException(id);

        if (input.Version != visit.Version)
        {
            // Eski istemci görünümü, daha yeni lifecycle kararını state kontrolüne ulaşmadan conflict olarak korur.
            throw new ConcurrencyConflictException();
        }

        visit.Cancel();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var detail = await _visitRepository.GetDetailAsync(visit.Id, cancellationToken);

        return detail ?? throw new InvalidOperationException("The newly cancelled visit could not be retrieved.");
    }

    public async Task<VisitListResult> ListAsync(
        VisitListFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new ApplicationValidationException(nameof(page), "Page must be at least 1.");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ApplicationValidationException(nameof(pageSize), "PageSize must be between 1 and 100.");
        }

        if (filter.StartDate.HasValue && filter.EndDate.HasValue && filter.StartDate > filter.EndDate)
        {
            throw new ApplicationValidationException(nameof(filter.StartDate), "StartDate must not be later than EndDate.");
        }

        var skipAsLong = ((long)page - 1) * pageSize;

        if (skipAsLong > int.MaxValue)
        {
            throw new ApplicationValidationException(nameof(page), "Page is too large.");
        }

        // TotalCount yerine pageSize + 1, büyük Visit tablolarında ek COUNT(*) sorgusunu önler.
        var visits = await _visitRepository.ListAsync(
            filter,
            (int)skipAsLong,
            pageSize + 1,
            cancellationToken);

        var hasNextPage = visits.Count > pageSize;
        var items = visits.Take(pageSize).ToList();

        return new VisitListResult(items, page, pageSize, hasNextPage);
    }

    private static void ValidateStartCoordinates(StartVisitInput input)
    {
        if (double.IsNaN(input.Latitude) || double.IsInfinity(input.Latitude)
            || input.Latitude is < -90d or > 90d)
        {
            throw new ApplicationValidationException(nameof(input.Latitude), "Latitude must be between -90 and 90.");
        }

        if (double.IsNaN(input.Longitude) || double.IsInfinity(input.Longitude)
            || input.Longitude is < -180d or > 180d)
        {
            throw new ApplicationValidationException(nameof(input.Longitude), "Longitude must be between -180 and 180.");
        }
    }
}
