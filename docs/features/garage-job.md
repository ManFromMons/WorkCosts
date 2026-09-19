# Feature: Garage job (define and build)

- **Id:** `docs/features/garage-job.md`
- **Seq:** 9
- **Depends-on:** none
- **Status:** ready-for-agent
- **PR:** none
- **Windows:** Core + tests + data docs in v1 (no WinUI)
- **Related screens:** none yet (new surface TBD; existing `docs/screens/jobs.md` stays for **`Job`** templates)
- **Related code:** existing **`Job`**, **`ProductJob`**, **`WorkJob`**, **`WorkJobItem`** (unchanged), **`Product`**, **`WorkCostsDbContext`**, **`DbInitializer`**, **`ProductCommands`**, **`DurationHelper`**, `DatabaseService` data folder layout (`docs/data/connection.md`)

## Objectives

- Introduce **`GarageJob`** as a **new** persisted concept — reusable definition of work for planning — **without new columns on** the existing **`Job`** table (garage price templates, `ProductJobs`, Home **Work Jobs**, Jobs page stay as today).
- **Define** fields: **icon**, name, target car or engine, description, **expected work duration/effort** (how long the task takes), **repetition / conditions-to-be-met** (e.g. calendar interval and/or distance travelled), ordered **required catalogue products**, and optional **composition**: an ordered set of references to existing **`Job`** templates (the app’s **sub-work / job-type** rows — garage £, duration, **`ProductJobs`**). A **`GarageJob`** is the larger planning envelope; **`Job`** remains where sub-work is defined today. Document how a future **`ItemOfWork`** will reference a `GarageJob` at a date/time and record odometer/readings for condition tracking.
- **Build** SQLite storage: `GarageJobs`, `GarageJobRepeatConditions`, `GarageJobRequiredProducts`, **`GarageJobReferencedJobs`**, icon **files on disk** (not SQLite BLOBs), EF migration, **`GarageJobCommands`**, tests, and **`docs/data/garage-job.md`** (+ `docs/data/schema.md`).
- **Out of scope:** **`ItemOfWork`** tables or UI (except documenting how repeat conditions will be evaluated later). **Roll-up UI or Core aggregators** (sum parts, DIY cost, garage £, times from referenced **`Job`** rows — specify intent only). Changes to **`WorkJobs`**, **`ProductJobs`**, or **`IsAllJobs`**. New **`Job`→`Job`** hierarchy tables (only **`GarageJob`→`Job`** links in this feature). Zip export/import implementation (note icon blobs + merge keys in domain doc). GNOME/iPad UI. Odometer logging on a vehicle entity (conditions store **thresholds on the template** only in this feature).

## User requirements

### Domain terms (coexist with today’s app)

| Term | Role |
| :--- | :--- |
| **`Job`** | *Existing.* **Sub-work / job-type** template on the Jobs page: garage price, duration, notes, **`ProductJobs`**. Feeds **Work Jobs** on Home. Scalar columns **unchanged** by this feature. |
| **`GarageJob`** | *New.* Larger **planning** definition: icon, target, description, optional top-level work duration, repeat rules, extra required products, optional ordered **referenced `Job`** set. |
| **Referenced job** | A **`Job`** template linked from a **`GarageJob`** (ordered). Drives **future UI** roll-up of parts, costs, and times from each sub-work type; stored as junction rows only in v1 Core. |
| **Other sub-work on `GarageJob`** | Products on **`GarageJobRequiredProducts`** that are not implied by a referenced **`Job`** (planning-only lines). Not a second entity type. |
| **Work duration** | How long performing the work once is expected to take (`DurationMinutes`). **Not** the same as repeat interval. |
| **Repeat condition** | A rule for when the work should be done again (e.g. every 12 months, every 10 000 km). Multiple conditions can apply; combine mode says whether **any** threshold triggers due (**whichever first**) or **all** must be satisfied. |
| **Required product** | Catalogue `Product` needed for a `GarageJob`, with quantity and list order. |
| **`ItemOfWork`** | *Future.* Instance of a **`GarageJob`** at a date/time (and later odometer); used to evaluate repeat conditions. Docs only here. |

### GarageJob fields (persisted)

- **Icon** — each garage job has a visual identity in lists and planning UI. Stored as a **file** under the app data folder; SQLite holds relative path + content type. Empty path → platform **default garage-job icon** (built-in), not null row.
- **Name** — required (max 200).
- **Target** — **car** or **engine** (`TargetKind` + `TargetLabel`, unless structured fields win in questions).
- **Description** — work scope / notes (max 8000; plain vs markdown — see questions).
- **Work duration** — `DurationMinutes` (whole minutes, `DurationHelper`); 0 = unspecified.
- **Repetition** — zero or more **repeat conditions** (child rows). Examples: every 6 months; every 8 000 mi; every 12 months **or** 10 000 mi whichever comes first.
- **Required products** — zero or more products via junction; unique `(GarageJobId, ProductId)`; stable ordering.
- **Referenced jobs** — zero or more **`Job`** rows via junction; unique `(GarageJobId, JobId)`; stable **`SortOrder`**.

### Future UI: roll-up from referenced **`Job`** rows (document only in v1)

When a garage-job surface ships, the ordered referenced-job list is the input for **read-only aggregation** (not persisted totals on **`GarageJobs`**):

| Roll-up | Source per referenced **`Job`** | Notes |
| :--- | :--- | :--- |
| **Parts / catalogue lines** | **`ProductJobs`** (+ quantities on **`GarageJobRequiredProducts`** for the parent) | Merge by **`ProductId`**; same product in multiple jobs → sum quantities or show grouped rows (UI choice later). Respect **`Product.IsAllJobs`** only when a work instance is built, not in template preview. |
| **Garage cost (GBP)** | **`GaragePrice`** | Default: **sum** referenced jobs’ garage prices for a “pay the garage” headline; show per-job breakdown. |
| **DIY parts cost (GBP)** | **`Product`** **`UnitCost`** × link quantity via **`ProductJobs`** | Same merge rules as parts list. |
| **Time** | **`DurationMinutes`** | Default: **sum** referenced jobs’ durations for a planning headline; optional per-job lines. Parent **`GarageJob.DurationMinutes`** stays independent unless the user edits it (not auto-synced from roll-up in v1). |

If **`Job`→`Job`** nesting is added later on the template model, roll-up **recurses** into referenced sub-jobs; this feature does not add that table.

### Repeat conditions (behaviour at template level)

- A garage job may have **no** repeat conditions (one-off definition only).
- Each condition has a **kind**: **time period** or **distance**.
- **Time:** positive integer + unit (days, months, years — see questions for weeks).
- **Distance:** positive integer + unit (**miles** or **kilometres**; UK app defaults display to miles unless settings say otherwise later).
- **`RepeatCombine`** on the parent garage job: **`WhicheverFirst`** (default, typical service interval) vs **`AllMustBeMet`** (uncommon; both thresholds required before “due”).
- This feature **stores** conditions only; **computing “due now”** needs `ItemOfWork` + last odometer/date — document in `docs/data/garage-job.md`, do not implement evaluators here.

### Core behaviour

- CRUD on `GarageJob` scalars and repeat conditions via **`GarageJobCommands`**.
- **Set icon:** write/replace file under `{data}/icons/garage-jobs/{garageJobId}.{ext}`; update path columns; deleting garage job **deletes icon file** if present.
- Add, remove, reorder required products; replace-all repeat conditions in one transaction when bulk-updating.
- **Composition:** `ReplaceReferencedJobsAsync` replaces the referenced-**`Job`** junction set in one transaction (dedupe ids, assign contiguous `SortOrder`). Reject unknown **`Job`** ids.
- Delete `GarageJob`: cascades repeat conditions, required-product links, and referenced-job links; removes icon file. Does **not** delete referenced **`Job`** templates.
- Deleting a **`Job`** template: remove **`GarageJobReferencedJobs`** rows for that job (same cascade pattern as **`ProductJobs`**). **Do not** block template delete because a garage job references it (unlike **`WorkJobs`**).
- **`ProductCommands.DeleteAsync`**: also delete `GarageJobRequiredProducts` for that product.
- Composition is **optional**; a garage job may have referenced jobs, required products, both, or neither.

### ItemOfWork (document only)

In **`docs/data/garage-job.md`**:

- `ItemOfWork` will hold `GarageJobId`, `OccurredAt`, optional **odometer** at completion, and instance product usage.
- Repeat evaluation: compare latest `ItemOfWork` for that garage job (+ odometer delta) against `GarageJobRepeatConditions` and `RepeatCombine`.

### Empty / error

- Empty name → reject.
- Invalid work duration when provided → reject.
- Repeat condition with non-positive amount → reject.
- Composition: duplicate **`Job`** id in one replace list → reject.
- Unknown ids → not-found; no partial writes.
- Icon: reject unsupported content type or oversize file per limits in technical design (defaults TBD in questions).

## Layout

- **No existing screen.** When UI ships: list rows show **icon** + name + short summary (target, repeat summary e.g. “Every 12 mo or 10k mi”).
- Editor: icon picker/preview; name; target; description; **work duration**; **repeat conditions** editor (add time row, add distance row, combine mode); **referenced Jobs** (pick sub-work / job-type templates, reorder); **required products**; *(future)* read-only **roll-up** panel (parts, DIY £, garage £, time) from referenced jobs.
- Compact = stack per layout grammar. **`MasterDetailPage`** remains **`Job`** templates only.

## Workflow

1. Migration runs; new tables exist; **`Jobs`** untouched.
2. `GarageJobCommands.CreateAsync` → row + default icon path empty + default combine mode + optional default duration.
3. `GarageJobCommands.UpdateAsync` → scalars + `RepeatCombine`.
4. `GarageJobCommands.ReplaceRepeatConditionsAsync` → replace child condition set.
5. `GarageJobCommands.SetIconAsync` / `ClearIconAsync` → file + columns.
6. Required-product commands unchanged from prior spec.
7. `GarageJobCommands.ReplaceReferencedJobsAsync` → referenced **`Job`** set (optional).
8. `GarageJobCommands.TryDeleteAsync` → row, dependent junction rows, icon file; referenced **`Job`** templates remain.
9. *(Future)* ItemOfWork records completion → planner evaluates repeat conditions; roll-up panel shows planning totals (not in v1 Core).

## Technical design

### New table: `GarageJobs`

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | Guid PK | |
| `Name` | string, max 200, required | |
| `TargetKind` | int / enum | `GarageJobTargetKind`: `Car`, `Engine` |
| `TargetLabel` | string, max 200 | default empty |
| `Description` | string, max 8000 | default empty |
| `DurationMinutes` | int, ≥ 0 | **Work effort** duration; 0 = unspecified |
| `IconRelativePath` | string, max 500 | Path under app data root, e.g. `icons/garage-jobs/{id}.png`; empty = default icon |
| `IconContentType` | string, max 100 | e.g. `image/png`; empty when no custom icon |
| `RepeatCombine` | int / enum | `WhicheverFirst` (0), `AllMustBeMet` (1) |

### New table: `GarageJobRepeatConditions`

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | Guid PK | |
| `GarageJobId` | Guid FK | → `GarageJobs`, cascade delete |
| `Kind` | int / enum | `TimePeriod`, `Distance` |
| `Amount` | int | > 0 (e.g. 12, 8000) |
| `Unit` | int / enum | Time: `Days`, `Weeks`, `Months`, `Years`. Distance: `Miles`, `Kilometres` |
| `SortOrder` | int | Stable display order |

- **Model enums:** `GarageJobRepeatKind`, `GarageJobTimeUnit`, `GarageJobDistanceUnit`, `GarageJobRepeatCombine`.
- Multiple time and/or distance rows allowed (e.g. one row 12 Months + one row 10000 Miles with `WhicheverFirst`).

### New table: `GarageJobRequiredProducts`

| Column | Notes |
| :--- | :--- |
| `GarageJobId` | FK → `GarageJobs`, cascade delete |
| `ProductId` | FK → `Products`, restrict delete |
| `Quantity` | short, default 1, minimum 1 |
| `SortOrder` | int, default 0 |
| PK | `(GarageJobId, ProductId)` |

### New table: `GarageJobReferencedJobs`

| Column | Notes |
| :--- | :--- |
| `GarageJobId` | FK → `GarageJobs`, cascade delete |
| `JobId` | FK → `Jobs`, cascade delete |
| `SortOrder` | int, default 0 |
| PK | `(GarageJobId, JobId)` |

- Links a **`GarageJob`** to existing **`Job`** sub-work templates. No extra columns on **`Jobs`**. Intended for **future UI summation** (see roll-up table above).

### Icon storage (files)

- Root: same directory family as DB (`docs/data/connection.md`) — `{dataRoot}/icons/garage-jobs/`.
- Filename: `{garageJobId}.{ext}` (extension from uploaded/picked file; normalize to lowercase ext).
- **No new SQLite BLOB column** for icons.
- **`GarageJobIconStore`** (Core, static or small class): resolve full path, write stream, delete file, validate content type whitelist (`image/png`, `image/jpeg`, `image/webp`) and max size (e.g. 512 KB — confirm in questions).
- Export/import zip (future): include `icons/garage-jobs/` in blob set keyed by `GarageJob` id.

### Unchanged (explicit)

- **`Jobs`** scalar columns, **`ProductJobs`**, **`WorkJobs`**, **`WorkJobItems`**, **`Product.IsAllJobs`**, **`DbInitializer`** job seeds. Optional EF navigation **`Job.GarageJobReferences`** (junction only) is fine.

### Core commands

| Need | Reuse | Create |
| :--- | :--- | :--- |
| DB context | `WorkCostsDbContext` | DbSets for four new entity types (+ junctions) |
| Migration | Existing chain | Add-only migration |
| Duration | `DurationHelper` | Repeat/unit enums |
| Product delete | `ProductCommands.DeleteAsync` | Remove `GarageJobRequiredProducts` rows |
| Icons | Product image path pattern (`connection.md`) | **`GarageJobIconStore`**, icon methods on **`GarageJobCommands`** |
| CRUD | — | **`GarageJobCommands`** |

**`GarageJobCommands` (minimum):**

- `CreateAsync`, `GetByIdAsync`, `ListAsync` (include conditions for summary helper optional in Core)
- `UpdateAsync` (scalars + `RepeatCombine`)
- `ReplaceRepeatConditionsAsync(garageJobId, IReadOnlyList<…>)`
- `GetRequiredProductsAsync`, add/remove/replace required products
- `GetReferencedJobsAsync`, `ReplaceReferencedJobsAsync(garageJobId, IReadOnlyList<Guid> jobIdsInOrder)`
- `SetIconAsync` / `ClearIconAsync` (stream or byte[] + content type)
- `TryDeleteAsync` → Success | NotFound (deletes icon file)

Optional Core helper: `GarageJobRepeatSummary.Format(conditions, combine)` for UI subtitle strings (no locale logic required in tests beyond en-GB miles label if hardcoded). Roll-up aggregation stays in a **future UI** layer, not v1 Core.

**Wiring:** `App.Database` + pass `DatabaseService.DataDirectory` or equivalent root into icon store (match how cache/images resolve paths today).

### ItemOfWork (future sketch in `docs/data/garage-job.md` only)

```
ItemOfWork { Id, GarageJobId FK Restrict, OccurredAt DateTimeOffset, OdometerReading int? … }
```

Evaluator (later): for each condition, compute elapsed time or distance since last ItemOfWork; apply `RepeatCombine`.

### Ports

- EF migration canonical; icon files live beside DB on all platforms.

## Tests

- Project: `WorkCosts.Tests`, temp SQLite + temp data folder for icons.
- `Migration_AddGarageJobTables_DoesNotAlterJobsTable`
- `GarageJobCommands_CreateAndUpdate_PersistsScalarsAndRepeatCombine`
- `GarageJobCommands_ReplaceRepeatConditions_PersistsMultipleTimeAndDistance`
- `GarageJobCommands_SetIcon_WritesFileAndUpdatesPath_ClearAndDeleteRemoveFile`
- `GarageJobCommands_RequiredProducts_AddRemoveReorder`
- `GarageJobCommands_TryDeleteAsync_CascadesConditionsAndProducts`
- `GarageJobCommands_ReplaceReferencedJobs_PersistsOrderAndDedupes`
- `ProductCommands_DeleteAsync_RemovesGarageJobRequiredProductLinks`
- Deleting **`Job`** (direct EF or test helper) removes **`GarageJobReferencedJobs`** rows without deleting parent **`GarageJob`**
- `GarageJobRepeatSummary` or enum validation rejects zero/negative amounts
- Existing **`Job`** seed tests unchanged

## Open questions

(none — override any **Accepted default** before `/start-implement` if you want different behaviour.)

## Accepted defaults

(Override any item before `/start-implement` if you want different behaviour.)

- Feature id **`garage-job`**; Seq **9**; **Depends-on** `none`.
- **`GarageJob`** naming for entity/table/commands; existing **`Job`** untouched.
- Work **duration** (`DurationMinutes`) ≠ **repeat interval** (child table).
- Icons: files + relative path columns; no BLOBs; max **512 KB**; PNG/JPEG/WebP; empty path = default icon in UI (built-in glyph catalogue is a **follow-up** — v1 Core only stores custom files).
- **`ItemOfWork`** (future) references **`GarageJobId` only**; no FK to legacy **`Job`**.
- **Target:** `TargetKind` + `TargetLabel` only in v1 (no structured make/model columns).
- **Description:** plain text (8000 chars).
- No separate **Effort** column in v1.
- Required products: **quantity** ≥ 1, default 1.
- **No seed `GarageJob` rows** on first launch.
- **Core + tests + data docs only** in v1 — no WinUI page or shell route.
- Repeat distance: **miles and kilometres**; summary strings use **miles** label for UK unless a later settings feature adds preference.
- Repeat time units: **days, weeks, months, years**.
- **`RepeatCombine` default:** `WhicheverFirst`; new rows default `RepeatCombine = WhicheverFirst`.
- **`GarageJobCommands.SetIconAsync` max size 512 KB**; reject other content types at command layer.
- **Composition:** single junction **`GarageJobReferencedJobs`**; ordered **`Job`** sub-work types only (no **`GarageJob`→`GarageJob`** links).
- v1 Core **stores** referenced-job links only; **does not** compute or persist roll-up totals. Future UI sums **`GaragePrice`**, **`DurationMinutes`**, and **`ProductJobs`** per **Accepted roll-up table** above.
- Parent **`GarageJob.DurationMinutes`** is independent of referenced jobs unless the user sets it manually.
- **`Job`** template delete: still blocked when **`WorkJobs`** exist (existing UI); **`GarageJobReferencedJobs`** do not add a second block.

## Implementation notes for an agent

1. **`docs/data/garage-job.md`**: domain diagram (GarageJob → conditions, products, referenced Job sub-work types, icon file; ItemOfWork future); clarify **`Job`** vs **`GarageJob`**; document roll-up rules for future UI; note composition is planning structure, not **`WorkJob`** instances.
2. Update **`docs/data/schema.md`** and **`docs/data/connection.md`** (icon folder row).
3. Models + EF migration (**add only**); implement **`GarageJobIconStore`**, **`GarageJobCommands`**, tests.
4. Extend **`ProductCommands.DeleteAsync`** for `GarageJobRequiredProducts`.
5. Do **not** modify **`MasterDetailPage`**, **`HomePage`**, or **`Jobs`** seed.
6. Do not implement ItemOfWork or due-date evaluation.
7. **`update-to-review`** when ready; PR after inbox **done**.
