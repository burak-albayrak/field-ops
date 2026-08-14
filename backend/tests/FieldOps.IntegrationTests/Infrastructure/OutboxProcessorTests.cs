using FieldOps.Infrastructure.Analytics;
using FieldOps.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FieldOps.IntegrationTests.Infrastructure;

public sealed class OutboxProcessorTests : IntegrationTestBase
{
    private const string Payload = "{\"type\":\"VisitCompleted\"}";
    private const string DeliveryError = "Analytics returned HTTP 503.";

    private readonly PostgreSqlFixture _fixture;

    public OutboxProcessorTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Successful_delivery_marks_message_processed_without_incrementing_attempt()
    {
        var messageId = await SeedMessageAsync(OutboxWriter.VisitCompletedType);
        var analytics = new FakeAnalyticsClient(AnalyticsDeliveryResult.Success());

        var claimedCount = await ProcessBatchAsync(analytics);

        Assert.Equal(1, claimedCount);
        Assert.Equal(1, analytics.CallCount);
        var persisted = await LoadMessageAsync(messageId);
        Assert.NotNull(persisted.ProcessedAt);
        Assert.Null(persisted.LockedUntil);
        Assert.Equal(0, persisted.AttemptCount);
        Assert.Null(persisted.LastError);
    }

    [Fact]
    public async Task Failed_delivery_schedules_retry_and_records_safe_error()
    {
        var messageId = await SeedMessageAsync(OutboxWriter.VisitCompletedType);
        var analytics = new FakeAnalyticsClient(AnalyticsDeliveryResult.Failure(DeliveryError));
        var beforeFailure = DateTime.UtcNow;

        var claimedCount = await ProcessBatchAsync(analytics);

        var afterFailure = DateTime.UtcNow;
        Assert.Equal(1, claimedCount);
        var persisted = await LoadMessageAsync(messageId);
        Assert.Null(persisted.ProcessedAt);
        Assert.Null(persisted.LockedUntil);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal(DeliveryError, persisted.LastError);
        Assert.InRange(
            persisted.NextAttemptAt,
            beforeFailure.AddSeconds(5),
            afterFailure.AddSeconds(5));
    }

    [Fact]
    public async Task Unsupported_type_uses_failure_lifecycle_without_calling_analytics()
    {
        var messageId = await SeedMessageAsync("UnknownEvent");
        var analytics = new FakeAnalyticsClient(AnalyticsDeliveryResult.Success());

        var claimedCount = await ProcessBatchAsync(analytics);

        Assert.Equal(1, claimedCount);
        Assert.Equal(0, analytics.CallCount);
        var persisted = await LoadMessageAsync(messageId);
        Assert.Null(persisted.ProcessedAt);
        Assert.Null(persisted.LockedUntil);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal("Unsupported outbox message type.", persisted.LastError);
        Assert.True(persisted.NextAttemptAt > persisted.CreatedAt);
    }

    [Fact]
    public async Task One_delivery_failure_does_not_prevent_later_message_success()
    {
        var firstId = await SeedMessageAsync(OutboxWriter.VisitCompletedType);
        var secondId = await SeedMessageAsync(OutboxWriter.VisitCompletedType);
        var analytics = new FakeAnalyticsClient(
            AnalyticsDeliveryResult.Failure(DeliveryError),
            AnalyticsDeliveryResult.Success());

        var claimedCount = await ProcessBatchAsync(analytics);

        Assert.Equal(2, claimedCount);
        Assert.Equal(2, analytics.CallCount);
        var first = await LoadMessageAsync(firstId);
        var second = await LoadMessageAsync(secondId);
        Assert.Equal(1, first.AttemptCount);
        Assert.Equal(DeliveryError, first.LastError);
        Assert.Null(first.ProcessedAt);
        Assert.NotNull(second.ProcessedAt);
        Assert.Equal(0, second.AttemptCount);
        Assert.Null(second.LastError);
    }

    [Fact]
    public async Task Explicit_cancellation_propagates_without_recording_failure()
    {
        var messageId = await SeedMessageAsync(OutboxWriter.VisitCompletedType);
        var analytics = new FakeAnalyticsClient(AnalyticsDeliveryResult.Success());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProcessBatchAsync(analytics, cancellation.Token));

        Assert.Equal(0, analytics.CallCount);
        var persisted = await LoadMessageAsync(messageId);
        Assert.Null(persisted.ProcessedAt);
        Assert.Null(persisted.LockedUntil);
        Assert.Equal(0, persisted.AttemptCount);
        Assert.Null(persisted.LastError);
    }

    private Task<long> SeedMessageAsync(string type)
    {
        return ExecuteDbContextAsync(async context =>
        {
            var message = new OutboxMessage(type, Payload, DateTime.UtcNow.AddMinutes(-1));
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync();
            return message.Id;
        });
    }

    private Task<OutboxMessage> LoadMessageAsync(long id)
    {
        return ExecuteDbContextAsync(context => context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == id));
    }

    private async Task<int> ProcessBatchAsync(
        IAnalyticsClient analyticsClient,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var options = Options.Create(new OutboxProcessingOptions
        {
            Enabled = false,
            PollIntervalSeconds = 2,
            BatchSize = 5,
            LeaseSeconds = 30,
            BaseRetrySeconds = 5,
            MaxRetrySeconds = 300
        });
        var processor = new OutboxProcessor(
            scope.ServiceProvider.GetRequiredService<OutboxRepository>(),
            analyticsClient,
            new OutboxRetryBackoff(options),
            options,
            NullLogger<OutboxProcessor>.Instance);

        return await processor.ProcessBatchAsync(cancellationToken);
    }

    private sealed class FakeAnalyticsClient : IAnalyticsClient
    {
        private readonly Queue<AnalyticsDeliveryResult> _results;

        public FakeAnalyticsClient(params AnalyticsDeliveryResult[] results)
        {
            _results = new Queue<AnalyticsDeliveryResult>(results);
        }

        public int CallCount { get; private set; }

        public Task<AnalyticsDeliveryResult> SendAsync(
            ClaimedOutboxMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_results.Dequeue());
        }
    }
}
