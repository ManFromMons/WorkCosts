# Delivery: Tayna

- **Feature:** [docs/features/source-tayna.md](source-tayna.md)
- **Seq:** 4
- **Branch:** `feature/source-tayna-Tayna`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/5

## What landed

- `IsTaynaHost` (`tayna.*`) and `ParseTayna`: uppercase product H1, `#prodprice` in `.pricing-holder` (else `product:price:amount` / `twitter:data1` when the buy box has no price), Product Code or H1 part number, EAN, source/vendor `"Tayna"`.
- ExtraYaml battery fields from the Technical Specification table, including **Height inc. terms** (sample 1 Wet 30 Ah 185×130×170 CCA 300; sample 2 AGM 80 Ah 315×175×190 CCA 800; sample 3 Wet 95 Ah 353×175×190 CCA 800).
- HttpClient fetch; no Chromium host gate. `ProductVendorHelper.InferSourceFromUrl` returns `"Tayna"`.
- Three trimmed fixtures and `TaynaPageParserTests`. `docs/parsing/overview.md` lists the host.

## Tests

- `dotnet test WorkCosts.slnx --settings .runsettings` — `WorkCosts.Tests` 97 passed (`TaynaPageParserTests` plus existing cases). Solution exit 1 is the pre-existing Package VSTest target.

## Deviations

- Vendor is the host label `"Tayna"` (first-party shop; no sold-by node). Also listed in `docs/features/to-review.md` on `main`.
