# GNOME (Flatpak)

New C# app using **Gir.Core** (GTK4 + libadwaita) that **project-references** `WorkCosts.Core` and `WorkCosts.Parsing`.

**Build order:** [gnome-build-order.md](gnome-build-order.md). Kickoff: `/start-port gnome` (one slice per pass). Do not add this project to `WorkCosts.slnx`.

## Chrome

- `AdwApplicationWindow` + `AdwNavigationView` / `AdwBreakpointBin`.  
- Wide: sidebar (Home, Work Jobs, Products, Jobs, Categories, Settings).  
- Narrow: **stack**; compact iPad uses a tab bar — GNOME should use a bottom bar or `AdwViewStack` plus a sidebar that collapses, not a copy of WinUI NavigationView pixels.  
- Settings is a **page** in the same navigation, not Preferences window.  
- Garage background + scrim: required. Theme: follow GNOME + in-app Auto/Light/Dark.  
- Add Product: `AdwDialog` or overlay **sheet**, WebKit **not** parented inside a modal that cannot show.

## Data

SQLite + files under the Flatpak data dir. Migrate with EF from Core. Seed on first run.

## Package

Flatpak first (`app.willidiy.WillIDIY` unless changed). Finish: network, optionally Downloads for HTML paste. WebKitGTK + .NET 9 runtime inside the sandbox (self-contained publish is simpler than depending on org.freedesktop.Sdk-only).

## Layout

OS conventions (Adwaita spacing, header bars, boxed lists). Structural grammar: [layout-grammar.md](../layout-grammar.md).
