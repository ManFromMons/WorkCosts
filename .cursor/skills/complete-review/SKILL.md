---
name: complete-review
description: Close out a ready-to-complete story after the human squash-merges on GitHub, or send the heading back to ready-for-review. Does not squash-merge.
disable-model-invocation: true
---

# Complete review

Use when the inbox heading is **Status** `ready-to-complete` (questions answered, deviations accepted, Verify ticked). Do **not** squash-merge the GitHub PR.

## Gate

1. `git fetch origin`. Read `git show origin/main:docs/features/to-review.md`.
2. The selected kebab’s heading **Status** first token must be `ready-to-complete`. Otherwise stop.
3. If the heading **Change set** / **PR** field already contains an `https://` pull-request URL, show **that** URL. Do not call `gh` / `origin pr list` to invent another. If there is no URL, say **no PR yet**.

Tell the human: review the PR and **squash-merge on GitHub**. The board / this skill never squash-merges.

Then **wait** for Continue or Send back.

## Continue (squash already on main)

1. Land inbox **Status** `done` on `main` via skill `update-to-review` if it is not already `done`.
2. `git fetch origin`, `git checkout main`, `git pull` (fast-forward).
3. Squash SHA is **`origin/main` tip after pull**. Do not scrape GitHub.
4. Delete the feature branch locally and on the remote (`git branch -d`, `git push origin --delete`) when it still exists.
5. Last note on the inbox heading: squash SHA (and PR number if the inbox URL already had one).
6. On the leftover feature checkout if needed: set the feature file **Status** `done`, **PR** field, add `docs/features/<kebab>-delivery.md`. Commit those on the branch only if it still exists; after squash they should already be on `main` or you add delivery on a follow-up commit **only if the spec for that story requires it and the branch still exists**. For agent-ops (`docs/agent-ops/agent-board.md`) do not invent a `docs/features/` file.

## Send back

Set the heading **Status** to `ready-for-review`, land on `main` via `update-to-review`. Do not delete the branch. Do not squash-merge.
