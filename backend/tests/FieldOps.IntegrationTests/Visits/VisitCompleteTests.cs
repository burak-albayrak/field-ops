using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.IntegrationTests.Infrastructure;
using FieldOps.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.IntegrationTests.Visits;

public sealed class VisitCompleteTests : IntegrationTestBase
{
    private static readonly DateTime OriginalStartedAt =
        new(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc);

    private const double StartLatitude = 39.9335;
    private const double StartLongitude = 32.8598;

    public VisitCompleteTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Complete_returns_updated_detail_and_persists_an_in_progress_visit()
    {
        var visitId = await ArrangeVisitAsync(VisitStatus.InProgress);
        var before = DateTime.UtcNow;

        using var response = await PostCompleteAsync(visitId, "Completed from integration test.");

        var after = DateTime.UtcNow;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertCompletedDetail(
            document.RootElement,
            "Completed from integration test.",
            before,
            after);

        using var getResponse = await Client.GetAsync($"/api/visits/{visitId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        AssertCompletedDetail(
            getDocument.RootElement,
            "Completed from integration test.",
            before,
            after);

        var persisted = await LoadVisitAsync(visitId);
        Assert.Equal(VisitStatus.Completed, persisted.Status);
        Assert.Equal("Completed from integration test.", persisted.Notes);
        Assert.Equal(3, persisted.Version);

        var outbox = Assert.Single(await LoadOutboxMessagesAsync());
        Assert.Equal("VisitCompleted", outbox.Type);
        Assert.Equal(persisted.CompletedAt, outbox.CreatedAt);
        Assert.Equal(outbox.CreatedAt, outbox.NextAttemptAt);
        Assert.Null(outbox.ProcessedAt);
        Assert.Equal(0, outbox.AttemptCount);
        Assert.Null(outbox.LockedUntil);
        Assert.Null(outbox.LastError);

        using var payload = JsonDocument.Parse(outbox.Payload);
        var payloadRoot = payload.RootElement;
        Assert.Equal(5, payloadRoot.EnumerateObject().Count());
        Assert.Equal("VisitCompleted", payloadRoot.GetProperty("type").GetString());
        Assert.Equal(persisted.Id, payloadRoot.GetProperty("visitId").GetInt64());
        Assert.Equal(persisted.EmployeeId, payloadRoot.GetProperty("employeeId").GetInt64());
        Assert.Equal(persisted.StoreId, payloadRoot.GetProperty("storeId").GetInt64());
        Assert.Equal(persisted.CompletedAt, payloadRoot.GetProperty("completedAt").GetDateTime());
    }

    [Fact]
    public async Task Complete_retry_returns_original_completion_without_mutating_it_again()
    {
        var visitId = await ArrangeVisitAsync(VisitStatus.InProgress);

        using var firstResponse = await PostCompleteAsync(visitId, "Original completion.");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        using var firstDocument = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var firstCompletedAt = firstDocument.RootElement.GetProperty("completedAt").GetDateTime();
        var firstVersion = firstDocument.RootElement.GetProperty("version").GetInt64();

        using var retryResponse = await PostCompleteAsync(visitId, "Different retry notes.");
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        using var retryDocument = JsonDocument.Parse(await retryResponse.Content.ReadAsStringAsync());
        var retry = retryDocument.RootElement;

        Assert.Equal("Completed", retry.GetProperty("status").GetString());
        Assert.Equal(firstCompletedAt, retry.GetProperty("completedAt").GetDateTime());
        Assert.Equal("Original completion.", retry.GetProperty("notes").GetString());
        Assert.Equal(firstVersion, retry.GetProperty("version").GetInt64());

        using var getResponse = await Client.GetAsync($"/api/visits/{visitId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using var getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var persistedDetail = getDocument.RootElement;
        Assert.Equal(firstCompletedAt, persistedDetail.GetProperty("completedAt").GetDateTime());
        Assert.Equal("Original completion.", persistedDetail.GetProperty("notes").GetString());
        Assert.Equal(firstVersion, persistedDetail.GetProperty("version").GetInt64());

        var persisted = await LoadVisitAsync(visitId);
        Assert.Equal(firstCompletedAt, persisted.CompletedAt);
        Assert.Equal("Original completion.", persisted.Notes);
        Assert.Equal(firstVersion, persisted.Version);

        var outbox = Assert.Single(await LoadOutboxMessagesAsync());
        using var payload = JsonDocument.Parse(outbox.Payload);
        Assert.Equal(firstCompletedAt, payload.RootElement.GetProperty("completedAt").GetDateTime());
    }

    [Fact]
    public async Task Complete_accepts_an_omitted_notes_property()
    {
        var visitId = await ArrangeVisitAsync(VisitStatus.InProgress);

        using var response = await Client.PostAsJsonAsync($"/api/visits/{visitId}/complete", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("notes").ValueKind);

        var persisted = await LoadVisitAsync(visitId);
        Assert.Equal(VisitStatus.Completed, persisted.Status);
        Assert.Null(persisted.Notes);
    }

    [Fact]
    public async Task Complete_returns_not_found_for_a_missing_visit()
    {
        using var response = await PostCompleteAsync(999999, "Notes");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "visit_not_found");
        Assert.Empty(await LoadOutboxMessagesAsync());
    }

    [Theory]
    [InlineData(VisitStatus.Planned, 1L)]
    [InlineData(VisitStatus.Cancelled, 2L)]
    public async Task Complete_returns_conflict_and_preserves_an_invalid_state(
        VisitStatus status,
        long expectedVersion)
    {
        var visitId = await ArrangeVisitAsync(status);

        using var response = await PostCompleteAsync(visitId, "Rejected notes");

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "invalid_visit_status");

        var persisted = await LoadVisitAsync(visitId);
        Assert.Equal(status, persisted.Status);
        Assert.Null(persisted.CompletedAt);
        Assert.Null(persisted.Notes);
        Assert.Equal(expectedVersion, persisted.Version);
        Assert.Empty(await LoadOutboxMessagesAsync());
    }

    private Task<long> ArrangeVisitAsync(VisitStatus status)
    {
        return ExecuteDbContextAsync(async context =>
        {
            var employee = new Employee("Complete Employee", "complete@example.com", "TR");
            var store = new Store("Complete Store", "TR", 39.9334, 32.8597);
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

    private Task<List<OutboxMessage>> LoadOutboxMessagesAsync()
    {
        return ExecuteDbContextAsync(context => context.OutboxMessages
            .AsNoTracking()
            .OrderBy(message => message.Id)
            .ToListAsync());
    }

    private Task<HttpResponseMessage> PostCompleteAsync(long visitId, string? notes)
    {
        return Client.PostAsJsonAsync($"/api/visits/{visitId}/complete", new { Notes = notes });
    }

    private static void AssertCompletedDetail(
        JsonElement visit,
        string expectedNotes,
        DateTime before,
        DateTime after)
    {
        Assert.Equal("Completed", visit.GetProperty("status").GetString());
        Assert.Equal(OriginalStartedAt, visit.GetProperty("startedAt").GetDateTime());
        Assert.Equal(StartLatitude, visit.GetProperty("startLatitude").GetDouble());
        Assert.Equal(StartLongitude, visit.GetProperty("startLongitude").GetDouble());
        Assert.Equal(expectedNotes, visit.GetProperty("notes").GetString());
        Assert.Equal(3, visit.GetProperty("version").GetInt64());

        var completedAt = visit.GetProperty("completedAt").GetDateTime();
        Assert.Equal(DateTimeKind.Utc, completedAt.Kind);
        Assert.InRange(completedAt, before.AddSeconds(-1), after.AddSeconds(1));
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
