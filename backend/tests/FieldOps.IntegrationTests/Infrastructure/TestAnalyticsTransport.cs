using System.Collections.Concurrent;
using System.Net;

namespace FieldOps.IntegrationTests.Infrastructure;

public sealed class TestAnalyticsTransport
{
    private readonly Func<int, CancellationToken, Task<HttpResponseMessage>> _responseFactory;
    private readonly ConcurrentQueue<CapturedAnalyticsRequest> _requests = new();
    private int _requestCount;

    private TestAnalyticsTransport(
        Func<int, CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    public int RequestCount => Volatile.Read(ref _requestCount);

    public IReadOnlyList<CapturedAnalyticsRequest> Requests => _requests.ToArray();

    public Task FirstRequestObserved => FirstRequestSource.Task;

    public Task CancellationObserved => CancellationSource.Task;

    private TaskCompletionSource FirstRequestSource { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TaskCompletionSource CancellationSource { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static TestAnalyticsTransport Always(HttpStatusCode statusCode)
    {
        return new TestAnalyticsTransport((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)));
    }

    public static TestAnalyticsTransport FailThenSucceed(int failureCount)
    {
        return new TestAnalyticsTransport((attempt, _) =>
        {
            var statusCode = attempt <= failureCount
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.NoContent;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        });
    }

    public static TestAnalyticsTransport LoseFirstAcknowledgementThenSucceed()
    {
        return new TestAnalyticsTransport((attempt, _) =>
        {
            if (attempt == 1)
            {
                // Downstream'un isteği işlediği fakat cevabın kaybolduğu at-least-once sınırını taklit eder.
                return Task.FromException<HttpResponseMessage>(
                    new HttpRequestException("simulated acknowledgement loss"));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
    }

    public static TestAnalyticsTransport BlockUntilCancellation()
    {
        return new TestAnalyticsTransport(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
    }

    internal async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var attempt = Interlocked.Increment(ref _requestCount);
        _requests.Enqueue(new CapturedAnalyticsRequest(
            attempt,
            request.Method,
            request.RequestUri,
            request.Content?.Headers.ContentType?.MediaType,
            body,
            DateTime.UtcNow));
        FirstRequestSource.TrySetResult();

        try
        {
            return await _responseFactory(attempt, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            CancellationSource.TrySetResult();
            throw;
        }
    }

    private sealed class TestAnalyticsHandler : HttpMessageHandler
    {
        private readonly TestAnalyticsTransport _transport;

        public TestAnalyticsHandler(TestAnalyticsTransport transport)
        {
            _transport = transport;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _transport.SendAsync(request, cancellationToken);
        }
    }

    internal HttpMessageHandler CreateHandler()
    {
        return new TestAnalyticsHandler(this);
    }
}

public class CapturedAnalyticsRequest
{
    public CapturedAnalyticsRequest(
        int attempt,
        HttpMethod method,
        Uri? requestUri,
        string? contentType,
        string? body,
        DateTime observedAtUtc)
    {
        Attempt = attempt;
        Method = method;
        RequestUri = requestUri;
        ContentType = contentType;
        Body = body;
        ObservedAtUtc = observedAtUtc;
    }

    public int Attempt { get; }

    public HttpMethod Method { get; }

    public Uri? RequestUri { get; }

    public string? ContentType { get; }

    public string? Body { get; }

    public DateTime ObservedAtUtc { get; }
}
