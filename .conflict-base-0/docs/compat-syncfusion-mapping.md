# compat-syncfusion Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Syncfusion code maps to after migration. Any change to an entry (promoting a
> parameter from "mapped" to "throws", changing a default, adding a divergence) is a
> **breaking change** for consumers and must land under the policy gate in the same PR as
> the code change. See `packages/compat-syncfusion/POLICY.md`.

## Conventions

- **Mapped** — Syncfusion parameter value translates 1:1 to a Sunfish parameter / value.
- **Forwarded** — Syncfusion attribute is passed through via `AdditionalAttributes` (e.g.
  `class`, `style`, `tabindex`). No semantic transform.
- **Dropped (cosmetic)** — Silently ignored; logged via ILogger at Warning level. Reserved
  for parameters with no functional impact.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values that
  would silently change behavior if dropped.
- **LogAndFallback** — Unrecognized value (not in the mapping table) logs a warning and
  falls back to the Sunfish default.
- **Not-in-scope** — Parameter declared but no migration path; consumer must refactor.

---

## SfButton

- **Syncfusion target:** `Syncfusion.Blazor.Buttons.SfButton`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Buttons.SunfishButton`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Content` | `string` | → `ChildContent` (rendered as text when `ChildContent` is null) | Mapped |
| `CssClass` | `string` | Forwarded via `AdditionalAttributes["class"]` | Forwarded |
| `Disabled` | `bool` | → `Enabled = !Disabled` | Mapped |
| `IconCss` | `string` | Wrapped as `<span class="@IconCss" />` → `SunfishButton.Icon` | Mapped |
| `IconPosition` | `IconPosition` enum | Cosmetic — log-and-drop (Sunfish renders icon before content) | Dropped |
| `IsPrimary` | `bool` | `true → Variant=Primary`, `false → Variant=Secondary` | Mapped |
| `IsToggle` | `bool` | Not supported — logs warning. Migration: use `SunfishToggleButton` directly | Dropped |
| `EnableRtl` | `bool` | Log-and-drop (Sunfish theme controls RTL globally) | Dropped |
| `Created` | `EventCallback<object>` | Currently a no-op shim; Sunfish has no post-render event | Not-in-scope |
| `OnClick` | `EventCallback<MouseEventArgs>` | Passthrough | Mapped |
| `ChildContent` | `RenderFragment?` | Passthrough | Mapped |

### Divergences

- Syncfusion's `IconPosition` (Left/Right/Top/Bottom) is dropped — Sunfish always renders the icon
  before button content.
- `IsToggle=true` is a runtime no-op; consumers migrating toggle buttons must use
  `SunfishToggleButton` from the Sunfish adapter directly.

---

## SfIcon

- **Syncfusion target:** `Syncfusion.Blazor.Buttons.SfIcon`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Utility.SunfishIcon`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Name` | `string` (Syncfusion: `IconName` enum, ~1,500 values) | Curated subset mapped to Sunfish icon names (~50 common icons); unmapped → LogAndFallback | LogAndFallback |
| `IconCss` | `string` | Forwarded via `AdditionalAttributes["class"]` | Forwarded |
| `Size` | `IconSize` enum | `Small/Medium/Large` → `Sunfish.Foundation.Enums.IconSize` | Mapped |
| `Title` | `string` | → `SunfishIcon.AriaLabel` (title attribute) | Mapped |

### Divergences — IconName enum subset

Syncfusion's `IconName` enum exposes ~1,500 font-icon values. compat-syncfusion ships a
curated subset of ~50 common icons. Consumer migration options:

1. **Icon is in the subset** — map as-is; Sunfish renders the matching glyph.
2. **Icon is NOT in the subset** — LogAndFallback warning is emitted, the raw name is
   passed to Sunfish's IconProvider (may resolve if the consumer has also migrated their
   icon provider).
3. **Raw CSS-class pattern** — use `<span class="e-icons e-<icon-name>" />` directly; no
   shim involved. This is the recommended pattern for consumers needing a Syncfusion icon
   outside the curated subset who have not migrated their icon provider.

Additions to the subset are policy-gated PRs under `POLICY.md`.

Known icons in the subset (case-insensitive): Add, Cancel, Cart, Check, Close, Copy, Cut,
Delete, Download, Edit, Email, File, Filter, Folder, Group, Help, Home, Image, Info, Inbox,
Like, Location, Lock, Logout, Menu, More, New, Open, Paste, Phone, Play, Plus, Print,
Refresh, Remove, Save, Search, Settings, Share, Sort, Star, Stop, Tag, Trash, Undo, Unlock,
Upload, User, Users, Warning, Zoom.

---

## SfCheckBox\<TChecked\>

- **Syncfusion target:** `Syncfusion.Blazor.Buttons.SfCheckBox<TChecked>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishCheckbox`
  (note: Sunfish spells "Checkbox" as one word; Syncfusion uses "CheckBox" two words)

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Checked` | `TChecked` | Coerced to `bool` for Sunfish (`null → false`) | Mapped |
| `CheckedChanged` | `EventCallback<TChecked?>` | Translated from `SunfishCheckbox.ValueChanged` | Mapped |
| `ValueChange` | `EventCallback<ChangeEventArgs<TChecked>>` | Translated (args constructed at the boundary) | Mapped |
| `Label` | `string` | Passthrough | Mapped |
| `LabelPosition` | `LabelPosition` enum | Honored via Sunfish's own surface best-effort | Mapped |
| `Disabled` | `bool` | → `Enabled = !Disabled` | Mapped |
| `EnableTriState` | `bool` | Honored via Sunfish `Indeterminate` | Mapped |
| `Indeterminate` | `bool` | Passthrough | Mapped |
| `CssClass` | `string` | Forwarded | Forwarded |
| `EnableRtl` | `bool` | Log-and-drop | Dropped |
| `Name` | `string` | Forwarded via `AdditionalAttributes["name"]` | Forwarded |
| `EnablePersistence` | `bool` | Not-in-scope (consumer owns session persistence) | Not-in-scope |

### Divergences

- `ValueChange` wraps the new value in a Syncfusion-shaped `ChangeEventArgs<TChecked>`
  type shipped in compat-syncfusion.
- When `TChecked = bool?`, `null` is coerced to `false` on the Sunfish side since
  Sunfish's `SunfishCheckbox.Value` is non-nullable.

---

## SfTextBox

- **Syncfusion target:** `Syncfusion.Blazor.Inputs.SfTextBox`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishTextBox`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Value` | `string` | Passthrough via `@bind-Value` | Mapped |
| `ValueChanged` | `EventCallback<string>` | Passthrough | Mapped |
| `Placeholder` | `string` | Passthrough | Mapped |
| `Multiline` | `bool` | Logs warning (initial shim falls back to single-line). `Multiline + Type=Password` throws. | Dropped |
| `Type` | `InputType` enum | Mapped to Sunfish's string InputType (`text`/`password`/`email`/etc.) | Mapped |
| `FloatLabelType` | `FloatLabelType` enum | Not-in-scope — log-and-drop | Not-in-scope |
| `ShowClearButton` | `bool` | Passthrough | Mapped |
| `Readonly` | `bool` | → `SunfishTextBox.ReadOnly` | Mapped |
| `Enabled` | `bool` | → `Disabled = !Enabled` | Mapped |
| `CssClass` | `string` | Forwarded | Forwarded |
| `Width` | `string` | Merged into `style="width: {Width}"` | Forwarded |
| `TabIndex` | `int` | Forwarded via `AdditionalAttributes["tabindex"]` | Forwarded |
| `Autocomplete` | `AutoComplete` enum | → string `"on"` / `"off"` | Mapped |
| `HtmlAttributes` | `Dictionary<string, object>` | Merged into `AdditionalAttributes` | Forwarded |
| `InputAttributes` | `Dictionary<string, object>` | Not-in-scope (Sunfish has no split wrapper/input attr surface) | Not-in-scope |

### Divergences

- `Multiline=true` with `Type=Password` throws (Syncfusion also rejects this combo).
- `Multiline=true` with any other type currently falls back to single-line with a warning —
  consumers requiring true textarea behavior should use Sunfish's own textarea input
  directly.

---

## SfDropDownList\<TValue, TItem\>

- **Syncfusion target:** `Syncfusion.Blazor.DropDowns.SfDropDownList<TValue, TItem>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDropDownList`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `DataSource` | `IEnumerable<TItem>` | → `SunfishDropDownList.Data` | Mapped |
| `Value` | `TValue` | Passthrough via `@bind-Value` | Mapped |
| `ValueChanged` | `EventCallback<TValue?>` | Passthrough | Mapped |
| `Index` | `int?` | Not currently wired — shim accepts the parameter | Not-in-scope |
| `Placeholder` | `string` | Passthrough | Mapped |
| `Enabled` | `bool` | Passthrough | Mapped |
| `Readonly` | `bool` | → `SunfishDropDownList.ReadOnly` | Mapped |
| `AllowFiltering` | `bool` | → `Filterable` | Mapped |
| `ShowClearButton` | `bool` | Passthrough (cosmetic flag) | Mapped |
| `ValueTemplate` | `RenderFragment<TItem>` | Passthrough | Mapped |
| `ItemTemplate` | `RenderFragment<TItem>` | Passthrough | Mapped |
| `NoRecordsTemplate` | `RenderFragment` | Not wired in the initial shim | Not-in-scope |
| `PopupHeight` / `PopupWidth` | `string` | Not-in-scope (Sunfish controls popup sizing internally) | Not-in-scope |

### Child-component shim: `DropDownListFieldSettings`

Syncfusion uses `<DropDownListFieldSettings Text="Name" Value="Id" />` as a child component
to bind the item's text and value property accessors. The shim publishes cascading values
`SfDropDown_TextField` and `SfDropDown_ValueField` that `SfDropDownList` and `SfComboBox`
pick up.

---

## SfComboBox\<TValue, TItem\>

- **Syncfusion target:** `Syncfusion.Blazor.DropDowns.SfComboBox<TValue, TItem>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishComboBox`

Inherits the SfDropDownList surface, plus:

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `AllowCustom` | `bool` | Passthrough (default `true`) | Mapped |
| `Autofill` | `bool` | Best-effort — passes through Sunfish's filter surface | Mapped |
| `ValidateOnInput` | `bool` | Log-and-drop in the initial shim | Dropped |

---

## SfDatePicker\<TValue\>

- **Syncfusion target:** `Syncfusion.Blazor.Calendars.SfDatePicker<TValue>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDatePicker`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Value` | `TValue` | Coerced to `DateTime?` for Sunfish | Mapped |
| `ValueChanged` | `EventCallback<TValue?>` | Translated at the boundary | Mapped |
| `Min` / `Max` | `TValue` | Coerced to `DateTime?` | Mapped |
| `Format` | `string` | Passthrough | Mapped |
| `Placeholder` | `string` | Passthrough | Mapped |
| `Enabled` | `bool` | Passthrough | Mapped |
| `Readonly` | `bool` | → `SunfishDatePicker.ReadOnly` | Mapped |
| `ShowClearButton` | `bool` | Passthrough | Mapped |
| `FirstDayOfWeek` | `int` | Not wired in initial shim | Not-in-scope |
| `Start` / `Depth` | `CalendarView` enum | Cosmetic — log-and-drop (Sunfish calendar always opens in Month view) | Dropped |
| `AllowEdit` | `bool` | Default `true` — not wired to Sunfish's read-only flag | Not-in-scope |

---

## SfDataForm

- **Syncfusion target:** `Syncfusion.Blazor.DataForm.SfDataForm`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Containers.SunfishForm`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Model` | `object` | Passthrough | Mapped |
| `EditContext` | `EditContext?` | Passthrough | Mapped |
| `ColumnCount` | `int` | Log-and-drop (Sunfish delegates layout to consumer) | Dropped |
| `ColumnSpacing` | `string` | Log-and-drop | Dropped |
| `ValidationDisplayMode` | enum | `Inline`/`None` → mapped; `ToolTip` → **throws** | Mixed |
| `ButtonsAlignment` | enum | Log-and-drop | Dropped |
| `OnSubmit` | `EventCallback` | Invoked from Sunfish's `OnValidSubmit` chain | Mapped |
| `OnValidSubmit` | `EventCallback<EditContext>` | Mapped | Mapped |
| `OnInvalidSubmit` | `EventCallback<EditContext>` | Mapped | Mapped |
| `OnUpdate` | `EventCallback` | Not wired in initial shim | Not-in-scope |
| `EnableFloatingLabel` | `bool` | Log-and-drop | Dropped |
| `LabelPosition` | enum | Log-and-drop | Dropped |
| `FormValidator` | type | Not wired in initial shim | Not-in-scope |
| `Width` | `string` | Passthrough | Mapped |
| `ID` | `string` | → `SunfishForm.Id` | Mapped |

### Divergences — SfDataForm is opinionated, SunfishForm is thin

SfDataForm auto-generates field layout from the bound model; SunfishForm is closer to
Blazor's `EditForm`. Layout-orchestration parameters (`ColumnCount`, `ColumnSpacing`,
`ButtonsAlignment`, `EnableFloatingLabel`, `LabelPosition`) are log-and-dropped.

**Migration guidance:** Wrap form children in a Sunfish grid/flex layout to achieve the
multi-column effect. Use Sunfish's own field label-position API on each field rather than a
form-wide setting.

**Why throws on `ValidationDisplayMode=ToolTip`:** there is no Sunfish surface for tooltip
validation messages today; silently dropping would hide validation errors at runtime.

---

## SfGrid\<TValue\>

- **Syncfusion target:** `Syncfusion.Blazor.Grids.SfGrid<TValue>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishDataGrid<TItem>`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `DataSource` | `IEnumerable<TValue>` | → `Data` | Mapped |
| `AllowPaging` | `bool` | → `Pageable` | Mapped |
| `AllowSorting` | `bool` | → `Sortable` | Mapped |
| `AllowFiltering` | `bool` | Registers feature flag (Sunfish controls filter mode via its own surface) | Mapped |
| `AllowGrouping` | `bool` | Best-effort — logs when set | Mapped |
| `AllowSelection` | `bool` | Combined with child `GridSelectionSettings` to produce Sunfish `SelectionMode` | Mapped |
| `AllowReordering` | `bool` | Not wired in initial shim | Not-in-scope |
| `AllowResizing` | `bool` | Not wired in initial shim | Not-in-scope |
| `Height` / `Width` | `string` | Forwarded via attributes | Forwarded |
| `EnableVirtualization` | `bool` | Not wired in initial shim | Not-in-scope |
| `RowHeight` | `double` | Not wired in initial shim | Not-in-scope |
| `Toolbar` | `object` | Log-and-drop (Syncfusion's shape is untyped; Sunfish's toolbar surface differs) | Dropped |
| `ID` | `string` | Forwarded | Forwarded |
| `Columns` | `List<GridColumn>` | Not-in-scope — consumers use the `<GridColumns>` / `<GridColumn>` child pattern | Not-in-scope |

### Child-component shims

- `GridColumns<TValue>` — semantic container (ride on `CompatChildComponent<SfGrid<TValue>>`).
- `GridColumn<TValue>` — delegates to `SunfishGridColumn`.
- `GridPageSettings<TValue>` — publishes `PageSize` back to parent `SfGrid` via internal state.
- `GridSelectionSettings<TValue>` — publishes `SelectionMode` back to parent via `Type = Single | Multiple`.
- `GridFilterSettings<TValue>` — diagnostic-only (Sunfish filter modes differ from Syncfusion).
- `GridSortSettings<TValue>` — container for `GridSortColumn` descriptors.
- `GridSortColumn` — diagnostic-only (Sunfish's grid owns sort state through its own API).
- `GridEditSettings<TValue>` — diagnostic-only.
- `GridEvents<TValue>` — Syncfusion's unique idiom for wiring grid events on a child. Shim
  accepts callbacks and logs; functional wiring is incremental.

### Divergences

- `GridEvents<TValue>` callbacks are registered but currently no-op — consumer handler
  signatures compile, but Sunfish's grid does not yet invoke them. Tracked as a future
  gap-closure PR.
- `Toolbar` is untyped `object` (accepts `List<string>` or `List<ItemModel>`) — the shim
  log-and-drops rather than guessing.

---

## SfDialog (→ Window)

- **Syncfusion target:** `Syncfusion.Blazor.Popups.SfDialog`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Overlays.SunfishWindow`

Syncfusion has **no `SfWindow` type**; `SfDialog` is the analog of Telerik's `TelerikWindow`.
compat-syncfusion therefore intentionally does NOT ship an `<SfWindow>` wrapper.

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Visible` | `bool` | Passthrough via `@bind-Visible` | Mapped |
| `Header` | `string` | → `SunfishWindow.Title` | Mapped |
| `Content` | `string` | Rendered as `MarkupString` when no template / ChildContent present | Mapped |
| `Width` / `Height` | `string` | Passthrough (defaults `"100%"` / `"auto"`) | Mapped |
| `IsModal` | `bool` | → `SunfishWindow.Modal` | Mapped |
| `ShowCloseIcon` | `bool` | Not wired — Sunfish window always shows close | Not-in-scope |
| `AllowDragging` | `bool` | Not-in-scope in the initial shim | Not-in-scope |
| `EnableResize` | `bool` | Not-in-scope in the initial shim | Not-in-scope |
| `Target` | `string` | Not wired | Not-in-scope |
| `CssClass` | `string` | Forwarded | Forwarded |
| `MinHeight` | `string` | Not wired | Not-in-scope |
| `CloseOnEscape` | `bool` | Default `true`; Sunfish honors Escape by default | Mapped |
| `ZIndex` | `double` | Not wired | Not-in-scope |
| `EnableRtl` | `bool` | Dropped (theme controls RTL) | Dropped |

### Child-component shims

- `DialogTemplates` — container publishing `Header` / `Content` / `FooterTemplate` fragments.
- `DialogHeaderTemplate` / `DialogContentTemplate` / `DialogFooterTemplate` — standalone
  alternates.
- `DialogButtons` — forwards its ChildContent to the dialog footer slot.
- `DialogButton` — renders as a `SunfishButton` with `IsPrimary` mapping to `Variant=Primary`.

### Divergences

- `Content` (string) and `DialogTemplates.Content` (render fragment) are mutually
  exclusive in Syncfusion. The shim prefers `ChildContent` / `DialogTemplates.Content`
  over the string `Content` parameter; when only the string is present it is rendered as
  `MarkupString`.
- Name divergence: Sunfish has `SunfishWindow`, Syncfusion has `SfDialog`. The compat
  shim keeps Syncfusion's name (consumer-facing).

---

## SfTooltip

- **Syncfusion target:** `Syncfusion.Blazor.Popups.SfTooltip`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishTooltip`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Content` | `string` | → `SunfishTooltip.Text` | Mapped |
| `Target` | `string` | → `TargetSelector` | Mapped |
| `Position` | `Position` enum (12 values) | Collapsed to 4 Sunfish cardinals — see below | LogAndFallback |
| `OpensOn` | `string` | Mapped (`Hover`/`Click`/`Focus`); `Custom` logs warning and falls back to Hover | Mapped |
| `OpenDelay` / `CloseDelay` | `double` | Not-in-scope in initial shim | Not-in-scope |
| `ShowTipPointer` | `bool` | Not wired | Not-in-scope |
| `Width` / `Height` | `string` | Passthrough | Mapped |
| `IsSticky` | `bool` | Log-and-drop | Dropped |
| `MouseTrail` | `bool` | Log-and-drop | Dropped |
| `WindowCollision` | `bool` | Log-and-drop | Dropped |
| `CssClass` | `string` | Forwarded | Forwarded |
| `ContentTemplate` | `RenderFragment` | → `SunfishTooltip.TooltipTemplate` | Mapped |

### Divergences — Position collapse

Syncfusion's 12-value `Position` enum collapses to 4 Sunfish cardinal placements:

| Syncfusion Position | Sunfish TooltipPosition |
|---|---|
| `TopLeft` / `TopCenter` / `TopRight` | `Top` |
| `BottomLeft` / `BottomCenter` / `BottomRight` | `Bottom` |
| `LeftTop` / `LeftCenter` / `LeftBottom` | `Left` |
| `RightTop` / `RightCenter` / `RightBottom` | `Right` |

Consumers relying on the corner-biased positions (e.g. `TopLeft` vs `TopCenter`) lose the
fine placement; the tooltip still appears on the correct edge.

---

## SfToast (→ Notification)

- **Syncfusion target:** `Syncfusion.Blazor.Notifications.SfToast`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Feedback.SunfishSnackbarHost`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Title` | `string` | Consumed when `ShowAsync()` is called (combined with `Content`) | Mapped |
| `Content` | `string` | Consumed when `ShowAsync()` is called (`NotificationModel.Message`) | Mapped |
| `Timeout` | `int` | → `NotificationModel.CloseAfterMs` | Mapped |
| `ShowCloseButton` | `bool` | → `NotificationModel.Closeable` | Mapped |
| `Icon` | `string` | Not-in-scope in initial shim | Not-in-scope |
| `CssClass` | `string` | Not wired | Not-in-scope |
| `Width` / `Height` | `string` | Not wired (Sunfish snackbar sizes automatically) | Not-in-scope |
| `NewestOnTop` | `bool` | Log-and-drop | Dropped |
| `ShowProgressBar` | `bool` | Not wired | Not-in-scope |
| `Target` | `string` | Not wired | Not-in-scope |
| `ExtendedTimeout` | `int` | Log-and-drop | Dropped |
| `ProgressDirection` | `ProgressDirection` enum | Log-and-drop | Dropped |

### Imperative methods

Syncfusion exposes `ShowAsync(ToastModel)` / `HideAsync()` via `@ref`. compat-syncfusion
mirrors these on `SfToast`, forwarding to the injected `ISunfishNotificationService`:

- `SfToast.ShowAsync(NotificationModel)` — shows a Sunfish `NotificationModel` directly.
  Consumers who previously passed `ToastModel` must update to the Sunfish type.
- `SfToast.ShowAsync()` — convenience overload that builds a `NotificationModel` from the
  component's `Title` / `Content` / `Timeout` / `ShowCloseButton` defaults.
- `SfToast.HideAsync()` — hides all active notifications.

---

## Shared / EventArgs shims

The following EventArgs types live under `packages/compat-syncfusion/EventArgs/` in the
root `Sunfish.Compat.Syncfusion` namespace so consumer handler signatures compile after a
`using` swap:

| EventArgs type | Source component(s) | Notes |
|---|---|---|
| `ChangeEventArgs<TChecked>` | `SfCheckBox` | Wraps the new checked value. |
| `ChangedEventArgs` / `ChangedEventArgs<TValue>` | `SfTextBox`, `SfDatePicker` | Value-commit. |
| `DropDownChangeEventArgs<TValue, TItem>` | `SfDropDownList`, `SfComboBox` | Selection change. |
| `FilteringEventArgs` | DropDowns | Filter-text change. |
| `SelectEventArgs<TItem>` | DropDowns | Item-select. |
| `CustomValueSpecifierEventArgs<TValue>` | `SfComboBox` | Custom-value handling. |
| `InputEventArgs` | `SfTextBox` | Per-keystroke. |
| `FocusInEventArgs` / `FocusOutEventArgs` | `SfTextBox` | Focus lifecycle. |
| `FocusEventArgs` | `SfDatePicker` | Focus/blur. |
| `RowSelectEventArgs<TValue>` | `SfGrid` | Row selected (wired incrementally). |
| `RowSelectingEventArgs<TValue>` | `SfGrid` | Cancellable pre-select. |
| `RowDataBoundEventArgs<TValue>` | `SfGrid` | Row render. |
| `ActionBeginArgs` / `ActionCompleteArgs` / `ActionFailureArgs` | `SfGrid` | Grid lifecycle. |
| `FilterEventArgs` / `SortEventArgs` | `SfGrid` | Grid filter / sort. |
| `BeforeOpenEventArgs` / `BeforeCloseEventArgs` | `SfDialog` | Dialog lifecycle. |
| `TooltipEventArgs` | `SfTooltip` | Tooltip lifecycle. |
| `ToastBeforeOpenArgs` / `ToastOpenArgs` / `ToastCloseArgs` / `ToastClickEventArgs` | `SfToast` | Toast lifecycle. |

Per the precedent in `docs/compat-telerik-mapping.md` § "Grid EventArgs shims", these
ship as data-only types. Functional wiring from wrapper events to these types is
incremental (tracked as future gap-closure PRs under the POLICY gate).

---

## Enum shims

See `packages/compat-syncfusion/Enums/` for the following Syncfusion-shaped enum shims:

- `IconPosition` (SfButton)
- `IconSize` (SfIcon)
- `LabelPosition` (SfCheckBox) / `FormLabelPosition` (SfDataForm — distinct to avoid name collision)
- `InputType` / `FloatLabelType` / `AutoComplete` (SfTextBox)
- `FilterType` / `SelectionMode` / `SelectionType` / `SortDirection` (SfGrid children)
- `CalendarView` (SfDatePicker)
- `Position` (SfTooltip — 12-value)
- `ProgressDirection` (SfToast)
- `ValidationDisplayMode` / `ButtonsAlignment` (SfDataForm)

Adding, renaming, or reordering enum members is a policy-gated breaking change.
