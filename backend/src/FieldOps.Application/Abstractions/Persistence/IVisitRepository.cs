using FieldOps.Application.Visits.Models;
using FieldOps.Domain.Entities;

namespace FieldOps.Application.Abstractions.Persistence;

public interface IVisitRepository
{
    void Add(Visit visit);

    Task<VisitDetailDto?> GetDetailAsync(long id, CancellationToken cancellationToken = default);

    // IQueryable dışarı verilmez; filtreleme ve projeksiyon persistence sınırında tutulur.
    Task<IReadOnlyList<VisitListItemDto>> ListAsync(
        VisitListFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
