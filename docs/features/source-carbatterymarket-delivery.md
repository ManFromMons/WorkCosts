# Delivery: Car Battery Market

- **Feature:** [docs/features/source-carbatterymarket.md](source-carbatterymarket.md)
- **Seq:** 3
- **Branch:** `feature/source-carbatterymarket-Car-Battery-Market`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/4

## What landed

- `IsCarBatteryMarketHost` (`carbatterymarket.*`) and `ParseCarBatteryMarket`: product H1, `.product--price.price--default` (not RRP, warranty add-on, or Special buy), brand / MPN, source/vendor `"Car Battery Market"`.
- ExtraYaml battery fields from the properties table and Technical Specifications list (sample 1 Wet 110 Ah 393×175×190 CCA 950; sample 2 SMF 110 Ah 393×174×189 CCA 850; sample 3 AGM 95 Ah 353×175×190 CCA 850).
- HttpClient fetch; no Chromium host gate. `ProductVendorHelper.InferSourceFromUrl` returns `"Car Battery Market"`.
- Three trimmed fixtures and `CarBatteryMarketPageParserTests`. Tests assert a price was parsed, not a GBP amount. `docs/parsing/overview.md` lists the host.

## Tests

- `dotnet test WorkCosts.slnx --settings .runsettings` — `WorkCosts.Tests` 93 passed (`CarBatteryMarketPageParserTests` plus existing cases). Solution exit 1 is the pre-existing Package VSTest target.

## Deviations

- Vendor is the host label `"Car Battery Market"` (first-party shop; no sold-by node).
- Source tests do not lock a GBP unit price (shop prices change). Also listed in `docs/features/to-review.md` on `main`.
