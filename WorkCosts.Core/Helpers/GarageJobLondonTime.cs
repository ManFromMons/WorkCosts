using WorkCosts.Models;

namespace WorkCosts.Helpers;

public static class GarageJobLondonTime
{
    public static TimeZoneInfo TimeZone { get; } = ResolveLondon();

    public static DateTimeOffset AtStartOfDay(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return FromUnspecifiedLocal(local);
    }

    public static DateTimeOffset Convert(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, TimeZone);

    public static DateTimeOffset Add(DateTimeOffset origin, int amount, GarageJobTimeUnit unit)
    {
        var local = Convert(origin).DateTime;
        var next = unit switch
        {
            GarageJobTimeUnit.Days => local.AddDays(amount),
            GarageJobTimeUnit.Weeks => local.AddDays(7d * amount),
            GarageJobTimeUnit.Months => local.AddMonths(amount),
            GarageJobTimeUnit.Years => local.AddYears(amount),
            _ => local,
        };
        return FromUnspecifiedLocal(DateTime.SpecifyKind(next, DateTimeKind.Unspecified));
    }

    private static DateTimeOffset FromUnspecifiedLocal(DateTime unspecifiedLocal)
    {
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocal, TimeZone);
        return TimeZoneInfo.ConvertTime(new DateTimeOffset(utc, TimeSpan.Zero), TimeZone);
    }

    private static TimeZoneInfo ResolveLondon()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        }
    }
}
