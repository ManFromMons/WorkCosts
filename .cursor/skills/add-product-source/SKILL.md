---
name: add-product-source
description: Add a supplier website as a product source. Discover HttpClient vs browser fetch, check in HTML fixtures and xUnit tests, then wire host detection, parser, and fetch routing. Use when the user wants to integrate a new shop URL or follow a docs/features/source-*.md story.
---

# Add a product source

Each host is an **independent** story: `docs/features/source-<host>.md` from [template.md](template.md). **Product URL**, **expected Name**, and **expected UnitPrice (GBP)** are required parameters. Do not invent a closed vendor enum.

Do not scrape behind a login. Do not commit cookies or secrets. Do not put WebView2 in a blocking dialog.

Then follow `implement-feature` / `update-to-review` (inbox on `main`, **no PR until scan is `done`**). Branch `feature/source-<host>-<Title>` from `origin/main`.

## Gate

The story **Status** is `ready-for-agent` only when it contains:

- At least one **canonical product URL**
- **Expected Name** and **Expected UnitPrice** (GBP)

HTML fixture: use the file named in the story if present; otherwise fetch during discovery, **trim**, and check it in under `WorkCosts.Tests/Fixtures/`.

If generic parse already returns that Name and price, **do not** add a dedicated parser. Still add host tests, `InferSourceFromUrl` display name if useful, and fetch routing only if HttpClient cannot load the page.

## Discovery (fetch)

1. Try the existing `ProductImageService` / HttpClient path (same Chrome identity headers) for the sample URL.
2. If HTML is missing, 403, or `IsUsablePageHtml` fails (challenge/CAPTCHA), route this host through `ChromiumPageLoader` the same way Autodoc is gated in `ProductImagePicker.FetchPageAsync` (host check, not a vendor enum). Surface a clear error and rely on **Paste HTML** as fallback.
3. Record the chosen fetch path in the story **Implementation notes** and in to-review **Deviations** if you had to add a host to the Chromium list.

## Tests first

1. Add a trimmed fixture `WorkCosts.Tests/Fixtures/<host>-<id>.snippet.html` (no megabyte dumps).
2. Add xUnit like `AmazonPageParserTests`: host detector facts + theory over the sample URL asserting **Name** and **UnitPrice**. Other `ProductPageMetadata` fields: assert when the story listed them; otherwise allow null.
3. Run `dotnet test WorkCosts.slnx --settings .runsettings` — these cases should **fail** until the parser/fetch work lands.

## Integrate

1. **Host detector** on `ProductPageMetadataParser` (`Uri.Host`, same style as `IsAmazonHost` / `IsAutodocHost`).
2. **Parser** only if generic `ParseGeneric` fails the Name/price asserts. Prefer JSON-LD and obvious DOM; GBP `decimal`.
3. **URL normalize** in `ProductUrl.Normalize` only if the site has a stable product id (Amazon `/dp/{ASIN}` pattern).
4. **Source string** in `ProductVendorHelper.InferSourceFromUrl` (human host name, e.g. `"Halfords"`). Vendor = seller on the page.
5. **Fetch allowlist** in `ProductImagePicker` / `ProductImageService` if discovery required Chromium or special image rules.
6. `IsUsablePageHtml` / challenge detection if this host serves interstitials.
7. Re-run tests. Land to-review `scan` via `Update-ToReviewOnMain.ps1`. No PR until **Status** `done` on that inbox heading.

## Do not

- New screens or Add Product destinations.
- Password walls, cookie jars in git, or live PII in fixtures.
- Extra TFMs. Swift notes belong under **Ports** on the story, not a second Windows spec.
- `git add docs/features/to-review.md` on the feature branch.
