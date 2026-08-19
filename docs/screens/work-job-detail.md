# Work Job detail

Opened from a Home card. Back returns to Home.

## Regions (regular)

- Main column: title, subtitle (template + duration/garage), category filter + **Add product**, line list.  
- Trailing column (~sidebar): duration, garage price, DIY total, **saving vs garage** (large GBP), and a **read-only** notes preview from the job template.

Compact: **stack** — lines first, totals/notes below or a second screen.

## Line list

Header row: product, qty, unit, line total. Each row is a product on this work job (`WorkJobItems`, unique product per work job). Quantity editable; unit cost is the **snapshot** from add time.

**Add product** offers catalogue rows that match the work job’s template (via `ProductJobs`) plus `IsAllJobs` products, optionally filtered by category. Do not reopen the global Add Product overlay unless the user is creating a new catalogue item (that path was tried on Windows and rolled back — keep catalogue add on Products).

## Totals

GBP. Savings = job.GaragePrice − sum(qty × snapshot). Show clearly when DIY is cheaper or not.

## Notes

Job template markdown is **previewed** here. Editing belongs on the **Jobs** page (`MarkdownEditor`). Do not add a second editor on this screen unless a later spec says so.
