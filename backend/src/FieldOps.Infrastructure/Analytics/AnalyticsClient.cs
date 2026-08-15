using System.Net;
using System.Text;
using FieldOps.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Logging;

namespace FieldOps.Infrastructure.Analytics;

public class AnalyticsClient : IAnalyticsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnalyticsClient> _logger;

    public AnalyticsClient(HttpClient httpClient, ILogger<AnalyticsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AnalyticsDeliveryResult> SendAsync(
        ClaimedOutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "events")
        {
            // Durable payload tekrar serialize edilmez; Analytics'e Outbox'taki immutable JSON snapshot gönderilir.
            Content = new StringContent(message.Payload, Encoding.UTF8, "application/json")
        };

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return AnalyticsDeliveryResult.Success();
            }

            // Yalnızca geçici HTTP durumları retry edilir; normal 4xx yanıtları aynı isteği tekrarlamakla düzelmez.
            var error = $"Analytics returned HTTP {(int)response.StatusCode}.";
            _logger.LogWarning("Analytics delivery returned HTTP {StatusCode} for Outbox message {OutboxId}.",
                (int)response.StatusCode,
                message.Id);
            return IsTransient(response.StatusCode)
                ? AnalyticsDeliveryResult.TransientFailure(error)
                : AnalyticsDeliveryResult.PermanentFailure(error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Analytics delivery timed out for Outbox message {OutboxId}.", message.Id);
            return AnalyticsDeliveryResult.TransientFailure("Analytics request timed out.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Analytics request failed for Outbox message {OutboxId}.", message.Id);
            return AnalyticsDeliveryResult.TransientFailure("Analytics request failed.");
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;

        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || numericStatusCode is >= 500 and <= 599;
    }
}
