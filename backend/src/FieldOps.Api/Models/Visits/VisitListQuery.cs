using System.ComponentModel.DataAnnotations;
using FieldOps.Domain.Enums;

namespace FieldOps.Api.Models.Visits;

public class VisitListQuery
{
    [Range(1, long.MaxValue)]
    public long? EmployeeId { get; init; }

    [Range(1, long.MaxValue)]
    public long? StoreId { get; init; }

    public VisitStatus? Status { get; init; }

    public string? CountryCode { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
