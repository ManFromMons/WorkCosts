# Delivery: Garage job interval logic

- **Feature:** [docs/features/garage-job-interval-logic.md](garage-job-interval-logic.md)
- **Seq:** 10
- **Branch:** `cursor/garage-job-interval-logic-f3f1`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/11

## What landed

- **`ItemOfWork`** completions (`OccurredAt`, optional odometer miles) with Restrict FK; `ItemOfWorkCommands` create / list / latest / delete. Deleting a garage job is blocked while completions exist (`HasCompletions`).
- **`GarageJob.IntervalAnchorDate`** (`DateOnly?`) via `UpdateAsync`. Empty anchor + no completions → **`DueImmediately`**.
- **`GarageJobLondonTime`** and **`GarageJobDueEvaluator`** in Europe/London: `NotScheduled` / `DueImmediately` / `NeverDone` / `NotDue` / `Due` / `Overdue`. Multiple same-kind conditions stay in force. Miles canonical (`1.609344` km/mile).
- **`GarageJobRollupCalculator`**: `ProductJob` quantity 1, merge required products, garage £ + DIY £ (2 dp away-from-zero), referenced job duration.
- Migration `20260920193900_AddItemOfWorkAndIntervalAnchor`. `docs/data/garage-job.md` and `docs/data/schema.md`. No WinUI.

## Tests

- `dotnet test WorkCosts.Tests/WorkCosts.Tests.csproj --settings .runsettings` — 160 passed (Linux agent; WinUI project not built). Includes the evaluator, London calendar, ItemOfWork, restrict-delete, and roll-up cases named in the spec.

## Deviations

- `ItemOfWork` latest/list: load then sort in memory by `OccurredAt.UtcDateTime` then `Id` (SQLite cannot translate that `OrderByDescending`).
- Branch `cursor/garage-job-interval-logic-f3f1` instead of `feature/garage-job-interval-logic-…`.
- EF migration authored by hand (`dotnet ef` unavailable on the Linux agent).
- Review PR opened at inbox `ready-for-review` rather than waiting for **Status** `done`.
