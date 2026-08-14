namespace FieldOps.Application.Common.Exceptions;

public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(Exception? innerException = null)
        : base("The resource changed after it was read and the attempted mutation could not be safely persisted.", innerException)
    {
    }
}
