# Products

Catalogue of tools, parts, consumables, garage supplies.

## Regions (regular)

- Top-left: title, subtitle, **Add**.  
- Top: job/category **filter strip** (must not be clipped).  
- Left (~40%): two groups — products for the filtered job(s), and “all jobs” products. Each row: 56px thumbnail, name, detail line, **G** badge if `IsAllJobs`.  
- Right (~60%): `ProductEditor` or assignment panel or empty “select a product”.  
- Overlay/sheet: Add Product (see below).

Compact: **stack** — list, then push editor. Add still a **sheet**.

## Filters

Selecting jobs in the strip filters the list. Mirror Categories page semantics where they overlap: multiple job filters = union; none = no extra job filter (show catalogue per Windows).

## Detail editor

`ProductEditor`: image 118px square; fetch-from-URL and clear; open-in-browser. Fields from the page (name, manufacturer, refs, EAN, variation, OEM, URL, source/vendor, unit cost) and app fields (category, price point, is-all-jobs, job assignments, equivalents). Save/delete on the editor.

Delete: confirm Yes/No, then `ProductCommands.DeleteAsync`.

## Add Product sheet

1. URL stage (coerce `https://` if missing). Enter/Add opens **details immediately**, then loads the page **in the sheet** (browser engine not in a nested dialog).  
2. Status text while fetching. Auto-use first image when possible.  
3. If URL already exists: **in-sheet banner** Overwrite / Keep existing (read-only) / Cancel — not a second modal.  
4. Planned: **Paste HTML** ([docs/features/paste-html.md](../features/paste-html.md)).  
5. Esc closes; do not steal Enter from confirmations.

Top-right Products **Add** while the sheet is open **continues** from the current URL (does not wipe the form).
