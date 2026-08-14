namespace FieldOps.IntegrationTests.Infrastructure;

public class WorkerTestSettings
{
    public int PollIntervalSeconds { get; set; } = 1;

    public int BatchSize { get; set; } = 5;

    public int LeaseSeconds { get; set; } = 10;

    public int BaseRetrySeconds { get; set; } = 1;

    public int MaxRetrySeconds { get; set; } = 4;

    public int AnalyticsTimeoutSeconds { get; set; } = 10;
}
