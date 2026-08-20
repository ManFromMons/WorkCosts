---
name: implement-feature
description: Implement a Will I DIY? feature from a ready-for-agent spec under docs/features/. Use when the user asks to implement, build, code, or resume a planned feature, or to continue after answers in docs/features/to-review.md on main.
---

# Implement a feature

Coding consumes a **feature file**. It does not invent product behaviour from chat.

Source of truth: `docs/features/<kebab-case-name>.md`  
Inbox (questions, deviations, status): `docs/features/to-review.md` **on `main`**  
Land inbox edits with skill `update-to-review` (never commit that file on this branch).  
Entry shape: [to-review-entry.md](to-review-entry.md)

Do not implement until that file exists and **Status** is `ready-for-agent`. Otherwise stop and send the user to skill `plan-feature`.

## Gate

1. Identify the feature id from the user (`paste-html` or `docs/features/paste-html.md`).
2. Read the feature file, `AGENTS.md`, `docs/layout-grammar.md`, and the named related screens/code.
3. **Refuse** if the file is missing, **Status** is `draft`, or **Status** is `done` (a change to a done feature is a new plan, not this skill).
4. Chat-only requirements are not enough. No product behaviour without the spec.

Fetch and read the inbox from main:

```powershell
git fetch origin
git show origin/main:docs/features/to-review.md
```

If that path does not exist on `origin/main` yet, the inbox is empty. If the feature heading **Status** is `blocked` and any **Questions** boxes are still unchecked, do not write code. Wait.

## Code units

Branch from up-to-date `origin/main` as `feature/<feature_code>-<Title>` (see `AGENTS.md`). Do not implement on `Planning` or `main`. Each commit must be **code that builds** (`dotnet build WorkCosts.slnx`). Include the tests the spec names when you claim a unit is done.

Push the feature branch and open a **squash PR to `main`**. Do not merge it; the human reviews and tests on GitHub.

Then, on the **feature branch** (not `main`):

1. Set **PR** in the story header to the GitHub URL.
2. Add `docs/features/<kebab>-delivery.md` from [delivery-template.md](delivery-template.md). Bullets for what landed, tests, deviations. No chain of thought.
3. Commit those two files and push. Do not `git add docs/features/to-review.md`.

- Do not mix inbox edits into code commits.
- Do not `git add docs/features/to-review.md` on this branch.
- Do not stash unfinished code in order to update the inbox. Commit a buildable unit or revert until the tree is clean except the inbox file.
- Then follow skill `update-to-review` to switch to `main`, commit the inbox, push, and return.

## Resume

Resume when the user says resume / continue, or the inbox on `main` has **Status** `resume`.

1. Fetch and read answers from `origin/main:docs/features/to-review.md` (checked box + **Answer:**).
2. Fold them into the feature file the same way plan-feature does after answers: remove resolved questions; put mechanical choices under **Accepted defaults**; rewrite the conflicting spec section if the answer changes UX or architecture.
3. Do not re-ask resolved items.
4. Set inbox **Status** to `in-progress` via `update-to-review`, then continue implementation.

## Workflow

1. Start from the inbox on `main` (`git show origin/main:docs/features/to-review.md`). Write the updated file in the worktree only. Upsert a heading from [to-review-entry.md](to-review-entry.md). **Status:** `in-progress`. Land it with `update-to-review`. Chat is not the inbox.
2. Follow **Implementation notes for an agent** in order, and the Technical design reuse table. Do not add destinations, sheets, schema, or controls the spec did not ask for.
3. **UX / layout-grammar / architecture conflict:** do not invent. Commit any buildable code already done. Add numbered *Assumption:* … → **Question:** …? boxes under **Questions**, set **Status** `blocked`, land with `update-to-review`, stop.
4. **Reuse deviation:** if the spec said create a type but the codebase already has the behaviour, use the existing type and keep going. Add an unchecked box under **Deviations to scan**, and one line under the feature file **Implementation notes**. Land the inbox on `main`. Stop only for UX / architecture conflicts.
5. Run the tests named in the spec:

   ```powershell
   dotnet test WorkCosts.slnx --settings .runsettings
   ```

   Skip the solution test run only if the spec says UI-only and names no test cases.
6. If tests pass and no open questions: set inbox **Status** to `scan`. Tick **Verify** → tests passed. Leave deviation boxes for the human. Land with `update-to-review`. Feature file **Status** becomes `done` only after the human accepts the scan — propose it, do not silently close the review.

One named spec per pass, unless the user named more than one ready-for-agent file.

## Feature file Status

`draft | ready-for-agent | done` only. `blocked` / `in-progress` / `resume` / `scan` belong in `docs/features/to-review.md` on `main`, not on the spec.

When folding answers, do not invent extra spec sections. Update **Implementation notes** (and **Accepted defaults** / the conflicting section) only. After the PR exists, also set **PR** in the header and keep `docs/features/<kebab>-delivery.md` in sync (still no diary).

## Do not

- Implement during planning, or plan during implementation (no new feature file from this skill).
- Host questions or status only in chat, or commit the inbox off `main`.
- Merge Planning to main, squash-merge the feature PR, pack MSIX, or drive-by refactor unrelated files.
- Force-push `main`, or mark the feature **Status** `done` without a human scan.
- Duplicate existing helpers; do not invent a DI container unless the spec requires it.
