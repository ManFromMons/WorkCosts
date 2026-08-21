# Feature: Unsaved changes prompt

- **Id:** `docs/features/unsaved-changes-prompt.md`
- **Seq:** 7
- **Depends-on:** none
- **Status:** ready-for-agent
- **PR:** none
- **Windows:** required first
- **Related screens:** `docs/screens/dialogs.md`, `docs/screens/products.md`, `docs/screens/jobs.md`, `docs/screens/shell.md`
- **Related code:** `DialogHelper`, `MainWindow` (`Closed` only today; `NavigateTo`, `TitleBar_BackRequested`), `AppWindow`, `ProductsPage` (`TryDiscardAddOverlayAsync`, `AddOverlay`, `PersistDetailAsync`, `SaveNewProductAsync`, `SaveViewExistingAsync`), `ProductAddEditor` (`IsDirty`, `TryRead`), `ProductEditor` (`TryRead`, `ValuesChanged`), `MasterDetailPage` (`IsDirty`, `Save_Click`, `PersistCoreFieldsAsync`, `JobsList_SelectionChanged`)

## Objectives

- If the user leaves a **dirty** Add Product sheet or a **dirty** details panel, or closes the **app**, show a short confirm dialog: **Save** / **Don't Save** / **Cancel**.
- Timeout then **Save** and continue leaving, so Windows is not blocked: **20 seconds** for user close / Esc / nav / selection; **10 seconds** when Windows is shutting down or logging off.
- Keep saving as you type where that already happens (product details, job name/price/duration, work-job quantities). Do not turn those into explicit-save editors.
- **Out of scope:** GNOME and iPad (Ports only). Categories chip add/rename. Fetch/parse. Zip export. New pages. WebView2 in this dialog. Delete Yes/No and other existing confirms. Settings. Home / Work Job “Add product to plan” dialogs (they already have Add/Cancel). Add Product **URL stage** (typed URL, details not open) — Esc still closes with no prompt.

## User requirements

- **Dirty** = on-screen values not yet written to SQLite, or Add Product details still not added. After an auto-persist completes, that surface is not dirty. Close / leave must **await in-flight persist** instead of prompting.
- Prompt when all are true: the surface is dirty; the user is leaving it; no other `ContentDialog` is open (`DialogHelper.HasOpenDialog` — wait, do not stack).
- Leaves that use this dialog: Esc / Cancel on a dirty sheet; closing a dirty details panel (including compact back); selecting another job while job details are dirty; switching nav (e.g. Products → Home) while the current page is dirty; title-bar Close / Alt+F4 / OS shutdown or logoff.
- Dialog: title **Unsaved changes**; body **Save your changes before closing?** Static `TextBlock`, not a text box. Buttons: **Save** (primary, default, Enter), **Don't Save** (secondary), **Cancel** (close, Esc). Save label shows remaining seconds: `Save (20)` or `Save (10)` down to `Save (1)`.
- **Save:** persist with that surface’s existing save method, then complete the leave. **Don't Save:** discard the buffer, then complete the leave. **Cancel:** abort; sheet, editor, navigation, and window stay as they were.
- Timeouts: user-initiated leave **20 s**; OS shutdown/logoff **10 s**. At 0 the dialog acts as **Save**, then continues the leave. Don't Save and Cancel still work until then.
- Add Product sheet: replace `TryDiscardAddOverlayAsync`’s Yes/No discard with this dialog. Prompt when **details** are visible for a **new** product, or `_addViewExisting && AddEditor.IsDirty`. Save → `SaveNewProductAsync` or `SaveViewExistingAsync`. Don't Save → `CloseAddOverlayAsync` without writing. URL stage and collision banner (`_existingChoice`) unchanged (no this prompt).
- Job details: name / price / duration stay auto-persisted. Notes (`MarkdownEditor`) and a duration string that has not parsed/persisted are dirty. Save → existing `Save_Click` persist. Don't Save → reload the selected job’s last saved values (notes, duration text).
- Product details: keep `ValuesChanged` → `PersistDetailAsync`. Await in-flight persist on leave. Prompt only when `TryRead` currently fails. User-clicked Save → existing validation message, **stay**. Timeout Save or Don't Save → reload last saved product, then proceed.
- Work Job quantities: keep auto-persist. No extra prompt. If “Add product to plan” is open during app close, do not stack; wait until `HasOpenDialog` is false, then check dirty (usually none).
- Empty / not dirty: leave immediately, no dialog.
- Save **click** + validation or DB error: existing message; stay; do not close the window.
- Timeout Save + validation or DB error during a leave that must finish (especially **app close** / shutdown): **discard** and continue leaving. Do not re-open the dialog.
- Keyboard: Enter = Save; Esc = Cancel. Follow `docs/screens/dialogs.md` (no text box in the body).

## Layout

- Size classes unchanged: regular list beside detail; compact **stack**. No new page.
- Regions: Add Product **sheet**; Jobs and Products **detail** panels; app chrome close. Not Categories chips.
- Controls: existing `ContentDialog` via a new `DialogHelper` method. No new user control. Primary Add stays trailing. Never host WebView2 in this dialog.
- This is a short confirmation, not a replacement for the Add Product sheet.

## Workflow

1. User edits a buffered surface, or tries to leave (Esc, Cancel, other job, compact back, nav, window close, shutdown).
2. Current page’s try-leave: if `HasOpenDialog`, return (do not stack). Await in-flight auto-persist. If not dirty, complete the leave.
3. If dirty: `DialogHelper.ConfirmUnsavedWithTimeoutAsync(xamlRoot, timeout)` with 20 s or 10 s as above.
4. Save → existing save method → on success complete leave; on click-failure stay; on timeout-failure discard and complete leave.
5. Don't Save → discard → complete leave.
6. Cancel / Esc on the dialog → abort leave.
7. Timeout → same as Save, then complete leave.

Window close: handle `AppWindow.Closing` (not `Window.Closed`). If not yet confirmed, `args.Cancel = true`, run try-leave, then set `_allowClose` and `Close()` only after Save / Don't Save / timeout Save. `Closed` still unsubscribes theme handlers after a real close.

OS shutdown/logoff: set a flag from `WM_QUERYENDSESSION` (HWND subclass on the main window) and/or `Microsoft.Win32.SystemEvents.SessionEnding`. While that flag is set, use the **10 s** timeout. User Close / Alt+F4 without that flag uses **20 s**.

Navigation: `MainWindow.NavigateTo` / back must call try-leave on `NavFrame.Content` and abort nav if Cancel. Jobs `JobsList_SelectionChanged` must not switch the editor until try-leave succeeds (revert selection on Cancel).

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Countdown contract | none | `UnsavedPrompt` in **Core** (no WinUI types): `UserLeaveTimeout = TimeSpan.FromSeconds(20)`, `ShutdownTimeout = TimeSpan.FromSeconds(10)`, `IsTimedOutSave(TimeSpan elapsed, TimeSpan timeout)` true when `elapsed >= timeout`. |
| Dialog | `DialogHelper.ShowAsync`, `HasOpenDialog`, `TextBlock` body like `ConfirmYesNoAsync` | `UnsavedPromptResult` { Save, Discard, Cancel } in the WinUI project (or next to the helper). `DialogHelper.ConfirmUnsavedWithTimeoutAsync(XamlRoot, TimeSpan timeout)`: Primary Save with countdown label, Secondary Don't Save, Close Cancel, Default Primary. `DispatcherQueueTimer` 1 s ticks; at 0 `Hide()` as Save. |
| Page API | `ProductAddEditor.IsDirty`, `MasterDetailPage.IsDirty()`, existing save methods | `IUnsavedChangesSource` on WinUI pages only: `bool HasUnsavedChanges { get; }`, `Task FlushPendingAsync()` (await in-flight auto-persist), `Task<bool> SaveUnsavedAsync()` (true = saved or nothing to save; false = validation, stay), `Task DiscardUnsavedAsync()`. Implement on `ProductsPage` and `MasterDetailPage`. **Not** `CategoriesPage`. No DI container. |
| App close / shutdown | `MainWindow`, `AppWindow.Closing`, `WindowNative.GetWindowHandle` | `_allowClose`, `_osSessionEnding`. Subclass HWND for `WM_QUERYENDSESSION` (and treat `SessionEnding` as the same flag if it fires). Pass `UnsavedPrompt.ShutdownTimeout` vs `UserLeaveTimeout` into the helper. |
| Add Product | `TryDiscardAddOverlayAsync`, `SaveNewProductAsync`, `SaveViewExistingAsync`, `CloseAddOverlayAsync` | Replace Yes/No discard with the timed dialog. |
| Jobs | `IsDirty()`, `Save_Click`, `JobsList_SelectionChanged` | Gate selection change and page leave. |
| Products detail | `PersistDetailAsync`, `TryRead` | Await in-flight persist; prompt only when `TryRead` fails. |
| Docs | `docs/screens/dialogs.md`, sheet Esc sentence on products | Subsection **Unsaved changes**. Dirty sheet Esc **prompts**; URL stage still closes with no prompt. |

- **Wiring:** static `DialogHelper`. Pages keep `App.Database.CreateContext()`. Timer on the dialog dispatcher. MainWindow asks `NavFrame.Content as IUnsavedChangesSource`.
- **Data:** none. No migrations, no BLOBs.
- **Ports:** later GNOME `AdwAlertDialog` / iPad `confirmationDialog` with the same 20 s / 10 s Save defaults. Not this story.

## Tests

- Project: `WorkCosts.Tests` (no WinUI).
- `UnsavedPromptTests.UserLeaveTimeout_is_20_seconds`
- `UnsavedPromptTests.ShutdownTimeout_is_10_seconds`
- `UnsavedPromptTests.Elapsed_under_timeout_is_not_timed_out_save`
- `UnsavedPromptTests.Elapsed_equal_or_over_timeout_is_timed_out_save` (assert both 20 s and 10 s)
- UI-only (manual): dirty Add Product Esc → dialog; Save adds product; Don't Save closes sheet; Cancel keeps sheet; wait 20 s → saved and sheet closes; Alt+F4 with dirty job notes → 20 s dialog; Cancel keeps window; timeout Save then window closes. URL stage Esc still silent. No live network tests.

## Open questions

_(none)_

## Accepted defaults

- Seq 7; Depends-on none; Windows first.
- Keep auto-save as you type. Prompt only buffered UI: Add Product details (and dirty view-existing assignments), job notes / unparsed duration, invalid product fields that never persisted.
- Timeout always **Save** then close, not Don't Save.
- Buttons Save / Don't Save / Cancel.
- Timeout Save that cannot validate on app close / shutdown: **discard** and quit.
- Add Product URL stage: no prompt.
- Same dialog for job selection, nav, compact back, Esc/Cancel, and app close.
- Ignore Categories chip editor.
- Body is a `TextBlock`. Countdown constants live in Core so tests do not need WinUI.
- One body string for all leaves. Do not invent a second Add Product path.

## Implementation notes for an agent

1. Add `UnsavedPrompt` in Core and the xUnit cases. Then `DialogHelper.ConfirmUnsavedWithTimeoutAsync`.
2. Add `IUnsavedChangesSource` on `ProductsPage` and `MasterDetailPage`. `MainWindow`: `AppWindow.Closing`, shutdown flag, try-leave before `NavigateTo` / back.
3. Replace `TryDiscardAddOverlayAsync` Yes/No with the timed dialog; Save must call the same methods the sheet already uses.
4. Gate Jobs selection change on dirty notes (revert selection if Cancel).
5. Await product `PersistDetailAsync` on leave; prompt only when `TryRead` fails.
6. Update `docs/screens/dialogs.md` and the products sheet Esc line (dirty → prompt; URL stage unchanged).
7. Do not: WebView2 in this dialog; ContentDialog width/height layout loop; Categories chip prompt; stop auto-persist; `git add docs/features/to-review.md` on Planning; open a PR before to-review **Status** `done`; implement GNOME/iPad.
