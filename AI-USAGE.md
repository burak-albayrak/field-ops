# AI Usage

AI tools were used throughout this technical assessment as development assistants.

The tools were used to accelerate analysis, implementation, review and documentation, but their output was not treated as authoritative. Important business rules, architectural decisions and generated code were reviewed against the case requirements and the actual behavior of the application.

## Tools Used

### ChatGPT

ChatGPT was mainly used for:

- Requirement analysis
- Architecture discussions
- Database and concurrency design
- Timezone reasoning
- Transactional outbox design
- Performance and indexing discussions
- Test planning
- Debugging support
- Docker and deployment planning
- Reviewing implementation decisions
- Documentation drafting

### Codex

Codex was used as an implementation assistant while working in the repository.

It assisted with areas including:

- Backend implementation
- Entity Framework Core persistence
- Database migrations
- Automated tests
- Docker configuration
- Frontend implementation
- Deployment infrastructure and scripts
- Code review and iterative fixes

Generated changes were reviewed before being accepted.

I did not rely on AI output as a substitute for understanding the implementation. When an AI suggestion conflicted with the case, the existing architecture or observed runtime behavior, it was changed or rejected.

---

# How AI Was Used

## Requirement Analysis

AI was used to break the case into individual technical problems instead of immediately generating an application.

Examples included:

- Distinguishing business dates from timestamps
- Identifying the race condition in duplicate Visit creation
- Separating optimistic concurrency from request idempotency
- Evaluating failure modes around the Analytics integration
- Identifying the performance implications of approximately 10 million Visit rows
- Planning the offline requirement separately because the case asks for a design rather than a full implementation

Each important requirement was checked against the original case before implementation.

---

## Architecture

AI was used to discuss multiple architecture options.

The final backend uses a layered monolith with:

```text
Domain
Application
Infrastructure
API
```

I intentionally kept the architecture relatively conventional.

For example, additional abstractions such as a full CQRS architecture, generic repository framework, distributed messaging platform or microservices were not introduced because the requirements did not justify their operational and conceptual complexity.

The goal was not to maximize the number of architectural patterns, but to use patterns where they solved a specific problem.

---

## Database Design

AI assisted with reviewing:

- Entity relationships
- Foreign-key behavior
- PostgreSQL constraints
- Visit status persistence
- Optimistic concurrency
- Partial unique indexes
- Query-oriented indexes

One important decision was to enforce active Visit uniqueness in PostgreSQL rather than relying only on application code.

The application may perform an initial check for a better error path, but the database constraint remains authoritative because two application instances can race.

---

## Concurrency and Idempotency

AI was used to reason through two different problems that can easily be confused.

### Optimistic concurrency

A stale client must not overwrite a Visit that has already changed.

This is handled using the Visit `Version` concurrency token.

### Completion idempotency

A client may retry a successful completion request because it never received the original response.

A repeated completion must not create another completion timestamp, version change or Analytics event.

Keeping these two problems separate was an important part of the final design.

---

## Analytics Integration

AI was used to examine failure ordering around:

```text
database commit
external Analytics call
```

A synchronous Analytics call could produce inconsistent outcomes if one operation succeeds and the other fails.

The final solution uses the Transactional Outbox Pattern:

```text
Visit update
+
OutboxMessage insert
```

are committed atomically.

A background worker then performs external delivery with retry behavior.

---

## Performance

AI assisted with identifying indexes for the expected production-scale dataset:

```text
50,000 Employees
100,000 Stores
10,000,000 Visits
```

The critical completed-Visit query was not accepted based only on theoretical reasoning.

A development benchmark database containing approximately 10 million Visit rows was generated, and the resulting PostgreSQL query plan and runtime were inspected using `EXPLAIN ANALYZE`.

The benchmark results were then used to evaluate whether additional infrastructure such as caching or partitioning was justified.

They were not required for the observed workload.

---

## Testing

AI assisted in planning and generating tests, but the testing strategy was chosen around the failure modes of the system rather than maximizing code coverage.

The final backend test suite includes unit and PostgreSQL-backed integration tests.

Important scenarios include:

- Visit creation
- Concurrent duplicate creation
- Visit start
- Distance validation
- Visit completion
- Completion retries
- Invalid status transitions
- Optimistic concurrency conflicts
- Transactional outbox behavior
- Filtering and pagination

PostgreSQL Testcontainers were preferred over an in-memory database because important behaviors depend on real PostgreSQL constraints, transactions and indexes.

---

## Deployment

AI was also used during deployment planning and troubleshooting.

The final assessment environment uses:

```text
DigitalOcean Droplet
Caddy
Frontend Nginx
ASP.NET Core
PostgreSQL
Docker Compose
```

AI assistance included:

- Droplet sizing
- Docker installation planning
- SSH/deploy-key setup
- Network exposure review
- HTTPS configuration
- Caddy/Let's Encrypt troubleshooting
- Runtime memory inspection
- Frontend-only deployment workflow validation

The final deployment was verified from an external machine rather than assuming that container health alone meant the application was publicly reachable.

---

# Example Prompts

The actual development process involved many iterative prompts rather than a single large generation prompt.

Representative examples are shown below.

## Architecture

> Analyze this case as a production-oriented backend problem. Do not generate code yet. Identify the important consistency, concurrency, timezone, external-service and performance problems. Prefer a conventional architecture and avoid introducing abstractions that are not justified by the requirements.

## Duplicate Visit Creation

> A worker cannot have more than one active visit for the same store and local planned date, and the application may run on multiple instances. Explain why an application-side existence check is not enough and propose a PostgreSQL-safe solution.

## Completion and Analytics

> Analyze the failure cases if Visit completion updates PostgreSQL and synchronously calls an unreliable Analytics service. Design a solution where completion does not depend on Analytics availability and the event is delivered as reliably as practical.

## Concurrency

> Two users can modify the same Visit using stale screens. Design an optimistic concurrency strategy that prevents a stale cancellation from overwriting a newer completion, while keeping repeated completion requests idempotent.

## Performance

> Assume approximately 10 million Visit rows. Design the indexes for fetching one employee's completed visits in Turkey during the last 30 days ordered newest first. Explain the index column order and whether a partial index is appropriate.

## Deployment

> Design a low-cost deployment for this assessment using one small DigitalOcean Droplet. Only the public reverse proxy should expose HTTP/HTTPS; PostgreSQL, backend and frontend service ports should not be publicly reachable. The frontend should be independently redeployable without restarting the backend or database.

---

# Example of an AI Suggestion That Was Rejected

One AI-generated interpretation initially proposed an additional Visit-start rule:

> A Visit should only be startable when its `PlannedDate` matches the Store's current local calendar date.

I rejected this rule after checking the case specification again.

The case requires a Visit to be:

```text
Status = Planned
```

and requires the employee to be within 200 meters of the Store.

It does **not** state that `PlannedDate` must equal today's local date.

Adding this rule would therefore have changed the business requirements instead of implementing them.

The final implementation does not impose that restriction.

This was a useful example of why AI-generated business rules must be verified against the original specification rather than accepted because they sound reasonable.

---

# Another Example of AI Output Being Corrected

During deployment validation, an AI suggestion initially stated that executing the frontend deployment script should necessarily produce a new frontend container ID.

The test showed that the frontend source had not changed and Docker reused the exact same cached image. Docker Compose therefore correctly left the existing healthy frontend container running.

The conclusion was corrected:

```text
same source + same image
→ no container replacement is required
```

The important property of the frontend-only deployment script is that it does not unnecessarily recreate the backend, PostgreSQL or Caddy services.

A real frontend image change will cause the frontend service to be updated.

This reinforced the same principle: observed behavior and tool semantics were used to validate AI advice instead of adjusting the system merely to make the original suggestion appear correct.

---

# Approach to AI-Generated Output

My general approach was:

1. Understand the requirement before asking for implementation.
2. Use AI to compare possible solutions.
3. Prefer the simplest solution that satisfies the concrete requirement.
4. Review generated code and configuration.
5. Build and run automated tests.
6. Validate PostgreSQL-specific behavior using a real PostgreSQL instance.
7. Measure performance instead of relying only on theoretical claims.
8. Verify deployment behavior on the actual server.
9. Correct AI suggestions when they conflict with the specification or observed behavior.

AI was therefore used as an engineering assistant rather than as an unquestioned source of implementation decisions.