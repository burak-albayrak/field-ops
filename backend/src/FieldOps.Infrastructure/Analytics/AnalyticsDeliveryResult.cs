namespace FieldOps.Infrastructure.Analytics;

public class AnalyticsDeliveryResult
{
    private AnalyticsDeliveryResult(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public string? Error { get; }

    public static AnalyticsDeliveryResult Success()
    {
        return new AnalyticsDeliveryResult(true, null);
    }

    public static AnalyticsDeliveryResult Failure(string error)
    {
        return new AnalyticsDeliveryResult(false, error);
    }
}
