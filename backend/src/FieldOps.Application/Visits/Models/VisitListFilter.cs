using FieldOps.Domain.Enums;

namespace FieldOps.Application.Visits.Models;

public class VisitListFilter
{
    public long? EmployeeId { get; init; }

    public long? StoreId { get; init; }

    public VisitStatus? Status { get; init; }

    public string? CountryCode { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }
}
