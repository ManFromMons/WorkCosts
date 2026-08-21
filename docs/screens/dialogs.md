# Dialogs and sheets

## Confirm Yes/No

Short question. Primary = Yes, dismiss/Esc = No. Body is static text, **not** a text box (Enter must not insert a newline). Windows: `DialogHelper.ConfirmYesNoAsync`.

Used for product delete and similar.

## Message

Title + message, single Close.

## Do not

- Full-size ContentDialog with layout loops (that froze Add Product).  
- WebView inside the confirm dialog.  
- Nested modal for “URL already exists” — use an **in-sheet banner**.

## Unsaved changes

When leaving a **dirty** Add Product details sheet, Jobs notes / unparsed duration, or invalid Product fields that never persisted: title **Unsaved changes**, body **Save your changes before closing?** (static `TextBlock`). Buttons **Save** (primary, countdown `Save (20)` or `Save (10)`), **Don't Save**, **Cancel**. Enter = Save, Esc = Cancel. User leave / Esc / nav is 20 seconds; OS shutdown or logoff is 10 seconds. Timeout acts as Save. Windows: `DialogHelper.ConfirmUnsavedWithTimeoutAsync`.

Do not stack on another `ContentDialog` (`DialogHelper.HasOpenDialog`). Auto-persisted fields (product details that `TryRead`, job name/price/parsed duration, work-job quantities) await in-flight save instead of prompting.

## Sheets

Add Product, image chooser (if more than one image), paste HTML. Default button = continue/save. Esc on a **dirty** Add Product details sheet prompts (see Unsaved changes). URL stage still closes with no prompt.
