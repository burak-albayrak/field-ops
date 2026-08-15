# Offline Visit Operations – Technical Design

The offline feature is not fully implemented; this document describes the proposed design for starting and completing Visits when connectivity is unreliable.

## Client-side storage

The mobile client would use persistent local storage such as SQLite. Besides cached Visit data, it would keep a durable `PendingOperation` queue containing:

```text
OperationId (UUID)
VisitId
OperationType (Start / Complete)
ExpectedVersion
ClientOccurredAtUtc
Payload
RetryCount
Status
```

The queue must survive application restarts. Start payloads contain coordinates; Complete payloads contain notes.

## Synchronization

When connectivity returns, a background sync worker processes pending operations. Operations for the same Visit are sent in order; for example, an offline `Start` must be accepted before a later offline `Complete`.

After each successful request, the client replaces its cached Visit with the server response and removes the corresponding pending operation.

The server remains authoritative for state transitions, distance validation and concurrency.

## Duplicate requests / idempotency

A request may be processed by the server even when the response never reaches the client. Therefore every queued operation has a stable client-generated `OperationId` that is reused across retries.

The server should persist processed operation IDs behind a unique constraint and return the previous logical result when the same operation is received again.

Completion is also logically idempotent: retrying an already successfully completed Visit must not change `CompletedAt`, increment its version or create another Analytics outbox event.

## Conflict resolution

Each pending operation includes the `ExpectedVersion` of the Visit known when the user acted.

Example:

```text
Client has Version 3
Another user cancels Visit -> Version 4
Offline client later sends Complete with Version 3
```

The stale operation must not overwrite the newer server state. The server returns a conflict and the client refreshes the Visit.

Conflicts are handled as follows:

```text
Server already reflects the same logical action
    -> treat as success

Operation is still valid after refresh
    -> retry only when it is safe

States are incompatible
    -> mark NeedsAttention and show the conflict to the user
```

A general "last write wins" policy is deliberately avoided because it could silently destroy newer business state.

## Timestamp strategy

Device clocks are not trusted as authoritative timestamps.

The client records `ClientOccurredAtUtc` so the system can preserve when the user performed the offline action, but the server generates the authoritative acceptance timestamp when synchronization succeeds.

Therefore:

```text
ClientOccurredAtUtc = audit/context
Server timestamp     = authoritative system time
```

Absolute server timestamps remain UTC. If the product later needs the original offline action time for reporting, it can be stored separately after validation rather than replacing server time.

## Failed synchronization

Failures are classified:

```text
Network timeout / 5xx
    -> retry with exponential backoff and jitter

409 concurrency conflict
    -> refresh and resolve, otherwise NeedsAttention

Validation / invalid transition
    -> do not retry indefinitely

Authentication failure
    -> pause sync until authentication is restored
```

Retry state is persisted locally. The UI should distinguish `Pending`, `Synchronizing`, `Synced` and `Needs Attention` so a locally recorded action is never presented as successfully accepted by the server before synchronization completes.

This design adds local queue management and server-side idempotency tracking, but preserves the existing guarantees around optimistic concurrency, Visit state transitions and transactional outbox delivery.