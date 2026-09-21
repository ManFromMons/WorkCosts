# Feature: Car details (type catalogue)

- **Id:** `docs/features/12-car-details.md`
- **Seq:** 12
- **Depends-on:** `11-cars`
- **Status:** ready-for-agent
- **PR:** none
- **Windows:** Core + WinUI (**Stuff → Car types** master/detail)
- **Related screens:** `docs/screens/car-types.md` (new), `docs/screens/cars.md`, `docs/screens/jobs.md`, `docs/screens/shell.md`
- **Related code:** `Car`, `Job`, `GarageJob`, `ItemOfWork`, `DbInitializer`, `WorkCostsDbContext`, `DialogHelper`, Stuff nav

A **car type** (`CarDetails`) is “BMW E60 / 545 / 2004 / 4.4L V8”, not a vehicle the user owns. FastCarCheck ([14-car-fastcarcheck.md](14-car-fastcarcheck.md)) and a later **seed-from-repo** story ([16-car-details-seed.md](16-car-details-seed.md)) **hook onto the Car types page**. Job work-job subset (Core copy): [15-workjob-job-subset.md](15-workjob-job-subset.md). Completions / garage-job collation UI: [17-item-of-work-ui.md](17-item-of-work-ui.md) (refine later).

## Objectives

- Persist **`CarDetails`** including **model-number** (chassis code: **E60**, **E63**, …). Unique type = Make + ModelNumber + model year + engine text (plus display **Model**).
- **v1 table is empty** — `DbInitializer` does **not** insert types. Population from project source is [16-car-details-seed.md](16-car-details-seed.md). Users can add rows by hand on **Stuff → Car types**.
- Bind:
  - **`Car.CarDetailsId`** optional (Seq 11 scalars **stay**; lookups **supplement JSON**, they do not drop make/model/model-number/year/engine).
  - **`Job`**: many types via junction **`JobCarDetails`**.
  - **`GarageJob.CarDetailsId`**: snapshot of that garage job’s car’s type (one type; garage job is for one car).
  - **`ItemOfWork.CarDetailsId`**: **snapshot** at completion (not a live join to the car).
- **Stuff → Car types**: master/detail like Jobs. Later stories (seed import, FastCarCheck, job fitment UI) attach to this page — do not invent those UIs here beyond a type picker on the car editor.
- **Out of scope:** Encoding a catalogue in the repo. mdecoder. FastCarCheck HTTP. Home rewrite. Image files on the **type** (photos stay on `Car`, searched as `{Make} {ModelNumber}`).

## User requirements

### Type fields (all required)

| Column | Rules |
| :--- | :--- |
| **Make** | Max 120. |
| **Model** | Display name (e.g. 545). Max 120. |
| **ModelNumber** | Chassis / series (**E60**, **E63**). Max 32. Image search key with Make. |
| **Year** | Model year, same range as `Car.Year`. |
| **EngineType** | Free text. Max 200. |

Unique among types: normalized **Make + ModelNumber + Year + EngineType** (case-insensitive trim). Two engines in the same E60 year are two rows.

### Stuff → Car types

- Nav next to Cars. Tag `car-types`. Title **Car types**, subtitle, trailing **Add**.
- Regular: list beside editor. Compact: **stack**.
- List: make · model-number · year · engine. Empty: “No car types yet.”
- Add: **sheet** with the five required fields (no empty invalid row). Save inserts and selects.
- Detail: edit fields. Delete: Yes/No. **Restrict** if any `Car`, `JobCarDetails`, `GarageJob`, or `ItemOfWork` points at it (message; no delete). No cascade. No soft-delete on types in v1 (unlike cars).
- Unsaved changes: same helper as Cars/Jobs.
- Later features may add buttons/import on this page; this Seq is CRUD only.

### Bindings

- **Car editor:** optional combo of types (search by make / model-number). Does not clear nickname or scalars. `CarDetailsId` null is allowed.
- **Job:** not a full fitment UI in this Seq — Core junction `ReplaceJobCarDetailsAsync`. Job page checkboxes/chips can wait for a later UI story if too large; **minimum:** commands + tests. If a small “applies to types” list on Jobs is cheap, include it; do not block on a new destination.
- **GarageJob:** `CarDetailsId` set when the garage job’s car has a type (commands). Snapshot: copy from `Car.CarDetailsId` at write; do not live-update if the car’s type changes later unless the garage job is saved again.
- **ItemOfWork create:** copy `CarDetailsId` from the car at completion time (null if the car has no type). Do not follow later edits to the car’s type.

### Empty / error

- Duplicate type key: no write; visible reason.
- Unknown type id on a bind: no write.
- Delete in use: Restrict, keep the row.

## Layout

- OS spacing. Header title + subtitle + trailing Add. Detail inset on garage scrim.
- Sheets: Add type. Dialogs: delete, unsaved. No WebView.
- `docs/screens/car-types.md`. Shell Stuff: Products, Jobs, Categories, Cars, **Car types**. Compact iPad: still under Stuff, not a new top tab.

## Workflow

1. Stuff → Car types (empty until the user adds, or until Seq 16 / FastCarCheck).
2. Add → sheet → Save.
3. Edit / delete (if unused).
4. On a car, optionally pick a type. Completions snapshot that id.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Table | `WorkCostsDbContext` | `CarDetails`, unique index |
| Seed | `DbInitializer` | **no type rows** in v1 |
| Car | `Car` | `CarDetailsId` Guid? Restrict |
| Job | `Job` | `JobCarDetails` (`JobId`, `CarDetailsId`, `SortOrder`) |
| Garage job | `GarageJob` | `CarDetailsId` Guid? Restrict |
| Completion | `ItemOfWork` | `CarDetailsId` Guid? Restrict |
| UI | Jobs/Cars master-detail, `DialogHelper` | `CarTypesPage`, add sheet, `CarDetailsCommands` |

- **Wiring:** `App.Database` / `CreateContext()`. Static commands. No DI.
- **Data:** add-only migration. Restrict from type to dependents. Zip later: include `CarDetails` + junctions; merge by id.
- **Ports:** EF canonical.

## Tests

- `CarDetailsCommands_CreateListUpdateDelete`
- `CarDetailsCommands_RejectsDuplicateMakeModelNumberYearEngine`
- `CarDetailsCommands_TryDelete_InUseByCar_Restrict`
- `CarDetailsCommands_TryDelete_InUseByJobJunction_Restrict`
- `DbInitializer_DoesNotSeedCarDetails`
- `CarCommands_Update_PersistsOptionalCarDetailsId`
- `JobCarDetails_Replace_DedupesAndOrders`
- `GarageJobCommands_Update_SnapshotsCarDetailsId`
- `ItemOfWork_Create_SnapshotsCarDetailsId_FromCar`
- `ItemOfWork_Snapshot_DoesNotFollowLaterCarTypeChange`

## Open questions

(none)

## Accepted defaults

- Seq **12**; Depends-on **`11-cars`**.
- Grain includes **ModelNumber** (E60/E63). Image search on Cars stays `{Make} {ModelNumber}`.
- Empty catalogue in v1; [16-car-details-seed.md](16-car-details-seed.md) encodes data in the repo later.
- Keep Car scalars; JSON supplements. Optional `Car.CarDetailsId`.
- Job = many types (junction). Garage job = one type snapshot. ItemOfWork = snapshot.
- Stuff page **Car types**. Later stories hook here. No type photos.

## Implementation notes for an agent

1. Migration: `CarDetails`, `JobCarDetails`, FKs on `Car` / `GarageJob` / `ItemOfWork`. Empty initializer.
2. `CarDetailsCommands` + WinUI `CarTypesPage`. Optional type combo on car editor.
3. `docs/data/schema.md`, `docs/screens/car-types.md`, Stuff in `docs/screens/shell.md`.
4. Do not: seed a catalogue; FastCarCheck/mdecoder HTTP; Home; type image library; cascade from type.
