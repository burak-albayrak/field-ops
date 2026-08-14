namespace FieldOps.Application.Visits.Models;

// Version, istemcinin son gördüğü kaynağı temsil eder; bu nedenle lifecycle değil Application concurrency girdisidir.
public class CancelVisitInput
{
    public CancelVisitInput(long version)
    {
        Version = version;
    }

    public long Version { get; }
}
