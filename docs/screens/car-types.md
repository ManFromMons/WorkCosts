# Car types

Catalogue of **car types** (`CarDetails`): make, model, **model-number** (E60 / E63), year, engine. Not a vehicle the user owns. Photos live on **Cars**, searched as `{Make} {ModelNumber}`.

v1 table starts **empty**. Seed-from-repo and FastCarCheck hook onto this page later.

## Regions (regular)

- Header: title **Car types**, subtitle, trailing **Add**.
- Left: list (make · model-number · year · engine). Empty: “No car types yet.”
- Right: editor, or “select a car type”.
- Overlay/sheet: Add type (five required fields).

Compact: **stack**. Add is a **sheet**. No WebView.

## Behaviour

- Unique: Make + ModelNumber + Year + EngineType.
- Delete **Restrict** if a car, job, garage job, or completion references the type.
- Unsaved changes: same as Jobs/Cars.

Spec: `docs/features/car-details.md`. Later: `docs/features/car-details-seed.md`, `docs/features/car-fastcarcheck.md`.
