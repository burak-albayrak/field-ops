using FieldOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FieldOps.Api.HealthChecks;

public sealed class PostgreSqlHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PostgreSqlHealthCheck> _logger;

    public PostgreSqlHealthCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<PostgreSqlHealthCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Health check uzun ömürlü olabilir; scoped DbContext her kontrol için kısa bir DI scope içinde çözülür.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Healthy();
            }

            _logger.LogWarning("PostgreSQL health check reported that the database is unreachable.");
            return HealthCheckResult.Unhealthy();
        }
        catch (Exception exception)
        {
            // Exception logda operasyonel incelemeye açık kalır; public health yanıtına taşınmaz.
            _logger.LogWarning(exception, "PostgreSQL health check failed.");
            return HealthCheckResult.Unhealthy();
        }
    }
}
