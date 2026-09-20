# Feature: Car links to garage jobs and work jobs

- **Id:** `docs/features/car-job-links.md`
- **Seq:** 13
- **Depends-on:** `cars`, `garage-job`
- **Status:** draft
- **PR:** none
- **Windows:** Core first; WinUI only where an existing surface already edits the row (Work Job create on Home is a question). No new Home.
- **Related screens:** `docs/screens/cars.md`, `docs/screens/home.md` (today’s Work Jobs add — do **not** replace Home), `docs/screens/work-job-detail.md`, `docs/screens/jobs.md`
- **Related code:** `Car`, `GarageJob` (`TargetKind`, `TargetLabel`), `GarageJobCommands`, `WorkJob`, `Job`, `ItemOfWork`, `GarageJobDueEvaluator`

Depends on [cars.md](cars.md) and Seq 9 `garage-job`. VIN lookup is independent ([car-vin-lookup.md](car-vin-lookup.md)). **Not** the future Home (current/pending work, active garage jobs, timeline).

## Objectives

- A **`GarageJob` may optionally be for a particular `Car`**, or for none (template still valid for any car / engine target as today).
- A **`WorkJob` may optionally be for a particular `Car`**, or for none.
- Starting a piece of work is **for a `GarageJob`**. That garage job need not have a car; the **work job should usually have a car**.
- When choosing **which `Job` templates / work jobs** apply, use the car’s **make and model**.
- **Out of scope:** Cars CRUD and Add Car image sheet. VIN lookup. Replacing Home with current/pending/timeline. GarageJob WinUI editor if it still does not exist — Core FKs can land without that page.

## User requirements

### Optional car on a garage job

- `GarageJob` keeps working with **no** car (shared template: “Oil service”, engine-only work, etc.).
- When a car is set: that is “this definition is for this vehicle”.
- Today’s `TargetKind` (Car / Engine) + `TargetLabel` (free text) must either coexist or be replaced — question.

### Optional car on a work job

- Existing Work Jobs stay valid with **no** car.
- New work “should really” be for a car: strong default, not necessarily a hard save-block — question.
- A work job is an instance of work; linking **`CarId`** and possibly **`GarageJobId`** is the connection (today `WorkJob` only has `JobId`).

### Starting work from a garage job

- User picks a garage job, then a car (if the garage job has none), then work is created.
- If the garage job already has a car, default the work job to that car; allow override — question.
- Referenced `Job` rows on the garage job are the candidates for Work Jobs; **filter by make/model** of the chosen car — question on how templates declare fitment.

### Make and model selection

- Cars have make+model (Seq 11). Jobs today have **no** fitment columns.
- Either: (a) `Job` / `GarageJob` gain make/model applicability, (b) filter is “jobs that already have a work instance on this make/model”, or (c) something else.

### Empty / error

- Unknown `CarId`: reject the write, no partial garage-job update.
- Delete car: **Restrict** while garage jobs, work jobs, or items of work point at it (cannot orphan). User deletes or unlinks first.
- Delete garage job: still blocked by `ItemsOfWork`; work jobs that pointed at it — question (Restrict vs null the `GarageJobId`).

## Layout

- **No new Home.** Do not add timeline or pending-work dashboard here.
- If this Seq touches WinUI: optional Car combo on existing **New Work Job** flow (`HomePage`) and/or a future garage-job editor — see questions. Compact still stacks.
- Cars page does not become a job list.

## Workflow

1. User has at least one car (Seq 11).
2. Attach or clear a car on a garage job (Core API; UI if a garage-job surface exists).
3. Start work: choose garage job → choose car if needed → create work job(s) from referenced jobs that match make/model.
4. Work job detail shows which car (read-only or editable — question).
5. Esc/cancel on any new picker: no write.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Car row | `Car` | none |
| Garage job | `GarageJob`, `GarageJobCommands`, `TargetKind` / `TargetLabel` | nullable `CarId` FK Restrict |
| Work job | `WorkJob` (`JobId` Restrict today) | nullable `CarId`; nullable `GarageJobId`? |
| Fitment | `Job` templates | make/model on Job, or a fitment table, or none |
| Delete | `CarCommands.TryDeleteAsync` | `HasLinks` / Restrict like `HasCompletions` |

- **Wiring:** extend existing command types. No DI container.
- **Data:** add-only migration. Do not drop `TargetKind` until a question says replace.
- **Ports:** Swift follows FKs. Evaluator still keys off `ItemOfWork` + odometer; live miles may later come from the car — **not** in this Seq unless asked.

## Tests

- `GarageJobCommands_Update_PersistsOptionalCarId`
- `GarageJobCommands_Update_UnknownCar_NoWrite`
- `WorkJob_OptionalCarId`
- `CarCommands_TryDelete_HasGarageJobs_DoesNotDelete`
- `CarCommands_TryDelete_HasWorkJobs_DoesNotDelete`
- Fitment filter cases once Q5 is answered
- Start-work-from-garage-job cases once Q3–Q4 are answered

## Open questions

1. *Assumption:* `GarageJob.CarId` is **optional FK**; `TargetKind` + `TargetLabel` stay for engine-only / unlinked templates. → **Question:** Optional FK alongside target label, or replace the free-text target when a car is selected?
2. *Assumption:* `WorkJob.CarId` is optional; creating from a garage job **defaults** it but can save without a car. → **Question:** Must a work job have a car, or only “should really”?
3. *Assumption:* Starting work adds nullable **`WorkJob.GarageJobId`** and still creates one `WorkJob` per referenced `Job` (today’s instance shape). → **Question:** Is a work job still one `Job` template instance, now also pointing at a garage job, or a new instance type?
4. *Assumption:* This Seq is **Core + tests** (and a Car picker on existing Home Add only if you want it). The new Home (current/pending/timeline) stays a later story. → **Question:** Any WinUI in Seq 13, and where?
5. *Assumption:* Make/model filter needs **fitment on `Job`** (make/model/all). → **Question:** How should templates declare which cars they apply to — columns on `Job`, on `GarageJob`, a junction, or match free-text `TargetLabel`?
6. *Assumption:* One garage job can be reused across cars when `CarId` is null; when set, it is that car only. → **Question:** Can several cars share one garage-job definition, or is it copy-per-car?
7. *Assumption:* `ItemOfWork` stays on `GarageJobId` only (not also `CarId`). If the garage job has a car, the completion implies that car. → **Question:** Should a completion also store `CarId` (e.g. when the garage job is unlinked)?

## Accepted defaults

- Seq **13**; Depends-on **`cars`**, **`garage-job`**. No Home rewrite. Restrict delete car while links exist. Miles still on `ItemOfWork`, not on `Car`, unless a later story adds a live odometer on the vehicle.

## Implementation notes for an agent

Do not implement while **Status** is `draft`. Do not implement before `cars` is **done**.

1. Fold answers into FKs vs fitment vs start-work flow; then `ready-for-agent`.
2. Add-only migration. Update `docs/data/schema.md` and `docs/data/garage-job.md`.
3. Do not: VIN lookup, Cars add sheet, Home timeline, drop `Jobs` / `WorkJobs`.
