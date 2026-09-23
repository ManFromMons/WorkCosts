# Agent board (GTK)

- **Id:** `docs/agent-ops/agent-board.md`
- **Status:** ready-for-agent
- **PR:** none
- **Not a product Seq story.** Do not put this under `docs/features/` (that would enter `Get-FeatureQueue` / pickup). Same rule as GNOME playbook slices.

Rewrites the **ops board** as an unpackaged **GTK4 + libadwaita** C# app under `tools/agent-board/`. It replaces the Ink/React TUI as the intended human surface, but **`tools/agent-tui` stays in the tree** (Q34). Agents are the **Cursor CLI** (`agent`) only — no `@cursor/sdk`, no WebView, no `cursor.com/agents` UI. **Next** always names one action for the **focused pane’s** selected story.

Companion invoke-only skills (same implementation PR): **`complete-review`**, **`resume-implementation`**.

## Objectives

- One Gir.Core GTK4 + libadwaita window on **Linux and Windows** (same UI, same slnx).
- **Next** is always visible and shows **one** action for the selected row in the **focused** pane.
- **Queue** = plan board. **Working** = live pipeline. **Inbox** = Outlook-style overlay for review.
- **One implement agent per story** (attach if it exists). **One shared Planning agent** for `plan-feature` (long context).
- Quit **detaches**; it does not wait for agents to idle.
- **Out of scope:** Will I DIY product UI (WinUI / iPad / GNOME product app). Embedding WebView/WebKit. Squash-merging GitHub PRs from the board. A third “Active agents” pane. Story **edit mode** in v1 (leave a hook). Optional vim **j/k h/l**. Standalone key **`L`**. Deleting `tools/agent-tui`. Flatpak. Adding this project to `WorkCosts.slnx`. `--worktree` isolation.

## User requirements

### Keys and widgets (whole app)

- **Arrows** (and Home/End/PgUp/PgDn where a list or document scrolls) move **inside** the focused pane.
- **Tab / Shift-Tab** move between panes (Queue ↔ Working ↔ Story ↔ Chat). Inbox overlay: list ↔ detail, then Esc closes.
- **Enter** in Chat **sends**. **Esc** dismisses overlay/prompt; does not quit.
- **q** quit (see Agents).
- **g** `git fetch` + refresh all panes.
- **?** help overlay (shortcuts + Next table).
- **r** opens the Inbox overlay. The **Next** control is also a **GTK Button** in the header; activating Next (click or focused Button + Enter/space) runs the same action Next currently names (including Open Inbox).
- **d** toggles showing **done** stories in Queue.
- **p** plan-feature (Planning agent). **i** start-implement. **n** pickup-next. **m** merge-planning.
- No **j/k** or **h/l** as primary keys. No **`L`**.

### Next (always on screen)

Header: title **Agent board** left; **Next** as a **GTK Button** (label = action + key hint + kebab/Seq). Follows the **focused pane’s** selected row. Updates immediately when the cursor moves.

On load and on **g**, Queue and Working each place their cursor on the first row in that pane’s sort; Next uses whichever pane is focused (default focus **Working** if it has rows, else **Queue**).

Action table (for that row):

| Situation | Next |
| :--- | :--- |
| Inbox `blocked` | Open Inbox — unanswered questions (reason in the inbox file) |
| `ready-for-review` | Open Inbox / review PR |
| `ready-to-resume` | **resume-implementation** |
| Agent running / `in-progress` | Attach Chat |
| `draft` | **p** plan-feature (Planning agent) |
| `ready-to-complete` | **complete-review** — show PR link; wait Continue or send back |
| Planning-only `ready-for-agent` | **m** merge-planning (tree must be clean) |
| `ready-for-agent` on main, deps done | **i** start-implement (story agent) |
| `ready-for-agent`, deps not done | Show waiting-on line; no start |
| Nothing selected / empty pane | One-line empty: “No story selected.” |

### Queue

Membership: **`ready-for-agent`** and **`ready-for-review`**, plus Queue **suffix indicators** (not extra roots) for **`ready-to-resume`** and **`ready-to-complete`** when those inbox states apply (a review story may appear in **both** Queue and Working).

- Dependency **tree**, **not inverted**. Seq order inside the tree.
- **Done** (feature file Status `done` **and** close-out finished) **hidden** unless **d** is on.
- **Ignore GNOME playbook slices** (`gnome-scaffold`, … in `docs/platforms/gnome-build-order.md`). They rebuild the **Will I DIY? product** as a GNOME app (`/start-port gnome`). They are **not** agent-board work, not Queue/Working rows, and not started by **i** / **n**. No filter toggle — omit them.
- **Hide `job-concept`:** superseded by `docs/features/garage-job.md`; no Seq; must not look startable.
- **draft** rows include feature files with Status `draft` **and** uncommitted new `docs/features/*.md` that are not yet on origin.
- Arrows move the cursor; Next, Story, and Chat follow. Enter is **not** required to activate a row.

### Working

Live pipeline only. Sort:

**blocked → review (`ready-for-review`) → resume (`ready-to-resume`) → agent working → draft → complete (`ready-to-complete`)**

**Agent working** is true if **either** a stored CLI session for that kebab (or Planning, for drafts) is a **live process**, **or** the inbox heading is **`in-progress`**.

Not in Working: `ready-for-agent` with no inbox work; stories after close-out; GNOME **product-port** slices; `job-concept`.

Selecting a Working row attaches Chat to **that story’s implement agent** (or shows idle / none). Selecting a **draft** attaches the **Planning** agent.

### Story

Read-only markdown of `docs/features/<seq>-<kebab>.md` (or unprefixed older files) for the focused selection. Arrows/PgUp/PgDn scroll. Missing file: **one-line empty state**, no crash.

v1 is not an editor. Keep a hook so a later pass can toggle edit. **plan-feature resume** is **p**, not editing in this pane.

### Inbox overlay

Outlook-style **AdwDialog / overlay** **on top of** the main window with a **margin** (~10–15%). Not a mode of Story. Not a blocking dialog that hosts a browser (there is no browser).

- **Left:** `blocked`, `ready-for-review`, `ready-to-resume`, `ready-to-complete` only. **`in-progress` headings are not listed.** Status on each row. Block **reason** is unanswered question text and/or reject reasons in `docs/features/to-review.md`.
- **Right:** Questions, Deviations, Verify for that heading.
  - **Questions:** one **editable Answer field per question** (not one box for the whole heading). Unanswered = empty / unchecked.
  - **Deviations:** each row is **unset / Accept / Reject**. Reject **requires a reason** field. Incomplete reject (Reject chosen, empty reason) counts as still open.
  - **Verify:** tick when the human has verified the change set.
- Headings must parse **`## 11-cars`** (leading digits), not only `## paste-html`.
- **Esc / close, no edits:** dismiss, no git.
- **Esc / close, dirty:** land **only** `docs/features/to-review.md` on **`main`** via the **platform script host** (bash canonical; see Technical design). Then refresh.

**Auto-status only** (on each edit, before land). No manual Status combo.

1. Any question open (unchecked or empty Answer) **or** any Reject with empty reason → **`blocked`**
2. All questions answered, at least one **Reject with reason**, Verify not required for this branch → **`ready-to-resume`** (implementer must follow spec / undo the deviation)
3. All questions answered, **no rejects**, Verify + all deviations **Accept** (or there are no deviations) **not** all done → **`ready-to-resume`**
4. All questions answered, **no rejects**, Verify ticked, every deviation **Accept** (or none) → **`ready-to-complete`**

There is **no `L` key**. Inbox close is the only inbox land.

**PR URL:** if the heading’s **Change set** / **PR** field already contains a correct `https://` pull-request URL, complete-review and Next use **that** URL. Do not call `gh` / `origin pr list` to invent another. If the field is missing or not a URL, show **no PR yet**.

### Chat

- **Type and Send:** a prompt box; Send / Enter runs a **new one-shot** CLI process (not a long-lived stdin TTY). If the process has exited, the next send still **`--resume`s** the stored id.
- Header: kebab + running / idle / failed.
- **ANSI:** keep and **render** SGR (colours, bold, underline, reverse). Do not strip escape sequences to plain text. Use a GTK text buffer (or equivalent) with tag colours from the decoded stream — **not** a WebView.
- Drive agents with the **exact Cursor CLI flags** in [docs/agent-handbook.md](../agent-handbook.md) (Print / headless + Resume). Do **not** embed `@cursor/sdk`.
- **Always `--force`** on every board send (see Accepted defaults).
- **`--workspace`** = repository root. **Do not** pass `--worktree`. All agents share **this** git worktree.
- **Model:** CLI default. Do not pass `--model`. Ignore `AGENT_TUI_MODEL` for this app.
- **One implement agent per story.** If a session id exists, **`--resume`** it; never start a second implement agent for that kebab.
- **One Planning agent** for all `/plan-feature` work (shared transcript).
- No dedicated “Active agents” pane; Working/Queue selection is the switcher.

Exact invocations (repo root = `--workspace`):

```text
# List sessions (discover / refresh stored ids)
agent ls

# First send for a new Planning or implement session (no id yet)
agent -p --force --workspace <repoRoot> --output-format text "<prompt>"

# Every later Type/Send, including idle sessions
agent -p --force --resume "<session-id>" --workspace <repoRoot> --output-format text "<prompt>"
```

After a **create** send, refresh id from `agent ls` (and JSON if the CLI prints a session id) into `tools/agent-board/.sessions.json`. Do not use `agent --continue` (that is “last session”, not per-story). Do not use interactive `agent` / `agent resume` without `-p` for Chat. Handbook also documents `agent resume` and `agent --resume "thread-id"`; the board’s send path is always **`-p --force --resume`**.

Missing `agent` on PATH or unauthenticated: Chat status **failed**; one message pointing at the handbook **Install and login** section. No login UI in the board. `CURSOR_API_KEY` / existing `agent login` is enough.

### Plan Feature

- **`p`:** resume/start `/plan-feature` on the **Planning** agent for the selected kebab.
- New name only when nothing is selected or the user explicitly plans new. **One-line prompt** in Next/Chat (no floating overlay besides a simple Adw entry if a name is required).
- The board **may `git checkout Planning`** (fetch + checkout) before Planning sends, and **may `git checkout main`** for **complete-review Continue**. Implementers still create `feature/…` branches themselves. **`m`** still goes through the merge-planning script (that script switches branches). Do not invent other checkouts.
- Agent works on the **Planning** branch. Board does not merge until **`m`**.

### Agents and quit

- Persist `tools/agent-board/.sessions.json` (gitignored): Planning CLI session id + map kebab → implement CLI session id.
- **q:** **detach** and exit. Do **not** wait for idle. Relaunch **attaches** via stored ids + `--resume`. One-shot `-p` processes should already have exited; if a send is **in flight**, confirm **Stop N sends** vs **Cancel quit**. No silent kill.
- Separate later: stop one story’s agent from Working. Not implied by quit.

### Skills and git

| Invoke | Does |
| :--- | :--- |
| `/complete-review` | Selected row must be `ready-to-complete`. Land inbox **Status `done`** on `main` if not already. Show **PR URL from inbox text** when it is a URL. Instruct to review and squash-merge on GitHub. **Wait:** **Continue** = fetch, **checkout `main`**, pull, **delete feature branch**, inbox last note (squash SHA = **`origin/main` tip after pull**, do not scrape GitHub). **Send back** = heading **`ready-for-review`** again, land on `main`. Does **not** squash-merge. |
| `/resume-implementation` | Attach that story’s **implement** agent; send recommence-from-review (`implement-feature`). |
| **`m`** | Confirm, then merge-planning via script host. **Dirty tree → refuse (message).** Only **`m`** is blocked by a dirty tree; **`i` / `p` / Chat** may run dirty. |
| **`i`** | `/start-implement` on selected Seq/kebab (story agent). |
| **`n`** | `/pickup-next-feature` if startable; else no-op + message. |
| **`g`** | fetch + refresh. |

Both new skills are **invoke-only** (`disable-model-invocation: true`), same as `start-implement`. Ship them **in this implementation**.

### Empty / error

- Missing story file: one-line empty in Story.
- `QUEUE_EMPTY` / empty Working: Next says so; keys that need a row no-op with a message.
- Inbox land fails (dirty other paths, script error): keep overlay open, show the error, do not discard ticks.
- Agent launch fail: Chat status failed; Queue/Working unchanged.
- GTK runtime missing on Windows: start script prints how to install GTK4/libadwaita for Gir.Core; do not crash with an empty window.

## Layout

Developer tool. **Adwaita**, not WinUI pixel copies. Structural grammar still applies: header with primary action on the right; **wide = grid**, **narrow = stack**.

**Wide** (`AdwBreakpoint` **min-width 800px**, locked):

```
AdwHeaderBar:  Agent board          [ Next: action · key · kebab ]
┌─ Queue (tree) ─────────┬─ Working (pipeline) ──────────────────┐
│                        │                                       │
├─ Story (markdown) ─────┼─ Chat (header + ANSI log + prompt) ───┤
│                        │                                       │
└────────────────────────┴───────────────────────────────────────┘
```

**Narrow** (window width **below 800px**): `AdwBreakpointBin` stacks **Next header → focused list (Queue or Working) → Story or Chat**. Tab still cycles the four regions. Measure window pixels, not terminal columns.

Inbox: overlay with margin; left list, right detail.

## Workflow

1. Launch `scripts/Start-AgentBoard.sh` (Linux) or `scripts/Start-AgentBoard.ps1` (Windows) from the repo root. Keep `Start-AgentTui.ps1` working.
2. Default focus Working if any pipeline rows, else Queue. Next names the action.
3. Tab to Queue/Working; arrows select; Next and Story follow; Chat attaches.
4. Review: **r** or Next Button when it says Open Inbox; tick/answer/accept/reject; auto-status; close to land on `main`.
5. `ready-to-resume` → `/resume-implementation`. `ready-to-complete` → `/complete-review` → PR link → Continue or send back.
6. Plan: `p` (Planning agent; board may checkout Planning). Land specs: `m` (clean tree).
7. Implement: `i` / `n` (story agent). Same worktree.
8. **q** detach.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Queue data | Port logic of `scripts/Get-FeatureQueue.ps1` | C# parse + bash twin; tree not inverted; hide done unless **d**; omit GNOME product-port slices and `job-concept` |
| Pickup | `scripts/Get-NextReadyFeature.ps1` + bash twin | **n** |
| Inbox land | Today `Update-ToReviewOnMain.ps1` | Canonical **bash** + Windows-capable host; **no `cmd.exe`** |
| Merge planning | Today `Merge-PlanningToMain.ps1` | Same: bash canonical + host; **no `cmd.exe`** |
| Inbox parse/patch | `tools/agent-tui/src/inbox.ts` (port to C#) | Headings `## 11-cars`; per-question answers; Accept/Reject + reason; auto-status |
| Git snapshot | `git` CLI from C# (`Process`) | dirty paths, branch, unpushed |
| Agents | Cursor CLI flags above | Session store; attach; per-story + Planning. No SDK |
| UI | none of Ink/React | Gir.Core **GTK4 + libadwaita**, `tools/agent-board/` |
| Skills | `update-to-review`, `start-implement`, `plan-feature` | `complete-review`, `resume-implementation` |
| Start | `scripts/Start-AgentTui.ps1` (keep) | `Start-AgentBoard.sh` / `.ps1` |

**Inbox work states** (heading **Status** first token). Feature file Status remains `draft | ready-for-agent | done`.

| Status | Meaning |
| :--- | :--- |
| `in-progress` | Implement agent working (not listed in Inbox overlay) |
| `blocked` | Open questions or incomplete reject |
| `ready-for-review` | Handover; human reviews |
| `ready-to-resume` | Questions answered; coder continues (including rejected deviations) |
| `ready-to-complete` | Verify + all deviations accepted; waiting squash |
| `done` | After Continue close-out |

Replace old inbox `resume` with **`ready-to-resume`**. Do not use `done` until complete-review Continue (or the heading was already done and we are only writing the last note). `Get-FeatureQueue.ps1` / handbook inbox how-to **must** learn the new names **in this implementation**.

### Host

- Language: **C# / net9.0**.
- Solution: **`tools/agent-board/AgentBoard.slnx`** (app + test project). **Not** on `WorkCosts.slnx`. Same pattern as GNOME: own slnx.
- UI: **Gir.Core** GTK4 + libadwaita on **Linux and Windows**. GTK is the **only** UI (no Spectre/terminal twin).
- Unpackaged developer tool next to the repo. **Not** a Flatpak. Windows: document GTK4/libadwaita runtime (MSYS2 or equivalent); start script fails clearly if GObject libraries are missing.
- Wiring: **no DI container**. `new` services from `Program`. `WORKCOSTS_ROOT` or `git rev-parse --show-toplevel`.
- Data: **no SQLite**. Session file gitignored. Inbox still only on `main`.
- Pin Gir.Core package versions at implement time (latest stable), record in the csproj.

### Script host (Linux + Windows)

Port land/merge/queue helpers **to bash**. C# `ScriptHost`:

- **Linux:** `/bin/bash scripts/<name>.sh …`
- **Windows:** Git-for-Windows `bash.exe` if present, else a **PowerShell wrapper** that calls **`git` directly** (never `cmd.exe /c`).

Must rewrite `Test-GitBlob` in any remaining `.ps1` to:

```powershell
git cat-file -e $RevPath
# use $LASTEXITCODE; do not call cmd.exe
```

Canonical new files (names may match this pattern):

- `scripts/update-to-review.sh`
- `scripts/merge-planning.sh`
- `scripts/get-feature-queue.sh`
- `scripts/get-next-ready-feature.sh`

Keep `.ps1` entry points for humans on Windows; they go through the same abstraction. Board C# **always** uses `ScriptHost`, not a hard-coded `pwsh` requirement on Linux.

C# may parse `docs/features/*.md` and inbox markdown itself for the UI so lists stay snappy; membership and sort **must match** the queue script tests.

### GNOME product-port slices vs this GTK board

**Agent board** is a **developer ops** GTK app (Gir.Core, unpackaged, Linux and Windows). It is **not** slice work from `docs/platforms/gnome-build-order.md`.

Those slices rebuild the **main Will I DIY? app** as GNOME (`src/linux/`, Flatpak, `/start-port gnome`). **Ignore them for the agent-board implementation:** do not implement them here, do not list them in Queue/Working, do not wire **i** / **n** to `start-port`. Pickup for that track stays `scripts/Get-NextPortSlice.ps1` outside this board.

**`job-concept`** is **Status superseded** — replaced by `docs/features/garage-job.md` (and later WorkJob/ItemOfWork stories). Hide it; it has no Seq and must not look startable.

## Tests

Not `WorkCosts.Tests` product cases. `tools/agent-board` unit tests:

- `QueueMembership_ReadyForAgentAndReview_HidesDoneUnlessToggled`
- `QueueHides_GnomeSlices_AndJobConcept`
- `QueueIncludes_UncommittedDraftFeatureFiles`
- `WorkingSort_BlockedReviewResumeAgentDraftComplete`
- `AgentWorking_TrueIfLiveProcessOrInboxInProgress`
- `NextFollowsFocusedPaneSelection`
- `InboxParse_SeqPrefixedHeading_11Cars`
- `InboxAutoStatus_OpenQuestion_Blocked`
- `InboxAutoStatus_RejectEmptyReason_Blocked`
- `InboxAutoStatus_RejectWithReason_ReadyToResume`
- `InboxAutoStatus_AnswersWithoutVerify_ReadyToResume`
- `InboxAutoStatus_VerifyAndAllAccept_ReadyToComplete`
- `InboxClose_Clean_NoGit` / `InboxClose_Dirty_LandsOnlyToReviewOnMain`
- `PrUrl_UsesInboxTextWhenHttpsPresent`
- `Attach_DoesNotStartSecondImplementAgentForKebab`
- `PlanFeature_UsesSharedPlanningAgent`
- `Quit_Detaches_DoesNotWaitForIdle`
- `ScriptHost_GitCatFile_DoesNotUseCmdExe`
- `Cli_Send_UsesPrintForceResumeWorkspace`

No live Cursor network in CI. Fake session ids. Fake `agent` executable for CLI argv tests.

## Open questions

_(none)_

## Accepted defaults

- Tooling spec at `docs/agent-ops/agent-board.md`, not Seq.
- GTK4 + libadwaita via Gir.Core, Linux **and** Windows, unpackaged, own `AgentBoard.slnx`.
- Always **`--force`** on every board send (locked). Without it, `agent -p` proposes diffs and **does not write files**. No per-send prompt.
- Exact CLI: `-p --force --resume <id> --workspace <root> --output-format text` after create; `agent ls` to refresh ids. Same worktree. CLI default model. Type-and-Send one-shots. Render ANSI.
- Arrows + Tab. No vim keys in v1.
- Next follows focused selection. Default focus Working if non-empty. Next is header **GTK Button**; Inbox also **r**.
- Narrow layout is **AdwBreakpoint 800px** (locked).
- Queue tree, not inverted; done hidden behind **d**; GNOME **product-port** slices omitted (wrong track); `job-concept` hidden; uncommitted drafts included.
- Working sort: blocked → review → resume → agent working → draft → complete. Agent working = live process **or** `in-progress`.
- Shared Planning agent; one implement agent per story; attach.
- **Cursor CLI** (`agent`) only. No `@cursor/sdk`, no cloud-agent URLs in v1.
- Quit detaches; confirm only if a send is in flight.
- Drop **`L`**. Skills **`complete-review`** and **`resume-implementation`** (invoke-only), **same PR**.
- complete-review Continue = fetch, checkout `main`, pull, delete feature branch, last note with `origin/main` tip. Never squash-merge from the app. PR URL from inbox text when present.
- Story read-only v1; empty state if missing file.
- Inbox overlay margin; auto-status only; per-question answers; Accept **and** Reject-with-reason.
- Dirty tree blocks **`m` only**.
- Implement branch name: **`feature/agent-board-Agent-board`** from `origin/main`.
- Keep **`tools/agent-tui`**. Add start scripts beside it; handbook may list both until a later cutover.
- Update `Get-FeatureQueue.ps1` (and bash twin) plus inbox how-to on main for the new Status names.

## Implementation notes for an agent

1. Do not add `docs/features/*` for this. Do not change pickup so this becomes next Seq.
2. Branch **`feature/agent-board-Agent-board`** from up-to-date `origin/main`.
3. Port inbox parse/patch with Seq-prefixed headings. Extend queue/inbox leftovers that still assume `resume` and `## kebab` without digits — board + `Get-FeatureQueue` must understand **`ready-to-resume`** / **`ready-to-complete`**.
4. Create `tools/agent-board/` (Gir.Core GTK4/libadwaita, net9.0, `AgentBoard.slnx` + tests), session file gitignored, ANSI decoder, `ScriptHost`, bash twins for land/merge/queue. Remove **`cmd.exe`** from `Test-GitBlob` in existing `.ps1` even if bash is primary.
5. Skills `.cursor/skills/complete-review/` and `.cursor/skills/resume-implementation/` in this PR (`disable-model-invocation: true`).
6. Start scripts `scripts/Start-AgentBoard.sh` and `.ps1`. Keep `tools/agent-tui` and `Start-AgentTui.ps1`. Handbook: add the GTK board as the preferred Seq board; do not delete TUI docs in this PR.
7. Do not: WinUI; squash-merge; wait-on-quit; `@cursor/sdk`; WebView; second implement agent per kebab; Story editor in v1; `git add` to-review off `main`; `--worktree`; Flatpak; add to `WorkCosts.slnx`; Spectre.Console; require pwsh on Linux; implement GNOME **product** port slices (`gnome-scaffold`, …) as part of this board.
