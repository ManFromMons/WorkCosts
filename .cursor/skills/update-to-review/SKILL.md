---
name: update-to-review
description: Land docs/features/to-review.md on main only. Use when the coder must record questions, deviations, blocked/resume/scan status, or when the user asks to update the to-review inbox.
---

# Update to-review on main

The living inbox is **`docs/features/to-review.md` on `main`**. Scan it there. Do not commit that file on `Planning` or a feature branch.

Do not open a feature PR until this inbox shows the human has accepted the scan (**Status** `done`, questions resolved, deviations ticked).

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
   powershell -File scripts/Update-ToReviewOnMain.ps1 -Message "to-review: paste-html scan"
   ```

6. The script: fetches, fast-forwards local `main` to `origin/main`, commits **only** `docs/features/to-review.md`, pushes `main` (never `--force`), checks out the branch you were on, and drops the worktree copy so you cannot commit it off `main`.
7. Confirm with `git show origin/main:docs/features/to-review.md`.

If the file is already committed on this branch and differs from `main`, restore it (`git checkout main -- docs/features/to-review.md`) and keep inbox edits uncommitted until the script runs.

## After humans answer

They tick boxes and write **Answer:** (then you land those ticks with this script if they edited a worktree copy, or they land them the same way). Skill `implement-feature` **resume**: fetch, read `origin/main:docs/features/to-review.md`, fold answers into the feature spec, continue coding. Still no PR until **Status** is `done`.

VS Code / Cursor task label: `update-to-review`.
