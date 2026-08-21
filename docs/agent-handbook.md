# Agent handbook

This is the map of **Will I DIY?** agent docs, skills, and scripts. Product behaviour still lives in the files listed here — this page does not replace them.

Use it when you need to know **which file to open**, **which skill to invoke**, or **how to run that skill from the Cursor CLI**. Humans adding a shop can start at [README.md](../README.md) (Add a supplier website) or the section [Planning a supplier source](#planning-a-supplier-source-interactive) below.

Canonical CLI docs: [cursor.com/docs/cli](https://cursor.com/docs/cli/overview) and [cursor.com/docs/skills](https://cursor.com/docs/skills).

---

## How to read this repo

| You are… | Start here | Then |
| :--- | :--- | :--- |
| A coder agent | [AGENTS.md](../AGENTS.md) | The screen/parsing/data file for the surface you touch; the feature file under `docs/features/` |
| Planning a product change | Skill `plan-feature` | [PLANNING.md](../PLANNING.md) (locked decisions only), then write `docs/features/<kebab>.md` |
| Adding a supplier host | `/start-add-source` + a URL ([AGENTS.md](../AGENTS.md), [README.md](../README.md)) | Confirm Name/price on **≥3** pages ([confirm-samples.md](../.cursor/skills/add-product-source/confirm-samples.md)); story then [parsing/adding-a-source.md](parsing/adding-a-source.md) |
| Implementing a ready story | `@start-implement` or skill `pickup-next-feature` | Feature file + [layout-grammar.md](layout-grammar.md) + named screens |
| Landing specs onto `main` | Skill `merge-planning` | `scripts/Merge-PlanningToMain.ps1` |
| Recording questions / scan | Skill `update-to-review` | `git show origin/main:docs/features/to-review.md` |

**Two long-lived branches**

- **`main`** — shippable app. Inbox `docs/features/to-review.md` lives **only** here.
- **`Planning`** — specs, this handbook, skills, `AGENTS.md`, `docs/`. Land with merge-planning (rebase + squash + fast-forward). Never `git merge Planning` onto `main`. Never force-push `main`.

**Two kinds of “script”**

- **Cursor skill** (`.cursor/skills/<name>/SKILL.md`) — instructions for an agent. Invoke with `/skill-name` in chat or CLI.
- **PowerShell** (`scripts/*.ps1`) — git plumbing the skills call. You can run those yourself; they do not load agent skills.

---

## Reading order for a new agent

1. [AGENTS.md](../AGENTS.md) — product in one page, branch rules, commands.
2. [PLANNING.md](../PLANNING.md) — locked decisions (do not re-litigate).
3. [architecture.md](architecture.md) — layers, process map, projects.
4. [layout-grammar.md](layout-grammar.md) — how screens are put together.
5. The **screen** file under [screens/](screens/) for the surface you change.
6. [data/schema.md](data/schema.md) and [data/connection.md](data/connection.md) if you touch persistence.
7. [parsing/overview.md](parsing/overview.md) if you touch import or HTML.
8. The **feature file** `docs/features/<kebab>.md` if one exists for this work.

Always-on Cursor rules (injected without asking): `.cursor/rules/product.mdc`, `.cursor/rules/layout-grammar.mdc`. Parsing/data rule applies when you edit Core / Parsing / Tests: `.cursor/rules/data-and-parsing.mdc`.

---

## Catalogue: root files

| File | What it is | Do not |
| :--- | :--- | :--- |
| [AGENTS.md](../AGENTS.md) | Short operating manual for agents. Points here for the full map. **Adding a supplier website** is a dedicated section there. | Invent a second product. Implement on `Planning`. |
| [PLANNING.md](../PLANNING.md) | Locked product decisions for Windows / GNOME / iPad rebuilds. Historical questions stay here. | Put a new feature’s source of truth only here. |
| [README.md](../README.md) | Human getting-started: build, test, Inno, MSIX, and **how to start `/start-add-source`**. | Treat as the full agent spec (that is still `AGENTS.md`). |

---

## Catalogue: `docs/` (product and ports)

### Architecture and layout

| File | Role |
| :--- | :--- |
| [architecture.md](architecture.md) | Three shells over Core + Parsing. Domain paragraph. Windows process map (Add Product, cache). |
| [layout-grammar.md](layout-grammar.md) | Size classes (wide list+detail / narrow stack), header + trailing Add, sheets vs dialogs, OS spacing. |
| [tests.md](tests.md) | xUnit contract. Fixtures, no live network in CI, Mac agents run the same tests. |

### Data

| File | Role |
| :--- | :--- |
| [data/schema.md](data/schema.md) | Tables and columns. EF is canonical; Swift follows migrations. |
| [data/connection.md](data/connection.md) | SQLite path per platform; images and page cache as **files**. |
| [data/export-import.md](data/export-import.md) | Zip backup spec (XML + blobs). **Spec only** until a feature lands. Import merges. |

### Parsing and fetch

| File | Role |
| :--- | :--- |
| [parsing/overview.md](parsing/overview.md) | HTML → `ProductPageMetadata`. Amazon / Autodoc / generic. Source from URL host. |
| [parsing/adding-a-source.md](parsing/adding-a-source.md) | Playbook: ≥3 confirmed product pages, then discover fetch, fixtures, tests, detector. |
| [parsing/browser-session.md](parsing/browser-session.md) | `IBrowserPageSession` / WebView2 off-screen. Never inside a blocking dialog. |
| [parsing/paste-html.md](parsing/paste-html.md) | Background for paste-HTML. Source of truth is the feature file. |

### Screens (information architecture)

One file per surface. Change the matching file when you change that UI.

| File | Surface |
| :--- | :--- |
| [screens/shell.md](screens/shell.md) | Title bar, nav, theme switch, garage background |
| [screens/home.md](screens/home.md) | Work jobs list and savings |
| [screens/jobs.md](screens/jobs.md) | Job templates master/detail |
| [screens/work.md](screens/work.md) | Placeholder Work nav |
| [screens/work-job-detail.md](screens/work-job-detail.md) | Instance of a job + line items |
| [screens/products.md](screens/products.md) | Catalogue + **Add Product sheet** |
| [screens/categories.md](screens/categories.md) | Category chips and job toggles |
| [screens/settings.md](screens/settings.md) | Settings **page** (not a preferences window) |
| [screens/markdown-editor.md](screens/markdown-editor.md) | Write / Preview + formatting toolbar |
| [screens/dialogs.md](screens/dialogs.md) | Yes/No confirms; sheets vs dialogs |

### Platforms

| File | Role |
| :--- | :--- |
| [platforms/windows.md](platforms/windows.md) | WinUI 3 reference app |
| [platforms/gnome-flatpak.md](platforms/gnome-flatpak.md) | Gir.Core + libadwaita, Flatpak data dir |
| [platforms/ipados-swiftui.md](platforms/ipados-swiftui.md) | SwiftUI; no .NET runtime in the iPad binary |

---

## Catalogue: features and review inbox

| Path | Role |
| :--- | :--- |
| `docs/features/<kebab>.md` | **Source of truth** for one story. Template: `.cursor/skills/plan-feature/template.md`. |
| `docs/features/source-<host>.md` | One supplier website. Template: `.cursor/skills/add-product-source/template.md`. Needs **≥3** URLs, each with user-confirmed Name and GBP price. |
| `docs/features/<kebab>-delivery.md` | Short “what landed” after a PR exists. Template: `.cursor/skills/implement-feature/delivery-template.md`. Not a diary. |
| `docs/features/to-review.md` | Human inbox. **Canonical copy is on `main` only.** Read with `git show origin/main:docs/features/to-review.md`. |
| `docs/features/paste-html.md` | First product story (Seq 1). Paste / open HTML on Add Product. |

**Feature file Status** (on the story): `draft` → `ready-for-agent` → `done`.

**Work states** (inbox on `main` only): `in-progress`, `blocked`, `resume`, `scan`. Never put those on the feature file.

**Queue:** integer **Seq** (never reuse) + **Depends-on** (kebab ids or `none`). Pickup: lowest Seq that is `ready-for-agent` whose dependencies are `done`. Script: `scripts/Get-NextReadyFeature.ps1` (reads **`origin/main`**, not Planning).

---

## Catalogue: Cursor rules

| File | When it applies |
| :--- | :--- |
| `.cursor/rules/product.mdc` | Always. Display name, GBP, offline, no extra iOS/Mac TFMs on WinUI. |
| `.cursor/rules/layout-grammar.mdc` | Always. Header + Add, stack on narrow, Add Product is a sheet. |
| `.cursor/rules/data-and-parsing.mdc` | When editing `WorkCosts.Core`, `WorkCosts.Parsing`, `WorkCosts.Tests`. |

---

## Catalogue: skills

All project skills live under `.cursor/skills/<name>/SKILL.md`. Folder name **must** match the `name:` frontmatter.

| Skill | Auto? | What it does |
| :--- | :--- | :--- |
| `plan-feature` | Yes | Write `docs/features/<kebab>.md` on **Planning**. For shops, copy the source template. Does not implement. |
| `start-implement` | **No** (`disable-model-invocation: true`) | Kickoff: named ready story, or next in queue. Then follow `implement-feature`. |
| `implement-feature` | Yes | Code from a `ready-for-agent` spec. Branch from `origin/main`. Inbox via `update-to-review`. No PR until scan **done**. |
| `pickup-next-feature` | Yes | Run `Get-NextReadyFeature.ps1`, then `start-implement` on that id. Stop on `QUEUE_EMPTY`. |
| `start-add-source` | **No** | URL → interactive confirm of ≥3 pages + story. Ready story id → `add-product-source`. |
| `add-product-source` | Yes | Discover HttpClient vs Chromium, fixture, failing tests, detector/parser/fetch. Same inbox/PR rules. |
| `update-to-review` | Yes | Land **only** `docs/features/to-review.md` on `main` via `Update-ToReviewOnMain.ps1`. |
| `merge-planning` | Yes | Rebase/squash Planning onto main, fast-forward, push both. Preserves to-review on main. |

**Invoke-only** skills (`start-implement`, `start-add-source`) are never applied from ambient chat. You must type `/start-implement` or `/start-add-source` (editor or CLI).

Templates next to skills (agents read them when the skill says so):

| Path | Used by |
| :--- | :--- |
| `.cursor/skills/plan-feature/template.md` | New product stories |
| `.cursor/skills/plan-feature/examples.md` | Shape only |
| `.cursor/skills/add-product-source/template.md` | `source-<host>.md` |
| `.cursor/skills/add-product-source/confirm-samples.md` | Interactive planning: confirm Name/price; ≥3 pages |
| `.cursor/skills/implement-feature/to-review-entry.md` | Inbox heading |
| `.cursor/skills/implement-feature/delivery-template.md` | `*-delivery.md` |

---

## Catalogue: PowerShell (git), not skills

Run from the **repository root**. Working tree must be clean except where a skill says otherwise.

```powershell
powershell -File scripts/Merge-PlanningToMain.ps1
powershell -File scripts/Merge-PlanningToMain.ps1 -Message "Add paste-HTML feature spec."

powershell -File scripts/Update-ToReviewOnMain.ps1
powershell -File scripts/Update-ToReviewOnMain.ps1 -Message "to-review: paste-html scan"

powershell -File scripts/Get-NextReadyFeature.ps1
```

VS Code / Cursor task labels: `merge-planning`, `update-to-review`, `next-ready-feature`.

| Script | Effect |
| :--- | :--- |
| `Merge-PlanningToMain.ps1` | Fetch; FF local `main`; rebase Planning; squash if needed; FF-merge into `main`; restore to-review from main; push `main`; push Planning `--force-with-lease`. |
| `Update-ToReviewOnMain.ps1` | Commit **only** `docs/features/to-review.md` on `main`, push `main` (never `--force`), return to the previous branch. |
| `Get-NextReadyFeature.ps1` | Prints a kebab id or `QUEUE_EMPTY`. Reads `origin/main`. Fetch only. |

Build/test (any branch, for product code):

```powershell
dotnet build WorkCosts.slnx
dotnet test WorkCosts.slnx --settings .runsettings
dotnet run --project WorkCosts/WorkCosts.csproj
```

---

## Lifecycle (specs → code → main)

```
Planning branch                          main
─────────────────                        ────
plan-feature  →  docs/features/foo.md
merge-planning  ─────────────────────►   foo.md lands on main
                                         (to-review.md untouched)

feature/foo-Title  (from origin/main)
  start-implement / implement-feature
  update-to-review  ─────────────────►   to-review.md (in-progress / blocked / scan)
  human ticks scan → Status done
  open squash PR  ───────────────────►   GitHub PR (human squash-merges)
  foo.md Status done + foo-delivery.md
```

Do **not** open the GitHub PR while to-review for that feature is still questions or unchecked deviations.

---

## Planning a supplier source (interactive)

Full protocol: `.cursor/skills/add-product-source/confirm-samples.md`. Also: [AGENTS.md](../AGENTS.md) (Adding a supplier website), [README.md](../README.md), [parsing/adding-a-source.md](parsing/adding-a-source.md).

`/start-add-source` is invoke-only. Starting with a **URL** is a conversation, not a one-shot scrape.

**What you type**

```text
/start-add-source https://www.example.com/product/…
```

On **`Planning`**. Interactive `agent` or Cursor chat. Do not use `agent -p` for this step.

**What happens**

1. The agent opens or fetches the page and **proposes** Name and GBP price (optional fields only if obvious).
2. You **confirm or correct** from what you see on the page. Your answer is the contract; an unconfirmed scrape is not.
3. It asks for **more product URLs on the same host** until **three** pages are confirmed. **One page at a time.**
4. It writes `docs/features/source-<host>.md` on **Planning** with those three rows, **Status** `ready-for-agent`.
5. It **stops**. No feature branch and no parser until you invoke `/start-add-source source-<host>` (after `merge-planning` if you want the story on `main` first).

Implementation then: one trimmed HTML fixture **per sample**, xUnit Name + UnitPrice for **all three**, HttpClient first then Chromium if blocked. Branch `feature/source-<host>-<Title>` from `origin/main`.

Need a login, a CAPTCHA you cannot pass, or a listing page: skip that URL and give another product page.

---

## Skills from the Cursor CLI

Project skills in `.cursor/skills/` load in the **editor** and in the **Cursor CLI** (`agent`). Plugin skills may not appear in the CLI; these repo skills are project-local and should.

Official references: [Using Agent in CLI](https://cursor.com/docs/cli/using), [Headless CLI](https://cursor.com/docs/cli/headless), [Agent Skills](https://cursor.com/docs/skills).

### Install and login (once)

Windows PowerShell:

```powershell
irm 'https://cursor.com/install?win32=true' | iex
agent login
agent status
```

macOS / Linux / WSL:

```bash
curl https://cursor.com/install -fsS | bash
agent login
```

Unattended scripts can use `CURSOR_API_KEY` instead of `agent login`.

Always run `agent` from the **WorkCosts repository root** (or pass `--workspace` to that path). The CLI reads `AGENTS.md`, `.cursor/rules/`, and `.cursor/skills/` from that root.

### Interactive session

```powershell
cd C:\Users\julia\source\repos\WorkCosts
agent
```

Or start with a prompt:

```powershell
agent "Read AGENTS.md and summarise the next ready-for-agent story."
```

| Input | Effect |
| :--- | :--- |
| `/` then skill name | Slash menu. Enter attaches the skill to **this message**. |
| `/plan-feature` … | Typed invocation. Same as picking from the menu. |
| Alt+Enter (Windows) / Option+Enter (Mac) on a skill | **Sticky custom mode** — skill stays on until you exit it. Use for a long implement or add-source session. |
| Shift+Tab | Rotate Agent / Plan / Ask **modes** (Cursor product modes, not our skill names). |
| `/plan` or `--mode=plan` | Cursor Plan mode: design, fewer edits. **Do not** use this for `plan-feature` if you want the markdown file written — that skill must **write** `docs/features/`. Use default Agent mode. |
| `/ask` or `--mode=ask` | Read-only. Fine for “which doc do I read?”; useless for implement/merge. |
| `@` | Attach files/folders as extra context. |
| `&` + message | Hand off to a Cloud Agent (own branch, async). |
| `-w` / `--worktree` | Isolated git worktree under `~/.cursor/worktrees/`. Useful so Planning WIP stays untouched. |

**Invoke-only skills** — type the slash name. Natural language will **not** load them:

```text
/start-implement paste-html
/start-add-source source-halfords
```

Auto skills can be named the same way, or described in English (“merge Planning into main”). Naming `/merge-planning` is more reliable.

Stay in **Agent mode** for every skill that edits git or files (`plan-feature`, `implement-feature`, `add-product-source`, `update-to-review`, `merge-planning`).

### Print / headless mode (scripts and one-shot jobs)

`-p` / `--print` runs one prompt and prints the result. **File writes require `--force` (or `--yolo`).** Without `--force`, the agent proposes changes and does not apply them.

Slash skills work in print mode: put `/skill-name` at the start of the prompt string.

PowerShell quoting: use double quotes around the prompt. If the prompt contains `"`, use a here-string.

```powershell
# Read-only question (no --force)
agent -p "What does docs/parsing/overview.md say about generic vs dedicated parsers?"

# Write a feature spec on Planning (check out Planning first)
git checkout Planning
git pull
agent -p --force "/plan-feature Plan zip export/import. Use docs/data/export-import.md. Status draft until open questions are answered."

# Kick off the next queued story (invoke-only skill)
agent -p --force "/start-implement"

# Named story
agent -p --force "/start-implement paste-html"

# New supplier host (story already ready-for-agent)
agent -p --force "/start-add-source source-halfords"

# URL only — interactive planning (do not use -p; needs a conversation)
agent "/start-add-source https://www.example.com/p/123"

# Inbox on main (coder already committed product code; only to-review.md dirty)
agent -p --force "/update-to-review Land scan for paste-html after tests passed."

# Land Planning
agent -p --force "/merge-planning Message: Add agent handbook and source-host playbook."
```

Useful flags:

```powershell
agent -p --force --model "gpt-5" "/pickup-next-feature"
agent -p --output-format text "/pickup-next-feature"
agent -p --output-format json "List docs/features/*.md Status and Seq from origin/main."
agent --workspace C:\Users\julia\source\repos\WorkCosts -p --force "/merge-planning"
agent --worktree add-halfords -p --force "/start-add-source source-halfords"
```

`--mode=ask` with `-p` is safe for documentation questions. Do not combine Ask mode with `--force` and an implement skill.

Resume:

```powershell
agent ls
agent resume
agent --continue
agent --resume "thread-id"
```

### Which checkout for which skill

| Skill | Git checkout before you run `agent` |
| :--- | :--- |
| `plan-feature` | **`Planning`**, pulled. |
| `start-add-source` **with a URL** / incomplete story | **`Planning`**. Interactive confirm of ≥3 pages. No feature branch yet. |
| `merge-planning` | Clean tree; script fetches and switches. Dirty tree → stop. |
| `start-implement`, `implement-feature`, `pickup-next-feature`, `start-add-source` **on a ready story**, `add-product-source` | Prefer a **clean** repo. The skill branches from `origin/main`. Do not commit product WIP on `Planning` or `main`. `--worktree` avoids touching a dirty Planning tree. |
| `update-to-review` | **Feature branch**, product code already committed; working tree dirty **only** with `docs/features/to-review.md` (copied from main). |

Cloud Agents (`&` or the Cloud Agent UI) use a remote checkout. They still must follow the same branch rules. Do not let a cloud job merge Planning with a merge commit or force-push `main`.

### Editor vs CLI (same skills)

| Place | How to attach a skill |
| :--- | :--- |
| Cursor chat | `/skill-name` or `@` skill. Alt+Enter = sticky mode. |
| CLI interactive | `/` menu or type `/skill-name`. |
| CLI print | `agent -p --force "/skill-name …"` |

If a brand-new skill folder does not appear in an **already running** CLI session, start a new `agent` process (CLI does not always watch for new skills).

### Worked prompts (copy and adapt)

**Plan a host (on Planning — conversation, not a one-shot)**

```text
/start-add-source https://www.halfords.com/…
```

The agent opens or fetches the page, proposes Name and GBP price, and waits. You confirm or paste what you see. It then asks for more product URLs until **three** pages are confirmed. Only then it writes `docs/features/source-<host>.md` as `ready-for-agent`. It does **not** implement until you say so (`/start-add-source source-halfords`).

Do not pre-fill Expected Name/price in the prompt and skip confirmation. Headless `agent -p` must not invent those fields.

**Implement next story**

```text
/start-implement
Follow implement-feature. Use the next ready-for-agent story if I did not name one.
```

**Implement a source** (story already `ready-for-agent` with three confirmed samples)

```text
/start-add-source source-halfords
Discover fetch, one fixture per sample, failing Name+UnitPrice tests for all three, then integrate.
Inbox on main via update-to-review. No GitHub PR until that heading is Status done.
```

---

## Do not confuse

| This | Is not |
| :--- | :--- |
| Cursor **Plan mode** (`--mode=plan`) | Skill **`plan-feature`** (writes markdown on Planning) |
| `/start-implement` | Automatically picking a story — it will, but only **after** you invoke it |
| `docs/parsing/paste-html.md` | `docs/features/paste-html.md` (the latter is the source of truth) |
| `to-review.md` on Planning / a feature branch | The inbox — only `origin/main` counts |
| `scripts/*.ps1` | Cursor skills — scripts are git helpers the skills run |
| A GitHub **feature PR** | `merge-planning` — specs do not go through a feature PR |
| Agent scrape of a shop page | User-confirmed Name/price on the story — confirmation is the contract |

---

## Maintenance

When you add a screen, feature, skill, or script: add a row to the matching table above in the same change on **Planning**, then land with `merge-planning`.
