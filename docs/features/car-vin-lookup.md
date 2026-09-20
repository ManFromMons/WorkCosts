# Feature: Car VIN lookup (mdecoder)

- **Id:** `docs/features/car-vin-lookup.md`
- **Seq:** 13
- **Depends-on:** `cars`
- **Status:** draft
- **PR:** none
- **Windows:** WinUI on the Cars add sheet + editor; Core helper for fetch/poll/JSON
- **Related screens:** `docs/screens/cars.md`, `docs/screens/dialogs.md`
- **Related code:** `Car`, `CarCommands`, Add Car sheet, `ChromiumPageLoader` / `ProductImagePicker` fetch grammar (engine **not** in a ContentDialog), `VehicleOrderJson`

BMW-specific **vehicle order / equipment** from [mdecoder.com](https://www.mdecoder.com/). UK “car type” / MOT / spec is **[car-fastcarcheck.md](car-fastcarcheck.md)**, not this Seq. Lookup keys may be **VIN and/or VRM** depending on the source; mdecoder is VIN-based.

## Objectives

- From the Cars add sheet or editor, run **mdecoder** on the car’s **VIN**.
- Convert the returned **vehicle details** to JSON and **replace** `Car.VehicleOrderJson` (column from Seq 11).
- mdecoder can take **~30 seconds**. The client **keeps state** and **re-requests after a wait** until the page is ready, the user cancels, or a timeout.
- **Never overwrite the nickname (`Name`)**. The user can still save it. Make / model / year / engine may be filled from the decode only if the user already has them required — do not blank them on failure.
- **Out of scope:** FastCarCheck. Car-details seed. Home. Job links. Secrets in git.

## User requirements

- Control: **Lookup** next to VIN on add sheet and detail editor. Disabled when VIN is empty.
- Status in the **sheet** (not a blocking dialog): e.g. “Requesting mdecoder…”, “Waiting, retrying in 30s…”.
- Cancel stops polling; no JSON write.
- Success: replace `VehicleOrderJson` with the new JSON; keep nickname; do not require a second confirm to store JSON (it is the lookup payload). If decode also suggests make/model/year/engine and those fields are already filled, **do not overwrite them unless the user confirms** (in-sheet banner). Empty required fields may be filled from decode so the user can save.
- Failure / timeout / non-BMW VIN: message in the sheet; `VehicleOrderJson` unchanged; typed fields unchanged.
- Offline: fail visibly.
- Cloudflare / bot check: use Chromium like other blocked hosts if HttpClient cannot load the page. Never put that WebView in a ContentDialog.

## Layout

- Lookup button + status on the Cars sheet/editor, VIN row.
- Polling UI stays in the sheet. Esc on a dirty sheet still uses unsaved-changes; cancelling lookup is not discard-all.

## Workflow

1. User enters VIN (required on the car).
2. Lookup → first request to mdecoder.
3. If the site says wait / not ready: keep in-memory state (`CarId` or add-sheet draft), wait ~30s, **re-request** with the same VIN. Repeat until HTML/JSON is usable, user cancels, or timeout (default **2 minutes** — leftover if you want a different cap).
4. Parse vehicle details → JSON object → `VehicleOrderJson` **replace**.
5. Save car as today.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Column | `Car.VehicleOrderJson` | none |
| Fetch | `HttpClient` first; `ChromiumPageLoader` if challenge (mdecoder showed Cloudflare in planning) | `MdecoderVehicleLookup` (name TBD) |
| Poll | — | State object: VIN, started-at, attempt count, last status |
| UI | Cars add/editor | Lookup + status + optional apply-fields banner |

- **Wiring:** static helper / small service like `ProductImageService`. No DI. No API keys in the repo unless you later supply one (mdecoder public site).
- **Data:** replace entire `VehicleOrderJson` string. Define the JSON shape from a **fixture** captured during implementation (not invented now). UTF-8 TEXT.
- **Ports:** same column; Chromium vs WebKit later.

## Tests

No live network in CI. Fixtures for “ready” HTML/JSON and “please wait” HTML.

- `MdecoderLookup_EmptyVin_DoesNotRequest`
- `MdecoderLookup_NotReady_RetriesAfterDelay` (fake clock / injected delay)
- `MdecoderLookup_Timeout_LeavesJsonUnchanged`
- `MdecoderLookup_Ready_ReplacesVehicleOrderJson`
- `MdecoderLookup_DoesNotOverwriteNickname`
- `MdecoderLookup_Cancel_StopsPolling`

## Open questions

1. *Assumption:* Poll every **30s**, give up after **2 minutes**. → **Question:** Different interval or cap?
2. *Assumption:* mdecoder URL is the public decoder for the VIN (discover the exact request during implementation; planning fetch was a Cloudflare wall). → **Question:** Any login, paid key, or exact URL template you already use?
3. *Assumption:* Only run this lookup for **BMW-family VINs** (or when Make is BMW); otherwise tell the user to use FastCarCheck later. → **Question:** Gate on Make/VIN WMI, or always allow the button?

## Accepted defaults

- Seq **13**; Depends-on **`cars`**. Replace JSON each success. Nickname never overwritten. FastCarCheck is a separate story. No live CI network.

## Implementation notes for an agent

Do not implement while **Status** is `draft`. Requires `cars` **done**.

1. Discover HttpClient vs Chromium on mdecoder; one wait fixture + one ready fixture; then parser.
2. Polling state on the client; do not block the UI thread with a 30s sleep without pumping status.
3. Do not: FastCarCheck, Home, WebView in a ContentDialog, commit cookies.
