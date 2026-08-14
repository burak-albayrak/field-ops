using FieldOps.Application.Abstractions.Persistence;
using FieldOps.Application.Visits;
using FieldOps.Api.ExceptionHandling;
using FieldOps.Infrastructure.Persistence;
using FieldOps.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

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
// Repository'nin Add ile takip ettiği entity ile SaveChanges aynı scoped AppDbContext örneğinde buluşmalıdır.
builder.Services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<IVisitService, VisitService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
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

app.MapControllers();

app.Run();

// Bu partial bildirim, minimal-hosting giriş noktasını çalışma zamanını değiştirmeden WebApplicationFactory testlerine açar.
public partial class Program
{
}
