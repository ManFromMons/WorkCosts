# Delivery: Car-details catalogue seed

- **Feature:** [docs/features/19-car-details-catalogue.md](19-car-details-catalogue.md)
- **Seq:** 19
- **Branch:** `feature/19-car-details-catalogue-Car-details-catalogue`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/20

## What landed

- `CarDetails` no longer has engine. Unique key is `Make|Model|ModelNumber|Year`. Nullable `EndYear`. Migration `20260923200000_CarDetailsDropEngineAddEndYear`.
- Seed file has 2515 Tiresaddict generations. Seq 16 upsert overwrites the same id. User-added other keys stay. The app does not call Tiresaddict or Wikidata.
- Car types page: four required fields plus optional end year. Cars page: **Engine code** label; type combo shows years, not engine.
- Generator: `scripts/Build-CarDetailsSeed.ps1` runs `tools/BuildCarDetailsSeed`. Committed JSON was built with `-SkipWikidata`.

## Tests

- `dotnet test WorkCosts.slnx --settings .runsettings` — 226 passed, including the named mapper, command, seed, and Wikidata-error cases.
- `dotnet build WorkCosts/WorkCosts.csproj` — succeeded.

## Deviations

- `scripts/Build-CarDetailsSeed.ps1` wraps `tools/BuildCarDetailsSeed` (not in `WorkCosts.slnx`).
- Committed `car-details.json` was generated with `-SkipWikidata`; no-parens chassis is the Tiresaddict model line.
- Mapper tests use in-memory Tiresaddict rows instead of files under `WorkCosts.Tests/Fixtures/car-details/`.
