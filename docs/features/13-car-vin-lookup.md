# Feature: Car VIN lookup (mdecoder)

- **Id:** `docs/features/13-car-vin-lookup.md`
- **Seq:** 13
- **Depends-on:** `11-cars`
- **Status:** done
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/16
- **Windows:** WinUI on the Cars add sheet + editor; Core helper for fetch/poll/JSON
- **Related screens:** `docs/screens/cars.md`, `docs/screens/dialogs.md`
- **Related code:** `Car`, `CarCommands`, Add Car sheet, `ChromiumPageLoader` / `ProductImagePicker` fetch grammar (engine **not** in a ContentDialog), `VehicleOrderJson`

BMW-specific **vehicle order / equipment** from [mdecoder.com](https://www.mdecoder.com/). UK car-type / MOT is **[14-car-fastcarcheck.md](14-car-fastcarcheck.md)**. mdecoder is **VIN**-based.

## Objectives

- From the Cars add sheet or editor, run **mdecoder** on the car’s **VIN**.
- Convert the returned vehicle details to JSON and **replace** `Car.VehicleOrderJson`.
- Client **keeps state** and **re-requests every 30 seconds** until ready, cancel, or **2 minutes**.
- **Never overwrite nickname (`Name`)**. Empty required scalars may be filled from decode; already-filled make/model/model-number/year/engine need an in-sheet confirm to overwrite.
- **Out of scope:** FastCarCheck. Car-details seed. Home. Job links. ItemOfWork UI. Secrets in git.

## User requirements

- **Lookup** next to VIN. Enabled only when VIN is non-empty **and** the car looks BMW: `Make` is BMW (ignore case) **or** VIN starts with `WBA` / `WBS` / `WBY` / `5UX` / `5YM`. Otherwise status: use FastCarCheck later; do not call mdecoder.
- Status in the **sheet**: “Requesting mdecoder…”, “Waiting, retrying in 30s…”.
- Cancel stops polling; no JSON write.
- Success: **replace** `VehicleOrderJson`; keep nickname.
- Failure / timeout: message; JSON and typed fields unchanged.
- Offline: fail visibly.
- Cloudflare: HttpClient first, then Chromium. Never in a ContentDialog.

## Layout

- Lookup + status on the VIN row of Add Car / car editor. Polling stays in the sheet. Esc still uses unsaved-changes; cancel lookup ≠ discard all.

## Workflow

1. User enters VIN.
2. Lookup → first request.
3. If not ready: keep in-memory state, wait 30s, re-request. Repeat until usable, cancel, or 2 minutes (about four retries after the first).
4. Parse → JSON → replace `VehicleOrderJson`.
5. Save car as Seq 11.

Discover the exact mdecoder URL/request during implementation (planning fetch was a Cloudflare wall). No login or API key unless a fixture proves one is required — then stop and inbox it.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Column | `Car.VehicleOrderJson` | none |
| Fetch | `HttpClient`; `ChromiumPageLoader` if challenge | `MdecoderVehicleLookup` |
| Poll | — | State: VIN, started-at, attempt count, last status |
| UI | Cars add/editor | Lookup + status + optional apply-fields banner |

- **Wiring:** static helper like `ProductImageService`. No DI. No secrets in git.
- **Data:** replace entire JSON string. Shape from an implementation **fixture**, not invented here.
- **Ports:** same column; Chromium vs WebKit later.

## Tests

No live network. Fixtures: ready HTML/JSON, please-wait HTML.

- `MdecoderLookup_EmptyVin_DoesNotRequest`
- `MdecoderLookup_NonBmw_DoesNotRequest`
- `MdecoderLookup_NotReady_RetriesAfter30s` (fake clock)
- `MdecoderLookup_TimeoutAt2Minutes_LeavesJsonUnchanged`
- `MdecoderLookup_Ready_ReplacesVehicleOrderJson`
- `MdecoderLookup_DoesNotOverwriteNickname`
- `MdecoderLookup_Cancel_StopsPolling`

## Open questions

(none)

## Accepted defaults

- Seq **13**; Depends-on **`11-cars`**. Poll 30s; cap 2 minutes. BMW gate on Make or VIN prefix. Replace JSON. Nickname never overwritten. No live CI network.
- Wait/ready HTML fixtures are trimmed from documented mdecoder fields (Cloudflare blocked a live capture). JSON is `{ source, vin, productionDate, type, model, steering, engine, transmission, color, upholstery, options[] }` from the ready fixture.

## Implementation notes for an agent

Requires `11-cars` **Status** `done`.

1. Discover HttpClient vs Chromium; wait fixture + ready fixture; parser. Request URL is `GET https://www.mdecoder.com/decode/{vin}` (`MdecoderVehicleLookup.DecodeUrlFormat`).
2. Polling on the client; do not freeze the UI for 30s without status.
3. Do not: FastCarCheck, Home, ItemOfWork UI, WebView in a ContentDialog, commit cookies.
