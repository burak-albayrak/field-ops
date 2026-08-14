using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.IntegrationTests.Visits;

public sealed class VisitStartTests : IntegrationTestBase
{
    private const double StoreLatitude = 0d;
    private const double StoreLongitude = 0d;
    private const double NearbyLatitude = 0d;
    private const double NearbyLongitude = 0.0009d;
    private const double TooFarLongitude = 0.0019d;

    public VisitStartTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Start_returns_updated_detail_and_persists_a_nearby_visit()
    {
        var arranged = await ArrangeVisitAsync(VisitStatus.Planned);
        var before = DateTime.UtcNow;

        using var response = await PostStartAsync(arranged.VisitId, NearbyLatitude, NearbyLongitude);

        var after = DateTime.UtcNow;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var started = document.RootElement;
        AssertStartedDetail(started, arranged, before, after);

        using var getResponse = await Client.GetAsync($"/api/visits/{arranged.VisitId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        AssertStartedDetail(getDocument.RootElement, arranged, before, after);

        var persisted = await LoadVisitAsync(arranged.VisitId);
        Assert.Equal(VisitStatus.InProgress, persisted.Status);
        Assert.Equal(2, persisted.Version);
        Assert.Equal(NearbyLatitude, persisted.StartLatitude);
        Assert.Equal(NearbyLongitude, persisted.StartLongitude);
    }

    [Theory]
    [InlineData("missing-latitude")]
    [InlineData("missing-longitude")]
    [InlineData("latitude-out-of-range")]
    [InlineData("longitude-out-of-range")]
    public async Task Start_returns_validation_problem_for_invalid_transport_input(string scenario)
    {
        var payload = new Dictionary<string, object?>();

        if (scenario != "missing-latitude")
        {
            payload["latitude"] = scenario == "latitude-out-of-range" ? 91d : NearbyLatitude;
        }

        if (scenario != "missing-longitude")
        {
            payload["longitude"] = scenario == "longitude-out-of-range" ? 181d : NearbyLongitude;
        }

        using var response = await Client.PostAsJsonAsync("/api/visits/999999/start", payload);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task Start_returns_unprocessable_content_and_does_not_persist_a_too_far_attempt()
    {
        var arranged = await ArrangeVisitAsync(VisitStatus.Planned);

        using var response = await PostStartAsync(arranged.VisitId, NearbyLatitude, TooFarLongitude);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "visit_too_far_from_store");

        var persisted = await LoadVisitAsync(arranged.VisitId);
        Assert.Equal(VisitStatus.Planned, persisted.Status);
        Assert.Null(persisted.StartedAt);
        Assert.Null(persisted.StartLatitude);
        Assert.Null(persisted.StartLongitude);
        Assert.Equal(1, persisted.Version);
    }

    [Fact]
    public async Task Start_returns_not_found_for_a_missing_visit()
    {
        using var response = await PostStartAsync(999999, NearbyLatitude, NearbyLongitude);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "visit_not_found");
    }

    [Theory]
    [InlineData(VisitStatus.InProgress)]
    [InlineData(VisitStatus.Completed)]
    [InlineData(VisitStatus.Cancelled)]
    public async Task Start_returns_conflict_and_preserves_an_invalid_state(VisitStatus status)
    {
        var arranged = await ArrangeVisitAsync(status);
        var before = await LoadVisitAsync(arranged.VisitId);

        using var response = await PostStartAsync(arranged.VisitId, NearbyLatitude, NearbyLongitude);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "invalid_visit_status");

        var after = await LoadVisitAsync(arranged.VisitId);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.StartedAt, after.StartedAt);
        Assert.Equal(before.CompletedAt, after.CompletedAt);
        Assert.Equal(before.StartLatitude, after.StartLatitude);
        Assert.Equal(before.StartLongitude, after.StartLongitude);
        Assert.Equal(before.Version, after.Version);
    }

    private Task<VisitArrangement> ArrangeVisitAsync(VisitStatus status)
    {
        return ExecuteDbContextAsync(async context =>
        {
            var employee = new Employee("Start Employee", "start@example.com", "TR");
            var store = new Store("Start Store", "TR", StoreLatitude, StoreLongitude);
            context.Employees.Add(employee);
            context.Stores.Add(store);
            await context.SaveChangesAsync();

            var visit = new Visit(
                employee.Id,
                store.Id,
                new DateOnly(2026, 8, 20),
                new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc));

            switch (status)
            {
                case VisitStatus.Planned:
                    break;
                case VisitStatus.InProgress:
                    visit.Start(
                        new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc),
                        NearbyLatitude,
                        NearbyLongitude);
                    break;
                case VisitStatus.Completed:
                    visit.Start(
                        new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc),
                        NearbyLatitude,
                        NearbyLongitude);
                    visit.Complete(
                        new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
                        "Completed before repeated Start.");
                    break;
                case VisitStatus.Cancelled:
                    visit.Cancel();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }

            context.Visits.Add(visit);
            await context.SaveChangesAsync();

            return new VisitArrangement(visit.Id, employee.Id, store.Id);
        });
    }

    private Task<Visit> LoadVisitAsync(long visitId)
    {
        return ExecuteDbContextAsync(context => context.Visits
            .AsNoTracking()
            .SingleAsync(visit => visit.Id == visitId));
    }

    private Task<HttpResponseMessage> PostStartAsync(long visitId, double latitude, double longitude)
    {
        return Client.PostAsJsonAsync($"/api/visits/{visitId}/start", new
        {
            Latitude = latitude,
            Longitude = longitude
        });
    }

    private static void AssertStartedDetail(
        JsonElement visit,
        VisitArrangement arranged,
        DateTime before,
        DateTime after)
    {
        Assert.Equal(arranged.VisitId, visit.GetProperty("id").GetInt64());
        Assert.Equal("InProgress", visit.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, visit.GetProperty("completedAt").ValueKind);
        Assert.Equal(NearbyLatitude, visit.GetProperty("startLatitude").GetDouble());
        Assert.Equal(NearbyLongitude, visit.GetProperty("startLongitude").GetDouble());
        Assert.Equal(2, visit.GetProperty("version").GetInt64());

        var startedAt = visit.GetProperty("startedAt").GetDateTime();
        Assert.Equal(DateTimeKind.Utc, startedAt.Kind);
        Assert.InRange(startedAt, before.AddSeconds(-1), after.AddSeconds(1));

        Assert.Equal(arranged.EmployeeId, visit.GetProperty("employee").GetProperty("id").GetInt64());
        Assert.Equal("Start Employee", visit.GetProperty("employee").GetProperty("name").GetString());
        Assert.Equal(arranged.StoreId, visit.GetProperty("store").GetProperty("id").GetInt64());
        Assert.Equal("Start Store", visit.GetProperty("store").GetProperty("name").GetString());
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
