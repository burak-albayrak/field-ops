using FieldOps.Infrastructure.Persistence.Outbox;

namespace FieldOps.Infrastructure.Analytics;

public interface IAnalyticsClient
{
    Task<AnalyticsDeliveryResult> SendAsync(
        ClaimedOutboxMessage message,
        CancellationToken cancellationToken = default);
}
