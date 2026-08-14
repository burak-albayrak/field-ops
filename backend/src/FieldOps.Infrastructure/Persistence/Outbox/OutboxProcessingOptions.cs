namespace FieldOps.Infrastructure.Persistence.Outbox;

public class OutboxProcessingOptions
{
    public const string SectionName = "OutboxProcessing";

    public bool Enabled { get; set; }

    public int PollIntervalSeconds { get; set; }

    // Batch küçük tutulur; lease süresi, sıralı HTTP teslimatlarının beklenen toplam üst süresini aşmalıdır.
    public int BatchSize { get; set; }

    public int LeaseSeconds { get; set; }

    public int BaseRetrySeconds { get; set; }

    public int MaxRetrySeconds { get; set; }
}
