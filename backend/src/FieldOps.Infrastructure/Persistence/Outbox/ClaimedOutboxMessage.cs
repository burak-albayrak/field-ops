namespace FieldOps.Infrastructure.Persistence.Outbox;

public class ClaimedOutboxMessage
{
    public ClaimedOutboxMessage(
        long id,
        string type,
        string payload,
        int attemptCount,
        DateTime lockedUntil)
    {
        Id = id;
        Type = type;
        Payload = payload;
        AttemptCount = attemptCount;
        LockedUntil = lockedUntil;
    }

    public long Id { get; }

    public string Type { get; }

    public string Payload { get; }

    public int AttemptCount { get; }

    public DateTime LockedUntil { get; }
}
