using FieldOps.Application.Abstractions.Persistence;
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
        UnitOfWorkFake? unitOfWork = null)
    {
        return new VisitService(
            visitRepository,
            employeeRepository ?? new EmployeeRepositoryFake(),
            storeRepository ?? new StoreRepositoryFake(),
            unitOfWork ?? new UnitOfWorkFake());
    }

    private static VisitDetailDto CreateDetail(long id, VisitStatus status = VisitStatus.Planned, long version = 1)
    {
        return new VisitDetailDto(
            id,
            new EmployeeSummaryDto(10, "Ayşe", "ayse@example.com", "TR"),
            new StoreSummaryDto(20, "Ankara", "TR", 39.9334, 32.8597),
            new DateOnly(2026, 8, 14),
            status,
            null,
            null,
            null,
            null,
            null,
            new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc),
            version);
    }

    private static Visit CreatePlannedVisit()
    {
        return new Visit(10, 20, new DateOnly(2026, 8, 14), DateTime.UtcNow);
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

        public void Add(Visit visit)
        {
            AddedVisit = visit;
        }

        public Task<VisitDetailDto?> GetDetailAsync(long id, CancellationToken cancellationToken = default)
        {
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
        public int SaveChangesCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }
    }
}
