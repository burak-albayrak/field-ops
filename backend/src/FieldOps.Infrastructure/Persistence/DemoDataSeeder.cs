using FieldOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Infrastructure.Persistence;

// Demo seed, AppDbContext kullanan development bootstrap davranışıdır; bu nedenle Domain yerine Infrastructure'da kalır.
public static class DemoDataSeeder
{
    private const string AyseEmail = "ayse.yilmaz@fieldops.demo";
    private const string MaxEmail = "max.mueller@fieldops.demo";

    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var ayse = await GetOrCreateEmployeeAsync(
            context,
            "Ayşe Yılmaz",
            AyseEmail,
            "TR",
            cancellationToken);
        var max = await GetOrCreateEmployeeAsync(
            context,
            "Max Müller",
            MaxEmail,
            "DE",
            cancellationToken);

        var ankara = await GetOrCreateStoreAsync(
            context,
            "Ankara Çankaya Store",
            "TR",
            39.9334,
            32.8597,
            cancellationToken);
        await GetOrCreateStoreAsync(
            context,
            "London Central Store",
            "UK",
            51.5074,
            -0.1278,
            cancellationToken);
        await GetOrCreateStoreAsync(
            context,
            "Dubai Downtown Store",
            "AE",
            25.2048,
            55.2708,
            cancellationToken);
        var berlin = await GetOrCreateStoreAsync(
            context,
            "Berlin Mitte Store",
            "DE",
            52.5200,
            13.4050,
            cancellationToken);

        // ID'ler veritabanınca üretildiği için çalışan ve mağazaları önce kaydediyoruz.
        await context.SaveChangesAsync(cancellationToken);

        if (!await DemoVisitExistsAsync(context, ayse.Id, ankara.Id, new DateOnly(2026, 8, 14), cancellationToken))
        {
            context.Visits.Add(new Visit(
                ayse.Id,
                ankara.Id,
                new DateOnly(2026, 8, 14),
                Utc(2026, 8, 10, 9, 0)));
        }

        if (!await DemoVisitExistsAsync(context, ayse.Id, ankara.Id, new DateOnly(2026, 8, 13), cancellationToken))
        {
            var visit = new Visit(
                ayse.Id,
                ankara.Id,
                new DateOnly(2026, 8, 13),
                Utc(2026, 8, 10, 9, 0));

            // Demo kayıtları gerçek geçiş kurallarını da örneklemelidir; durum alanları doğrudan atanmaz.
            visit.Start(Utc(2026, 8, 13, 8, 0), 39.9334, 32.8597);
            context.Visits.Add(visit);
        }

        if (!await DemoVisitExistsAsync(context, ayse.Id, ankara.Id, new DateOnly(2026, 8, 12), cancellationToken))
        {
            var visit = new Visit(
                ayse.Id,
                ankara.Id,
                new DateOnly(2026, 8, 12),
                Utc(2026, 8, 10, 9, 0));

            visit.Start(Utc(2026, 8, 12, 8, 0), 39.9334, 32.8597);
            visit.Complete(Utc(2026, 8, 12, 9, 0), "Demo visit completed successfully.");
            context.Visits.Add(visit);
        }

        if (!await DemoVisitExistsAsync(context, max.Id, berlin.Id, new DateOnly(2026, 8, 13), cancellationToken))
        {
            var visit = new Visit(
                max.Id,
                berlin.Id,
                new DateOnly(2026, 8, 13),
                Utc(2026, 8, 10, 9, 0));

            visit.Cancel();
            context.Visits.Add(visit);
        }

        // Demo kimliği status'u bilerek içermez: normal kullanım yaşam döngüsünü ilerletebilir;
        // restart, kullanıcıya görünen yeni durumu korumalı ve ilk seed durumunu yeniden oluşturmamalıdır.
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Employee> GetOrCreateEmployeeAsync(
        AppDbContext context,
        string name,
        string email,
        string countryCode,
        CancellationToken cancellationToken)
    {
        var employee = await context.Employees.SingleOrDefaultAsync(
            candidate => candidate.Email == email,
            cancellationToken);

        if (employee is not null)
        {
            return employee;
        }

        employee = new Employee(name, email, countryCode);
        context.Employees.Add(employee);
        return employee;
    }

    private static async Task<Store> GetOrCreateStoreAsync(
        AppDbContext context,
        string name,
        string countryCode,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var store = await context.Stores.SingleOrDefaultAsync(
            candidate => candidate.Name == name && candidate.CountryCode == countryCode,
            cancellationToken);

        if (store is not null)
        {
            return store;
        }

        store = new Store(name, countryCode, latitude, longitude);
        context.Stores.Add(store);
        return store;
    }

    private static Task<bool> DemoVisitExistsAsync(
        AppDbContext context,
        long employeeId,
        long storeId,
        DateOnly plannedDate,
        CancellationToken cancellationToken)
    {
        return context.Visits.AnyAsync(
            visit => visit.EmployeeId == employeeId &&
                     visit.StoreId == storeId &&
                     visit.PlannedDate == plannedDate,
            cancellationToken);
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }
}
