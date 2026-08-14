using FieldOps.Application.Common.Exceptions;
using FieldOps.Domain.Entities;
using FieldOps.Domain.Enums;
using FieldOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldOps.IntegrationTests.Infrastructure;

public sealed class ConcurrencyTokenTests : IntegrationTestBase
{
    private readonly PostgreSqlFixture _fixture;

    public ConcurrencyTokenTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Stale_visit_update_is_translated_to_concurrency_conflict()
    {
        var visitId = await ArrangeInProgressVisitAsync();

        await using var scopeA = _fixture.Factory.Services.CreateAsyncScope();
        await using var scopeB = _fixture.Factory.Services.CreateAsyncScope();
        var contextA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var contextB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
        var visitA = await contextA.Visits.SingleAsync(visit => visit.Id == visitId);
        var visitB = await contextB.Visits.SingleAsync(visit => visit.Id == visitId);

        visitA.Complete(
            new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
            "Winning completion");
        await contextA.SaveChangesAsync();

        visitB.Complete(
            new DateTime(2026, 8, 20, 9, 5, 0, DateTimeKind.Utc),
            "Stale completion");

        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => contextB.SaveChangesAsync());

        Assert.IsType<DbUpdateConcurrencyException>(exception.InnerException);

        var persisted = await ExecuteDbContextAsync(context => context.Visits
            .AsNoTracking()
            .SingleAsync(visit => visit.Id == visitId));
        Assert.Equal(VisitStatus.Completed, persisted.Status);
        Assert.Equal("Winning completion", persisted.Notes);
        Assert.Equal(new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc), persisted.CompletedAt);
        Assert.Equal(3, persisted.Version);
    }

    private Task<long> ArrangeInProgressVisitAsync()
    {
        return ExecuteDbContextAsync(async context =>
        {
            var employee = new Employee("Concurrency Employee", "concurrency@example.com", "TR");
            var store = new Store("Concurrency Store", "TR", 39.9334, 32.8597);
            context.Employees.Add(employee);
            context.Stores.Add(store);
            await context.SaveChangesAsync();

            var visit = new Visit(
                employee.Id,
                store.Id,
                new DateOnly(2026, 8, 20),
                new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc));
            visit.Start(
                new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc),
                39.9335,
                32.8598);
            context.Visits.Add(visit);
            await context.SaveChangesAsync();

            return visit.Id;
        });
    }
}
