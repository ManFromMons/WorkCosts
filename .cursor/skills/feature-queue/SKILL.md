---
name: feature-queue
description: Lists Will I DIY? feature stories as a Seq dependency tree with status, and starts implementation by sequence number. Use when the user asks for the work queue, feature tree, Seq list, what's ready, or to start/implement Seq N / story number N.
---

# Feature queue

Two modes. Run the script first; do not rebuild the tree by hand.

```powershell
powershell -File scripts/Get-FeatureQueue.ps1
powershell -File scripts/Get-FeatureQueue.ps1 -Seq 5
```

`-FromMain` lists `origin/main` only (same population as `Get-NextReadyFeature.ps1`).

## List (no number)

When the user asks to list work, the queue, Seq, or the dependency tree:

1. Run `Get-FeatureQueue.ps1` (no `-Seq`).
2. Show that tree. Do not implement.
3. If they ask what they can start, point at **Startable Seq** / **Next pickup**. Ready stories that say **Planning only** need `merge-planning` before coding.

## Start by Seq

When the user names a sequence number (`5`, `Seq 2`, `/feature-queue 5`, `/start-implement 5`):

1. Run `Get-FeatureQueue.ps1 -Seq N`.
2. If `FOUND=false` or exit `1`, show the list tree and stop.
3. If `STARTABLE=false` (exit `2`), print `REASON` and the tree. Stop. Do not branch or code. If the reason is Planning-only, offer `merge-planning`.
4. If `STARTABLE=true`, that `KEBAB` is the job:
   - `START_SKILL=start-add-source` → follow skill `start-add-source` on that id (ready `source-*` story).
   - otherwise → follow skill `start-implement` on that kebab (loads `implement-feature`).
5. One Seq per pass unless they named more than one startable number.

Do not start a `draft` or `done` story. Do not skip unmet **Depends-on**. Do not implement on `Planning`. GNOME slices are not Seq numbers; use `/start-port gnome`.

## Tree meaning

- Roots have **Depends-on** `none` (or missing deps). Children hang off the lowest-Seq dependency.
- Feature **Status** is `draft | ready-for-agent | done`. Inbox states (`in-progress`, `blocked`, `ready-for-review`) come from `origin/main:docs/features/to-review.md` and show as `[inbox …]`.
- **startable** = `ready-for-agent` on `origin/main`, every dependency **done** on main.
- Assigning a new Seq: one higher than the current max on **both** `origin/main` and this branch (`plan-feature`).
