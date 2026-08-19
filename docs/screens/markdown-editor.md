# Markdown editor

Shared control (`MarkdownEditor`). Used for job notes.

## UI

Pivot/tabs: **Write** | **Preview**.

Write: toolbar then multiline editor.

Toolbar (required): Undo, Redo, Bold (`**`), Italic (`*`), Heading, lists (as on Windows). Pointer-down on the toolbar must not steal focus from the editor. Ctrl/Cmd+Z, Y, B, I follow the OS.

Preview: render markdown (Windows: Markdig). Sanitise; no raw script.

Binding: two-way markdown string, max length 8000 on jobs.

GNOME/iPad: same commands and both modes; widgets may differ.
