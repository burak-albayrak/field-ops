using FieldOps.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FieldOps.IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;

    protected IntegrationTestBase(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    protected HttpClient Client => _fixture.Client;

    public Task InitializeAsync()
    {
        return ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    protected Task ResetDatabaseAsync()
    {
        return _fixture.ResetDatabaseAsync();
    }

    protected async Task ExecuteDbContextAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await action(context);
    }

    protected async Task<TResult> ExecuteDbContextAsync<TResult>(Func<AppDbContext, Task<TResult>> action)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await action(context);
    }
}
