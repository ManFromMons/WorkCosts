# Delivery: Cars

- **Feature:** [docs/features/11-cars.md](11-cars.md)
- **Seq:** 11
- **Branch:** `feature/11-cars-Cars`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/13

## What landed

- Stuff → Cars master/detail with trailing Add. Narrow width stacks the list, then the detail, with Back. Detail fields are three per row.
- Add Car is a sheet. Nickname, make, model, model number, engine, VRM, model year, VIN, and a photo are required. Duplicate active registrations do not write.
- Image search uses `{Make} {ModelNumber}` on Bing Images, then Google Images. HttpClient first; Chromium only if challenged, outside any dialog. One photo applies immediately; several use the existing chooser. Local PNG, JPEG, or WebP is allowed.
- Cars are SQLite rows. Photos are files under `images/cars/`. `VehicleOrderJson` starts empty. Soft-delete keeps the row, photo, and foreign keys, and hides the car.
- Nullable Restrict `CarId` on garage jobs, work jobs, and items of work. An unknown car id does not write.

## Tests

- `dotnet test WorkCosts.Tests/WorkCosts.Tests.csproj --settings .runsettings` — 175 passed, including the car command, image store, image search, and foreign-key cases named in the spec.
- `dotnet build WorkCosts/WorkCosts.csproj` — succeeded before the three-column detail layout. That layout is XAML only.

## Deviations

- Normalized registration is stored as `VrmKey` (upper case, spaces removed) with a unique filtered index where `DeletedAt` is null.
- `GarageJobCommands.UpdateAsync` leaves `CarId` unchanged unless `setCarId` is true. Work jobs use `WorkJobCommands.TrySetCarIdAsync`. Items of work take an optional `CarId` on create, plus `TrySetCarIdAsync`.
- The image chooser reuses `ProductImagePicker.ChooseFromCandidatesAsync` (a ContentDialog) with an optional title. Add Car stays a sheet. Chromium runs before that dialog.
- `Microsoft.EntityFrameworkCore.Design` IncludeAssets now includes runtime so `dotnet ef` can see the package. PrivateAssets stays `all`.
