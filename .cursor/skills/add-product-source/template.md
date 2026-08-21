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
- **Samples:** **at least three** product pages. Values are **user-confirmed** (see `.cursor/skills/add-product-source/confirm-samples.md`). Do not invent them from an unconfirmed scrape.

| # | Product URL | Expected Name | Expected UnitPrice (GBP) | Optional fields | Fixture |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | `https://…` | … | … | Manufacturer / Vendor / Ean / … or — | path or “agent captures” |
| 2 | `https://…` | … | … | … | … |
| 3 | `https://…` | … | … | … | … |

Optional columns: Manufacturer, ManufacturerReference, Vendor, Ean, Variation, OemEquivalent — only if the user confirmed them on that page.

**Status** stays `draft` until all three rows have URL + Expected Name + Expected UnitPrice confirmed by the user.

## Objectives

- Add Product (live fetch and Paste HTML) extracts **Name** and **UnitPrice** for this host from **each** sample URL/fixture.
- Other client fields best-effort; null if not on the page.
- **Out of scope:** new UI chrome; login-gated pages; GNOME/iPad implementation in this pass (Ports note only).

## User requirements

- User pastes this host’s product URL on the existing Add Product sheet. **Add** / Paste HTML / Open HTML file behave as today.
- Empty/error/cancel: existing URL coerce, unusable-page message, Paste HTML fallback. No new dialogs.

## Layout

- No new regions. Keep `AddOverlay` / `ProductAddEditor`. Never host WebView2 in a blocking dialog.

## Workflow

1. User enters a sample URL (`ProductUrl.TryCoerceHttpUrl`).
2. Fetch via the path this story records (HttpClient and/or Chromium like Autodoc).
3. Parse with `ProductPageMetadataParser.ParseHtmlAsync`.
4. Editor shows Name and GBP price; collision banner unchanged.
5. Esc/Cancel unchanged.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Parse | `ProductPageMetadataParser.ParseHtmlAsync` / `ParseGeneric` | `Is…Host` + dedicated parse **only if generic fails Name/price on the samples** |
| Source label | `ProductVendorHelper.InferSourceFromUrl` | one host branch returning a display string |
| URL | `ProductUrl` | normalize only if a stable product id exists |
| Fetch | `ProductImageService`, `ProductImagePicker.FetchPageAsync` | Chromium host gate **only if HttpClient is blocked** |
| Cache | `WebCacheStore` | none |

- **Wiring:** existing `new ProductImageService()` / `App.Database`. No DI container.
- **Data:** no schema change. Fixtures are files in Tests. Page cache still files + index.
- **Ports:** Swift later mirrors detector + parser + the same fixtures.

## Tests

- Project: `WorkCosts.Tests`.
- Host recogniser facts (positive + a negative host).
- One trimmed fixture per sample URL.
- Fixture theory: for **each** sample, `ParseHtmlAsync` equals that row’s **Expected Name** and **Expected UnitPrice**; optional fields as listed.
- Do not require UI automation.

## Open questions

Each item: *Assumption:* … → **Question:** …?

## Accepted defaults

- Currency GBP. Trimmed snippet fixtures, not full homepages.
- Kickoff: skill `start-add-source` or `start-implement` on this file.
- Later branch: `feature/source-<host>-<Title>`.

## Implementation notes for an agent

1. Follow skill `add-product-source` (discover fetch → one fixture per sample → failing tests → integrate). When tests pass, set to-review **Status** `ready-for-review` (not `done`).
2. Do not: login scrape; commit cookies; WebView2 in a ContentDialog; `git add` to-review on this branch; open a PR before to-review **Status** `done`.
