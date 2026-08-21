# Delivery: Euro Car Parts

- **Feature:** [docs/features/source-eurocarparts.md](source-eurocarparts.md)
- **Seq:** 2
- **Branch:** `feature/source-eurocarparts-Euro-Car-Parts`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/3

## What landed

- `IsEuroCarPartsHost` (`eurocarparts.*`) and `ParseEuroCarParts`: product H1, first `pdpPrice` (not frequently-bought add-ons), brand image manufacturer, source/vendor `"Euro Car Parts"`.
- Sample 2 fills ExtraYaml battery fields (105 Ah, 393×175×190 mm, CCA 950 from the name, AGM). Samples 1 and 3 leave extra fields null.
- HttpClient fetch; no Chromium host gate. `ProductVendorHelper.InferSourceFromUrl` returns `"Euro Car Parts"`.
- Three trimmed fixtures and `EuroCarPartsPageParserTests`. `docs/parsing/overview.md` lists the host.

## Tests

- `dotnet test WorkCosts.slnx --settings .runsettings` — `WorkCosts.Tests` 89 passed (`EuroCarPartsPageParserTests` plus existing cases). Solution exit 1 is the pre-existing Package VSTest target.

## Deviations

- Manufacturer is the first token of `brandImage` alt so “Eicher Premium” matches confirmed **Eicher**.
- Vendor is the host label `"Euro Car Parts"` (first-party shop; no sold-by node).
