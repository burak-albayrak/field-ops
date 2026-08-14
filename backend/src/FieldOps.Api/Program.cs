using FieldOps.Application.Abstractions.Persistence;
using FieldOps.Application.Abstractions.Outbox;
using FieldOps.Application.Visits;
using FieldOps.Api.ExceptionHandling;
using FieldOps.Api.HealthChecks;
using FieldOps.Api.HostedServices;
using FieldOps.Infrastructure.Analytics;
using FieldOps.Infrastructure.Persistence;
using FieldOps.Infrastructure.Persistence.Repositories;
using FieldOps.Infrastructure.Persistence.Outbox;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Connection string ASP.NET Core yapılandırmasından gelir; Docker'da parola kaynak kod yerine yerel .env üzerinden sağlanır.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IStoreRepository, StoreRepository>();
builder.Services.AddScoped<IVisitRepository, VisitRepository>();
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
builder.Services.AddScoped<OutboxRepository>();
builder.Services.AddScoped<OutboxProcessor>();
builder.Services.AddSingleton<OutboxRetryBackoff>();
// Repository'nin Add ile takip ettiği entity ile SaveChanges aynı scoped AppDbContext örneğinde buluşmalıdır.
builder.Services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<IVisitService, VisitService>();

builder.Services.AddOptions<AnalyticsOptions>()
    .Bind(builder.Configuration.GetSection(AnalyticsOptions.SectionName))
    .Validate(options =>
    {
        return Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }, "Analytics:BaseUrl must be an absolute HTTP or HTTPS URI.")
    .Validate(options => options.TimeoutSeconds > 0, "Analytics:TimeoutSeconds must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddOptions<OutboxProcessingOptions>()
    .Bind(builder.Configuration.GetSection(OutboxProcessingOptions.SectionName))
    .Validate(options => options.PollIntervalSeconds > 0, "OutboxProcessing:PollIntervalSeconds must be greater than zero.")
    .Validate(options => options.BatchSize > 0, "OutboxProcessing:BatchSize must be greater than zero.")
    .Validate(options => options.LeaseSeconds > 0, "OutboxProcessing:LeaseSeconds must be greater than zero.")
    .Validate(options => options.BaseRetrySeconds > 0, "OutboxProcessing:BaseRetrySeconds must be greater than zero.")
    .Validate(
        options => options.MaxRetrySeconds >= options.BaseRetrySeconds,
        "OutboxProcessing:MaxRetrySeconds must be greater than or equal to BaseRetrySeconds.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IAnalyticsClient, AnalyticsClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AnalyticsOptions>>().Value;
    var baseUrl = options.BaseUrl.EndsWith("/", StringComparison.Ordinal)
        ? options.BaseUrl
        : $"{options.BaseUrl}/";
    httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
})
    // Redirect otomatik izlenirse ilk 3xx görünmez; case politikası her non-2xx yanıtı retry olarak değerlendirmelidir.
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });

if (builder.Configuration.GetValue<bool>($"{OutboxProcessingOptions.SectionName}:Enabled"))
{
    builder.Services.AddHostedService<OutboxBackgroundService>();
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<PostgreSqlHealthCheck>("postgresql");
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = actionContext =>
    {
        var problemDetails = new ValidationProblemDetails(actionContext.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed"
        };
        problemDetails.Extensions["code"] = "validation_error";

        return new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
    {
        // Tek instance Docker geliştirme ortamında otomatik migration pratiktir; production'da her replica migration çalıştırmamalıdır.
        await context.Database.MigrateAsync();
    }

    if (builder.Configuration.GetValue<bool>("DemoData:Enabled"))
    {
        // Seed, migration sonrası çalışır; boş bir veritabanında tablo oluşmadan sorgu yapmaz.
        await DemoDataSeeder.SeedAsync(context);
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    },
    ResponseWriter = static async (httpContext, healthReport) =>
    {
        // Dış yanıt yalnızca readiness sonucunu açıklar; altyapı exception ve bağlantı ayrıntıları sızdırılmaz.
        var publicStatus = healthReport.Status == HealthStatus.Healthy
            ? "Healthy"
            : "Unhealthy";

        await httpContext.Response.WriteAsJsonAsync(new { status = publicStatus });
    }
});

app.MapControllers();

app.Run();

// Bu partial bildirim, minimal-hosting giriş noktasını çalışma zamanını değiştirmeden WebApplicationFactory testlerine açar.
public partial class Program
{
}
