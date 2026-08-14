using FieldOps.Application.Common.Geography;

namespace FieldOps.UnitTests.Geography;

public class HaversineDistanceCalculatorTests
{
    [Fact]
    public void CalculateMeters_returns_zero_for_the_same_coordinate()
    {
        var distance = HaversineDistanceCalculator.CalculateMeters(
            41.0082,
            28.9784,
            41.0082,
            28.9784);

        Assert.Equal(0d, distance);
    }

    [Fact]
    public void CalculateMeters_calculates_a_known_short_distance_with_tolerance()
    {
        var distance = HaversineDistanceCalculator.CalculateMeters(
            0d,
            0d,
            0d,
            0.00089932d);

        Assert.InRange(distance, 99d, 101d);
    }

    [Fact]
    public void CalculateMeters_distinguishes_positions_on_both_sides_of_two_hundred_metres()
    {
        var belowBoundary = HaversineDistanceCalculator.CalculateMeters(0d, 0d, 0d, 0.0017d);
        var aboveBoundary = HaversineDistanceCalculator.CalculateMeters(0d, 0d, 0d, 0.0019d);

        Assert.InRange(belowBoundary, 188d, 190d);
        Assert.InRange(aboveBoundary, 210d, 212d);
    }

    [Fact]
    public void CalculateMeters_is_symmetric()
    {
        var forward = HaversineDistanceCalculator.CalculateMeters(41.0082, 28.9784, 41.015, 28.9795);
        var reverse = HaversineDistanceCalculator.CalculateMeters(41.015, 28.9795, 41.0082, 28.9784);

        Assert.Equal(forward, reverse, 8);
    }

    [Fact]
    public void CalculateMeters_returns_a_sensible_large_city_distance()
    {
        var distance = HaversineDistanceCalculator.CalculateMeters(
            51.5074,
            -0.1278,
            48.8566,
            2.3522);

        Assert.InRange(distance, 340_000d, 350_000d);
    }
}
