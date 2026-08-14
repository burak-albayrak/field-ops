using System.ComponentModel.DataAnnotations;

namespace FieldOps.Api.Models.Visits;

public class CancelVisitRequest
{
    // Nullable alan, eksik JSON özelliğinin sessizce 0'a dönüşmesini önler.
    [Required]
    [Range(1, long.MaxValue)]
    public long? Version { get; init; }
}
