using Claims.Models;

namespace Claims.Application;

public static class PremiumCalculator
{
    public static decimal Calculate(DateOnly startDate, DateOnly endDate, CoverType type)
    {
        if (endDate < startDate)
            throw new ArgumentException("End date cannot precede start date.", nameof(endDate));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        var days = endDate.DayNumber - startDate.DayNumber;
        var multiplier = type switch
        {
            CoverType.Yacht => 1.1m,
            CoverType.PassengerShip => 1.2m,
            CoverType.Tanker => 1.5m,
            _ => 1.3m
        };
        var firstBand = Math.Min(days, 30);
        var secondBand = Math.Clamp(days - 30, 0, 150);
        var thirdBand = Math.Max(days - 180, 0);
        var secondRate = type == CoverType.Yacht ? 0.95m : 0.98m;
        var thirdRate = type == CoverType.Yacht ? 0.92m : 0.97m;

        return decimal.Round(1250m * multiplier *
            (firstBand + secondBand * secondRate + thirdBand * thirdRate),
            2, MidpointRounding.AwayFromZero);
    }
}