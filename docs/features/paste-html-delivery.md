# Delivery: Paste HTML

- **Feature:** [docs/features/paste-html.md](paste-html.md)
- **Seq:** 1
- **Branch:** `feature/paste-html-Paste-HTML`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/1

## What landed

- Add Product URL stage: **Paste HTML** (clipboard), **Open HTML file**, and **Skip**, with live **Add** unchanged.
- Paste/Open ignore the URL box; product URL comes from the HTML (`FindPageUrlAsync`).
- Parse and cache without Chromium; reject unusable/CAPTCHA HTML; require a product image before Add on the paste path.
- Image HTTP is best-effort (12s timeout, logged skips). Several downloaded images open **Select product image** (double-click confirms). Globe/pictures reopen that grid; file picker is the fallback. Product detail top icon is choose-from-cache, not live fetch.
- Esc cancels URL-edit on the add sheet without discarding the form.

## Tests

- Named in the spec (`PasteHtmlParserTests`, `LoadFromHtmlAsync`); coder reported `WorkCosts.Tests` passing on the PR branch.

## Deviations

- `DatabaseService(string databasePath)` for tests — also listed in `docs/features/to-review.md` on `main`.
