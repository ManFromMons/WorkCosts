# Feature: Source Tayna

- **Id:** `docs/features/source-tayna.md`
- **Seq:** 4
- **Depends-on:** `product-extra-data`
- **Status:** done
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/5
- **Windows:** required first
- **Related screens:** `docs/screens/products.md`, `docs/parsing/adding-a-source.md`, `docs/parsing/overview.md`, `docs/parsing/browser-session.md`, `docs/data/schema.md`
- **Related code:** `ProductPageMetadataParser`, `ProductPageClientValues`, `ProductExtra` / `ExtraYaml` (from `product-extra-data`), `ProductUrl`, `ProductVendorHelper`, `ProductImageService`, `ProductImagePicker`, `ChromiumPageLoader`, `IsUsablePageHtml`

## Parameters (required)

- **Host family:** `tayna.co.uk` (match on `Uri.Host`, not a vendor enum). Detector should treat `www.tayna.co.uk` the same way Autodoc matches `autodoc.`.
- **Samples:** three product pages. Values are **user-confirmed**. Canonical URLs have **no** tracking query (`msclkid` and similar are stripped by existing `ProductUrl.Normalize`).

| # | Product URL | Expected Name | Expected UnitPrice (GBP) | Optional fields | Battery specs (ExtraYaml) | Fixture |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | `https://www.tayna.co.uk/motorcycle-batteries/exide/e60-n30l-b/` | EXIDE E60-N30L-B 12V CONVENTIONAL MOTORCYCLE BATTERY | 91.73 | Manufacturer: Exide; ManufacturerReference: E60-N30L-B; Vendor: Tayna; Ean: 3661024033596 | `capacity: 30`; 185 × 130 × 170 mm; `cca: 300`; `technology: Wet` | agent captures `WorkCosts.Tests/Fixtures/tayna-e60-n30l-b.snippet.html` |
| 2 | `https://www.tayna.co.uk/car-batteries/bosch/s5a11/` | S5 A11 BOSCH AGM CAR BATTERY 12V 80AH TYPE 115 S5A11 | 136.88 | Manufacturer: Bosch; ManufacturerReference: S5 A11; Vendor: Tayna; Ean: 4047025244350 | `capacity: 80`; 315 × 175 × 190 mm; `cca: 800`; `technology: AGM` | agent captures `WorkCosts.Tests/Fixtures/tayna-s5a11.snippet.html` |
| 3 | `https://www.tayna.co.uk/car-batteries/bosch/s4013/` | S4 013 BOSCH CAR BATTERY 12V 95AH TYPE 019 S4013 | 97.78 | Manufacturer: Bosch; ManufacturerReference: S4 013; Vendor: Tayna; Ean: 4047023479471 | `capacity: 95`; 353 × 175 × 190 mm; `cca: 800`; `technology: Wet` | agent captures `WorkCosts.Tests/Fixtures/tayna-s4013.snippet.html` |

Unit price is **inc. VAT, excl. delivery**. Ignore Standard Delivery, PayPal instalments, “Star Buy” / alternative batteries, and “Also Add…” accessories.

Sample 1 is **out of stock**: there is no visible add-to-basket price. Expected UnitPrice **91.73** is the page’s product price metadata (`product:price:amount` / `twitter:data1`). Keep that fallback for this host when the buy box has no price.

Height on Tayna is labelled **Height inc. terms** — that value is `heightMm`.

## Objectives

- Add Product (live fetch and Paste HTML) extracts **Name** and **UnitPrice** for this host from **each** sample, plus Manufacturer, ManufacturerReference, Vendor, Ean, and the battery specs in that row.
- Battery specs persist through the **existing** `Products.ExtraYaml` camelCase YAML from `product-extra-data` (same keys and technology tokens). Do not add another column.
- **Out of scope:** new UI chrome (editor ExtraYaml controls ship with `product-extra-data`); login-gated pages; GNOME/iPad shells in this pass; zip export/import.

## User requirements

- User pastes a `tayna.co.uk` product URL on the existing Add Product sheet. **Add** / Paste HTML / Open HTML file behave as today.
- After a successful fetch or paste, the editor shows Name, GBP unit cost, manufacturer, part number, EAN, vendor breadcrumb, and the battery spec fields already added for ExtraYaml.
- Empty/error/cancel: existing URL coerce, unusable-page message, Paste HTML fallback. No new dialogs.
- Cookie/CMP (OneTrust) is not a login wall. If HttpClient HTML is unusable, Chromium or Paste HTML as Autodoc does.

## Layout

- No new regions. Keep `AddOverlay` / `ProductAddEditor` and `ProductEditor`. Never host WebView2 in a blocking dialog.
- Regular: list beside detail. Compact: stack as today.
- Battery spec controls are those from `product-extra-data` (always visible). Do not add a second YAML/raw-spec surface.

## Workflow

1. User enters a sample URL (`ProductUrl.TryCoerceHttpUrl`). Query strings are not part of the canonical URL (`ProductUrl.Normalize`).
2. Fetch via discovery (HttpClient first; Chromium like Autodoc only if blocked / `IsUsablePageHtml` fails).
3. Parse with `ProductPageMetadataParser.ParseHtmlAsync`.
4. `ProductPageClientValues.From` copies non-null fields onto the editor. Save writes ExtraYaml as already specified on `product-extra-data`.
5. Esc/Cancel unchanged.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Schema / YAML / editor | `ExtraYaml`, `ProductExtra`, YamlDotNet camelCase, battery spec controls from `product-extra-data` | none |
| Parse | `ProductPageMetadataParser.ParseHtmlAsync` / `ParseGeneric` | `IsTaynaHost` + dedicated parse **if** generic fails Name/price **or** battery spec / metadata-price asserts |
| Client mapping | `ProductPageClientValues` structured extra fields from `product-extra-data` | none |
| Source label | `ProductVendorHelper.InferSourceFromUrl` | one host branch returning `"Tayna"` |
| URL | `ProductUrl.Normalize` (path without query) | none — sample 1 must not keep `msclkid` |
| Fetch | `ProductImageService`, `ProductImagePicker.FetchPageAsync` | Chromium host gate **only if HttpClient is blocked** |
| Cache | `WebCacheStore` | none |

- **Wiring:** existing `new ProductImageService()` / `App.Database`. No DI container.
- **Data:** no new migration in this story. ExtraYaml already exists when this story is picked up.
- **Ports:** Swift later mirrors detector + parser + the same fixtures. No extra TFMs.

Price:

- Prefer the visible product price next to **ADD TO BASKET** (samples 2 and 3: `£ 136.88` / `£ 97.78`).
- If that price is missing (out of stock / no buy box), use Open Graph / product meta `product:price:amount` in GBP (sample 1: `91.73`).
- Do not use delivery, star-buy, accessory, or instalment amounts.

Battery specs from the **Technical Specification** table (not star-buy cards). Technology normalisation is the helper from `product-extra-data`. Sample 1 and 3 page text is `Wet` → `Wet`. Sample 2 `AGM` → `AGM`.

Parse pitfalls:

- Prefer the product **H1** (Tayna uses uppercase H1s as in the sample Names).
- “Height inc. terms” is `heightMm`.
- Sample 1 fixture must still contain enough markup for Name, metadata price, and the spec table even though the buy box has no price.

## Tests

- Project: `WorkCosts.Tests`.
- Host recogniser: `www.tayna.co.uk` / `tayna.co.uk` true; `www.amazon.co.uk` false.
- One trimmed fixture per sample URL (include `product:price:amount` on sample 1).
- Fixture theory: for **each** sample, `ParseHtmlAsync` equals Expected Name, UnitPrice, Manufacturer, ManufacturerReference, Vendor, Ean, capacity, length/width/height mm, cca, and normalised technology.
- Do not require UI automation.

## Open questions

(none)

## Accepted defaults

- Currency GBP. Trimmed snippet fixtures, not full homepages.
- Source display string `"Tayna"`.
- ExtraYaml keys and technology tokens identical to `product-extra-data`.
- Kickoff: skill `start-add-source` or `start-implement` on this file, after `product-extra-data` is **done**.
- Later branch: `feature/source-tayna-Tayna`.

## Implementation notes for an agent

1. Do not implement until `product-extra-data` is **Status** `done` (ExtraYaml + editor + client fields exist). Then follow skill `add-product-source`: discover fetch → one fixture per sample → failing tests for Name, UnitPrice, optional fields, **and** battery specs on all three → `IsTaynaHost` / parser / `"Tayna"` source label / Chromium gate if needed.
2. Record the chosen fetch path in to-review **Deviations** if this host is added to the Chromium list.
3. Discovery (2026-08-21): HttpClient with Chrome identity returned HTTP 200 product HTML for all three sample URLs (~155–157 KB). `IsUsablePageHtml` passes (no challenge in the prefix). **No Chromium host gate.** Generic parse is not enough: `og:title` is title case; the confirmed Names are the CSS-uppercase H1s. Dedicated `IsTaynaHost` / `ParseTayna`. Price is `#prodprice` in `.pricing-holder` (not `#pandpprice`, Star Buy, or Also Add); sample 1 fixture omits the buy box and uses `product:price:amount` **91.73**. ManufacturerReference is **Product Code** when present (`S5 A11`, `S4 013`), else the token after the brand in the H1 (`E60-N30L-B`). Height is **Height inc. terms**. Vendor is the host label `"Tayna"` (first-party shop; no sold-by node). Live sample 1 was In Stock on this date; the fixture still models the specified out-of-stock metadata-price path.
4. Do not: login scrape; commit cookies; WebView2 in a ContentDialog; a second extra-info column; `git add` to-review on this branch; open a PR before to-review **Status** `done`.
