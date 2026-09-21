# Feature: Item of work UI and lifetime

- **Id:** `docs/features/17-item-of-work-ui.md`
- **Seq:** 17
- **Depends-on:** `garage-job-interval-logic`, `11-cars`, `15-workjob-job-subset`
- **Status:** draft
- **PR:** none
- **Windows:** WinUI + Core (Core already has `ItemOfWork` + due evaluator)
- **Related screens:** TBD (not Home timeline unless a later Home story says so)
- **Related code:** `ItemOfWork`, `ItemOfWorkCommands`, `GarageJobDueEvaluator`, `Car`, `CarDetails`, `WorkJob`, `WorkJobCommands`

**Refine later. Do not implement.** Seq 15 is Core-only: a `WorkJob` belongs to a **`Job`**, and copying that job’s definition subset creates the **actual work** instances. This story is when we **resume** garage-job collation, completion logging, and any start-work chrome.

Domain (locked so this file does not contradict Seq 15):

- A **`WorkJob` is part of a `Job`**, not a `GarageJob`. No `GarageJobId` on `WorkJobs`.
- A **`GarageJob` will collate work items** (the copied instance set, or a later work-item type) and build behaviour around that set.
- An **`ItemOfWork`** is the **completion / service event** used for interval due logic — not the same row as a `WorkJob`.

Seq 10 already **persists** completions (date, odometer miles, Restrict to garage job). Seq 11–12 add `CarId` / `CarDetailsId` columns. Seq 15 adds definition CRUD + copy.

## Objectives

- User-visible **log completion** (OccurredAt, odometer miles, car + type snapshot).
- How a **GarageJob collates** the copied work-job instances (or work items) and what start-work UI that needs.
- Rules for **lifetime**: who can delete, whether completing creates/closes work jobs, whether one garage job has many completions (already true in Core).
- A **surface** (garage-job page, work-job detail, or elsewhere) — **not** the future Home current/pending/timeline unless we say so then.
- **Out of scope until resume:** that Home rewrite. Changing evaluator maths. VIN lookup. Redoing Seq 15 Core copy.

## User requirements (intent only)

- Completing a garage job writes `ItemOfWork` with `GarageJobId`, `CarId` (from the garage job), `CarDetailsId` snapshot from the car at that moment.
- Work jobs copied from a Job may be marked done or left open — **question on resume**.
- Soft-deleted cars: history rows keep ids.

## Layout

TBD on resume. No WebView. Dialogs only for short confirms. Compact = stack. Do not host a browser in a blocking dialog.

## Workflow

TBD after questions on resume.

## Technical design

Reuse `ItemOfWorkCommands` / evaluator and Seq 15 `WorkJobCommands.CopyDefinitionsToInstancesAsync`. No `GarageJobId` on `WorkJob`. No new table unless lifetime needs a status column (question).

## Tests

Named on resume (UI + command cases for create/delete/snapshot/collation).

## Open questions

Parked until resume:

1. *Assumption:* Logging a completion is a button on a **garage-job** screen, not on each work-job detail. → **Question:** Where do you log “this was done”?
2. *Assumption:* Completing does **not** auto-delete or auto-close copied instance `WorkJob` rows. → **Question:** What should happen to open work jobs when you log an ItemOfWork?
3. *Assumption:* User can delete a completion (already in Core) with Yes/No; due status re-evaluates from the previous item. → **Question:** Any extra lifetime rules (cannot delete last, cannot delete after N days)?
4. *Assumption:* GarageJob collation uses the **copied instance WorkJobs** from the referenced Jobs’ definition subsets. → **Question:** Confirm that, or introduce a separate work-item type?

## Accepted defaults

- Seq **17**; Depends-on interval logic, **`11-cars`**, **`15-workjob-job-subset`**. `WorkJob` ≠ `ItemOfWork`. `WorkJob` belongs to `Job`. Status **draft**. Refine later.

## Implementation notes for an agent

**Stop.** Do not implement. Do not fold this into Seq 15 or Seq 16.
