namespace FieldOps.Application.Common.Exceptions;

public class VisitNotFoundException : Exception
{
    public VisitNotFoundException(long visitId)
        : base($"Visit {visitId} was not found.")
    {
        VisitId = visitId;
    }

    public long VisitId { get; }
}
