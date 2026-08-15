# Code Review – Visit Completion

The following implementation is assumed to be running in production:

```csharp
public async Task CompleteVisit(long id)
{
    var visit = await _db.Visits
        .FirstOrDefaultAsync(x => x.Id == id);

    if (visit == null)
        throw new Exception("Visit not found");

    if (visit.Status != "InProgress")
        throw new Exception("Invalid status");

    visit.Status = "Completed";
    visit.CompletedAt = DateTime.Now;

    await _analytics.SendVisitCompleted(visit);

    await _db.SaveChangesAsync();
}
```

The code appears simple, but it has several correctness and reliability problems in a production environment.

---

## 1. External Service Is Called Before the Database Commit

The most important problem is the ordering:

```text
1. Change Visit in memory
2. Send Analytics event
3. Save database changes
```

Consider this failure:

```text
Analytics call succeeds
        ↓
Database SaveChanges fails
```

Analytics now contains a `VisitCompleted` event for a Visit that is still `InProgress` in the primary database.

The external system and the database have diverged.

The database should remain the source of truth, and an external side effect should not be published unless the corresponding business transaction has been committed.

---

## 2. Visit Completion Depends on Analytics Availability

The opposite failure is also possible:

```text
Analytics service is unavailable
        ↓
SendVisitCompleted throws
        ↓
SaveChanges is never executed
```

The Visit therefore cannot be completed simply because Analytics is temporarily unavailable.

This directly couples an important business operation to the availability and latency of an external system.

A slow Analytics service would also increase the response time of the completion endpoint.

The completion of a Visit should not depend on Analytics being online.

---

## 3. There Is No Transactional Outbox

Simply moving the Analytics call after `SaveChangesAsync()` would not fully solve the problem.

For example:

```text
Database commit succeeds
        ↓
Application crashes
        ↓
Analytics call never happens
```

The Visit would be completed but its Analytics event would be lost.

The safer design is a **Transactional Outbox**.

Within the same database transaction:

```text
Visit -> Completed
OutboxMessage -> inserted
```

are persisted atomically.

A background worker delivers the outbox message afterwards.

This changes the failure model from:

```text
database transaction + unreliable external call
```

to:

```text
one local atomic database transaction
+
retryable asynchronous delivery
```

---

## 4. The Operation Is Not Idempotent

Mobile networks are unreliable.

A possible sequence is:

```text
Client sends Complete request
        ↓
Server completes the Visit
        ↓
Response is lost
        ↓
Client retries
```

With the reviewed implementation, the second request sees:

```text
Status = Completed
```

and throws `"Invalid status"`.

The client cannot distinguish:

```text
"my previous request succeeded"
```

from:

```text
"this Visit was already completed by somebody else"
```

For this use case, repeated completion should be idempotent.

If the Visit is already `Completed`, the API can return the current completed representation without:

- changing `CompletedAt`,
- incrementing the version again,
- or creating another Analytics event.

---

## 5. There Is a Concurrency Race

The implementation follows a classic read-modify-write pattern:

```text
SELECT Visit
check Status
modify Visit
SaveChanges
```

Without a concurrency token, two requests can both read the same state.

Example:

```text
Request A reads InProgress
Request B reads InProgress

Request A -> Completed
Request B -> Completed
```

Both requests may believe that they successfully performed the transition.

More importantly, stale clients can overwrite newer business state.

For example:

```text
10:00 Manager reads Visit
10:05 Employee completes Visit
10:07 Manager attempts to cancel the stale Visit
```

The system must detect that the manager is operating on an outdated version.

An optimistic concurrency token such as a numeric `Version` should be used.

The database update must fail when the version read by the client no longer matches the stored version.

---

## 6. Concurrent Completion Can Produce Duplicate Events

The concurrency problem is especially dangerous because the Analytics call happens before the database write.

Two requests could execute:

```text
A: read InProgress
B: read InProgress

A: send VisitCompleted
B: send VisitCompleted

A: SaveChanges
B: SaveChanges
```

The external system may receive duplicate completion events.

Even if the final Visit row looks correct, an irreversible side effect has already happened twice.

Using optimistic concurrency together with an outbox created in the same transaction prevents the losing request from independently producing another logical completion event.

---

## 7. `DateTime.Now` Is Not Appropriate

The code uses:

```csharp
DateTime.Now
```

This depends on the local timezone configuration of the application server.

If application instances run in different regions or server configuration changes, stored timestamps may become inconsistent.

Actual moments such as completion time should be represented in UTC.

For example:

```csharp
DateTime.UtcNow
```

or an equivalent centralized UTC clock abstraction.

In this project, business calendar dates and absolute timestamps are intentionally treated differently:

```text
PlannedDate   -> local business date
CompletedAt   -> UTC instant
```

---

## 8. Status Is Compared as a Raw String

The implementation uses:

```csharp
visit.Status != "InProgress"
```

Raw strings make invalid values and typographical mistakes easier to introduce.

The application should model Visit status using a constrained type such as an enum:

```csharp
VisitStatus.InProgress
VisitStatus.Completed
```

The persistence layer may still store readable string values in PostgreSQL.

---

## 9. Generic Exceptions Are Used for Expected Business Errors

Both cases throw:

```csharp
throw new Exception(...)
```

but these are expected application outcomes:

```text
Visit not found
Invalid Visit transition
Concurrency conflict
```

They should not be treated as unexpected server failures.

The API should map them to meaningful HTTP responses.

For example:

```text
404 Not Found
    visit not found

409 Conflict
    concurrency conflict

409 Conflict
    invalid status transition
```

or another consistently documented mapping.

Machine-readable error codes are also useful for frontend behavior.

Examples:

```text
invalid_visit_status
concurrency_conflict
```

---

## 10. The External Call Has No Visible Reliability Policy

The code does not show:

- timeout handling,
- retry scheduling,
- backoff,
- delivery tracking,
- or failed-message persistence.

Retries performed directly inside the HTTP request would also be problematic because they increase request latency and can create duplicate external calls.

With an outbox, retry behavior can occur asynchronously and independently of the Visit completion request.

---

## 11. No Cancellation Token Is Propagated

The asynchronous calls do not receive a cancellation token:

```csharp
FirstOrDefaultAsync(...)
SendVisitCompleted(...)
SaveChangesAsync()
```

For request-driven work, propagating the request cancellation token allows work that is no longer useful to be cancelled where appropriate.

This is not the most serious correctness issue in this method, but it is a production-quality improvement.

Care is still required around transaction boundaries: client disconnection must not leave an already-started critical transaction in an inconsistent state.

---

# Recommended Design

The completion flow should instead look conceptually like this:

```text
Receive Complete request
        ↓
Load Visit
        ↓
If not found
    -> 404
        ↓
If already Completed
    -> return current representation
       without another side effect
        ↓
Validate current state
        ↓
Apply Completed state
CompletedAt = UTC now
Version++
        ↓
Create VisitCompleted OutboxMessage
        ↓
Save Visit + OutboxMessage atomically
        ↓
Return successful response
```

A separate background worker performs:

```text
Read pending OutboxMessage
        ↓
Claim message
        ↓
POST event to Analytics
        ↓
Success
    -> mark ProcessedAt

Failure
    -> schedule retry
```

The HTTP request therefore does not wait for Analytics.

---

# Conceptual Improved Implementation

A simplified version could look like:

```csharp
public async Task<VisitDto> CompleteVisitAsync(
    long id,
    string? notes,
    CancellationToken cancellationToken)
{
    var visit = await _visitRepository.GetForUpdateAsync(
        id,
        cancellationToken);

    if (visit is null)
    {
        throw new VisitNotFoundException(id);
    }

    // A retry after a successfully processed request is safe.
    if (visit.Status == VisitStatus.Completed)
    {
        return Map(visit);
    }

    if (visit.Status != VisitStatus.InProgress)
    {
        throw new InvalidVisitStatusException(
            visit.Status,
            VisitStatus.InProgress);
    }

    var completedAt = DateTime.UtcNow;

    visit.Complete(
        completedAt,
        notes);

    var message = OutboxMessage.VisitCompleted(
        visit.Id,
        visit.EmployeeId,
        visit.StoreId,
        completedAt);

    await _outboxRepository.AddAsync(
        message,
        cancellationToken);

    // Visit and OutboxMessage are committed through the same DbContext /
    // PostgreSQL transaction.
    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return Map(visit);
}
```

The exact classes are less important than the guarantees provided by the design:

```text
Visit completion is atomic
Analytics does not block completion
Analytics events are not silently lost after commit
Completion retries are safe
Concurrent stale writes are detected
UTC timestamps are consistent
Expected failures map to meaningful API responses
```

---

# Delivery Semantics

The outbox worker provides **at-least-once delivery**, not exactly-once delivery.

A worker can theoretically send an event successfully and crash before recording:

```text
ProcessedAt
```

The same event may then be retried.

For that reason, a real Analytics consumer should process events idempotently, preferably using a stable event identifier.

Trying to guarantee exactly-once behavior across PostgreSQL and an unrelated HTTP service would require substantially more coordination and is not justified by this use case.

---

# Summary

The original implementation has a correct high-level intention but combines three operations with different reliability characteristics:

```text
business state transition
database persistence
external network side effect
```

inside one synchronous request without an atomicity strategy.

The most important production changes are:

1. Persist the Visit and an outbox event atomically.
2. Deliver Analytics events asynchronously.
3. Make completion idempotent.
4. Add optimistic concurrency protection.
5. Use UTC for absolute timestamps.
6. Replace raw status strings with a constrained application model.
7. Return structured application/API errors instead of generic exceptions.
8. Apply an explicit retry and failure strategy to external delivery.

These changes ensure that an unreliable Analytics service or a concurrent request cannot silently corrupt the authoritative Visit state.