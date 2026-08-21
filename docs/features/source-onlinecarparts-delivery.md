# Delivery: Online Car Parts

- **Feature:** [docs/features/source-onlinecarparts.md](source-onlinecarparts.md)
- **Seq:** 6
- **Branch:** `feature/source-onlinecarparts-Online-Car-Parts`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/6

## What landed

- `IsOnlineCarPartsHost` (`onlinecarparts.*`) and `ParseOnlineCarParts`: full product H1 including `.product__subtitle` (not the short JSON-LD name), `.product__new-price` / JSON-LD `offers.price` for this product, article number and manufacturer from `.product__artkl`, EAN from JSON-LD `gtin13`, source/vendor `"Online Car Parts"`.
- ExtraYaml unknown keys (YAML-only, no editor boxes): sample 1 `axle` / `size` (`347,8x30mm`) / `material` / `type`; sample 2 `size` (`253 mm`) / `material`; sample 3 those keys absent. Battery extra fields stay null.
- `ProductPageMetadata.ExtraUnknown` / `ProductPageClientValues.ExtraUnknown` merge into `ProductExtra.UnknownKeys` on apply. HttpClient fetch; no Chromium host gate. `IsAutodocHost` stays false.
- Three trimmed fixtures and `OnlineCarPartsPageParserTests`. `docs/parsing/overview.md` lists the host.

## Tests

- `dotnet test WorkCosts.slnx --settings .runsettings` — `WorkCosts.Tests` 103 passed (`OnlineCarPartsPageParserTests` plus existing cases). Solution exit 1 is the pre-existing Package VSTest target.

## Deviations

- Vendor is the host label `"Online Car Parts"` (JSON-LD seller is the shop URL).
- Sample 1 live `.product__new-price` on 2026-08-21 was **£49.96**; fixtures lock the confirmed **£50.24**.
- Added `ProductPageMetadata.ExtraUnknown` / client merge into `ProductExtra.UnknownKeys` (no editor boxes).
