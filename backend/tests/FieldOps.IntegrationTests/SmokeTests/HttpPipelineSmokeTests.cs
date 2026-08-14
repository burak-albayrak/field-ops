using System.Net;
using FieldOps.IntegrationTests.Infrastructure;

namespace FieldOps.IntegrationTests.SmokeTests;

public sealed class HttpPipelineSmokeTests : IntegrationTestBase
{
    public HttpPipelineSmokeTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Unknown_api_route_returns_not_found()
    {
        var response = await Client.GetAsync("/api/__integration-test-missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
