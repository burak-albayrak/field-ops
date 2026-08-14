using FieldOps.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldOps.IntegrationTests.Infrastructure;

public sealed class OutboxRepositoryTests : IntegrationTestBase
{
    private static readonly DateTime NowUtc =
        new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

    private const string MessageType = "VisitCompleted";
    private const string MessagePayload = "{\"type\":\"VisitCompleted\"}";

    private readonly PostgreSqlFixture _fixture;

    public OutboxRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Due_pending_message_is_claimed_without_changing_delivery_content()
    {
        var messageId = await SeedMessageAsync(NowUtc.AddMinutes(-1));
        var beforeClaim = await LoadMessageAsync(messageId);
        var leaseDuration = TimeSpan.FromMinutes(5);

        var claimed = await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(NowUtc, leaseDuration, 10));

        var message = Assert.Single(claimed);
        Assert.Equal(messageId, message.Id);
        Assert.Equal(beforeClaim.Type, message.Type);
        Assert.Equal(beforeClaim.Payload, message.Payload);
        Assert.Equal(0, message.AttemptCount);
        Assert.Equal(NowUtc.Add(leaseDuration), message.LockedUntil);

        var persisted = await LoadMessageAsync(messageId);
        Assert.Equal(message.LockedUntil, persisted.LockedUntil);
        Assert.Null(persisted.ProcessedAt);
        Assert.Equal(0, persisted.AttemptCount);
        Assert.Equal(beforeClaim.Type, persisted.Type);
        Assert.Equal(beforeClaim.Payload, persisted.Payload);
    }

    [Fact]
    public async Task Processed_future_and_actively_locked_messages_are_not_claimed()
    {
        var processedId = await SeedMessageAsync(NowUtc.AddHours(-2));
        var processedClaim = Assert.Single(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(NowUtc.AddHours(-1), TimeSpan.FromMinutes(5), 1)));
        Assert.Equal(processedId, processedClaim.Id);
        Assert.True(await ExecuteRepositoryAsync(repository => repository.MarkProcessedAsync(
            processedId,
            processedClaim.LockedUntil,
            NowUtc.AddMinutes(-50))));

        var lockedId = await SeedMessageAsync(NowUtc.AddHours(-1));
        var activeClaim = Assert.Single(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(NowUtc, TimeSpan.FromMinutes(10), 1)));
        Assert.Equal(lockedId, activeClaim.Id);

        await SeedMessageAsync(NowUtc.AddMinutes(20));

        var claimed = await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(NowUtc.AddMinutes(5), TimeSpan.FromMinutes(5), 10));

        Assert.Empty(claimed);
    }

    [Fact]
    public async Task Expired_lease_can_be_reclaimed()
    {
        var messageId = await SeedMessageAsync(NowUtc.AddMinutes(-1));
        var firstClaim = Assert.Single(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(NowUtc, TimeSpan.FromMinutes(5), 1)));

        var secondClaim = Assert.Single(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(NowUtc.AddMinutes(6), TimeSpan.FromMinutes(10), 1)));

        Assert.Equal(messageId, secondClaim.Id);
        Assert.NotEqual(firstClaim.LockedUntil, secondClaim.LockedUntil);
        Assert.Equal(0, secondClaim.AttemptCount);
        Assert.Equal(secondClaim.LockedUntil, (await LoadMessageAsync(messageId)).LockedUntil);
    }

    [Fact]
    public async Task Two_repository_instances_claim_one_message_exactly_once()
    {
        var messageId = await SeedMessageAsync(NowUtc.AddMinutes(-1));

        await using var scopeA = _fixture.Factory.Services.CreateAsyncScope();
        await using var scopeB = _fixture.Factory.Services.CreateAsyncScope();
        var repositoryA = scopeA.ServiceProvider.GetRequiredService<OutboxRepository>();
        var repositoryB = scopeB.ServiceProvider.GetRequiredService<OutboxRepository>();

        var claimA = repositoryA.ClaimPendingAsync(NowUtc, TimeSpan.FromMinutes(5), 1);
        var claimB = repositoryB.ClaimPendingAsync(NowUtc, TimeSpan.FromMinutes(5), 1);
        var results = await Task.WhenAll(claimA, claimB);
        var claimedIds = results.SelectMany(result => result).Select(message => message.Id).ToList();

        Assert.Equal([messageId], claimedIds);
        Assert.NotNull((await LoadMessageAsync(messageId)).LockedUntil);
    }

    [Fact]
    public async Task Two_repository_instances_never_claim_the_same_message_in_a_batch()
    {
        var seededIds = await SeedMessagesAsync(6, NowUtc.AddMinutes(-1));

        await using var scopeA = _fixture.Factory.Services.CreateAsyncScope();
        await using var scopeB = _fixture.Factory.Services.CreateAsyncScope();
        var repositoryA = scopeA.ServiceProvider.GetRequiredService<OutboxRepository>();
        var repositoryB = scopeB.ServiceProvider.GetRequiredService<OutboxRepository>();

        var claimA = repositoryA.ClaimPendingAsync(NowUtc, TimeSpan.FromMinutes(5), 4);
        var claimB = repositoryB.ClaimPendingAsync(NowUtc, TimeSpan.FromMinutes(5), 4);
        var results = await Task.WhenAll(claimA, claimB);
        var firstIds = results[0].Select(message => message.Id).ToHashSet();
        var secondIds = results[1].Select(message => message.Id).ToHashSet();
        var allClaimedIds = firstIds.Concat(secondIds).ToHashSet();

        Assert.Empty(firstIds.Intersect(secondIds));
        Assert.Equal(seededIds.Order(), allClaimedIds.Order());
    }

    [Fact]
    public async Task MarkProcessed_preserves_failure_count_and_removes_message_from_pending_work()
    {
        var messageId = await SeedMessageAsync(NowUtc.AddMinutes(-1));
        var firstClaim = Assert.Single(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(NowUtc, TimeSpan.FromMinutes(5), 1)));
        var retryAt = NowUtc.AddMinutes(10);
        Assert.True(await ExecuteRepositoryAsync(repository => repository.MarkFailedAsync(
            messageId,
            firstClaim.LockedUntil,
            retryAt,
            "analytics unavailable")));

        var retryClaim = Assert.Single(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(retryAt, TimeSpan.FromMinutes(5), 1)));
        var processedAt = retryAt.AddMinutes(1);

        var marked = await ExecuteRepositoryAsync(repository => repository.MarkProcessedAsync(
            messageId,
            retryClaim.LockedUntil,
            processedAt));

        Assert.True(marked);
        var persisted = await LoadMessageAsync(messageId);
        Assert.Equal(processedAt, persisted.ProcessedAt);
        Assert.Null(persisted.LockedUntil);
        Assert.Null(persisted.LastError);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Empty(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(processedAt.AddHours(1), TimeSpan.FromMinutes(5), 1)));
    }

    [Fact]
    public async Task MarkFailed_schedules_retry_and_increments_attempt_only_after_failure()
    {
        var messageId = await SeedMessageAsync(NowUtc.AddMinutes(-1));
        var firstClaim = Assert.Single(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(NowUtc, TimeSpan.FromMinutes(5), 1)));
        Assert.Equal(0, firstClaim.AttemptCount);
        var retryAt = NowUtc.AddMinutes(10);

        var marked = await ExecuteRepositoryAsync(repository => repository.MarkFailedAsync(
            messageId,
            firstClaim.LockedUntil,
            retryAt,
            "analytics unavailable"));

        Assert.True(marked);
        var persisted = await LoadMessageAsync(messageId);
        Assert.Null(persisted.ProcessedAt);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal(retryAt, persisted.NextAttemptAt);
        Assert.Equal("analytics unavailable", persisted.LastError);
        Assert.Null(persisted.LockedUntil);

        Assert.Empty(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(retryAt.AddTicks(-1), TimeSpan.FromMinutes(5), 1)));
        var retryClaim = Assert.Single(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(retryAt, TimeSpan.FromMinutes(5), 1)));
        Assert.Equal(1, retryClaim.AttemptCount);
    }

    [Fact]
    public async Task Stale_worker_cannot_write_failure_or_success_after_another_worker_reclaims()
    {
        var messageId = await SeedMessageAsync(NowUtc.AddMinutes(-1));
        var firstClaim = Assert.Single(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(NowUtc, TimeSpan.FromMinutes(5), 1)));
        var secondClaim = Assert.Single(await ExecuteRepositoryAsync(repository =>
            repository.ClaimPendingAsync(NowUtc.AddMinutes(6), TimeSpan.FromMinutes(10), 1)));
        Assert.NotEqual(firstClaim.LockedUntil, secondClaim.LockedUntil);

        var beforeStaleWrites = await LoadMessageAsync(messageId);
        var staleFailure = await ExecuteRepositoryAsync(repository => repository.MarkFailedAsync(
            messageId,
            firstClaim.LockedUntil,
            NowUtc.AddHours(1),
            "stale worker error"));
        var staleSuccess = await ExecuteRepositoryAsync(repository => repository.MarkProcessedAsync(
            messageId,
            firstClaim.LockedUntil,
            NowUtc.AddMinutes(7)));

        Assert.False(staleFailure);
        Assert.False(staleSuccess);
        var afterStaleWrites = await LoadMessageAsync(messageId);
        Assert.Equal(secondClaim.LockedUntil, afterStaleWrites.LockedUntil);
        Assert.Equal(beforeStaleWrites.AttemptCount, afterStaleWrites.AttemptCount);
        Assert.Equal(beforeStaleWrites.NextAttemptAt, afterStaleWrites.NextAttemptAt);
        Assert.Equal(beforeStaleWrites.LastError, afterStaleWrites.LastError);
        Assert.Null(afterStaleWrites.ProcessedAt);

        Assert.True(await ExecuteRepositoryAsync(repository => repository.MarkProcessedAsync(
            messageId,
            secondClaim.LockedUntil,
            NowUtc.AddMinutes(7))));
    }

    private Task<long> SeedMessageAsync(DateTime createdAt)
    {
        return ExecuteDbContextAsync(async context =>
        {
            var message = new OutboxMessage(MessageType, MessagePayload, createdAt);
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync();
            return message.Id;
        });
    }

    private Task<List<long>> SeedMessagesAsync(int count, DateTime createdAt)
    {
        return ExecuteDbContextAsync(async context =>
        {
            var messages = Enumerable.Range(0, count)
                .Select(_ => new OutboxMessage(MessageType, MessagePayload, createdAt))
                .ToList();
            context.OutboxMessages.AddRange(messages);
            await context.SaveChangesAsync();
            return messages.Select(message => message.Id).ToList();
        });
    }

    private Task<OutboxMessage> LoadMessageAsync(long id)
    {
        return ExecuteDbContextAsync(context => context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == id));
    }

    private async Task<TResult> ExecuteRepositoryAsync<TResult>(Func<OutboxRepository, Task<TResult>> action)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<OutboxRepository>();
        return await action(repository);
    }
}
