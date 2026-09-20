using Microsoft.EntityFrameworkCore;
using WorkCosts.Data;
using WorkCosts.Models;

namespace WorkCosts.Helpers;

public enum GarageJobDueStatus
{
    NotScheduled = 0,
    DueImmediately = 1,
    NeverDone = 2,
    NotDue = 3,
    Due = 4,
    Overdue = 5,
}

public sealed record GarageJobDueRequest(
    IReadOnlyList<GarageJobRepeatCondition> Conditions,
    GarageJobRepeatCombine Combine,
    DateTimeOffset AsOf,
    DateTimeOffset? LastOccurredAt,
    DateOnly? IntervalAnchorDate,
    int? LastOdometerMiles,
    int? CurrentOdometerMiles);

public sealed record GarageJobDueResult(
    GarageJobDueStatus Status,
    bool HasCompletion,
    DateTimeOffset? NextDueAt,
    double? NextDueOdometerMiles,
    TimeSpan? RemainingTime,
    double? RemainingDistanceMiles,
    IReadOnlyList<GarageJobRepeatKind> TriggeringKinds);

public static class GarageJobDueEvaluator
{
    public const double MilesPerKilometre = 1.0 / 1.609344;
    private static readonly TimeSpan TimeEqualityBand = TimeSpan.FromSeconds(1);
    private const double DistanceEqualityBandMiles = 0.5;

    public static GarageJobDueResult Evaluate(GarageJobDueRequest request)
    {
        var conditions = (request.Conditions ?? Array.Empty<GarageJobRepeatCondition>())
            .Where(IsValid)
            .OrderBy(c => c.SortOrder)
            .ToList();

        var emptyKinds = (IReadOnlyList<GarageJobRepeatKind>)Array.Empty<GarageJobRepeatKind>();
        var hasCompletion = request.LastOccurredAt.HasValue;

        if (conditions.Count == 0)
        {
            return new GarageJobDueResult(
                GarageJobDueStatus.NotScheduled,
                hasCompletion,
                null,
                null,
                null,
                null,
                emptyKinds);
        }

        DateTimeOffset? timeOrigin = request.LastOccurredAt
            ?? (request.IntervalAnchorDate is { } anchor
                ? GarageJobLondonTime.AtStartOfDay(anchor)
                : null);
        var hasTimeOrigin = timeOrigin.HasValue;
        var hasDistanceOrigin = request.LastOdometerMiles.HasValue;

        if (!hasTimeOrigin && !hasDistanceOrigin)
        {
            return new GarageJobDueResult(
                GarageJobDueStatus.DueImmediately,
                hasCompletion,
                null,
                null,
                null,
                null,
                emptyKinds);
        }

        var timeRows = conditions.Where(c => c.Kind == GarageJobRepeatKind.TimePeriod).ToList();
        var distanceRows = conditions.Where(c => c.Kind == GarageJobRepeatKind.Distance).ToList();
        var timeEvaluable = hasTimeOrigin && timeRows.Count > 0;
        var distanceEvaluable = hasDistanceOrigin
            && request.CurrentOdometerMiles.HasValue
            && distanceRows.Count > 0;

        DateTimeOffset? nextDueAt = null;
        if (timeEvaluable)
        {
            var nexts = timeRows
                .Select(c => GarageJobLondonTime.Add(timeOrigin!.Value, c.Amount, (GarageJobTimeUnit)c.Unit))
                .ToList();
            nextDueAt = request.Combine == GarageJobRepeatCombine.AllMustBeMet
                ? nexts.Max()
                : nexts.Min();
        }

        double? nextDueMiles = null;
        double? selectedIntervalMiles = null;
        double elapsedMiles = 0;
        if (distanceEvaluable)
        {
            var last = request.LastOdometerMiles!.Value;
            var current = request.CurrentOdometerMiles!.Value;
            elapsedMiles = Math.Max(0, current - last);
            var intervals = distanceRows.Select(ToMiles).ToList();
            selectedIntervalMiles = request.Combine == GarageJobRepeatCombine.AllMustBeMet
                ? intervals.Max()
                : intervals.Min();
            nextDueMiles = last + selectedIntervalMiles;
        }

        var timeReached = nextDueAt is { } dueInstant && request.AsOf >= dueInstant;
        var timeOverdue = nextDueAt is { } dueForOverdue
            && request.AsOf - dueForOverdue > TimeEqualityBand;
        var distanceReached = selectedIntervalMiles is { } interval
            && elapsedMiles >= interval;
        var distanceOverdue = selectedIntervalMiles is { } intervalOverdue
            && elapsedMiles - intervalOverdue > DistanceEqualityBandMiles;

        var triggering = new List<GarageJobRepeatKind>();
        if (timeRows.Exists(c =>
                timeEvaluable
                && GarageJobLondonTime.Add(timeOrigin!.Value, c.Amount, (GarageJobTimeUnit)c.Unit) <= request.AsOf))
        {
            triggering.Add(GarageJobRepeatKind.TimePeriod);
        }

        if (distanceRows.Exists(c => distanceEvaluable && IsDistanceRowMet(
                request.LastOdometerMiles!.Value,
                request.CurrentOdometerMiles!.Value,
                c)))
        {
            triggering.Add(GarageJobRepeatKind.Distance);
        }

        var allMet = conditions.TrueForAll(condition =>
            condition.Kind == GarageJobRepeatKind.TimePeriod
                ? timeEvaluable && IsRowMet(
                    timeOrigin!.Value,
                    request.AsOf,
                    condition.Amount,
                    (GarageJobTimeUnit)condition.Unit)
                : distanceEvaluable && IsDistanceRowMet(
                    request.LastOdometerMiles!.Value,
                    request.CurrentOdometerMiles!.Value,
                    condition));

        GarageJobDueStatus status;
        if (request.Combine == GarageJobRepeatCombine.AllMustBeMet)
        {
            status = allMet
                ? CombinedDueOrOverdue(timeEvaluable && timeOverdue, distanceEvaluable && distanceOverdue)
                : GarageJobDueStatus.NotDue;
        }
        else
        {
            var anyReached = timeReached || distanceReached;
            status = anyReached
                ? CombinedDueOrOverdue(timeReached && timeOverdue, distanceReached && distanceOverdue)
                : GarageJobDueStatus.NotDue;
        }

        if (!hasCompletion
            && request.IntervalAnchorDate is not null
            && !request.LastOccurredAt.HasValue
            && status == GarageJobDueStatus.NotDue)
        {
            status = GarageJobDueStatus.NeverDone;
        }

        TimeSpan? remainingTime = nextDueAt is { } dueAt ? dueAt - request.AsOf : null;
        double? remainingMiles = nextDueMiles is { } dueMiles && request.CurrentOdometerMiles is { } cur
            ? dueMiles - cur
            : null;

        return new GarageJobDueResult(
            status,
            hasCompletion,
            nextDueAt,
            nextDueMiles,
            remainingTime,
            remainingMiles,
            triggering);
    }

    public static async Task<GarageJobDueResult?> EvaluateAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        DateTimeOffset asOf,
        int? currentOdometerMiles,
        CancellationToken cancellationToken = default)
    {
        var job = await GarageJobCommands.GetByIdAsync(db, garageJobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        var latest = await ItemOfWorkCommands.GetLatestAsync(db, garageJobId, cancellationToken);
        return Evaluate(new GarageJobDueRequest(
            job.RepeatConditions.ToList(),
            job.RepeatCombine,
            asOf,
            latest?.OccurredAt,
            job.IntervalAnchorDate,
            latest?.OdometerMiles,
            currentOdometerMiles));
    }

    private static GarageJobDueStatus CombinedDueOrOverdue(bool overdueA, bool overdueB) =>
        overdueA || overdueB ? GarageJobDueStatus.Overdue : GarageJobDueStatus.Due;

    private static bool IsValid(GarageJobRepeatCondition condition) =>
        GarageJobRepeatValidation.IsValidAmount(condition.Amount)
        && GarageJobRepeatValidation.IsValidUnitForKind(condition.Kind, condition.Unit);

    private static double ToMiles(GarageJobRepeatCondition condition) =>
        (GarageJobDistanceUnit)condition.Unit switch
        {
            GarageJobDistanceUnit.Miles => condition.Amount,
            GarageJobDistanceUnit.Kilometres => condition.Amount * MilesPerKilometre,
            _ => condition.Amount,
        };

    private static bool IsRowMet(
        DateTimeOffset origin,
        DateTimeOffset asOf,
        int amount,
        GarageJobTimeUnit unit) =>
        GarageJobLondonTime.Add(origin, amount, unit) <= asOf;

    private static bool IsDistanceRowMet(int lastMiles, int currentMiles, GarageJobRepeatCondition condition)
    {
        var elapsed = Math.Max(0, currentMiles - lastMiles);
        return elapsed >= ToMiles(condition);
    }
}
