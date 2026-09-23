# Delivery: Car-details seed (in repo)

- **Feature:** [docs/features/16-car-details-seed.md](16-car-details-seed.md)
- **Seq:** 16
- **Branch:** `feature/16-car-details-seed-Car-details-seed`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/19

## What landed

- `WorkCosts.Core/Data/car-details.json` is embedded `[]`. `CarDetailsJsonSeed.Read` returns an empty list for that file and throws `InvalidOperationException` on malformed JSON.
- `DbInitializer.SeedAsync` calls `SeedCarDetailsAsync` after jobs. Empty JSON inserts nothing. User-added types stay. A missing embedded file is a no-op. Malformed JSON is skipped in initialize (helper still throws for tests).
- No catalogue rows. No migration. No Car types UI change.

## Tests

- `dotnet test WorkCosts.Tests/WorkCosts.Tests.csproj --settings .runsettings` — 208 passed, including `CarDetailsJsonSeed_EmptyArray_ReturnsEmpty`, `DbInitializer_EmptyCarDetailsJson_InsertsNothing`, `DbInitializer_KeepsUserAddedTypes_WhenJsonEmpty`, `CarDetailsJsonSeed_Malformed_Throws`, and `MalformedCarDetailsJson_SkipsWithoutWiping`.
- `dotnet build WorkCosts/WorkCosts.csproj` — succeeded.

## Deviations

- none
