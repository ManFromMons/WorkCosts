# Feature: Car-details seed (in repo)

- **Id:** `docs/features/16-car-details-seed.md`
- **Seq:** 16
- **Depends-on:** `12-car-details`
- **Status:** done
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/19
- **Windows:** Core initializer only (Car types page already from Seq 12; no new UI here)
- **Related screens:** `docs/screens/car-types.md`
- **Related code:** `CarDetails`, `DbInitializer`, `WorkCostsDbContext`

Seq 12 ships an **empty** `CarDetails` table. This Seq is the **empty loader first**: add `car-details.json` as `[]` plus the reader/`DbInitializer` hook. Filling the file and dropping type engine is [19-car-details-catalogue.md](19-car-details-catalogue.md). FastCarCheck ([14-car-fastcarcheck.md](14-car-fastcarcheck.md)) may also create types at runtime. Do not invent a catalogue here.

## Objectives

- Ship `WorkCosts.Core/Data/car-details.json` as **`[]`**.
- `DbInitializer` reads that JSON (embedded or content file copied to output) and **upserts** by stable `id` and unique key Make+ModelNumber+Year+EngineType.
- Empty array → insert **nothing**, no throw, do **not** delete user-added types.
- **Out of scope:** Inventing a catalogue. Scraping FastCarCheck/mdecoder. Home. Changing the unique key. New UI.

## User requirements

- First launch after this Seq: Car types still empty unless the user added rows.
- When the JSON later gains objects, initialize upserts those ids without wiping user types with different keys.
- Duplicate unique key in DB: skip insert (keep existing).
- Invalid JSON: do not crash initialize; log/skip (test: malformed file does not throw to the UI thread — throw only in tests that pass a bad stream if we expose a helper). **Accepted:** `SeedCarDetailsFromJson` throws `InvalidOperationException` on malformed JSON so tests can assert; `InitializeAsync` catches and continues? Safer for empty loader: **throw in tests via helper; InitializeAsync calls helper only if file exists and is well-formed `[]`.** Malformed in production: skip seed, do not wipe. Test both.

Keep it simple for v1 empty loader:

- File is exactly `[]` (optional whitespace).
- Helper `CarDetailsJsonSeed.Read(Stream)` returns a list (empty).
- `DbInitializer.SeedCarDetailsAsync` upserts that list (no-op).
- Missing file → no-op, no throw.

## Layout

- No new page. Stuff → Car types unchanged.

## Workflow

1. Migrate / initialize.
2. Read JSON `[]`.
3. Upsert zero rows.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Table / page | Seq 12 `CarDetails` | none |
| File | `DbInitializer` | `WorkCosts.Core/Data/car-details.json` = `[]`; `CarDetailsJsonSeed` |
| Ids | stable GUIDs when rows exist later | none in the empty file |

JSON element shape (for when rows are added later):

```json
{
  "id": "guid",
  "make": "BMW",
  "model": "545",
  "modelNumber": "E60",
  "year": 2004,
  "engineType": "4.4L V8"
}
```

`System.Text.Json`. File as content copied to output, or `Assembly.GetManifestResourceStream`. Pick **embedded resource** so the library always finds it.

- **Wiring:** `SeedAsync` calls `SeedCarDetailsAsync` after jobs. No DI.
- **Data:** no migration if Seq 12 already created the table. This Seq is loader + empty file only.
- **Ports:** Swift can ignore the C# json until a port story; schema unchanged.

## Tests

- `CarDetailsJsonSeed_EmptyArray_ReturnsEmpty`
- `DbInitializer_EmptyCarDetailsJson_InsertsNothing`
- `DbInitializer_KeepsUserAddedTypes_WhenJsonEmpty`
- `CarDetailsJsonSeed_Malformed_Throws` (helper only)

Do **not** add a sample BMW row in the file.

## Open questions

(none)

## Accepted defaults

- Seq **16**; Depends-on **`12-car-details`**. JSON; empty `[]` first; path/embedded `WorkCosts.Core/Data/car-details.json`. Do not invent types.

## Implementation notes for an agent

1. Add empty JSON + reader + `DbInitializer` call.
2. Tests above. Do not populate makes/models.
3. Do not: UI, FastCarCheck HTTP, wipe user types.
