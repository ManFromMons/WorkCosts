using WorkCosts.Helpers;
using WorkCosts.Models;
using Xunit;

namespace WorkCosts.Tests;

public sealed class GarageJobDueEvaluatorTests
{
    [Fact]
    public void LondonTime_January_IsGmtPlusZero()
    {
        var instant = GarageJobLondonTime.AtStartOfDay(new DateOnly(2026, 1, 15));
        Assert.Equal(TimeSpan.Zero, instant.Offset);
    }

    [Fact]
    public void LondonTime_July_IsBstPlusOne()
    {
        var instant = GarageJobLondonTime.AtStartOfDay(new DateOnly(2026, 7, 15));
        Assert.Equal(TimeSpan.FromHours(1), instant.Offset);
    }

    [Fact]
    public void Evaluate_NoConditions_NotScheduled()
    {
        var result = Evaluate(Array.Empty<GarageJobRepeatCondition>(), lastOccurredAt: London(2026, 1, 1));
        Assert.Equal(GarageJobDueStatus.NotScheduled, result.Status);
        Assert.True(result.HasCompletion);
        Assert.Null(result.NextDueAt);
    }

    [Fact]
    public void Evaluate_NoOrigin_DueImmediately()
    {
        var result = Evaluate([Time(12, GarageJobTimeUnit.Months)]);
        Assert.Equal(GarageJobDueStatus.DueImmediately, result.Status);
        Assert.False(result.HasCompletion);
        Assert.Null(result.NextDueAt);
        Assert.Null(result.NextDueOdometerMiles);
    }

    [Fact]
    public void Evaluate_AnchorOnly_BeforeFirstDue_NeverDone()
    {
        var asOf = London(2026, 3, 1);
        var result = Evaluate(
            [Time(12, GarageJobTimeUnit.Months)],
            asOf: asOf,
            anchor: new DateOnly(2026, 1, 1));
        Assert.Equal(GarageJobDueStatus.NeverDone, result.Status);
        Assert.False(result.HasCompletion);
        Assert.Equal(London(2027, 1, 1, 0, 0), result.NextDueAt);
        Assert.True(result.RemainingTime > TimeSpan.Zero);
    }

    [Fact]
    public void Evaluate_AnchorOnly_AtInterval_Due()
    {
        var result = Evaluate(
            [Time(12, GarageJobTimeUnit.Months)],
            asOf: London(2027, 1, 1, 0, 0),
            anchor: new DateOnly(2026, 1, 1));
        Assert.Equal(GarageJobDueStatus.Due, result.Status);
        Assert.False(result.HasCompletion);
    }

    [Fact]
    public void Evaluate_AnchorOnly_AfterInterval_Overdue()
    {
        var result = Evaluate(
            [Time(12, GarageJobTimeUnit.Months)],
            asOf: London(2027, 1, 1, 0, 0).AddSeconds(2),
            anchor: new DateOnly(2026, 1, 1));
        Assert.Equal(GarageJobDueStatus.Overdue, result.Status);
    }

    [Fact]
    public void Evaluate_CompletionOverridesAnchor_ForTimeOrigin()
    {
        var result = Evaluate(
            [Time(12, GarageJobTimeUnit.Months)],
            asOf: London(2026, 6, 1),
            lastOccurredAt: London(2026, 1, 1),
            anchor: new DateOnly(2020, 1, 1));
        Assert.Equal(GarageJobDueStatus.NotDue, result.Status);
        Assert.True(result.HasCompletion);
        Assert.Equal(London(2027, 1, 1), result.NextDueAt);
    }

    [Fact]
    public void Evaluate_TimeDays_NotDueBefore_DueAt_OverdueAfter()
    {
        var origin = London(2026, 3, 1, 12);
        var due = GarageJobLondonTime.Add(origin, 10, GarageJobTimeUnit.Days);
        Assert.Equal(
            GarageJobDueStatus.NotDue,
            Evaluate([Time(10, GarageJobTimeUnit.Days)], asOf: due.AddMinutes(-1), lastOccurredAt: origin).Status);
        Assert.Equal(
            GarageJobDueStatus.Due,
            Evaluate([Time(10, GarageJobTimeUnit.Days)], asOf: due, lastOccurredAt: origin).Status);
        Assert.Equal(
            GarageJobDueStatus.Overdue,
            Evaluate([Time(10, GarageJobTimeUnit.Days)], asOf: due.AddSeconds(2), lastOccurredAt: origin).Status);
    }

    [Fact]
    public void Evaluate_TimeWeeks_AddsSevenDaysPerWeek()
    {
        var origin = London(2026, 1, 1, 9);
        var due = GarageJobLondonTime.Add(origin, 2, GarageJobTimeUnit.Weeks);
        Assert.Equal(origin.AddDays(14), due);
        Assert.Equal(
            GarageJobDueStatus.Due,
            Evaluate([Time(2, GarageJobTimeUnit.Weeks)], asOf: due, lastOccurredAt: origin).Status);
    }

    [Fact]
    public void Evaluate_TimeMonths_EndOfMonth_Jan31PlusOneMonth_London()
    {
        var origin = London(2026, 1, 31, 12);
        var due = GarageJobLondonTime.Add(origin, 1, GarageJobTimeUnit.Months);
        Assert.Equal(new DateOnly(2026, 2, 28), DateOnly.FromDateTime(due.DateTime));
        Assert.Equal(12, due.Hour);
        Assert.Equal(
            GarageJobDueStatus.Due,
            Evaluate([Time(1, GarageJobTimeUnit.Months)], asOf: due, lastOccurredAt: origin).Status);
    }

    [Fact]
    public void Evaluate_TimeYears_LeapDay_Feb29PlusOneYear_London()
    {
        var origin = London(2024, 2, 29, 10);
        var due = GarageJobLondonTime.Add(origin, 1, GarageJobTimeUnit.Years);
        Assert.Equal(new DateOnly(2025, 2, 28), DateOnly.FromDateTime(due.DateTime));
        Assert.Equal(
            GarageJobDueStatus.Due,
            Evaluate([Time(1, GarageJobTimeUnit.Years)], asOf: due, lastOccurredAt: origin).Status);
    }

    [Fact]
    public void Evaluate_DistanceMiles_NotDueDueOverdue()
    {
        var conditions = new[] { Dist(10_000, GarageJobDistanceUnit.Miles) };
        Assert.Equal(
            GarageJobDueStatus.NotDue,
            Evaluate(conditions, lastOccurredAt: London(2026, 1, 1), lastMiles: 1000, currentMiles: 10_999).Status);
        Assert.Equal(
            GarageJobDueStatus.Due,
            Evaluate(conditions, lastOccurredAt: London(2026, 1, 1), lastMiles: 1000, currentMiles: 11_000).Status);
        Assert.Equal(
            GarageJobDueStatus.Overdue,
            Evaluate(conditions, lastOccurredAt: London(2026, 1, 1), lastMiles: 1000, currentMiles: 11_001).Status);
    }

    [Fact]
    public void Evaluate_DistanceConditionKilometres_ConvertsUsing1_609344()
    {
        var conditions = new[] { Dist(10_000, GarageJobDistanceUnit.Kilometres) };
        var intervalMiles = 10_000 * GarageJobDueEvaluator.MilesPerKilometre;
        Assert.Equal(
            GarageJobDueStatus.NotDue,
            Evaluate(conditions, lastOccurredAt: London(2026, 1, 1), lastMiles: 0, currentMiles: (int)intervalMiles).Status);
        Assert.Equal(
            GarageJobDueStatus.Due,
            Evaluate(conditions, lastOccurredAt: London(2026, 1, 1), lastMiles: 0, currentMiles: 6214).Status);
        Assert.Equal(
            GarageJobDueStatus.Overdue,
            Evaluate(conditions, lastOccurredAt: London(2026, 1, 1), lastMiles: 0, currentMiles: 6215).Status);
    }

    [Fact]
    public void Evaluate_WhicheverFirst_TimeOrDistance_DueWhenEitherMet()
    {
        var origin = London(2026, 1, 1);
        var conditions = new[]
        {
            Time(12, GarageJobTimeUnit.Months),
            Dist(10_000, GarageJobDistanceUnit.Miles, sort: 1),
        };
        Assert.Equal(
            GarageJobDueStatus.Due,
            Evaluate(conditions, asOf: origin.AddDays(1), lastOccurredAt: origin, lastMiles: 0, currentMiles: 10_000).Status);
        var yearLater = GarageJobLondonTime.Add(origin, 12, GarageJobTimeUnit.Months);
        Assert.Equal(
            GarageJobDueStatus.Due,
            Evaluate(conditions, asOf: yearLater, lastOccurredAt: origin, lastMiles: 0, currentMiles: 100).Status);
    }

    [Fact]
    public void Evaluate_AllMustBeMet_DueOnlyWhenBothMet()
    {
        var origin = London(2026, 1, 1);
        var conditions = new[]
        {
            Time(12, GarageJobTimeUnit.Months),
            Dist(10_000, GarageJobDistanceUnit.Miles, sort: 1),
        };
        var yearLater = GarageJobLondonTime.Add(origin, 12, GarageJobTimeUnit.Months);
        Assert.Equal(
            GarageJobDueStatus.NotDue,
            Evaluate(
                conditions,
                combine: GarageJobRepeatCombine.AllMustBeMet,
                asOf: yearLater,
                lastOccurredAt: origin,
                lastMiles: 0,
                currentMiles: 100).Status);
        Assert.Equal(
            GarageJobDueStatus.Due,
            Evaluate(
                conditions,
                combine: GarageJobRepeatCombine.AllMustBeMet,
                asOf: yearLater,
                lastOccurredAt: origin,
                lastMiles: 0,
                currentMiles: 10_000).Status);
    }

    [Fact]
    public void Evaluate_AllMustBeMet_MissingCurrentMiles_NotDue()
    {
        var origin = London(2026, 1, 1);
        var yearLater = GarageJobLondonTime.Add(origin, 12, GarageJobTimeUnit.Months);
        var result = Evaluate(
            [Time(12, GarageJobTimeUnit.Months), Dist(10_000, GarageJobDistanceUnit.Miles, sort: 1)],
            combine: GarageJobRepeatCombine.AllMustBeMet,
            asOf: yearLater.AddDays(1),
            lastOccurredAt: origin,
            lastMiles: 0,
            currentMiles: null);
        Assert.Equal(GarageJobDueStatus.NotDue, result.Status);
        Assert.Contains(GarageJobRepeatKind.TimePeriod, result.TriggeringKinds);
        Assert.DoesNotContain(GarageJobRepeatKind.Distance, result.TriggeringKinds);
    }

    [Fact]
    public void Evaluate_WhicheverFirst_MissingCurrentMiles_TimeCanStillDue()
    {
        var origin = London(2026, 1, 1);
        var yearLater = GarageJobLondonTime.Add(origin, 12, GarageJobTimeUnit.Months);
        var result = Evaluate(
            [Time(12, GarageJobTimeUnit.Months), Dist(10_000, GarageJobDistanceUnit.Miles, sort: 1)],
            asOf: yearLater,
            lastOccurredAt: origin,
            lastMiles: 0,
            currentMiles: null);
        Assert.Equal(GarageJobDueStatus.Due, result.Status);
        Assert.Contains(GarageJobRepeatKind.TimePeriod, result.TriggeringKinds);
    }

    [Fact]
    public void Evaluate_TwoTimeRows_WhicheverFirst_UsesSooner()
    {
        var origin = London(2026, 1, 1, 8);
        var six = GarageJobLondonTime.Add(origin, 6, GarageJobTimeUnit.Months);
        var result = Evaluate(
            [Time(6, GarageJobTimeUnit.Months), Time(12, GarageJobTimeUnit.Months, sort: 1)],
            asOf: six,
            lastOccurredAt: origin);
        Assert.Equal(GarageJobDueStatus.Due, result.Status);
        Assert.Equal(six, result.NextDueAt);
    }

    [Fact]
    public void Evaluate_TwoTimeRows_AllMustBeMet_UsesLater()
    {
        var origin = London(2026, 1, 1, 8);
        var six = GarageJobLondonTime.Add(origin, 6, GarageJobTimeUnit.Months);
        var twelve = GarageJobLondonTime.Add(origin, 12, GarageJobTimeUnit.Months);
        var atSix = Evaluate(
            [Time(6, GarageJobTimeUnit.Months), Time(12, GarageJobTimeUnit.Months, sort: 1)],
            combine: GarageJobRepeatCombine.AllMustBeMet,
            asOf: six,
            lastOccurredAt: origin);
        Assert.Equal(GarageJobDueStatus.NotDue, atSix.Status);
        Assert.Equal(twelve, atSix.NextDueAt);
        Assert.Equal(
            GarageJobDueStatus.Due,
            Evaluate(
                [Time(6, GarageJobTimeUnit.Months), Time(12, GarageJobTimeUnit.Months, sort: 1)],
                combine: GarageJobRepeatCombine.AllMustBeMet,
                asOf: twelve,
                lastOccurredAt: origin).Status);
    }

    [Fact]
    public void Evaluate_TwoDistanceRows_WhicheverFirst_UsesShorter()
    {
        var result = Evaluate(
            [Dist(8_000, GarageJobDistanceUnit.Miles), Dist(10_000, GarageJobDistanceUnit.Miles, sort: 1)],
            lastOccurredAt: London(2026, 1, 1),
            lastMiles: 0,
            currentMiles: 8_000);
        Assert.Equal(GarageJobDueStatus.Due, result.Status);
        Assert.Equal(8_000d, result.NextDueOdometerMiles);
    }

    [Fact]
    public void Evaluate_ThreeRows_TwoTimeOneDistance_WhicheverFirst()
    {
        var origin = London(2026, 1, 1);
        var six = GarageJobLondonTime.Add(origin, 6, GarageJobTimeUnit.Months);
        var result = Evaluate(
            [
                Time(12, GarageJobTimeUnit.Months),
                Dist(10_000, GarageJobDistanceUnit.Miles, sort: 1),
                Time(6, GarageJobTimeUnit.Months, sort: 2),
            ],
            asOf: six,
            lastOccurredAt: origin,
            lastMiles: 0,
            currentMiles: 100);
        Assert.Equal(GarageJobDueStatus.Due, result.Status);
        Assert.Equal(six, result.NextDueAt);
        Assert.Equal(10_000d, result.NextDueOdometerMiles);
    }

    [Fact]
    public void Evaluate_InvalidRowSkipped_ValidRowStillEvaluates()
    {
        var origin = London(2026, 1, 1);
        var due = GarageJobLondonTime.Add(origin, 12, GarageJobTimeUnit.Months);
        var result = Evaluate(
            [
                new GarageJobRepeatCondition { Kind = GarageJobRepeatKind.TimePeriod, Amount = 0, Unit = (int)GarageJobTimeUnit.Months },
                Time(12, GarageJobTimeUnit.Months, sort: 1),
            ],
            asOf: due,
            lastOccurredAt: origin);
        Assert.Equal(GarageJobDueStatus.Due, result.Status);
    }

    [Fact]
    public void Evaluate_AsOfBeforeOrigin_TimeElapsedZero()
    {
        var origin = London(2026, 6, 1);
        var result = Evaluate(
            [Time(12, GarageJobTimeUnit.Months)],
            asOf: London(2026, 1, 1),
            lastOccurredAt: origin);
        Assert.Equal(GarageJobDueStatus.NotDue, result.Status);
        Assert.True(result.RemainingTime > TimeSpan.Zero);
    }

    [Fact]
    public void Evaluate_OdometerWentBackwards_DistanceElapsedZero()
    {
        var result = Evaluate(
            [Dist(1_000, GarageJobDistanceUnit.Miles)],
            lastOccurredAt: London(2026, 1, 1),
            lastMiles: 5_000,
            currentMiles: 4_000);
        Assert.Equal(GarageJobDueStatus.NotDue, result.Status);
        Assert.Equal(6_000d, result.NextDueOdometerMiles);
        Assert.Equal(2_000d, result.RemainingDistanceMiles);
    }

    [Fact]
    public void Evaluate_DurationMinutesIgnored()
    {
        var origin = London(2026, 1, 1);
        var result = Evaluate(
            [Time(12, GarageJobTimeUnit.Months)],
            asOf: origin.AddHours(3),
            lastOccurredAt: origin);
        Assert.Equal(GarageJobDueStatus.NotDue, result.Status);
        Assert.Equal(GarageJobLondonTime.Add(origin, 12, GarageJobTimeUnit.Months), result.NextDueAt);
    }

    [Fact]
    public void Evaluate_AfterCompletion_NeverReturnsDueImmediatelyOrNeverDone()
    {
        var origin = London(2026, 1, 1);
        var result = Evaluate(
            [Time(12, GarageJobTimeUnit.Months)],
            asOf: origin.AddDays(1),
            lastOccurredAt: origin);
        Assert.Equal(GarageJobDueStatus.NotDue, result.Status);
        Assert.True(result.HasCompletion);
        Assert.NotEqual(GarageJobDueStatus.DueImmediately, result.Status);
        Assert.NotEqual(GarageJobDueStatus.NeverDone, result.Status);
    }

    private static GarageJobDueResult Evaluate(
        IReadOnlyList<GarageJobRepeatCondition> conditions,
        GarageJobRepeatCombine combine = GarageJobRepeatCombine.WhicheverFirst,
        DateTimeOffset? asOf = null,
        DateTimeOffset? lastOccurredAt = null,
        DateOnly? anchor = null,
        int? lastMiles = null,
        int? currentMiles = null) =>
        GarageJobDueEvaluator.Evaluate(new GarageJobDueRequest(
            conditions,
            combine,
            asOf ?? London(2026, 6, 1),
            lastOccurredAt,
            anchor,
            lastMiles,
            currentMiles));

    private static GarageJobRepeatCondition Time(int amount, GarageJobTimeUnit unit, int sort = 0) =>
        new()
        {
            Kind = GarageJobRepeatKind.TimePeriod,
            Amount = amount,
            Unit = (int)unit,
            SortOrder = sort,
        };

    private static GarageJobRepeatCondition Dist(int amount, GarageJobDistanceUnit unit, int sort = 0) =>
        new()
        {
            Kind = GarageJobRepeatKind.Distance,
            Amount = amount,
            Unit = (int)unit,
            SortOrder = sort,
        };

    private static DateTimeOffset London(int year, int month, int day, int hour = 12, int minute = 0)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return GarageJobLondonTime.Convert(
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, GarageJobLondonTime.TimeZone), TimeSpan.Zero));
    }
}
