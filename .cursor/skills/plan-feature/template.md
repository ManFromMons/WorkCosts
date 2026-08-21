# Feature: &lt;Title&gt;

- **Id:** `docs/features/<kebab-case-name>.md`
- **Seq:** &lt;integer, assigned by plan-feature, never reuse&gt;
- **Depends-on:** none | `kebab-id`, `other-id`
- **Status:** draft | ready-for-agent | done
  (`blocked` / `in-progress` / `resume` / `ready-for-review` are **not** feature Status; they belong in `docs/features/to-review.md` on **main**. )
- **PR:** none | `https://github.com/…/pull/N`
- **Windows:** required first unless stated
- **Related screens:** `docs/screens/…`
- **Related code:** types you inspected

## Objectives

- …
- **Out of scope:** …

## User requirements

- …
- Empty / loading / error / cancel: …

## Layout

- Size classes: regular (side by side) vs compact (**stack**).
- Regions (header / master / detail / sheet).
- Controls: existing vs new. Primary Add stays trailing per layout grammar.
- Sheets vs dialogs: never host WebView2 inside a blocking dialog.

## Workflow

Numbered steps from trigger to saved result, including Esc / Enter and which existing helper (`DialogHelper.ConfirmYesNoAsync`, …).

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| … | e.g. `ProductPageMetadataParser.ParseHtmlAsync` | e.g. none |

- **Wiring:** how the page/control gets dependencies (existing `App.Database`, new ctor param, static helper).
- **Data:** SQLite vs files; no new BLOBs; schema/migrations if any.
- **Ports (optional):** GNOME / iPad only if this feature must mention them.

## Tests

- Project: `WorkCosts.Tests` unless UI-only.
- Cases: …

## Open questions

Each item: *Assumption:* … → **Question:** …?

## Accepted defaults

- …

## Implementation notes for an agent

1. …
2. Do not: …

Coder deviations (existing type instead of a new one) are extra numbered notes here. Do not add new spec sections for work state; that lives in `docs/features/to-review.md` on `main`. PR URL goes in the header **PR** field and in `docs/features/<kebab>-delivery.md`, not only here.
