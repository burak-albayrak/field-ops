using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace FieldOps.IntegrationTests.Visits;

public sealed class VisitConcurrencyTests : IntegrationTestBase
{
    private static readonly DateTime OriginalStartedAt =
        new(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc);

    private const double StartLatitude = 39.9335;
    private const double StartLongitude = 32.8598;

    private readonly ITestOutputHelper _output;

    public VisitConcurrencyTests(PostgreSqlFixture fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _output = output;
    }

    [Fact]
    public async Task Concurrent_complete_requests_return_the_same_single_completion()
    {
        var visitId = await ArrangeInProgressVisitAsync();

        var firstTask = PostCompleteAsync(visitId, "Concurrent completion A");
        var secondTask = PostCompleteAsync(visitId, "Concurrent completion B");
        var responses = await Task.WhenAll(firstTask, secondTask);

        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

            var results = await Task.WhenAll(responses.Select(ReadVisitResultAsync));
            Assert.All(results, result =>
            {
                Assert.Equal("Completed", result.Status);
                Assert.Equal(3, result.Version);
                Assert.NotNull(result.CompletedAt);
            });
            Assert.Equal(results[0].CompletedAt, results[1].CompletedAt);
            Assert.Equal(results[0].Notes, results[1].Notes);
            Assert.Contains(results[0].Notes, new[] { "Concurrent completion A", "Concurrent completion B" });

            var persisted = await LoadVisitAsync(visitId);
            Assert.Equal(VisitStatus.Completed, persisted.Status);
            // Start Version 2'dir; iki istekten yalnızca bir gerçek mutation kazanabildiği için son sürüm 3 kalmalıdır.
            Assert.Equal(3, persisted.Version);
            Assert.Equal(results[0].CompletedAt, persisted.CompletedAt);
            Assert.Equal(results[0].Notes, persisted.Notes);
            _output.WriteLine("Complete+Complete winner notes: {0}", persisted.Notes);
        }
        finally
        {
            DisposeResponses(responses);
        }
    }

    [Fact]
    public async Task Concurrent_cancel_requests_allow_one_mutation_and_one_concurrency_conflict()
    {
        var visitId = await ArrangeInProgressVisitAsync();

        var firstTask = PostCancelAsync(visitId, 2);
        var secondTask = PostCancelAsync(visitId, 2);
        var responses = await Task.WhenAll(firstTask, secondTask);

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var conflictResponse = Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("concurrency_conflict", await ReadProblemCodeAsync(conflictResponse));

            var persisted = await LoadVisitAsync(visitId);
            Assert.Equal(VisitStatus.Cancelled, persisted.Status);
            Assert.Equal(3, persisted.Version);
            Assert.Equal(OriginalStartedAt, persisted.StartedAt);
            Assert.Equal(StartLatitude, persisted.StartLatitude);
            Assert.Equal(StartLongitude, persisted.StartLongitude);
            Assert.Null(persisted.CompletedAt);
            _output.WriteLine("Cancel+Cancel losing code: concurrency_conflict");
        }
        finally
        {
            DisposeResponses(responses);
        }
    }

    [Fact]
    public async Task Concurrent_complete_and_cancel_allow_only_one_incompatible_decision()
    {
        var visitId = await ArrangeInProgressVisitAsync();

        var completeTask = PostCompleteAsync(visitId, "Concurrent completion");
        var cancelTask = PostCancelAsync(visitId, 2);
        var responses = await Task.WhenAll(completeTask, cancelTask);

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var conflictResponse = Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.Conflict);
            var conflictCode = await ReadProblemCodeAsync(conflictResponse);
            Assert.Contains(conflictCode, new[] { "concurrency_conflict", "invalid_visit_status" });

            var persisted = await LoadVisitAsync(visitId);
            Assert.Equal(3, persisted.Version);
            Assert.Equal(OriginalStartedAt, persisted.StartedAt);
            Assert.Equal(StartLatitude, persisted.StartLatitude);
            Assert.Equal(StartLongitude, persisted.StartLongitude);

            if (persisted.Status == VisitStatus.Completed)
            {
                Assert.NotNull(persisted.CompletedAt);
                Assert.Equal("Concurrent completion", persisted.Notes);
            }
            else
            {
                Assert.Equal(VisitStatus.Cancelled, persisted.Status);
                Assert.Null(persisted.CompletedAt);
                Assert.Null(persisted.Notes);
            }

            _output.WriteLine(
                "Complete+Cancel final state: {0}; losing code: {1}",
                persisted.Status,
                conflictCode);
        }
        finally
        {
            DisposeResponses(responses);
        }
    }

    private Task<long> ArrangeInProgressVisitAsync()
    {
        return ExecuteDbContextAsync(async context =>
        {
            var employee = new Employee("Concurrent Employee", "concurrent@example.com", "TR");
            var store = new Store("Concurrent Store", "TR", 39.9334, 32.8597);
            context.Employees.Add(employee);
            context.Stores.Add(store);
            await context.SaveChangesAsync();

            var visit = new Visit(
                employee.Id,
                store.Id,
                new DateOnly(2026, 8, 20),
                new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc));
            visit.Start(OriginalStartedAt, StartLatitude, StartLongitude);
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

    private Task<HttpResponseMessage> PostCompleteAsync(long visitId, string notes)
    {
        return Client.PostAsJsonAsync($"/api/visits/{visitId}/complete", new { Notes = notes });
    }

    private Task<HttpResponseMessage> PostCancelAsync(long visitId, long version)
    {
        return Client.PostAsJsonAsync($"/api/visits/{visitId}/cancel", new { Version = version });
    }

    private static async Task<VisitResult> ReadVisitResultAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var visit = document.RootElement;
        var completedAt = visit.GetProperty("completedAt");

        return new VisitResult(
            visit.GetProperty("status").GetString()!,
            completedAt.ValueKind == JsonValueKind.Null ? null : completedAt.GetDateTime(),
            visit.GetProperty("notes").GetString(),
            visit.GetProperty("version").GetInt64());
    }

    private static async Task<string> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString()!;
    }

    private static void DisposeResponses(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    private sealed class VisitResult
    {
        public VisitResult(string status, DateTime? completedAt, string? notes, long version)
        {
            Status = status;
            CompletedAt = completedAt;
            Notes = notes;
            Version = version;
        }

        public string Status { get; }

        public DateTime? CompletedAt { get; }

        public string? Notes { get; }

        public long Version { get; }
    }
}
