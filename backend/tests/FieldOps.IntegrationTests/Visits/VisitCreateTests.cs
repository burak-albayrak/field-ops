using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.IntegrationTests.Visits;

public sealed class VisitCreateTests : IntegrationTestBase
{
    private static readonly DateOnly PlannedDate = new(2026, 8, 20);

    public VisitCreateTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Create_returns_created_detail_location_and_persists_the_visit()
    {
        var key = await ArrangeEmployeeAndStoreAsync();

        using var response = await PostVisitAsync(key);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var created = document.RootElement;
        var createdId = created.GetProperty("id").GetInt64();

        Assert.True(createdId > 0);
        Assert.Equal(key.EmployeeId, created.GetProperty("employee").GetProperty("id").GetInt64());
        Assert.Equal("Test Employee", created.GetProperty("employee").GetProperty("name").GetString());
        Assert.Equal(key.StoreId, created.GetProperty("store").GetProperty("id").GetInt64());
        Assert.Equal("Test Store", created.GetProperty("store").GetProperty("name").GetString());
        Assert.Equal("2026-08-20", created.GetProperty("plannedDate").GetString());
        Assert.Equal("Planned", created.GetProperty("status").GetString());
        Assert.Equal(1, created.GetProperty("version").GetInt64());
        Assert.EndsWith("Z", created.GetProperty("createdAt").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("startedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("completedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("startLatitude").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("startLongitude").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("notes").ValueKind);
        Assert.EndsWith($"/api/visits/{createdId}", response.Headers.Location!.OriginalString);

        using var getResponse = await Client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var persistedCount = await CountVisitsAsync(key);
        Assert.Equal(1, persistedCount);
    }

    [Fact]
    public async Task Create_returns_employee_not_found_when_employee_is_missing()
    {
        var storeId = await ArrangeStoreAsync();
        var key = new VisitKey(999999, storeId);

        using var response = await PostVisitAsync(key);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "employee_not_found");
    }

    [Fact]
    public async Task Create_returns_store_not_found_when_store_is_missing()
    {
        var employeeId = await ArrangeEmployeeAsync();
        var key = new VisitKey(employeeId, 999999);

        using var response = await PostVisitAsync(key);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "store_not_found");
    }

    [Theory]
    [InlineData(0L, 1L, true)]
    [InlineData(1L, 0L, true)]
    [InlineData(1L, 1L, false)]
    public async Task Create_returns_validation_problem_for_invalid_transport_input(
        long employeeId,
        long storeId,
        bool includePlannedDate)
    {
        var payload = new Dictionary<string, object?>
        {
            ["employeeId"] = employeeId,
            ["storeId"] = storeId
        };

        if (includePlannedDate)
        {
            payload["plannedDate"] = PlannedDate;
        }

        using var response = await Client.PostAsJsonAsync("/api/visits", payload);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task Create_returns_conflict_when_a_planned_visit_already_exists()
    {
        var key = await ArrangeEmployeeAndStoreAsync();
        await ArrangeVisitAsync(key);

        using var response = await PostVisitAsync(key);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "duplicate_visit");
        Assert.Equal(1, await CountVisitsAsync(key));
    }

    [Fact]
    public async Task Create_returns_conflict_when_an_in_progress_visit_already_exists()
    {
        var key = await ArrangeEmployeeAndStoreAsync();
        await ArrangeVisitAsync(
            key,
            visit => visit.Start(
                new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc),
                39.9335,
                32.8598));

        using var response = await PostVisitAsync(key);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "duplicate_visit");
        Assert.Equal(1, await CountVisitsAsync(key));
    }

    [Fact]
    public async Task Create_succeeds_after_a_cancelled_visit()
    {
        var key = await ArrangeEmployeeAndStoreAsync();
        await ArrangeVisitAsync(key, visit => visit.Cancel());

        using var response = await PostVisitAsync(key);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            [VisitStatus.Cancelled, VisitStatus.Planned],
            await GetVisitStatusesAsync(key));
    }

    [Fact]
    public async Task Create_succeeds_after_a_completed_visit()
    {
        var key = await ArrangeEmployeeAndStoreAsync();
        await ArrangeVisitAsync(
            key,
            visit =>
            {
                visit.Start(
                    new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc),
                    39.9335,
                    32.8598);
                visit.Complete(
                    new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
                    "Completed before replacement.");
            });

        using var response = await PostVisitAsync(key);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            [VisitStatus.Completed, VisitStatus.Planned],
            await GetVisitStatusesAsync(key));
    }

    [Fact]
    public async Task Concurrent_create_allows_one_request_and_rejects_the_other()
    {
        var key = await ArrangeEmployeeAndStoreAsync();
        using var firstRequest = CreatePostRequest(key);
        using var secondRequest = CreatePostRequest(key);

        var responses = await Task.WhenAll(
            Client.SendAsync(firstRequest),
            Client.SendAsync(secondRequest));

        try
        {
            var statuses = responses
                .Select(response => response.StatusCode)
                .OrderBy(status => (int)status)
                .ToArray();

            Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses);

            var conflictResponse = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
            await AssertProblemAsync(conflictResponse, HttpStatusCode.Conflict, "duplicate_visit");
            Assert.Equal(1, await CountActiveVisitsAsync(key));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private Task<VisitKey> ArrangeEmployeeAndStoreAsync()
    {
        return ExecuteDbContextAsync(async context =>
        {
            var employee = new Employee("Test Employee", "employee@example.com", "TR");
            var store = new Store("Test Store", "TR", 39.9334, 32.8597);
            context.Employees.Add(employee);
            context.Stores.Add(store);
            await context.SaveChangesAsync();

            return new VisitKey(employee.Id, store.Id);
        });
    }

    private Task<long> ArrangeEmployeeAsync()
    {
        return ExecuteDbContextAsync(async context =>
        {
            var employee = new Employee("Test Employee", "employee@example.com", "TR");
            context.Employees.Add(employee);
            await context.SaveChangesAsync();

            return employee.Id;
        });
    }

    private Task<long> ArrangeStoreAsync()
    {
        return ExecuteDbContextAsync(async context =>
        {
            var store = new Store("Test Store", "TR", 39.9334, 32.8597);
            context.Stores.Add(store);
            await context.SaveChangesAsync();

            return store.Id;
        });
    }

    private Task ArrangeVisitAsync(VisitKey key, Action<Visit>? configureVisit = null)
    {
        return ExecuteDbContextAsync(async context =>
        {
            var visit = new Visit(
                key.EmployeeId,
                key.StoreId,
                PlannedDate,
                new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc));
            configureVisit?.Invoke(visit);
            context.Visits.Add(visit);
            await context.SaveChangesAsync();
        });
    }

    private Task<int> CountVisitsAsync(VisitKey key)
    {
        return ExecuteDbContextAsync(context => context.Visits.CountAsync(visit =>
            visit.EmployeeId == key.EmployeeId
            && visit.StoreId == key.StoreId
            && visit.PlannedDate == PlannedDate));
    }

    private Task<int> CountActiveVisitsAsync(VisitKey key)
    {
        return ExecuteDbContextAsync(context => context.Visits.CountAsync(visit =>
            visit.EmployeeId == key.EmployeeId
            && visit.StoreId == key.StoreId
            && visit.PlannedDate == PlannedDate
            && (visit.Status == VisitStatus.Planned || visit.Status == VisitStatus.InProgress)));
    }

    private Task<List<VisitStatus>> GetVisitStatusesAsync(VisitKey key)
    {
        return ExecuteDbContextAsync(context => context.Visits
            .Where(visit =>
                visit.EmployeeId == key.EmployeeId
                && visit.StoreId == key.StoreId
                && visit.PlannedDate == PlannedDate)
            .OrderBy(visit => visit.Id)
            .Select(visit => visit.Status)
            .ToListAsync());
    }

    private Task<HttpResponseMessage> PostVisitAsync(VisitKey key)
    {
        return Client.PostAsJsonAsync("/api/visits", new
        {
            key.EmployeeId,
            key.StoreId,
            PlannedDate
        });
    }

    private static HttpRequestMessage CreatePostRequest(VisitKey key)
    {
        return new HttpRequestMessage(HttpMethod.Post, "/api/visits")
        {
            Content = JsonContent.Create(new
            {
                key.EmployeeId,
                key.StoreId,
                PlannedDate
            })
        };
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private sealed class VisitKey
    {
        public VisitKey(long employeeId, long storeId)
        {
            EmployeeId = employeeId;
            StoreId = storeId;
        }

        public long EmployeeId { get; }

        public long StoreId { get; }
    }
}
