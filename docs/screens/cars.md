# Cars

User’s vehicles (nickname, make, model, model-number, engine, VRM, year, VIN, photo). Not work instances. Not car **types** (`docs/screens/car-types.md`).

## Regions (regular)

- Header: title **Cars**, subtitle, trailing **Add**.
- Left: list of **active** cars (soft-deleted hidden). Row: thumbnail, nickname, make · model-number · year · VRM. Empty: “No cars yet.”
- Right: editor for the selection, or “select a car”.
- Overlay/sheet: Add Car (fields + `{Make} {ModelNumber}` image search on Bing, then Google; image chooser). Local file allowed too.

Compact: **stack**. Add stays a **sheet**. Never host WebView2 in a ContentDialog.

## Behaviour

- All identity fields + image required. VRM unique among active cars. `VehicleOrderJson` optional.
- Delete is **soft-delete** (`DeletedAt` + `UpdatedAt`); keep row, FKs, and photo file.
- Unsaved changes: same prompt as Add Product / Jobs.

Spec: `docs/features/cars.md`.
