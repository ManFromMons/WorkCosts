using WorkCosts.Models;

namespace WorkCosts.Helpers;

public static class GarageJobRepeatValidation
{
    public static bool IsValidAmount(int amount) => amount > 0;

    public static bool IsValidTimeUnit(int unit) =>
        Enum.IsDefined(typeof(GarageJobTimeUnit), unit);

    public static bool IsValidDistanceUnit(int unit) =>
        Enum.IsDefined(typeof(GarageJobDistanceUnit), unit);

    public static bool IsValidUnitForKind(GarageJobRepeatKind kind, int unit) =>
        kind switch
        {
            GarageJobRepeatKind.TimePeriod => IsValidTimeUnit(unit),
            GarageJobRepeatKind.Distance => IsValidDistanceUnit(unit),
            _ => false,
        };
}
