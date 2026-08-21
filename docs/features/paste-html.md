# Feature: Paste HTML

- **Id:** `docs/features/paste-html.md`
- **Seq:** 1
- **Depends-on:** none
- **Status:** done
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/1
- **Windows:** required first
- **Related screens:** `docs/screens/products.md`, `docs/parsing/paste-html.md`, `docs/parsing/overview.md`
- **Related code:** `ProductsPage` (`AddOverlay`, `AddUrlStage`, `ContinueFromUrlStageAsync`, `ResolveExistingUrlAsync`, `SaveNewProductAsync`), `ProductAddEditor` (`BeginWithUrlAsync`, `LoadFromUrlAsync`, `ApplyPageMetadata`, `TryRead`, `FetchImages_Click`), `ProductUrl.TryCoerceHttpUrl`, `ProductPageMetadataParser.ParseHtmlAsync`, `ProductImageService` (`IsUsablePageHtml`, `FormatUnusablePageMessage`, `LoadPageAsync`), `WebCacheStore.SaveHtmlAsync`, `ProductImagePicker`, `ChromiumPageLoader`, `DialogHelper`

## Objectives

- When live fetch fails (CAPTCHA, region wall, empty HttpClient/WebView2 body), the user can still import a product by supplying page **HTML** plus the product **URL**.
- Parsers use that URL for host detection, Amazon `/dp/{ASIN}` normalisation, and relative image links.
- Live scrape stays the primary path (`Add` / Enter on the URL box). Paste HTML is a **secondary** fallback.
- **Out of scope:** GNOME and iPad shells. A nested dialog that hosts WebView2. A visible HTML editor. Paste HTML on the **details** stage. New navigation destinations. Changing overwrite / keep / cancel collision copy.

## User requirements

- URL stage secondaries: **Paste HTML** (clipboard, no visible markup) and **Open HTML file** (`.html` / `.htm`). Primary **Add** is unchanged live fetch.
- If live fetch fails on details, the user returns to the URL stage to paste or open a file. No paste control on details.
- URL required first. Invalid URL → existing `AddUrlError` (`Enter a valid http(s) product page URL.`); do not read clipboard or open a picker. Coerce with `ProductUrl.TryCoerceHttpUrl`.
- Empty clipboard / cancelled picker: clipboard → in-sheet error; picker cancel → no-op; empty/unreadable file → in-sheet error.
- After HTML is obtained, reject it if `ProductImageService.IsUsablePageHtml` is false, using the same style of message as live fetch (`FormatUnusablePageMessage` / `InvalidOperationException` message). Stay on the URL stage.
- If usable: `ResolveExistingUrlAsync` then parse with `ProductPageMetadataParser.ParseHtmlAsync(html, url)` — **no** `ChromiumPageLoader`.
- Cache HTML via `WebCacheStore.SaveHtmlAsync` (`ProductUrl.Normalize` key).
- Images: parse `<img>` / OG URLs; try HttpClient downloads as non-Autodoc live fetch does. Do not start Chromium from the paste path.
- **A product image is required before Add** on this paste/file path. If none downloaded, open details with metadata but **Add** / **Add and Close** stay disabled (or `TryRead` fails with a clear error) until `_imageBlob` is set. The user uses **Load images from product URL** (existing, may live-fetch/Chromium) or **Choose image file** on `ProductAddEditor` (local image picker — required because live fetch often already failed).
- Esc / Cancel: existing `TryDiscardAddOverlayAsync`. Enter on the URL box still means live **Add**.
- Header **Add** while the sheet is open still continues from the current URL.

## Layout

- Regular and compact: existing Add Product **sheet** (`AddOverlay`).
- URL stage: URL box | **Add** (accent) | **Paste HTML** | **Open HTML file** | **Cancel**. Wrap buttons on narrow width; do not clip. No multiline HTML field.
- Details: existing `ProductAddEditor` plus a **Choose image file** icon button next to Fetch / Clear (same 36px icon column). Collision banner unchanged (`AddExistingBanner`).
- Never host WebView2 inside a blocking dialog.

## Workflow

1. Open Add Product (`OpenAddOverlayAsync`).
2. Enter URL. `ProductUrl.TryCoerceHttpUrl`.
3. **Add** / Enter → existing `ContinueFromUrlStageAsync` / `BeginWithUrlAsync` / `ProductImagePicker.FetchPageAsync`.
4. **Paste HTML** or **Open HTML file** → validate URL, coerce into `AddUrlBox`, read clipboard or file (UTF-8).
5. If not `IsUsablePageHtml`, show unusable-page error on URL stage and stop.
6. `ResolveExistingUrlAsync(url)`. Abort → close. ShowExisting → load existing read-only, ignore HTML. Fetch → parse, cache, try image HTTP, `ShowAddDetailsStage`.
7. If no image, disable save until Fetch or Choose image file succeeds.
8. **Add** / **Add and Close** → existing `SaveNewProductAsync` once `TryRead` succeeds including image.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| URL | `ProductUrl.TryCoerceHttpUrl`, `Same`, `Normalize` | none |
| Collision | `ResolveExistingUrlAsync`, `AddExistingBanner` | none |
| Clipboard text | WinUI clipboard | helper method on `ProductsPage` (not a new service) |
| HTML file | none in app today | `FileOpenPicker` `.html`/`.htm` + window HWND (`InitializeWithWindow`) |
| Usable HTML gate | `ProductImageService.IsUsablePageHtml`, `FormatUnusablePageMessage` | make them callable from the paste path (`public` or `LoadFromHtmlAsync` throws the same exception) |
| Parse | `ProductPageMetadataParser.ParseHtmlAsync` | none |
| Cache + image HTTP | `WebCacheStore`, existing download helpers on `ProductImageService` | `ProductImageService.LoadFromHtmlAsync(url, html)` — **no** `IBrowserPageSession` / `ChromiumPageLoader` |
| Form | `ApplyPageMetadata` | `ProductAddEditor.BeginWithHtmlAsync`; optional `RequiresImage` flag for this session |
| Local product photo | none on add editor | **Choose image file** on `ProductAddEditor` (`FileOpenPicker` images); store bytes as the editor already does for fetched images |
| Save gate | `TryRead` / save buttons | require image when the session started from paste/file |

- **Wiring:** `new ProductImageService()` / `App.Database`. No DI container.
- **Data:** no migration. HTML file + `CachedWebPages`. Product image files / existing columns. No new BLOBs.
- **Ports:** Windows only. Later shells: clipboard + file on the URL stage, same usable-HTML rule, image required.

## Tests

- Project: `WorkCosts.Tests`.
- `PasteHtmlParserTests`: fixture strings (`WorkCosts.Tests/Fixtures` Amazon + Autodoc) — `ParseHtmlAsync` from string equals from file; host still selects parser.
- `LoadFromHtmlAsync` (or equivalent): usable Autodoc/Amazon fixture HTML caches without a browser; a challenge/`Just a moment` snippet throws the unusable-page error; does not use `IBrowserPageSession`.
- Empty clipboard is UI-only; skip UI automation.

## Open questions

- **Problem #1 (test, PR #1):** Paste HTML engages validation of the URL input field and does not continue.

## Accepted defaults

- Later branch: `feature/paste-html-Paste-HTML`.
- HTML files UTF-8. Image picker: common raster types the editor already displays (png/jpeg/webp as `ProductImagePicker`/`ToBitmapAsync` already allow).
- No extra size cap. No script-stripping before AngleSharp.
- `IsUsablePageHtml` length/challenge rules apply to paste (short test **parser** tests stay on `ParseHtmlAsync` without that gate).
- GBP/schema unchanged. No extra TFMs.
- `DatabaseService(string databasePath)` is allowed for isolated `LoadFromHtmlAsync` tests.

## Implementation notes for an agent

1. `WorkCosts/Pages/ProductsPage.xaml` — URL stage buttons only.
2. `ProductAddEditor.BeginWithHtmlAsync`; `TryRead`/save buttons honour required image for paste sessions.
3. `ProductImageService.LoadFromHtmlAsync` — usable check, parse, cache, optional HTTP images; never Chromium.
4. `ResolveExistingUrlAsync` before applying HTML.
5. FileOpenPicker + HWND for HTML and for Choose image file.
6. Tests in `WorkCosts.Tests` with existing Fixtures plus a small challenge-HTML sample if none exists.
7. Do not: WebView2 in a ContentDialog; paste on details; GNOME/iPad; `git add docs/features/to-review.md` on `Planning`.
