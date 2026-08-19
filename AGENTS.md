# AGENTS.md

You are working on **Will I DIY?** (repo folder names still use WorkCosts). It is an offline-first DIY cost tracker: job templates, a product catalogue imported from supplier pages, work jobs with line items, and savings versus garage prices. Currency is **GBP**.

## Read this first

1. [PLANNING.md](PLANNING.md) — locked product decisions  
2. [docs/architecture.md](docs/architecture.md) — process map and projects  
3. [docs/layout-grammar.md](docs/layout-grammar.md) — how screens are put together  
4. The screen file for the surface you are changing under [docs/screens/](docs/screens/)  
5. [docs/data/schema.md](docs/data/schema.md) and [docs/data/connection.md](docs/data/connection.md) for persistence  
6. [docs/parsing/overview.md](docs/parsing/overview.md) if you touch import or HTML  

Do not invent a second product. Windows WinUI in `WorkCosts/` is the behaviour reference. GNOME and iPad may use native widgets and spacing, but they keep the same information architecture, workflows, and data.

## Planning a feature

Use the project skill `.cursor/skills/plan-feature/`. The source of truth is `docs/features/<name>.md` (template in that skill). Do not implement until that file is ready for an unsupervised agent.

## Solutions and projects

| Project | Role |
| :--- | :--- |
| `WorkCosts` | WinUI 3 unpackaged desktop app (`net9.0-windows10.0.26100.0`) |
| `WorkCosts.Core` | Models, EF Core SQLite, seed, commands, cache index (`net9.0`) |
| `WorkCosts.Parsing` | AngleSharp HTML → `ProductPageMetadata` (`net9.0`) |
| `WorkCosts.Tests` | xUnit; HTML fixtures are the parser contract |
| `WorkCosts.Package` | MSIX packaging (Windows) |

GNOME and iPad are **new apps in this repo** (planned: `src/linux`, `src/ios`). They are not extra TFMs on the WinUI csproj.

- **GNOME:** new Gir.Core (GTK4/libadwaita) project that references Core + Parsing. Ship as Flatpak.  
- **iPad / Mac Catalyst:** SwiftUI. Swift opens the same SQLite schema (follow EF migrations). Port parsers using `WorkCosts.Tests` fixtures. Mac agents may run `dotnet test WorkCosts.slnx`.

## Non-negotiables

- Local data only. No accounts, no cloud sync. Backup is a **zip** (XML catalogue + images + page cache); import **merges**. Spec now; implementation later. See [docs/data/export-import.md](docs/data/export-import.md).  
- Catalogue in **SQLite**. Product photos and cached pages/images as **files**.  
- Seeded categories and jobs on first launch (stable GUIDs in `DbInitializer`).  
- Live page fetch must work (WebView2 / WebKit / WKWebView where HttpClient is blocked). **Paste HTML** is a planned product feature.  
- Source/vendor is derived from the **URL host** and page seller, not a closed vendor enum.  
- Garage background is required. Theme: Auto / Light / Dark.  
- Confirmations: platform primary = Yes, Esc / dismiss = No. Never host Chromium/WebKit inside a modal that freezes the UI.

## Commands (Windows / Mac with .NET 9)

```powershell
dotnet build WorkCosts.slnx
dotnet test WorkCosts.slnx --settings .runsettings
dotnet run --project WorkCosts/WorkCosts.csproj
```

Do not commit secrets, `.pfx`, or local `workcosts.db`. Do not invent git remotes. Close `WillIDIY.exe` before a full rebuild if copy/lock fails.
