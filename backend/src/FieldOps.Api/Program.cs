using FieldOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Connection string ASP.NET Core yapılandırmasından gelir; Docker'da parola kaynak kod yerine yerel .env üzerinden sağlanır.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

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
