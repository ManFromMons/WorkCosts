# Delivery: Unsaved changes prompt

- **Feature:** [docs/features/unsaved-changes-prompt.md](unsaved-changes-prompt.md)
- **Seq:** 7
- **Branch:** `feature/unsaved-changes-prompt-Unsaved-changes-prompt`
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/8

## What landed

- Timed **Unsaved changes** dialog (`Save` / `Don't Save` / `Cancel`) on dirty Add Product details, job notes / unparsed duration, invalid product fields, nav, and app close. URL stage Esc still closes with no prompt.
- Core `UnsavedPrompt` timeouts: 20 s user leave, 10 s OS shutdown/logoff (`WM_QUERYENDSESSION` via `OsSessionEndListener`). Timeout acts as Save.
- `IUnsavedChangesSource` on `ProductsPage` and `MasterDetailPage`. `MainWindow` gates `AppWindow.Closing`, `NavigateTo`, and back.
- `ConfirmUnsavedWithTimeoutAsync` returns `UnsavedPromptChoice` (Save / Discard / Cancel plus `TimedOut`) so a timeout Save that fails validation discards and finishes leaving.

## Tests

- `dotnet test WorkCosts.slnx --settings .runsettings` — `WorkCosts.Tests` 111 passed (`UnsavedPromptTests` plus existing cases). Solution exit 1 is the pre-existing Package VSTest target.

## Deviations

- `ConfirmUnsavedWithTimeoutAsync` returns `UnsavedPromptChoice` (the spec’s Save / Discard / Cancel plus `TimedOut`) so a timeout Save that fails validation can discard and finish leaving.
