# Feature: Garage job interval logic

- **Id:** `docs/features/garage-job-interval-logic.md`
- **Seq:** 10
- **Depends-on:** `garage-job`
- **Status:** draft
- **PR:** none
- **Windows:** Core + tests + data docs (no WinUI)
- **Related screens:** none (no new surface; future garage-job UI will call this helper)
- **Related code:** `GarageJob`, `GarageJobRepeatCondition`, `GarageJobRepeatKind`, `GarageJobTimeUnit`, `GarageJobDistanceUnit`, `GarageJobRepeatCombine`, `GarageJobRepeatValidation`, `GarageJobRepeatSummary`, `GarageJobCommands`, `docs/data/garage-job.md` (on the Seq 9 branch until squash-merged)

## Objectives

- Implement **repeat / interval evaluation** for stored `GarageJob` metadata: given the template’s conditions + combine mode and a snapshot of “last done” / “now”, compute due status, remaining time/distance, and next thresholds.
- Keep work duration (`GarageJob.DurationMinutes`) **out** of this helper — it is effort, not interval (`docs/features/garage-job.md`).
- Reuse Seq 9 types and validation; **do not** add columns to `Jobs`, `WorkJobs`, or `GarageJobs`.
- **Out of scope (unless Open questions override):** WinUI / GNOME / iPad screens; `ItemOfWork` tables and CRUD; vehicle / odometer entity; persisted “next due” columns; roll-up of referenced `Job` parts/£/time; zip export/import; changing `GarageJobRepeatSummary` format strings.

## User requirements

Observable behaviour is **library behaviour** (commands/helpers + tests). There is no UI in this story.

### Inputs the caller supplies

| Input | Meaning |
| :--- | :--- |
| Repeat conditions | The garage job’s `GarageJobRepeatConditions` (kind, amount, unit, sort order) |
| `RepeatCombine` | `WhicheverFirst` or `AllMustBeMet` |
| `asOf` | Clock instant for the evaluation (`DateTimeOffset`) |
| `currentOdometer` + unit | Optional current distance reading |
| `lastOccurredAt` | Optional last completion instant |
| `lastOdometer` + unit | Optional odometer at that completion |

Callers (future `ItemOfWork`, or a test) pass these in. This feature does **not** load history from SQLite.

### Outputs

A value type (name in Technical design) with:

| Field | Meaning |
| :--- | :--- |
| `Status` | `NotScheduled` / `NeverDone` / `NotDue` / `Due` / `Overdue` — see rules below |
| `NextDueAt` | Instant when the **time** side would become due (null if no usable time condition or no last completion) |
| `NextDueOdometerMiles` | Canonical next odometer threshold in **miles** (null if no usable distance condition or missing last odometer) |
| `RemainingTime` | Time remaining until time-due (`TimeSpan`; negative when overdue) |
| `RemainingDistanceMiles` | Miles remaining until distance-due (negative when overdue) |
| `TriggeringKinds` | Which kinds are currently met (`TimePeriod`, `Distance`, both, or none) |

`GarageJobRepeatSummary.Format` remains the human subtitle (“Every 12 mo or 10k mi”). This helper does not replace it.

### Status rules (proposed)

1. **No repeat conditions** → `NotScheduled`. All next/remaining fields null/default. Combine mode ignored.
2. **Has conditions, no `lastOccurredAt` and no `lastOdometer`** → `NeverDone` (planner has never recorded a completion). Not `Due` until the first completion exists — there is no interval origin.
3. **Has only time conditions** and no `lastOccurredAt` → `NeverDone` (distance inputs ignored).
4. **Has only distance conditions** and no `lastOdometer` → `NeverDone` even if `lastOccurredAt` is set.
5. **Has both kinds**, `WhicheverFirst`, and only one origin is present (date **or** odometer) → evaluate the usable kind only; the missing kind is treated as **not met** (it cannot fire).
6. **Has both kinds**, `AllMustBeMet`, and a kind cannot be evaluated → **not due** (`NotDue`): all thresholds are not yet known to be met.
7. Otherwise compare elapsed vs each condition:
   - Time elapsed = `asOf - lastOccurredAt`.
   - Distance elapsed = `currentOdometer - lastOdometer` in miles (see conversion).
   - A condition is **met** when elapsed ≥ interval.
   - `WhicheverFirst`: due when **any** evaluable condition is met.
   - `AllMustBeMet`: due when **every** condition is met (unevaluable conditions count as not met).
8. **`Due` vs `Overdue`:** met and elapsed **equals** the interval (within 1 second for time, 0.5 miles for distance) → `Due`. Met and elapsed **greater** → `Overdue`. Unmet → `NotDue`.
9. Multiple rows of the same kind: each row is a separate condition (e.g. 6 months **and** 12 months). `WhicheverFirst` uses the **soonest** / **shortest** remaining; `AllMustBeMet` uses the **latest** / **longest**.

### Empty / error

- Null/empty condition list: `NotScheduled`, no throw.
- Invalid amount (≤ 0) or unit/kind mismatch: **skip that row** (same as `GarageJobRepeatSummary`); do not throw. If every row is invalid → `NotScheduled`.
- `asOf` earlier than `lastOccurredAt`: treat time elapsed as zero (not negative); distance still uses odometer delta.
- Current odometer **less than** last odometer: treat distance elapsed as zero (clock rolled back / unit mix-up), do not throw.
- Missing current odometer when last odometer is present: distance conditions unevaluable (rules 5–6).

## Layout

- **No screen.** Future garage-job list/editor (Seq 9 layout notes) will show `GarageJobRepeatSummary` plus this helper’s `Status` / remaining figures. Do not add a nav destination.

## Workflow

1. Seq 9 tables and `GarageJobCommands.ReplaceRepeatConditionsAsync` already persist conditions.
2. Caller builds `GarageJobDueRequest` from a `GarageJob` (conditions + `RepeatCombine`) plus last-completion / now snapshot.
3. `GarageJobDueEvaluator.Evaluate(request)` returns `GarageJobDueResult` (pure function; no `DbContext`).
4. Tests cover calendar edges, miles/km, combine modes, missing origins, invalid rows.
5. Update `docs/data/garage-job.md` “Repeat evaluation (later)” to this contract. No migration.

No dialogs, sheets, Enter/Esc, or `DialogHelper`.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Condition rows / enums | `GarageJobRepeatCondition`, `GarageJobRepeatKind`, `GarageJobTimeUnit`, `GarageJobDistanceUnit`, `GarageJobRepeatCombine` | none |
| Amount/unit checks | `GarageJobRepeatValidation` | none |
| Subtitle strings | `GarageJobRepeatSummary` | none |
| Persistence | `GarageJobCommands` (unchanged) | none |
| Evaluation | — | `GarageJobDueEvaluator` + request/result records + `GarageJobDueStatus` enum in `WorkCosts.Core/Helpers/` |
| Domain doc | `docs/data/garage-job.md` | replace the “later” sketch with this contract |

### Types

```csharp
public enum GarageJobDueStatus
{
    NotScheduled = 0,
    NeverDone = 1,
    NotDue = 2,
    Due = 3,
    Overdue = 4,
}

public readonly record struct GarageJobOdometerReading(int Amount, GarageJobDistanceUnit Unit);

public sealed record GarageJobDueRequest(
    IReadOnlyList<GarageJobRepeatCondition> Conditions,
    GarageJobRepeatCombine Combine,
    DateTimeOffset AsOf,
    DateTimeOffset? LastOccurredAt,
    GarageJobOdometerReading? LastOdometer,
    GarageJobOdometerReading? CurrentOdometer);

public sealed record GarageJobDueResult(
    GarageJobDueStatus Status,
    DateTimeOffset? NextDueAt,
    double? NextDueOdometerMiles,
    TimeSpan? RemainingTime,
    double? RemainingDistanceMiles,
    IReadOnlyList<GarageJobRepeatKind> TriggeringKinds);
```

Pass `GarageJobRepeatCondition` instances (including invalid ones). Do not require EF tracking.

### Time arithmetic

- Days: `lastOccurredAt.AddDays(amount)`
- Weeks: `lastOccurredAt.AddDays(7 * amount)`
- Months: `lastOccurredAt.AddMonths(amount)` (BCL end-of-month: 31 Jan + 1 month → 28/29 Feb)
- Years: `lastOccurredAt.AddYears(amount)` (29 Feb + 1 year → 28 Feb in non-leap years)
- Compare with `DateTimeOffset` as given (offsets preserved). Do **not** convert to UK local or strip the time-of-day.
- `NextDueAt` for several time rows: `WhicheverFirst` → **minimum** next instant; `AllMustBeMet` → **maximum** next instant.
- `RemainingTime` = `NextDueAt - asOf` when `NextDueAt` is set.

### Distance arithmetic

- Canonical store for maths: **miles** as `double`.
- Conversion: `1 mile = 1.609344 km` (exact). `km → miles` divide by `1.609344`.
- Interval miles = convert condition `Amount`+`Unit` to miles.
- Elapsed miles = convert `(current - last)` after both readings are converted to miles.
- `NextDueOdometerMiles` = last odometer in miles + interval miles (min or max across distance rows per combine mode).
- `RemainingDistanceMiles` = `NextDueOdometerMiles - currentMiles`.
- Equality band for `Due` vs `Overdue`: `0.5` miles.

### Combine (recap)

| Mode | Due when | Next threshold |
| :--- | :--- | :--- |
| `WhicheverFirst` (default) | Any evaluable condition met | Soonest time **or** shortest distance among evaluable rows; status uses OR |
| `AllMustBeMet` | Every condition met | Latest time **and** longest distance; both must be evaluable |

When both kinds are evaluable under `WhicheverFirst`, `Status` is due/overdue if **either** side is met. `NextDueAt` / `NextDueOdometerMiles` still both populate so UI can show “due on date or at mileage”. `RemainingTime` / `RemainingDistanceMiles` stay independent. `TriggeringKinds` lists currently met kinds.

### Wiring

- Static helper; no DI container. Future WinUI calls `GarageJobDueEvaluator.Evaluate(...)` with values from Core (and later `ItemOfWork`).
- No new `DbSet`. No change to `GarageJobCommands` signatures.
- Optional convenience: `Evaluate(GarageJob job, …)` that reads `job.RepeatConditions` and `job.RepeatCombine` — thin wrapper only.

### Data

- SQLite unchanged. No new BLOBs. No `ItemOfWork` table in this Seq.

### Ports

- Pure C# in Core; Swift can port from tests when iPad planning exists. GNOME calls the same helper once UI exists.

## Tests

- Project: `WorkCosts.Tests`. **No SQLite required** for evaluator cases (plain objects). Do not hit the network.

Named cases:

- `Evaluate_NoConditions_NotScheduled`
- `Evaluate_ConditionsWithoutOrigin_NeverDone`
- `Evaluate_TimeDays_NotDueBefore_DueAt_OverdueAfter`
- `Evaluate_TimeWeeks_AddsSevenDaysPerWeek`
- `Evaluate_TimeMonths_EndOfMonth_Jan31PlusOneMonth`
- `Evaluate_TimeYears_LeapDay_Feb29PlusOneYear`
- `Evaluate_DistanceMiles_NotDueDueOverdue`
- `Evaluate_DistanceKilometres_ConvertsUsing1_609344`
- `Evaluate_MixedUnitsLastKmCurrentMiles_ConvertsBoth`
- `Evaluate_WhicheverFirst_TimeOrDistance_DueWhenEitherMet`
- `Evaluate_AllMustBeMet_DueOnlyWhenBothMet`
- `Evaluate_AllMustBeMet_MissingOdometer_NotDue`
- `Evaluate_WhicheverFirst_MissingOdometer_TimeCanStillDue`
- `Evaluate_TwoTimeRows_WhicheverFirst_UsesSooner`
- `Evaluate_TwoTimeRows_AllMustBeMet_UsesLater`
- `Evaluate_InvalidRowSkipped_ValidRowStillEvaluates`
- `Evaluate_AsOfBeforeLastOccurred_TimeElapsedZero`
- `Evaluate_OdometerWentBackwards_DistanceElapsedZero`
- `Evaluate_DurationMinutesIgnored` (job duration 180, interval 12 months — due follows months only)

Keep existing `GarageJobCommands` / `GarageJobRepeatSummary` tests unchanged.

## Open questions

1. *Assumption:* Core evaluator only; `ItemOfWork` stays a later story; no WinUI. → **Question:** Should Seq 10 also persist `ItemOfWork` (completion date + odometer), or stay a pure function with caller-supplied snapshot?

2. *Assumption:* With conditions but no last completion/odometer, status is `NeverDone`, not `Due`. → **Question:** Should a never-logged service instead be **Due immediately** so a planner lists it as work to do?

3. *Assumption:* Clock maths use the `DateTimeOffset` values as given (no UK local calendar). → **Question:** Must month/year boundaries follow **Europe/London** local dates instead of the stored offset?

4. *Assumption:* Odometer readings include a unit (`Miles` / `Kilometres`) on the request. → **Question:** Are live readings always **miles** (UK default), with only condition rows allowed to be km?

5. *Assumption:* `Due` is “elapsed equals interval”; anything past that is `Overdue`. → **Question:** Do you want a single **Due** status (overdue folded in), or keep the split for UI badges?

6. *Assumption:* Roll-up of referenced `Job` parts / garage £ / DIY £ / time stays **out of scope**. → **Question:** Should this Seq also add Core aggregators for that roll-up (“etc”)?

7. *Assumption:* No extra origin (no vehicle purchase date, no `GarageJob.CreatedAt`). Interval starts at last completion only. → **Question:** Should a missing last completion fall back to an **anchor date** the caller passes (e.g. MOT / registration)?

8. *Assumption:* Several conditions of the same kind are all in force (min vs max by combine mode). → **Question:** Should duplicate kinds be illegal at evaluate time, or is “6 months or 12 months” a real template?

## Accepted defaults

- Feature id `garage-job-interval-logic`; Seq **10**; **Depends-on** `garage-job` (pickup waits until that story is **Status** `done`).
- Type names: `GarageJobDueEvaluator`, `GarageJobDueRequest`, `GarageJobDueResult`, `GarageJobDueStatus`.
- Miles canonical for distance maths; `1.609344` km per mile.
- BCL `AddMonths` / `AddYears` for calendar edges.
- Skip invalid condition rows; do not throw.
- No schema change; no DI container; no new screen.
- Work duration is ignored by the evaluator.

## Implementation notes for an agent

1. Land **after** `garage-job` is **Status** `done` on `main` (tables + `GarageJobRepeatValidation` exist). Branch from `origin/main`.
2. Add `WorkCosts.Core/Helpers/GarageJobDueEvaluator.cs` (enum + records in that file or a sibling `GarageJobDue.cs`). Keep it allocation-light and dependency-free.
3. Update `docs/data/garage-job.md` evaluation section to match this file. Do **not** rewrite Seq 9 storage rules.
4. Tests in `WorkCosts.Tests/GarageJobDueEvaluatorTests.cs`. Use `DateTimeOffset` with a fixed offset (e.g. `+00:00`) in cases.
5. Do **not** add `ItemOfWork`, WinUI pages, or roll-up helpers unless Open questions were answered that way and this file rewritten.
6. Do not change `Jobs` / `WorkJobs` / `ProductJobs`.
7. `update-to-review` when tests pass; no GitHub PR until that heading is **Status** `done`.
