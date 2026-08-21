# WorkCosts (Will I DIY?)

**WorkCosts** is a modern Windows desktop application built with **.NET 9** and **WinUI 3 (Windows App SDK)** to track DIY projects, jobs, parts, vendor pricing, and cost comparisons.

---

## Features

- **Jobs & Work Management**: Plan and track DIY jobs, log hours spent, and calculate total project costs.
- **Parts & Product Catalog**: Organize parts by category, track OEM and alternative part numbers, record manufacturer variations, and maintain vendor price points (Amazon, Autodoc, etc.).
- **Web Parser & Metadata Scraping**: Import product details, pricing, and images from supplier pages (Amazon, Autodoc today; more hosts via the add-source skill below).
- **Modern Fluent UI**: WinUI 3 with light/dark theme support and Mica material styling.
- **Offline First**: Local SQLite storage backed by Entity Framework Core with automatic migration handling.

---

## Solution Structure

The solution (`WorkCosts.slnx`) contains these projects, plus an Inno Setup folder for the shareable installer:

| Project | Target | Description |
| :--- | :--- | :--- |
| **`WorkCosts`** | `net9.0-windows10.0.26100.0` | WinUI 3 desktop application (UI, pages, controls, and styles). Unpackaged for daily F5. |
| **`WorkCosts.Package`** | MSIX (WAP) | Optional sideload `.msix` (requires a trusted publisher certificate). |
| **`WorkCosts.Installer`** | Inno Setup | Recommended shareable `Setup.exe` for GitHub Releases (no sideloading). |
| **`WorkCosts.Core`** | `net9.0` | Core domain models, EF Core SQLite `DbContext`, migrations, and cache services. |
| **`WorkCosts.Parsing`** | `net9.0` | AngleSharp-based HTML parsing for automotive and online retail product pages. |
| **`WorkCosts.Tests`** | `net9.0` | xUnit test suite for parsers, metadata extraction, and database initialization. |

---

## Prerequisites

- **OS**: Windows 10 (version 1809 / build 17763 or newer) or Windows 11
- **SDK**: [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- **IDE** (optional): Visual Studio 2022 / 2026 (with *.NET Desktop Development* and *Windows application development* workloads) or JetBrains Rider with .NET 9 support
- **Inno Setup** 6.3+ (for the shareable installer): [jrsoftware.org/isinfo.php](https://jrsoftware.org/isinfo.php)
- **Windows SDK** (optional, for CLI MSIX): [Windows 10/11 SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/) (`MakeAppx` / `SignTool`)

---

## Getting Started

### Build the Solution

```powershell
dotnet build WorkCosts.slnx
```

### Run Tests

```powershell
dotnet test WorkCosts.slnx
```

### Run the Application

From Visual Studio / Rider, set `WorkCosts` as the startup project and run (F5), or launch via CLI:

```powershell
dotnet run --project WorkCosts/WorkCosts.csproj
```

---

## Packaging a shareable installer (Inno Setup)

Daily development stays **unpackaged** (`WindowsPackageType=None`). The Inno installer wraps that same self-contained publish: recipients run `Setup.exe` with no sideloading and no publisher certificate.

Jobs and parts stay in `%LOCALAPPDATA%\WorkCosts` (same database as F5). Uninstall does not delete that folder.

Requires [Inno Setup](https://jrsoftware.org/isinfo.php) 6.3 or later (`ISCC.exe`). If it is not on `PATH`, pass `-IsccPath`.

```powershell
powershell -ExecutionPolicy Bypass -File WorkCosts.Installer\Pack-Inno.ps1
powershell -ExecutionPolicy Bypass -File WorkCosts.Installer\Pack-Inno.ps1 -Runtime x64 -Version 1.0.0
```

Output: `WorkCosts.Installer\Output\WillIDIY-Setup-1.0.0-x64.exe`. Attach that file to a GitHub Release.

You can also open `WorkCosts.Installer\WillIDIY.iss` in the Inno IDE after a publish folder exists. Rider’s auto-created **Publish to IIS** run configurations are folder publishes only; they do not build this installer or an MSIX.

---

## Packaging a sideload MSIX (optional)

Use **`WorkCosts.Package`** when you specifically want an `.msix`. Recipients must enable sideloading and trust the publisher certificate.

Bump `Identity Version` in `WorkCosts.Package/Package.appxmanifest` (and pass the same value to the script) for each release. The publisher is `CN=WillIDIY`; the signing certificate subject must match.

MSIX installs get a **separate** local SQLite database from F5 and from the Inno installer (Windows redirects packaged AppData).

### Visual Studio

1. Install Visual Studio with **.NET desktop development** and **Windows application development** (Desktop Bridge / Windows Application Packaging).
2. Open `WorkCosts.slnx`, set **WorkCosts.Package** as the startup project.
3. **Publish → Create App Packages…** and choose sideloading. Output lands under `WorkCosts.Package/AppPackages/`.

### Command line (no Visual Studio)

Requires the [Windows SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/) (`MakeAppx` / `SignTool`) and PowerShell. The first run creates a **Current User** self-signed certificate `CN=WillIDIY` (not committed).

```powershell
powershell -ExecutionPolicy Bypass -File WorkCosts.Package\Pack-Msix.ps1
powershell -ExecutionPolicy Bypass -File WorkCosts.Package\Pack-Msix.ps1 -Runtime x64 -Version 1.0.0.0
```

Install on this PC:

```powershell
Add-AppxPackage -Path .\WorkCosts.Package\AppPackages\Release\win-x64\WillIDIY_1.0.0.0_x64.msix
```

On another PC: enable **sideloading** (Settings → For developers), import the generated `WillIDIY.cer` into **Local Machine → Trusted People**, then run `Add-AppxPackage` on the `.msix`. Recipients also need the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (already present on most Windows 11 machines).

A folder publish (no installer) is still available via the `WorkCosts/Properties/PublishProfiles/win-*.pubxml` profiles.

---

## License

This project is open-source. See repository license for details.

## Agent specs

Rebuild and layout rules for GNOME and iPad: [AGENTS.md](AGENTS.md). File-by-file map, skills, and Cursor CLI (`agent`): [docs/agent-handbook.md](docs/agent-handbook.md).

### Add a supplier website

Each shop is its own story (`docs/features/source-<host>.md`). **Your** Name and GBP price from the page are the test contract — not an unconfirmed scrape. **At least three** product pages per host.

In Cursor chat or an **interactive** `agent` session (not a one-shot `agent -p`):

```text
/start-add-source https://www.example.com/product/…
```

1. The agent opens or fetches the page and proposes Name and unit price.
2. You confirm or paste what you see.
3. It asks for more product URLs on the **same host** until three pages are confirmed (one page at a time).
4. It writes the story on the **`Planning`** branch and **stops**. No parser work yet.
5. Land specs: skill `merge-planning` / `scripts/Merge-PlanningToMain.ps1`.
6. Implement: `/start-add-source source-<host>` (creates `feature/source-<host>-…` from `main`). Fixtures and tests cover all three pages.

Full protocol: `.cursor/skills/add-product-source/confirm-samples.md`. Playbook: [docs/parsing/adding-a-source.md](docs/parsing/adding-a-source.md).
