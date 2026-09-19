# Garage job domain

**Will I DIY?** uses two template layers for work planning:

| Concept | Table | Role |
| :--- | :--- | :--- |
| **`Job`** | `Jobs` | Sub-work / job-type template (Jobs page): garage £, duration, `ProductJobs`, feeds **Work Jobs** on Home. |
| **`GarageJob`** | `GarageJobs` | Larger planning definition: icon, target, description, work duration, repeat rules, extra required products, optional ordered references to **`Job`** rows. |

```
GarageJob
├── GarageJobRepeatConditions (when to repeat — template thresholds only)
├── GarageJobRequiredProducts → Product (planning-only catalogue lines)
├── GarageJobReferencedJobs → Job (ordered sub-work types for future roll-up)
└── icon file: {dataRoot}/icons/garage-jobs/{garageJobId}.{ext}
```

**`ItemOfWork`** (future) will reference **`GarageJobId` only** — not legacy **`Job`**. Sketch:

```
ItemOfWork { Id, GarageJobId FK Restrict, OccurredAt DateTimeOffset, OdometerReading int? … }
```

Repeat evaluation (later): load the latest `ItemOfWork` for that garage job, compare elapsed calendar time and odometer delta against `GarageJobRepeatConditions`, and apply parent `RepeatCombine` (`WhicheverFirst` vs `AllMustBeMet`). v1 Core **stores** conditions only; no due-date evaluator.

## Roll-up (future UI, not persisted)

From each referenced **`Job`** (in `SortOrder`):

| Roll-up | Source |
| :--- | :--- |
| Parts | `ProductJobs` quantities; merge with `GarageJobRequiredProducts` by `ProductId` |
| Garage cost (GBP) | Sum `Job.GaragePrice` (with per-job breakdown) |
| DIY parts (GBP) | `Product.UnitCost` × `ProductJobs` quantity |
| Time | Sum `Job.DurationMinutes`; parent `GarageJob.DurationMinutes` stays independent unless edited |

If **`Job`→`Job`** nesting is added later, roll-up recurses; this feature only links **`GarageJob`→`Job`**.

## Icons

Custom icons live under `{dataRoot}/icons/garage-jobs/`. SQLite stores `IconRelativePath` and `IconContentType`. Empty path means the platform default glyph in UI (built-in catalogue is a follow-up). Max **512 KB**; PNG, JPEG, WebP.

Zip export/import (future): include `icons/garage-jobs/` blobs keyed by `GarageJob` id; merge on import.

## Deletes

- Delete **`GarageJob`**: cascades conditions, required products, referenced-job links; removes icon file; **does not** delete referenced **`Job`** templates.
- Delete **`Job`**: removes `GarageJobReferencedJobs` rows (cascade); does **not** delete parent **`GarageJob`**; still blocked when **`WorkJobs`** exist (unchanged).
- Delete **`Product`**: `ProductCommands.DeleteAsync` removes `GarageJobRequiredProducts` for that product.

## Commands

`GarageJobCommands` in Core: CRUD scalars, replace repeat conditions, required products, referenced jobs, icon set/clear, delete. Pass app **data root** (same folder family as `workcosts.db`) for icon file I/O.
