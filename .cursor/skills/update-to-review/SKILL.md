---
name: update-to-review
description: Land docs/features/to-review.md on main only. Use when the coder must record questions, deviations, blocked/resume/scan status, or when the user asks to update the to-review inbox.
---

# Update to-review on main

The living inbox is **`docs/features/to-review.md` on `main`**. Scan it there. Do not commit that file on `Planning` or a feature branch.

## Read the inbox

```powershell
git fetch origin
git show origin/main:docs/features/to-review.md
```

Do not treat the working-tree copy (or chat) as the source of truth.

## Before you run the script

1. Finish a **buildable code unit** on the current branch (`dotnet build WorkCosts.slnx` succeeds). Run the tests the feature file names.
2. **Commit that code only.** Never `git add docs/features/to-review.md` on this branch. Do not stash code as a substitute for a commit.
3. Edit `docs/features/to-review.md` in the working tree (uncommitted). Upsert the feature heading from [to-review-entry.md](../implement-feature/to-review-entry.md). Start from `git show origin/main:docs/features/to-review.md` if the file is not already in the worktree.
4. Working tree dirty paths must be **only** `docs/features/to-review.md`.

If other files are dirty, stop. Commit or revert them first.

## Run

```powershell
powershell -File scripts/Update-ToReviewOnMain.ps1
powershell -File scripts/Update-ToReviewOnMain.ps1 -Message "to-review: block paste-html on layout question"
```

The script fast-forwards `main`, commits that file, pushes `main` (never `--force`), and checks out the previous branch. It removes the worktree copy so you cannot accidentally commit it off `main`.

If the file is already committed on this branch and differs from `main`, restore it (`git checkout main -- docs/features/to-review.md`) and keep inbox edits uncommitted until the script runs.

## After humans answer

They tick boxes and write **Answer:** on `main` (or you land those edits with this script). Then skill `implement-feature` **resume**: fetch, read `origin/main:docs/features/to-review.md`, fold answers into the feature spec, continue coding.

VS Code / Cursor task label: `update-to-review`.
