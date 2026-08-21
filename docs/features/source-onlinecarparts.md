# Feature: Source Online Car Parts

- **Id:** `docs/features/source-onlinecarparts.md`
- **Seq:** 6
- **Depends-on:** `product-extra-data`
- **Status:** ready-for-agent
- **PR:** none
- **Windows:** required first
- **Related screens:** `docs/screens/products.md`, `docs/parsing/adding-a-source.md`, `docs/parsing/overview.md`, `docs/parsing/browser-session.md`, `docs/features/product-extra-data.md`
- **Related code:** `ProductPageMetadataParser`, `ProductPageClientValues`, `ProductExtra` / `ExtraYaml` (from `product-extra-data`), `ProductUrl`, `ProductVendorHelper`, `ProductImageService`, `ProductImagePicker`, `ChromiumPageLoader`, `IsUsablePageHtml`

## Parameters (required)

- **Host family:** `onlinecarparts.co.uk` (match on `Uri.Host`, not a vendor enum). Detector should treat `www.onlinecarparts.co.uk` the same way Autodoc matches `autodoc.`.
- **Not Autodoc:** `IsAutodocHost` is `autodoc.` in the host. Do **not** widen that detector to this site. Pages can look like the Autodoc family; source label and host gate stay **Online Car Parts**.
- **Samples:** three product pages. Values are **user-confirmed**. Canonical URLs have **no** fragment (`#brake-disc` is already dropped by `ProductUrl.Normalize` via `GetLeftPart(UriPartial.Path)`).

| # | Product URL | Expected Name | Expected UnitPrice (GBP) | Optional fields | ExtraYaml | Fixture |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | `https://www.onlinecarparts.co.uk/ridex-8017007.html` | RIDEX 82B0779 Brake disc for BMW 7 Series, 5 Series, 6 Series Front Axle, 347,8x30mm, 5/6x120, Vented, Cast Iron | 50.24 | Manufacturer: RIDEX; ManufacturerReference: 82B0779; Vendor: Online Car Parts; Ean: 4059191128518 | `axle: Front Axle`; `size: 347,8x30mm`; `material: Cast Iron`; `type: Vented` | agent captures `WorkCosts.Tests/Fixtures/onlinecarparts-8017007.snippet.html` |
| 2 | `https://www.onlinecarparts.co.uk/ridex-15793852.html` | RIDEX 219G0962 Tailgate strut for BMW E61 140N, 253 mm | 10.24 | Manufacturer: RIDEX; ManufacturerReference: 219G0962; Vendor: Online Car Parts; Ean: 4064138316101 | `size: 253 mm`; `material: Steel` (no `axle` / `type`) | agent captures `WorkCosts.Tests/Fixtures/onlinecarparts-15793852.snippet.html` |
| 3 | `https://www.onlinecarparts.co.uk/nty-18603255.html` | NTY NSP-BM-001 Clutch master cylinder for BMW 5 Series, 6 Series | 25.72 | Manufacturer: NTY; ManufacturerReference: NSP-BM-001; Vendor: Online Car Parts; Ean: 5902048210371 | none (no axle / size / material / type on the page) | agent captures `WorkCosts.Tests/Fixtures/onlinecarparts-18603255.snippet.html` |

Visible URL ids (`8017007`, `15793852`, `18603255`) are site listing ids. Use them for fixture names. Do **not** assert them as `ManufacturerReference` (that field is the maker’s part number: `82B0779`, `219G0962`, `NSP-BM-001`).

Unit price is **incl. VAT, excl. shipping**, per item. Sample 1 quantity selector is **2** — `UnitPrice` is **50.24**, not 100.48. Ignore similar-product cards, delivery, and “Buy now: … + £… Shipping” footer copy.

ExtraYaml keys `axle`, `size`, `material`, and `type` are **unknown bag keys** (YAML-only). Do not add editor boxes. Battery ExtraYaml keys stay empty on all three samples.

## Objectives

- Add Product (live fetch and Paste HTML) extracts **Name** and **UnitPrice** for this host from **each** sample URL/fixture, plus Manufacturer, ManufacturerReference, Vendor, and Ean as listed.
- ExtraYaml keys as in the table persist through **existing** `Products.ExtraYaml` from `product-extra-data`. Sample 3 must **not** invent extra keys. Battery spec fields stay null.
- **Out of scope:** ExtraYaml schema/UI; editor fields for axle/size/material/type; login-gated pages; GNOME/iPad shells in this pass; zip export/import; widening Autodoc host matching.

## User requirements

- User pastes an `onlinecarparts.co.uk` product URL on the existing Add Product sheet. **Add** / Paste HTML / Open HTML file behave as today.
- After a successful fetch or paste, the editor shows Name, GBP unit cost, manufacturer, part number, EAN, and vendor breadcrumb. ExtraYaml axle/size/material/type are stored, not shown as their own controls. Battery extra-spec controls from `product-extra-data` stay empty.
- Empty/error/cancel: existing URL coerce, unusable-page message, Paste HTML fallback. No new dialogs.
- Cookie/CMP overlay is not a login wall. If HttpClient HTML is unusable, Chromium or Paste HTML as Autodoc does.

## Layout

- No new regions. Keep `AddOverlay` / `ProductAddEditor` and `ProductEditor`. Never host WebView2 in a blocking dialog.
- Regular: list beside detail. Compact: stack as today.
- Extra-spec controls remain the battery row from `product-extra-data`. Do not add axle/size/material/type boxes or a YAML text box.

## Workflow

1. User enters a sample URL (`ProductUrl.TryCoerceHttpUrl`). Fragments are not part of the canonical URL (`ProductUrl.Normalize`).
2. Fetch via discovery (HttpClient first; Chromium like Autodoc only if blocked / `IsUsablePageHtml` fails).
3. Parse with `ProductPageMetadataParser.ParseHtmlAsync`.
4. `ProductPageClientValues.From` copies non-null fields onto the editor. Merge ExtraYaml unknown keys as below. Save writes ExtraYaml as specified on `product-extra-data`.
5. Esc/Cancel unchanged.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Schema / YAML / editor | `ExtraYaml`, `ProductExtra` unknown-key bag, battery spec controls from `product-extra-data` | none — no new column, no new editor fields, no first-class `ProductExtra` properties for axle/size/material/type |
| Parse | `ProductPageMetadataParser.ParseHtmlAsync` / `ParseGeneric` | `IsOnlineCarPartsHost` + dedicated parse **if** generic fails Name/price **or** ExtraYaml key asserts |
| Client mapping | `ProductPageClientValues.From` (null = leave unchanged) | unknown-key map on metadata/client if `product-extra-data` only ships battery fields (e.g. `IReadOnlyDictionary<string, string>? ExtraUnknown`). Apply merges non-null keys into the ProductExtra unknown bag; omitted keys do not delete existing YAML keys |
| Source label | `ProductVendorHelper.InferSourceFromUrl` | one host branch returning `"Online Car Parts"` |
| URL | `ProductUrl.Normalize` (path without query/fragment) | none |
| Fetch | `ProductImageService`, `ProductImagePicker.FetchPageAsync` | Chromium host gate **only if HttpClient is blocked** (same pattern as Autodoc in `FetchPageAsync`) |
| Cache | `WebCacheStore` | none |

- **Wiring:** existing `new ProductImageService()` / `App.Database`. No DI container.
- **Data:** no new migration. ExtraYaml already exists when this story is picked up.
- **Ports:** Swift later mirrors detector + parser + the same fixtures. No extra TFMs.

Parse pitfalls (must not fail the sample asserts):

- Prefer the product **H1** (full heading, including the subtitle line), not the short document title (`82B0779 RIDEX Brake disc…` / `NSP-BM-001 NTY Clutch master cylinder…`).
- Unit price is `.product__new-price` / JSON-LD `offers.price` for **this** product. Do not take similar-product prices or shipping.
- Sample 1 quantity **2** does not change `UnitPrice` (**50.24**).
- Vendor is **Online Car Parts**, not the JSON-LD seller URL (`https://www.onlinecarparts.co.uk`).
- Extra keys come from the product spec rows / confirmed H1 fragments, **only when present**:
  - Sample 1: Fitting Position → `axle`; H1 size `347,8x30mm` (keep the European comma); Material → `material`; Brake Disc Type → `type`.
  - Sample 2: Length → `size: 253 mm`; Material → `Steel`. Do **not** invent `axle` or `type`.
  - Sample 3: fabrication number and condition only — ExtraYaml extra keys all absent.
- Do not copy battery keys onto any sample.
- Do not treat this host as Autodoc. Sharing a private HTML helper with `ParseAutodoc` is allowed only if markup is the same family; detector, source string, and Chromium allowlist stay separate.

## Tests

- Project: `WorkCosts.Tests`. Shape like `AmazonPageParserTests` / `AutodocPageParserTests`.
- Host recogniser: `www.onlinecarparts.co.uk` / `onlinecarparts.co.uk` true; `www.autodoc.co.uk` and `www.amazon.co.uk` false. `IsAutodocHost("www.onlinecarparts.co.uk")` stays false.
- One trimmed fixture per sample URL at the paths in the table.
- Fixture theory: for **each** sample, `ParseHtmlAsync` equals Expected Name, UnitPrice, Manufacturer, ManufacturerReference, Vendor, Ean; ExtraYaml unknown keys as in the ExtraYaml column (sample 3: those four keys absent; all battery extra fields null).
- `size` on sample 1 is exactly `347,8x30mm` (comma, no space). Sample 2 `size` is `253 mm`.
- Do not require UI automation.

## Open questions

(none)

## Accepted defaults

- Currency GBP. Trimmed snippet fixtures, not full homepages.
- Source display string `"Online Car Parts"`. Vendor on the page is also Online Car Parts (confirmed).
- Site listing id is fixture/URL id only, not a required `ManufacturerReference`.
- Quantity selector does not change `UnitPrice`.
- ExtraYaml keys `axle` / `size` / `material` / `type` are YAML-only unknown keys. No editor chrome. Size strings stay as on the page (European comma, units).
- Battery ExtraYaml keys unused on this host’s samples.
- Kickoff: skill `start-add-source` or `start-implement` on this file, after `product-extra-data` is **done**.
- Later branch: `feature/source-onlinecarparts-Online-Car-Parts`.

## Implementation notes for an agent

1. Do not implement until `product-extra-data` is **Status** `done`. Then follow skill `add-product-source`: discover fetch → one fixture per sample → failing tests for Name, UnitPrice, Manufacturer, ManufacturerReference, Vendor, Ean, **and** ExtraYaml unknown keys (sample 1 four keys; sample 2 size+material; sample 3 those keys absent) → detector / parser / `"Online Car Parts"` source label / Chromium gate if needed.
2. If `product-extra-data` left no path for a parser to inject unknown YAML keys, add the small ExtraUnknown map on metadata/client and merge on apply. Do not add WinUI fields.
3. Record the chosen fetch path in to-review **Deviations** if you add this host to the Chromium list.
4. After land, `docs/parsing/overview.md` can list Online Car Parts next to Amazon/Autodoc if a dedicated parser was required.
5. Discovery (2026-08-21): HttpClient with Chrome identity returned HTTP 200 product HTML for all three sample URLs (~157–184 KB). `IsUsablePageHtml` passes (no challenge in the prefix). **No Chromium host gate.** Generic parse is not enough: JSON-LD `name` omits the H1 subtitle; ExtraYaml unknown keys and article-number / EAN need the product block. Dedicated `IsOnlineCarPartsHost` / `ParseOnlineCarParts`. `IsAutodocHost` stays false. Vendor is the host label `"Online Car Parts"` (JSON-LD seller is the shop URL). Sample 1 live `.product__new-price` on this date was **£49.96**; fixtures lock the confirmed **£50.24**. Size on sample 1 is the H1 fragment `347,8x30mm` (not Diameter + thickness). `ProductPageMetadata.ExtraUnknown` merges into `ProductExtra.UnknownKeys` on apply.
6. Do not: widen `IsAutodocHost`; login scrape; commit cookies; WebView2 in a ContentDialog; editor boxes for axle/size/material/type; a second ExtraYaml column; `git add` to-review on this branch; open a PR before to-review **Status** `done`.
