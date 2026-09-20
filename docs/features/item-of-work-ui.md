# Feature: Item of work UI and lifetime

- **Id:** `docs/features/item-of-work-ui.md`
- **Seq:** 17
- **Depends-on:** `garage-job-interval-logic`, `cars`, `car-job-links`
- **Status:** draft
- **PR:** none
- **Windows:** WinUI + Core (Core already has `ItemOfWork` + due evaluator)
- **Related screens:** TBD (not Home timeline unless a later Home story says so)
- **Related code:** `ItemOfWork`, `ItemOfWorkCommands`, `GarageJobDueEvaluator`, `Car`, `CarDetails`

**Resume later.** Seq 10 already **persists** completions (date, odometer miles, Restrict to garage job). Seq 11–12 add `CarId` / `CarDetailsId` columns. This story is **when the user logs that work happened**, how long the row lives, and **which screen** shows it.

A **`WorkJob`** is DIY instance work under a **`Job`**, entailed by a **`GarageJob`**. An **`ItemOfWork`** is the **completion / service event** used for interval due logic — not the same row as a `WorkJob`.

## Objectives

- User-visible **log completion** (OccurredAt, odometer miles, car + type snapshot).
- Rules for **lifetime**: who can delete, whether completing creates/closes work jobs, whether one garage job has many completions (already true in Core).
- A **surface** (garage-job page, work-job detail, or elsewhere) — **not** the future Home current/pending/timeline unless we say so then.
- **Out of scope until resume:** that Home rewrite. Changing evaluator maths. VIN lookup.

## User requirements (intent only)

- Completing a garage job writes `ItemOfWork` with `GarageJobId`, `CarId` (from the garage job), `CarDetailsId` snapshot from the car at that moment.
- Work jobs entailed by that garage job may be marked done or left open — **question**.
- Soft-deleted cars: history rows keep ids.

## Layout

TBD. No WebView. Dialogs only for short confirms. Compact = stack.

## Workflow

TBD after questions.

## Technical design

Reuse `ItemOfWorkCommands` / evaluator. No new table unless lifetime needs a status column (question).

## Tests

Named on resume (UI + command cases for create/delete/snapshot).

## Open questions

1. *Assumption:* Logging a completion is a button on a **garage-job** screen, not on each work-job detail. → **Question:** Where do you log “this was done”?
2. *Assumption:* Completing does **not** auto-delete or auto-close the entailed `WorkJob` rows. → **Question:** What should happen to open work jobs when you log an ItemOfWork?
3. *Assumption:* User can delete a completion (already in Core) with Yes/No; due status re-evaluates from the previous item. → **Question:** Any extra lifetime rules (cannot delete last, cannot delete after N days)?

## Accepted defaults

- Seq **17**; Depends-on interval logic, cars, car-job-links. `WorkJob` ≠ `ItemOfWork`. Status **draft**.

## Implementation notes for an agent

**Stop.** Do not implement. Do not fold this into Seq 15.
