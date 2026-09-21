# Delivery: Car details (type catalogue)

- **Feature:** [docs/features/12-car-details.md](12-car-details.md)
- **Seq:** 12
- **Branch:** `feature/12-car-details-Car-types`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/14

## What landed

- Stuff → Car types master/detail with trailing Add. Narrow width stacks the list, then the detail, with Back.
- Add type is a sheet. Make, model, model number, year, and engine are required. Duplicate make + model-number + year + engine does not write.
- Delete is hard delete and Restrict if a car, job junction, garage job, or completion points at the type. No soft-delete.
- Optional car-type combo on the car editor. Nickname and scalars stay. An unknown type id does not write.
- `GarageJob` and `ItemOfWork` snapshot `CarDetailsId` from the car at write / completion. Later car-type edits do not follow unless the garage job is saved again.
- Job fitment is Core only: `ReplaceJobCarDetailsAsync` dedupes and orders. No Jobs-page chips.
- Migration `20260921220928_AddCarDetails`. `DbInitializer` does not seed types.

## Tests

- `dotnet test WorkCosts.Tests/WorkCosts.Tests.csproj --settings .runsettings` — 185 passed, including the CarDetails create/list/update/delete, duplicate key, Restrict delete, optional car bind, job junction, garage-job snapshot, and ItemOfWork snapshot cases named in the spec.
- `dotnet build WorkCosts.slnx` — succeeded.

## Deviations

- Unique type is stored as `TypeKey` (uppercase Make|ModelNumber|Year|EngineType) with a unique index, same idea as `Car.VrmKey`.
