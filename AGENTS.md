# AGENTS.md

You are working on **Will I DIY?** (repo folder names still use WorkCosts). It is an offline-first DIY cost tracker: job templates, a product catalogue imported from supplier pages, work jobs with line items, and savings versus garage prices. Currency is **GBP**.

## Read this first

1. [docs/agent-handbook.md](docs/agent-handbook.md) — map of every spec, skill, and script; **Cursor CLI** (`agent`) invocations  
2. [PLANNING.md](PLANNING.md) — locked product decisions  
3. [docs/architecture.md](docs/architecture.md) — process map and projects  
4. [docs/layout-grammar.md](docs/layout-grammar.md) — how screens are put together  
5. The screen file for the surface you are changing under [docs/screens/](docs/screens/)  
6. [docs/data/schema.md](docs/data/schema.md) and [docs/data/connection.md](docs/data/connection.md) for persistence  
7. [docs/parsing/overview.md](docs/parsing/overview.md) if you touch import or HTML  

Do not invent a second product. Windows WinUI in `WorkCosts/` is the behaviour reference. GNOME and iPad may use native widgets and spacing, but they keep the same information architecture, workflows, and data.

## Planning a feature

Use the project skill `.cursor/skills/plan-feature/`. The source of truth is `docs/features/<name>.md` (template in that skill). Do not implement until that file is ready for an unsupervised agent. Specs are written on **`Planning`**, then landed with `merge-planning` (not a GitHub feature PR).

## Adding a supplier website

One story per host: `docs/features/source-<host>.md` (template `.cursor/skills/add-product-source/template.md`). Not a closed vendor enum. Protocol: `.cursor/skills/add-product-source/confirm-samples.md`. Playbook: [docs/parsing/adding-a-source.md](docs/parsing/adding-a-source.md). Human summary: [README.md](README.md) (Add a supplier website).

`/start-add-source` is **invoke-only**. Type it in chat or interactive CLI.

**Plan** (stay on **`Planning`**, no feature branch):

```text
/start-add-source https://www.example.com/product/…
```

1. Open or fetch the page. **Propose** Name and GBP price (optional fields only if obvious).
2. **Wait.** The user confirms or pastes what they see. That answer is the contract. Do not write Expected Name or UnitPrice from an unconfirmed scrape.
3. Ask for more product URLs on the **same host** until **three** pages are confirmed. Confirm **one page at a time**. Skip login walls, CAPTCHAs they cannot pass, and non-product URLs; ask for a replacement.
4. Write the story with three sample rows. **Status** `ready-for-agent` only then. **Stop.** Do not implement in this pass unless they explicitly say to after the story is ready.
5. Use a conversation (`agent` or editor). Do not invent Name/price in `agent -p`.

**Implement** (story already `ready-for-agent` with three confirmed samples):

```text
/start-add-source source-<host>
```

Branch `feature/source-<host>-<Title>` from `origin/main`. Discover HttpClient vs Chromium, one trimmed fixture **per sample**, failing tests for Name and UnitPrice on **all three**, then parser/fetch as skill `add-product-source`. Inbox on `main` via `update-to-review`. No GitHub PR until that heading is **Status** `done`.

## Implementing a feature

To kick off a new coder chat, invoke skill `start-implement` (named spec, **Seq**, or next in the queue). Use `.cursor/skills/implement-feature/` only when `docs/features/<name>.md` exists and **Status** is `ready-for-agent`. List the board with skill `feature-queue` (`scripts/Get-FeatureQueue.ps1`); start a story with `/feature-queue 5` or `/start-implement 5`. To take the next queued story without a number, use skill `pickup-next-feature` (`scripts/Get-NextReadyFeature.ps1`). Branch from up-to-date `main` using the name below. Commit **buildable code only** on that branch.

The review inbox is **`docs/features/to-review.md` on `main`**. Read it with `git show origin/main:docs/features/to-review.md`. Land every inbox edit with skill `update-to-review` and:

```powershell
git fetch origin
git show origin/main:docs/features/to-review.md > docs/features/to-review.md
# edit only that file, then:
powershell -File scripts/Update-ToReviewOnMain.ps1 -Message "to-review: <kebab> <status>"
```

Do not `git add` that file on `Planning` or a feature branch. Do **not** open a GitHub PR until the inbox for that feature is **Status** `done` (questions and deviations approved). Then set **PR** on the story header and add `docs/features/<name>-delivery.md` on the feature branch.

## Branches and pull requests

Long-lived branches: **`main`** (shippable app) and **`Planning`** (specs and agent docs). Never force-push `main`.

### Feature branches (agent implementation)

Name:

`feature/<feature_code>-<Title>`

- **`<feature_code>`** is the spec id: the kebab name of `docs/features/<feature_code>.md` (`paste-html`, `zip-export-import`).
- **`<Title>`** is a short human title for the feature. Use hyphens instead of spaces (git-safe). Do not use slashes in the title.

Examples: `feature/paste-html-Paste-HTML`, `feature/source-halfords-Halfords`, `feature/zip-export-import-Zip-export-and-import`.

Create the branch from current `origin/main`. Do not implement on `Planning` or commit product WIP on `main`.

Stories are queued by **Seq** (integer, never reused) and **Depends-on** (kebab ids, or `none`). Skill `feature-queue` prints the dependency tree and can start by Seq. Skill `pickup-next-feature` takes the lowest Seq that is `ready-for-agent` whose dependencies are `done`.

### Landing agent work

All **implementation** lands on `main` as a **squash pull request** opened **after** the to-review heading is accepted (**Status** `done`). While the coder is finished, that heading is **Status** `ready-for-review` — not `done`. Open the PR against `main` (GitHub). The human then squash-merges. Agents do **not** squash-merge, rebase-merge, or merge-commit the PR unless the user explicitly asks. Do not open the PR while questions or deviations are still unchecked.

Do not use merge commits onto `main`. After a squash merge, delete the feature branch.

### Landing Planning

Specs and agent-doc changes on `Planning` are not a feature PR. Rebase + squash + fast-forward only. Never `git merge Planning` onto `main`. Never force-push `main`. Use skill `merge-planning`:

```powershell
powershell -File scripts/Merge-PlanningToMain.ps1
powershell -File scripts/Merge-PlanningToMain.ps1 -Message "Add paste-HTML feature spec."
```

That updates `main` locally, then pushes `main` and `Planning`. Planning may be force-pushed with `--force-with-lease` because rebase/squash rewrites it. The merge script keeps `docs/features/to-review.md` from `main` if it already exists.

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
