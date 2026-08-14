using FieldOps.Application.Abstractions.Persistence;
using FieldOps.Application.Visits.Models;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Infrastructure.Persistence.Repositories;

public class VisitRepository : IVisitRepository
{
    private readonly AppDbContext _context;

    public VisitRepository(AppDbContext context)
    {
        _context = context;
    }

    // Repository yalnızca entity'yi aynı scoped DbContext üzerinde takip etmeye alır; kalıcılık sınırı UnitOfWork'tür.
    public void Add(Visit visit)
    {
        _context.Visits.Add(visit);
    }

    // Mutasyondan sonra SaveChanges'in değişiklikleri görmesi için entity aynı scoped DbContext tarafından izlenir.
    public Task<Visit?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return _context.Visits.SingleOrDefaultAsync(visit => visit.Id == id, cancellationToken);
    }

    public Task<VisitDetailDto?> GetDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        // AsNoTracking ve doğrudan projection, salt-okunur detay isteğinde EF entity yükleme/izleme maliyetini önler.
        return (
                from visit in _context.Visits.AsNoTracking()
                join employee in _context.Employees.AsNoTracking() on visit.EmployeeId equals employee.Id
                join store in _context.Stores.AsNoTracking() on visit.StoreId equals store.Id
                where visit.Id == id
                select new VisitDetailDto(
                    visit.Id,
                    new EmployeeSummaryDto(employee.Id, employee.Name, employee.Email, employee.CountryCode),
                    new StoreSummaryDto(store.Id, store.Name, store.CountryCode, store.Latitude, store.Longitude),
                    visit.PlannedDate,
                    visit.Status,
                    visit.StartedAt,
                    visit.CompletedAt,
                    visit.StartLatitude,
                    visit.StartLongitude,
                    visit.Notes,
                    visit.CreatedAt,
                    visit.Version))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VisitListItemDto>> ListAsync(
        VisitListFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Join, filtre, sıralama ve projection sorgu içinde kalır; IQueryable Infrastructure sınırının dışına çıkmaz.
        var query =
            from visit in _context.Visits.AsNoTracking()
            join employee in _context.Employees.AsNoTracking() on visit.EmployeeId equals employee.Id
            join store in _context.Stores.AsNoTracking() on visit.StoreId equals store.Id
            select new { Visit = visit, Employee = employee, Store = store };

        if (filter.EmployeeId.HasValue)
        {
            query = query.Where(item => item.Visit.EmployeeId == filter.EmployeeId.Value);
        }

        if (filter.StoreId.HasValue)
        {
            query = query.Where(item => item.Visit.StoreId == filter.StoreId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(item => item.Visit.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            query = query.Where(item => item.Store.CountryCode == filter.CountryCode);
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(item => item.Visit.PlannedDate >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(item => item.Visit.PlannedDate <= filter.EndDate.Value);
        }

        if (filter.Status == VisitStatus.Completed)
        {
            query = query
                .OrderByDescending(item => item.Visit.CompletedAt)
                .ThenByDescending(item => item.Visit.Id);
        }
        else if (filter.Status.HasValue)
        {
            query = query
                .OrderByDescending(item => item.Visit.PlannedDate)
                .ThenByDescending(item => item.Visit.Id);
        }
        else
        {
            query = query
                .OrderByDescending(item => item.Visit.Status == VisitStatus.Completed)
                .ThenByDescending(item => item.Visit.Status == VisitStatus.Completed ? item.Visit.CompletedAt : null)
                .ThenByDescending(item => item.Visit.Status == VisitStatus.Completed
                    ? (DateOnly?)null
                    : item.Visit.PlannedDate)
                .ThenByDescending(item => item.Visit.Id);
        }

        return await query
            .Skip(skip)
            .Take(take)
            .Select(item => new VisitListItemDto(
                item.Visit.Id,
                item.Employee.Id,
                item.Employee.Name,
                item.Store.Id,
                item.Store.Name,
                item.Store.CountryCode,
                item.Visit.PlannedDate,
                item.Visit.Status,
                item.Visit.StartedAt,
                item.Visit.CompletedAt,
                item.Visit.Version))
            .ToListAsync(cancellationToken);
    }
}
