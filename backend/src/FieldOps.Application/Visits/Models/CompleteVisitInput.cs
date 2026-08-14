namespace FieldOps.Application.Visits.Models;

// Bu model HTTP'den bağımsızdır; tamamlama use case'i yalnızca kullanıcının isteğe bağlı notunu alır.
public class CompleteVisitInput
{
    public CompleteVisitInput(string? notes)
    {
        Notes = notes;
    }

    public string? Notes { get; }
}
