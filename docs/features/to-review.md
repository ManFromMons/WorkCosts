# Feature implementation review

Canonical copy lives on **main**. Scan `git show origin/main:docs/features/to-review.md`.

Do not commit this file on `Planning` or a feature branch. Coder: skill `update-to-review` / `scripts/Update-ToReviewOnMain.ps1`.

Unchecked items need a human.

- **Questions:** write **Answer:** on the line, tick the box, set **Status** to `resume`, then tell the coder to continue.
- **Deviations to scan:** tick when you accept the reuse (or say to follow the spec instead).
- **Verify:** tick when tests and deviations are accepted. Then the feature file **Status** may become `done`.

Coder: when development is finished, set this heading **Status** to `ready-for-review` (not `done`).

Copy a new heading from `.cursor/skills/implement-feature/to-review-entry.md`.

Feature file **Status** stays `draft | ready-for-agent | done`. Work states (`in-progress`, `blocked`, `resume`, `ready-for-review`) live only here.

## Entries

## source-onlinecarparts

- **Feature:** [docs/features/source-onlinecarparts.md](source-onlinecarparts.md)
- **Status:** done
- **Last note:** Scan accepted. Feature file Status is `done`. Opening squash PR.

### Questions

_(none)_

### Deviations to scan

- [x] Vendor is the host label `"Online Car Parts"` (JSON-LD seller is the shop URL).
- [x] Sample 1 live `.product__new-price` on 2026-08-21 was **�49.96**; fixtures lock the confirmed **�50.24**.
- [x] Added `ProductPageMetadata.ExtraUnknown` / client merge into `ProductExtra.UnknownKeys` (no editor boxes).

### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted

## source-tayna

- **Feature:** [docs/features/source-tayna.md](source-tayna.md)
- **Status:** done
- **Last note:** Scan accepted. Feature file Status is `done`. Opening squash PR.

### Questions

_(none)_

### Deviations to scan

- [x] Vendor is the host label `"Tayna"` (first-party shop; no sold-by node).

### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted

## source-carbatterymarket

- **Feature:** [docs/features/source-carbatterymarket.md](source-carbatterymarket.md)
- **Status:** done
- **Last note:** Scan accepted. Feature file Status is `done`. Opening squash PR.

### Questions

_(none)_

### Deviations to scan

- [x] Sample 2 fixture uses the confirmed unit price **£98.50**; live HttpClient HTML on 2026-08-21 showed **£103.30** (RRP £109.09). Tests no longer lock a GBP amount.
- [x] Vendor is the host label `"Car Battery Market"` (first-party shop; no sold-by node).

### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted

## source-eurocarparts

- **Feature:** [docs/features/source-eurocarparts.md](source-eurocarparts.md)
- **Status:** done
- **Last note:** Scan accepted. Feature file Status is `done`. PR https://github.com/ManFromMons/WorkCosts/pull/3 remains open (not squash-merged).

### Questions

_(none)_

### Deviations to scan

- [x] Manufacturer is the first token of `brandImage` alt so “Eicher Premium” matches confirmed **Eicher**.
- [x] Vendor is the host label `"Euro Car Parts"` (first-party shop; no sold-by node).

### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted

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

## product-extra-data

- **Feature:** [docs/features/product-extra-data.md](product-extra-data.md)
- **Status:** done
- **Last note:** Scan accepted. Opening squash PR for `feature/product-extra-data-Product-extra-YAML`.

### Questions

_(none)_

### Deviations to scan

- [x] Also added `ExtraYaml` in `DatabaseService.RepairProductSchema` (same pattern as `PricePoint`) so existing unpackaged databases get the column if migration history is incomplete.
- [x] Added `InputToolTip.Bind(ComboBox, …)` so Technology matches the other extra-spec tooltips.

### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted
