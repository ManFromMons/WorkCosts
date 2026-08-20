# Adding a supplier source

Do this when the generic Open Graph path is wrong for a real product URL, or HttpClient cannot fetch the host.

**Stories:** one independent file per host, `docs/features/source-<host>.md`, from `.cursor/skills/add-product-source/template.md`. Required parameters: **product URL**, **expected Name**, **expected UnitPrice (GBP)**. Other fields optional.

**Agent:** invoke `@start-add-source` or follow skill `add-product-source`. Land with `implement-feature` (to-review on `main`, PR only after scan **done**).

1. **Discover fetch:** HttpClient first (`ProductImageService`). If 403/empty/CAPTCHA, Chromium like Autodoc (`ProductImagePicker.FetchPageAsync` host gate). Paste HTML remains the user fallback.  
2. **Fixture** under `WorkCosts.Tests/Fixtures/` — trimmed HTML (no secrets, no megabyte homepages).  
3. **xUnit** asserting at least Name and GBP price (Amazon/Autodoc tests are the shape).  
4. **Host detector** in `ProductPageMetadataParser` (and later Swift). Match `Uri.Host`, not a vendor enum. Dedicated parser **only if** generic parse fails those asserts.  
5. **URL normalize** if the site has a canonical product id (Amazon ASIN pattern).  
6. **Source string** from the host (`ProductVendorHelper.InferSourceFromUrl`); vendor from the seller node.  
7. Swift later: same detector + parser + the same fixture assertions.

Do not scrape behind a user’s password. Do not commit live cookies. Cache HTML/images by **page domain**.
