# WorkCosts (Will I DIY?)

**WorkCosts** is a modern Windows desktop application built with **.NET 9** and **WinUI 3 (Windows App SDK)** to track DIY projects, jobs, parts, vendor pricing, and cost comparisons.

---

## Features

- **Jobs & Work Management**: Plan and track DIY jobs, log hours spent, and calculate total project costs.
- **Parts & Product Catalog**: Organize parts by category, track OEM and alternative part numbers, record manufacturer variations, and maintain vendor price points (Amazon, Autodoc, etc.).
- **Web Parser & Metadata Scraping**: Import product details, pricing, and images directly from supported supplier pages (Amazon, Autodoc).
- **Modern Fluent UI**: WinUI 3 with light/dark theme support and Mica material styling.
- **Offline First**: Local SQLite storage backed by Entity Framework Core with automatic migration handling.

---

## Solution Structure

The solution (`WorkCosts.slnx`) contains four projects:

| Project | Target | Description |
| :--- | :--- | :--- |
| **`WorkCosts`** | `net9.0-windows10.0.26100.0` | WinUI 3 desktop application (UI, pages, controls, and styles). |
| **`WorkCosts.Core`** | `net9.0` | Core domain models, EF Core SQLite `DbContext`, migrations, and cache services. |
| **`WorkCosts.Parsing`** | `net9.0` | AngleSharp-based HTML parsing for automotive and online retail product pages. |
| **`WorkCosts.Tests`** | `net9.0` | xUnit test suite for parsers, metadata extraction, and database initialization. |

---

## Prerequisites

- **OS**: Windows 10 (version 1809 / build 17763 or newer) or Windows 11
- **SDK**: [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- **IDE** (optional): Visual Studio 2022 / 2026 (with *.NET Desktop Development* workload) or JetBrains Rider with .NET 9 support

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

## License

This project is open-source. See repository license for details.
