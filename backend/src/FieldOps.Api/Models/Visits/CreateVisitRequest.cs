using System.ComponentModel.DataAnnotations;

namespace FieldOps.Api.Models.Visits;

public class CreateVisitRequest
{
    [Range(1, long.MaxValue)]
    public long EmployeeId { get; init; }

    [Range(1, long.MaxValue)]
    public long StoreId { get; init; }

    // Nullable taşıma alanı, eksik JSON değerinin DateOnly.MinValue'a sessizce dönüşmesini önler.
    [Required]
    public DateOnly? PlannedDate { get; init; }
}
