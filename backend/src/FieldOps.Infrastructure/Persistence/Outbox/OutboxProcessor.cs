using FieldOps.Infrastructure.Analytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FieldOps.Infrastructure.Persistence.Outbox;

public class OutboxProcessor
{
    private const string UnsupportedTypeError = "Unsupported outbox message type.";
    private const string UnexpectedDeliveryError = "Unexpected Analytics delivery error.";

    private readonly OutboxRepository _repository;
    private readonly IAnalyticsClient _analyticsClient;
    private readonly OutboxRetryBackoff _retryBackoff;
    private readonly OutboxProcessingOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        OutboxRepository repository,
        IAnalyticsClient analyticsClient,
        OutboxRetryBackoff retryBackoff,
        IOptions<OutboxProcessingOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _repository = repository;
        _analyticsClient = analyticsClient;
        _retryBackoff = retryBackoff;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var messages = await _repository.ClaimPendingAsync(
            DateTime.UtcNow,
            TimeSpan.FromSeconds(_options.LeaseSeconds),
            _options.BatchSize,
            cancellationToken);

        _logger.LogDebug("Claimed {Count} Outbox messages.", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                await ProcessMessageAsync(message, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Tek mesajdaki beklenmeyen persistence/program hatası batch'in kalanını engellemez.
                _logger.LogError(exception, "Unexpected error while processing Outbox message {OutboxId}.", message.Id);
            }
        }

        return messages.Count;
    }

    private async Task ProcessMessageAsync(
        ClaimedOutboxMessage message,
        CancellationToken cancellationToken)
    {
        var deliveryResult = await DeliverAsync(message, cancellationToken);

        if (deliveryResult.IsSuccess)
        {
            // HTTP kabulünden sonra DB işaretlemesi kaybolursa mesaj yeniden gönderilebilir; garanti bilerek at-least-once'tur.
            var marked = await _repository.MarkProcessedAsync(
                message.Id,
                message.LockedUntil,
                DateTime.UtcNow,
                cancellationToken);

            if (marked)
            {
                _logger.LogInformation("Delivered Outbox message {OutboxId} to Analytics.", message.Id);
            }
            else
            {
                LogLostLease(message.Id);
            }

            return;
        }

        var failureTime = DateTime.UtcNow;
        var error = deliveryResult.Error ?? UnexpectedDeliveryError;

        if (!deliveryResult.ShouldRetry)
        {
            var permanentlyFailedMarked = await _repository.MarkPermanentlyFailedAsync(
                message.Id,
                message.LockedUntil,
                failureTime,
                error,
                cancellationToken);

            if (permanentlyFailedMarked)
            {
                _logger.LogWarning(
                    "Outbox message {OutboxId} permanently failed after {PreviousAttemptCount} previous failures. Error: {Error}",
                    message.Id,
                    message.AttemptCount,
                    error);
            }
            else
            {
                LogLostLease(message.Id);
            }

            return;
        }

        var nextAttemptAt = failureTime.Add(_retryBackoff.CalculateDelay(message.AttemptCount));
        var failedMarked = await _repository.MarkFailedAsync(
            message.Id,
            message.LockedUntil,
            nextAttemptAt,
            error,
            cancellationToken);

        if (failedMarked)
        {
            _logger.LogWarning(
                "Outbox message {OutboxId} delivery failed after {PreviousAttemptCount} previous failures; next retry at {NextAttemptAt}. Error: {Error}",
                message.Id,
                message.AttemptCount,
                nextAttemptAt,
                error);
        }
        else
        {
            LogLostLease(message.Id);
        }
    }

    private async Task<AnalyticsDeliveryResult> DeliverAsync(
        ClaimedOutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(message.Type, OutboxWriter.VisitCompletedType, StringComparison.Ordinal))
        {
            return AnalyticsDeliveryResult.PermanentFailure(UnsupportedTypeError);
        }

        try
        {
            return await _analyticsClient.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected Analytics error for Outbox message {OutboxId}.", message.Id);
            return AnalyticsDeliveryResult.TransientFailure(UnexpectedDeliveryError);
        }
    }

    private void LogLostLease(long messageId)
    {
        _logger.LogWarning(
            "Outbox message {OutboxId} lease was no longer owned when recording delivery result.",
            messageId);
    }
}
