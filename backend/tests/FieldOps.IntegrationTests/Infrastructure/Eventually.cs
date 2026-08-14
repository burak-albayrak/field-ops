using System.Diagnostics;

namespace FieldOps.IntegrationTests.Infrastructure;

public static class Eventually
{
    public static async Task UntilAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        string description,
        TimeSpan? pollingInterval = null)
    {
        var interval = pollingInterval ?? TimeSpan.FromMilliseconds(50);
        var stopwatch = Stopwatch.StartNew();
        Exception? lastException = null;

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                if (await condition())
                {
                    return;
                }

                lastException = null;
            }
            catch (Exception exception)
            {
                // Worker ve test sorgusu kısa süre aynı satıra dokunabilir; timeout'a kadar son tanısal hata korunur.
                lastException = exception;
            }

            await Task.Delay(interval);
        }

        var detail = lastException is null
            ? string.Empty
            : $" Last observed error: {lastException.Message}";
        throw new TimeoutException($"Timed out waiting for {description}.{detail}");
    }
}
