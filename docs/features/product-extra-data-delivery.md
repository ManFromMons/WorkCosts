# Delivery: Product extra YAML

- **Feature:** [docs/features/product-extra-data.md](product-extra-data.md)
- **Seq:** 5
- **Branch:** `feature/product-extra-data-Product-extra-YAML`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/2

## What landed

- Products store extra specs in `ExtraYaml` (camelCase YAML, max 8000).
- Add Product and product detail always show Capacity (Ah), Length/Width/Height (mm), CCA, and Technology. Save writes YAML; load fills the controls. Raw YAML is never shown.
- Parsers can supply extra fields on `ProductPageMetadata` / `ProductPageClientValues` (null does not overwrite). Host-specific extraction is out of this story.
- `ProductTechnology.Normalize` maps page phrases to tokens (AGM/EFB/SMF/Gel/Lithium before Wet).

## Tests

- `dotnet test WorkCosts.slnx --settings .runsettings` — `WorkCosts.Tests` 71 passed. Named cases: `ProductExtraTests`, `ProductTechnologyTests`, extra fields in `ProductPageClientContractTests`. Solution exit 1 is the pre-existing Package VSTest target.

## Deviations

- `DatabaseService.RepairProductSchema` also adds `ExtraYaml` (same pattern as `PricePoint`).
- `InputToolTip.Bind(ComboBox, …)` for Technology.
