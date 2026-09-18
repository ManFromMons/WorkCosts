# Feature: Source Demon Tweeks

- **Id:** `docs/features/source-demon-tweeks.md`
- **Seq:** 8
- **Depends-on:** none
- **Status:** ready-for-agent
- **PR:** none
- **Windows:** required first
- **Related screens:** `docs/screens/products.md`, `docs/parsing/adding-a-source.md`, `docs/parsing/overview.md`, `docs/parsing/browser-session.md`
- **Related code:** `ProductPageMetadataParser`, `ProductPageClientValues`, `ProductUrl`, `ProductVendorHelper`, `ProductImageService`, `ProductImagePicker`, `ChromiumPageLoader`, `IsUsablePageHtml`

## Parameters (required)

- **Host family:** `demon-tweeks.com` (match on `Uri.Host`, not a vendor enum). Detector should treat `www.demon-tweeks.com` the same way Autodoc matches `autodoc.`. Regional path prefixes (`/uk/`, `/us/`, …) are **not** the host; samples are the **UK** URLs below.
- **Samples:** three product pages. Values are **user-confirmed**.

| # | Product URL | Expected Name | Expected UnitPrice (GBP) | Optional fields | Fixture |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | `https://www.demon-tweeks.com/uk/laser-tools-racing-karting-tool-kit-36pc-t-clas8058/` | Laser Tools Racing Karting Tool Kit 36pc | 228.37 | Manufacturer: Laser Tools; ManufacturerReference: CLAS8058; Vendor: Demon Tweeks | agent captures `WorkCosts.Tests/Fixtures/demon-tweeks-clas8058.snippet.html` |
| 2 | `https://www.demon-tweeks.com/uk/pitking-products-fluid-oil-suction-syringe-500ml-pkpfs500ml/` | Pitking Products Fluid / Oil Suction Syringe - 500ml | 13.14 | Manufacturer: Pitking Products; ManufacturerReference: FS500ML; Vendor: Demon Tweeks | agent captures `WorkCosts.Tests/Fixtures/demon-tweeks-fs500ml.snippet.html` |
| 3 | `https://www.demon-tweeks.com/uk/stahlbus-oil-drain-valve-245980/` | Stahlbus Oil Drain Valve | 36.28 | Manufacturer: Stahlbus; Vendor: Demon Tweeks | agent captures `WorkCosts.Tests/Fixtures/demon-tweeks-245980.snippet.html` |

Unit price is **inc. VAT, excl. delivery** (the large **INC VAT** figure). Ignore **EX VAT**, WAS / SAVE %, Finance Available, and delivery.

Visible slug tails (`clas8058`, `pkpfs500ml`, `245980`) are site listing ids. Use them for fixture names. Sample 3 listing `245980` is **not** `ManufacturerReference`: that page is a **Thread** variant family with several Stahlbus MPNs. Do not require a selected thread. `UnitPrice` **36.28** is the displayed NOW / from inc-VAT price with no thread chosen.

## Objectives

- Add Product (live fetch and Paste HTML) extracts **Name** and **UnitPrice** for this host from **each** sample URL/fixture, plus Manufacturer, Vendor, and ManufacturerReference as listed (sample 3: ManufacturerReference null).
- Other client fields best-effort; null if not on the page. No ExtraYaml / battery specs on these samples.
- **Out of scope:** new UI chrome; login-gated pages; GNOME/iPad implementation in this pass (Ports note only); zip export/import.

## User requirements

- User pastes a `demon-tweeks.com` product URL on the existing Add Product sheet. **Add** / Paste HTML / Open HTML file behave as today.
- After a successful fetch or paste, the editor shows Name, GBP unit cost, manufacturer, vendor breadcrumb, and part number when listed.
- Empty/error/cancel: existing URL coerce, unusable-page message, Paste HTML fallback. No new dialogs.
- Cloudflare challenge is not a login wall. HttpClient HTML is expected to be unusable (`403` + `Cf-Mitigated: challenge`). Route this host through Chromium like Autodoc (`ProductImagePicker.FetchPageAsync` host check). Surface a clear error and keep **Paste HTML** as fallback if Chromium also fails.

## Layout

- No new regions. Keep `AddOverlay` / `ProductAddEditor`. Never host WebView2 in a blocking dialog.
- Regular: list beside detail. Compact: stack as today.

## Workflow

1. User enters a sample URL (`ProductUrl.TryCoerceHttpUrl`).
2. Fetch via the path this story records (HttpClient first in discovery; **Chromium** for this host because HttpClient is blocked).
3. Parse with `ProductPageMetadataParser.ParseHtmlAsync`.
4. Editor shows Name and GBP price; collision banner unchanged.
5. Esc/Cancel unchanged.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Parse | `ProductPageMetadataParser.ParseHtmlAsync` / `ParseGeneric` | `IsDemonTweeksHost` + dedicated parse **only if** generic fails Name/price on the samples |
| Source label | `ProductVendorHelper.InferSourceFromUrl` | one host branch returning `"Demon Tweeks"` |
| URL | `ProductUrl.Normalize` (path without query) | no Amazon-style rewrite; keep `/uk/` in the path |
| Fetch | `ProductImageService`, `ProductImagePicker.FetchPageAsync` | Chromium host gate **required** (HttpClient 403 Cloudflare), same pattern as Autodoc in `FetchPageAsync` |
| Cache | `WebCacheStore` | none |

- **Wiring:** existing `new ProductImageService()` / `App.Database`. No DI container.
- **Data:** no schema change. Fixtures are files in Tests. Page cache still files + index.
- **Ports:** Swift later mirrors detector + parser + the same fixtures. No extra TFMs.

Parse pitfalls (must not fail the sample asserts):

- Prefer the product **H1** (e.g. “Stahlbus Oil Drain Valve”), not the document title (“Buy … | Demon Tweeks”).
- Unit price is the main **INC VAT** product price. Do not use EX VAT, WAS, SAVE %, or finance.
- Sample 2 Name includes the slash and hyphen as confirmed: `Pitking Products Fluid / Oil Suction Syringe - 500ml`.
- Sample 3: ignore Thread options and the list of Stahlbus MPNs; do not pick a variant. `UnitPrice` is **36.28**.
- Cloudflare interstitial / `IsUsablePageHtml` failure is not product HTML.

## Tests

- Project: `WorkCosts.Tests`. Shape like `AmazonPageParserTests` / `AutodocPageParserTests`.
- Host recogniser: `www.demon-tweeks.com` / `demon-tweeks.com` true; `www.amazon.co.uk` and `www.autodoc.co.uk` false.
- One trimmed fixture per sample URL at the paths in the table.
- Fixture theory: for **each** sample, `ParseHtmlAsync` equals that row’s Expected Name, UnitPrice, Manufacturer, Vendor; ManufacturerReference as listed (null on sample 3). Other fields may be null.
- Do not require UI automation.

## Open questions

(none)

## Accepted defaults

- Currency GBP. Trimmed snippet fixtures, not full homepages.
- Source display string `"Demon Tweeks"`. Vendor is that host label (first-party shop; no sold-by node).
- Site listing id is fixture/URL id only. `ManufacturerReference` is the maker code the user confirmed (`CLAS8058`, `FS500ML`), not `245980`.
- Sample 3 “from” / unselected Thread still uses the confirmed **36.28** inc-VAT figure.
- Kickoff: skill `start-add-source` or `start-implement` on this file.
- Later branch: `feature/source-demon-tweeks-Demon-Tweeks`.

## Implementation notes for an agent

1. Follow skill `add-product-source` (discover fetch → one fixture per sample → failing tests → integrate). When tests pass, set to-review **Status** `ready-for-review` (not `done`).
2. Discovery (2026-09-18): HttpClient with Chrome identity returned **HTTP 403** and `Cf-Mitigated: challenge`. **Chromium host gate** via `RequiresChromiumFetch` (`IsAutodocHost` **or** `IsDemonTweeksHost`) in `ProductImagePicker.FetchPageAsync` / `ProductImageService.LoadPageAsync` / `ChromiumPageLoader`. Paste HTML remains the user fallback. Dedicated `IsDemonTweeksHost` / `ParseDemonTweeks`: H1 name, `.now-price` INC VAT (not WAS / EX VAT / JSON-LD), Brand table, single `MPN` (not `MPN(s)` / listing id). Vendor is the host label `"Demon Tweeks"`.
3. Dedicated parser only if generic `ParseGeneric` fails Name/price on any sample. Prefer JSON-LD and obvious DOM; GBP `decimal` is the INC VAT figure.
4. After land, `docs/parsing/overview.md` can list Demon Tweeks if a dedicated parser was required.
5. Do not: login scrape; commit cookies; WebView2 in a ContentDialog; `git add` to-review on this branch; open a PR before to-review **Status** `done`.
