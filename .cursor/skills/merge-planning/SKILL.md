---
name: merge-planning
description: Rebase and squash the Planning branch onto main, fast-forward merge locally, then push main and Planning. Use when the user asks to merge Planning into main, land planning on main, or push both after planning work.
---

# Merge Planning into main

Do **not** run `git merge Planning` on `main` (that creates merge commits). Do **not** force-push `main`.

Run the repo script from the repository root (needs a clean working tree):

```powershell
powershell -File scripts/Merge-PlanningToMain.ps1
powershell -File scripts/Merge-PlanningToMain.ps1 -Message "Add paste-HTML feature spec."
```

The script:

1. Fetches `origin`
2. Fast-forwards local `main` to `origin/main`
3. Checks out `Planning` and **rebases** onto `main`
4. **Squashes** if Planning is more than one commit ahead
5. Checks out `main` and **fast-forward** merges Planning
6. Restores `docs/features/to-review.md` from `main` if it already existed (inbox is not owned by Planning)
7. Pushes `main` normally
8. Pushes `Planning` with `--force-with-lease` (rebase/squash rewrote it)

If the working tree is dirty, stop and tell the user. If rebase conflicts, the script aborts the rebase; do not continue with a merge commit.

VS Code / Cursor task label: `merge-planning`.
