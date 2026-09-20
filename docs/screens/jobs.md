# Jobs (templates)

Master/detail of job **templates** (not work job instances).

## Regions (regular)

- Left list (~320-class sidebar, OS width): title “Jobs”, subtitle, **Add**. Rows: name + summary (duration / garage). Empty: “No jobs yet.”  
- Right panel: name, garage price (GBP), duration, notes **MarkdownEditor**, save.

Compact: stack.

## Behaviour

Add inserts a template and selects it. Seeded jobs (Oil Service, …) are editable. Deleting a template must respect `WorkJobs` Restrict FK — block or warn if instances exist (match Windows if it already handles this).

This page does not list live work; that is Home. A Job may own **definition** `WorkJobs` (recipe subset, Core in [workjob-job-subset.md](../features/workjob-job-subset.md)); this page does **not** edit that subset until a later UI story.
