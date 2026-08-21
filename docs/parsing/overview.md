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

There is no closed vendor list. **Source** comes from the URL host (`ProductVendorHelper.InferSourceFromUrl`: Amazon, Autodoc, Euro Car Parts, Car Battery Market, otherwise leave/generic host). **Vendor** is the seller on the page. UI breadcrumb: `Source › Vendor`.

## Hosts with dedicated parsers

- **Amazon** (`amazon.*`, `amzn.*`): title, brand, part number, price, seller, EAN/GTIN, variation, OEM field. Normalize URL to `/dp/{ASIN}`.  
- **Autodoc** (`autodoc.*`): JSON-LD + listing markup; strip title suffix; seller URL may map to Autodoc.  
- **Euro Car Parts** (`eurocarparts.*`): product H1 (not the short `og:title`), `pdpPrice` (ignore quantity and frequently-bought add-ons), brand image, ExtraYaml battery specs from the spec list / name. HttpClient fetch.  
- **Car Battery Market** (`carbatterymarket.*`): product H1 (not the shorter document title), `.product--price.price--default` (ignore RRP, PayPal, warranty add-on, Special buy), brand / MPN, ExtraYaml battery specs from the properties table and Technical Specifications list. HttpClient fetch.  
- **Generic**: `og:title`, `h1`, `product:brand` / `og:brand`, generic price meta.

When generic is good enough, do not add a parser. When a host is wrong in production, add a dedicated path (see [adding-a-source.md](adding-a-source.md)). Each host is its own story `docs/features/source-<host>.md` with **≥3 user-confirmed** sample pages; agent skill `add-product-source`.

## Paste HTML

Users can paste page HTML if live fetch fails (CAPTCHA, login, offline). See [paste-html.md](paste-html.md). Shipped on Windows first; other shells later. URL comes from the HTML, not from the URL box.

## Client fields

`Name`, `Manufacturer`, `ManufacturerReference`, `UnitPrice`, `Vendor`, `Ean`, `Variation`, `OemEquivalent`, `Source`, `Capacity`, `LengthMm`, `WidthMm`, `HeightMm`, `Cca`, `Technology`. Null extra fields do not overwrite editor controls. Tests: `ProductPageClientContractTests`.
