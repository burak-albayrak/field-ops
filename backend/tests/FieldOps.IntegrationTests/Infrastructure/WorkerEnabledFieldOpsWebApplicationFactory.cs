using System.Globalization;
using FieldOps.Infrastructure.Analytics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FieldOps.IntegrationTests.Infrastructure;

public sealed class WorkerEnabledFieldOpsWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string AnalyticsBaseUrl = "https://analytics.test/";

    private readonly string _connectionString;
    private readonly TestAnalyticsTransport _transport;
    private readonly WorkerTestSettings _settings;
    private readonly Dictionary<string, string?> _originalEnvironmentValues = new();

    public WorkerEnabledFieldOpsWebApplicationFactory(
        string connectionString,
        TestAnalyticsTransport transport,
        WorkerTestSettings? settings = null)
    {
        _connectionString = connectionString;
        _transport = transport;
        _settings = settings ?? new WorkerTestSettings();

        // Minimal Program worker kaydını host test kancalarından önce seçtiği için bu değerler yalnızca host kurulurken sağlanır.
        SetEnvironmentValue("ASPNETCORE_ENVIRONMENT", "Testing");
        SetEnvironmentValue("ConnectionStrings__DefaultConnection", _connectionString);
        SetEnvironmentValue("Database__ApplyMigrations", "false");
        SetEnvironmentValue("DemoData__Enabled", "false");
        SetEnvironmentValue("Logging__LogLevel__Default", "Warning");
        SetEnvironmentValue("Analytics__BaseUrl", AnalyticsBaseUrl);
        SetEnvironmentValue("Analytics__TimeoutSeconds", Format(_settings.AnalyticsTimeoutSeconds));
        SetEnvironmentValue("OutboxProcessing__Enabled", "true");
        SetEnvironmentValue("OutboxProcessing__PollIntervalSeconds", Format(_settings.PollIntervalSeconds));
        SetEnvironmentValue("OutboxProcessing__BatchSize", Format(_settings.BatchSize));
        SetEnvironmentValue("OutboxProcessing__LeaseSeconds", Format(_settings.LeaseSeconds));
        SetEnvironmentValue("OutboxProcessing__BaseRetrySeconds", Format(_settings.BaseRetrySeconds));
        SetEnvironmentValue("OutboxProcessing__MaxRetrySeconds", Format(_settings.MaxRetrySeconds));
    }

    public HttpClient StartClient()
    {
        try
        {
            return CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        }
        finally
        {
            // Host kendi immutable configuration snapshot'ını aldıktan sonra process-global test değerleri hemen geri konur.
            RestoreOriginalEnvironment();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(CreateConfiguration());
        });
        builder.ConfigureServices(services =>
        {
            // Production AnalyticsClient korunur; yalnızca onun son HttpMessageHandler transport'u test double olur.
            services.AddHttpClient<IAnalyticsClient, AnalyticsClient>()
                .ConfigurePrimaryHttpMessageHandler(_transport.CreateHandler);
        });
    }

    private Dictionary<string, string?> CreateConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _connectionString,
            ["Database:ApplyMigrations"] = "false",
            ["DemoData:Enabled"] = "false",
            ["Logging:LogLevel:Default"] = "Warning",
            ["Analytics:BaseUrl"] = AnalyticsBaseUrl,
            ["Analytics:TimeoutSeconds"] = Format(_settings.AnalyticsTimeoutSeconds),
            ["OutboxProcessing:Enabled"] = "true",
            ["OutboxProcessing:PollIntervalSeconds"] = Format(_settings.PollIntervalSeconds),
            ["OutboxProcessing:BatchSize"] = Format(_settings.BatchSize),
            ["OutboxProcessing:LeaseSeconds"] = Format(_settings.LeaseSeconds),
            ["OutboxProcessing:BaseRetrySeconds"] = Format(_settings.BaseRetrySeconds),
            ["OutboxProcessing:MaxRetrySeconds"] = Format(_settings.MaxRetrySeconds)
        };
    }

    private void RestoreOriginalEnvironment()
    {
        foreach (var (key, value) in _originalEnvironmentValues)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        _originalEnvironmentValues.Clear();
    }

    private void SetEnvironmentValue(string key, string value)
    {
        _originalEnvironmentValues[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    private static string Format(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
