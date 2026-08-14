namespace FieldOps.Application.Common.Exceptions;

public class VisitTooFarFromStoreException : Exception
{
    public VisitTooFarFromStoreException(long visitId, double distanceMeters, double maximumDistanceMeters)
        : base("Visit cannot be started because the employee is too far from the store.")
    {
        VisitId = visitId;
        DistanceMeters = distanceMeters;
        MaximumDistanceMeters = maximumDistanceMeters;
    }

    public long VisitId { get; }

    public double DistanceMeters { get; }

    public double MaximumDistanceMeters { get; }
}
