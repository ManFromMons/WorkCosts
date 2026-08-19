# Layout grammar

WinUI measurements (padding 24, Jobs master 320, Products ~40/60) are **historical**, not targets. Use **Adwaita** and **Human Interface Guidelines** spacing, type, and safe areas. Keep this **structure**.

## Size classes

| Name | Typical | Pattern |
| :--- | :--- | :--- |
| Regular / wide | Desktop, iPad landscape | List **beside** detail |
| Compact / narrow | Phone-width, iPad portrait, GNOME narrow | **Stack**: list first, navigate to detail |

Breakpoints are OS-native (`AdwBreakpoint`, `horizontalSizeClass`), not copied from WinUI.

## Shared regions

```
┌─────────────────────────────────────────────┐
│  App chrome (nav / tabs / header bar)       │
├─────────────────────────────────────────────┤
│  Page header                                │
│  Title + subtitle                    [Add]  │
├──────────────────────┬──────────────────────┤
│  Master / filters    │  Detail panel        │
│  (list, search)      │  (editor or empty)   │
└──────────────────────┴──────────────────────┘
         compact: master fills; detail is a push
```

1. **Primary Add** is trailing in the page header (accent / suggested action). Icon-only is OK if tooltip/accessibility name is “Add”.  
2. **Detail** sits in a grouped/inset panel, not raw on the garage photo.  
3. **Empty states** are centered in the list: “No jobs yet.”  
4. **Sheets** for Add Product and image chooser. **Dialogs** only for short confirmations.  
5. **Filters** stay visible above the list (Products job chips; Categories job toggles). They must not be clipped by the detail pane.  
6. **Garage background** is always behind a high-opacity scrim so text stays readable.

## Controls mapping (intent, not widgets)

| Intent | Windows | GNOME | iPad |
| :--- | :--- | :--- | :--- |
| App nav | NavigationView | Sidebar / ViewStack | Split + compact tabs |
| List + select | ListView | GtkListView / Adw | List | List |
| Accent add | AppAccentButtonStyle | suggested-action | borderedProminent |
| Switch | ToggleSwitch | GtkSwitch | Toggle |
| Markdown | MarkdownEditor | same features | same features |
| Confirm | ContentDialog | AdwAlertDialog | confirmationDialog |

## Keyboard

Follow the platform. Minimum: Esc / back closes sheet; primary button is the default; text fields must not steal Enter from Yes/No confirms (Windows already hit this with TextBox in dialogs).
