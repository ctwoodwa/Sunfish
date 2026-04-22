# compat-telerik Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Telerik code maps to after migration. Any change to an entry (promoting a
> parameter from "mapped" to "throws", changing a default, adding a divergence) is a
> **breaking change** for consumers and must land under the policy gate in the same PR as
> the code change. See `packages/compat-telerik/POLICY.md`.

## Conventions

- **Mapped** — Telerik parameter value translates 1:1 to a Sunfish parameter / value.
- **Forwarded** — Telerik attribute is passed through via `AdditionalAttributes` (e.g.
  `class`, `style`, `tabindex`). No semantic transform.
- **Dropped (cosmetic)** — Silently ignored; logged via ILogger at Warning level. Reserved
  for parameters with no functional impact.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values that
  would silently change behavior if dropped.
- **LogAndFallback** — Unrecognized value (not in the mapping table) logs a warning and
  falls back to the Sunfish default.

---

## TelerikButton

- **Telerik target:** `Telerik.Blazor.Components.TelerikButton`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Buttons.SunfishButton`

| Telerik parameter | Type (Telerik) | Sunfish parameter | Type (Sunfish) | Mapping |
|---|---|---|---|---|
| `ThemeColor` | `string` | `Variant` | `ButtonVariant` | `"primary"→Primary`, `"secondary"→Secondary`, `"info"→Info`, `"success"→Success`, `"warning"→Warning`, `"error"→Danger`, `"tertiary"/"dark"/"light"/"inverse"/"base"→Secondary` (fallback; Sunfish lacks these variants), `null/""→Primary`. Unrecognized strings LogAndFallback to Primary. |
| `Size` | `string` | `Size` | `ButtonSize` | `"sm"→Small`, `"md"→Medium`, `"lg"→Large`, `null→Medium`. LogAndFallback on unrecognized. |
| `FillMode` | `string` | `FillMode` | `FillMode` | `"solid"→Solid`, `"flat"→Flat`, `"outline"→Outline`, `"link"→Link`, `"clear"→Clear`, `null→Solid`. LogAndFallback. |
| `Rounded` | `string` | `Rounded` | `RoundedMode` | `"small"→Small`, `"medium"→Medium`, `"large"→Large`, `"full"→Full`, `"none"→None`, `null→Medium`. LogAndFallback. |
| `Enabled` | `bool` | `Enabled` | `bool` | Passthrough. |
| `ButtonType` | `Sunfish.Compat.Telerik.Enums.ButtonType` | `ButtonType` | `Sunfish.Foundation.Enums.ButtonType` | `Button→Button`, `Submit→Submit`, **`Reset→throws`** (Sunfish has no Reset; hint: "Use an OnClick handler that resets form state explicitly"). |
| `Form` | `string?` | `Form` | `string?` | Passthrough. |
| `OnClick` | `EventCallback<MouseEventArgs>` | `OnClick` | `EventCallback<MouseEventArgs>` | Passthrough. |
| `Icon` | `object?` | `Icon` | `RenderFragment?` | Normalized via `SvgIconAdapter.ToRenderFragment`. |
| `ChildContent` | `RenderFragment?` | `ChildContent` | `RenderFragment?` | Passthrough. |
| `Class` / others | `string?` etc. | — | — | Forwarded via `AdditionalAttributes`. |

### Divergences

- Sunfish's `ButtonVariant` enum lacks `Tertiary`, `Dark`, `Light`, `Inverse`, `Base`. These
  Telerik theme colors collapse to `Secondary` with a warning log. Consumers relying on
  these specific visual styles must migrate styling manually.
- Sunfish's `ButtonVariant.Danger` is the mapping target for Telerik's `"error"` theme
  color.

---

## TelerikIcon

- **Telerik target:** `Telerik.Blazor.Components.TelerikIcon`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Utility.SunfishIcon`

| Telerik parameter | Type | Mapping |
|---|---|---|
| `Icon` | `object?` | If `RenderFragment`: forwarded to `SunfishIcon.ChildContent`. If a string: forwarded to `SunfishIcon.Name`. If a typed icon object: normalized via `SvgIconAdapter`. |
| `Size` | `string?` | Forwarded via `style="font-size: {size}"` on the inner icon. |
| `Class` / others | `string?` | Forwarded via `AdditionalAttributes`. |

### Divergences

- Telerik's built-in icon font names (`"file"`, `"folder"`, etc.) will not render unless
  the consumer has also migrated their icon provider to one of Sunfish's icon packages
  (`Sunfish.Icons.Tabler`, `Sunfish.Icons.Legacy`).

---

## TelerikCheckBox

- **Telerik target:** `Telerik.Blazor.Components.TelerikCheckBox`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishCheckbox`

Note the spelling: Telerik uses `CheckBox` (two words); Sunfish uses `Checkbox` (one word).
The wrapper file is `TelerikCheckBox.razor` and delegates to `<SunfishCheckbox>`.

| Telerik parameter | Type | Mapping |
|---|---|---|
| `Value` | `bool` / `bool?` | Passthrough via `@bind-Value`. |
| `ValueChanged` | `EventCallback<bool>` | Passthrough. |
| `Enabled` | `bool` | Passthrough. |
| `Id` | `string?` | Forwarded via `AdditionalAttributes["id"]`. |
| `TabIndex` | `int?` | Forwarded via `AdditionalAttributes["tabindex"]`. |
| `Class` | `string?` | Forwarded. |

---

## TelerikTextBox

- **Telerik target:** `Telerik.Blazor.Components.TelerikTextBox`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishTextBox`

| Telerik parameter | Type | Mapping |
|---|---|---|
| `Value` | `string?` | Passthrough via `@bind-Value`. |
| `ValueChanged` | `EventCallback<string?>` | Passthrough. |
| `Label` | `string?` | Passthrough if Sunfish exposes `Label`; else forwarded via `placeholder`. |
| `Placeholder` | `string?` | Passthrough. |
| `Enabled` | `bool` | Passthrough. |
| `DebounceDelay` | `int` | Dropped (cosmetic); logged. Sunfish's TextBox does not expose debounce in Phase 6. |
| `Width` | `string?` | Forwarded via `style="width: {value}"`. |

---

## TelerikDropDownList\<TItem, TValue\>

- **Telerik target:** `Telerik.Blazor.Components.TelerikDropDownList<TItem, TValue>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDropDownList`

| Telerik parameter | Type | Mapping |
|---|---|---|
| `Data` | `IEnumerable<TItem>` | Passthrough to `SunfishDropDownList.Data`. |
| `TextField` | `string` | Mapped to a `Func<TItem, string>` accessor compiled from the property name. |
| `ValueField` | `string` | Mapped to a `Func<TItem, TValue>` accessor compiled from the property name. |
| `Value` | `TValue` | Passthrough via `@bind-Value`. |
| `ValueChanged` | `EventCallback<TValue>` | Passthrough. |
| `DefaultText` | `string?` | Passthrough as placeholder. |

### Divergences

- Telerik's `TextField`/`ValueField` accept property-name strings; the wrapper uses a
  reflection-based accessor factory. Complex bindings (e.g., `"Address.City"`) require
  consumer-supplied lambdas instead.

---

## TelerikComboBox\<TItem, TValue\>

- **Telerik target:** `Telerik.Blazor.Components.TelerikComboBox<TItem, TValue>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishComboBox`

Same shape as TelerikDropDownList plus `Filterable` (bool) and `AllowCustom` (bool).
Both are passed through when Sunfish supports them; logged+dropped otherwise.

---

## TelerikDatePicker

- **Telerik target:** `Telerik.Blazor.Components.TelerikDatePicker`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDatePicker`

| Telerik parameter | Type | Mapping |
|---|---|---|
| `Value` | `DateTime?` | Passthrough. |
| `ValueChanged` | `EventCallback<DateTime?>` | Passthrough. |
| `Format` | `string?` | Passthrough. |
| `Min` | `DateTime?` | Passthrough. |
| `Max` | `DateTime?` | Passthrough. |
| `View` / `BottomView` | enum | Dropped (cosmetic calendar behavior). |

---

## TelerikForm

- **Telerik target:** `Telerik.Blazor.Components.TelerikForm`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.SunfishForm`

| Telerik parameter | Type | Mapping |
|---|---|---|
| `Model` | `object?` | Passthrough. |
| `EditContext` | `EditContext?` | Passthrough. |
| `OnSubmit` | `EventCallback` | Passthrough. |
| `OnValidSubmit` | `EventCallback<EditContext>` | Passthrough. |
| `OnInvalidSubmit` | `EventCallback<EditContext>` | Passthrough. |
| `Orientation` | `string?` / enum | Forwarded if Sunfish exposes; else logged+dropped. |

### Divergences

- Telerik's validation child components (`<TelerikValidationSummary>`,
  `<TelerikValidationMessage>`, `<FormValidation>`) are NOT shimmed in Phase 6. Consumers
  must migrate these manually to `<SunfishValidationSummary>` / `<SunfishValidationMessage>`.
- The `ChildContent` is forwarded verbatim; unknown nested components will fail to
  compile at the consumer site if they reference Telerik-only components.

---

## TelerikGrid\<TItem\>

- **Telerik target:** `Telerik.Blazor.Components.TelerikGrid<TItem>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishDataGrid<TItem>`

| Telerik parameter | Type (Telerik) | Mapping |
|---|---|---|
| `Data` | `IEnumerable<TItem>` | Passthrough. |
| `Pageable` | `bool` | Passthrough. |
| `PageSize` | `int` | Passthrough. |
| `Sortable` | `bool` | Passthrough. |
| `SortMode` | `Sunfish.Compat.Telerik.Enums.SortMode` | Mapped to Sunfish's sort mode if present; else log+drop. |
| `Filterable` | `bool` | Passthrough. |
| `FilterMode` | `Sunfish.Compat.Telerik.Enums.FilterMode` | Mapped; `CheckBoxList` → throws (no Sunfish equivalent). |
| `Selectable` | `bool` | Passthrough. |
| `SelectionMode` | `Sunfish.Compat.Telerik.Enums.SelectionMode` | Passthrough. |
| `SelectedItems` | `IEnumerable<TItem>?` | Passthrough. |
| `SelectedItemsChanged` | `EventCallback<IEnumerable<TItem>>` | Passthrough. |
| `OnRead` | `EventCallback<GridReadEventArgs>` | **Throws**. Hint: "Use `Data` with an in-memory or server-bound collection. Telerik `GridReadEventArgs` is not shimmed." |
| `ChildContent` | `RenderFragment?` | Passthrough — but see divergence below. |

### GridColumn child markup — **now shimmed** (gap closed 2026-04-22)

Telerik's `<GridColumns>`/`<GridColumn>` child markup is shimmed as
`<GridColumns>`/`<TelerikGridColumn TItem="...">` — see the `TelerikGridColumn<TItem>` and
`GridColumns` entries below.

---

## TelerikWindow

- **Telerik target:** `Telerik.Blazor.Components.TelerikWindow`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Overlays.SunfishWindow`

| Telerik parameter | Type | Mapping |
|---|---|---|
| `Visible` | `bool` | Passthrough via `@bind-Visible`. |
| `VisibleChanged` | `EventCallback<bool>` | Passthrough. |
| `Title` | `string?` / `RenderFragment?` | Passthrough. |
| `Width` | `string?` | Forwarded via style. |
| `Height` | `string?` | Forwarded via style. |
| `Modal` | `bool` | Passthrough if Sunfish exposes; else logged. |
| `State` | `Sunfish.Compat.Telerik.Enums.WindowState` | `Default→(passthrough)`, `Maximized`/`Minimized`→ **throws** (Sunfish does not model these states in Phase 6). |
| `OnClose` | `EventCallback` | Passthrough. |

---

## TelerikTooltip

- **Telerik target:** `Telerik.Blazor.Components.TelerikTooltip`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishTooltip`

| Telerik parameter | Type | Mapping |
|---|---|---|
| `TargetSelector` | `string?` | Passthrough. |
| `Position` | `string?` | Mapped to Sunfish `Placement`: `"top"→Top`, `"bottom"→Bottom`, `"left"→Left`, `"right"→Right`. LogAndFallback to Top. |
| `ShowOn` | `string?` / enum | Mapped to Sunfish trigger. |
| `Content` | `string?` / `RenderFragment?` | Forwarded to `ChildContent`. |

---

## TelerikNotification

- **Telerik target:** `Telerik.Blazor.Components.TelerikNotification`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Feedback.SunfishSnackbarHost`

### Divergences

- Telerik exposes imperative `Show(NotificationModel)` / `Hide(Guid)` methods via `@ref`.
  Sunfish's `SunfishSnackbarHost` uses a scoped `ISnackbarService`. The wrapper exposes
  `Show`/`Hide` method shims that forward to the injected service.
- Telerik's `NotificationModel.ThemeColor` (string) maps to Sunfish's severity enum.

| Telerik parameter | Type | Mapping |
|---|---|---|
| `AnimationType` | `string?` | Dropped (cosmetic). |
| `HorizontalPosition` / `VerticalPosition` | `string?` | Forwarded via host parameters. |

---

## TelerikGridColumn\<TItem\>

_Added 2026-04-22 as part of the Decision-4 gap-closure milestone
(`icm/00_intake/output/compat-expansion-intake.md`)._

- **Telerik target:** `Telerik.Blazor.Components.GridColumn` (note: Telerik's child element is
  *not* `TelerikGrid`-prefixed; we use `TelerikGridColumn` for type-name disambiguation and
  consistency with the other compat-telerik shims).
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishGridColumn<TItem>`

Parent discovery: the shim declares itself as a child of `SunfishDataGrid<TItem>` via
`CompatChildComponent<TParent>` (the `TelerikGrid` wrapper forwards its `ChildContent`
verbatim, so the inner `SunfishGridColumn` picks up the existing cascading grid parent).

| Telerik parameter | Type | Mapping |
|---|---|---|
| `Field` | `string?` | Passthrough; coerced to `""` when null (Sunfish requires non-null). |
| `Title` | `string?` | Passthrough. |
| `Width` | `string?` | Passthrough. |
| `Sortable` | `bool` | Passthrough; default `true`. |
| `Filterable` | `bool` | Passthrough; default `true`. |
| `Visible` | `bool` | Passthrough; default `true`. |
| `Editable` | `bool` | Passthrough; default `true`. |
| `Locked` | `bool` | Passthrough. |
| `Format` / `DisplayFormat` | `string?` | Passthrough. |
| `TextAlign` | `string?` (enum-name OR raw CSS) | `"Left"/"Center"/"Right"/"Justify"` → lowercase CSS value; unrecognised → passthrough (e.g. `"start"`, `"end"`). |
| `Template` | `RenderFragment<TItem>?` | Passthrough. |
| `HeaderTemplate` | `RenderFragment?` | Passthrough. |
| `EditorTemplate` | `RenderFragment<TItem>?` | Passthrough. |
| `FooterTemplate` | `RenderFragment?` | Passthrough. |

### Divergences

- Telerik's `GridColumn` exposes many more parameters (e.g. aggregate descriptors,
  context-menu configuration, lockable-position). These are not mapped in this gap-closure;
  future policy-gated PRs can extend the surface without breaking the existing mapping.
- Telerik's `TextAlign` is a typed enum (`ColumnTextAlign`). The shim accepts the enum-name
  string for source-shape parity; consumers using the typed enum must update to the string
  form.

---

## GridColumns (container shim)

- **Telerik target:** `Telerik.Blazor.Components.GridColumns`
- **Sunfish target:** no component — pass-through of `ChildContent`.

Telerik uses `<GridColumns>` as a semantic wrapper around `<GridColumn>` children inside
`<TelerikGrid>`. The shim mirrors that shape with a content-only passthrough. No parameters
beyond `ChildContent`.

### Divergences

- This is the one shim whose type name does **not** carry the `Telerik` prefix (Telerik
  itself ships the element as `GridColumns`). Matches Telerik's source-shape exactly.

---

## TelerikValidationSummary

_Added 2026-04-22 as part of the Decision-4 gap-closure milestone._

- **Telerik target:** `Telerik.Blazor.Components.TelerikValidationSummary`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Containers.SunfishValidationSummary`

| Telerik parameter | Type | Mapping |
|---|---|---|
| `Template` | `RenderFragment<IEnumerable<string>>?` | Passthrough. |
| `Class` / others | `string?` | Forwarded via `AdditionalAttributes`. |

### Divergences

- Must be used inside a cascading `EditContext` (i.e. under `SunfishForm`, `TelerikForm`, or
  a plain `EditForm`). Rendering outside one throws `InvalidOperationException` — matches
  Telerik's own contract.
- Telerik's `AllFieldsErrorLabel` / `NoValidFieldErrorLabel` / visual-customization
  parameters are not mapped — use the `Template` override to customize rendering.

---

## TelerikValidationMessage\<TValue\>

_Added 2026-04-22 as part of the Decision-4 gap-closure milestone._

- **Telerik target:** `Telerik.Blazor.Components.TelerikValidationMessage<TValue>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Containers.SunfishValidationMessage<TValue>`

| Telerik parameter | Type | Mapping |
|---|---|---|
| `For` | `Expression<Func<TValue>>?` | Passthrough (same shape as Sunfish / stock Blazor). |
| `Template` | `RenderFragment<IEnumerable<string>>?` | Passthrough. |
| `Class` / others | `string?` | Forwarded via `AdditionalAttributes`. |

### Divergences

- `For` is **required**; omitting it throws `InvalidOperationException`. Matches
  `SunfishValidationMessage` and Telerik behavior.

---

## Grid EventArgs shims

_Added 2026-04-22 as part of the Decision-4 gap-closure milestone._

These types let consumers keep their existing Telerik handler signatures compiling after
the using-swap. They are data-only types — the functional wiring from `TelerikGrid` to each
args type lands incrementally as individual grid-event surfaces are shimmed.

| Compat type | Telerik target | Notes |
|---|---|---|
| `Sunfish.Compat.Telerik.GridRowClickEventArgs` | `Telerik.Blazor.Components.GridRowClickEventArgs` | `Item` typed as `object` (Telerik-shape erasure); consumer casts. Carries `EventArgs` and optional `Field`. Translated at the delegation boundary from Sunfish's generic `GridRowClickEventArgs<TItem>`. |
| `Sunfish.Compat.Telerik.GridCommandEventArgs` | `Telerik.Blazor.Components.GridCommandEventArgs` | `Item` erased to `object`. Exposes `Command`, `Field`, `IsCancelled`, `IsNew`. Grid-wrapper routing of command buttons is not plumbed in this gap-closure — type shipped so signatures compile. |
| `Sunfish.Compat.Telerik.GridReadEventArgs` | `Telerik.Blazor.Components.GridReadEventArgs` | Weakly-typed (`Data` is `IEnumerable`). `TelerikGrid.OnRead` still throws in Phase 6 (see `TelerikGrid` entry above); type exists so consumer handler signatures compile. |
| `Sunfish.Compat.Telerik.DatePickerChangeEventArgs` | `Telerik.Blazor.Components.DatePickerChangeEventArgs` | Single-property (`Value`). `TelerikDatePicker` uses the `ValueChanged` pattern directly; type shipped so consumers using the explicit args name compile. |

### Divergences

- Telerik's generic-erasure (`Item` as `object`) is preserved in the shim for source-shape
  parity — migrators keep their existing handler signatures. If a consumer prefers the
  strongly-typed Sunfish shape, they can target `Sunfish.UIAdapters.Blazor.Components.DataDisplay.GridRowClickEventArgs<TItem>`
  directly on the compat wrapper.
- Types ship without full bi-directional wiring on each event surface. See the event-args
  type-level XML doc for the current status per type.

### Shared pattern: `CompatChildComponent<TParent>`

`packages/compat-telerik/Internal/CompatChildComponent.cs` formalises the
"cascading-parent child component" pattern used by `TelerikGridColumn`. It lives inside
compat-telerik for now; if Syncfusion / DevExpress / Infragistics compat packages adopt the
same shape (expected — `SfGridColumn`, `DxGridDataColumn`, `IgbColumn` are all children of
their grids), lift this helper to a shared `compat-shared` package. Tracked in
`icm/00_intake/output/compat-expansion-intake.md` Decision 4 implementation note.

---

## Notes for Future Phases

- Additional grid-event surfaces (`OnRowClick`, `OnRowContextMenu`, command column,
  `OnRead` server-side binding) wire through the existing `TelerikGrid` wrapper in
  follow-up policy-gated PRs now that the args types are shipped.
- A Roslyn analyzer that flags `using Telerik.Blazor.Components` and suggests the compat
  replacement is tracked as Task #105 (scheduled after compat-syncfusion lands per
  Decision 3 of the compat-expansion intake).
