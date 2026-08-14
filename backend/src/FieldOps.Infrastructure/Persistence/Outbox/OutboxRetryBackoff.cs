using Microsoft.Extensions.Options;

namespace FieldOps.Infrastructure.Persistence.Outbox;

public class OutboxRetryBackoff
{
    private readonly OutboxProcessingOptions _options;

    public OutboxRetryBackoff(IOptions<OutboxProcessingOptions> options)
    {
        _options = options.Value;
    }

    public TimeSpan CalculateDelay(int currentAttemptCount)
    {
        if (currentAttemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentAttemptCount));
        }

        long delaySeconds = _options.BaseRetrySeconds;
        var maximumSeconds = (long)_options.MaxRetrySeconds;

        for (var attempt = 0; attempt < currentAttemptCount && delaySeconds < maximumSeconds; attempt++)
        {
            delaySeconds = delaySeconds > maximumSeconds / 2
                ? maximumSeconds
                : delaySeconds * 2;
        }

        return TimeSpan.FromSeconds(Math.Min(delaySeconds, maximumSeconds));
    }
}
