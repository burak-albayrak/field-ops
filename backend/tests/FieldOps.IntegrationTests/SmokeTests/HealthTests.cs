using System.Net;
using System.Net.Http.Json;
using FieldOps.IntegrationTests.Infrastructure;

namespace FieldOps.IntegrationTests.SmokeTests;

public sealed class HealthTests : IntegrationTestBase
{
    public HealthTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Health_returns_healthy_when_PostgreSQL_is_reachable()
    {
        using var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.NotNull(body);
        Assert.Equal("Healthy", body.Status);
    }

    private sealed class HealthResponse
    {
        public string Status { get; init; } = string.Empty;
    }
}
