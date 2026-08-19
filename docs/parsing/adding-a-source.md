# Adding a supplier source

Do this when the generic Open Graph path is wrong for a real product URL.

1. **Host detector** in `ProductPageMetadataParser` (and Swift mirror). Match on `Uri.Host`, not on a vendor enum.  
2. **Parser** that fills `ProductPageMetadata`. Prefer DOM + JSON-LD already on the page.  
3. **Fixture** under `WorkCosts.Tests/Fixtures/` — a trimmed HTML snippet checked in (no secrets, no megabyte homepages).  
4. **xUnit cases** asserting the client fields (see Amazon/Autodoc tests).  
5. **Fetch path**: if HttpClient gets 403/empty, route that host through the embedded browser like Autodoc.  
6. **URL normalize** if the site has a canonical product id (Amazon ASIN pattern).  
7. **Source string** from the host (e.g. `"Amazon"`), vendor from the seller node.  
8. Swift: same detector + parser + the same fixture assertions.

Do not scrape behind a user’s password. Do not commit live cookies. Cache HTML/images by **page domain**.
