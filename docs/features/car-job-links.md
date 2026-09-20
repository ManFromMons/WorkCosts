# Feature: Car links to garage jobs and work jobs

- **Id:** `docs/features/car-job-links.md`
- **Seq:** 15
- **Depends-on:** `cars`, `garage-job`, `car-details`
- **Status:** draft
- **PR:** none
- **Windows:** Core + WinUI (start-work entry on existing Home **or** a garage-job surface — see Q1). **Do not** replace Home with a timeline dashboard.
- **Related screens:** `docs/screens/cars.md`, `docs/screens/home.md`, `docs/screens/work-job-detail.md`, `docs/screens/jobs.md`
- **Related code:** `Car`, `CarDetails`, `JobCarDetails`, `GarageJob`, `WorkJob`, `Job`

A **`WorkJob` sits beneath a `Job`** (template instance, `JobId` unchanged) and is **part of the work a `GarageJob` entails** (new rows have `GarageJobId` + `CarId` from that garage job).

**`ItemOfWork`** (completion date, odometer, due evaluation) already exists in Core. Its **behaviour, lifetime, and UI** are **[item-of-work-ui.md](item-of-work-ui.md)** — not this Seq.

## Objectives

- A **`GarageJob` is for one particular `Car`**. `TargetKind` + `TargetLabel` stay **alongside** `CarId`.
- A **`WorkJob`** remains one **`Job`** instance (lines, DIY vs garage £). New work jobs are created **from a garage job**, not as a free-floating template pick. Copy `CarId` from the garage job. Set `GarageJobId`.
- Starting a garage job creates work jobs for **referenced `Job` rows** that **fit** the car’s type (`JobCarDetails` contains `Car.CarDetailsId`). If the car has no type, include all referenced jobs.
- **Out of scope:** ItemOfWork logging UI, due timeline, Home rewrite, Cars add sheet, VIN lookups, hard-delete cars.

## User requirements

### Garage job

- New create/update **requires** an **active** `CarId`. Existing nulls stay until the next save, which must set a car.
- One car per garage job (not a shared template across cars).

### Work job

- Always **`JobId`** (sits beneath that job).
- New writes **require** `GarageJobId` and `CarId` (car = garage job’s car). Existing Home work jobs with nulls remain until edited.
- Title default: the `Job` name. User can rename on the existing work-job detail.
- Detail shows car nickname (read-only from the garage job’s car unless we later allow override — default **read-only**).
- Soft-deleted cars: not in pickers; existing work jobs still show the nickname from the stored id.

### Start work

- User starts a **garage job** (not “new work job from a job template”).
- Create one `WorkJob` per fitting referenced job, `SortOrder` of `GarageJobReferencedJobs`.
- Fitment: `JobCarDetails` includes the car’s `CarDetailsId`. No type on the car → all referenced jobs.
- Do **not** create `ItemOfWork` rows here (that is a completion, later story).

### Empty / error

- Unknown / deleted car, unknown garage job, missing car on new garage job: no write.
- Garage job with zero referenced jobs: no work jobs; visible “nothing to start”.

## Layout

- **No new Home** (no current/pending/timeline).
- Work-job detail: car line in subtitle. Compact stack.
- Start-work control: see Q1 (Home Add becomes “start garage job”, vs a garage-job page that does not exist yet).
- Cars page is not a job list.

## Workflow

1. Active car exists; garage job is saved with that car and referenced jobs.
2. User starts that garage job.
3. App creates `WorkJob` rows (`JobId`, `GarageJobId`, `CarId`, title from job name).
4. User opens a work job as today (lines, products). Completing the garage job (ItemOfWork) is a later story.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Car / type FKs | Seq 11–12 | none |
| Entailment | `WorkJob` | `GarageJobId` Restrict → `GarageJobs` |
| Start | `GarageJobReferencedJobs`, `JobCarDetails` | `StartGarageJobWorkCommand` (name TBD) |
| UI | `HomePage` / future garage-job page, work-job detail | start picker (Q1) |

- **Wiring:** existing commands + pages. No DI.
- **Data:** add `WorkJobs.GarageJobId` only.
- **Ports:** Swift follows.

## Tests

- `GarageJobCommands_Create_RequiresCarId_ActiveCar`
- `WorkJob_Create_RequiresGarageJobIdAndCarId`
- `StartGarageJob_CreatesWorkJobPerFittingReferencedJob`
- `StartGarageJob_CopiesCarIdFromGarageJob`
- `StartGarageJob_FiltersByJobCarDetails`
- `StartGarageJob_CarWithNoType_StartsAllReferencedJobs`
- `StartGarageJob_DoesNotCreateItemOfWork`
- `WorkJob_Create_UnknownOrDeletedCar_NoWrite`

## Open questions

1. *Assumption:* Until a garage-job WinUI page exists, **Home Add** is repurposed to **pick a garage job and start it** (no more “new work job from a job template only”). → **Question:** Is that OK, or should Seq 15 wait for a garage-job screen, or keep both entry points?
2. *Assumption:* Start creates **all** fitting referenced jobs in one go. → **Question:** All of them, or a checklist to start a subset?

## Accepted defaults

- Seq **15**; Depends-on `cars`, `garage-job`, `car-details`.
- WorkJob beneath Job; entailed by GarageJob. New rows need both FKs. ItemOfWork UI is a later story.
- No Home dashboard rewrite. Soft-delete unchanged.

## Implementation notes for an agent

Do not implement while **Status** is `draft` (Q1–Q2).

1. After answers: rewrite start-work layout; then `ready-for-agent`.
2. Migration: `WorkJobs.GarageJobId` only. Do not add ItemOfWork screens.
3. Do not: VIN HTTP; timeline Home; cascade-delete cars; drop `Jobs`.
