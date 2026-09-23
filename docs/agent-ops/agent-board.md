# Agent board (terminal)

- **Id:** `docs/agent-ops/agent-board.md`
- **Status:** ready-for-agent
- **PR:** none
- **Not a product Seq story.** Do not put this under `docs/features/` (that would enter `Get-FeatureQueue` / pickup). Same rule as GNOME playbook slices.

Rewrites `tools/agent-tui/` (Ink + React + `@cursor/sdk`) as a **terminal-native** app under `tools/`. Agents are the **Cursor CLI** (`agent`) only — no SDK embedding. Direct control of the agent workflow: **Next** always says what to do on the **focused pane’s** selected story.

Companion invoke-only skills: **`complete-review`**, **`resume-implementation`**.

## Objectives

- A robust text UI that runs in the terminal (lazygit-class: arrows in a pane, Tab between panes).
- **Next** is always visible and shows **one** action for the selected row in the **focused** pane.
- **Queue** = plan board. **Working** = live pipeline. **Inbox** = Outlook-style overlay for review.
- **One implement agent per story** (attach if it exists). **One shared Planning agent** for `plan-feature` (long context).
- Quit **detaches**; it does not wait for agents to idle.
- **Out of scope:** WinUI / GNOME / iPad product UI. Embedding WebView. Squash-merging GitHub PRs. A third “Active agents” pane. Story **edit mode** in v1 (leave a hook). Optional vim **j/k h/l** (add later if wanted). Standalone key **`L`** (Inbox close lands).

## User requirements

### Keys (whole app)

- **Arrows** (and Home/End/PgUp/PgDn where a list or document scrolls) move **inside** the focused pane.
- **Tab / Shift-Tab** move between panes (Queue ↔ Working ↔ Story ↔ Chat). Inbox overlay: list ↔ detail, then Esc closes.
- **Enter** sends Chat. **Esc** dismisses overlay/prompt; does not quit.
- **q** quit (see Agents).
- **g** `git fetch` + refresh all panes.
- **?** help.
- No **j/k** or **h/l** as primary keys.

### Next (always on screen)

One line (plus the key). Follows the **focused pane’s** selected row. Updates immediately when the cursor moves.

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

Membership: **`ready-for-agent`** and **`ready-for-review`**, plus Queue indicators for **`ready-to-resume`** and **`ready-to-complete`** when those inbox states apply (a review story may appear in **both** Queue and Working).

- Dependency **tree**, **not inverted**. Seq order inside the tree.
- **Done** (feature file Status `done` **and** close-out finished) **hidden** unless toggled.
- Arrows move the cursor; Next, Story, and Chat follow. Enter is **not** required to activate.

### Working

Live pipeline only. Sort:

**blocked → review (`ready-for-review`) → resume (`ready-to-resume`) → agent working (`in-progress`) → draft → complete (`ready-to-complete`)**

Not in Working: `ready-for-agent` with no inbox work; stories after close-out.

Selecting a Working row attaches Chat to **that story’s implement agent** (or shows idle / none). Selecting a **draft** attaches the **Planning** agent.

### Story

Read-only markdown of `docs/features/<seq>-<kebab>.md` (or unprefixed older files) for the focused selection. Arrows/PgUp/PgDn scroll. Missing file: **one-line empty state**, no crash.

v1 is not an editor. Keep a hook so a later pass can toggle edit. **plan-feature resume** is **p**, not editing in this pane.

### Inbox overlay

Outlook-style panel **on top of** the main window with a **margin**. Not a mode of Story.

- **Left:** `blocked`, `ready-for-review`, `ready-to-resume`, `ready-to-complete`. Status on each row. Block **reason** is the unanswered question text in `docs/features/to-review.md`.
- **Right:** Questions, Deviations, Verify for that heading. Tick boxes; type **Answer:** on question lines.
- Headings must parse **`## 11-cars`** (leading digits), not only `## paste-html`.
- **Esc / close, no edits:** dismiss, no git.
- **Esc / close, dirty:** land **only** `docs/features/to-review.md` on **`main`** via `scripts/Update-ToReviewOnMain.ps1`. Then refresh.

**Auto-status** (on each edit, before land):

1. Any question open (unchecked or no **Answer:**) → **`blocked`**
2. All questions answered, Verify + deviations **not** all ticked → **`ready-to-resume`**
3. All questions answered, Verify + all deviations ticked → **`ready-to-complete`**

There is **no `L` key**. Inbox close is the only inbox land.

### Chat

- Type-and-send prompt for the **attached** session (`agent` stdin / resume).
- Header: kebab + running / idle / failed.
- Drive agents with the **Cursor CLI** (`agent` / `agent resume`). Do **not** embed `@cursor/sdk` or open `cursor.com/agents` from this app.
- **One implement agent per story.** If a session exists, **attach** (`agent resume` with the stored id); never start a second implement agent for that kebab.
- **One Planning agent** for all `/plan-feature` work (shared transcript).
- No dedicated “Active agents” pane; Working/Queue selection is the switcher. Background CLI sessions keep running.

### Plan Feature

- **`p`:** resume/start `/plan-feature` on the **Planning** agent for the selected kebab.
- New name only when nothing is selected or the user explicitly plans new. **One-line prompt** in Next/Chat (no floating overlay).
- Agent works on the **Planning** branch. Board does not merge until **`m`**.

### Agents and quit

- Persist `tools/agent-board/.sessions.json` (gitignored): Planning CLI session id + map kebab → implement CLI session id.
- **q:** **detach** and exit. Do **not** wait for idle. Relaunch **attaches** via `agent resume`. CLI agents outlive the board.
- If a wrapper child would **die with this process**: confirm **Stop N agents** vs **Cancel quit**. No silent kill. Prefer launching `agent` so it **outlives** the board (no wait-on-quit).
- Separate later: stop one story’s agent from Working. Not implied by quit.

### Skills and git

| Invoke | Does |
| :--- | :--- |
| `/complete-review` | Selected row must be `ready-to-complete`. Land inbox **Status `done`** on `main` if not already. Show **PR URL**. Instruct to review and squash-merge on GitHub. **Wait:** **Continue** = fetch, checkout `main`, pull, **delete feature branch**, inbox last note (squash SHA). **Send back** = heading **`ready-for-review`** again, land on `main`. Does **not** squash-merge. |
| `/resume-implementation` | Attach that story’s **implement** agent; send recommence-from-review (`implement-feature`). |
| **`m`** | Confirm, then `Merge-PlanningToMain.ps1`. Dirty tree → refuse (message). |
| **`i`** | `/start-implement` on selected Seq/kebab (story agent). |
| **`n`** | `/pickup-next-feature` if startable; else no-op + message. |
| **`g`** | fetch + refresh. |

Both new skills are **invoke-only** (`disable-model-invocation: true`), same as `start-implement`.

### Empty / error

- Missing story file: one-line empty in Story.
- `QUEUE_EMPTY` / empty Working: Next says so; keys that need a row no-op with a message.
- Inbox land fails (dirty other paths, script error): keep overlay open, show the error, do not discard ticks.
- Agent launch fail: Chat status failed; Queue/Working unchanged.

## Layout

Terminal, not WinUI layout-grammar.

```
┌─ Next: [action]  [key]  [kebab / Seq] ─────────────────────────┐
│ Queue (tree)     │ Working (pipeline)                          │
│                  │                                             │
│ Story (markdown) │ Chat (header + log + prompt)                │
└────────────────────────────────────────────────────────────────┘
```

Narrow terminal: **stack** Next → focused list → Story or Chat. Tab still cycles.

Inbox: overlay with ~10–15% margin; left list, right detail.

## Workflow

1. Launch `scripts/Start-AgentBoard.ps1` from the repo root (replaces `Start-AgentTui.ps1` when this ships).
2. Default focus Working if any pipeline rows, else Queue. Next names the action.
3. Tab to Queue/Working; arrows select; Next and Story follow; Chat attaches.
4. Review: open Inbox overlay; tick/answer; auto-status; close to land on `main`.
5. `ready-to-resume` → `/resume-implementation`. `ready-to-complete` → `/complete-review` → PR link → Continue or send back.
6. Plan: `p` (Planning agent). Land specs: `m`.
7. Implement: `i` / `n` (story agent).
8. **q** detach.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Queue data | `scripts/Get-FeatureQueue.ps1` | Parse tree; do not invert; hide done unless toggled |
| Pickup | `scripts/Get-NextReadyFeature.ps1` | **n** |
| Inbox land | `scripts/Update-ToReviewOnMain.ps1` | Inbox overlay dirty → this script only |
| Merge planning | `scripts/Merge-PlanningToMain.ps1` | **m** confirm |
| Inbox parse/patch | logic in old `tools/agent-tui/src/inbox.ts` (port) | Headings `## 11-cars`; statuses below |
| Git snapshot | `git` CLI | dirty paths, branch, unpushed |
| Agents | Cursor CLI `agent` (`resume` with stored session id) | Session store; attach; per-story + Planning. No SDK |
| UI | none of Ink/React | `tools/agent-board/` net9.0 console, Spectre.Console |
| Skills | `update-to-review`, `start-implement`, `plan-feature` | `complete-review`, `resume-implementation` |
| Start script | `scripts/Start-AgentTui.ps1` pattern | `scripts/Start-AgentBoard.ps1`; keep old script until cutover |

**Inbox work states** (heading **Status** first token). Feature file Status remains `draft | ready-for-agent | done`.

| Status | Meaning |
| :--- | :--- |
| `in-progress` | Implement agent working |
| `blocked` | Open questions; reason in file |
| `ready-for-review` | Handover; human reviews |
| `ready-to-resume` | Questions answered; coder continues |
| `ready-to-complete` | Verify + deviations ticked; waiting squash |
| `done` | After Continue close-out |

Replace old inbox `resume` with **`ready-to-resume`**. Do not use `done` until complete-review Continue (or the heading was already done and we are only writing the last note).

- **Wiring:** no DI container. Process at repo root. `WORKCOSTS_ROOT` if needed. Spectre.Console app, git and pwsh as subprocesses (same as today’s TUI calling the scripts).
- **Data:** no SQLite. Session file gitignored. Inbox still only on `main`.
- **Ports:** N/A (developer tool). Must run on Windows and Linux agents (net9.0).
- **Cutover:** ship board; then delete or stub `tools/agent-tui`. Do not leave two “start TUI” docs.

## Tests

Not `WorkCosts.Tests` product cases. `tools/agent-board` unit tests (or sibling test project):

- `QueueMembership_ReadyForAgentAndReview_HidesDoneUnlessToggled`
- `WorkingSort_BlockedReviewResumeAgentDraftComplete`
- `NextFollowsFocusedPaneSelection`
- `InboxParse_SeqPrefixedHeading_11Cars`
- `InboxAutoStatus_OpenQuestion_Blocked`
- `InboxAutoStatus_AnswersWithoutVerify_ReadyToResume`
- `InboxAutoStatus_VerifyAndDeviations_ReadyToComplete`
- `InboxClose_Clean_NoGit` / `InboxClose_Dirty_LandsOnlyToReviewOnMain`
- `Attach_DoesNotStartSecondImplementAgentForKebab`
- `PlanFeature_UsesSharedPlanningAgent`
- `Quit_Detaches_DoesNotWaitForIdle`

No live Cursor network in CI. Fake session ids.

## Open questions

(none)

## Accepted defaults

- Tooling spec at `docs/agent-ops/agent-board.md`, not Seq.
- net9.0 + Spectre.Console in `tools/agent-board/`. Not Ink/React.
- Arrows + Tab. No vim keys in v1.
- Next follows focused selection. Default focus Working if non-empty.
- Queue tree, not inverted; done hidden.
- Working sort: blocked → review → resume → agent working → draft → complete.
- Shared Planning agent; one implement agent per story; attach.
- **Cursor CLI** (`agent`) only. No `@cursor/sdk`, no cloud-agent URLs in v1.
- Quit detaches; CLI sessions outlive the board; confirm if a wrapper child would die.
- Drop **`L`**. Skills **`complete-review`** and **`resume-implementation`** (invoke-only).
- complete-review Continue = fetch, checkout `main`, pull, delete feature branch, last note. Never squash-merge from the app.
- Story read-only v1; empty state if missing file.
- Inbox overlay margin; auto-status as above.

## Implementation notes for an agent

1. Do not add `docs/features/*` for this. Do not change pickup so this becomes next Seq.
2. Port inbox parse/patch with Seq-prefixed headings. Extend `Get-InboxStatuses` / TUI leftovers if they still assume `resume` and `## kebab` without digits — board + `Get-FeatureQueue.ps1` must understand **`ready-to-resume`** / **`ready-to-complete`**.
3. `tools/agent-board/` console + tests. `scripts/Start-AgentBoard.ps1`. Skills `complete-review` and `resume-implementation`.
4. Session file gitignored. Handbook: point “Seq board” at this app; retire `Start-AgentTui.ps1` when the board is the default.
5. Do not: WinUI; squash-merge; wait-on-quit; `@cursor/sdk`; second implement agent per kebab; Story editor in v1; `git add` to-review off `main`.
