# Feature: Product extra data

- **Id:** `docs/features/product-extra-data.md`
- **Seq:** 5
- **Depends-on:** none
- **Status:** done
- **PR:** https://github.com/ManFromMons/WorkCosts/pull/2
- **Windows:** required first
- **Related screens:** `docs/screens/products.md`, `docs/data/schema.md`, `docs/parsing/overview.md`
- **Related code:** `Product`, `WorkCostsDbContext`, `ProductPageMetadata`, `ProductPageClientValues`, `ProductPageClientContractTests`, `ProductEditor`, `ProductAddEditor`, `ProductEditorValues`, `ProductsPage` (`CreateProductFromValues`, save/load)

## Objectives

- Add one text column on `Products` that stores extra product data as a **camelCase YAML** block.
- Windows Add Product and product detail always show structured extra fields (battery specs first). Save writes YAML; load reads YAML. The user never edits raw YAML.
- Page parsers populate those fields through `ProductPageMetadata` / `ProductPageClientValues` (null = do not overwrite). Host-specific extraction is **out of this story** (`source-eurocarparts`, `source-carbatterymarket`, `source-tayna`, `source-onlinecarparts`).
- **Out of scope:** new pages; a second extra table or Capacity/CCA columns; GNOME/iPad shells (Ports: same column later); zip export/import (the column travels with the schema when that ships); filling ExtraYaml from Amazon/Autodoc in this pass.

## User requirements

- On Add Product details and on `ProductEditor`, extra fields are **always visible**, including when empty (same as EAN).
- Fields: Capacity (Ah, integer), Length / Width / Height (mm, integers), CCA (integer amps), Technology (empty or Wet / SMF / AGM / EFB / Gel / Lithium).
- All extra fields are optional. Save with everything empty stores `ExtraYaml` as empty. Clearing a control drops that key from the YAML.
- Fetch/paste: non-null extra fields from the parser overwrite the matching controls. Null extra fields leave the current control values. Unknown YAML keys already on the product are preserved across fetch and save.
- Invalid stored YAML: editor shows empty extra fields; the next save writes a valid block from the current controls (unknown keys from the broken blob are not recovered).
- Esc / Cancel / collision banner unchanged. No new dialogs. Never host WebView2 in a blocking dialog.

## Layout

- No new pages. Regular: list beside detail. Compact: stack as today. Add Product stays a sheet (`AddOverlay` / `ProductAddEditor`).
- Extra-spec row in `PageFieldsPanel` on **both** `ProductEditor` and `ProductAddEditor`, after OEM equivalent, before equivalent-products (editor) / category column (add sheet).
- Controls, OS spacing: integer NumberBoxes for Capacity, Length, Width, Height, CCA; ComboBox for Technology (empty + the six tokens). Do not hide the row for non-batteries. Do not show a YAML text box.
- Update `docs/screens/products.md` detail-editor field list.

## Workflow

1. Open Add Product or select a product. Extra controls are empty, or filled from `Product.ExtraYaml` on load.
2. User may type extra values, or load a URL / paste HTML. `ApplyPageMetadata` copies non-null extra client fields onto the controls.
3. Save (`TryRead` → `ProductEditorValues` / add-editor equivalent → `CreateProductFromValues` or existing product update) serialises extra controls + preserved unknown keys to `Product.ExtraYaml`.
4. Esc / Cancel discards as today. Delete unchanged (`ProductCommands.DeleteAsync`).

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Schema | `Product`, `WorkCostsDbContext`, EF migrations | `Product.ExtraYaml` `TEXT` max 8000, default empty string (same style as `OemEquivalent`) |
| YAML | none in repo | `ProductExtra` in **Core**: `int? Capacity`, `int? LengthMm`, `int? WidthMm`, `int? HeightMm`, `int? Cca`, `string? Technology`, plus a bag for unknown keys. YamlDotNet camelCase; omit null/empty known keys; round-trip unknown keys |
| Parse DTO | `ProductPageMetadata`, `ProductPageClientValues` | optional extra fields on both records (null defaults so Amazon/Autodoc call sites compile). `From` copies them; blank/whitespace technology → null; negative ints → null |
| Technology tokens | none | helper in **Parsing** (parsers will call it): first case-insensitive match, AGM/EFB/SMF/Gel/Lithium before Wet — table below. This story ships the helper and unit tests; host parsers start using it in their stories |
| Editor | `ProductEditor`, `ProductAddEditor`, `ProductEditorValues`, `ProductsPage` | bind the six controls; load/save via `ProductExtra`; copy `ExtraYaml` on create/update like `OemEquivalent` |
| Docs | `docs/data/schema.md`, `docs/screens/products.md`, `docs/parsing/overview.md` client-fields list | none |

- **Wiring:** `new ProductImageService()` / `App.Database` unchanged. `ProductExtra` is a static helper in Core. No DI container.
- **Data:** one EF migration. Existing products: `ExtraYaml` empty. No new BLOBs.
- **Ports:** Swift later reads/writes the same `ExtraYaml` column and camelCase keys. No extra TFMs.

Known YAML keys (integers unquoted):

```yaml
capacity: 110
lengthMm: 393
widthMm: 175
heightMm: 190
cca: 950
technology: Wet
```

`capacity` is Ah as **int**. Dimensions millimetres as **int**. `cca` is amps as **int**. `technology` is one of `Wet`, `SMF`, `AGM`, `EFB`, `Gel`, `Lithium`.

Technology normalisation (page string → token):

| Page contains | Stored token |
| :--- | :--- |
| `agm` | `AGM` |
| `efb` | `EFB` |
| `smf` or `sealed maintenance` | `SMF` |
| `gel` | `Gel` |
| `lithium` or `li-ion` or `liion` | `Lithium` |
| `wet` or `flooded` | `Wet` |

Editor ComboBox stores the token, not the page phrase. Unrecognised technology is omitted.

## Tests

- Project: `WorkCosts.Tests`.
- `ProductExtra` YAML: round-trip the sample block above; omit null keys; empty extra → empty string; preserve an unknown key (`foo: bar`) across load/save; invalid YAML → empty extra (no throw).
- Technology helper: “Standard Wet Battery” → Wet; “SMF” → SMF; “AGM” → AGM; empty/unknown → null.
- `ProductPageClientContractTests`: extra fields on metadata and client; blank does not overwrite; single-field cases cover every client property. Existing Amazon/Autodoc fixture tests still pass with extra fields null.
- Do not require UI automation.

## Open questions

(none)

## Accepted defaults

- Column name `ExtraYaml`. YamlDotNet in Core, not a hand-rolled emitter.
- Extra is a general bag; battery keys are the first documented known keys. Hosts may add unknown keys later without a migration.
- Controls always visible. YAML never shown in the UI.
- Decisions from planning: Capacity is int; technology normalised; sample CCA values belong on the source stories, not here.
- Kickoff: skill `start-implement` on this file (not `start-add-source`).
- Later branch: `feature/product-extra-data-Product-extra-YAML`.
- `DatabaseService.RepairProductSchema` also adds `ExtraYaml` (same pattern as `PricePoint`). Accepted in review.
- `InputToolTip.Bind(ComboBox, …)` for Technology. Accepted in review.

## Implementation notes for an agent

1. Migration + `Product.ExtraYaml` + `docs/data/schema.md`.
2. `ProductExtra` YAML helper + tests, then extra fields on `ProductPageMetadata` / `ProductPageClientValues` + contract tests + technology helper in Parsing.
3. WinUI: `ProductEditor` / `ProductAddEditor` / `ProductEditorValues` / `ProductsPage` load-save. Update `docs/screens/products.md` and client-fields in `docs/parsing/overview.md`.
4. Accepted reuse: ExtraYaml column repair in `DatabaseService.RepairProductSchema`; `InputToolTip.Bind` for ComboBox.
5. Do not: host parsers; separate spec columns; ExtraYaml text box in the UI; `git add` to-review on this branch; open a PR before to-review **Status** `done`.
