# Feature: Car-details catalogue seed

- **Id:** `docs/features/19-car-details-catalogue.md`
- **Seq:** 19
- **Depends-on:** `16-car-details-seed`
- **Status:** done
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/20
- **Windows:** Core + WinUI (Car types page and Cars engine label; no new destination)
- **Related screens:** `docs/screens/car-types.md`, `docs/screens/cars.md`
- **Related code:** `CarDetails`, `CarDetailsCommands`, `CarDetailsJsonSeed`, `CarDetailsJsonRow`, `DbInitializer`, `Car`, `CarCommands`, `CarTypesPage`, `CarsPage`

Product name **16.1**: fill the Seq 16 loader with a real type catalogue. Board Seq is **19** because [18-car-image-sources.md](18-car-image-sources.md) already uses 18.

A **car type** is a generation (make + generation label + chassis + years), not a vehicle and not an engine. Engines live on **`Car`**. Seq 12’s unique key `Make|ModelNumber|Year|EngineType` and required type engine are **superseded here**. Seq 16’s empty `[]` file and upsert hook stay; this story changes the row shape and populates the file.

## Objectives

- Generate `WorkCosts.Core/Data/car-details.json` from the free Tiresaddict generation dump ([docs/data/tiresaddict-gen.json](../data/tiresaddict-gen.json)), optionally filling missing chassis via Wikidata SPARQL.
- Drop **`EngineType`** from `CarDetails`. Unique key becomes **`Make|Model|ModelNumber|Year`**. Add nullable **`EndYear`**.
- Keep **`Car.EngineType`** required (column unchanged). UI label **Engine code**. Picking a type does not fill engine; the user or a later lookup (mdecoder / FastCarCheck) must.
- Seeded ids are **overwritten** on the next initialize if the JSON changes. User-added types with a different key stay.
- **Out of scope:** Runtime download of Tiresaddict or Wikidata. Inventing engines. FastCarCheck / mdecoder catalogue scrape. Paid Tiresaddict dumps. Home. ItemOfWork UI. Seq 14 resume. New nav destinations.

## User requirements

### Type fields

| Column | Rules |
| :--- | :--- |
| **Make** | Required, max 120. From Tiresaddict `brand`. |
| **Model** | Required, max 120. From Tiresaddict `mod` (generation label), e.g. `5 series (E60)`, `A-class AMG (W177)`. |
| **ModelNumber** | Required, max 32. Chassis. Never blank. |
| **Year** | Required. First year of the generation (`start_year`). Same range as `Car.Year` (1900 through current calendar year + 1). |
| **EndYear** | Optional. Last year (`end_year`). Empty / `""` in the dump → null (still in production). If set, must be ≥ `Year` and in the same allowed range. |

No engine column on the type. Two engines on the same generation are **one** type.

Unique among types: uppercase trimmed **`Make|Model|ModelNumber|Year`**. `Model` is in the key so `A-class (W177)` and `A-class AMG (W177)` in the same start year stay two rows.

### Stuff → Car types

- Same page as Seq 12 (`CarTypesPage`). No new destination.
- List: make · model · model-number · year–end year (`2004–2010`, or `2004–` when `EndYear` is null). Empty: “No car types yet.”
- Add **sheet**: Make, Model, Model-number, Year, End year (optional). Four required fields; no engine box.
- Detail: the same fields. Delete: `DialogHelper.ConfirmYesNoAsync`; **Restrict** if any `Car`, `JobCarDetails`, `GarageJob`, or `ItemOfWork` points at it. Unsaved changes: existing helper.
- After this Seq, first launch with a populated seed shows the generated types (not an empty list).

### Cars (engine code)

- `CarsPage` Add sheet and editor: header **Engine code** (was “Engine type”). Placeholder an engine code (`N62`, `M57`), not `4.4 V8`. Still required. Max 200. Column remains `Car.EngineType`.
- Type combo search stays make / model-number. Combo label is make · model-number · years — **no engine**. Selecting a type does **not** write `Car.EngineType`.
- Save still refuses a blank engine code (`CarCommands` unchanged except tests that mention the label).

### Empty / error / cancel

- Generator: skip a Tiresaddict row with a missing `brand` / `model` / `mod`, or an unparseable / out-of-range `start_year`. Do not fail the whole run.
- Wikidata failure or timeout: use `model` as `ModelNumber` and continue. Never throw to the UI thread (the app does not call Wikidata).
- Duplicate TypeKey in the generated list: keep the first row, skip the rest.
- Seed upsert: same id → overwrite scalars including `EndYear`. Same TypeKey, different id already in the DB → skip insert (keep the user’s row). Missing embedded file → no-op (Seq 16).
- Malformed `car-details.json`: Seq 16 behaviour (helper throws; initialize skips, does not wipe).
- Add/edit type: duplicate key → no write; visible reason. Esc / Back dismisses the sheet per existing unsaved-changes.

## Layout

- Size classes: regular list beside detail; compact **stack**. Primary **Add** stays trailing.
- Regions unchanged: header, master list, detail editor, Add sheet.
- No WebView2 / WebKit in a dialog. Generator is not a UI surface.
- OS spacing. Garage scrim on the detail inset.

## Workflow

### Build-time (not at launch)

1. Read [docs/data/tiresaddict-gen.json](../data/tiresaddict-gen.json) (all brands).
2. Map each object (`brand`, `model`, `gen`, `mod`, `start_year`, `end_year`) with the mapping below. `gen` is not stored.
3. For rows whose `mod` has no parentheses, ask Wikidata for a chassis. On miss or error, `ModelNumber` = `model`.
4. Assign a **stable GUID** from TypeKey. Skip exact TypeKey duplicates.
5. Write embedded `WorkCosts.Core/Data/car-details.json`.
6. Implementer commits that file. The running app never hits Tiresaddict or Wikidata.

### App initialize

1. Migrate (drop type engine, add `EndYear`, rebuild `TypeKey`).
2. `DbInitializer.SeedAsync` → `SeedCarDetailsAsync` (Seq 16 hook) upserts the new JSON.
3. Stuff → Car types lists seeded generations.

### Add / edit type (user)

1. Add → sheet → Save (Enter) / Esc (unsaved helper).
2. Edit in the detail pane. Delete unused: Yes/No.

### Add / edit car

1. Engine code is required before Save.
2. Optional type combo does not fill engine.

## Technical design

### Tiresaddict → `CarDetails`

Source: free “Make / Model / Generation / Years” JSON from [tiresaddict.com/help/databases/cars/](https://tiresaddict.com/help/databases/cars/) (`gen_json.free`). Attribution: [docs/data/tiresaddict-gen.md](../data/tiresaddict-gen.md). They mark it free and without guarantee of fullness. Do not commit a paid dump.

| Source | `CarDetails` |
| :--- | :--- |
| `brand` | **Make** |
| `mod` | **Model** |
| Chassis from `mod` parens, else Wikidata, else `model` | **ModelNumber** |
| `start_year` | **Year** |
| `end_year` (`""` → null) | **EndYear** |

**Chassis from `mod`:** take the inside of the last `(…)`. Keep comma- or slash-separated lists when length ≤ 32 (`E90, E91, E92, E93`, `X350/X358`). If longer than 32, keep only the first code (split on comma, then slash). Examples:

- `5 series (E60)` → `E60`
- `1 series (E87, E81, E82, E88)` → `E87, E81, E82, E88` (20 chars)
- `XJ III (X350\/X358)` → `X350/X358`
- `C-class (W204)` → `W204`
- `E-Pace` (no parens) → Wikidata, else `E-Pace`

### Unique key and ids

```csharp
NormalizeTypeKey(make, model, modelNumber, year)
    => $"{make.Trim().ToUpperInvariant()}|{model.Trim().ToUpperInvariant()}|{modelNumber.Trim().ToUpperInvariant()}|{year}";
```

Stable id: SHA-256 of UTF-8 `willidiy.cardetails/` + TypeKey; take 16 bytes; set RFC 4122 version 5 and variant bits; `new Guid(...)`. Same TypeKey always yields the same id so upsert overwrites the seed row.

### Reuse vs create

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Load / upsert | `CarDetailsJsonSeed`, `DbInitializer.SeedCarDetailsAsync` | New `CarDetailsJsonRow` shape (no `EngineType`; add `int? EndYear`) |
| Commands / page | `CarDetailsCommands`, `CarTypesPage`, `DialogHelper` | Drop engine validation; add `EndYear`; new TypeKey |
| Car engine | `Car.EngineType`, `CarCommands` | Label/placeholder only on `CarsPage` |
| Map dump | — | `CarDetailsCatalogueMapper` (pure; Core; no HTTP) |
| Chassis gaps | — | `WikidataChassisLookup` (optional; **not** called from initialize) |
| Write seed file | — | `scripts/Build-CarDetailsSeed.ps1` (maps, optional SPARQL, writes `car-details.json`) |

- **Wiring:** no DI. Mapper and lookup are static helpers, same style as `CarDetailsJsonSeed`. The script is the only Wikidata caller. `App.Database` / `CreateContext()` unchanged.
- **Data:** one add-only EF migration on `CarDetails`: drop `EngineType`; add nullable `EndYear` int; drop and recreate unique index on `TypeKey`; rewrite every existing `TypeKey` to the new formula. If two rows then share a key, **keep the first** (lowest `Id` string order) and delete the duplicate only when it has **no** Restrict dependents; if a duplicate is in use, leave both and skip rewriting the in-use row’s key (inbox that case — empty table is the usual path). No new BLOBs. Zip later: `CarDetails` without engine; merge by id (already the zip rule).
- **Ports:** EF is canonical. Swift follows the migration. GNOME/iPad pick up the same seed file; they do not run the generator.

### Wikidata (optional, generator only)

- Endpoint: `https://query.wikidata.org/sparql` (GET, `application/sparql-query` or `query=`).
- User-Agent: `WillIDIY-CarDetailsSeed/1.0 (offline catalogue generator; not the shipping app)`.
- Search English labels for `{brand} {model}` (Tiresaddict **model line**, not `mod`). Prefer a chassis / model-code **statement** if present; else parse `(CODE)` from the English label.
- Do **not** invent a Wikidata property id in this spec. Discover the property on a known item (BMW E60 / Jaguar X350) during implementation; if none exists, label-parentheses only.
- Timeout or HTTP error: fallback to `model`. Memory-cache per `brand|model` for one script run.
- No live network in CI.

### Embedded JSON shape

```json
{
  "id": "guid",
  "make": "BMW",
  "model": "5 series (E60)",
  "modelNumber": "E60",
  "year": 2004,
  "endYear": 2010
}
```

`endYear` may be `null`. `System.Text.Json`, camelCase, same options as Seq 16.

## Tests

Project: `WorkCosts.Tests`. Fixtures are **tiny** slices, not the full dump. No live network.

- `CatalogueMapper_BmwE60_MapsModAndChassisAndYears` — `5 series (E60)` / 2004–2010
- `CatalogueMapper_JaguarX350_KeepsSlashChassis` — `XJ III (X350/X358)`
- `CatalogueMapper_MercedesW204_MapsChassis` — `C-class (W204)`
- `CatalogueMapper_NoParens_UsesFallbackModel` — E-Pace with Wikidata mocked to miss
- `CatalogueMapper_NoParens_UsesWikidataWhenProvided` — E-Pace mock returns a chassis
- `CatalogueMapper_LongChassisList_TakesFirstCodeWhenOver32`
- `CatalogueMapper_SkipsBadStartYear`
- `CatalogueMapper_AmgAndStandard_AreTwoTypes` — same brand/chassis/year, different `mod`
- `NormalizeTypeKey_DoesNotIncludeEngine`
- `CarDetailsCommands_RejectsDuplicateMakeModelModelNumberYear`
- `CarDetailsCommands_AllowsMissingEndYear`
- `CarDetailsCommands_RejectsEndYearBeforeYear`
- `CarDetailsCommands_Create_DoesNotRequireEngine`
- `CarCommands_Create_StillRequiresEngineCode`
- `CarDetailsJsonSeed_Upsert_OverwritesSameId`
- `CarDetailsJsonSeed_KeepsUserType_WhenTypeKeyDiffers`
- `CarDetailsJsonSeed_SkipsInsert_WhenTypeKeyExistsWithOtherId`
- `GuidFromTypeKey_IsStable`
- `DbInitializer_SeededCatalogue_InsertsMappedRows` — fixture stream, not the full file
- `WikidataChassisLookup_OnError_DoesNotThrowToCaller` — mapper still emits a row

## Open questions

(none)

## Accepted defaults

- Seq **19**; Depends-on **`16-car-details-seed`**. Product name 16.1.
- All brands in the Tiresaddict file.
- `Model` = `mod`; chassis from parens / Wikidata / `model`.
- TypeKey `Make|Model|ModelNumber|Year`. Engine only on `Car`.
- `EndYear` nullable. Seeded ids overwrite. First-code-only when chassis text exceeds 32.
- Relabel `Car.EngineType` to Engine code; no rename migration.
- Generator is `scripts/Build-CarDetailsSeed.ps1` + Core mapper. App stays offline.
- Schema and screen files are updated **in this implementation** to match this file (Planning copies of those docs may be behind `main`).

## Implementation notes for an agent

1. Branch `feature/19-car-details-catalogue-Car-details-catalogue` from current `origin/main`.
2. Migration first: drop `CarDetails.EngineType`, add `EndYear`, rebuild `TypeKey`. Update `CarDetails`, `CarDetailsInput`, `CarDetailsCommands.NormalizeTypeKey`, `CarDetailsJsonRow`, `CarDetailsJsonSeed.UpsertAsync`.
3. Add `CarDetailsCatalogueMapper` + optional `WikidataChassisLookup`. Tests above with fixtures under `WorkCosts.Tests/Fixtures/car-details/`.
4. Run `scripts/Build-CarDetailsSeed.ps1` (Wikidata on is OK for the one committed file; tests stay mocked). Commit `WorkCosts.Core/Data/car-details.json`.
5. `CarTypesPage`: remove engine boxes; add optional End year; list years. `CarsPage`: Engine code label and placeholder. Type combo without engine.
6. Update `docs/data/schema.md`, `docs/screens/car-types.md`, `docs/screens/cars.md` to this contract. Point Seq 12 / 16 at this file for the superseded key.
7. Do not: call Tiresaddict or Wikidata from `DbInitializer` or the UI; scrape FastCarCheck/mdecoder; invent engines; pay for or commit a paid dump; add a new page; change `Car.EngineType`’s column name; host a browser in a dialog.
8. Coder: `scripts/Build-CarDetailsSeed.ps1` runs `tools/BuildCarDetailsSeed` (not in `WorkCosts.slnx`). The committed `car-details.json` was built with `-SkipWikidata`; no-parens chassis is the Tiresaddict `model` line. Mapper tests use in-memory rows rather than files under `WorkCosts.Tests/Fixtures/car-details/`.
