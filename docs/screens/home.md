# Home (Work Jobs)

Purpose: plan DIY work and see savings versus garage prices.

## Regions

- Header: title “Work Jobs”, subtitle about DIY vs garage, **Add** (icon, tooltip New Work Job) trailing.  
- Body: card grid of work jobs.  
- Empty: message when none.  
- Footer band (Windows): optional **image carousel** of product photos from the library (slow marquee). Other platforms should keep a decorative photo strip if it does not hurt performance.

## Card (each work job)

- Title  
- Job template name  
- Meta (created / duration as Windows shows)  
- Trailing: DIY total vs garage, or savings figure (GBP)  
- Click → Work Job detail  

Wide: multi-column grid. Compact: one column, stack.

## Add

Creates a work job (pick template + title — follow Windows flow in `HomePage.xaml.cs`) then opens detail.

## Data

`WorkJobs` include Job + Items, newest first. SQLite cannot `ORDER BY` DateTimeOffset directly; use UTC.
