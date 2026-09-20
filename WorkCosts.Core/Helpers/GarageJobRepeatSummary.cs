using System.Globalization;
using WorkCosts.Models;

namespace WorkCosts.Helpers;

public static class GarageJobRepeatSummary
{
    public static string Format(
        IReadOnlyList<GarageJobRepeatCondition> conditions,
        GarageJobRepeatCombine combine)
    {
        if (conditions.Count == 0)
        {
            return string.Empty;
        }

        var parts = conditions
            .OrderBy(c => c.SortOrder)
            .Select(FormatCondition)
            .Where(p => p.Length > 0)
            .ToList();

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        var joined = combine switch
        {
            GarageJobRepeatCombine.AllMustBeMet => string.Join(" and ", parts),
            _ => string.Join(" or ", parts),
        };

        return $"Every {joined}";
    }

    private static string FormatCondition(GarageJobRepeatCondition condition)
    {
        if (!GarageJobRepeatValidation.IsValidAmount(condition.Amount))
        {
            return string.Empty;
        }

        return condition.Kind switch
        {
            GarageJobRepeatKind.TimePeriod when GarageJobRepeatValidation.IsValidTimeUnit(condition.Unit)
                => FormatTime(condition.Amount, (GarageJobTimeUnit)condition.Unit),
            GarageJobRepeatKind.Distance when GarageJobRepeatValidation.IsValidDistanceUnit(condition.Unit)
                => FormatDistance(condition.Amount, (GarageJobDistanceUnit)condition.Unit),
            _ => string.Empty,
        };
    }

    private static string FormatTime(int amount, GarageJobTimeUnit unit) =>
        unit switch
        {
            GarageJobTimeUnit.Days => amount == 1 ? "1 day" : $"{amount.ToString(CultureInfo.InvariantCulture)} days",
            GarageJobTimeUnit.Weeks => amount == 1 ? "1 wk" : $"{amount.ToString(CultureInfo.InvariantCulture)} wk",
            GarageJobTimeUnit.Months => amount == 1 ? "1 mo" : $"{amount.ToString(CultureInfo.InvariantCulture)} mo",
            GarageJobTimeUnit.Years => amount == 1 ? "1 yr" : $"{amount.ToString(CultureInfo.InvariantCulture)} yr",
            _ => string.Empty,
        };

    private static string FormatDistance(int amount, GarageJobDistanceUnit unit)
    {
        var n = amount.ToString(CultureInfo.InvariantCulture);
        return unit switch
        {
            GarageJobDistanceUnit.Miles => $"{n} mi",
            GarageJobDistanceUnit.Kilometres => $"{n} km",
            _ => string.Empty,
        };
    }
}
