namespace FieldOps.Api.Models.Visits;

public class CompleteVisitRequest
{
    // Case notu zorunlu kılmaz; null veya eksik notes, geçerli bir tamamlama isteğidir.
    public string? Notes { get; init; }
}
