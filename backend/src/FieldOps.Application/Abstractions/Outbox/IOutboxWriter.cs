namespace FieldOps.Application.Abstractions.Outbox;

public interface IOutboxWriter
{
    void AddVisitCompleted(
        long visitId,
        long employeeId,
        long storeId,
        DateTime completedAt);
}
