# GNOME build order

Ordered slices for rebuilding **Will I DIY?** as a Gir.Core Flatpak. Windows WinUI in `WorkCosts/` is the behaviour reference. This file is **not** on the Seq board; do not add `docs/features/gnome-*.md` stories.

**Start:** `/start-port gnome` (skill `start-port`). One slice per pass. Pickup reads **`origin/main`**.

**Caught up** means every slice whose **Requires-windows** is `none` (or a story that is **Status** `done` on `origin/main`) is itself **Status** `done`. Remaining slices wait on those Windows stories. Zip export/import is not in this list until that Windows feature is `done`.

Script: `scripts/Get-NextPortSlice.ps1`.

## How to read a slice

- **Status** `ready-for-agent` | `done` only. Work states live in `docs/features/to-review.md` on `main`.
- **Depends-on** — earlier **slice ids** that must be `done`.
- **Requires-windows** — `none`, or a `docs/features/<kebab>.md` that must be **Status** `done` on `origin/main`.
- **Done when** — observable in the tree after the squash merge. Pickup uses **Status**, not a guess from files.
- Screen files under `docs/screens/` are the UX source of truth. Layout: `docs/layout-grammar.md`. Chrome: `docs/platforms/gnome-flatpak.md`.

Do not build `WorkCosts.slnx` on Linux. Portable tests: `dotnet test WorkCosts.Tests/WorkCosts.Tests.csproj --settings .runsettings`.

---

## Slice 1 — Scaffold

- **Id:** `gnome-scaffold`
- **Status:** ready-for-agent
- **Depends-on:** none
- **Requires-windows:** none
- **Branch-title:** GNOME-scaffold
- **Related screens:** `docs/screens/shell.md`
- **Related code:** `WorkCosts.Core` (`DatabaseService`, `DbInitializer`, `WorkCostsDbContext`), `docs/data/connection.md`, `docs/data/schema.md`

### Done when

- `src/linux/WillIDIY.Gnome/WillIDIY.Gnome.csproj` is `net9.0`, references Core + Parsing, and is **not** in `WorkCosts.slnx`.
- `src/linux/WillIDIY.Gnome.slnx` includes Core, Parsing, Tests, and the GNOME app.
- A Flatpak manifest exists (app id `app.willidiy.WillIDIY` unless already changed) — may be incomplete (no WebKit finish yet).
- First launch: `DatabaseService.InitializeAsync`, seed via `DbInitializer`, SQLite at `$XDG_DATA_HOME/WorkCosts/workcosts.db` (Flatpak: under the sandbox data dir).
- A window titled **Will I DIY?** opens. Seeded jobs/categories exist in the database (UI can still be empty).
- Portable tests pass on Linux.

### Implementation notes

1. New Gir.Core GTK4 + libadwaita project under `src/linux/`. Pin latest stable Gir.Core packages at implement time; record versions in the csproj.
2. Copy `WorkCosts/Assets/GarageBackground.png` into the GNOME project (required later; include it now so the asset is not forgotten).
3. Wire `DatabaseService` like Windows `App`: migrate off the UI thread, short-lived contexts.
4. Do not add Mac/iOS TFMs or retarget `WorkCosts/WorkCosts.csproj`.
5. Do not: navigation destinations beyond a stub, Add Product, parsers in the UI, zip export.

### Tests

- `dotnet test WorkCosts.Tests/WorkCosts.Tests.csproj --settings .runsettings`
- `dotnet build src/linux/WillIDIY.Gnome.slnx`

---

## Slice 2 — Shell

- **Id:** `gnome-shell`
- **Status:** ready-for-agent
- **Depends-on:** `gnome-scaffold`
- **Requires-windows:** none
- **Branch-title:** GNOME-shell
- **Related screens:** `docs/screens/shell.md`, `docs/screens/dialogs.md`, `docs/screens/work.md`

### Done when

- `AdwApplicationWindow` + wide sidebar / narrow stack (`AdwBreakpointBin` / `AdwNavigationView` as in `gnome-flatpak.md`).
- Destinations: Home, Work (placeholder), Products, Jobs, Categories, Settings. Selecting a leaf clears the back stack. Home after launch.
- Garage background + high-opacity scrim. Theme: Auto / Light / Dark (GNOME + in-app). Title **Will I DIY?**.
- Yes/No and message dialogs exist (Adwaita; primary = Yes, Esc = No). WebKit is not in those dialogs (engine may be absent until Add Product).

### Implementation notes

1. Follow `docs/screens/shell.md` destinations. Compact: collapsible sidebar or view switcher, not a WinUI NavigationView copy.
2. Settings is a **page** in this navigation, even if its body is still a stub.
3. Work stays a placeholder until later slices. Do not add a tab bar (that is iPad).
4. Do not: product/job editors, live fetch, paste HTML.

### Tests

- Portable tests as slice 1. Manual: wide vs narrow, theme switch, garage visible behind scrim.

---

## Slice 3 — Settings

- **Id:** `gnome-settings`
- **Status:** ready-for-agent
- **Depends-on:** `gnome-shell`
- **Requires-windows:** none
- **Branch-title:** GNOME-settings
- **Related screens:** `docs/screens/settings.md`

### Done when

- Settings page: Theme Auto/Light/Dark; database path as selectable text; page-cache explanation + path (stats/clear can be empty until cache exists).
- Not a GNOME Preferences window. No zip export/import actions.

### Implementation notes

1. Implement `docs/screens/settings.md` cards. Narrow readable column.
2. Theme control must drive the same in-app override as the shell switch.
3. Do not: export/import, live scrape, new destinations.

### Tests

- Portable tests. Manual: theme persists; path matches the real SQLite file.

---

## Slice 4 — Jobs

- **Id:** `gnome-jobs`
- **Status:** ready-for-agent
- **Depends-on:** `gnome-shell`
- **Requires-windows:** none
- **Branch-title:** GNOME-jobs
- **Related screens:** `docs/screens/jobs.md`, `docs/screens/markdown-editor.md`

### Done when

- Jobs master/detail: seeded templates listed; add; name, garage GBP, duration, markdown notes with Write/Preview + toolbar.
- Regular: list beside detail. Compact: stack. Primary Add trailing.
- Delete respects WorkJobs Restrict FK (match Windows).

### Implementation notes

1. Seeded jobs are editable. Notes max length 8000.
2. Markdown: same commands as `docs/screens/markdown-editor.md` (widgets may differ).
3. Do not: work-job instances (Home), product assignment UI (Products).

### Tests

- Portable tests. Manual: add/edit/delete template; Write/Preview; compact stack.

---

## Slice 5 — Products

- **Id:** `gnome-products`
- **Status:** ready-for-agent
- **Depends-on:** `gnome-jobs`
- **Requires-windows:** none
- **Branch-title:** GNOME-products
- **Related screens:** `docs/screens/products.md`

### Done when

- Catalogue list + detail editor (image, fields, category, jobs, equivalents, extra specs). Job/category filter strip. **G** badge for all-jobs.
- Add in the header may exist but must **not** open a fetch sheet yet (disable or no-op with a clear comment). Delete Yes/No.
- Photos as **files**, not new BLOBs.

### Implementation notes

1. Match `docs/screens/products.md` regions and compact stack. Extra specs always visible (empty when none) — `product-extra-data` is done on Windows.
2. Do not implement Add Product sheet, WebKit, or paste HTML in this slice.
3. Reuse Core commands (`ProductCommands`, etc.) rather than a second catalogue API.

### Tests

- Portable tests. Manual: filter, edit, delete confirm, image from file.

---

## Slice 6 — Categories

- **Id:** `gnome-categories`
- **Status:** ready-for-agent
- **Depends-on:** `gnome-products`
- **Requires-windows:** none
- **Branch-title:** GNOME-categories
- **Related screens:** `docs/screens/categories.md`

### Done when

- Category chips (add/rename), job toggles as union filter, product list + GBP totals, Unassigned when that group exists.

### Implementation notes

1. Multiple jobs on = union. None on = no extra job filter. Do not add supplier URLs here.
2. Do not: Add Product sheet.

### Tests

- Portable tests. Manual: chip add/rename; toggle filters.

---

## Slice 7 — Add Product

- **Id:** `gnome-add-product`
- **Status:** ready-for-agent
- **Depends-on:** `gnome-products`
- **Requires-windows:** none
- **Branch-title:** GNOME-add-product
- **Related screens:** `docs/screens/products.md`, `docs/parsing/overview.md`, `docs/parsing/browser-session.md`

### Done when

- Header **Add** opens a **sheet** (not a blocking dialog that hosts the browser). URL stage then details in the sheet.
- Live fetch via WebKitGTK (`IBrowserPageSession` equivalent). Prefer cache. Status text while loading. Image chooser when multiple images.
- Existing URL: in-sheet Overwrite / Keep / Cancel banner. HttpClient first where it works; WebKit when blocked (same idea as Windows Chromium).
- Dedicated parsers already in Parsing (Amazon, Autodoc, Euro Car Parts, Tayna, Car Battery Market, Online Car Parts) apply via host detection — do not reimplement them.

### Implementation notes

1. WebKit is created on the UI thread, off-screen or in the sheet content — never inside `AdwAlertDialog`.
2. Source/vendor from URL host + page seller. GBP. Product image required before Add on the live path (match Windows).
3. Skip / Paste HTML controls can be hidden or disabled until the next slice; live **Add** must work.
4. Do not: paste-HTML behaviour in this slice if it would delay live fetch.

### Tests

- Portable parser tests (fixtures). No live network in CI. Manual: one HttpClient-friendly URL and one WebKit-needed URL if available.

---

## Slice 8 — Paste HTML

- **Id:** `gnome-paste-html`
- **Status:** ready-for-agent
- **Depends-on:** `gnome-add-product`
- **Requires-windows:** `paste-html`
- **Branch-title:** GNOME-paste-HTML
- **Related screens:** `docs/screens/products.md`, `docs/features/paste-html.md`, `docs/parsing/paste-html.md`

### Done when

- URL stage: **Paste HTML**, **Open HTML file**, **Skip**, matching `docs/features/paste-html.md` (ignore URL box on paste; URL from HTML; image rules; Esc URL-edit vs close).

### Implementation notes

1. The feature file is the contract. Ports must keep that behaviour; widgets may differ.
2. Do not start WebKit on the paste path. Do not put paste on the details stage.

### Tests

- Cases named in `docs/features/paste-html.md` that are not WinUI-only. Portable test project.

---

## Slice 9 — Home

- **Id:** `gnome-home`
- **Status:** ready-for-agent
- **Depends-on:** `gnome-jobs`
- **Requires-windows:** none
- **Branch-title:** GNOME-home
- **Related screens:** `docs/screens/home.md`

### Done when

- Work job cards (title, template, meta, DIY vs garage GBP). Add work job (pick template + title). Empty state. Compact one column.
- Decorative photo strip if it does not hurt performance.

### Implementation notes

1. Newest first. SQLite DateTimeOffset: use UTC as on Windows.
2. Click card → must not 404; if work-job detail is not built yet, push a stub that at least shows the title (slice 10 fills it). Prefer implementing enough navigation that slice 10 can drop in.
3. Do not: line items (next slice), catalogue Add Product from Home.

### Tests

- Portable tests. Manual: add work job; savings figure uses GBP.

---

## Slice 10 — Work job detail

- **Id:** `gnome-work-job`
- **Status:** ready-for-agent
- **Depends-on:** `gnome-home`, `gnome-products`
- **Requires-windows:** none
- **Branch-title:** GNOME-work-job
- **Related screens:** `docs/screens/work-job-detail.md`

### Done when

- Line items with snapshot unit cost and qty. Add product from catalogue (template jobs + all-jobs, category filter). Totals and saving vs garage. Read-only notes preview from the template.
- Back returns to Home. Compact stack.

### Implementation notes

1. Unique product per work job. Do not open the global Add Product sheet to create catalogue items from here.
2. Notes editing stays on Jobs.

### Tests

- Portable tests. Manual: add line, change qty, savings update.

---

## Slice 11 — Unsaved changes

- **Id:** `gnome-unsaved-changes`
- **Status:** ready-for-agent
- **Depends-on:** `gnome-add-product`, `gnome-jobs`, `gnome-products`
- **Requires-windows:** `unsaved-changes-prompt`
- **Branch-title:** GNOME-unsaved-changes
- **Related screens:** `docs/screens/dialogs.md`, `docs/features/unsaved-changes-prompt.md`

### Done when

- Timed Save / Don't Save / Cancel on dirty leave, matching the Windows story once that story is **Status** `done`. Adwaita dialog; no WebKit inside it.

### Implementation notes

1. Do not start this slice until pickup says it is startable (`Requires-windows` met).
2. Timeouts and which surfaces are dirty are in the feature file — do not invent a second prompt model.

### Tests

- Cases from that feature file that are not WinUI-only.
