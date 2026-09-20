# Garage job domain

**Will I DIY?** uses two template layers for work planning:

| Concept | Table | Role |
| :--- | :--- | :--- |
| **`Job`** | `Jobs` | Sub-work / job-type template (Jobs page): garage £, duration, `ProductJobs`, feeds **Work Jobs** on Home. |
| **`GarageJob`** | `GarageJobs` | Larger planning definition: icon, target, description, work duration, repeat rules, extra required products, optional ordered references to **`Job`** rows, optional `IntervalAnchorDate`. |
| **`ItemOfWork`** | `ItemsOfWork` | Completion of a garage job (`OccurredAt`, optional odometer **miles**). FK Restrict to `GarageJob`. |

```
GarageJob
├── IntervalAnchorDate (optional London calendar origin until first completion)
├── GarageJobRepeatConditions (time and/or distance; several of the same kind allowed)
├── GarageJobRequiredProducts → Product (planning-only catalogue lines)
├── GarageJobReferencedJobs → Job (ordered sub-work types)
├── ItemsOfWork (completions; latest OccurredAt wins)
└── icon file: {dataRoot}/icons/garage-jobs/{garageJobId}.{ext}
```

## Repeat evaluation

`GarageJobDueEvaluator` (Europe/London wall time) compares the latest `ItemOfWork` — or `IntervalAnchorDate` at London midnight if none — with `GarageJobRepeatConditions` and `RepeatCombine`.

Live odometer readings are **miles**. Condition rows may still be kilometres (`÷ 1.609344`). Multiple rows of the same kind all apply (`WhicheverFirst` → soonest/shortest; `AllMustBeMet` → latest/longest).

Statuses: `NotScheduled` (no valid rows), `DueImmediately` (no time origin and no last odometer), `NeverDone` (no completion, anchor in the future), `NotDue`, `Due`, `Overdue`. After a completion, never `NeverDone` or `DueImmediately`.

`GarageJob.DurationMinutes` is effort, not interval — the evaluator ignores it.

## Roll-up (not persisted)

`GarageJobRollupCalculator` from referenced **`Job`** rows (`SortOrder`) plus `GarageJobRequiredProducts`:

| Roll-up | Source |
| :--- | :--- |
| Parts | Each `ProductJob` contributes quantity **1** (no quantity column). Same `ProductId` on several jobs → sum. Then add `GarageJobRequiredProducts.Quantity`. |
| Garage cost (GBP) | Sum `Job.GaragePrice` |
| DIY parts (GBP) | `Product.UnitCost` × merged quantity (2 dp, away-from-zero) |
| Time | Sum `Job.DurationMinutes`; parent `GarageJob.DurationMinutes` echoed separately |

Do not include `Product.IsAllJobs` unless the product is already linked. If **`Job`→`Job`** nesting is added later, roll-up recurses; this feature only links **`GarageJob`→`Job`**.

## Icons

Custom icons live under `{dataRoot}/icons/garage-jobs/`. SQLite stores `IconRelativePath` and `IconContentType`. Empty path means the platform default glyph in UI (built-in catalogue is a follow-up). Max **512 KB**; PNG, JPEG, WebP.

## Zip export/import (future)

Include `icons/garage-jobs/` blobs keyed by `GarageJob` id. Catalogue XML should carry `GarageJobs` (including `IntervalAnchorDate`), junctions, and `ItemsOfWork`; merge on import by id.

## Deletes

- Delete **`GarageJob`**: blocked while `ItemsOfWork` exist (`HasCompletions`). Otherwise cascades conditions, required products, referenced-job links; removes icon file; **does not** delete referenced **`Job`** templates.
- Delete **`ItemOfWork`**: allowed (`ItemOfWorkCommands.TryDeleteAsync`).
- Delete **`Job`**: removes `GarageJobReferencedJobs` rows (cascade); does **not** delete parent **`GarageJob`**; still blocked when **`WorkJobs`** exist (unchanged).
- Delete **`Product`**: `ProductCommands.DeleteAsync` removes `GarageJobRequiredProducts` for that product.

## Commands

`GarageJobCommands` in Core: CRUD scalars including `IntervalAnchorDate`, replace repeat conditions, required products, referenced jobs, icon set/clear, delete. `ItemOfWorkCommands`: create / list / latest / delete. Pass app **data root** (same folder family as `workcosts.db`) for icon file I/O. `GarageJobDueEvaluator` and `GarageJobRollupCalculator` are static helpers (no DI).
