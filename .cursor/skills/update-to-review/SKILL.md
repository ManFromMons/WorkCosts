---
name: update-to-review
description: Land docs/features/to-review.md on main only. Use when the coder must record questions, deviations, blocked/resume/ready-for-review status, or when the user asks to update the to-review inbox.
---

# Update to-review on main

The living inbox is **`docs/features/to-review.md` on `main`**. Scan it there. Do not commit that file on `Planning` or a feature branch.

This file is the **handover package** the human reviews on `main`: **Work summary**, **Questions**, **Deviations**, **Verify**, and a **Change set** pointer (branch + PR). A one-line **Last note** is not a handover.

When development is finished, set **Status** to `ready-for-review` (not `done`), land this file on `main`, then **open the squash PR** so the human can review the code next to the inbox. Do **not** squash-merge. After they answer on `main`, **recommence from review**.

## Read the inbox

```powershell
git fetch origin
git show origin/main:docs/features/to-review.md
```

Do not treat the working-tree copy (or chat) as the source of truth.

## Land an update (coder)

1. On the **feature branch**, commit all product code. `dotnet build WorkCosts.slnx` must succeed. Do not stash broken code.
2. Fetch and copy main’s file into the worktree (overwrite any local copy):

   ```powershell
   git fetch origin
   git show origin/main:docs/features/to-review.md > docs/features/to-review.md
   ```

   If the path does not exist on `origin/main` yet, create `docs/features/to-review.md` with the how-to already used on main and an `## Entries` section.
3. Edit **only** that file. Upsert the feature heading from [to-review-entry.md](../implement-feature/to-review-entry.md).
4. Confirm nothing else is dirty:

   ```powershell
   git status --porcelain
   ```

   Allowed: `docs/features/to-review.md` only (or untracked `docs/features/` if the file is new). If other paths appear, commit or revert them first.
5. Run the script from the repo root:

   ```powershell
   powershell -File scripts/Update-ToReviewOnMain.ps1
   powershell -File scripts/Update-ToReviewOnMain.ps1 -Message "to-review: paste-html ready-for-review"
   ```

6. The script: fetches, fast-forwards local `main` to `origin/main`, commits **only** `docs/features/to-review.md`, pushes `main` (never `--force`), checks out the branch you were on, and drops the worktree copy so you cannot commit it off `main`.
7. Confirm with `git show origin/main:docs/features/to-review.md`.

If the file is already committed on this branch and differs from `main`, restore it (`git checkout main -- docs/features/to-review.md`) and keep inbox edits uncommitted until the script runs.

## Ready-for-review handover

The heading on `main` must include:

- **Status** `ready-for-review`
- **Change set:** branch name (and PR url once opened)
- **Work summary** (bullets; required)
- **Questions** (or `_(none)_`)
- **Deviations to scan** (every real deviation, unchecked for the human; `_(none)_` only if none)
- **Verify:** tests ticked; deviations left for the human

Then open the squash PR against `main` (same summary / questions / deviations in the PR body). **Stop.** Do not set **Status** `done`. Do not squash-merge.

## After humans answer (recommence from review)

They tick boxes and write **Answer:** on `main` (land those ticks with this script if they edited a worktree copy). Then skill `implement-feature` **recommence from review**: fetch, read `origin/main:docs/features/to-review.md`, fold answers into the feature spec, continue coding or close out.

- **Status** `resume` (open questions answered, more code needed): set inbox `in-progress`, keep coding, update the existing PR.
- **Status** `done` (verify + deviations ticked, no open questions): feature file **Status** `done`, `*-delivery.md`, mark the PR ready. Still do **not** squash-merge unless the user explicitly asks.

VS Code / Cursor task label: `update-to-review`.
