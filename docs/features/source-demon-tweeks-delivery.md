# Delivery: Demon Tweeks

- **Feature:** [docs/features/source-demon-tweeks.md](source-demon-tweeks.md)
- **Seq:** 8
- **Branch:** `feature/source-demon-tweeks-Demon-Tweeks`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/7

## What landed

- `IsDemonTweeksHost` (`demon-tweeks.*`) and `ParseDemonTweeks`: product H1 (not “Buy … | Demon Tweeks”), `.now-price` **INC VAT** (ignore WAS, EX VAT, finance, and JSON-LD ex-VAT), Brand / single `MPN` from the attributes table (not `MPN(s)` lists or listing id `245980`), source/vendor `"Demon Tweeks"`.
- Chromium fetch via `RequiresChromiumFetch` (`IsAutodocHost` or `IsDemonTweeksHost`) in `ProductImagePicker.FetchPageAsync` / `ProductImageService.LoadPageAsync` / `ChromiumPageLoader`. HttpClient is Cloudflare 403; Paste HTML remains the fallback.
- Three trimmed fixtures and `DemonTweeksPageParserTests`. `docs/parsing/overview.md` lists the host.

## Tests

- `dotnet test WorkCosts.slnx --settings .runsettings` — `WorkCosts.Tests` 107 passed (`DemonTweeksPageParserTests` plus existing cases). Solution exit 1 is the pre-existing Package VSTest target.

## Deviations

- Add `demon-tweeks.com` to the Chromium fetch gate (`RequiresChromiumFetch`).
- Vendor is the host label `"Demon Tweeks"` (first-party shop; no sold-by node).
- Fixtures are trimmed Magento-style snippets (Cloudflare blocked live HttpClient capture); they lock the confirmed Name / INC VAT prices.
