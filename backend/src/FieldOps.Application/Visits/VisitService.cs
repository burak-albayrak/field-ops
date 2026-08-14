using FieldOps.Application.Abstractions.Persistence;
using FieldOps.Application.Common.Exceptions;
using FieldOps.Application.Visits.Models;
using FieldOps.Domain.Entities;

namespace FieldOps.Application.Visits;

public class VisitService : IVisitService
{
    private readonly IVisitRepository _visitRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public VisitService(
        IVisitRepository visitRepository,
        IEmployeeRepository employeeRepository,
        IStoreRepository storeRepository,
        IUnitOfWork unitOfWork)
    {
        _visitRepository = visitRepository;
        _employeeRepository = employeeRepository;
        _storeRepository = storeRepository;
        _unitOfWork = unitOfWork;
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
}
