# Architecture

Will I DIY? is three layers. The WinUI app is one shell. GNOME and iPad are other shells over the same catalogue and (on C#) the same libraries.

```
┌─────────────────────────────────────────────────────────┐
│  Shell (navigation, theme, garage background, dialogs)  │
│  WinUI  │  Gir.Core + libadwaita  │  SwiftUI            │
├─────────────────────────────────────────────────────────┤
│  Fetch session (WebView2 / WebKitGTK / WKWebView)       │
│  Paste HTML (Windows shipped; other shells later)       │
├─────────────────────────────────────────────────────────┤
│  WorkCosts.Parsing   AngleSharp → ProductPageMetadata   │
│  (Swift ports this using tests as the contract)         │
├─────────────────────────────────────────────────────────┤
│  WorkCosts.Core      models, EF, seed, commands, cache  │
│  SQLite file + images/ + cache/                         │
└─────────────────────────────────────────────────────────┘
```

## Domain in one paragraph

A **Job** is a template (name, garage price, duration, markdown notes). A **Work Job** is an instance of a job you are actually doing (title, created date, line items). A **Product** is a catalogue row (URL, identity fields, unit cost, photo, category, optional “all jobs” flag, links to jobs and equivalent products). **Work Job Items** snapshot unit cost and quantity. **Categories** group products. Savings on Home = garage price of the template minus the sum of line items.

## C# reuse

| Who | How |
| :--- | :--- |
| Windows | `WorkCosts` project-references Core and (transitively) Parsing |
| GNOME | New `net9.0` Gir.Core app project-references the same two libraries |
| Mac agent | `dotnet test` / `dotnet build` on Core and Parsing; no WinUI |
| iPad | Swift UI + Swift SQLite. Port Parsing; apply EF migrations as SQL. No .NET runtime in the app |

Do not multi-target the WinUI csproj. Gir.Core is Linux GTK, not AppKit.

## Process map (Windows today)

1. `App` constructs `DatabaseService`, migrates, seeds, then navigates Home.  
2. Pages open short-lived `DbContext` instances (`CreateContext()`), not a long-lived context on the UI thread.  
3. Add Product: user enters URL → overlay/sheet opens immediately → `ProductImagePicker.FetchPageAsync` loads Chromium **outside** any dialog → parser fills fields → first image auto-used when possible → save.  
4. Autodoc (and similar) use `ChromiumPageLoader` because HttpClient is blocked. Amazon often works from HTML; still prefer a consistent fetch path.  
5. Cache: HTML and chooser images under a per-domain folder; SQLite indexes them.

## Planned shells

See [platforms/windows.md](platforms/windows.md), [platforms/gnome-flatpak.md](platforms/gnome-flatpak.md), [platforms/ipados-swiftui.md](platforms/ipados-swiftui.md).
