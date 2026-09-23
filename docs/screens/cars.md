# Cars

User’s vehicles (nickname, make, model, model-number, **engine code**, VRM, year, VIN, photo). Not work instances. Not car **types** (`docs/screens/car-types.md`).

## Regions (regular)

- Header: title **Cars**, subtitle, trailing **Add**.
- Left: list of **active** cars (soft-deleted hidden). Row: thumbnail, nickname, make · model-number · year · VRM. Empty: “No cars yet.”
- Right: editor for the selection, or “select a car”.
- Overlay/sheet: Add Car (fields + `{Make} {ModelNumber}` image search on Bing, then Google; image chooser). Local file allowed too.

Compact: **stack**. Add stays a **sheet**. Never host WebView2 in a ContentDialog.

## Behaviour

- All identity fields + image required. Engine field label is **Engine code**. VRM unique among active cars. `VehicleOrderJson` optional. Optional **car type** combo (search by make / model-number, label is make · model-number · years); does not clear nickname, scalars, or engine.
- **Lookup** next to VIN runs mdecoder when the VIN is set and the car looks BMW (`Make` BMW or VIN `WBA`/`WBS`/`WBY`/`5UX`/`5YM`). Status stays on the sheet; cancel stops polling and does not discard the form. Nickname is never overwritten.
- Delete is **soft-delete** (`DeletedAt` + `UpdatedAt`); keep row, FKs, and photo file.
- Unsaved changes: same prompt as Add Product / Jobs.

Spec: `docs/features/11-cars.md`.
