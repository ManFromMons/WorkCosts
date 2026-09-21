# Feature: FastCarCheck (UK car-type lookup)

- **Id:** `docs/features/14-car-fastcarcheck.md`
- **Seq:** 14
- **Depends-on:** `11-cars`, `12-car-details`
- **Status:** draft
- **PR:** none
- **Windows:** WinUI on Cars add/editor; Core fetch + map into **car-details** (and car identity fields)
- **Related screens:** `docs/screens/cars.md`, `docs/screens/car-types.md` (hook: create/match types on that page)
- **Related code:** `Car`, `CarDetails`, `CarCommands`, Chromium/HttpClient fetch grammar

**Resume later.** UK source for **car-type** details (make, model, year, engine, body, MOT/mileage context). Not the BMW vehicle-order JSON ([13-car-vin-lookup.md](13-car-vin-lookup.md) / mdecoder).

Planning sample (not a confirmed field contract yet):

`https://fastcarcheck.uk/vin-check/WBANB32090B361100`

That page showed (unconfirmed scrape, **do not treat as Expected**): 2004 BMW 545, engine 4.4L V8, UK match FG04RKX, petrol, specs block, VIN breakdown. FastCarCheck is **UK-oriented**; other nations use other sources. Lookup may need **VIN and/or VRM**.

## Objectives

- Look up a car on **fastcarcheck.uk** from VIN and/or VRM.
- Use the result to **match or create `CarDetails`** and to fill the car’s type-ish fields (make, model, year, engine) — **never overwrite nickname**.
- Keep the UI responsive (status in the sheet). Use Chromium if HttpClient is blocked. No WebView in a ContentDialog.
- **Out of scope until resume:** fixtures, parser, ready-for-agent. Do not implement in the cars Seq. Do not fold this into mdecoder.

## User requirements (intent)

- Trigger from Cars add/editor (Lookup UK / FastCarCheck — label TBD).
- VIN required on the car already; VRM required too (Seq 11). Send whichever the site needs (URL pattern today is `/vin-check/{VIN}`).
- Success: update car-details binding + identity fields except nickname; user stays on the sheet.
- Failure: message; no partial type row unless we decide to create one (question on resume).
- Paid/locked “more you can’t see yet” blocks are **not** product fields.

## Layout

- Same Cars sheet/editor as Seq 11/13. Status in-sheet. Compact stack unchanged.

## Workflow

1. User has VIN (and VRM).
2. Request FastCarCheck.
3. Parse type + identity; bind `CarDetails`; fill make/model/year/engine if empty or after confirm.
4. Save car.

Exact steps on resume after a live confirmation (Cloudflare/layout may differ).

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Car | `Car`, `VehicleOrderJson` is **not** this payload | — |
| Type | `CarDetails` | Match key + insert if missing |
| Fetch | HttpClient then Chromium | `FastCarCheckLookup` |

- No secrets in git. No live network in CI once tests exist.
- Host: `fastcarcheck.uk` (and `www` if needed). Not a product `source-*` shop story.

## Tests

Named on resume (fixtures per confirmed page, like parsers). At least VIN-in-URL sample above plus two more pages when we continue.

## Open questions

Resume with the user in front of the site (same spirit as confirm-samples, but this is **not** Name + GBP):

1. Confirm what **they** see for the sample VIN (make, model, year, engine, VRM).
2. Two more URLs (another VIN / a VRM-only lookup if the site supports it).
3. Map which FastCarCheck fields become `CarDetails` vs extra JSON vs ignored (MOT, valuation, recalls).
4. HttpClient vs Chromium.

## Accepted defaults

- Seq **14**; Depends-on **`11-cars`**, **`12-car-details`**. Nickname never overwritten. mdecoder remains Seq 13. Status stays **draft** until resumed.

## Implementation notes for an agent

**Stop.** Do not implement. Do not set `ready-for-agent` without three confirmed pages and field mapping. When the user says resume this story, follow the open questions, then write fixtures/tests/parser.
