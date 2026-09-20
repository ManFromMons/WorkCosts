# Feature: Car links to garage jobs and work jobs

- **Id:** `docs/features/car-job-links.md`
- **Seq:** 15
- **Depends-on:** `cars`, `garage-job`, `car-details`
- **Status:** draft
- **PR:** none
- **Windows:** Core + WinUI (existing Home add / work-job detail; garage-job commands already Core)
- **Related screens:** `docs/screens/cars.md`, `docs/screens/home.md` (today’s Add Work Job — **do not** replace Home), `docs/screens/work-job-detail.md`, `docs/screens/jobs.md`
- **Related code:** `Car` (soft-delete, `CarId` FKs from Seq 11), `CarDetails`, `GarageJob` (`TargetKind`, `TargetLabel`, `CarId`), `WorkJob`, `Job`, `ItemOfWork`, `GarageJobDueEvaluator`

VIN stories are independent. **Not** the future Home (current/pending/timeline). **`CarId` columns already exist in Seq 11**; this Seq makes the **workflows** and requires a car on new garage/work writes. Fitment uses **[car-details.md](car-details.md)** once that grain is decided.

## Objectives

- A **`GarageJob` is for a particular `Car`**. `TargetKind` + `TargetLabel` stay **alongside** `CarId`.
- A **`WorkJob` must have a `Car`**. It remains **one `Job` template instance**, and also points at the **`GarageJob`** it was started from (`WorkJob.GarageJobId`).
- Starting work: pick a garage job (which already has a car) → create work job(s) from referenced `Job` rows that **fit the car-details** type.
- Completions already have `CarId` (Seq 11); this Seq ensures writes set it (and `CarDetailsId` once Seq 12 exists).
- **Out of scope:** Cars add sheet, mdecoder, FastCarCheck, Home rewrite, hard-delete cars.

## User requirements

### Garage job

- Each garage job belongs to **one car** (not a shared definition copied implicitly). New create/update **requires** `CarId` (active car, `DeletedAt` null).
- Existing rows with null `CarId` remain until edited; the next save must set a car.
- `TargetKind` / `TargetLabel` still describe car vs engine / a label.

### Work job

- New work jobs **require** `CarId`. Existing nulls stay until edited.
- Still `JobId` + title + lines. Add **`GarageJobId`** (nullable for old rows; required when started from a garage job).
- Car defaults from the garage job; user can pick another **active** car (same type / any car — leftover under car-details).
- Work-job detail shows the car nickname (and make/model). Changing car is allowed only among active cars.

### Start work (WinUI, existing Home — not a new Home)

- Home **Add** (or equivalent): choose garage job **or** job template as today, **and** a car. If a garage job is chosen, car defaults from it and referenced jobs are the candidates, filtered by **car-details** binding (Seq 12).
- Cannot save without a car.
- Soft-deleted cars do not appear in pickers.

### Item of work

- Create completion: set `CarId` (from the garage job’s car if not passed) and `CarDetailsId` (snapshot from the car’s type — Seq 12).
- No cascade if the car is later soft-deleted; history keeps the ids.

### Empty / error

- Unknown or deleted car id: no write.
- Missing car on new garage job / work job: validation error, no persist.
- Delete car remains **soft-delete** (Seq 11); links stay.

## Layout

- **No new Home.** No timeline.
- Home Add Work Job: extra **Car** required picker (combo of active cars). Compact stack.
- Work-job detail: car line in the header/subtitle.
- Garage-job UI: if no WinUI editor exists yet, Core commands enforce `CarId`; a picker lands on that editor when it exists, and on start-work here.
- Cars page is not a job list.

## Workflow

1. User has an active car.
2. Garage job create/update includes that car.
3. Home Add: pick garage job (optional) + car (required) + title; create `WorkJob` with `JobId`, `CarId`, `GarageJobId` when applicable.
4. Detail shows car. Esc/cancel on Add: no row.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Car FK | Seq 11 `CarId` on garage job / work job / item of work | none (already Restrict) |
| Garage job source | `WorkJob` | `GarageJobId` Guid? FK Restrict to `GarageJobs` |
| Fitment | `CarDetails` + job bindings from Seq 12 | filter referenced jobs by type |
| UI | `HomePage` add flow, work-job detail | car combo; show nickname |

- **Wiring:** existing pages + commands. No DI.
- **Data:** add `WorkJobs.GarageJobId` only (other FKs from Seq 11). Do not drop `TargetKind`.
- **Ports:** Swift follows.

## Tests

- `GarageJobCommands_Create_RequiresCarId_ActiveCar`
- `GarageJobCommands_Update_ExistingNullCar_MustSetCar`
- `WorkJob_Create_RequiresCarId`
- `WorkJob_CreateFromGarageJob_CopiesCarId_SetsGarageJobId`
- `WorkJob_Create_UnknownOrDeletedCar_NoWrite`
- `ItemOfWork_Create_SetsCarId_AndCarDetailsId`
- Fitment filter once Seq 12 cardinality is fixed

## Open questions

1. *Assumption:* Fitment is “referenced `Job` rows whose car-details bindings include this car’s type”. Exact join waits on [car-details.md](car-details.md) Q1/Q4. → **Question:** None until car-details grain is chosen — then rewrite this section.
2. *Assumption:* Home Add still can start from a **Job template only** (no garage job), but **must** pick a car. → **Question:** Is template-only start still allowed, or must every work job come from a garage job?

## Accepted defaults

- Seq **15**; Depends-on `cars`, `garage-job`, `car-details`.
- Garage job = one particular car; target label alongside.
- Work job = still one `Job` instance + `CarId` + optional `GarageJobId`.
- WinUI on Home Add + work-job detail. No Home rewrite. Soft-delete unchanged.

## Implementation notes for an agent

Do not implement while **Status** is `draft` (blocked on car-details grain and Q2).

1. After car-details is `ready-for-agent` and Q2: rewrite fitment; then this file `ready-for-agent`.
2. Migration: `WorkJobs.GarageJobId` only.
3. Do not: VIN HTTP; new Home; cascade-delete cars; drop `Jobs`.
