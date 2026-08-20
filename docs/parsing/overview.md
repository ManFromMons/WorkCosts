# Parsing

HTML → `ProductPageMetadata` → `ProductPageClientValues` (null means “do not overwrite this field”). The WinUI editor copies non-null values onto the product.

Canonical C#: `WorkCosts.Parsing/ProductPageMetadataParser.cs`. Swift **mirrors** behaviour; `WorkCosts.Tests` fixtures are the contract.

## Fetch vs parse

| Step | Windows | GNOME | iPad |
| :--- | :--- | :--- | :--- |
| Download | HttpClient when it works; **WebView2** for Autodoc (and any host that blocks) | WebKitGTK / webview | **WKWebView** |
| Parse | AngleSharp | AngleSharp (same library) | Swift HTML parser equivalent |
| Cache | Files + SQLite index | Same idea | Same idea |

Never create the browser widget inside a blocking dialog. Windows already loads Chromium in-panel/off-screen (`ChromiumPageLoader`, `ProductImagePicker.FetchPageAsync`).

## Source and vendor

There is no closed vendor list. **Source** comes from the URL host (`ProductVendorHelper.InferSourceFromUrl`: Amazon, Autodoc, otherwise leave/generic host). **Vendor** is the seller on the page. UI breadcrumb: `Source › Vendor`.

## Hosts with dedicated parsers

- **Amazon** (`amazon.*`, `amzn.*`): title, brand, part number, price, seller, EAN/GTIN, variation, OEM field. Normalize URL to `/dp/{ASIN}`.  
- **Autodoc** (`autodoc.*`): JSON-LD + listing markup; strip title suffix; seller URL may map to Autodoc.  
- **Generic**: `og:title`, `h1`, `product:brand` / `og:brand`, generic price meta.

When generic is good enough, do not add a parser. When a host is wrong in production, add a dedicated path (see [adding-a-source.md](adding-a-source.md)). Each host is its own story `docs/features/source-<host>.md`; agent skill `add-product-source`.

## Planned: paste HTML

Users must be able to paste page HTML if live fetch fails (CAPTCHA, login, offline). See [paste-html.md](paste-html.md). Implement on Windows first, then other shells.

## Client fields

`Name`, `Manufacturer`, `ManufacturerReference`, `UnitPrice`, `Vendor`, `Ean`, `Variation`, `OemEquivalent`, `Source`. Tests: `ProductPageClientContractTests`.
