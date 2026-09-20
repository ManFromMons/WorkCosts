# Feature: Car-details seed (in repo)

- **Id:** `docs/features/car-details-seed.md`
- **Seq:** 16
- **Depends-on:** `car-details`
- **Status:** draft
- **PR:** none
- **Windows:** `DbInitializer` / migration data file in the repo; Car types page already exists
- **Related screens:** `docs/screens/car-types.md`
- **Related code:** `CarDetails`, `DbInitializer`, `CarDetailsCommands`

**Resume later.** Seq 12 ships an **empty** `CarDetails` table and a Stuff page. This story **encodes a type catalogue in project source** and loads it on initialize (stable GUIDs, insert-if-missing like seeded jobs).

FastCarCheck ([car-fastcarcheck.md](car-fastcarcheck.md)) may also create/match types at runtime; that is a different Seq. This one is the **checked-in** list.

## Objectives

- Store car types (Make, Model, **ModelNumber**, Year, EngineType) as **source in the repo** (JSON, CSV, or similar — question).
- `DbInitializer` upserts by stable id (and/or unique key Make+ModelNumber+Year+EngineType). Do not wipe user-added types.
- Car types page shows the seeded rows after first launch / migrate.
- **Out of scope:** Scraping FastCarCheck or mdecoder to build the file. Home. Changing the unique key.

## User requirements

- After install, Car types is no longer empty if the file has rows.
- User-added types with a different key remain.
- Re-running seed does not duplicate keys.

## Layout

- No new page. Uses Stuff → Car types.

## Workflow

1. Ship data file next to Core / initializer.
2. Initialize → upsert.
3. User opens Car types.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Table / page | Seq 12 | none |
| Data file | `DbInitializer` pattern | catalogue file in repo |
| Ids | stable GUIDs like jobs/categories | one Guid per type row |

## Tests

- `DbInitializer_UpsertsCarDetailsFromRepoFile`
- `DbInitializer_DoesNotDuplicateUniqueKey`
- `DbInitializer_KeepsUserAddedTypes`

Exact cases after the file format is chosen.

## Open questions

1. *Assumption:* One JSON file under `WorkCosts.Core/Data/` (or `docs/data/`) with Guid, Make, Model, ModelNumber, Year, EngineType. → **Question:** JSON vs CSV vs C# array, and where should the file live?
2. *Assumption:* First catalogue is **small** (the makes you care about), not every car ever. → **Question:** Who supplies the first list, and roughly how many rows?

## Accepted defaults

- Seq **16**; Depends-on **`car-details`**. Empty table until this lands. Do not delete user types. Status stays **draft** until the file format and first list are decided.

## Implementation notes for an agent

**Stop** until the user resumes this story with a format and a list. Do not invent a large unconfirmed catalogue. Hook onto the existing Car types page only.
