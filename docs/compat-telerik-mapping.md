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

### Divergences — GridColumn (LARGEST)

Telerik's child markup `<GridColumn Field="X" Title="Y">` has **no compat shim** in Phase 6.
Consumers MUST migrate column markup manually:

```razor
<!-- Before (Telerik) -->
<TelerikGrid Data="@data">
  <GridColumns>
    <GridColumn Field="Name" Title="Name" />
  </GridColumns>
</TelerikGrid>

<!-- After (compat-telerik + manual column migration) -->
<TelerikGrid Data="@data">
  <SunfishDataGridColumn Field="@((MyItem x) => x.Name)" Title="Name" />
</TelerikGrid>
```

A future policy-gated PR may ship `TelerikGridColumn` as a shim if demand warrants.

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

## Notes for Future Phases

- A `TelerikGridColumn` shim can be added under the policy gate if demand warrants.
- `Telerik.Blazor.EventArgs.*` types (e.g., `GridReadEventArgs`, `DatePickerChangeEventArgs`)
  are not shimmed in Phase 6. Consumers using these must migrate signatures.
- A Roslyn analyzer that flags `using Telerik.Blazor.Components` and suggests the compat
  replacement is tracked as a separate scaffolding ticket.
