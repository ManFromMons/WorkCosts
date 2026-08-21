# Paste HTML

Source of truth: [docs/features/paste-html.md](../features/paste-html.md). This page is background only.

**Product feature — shipped on Windows; GNOME and iPad later.** Live scrape stays; this is the fallback.

## Why

Supplier pages (especially Autodoc) may show CAPTCHA, region walls, or empty HttpClient responses. The user can save the page in a desktop browser and paste HTML (or drop a `.html` file).

## UI

On the Add Product sheet, beside the URL box:

- Primary: URL + **Add** (live fetch).
- Secondaries: **Paste HTML**, **Open HTML file**, **Skip**.
- Paste/Open **ignore** the URL box. The product URL comes from the HTML (`FindPageUrlAsync`: canonical, `og:url`, `base href`, `saved from url=(…)`). If none, stay on the URL stage and offer Skip.

Do not run paste from inside a nested dialog that hosts a WebView.

## Behaviour

1. User supplies HTML (clipboard or file). No URL-box coerce.
2. Discover URL from HTML; `ProductPageMetadataParser.ParseHtmlAsync(html, url)` (no browser).
3. Images: parse `<img>` / OG image URLs; try HttpClient with a short per-image timeout. Failures must not wipe metadata or leave a spinner. If several images download, show the existing **Select product image** grid (double-click confirms). If blocked, pick from downloaded images, a local file, or retry with live Chromium via the globe when this session has no candidates.
4. Cache the pasted HTML like a fetched page.
5. Same existing-URL merge banner as live add (overwrite / keep / cancel).

## Tests

Add fixtures that are pasted snippets (can reuse existing Amazon/Autodoc snippets) and a test that parser-from-string equals parser-from-file. Cover `FindPageUrlAsync`.
