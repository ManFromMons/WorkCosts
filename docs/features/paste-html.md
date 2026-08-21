# Feature: Paste HTML

- **Id:** `docs/features/paste-html.md`
- **Seq:** 1
- **Depends-on:** none
- **Status:** done
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/1
- **Windows:** required first
- **Related screens:** `docs/screens/products.md`, `docs/parsing/paste-html.md`, `docs/parsing/overview.md`
- **Related code:** `ProductsPage` (`AddOverlay`, `AddUrlStage`, `SkipUrlStage_Click`, `ContinueFromHtmlAsync`, `ContinueFromUrlStageAsync`, `ResolveExistingUrlAsync`, `SaveNewProductAsync`, `Page_PreviewKeyDown`), `ProductAddEditor` (`BeginWithHtmlAsync`, `BeginWithUrlAsync`, `LoadEmpty`, `TryCancelUrlEdit`, `ChooseFromLoadedImagesAsync`, `ApplyPageMetadata`, `TryRead`), `ProductEditor` (`ChooseCachedImages_Click`, `TryGetCachedImagesAsync` via service), `ProductUrl.TryCoerceHttpUrl`, `ProductPageMetadataParser` (`ParseHtmlAsync`, `FindPageUrlAsync`), `ProductImageService` (`IsUsablePageHtml`, `FormatUnusablePageMessage`, `LoadFromHtmlAsync`, `TryGetCachedImagesAsync`), `WebCacheStore.SaveHtmlAsync`, `ProductImagePicker` (`ChooseFromCandidatesAsync`, `FetchPageAsync`), `ChromiumPageLoader`, `DialogHelper`, `StartupLog`

## Objectives

- When live fetch fails (CAPTCHA, region wall, empty HttpClient/WebView2 body), the user can still import a product by supplying page **HTML**. The product **URL** comes from that HTML when present.
- Parsers use that URL for host detection, Amazon `/dp/{ASIN}` normalisation, and relative image links.
- Live scrape stays the primary path (`Add` / Enter on the URL box). Paste HTML is a **secondary** fallback.
- **Out of scope:** GNOME and iPad shells. A nested dialog that hosts WebView2. A visible HTML editor. Paste HTML on the **details** stage. New navigation destinations. Changing overwrite / keep / cancel collision copy.

## User requirements

- URL stage secondaries: **Paste HTML** (clipboard, no visible markup), **Open HTML file** (`.html` / `.htm`), and **Skip**. Primary **Add** is unchanged live fetch from the URL box.
- If live fetch fails on details, the user returns to the URL stage to paste or open a file. No paste control on details.
- **Paste HTML and Open HTML file ignore the URL box.** Do not call `TryGetCoercedAddUrl`. Do not show URL-field validation or focus the box.
- Product URL is taken from the HTML, in order: canonical, `og:url`, `base href`, then IE `saved from url=(…)https://…` (`ProductPageMetadataParser.FindPageUrlAsync`). If none: in-sheet error *This HTML has no product page URL. Use Skip…* — stay on the URL stage, no focus steal.
- **Skip** continues to an empty details form (`ProductAddEditor.LoadEmpty()`). Image is **not** required on Skip (same as live Add without paste).
- Empty clipboard / cancelled picker: clipboard → in-sheet error; picker cancel → no-op; empty/unreadable file → in-sheet error.
- After HTML is obtained, reject it if `ProductImageService.IsUsablePageHtml` is false, using the same style of message as live fetch (`FormatUnusablePageMessage` / `InvalidOperationException` message). Stay on the URL stage.
- If usable: `ResolveExistingUrlAsync` on the discovered URL, then `BeginWithHtmlAsync` — **no** `ChromiumPageLoader`.
- Cache HTML via `WebCacheStore.SaveHtmlAsync` (`ProductUrl.Normalize` key).
- Images: parse `<img>` / OG URLs; try HttpClient downloads. Do not start Chromium from the paste path. Image HTTP is **best-effort**: failed or timed-out downloads must not discard parsed metadata or leave the sheet busy.
- After a successful paste load, if more than one image was captured, open the existing **Select product image** grid (`ProductImagePicker.ChooseFromCandidatesAsync`) over those candidates. One image is applied without a grid. Zero images: details stay open; save stays disabled until an image is set.
- **A product image is required before Add** on this paste/file path. If none downloaded, open details with metadata but **Add** / **Add and Close** stay disabled (or `TryRead` fails with a clear error) until `_imageBlob` is set.
- **Load images from product URL** (globe) on the add sheet: if this session already has downloaded candidates, reopen that grid (do not live-refetch). If none, existing live fetch / Chromium path.
- **Choose from downloaded images** (pictures glyph) on the add sheet: reopen the same grid over this session’s candidates. If none, `FileOpenPicker` for a local raster (png/jpeg/webp).
- Product **detail** (`ProductEditor`): the top icon next to the thumbnail is **Choose from downloaded images**, not live fetch. It opens the grid over page-cache images for the committed URL (`ProductImageService.TryGetCachedImagesAsync`). If none, file picker. Live fetch stays on **Load from new URL…** / Go.
- Double-click a thumbnail in the image grid: select that image and close the chooser (same as **Use image**). Cancel leaves the previous image.
- Esc / back: if the add-sheet **URL field is in edit mode** (display clicked → box + Accept), Esc **cancels that edit only** — restore the previous URL text, return to the clickable display, do not close the sheet (`ProductAddEditor.TryCancelUrlEdit` before `TryDiscardAddOverlayAsync`). A further Esc still runs `TryDiscardAddOverlayAsync`. Enter on the URL stage still means live **Add**.
- Header **Add** while the sheet is open still continues from the current URL.

### Data protection

- Image download must not wipe a successful HTML parse. Timeouts and HTTP/CDN failures are skipped and logged; the form stays on details with name/price/etc.
- The add sheet must not stay in a busy/spinner state after an exception (`finally` clears fetch busy and overlay status).
- Esc on URL-edit must not discard the in-progress add form; it only reverts the URL text.
- On product detail, cancelling the image chooser must not change the committed URL or persist. Choosing from cache only updates the image when the user confirms.

## Layout

- Regular and compact: existing Add Product **sheet** (`AddOverlay`).
- URL stage: URL box | **Add** (accent) | **Paste HTML** | **Open HTML file** | **Skip** | **Cancel**. Wrap buttons on narrow width; do not clip. No multiline HTML field.
- Add details image column (36px): globe (load / reopen downloaded), pictures (downloaded images or file), clear. Collision banner unchanged (`AddExistingBanner`).
- Product detail image column: pictures (downloaded / file), clear, open-in-browser. Do not put live-fetch on the top icon.
- Image chooser is a dialog over the add overlay / page, not a nested WebView.
- Never host WebView2 inside a blocking dialog.

## Workflow

1. Open Add Product (`OpenAddOverlayAsync`).
2. **Add** / Enter → coerce URL box (`ProductUrl.TryCoerceHttpUrl`) → existing `ContinueFromUrlStageAsync` / `BeginWithUrlAsync` / `ProductImagePicker.FetchPageAsync`.
3. **Paste HTML** / **Open HTML file** → read clipboard or UTF-8 file (no URL-box check) → `FindPageUrlAsync` → usable-HTML gate → `ResolveExistingUrlAsync` → `BeginWithHtmlAsync`.
4. **Skip** → empty details, image not required.
5. After paste parse: show details; if several images downloaded, show **Select product image**. Double-click or **Use image** applies; Cancel keeps the first (or none if zero downloaded).
6. Save stays disabled on the paste path until `_imageBlob` is set. User picks from downloaded images, live-fetches with the globe when the session has no candidates, or chooses a file.
7. **Add** / **Add and Close** → existing `SaveNewProductAsync` once `TryRead` succeeds (including image when required).
8. Collision: Abort → close. ShowExisting → load existing, ignore HTML. Fetch → parse, cache, try image HTTP.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Live-fetch URL | `ProductUrl.TryCoerceHttpUrl`, `Same`, `Normalize` | none (URL stage **Add** only) |
| URL from HTML | canonical / `og:url` / `base href` | `ProductPageMetadataParser.FindPageUrlAsync` (also `saved from url=(…)`) |
| Paste ignores URL box | URL-stage buttons | do **not** call `TryGetCoercedAddUrl` on Paste/Open |
| Skip | `LoadEmpty` | URL-stage **Skip** |
| Collision | `ResolveExistingUrlAsync`, `AddExistingBanner` | none |
| Clipboard text | WinUI clipboard | helper method on `ProductsPage` (not a new service) |
| HTML file | `FileOpenPicker` + HWND | none |
| Usable HTML gate | `ProductImageService.IsUsablePageHtml`, `FormatUnusablePageMessage` | `LoadFromHtmlAsync` throws the same exception |
| Parse | `ProductPageMetadataParser.ParseHtmlAsync` | none |
| Cache + image HTTP | `WebCacheStore`, `DownloadImagesAsync` | `LoadFromHtmlAsync(url, html)` — **no** Chromium; per-image **12s** timeout; log skips; on total failure return metadata + **zero** images (do not throw) |
| Diagnostics | `StartupLog` | also `Console` / `Debug`; `ProductImageService` traces URL counts, HTTP status, sizes, exceptions |
| Form | `ApplyPageMetadata` | `BeginWithHtmlAsync`; `RequiresImage` for paste/file sessions |
| Chooser | `ChooseFromCandidatesAsync` | double-click = accept; thumbnail decode failures skip that tile |
| Downloaded images on add | session `_loadedImages` | globe/pictures reopen the grid when candidates exist |
| Downloaded images on detail | page cache | `TryGetCachedImagesAsync` — **no** live fetch |
| Local product photo | `FileOpenPicker` images | fallback when no downloaded candidates |
| URL-edit Esc | `Page_PreviewKeyDown` | `TryCancelUrlEdit()` **before** `TryDiscardAddOverlayAsync`; restore `_urlBeforeEdit` |
| Save gate | `TryRead` / save buttons | require image when the session started from paste/file |

- **Wiring:** `new ProductImageService()` / `App.Database`. No DI container.
- **Data:** no migration. HTML file + `CachedWebPages`. Product image files / existing columns. No new BLOBs.
- **Ports:** Windows shipped first. Later shells: clipboard + file + Skip on the URL stage, URL from HTML, same usable-HTML rule, image required on paste, chooser over downloaded images, Esc cancels URL-edit before dismissing the sheet.

## Tests

- Project: `WorkCosts.Tests`.
- `PasteHtmlParserTests`: fixture strings (`WorkCosts.Tests/Fixtures` Amazon + Autodoc) — `ParseHtmlAsync` from string equals from file; host still selects parser.
- `FindPageUrlAsync`: canonical, `og:url`, `base href`, `saved from url=(…)https://…`.
- `LoadFromHtmlAsync`: usable Autodoc/Amazon fixture HTML caches without a browser; a challenge/`Just a moment` snippet throws the unusable-page error; does not use `IBrowserPageSession`; image-download failure does not throw if HTML was usable.
- Empty clipboard is UI-only; skip UI automation.

## Open questions

none.

## Accepted defaults

- Branch that shipped: `feature/paste-html-Paste-HTML`. Squash PR #1.
- HTML files UTF-8. Image picker: png/jpeg/webp as `ProductImagePicker`/`ToBitmapAsync` already allow.
- No extra size cap. No script-stripping before AngleSharp.
- `IsUsablePageHtml` length/challenge rules apply to paste (short test **parser** tests stay on `ParseHtmlAsync` without that gate).
- GBP/schema unchanged. No extra TFMs.
- `DatabaseService(string databasePath)` is allowed for isolated `LoadFromHtmlAsync` tests.
- **Problem #1 (test, PR #1):** Paste HTML engaged URL-box validation and did not continue. **Accepted:** Paste/Open ignore the URL box; URL comes from HTML; Skip when there is no URL.

## Implementation notes for an agent

Shipped on Windows in PR #1. Later ports and maintenance must keep this behaviour:

1. `ProductsPage`: Paste/Open skip URL coerce; **Skip**; Esc asks `TryCancelUrlEdit` first.
2. `ProductAddEditor.BeginWithHtmlAsync`: status callback; busy always cleared; chooser after load.
3. `ProductImageService.LoadFromHtmlAsync` / `DownloadImagesAsync`: 12s per image, log, continue on failure; `TryGetCachedImagesAsync`.
4. `ProductImagePicker.ChooseFromCandidatesAsync`: double-click to accept.
5. `ProductEditor` top icon = choose from cache / file; live fetch remains Url Go.
6. Do not: require the URL box before paste; WebView2 in a ContentDialog; paste on details; `git add docs/features/to-review.md` on `Planning`.
