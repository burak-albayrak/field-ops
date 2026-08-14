using FieldOps.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Options;

namespace FieldOps.Api.HostedServices;

public class OutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxProcessingOptions _options;
    private readonly ILogger<OutboxBackgroundService> _logger;

    public OutboxBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxProcessingOptions> options,
        ILogger<OutboxBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox background worker started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var claimedCount = 0;

                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                    claimedCount = await processor.ProcessBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Unexpected Outbox processing loop error.");
                }

                if (claimedCount > 0)
                {
                    // Backlog varken beklemeden sonraki kısa scope/batch'e geçilir.
                    continue;
                }

                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host kapanışı normal kontrol akışıdır; başarısız teslimat olarak kaydedilmez.
        }
        finally
        {
            _logger.LogInformation("Outbox background worker stopped.");
        }
    }
}
