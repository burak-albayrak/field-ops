using FieldOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FieldOps.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("fieldops_integration")
        .WithUsername("fieldops_test")
        .WithPassword("fieldops_test_password")
        .Build();

    public FieldOpsWebApplicationFactory Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new FieldOpsWebApplicationFactory(_postgres.GetConnectionString());
        // Test hostu Development seed/migration davranışını çalıştırmaz; migration aşağıda açıkça test kurulumu olarak uygulanır.
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // EnsureCreated yerine gerçek migration kullanmak, test edilen şemayı production/development şemasıyla aynı tutar.
        await context.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Paylaşılan container'da test izolasyonu için yalnızca uygulama verisini temizleriz; migration geçmişi korunur.
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE visits, stores, employees RESTART IDENTITY CASCADE;");
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        Factory?.Dispose();
        Factory?.RestoreOriginalEnvironment();
        await _postgres.DisposeAsync();
    }
}
