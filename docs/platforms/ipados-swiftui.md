# iPadOS and Mac Catalyst (SwiftUI)

Shipping UI is **SwiftUI**. No .NET runtime in the iPad binary. Mac agents may still run `dotnet test` to check parsers and migrations.

## Chrome

- Regular width: `NavigationSplitView` (sidebar + detail).  
- Compact: **Tab bar** — Home (work jobs), Products, Jobs, Categories, Settings. Work Job detail pushes on the Home stack.  
- Add Product: **sheet**. WKWebView for live scrape lives in the sheet’s content or an offscreen helper, not in a `UIAlertController`.  
- Garage background + material scrim: required. Color scheme: system + in-app override (Auto/Light/Dark).  
- Markdown: full Write/Preview toolbar (SwiftUI `TextEditor` + a preview `WebView` or native markdown).  
- Shortcuts: iPadOS/macOS conventions (Esc dismiss sheet, Return confirm, pointer hover where available).

## Data

SQLite in Application Support, schema from EF migrations (generate SQL from the migrations folder or a documented dump). Files for photos and cache. Seed the same GUIDs/names as `DbInitializer`.

## Parsing

Port `ProductPageMetadataParser` host detectors and field rules. Run the same HTML fixtures. Source/vendor from URL. WKWebView must not be skipped. Paste HTML after Windows ships it.

## Interchange

Zip merge import/export when that spec is implemented. Until then, local only.

## Mac

Same SwiftUI app via Mac Catalyst or “iPad app on Mac” is in scope. Pointer and keyboard matter. Live scrape is easier on Mac than on a locked-down iPad network, but behaviour should match.
