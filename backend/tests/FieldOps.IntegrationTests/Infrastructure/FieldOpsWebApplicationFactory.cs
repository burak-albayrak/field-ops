using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FieldOps.IntegrationTests.Infrastructure;

public sealed class FieldOpsWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly Dictionary<string, string?> _originalEnvironmentValues = new();

    public FieldOpsWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;

        // Minimal Program bağlantı dizesini host test kancalarından önce okuduğu için bu değerler yalnızca test işlemi süresince erken sağlanır.
        SetEnvironmentValue("ASPNETCORE_ENVIRONMENT", "Testing");
        SetEnvironmentValue("ConnectionStrings__DefaultConnection", _connectionString);
        SetEnvironmentValue("Database__ApplyMigrations", "false");
        SetEnvironmentValue("DemoData__Enabled", "false");
        SetEnvironmentValue("OutboxProcessing__Enabled", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            // Testler gerçek PostgreSQL kullanır; sadece bağlantı hedefi geçici Testcontainer veritabanına yönlendirilir.
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                // Development bootstrap verisi test senaryolarına gizli bağımlılık oluşturmamalıdır.
                ["Database:ApplyMigrations"] = "false",
                ["DemoData:Enabled"] = "false",
                // Normal entegrasyon hostu pending Outbox satırlarıyla yarışmamalı veya gerçek Analytics ağına çıkmamalıdır.
                ["OutboxProcessing:Enabled"] = "false"
            });
        });
    }

    public void RestoreOriginalEnvironment()
    {
        foreach (var (key, value) in _originalEnvironmentValues)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private void SetEnvironmentValue(string key, string value)
    {
        _originalEnvironmentValues[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }
}
