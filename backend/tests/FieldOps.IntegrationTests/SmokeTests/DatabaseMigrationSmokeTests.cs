using FieldOps.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.IntegrationTests.SmokeTests;

public sealed class DatabaseMigrationSmokeTests : IntegrationTestBase
{
    public DatabaseMigrationSmokeTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Testcontainer_database_has_applied_migrations_and_no_pending_migrations()
    {
        var appliedMigrations = await ExecuteDbContextAsync(context => context.Database.GetAppliedMigrationsAsync());
        var pendingMigrations = await ExecuteDbContextAsync(context => context.Database.GetPendingMigrationsAsync());

        Assert.Contains("20260813202257_InitialCreate", appliedMigrations);
        Assert.Contains("20260814130126_AddOutboxMessages", appliedMigrations);
        Assert.Empty(pendingMigrations);
    }
}
