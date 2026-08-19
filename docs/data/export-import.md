# Export and import

**Status: spec only.** Do not implement on Windows until a later branch. GNOME and iPad must keep this format in mind.

## Container

One **zip** file, suggested name `WillIDIY-backup-yyyyMMdd.zip`.

```
manifest.xml
catalog.xml
images/{productId}.jpg   (library photos)
cache/{domain}/pages/…
cache/{domain}/images/…
```

No raw `.db` as the interchange. SQLite is the runtime store; the zip is the portable package.

## manifest.xml

- Format version (start at `1`)
- App display name, source platform, exported-at UTC
- Schema / last EF migration id

## catalog.xml

XML dump of the relational catalogue (categories, jobs, products, product-jobs, equivalents, work jobs, work-job items). Use stable element names matching CLR properties. Include product `Id` values so images can match. Currency is GBP; do not localize numbers in XML (invariant `decimal` with `.`).

Markdown in notes: preserve as text (CDATA or escaped).

## Merge import

Never wipe the destination by default.

| Entity | Match key | On match | On miss |
| :--- | :--- | :--- | :--- |
| Category | `Id` then `Name` | Keep dest; remap incoming FKs | Insert |
| Job | `Id` then `Name` | Keep dest (do not overwrite garage price unless empty) | Insert |
| Product | Normalized `Url`, else Amazon ASIN, else `Id` | Merge: fill blank dest fields from incoming; keep dest costs if set; union job links and equivalents | Insert |
| WorkJob | `Id` | Skip duplicate id; do not clone | Insert with remapped JobId/ProductIds |
| Cache files | `PageUrl` / image URL | Keep newer `CachedAtUtc` | Copy file + index row |

If both libraries have the same product URL with different unit costs, **keep destination cost**, copy missing identity fields and photo if dest has none.

Show a summary after import: inserted / merged / skipped.

## Images and cache

Include library photos and the page cache in the zip. Clearing cache in Settings must not appear in a later export.

## Security

XML and HTML in the zip are untrusted. Parse with a non-validating XML reader; do not execute HTML. Images: sniff content-type, reject huge files.
