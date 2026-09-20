# Feature: Car details (type catalogue)

- **Id:** `docs/features/car-details.md`
- **Seq:** 12
- **Depends-on:** `cars`
- **Status:** draft
- **PR:** none
- **Windows:** Core (seed + bindings). WinUI only if a picker is required on Cars / Jobs; otherwise bind in commands and later editors.
- **Related screens:** `docs/screens/cars.md`, `docs/screens/jobs.md`
- **Related code:** `Car`, `Job`, `GarageJob`, `ItemOfWork`, `DbInitializer`, `WorkCostsDbContext`

This is the **car-type** row that a real **`Car`**, a **`Job`**, a **`GarageJob`**, and an **`ItemOfWork`** can point at. FastCarCheck ([car-fastcarcheck.md](car-fastcarcheck.md)) is expected to **match or fill** these types. Fitment / start-work filtering is [car-job-links.md](car-job-links.md).

Still in **discussion** — grain of a type, seed source, and whether jobs are many-to-many. Do not mark `ready-for-agent` until the questions below are answered.

## Objectives

- Persist **`CarDetails`**: a reusable make/model/year/engine-style **type**, distinct from a user’s **`Car`** (nickname, VRM, VIN, photo, `VehicleOrderJson`).
- **Seed** rows at migration / `DbInitializer` (stable GUIDs), same idea as seeded jobs/categories.
- Bind:
  - **`Car`** → optional or required `CarDetailsId` (question)
  - **`Job`** and **`GarageJob`** → the types they apply to
  - **`ItemOfWork`** → `CarId` (already Seq 11) **and** `CarDetailsId`
- **Out of scope:** Cars CRUD UI, mdecoder `VehicleOrderJson`, FastCarCheck HTTP, Home rewrite. Do not scrape a live catalogue in this Seq unless a question says the seed is an import.

## User requirements

- A car-details row is **not** a vehicle the user owns. It is “2004 BMW 545, 4.4L V8” (shape TBD).
- User cars still have their own Make/Model/Year/EngineType (Seq 11). Binding to car-details **links** that instance to the type; it does not replace the nickname/VRM/VIN.
- Jobs / garage jobs use the same type to say “this work fits these cars” (exact cardinality TBD).
- Completions (`ItemOfWork`) store which car **and** which type was in force (so history survives if the car’s type link changes later).
- Empty seed: still a valid database; bindings null until chosen — unless seed is mandatory (question).

## Layout

- No new nav destination unless we add a small Stuff editor (question). Default: **no Cars-like page** in this Seq; types are data Jobs/Cars pickers consume later.
- If a picker is needed on the car editor: combo/search of seeded types, compact stack still applies. No WebView.

## Workflow

1. App migrates / initializes; seed car-details exist.
2. Creating or editing a car may set `CarDetailsId` (when we decide it is required).
3. Job / garage-job editors (later or this Seq) attach one or more types.
4. Logging `ItemOfWork` copies `CarId` and the type id (from the car’s binding or an explicit snapshot — question).

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Seed | `DbInitializer` stable GUIDs | `CarDetails` table + seed rows |
| Car | `Car` | `CarDetailsId` FK Restrict |
| Job / GarageJob | `Job`, `GarageJob` | FK or junction (question) |
| Completion | `ItemOfWork` | `CarDetailsId` FK Restrict |
| FastCarCheck later | — | match on make/model/year/engine keys |

- **Wiring:** commands + initializer. No DI container.
- **Data:** add-only migration. No cascade from `CarDetails` to cars/jobs (Restrict). Soft-deleted cars keep their type FK.
- **Ports:** EF canonical; Swift follows.

### Schema (draft — grain is an open question)

**`CarDetails`** (names TBD)

| Column | Intent |
| :--- | :--- |
| `Id` | Guid PK, stable for seed |
| Make, Model, Year, EngineType, … | Type identity |
| Maybe body, fuel, doors | Only if we keep them |

## Tests

- `DbInitializer_SeedsCarDetails_StableIds`
- `CarCommands_Update_PersistsCarDetailsId`
- `ItemOfWork_PersistsCarDetailsId`
- Job/garage-job binding tests once cardinality is decided
- Unknown type id → no write

## Open questions

1. *Assumption:* One type row is **Make + Model + Model year + EngineType** (free text engine). → **Question:** Is that the grain, or do we also key body/fuel/generation? What is a unique type?
2. *Assumption:* Seed is a **small built-in list** (the user’s cars’ types can be added when FastCarCheck runs later). → **Question:** What should the v1 seed contain — nothing but the table, a handful of common types, or a large catalogue?
3. *Assumption:* **`Car.CarDetailsId`** is optional in schema, required in the Cars UI once types exist. Seq 11 cars already have denormalized make/model/year/engine. → **Question:** Required bind, optional bind, or drop denormalized fields later?
4. *Assumption:* A **`Job`** applies to **many** types (junction `JobCarDetails`). A **`GarageJob`** is for one car (Seq 11/15) and also stores **`CarDetailsId`** snapshot for that car’s type. → **Question:** Job = many types (junction) vs one type? Garage job = snapshot of the car’s type, or its own list?
5. *Assumption:* **`ItemOfWork.CarDetailsId`** is copied from the car’s binding at completion time (snapshot), not a live join. → **Question:** Snapshot or always follow the car?
6. *Assumption:* **No Stuff page** for types in this Seq (seed + FKs only). → **Question:** Do you want a master/detail to edit the catalogue by hand?

## Accepted defaults

- Seq **12**; Depends-on **`cars`**. Restrict FKs; no cascade from type to cars. FastCarCheck is a different Seq. Home not in scope.

## Implementation notes for an agent

Do not implement while **Status** is `draft`.

1. After answers: rewrite grain, seed, and cardinality; then `ready-for-agent`.
2. Migration + `DbInitializer` seeds. `docs/data/schema.md`.
3. Do not: VIN HTTP; Home; invent a huge unconfirmed catalogue.
