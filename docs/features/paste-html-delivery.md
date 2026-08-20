# Delivery: Paste HTML

- **Feature:** [docs/features/paste-html.md](paste-html.md)
- **Seq:** 1
- **Branch:** `feature/paste-html-Paste-HTML`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/1

## What landed

- Add Product URL stage: **Paste HTML** (clipboard) and **Open HTML file**, with live **Add** unchanged.
- Parse and cache without Chromium; reject unusable/CAPTCHA HTML; require a product image before Add (fetch from URL or choose a file).

## Tests

- Named in the spec (`PasteHtmlParserTests`, `LoadFromHtmlAsync`); coder reported `WorkCosts.Tests` passing on the PR branch.

## Deviations

- `DatabaseService(string databasePath)` for tests — also listed in `docs/features/to-review.md` on `main`.
