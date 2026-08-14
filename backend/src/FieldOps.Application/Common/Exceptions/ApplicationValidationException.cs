namespace FieldOps.Application.Common.Exceptions;

public class ApplicationValidationException : Exception
{
    public ApplicationValidationException(string field, string error)
        : this(new Dictionary<string, string[]>
        {
            [field] = [error]
        })
    {
    }

    public ApplicationValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
