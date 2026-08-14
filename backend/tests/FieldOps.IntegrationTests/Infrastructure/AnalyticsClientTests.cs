using System.Net;
using FieldOps.Infrastructure.Analytics;
using FieldOps.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace FieldOps.IntegrationTests.Infrastructure;

public class AnalyticsClientTests
{
    private const string Payload =
        "{\"type\":\"VisitCompleted\",\"visitId\":42,\"employeeId\":7,\"storeId\":9}";

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task Any_2xx_response_is_success_and_exact_payload_is_posted(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler((request, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)));
        using var httpClient = CreateHttpClient(handler);
        var client = new AnalyticsClient(httpClient, NullLogger<AnalyticsClient>.Instance);

        var result = await client.SendAsync(CreateMessage());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(new Uri("https://analytics.example.com/events"), handler.RequestUri);
        Assert.Equal("application/json", handler.ContentType);
        Assert.Equal(Payload, handler.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Found, "Analytics returned HTTP 302.")]
    [InlineData(HttpStatusCode.InternalServerError, "Analytics returned HTTP 500.")]
    [InlineData(HttpStatusCode.BadRequest, "Analytics returned HTTP 400.")]
    public async Task Non_2xx_response_is_retryable_failure(HttpStatusCode statusCode, string expectedError)
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)));
        using var httpClient = CreateHttpClient(handler);
        var client = new AnalyticsClient(httpClient, NullLogger<AnalyticsClient>.Instance);

        var result = await client.SendAsync(CreateMessage());

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public async Task Network_exception_is_retryable_failure()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("connection refused")));
        using var httpClient = CreateHttpClient(handler);
        var client = new AnalyticsClient(httpClient, NullLogger<AnalyticsClient>.Instance);

        var result = await client.SendAsync(CreateMessage());

        Assert.False(result.IsSuccess);
        Assert.Equal("Analytics request failed.", result.Error);
    }

    [Fact]
    public async Task Request_timeout_is_retryable_failure()
    {
        var timeoutToken = new CancellationToken(canceled: true);
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromCanceled<HttpResponseMessage>(timeoutToken));
        using var httpClient = CreateHttpClient(handler);
        var client = new AnalyticsClient(httpClient, NullLogger<AnalyticsClient>.Instance);

        var result = await client.SendAsync(CreateMessage());

        Assert.False(result.IsSuccess);
        Assert.Equal("Analytics request timed out.", result.Error);
    }

    [Fact]
    public async Task Explicit_caller_cancellation_is_propagated()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = CreateHttpClient(handler);
        var client = new AnalyticsClient(httpClient, NullLogger<AnalyticsClient>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SendAsync(CreateMessage(), cancellation.Token));
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://analytics.example.com/"),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    private static ClaimedOutboxMessage CreateMessage()
    {
        return new ClaimedOutboxMessage(
            1,
            OutboxWriter.VisitCompletedType,
            Payload,
            0,
            new DateTime(2026, 8, 14, 12, 0, 30, DateTimeKind.Utc));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? ContentType { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return await _responseFactory(request, cancellationToken);
        }
    }
}
