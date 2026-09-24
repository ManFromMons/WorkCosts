---
name: resume-implementation
description: Attach the existing implement agent for a ready-to-resume story and recommence from review. Never start a second implement session for that kebab.
disable-model-invocation: true
---

# Resume implementation

Use when the inbox heading is **Status** `ready-to-resume` (questions answered; coder must continue, including rejected deviations).

## Gate

1. `git fetch origin`. Read `git show origin/main:docs/features/to-review.md` and fold **Answer:** / reject reasons.
2. Attach the **existing** implement CLI session for this kebab (`agent -p --force --resume <id> --workspace <repoRoot>`). Do **not** start a second implement agent for the same kebab.
3. Follow skill `implement-feature` **Recommence from review**.

If a deviation was **Reject** with a reason, follow the spec instead of the deviation.

Set inbox **Status** `in-progress` via `update-to-review` while coding. Do not squash-merge.
