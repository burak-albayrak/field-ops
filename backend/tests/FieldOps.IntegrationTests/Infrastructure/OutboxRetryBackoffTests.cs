using FieldOps.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Options;

namespace FieldOps.IntegrationTests.Infrastructure;

public class OutboxRetryBackoffTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(int.MaxValue, 300)]
    public void Delay_is_exponential_and_safely_capped(int attemptCount, int expectedSeconds)
    {
        var calculator = new OutboxRetryBackoff(Options.Create(new OutboxProcessingOptions
        {
            BaseRetrySeconds = 5,
            MaxRetrySeconds = 300
        }));

        var delay = calculator.CalculateDelay(attemptCount);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }
}
