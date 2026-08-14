using FieldOps.Application.Visits.Models;

namespace FieldOps.Application.Visits;

public interface IVisitService
{
    Task<VisitDetailDto> CreateAsync(
        CreateVisitInput input,
        CancellationToken cancellationToken = default);

    Task<VisitDetailDto> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<VisitListResult> ListAsync(
        VisitListFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
