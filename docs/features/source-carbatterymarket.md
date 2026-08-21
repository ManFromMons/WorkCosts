# Feature: Source Car Battery Market

- **Id:** `docs/features/source-carbatterymarket.md`
- **Seq:** 3
- **Depends-on:** `product-extra-data`
- **Status:** ready-for-agent
- **PR:** none
- **Windows:** required first
- **Related screens:** `docs/screens/products.md`, `docs/parsing/adding-a-source.md`, `docs/parsing/overview.md`, `docs/parsing/browser-session.md`, `docs/features/product-extra-data.md`
- **Related code:** `ProductPageMetadataParser`, `ProductPageClientValues`, `ProductExtra` / `ExtraYaml` (from `product-extra-data`), `ProductUrl`, `ProductVendorHelper`, `ProductImageService`, `ProductImagePicker`, `ChromiumPageLoader`, `IsUsablePageHtml`

## Parameters (required)

- **Host family:** `carbatterymarket.co.uk` (match on `Uri.Host`, not a vendor enum). Detector should treat `www.carbatterymarket.co.uk` the same way Autodoc matches `autodoc.`.
- **Samples:** three product pages. Values are **user-confirmed**.

| # | Product URL | Expected Name | Expected UnitPrice (GBP) | Optional fields | Battery specs (after persist) | Fixture |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | `https://carbatterymarket.co.uk/yuasa/ybx5020` | Yuasa YBX5020 12V 110Ah 900A/EN Car Battery - Type 020 | 148.97 | Manufacturer: Yuasa; ManufacturerReference: YBX5020; Vendor: Car Battery Market | `capacity: 110`; 393 × 175 × 190 mm; `cca: 950`; `technology: Wet` | agent captures `WorkCosts.Tests/Fixtures/carbatterymarket-ybx5020.snippet.html` |
| 2 | `https://carbatterymarket.co.uk/dynamp/de110` | Dynamp DE110 SMF 110Ah 850CCA 12V Car Battery (Type 020) | 98.50 | Manufacturer: Dynamp; ManufacturerReference: DE110; Vendor: Car Battery Market | `capacity: 110`; 393 × 174 × 189 mm; `cca: 850`; `technology: SMF` | agent captures `WorkCosts.Tests/Fixtures/carbatterymarket-de110.snippet.html` |
| 3 | `https://carbatterymarket.co.uk/bosch/s5-a13` | Bosch S5A13 Start-Stop AGM 95Ah 850A Type 019 12V Car Battery | 167.52 | Manufacturer: Bosch; ManufacturerReference: S5A13; Vendor: Car Battery Market | `capacity: 95`; 353 × 175 × 190 mm; `cca: 850`; `technology: AGM` | agent captures `WorkCosts.Tests/Fixtures/carbatterymarket-s5-a13.snippet.html` |

Unit price is the **current** product price (incl. VAT, excl. delivery). Ignore RRP, PayPal instalments, and the extended-warranty add-on.

Sample 1 title says `900A/EN`; Expected CCA is **950** from the spec table.

## Objectives

- Add Product (live fetch and Paste HTML) extracts **Name** and **UnitPrice** for this host from **each** sample, plus Manufacturer, ManufacturerReference, Vendor, and the battery specs in that row.
- Battery specs persist through **existing** `Products.ExtraYaml` from `product-extra-data` (same keys and technology tokens). Do not add a column or editor chrome here.
- **Out of scope:** ExtraYaml schema/UI (that story); new navigation destinations; login-gated pages; GNOME/iPad shells in this pass; filling ExtraYaml from other hosts in this story.

## User requirements

- User pastes a `carbatterymarket.co.uk` product URL on the existing Add Product sheet. **Add** / Paste HTML / Open HTML file behave as today.
- After a successful fetch or paste, the editor shows Name, GBP unit cost, manufacturer, part number, vendor breadcrumb, and the extra-spec fields from `product-extra-data`.
- Parser normalises technology with the helper from `product-extra-data`. Unrecognised technology is left empty.
- Empty/error/cancel: existing URL coerce, unusable-page message, Paste HTML fallback. No new dialogs.
- Cookie/CMP overlay is not a login wall. If HttpClient HTML is unusable, Chromium or Paste HTML as Autodoc does.

## Layout

- No new pages. Keep `AddOverlay` / `ProductAddEditor` and `ProductEditor` detail. Never host WebView2 in a blocking dialog.
- Regular: list beside detail. Compact: stack as today.
- Extra-spec controls are those from `product-extra-data` (always visible). Do not add a second YAML surface.

## Workflow

1. User enters a sample URL (`ProductUrl.TryCoerceHttpUrl`).
2. Fetch via discovery (HttpClient first; Chromium like Autodoc only if blocked / `IsUsablePageHtml` fails).
3. Parse with `ProductPageMetadataParser.ParseHtmlAsync` (structured battery fields, not YAML).
4. `ProductPageClientValues.From` copies non-null fields onto the editor (`ApplyPageMetadata`). Null means do not overwrite that control.
5. Save uses existing ExtraYaml serialisation from `product-extra-data`.
6. Esc/Cancel unchanged.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Schema / YAML / editor | `ExtraYaml`, `ProductExtra`, extra client fields, extra-spec controls from `product-extra-data` | none |
| Parse | `ProductPageMetadataParser.ParseHtmlAsync` / `ParseGeneric`; technology helper from `product-extra-data` | `IsCarBatteryMarketHost` + dedicated parse **if** generic fails Name/price **or** the battery spec asserts |
| Client mapping | `ProductPageClientValues.From` (null = leave unchanged) | fill the extra fields that `product-extra-data` already added |
| Source label | `ProductVendorHelper.InferSourceFromUrl` | one host branch returning `"Car Battery Market"` |
| URL | `ProductUrl.Normalize` (path without query) | no Amazon-style rewrite |
| Fetch | `ProductImageService`, `ProductImagePicker.FetchPageAsync` | Chromium host gate **only if HttpClient is blocked** |
| Cache | `WebCacheStore` | none |

- **Wiring:** existing `new ProductImageService()` / `App.Database`. No DI container.
- **Data:** no new migration. ExtraYaml already exists when this story is picked up.
- **Ports:** Swift later mirrors detector + parser + the same fixtures. No extra TFMs.

Sample 1 ExtraYaml after persist matches `product-extra-data` (`capacity: 110`, 393 × 175 × 190, `cca: 950`, `technology: Wet`). Sample 1 page “Standard Wet Battery” → `Wet`. Sample 2 “SMF” → `SMF`. Sample 3 “AGM” → `AGM`.

Parse pitfalls:

- Prefer the product **H1**, not a shorter document title.
- Unit price is the large current price, not RRP, not “3 payments of …”, not the warranty checkbox price.
- Specs come from the **spec table** (and matching Technical Specifications list), not “Special buy” sidebars.
- Do not persist raw “Standard Wet Battery” — persist `Wet`.

## Tests

- Project: `WorkCosts.Tests`.
- Host recogniser: `carbatterymarket.co.uk` / `www.carbatterymarket.co.uk` true; `www.amazon.co.uk` false.
- One trimmed fixture per sample URL.
- Fixture theory: for **each** sample, `ParseHtmlAsync` equals Expected Name, Manufacturer, ManufacturerReference, Vendor, capacity, length/width/height mm, cca, and **normalised** technology. Assert that `UnitPrice` is present; do **not** assert a GBP amount (shop prices change).
- Do not require UI automation. YAML helper and contract-test extra fields ship in `product-extra-data`.

## Open questions

(none)

## Accepted defaults

- Currency GBP. Trimmed snippet fixtures, not full homepages.
- Source display string `"Car Battery Market"`.
- ExtraYaml keys and technology tokens identical to `product-extra-data`.
- Source tests do not lock a GBP unit price; parser still fills `UnitPrice` from the current `.product--price`.
- Kickoff: skill `start-add-source` or `start-implement` on this file, after `product-extra-data` is **done**.
- Later branch: `feature/source-carbatterymarket-Car-Battery-Market`.

## Implementation notes for an agent

1. Do not implement until `product-extra-data` is **Status** `done`. Then follow skill `add-product-source`: discover fetch → one fixture per sample → failing tests for Name, **presence of** UnitPrice (not a GBP amount), **and** battery specs (normalised technology) on all three → detector / parser / source label / Chromium gate if needed.
2. Record the chosen fetch path in to-review **Deviations** if this host is added to the Chromium list.
3. Discovery (2026-08-21): HttpClient with Chrome identity returned HTTP 200 Shopware product HTML for all three sample URLs (~150–240 KB). `IsUsablePageHtml` passes (no challenge in the prefix). **No Chromium host gate.** Generic parse is not enough: no `product:price:amount`; battery specs and manufacturer reference need the properties table / Technical Specifications list. Dedicated `IsCarBatteryMarketHost` / `ParseCarBatteryMarket`. CCA is the spec-table value (sample 1 **950**, not title `900A/EN`). Technology: H1 then Technical Specifications then table, so sample 2 **SMF** (name / list) not table **Lead**; sample 1 table “Standard Wet Battery” → `Wet`. Vendor is the host label `"Car Battery Market"`. ManufacturerReference is Model / MPN, else the token after the brand in the H1. Source tests do not assert a GBP amount.
4. Do not: login scrape; commit cookies; WebView2 in a ContentDialog; a second ExtraYaml column; `git add` to-review on this branch; open a PR before to-review **Status** `done`.
