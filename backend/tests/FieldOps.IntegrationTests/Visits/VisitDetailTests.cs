using System.Net;
using System.Text.Json;
using FieldOps.Domain.Entities;
using FieldOps.IntegrationTests.Infrastructure;

namespace FieldOps.IntegrationTests.Visits;

public sealed class VisitDetailTests : IntegrationTestBase
{
    public VisitDetailTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetById_returns_an_existing_planned_visit()
    {
        var createdAt = new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc);
        var arranged = await ArrangeVisitAsync(createdAt);

        var response = await Client.GetAsync($"/api/visits/{arranged.VisitId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var visit = document.RootElement;

        Assert.Equal(arranged.VisitId, visit.GetProperty("id").GetInt64());
        Assert.Equal("2026-08-14", visit.GetProperty("plannedDate").GetString());
        Assert.Equal("Planned", visit.GetProperty("status").GetString());
        Assert.Equal(1, visit.GetProperty("version").GetInt64());
        Assert.Equal(createdAt, visit.GetProperty("createdAt").GetDateTime());
        Assert.Equal(JsonValueKind.Null, visit.GetProperty("startedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, visit.GetProperty("completedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, visit.GetProperty("startLatitude").ValueKind);
        Assert.Equal(JsonValueKind.Null, visit.GetProperty("startLongitude").ValueKind);
        Assert.Equal(JsonValueKind.Null, visit.GetProperty("notes").ValueKind);

        var employee = visit.GetProperty("employee");
        Assert.Equal(arranged.EmployeeId, employee.GetProperty("id").GetInt64());
        Assert.Equal("Ayşe", employee.GetProperty("name").GetString());
        Assert.Equal("ayse@example.com", employee.GetProperty("email").GetString());
        Assert.Equal("TR", employee.GetProperty("countryCode").GetString());

        var store = visit.GetProperty("store");
        Assert.Equal(arranged.StoreId, store.GetProperty("id").GetInt64());
        Assert.Equal("Ankara Store", store.GetProperty("name").GetString());
        Assert.Equal("TR", store.GetProperty("countryCode").GetString());
        Assert.Equal(39.9334, store.GetProperty("latitude").GetDouble());
        Assert.Equal(32.8597, store.GetProperty("longitude").GetDouble());
    }

    [Fact]
    public async Task GetById_returns_an_existing_completed_visit()
    {
        var arranged = await ArrangeVisitAsync(
            new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc),
            visit =>
            {
                visit.Start(new DateTime(2026, 8, 14, 9, 0, 0, DateTimeKind.Utc), 39.9335, 32.8598);
                visit.Complete(new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc), "Completed successfully.");
            });

        var response = await Client.GetAsync($"/api/visits/{arranged.VisitId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var visit = document.RootElement;

        Assert.Equal("Completed", visit.GetProperty("status").GetString());
        Assert.Equal(3, visit.GetProperty("version").GetInt64());
        Assert.Equal("Completed successfully.", visit.GetProperty("notes").GetString());
        Assert.Equal(39.9335, visit.GetProperty("startLatitude").GetDouble());
        Assert.Equal(32.8598, visit.GetProperty("startLongitude").GetDouble());
        Assert.NotEqual(JsonValueKind.Null, visit.GetProperty("startedAt").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, visit.GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public async Task GetById_returns_problem_details_for_a_missing_visit_without_internal_details()
    {
        var response = await Client.GetAsync("/api/visits/999999");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("stack", body.ToLowerInvariant());
        Assert.DoesNotContain("exception", body.ToLowerInvariant());

        using var document = JsonDocument.Parse(body);
        var problem = document.RootElement;
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Equal("visit_not_found", problem.GetProperty("code").GetString());
    }

    private Task<VisitArrangement> ArrangeVisitAsync(DateTime createdAt, Action<Visit>? configureVisit = null)
    {
        return ExecuteDbContextAsync(async context =>
        {
            var employee = new Employee("Ayşe", "ayse@example.com", "TR");
            var store = new Store("Ankara Store", "TR", 39.9334, 32.8597);
            context.Employees.Add(employee);
            context.Stores.Add(store);
            await context.SaveChangesAsync();

            var visit = new Visit(employee.Id, store.Id, new DateOnly(2026, 8, 14), createdAt);
            configureVisit?.Invoke(visit);
            context.Visits.Add(visit);
            await context.SaveChangesAsync();

            return new VisitArrangement(visit.Id, employee.Id, store.Id);
        });
    }

    private sealed class VisitArrangement
    {
        public VisitArrangement(long visitId, long employeeId, long storeId)
        {
            VisitId = visitId;
            EmployeeId = employeeId;
            StoreId = storeId;
        }

        public long VisitId { get; }

        public long EmployeeId { get; }

        public long StoreId { get; }
    }
}
