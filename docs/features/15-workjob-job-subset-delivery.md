# Delivery: Work-job subset of a Job (Core)

- **Feature:** [docs/features/15-workjob-job-subset.md](15-workjob-job-subset.md)
- **Seq:** 15
- **Branch:** `feature/15-workjob-job-subset-Work-job-subset`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/17

## What landed

- `WorkJobs` gained `IsDefinition` (default false) and `SortOrder` (default 0). Migration `20260921233755_AddWorkJobDefinition`. Existing Home Add rows stay instances.
- `WorkJobCommands` creates / lists / updates / deletes Job definition work jobs and their line items. Unknown job, blank title, unknown product, qty &lt; 1, or a duplicate product does not write. Deleting a definition removes its items and leaves copied instances.
- `CopyDefinitionsToInstancesAsync` clones all definitions or a chosen subset (fail the whole call on mixed, unknown, instance, duplicate, or foreign-job ids). Copies get new ids, `IsDefinition = false`, `SortOrder` 0, `CarId` null, and cloned items (skip a line if the product is gone). Empty definition set returns an empty list.
- Home lists instances only (`!IsDefinition`). No new pages, Jobs editor, or Home Add change.

## Tests

- `dotnet test WorkCosts.Tests/WorkCosts.Tests.csproj --settings .runsettings` — 203 passed, including the named `Definition_*`, `DefinitionItem_*`, `Copy_*`, and `InstanceList_OmitsDefinitions` cases.
- `dotnet build WorkCosts/WorkCosts.csproj` — succeeded.

## Deviations

- Extended the existing `WorkJobCommands` type (it already had `TrySetCarIdAsync` from Seq 11) instead of adding a second command class.
- SQLite cannot `ORDER BY CreatedAt.UtcDateTime` together with the `!IsDefinition` filter. Home and `ListInstancesAsync` load then sort in memory, same idea as ItemOfWork.
