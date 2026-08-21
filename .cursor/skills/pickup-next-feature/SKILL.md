---
name: pickup-next-feature
description: Pick the next ready-for-agent feature by Seq whose dependencies are done, then implement it. Use when the user asks to pick up the next job, next ready story, next available feature, or work the queue.
---

# Pick up the next ready feature

Do not invent a feature. Do not skip Seq order. Do not implement `draft` specs.

## Select

From the repository root (needs `git fetch`):

```powershell
powershell -File scripts/Get-NextReadyFeature.ps1
```

The script reads **`origin/main`** (landed stories), not `Planning`.

Eligible:

- `docs/features/<kebab>.md` except `to-review.md` and `*-delivery.md`
- **Status** is `ready-for-agent`
- Every **Depends-on** kebab has **Status** `done` (`none` means no deps)
- Lowest **Seq** wins (then kebab name)

If the script prints `QUEUE_EMPTY`, stop. There is nothing to code.

Do not use this skill for the GNOME port (`/start-port gnome`).

If it prints a kebab id (for example `paste-html`), that is the job.

To print the full Seq tree instead, follow skill `feature-queue` (`scripts/Get-FeatureQueue.ps1`). To start a specific number, use that skill with `-Seq N`.

## Implement

Follow skill `start-implement` for that id (it loads `implement-feature`). Branch `feature/<feature_code>-<Title>` from `origin/main` as in `AGENTS.md`. When development is finished, that skill sets the inbox heading **Status** to `ready-for-review`.

One job per pass unless the user named more than one.
