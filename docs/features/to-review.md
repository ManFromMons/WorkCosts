# Feature implementation review

Canonical copy lives on **main**. Scan `git show origin/main:docs/features/to-review.md`.

Do not commit this file on `Planning` or a feature branch. Coder: skill `update-to-review` / `scripts/Update-ToReviewOnMain.ps1`.

Unchecked items need a human.

- **Work summary:** required at `ready-for-review`. What landed, in bullets. **Last note** is not enough.
- **Questions:** write **Answer:** on the line, tick the box, set **Status** to `resume`, then tell the coder to continue.
- **Deviations to scan:** tick when you accept the reuse (or say to follow the spec instead). List every real deviation; `_(none)_` only if there are none.
- **Verify:** tick when tests and deviations are accepted. Then the feature file **Status** may become `done`.
- **Change set:** at `ready-for-review` the coder lands this heading on `main` and opens a squash PR against `main` so you can review the diff while answering. Commit your answers on `main`. The agent then **recommences from review**. Do not squash-merge the PR until this heading is **Status** `done`.

Coder: when development is finished, set this heading **Status** to `ready-for-review` (not `done`). Fill **Work summary**, **Questions**, and **Deviations**, land this file on `main`, open the PR, then stop.

Copy a new heading from `.cursor/skills/implement-feature/to-review-entry.md`. New stories use `## <seq>-<kebab>` (example `## 11-cars`) and a **Seq** field that matches `docs/features/<seq>-<kebab>.md`.

Feature file **Status** stays `draft | ready-for-agent | done`. Work states (`in-progress`, `blocked`, `resume`, `ready-for-review`) live only here.

## Entries



## 11-cars

- **Feature:** [docs/features/11-cars.md](11-cars.md)
- **Seq:** 11
- **Status:** done
- **Change set:** branch `feature/11-cars-Cars` — [https://github.com/ManFromMons/WorkCosts/pull/13](https://github.com/ManFromMons/WorkCosts/pull/13)
- **Last note:** Feature file Status is `done`. PR #13 is ready.



### Work summary

- Stuff → Cars master/detail, trailing Add. Narrow width stacks the list, then the detail, with Back to the list. Detail fields are three per row.
- Add Car is a sheet. Nickname, make, model, model number, engine, VRM, model year, VIN, and a photo are required. Save stays off until they are set. A duplicate active registration shows an error and does not write.
- Image search uses `{Make} {ModelNumber}` on Bing Images, then Google Images when Bing has no usable files. HttpClient first; Chromium only if the page is challenged, and not inside a dialog. One photo applies immediately; several open the existing chooser. A local PNG, JPEG, or WebP up to 512 KB is allowed as well.
- Cars are SQLite rows. Photos are files under `images/cars/`. `VehicleOrderJson` starts empty. Soft-delete sets `DeletedAt` and `UpdatedAt`, keeps the row, the photo, and foreign keys, and drops the car from the list.
- Nullable Restrict `CarId` on garage jobs, work jobs, and items of work. An unknown car id does not write. Soft-deleting a car does not clear those links.



### Questions

*(none)*

### Deviations to scan

- [x] Normalized registration is stored as `VrmKey` (upper case, spaces removed) with a unique filtered index where `DeletedAt` is null.
- [x] `GarageJobCommands.UpdateAsync` leaves `CarId` unchanged unless `setCarId` is true, so existing updates do not clear the FK. An unknown car with `setCarId` returns false and writes nothing. Work jobs use new `WorkJobCommands.TrySetCarIdAsync` (there was no work-job command type). Items of work take an optional `CarId` on create, plus `TrySetCarIdAsync` for later updates.
- [x] The image chooser reuses `ProductImagePicker.ChooseFromCandidatesAsync` (a ContentDialog). An optional title lets cars say “Select a photo”. Add Car stays a sheet. Chromium runs before that dialog.
- [x] `Microsoft.EntityFrameworkCore.Design` IncludeAssets now includes runtime so `dotnet ef` can see the package. PrivateAssets stays `all`.



### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted



## 12-car-details

- **Feature:** [docs/features/12-car-details.md](12-car-details.md)
- **Seq:** 12
- **Status:** ready-for-review
- **Change set:** branch `feature/12-car-details-Car-types` — [https://github.com/ManFromMons/WorkCosts/pull/14](https://github.com/ManFromMons/WorkCosts/pull/14)
- **Last note:** Ready for review. Squash PR [https://github.com/ManFromMons/WorkCosts/pull/14](https://github.com/ManFromMons/WorkCosts/pull/14).



### Work summary

- Stuff → Car types master/detail, trailing Add. Narrow width stacks the list, then the detail, with Back to the list.
- Add type is a sheet. Make, model, model number, year, and engine are required. Save inserts and selects. Duplicate make + model-number + year + engine does not write.
- Delete is hard delete and Restrict if a car, job junction, garage job, or completion points at the type. No soft-delete. Unsaved changes use the same helper as Cars.
- Optional car-type combo on the car editor. Nickname and scalars stay. Unknown type id does not write.
- `GarageJob` and `ItemOfWork` snapshot `CarDetailsId` from the car at write / completion. Later car-type edits do not follow unless the garage job is saved again.
- Job fitment is Core only: `ReplaceJobCarDetailsAsync` dedupes and orders. No Jobs-page chips in this Seq.
- Migration `20260921220928_AddCarDetails`. `DbInitializer` still seeds no types.



### Questions

*(none)*

### Deviations to scan

- [x] Unique type is stored as `TypeKey` (uppercase Make|ModelNumber|Year|EngineType) with a unique index, same idea as `Car.VrmKey`.



### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted



## 13-car-vin-lookup

- **Feature:** [docs/features/13-car-vin-lookup.md](13-car-vin-lookup.md)
- **Seq:** 13
- **Status:** ready-for-agent
- **Change set:** none (spec only; not started)
- **Last note:** Landed on main via merge-planning. Waits on **11-cars**. Filename is `13-car-vin-lookup.md`.



### Work summary

- Spec only. mdecoder VIN lookup, 30s poll, 2 min cap, BMW gate. Not implemented.



### Questions

*(none)*

### Deviations to scan

*(none)*

### Verify

- [ ] Tests from the feature file passed
- [ ] Deviations accepted



## 14-car-fastcarcheck

- **Feature:** [docs/features/14-car-fastcarcheck.md](14-car-fastcarcheck.md)
- **Seq:** 14
- **Status:** draft
- **Change set:** none
- **Last note:** Landed on main via merge-planning. **Draft — resume later.** Filename is `14-car-fastcarcheck.md`.



### Work summary

- Spec intent only. UK FastCarCheck type lookup. Do not implement.



### Questions

*(parked until resume)*

### Deviations to scan

*(none)*

### Verify

- [ ] Tests from the feature file passed
- [ ] Deviations accepted



## 15-workjob-job-subset

- **Feature:** [docs/features/15-workjob-job-subset.md](15-workjob-job-subset.md)
- **Seq:** 15
- **Status:** ready-for-agent
- **Change set:** none (spec only; not started)
- **Last note:** Landed on main via merge-planning. Depends-on **none**. Filename is `15-workjob-job-subset.md`.



### Work summary

- Spec only. Core definition CRUD + copy Job work-job subset to instances. No new UI. Not implemented.



### Questions

*(none)*

### Deviations to scan

*(none)*

### Verify

- [ ] Tests from the feature file passed
- [ ] Deviations accepted



## 16-car-details-seed

- **Feature:** [docs/features/16-car-details-seed.md](16-car-details-seed.md)
- **Seq:** 16
- **Status:** ready-for-agent
- **Change set:** none (spec only; not started)
- **Last note:** Landed on main via merge-planning. Empty JSON loader first. Waits on **12-car-details**. Filename is `16-car-details-seed.md`.



### Work summary

- Spec only. `car-details.json` = `[]` plus DbInitializer hook. Not implemented.



### Questions

*(none)*

### Deviations to scan

*(none)*

### Verify

- [ ] Tests from the feature file passed
- [ ] Deviations accepted



## 17-item-of-work-ui

- **Feature:** [docs/features/17-item-of-work-ui.md](17-item-of-work-ui.md)
- **Seq:** 17
- **Status:** draft
- **Change set:** none
- **Last note:** Landed on main via merge-planning. **Draft — refine later.** Filename is `17-item-of-work-ui.md`.



### Work summary

- Spec intent only. GarageJob collates copied work items; ItemOfWork is the completion event. Do not implement.



### Questions

*(parked until resume)*

### Deviations to scan

*(none)*

### Verify

- [ ] Tests from the feature file passed
- [ ] Deviations accepted



## garage-job-interval-logic

- **Feature:** [docs/features/garage-job-interval-logic.md](garage-job-interval-logic.md)
- **Status:** done
- **Change set:** branch `cursor/garage-job-interval-logic-f3f1` — [https://github.com/ManFromMons/WorkCosts/pull/11](https://github.com/ManFromMons/WorkCosts/pull/11)
- **Last note:** Squash-merged to `main` as `016a199` (#11). Feature file Status is `done`.



### Work summary

- Persist `ItemOfWork` completions (`OccurredAt`, optional odometer miles ≥ 0) with Restrict FK. `ItemOfWorkCommands` create / list / latest / delete. `GarageJobCommands.TryDeleteAsync` returns `HasCompletions` and keeps the parent.
- Persist `GarageJob.IntervalAnchorDate` (`DateOnly?`) via `UpdateAsync`. Empty anchor + no completions → `DueImmediately`.
- `GarageJobLondonTime` (`Europe/London`, else Windows `GMT Standard Time`) and `GarageJobDueEvaluator`: `NotScheduled` / `DueImmediately` / `NeverDone` / `NotDue` / `Due` / `Overdue`. Multiple same-kind rows stay in force (6 mo vs 12 mo). Miles canonical (`1.609344` km/mile). Due vs Overdue uses the combined next threshold (min/max), so AllMustBeMet 6+12 months is **Due** at 12 months.
- `GarageJobRollupCalculator`: each `ProductJob` quantity 1, merge required products, garage £ + DIY £ to 2 dp `AwayFromZero`, referenced job duration (parent duration echoed, not added). `IsAllJobs` excluded unless linked.
- Migration `20260920193900_AddItemOfWorkAndIntervalAnchor`. `docs/data/garage-job.md` and `docs/data/schema.md` updated. No WinUI.



### Questions

*(none)*

### Deviations to scan

- [x] `ItemOfWork` latest/list: load then sort in memory by `OccurredAt.UtcDateTime` then `Id`. SQLite cannot translate that `OrderByDescending`. Newest-first contract unchanged.
- [x] Branch `cursor/garage-job-interval-logic-f3f1` (cloud-agent prefix) instead of `feature/garage-job-interval-logic-…`.
- [x] EF migration authored by hand because `dotnet ef` was not available on the Linux agent.
- [x] Review PR opened at inbox `ready-for-review` ([https://github.com/ManFromMons/WorkCosts/pull/11](https://github.com/ManFromMons/WorkCosts/pull/11)) rather than waiting for **Status** `done`.



### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted



## garage-job

- **Feature:** [docs/features/garage-job.md](garage-job.md)
- **Status:** done
- **Last note:** Scan accepted. Feature file Status is `done`. Opening squash PR.



### Questions

*(none)*

### Deviations to scan

*(none)*

### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted



## source-demon-tweeks

- **Feature:** [docs/features/source-demon-tweeks.md](source-demon-tweeks.md)
- **Status:** done
- **Last note:** Scan accepted. Feature file Status is `done`. Opening squash PR.



### Questions

*(none)*

### Deviations to scan

- [x] Add `demon-tweeks.com` to the Chromium fetch gate (`RequiresChromiumFetch` = Autodoc **or** Demon Tweeks) in `ProductImagePicker.FetchPageAsync` / `ProductImageService.LoadPageAsync` / `ChromiumPageLoader`.
- [x] Vendor is the host label `"Demon Tweeks"` (first-party shop; no sold-by node).
- [x] Fixtures are trimmed Magento-style snippets (Cloudflare blocked live HttpClient capture); they lock the confirmed Name / INC VAT prices.



### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted



## unsaved-changes-prompt

- **Feature:** [docs/features/unsaved-changes-prompt.md](unsaved-changes-prompt.md)
- **Status:** done
- **Last note:** Scan accepted. Feature file Status is `done`. Opening squash PR.



### Questions

*(none)*

### Deviations to scan

- [x] `ConfirmUnsavedWithTimeoutAsync` returns `UnsavedPromptChoice` (Save / Discard / Cancel plus `TimedOut`) so a timeout Save that fails validation can discard and finish leaving.



### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted



## source-onlinecarparts

- **Feature:** [docs/features/source-onlinecarparts.md](source-onlinecarparts.md)
- **Status:** done
- **Last note:** Scan accepted. Feature file Status is `done`. Opening squash PR.



### Questions

*(none)*

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

*(none)*

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

*(none)*

### Deviations to scan

- [x] Sample 2 fixture uses the confirmed unit price **£98.50**; live HttpClient HTML on 2026-08-21 showed **£103.30** (RRP £109.09). Tests no longer lock a GBP amount.
- [x] Vendor is the host label `"Car Battery Market"` (first-party shop; no sold-by node).



### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted



## source-eurocarparts

- **Feature:** [docs/features/source-eurocarparts.md](source-eurocarparts.md)
- **Status:** done
- **Last note:** Scan accepted. Feature file Status is `done`. PR [https://github.com/ManFromMons/WorkCosts/pull/3](https://github.com/ManFromMons/WorkCosts/pull/3) remains open (not squash-merged).



### Questions

*(none)*

### Deviations to scan

- [x] Manufacturer is the first token of `brandImage` alt so “Eicher Premium” matches confirmed **Eicher**.
- [x] Vendor is the host label `"Euro Car Parts"` (first-party shop; no sold-by node).



### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted



## paste-html

- **Feature:** [docs/features/paste-html.md](paste-html.md)
- **Status:** done
- **Last note:** Scan accepted. Feature file Status is `done`. PR [https://github.com/ManFromMons/WorkCosts/pull/1](https://github.com/ManFromMons/WorkCosts/pull/1) remains open (not squash-merged).



### Questions

*(none)*

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

*(none)*

### Deviations to scan

- [x] Also added `ExtraYaml` in `DatabaseService.RepairProductSchema` (same pattern as `PricePoint`) so existing unpackaged databases get the column if migration history is incomplete.
- [x] Added `InputToolTip.Bind(ComboBox, …)` so Technology matches the other extra-spec tooltips.



### Verify

- [x] Tests from the feature file passed
- [x] Deviations accepted