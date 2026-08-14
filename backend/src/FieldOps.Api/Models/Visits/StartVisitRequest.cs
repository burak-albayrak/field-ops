using System.ComponentModel.DataAnnotations;

namespace FieldOps.Api.Models.Visits;

public class StartVisitRequest
{
    // Nullable alanlar, eksik JSON özelliklerinin geçerli bir koordinat olan 0'a sessizce dönüşmesini önler.
    [Required]
    [Range(-90d, 90d)]
    public double? Latitude { get; init; }

    [Required]
    [Range(-180d, 180d)]
    public double? Longitude { get; init; }
}
