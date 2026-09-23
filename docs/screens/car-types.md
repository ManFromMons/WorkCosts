# Car types

Catalogue of **car types** (`CarDetails`): make, generation label, **model-number** (E60 / E63), start year, optional end year. Not a vehicle the user owns. Photos live on **Cars**, searched as `{Make} {ModelNumber}`. Engine lives on the **car**.

Seeded from the repo JSON ([19-car-details-catalogue.md](../features/19-car-details-catalogue.md)). FastCarCheck may still create types later.

## Regions (regular)

- Header: title **Car types**, subtitle, trailing **Add**.
- Left: list (make · model-number · year–end year), sorted by **Make**, then **Model**. Empty: “No car types yet.”
- Right: editor, or “select a car type”.
- Overlay/sheet: Add type (make, model, model-number, year; end year optional).

Compact: **stack**. Add is a **sheet**. No WebView.

## Behaviour

- Unique: Make + Model + ModelNumber + Year.
- Delete **Restrict** if a car, job, garage job, or completion references the type.
- Unsaved changes: same as Jobs/Cars.

Spec: `docs/features/12-car-details.md` (CRUD), superseded key and seed: `docs/features/19-car-details-catalogue.md`. Later: `docs/features/14-car-fastcarcheck.md`.
