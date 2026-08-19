# Connection and files

## SQLite (catalogue)

Windows (unpackaged): `%LocalAppData%\WorkCosts\workcosts.db`  
Windows (MSIX): same API, redirected under the package family. Treat as a **separate** database from unpackaged F5.  
GNOME Flatpak: `$XDG_DATA_HOME/WorkCosts/workcosts.db` (sandbox: `~/.var/app/<app-id>/data/WorkCosts/`).  
iPad: app Support directory, e.g. `Application Support/WorkCosts/workcosts.db`.

Connection string (C#): `Data Source={path};Cache=Shared`. Open **short-lived** contexts; migrate off the UI thread (`DatabaseService.InitializeAsync`).

C#: `Microsoft.EntityFrameworkCore.Sqlite`.  
Swift: SQLite (GRDB or SQLite.swift) applying the same SQL as EF migrations. Do not invent a second schema.

## Blobs on disk

| Kind | Location (Windows today) | Indexed by |
| :--- | :--- | :--- |
| Page HTML | cache root / `{domain}/pages/…` | `CachedWebPages` |
| Chooser images | cache root / `{domain}/images/…` | `CachedWebImages` |
| Product library photos | **Target:** `…/WorkCosts/images/{productId}.{ext}` | Product row (path or still BLOB until migrated) |

Cache root today is beside the database folder (see `WebCacheStore` / Settings). Domain folders use the **product page host**, not the CDN host.

New platforms: never store new product photos only as SQLite BLOBs. Read legacy BLOBs if present.

## Concurrency

Single-user local app. No multi-process writers required. WAL is acceptable. Clear SQLite pools before deleting a corrupt/unmigrated file (`DatabaseService` repair path).

## Packaged vs unpackaged

Packaged installs must not silently open the unpackaged file. Export/import zip is how a user moves a library.
