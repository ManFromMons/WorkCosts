# Example (shape only)

A ready feature file looks like this — not a substitute for `docs/features/paste-html.md` until that feature is planned.

```markdown
# Feature: Paste HTML on Add Product

- **Id:** `docs/features/paste-html.md`
- **Status:** ready-for-agent
- **Related screens:** `docs/screens/products.md`
- **Related code:** `ProductImagePicker`, `ProductPageMetadataParser`, `ProductsPage` add overlay

## Objectives
- User can supply page HTML when live fetch fails, still bound to a URL for host detection.

## User requirements
- Secondary action on the Add Product URL stage: Paste HTML.
- Parser runs on the string; live scrape remains available.

## Layout
- Same Add Product **sheet** (`AddOverlay` on `ProductsPage`). Extra button next to Add, not a nested ContentDialog hosting WebView2.

## Workflow
1. User enters URL (coerce via `ProductUrl.TryCoerceHttpUrl`).
2. Paste HTML opens a multiline field in the same sheet.
3. Parse with `ProductPageMetadataParser.ParseHtmlAsync(html, url)`.
4. Existing URL collision banner unchanged.

## Technical design
- Reuse: `IProductPageParser` / `ProductPageMetadataParser`, `WebCacheStore` to save pasted HTML, `ProductUrl`.
- Create: none, or a small helper method on `ProductsPage` — do not clone `ChromiumPageLoader`.
- Wiring: keep current page methods; no new DI container.

## Tests
- `PasteHtmlParserTests` reusing Autodoc/Amazon snippets as strings.

## Open questions
(none)

## Implementation notes for an agent
1. Extend URL stage in `ProductsPage.xaml` only.
2. Do not put WebView2 in a ContentDialog.
```
