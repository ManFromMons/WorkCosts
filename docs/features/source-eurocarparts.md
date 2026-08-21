# Feature: Source Euro Car Parts

- **Id:** `docs/features/source-eurocarparts.md`
- **Seq:** 2
- **Depends-on:** `product-extra-data`
- **Status:** done
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/3
- **Windows:** required first
- **Related screens:** `docs/screens/products.md`, `docs/parsing/adding-a-source.md`, `docs/parsing/overview.md`, `docs/parsing/browser-session.md`, `docs/features/product-extra-data.md`
- **Related code:** `ProductPageMetadataParser`, `ProductPageClientValues`, `ProductExtra` / `ExtraYaml` (from `product-extra-data`), `ProductUrl`, `ProductVendorHelper`, `ProductImageService`, `ProductImagePicker`, `ChromiumPageLoader`, `IsUsablePageHtml`

## Parameters (required)

- **Host family:** `eurocarparts.com` (match on `Uri.Host`, not a vendor enum). Detector should treat `www.eurocarparts.com` and any `*.eurocarparts.*` host the same way Autodoc matches `autodoc.`.
- **Samples:** three product pages. Values are **user-confirmed**.

| # | Product URL | Expected Name | Expected UnitPrice (GBP) | Optional fields | Battery specs (ExtraYaml) | Fixture |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | `https://www.eurocarparts.com/p/crosland-air-filter-502110318` | Crosland Air Filter | 22.49 | Manufacturer: Crosland; Vendor: Euro Car Parts | none (not a battery — extra fields null / ExtraYaml empty) | agent captures `WorkCosts.Tests/Fixtures/eurocarparts-502110318.snippet.html` |
| 2 | `https://www.eurocarparts.com/p/bosch-s5a15-agm-stop-start-020-105ah-950cca-car-battery-3-year-guarantee-444779118` | Bosch S5A15 AGM Stop/Start 020 105AH 950CCA Car Battery - 3 Year Guarantee | 346.49 | Manufacturer: Bosch; Vendor: Euro Car Parts | `capacity: 105`; 393 × 175 × 190 mm; `cca: 950`; `technology: AGM` | agent captures `WorkCosts.Tests/Fixtures/eurocarparts-444779118.snippet.html` |
| 3 | `https://www.eurocarparts.com/p/eicher-premium-brake-disc-104110939` | Eicher Premium Brake Disc | 45.89 | Manufacturer: Eicher; Vendor: Euro Car Parts | none (not a battery — extra fields null / ExtraYaml empty) | agent captures `WorkCosts.Tests/Fixtures/eurocarparts-104110939.snippet.html` |

Visible page product numbers (`502110318`, `444779118`, `104110939`) are Euro Car Parts SKUs in the URL and under the title. Use them for fixture names. Do **not** assert them as `ManufacturerReference` (that field is the maker’s part number, which was not confirmed separately).

Sample 2 extra values: Capacity **105 Ah** and L/W/H **393 / 175 / 190 mm** from the spec list; CCA **950** and technology **AGM** from the confirmed product name (`105AH 950CCA`, `AGM`) and battery label. Normalise AGM with the helper from `product-extra-data`.

## Objectives

- Add Product (live fetch and Paste HTML) extracts **Name** and **UnitPrice** for this host from **each** sample URL/fixture, plus Manufacturer and Vendor as listed.
- Sample 2 also fills ExtraYaml battery keys. Samples 1 and 3 must **not** invent extra specs (all extra client fields null).
- Battery specs persist through **existing** `Products.ExtraYaml` from `product-extra-data`. Do not add a column or editor chrome here.
- **Out of scope:** ExtraYaml schema/UI; login-gated pages; GNOME/iPad shells in this pass; zip export/import.

## User requirements

- User pastes an `eurocarparts.com` product URL on the existing Add Product sheet. **Add** / Paste HTML / Open HTML file behave as today.
- After a successful fetch or paste, the editor shows Name, GBP unit cost, manufacturer, vendor breadcrumb, and (for sample 2) the extra-spec fields from `product-extra-data`.
- Empty/error/cancel: existing URL coerce, unusable-page message, Paste HTML fallback. No new dialogs.
- Cookie/CMP overlay is not a login wall. If HttpClient HTML is unusable, Chromium or Paste HTML as Autodoc does.

## Layout

- No new regions. Keep `AddOverlay` / `ProductAddEditor` and `ProductEditor`. Never host WebView2 in a blocking dialog.
- Regular: list beside detail. Compact: stack as today.
- Extra-spec controls are those from `product-extra-data` (always visible). Do not add a second YAML surface.

## Workflow

1. User enters a sample URL (`ProductUrl.TryCoerceHttpUrl`).
2. Fetch via discovery (HttpClient first; Chromium like Autodoc only if blocked / `IsUsablePageHtml` fails).
3. Parse with `ProductPageMetadataParser.ParseHtmlAsync` (structured extra fields, not YAML).
4. `ProductPageClientValues.From` copies non-null fields onto the editor. Save writes ExtraYaml as specified on `product-extra-data`.
5. Esc/Cancel unchanged.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Schema / YAML / editor | `ExtraYaml`, `ProductExtra`, extra client fields, extra-spec controls from `product-extra-data` | none |
| Parse | `ProductPageMetadataParser.ParseHtmlAsync` / `ParseGeneric`; technology helper from `product-extra-data` | `IsEuroCarPartsHost` + dedicated parse **if** generic fails Name/price **or** sample 2 extra asserts |
| Client mapping | `ProductPageClientValues.From` (null = leave unchanged) | fill extra fields on sample 2 only |
| Source label | `ProductVendorHelper.InferSourceFromUrl` | one host branch returning `"Euro Car Parts"` |
| URL | `ProductUrl.Normalize` (path without query) | extra rewrite **only if** discovery shows the same product under multiple slugs; do not invent `/p/{id}` unless that URL actually loads |
| Fetch | `ProductImageService`, `ProductImagePicker.FetchPageAsync` | Chromium host gate **only if HttpClient is blocked** (same pattern as Autodoc in `FetchPageAsync`) |
| Cache | `WebCacheStore` | none |

- **Wiring:** existing `new ProductImageService()` / `App.Database`. No DI container.
- **Data:** no new migration. ExtraYaml already exists when this story is picked up.
- **Ports:** Swift later mirrors detector + parser + the same fixtures. No extra TFMs.

Parse pitfalls (must not fail the sample asserts):

- Prefer the product **H1** (e.g. “Crosland Air Filter”), not the short document title (“Crosland”).
- Unit price is the main product price. Sample 3 shows “Price per brake disc” **£45.89** with the quantity selector at **2** — `UnitPrice` is **45.89**, not 91.78.
- Ignore “Frequently bought together” add-on prices (cleaner £2.99, grease £4.09 on sample 3).
- Sample 2 extra specs from the product spec list / confirmed name. Do not copy extra keys onto samples 1 and 3.
- Cookie/CMP overlay is not a login wall; if it makes HttpClient HTML unusable, use Chromium or fail through to Paste HTML as Autodoc does.

## Tests

- Project: `WorkCosts.Tests`. Shape like `AmazonPageParserTests` / `AutodocPageParserTests`.
- Host recogniser fact: `www.eurocarparts.com` is true; a negative host (`www.amazon.co.uk` or `www.halfords.com`) is false.
- One trimmed fixture per sample URL at the paths in the table.
- Fixture theory: for **each** sample, `ParseHtmlAsync` equals Expected Name, UnitPrice, Manufacturer, Vendor; extra capacity / L/W/H / cca / technology as in the ExtraYaml column (null on rows 1 and 3).
- Do not require UI automation.

## Open questions

(none)

## Accepted defaults

- Currency GBP. Trimmed snippet fixtures, not full homepages.
- Source display string `"Euro Car Parts"`. Vendor is that host label (first-party shop; no sold-by node). Accepted on scan.
- Manufacturer is the first token of `brandImage` alt, so “Eicher Premium” matches confirmed **Eicher**. Accepted on scan.
- Euro Car Parts SKU is fixture/URL id only, not a required `ManufacturerReference`.
- Quantity selector and “price per …” copy do not change `UnitPrice`.
- ExtraYaml keys and technology tokens identical to `product-extra-data`.
- Kickoff: skill `start-add-source` or `start-implement` on this file, after `product-extra-data` is **done**.
- Later branch: `feature/source-eurocarparts-Euro-Car-Parts`.

## Implementation notes for an agent

1. Do not implement until `product-extra-data` is **Status** `done`. Then follow skill `add-product-source`: discover fetch → one fixture per sample → failing tests for Name, UnitPrice, Manufacturer, Vendor, **and** ExtraYaml battery specs (sample 2 filled; 1 and 3 empty) → detector / parser / source label / Chromium gate if needed.
2. Record the chosen fetch path in to-review **Deviations** if you add this host to the Chromium list.
3. Discovery (2026-08-21): HttpClient with Chrome identity returned HTTP 200 Next.js product HTML for all three sample URLs (~760–800 KB). `IsUsablePageHtml` passes (no challenge in the prefix). **No Chromium host gate.** Generic parse is not enough: `og:title` is the short brand (“Crosland”), and there is no `product:price:amount`. Dedicated `IsEuroCarPartsHost` / `ParseEuroCarParts`. CCA is `950CCA` from the name / cranking line, not Cold Test Current ENA (920).
4. Scan accepted: manufacturer first-token of `brandImage` alt; vendor host label `"Euro Car Parts"`. `docs/parsing/overview.md` lists Euro Car Parts.
5. Do not: login scrape; commit cookies; WebView2 in a ContentDialog; a second ExtraYaml column; `git add` to-review on this branch; open a PR before to-review scan is accepted.
