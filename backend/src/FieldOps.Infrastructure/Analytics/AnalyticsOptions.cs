namespace FieldOps.Infrastructure.Analytics;

public class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    public string BaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; }
}
