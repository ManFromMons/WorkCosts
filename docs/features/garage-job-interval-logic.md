# Feature: Garage job interval logic

- **Id:** `docs/features/garage-job-interval-logic.md`
- **Seq:** 10
- **Depends-on:** `garage-job`
- **Status:** ready-for-agent
- **PR:** none
- **Windows:** Core + tests + data docs (no WinUI)
- **Related screens:** none (no new surface; future garage-job UI will call these helpers)
- **Related code:** `GarageJob`, `GarageJobRepeatCondition`, `GarageJobRepeatKind`, `GarageJobTimeUnit`, `GarageJobDistanceUnit`, `GarageJobRepeatCombine`, `GarageJobRepeatValidation`, `GarageJobRepeatSummary`, `GarageJobCommands`, `GarageJobDeleteResult`, `ProductJob` (no quantity column — each link counts as 1), `Product.UnitCost`, `Job.GaragePrice` / `DurationMinutes`, `docs/data/garage-job.md` (Seq 9)

## Objectives

- Persist **`ItemOfWork`**: a dated completion of a `GarageJob` with optional odometer in **miles**.
- Persist **`GarageJob.IntervalAnchorDate`**: calendar date used as the interval origin when no completion exists (MOT / registration / last known service before the app).
- Evaluate repeat metadata in **Europe/London** local time: due status, remaining time/miles, next thresholds. **Multiple conditions of the same kind are required** (e.g. 6 months **or** 12 months).
- Add Core **roll-up** of referenced `Job` rows + `GarageJobRequiredProducts` (parts, garage £, DIY £, time).
- Work duration (`GarageJob.DurationMinutes`) stays **effort**, not interval — the evaluator ignores it.
- **Out of scope:** WinUI / GNOME / iPad screens; vehicle entity; zip export/import implementation (document new rows/files only); `ItemOfWork` product-usage lines; persisted “next due” columns; changing `GarageJobRepeatSummary` format strings; `Jobs` / `WorkJobs` / `ProductJobs` schema.

## User requirements

Observable behaviour is **library behaviour** (commands, helpers, tests). No UI in this story.

### ItemOfWork (completions)

- A garage job may have **zero or more** completions, newest `OccurredAt` wins for evaluation.
- Each row: when it was done (`OccurredAt`), optional **odometer miles** (`int?`, ≥ 0).
- FK to `GarageJob` is **Restrict**: cannot delete a garage job while completions exist (`GarageJobDeleteResult.HasCompletions`). Completions are **not** deleted with the parent.
- Deleting a completion is allowed (`ItemOfWorkCommands.TryDeleteAsync`).
- Empty list: evaluation uses the **anchor date** (and no last odometer).

### Interval anchor

- `GarageJob.IntervalAnchorDate` is a nullable **calendar date** (`DateOnly?`), not a time of day.
- Meaning: “intervals start from this London date” until the first `ItemOfWork`.
- Empty anchor + no completions → first service is **`DueImmediately`** (see status rules).
- Set/clear via `GarageJobCommands.UpdateAsync` (new parameter). Default on create: `null`.

### Live odometer

- Current and last readings are **miles** only (integer miles on `ItemOfWork` and on the evaluate request).
- Template **condition** rows may still be miles **or** kilometres (`GarageJobDistanceUnit`); convert conditions into miles for maths.

### Multiple conditions (required)

Templates may have **any number** of time rows and **any number** of distance rows, including several of the same kind.

Examples that **must** work:

| Conditions | Combine | Due when |
| :--- | :--- | :--- |
| 6 months, 12 months | `WhicheverFirst` | the **sooner** (6 months) |
| 6 months, 12 months | `AllMustBeMet` | the **later** (12 months) |
| 8 000 mi, 10 000 mi | `WhicheverFirst` | 8 000 mi |
| 12 months, 10 000 mi, 6 months | `WhicheverFirst` | whichever of the three hits first |
| 12 months, 10 000 mi | `AllMustBeMet` | both the 12-month **and** 10 000 mi thresholds |

Do **not** reject duplicate kinds in `ReplaceRepeatConditionsAsync` or in the evaluator.

### Due evaluation (outputs)

| Field | Meaning |
| :--- | :--- |
| `Status` | `NotScheduled` / `DueImmediately` / `NeverDone` / `NotDue` / `Due` / `Overdue` |
| `HasCompletion` | `true` when the request has a last `OccurredAt` from an `ItemOfWork` |
| `NextDueAt` | Instant the **time** side becomes due (`DateTimeOffset` with London offset), or null |
| `NextDueOdometerMiles` | Next odometer threshold in **miles**, or null |
| `RemainingTime` | `NextDueAt - asOf` (negative when overdue) |
| `RemainingDistanceMiles` | `NextDueOdometerMiles - currentMiles` (negative when overdue) |
| `TriggeringKinds` | Kinds whose threshold is currently met |

`GarageJobRepeatSummary.Format` stays the subtitle (“Every 12 mo or 10k mi”).

### Status rules

Time origin (first that applies):

1. `LastOccurredAt` from the latest `ItemOfWork`
2. else `IntervalAnchorDate` as **00:00:00 Europe/London** on that date
3. else none

Distance origin: `LastOdometerMiles` from that same latest item (or the request). No anchor odometer.

Then:

1. **No valid condition rows** → `NotScheduled`. Other fields default/null. Combine ignored.
2. **`DueImmediately`:** valid conditions, **no time origin** and **no distance origin**. First service is now (never logged, no MOT/registration date). `HasCompletion` is false. Next/remaining null.
3. **`NeverDone`:** valid conditions, **no completion** (`HasCompletion` false), a time origin **from the anchor**, and **no** condition is met yet at `asOf` / current miles. First service is still in the future. Populate `NextDueAt` / remaining from the anchor.
4. Otherwise compare elapsed vs **each** condition (see arithmetic). A condition is **met** when elapsed ≥ interval.
   - `WhicheverFirst`: due when **any** evaluable condition is met.
   - `AllMustBeMet`: due when **every** condition is met. Unevaluable conditions count as **not met**.
5. **`Due` vs `Overdue`:** met and elapsed **equals** the interval (within **1 second** for time, **0.5 miles** for distance) → `Due`. Met and **greater** → `Overdue`. Unmet (and not rules 1–3) → `NotDue`.
6. Mixed kinds, `WhicheverFirst`, only one origin present → evaluate the usable kind; the other kind is not met and cannot trigger.
7. Mixed kinds, `AllMustBeMet`, a kind cannot be evaluated → `NotDue` (or `NeverDone` if rule 3 applies: no completion, anchor present, time not yet met).
8. Several rows of the same kind: **all in force**. `WhicheverFirst` → **minimum** next instant / **shortest** remaining miles. `AllMustBeMet` → **maximum** / **longest**.

Priority if two labels could apply: `NotScheduled` > `DueImmediately` > (`Due` / `Overdue`) > `NeverDone` > `NotDue`. Once a completion exists, never return `NeverDone` or `DueImmediately`.

### Roll-up (template planning totals)

Given a garage job’s **ordered** `GarageJobReferencedJobs` plus `GarageJobRequiredProducts`:

| Total | Rule |
| :--- | :--- |
| **Parts** | Each `ProductJob` for a referenced `Job` contributes **quantity 1** (the `ProductJobs` table has **no** quantity). Same `ProductId` on several referenced jobs → **sum**. Then **add** `GarageJobRequiredProducts.Quantity` for that product (create a line if it was not already in `ProductJobs`). |
| **Garage cost (GBP)** | **Sum** `Job.GaragePrice` of referenced jobs (order does not change the sum). |
| **DIY parts (GBP)** | Sum of `Product.UnitCost × line quantity` on the merged parts list. |
| **Referenced time** | **Sum** `Job.DurationMinutes`. Do **not** add `GarageJob.DurationMinutes`. |
| **Parent duration** | Echo `GarageJob.DurationMinutes` separately (0 = unspecified). |

Do **not** pull in `Product.IsAllJobs` unless that product is already on a referenced job or a required-product row. Do not persist these totals.

### Empty / error

- Empty/null condition list: `NotScheduled`, no throw.
- Invalid amount (≤ 0) or unit/kind mismatch: **skip that row**. If every row is invalid → `NotScheduled`.
- `asOf` earlier than time origin: time elapsed = 0.
- Current miles **less than** last miles: distance elapsed = 0.
- Missing current miles: distance conditions unevaluable (rules 6–7).
- `ItemOfWork` with negative odometer → reject on write.
- Unknown `GarageJobId` on item create / evaluate-from-db → not found, no partial writes.
- `GarageJobCommands.TryDeleteAsync` with existing items → `HasCompletions` (no delete, icon file kept).
- Roll-up of a missing garage job → not found / null. Missing referenced `Job` rows should not happen (FK); skip a broken include rather than throw.

## Layout

- **No screen.** Future list rows: icon, name, `GarageJobRepeatSummary`, then this helper’s `Status` + remaining. Roll-up feeds a future read-only totals panel (Seq 9 layout notes). Do not add a nav destination. Compact = stack when UI exists.

## Workflow

1. Seq 9 migration already created `GarageJobs` and repeat/product/job junctions. This Seq adds `IntervalAnchorDate` + `ItemsOfWork`.
2. `GarageJobCommands.UpdateAsync` writes scalars including `IntervalAnchorDate`.
3. `ItemOfWorkCommands.CreateAsync` logs a completion (date + optional miles).
4. `GarageJobDueEvaluator.Evaluate(request)` is a **pure function**. `EvaluateAsync(db, garageJobId, asOf, currentOdometerMiles)` loads the job, conditions, anchor, and **latest** item, then calls `Evaluate`.
5. `GarageJobRollupCalculator.ComputeAsync(db, garageJobId)` loads referenced jobs + products and returns totals.
6. Tests: London calendar (GMT and BST), multiple same-kind rows, ItemOfWork CRUD, restrict-delete, roll-up merge.
7. Update `docs/data/garage-job.md`, `docs/data/schema.md`. Note future zip: `ItemsOfWork` + `IntervalAnchorDate`.

No dialogs, sheets, Enter/Esc, or `DialogHelper`.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Conditions / enums | Seq 9 `GarageJob*` types, `GarageJobRepeatValidation`, `GarageJobRepeatSummary` | none |
| Job/product graph | `Job`, `ProductJob`, `Product`, `GarageJobReferencedJob`, `GarageJobRequiredProduct` | none |
| Garage job CRUD | `GarageJobCommands` | `IntervalAnchorDate` on `UpdateAsync`; `HasCompletions` on `GarageJobDeleteResult` |
| Completions | — | `ItemOfWork` entity, `ItemOfWorkCommands` |
| London calendar | — | `GarageJobLondonTime` |
| Due maths | — | `GarageJobDueEvaluator` + request/result/`GarageJobDueStatus` |
| Roll-up | — | `GarageJobRollupCalculator` + result records |
| Schema | Seq 9 migration chain | Add-only migration |
| Docs | `docs/data/garage-job.md` | evaluation + ItemOfWork + roll-up |

### Schema additions

**`GarageJobs.IntervalAnchorDate`** — `DateOnly?`, null default. SQLite TEXT `yyyy-MM-dd`.

**New table: `ItemsOfWork`**

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | Guid PK | |
| `GarageJobId` | Guid FK | → `GarageJobs`, **Restrict** delete |
| `OccurredAt` | DateTimeOffset | required; SQLite sort via `UtcDateTime` (same pattern as `WorkJobs.CreatedAt`) |
| `OdometerMiles` | int? | null = not recorded; if set, ≥ 0 |

Index `(GarageJobId, OccurredAt)`. Navigation `GarageJob.ItemsOfWork`. CLR type name **`ItemOfWork`**, table **`ItemsOfWork`**.

No instance product-usage table in this Seq.

### London time (`GarageJobLondonTime`)

```csharp
public static class GarageJobLondonTime
{
    public static TimeZoneInfo TimeZone { get; } // Europe/London, else Windows "GMT Standard Time"
    public static DateTimeOffset AtStartOfDay(DateOnly date);
    public static DateTimeOffset Add(DateTimeOffset origin, int amount, GarageJobTimeUnit unit);
    public static DateTimeOffset Convert(DateTimeOffset value); // same instant, London offset
}
```

- Resolve the zone with `TimeZoneInfo.FindSystemTimeZoneById("Europe/London")`, and if that throws, `"GMT Standard Time"`.
- `Add`: convert `origin` to London wall time, `AddDays` / `AddDays(7*n)` / `AddMonths` / `AddYears` on that `DateTime`, then apply `TimeZone.GetUtcOffset` at the result (handles GMT vs BST).
- Month-end: BCL `AddMonths` (31 Jan + 1 month → 28/29 Feb in London).
- Leap day: 29 Feb + 1 year → 28 Feb in a non-leap year.
- `NextDueAt` is a `DateTimeOffset` whose offset is London’s at that instant.

### Due types

```csharp
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
```

`Evaluate(GarageJobDueRequest)` must not use `DbContext`.

`EvaluateAsync(WorkCostsDbContext db, Guid garageJobId, DateTimeOffset asOf, int? currentOdometerMiles)`:

- Load job + `RepeatConditions` (as Seq 9 `GetByIdAsync`).
- Latest item: `OrderByDescending(OccurredAt.UtcDateTime).ThenByDescending(Id)`.
- Map `LastOccurredAt` / `LastOdometerMiles` from that item; `IntervalAnchorDate` from the job.
- Missing job → return `null`.

### Distance arithmetic

- Live readings: **integer miles**.
- Condition km → miles: divide by **`1.609344`**. Condition miles stay as `double`.
- `NextDueOdometerMiles` = last miles + interval miles (min or max across **all** distance rows per combine).
- `RemainingDistanceMiles` = next − current.
- `Due` equality band: **0.5** miles.

### Combine (recap)

| Mode | Due when | Next threshold among **all** rows of that kind |
| :--- | :--- | :--- |
| `WhicheverFirst` | Any evaluable condition met | Soonest time **and** shortest distance (both populated when evaluable) |
| `AllMustBeMet` | Every condition met | Latest time **and** longest distance |

`TriggeringKinds` lists kinds currently met. `RemainingTime` and `RemainingDistanceMiles` stay independent.

### Roll-up types

```csharp
public sealed record GarageJobRollupLine(
    Guid ProductId,
    string Name,
    short Quantity,
    decimal UnitCost,
    decimal LineTotal);

public sealed record GarageJobRollup(
    IReadOnlyList<GarageJobRollupLine> Parts, // stable order: referenced-job SortOrder, then ProductJobs as stored, then required-product SortOrder for leftovers
    decimal GarageCostGbp,
    decimal DiyPartsCostGbp,
    int ReferencedDurationMinutes,
    int ParentDurationMinutes);
```

`ComputeAsync` Includes: `ReferencedJobs.Job.ProductJobs.Product`, `RequiredProducts.Product`. Money precision `decimal(18,2)` (round `LineTotal` and DIY sum with `AwayFromZero` to 2 dp).

### Commands

**`ItemOfWorkCommands`** (static, same pattern as `GarageJobCommands`):

- `CreateAsync(db, garageJobId, DateTimeOffset occurredAt, int? odometerMiles)` → entity; throw if miles &lt; 0; `false`/null if garage job missing — use `ItemOfWork?` null for not found
- `GetLatestAsync(db, garageJobId)`
- `ListAsync(db, garageJobId)` newest first
- `TryDeleteAsync(db, itemId)` → Success / NotFound

**`GarageJobCommands` changes:**

- `UpdateAsync`: add `DateOnly? intervalAnchorDate` after existing scalars (keep other parameters).
- `TryDeleteAsync`: if any `ItemsOfWork` for that id, return **`HasCompletions`** and do not remove the row or icon.
- Extend `GarageJobDeleteResult` with `HasCompletions`.

**Wiring:** static helpers; `App.Database` / `CreateContext()` later in UI. No DI container. Pass `dataRoot` only into existing icon delete.

### Ports

- EF migration is canonical. Swift follows `ItemsOfWork` + `IntervalAnchorDate`. GNOME calls the same Core helpers once UI exists.
- London TZ: ICU `Europe/London` on Linux/Mac agents.

## Tests

Project: `WorkCosts.Tests`. Evaluator/London tests need **no** SQLite. Command/roll-up tests use temp SQLite (same as Seq 9). No network.

**London / evaluator**

- `LondonTime_January_IsGmtPlusZero`
- `LondonTime_July_IsBstPlusOne`
- `Evaluate_NoConditions_NotScheduled`
- `Evaluate_NoOrigin_DueImmediately`
- `Evaluate_AnchorOnly_BeforeFirstDue_NeverDone`
- `Evaluate_AnchorOnly_AtInterval_Due`
- `Evaluate_AnchorOnly_AfterInterval_Overdue`
- `Evaluate_CompletionOverridesAnchor_ForTimeOrigin`
- `Evaluate_TimeDays_NotDueBefore_DueAt_OverdueAfter` (London add)
- `Evaluate_TimeWeeks_AddsSevenDaysPerWeek`
- `Evaluate_TimeMonths_EndOfMonth_Jan31PlusOneMonth_London`
- `Evaluate_TimeYears_LeapDay_Feb29PlusOneYear_London`
- `Evaluate_DistanceMiles_NotDueDueOverdue`
- `Evaluate_DistanceConditionKilometres_ConvertsUsing1_609344` (live miles, condition km)
- `Evaluate_WhicheverFirst_TimeOrDistance_DueWhenEitherMet`
- `Evaluate_AllMustBeMet_DueOnlyWhenBothMet`
- `Evaluate_AllMustBeMet_MissingCurrentMiles_NotDue`
- `Evaluate_WhicheverFirst_MissingCurrentMiles_TimeCanStillDue`
- `Evaluate_TwoTimeRows_WhicheverFirst_UsesSooner` (**6 mo vs 12 mo**)
- `Evaluate_TwoTimeRows_AllMustBeMet_UsesLater`
- `Evaluate_TwoDistanceRows_WhicheverFirst_UsesShorter`
- `Evaluate_ThreeRows_TwoTimeOneDistance_WhicheverFirst`
- `Evaluate_InvalidRowSkipped_ValidRowStillEvaluates`
- `Evaluate_AsOfBeforeOrigin_TimeElapsedZero`
- `Evaluate_OdometerWentBackwards_DistanceElapsedZero`
- `Evaluate_DurationMinutesIgnored`
- `Evaluate_AfterCompletion_NeverReturnsDueImmediatelyOrNeverDone`

**ItemOfWork / schema**

- `ItemOfWorkCommands_CreateListLatestDelete`
- `ItemOfWorkCommands_RejectsNegativeMiles`
- `ItemOfWorkCommands_Create_UnknownGarageJob_ReturnsNull`
- `GarageJobCommands_Update_PersistsIntervalAnchorDate`
- `GarageJobCommands_TryDeleteAsync_HasCompletions_DoesNotDelete`
- `EvaluateAsync_UsesLatestItemAndAnchor`

**Roll-up**

- `Rollup_EmptyComposition_ZeroTotals_EchoesParentDuration`
- `Rollup_SumsReferencedGaragePriceAndDuration`
- `Rollup_MergesProductJobsQuantityOnePerLink`
- `Rollup_AddsRequiredProductQuantity_SameProductSums`
- `Rollup_DoesNotIncludeIsAllJobsUnlessLinked`
- `Rollup_UnknownGarageJob_ReturnsNull`

Keep Seq 9 `GarageJobCommands` / `GarageJobRepeatSummary` tests passing (extend `UpdateAsync` call sites for the new parameter).

## Open questions

(none)

## Accepted defaults

- Feature id `garage-job-interval-logic`; Seq **10**; **Depends-on** `garage-job`.
- Type names as in Technical design (`GarageJobDueEvaluator`, `ItemOfWork`, table `ItemsOfWork`, `GarageJobLondonTime`, `GarageJobRollupCalculator`).
- Miles canonical; `1.609344` km per mile; live odometer **miles only**.
- Anchor = `DateOnly` at London midnight; completions keep their `DateTimeOffset` time-of-day in London when adding intervals.
- Skip invalid condition rows; do not throw.
- No DI container; no new screen; no `ItemOfWork` notes or product-usage lines.
- `ProductJobs` quantity is **1 per link**.
- Money rounded to 2 dp, `AwayFromZero`.
- Latest item: `OccurredAt.UtcDateTime` desc, then `Id` desc.
- Work duration ignored by the evaluator.

## Implementation notes for an agent

1. Land **after** `garage-job` is **Status** `done` on `main`. Branch from `origin/main`.
2. Migration: add `IntervalAnchorDate` to `GarageJobs`; create `ItemsOfWork` with Restrict FK. Do **not** alter `Jobs`.
3. `GarageJobLondonTime`, `GarageJobDueEvaluator`, `GarageJobRollupCalculator`, `ItemOfWork` + `ItemOfWorkCommands`.
4. Extend `GarageJobCommands.UpdateAsync` and `GarageJobDeleteResult`. Fix Seq 9 tests that call `UpdateAsync`.
5. Rewrite the evaluation / ItemOfWork / roll-up sections of `docs/data/garage-job.md`. Add tables to `docs/data/schema.md`. Mention zip merge keys for later.
6. Tests listed above. Linux agents must resolve `Europe/London`.
7. Do **not** add WinUI pages, vehicle tables, or zip import.
8. Do not collapse multiple same-kind conditions into one row.
9. `update-to-review` when tests pass; no GitHub PR until that heading is **Status** `done`.
