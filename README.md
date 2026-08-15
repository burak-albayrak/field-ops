# FieldOps – Field Operations & Visit Management System

FieldOps is a small field-operations platform for planning, starting, completing and reviewing store visits.

The project was implemented as a technical assessment with emphasis on business-rule correctness, concurrency, data consistency, failure handling, performance at large data volumes, and production-oriented deployment decisions.

## Live Application

- Git Repository: `https://github.com/burak-albayrak/field-ops`
- Live Application: `https://fieldops.161-35-23-78.sslip.io`
- API Base URL: `https://fieldops.161-35-23-78.sslip.io/api`

The live environment runs the React frontend, ASP.NET Core API and PostgreSQL database together on a DigitalOcean Droplet.

---

## Technology Stack

### Backend

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- Npgsql
- PostgreSQL 17
- xUnit
- Testcontainers for PostgreSQL

### Frontend

- React
- TypeScript
- Vite
- TanStack Query
- Native Fetch API
- Nginx

### Infrastructure

- Docker
- Docker Compose
- Caddy
- DigitalOcean Droplet
- Let's Encrypt TLS certificates through Caddy

### Why these technologies?

.NET and ASP.NET Core were chosen because they provide a mature web stack, strong typing, good async support and first-class dependency injection.

PostgreSQL was used as the relational database as required by the case. Database-level constraints and indexes are intentionally used for rules that must remain correct when multiple application instances operate concurrently.

React with TypeScript provides a small and maintainable web client while keeping the frontend architecture intentionally simple.

Docker Compose provides a reproducible environment for the frontend, backend and PostgreSQL services and also closely matches the deployed topology.

---

# Running the Project Locally

## Requirements

The simplest way to run the application is with Docker and Docker Compose.

No local PostgreSQL or .NET installation is required when using Docker.

## 1. Configure the environment

Create a local `.env` file based on `.env.example`.

Example:

```env
POSTGRES_DB=fieldops
POSTGRES_USER=fieldops
POSTGRES_PASSWORD=replace-with-a-local-password
ASPNETCORE_ENVIRONMENT=Development
SITE_ADDRESS=fieldops.localhost
```

For the initial local bootstrap, `ASPNETCORE_ENVIRONMENT=Development` is intentional.

In Development, the application applies the EF Core migrations and inserts idempotent demo data. Production startup does not automatically mutate the database schema.

The real `.env` file is ignored by Git.

## 2. Start the application

From the repository root:

```bash
docker compose up --build
```

The local services are exposed as:

```text
Frontend:   http://localhost:18081
Backend:    http://localhost:18080
PostgreSQL: localhost:15432
```

The frontend proxies `/api` requests to the backend, so normal browser usage does not require a separate API origin.

## 3. Example API request

```bash
curl 'http://localhost:18080/api/visits?page=1&pageSize=20'
```

---

# Architecture

The backend is implemented as a layered monolith with pragmatic Clean Architecture boundaries.

```text
backend/
├── FieldOps.sln
├── src/
│   ├── FieldOps.Api/
│   ├── FieldOps.Application/
│   ├── FieldOps.Domain/
│   └── FieldOps.Infrastructure/
└── tests/
    ├── FieldOps.UnitTests/
    └── FieldOps.IntegrationTests/
```

Dependency direction:

```text
Domain
  ↑
Application
  ↑
Infrastructure
  ↑
API
```

More precisely:

```text
FieldOps.Domain
    no project dependencies

FieldOps.Application
    -> Domain

FieldOps.Infrastructure
    -> Application
    -> Domain

FieldOps.Api
    -> Application
    -> Infrastructure
```

The responsibilities are intentionally separated:

- **Domain** contains entities, status transitions and domain rules.
- **Application** coordinates use cases and defines repository/service contracts.
- **Infrastructure** contains Entity Framework Core, PostgreSQL persistence, migrations, repositories and external-service infrastructure.
- **API** contains HTTP controllers, dependency injection, request/response handling and hosted services.

The project deliberately avoids additional architectural frameworks such as MediatR, a generic repository abstraction, AutoMapper or a full CQRS implementation.

For the size of this system, these abstractions would add complexity without solving a concrete requirement.

---

# Database Design

The main entities are:

```text
Employee
Store
Visit
OutboxMessage
```

## Employee

Important fields include:

```text
Id
Name
Email
CountryCode
```

Employee email has a unique database constraint.

## Store

Important fields include:

```text
Id
Name
CountryCode
Latitude
Longitude
```

Latitude and longitude are validated to remain inside valid geographic ranges.

## Visit

Important fields include:

```text
Id
EmployeeId
StoreId
PlannedDate
Status
StartedAt
CompletedAt
StartLatitude
StartLongitude
Notes
CreatedAt
Version
```

`EmployeeId` and `StoreId` are foreign keys.

Cascade delete is intentionally disabled for Visit relations because deleting an employee or store should not silently delete historical visit data.

Visit status is stored as a readable string:

```text
Planned
InProgress
Completed
Cancelled
```

`Version` is used for optimistic concurrency control.

## Active Visit Uniqueness

A worker must not have multiple active visits to the same store on the same planned local date.

The database is the authoritative enforcement point:

```sql
CREATE UNIQUE INDEX ux_visits_active_employee_store_planned_date
ON visits (employee_id, store_id, planned_date)
WHERE status IN ('Planned', 'InProgress');
```

Cancelled and Completed visits do not prevent a new active visit from being created.

This rule is enforced at database level instead of relying only on an application-side `SELECT`, because multiple application instances could attempt the same insert concurrently.

---

# Visit Lifecycle

Supported state transitions are:

```text
Planned
 ├──> InProgress
 └──> Cancelled

InProgress
 ├──> Completed
 └──> Cancelled
```

`Completed` and `Cancelled` are terminal states.

Invalid transitions are rejected.

---

# Visit Start and Distance Validation

A visit may only be started when its current status is `Planned`.

The client sends its current latitude and longitude.

The distance between the reported location and store location is calculated using the Haversine formula.

The start operation is accepted only when the employee is at most 200 meters from the store.

Haversine was chosen instead of introducing PostGIS because the requirement is a simple point-to-point distance validation rather than large-scale spatial querying.

The database still stores normal latitude and longitude values.

---

# Timezone Strategy

FieldOps separates **business dates** from **absolute timestamps**.

## PlannedDate

`PlannedDate` represents a local business calendar date and is stored as a PostgreSQL `date`.

For example:

```text
2026-08-15
```

It does not represent an instant in UTC.

This makes same-day visit rules independent from UTC midnight boundaries.

## Timestamps

Actual moments such as:

```text
StartedAt
CompletedAt
CreatedAt
```

are stored as UTC timestamps using PostgreSQL `timestamptz`.

API timestamps are returned in UTC using the `Z` suffix.

Example:

```text
2026-08-15T08:30:00Z
```

## Country-to-timezone mapping

The supported business timezone mapping is:

```text
TR -> Europe/Istanbul
DE -> Europe/Berlin
UK -> Europe/London
AE -> Asia/Dubai
```

IANA timezone identifiers are used instead of hard-coded UTC offsets because offsets may vary due to daylight-saving rules.

A visit is **not** required to have `PlannedDate` equal to today's date in order to be started. The case only requires the Visit to be in `Planned` status and within the distance limit.

---

# Concurrency Strategy

Two different concurrency problems are handled separately.

## Duplicate Visit Creation

Application-side duplicate checking improves error handling, but it is not sufficient for correctness.

Two application instances could execute:

```text
check -> no visit exists
check -> no visit exists
insert
insert
```

at the same time.

Therefore the partial unique PostgreSQL index is the final authority.

A violation of the active-visit uniqueness constraint is translated to:

```text
409 Conflict
```

with a machine-readable error code.

## Stale Updates

Visits contain a numeric `Version` concurrency token.

Operations that depend on an already-read version can detect when another request has modified the Visit first.

For example:

```text
10:00 Manager reads Version 3
10:05 Employee completes Visit -> Version 4
10:07 Manager attempts cancellation using Version 3
```

The cancellation is rejected with:

```text
409 Conflict
```

instead of overwriting the newer state.

This is optimistic concurrency: locks are not held while users view records.

---

# Completion Idempotency

Visit completion is intentionally idempotent.

A mobile client may send:

```text
POST /complete
```

and the server may successfully process the request while the network connection is lost before the response reaches the device.

The client can therefore retry the same request.

If the Visit is already `Completed`, the server returns the current completed representation instead of producing another state change.

The retry does not:

```text
change CompletedAt again
increment Version again
create another outbox event
```

This prevents duplicate side effects.

---

# Analytics Integration and Transactional Outbox

Completing a Visit must not depend on the Analytics service being online.

Calling Analytics before committing the Visit would create two possible failures:

```text
Analytics succeeds, database fails
```

or:

```text
Database succeeds, Analytics fails
```

To avoid coupling the database transaction to the external service, FieldOps uses the **Transactional Outbox Pattern**.

During completion:

```text
Visit -> Completed
OutboxMessage -> created
```

are committed in the same PostgreSQL transaction.

Only after the transaction succeeds does a background worker attempt delivery to Analytics.

The worker uses retry scheduling and claim/lock information such as:

```text
NextAttemptAt
LockedUntil
```

so failed deliveries can be retried without blocking the Visit completion API.

This provides **at-least-once delivery** semantics.

A production Analytics consumer should therefore also process events idempotently.

The case uses `https://analytics.example.com/events` as a placeholder service. A real Analytics implementation is intentionally not part of this repository.

---

# API Error Strategy

Successful API responses return normal response DTOs.

Failures use `ProblemDetails` together with machine-readable error codes.

Important examples include:

```text
duplicate_visit
invalid_visit_status
concurrency_conflict
```

Typical HTTP mappings are:

```text
400  Invalid request / validation
404  Resource not found
409  Duplicate or concurrency conflict
422  Business validation such as excessive start distance
500  Unexpected server error
```

Entities are not exposed directly through the API. Request and response DTOs are mapped explicitly.

---

# Visit Listing and Pagination

The Visit list supports:

```text
employeeId
storeId
status
countryCode
startDate
endDate
page
pageSize
```

Default page size:

```text
20
```

Maximum page size:

```text
100
```

The API requests `pageSize + 1` rows to determine whether another page exists without performing an expensive `COUNT(*)` for every request.

Completed visits are ordered by:

```text
CompletedAt DESC
Id DESC
```

Other visits use:

```text
PlannedDate DESC
Id DESC
```

`Id` provides deterministic ordering when multiple rows have the same date or timestamp.

Offset pagination was chosen because the assessment API explicitly exposes `page` and `pageSize` and the UI requires conventional page navigation.

For extremely deep pagination on a production dataset, cursor/keyset pagination would scale better.

---

# Index Strategy and Performance

The expected production-scale scenario is approximately:

```text
50,000 Employees
100,000 Stores
10,000,000 Visits
```

The most important read scenario is:

> Fetch a specific employee's completed visits in Turkey during the last 30 days, newest first.

The main supporting index is:

```sql
CREATE INDEX ix_visits_completed_employee_completed_at
ON visits (
    employee_id,
    completed_at DESC,
    id DESC
)
WHERE status = 'Completed';
```

This is a partial index because the target query only needs completed visits.

It keeps the index smaller than indexing every Visit status while aligning the index order with:

```text
employee equality filter
completed timestamp ordering
deterministic Id ordering
```

Other list-oriented indexes include:

```sql
CREATE INDEX ix_visits_employee_planned_date
ON visits (
    employee_id,
    planned_date DESC,
    id DESC
);

CREATE INDEX ix_visits_store_planned_date
ON visits (
    store_id,
    planned_date DESC,
    id DESC
);

CREATE INDEX ix_stores_country_code
ON stores (country_code);
```

The application also avoids unnecessary Entity Framework tracking for read-only queries by using projection and no-tracking queries.

Filtering and pagination are performed in PostgreSQL rather than in application memory.

## Representative SQL

The high-volume query is conceptually equivalent to:

```sql
SELECT
    v.id,
    v.employee_id,
    v.store_id,
    v.planned_date,
    v.status,
    v.started_at,
    v.completed_at,
    v.notes
FROM visits AS v
INNER JOIN stores AS s
    ON s.id = v.store_id
WHERE v.employee_id = @employee_id
  AND s.country_code = 'TR'
  AND v.status = 'Completed'
  AND v.completed_at >= @from_utc
  AND v.completed_at < @to_utc
ORDER BY
    v.completed_at DESC,
    v.id DESC
LIMIT @page_size
OFFSET @offset;
```

A benchmark dataset containing approximately 10 million Visit rows was also used during development.

The critical query was inspected using PostgreSQL `EXPLAIN ANALYZE` and completed in approximately tens of milliseconds on the development environment after warm-up.

The benchmark database is development-only and is not included in the deployed demo environment.

---

# Testing Strategy

The backend uses both unit tests and integration tests.

## Unit Tests

Unit tests focus on deterministic business logic such as:

```text
status transitions
distance calculation
domain validation
application rules
```

## Integration Tests

Integration tests use:

```text
ASP.NET Core WebApplicationFactory
PostgreSQL Testcontainers
```

A real PostgreSQL instance is used instead of replacing the database with an in-memory provider.

This is important because several critical behaviors depend specifically on PostgreSQL:

```text
partial indexes
unique constraints
concurrency behavior
transactions
EF Core PostgreSQL mappings
```

The completed backend suite contains 153 automated tests covering the critical business and persistence scenarios.

100% code coverage was not treated as the goal. Tests were prioritized around correctness-sensitive behavior.

---

# Frontend Architecture

The frontend is intentionally lightweight.

Main technologies:

```text
React
TypeScript
TanStack Query
Native Fetch API
Plain CSS
```

The application provides:

```text
Visit list
Filtering
Pagination
Visit creation
Visit details
Visit start
Visit completion
Loading states
Empty states
Validation
API error handling
```

TanStack Query is used for server-state caching and invalidation.

After state-changing operations, the relevant Visit detail is updated and list data is invalidated so the UI retrieves the current server state.

Visit creation uses numeric Employee and Store IDs because the case API does not expose Employee or Store catalogue endpoints. The list and detail views show these IDs alongside names so users can identify valid values without deriving incomplete dropdown options from the current Visit page.

After a Visit is created, the frontend writes the returned Visit into the detail cache, invalidates all Visit list queries, clears filters, returns pagination to page 1 and opens the new `Planned` Visit directly. This makes the next valid action, `Start Visit`, immediately available without relying on the new row's position in the refreshed list.

The project intentionally does not introduce Redux, Zustand, React Router, a UI component framework or a form framework because the current interface does not require those abstractions.

---

# Offline Operation

The complete offline workflow is intentionally not implemented because the case requests a technical design rather than full implementation.

The proposed design is documented separately in:

```text
OFFLINE-DESIGN.md
```

The design covers:

```text
local client storage
synchronization
conflict resolution
duplicate requests
client/server timestamps
failed synchronization
```

---

# Deployment

The live application is deployed to a DigitalOcean Droplet running Ubuntu 24.04 LTS.

The production topology is:

```text
Internet
   |
   | 80 / 443
   v
Caddy
   |
   v
Frontend Nginx
   |
   | /api
   v
ASP.NET Core API
   |
   v
PostgreSQL
```

All four services run in Docker containers on the same Droplet.

## Public exposure

Only these public TCP ports are required:

```text
22  SSH
80  HTTP
443 HTTPS
```

Application-internal host ports are bound only to loopback:

```text
127.0.0.1:15432 -> PostgreSQL
127.0.0.1:18080 -> Backend
127.0.0.1:18081 -> Frontend
```

They are therefore not intended to be directly accessible from the public internet.

A DigitalOcean Cloud Firewall provides an additional network perimeter.

## HTTPS

The public hostname is:

```text
fieldops.161-35-23-78.sslip.io
```

Caddy terminates TLS and automatically manages the Let's Encrypt certificate.

Plain HTTP traffic is redirected to HTTPS.

Certificate state is stored in persistent Docker volumes.

## PostgreSQL persistence

PostgreSQL data is stored in a named Docker volume.

Application containers can therefore be recreated without deleting the database.

Production deployment must never use:

```bash
docker compose down -v
```

unless database destruction is explicitly intended.

## Production migration strategy

The initial demo database was deliberately bootstrapped in Development mode so EF Core could apply the versioned migrations and insert idempotent demo data.

After bootstrap, the backend was recreated with:

```text
ASPNETCORE_ENVIRONMENT=Production
```

while preserving the PostgreSQL volume.

Automatic production schema mutation is intentionally disabled.

For future schema changes, migrations should be applied as an explicit deployment step before starting the new backend version.

## Frontend-only deployment

A separate script is provided:

```bash
./scripts/deploy-frontend.sh
```

It fetches the current `main` branch, builds the frontend image and updates the frontend service without intentionally rebuilding the backend or database.

This keeps UI-only deployment independent from the backend and PostgreSQL lifecycle.

---

# Assumptions

Important assumptions made during implementation:

1. `PlannedDate` is a business calendar date, not a UTC timestamp.
2. Starting a Visit does not require `PlannedDate` to equal the current date.
3. A Visit can only be started from `Planned`.
4. A Visit can only be completed from `InProgress`.
5. Completed and Cancelled Visits are terminal.
6. Cancelled or Completed visits do not block the creation of another active Visit for the same employee, store and planned date.
7. Store country determines the business timezone used for local-date interpretation.
8. All actual instants are stored in UTC.
9. Analytics delivery is at-least-once, so the downstream consumer should be idempotent.
10. Authentication and authorization were not implemented because they are outside the assessment scope.

---

# Trade-offs

Several choices intentionally favor simplicity and correctness over additional infrastructure.

### Layered monolith instead of microservices

The current domain does not justify distributed services. A monolith preserves transactional consistency and keeps deployment and debugging straightforward.

### PostgreSQL constraint instead of application-only duplicate checking

This adds database-specific logic but provides correctness across concurrent requests and multiple application instances.

### Optimistic concurrency instead of long database locks

Users may keep screens open for minutes. Holding database locks during that time would reduce scalability and increase operational risk.

### Transactional outbox instead of synchronous Analytics calls

This introduces an outbox table and worker but prevents an external service outage from breaking Visit completion.

### Haversine instead of PostGIS

PostGIS would provide richer spatial capabilities, but the case only requires validation against one store coordinate with a 200-meter threshold.

### Offset pagination instead of keyset pagination

Offset pagination matches the required `page` / `pageSize` API and keeps the UI simple. Very deep pages would be more efficient with keyset pagination.

### Single Droplet instead of managed/high-availability infrastructure

For a short-lived assessment environment, running Caddy, frontend, backend and PostgreSQL on one Droplet minimizes cost and operational complexity.

A production commercial deployment would likely separate PostgreSQL, introduce backups and monitoring/alerting, and provide redundancy.

### No distributed cache

The measured query performance did not justify Redis or another cache layer. Introducing a cache before demonstrating a real bottleneck would add invalidation and operational complexity.

---

# Known Limitations

This repository is intentionally scoped to the technical assessment.

Notable limitations include:

- Authentication and authorization are not implemented.
- The Analytics URL is a placeholder service supplied by the assessment.
- Full offline synchronization is documented but not implemented.
- The current deployment uses a single server and is not highly available.
- Automated database backups are not enabled for the demo environment.
- Offset pagination becomes less efficient for extremely deep pages.
- The frontend does not currently include its own automated test suite.
- Production database migrations require an explicit deployment step.

---

# Additional Documentation

Additional assessment documents are located in the repository root:

```text
AI-USAGE.md
CODE-REVIEW.md
OFFLINE-DESIGN.md
```

`AI-USAGE.md` explains where AI assistance was used and how generated suggestions were reviewed.

`CODE-REVIEW.md` contains the production review of the supplied Visit completion implementation.

`OFFLINE-DESIGN.md` describes the proposed mobile offline and synchronization architecture.
