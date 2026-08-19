# Schema

Canonical definition: `WorkCosts.Core/Data/WorkCostsDbContext.cs` and `WorkCosts.Core/Data/Migrations/`. Swift and any other store **follow these tables and migrations**. Do not rename columns in a port.

GUIDs are the primary keys (string/blob in SQLite). Money is `decimal(18,2)` displayed as GBP.

## Tables

### Categories

| Column | Notes |
| :--- | :--- |
| Id | Guid. Seeded: Tools, Garage, Consumables, Parts (`DbInitializer` fixed ids) |
| Name | Required, max 120, **unique** |

### Jobs (templates)

| Column | Notes |
| :--- | :--- |
| Id | Guid. Seeded templates (Air-Con, All, Brake Pads, …) |
| Name | Required, max 200 |
| GaragePrice | GBP |
| NotesMarkdown | Max 8000 |
| DurationMinutes | Whole minutes |

### Products

| Column | Notes |
| :--- | :--- |
| Id | Guid |
| Name | Required, max 200 |
| UnitCost | GBP |
| Vendor | Seller as scraped/edited |
| Source | Marketplace/host family (Amazon, Autodoc, or derived from URL host) |
| Manufacturer, ManufacturerReference, Ean, Variation, OemEquivalent | Identity fields |
| Url | Max 2000; Amazon URLs normalize to `/dp/{ASIN}` |
| PricePoint | One of Low, Medium-low, Medium-high, OEM, OEM+, High |
| ImageBlob / ImageContentType | **Legacy**. New writes store files; keep columns until a migration drops them |
| CategoryId | FK, Restrict delete |
| IsAllJobs | Available on every work job (“G” badge) |

### ProductJobs

Composite key `(ProductId, JobId)`. Cascade from either side.

### ProductEquivalents

Composite key `(ProductId, EquivalentProductId)`. Check: not self. Cascade. Treat as undirected in the UI (store one or both directions consistently with Windows).

### WorkJobs

| Column | Notes |
| :--- | :--- |
| Id | Guid |
| JobId | FK to Jobs, Restrict |
| Title | Required |
| CreatedAt | DateTimeOffset. SQLite: sort via `UtcDateTime` |

### WorkJobItems

| Column | Notes |
| :--- | :--- |
| Id | Guid |
| WorkJobId | Cascade |
| ProductId | Restrict |
| Quantity | short, default 1 |
| UnitCostSnapshot | GBP at add time |
| Unique | `(WorkJobId, ProductId)` |

Deleting a product (`ProductCommands.DeleteAsync`) removes its work-job lines, job links, and equivalent rows, then the product.

### CachedWebPages / CachedWebImages

Index only. Bytes live on disk (`WebCacheStore`). Unique `PageUrl`; unique `(PageUrl, ImageUrl)` for images. `RelativePath` is under the cache root.

## Seed (first launch, all platforms)

Categories: Tools, Garage, Consumables, Parts.  
Jobs: Air-Con, All, Brake Pads, Brake Rotors, Oil Service, Coolant Service, Suspension — prices and durations as in `DbInitializer`. Insert if id **or** name missing.
