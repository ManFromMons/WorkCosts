# Feature: Source &lt;Host display name&gt;

- **Id:** `docs/features/source-<host>.md`
- **Seq:** &lt;integer from plan-feature&gt;
- **Depends-on:** none
- **Status:** draft | ready-for-agent | done
- **PR:** none
- **Windows:** required first
- **Related screens:** `docs/screens/products.md`, `docs/parsing/adding-a-source.md`, `docs/parsing/overview.md`, `docs/parsing/browser-session.md`
- **Related code:** `ProductPageMetadataParser`, `ProductUrl`, `ProductVendorHelper`, `ProductImageService`, `ProductImagePicker`, `ChromiumPageLoader`, `IsUsablePageHtml`

## Parameters (required)

- **Host family:** e.g. `halfords.com` (match on `Uri.Host`, not a vendor enum)
- **Sample product URL:** `https://…` (one real product page)
- **Expected Name:** …
- **Expected UnitPrice:** … (GBP)
- **Optional expected fields:** Manufacturer, ManufacturerReference, Vendor, Ean, Variation, OemEquivalent — only if you can see them on the page
- **Fixture:** path under `WorkCosts.Tests/Fixtures/` once captured, or “agent captures”

**Status** stays `draft` until URL + Expected Name + Expected UnitPrice are filled.

## Objectives

- Add Product (live fetch and Paste HTML) extracts **Name** and **UnitPrice** for this host from the sample URL/fixture.
- Other client fields best-effort; null if not on the page.
- **Out of scope:** new UI chrome; login-gated pages; GNOME/iPad implementation in this pass (Ports note only).

## User requirements

- User pastes this host’s product URL on the existing Add Product sheet. **Add** / Paste HTML / Open HTML file behave as today.
- Empty/error/cancel: existing URL coerce, unusable-page message, Paste HTML fallback. No new dialogs.

## Layout

- No new regions. Keep `AddOverlay` / `ProductAddEditor`. Never host WebView2 in a blocking dialog.

## Workflow

1. User enters the sample URL (`ProductUrl.TryCoerceHttpUrl`).
2. Fetch via the path this story records (HttpClient and/or Chromium like Autodoc).
3. Parse with `ProductPageMetadataParser.ParseHtmlAsync`.
4. Editor shows Name and GBP price; collision banner unchanged.
5. Esc/Cancel unchanged.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Parse | `ProductPageMetadataParser.ParseHtmlAsync` / `ParseGeneric` | `Is…Host` + dedicated parse **only if generic fails Name/price** |
| Source label | `ProductVendorHelper.InferSourceFromUrl` | one host branch returning a display string |
| URL | `ProductUrl` | normalize only if a stable product id exists |
| Fetch | `ProductImageService`, `ProductImagePicker.FetchPageAsync` | Chromium host gate **only if HttpClient is blocked** |
| Cache | `WebCacheStore` | none |

- **Wiring:** existing `new ProductImageService()` / `App.Database`. No DI container.
- **Data:** no schema change. Fixtures are files in Tests. Page cache still files + index.
- **Ports:** Swift later mirrors detector + parser + the same fixture.

## Tests

- Project: `WorkCosts.Tests`.
- Host recogniser facts (positive + a negative host).
- Fixture theory: `ParseHtmlAsync` equals **Expected Name** and **Expected UnitPrice**; optional fields as listed.
- Do not require UI automation.

## Open questions

Each item: *Assumption:* … → **Question:** …?

## Accepted defaults

- Currency GBP. Trimmed snippet fixture, not a full homepage.
- Kickoff: skill `start-add-source` or `start-implement` on this file.
- Later branch: `feature/source-<host>-<Title>`.

## Implementation notes for an agent

1. Follow skill `add-product-source` (discover fetch → fixture → failing tests → integrate).
2. Do not: login scrape; commit cookies; WebView2 in a ContentDialog; `git add` to-review on this branch; open a PR before to-review **Status** `done`.
