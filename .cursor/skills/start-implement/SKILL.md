---
name: start-implement
description: Kickoff prompt for a coder agent. Invoke to start implementing a named ready-for-agent feature, or the next queued story if none is named.
disable-model-invocation: true
---

# Start implement

This is the kickoff. Then follow skill `implement-feature` (and `update-to-review` for the inbox). Do not invent product behaviour.

## Which feature

- If the user named a spec (`paste-html` or `docs/features/paste-html.md`), that is the job.
- If they did not, follow skill `pickup-next-feature`. Stop if it prints `QUEUE_EMPTY`.

The file must exist and **Status** must be `ready-for-agent`. Otherwise stop and send them to `plan-feature`.

## Kickoff

1. `git fetch origin`. Read `docs/features/<kebab>.md`, `AGENTS.md`, `docs/layout-grammar.md`, and the **Related screens** named in the spec. Read `git show origin/main:docs/features/to-review.md`.
2. Branch from `origin/main` as `feature/<feature_code>-<Title>` (`AGENTS.md`). Do not commit on `Planning` or `main`.
3. Follow **Implementation notes for an agent** in the spec, in order. Reuse the types it names. Do not add destinations, sheets, or Chromium/WebView2 in a dialog unless the spec says so.
4. Commit **buildable units** only (`dotnet build WorkCosts.slnx`). Run the tests the spec names:

   ```powershell
   dotnet test WorkCosts.slnx --settings .runsettings
   ```

5. Questions, blocks, deviations, and `in-progress` / `scan` go in `docs/features/to-review.md` on **main** via skill `update-to-review`. Never `git add` that file on the feature branch. Exact land:

   ```powershell
   git fetch origin
   git show origin/main:docs/features/to-review.md > docs/features/to-review.md
   # edit only that file
   powershell -File scripts/Update-ToReviewOnMain.ps1 -Message "to-review: <kebab> <status>"
   ```

6. You may push the feature branch. **Do not open a pull request** until that inbox heading is **Status** `done` (questions and deviations approved). Then set **PR** on the story, add `docs/features/<kebab>-delivery.md`, open a squash PR to `main`, and do not merge it.

One named spec per pass unless the user named more than one.
