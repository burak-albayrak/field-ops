using FieldOps.Application.Abstractions.Persistence;
using FieldOps.Application.Abstractions.Outbox;
using FieldOps.Application.Common.Exceptions;
using FieldOps.Application.Visits;
using FieldOps.Application.Visits.Models;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.Domain.Exceptions;

namespace FieldOps.UnitTests.Visits;

public class VisitServiceTests
{
    [Fact]
    public async Task CreateAsync_throws_when_employee_is_missing()
    {
        var employeeRepository = new EmployeeRepositoryFake { Exists = false };
        var service = CreateService(
            new VisitRepositoryFake(),
            employeeRepository: employeeRepository);

        var exception = await Assert.ThrowsAsync<EmployeeNotFoundException>(
            () => service.CreateAsync(new CreateVisitInput(10, 20, new DateOnly(2026, 8, 14))));

        Assert.Equal(10, exception.EmployeeId);
    }

    [Fact]
    public async Task CreateAsync_throws_when_store_is_missing()
    {
        var storeRepository = new StoreRepositoryFake { Exists = false };
        var service = CreateService(
            new VisitRepositoryFake(),
            storeRepository: storeRepository);

        var exception = await Assert.ThrowsAsync<StoreNotFoundException>(
            () => service.CreateAsync(new CreateVisitInput(10, 20, new DateOnly(2026, 8, 14))));

        Assert.Equal(20, exception.StoreId);
    }

    [Fact]
    public async Task CreateAsync_adds_saves_and_returns_the_created_detail()
    {
        var expected = CreateDetail(0);
        var visitRepository = new VisitRepositoryFake { Detail = expected };
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(visitRepository, unitOfWork: unitOfWork);

        var result = await service.CreateAsync(new CreateVisitInput(10, 20, new DateOnly(2026, 8, 14)));

        Assert.Same(expected, result);
        Assert.NotNull(visitRepository.AddedVisit);
        Assert.Equal(10, visitRepository.AddedVisit.EmployeeId);
        Assert.Equal(20, visitRepository.AddedVisit.StoreId);
        Assert.Equal(DateTimeKind.Utc, visitRepository.AddedVisit.CreatedAt.Kind);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_returns_repository_detail()
    {
        var expected = CreateDetail(1);
        var repository = new VisitRepositoryFake { Detail = expected };
        var service = CreateService(repository);

        var result = await service.GetByIdAsync(1);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetByIdAsync_throws_when_visit_is_missing()
    {
        var service = CreateService(new VisitRepositoryFake());

        var exception = await Assert.ThrowsAsync<VisitNotFoundException>(() => service.GetByIdAsync(42));

        Assert.Equal(42, exception.VisitId);
    }

    [Fact]
    public async Task StartAsync_throws_when_visit_is_missing_without_saving()
    {
        var visitRepository = new VisitRepositoryFake();
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(visitRepository, unitOfWork: unitOfWork);

        var exception = await Assert.ThrowsAsync<VisitNotFoundException>(
            () => service.StartAsync(42, new StartVisitInput(0d, 0d)));

        Assert.Equal(42, exception.VisitId);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartAsync_rejects_invalid_latitudes_without_persistence_work()
    {
        var visitRepository = new VisitRepositoryFake();
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(visitRepository, unitOfWork: unitOfWork);

        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.StartAsync(1, new StartVisitInput(91d, 0d)));
        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.StartAsync(1, new StartVisitInput(double.NaN, 0d)));

        Assert.Equal(0, visitRepository.GetByIdCallCount);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartAsync_rejects_invalid_longitudes_without_persistence_work()
    {
        var visitRepository = new VisitRepositoryFake();
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(visitRepository, unitOfWork: unitOfWork);

        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.StartAsync(1, new StartVisitInput(0d, 181d)));
        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.StartAsync(1, new StartVisitInput(0d, double.PositiveInfinity)));

        Assert.Equal(0, visitRepository.GetByIdCallCount);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartAsync_starts_a_nearby_planned_visit_saves_and_returns_detail()
    {
        var visit = CreatePlannedVisit();
        var expected = CreateDetail(1, VisitStatus.InProgress, 2);
        var visitRepository = new VisitRepositoryFake { TrackedVisit = visit, Detail = expected };
        var storeRepository = new StoreRepositoryFake { Coordinates = new StoreCoordinates(0d, 0d) };
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(visitRepository, storeRepository: storeRepository, unitOfWork: unitOfWork);
        var input = new StartVisitInput(0d, 0.0009d);
        var before = DateTime.UtcNow;

        var result = await service.StartAsync(1, input);

        var after = DateTime.UtcNow;
        Assert.Same(expected, result);
        Assert.Equal(VisitStatus.InProgress, visit.Status);
        Assert.NotNull(visit.StartedAt);
        Assert.InRange(visit.StartedAt.GetValueOrDefault(), before, after);
        Assert.Equal(input.Latitude, visit.StartLatitude.GetValueOrDefault());
        Assert.Equal(input.Longitude, visit.StartLongitude.GetValueOrDefault());
        Assert.Equal(2, visit.Version);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartAsync_rejects_a_location_more_than_200_metres_away_without_saving()
    {
        var visit = CreatePlannedVisit();
        var visitRepository = new VisitRepositoryFake { TrackedVisit = visit };
        var storeRepository = new StoreRepositoryFake { Coordinates = new StoreCoordinates(0d, 0d) };
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(visitRepository, storeRepository: storeRepository, unitOfWork: unitOfWork);

        var exception = await Assert.ThrowsAsync<VisitTooFarFromStoreException>(
            () => service.StartAsync(1, new StartVisitInput(0d, 0.0019d)));

        Assert.Equal(1, exception.VisitId);
        Assert.True(exception.DistanceMeters > exception.MaximumDistanceMeters);
        Assert.Equal(VisitStatus.Planned, visit.Status);
        Assert.Null(visit.StartedAt);
        Assert.Equal(1, visit.Version);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartAsync_propagates_domain_invalid_state_without_saving()
    {
        var visit = CreatePlannedVisit();
        visit.Start(DateTime.UtcNow, 0d, 0d);
        var visitRepository = new VisitRepositoryFake { TrackedVisit = visit };
        var storeRepository = new StoreRepositoryFake { Coordinates = new StoreCoordinates(0d, 0d) };
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(
            visitRepository,
            storeRepository: storeRepository,
            unitOfWork: unitOfWork);

        var exception = await Assert.ThrowsAsync<InvalidVisitStateException>(
            () => service.StartAsync(1, new StartVisitInput(0d, 0.0019d)));

        Assert.Equal(VisitStatus.InProgress, exception.CurrentStatus);
        Assert.Equal("Start", exception.AttemptedOperation);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartAsync_throws_when_store_coordinates_are_missing_without_saving()
    {
        var visit = CreatePlannedVisit();
        var visitRepository = new VisitRepositoryFake { TrackedVisit = visit };
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(visitRepository, unitOfWork: unitOfWork);

        var exception = await Assert.ThrowsAsync<StoreNotFoundException>(
            () => service.StartAsync(1, new StartVisitInput(0d, 0d)));

        Assert.Equal(20, exception.StoreId);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CompleteAsync_throws_when_visit_is_missing_without_saving()
    {
        var unitOfWork = new UnitOfWorkFake();
        var outboxWriter = new OutboxWriterFake();
        var service = CreateService(
            new VisitRepositoryFake(),
            unitOfWork: unitOfWork,
            outboxWriter: outboxWriter);

        var exception = await Assert.ThrowsAsync<VisitNotFoundException>(
            () => service.CompleteAsync(42, new CompleteVisitInput("Notes")));

        Assert.Equal(42, exception.VisitId);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
        Assert.Empty(outboxWriter.Events);
    }

    [Fact]
    public async Task CompleteAsync_completes_an_in_progress_visit_saves_and_returns_detail()
    {
        var visit = CreateInProgressVisit();
        var expected = CreateDetail(1, VisitStatus.Completed, 3, notes: "Completed notes");
        var visitRepository = new VisitRepositoryFake { TrackedVisit = visit, Detail = expected };
        var unitOfWork = new UnitOfWorkFake();
        var outboxWriter = new OutboxWriterFake();
        var service = CreateService(
            visitRepository,
            unitOfWork: unitOfWork,
            outboxWriter: outboxWriter);
        var before = DateTime.UtcNow;

        var result = await service.CompleteAsync(1, new CompleteVisitInput("Completed notes"));

        var after = DateTime.UtcNow;
        Assert.Same(expected, result);
        Assert.Equal(VisitStatus.Completed, visit.Status);
        Assert.NotNull(visit.CompletedAt);
        Assert.InRange(visit.CompletedAt.GetValueOrDefault(), before, after);
        Assert.Equal("Completed notes", visit.Notes);
        Assert.Equal(3, visit.Version);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);

        var stagedEvent = Assert.Single(outboxWriter.Events);
        Assert.Equal(visit.Id, stagedEvent.VisitId);
        Assert.Equal(visit.EmployeeId, stagedEvent.EmployeeId);
        Assert.Equal(visit.StoreId, stagedEvent.StoreId);
        Assert.Equal(visit.CompletedAt, stagedEvent.CompletedAt);
    }

    [Theory]
    [InlineData(VisitStatus.Planned)]
    [InlineData(VisitStatus.Cancelled)]
    public async Task CompleteAsync_propagates_invalid_state_without_saving(VisitStatus status)
    {
        var visit = CreatePlannedVisit();

        if (status == VisitStatus.Cancelled)
        {
            visit.Cancel();
        }

        var unitOfWork = new UnitOfWorkFake();
        var outboxWriter = new OutboxWriterFake();
        var service = CreateService(
            new VisitRepositoryFake { TrackedVisit = visit },
            unitOfWork: unitOfWork,
            outboxWriter: outboxWriter);

        var exception = await Assert.ThrowsAsync<InvalidVisitStateException>(
            () => service.CompleteAsync(1, new CompleteVisitInput("Notes")));

        Assert.Equal(status, exception.CurrentStatus);
        Assert.Equal("Complete", exception.AttemptedOperation);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
        Assert.Empty(outboxWriter.Events);
    }

    [Fact]
    public async Task CompleteAsync_returns_existing_completion_without_mutating_or_saving()
    {
        var visit = CreateInProgressVisit();
        var originalCompletedAt = new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);
        visit.Complete(originalCompletedAt, "Original notes");
        var expected = CreateDetail(
            1,
            VisitStatus.Completed,
            3,
            visit.StartedAt,
            originalCompletedAt,
            "Original notes");
        var visitRepository = new VisitRepositoryFake { TrackedVisit = visit, Detail = expected };
        var unitOfWork = new UnitOfWorkFake();
        var outboxWriter = new OutboxWriterFake();
        var service = CreateService(
            visitRepository,
            unitOfWork: unitOfWork,
            outboxWriter: outboxWriter);

        var result = await service.CompleteAsync(1, new CompleteVisitInput("Different notes"));

        Assert.Same(expected, result);
        Assert.Equal(originalCompletedAt, visit.CompletedAt);
        Assert.Equal("Original notes", visit.Notes);
        Assert.Equal(3, visit.Version);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
        Assert.Empty(outboxWriter.Events);
    }

    [Fact]
    public async Task CompleteAsync_returns_fresh_completed_detail_after_concurrency_conflict()
    {
        var visit = CreateInProgressVisit();
        var winningCompletedAt = new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);
        var winningDetail = CreateDetail(
            1,
            VisitStatus.Completed,
            3,
            visit.StartedAt,
            winningCompletedAt,
            "Winning notes");
        var conflict = new ConcurrencyConflictException();
        var visitRepository = new VisitRepositoryFake { TrackedVisit = visit, Detail = winningDetail };
        var unitOfWork = new UnitOfWorkFake { ExceptionToThrow = conflict };
        var outboxWriter = new OutboxWriterFake();
        var service = CreateService(
            visitRepository,
            unitOfWork: unitOfWork,
            outboxWriter: outboxWriter);

        var result = await service.CompleteAsync(1, new CompleteVisitInput("Losing notes"));

        Assert.Same(winningDetail, result);
        Assert.Equal(winningCompletedAt, result.CompletedAt);
        Assert.Equal("Winning notes", result.Notes);
        Assert.Equal(3, result.Version);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, visitRepository.GetDetailCallCount);
        Assert.Single(outboxWriter.Events);
    }

    [Fact]
    public async Task CompleteAsync_rethrows_concurrency_conflict_when_fresh_detail_is_not_completed()
    {
        var conflict = new ConcurrencyConflictException();
        var visitRepository = new VisitRepositoryFake
        {
            TrackedVisit = CreateInProgressVisit(),
            Detail = CreateDetail(1, VisitStatus.InProgress, 2)
        };
        var unitOfWork = new UnitOfWorkFake { ExceptionToThrow = conflict };
        var service = CreateService(visitRepository, unitOfWork: unitOfWork);

        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => service.CompleteAsync(1, new CompleteVisitInput("Notes")));

        Assert.Same(conflict, exception);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CompleteAsync_rethrows_concurrency_conflict_when_fresh_detail_is_missing()
    {
        var conflict = new ConcurrencyConflictException();
        var visitRepository = new VisitRepositoryFake { TrackedVisit = CreateInProgressVisit() };
        var unitOfWork = new UnitOfWorkFake { ExceptionToThrow = conflict };
        var service = CreateService(visitRepository, unitOfWork: unitOfWork);

        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => service.CompleteAsync(1, new CompleteVisitInput("Notes")));

        Assert.Same(conflict, exception);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelAsync_rejects_invalid_version_without_persistence_work()
    {
        var visitRepository = new VisitRepositoryFake();
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(visitRepository, unitOfWork: unitOfWork);

        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.CancelAsync(1, new CancelVisitInput(0)));

        Assert.Equal(0, visitRepository.GetByIdCallCount);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelAsync_throws_when_visit_is_missing_without_saving()
    {
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(new VisitRepositoryFake(), unitOfWork: unitOfWork);

        var exception = await Assert.ThrowsAsync<VisitNotFoundException>(
            () => service.CancelAsync(42, new CancelVisitInput(1)));

        Assert.Equal(42, exception.VisitId);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelAsync_cancels_a_planned_visit_saves_and_returns_detail()
    {
        var visit = CreatePlannedVisit();
        var expected = CreateDetail(1, VisitStatus.Cancelled, 2);
        var visitRepository = new VisitRepositoryFake { TrackedVisit = visit, Detail = expected };
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(visitRepository, unitOfWork: unitOfWork);

        var result = await service.CancelAsync(1, new CancelVisitInput(1));

        Assert.Same(expected, result);
        Assert.Equal(VisitStatus.Cancelled, visit.Status);
        Assert.Equal(2, visit.Version);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelAsync_cancels_an_in_progress_visit_and_preserves_start_details()
    {
        var visit = CreateInProgressVisit();
        var originalStartedAt = visit.StartedAt;
        var originalLatitude = visit.StartLatitude;
        var originalLongitude = visit.StartLongitude;
        var expected = CreateDetail(1, VisitStatus.Cancelled, 3, startedAt: originalStartedAt);
        var visitRepository = new VisitRepositoryFake { TrackedVisit = visit, Detail = expected };
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(visitRepository, unitOfWork: unitOfWork);

        var result = await service.CancelAsync(1, new CancelVisitInput(2));

        Assert.Same(expected, result);
        Assert.Equal(VisitStatus.Cancelled, visit.Status);
        Assert.Equal(originalStartedAt, visit.StartedAt);
        Assert.Equal(originalLatitude, visit.StartLatitude);
        Assert.Equal(originalLongitude, visit.StartLongitude);
        Assert.Equal(3, visit.Version);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelAsync_rejects_stale_version_before_domain_mutation()
    {
        var visit = CreateInProgressVisit();
        var originalStartedAt = visit.StartedAt;
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(
            new VisitRepositoryFake { TrackedVisit = visit },
            unitOfWork: unitOfWork);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => service.CancelAsync(1, new CancelVisitInput(1)));

        Assert.Equal(VisitStatus.InProgress, visit.Status);
        Assert.Equal(originalStartedAt, visit.StartedAt);
        Assert.Equal(2, visit.Version);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelAsync_with_matching_version_propagates_completed_state_conflict()
    {
        var visit = CreateCompletedVisit();
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(
            new VisitRepositoryFake { TrackedVisit = visit },
            unitOfWork: unitOfWork);

        var exception = await Assert.ThrowsAsync<InvalidVisitStateException>(
            () => service.CancelAsync(1, new CancelVisitInput(3)));

        Assert.Equal(VisitStatus.Completed, exception.CurrentStatus);
        Assert.Equal("Cancel", exception.AttemptedOperation);
        Assert.Equal(3, visit.Version);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelAsync_with_stale_version_prioritizes_concurrency_over_completed_state()
    {
        var visit = CreateCompletedVisit();
        var originalCompletedAt = visit.CompletedAt;
        var originalNotes = visit.Notes;
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(
            new VisitRepositoryFake { TrackedVisit = visit },
            unitOfWork: unitOfWork);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => service.CancelAsync(1, new CancelVisitInput(2)));

        Assert.Equal(VisitStatus.Completed, visit.Status);
        Assert.Equal(originalCompletedAt, visit.CompletedAt);
        Assert.Equal(originalNotes, visit.Notes);
        Assert.Equal(3, visit.Version);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelAsync_rejects_an_already_cancelled_visit_with_matching_version()
    {
        var visit = CreatePlannedVisit();
        visit.Cancel();
        var unitOfWork = new UnitOfWorkFake();
        var service = CreateService(
            new VisitRepositoryFake { TrackedVisit = visit },
            unitOfWork: unitOfWork);

        var exception = await Assert.ThrowsAsync<InvalidVisitStateException>(
            () => service.CancelAsync(1, new CancelVisitInput(2)));

        Assert.Equal(VisitStatus.Cancelled, exception.CurrentStatus);
        Assert.Equal("Cancel", exception.AttemptedOperation);
        Assert.Equal(2, visit.Version);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelAsync_propagates_save_concurrency_conflict_without_retry()
    {
        var visit = CreatePlannedVisit();
        var conflict = new ConcurrencyConflictException();
        var visitRepository = new VisitRepositoryFake { TrackedVisit = visit };
        var unitOfWork = new UnitOfWorkFake { ExceptionToThrow = conflict };
        var service = CreateService(visitRepository, unitOfWork: unitOfWork);

        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => service.CancelAsync(1, new CancelVisitInput(1)));

        Assert.Same(conflict, exception);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
        Assert.Equal(0, visitRepository.GetDetailCallCount);
    }

    [Fact]
    public async Task ListAsync_rejects_an_invalid_page()
    {
        var service = CreateService(new VisitRepositoryFake());

        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.ListAsync(new VisitListFilter(), 0, 20));
    }

    [Fact]
    public async Task ListAsync_rejects_a_reversed_date_range()
    {
        var service = CreateService(new VisitRepositoryFake());
        var filter = new VisitListFilter
        {
            StartDate = new DateOnly(2026, 8, 15),
            EndDate = new DateOnly(2026, 8, 14)
        };

        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.ListAsync(filter, 1, 20));
    }

    [Fact]
    public async Task ListAsync_requests_one_extra_item_and_reports_a_next_page()
    {
        var repository = new VisitRepositoryFake
        {
            ListItems = [CreateListItem(3), CreateListItem(2), CreateListItem(1)]
        };
        var service = CreateService(repository);

        var result = await service.ListAsync(new VisitListFilter(), 1, 2);

        Assert.Equal(0, repository.LastSkip);
        Assert.Equal(3, repository.LastTake);
        Assert.True(result.HasNextPage);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal([3L, 2L], result.Items.Select(item => item.Id));
    }

    private static VisitService CreateService(
        VisitRepositoryFake visitRepository,
        EmployeeRepositoryFake? employeeRepository = null,
        StoreRepositoryFake? storeRepository = null,
        UnitOfWorkFake? unitOfWork = null,
        OutboxWriterFake? outboxWriter = null)
    {
        return new VisitService(
            visitRepository,
            employeeRepository ?? new EmployeeRepositoryFake(),
            storeRepository ?? new StoreRepositoryFake(),
            unitOfWork ?? new UnitOfWorkFake(),
            outboxWriter ?? new OutboxWriterFake());
    }

    private static VisitDetailDto CreateDetail(
        long id,
        VisitStatus status = VisitStatus.Planned,
        long version = 1,
        DateTime? startedAt = null,
        DateTime? completedAt = null,
        string? notes = null)
    {
        return new VisitDetailDto(
            id,
            new EmployeeSummaryDto(10, "Ayşe", "ayse@example.com", "TR"),
            new StoreSummaryDto(20, "Ankara", "TR", 39.9334, 32.8597),
            new DateOnly(2026, 8, 14),
            status,
            startedAt,
            completedAt,
            null,
            null,
            notes,
            new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc),
            version);
    }

    private static Visit CreatePlannedVisit()
    {
        return new Visit(10, 20, new DateOnly(2026, 8, 14), DateTime.UtcNow);
    }

    private static Visit CreateInProgressVisit()
    {
        var visit = CreatePlannedVisit();
        visit.Start(new DateTime(2026, 8, 14, 9, 0, 0, DateTimeKind.Utc), 39.9335, 32.8598);
        return visit;
    }

    private static Visit CreateCompletedVisit()
    {
        var visit = CreateInProgressVisit();
        visit.Complete(
            new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc),
            "Completed notes");
        return visit;
    }

    private static VisitListItemDto CreateListItem(long id)
    {
        return new VisitListItemDto(
            id,
            10,
            "Ayşe",
            20,
            "Ankara",
            "TR",
            new DateOnly(2026, 8, 14),
            VisitStatus.Planned,
            null,
            null,
            1);
    }

    private sealed class VisitRepositoryFake : IVisitRepository
    {
        public VisitDetailDto? Detail { get; init; }

        public Visit? TrackedVisit { get; init; }

        public IReadOnlyList<VisitListItemDto> ListItems { get; init; } = [];

        public int LastSkip { get; private set; }

        public int LastTake { get; private set; }

        public Visit? AddedVisit { get; private set; }

        public int GetByIdCallCount { get; private set; }

        public int GetDetailCallCount { get; private set; }

        public void Add(Visit visit)
        {
            AddedVisit = visit;
        }

        public Task<VisitDetailDto?> GetDetailAsync(long id, CancellationToken cancellationToken = default)
        {
            GetDetailCallCount++;
            return Task.FromResult(Detail);
        }

        public Task<Visit?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            return Task.FromResult(TrackedVisit);
        }

        public Task<IReadOnlyList<VisitListItemDto>> ListAsync(
            VisitListFilter filter,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastSkip = skip;
            LastTake = take;
            return Task.FromResult(ListItems);
        }
    }

    private sealed class EmployeeRepositoryFake : IEmployeeRepository
    {
        public bool Exists { get; init; } = true;

        public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Exists);
        }
    }

    private sealed class StoreRepositoryFake : IStoreRepository
    {
        public bool Exists { get; init; } = true;

        public StoreCoordinates? Coordinates { get; init; }

        public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Exists);
        }

        public Task<StoreCoordinates?> GetCoordinatesAsync(long id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Coordinates);
        }
    }

    private sealed class UnitOfWorkFake : IUnitOfWork
    {
        public Exception? ExceptionToThrow { get; init; }

        public int SaveChangesCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;

            if (ExceptionToThrow is not null)
            {
                return Task.FromException<int>(ExceptionToThrow);
            }

            return Task.FromResult(1);
        }
    }

    private sealed class OutboxWriterFake : IOutboxWriter
    {
        public List<VisitCompletedEvent> Events { get; } = [];

        public void AddVisitCompleted(
            long visitId,
            long employeeId,
            long storeId,
            DateTime completedAt)
        {
            Events.Add(new VisitCompletedEvent(visitId, employeeId, storeId, completedAt));
        }
    }

    private sealed class VisitCompletedEvent
    {
        public VisitCompletedEvent(long visitId, long employeeId, long storeId, DateTime completedAt)
        {
            VisitId = visitId;
            EmployeeId = employeeId;
            StoreId = storeId;
            CompletedAt = completedAt;
        }

        public long VisitId { get; }

        public long EmployeeId { get; }

        public long StoreId { get; }

        public DateTime CompletedAt { get; }
    }
}
