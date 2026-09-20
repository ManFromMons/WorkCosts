# Feature: Cars

- **Id:** `docs/features/cars.md`
- **Seq:** 11
- **Depends-on:** none
- **Status:** draft
- **PR:** none
- **Windows:** Core + WinUI (Stuff → Cars master/detail + Add sheet)
- **Related screens:** `docs/screens/cars.md` (new), `docs/screens/shell.md`, `docs/screens/products.md` (Add Product sheet grammar), `docs/screens/jobs.md` (master/detail grammar), `docs/screens/dialogs.md`
- **Related code:** `Product` / `ProductEditor` / `ProductAddEditor` / `ProductImagePicker` / `WebCacheStore`, `GarageJob` / `GarageJobIconStore` (file-on-disk image pattern), `GarageJobCommands`, `WorkJob`, `MainWindow` Stuff group, `WorkCostsDbContext`, `DialogHelper`

Sibling stories (separate files, not this Seq): [car-vin-lookup.md](car-vin-lookup.md), [car-job-links.md](car-job-links.md). Home rewrite (current/pending work, active garage jobs, timeline) is **not** a story yet.

## Objectives

- Persist a **`Car`**: the user’s vehicles. Catalogue rows, not work instances.
- **Stuff → Cars** page: master/detail like Jobs/Products; trailing **Add**; edit the selected car in the detail pane.
- **Add Car** is a **sheet/overlay** with the same grammar as Add Product: not a blocking dialog; image fetch and image choice live in that sheet; browser/WebView is never inside a ContentDialog.
- Store the chosen photo as a **file** (same family as garage-job icons / product library photos). SQLite holds path + content type, not a new BLOB column.
- Add a **`VehicleOrderJson`** string column on `Car` so a later VIN lookup can persist its payload. This Seq does **not** call a lookup service.
- **Out of scope:** VIN / VRM network lookup and filling fields from it ([car-vin-lookup.md](car-vin-lookup.md)). Optional `CarId` on `GarageJob` / `WorkJob`, starting work from a garage job, make/model fitment filters ([car-job-links.md](car-job-links.md)). New Home (current/pending work, garage-job timeline). Seeded cars. Zip export/import implementation (document merge keys only). GNOME/iPad UI (schema is canonical for ports).

## User requirements

Observable: the user can add, list, edit, and delete cars, and pick a photo.

### Fields (persisted)

Exact names and which are required are **open questions**. Intent from planning chat:

| Intent | Notes |
| :--- | :--- |
| Identity the user gives the car | “Name” in the request — may be a nickname, or make; see Q1 |
| Model | e.g. MX-5 / Golf |
| Engine type | e.g. petrol / 1.8 / B6 |
| VRM | UK registration mark |
| Year | integer year |
| Image | chosen photo for lists and detail |
| VIN | needed for later lookup; may be optional here |
| `VehicleOrderJson` | opaque JSON text; empty until lookup story |

No GBP fields. No accounts.

### List and detail

- Stuff group gains **Cars** (with Products, Jobs, Categories).
- Regular: list beside editor. Compact: stack (list, then push editor).
- Header: title **Cars**, subtitle, trailing **Add**.
- List rows: thumbnail, primary title, secondary line (make/model/year or VRM — see questions). Empty: “No cars yet.”
- Detail: edit the selected car (same fields as add, minus the add-sheet staging). Save on the editor. Delete with Yes/No confirm (`DialogHelper.ConfirmYesNoAsync`).
- No car selected: empty “select a car”.
- Dirty editor / dirty add sheet: **Unsaved changes** (`DialogHelper.ConfirmUnsavedWithTimeoutAsync`) like Add Product / Jobs.

### Add Car sheet

- Trigger: trailing **Add**. Sheet/overlay, not a ContentDialog that hosts the browser.
- User can fetch candidate **make/model images** and **choose** one for the car (same “one image applied without a grid / several → chooser” idea as Add Product). Source of the fetch is an open question (page URL vs search vs file only).
- Esc: URL/fetch stage with no edits closes with no prompt; dirty details prompt unsaved changes.
- Enter confirms the primary action; do not steal Enter from Yes/No or unsaved prompts.
- After save, the new car is selected in the list.

### Empty / error / cancel

- Invalid year / empty required fields: do not persist; stay on the sheet/editor with a visible reason (no throw to a crash dialog).
- Image too large or wrong type: reject write; keep previous image if editing.
- Fetch failure: message in the sheet; user can retry, pick a local file, or continue without an image (if image is optional).
- Cancel / Esc on clean add: no row.
- Delete: Yes deletes the row and the image file. If later stories have linked garage/work jobs, this Seq either has no FKs yet or Restrict — see Q8 and the links story.

## Layout

- Size classes: regular list+detail; compact **stack**. OS spacing, not WinUI pixel copies.
- Regions: page header (title + subtitle + trailing Add) / master list / detail editor / Add sheet.
- Detail sits in a grouped/inset panel on the garage background scrim.
- Image in editor: square thumbnail (same order of size as `ProductEditor` ~118px), choose/clear, not a second destination.
- Sheets vs dialogs: Add Car + image chooser = **sheets**. Delete / unsaved = **dialogs**. Never host WebView2 / Chromium inside a blocking dialog.
- New screen file `docs/screens/cars.md`. Update `docs/screens/shell.md` Stuff children: Products, Jobs, Categories, **Cars**. Compact iPad tab bar: do not add Cars as a top tab in this story (Stuff remains the group); list the Cars page under Stuff.

## Workflow

1. User opens **Stuff → Cars**. Empty list or existing cars.
2. **Add** opens the Add Car **sheet**.
3. User enters identity fields (and VIN/VRM if shown). Image: fetch candidates (Q5) and choose, or skip, or pick a local file.
4. Save creates `Car`, writes image file if chosen, closes the sheet, selects the row.
5. Selecting a row shows the editor. Edit + save updates scalars and may replace the image file.
6. Delete: confirm Yes/No; Yes removes row + image file.
7. Esc/back dismisses the sheet (with unsaved prompt if dirty). Compact back returns to the list.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Persistence | `WorkCostsDbContext`, EF migrations, `DatabaseService` / `CreateContext()` | `Car` entity, `Cars` table, `CarCommands` |
| Image file | `GarageJobIconStore` pattern (max size, png/jpeg/webp, relative path under data root) | `CarImageStore` (or shared helper if a one-line extract is enough — do not invent a DI container) |
| Add sheet + image choice | `ProductsPage` AddOverlay, `ProductAddEditor`, `ProductImagePicker.FetchPageAsync` / `ChooseFromCandidatesAsync` (if fetch is a page URL) | `CarsPage`, add overlay, car add/editor controls |
| Confirm / unsaved | `DialogHelper.ConfirmYesNoAsync`, `ConfirmUnsavedWithTimeoutAsync` | none |
| Nav | `MainWindow` Stuff `NavigationViewItem`s, tag + frame navigation | Tag `cars` |
| Lookup payload | — | `VehicleOrderJson` string column, default empty; no parser in this Seq |

- **Wiring:** `App.Database` / `CreateContext()` like other pages. Static command helpers. No new DI container.
- **Data:** SQLite `Cars`. Images `{dataRoot}/images/cars/{carId}.{ext}` (or `icons/cars/` if we keep icons vs photos distinct — default **photos** under `images/cars/`). Future zip: include those blobs keyed by car id; merge on import by id.
- **Ports:** EF migration is canonical. Swift follows `Cars` + image files. GNOME gets the page in a later port slice, not this Seq.

### Schema (draft — field list pending Q1–Q4)

**`Cars`**

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | Guid PK | |
| `Name` | string | required unless Q1 says otherwise; max 200 |
| `Make` | string | max 120; may be the same as Name — Q1 |
| `Model` | string | max 120 |
| `EngineType` | string | max 120; free text unless Q4 |
| `Vrm` | string | max 16; UK registration; empty allowed unless Q2 |
| `Year` | int? | model year unless Q3 |
| `Vin` | string | max 17 typical; empty allowed |
| `VehicleOrderJson` | string | max 8000 or larger if lookup blobs are big — Q7; empty default |
| `ImageRelativePath` | string | empty = no photo |
| `ImageContentType` | string | empty when no photo |

Index unique on `Vrm` only if Q6 says VRM must be unique when set.

No FK from `GarageJob` / `WorkJob` in this Seq.

## Tests

Project: `WorkCosts.Tests` (temp SQLite). WinUI sheet is specified; no UI automation required if the rest of the app has none.

- `CarCommands_CreateListGetUpdateDelete`
- `CarCommands_Create_PersistsOptionalVinAndEmptyVehicleOrderJson`
- `CarCommands_RejectsEmptyRequiredName` (adjust if Q1/Q2 change required fields)
- `CarCommands_YearOutOfRange_Rejected` (range TBD)
- `CarImageStore_WriteReadDelete_PngJpegWebp`
- `CarImageStore_RejectsOversizeAndUnknownType`
- `CarCommands_TryDelete_RemovesImageFile`
- `CarCommands_VrmUnique_WhenSet` — only if Q6 unique

## Open questions

1. *Assumption:* **Name** is the user’s nickname (“the daily”) and **Make** is the manufacturer (Mazda), with **Model** separate. → **Question:** Is Name a nickname plus Make+Model, or is Name actually the make?
2. *Assumption:* Required to save: Name, Make, Model. Optional: Engine type, VRM, Year, VIN, image. → **Question:** Which fields are required?
3. *Assumption:* Year is **model year** (int), 1900–current+1, not first-registration date. → **Question:** Model year, registration year, or a date?
4. *Assumption:* Engine type is **free text** (lookup may fill it later). → **Question:** Free text, or a closed list (petrol / diesel / hybrid / EV / other)?
5. *Assumption:* Add-sheet image fetch reuses `ProductImagePicker` against a **page URL** the user pastes (manufacturer, Wikipedia, listing), then the same candidate chooser; plus “choose a local file”. → **Question:** Where do make/model images come from — pasted page URL, a search box, VIN lookup (later), local file only, or something else?
6. *Assumption:* VRM is optional and **unique when set** (case-insensitive, ignore spaces). → **Question:** Must VRM be unique? Required?
7. *Assumption:* `VehicleOrderJson` is added in **this** Seq as an empty string column (max 8000) so lookup can land later without another migration if 8000 is enough. → **Question:** Add the column now, and is 8000 enough?
8. *Assumption:* This Seq is **WinUI + Core** (Stuff → Cars), not Core-only like Seq 9 garage-job. → **Question:** Confirm Windows UI in Seq 11?
9. *Assumption:* Delete is allowed in this Seq because no job FKs exist yet. → **Question:** Any other reason to block delete (e.g. you want the FKs in this migration)?

## Accepted defaults

- Feature id `cars`; Seq **11**; **Depends-on** `none`.
- Currency still GBP app-wide; cars have no money fields.
- No seed cars. No DI container. Image is a file, not a SQLite BLOB.
- Add Car is a **sheet**, never a dialog hosting Chromium.
- Compact = stack. Primary Add trailing.
- `VehicleOrderJson` is opaque; this Seq does not parse it.
- GarageJob `TargetKind` / `TargetLabel` unchanged here.

## Implementation notes for an agent

Do not implement while **Status** is `draft`.

1. After answers: rewrite this file to `ready-for-agent`; remove resolved questions; put leftovers under **Accepted defaults**.
2. Migration: `Cars` + image files. Do not add `CarId` on `GarageJob` / `WorkJob` unless Q9 says so.
3. `CarCommands` + `CarImageStore`. Tests named above.
4. `docs/data/schema.md`, `docs/data/connection.md`, new `docs/screens/cars.md`, Stuff item in `docs/screens/shell.md`.
5. WinUI: `CarsPage` master/detail, Add overlay, editor. Reuse `ProductImagePicker` / `DialogHelper` as specified.
6. Do not: VIN HTTP; Home rewrite; garage-job WinUI; zip import; seed cars; WebView inside a ContentDialog.
