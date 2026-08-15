using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.IntegrationTests.Infrastructure;

public sealed class HostedOutboxWorkerTests : IntegrationTestBase
{
    private static readonly TimeSpan EventualTimeout = TimeSpan.FromSeconds(12);
    private static readonly DateTime StartedAt =
        new(2026, 8, 22, 8, 0, 0, DateTimeKind.Utc);

    private readonly PostgreSqlFixture _fixture;

    public HostedOutboxWorkerTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Analytics_down_does_not_rollback_or_fail_complete()
    {
        var visitId = await ArrangeInProgressVisitAsync();
        var transport = TestAnalyticsTransport.Always(HttpStatusCode.ServiceUnavailable);
        var settings = new WorkerTestSettings
        {
            BaseRetrySeconds = 5,
            MaxRetrySeconds = 5
        };
        using var factory = CreateWorkerFactory(transport, settings);
        using var client = factory.StartClient();

        using var response = await PostCompleteAsync(client, visitId, "Analytics is down.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var responseDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Completed", responseDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal(3, responseDocument.RootElement.GetProperty("version").GetInt64());

        await Eventually.UntilAsync(
            async () =>
            {
                var message = await LoadSingleOutboxOrDefaultAsync();
                return message is { AttemptCount: >= 1, LockedUntil: null };
            },
            EventualTimeout,
            "the failed Analytics attempt to be persisted");

        var outbox = (await LoadSingleOutboxOrDefaultAsync())!;
        Assert.Null(outbox.ProcessedAt);
        Assert.Null(outbox.FailedAt);
        Assert.True(outbox.AttemptCount >= 1);
        Assert.Null(outbox.LockedUntil);
        Assert.Equal("Analytics returned HTTP 503.", outbox.LastError);
        Assert.True(outbox.NextAttemptAt > outbox.CreatedAt);

        var visit = await LoadVisitAsync(visitId);
        Assert.Equal(VisitStatus.Completed, visit.Status);
        Assert.Equal(3, visit.Version);
    }

    [Fact]
    public async Task Analytics_recovery_delivers_the_same_durable_payload_after_retry()
    {
        var visitId = await ArrangeInProgressVisitAsync();
        var transport = TestAnalyticsTransport.FailThenSucceed(failureCount: 1);
        using var factory = CreateWorkerFactory(transport);
        using var client = factory.StartClient();

        using var response = await PostCompleteAsync(client, visitId, "Recovered delivery.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Eventually.UntilAsync(
            async () =>
            {
                var message = await LoadSingleOutboxOrDefaultAsync();
                return transport.RequestCount >= 2 && message?.ProcessedAt is not null;
            },
            EventualTimeout,
            "the failed Outbox delivery to retry and succeed");

        var outbox = (await LoadSingleOutboxOrDefaultAsync())!;
        Assert.NotNull(outbox.ProcessedAt);
        Assert.Null(outbox.FailedAt);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.Null(outbox.LockedUntil);
        Assert.Null(outbox.LastError);

        var requests = transport.Requests;
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/events", request.RequestUri?.AbsolutePath);
            Assert.Equal("application/json", request.ContentType);
            Assert.Equal(outbox.Payload, request.Body);
        });
        Assert.Equal(requests[0].Body, requests[1].Body);
        AssertPayloadMatchesVisit(outbox.Payload, await LoadVisitAsync(visitId));
    }

    [Fact]
    public async Task Lost_acknowledgement_can_deliver_the_same_payload_twice_by_design()
    {
        var visitId = await ArrangeInProgressVisitAsync();
        var transport = TestAnalyticsTransport.LoseFirstAcknowledgementThenSucceed();
        using var factory = CreateWorkerFactory(transport);
        using var client = factory.StartClient();

        using var response = await PostCompleteAsync(client, visitId, "At-least-once boundary.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Eventually.UntilAsync(
            async () =>
            {
                var message = await LoadSingleOutboxOrDefaultAsync();
                return transport.RequestCount >= 2 && message?.ProcessedAt is not null;
            },
            EventualTimeout,
            "the acknowledgement-loss retry to complete");

        var outbox = (await LoadSingleOutboxOrDefaultAsync())!;
        Assert.NotNull(outbox.ProcessedAt);
        Assert.Null(outbox.FailedAt);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.Null(outbox.LockedUntil);
        Assert.Null(outbox.LastError);

        var requests = transport.Requests;
        Assert.Equal(2, requests.Count);
        Assert.Equal(outbox.Payload, requests[0].Body);
        Assert.Equal(requests[0].Body, requests[1].Body);
    }

    [Fact]
    public async Task Two_hosted_workers_deliver_one_normally_acknowledged_message_once()
    {
        var transport = TestAnalyticsTransport.Always(HttpStatusCode.NoContent);
        using var factoryA = CreateWorkerFactory(transport);
        using var clientA = factoryA.StartClient();
        using var factoryB = CreateWorkerFactory(transport);
        using var clientB = factoryB.StartClient();
        var payload = CreatePayload(visitId: 1001);
        var messageId = await SeedOutboxMessageAsync(payload);

        await Eventually.UntilAsync(
            async () => (await LoadOutboxMessageAsync(messageId)).ProcessedAt is not null,
            EventualTimeout,
            "one of two hosted workers to process the shared message");

        var outbox = await LoadOutboxMessageAsync(messageId);
        Assert.NotNull(outbox.ProcessedAt);
        Assert.Null(outbox.FailedAt);
        Assert.Null(outbox.LockedUntil);
        Assert.Equal(0, outbox.AttemptCount);
        Assert.Null(outbox.LastError);
        var delivery = Assert.Single(transport.Requests);
        Assert.Equal(outbox.Payload, delivery.Body);
    }

    [Fact]
    public async Task Cancelled_worker_leaves_lease_for_another_host_to_reclaim_after_expiry()
    {
        var blockingTransport = TestAnalyticsTransport.BlockUntilCancellation();
        var settings = new WorkerTestSettings
        {
            LeaseSeconds = 5,
            AnalyticsTimeoutSeconds = 30
        };
        var payload = CreatePayload(visitId: 2001);
        var messageId = await SeedOutboxMessageAsync(payload);
        var factoryA = CreateWorkerFactory(blockingTransport, settings);
        var clientA = factoryA.StartClient();

        try
        {
            await blockingTransport.FirstRequestObserved.WaitAsync(EventualTimeout);
            var claimed = await LoadOutboxMessageAsync(messageId);
            Assert.NotNull(claimed.LockedUntil);
            Assert.Null(claimed.ProcessedAt);
            Assert.Null(claimed.FailedAt);
            Assert.Equal(0, claimed.AttemptCount);
        }
        finally
        {
            clientA.Dispose();
            // Graceful host iptali hard crash değildir; HTTP sonucu yazılmadan bırakılmış lease'i deterministik üretir.
            factoryA.Dispose();
        }

        await blockingTransport.CancellationObserved.WaitAsync(EventualTimeout);
        var abandoned = await LoadOutboxMessageAsync(messageId);
        var abandonedLease = Assert.IsType<DateTime>(abandoned.LockedUntil);
        Assert.Null(abandoned.ProcessedAt);
        Assert.Null(abandoned.FailedAt);
        Assert.Equal(0, abandoned.AttemptCount);
        Assert.Null(abandoned.LastError);

        var recoveryTransport = TestAnalyticsTransport.Always(HttpStatusCode.NoContent);
        using var factoryB = CreateWorkerFactory(recoveryTransport, settings);
        using var clientB = factoryB.StartClient();

        await Eventually.UntilAsync(
            async () => (await LoadOutboxMessageAsync(messageId)).ProcessedAt is not null,
            EventualTimeout,
            "the abandoned lease to expire and another host to reclaim it");

        var recovered = await LoadOutboxMessageAsync(messageId);
        Assert.NotNull(recovered.ProcessedAt);
        Assert.Null(recovered.FailedAt);
        Assert.Null(recovered.LockedUntil);
        Assert.Equal(0, recovered.AttemptCount);
        Assert.Null(recovered.LastError);
        var recoveryRequest = Assert.Single(recoveryTransport.Requests);
        Assert.True(recoveryRequest.ObservedAtUtc >= abandonedLease);
        Assert.Equal(recovered.Payload, recoveryRequest.Body);
    }

    [Fact]
    public async Task Two_hosted_workers_drain_shared_backlog_without_duplicate_normal_deliveries()
    {
        var transport = TestAnalyticsTransport.Always(HttpStatusCode.NoContent);
        var settings = new WorkerTestSettings
        {
            BatchSize = 2
        };
        using var factoryA = CreateWorkerFactory(transport, settings);
        using var clientA = factoryA.StartClient();
        using var factoryB = CreateWorkerFactory(transport, settings);
        using var clientB = factoryB.StartClient();
        var payloads = Enumerable.Range(1, 6)
            .Select(index => CreatePayload(visitId: 3000 + index))
            .ToList();
        var messageIds = await SeedOutboxMessagesAsync(payloads);

        await Eventually.UntilAsync(
            async () =>
            {
                var messages = await LoadOutboxMessagesAsync(messageIds);
                return messages.Count == 6 && messages.All(message => message.ProcessedAt is not null);
            },
            EventualTimeout,
            "two hosted workers to drain six shared messages");

        var finalMessages = await LoadOutboxMessagesAsync(messageIds);
        Assert.All(finalMessages, message =>
        {
            Assert.NotNull(message.ProcessedAt);
            Assert.Null(message.FailedAt);
            Assert.Null(message.LockedUntil);
            Assert.Equal(0, message.AttemptCount);
            Assert.Null(message.LastError);
        });
        Assert.Equal(6, transport.RequestCount);

        var deliveredVisitIds = transport.Requests
            .Select(request => ReadVisitId(request.Body!))
            .Order()
            .ToList();
        Assert.Equal(Enumerable.Range(3001, 6).Select(value => (long)value), deliveredVisitIds);
    }

    private WorkerEnabledFieldOpsWebApplicationFactory CreateWorkerFactory(
        TestAnalyticsTransport transport,
        WorkerTestSettings? settings = null)
    {
        return new WorkerEnabledFieldOpsWebApplicationFactory(
            _fixture.ConnectionString,
            transport,
            settings);
    }

    private Task<long> ArrangeInProgressVisitAsync()
    {
        return ExecuteDbContextAsync(async context =>
        {
            var employee = new Employee("Worker Employee", "worker@example.com", "TR");
            var store = new Store("Worker Store", "TR", 39.9334, 32.8597);
            context.Employees.Add(employee);
            context.Stores.Add(store);
            await context.SaveChangesAsync();

            var visit = new Visit(
                employee.Id,
                store.Id,
                new DateOnly(2026, 8, 22),
                new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc));
            visit.Start(StartedAt, 39.9335, 32.8598);
            context.Visits.Add(visit);
            await context.SaveChangesAsync();
            return visit.Id;
        });
    }

    private static Task<HttpResponseMessage> PostCompleteAsync(
        HttpClient client,
        long visitId,
        string notes)
    {
        return client.PostAsJsonAsync($"/api/visits/{visitId}/complete", new { Notes = notes });
    }

    private Task<Visit> LoadVisitAsync(long visitId)
    {
        return ExecuteDbContextAsync(context => context.Visits
            .AsNoTracking()
            .SingleAsync(visit => visit.Id == visitId));
    }

    private Task<OutboxMessage?> LoadSingleOutboxOrDefaultAsync()
    {
        return ExecuteDbContextAsync(context => context.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync());
    }

    private Task<OutboxMessage> LoadOutboxMessageAsync(long messageId)
    {
        return ExecuteDbContextAsync(context => context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == messageId));
    }

    private Task<List<OutboxMessage>> LoadOutboxMessagesAsync(IReadOnlyCollection<long> messageIds)
    {
        return ExecuteDbContextAsync(context => context.OutboxMessages
            .AsNoTracking()
            .Where(message => messageIds.Contains(message.Id))
            .OrderBy(message => message.Id)
            .ToListAsync());
    }

    private Task<long> SeedOutboxMessageAsync(string payload)
    {
        return ExecuteDbContextAsync(async context =>
        {
            var message = new OutboxMessage(
                OutboxWriter.VisitCompletedType,
                payload,
                DateTime.UtcNow.AddSeconds(-1));
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync();
            return message.Id;
        });
    }

    private Task<List<long>> SeedOutboxMessagesAsync(IEnumerable<string> payloads)
    {
        return ExecuteDbContextAsync(async context =>
        {
            var createdAt = DateTime.UtcNow.AddSeconds(-1);
            var messages = payloads
                .Select(payload => new OutboxMessage(OutboxWriter.VisitCompletedType, payload, createdAt))
                .ToList();
            context.OutboxMessages.AddRange(messages);
            await context.SaveChangesAsync();
            return messages.Select(message => message.Id).ToList();
        });
    }

    private static string CreatePayload(long visitId)
    {
        return JsonSerializer.Serialize(new
        {
            type = OutboxWriter.VisitCompletedType,
            visitId,
            employeeId = 10,
            storeId = 20,
            completedAt = new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc)
        });
    }

    private static long ReadVisitId(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("visitId").GetInt64();
    }

    private static void AssertPayloadMatchesVisit(string payload, Visit visit)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal("VisitCompleted", root.GetProperty("type").GetString());
        Assert.Equal(visit.Id, root.GetProperty("visitId").GetInt64());
        Assert.Equal(visit.EmployeeId, root.GetProperty("employeeId").GetInt64());
        Assert.Equal(visit.StoreId, root.GetProperty("storeId").GetInt64());
        Assert.Equal(visit.CompletedAt, root.GetProperty("completedAt").GetDateTime());
    }
}
