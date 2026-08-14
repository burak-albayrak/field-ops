namespace FieldOps.Infrastructure.Persistence.Outbox;

public class OutboxMessage
{
    public long Id { get; private set; }

    public string Type { get; private set; }

    public string Payload { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ProcessedAt { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTime NextAttemptAt { get; private set; }

    public DateTime? LockedUntil { get; private set; }

    public string? LastError { get; private set; }

    public OutboxMessage(string type, string payload, DateTime createdAt)
    {
        Type = type;
        Payload = payload;
        CreatedAt = createdAt;
        ProcessedAt = null;
        AttemptCount = 0;
        NextAttemptAt = createdAt;
        LockedUntil = null;
        LastError = null;
    }
}
