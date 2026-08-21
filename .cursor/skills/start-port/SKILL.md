---
name: start-port
description: Kickoff for the GNOME Flatpak port of Will I DIY?. Invoke /start-port gnome to implement the next playbook slice, or a named slice. One slice per pass. Use when rebuilding the app on Linux or continuing src/linux.
disable-model-invocation: true
---

# Start port

Kickoff only. Product behaviour comes from screen docs plus [docs/platforms/gnome-build-order.md](../../../docs/platforms/gnome-build-order.md). Inbox, branch, and PR rules are the same as skill `implement-feature`. Do not invent destinations or Windows-only chrome.

`/start-port` is **invoke-only**. Natural language must not start a port.

## Which platform

- `/start-port gnome` — GNOME Flatpak (Gir.Core). This is the Linux rebuild.
- `/start-port gnome gnome-shell` (or another slice **Id**) — that slice if it is startable.
- `/start-port ipad` — stop. There is no iPad playbook yet.
- No platform word — stop and ask. Do not guess.

Do **not** use `/start-implement` or `pickup-next-feature` for this. Those consume `docs/features/*.md` (Windows increments). The next Windows story may be WinUI-only and cannot be built on Linux.

## Select the slice

From the repository root (`git fetch` first):

```bash
pwsh -File scripts/Get-NextPortSlice.ps1
pwsh -File scripts/Get-NextPortSlice.ps1 -List
pwsh -File scripts/Get-NextPortSlice.ps1 -Slice gnome-scaffold
```

On Windows, `powershell -File` is the same. If `pwsh` is missing, read `git show origin/main:docs/platforms/gnome-build-order.md` and pick the first slice yourself using the rules in that file.

| Script output | What you do |
| :--- | :--- |
| a kebab (`gnome-scaffold`) | That is the job |
| `STARTABLE=true` (with `-Slice`) | That is the job |
| `PLAYBOOK_MISSING` | Stop. Land Planning with `merge-planning` so the playbook is on `origin/main` |
| `PORT_CAUGHT_UP` | Stop. GNOME matches **done** Windows behaviour. Do not start the next Windows Seq story |
| `PORT_WAITING_ON_WINDOWS:<kebab>` | Stop. That Windows story is not `done` on `origin/main` yet |
| `STARTABLE=false` | Print `REASON`. Stop. Do not branch |

If the user named a slice, use `-Slice`. Otherwise take the script’s kebab.

The playbook on **`origin/main`** is canonical (same idea as pickup-next-feature). A Planning-only playbook is not startable until `merge-planning`.

## Kickoff

1. `git fetch origin`. Read the matching **Slice** in the playbook, [AGENTS.md](../../../AGENTS.md), [docs/layout-grammar.md](../../../docs/layout-grammar.md), [docs/platforms/gnome-flatpak.md](../../../docs/platforms/gnome-flatpak.md), [docs/architecture.md](../../../docs/architecture.md), and every **Related screens** file the slice names. Read `git show origin/main:docs/features/to-review.md`.
2. Branch from up-to-date `origin/main` as `feature/<slice-id>-<Branch-title>` (playbook **Branch-title**). Not `Planning`, not `main`.
3. Follow that slice’s **Implementation notes** in order. Reuse `WorkCosts.Core` and `WorkCosts.Parsing`. Native widgets and Adwaita spacing; same workflows and schema as Windows.
4. Commit **buildable units**. On Linux do **not** build `WorkCosts/WorkCosts.csproj` or `WorkCosts.slnx` (WinUI). Tests:

   ```bash
   dotnet test WorkCosts.Tests/WorkCosts.Tests.csproj --settings .runsettings
   ```

   Plus any command the slice names (usually `dotnet build src/linux/WillIDIY.Gnome.slnx`).
5. Questions, blocks, deviations, and `in-progress` / `ready-for-review` go in `docs/features/to-review.md` on **main** via skill `update-to-review`. Heading **Feature** links the playbook slice, not a `docs/features/gnome-*.md` story. Never `git add` the inbox on this branch. Do not create `docs/features/gnome-*.md` (that would pollute the Windows Seq queue).
6. When the slice’s **Done when** is met and the named tests pass: inbox **Status** `ready-for-review`. Tick **Verify**. **Still no PR.**
7. After the human sets that heading **Status** `done`: set this slice **Status** `done` in `docs/platforms/gnome-build-order.md` on the feature branch, add `docs/features/<slice-id>-delivery.md` from [delivery-template.md](../implement-feature/delivery-template.md) (link the playbook instead of a feature file), open a squash PR to `main`, do not merge it.

One slice per pass unless they named more than one startable id.

## Do not

- Implement two slices in one pass, or skip unmet **Depends-on**.
- Add the Gir.Core project to `WorkCosts.slnx`.
- Copy WinUI pixels, host WebKit inside a blocking dialog, or skip garage background / seeded data / theme switch.
- Implement zip export/import or a Windows story that is not `done`.
- Mark a playbook slice **Status** `done` before the inbox heading is **done**.
