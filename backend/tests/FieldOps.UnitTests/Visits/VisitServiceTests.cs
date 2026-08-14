using FieldOps.Application.Abstractions.Persistence;
using FieldOps.Application.Common.Exceptions;
using FieldOps.Application.Visits;
using FieldOps.Application.Visits.Models;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;

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

    private static VisitDetailDto CreateDetail(long id)
    {
        return new VisitDetailDto(
            id,
            new EmployeeSummaryDto(10, "Ayşe", "ayse@example.com", "TR"),
            new StoreSummaryDto(20, "Ankara", "TR", 39.9334, 32.8597),
            new DateOnly(2026, 8, 14),
            VisitStatus.Planned,
            null,
            null,
            null,
            null,
            null,
            new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc),
            1);
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

        public IReadOnlyList<VisitListItemDto> ListItems { get; init; } = [];

        public int LastSkip { get; private set; }

        public int LastTake { get; private set; }

        public Visit? AddedVisit { get; private set; }

        public void Add(Visit visit)
        {
            AddedVisit = visit;
        }

        public Task<VisitDetailDto?> GetDetailAsync(long id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Detail);
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

        public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Exists);
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
