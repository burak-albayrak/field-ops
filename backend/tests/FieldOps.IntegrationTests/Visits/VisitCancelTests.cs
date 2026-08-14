using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.IntegrationTests.Visits;

public sealed class VisitCancelTests : IntegrationTestBase
{
    private static readonly DateTime OriginalStartedAt =
        new(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime OriginalCompletedAt =
        new(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc);

    private const double StartLatitude = 39.9335;
    private const double StartLongitude = 32.8598;

    public VisitCancelTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Cancel_cancels_and_persists_a_planned_visit()
    {
        var visitId = await ArrangeVisitAsync(VisitStatus.Planned);

        using var response = await PostCancelAsync(visitId, 1);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var cancelled = document.RootElement;
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(2, cancelled.GetProperty("version").GetInt64());
        Assert.Equal(JsonValueKind.Null, cancelled.GetProperty("startedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, cancelled.GetProperty("completedAt").ValueKind);

        using var getResponse = await Client.GetAsync($"/api/visits/{visitId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using var getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.Equal("Cancelled", getDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, getDocument.RootElement.GetProperty("version").GetInt64());

        var persisted = await LoadVisitAsync(visitId);
        Assert.Equal(VisitStatus.Cancelled, persisted.Status);
        Assert.Equal(2, persisted.Version);
    }

    [Fact]
    public async Task Cancel_cancels_an_in_progress_visit_and_preserves_start_details()
    {
        var visitId = await ArrangeVisitAsync(VisitStatus.InProgress);

        using var response = await PostCancelAsync(visitId, 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var cancelled = document.RootElement;
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(3, cancelled.GetProperty("version").GetInt64());
        Assert.Equal(OriginalStartedAt, cancelled.GetProperty("startedAt").GetDateTime());
        Assert.Equal(StartLatitude, cancelled.GetProperty("startLatitude").GetDouble());
        Assert.Equal(StartLongitude, cancelled.GetProperty("startLongitude").GetDouble());
        Assert.Equal(JsonValueKind.Null, cancelled.GetProperty("completedAt").ValueKind);

        var persisted = await LoadVisitAsync(visitId);
        Assert.Equal(VisitStatus.Cancelled, persisted.Status);
        Assert.Equal(OriginalStartedAt, persisted.StartedAt);
        Assert.Equal(StartLatitude, persisted.StartLatitude);
        Assert.Equal(StartLongitude, persisted.StartLongitude);
        Assert.Equal(3, persisted.Version);
    }

    [Theory]
    [InlineData(false, 1L)]
    [InlineData(true, 0L)]
    public async Task Cancel_returns_validation_problem_for_missing_or_invalid_version(
        bool includeVersion,
        long version)
    {
        var payload = new Dictionary<string, object?>();

        if (includeVersion)
        {
            payload["version"] = version;
        }

        using var response = await Client.PostAsJsonAsync("/api/visits/999999/cancel", payload);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task Cancel_returns_not_found_for_a_missing_visit()
    {
        using var response = await PostCancelAsync(999999, 1);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "visit_not_found");
    }

    [Fact]
    public async Task Cancel_returns_concurrency_conflict_for_a_stale_in_progress_version()
    {
        var visitId = await ArrangeVisitAsync(VisitStatus.InProgress);

        using var response = await PostCancelAsync(visitId, 1);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "concurrency_conflict");

        var persisted = await LoadVisitAsync(visitId);
        Assert.Equal(VisitStatus.InProgress, persisted.Status);
        Assert.Equal(2, persisted.Version);
    }

    [Fact]
    public async Task Cancel_returns_invalid_status_for_a_completed_visit_with_matching_version()
    {
        var visitId = await ArrangeVisitAsync(VisitStatus.Completed);

        using var response = await PostCancelAsync(visitId, 3);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "invalid_visit_status");

        var persisted = await LoadVisitAsync(visitId);
        Assert.Equal(VisitStatus.Completed, persisted.Status);
        Assert.Equal(OriginalCompletedAt, persisted.CompletedAt);
        Assert.Equal("Original completion", persisted.Notes);
        Assert.Equal(3, persisted.Version);
    }

    [Fact]
    public async Task Cancel_prioritizes_stale_version_and_does_not_overwrite_completed_visit()
    {
        var visitId = await ArrangeVisitAsync(VisitStatus.Completed);

        using var response = await PostCancelAsync(visitId, 2);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "concurrency_conflict");

        var persisted = await LoadVisitAsync(visitId);
        Assert.Equal(VisitStatus.Completed, persisted.Status);
        Assert.Equal(OriginalCompletedAt, persisted.CompletedAt);
        Assert.Equal("Original completion", persisted.Notes);
        Assert.Equal(3, persisted.Version);
    }

    [Fact]
    public async Task Cancel_returns_invalid_status_for_an_already_cancelled_visit()
    {
        var visitId = await ArrangeVisitAsync(VisitStatus.Cancelled);

        using var response = await PostCancelAsync(visitId, 2);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "invalid_visit_status");

        var persisted = await LoadVisitAsync(visitId);
        Assert.Equal(VisitStatus.Cancelled, persisted.Status);
        Assert.Equal(2, persisted.Version);
    }

    private Task<long> ArrangeVisitAsync(VisitStatus status)
    {
        return ExecuteDbContextAsync(async context =>
        {
            var employee = new Employee("Cancel Employee", "cancel@example.com", "TR");
            var store = new Store("Cancel Store", "TR", 39.9334, 32.8597);
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
                    visit.Start(OriginalStartedAt, StartLatitude, StartLongitude);
                    break;
                case VisitStatus.Completed:
                    visit.Start(OriginalStartedAt, StartLatitude, StartLongitude);
                    visit.Complete(OriginalCompletedAt, "Original completion");
                    break;
                case VisitStatus.Cancelled:
                    visit.Cancel();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }

            context.Visits.Add(visit);
            await context.SaveChangesAsync();

            return visit.Id;
        });
    }

    private Task<Visit> LoadVisitAsync(long visitId)
    {
        return ExecuteDbContextAsync(context => context.Visits
            .AsNoTracking()
            .SingleAsync(visit => visit.Id == visitId));
    }

    private Task<HttpResponseMessage> PostCancelAsync(long visitId, long version)
    {
        return Client.PostAsJsonAsync($"/api/visits/{visitId}/cancel", new { Version = version });
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
}
