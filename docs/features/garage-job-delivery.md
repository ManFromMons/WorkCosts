# Delivery: Garage job (define and build)

- **Feature:** [docs/features/garage-job.md](garage-job.md)
- **Seq:** 9
- **Branch:** `feature/garage-job-Garage-job-define-and-build`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/10

## What landed

- New **`GarageJob`** planning template in Core (not extra columns on **`Job`**): name, target, description, work duration, `RepeatCombine`, icon path/content type.
- Child tables for repeat conditions, required catalogue products, and ordered referenced **`Job`** rows. Icons are files under `{dataRoot}/icons/garage-jobs/`.
- `GarageJobCommands` CRUD, icon set/clear/delete, replace conditions/products/referenced jobs. `ProductCommands.DeleteAsync` removes `GarageJobRequiredProducts`. Deleting a **`Job`** cascades junction rows only.
- `docs/data/garage-job.md`, `docs/data/schema.md`, and `docs/data/connection.md`. No WinUI.

## Tests

- `dotnet test WorkCosts.Tests/WorkCosts.Tests.csproj --settings .runsettings --filter FullyQualifiedName~GarageJob` — 10 passed (`GarageJobCommandsTests` plus existing cases).

## Deviations

- none
