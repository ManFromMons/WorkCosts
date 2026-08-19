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

## Sheets

Add Product, image chooser (if more than one image), paste HTML. Default button = continue/save; Esc closes without saving.
