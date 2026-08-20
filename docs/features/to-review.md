# Feature implementation review

Canonical copy lives on **main**. Scan `git show origin/main:docs/features/to-review.md`.

Do not commit this file on `Planning` or a feature branch. Coder: skill `update-to-review` / `scripts/Update-ToReviewOnMain.ps1`.

Unchecked items need a human.

- **Questions:** write **Answer:** on the line, tick the box, set **Status** to `resume`, then tell the coder to continue.
- **Deviations to scan:** tick when you accept the reuse (or say to follow the spec instead).
- **Verify:** tick when tests and deviations are accepted. Then the feature file **Status** may become `done`.

Copy a new heading from `.cursor/skills/implement-feature/to-review-entry.md`.

Feature file **Status** stays `draft | ready-for-agent | done`. Work states (`in-progress`, `blocked`, `resume`, `scan`) live only here.

## Entries

## paste-html

- **Feature:** [docs/features/paste-html.md](paste-html.md)
- **Status:** done
- **Last note:** Scan accepted. Feature file Status is `done`. PR https://github.com/ManFromMons/WorkCosts/pull/1 remains open (not squash-merged).

### Questions

_(none)_

### Deviations to scan

- [x] Added `DatabaseService(string databasePath)` so `LoadFromHtmlAsync` tests can cache HTML without writing the user `workcosts.db`.

### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted

