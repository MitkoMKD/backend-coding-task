using Claims.Application;
using Claims.Models;
using Xunit;

namespace Claims.Tests;

public class PremiumCalculatorTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1375)]
    [InlineData(30, 41250)]
    [InlineData(31, 42556.25)]
    [InlineData(180, 237187.5)]
    [InlineData(181, 238452.5)]
    public void Yacht_uses_progressive_bands(int days, decimal expected)
    {
        var start = new DateOnly(2028, 1, 1);
        Assert.Equal(expected, PremiumCalculator.Calculate(start, start.AddDays(days), CoverType.Yacht));
    }

    [Theory]
    [InlineData(CoverType.PassengerShip, 1500)]
    [InlineData(CoverType.Tanker, 1875)]
    [InlineData(CoverType.ContainerShip, 1625)]
    [InlineData(CoverType.BulkCarrier, 1625)]
    public void Other_types_use_their_daily_rate(CoverType type, decimal dailyRate)
    {
        var start = new DateOnly(2028, 1, 1);
        foreach (var days in new[] { 0, 1, 30, 31, 180, 181, 366 })
        {
            var expected = dailyRate * (Math.Min(days, 30) + Math.Clamp(days - 30, 0, 150) * 0.98m
                + Math.Max(days - 180, 0) * 0.97m);
            Assert.Equal(expected, PremiumCalculator.Calculate(start, start.AddDays(days), type));
        }
    }

    [Fact]
    public void Reversed_dates_are_rejected()
    {
        var start = new DateOnly(2028, 1, 1);
        Assert.Throws<ArgumentException>(() => PremiumCalculator.Calculate(start, start.AddDays(-1), CoverType.Yacht));
    }

    [Fact]
    public void Unknown_type_is_rejected()
    {
        var start = new DateOnly(2028, 1, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => PremiumCalculator.Calculate(start, start.AddDays(1), (CoverType)99));
    }
}