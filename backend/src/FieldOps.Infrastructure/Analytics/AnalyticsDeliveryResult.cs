namespace FieldOps.Infrastructure.Analytics;

public class AnalyticsDeliveryResult
{
    private AnalyticsDeliveryResult(bool isSuccess, bool shouldRetry, string? error)
    {
        IsSuccess = isSuccess;
        ShouldRetry = shouldRetry;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool ShouldRetry { get; }

    public string? Error { get; }

    public static AnalyticsDeliveryResult Success()
    {
        return new AnalyticsDeliveryResult(true, false, null);
    }

    public static AnalyticsDeliveryResult TransientFailure(string error)
    {
        return new AnalyticsDeliveryResult(false, true, error);
    }

    public static AnalyticsDeliveryResult PermanentFailure(string error)
    {
        return new AnalyticsDeliveryResult(false, false, error);
    }
}
