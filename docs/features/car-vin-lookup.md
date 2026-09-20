# Feature: Car VIN lookup

- **Id:** `docs/features/car-vin-lookup.md`
- **Seq:** 12
- **Depends-on:** `cars`
- **Status:** draft
- **PR:** none
- **Windows:** WinUI on the Cars add sheet + editor (Core helpers for the lookup)
- **Related screens:** `docs/screens/cars.md`, `docs/screens/dialogs.md`
- **Related code:** `Car`, `CarCommands`, Add Car sheet, `ProductImagePicker` (do **not** put the lookup browser in a ContentDialog), `VehicleOrderJson`

Depends on [cars.md](cars.md). Does not include Home or job links ([car-job-links.md](car-job-links.md)).

## Objectives

- When the user enters a **VIN** (and/or **VRM** — see questions) on Add Car or the car editor, run a **details lookup**.
- Persist the raw result on the car as **`VehicleOrderJson`** (column from Seq 11).
- Optionally copy obvious fields (make, model, year, engine type) onto the car row when the user accepts the result.
- Lookup may take time and **multiple requests**; the UI must stay usable (status in the sheet, cancel, no frozen modal).
- **Out of scope:** Cars CRUD itself. Job/car links. Home. Inventing a paid API account in git. Chromium inside a blocking dialog.

## User requirements

- Trigger: leaving the VIN field, an explicit **Lookup** button, or both (question).
- Status text while requests run. User can cancel.
- Success: show a summary of what was found; user confirms before overwriting typed fields.
- Failure / timeout / unknown VIN: message; keep typed fields; `VehicleOrderJson` unchanged or cleared — question.
- Empty VIN: no request.
- Offline: fail visibly; app stays local-first otherwise.

## Layout

- Control on the Add Car sheet and the car detail editor, next to VIN (and VRM if that is a key).
- Progress/status **in the sheet**, not a blocking dialog. If a WebView is required for a site, it follows Add Product: engine **outside** any ContentDialog.
- Confirm apply-fields: short Yes/No or in-sheet banner, not a nested browser dialog.

## Workflow

1. User enters VIN (or VRM).
2. Lookup starts (debounce / button — question).
3. One or more requests run; status updates.
4. Result stored as JSON on the car when saved. User confirms copying into Make/Model/Year/Engine.
5. Esc cancels an in-flight lookup without closing the sheet if details are dirty (same unsaved rules as cars).

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Column | `Car.VehicleOrderJson` from Seq 11 | none (or widen max length) |
| HTTP | existing `HttpClient` identity headers if a JSON API; `ChromiumPageLoader` only if the source blocks HttpClient | `CarVehicleLookup` (name TBD) |
| UI | Cars add sheet / editor | Lookup status + apply confirmation |

- **Wiring:** static helper or small service constructed like `ProductImageService`. No DI container. No secrets in the repo.
- **Data:** overwrite `VehicleOrderJson` on successful lookup; schema of the JSON is defined here once the source is known.
- **Ports:** same JSON column; lookup implementation may be Windows-first if it needs WebView2, then GNOME/iPad.

## Tests

- `CarVehicleLookup_EmptyVin_DoesNotRequest`
- `CarVehicleLookup_PersistsVehicleOrderJson`
- `CarVehicleLookup_DoesNotOverwriteFields_UntilAccepted`
- Fixture/fake handler tests — **no live network in CI** (same rule as parsers).

Exact cases after the source is known.

## Open questions

1. *Assumption:* Lookup key is **VIN** as you said; VRM is stored but not sent. → **Question:** VIN, VRM, or both? UK DVLA-style services often key on VRM.
2. *Assumption:* We must not invent a vendor. → **Question:** Which service or site should we use? (URL, whether it needs a key, whether Chromium is required.)
3. *Assumption:* Multiple requests are sequential in one helper, with per-step status. → **Question:** What are the steps (e.g. decode VIN, then options/order, then image)?
4. *Assumption:* On success we fill Make, Model, Year, Engine type only after **Yes**. Nickname/Name is never overwritten. → **Question:** Which fields may the lookup write?
5. *Assumption:* Image from lookup is a later nice-to-have; Seq 11 image fetch stays. → **Question:** Should lookup also propose a photo?
6. *Assumption:* `VehicleOrderJson` is the full last successful payload, replaced on each lookup, not a history array. → **Question:** Replace, merge, or keep history?

## Accepted defaults

- Seq **12**; Depends-on **`cars`**. No secrets in git. No live network in `WorkCosts.Tests`. Local-first: lookup is optional.

## Implementation notes for an agent

Do not implement while **Status** is `draft`. Do not implement before `cars` is **done**.

1. Confirm source and field mapping; then `ready-for-agent`.
2. Fake HTTP / fixtures; never commit API keys.
3. Do not: Home, job FKs, WebView in a ContentDialog, scraping behind a login the user cannot pass.
