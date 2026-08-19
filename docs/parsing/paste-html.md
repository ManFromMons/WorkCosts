# Paste HTML (planned)

Source of truth: [docs/features/paste-html.md](../features/paste-html.md). This page is background only.

**Product feature — implement on Windows, then GNOME and iPad.** Live scrape stays; this is the fallback.

## Why

Supplier pages (especially Autodoc) may show CAPTCHA, region walls, or empty HttpClient responses. The user can save the page in a desktop browser and paste HTML (or drop a `.html` file).

## UI

On the Add Product sheet, beside the URL box:

- Primary: URL + Fetch (existing).  
- Secondary: **Paste HTML** (and optional file picker).  
- If URL is present, parsers still receive that URL for host detection and relative links. If URL is empty, ask for a URL first (`ProductUrl.TryCoerceHttpUrl`).

Do not run paste from inside a nested dialog that hosts a WebView.

## Behaviour

1. User supplies URL + HTML string.  
2. `ProductPageMetadataParser.ParseHtmlAsync(html, url)` (no browser).  
3. Images: parse `<img>` / OG image URLs; try HttpClient; if blocked, tell the user to pick a file or retry with live Chromium.  
4. Cache the pasted HTML like a fetched page.  
5. Same existing-URL merge banner as live add (overwrite / keep / cancel).

## Tests

Add fixtures that are pasted snippets (can reuse existing Amazon/Autodoc snippets) and a test that parser-from-string equals parser-from-file.
