# Delivery: Car VIN lookup (mdecoder)

- **Feature:** [docs/features/13-car-vin-lookup.md](13-car-vin-lookup.md)
- **Seq:** 13
- **Branch:** `feature/13-car-vin-lookup-VIN-lookup`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/16

## What landed

- Lookup sits next to VIN on the Cars add sheet and editor. Enabled when VIN is set and the car looks BMW (`Make` BMW, or VIN `WBA` / `WBS` / `WBY` / `5UX` / `5YM`). Otherwise the sheet says to use FastCarCheck later and mdecoder is not called.
- HttpClient `GET https://www.mdecoder.com/decode/{vin}` first; a robot-check body uses the same off-dialog Chromium path as Autodoc. The client polls every 30s for up to 2 minutes. Cancel stops polling and does not discard the form.
- A ready decode replaces `VehicleOrderJson`. Nickname is never overwritten. Empty scalars fill; filled make / model / model-number / year / engine that differ need an in-sheet Apply fields / Keep current banner.

## Tests

- `dotnet test WorkCosts.Tests/WorkCosts.Tests.csproj --settings .runsettings` — 192 passed, including the seven named `MdecoderLookup_*` cases (empty VIN, non-BMW, 30s retry, 2 min timeout, ready JSON, nickname, cancel).
- `dotnet build WorkCosts/WorkCosts.csproj` — succeeded. `dotnet test WorkCosts.slnx` still exits 1 because the MSIX package project has no `VSTest` target.

## Deviations

- Wait/ready HTML fixtures were written from documented mdecoder fields (Cloudflare blocked a live capture). JSON is `{ source, vin, productionDate, type, model, steering, engine, transmission, color, upholstery, options[] }` serialized from the ready fixture.
