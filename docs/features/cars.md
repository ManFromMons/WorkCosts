# Feature: Cars

- **Id:** `docs/features/cars.md`
- **Seq:** 11
- **Depends-on:** none
- **Status:** ready-for-agent
- **PR:** none
- **Windows:** Core + WinUI (Stuff → Cars master/detail + Add sheet)
- **Related screens:** `docs/screens/cars.md` (new), `docs/screens/shell.md`, `docs/screens/products.md` (Add Product sheet grammar), `docs/screens/jobs.md` (master/detail grammar), `docs/screens/dialogs.md`
- **Related code:** `Product` / `ProductAddEditor` / `ProductImagePicker` / `WebCacheStore`, `GarageJob` / `GarageJobIconStore` / `GarageJobCommands`, `WorkJob`, `ItemOfWork` / `ItemOfWorkCommands`, `MainWindow` Stuff group, `WorkCostsDbContext`, `DialogHelper`

Sibling stories: [car-details.md](car-details.md), [car-details-seed.md](car-details-seed.md), [car-vin-lookup.md](car-vin-lookup.md), [car-fastcarcheck.md](car-fastcarcheck.md), [car-job-links.md](car-job-links.md), [item-of-work-ui.md](item-of-work-ui.md). Home rewrite is **not** a story yet.

## Objectives

- Persist a **`Car`**: the user’s vehicles. Catalogue rows, not work instances.
- **Stuff → Cars** page: master/detail; trailing **Add**; edit the selected car in the detail pane.
- **Add Car** is a **sheet/overlay** with the same grammar as Add Product (no blocking dialog; Chromium never inside a ContentDialog). Image comes from an **online search** of **`{Make} {ModelNumber}`** (e.g. `BMW E60`) on **Bing Images**, then **Google Images** if Bing yields no usable files; user chooses a candidate (same one-image / chooser pattern as Add Product).
- Photo is a **file** under the data root. SQLite stores path + content type, not a new BLOB.
- Persist **`VehicleOrderJson`** now (empty until [car-vin-lookup.md](car-vin-lookup.md)). Extra lookup facts stay in that JSON; they do not replace nickname / make / model / model-number / year / engine on the row.
- Create **car FKs in full** on `GarageJob`, `WorkJob`, and `ItemOfWork` (**Restrict**, never cascade from a car). Soft-delete a car (`DeletedAt` + `UpdatedAt`); later stories filter deleted rows.
- **Out of scope:** Calling mdecoder or FastCarCheck. **Car types** catalogue UI ([car-details.md](car-details.md)) and encoding a type list in the repo ([car-details-seed.md](car-details-seed.md)). Start-work UI ([car-job-links.md](car-job-links.md)). New Home. Zip import implementation (document merge keys only). GNOME/iPad UI.

## User requirements

### Fields (all required to save, except `VehicleOrderJson`)

| Column / intent | Rules |
| :--- | :--- |
| **Name** | Nickname (“the daily”). Required, max 200. Lookups never overwrite it. |
| **Make** | Manufacturer. Required, max 120. |
| **Model** | Marketing name (e.g. 545). Required, max 120. |
| **ModelNumber** | Chassis / series code (e.g. **E60**, **E63**). Required, max 32. Used for image search. Same idea as on `CarDetails`. |
| **EngineType** | Free text (lookup may fill later). Required, max 200. |
| **Vrm** | UK registration. Required. Unique among **non-deleted** cars (compare case-insensitive, ignore spaces). Max 16. |
| **Year** | **Model year** `int`, 1900–current calendar year + 1. Required. |
| **Vin** | Required, max 17. |
| **Image** | Required. User must choose a photo before save. |
| **VehicleOrderJson** | Opaque text, default `""`. Not required. This Seq does not parse it. SQLite `TEXT`. Lookups **supplement** here; they do not drop the scalar columns. |
| **UpdatedAt** | `DateTimeOffset`. Set on create, every save, and soft-delete. |
| **DeletedAt** | `DateTimeOffset?`. Null = active. Set on soft-delete together with `UpdatedAt`. |

No GBP fields. No seed cars.

### List and detail

- Stuff: Products, Jobs, Categories, **Cars** (Car types is Seq 12). Tag `cars`.
- Regular: list beside editor. Compact: **stack**.
- Header: title **Cars**, subtitle, trailing **Add**.
- List: **active** cars only (`DeletedAt` is null). Row: thumbnail, nickname, secondary make · model-number · year · VRM. Empty: “No cars yet.” Showing deleted cars is a later story.
- Detail: editor for the selected car. Save updates scalars + `UpdatedAt` and may replace the image file. Soft-delete: Yes/No (`DialogHelper.ConfirmYesNoAsync`); Yes sets `DeletedAt`/`UpdatedAt`, keeps the row, FKs, and image file; list drops it.
- No selection: “select a car”.
- Dirty editor / dirty add sheet: **Unsaved changes** (`DialogHelper.ConfirmUnsavedWithTimeoutAsync`).

### Add Car sheet

1. Trailing **Add** opens the sheet (not a ContentDialog).
2. User fills nickname, make, model, **model-number**, engine type, VRM, model year, VIN (all required).
3. **Image search:** query **`{Make} {ModelNumber}`** (example: `BMW E60`). Do **not** put year in the query.
   1. Load **Bing Images** `https://www.bing.com/images/search?q={escaped query}`.
   2. If that page yields no usable image files, load **Google Images** `https://www.google.com/search?tbm=isch&q={escaped query}`.
   3. HttpClient first; **Chromium** if challenged (same gate style as Autodoc). Engine **outside** any ContentDialog.
   4. Collect candidate images; one image applies without a grid; several → chooser sheet (`ProductImagePicker.ChooseFromCandidatesAsync` grammar).
4. Local file pick is allowed **in addition** to search (PNG/JPEG/WebP, max 512 KB).
5. Save is disabled until every required field **including image** is set. Duplicate VRM among active cars: in-sheet error, no write.
6. Save creates the row (`VehicleOrderJson` = `""`, `DeletedAt` null, `UpdatedAt` now), writes `{dataRoot}/images/cars/{carId}.{ext}`, selects the row, closes the sheet.
7. Esc: clean sheet closes with no row; dirty details prompt unsaved changes. Enter = primary save; do not steal Enter from confirms.

### Car FKs (this Seq, schema + commands; no start-work UI)

Add-only migration. **No cascade from `Car`.**

| Table | Column | Rules |
| :--- | :--- | :--- |
| `GarageJobs` | `CarId` `Guid?` | FK → `Cars`, **Restrict**. Null allowed for rows created before this Seq. New garage-job writes in later UI require a car ([car-job-links.md](car-job-links.md)). `TargetKind` + `TargetLabel` **stay**. |
| `WorkJobs` | `CarId` `Guid?` | FK → `Cars`, **Restrict**. Null allowed for existing work jobs. Later UI requires a car on new work. |
| `ItemsOfWork` | `CarId` `Guid?` | FK → `Cars`, **Restrict**. Null allowed for existing completions. Later writes also set car-details ([car-details.md](car-details.md)). |

Soft-delete **does not** fail because FKs exist (the row remains). There is no hard delete of `Car` in v1. `CarCommands.TryDeleteAsync` = soft-delete. Unknown id → NotFound.

Commands: unknown `CarId` on garage job / work job / item-of-work update → no write.

### Empty / error / cancel

- Missing required field or year out of range: no persist; visible reason on the sheet/editor.
- Image too large / wrong type: reject that file; keep previous image when editing.
- Search failure: status in the sheet; retry Bing/Google or pick a local file; still cannot save without an image.
- Duplicate VRM (active): no write.

## Layout

- OS spacing. Regular list+detail; compact stack.
- Page header: title + subtitle + trailing Add. Detail in a grouped/inset panel on the garage scrim.
- Thumbnail ~ product editor square. Sheets: Add Car + image chooser. Dialogs: delete confirm, unsaved. Never host WebView2 inside a blocking dialog.
- New `docs/screens/cars.md`. `docs/screens/shell.md` Stuff children include Cars. Compact iPad: Cars stays under Stuff, not a new top tab.

## Workflow

1. Stuff → Cars.
2. Add → sheet → fields (including model-number) → search `{Make} {ModelNumber}` on Bing then Google → choose image → Save.
3. Select row → edit → Save (`UpdatedAt`). Re-search uses the same query rules.
4. Delete → Yes → soft-delete; list no longer shows it; FKs unchanged.
5. Compact back returns to the list.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Persistence | `WorkCostsDbContext`, EF, `CreateContext()` | `Car`, `Cars`, `CarCommands` |
| Image file | `GarageJobIconStore` pattern | `CarImageStore` (`images/cars/`) |
| Image search | `ProductImagePicker` / `ChromiumPageLoader` / `WebCacheStore` | `CarImageSearch` (Bing then Google; query make + model-number) |
| Add sheet | `ProductsPage` AddOverlay, `ProductAddEditor` | `CarsPage`, add overlay, editor |
| Confirm | `DialogHelper` | none |
| Nav | Stuff `NavigationViewItem` | Tag `cars` |
| FKs | `GarageJobCommands`, work-job commands, `ItemOfWorkCommands` | `CarId` on those entities |

- **Wiring:** `App.Database` / `CreateContext()`. Static helpers. No DI container.
- **Data:** SQLite. Images `{dataRoot}/images/cars/{carId}.{ext}`. Unique filtered index on normalized VRM where `DeletedAt` IS NULL. Future zip: car rows + image blobs keyed by id; merge by id; keep `DeletedAt`.
- **Ports:** EF is canonical. Swift follows `Cars` + FKs + files.

## Tests

`WorkCosts.Tests`, temp SQLite. No live network in CI. No UI automation. Search tests use **fixtures** (fake HTML), not Bing/Google.

- `CarCommands_CreateListGetUpdate_RequiresAllFields` (includes ModelNumber + image)
- `CarCommands_Create_EmptyVehicleOrderJson_AndUpdatedAt`
- `CarCommands_RejectsMissingImage_OrEmptyNicknameMakeModelModelNumberEngineVrmVin`
- `CarCommands_YearOutOfRange_Rejected`
- `CarCommands_VrmUnique_AmongActive_IgnoresSpacesAndCase`
- `CarCommands_VrmUnique_AllowsReuse_AfterSoftDelete`
- `CarCommands_TryDelete_SoftDeletes_SetsDeletedAtAndUpdatedAt_KeepsRowAndImage`
- `CarCommands_List_OmitsSoftDeleted`
- `CarImageStore_WriteRead_PngJpegWebp`
- `CarImageStore_RejectsOversizeAndUnknownType`
- `CarImageSearch_Query_IsMakeSpaceModelNumber` (e.g. BMW + E60 → `BMW E60`)
- `CarImageSearch_UsesBingThenGoogle_WhenBingHasNoImages` (fixtures)
- `GarageJobCommands_Update_PersistsCarId_UnknownCar_NoWrite`
- `WorkJob_PersistsCarId_Restrict_NoCascadeOnCarSoftDelete`
- `ItemOfWork_PersistsCarId_Restrict_NoCascadeOnCarSoftDelete`

## Open questions

(none)

## Accepted defaults

- Feature id `cars`; Seq **11**; Depends-on `none`.
- Nickname is `Name`; lookups never overwrite it. Extra facts go in `VehicleOrderJson`.
- **ModelNumber** (E60 / E63) is stored on the car for search; the type catalogue in Seq 12 stores it too.
- Image query is `{Make} {ModelNumber}` on Bing Images, then Google Images. Year is not in the query.
- All listed identity fields + image required; `VehicleOrderJson` optional empty string.
- Model year int; engine type free text; VRM unique among active cars.
- Soft-delete only; Restrict FKs; no cascade from car.
- `TargetKind` / `TargetLabel` unchanged.
- Add Car is a sheet. Compact = stack. No seed cars. No DI container.
- This Seq does not parse `VehicleOrderJson` or call VIN sites.

## Implementation notes for an agent

1. Migration: `Cars` (including `ModelNumber`, `VehicleOrderJson`, `UpdatedAt`, `DeletedAt`) + `CarId` on `GarageJobs`, `WorkJobs`, `ItemsOfWork` (Restrict). Do not cascade.
2. `CarCommands` + `CarImageStore` + `CarImageSearch`. Extend garage-job / work-job / item-of-work commands for `CarId`.
3. `docs/data/schema.md`, `docs/data/connection.md`, `docs/data/garage-job.md`, `docs/screens/cars.md`, Stuff in `docs/screens/shell.md`.
4. WinUI `CarsPage` + add sheet + Bing/Google chooser. Reuse `DialogHelper` / image-picker grammar. No WebView in a ContentDialog.
5. Do not: mdecoder/FastCarCheck HTTP; Home rewrite; hard-delete cars; seed cars; seed car-details; Car types page (Seq 12).
