# Feature: Work-job subset of a Job (Core)

- **Id:** `docs/features/15-workjob-job-subset.md`
- **Seq:** 15
- **Depends-on:** none
- **Status:** ready-for-agent
- **PR:** none
- **Windows:** Core + tests. **No new UI.** One existing-query filter on Home so definition rows never appear as cards.
- **Related screens:** none new. `docs/screens/home.md` — cards remain **instance** work jobs. `docs/screens/jobs.md` — no definition editor on this Seq.
- **Related code:** `WorkJob`, `WorkJobItem`, `Job`, `WorkCostsDbContext`, `HomePage.LoadAsync`

A **`WorkJob` is part of a `Job`** (`JobId` on `WorkJobs`). It is **not** part of a **`GarageJob`**. Do **not** add `GarageJobId`.

**GarageJob later collates work items** (the copied instance set) and builds behaviour around that set. That collation, start-work chrome, and ItemOfWork logging are **not this Seq** ([17-item-of-work-ui.md](17-item-of-work-ui.md) — refine later).

This Seq **replaces** the withdrawn draft `car-job-links.md` (do not recreate it).

## Objectives

- Some `WorkJobs` are the **definition subset** for a `Job`: the recipe that job entails (title + optional `WorkJobItems`).
- **Core CRUD** for that subset (definitions and their line items). No Jobs-page or Home chrome for it.
- **Copy:** select a `Job`, then copy **all** of its definition work jobs or a **chosen subset**. Each copy is a new **instance** `WorkJob` (new ids, cloned items). Those instances are the **actual work required to do the job**.
- **Out of scope:** New pages, sheets, or start-work UI. `GarageJob` collation. ItemOfWork UI. VIN. Cars. `GarageJobId` on `WorkJob`. Seeding definitions. Changing Home Add.

## User requirements

Library behaviour (commands + tests). No new controls.

### Two kinds (same `WorkJobs` table)

| Kind | `IsDefinition` | Role |
| :--- | :--- | :--- |
| **Definition** | `true` | Subset on a `Job`. Recipe. **Not** a Home card. |
| **Instance** | `false` | Actual work. Existing rows stay `false`. Home lists these only. |

`SortOrder` (`int`, default 0) orders definitions on a job. Instances leave 0.

### Definition CRUD

Static `WorkJobCommands` (new Core file; none exists today — Home creates instances inline). Pattern: `GarageJobCommands` / `ItemOfWorkCommands`. `CreateContext()`. No DI.

- `CreateDefinitionAsync(db, jobId, title)` — unknown job → null, no write. Title required (trim, max 200). `IsDefinition = true`. `SortOrder` = max existing definition sort for that job + 1 (or 0 if none). New id, `CreatedAt` now.
- `ListDefinitionsAsync(db, jobId)` — definitions only; include items; `SortOrder` ascending then `Id`.
- `UpdateDefinitionAsync(db, definitionId, title?, sortOrder?)` — unknown or not a definition → null, no write. Title if passed: same trim/max rules.
- `TryDeleteDefinitionAsync(db, definitionId)` — removes that definition and its **`WorkJobItems`** (cascade already). Does **not** delete instance copies (independent clones). Unknown or not a definition → NotFound.

### Definition line items

Same `WorkJobItems` table (product, qty, snapshot). Unique `(WorkJobId, ProductId)` as today. Definitions may have zero items.

- `AddDefinitionItemAsync(db, definitionId, productId, quantity)` — definition must exist (`IsDefinition`). Product must exist. Quantity ≥ 1. Snapshot = product `UnitCost` at add. Duplicate product on that definition → null, no write. Unknown definition/product → null, no write.
- `UpdateDefinitionItemQuantityAsync(db, itemId, quantity)` — item must belong to a definition. Quantity ≥ 1. Else null, no write.
- `RemoveDefinitionItemAsync(db, itemId)` — item must belong to a definition. Else NotFound.

Do **not** route instance line edits through these methods (Home/detail keep doing that).

### Copy (actual work)

`CopyDefinitionsToInstancesAsync(db, jobId, IReadOnlyList<Guid>? definitionIds = null)`:

- Unknown job → null, no write.
- `definitionIds` null or empty → copy **all** definitions for that job (`SortOrder`, then `Id`).
- `definitionIds` set → copy **that subset** in the **given list order**; every id must be a **definition** with that `JobId` or **fail the whole call** (return null, no partial write). Duplicates in the list: fail the whole call.
- Each copy: new work-job id, `IsDefinition = false`, same `JobId`, same title, `CreatedAt` now, `SortOrder` 0, **cloned items** (new item ids, same `ProductId` / qty / snapshot). Skip a line if the product no longer exists.
- Do not add or set `GarageJobId`. `CarId`: leave **null** if the column exists (Seq 11); this Seq does not require a car.
- Return the new instances in copy order, items included.

Empty definition set → empty list (not null), no throw.

This command is the Core primitive later UI will call: **pick a Job → copy its WorkJob subset → that set is the work to do.** GarageJob will later **collate those instances** (and/or a future work-item type) — not here.

### Home (only UI-adjacent change)

- `HomePage.LoadAsync`: add `.Where(w => !w.IsDefinition)` on the existing `db.WorkJobs` query. No new Add flow, no copy button, no definition editor.
- Existing Home Add still creates instance rows (`IsDefinition` defaults false). Do not change that dialog.
- Work Job detail is unchanged (opened from instance cards).

### Empty / error

- Missing required title on create/update definition: no write.
- Copy with mixed, unknown, instance, or foreign-job ids: no write.
- Quantity &lt; 1 on item add/update: no write.

## Layout

- No new layout. Home cards unchanged aside from hiding definitions.
- Jobs page does **not** gain a work-job subset editor.

## Workflow

1. Tests (and later UI) create definition work jobs on a Job, with optional items.
2. `CopyDefinitionsToInstancesAsync(job, subset?)` clones them to instances.
3. Home shows instances as today.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Instance work | `WorkJob`, `WorkJobItem`, existing Home create | `IsDefinition` bit default false; `SortOrder` int default 0 |
| Commands | `ItemOfWorkCommands` style | `WorkJobCommands` (definition CRUD, items, copy) |
| Home query | `HomePage.LoadAsync` Include Job + Items, UTC sort | `Where(w => !w.IsDefinition)` only |

- **Wiring:** static commands, `CreateContext()`. No DI.
- **Data:** add-only migration. No `GarageJobId`. Job delete **Restrict** while any work jobs exist (definitions or instances), same as today (`MasterDetailPage` already checks `WorkJobs.Any`).
- **Ports:** EF canonical.

## Tests

`WorkCosts.Tests`, temp SQLite. No UI automation. No network.

- `Definition_CreateListUpdateDelete_UnknownJob_NoWrite`
- `Definition_Create_AssignsSortOrder_AfterExisting`
- `Definition_Delete_RemovesItems_LeavesCopiedInstances`
- `DefinitionItem_AddUpdateRemove_UnknownProduct_NoWrite`
- `DefinitionItem_DuplicateProduct_NoWrite`
- `Copy_AllDefinitions_ClonesTitleAndItems_NewIds`
- `Copy_Subset_OnlySelected_InGivenOrder`
- `Copy_Subset_ForeignOrInstanceId_NoWrite`
- `Copy_EmptyDefinitions_EmptyResult`
- `Copy_SkipsItem_WhenProductMissing`
- `InstanceList_OmitsDefinitions` (query or `ListInstancesAsync` helper if you add one; Home filter is the product requirement)

## Open questions

(none)

## Accepted defaults

- Seq **15**; Depends-on **`none`**. Same `WorkJobs` table + `IsDefinition`. Independent clones. No garage-job FK. No new UI. Optional subset ids on copy. `CarId` not set by copy. Home Add unchanged.

## Implementation notes for an agent

1. Migration: `WorkJobs.IsDefinition` default false, `WorkJobs.SortOrder` default 0.
2. `WorkJobCommands` already has `TrySetCarIdAsync` (Seq 11). Add definition CRUD + items + copy on that type. Filter Home’s existing list query (`!IsDefinition`). SQLite cannot `ORDER BY CreatedAt.UtcDateTime` together with that filter; Home and `ListInstancesAsync` load then sort in memory (same idea as ItemOfWork).
3. `docs/data/schema.md`. Do not invent `GarageJobId`.
4. Do not: new pages, Jobs-page subset editor, ItemOfWork UI, VIN, seed definitions, garage-job collation, Home Add rewrite.
