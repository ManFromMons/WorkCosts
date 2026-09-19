namespace WorkCosts.Models;

public enum GarageJobTargetKind
{
    Car = 0,
    Engine = 1,
}

public enum GarageJobRepeatKind
{
    TimePeriod = 0,
    Distance = 1,
}

public enum GarageJobTimeUnit
{
    Days = 0,
    Weeks = 1,
    Months = 2,
    Years = 3,
}

public enum GarageJobDistanceUnit
{
    Miles = 0,
    Kilometres = 1,
}

public enum GarageJobRepeatCombine
{
    WhicheverFirst = 0,
    AllMustBeMet = 1,
}
