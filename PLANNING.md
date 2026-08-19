# Planning: agent specs for Windows, GNOME, and iPad

This branch holds **AGENTS.md**, **`.cursor/rules/`**, and **`docs/`** so another agent can rebuild **Will I DIY?** as:

- a **GNOME Flatpak** (GTK4 + libadwaita, C# via Gir.Core, referencing `WorkCosts.Core` + `WorkCosts.Parsing`)
- an **iPad / Apple silicon Mac** SwiftUI app (shipping binary is Swift; Mac agents may run `dotnet test`)

Windows WinUI remains the running reference in this repo.

---

## Locked decisions

| Topic | Decision |
| :--- | :--- |
| Display name | **Will I DIY?** (repo/projects may still say WorkCosts) |
| Currency | **GBP** only |
| Seed data | Same seeded categories and job templates on first launch of every platform |
| Feature parity | Nothing Windows-only. Garage background, page cache UI, and theme switch ship everywhere |
| C# reuse | `WorkCosts.Core` + `WorkCosts.Parsing` stay `net9.0`. GNOME is a **new Gir.Core project**. Do **not** add Mac/iOS TFMs to WinUI |
| iPad C# | iPad uses **Swift directly** (SQLite via GRDB or equivalent, schema from EF migrations). Do not embed the .NET runtime in the iPad binary |
| Runtime store | **SQLite** catalogue + **files for blobs** (product images and page cache) |
| Export / import | **Zip only** (spec now, implement later). Zip contains **XML catalogue** + **library images** + **page cache**. Import **merges** |
| Live scrape | Do not skip. Also **paste HTML** (new Windows feature, then GNOME/iPad) |
| Vendors | Come from the **product URL** (host → source; page seller → vendor). Dedicated parsers when a host needs them |
| New sources | Host detector + parser + HTML fixtures + field contract. **Swift mirrors** that |
| Layout numbers | **OS conventions** (libadwaita / Human Interface Guidelines), not WinUI pixel copies |
| Narrow layout | **Stack** (list, then detail) |
| Add product | **Sheet** (browser must not live inside a blocking dialog) |
| Garage background | **Required** |
| Markdown | Full **Write / Preview** toolbar |
| GNOME Settings | A **page**, not a separate window |
| iPad compact | **Tab bar** |
| Shortcuts | Yes, **platform conventions** (Esc dismiss, Enter confirm, etc.) |
| Swift schema | Follow **EF migrations** |
| Linux package | Flatpak; data under the app’s sandbox data dir |
| Repo | This repo; later `src/linux`, `src/ios` |

Open `docs/` from [AGENTS.md](AGENTS.md). Historical questions live only here; the specs are the source of truth going forward.
