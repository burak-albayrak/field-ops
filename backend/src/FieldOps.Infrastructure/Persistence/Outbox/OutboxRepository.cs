using Microsoft.EntityFrameworkCore;

namespace FieldOps.Infrastructure.Persistence.Outbox;

public class OutboxRepository
{
    private const int CandidateScanMultiplier = 2;

    private readonly AppDbContext _context;

    public OutboxRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ClaimedOutboxMessage>> ClaimPendingAsync(
        DateTime nowUtc,
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be greater than zero.");
        }

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        }

        var leaseUntil = nowUtc.Add(leaseDuration);
        var candidateLimit = batchSize > int.MaxValue / CandidateScanMultiplier
            ? int.MaxValue
            : batchSize * CandidateScanMultiplier;

        // Birden fazla instance aynı Id listesini görebilir; sahipliği aşağıdaki koşullu UPDATE belirler.
        var candidateIds = await _context.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.ProcessedAt == null
                && message.NextAttemptAt <= nowUtc
                && (message.LockedUntil == null || message.LockedUntil <= nowUtc))
            .OrderBy(message => message.NextAttemptAt)
            .ThenBy(message => message.Id)
            .Select(message => message.Id)
            .Take(candidateLimit)
            .ToListAsync(cancellationToken);

        var claimedMessages = new List<ClaimedOutboxMessage>(Math.Min(batchSize, candidateIds.Count));

        foreach (var candidateId in candidateIds)
        {
            var affectedRows = await _context.OutboxMessages
                .Where(message =>
                    message.Id == candidateId
                    && message.ProcessedAt == null
                    && message.NextAttemptAt <= nowUtc
                    && (message.LockedUntil == null || message.LockedUntil <= nowUtc))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(message => message.LockedUntil, (DateTime?)leaseUntil),
                    cancellationToken);

            if (affectedRows == 0)
            {
                continue;
            }

            var claimedMessage = await _context.OutboxMessages
                .AsNoTracking()
                .Where(message =>
                    message.Id == candidateId
                    && message.ProcessedAt == null
                    && message.LockedUntil == leaseUntil)
                .Select(message => new ClaimedOutboxMessage(
                    message.Id,
                    message.Type,
                    message.Payload,
                    message.AttemptCount,
                    message.LockedUntil!.Value))
                .SingleOrDefaultAsync(cancellationToken);

            // Claim sonrasında durum beklenmedik biçimde değiştiyse sahiplik varsaymak yerine bu adayı atlarız.
            if (claimedMessage is null)
            {
                continue;
            }

            claimedMessages.Add(claimedMessage);

            if (claimedMessages.Count == batchSize)
            {
                break;
            }
        }

        return claimedMessages;
    }

    public async Task<bool> MarkProcessedAsync(
        long id,
        DateTime expectedLockedUntil,
        DateTime processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(expectedLockedUntil, nameof(expectedLockedUntil));
        EnsureUtc(processedAtUtc, nameof(processedAtUtc));

        // expectedLockedUntil hafif bir lease token'ıdır; eski worker yeni sahibin durumunu temizleyemez.
        var affectedRows = await _context.OutboxMessages
            .Where(message =>
                message.Id == id
                && message.ProcessedAt == null
                && message.LockedUntil == expectedLockedUntil)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.ProcessedAt, (DateTime?)processedAtUtc)
                    .SetProperty(message => message.LockedUntil, (DateTime?)null)
                    .SetProperty(message => message.LastError, (string?)null),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> MarkFailedAsync(
        long id,
        DateTime expectedLockedUntil,
        DateTime nextAttemptAtUtc,
        string error,
        CancellationToken cancellationToken = default)
    {
        EnsureUtc(expectedLockedUntil, nameof(expectedLockedUntil));
        EnsureUtc(nextAttemptAtUtc, nameof(nextAttemptAtUtc));

        // Aynı lease predicate'i, süresi dolmuş worker'ın yeni claim sahibinin retry kararını ezmesini önler.
        var affectedRows = await _context.OutboxMessages
            .Where(message =>
                message.Id == id
                && message.ProcessedAt == null
                && message.LockedUntil == expectedLockedUntil)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.AttemptCount, message => message.AttemptCount + 1)
                    .SetProperty(message => message.NextAttemptAt, nextAttemptAtUtc)
                    .SetProperty(message => message.LastError, error)
                    .SetProperty(message => message.LockedUntil, (DateTime?)null),
                cancellationToken);

        return affectedRows == 1;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The timestamp must use DateTimeKind.Utc.", parameterName);
        }
    }
}
