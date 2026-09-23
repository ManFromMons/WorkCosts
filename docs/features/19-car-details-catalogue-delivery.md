# Delivery: Car-details catalogue seed

- **Feature:** [docs/features/19-car-details-catalogue.md](19-car-details-catalogue.md)
- **Seq:** 19
- **Branch:** `feature/19-car-details-catalogue-Car-details-catalogue`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/20

## What landed

- `CarDetails` no longer has engine. Unique key is `Make|Model|ModelNumber|Year`. Nullable `EndYear`. Migration `20260923200000_CarDetailsDropEngineAddEndYear`.
- Seed file has 2515 Tiresaddict generations. Seq 16 upsert overwrites the same id. User-added other keys stay. The app does not call Tiresaddict or Wikidata.
- Car types page: four required fields plus optional end year. List is sorted by **Make**, then **Model**.
- Cars page: **Engine code** label. Optional car type is an autocomplete box (Add and detail). `BMW E60` is two wildcard terms (AND) against make, model, chassis, and years. Suggestions stay in Make then Model order, capped at 25. Selecting a type does not fill engine.
- Generator: `scripts/Build-CarDetailsSeed.ps1` runs `tools/BuildCarDetailsSeed`. Committed JSON was built with `-SkipWikidata`.

## Tests

- `dotnet test WorkCosts.slnx --settings .runsettings` — named mapper, command, seed, and Wikidata-error cases. Follow-up: `CarDetailsCommands_List_SortsByMakeThenModel` and `CarDetailsTypeLookup` (7 passed).
- `dotnet build WorkCosts/WorkCosts.csproj` — succeeded.

## Deviations

- `scripts/Build-CarDetailsSeed.ps1` wraps `tools/BuildCarDetailsSeed` (not in `WorkCosts.slnx`).
- Committed `car-details.json` was generated with `-SkipWikidata`; no-parens chassis is the Tiresaddict model line.
- Mapper tests use in-memory Tiresaddict rows instead of files under `WorkCosts.Tests/Fixtures/car-details/`.
- Cars type picker is `AutoSuggestBox` (not a combo). Empty text clears the type. Enter in the box picks a match and does not save the car.
