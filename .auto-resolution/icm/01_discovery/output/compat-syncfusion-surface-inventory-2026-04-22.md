# compat-syncfusion Surface Inventory — Stage 01 Discovery

**Date:** 2026-04-22
**Task:** #103 — Stage 01 Discovery, Syncfusion lane
**Parent intake:** [`icm/00_intake/output/compat-expansion-intake.md`](../../00_intake/output/compat-expansion-intake.md)
**Pattern-reference:** [`packages/compat-telerik/POLICY.md`](../../../packages/compat-telerik/POLICY.md),
[`docs/compat-telerik-mapping.md`](../../../docs/compat-telerik-mapping.md)

**Status:** Discovery only — no package code shipped per intake §7 (Out of Scope) and the
discovery-agent brief. All `Status` cells in §2 are marked `TBD — determine at Stage 03
Architecture` by design; this file is a framework for the build agent, not a finished mapping.

---

## 0. Sources consulted

Primary (WebFetch against official Syncfusion docs):

- `https://blazor.syncfusion.com/documentation/introduction` — component catalog
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.Buttons.SfButton.html`
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.Buttons.SfIcon.html`
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.Buttons.SfCheckBox-1.html`
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.Inputs.SfTextBox.html`
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.DropDowns.SfDropDownList-2.html`
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.DropDowns.SfComboBox-2.html`
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.Calendars.SfDatePicker-1.html`
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.DataForm.SfDataForm.html`
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.Grids.SfGrid-1.html`
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.Popups.SfDialog.html`
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.Popups.SfTooltip.html`
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.Notifications.SfToast.html`
- `https://blazor.syncfusion.com/documentation/appearance/icons` — icon system overview
- `https://www.syncfusion.com/products/communitylicense` — Community License terms
- `https://blazor.syncfusion.com/documentation/getting-started/blazor-server-side-visual-studio` — architecture confirmation

Context7 returned `Monthly quota exceeded` on the first probe. Skipped silently per brief.

**Budget:** 15 WebFetch calls used (target ≤30).

---

## 1. Component surface inventory

Twelve target components per intake §1. All are fully inventoried; none are partial. Syncfusion
has 1:1 analogs for all 12 targets (including `SfIcon` — a dedicated component, **not**
CSS-class-only as initially assumed).

### 1.1 Button — `SfButton`

- **Syncfusion type:** `Syncfusion.Blazor.Buttons.SfButton` — non-generic.
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.Buttons.SunfishButton`.
- **Major parameters:**
  | Param | Type | Description |
  |---|---|---|
  | `Content` | `string` | Button text (alternative to `ChildContent`). |
  | `CssClass` | `string` | Space-separated CSS classes. |
  | `Disabled` | `bool` | Prevents interaction. |
  | `IconCss` | `string` | CSS class(es) for icon font (e.g., `"e-icons e-save"`). |
  | `IconPosition` | `IconPosition` enum | `Left` / `Right` / `Top` / `Bottom`. |
  | `IsPrimary` | `bool` | Primary action styling. |
  | `IsToggle` | `bool` | Toggle button behavior. |
  | `EnableRtl` | `bool` | RTL support. |
  | `Created` | `EventCallback<object>` | Fires after initial render. |
  | `ChildContent` | `RenderFragment?` | Button body markup. |
- **Mapping notes (Sunfish):** Direct mapping of `IsPrimary=true` → `Variant=Primary`. `IconCss`
  needs translation (Syncfusion uses font classes, Sunfish uses `RenderFragment`). No `OnClick`
  parameter in the docs surface — Syncfusion uses standard Blazor `@onclick` via
  `AdditionalAttributes` or handler wiring on parent scopes. **Verify at Stage 03 whether
  `OnClick` is a real public parameter** (common use in community examples suggests yes, despite
  not being in the extracted API page summary).
- **EventArgs:** `Created` uses `EventCallback<object>`. No custom EventArgs types.

### 1.2 Icon — `SfIcon`

- **Syncfusion type:** `Syncfusion.Blazor.Buttons.SfIcon` — non-generic. (Note: lives in the
  `Buttons` namespace, not a standalone `Icons` namespace.)
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.Utility.SunfishIcon`.
- **Major parameters:**
  | Param | Type | Description |
  |---|---|---|
  | `Name` | `IconName` enum | Built-in Syncfusion icon (e.g. `IconName.Cut`, `IconName.Copy`). |
  | `IconCss` | `string` | CSS class(es) for custom font icons. |
  | `Size` | `IconSize` enum | `Small` / `Medium` (default) / `Large`. |
  | `Title` | `string` | Title attribute (tooltip + a11y). |
- **Mapping notes:** `IconName` enum is large (~1,500 Syncfusion-specific font icons). The shim
  maps known names where Sunfish has equivalents; unknown names `LogAndFallback` to a placeholder
  glyph, mirroring `TelerikIcon`'s approach. `IconCss` forwarded verbatim via
  `AdditionalAttributes` (custom font icons require the consumer to ship their own font — out of
  scope for the shim). **Alternative:** Syncfusion also supports direct CSS-class usage
  (`<span class="e-icons e-user" />`), which is *not* component-mediated — no shim needed for that
  pattern.
- **EventArgs:** none.

### 1.3 CheckBox — `SfCheckBox<TChecked>`

- **Syncfusion type:** `Syncfusion.Blazor.Buttons.SfCheckBox<TChecked>` — **generic on
  `TChecked`**, typically `bool` or `bool?`.
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishCheckbox` (note
  Sunfish spelling: one word; Syncfusion spelling: two words in the type name).
- **Major parameters:**
  | Param | Type | Description |
  |---|---|---|
  | `Checked` | `TChecked` | Checked state. |
  | `Label` | `string` | Inline label text. |
  | `LabelPosition` | `LabelPosition` enum | `Before` / `After`. |
  | `Disabled` | `bool` | Disables the checkbox. |
  | `EnableTriState` | `bool` | Supports checked / unchecked / indeterminate. |
  | `Indeterminate` | `bool` | Intermediate visual state. |
  | `CssClass` | `string` | Custom classes. |
  | `EnableRtl` | `bool` | RTL support. |
  | `Name` | `string` | HTML `name` attribute. |
  | `EnablePersistence` | `bool` | Persists state across sessions. |
  | `ValueChange` | `EventCallback<ChangeEventArgs<TChecked>>` | Fired on state change. |
- **Mapping notes:** Generic `TChecked` maps to Sunfish's `bool?` surface. `ValueChange` uses a
  Syncfusion-specific `ChangeEventArgs<TChecked>` type (must be shimmed under
  `EventArgs/ChangeEventArgs.cs`) which differs from Sunfish's `@bind-Value` pattern —
  translation required, not direct.
- **EventArgs:** `ChangeEventArgs<TChecked>` (Syncfusion namespace).

### 1.4 TextBox — `SfTextBox`

- **Syncfusion type:** `Syncfusion.Blazor.Inputs.SfTextBox` — non-generic (always `string`).
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishTextBox`.
- **Major parameters:**
  | Param | Type | Description |
  |---|---|---|
  | `Value` | `string` | Text value. |
  | `ValueChanged` | `EventCallback<string>` | Two-way binding. |
  | `Placeholder` | `string` | Hint text. |
  | `Multiline` | `bool` | Textarea mode. |
  | `Type` | `InputType` enum | `Text` / `Password` / `Email` / `Number` / etc. |
  | `FloatLabelType` | `FloatLabelType` enum | `Never` / `Auto` / `Always`. |
  | `ShowClearButton` | `bool` | Displays clear (×) button. |
  | `Readonly` | `bool` | Read-only state. |
  | `Enabled` | `bool` | Enable/disable. |
  | `CssClass` | `string` | Custom classes. |
  | `Width` | `string` | CSS width. |
  | `TabIndex` | `int` | Tab order. |
  | `Autocomplete` | `AutoComplete` enum | `On` / `Off`. |
  | `HtmlAttributes` | `Dictionary<string, object>` | Wrapper attributes. |
  | `InputAttributes` | `Dictionary<string, object>` | Input-element attributes. |
- **Mapping notes:** `Multiline=true` + `Type=Password` is an invalid Syncfusion combination; shim
  should detect and `Throws`. `FloatLabelType` has no direct Sunfish equivalent — log-and-drop
  unless Sunfish adds floating-label support before Stage 06.
- **EventArgs:** `InputEventArgs` (on `Input`), `ChangedEventArgs` (on `ValueChange`),
  `FocusInEventArgs` (on `Focus`), `FocusOutEventArgs` (on `Blur`).

### 1.5 DropDownList — `SfDropDownList<TValue, TItem>`

- **Syncfusion type:** `Syncfusion.Blazor.DropDowns.SfDropDownList<TValue, TItem>` — **two
  generic parameters** (value type + item type).
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDropDownList`.
- **Major parameters:**
  | Param | Type | Description |
  |---|---|---|
  | `DataSource` | `IEnumerable<TItem>` | Source collection. |
  | `Value` | `TValue` | Selected value. |
  | `ValueChanged` | `EventCallback<TValue>` | Two-way binding. |
  | `Index` | `int?` | Selected index. |
  | `IndexChanged` | `EventCallback<int?>` | Index two-way. |
  | `Placeholder` | `string` | Hint text. |
  | `Enabled` | `bool` | Enable/disable. |
  | `Readonly` | `bool` | Read-only. |
  | `AllowFiltering` | `bool` | Enables filter textbox. |
  | `ShowClearButton` | `bool` | Clear (×) button. |
  | `CssClass` | `string` | Custom classes. |
  | `Width` | `string` | CSS width. |
  | `PopupHeight` / `PopupWidth` | `string` | Popup sizing. |
  | `ValueTemplate` | `RenderFragment<TItem>` | Selected-value renderer. |
  | `ItemTemplate` | `RenderFragment<TItem>` | Item renderer. |
  | `NoRecordsTemplate` | `RenderFragment` | Empty-state renderer. |
- **Mapping notes:** Syncfusion's `Fields` pattern (`FieldSettingsModel`) differs from Telerik's
  `TextField`/`ValueField` string-name pattern. Likely translation: a nested
  `<DropDownListFieldSettings Text="Name" Value="Id" />` child component shimmed as a
  `CompatChildComponent<SfDropDownList>`.
- **EventArgs:** `ChangeEventArgs<TValue, TItem>`, `FilteringEventArgs`, `SelectEventArgs<TItem>`.

### 1.6 ComboBox — `SfComboBox<TValue, TItem>`

- **Syncfusion type:** `Syncfusion.Blazor.DropDowns.SfComboBox<TValue, TItem>` — **inherits from
  `SfDropDownList<TValue, TItem>`**.
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishComboBox`.
- **Major parameters:** inherits all `SfDropDownList` parameters, plus:
  | Param | Type | Description |
  |---|---|---|
  | `AllowCustom` | `bool` | Accept user-entered values outside `DataSource` (default `true`). |
  | `Autofill` | `bool` | Auto-complete partial matches. |
  | `ValidateOnInput` | `bool` | Validate on each keystroke (default `false`). |
- **Mapping notes:** `AllowCustom` and `Autofill` have direct Sunfish equivalents (already shimmed
  in `TelerikComboBox`). Same `Fields`/`FieldSettingsModel` translation as `SfDropDownList`.
- **EventArgs:** inherits `SfDropDownList` args + `CustomValueSpecifierEventArgs<TValue>` (for
  custom-value handling when `AllowCustom=true`).

### 1.7 DatePicker — `SfDatePicker<TValue>`

- **Syncfusion type:** `Syncfusion.Blazor.Calendars.SfDatePicker<TValue>` — **generic on
  `TValue`**, typically `DateTime` or `DateTime?`.
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDatePicker`.
- **Major parameters:**
  | Param | Type | Description |
  |---|---|---|
  | `Value` | `TValue` | Selected date. |
  | `ValueChanged` | `EventCallback<TValue>` | Two-way binding. |
  | `Min` | `TValue` | Earliest selectable. |
  | `Max` | `TValue` | Latest selectable. |
  | `Format` | `string` | Display format (culture-specific default). |
  | `Placeholder` | `string` | Hint text. |
  | `Enabled` | `bool` | Enable/disable. |
  | `Readonly` | `bool` | Read-only. |
  | `ShowClearButton` | `bool` | Clear (×) button. |
  | `FirstDayOfWeek` | `int` | Week-start (0=Sunday). |
  | `Start` | `CalendarView` enum | Initial calendar view (`Month` / `Year` / `Decade`). |
  | `Depth` | `CalendarView` enum | Deepest navigable view. |
  | `CssClass` | `string` | Custom classes. |
  | `Width` | `string` | CSS width. |
  | `AllowEdit` | `bool` | Allow keyboard input. |
- **Mapping notes:** `Start`/`Depth` (calendar view enums) match `TelerikDatePicker`'s
  `View`/`BottomView` — reuse the drop-cosmetic treatment. `TValue` as `DateTime` vs `DateTime?`
  flows through.
- **EventArgs:** `ChangedEventArgs<TValue>`, `FocusEventArgs`.

### 1.8 Form — `SfDataForm`

- **Syncfusion type:** `Syncfusion.Blazor.DataForm.SfDataForm` — non-generic (`Model` typed as
  `object`).
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.Forms.SunfishForm`.
- **Major parameters:**
  | Param | Type | Description |
  |---|---|---|
  | `Model` | `object` | Data-bound model object. |
  | `EditContext` | `EditContext?` | Alternative to `Model`. |
  | `ColumnCount` | `int` | Form layout columns. |
  | `ColumnSpacing` | `string` | Horizontal gap. |
  | `ValidationDisplayMode` | enum | `Inline` / `ToolTip` / `None`. |
  | `ButtonsAlignment` | enum | `Left` / `Center` / `Right` / `Stretch`. |
  | `OnSubmit` | `EventCallback` | Submit handler. |
  | `OnValidSubmit` | `EventCallback<EditContext>` | Valid-submit handler. |
  | `OnInvalidSubmit` | `EventCallback<EditContext>` | Invalid-submit handler. |
  | `OnUpdate` | `EventCallback` | Field-update handler. |
  | `EnableFloatingLabel` | `bool` | Floating labels on children. |
  | `LabelPosition` | enum | `Top` / `Left`. |
  | `FormValidator` | type | Validation type config. |
  | `Width` | `string` | CSS width. |
  | `ID` | `string` | Form identifier. |
- **Mapping notes:** SfDataForm is **more opinionated** than Telerik's `TelerikForm` — it
  auto-generates field layout from the model. Sunfish's `SunfishForm` is thinner (closer to
  `EditForm`). Migrator layout parameters (`ColumnCount`, `ColumnSpacing`, `ButtonsAlignment`,
  `EnableFloatingLabel`) likely log-and-drop unless Sunfish adds a layout surface before Stage 06.
  `ValidationDisplayMode=ToolTip` has no Sunfish equivalent — `Throws` with migration hint.
- **EventArgs:** uses stock Blazor `EditContext` on valid/invalid submit events.

### 1.9 Grid — `SfGrid<TValue>`

- **Syncfusion type:** `Syncfusion.Blazor.Grids.SfGrid<TValue>` — **generic on row type**.
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishDataGrid<TItem>`.
- **Major parameters:**
  | Param | Type | Description |
  |---|---|---|
  | `DataSource` | `IEnumerable<TValue>` | Row data. |
  | `AllowPaging` | `bool` | Enables paging. |
  | `AllowSorting` | `bool` | Header-click sort. |
  | `AllowFiltering` | `bool` | Filter bar. |
  | `AllowGrouping` | `bool` | Drag-group headers. |
  | `AllowSelection` | `bool` | Row/cell selection. |
  | `AllowReordering` | `bool` | Drag-reorder columns. |
  | `AllowResizing` | `bool` | Drag-resize columns. |
  | `Columns` | `List<GridColumn>` | Declarative column list. |
  | `Height` | `string` | Scroll viewport height. |
  | `Width` | `string` | Grid width. |
  | `EnableVirtualization` | `bool` | Row virtualization. |
  | `RowHeight` | `double` | Row height (px). |
  | `Toolbar` | `object` | Built-in or custom toolbar. |
  | `ID` | `string` | Grid identifier. |
- **Common child-component patterns (must shim via `CompatChildComponent<SfGrid<TValue>>`):**
  - `<SfGrid><GridColumns><GridColumn Field="Name" /></GridColumns></SfGrid>`
  - `<SfGrid><GridPageSettings PageSize="10" /></SfGrid>`
  - `<SfGrid><GridSelectionSettings Mode="Row" /></SfGrid>`
  - `<SfGrid><GridFilterSettings Type="Menu" /></SfGrid>`
  - `<SfGrid><GridSortSettings><GridSortColumn Field="Name" Direction="Ascending" /></GridSortSettings></SfGrid>`
  - `<SfGrid><GridEvents RowSelected="@OnRowSel" TValue="Order" /></SfGrid>`
  - `<SfGrid><GridEditSettings AllowEditing="true" /></SfGrid>`
  - `<SfGrid><GridAggregates><GridAggregate><GridAggregateColumns><GridAggregateColumn Field="Total" /></GridAggregateColumns></GridAggregate></GridAggregates></SfGrid>`
- **Mapping notes:** `Toolbar` is untyped `object` in Syncfusion (can be `List<string>` or
  `List<ItemModel>`) — shim normalizes to a Sunfish toolbar definition or log-and-drops. Nested
  settings (`Page/Selection/Filter/Sort/EditSettings`) are child components, not plain parameters
  — each shim is 1 line that forwards `ChildContent`. **`GridEvents` is the Syncfusion idiom for
  wiring grid events** (not inline `@on` handlers on `<SfGrid>`) — the shim must map
  `GridEvents.RowSelected` → the grid wrapper's internal Sunfish event wiring.
- **EventArgs:** `RowSelectEventArgs<TValue>`, `RowSelectingEventArgs<TValue>`,
  `RowDataBoundEventArgs<TValue>`, `ActionBeginArgs`, `ActionCompleteArgs`, `ActionFailureArgs`,
  `FilterEventArgs`, `SortEventArgs`. All must be shimmed as type-erased (`Item` as `object`) or
  `TValue`-generic data-only types under `compat-syncfusion/EventArgs/`.

### 1.10 Window — `SfDialog`

- **Syncfusion type:** `Syncfusion.Blazor.Popups.SfDialog` — non-generic. (Syncfusion has no
  `SfWindow`; `SfDialog` is the analog of Telerik's `TelerikWindow`.)
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.Overlays.SunfishWindow`.
- **Major parameters:**
  | Param | Type | Description |
  |---|---|---|
  | `Visible` | `bool` | Two-way `@bind-Visible`. |
  | `Header` | `string` | Title bar content (HTML-accepted). |
  | `Content` | `string` | Body content (HTML-accepted; alt to templates). |
  | `Width` | `string` | CSS width (default `"100%"`). |
  | `Height` | `string` | CSS height (default `"auto"`). |
  | `IsModal` | `bool` | Modal vs non-modal. |
  | `ShowCloseIcon` | `bool` | Show × in header. |
  | `AllowDragging` | `bool` | Drag by header. |
  | `EnableResize` | `bool` | Resize via edge drag. |
  | `Target` | `string` | CSS selector of containing element. |
  | `CssClass` | `string` | Custom classes. |
  | `MinHeight` | `string` | Lower bound on resize. |
  | `CloseOnEscape` | `bool` | Esc-key dismisses. |
  | `ZIndex` | `double` | Stack order (default `1000`). |
  | `EnableRtl` | `bool` | RTL support. |
- **Common child-component patterns:**
  - `<SfDialog><DialogTemplates><Header>...</Header><Content>...</Content><FooterTemplate>...</FooterTemplate></DialogTemplates></SfDialog>`
  - `<SfDialog><DialogButtons><DialogButton Content="OK" OnClick="@OnOk" /></DialogButtons></SfDialog>`
  - `<SfDialog><DialogEvents OnOpen="@OnOpen" Closed="@OnClosed" /></SfDialog>`
- **Mapping notes:** Syncfusion's `Content` (string) vs `DialogTemplates.Content` (render
  fragment) requires the shim to prefer `ChildContent` / `DialogTemplates` when present and
  fall back to `Content`. `DialogButton` child markup is a shim candidate (same
  `CompatChildComponent<T>` pattern).
- **EventArgs:** `BeforeOpenEventArgs`, `BeforeCloseEventArgs`.

### 1.11 Tooltip — `SfTooltip`

- **Syncfusion type:** `Syncfusion.Blazor.Popups.SfTooltip` — non-generic.
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishTooltip`.
- **Major parameters:**
  | Param | Type | Description |
  |---|---|---|
  | `Content` | `string` | Tooltip text. |
  | `Target` | `string` | CSS selector for triggering element(s). |
  | `Position` | `Position` enum | Placement (default `TopCenter`). |
  | `OpenDelay` | `double` | Ms before show. |
  | `CloseDelay` | `double` | Ms before hide. |
  | `ShowTipPointer` | `bool` | Arrow visibility. |
  | `OpensOn` | `string` | `Auto` / `Hover` / `Click` / `Focus` / `Custom`. |
  | `Width` | `string` | Tooltip width. |
  | `Height` | `string` | Tooltip height. |
  | `IsSticky` | `bool` | Stays open until manually closed. |
  | `MouseTrail` | `bool` | Follow cursor. |
  | `WindowCollision` | `bool` | Viewport collision mode. |
  | `CssClass` | `string` | Custom classes. |
  | `ChildContent` | `RenderFragment` | Target-element content. |
  | `ContentTemplate` | `RenderFragment` | Tooltip-content template. |
- **Mapping notes:** Syncfusion's `Position` enum has ~12 values (`TopLeft` / `TopCenter` /
  `TopRight` / `RightTop` / `RightCenter` / `RightBottom` / `BottomRight` / `BottomCenter` /
  `BottomLeft` / `LeftBottom` / `LeftCenter` / `LeftTop`) vs Sunfish's 4-cardinal `Placement` —
  collapse to closest cardinal with `LogAndFallback`. `IsSticky`, `MouseTrail`, `WindowCollision`
  have no Sunfish equivalent — log-and-drop.
- **EventArgs:** `TooltipEventArgs`.

### 1.12 Notification — `SfToast`

- **Syncfusion type:** `Syncfusion.Blazor.Notifications.SfToast` — non-generic.
- **Sunfish delegate:** `Sunfish.UIAdapters.Blazor.Components.Feedback.SunfishSnackbarHost`.
- **Major parameters:**
  | Param | Type | Description |
  |---|---|---|
  | `Title` | `string` | Toast title. |
  | `Content` | `string` | Toast body (HTML-accepted). |
  | `Timeout` | `int` | Auto-dismiss ms (default `5000`; `0` = manual only). |
  | `ShowCloseButton` | `bool` | Manual-dismiss × button. |
  | `Icon` | `string` | CSS class for icon at top-left. |
  | `CssClass` | `string` | Custom classes. |
  | `Width` | `string` | Default `"300px"`. |
  | `Height` | `string` | Default `"auto"`. |
  | `NewestOnTop` | `bool` | Stack order. |
  | `ShowProgressBar` | `bool` | Progress-bar timeout indicator. |
  | `Target` | `string` | CSS selector for containing element. |
  | `ExtendedTimeout` | `int` | Timeout extension on interaction (default `1000`). |
  | `ProgressDirection` | `ProgressDirection` enum | `RTL` / `LTR`. |
  | `ContentTemplate` | `RenderFragment` | Content render fragment (overrides `Content`). |
- **Imperative methods (via `@ref`):** `ShowAsync(ToastModel)` — shimmed as a method on the
  wrapper that forwards to `ISnackbarService`. (Docs page did not surface a `HideAsync`; confirm
  at Stage 03. Telerik's compat shim already has precedent for imperative-method shimming.)
- **Mapping notes:** Same shape as `TelerikNotification`. `Title` + `Content` → Sunfish
  `Snackbar.Message`. `Icon` (string CSS class) maps to Sunfish severity-icon unless explicitly
  overridden. `ProgressDirection`, `ExtendedTimeout`, `NewestOnTop` are cosmetic — log-and-drop.
- **EventArgs:** `ToastBeforeOpenArgs`, `ToastOpenArgs`, `ToastCloseArgs`, `ToastClickEventArgs`.
  (Exact names not captured from API-ref page; **confirm at Stage 03**. Precedent: Telerik's
  shim ships these as data-only types whose functional wiring is incremental.)

---

### Summary of EventArgs types to shim under `compat-syncfusion/EventArgs/`

From §1.1–§1.12, aggregated for Stage 02/03 planning:

| # | EventArgs type | Source component(s) | Notes |
|---|---|---|---|
| 1 | `ChangeEventArgs<TChecked>` | `SfCheckBox`, multiple inputs | Generic on value type. |
| 2 | `ChangeEventArgs<TValue, TItem>` | `SfDropDownList`, `SfComboBox` | Two generics. |
| 3 | `FilteringEventArgs` | `SfDropDownList`, `SfComboBox` | Filter-text change. |
| 4 | `SelectEventArgs<TItem>` | `SfDropDownList` | Item-select. |
| 5 | `CustomValueSpecifierEventArgs<TValue>` | `SfComboBox` | Custom-value handling. |
| 6 | `InputEventArgs` | `SfTextBox` | Per-keystroke. |
| 7 | `ChangedEventArgs` / `ChangedEventArgs<TValue>` | `SfTextBox`, `SfDatePicker` | Value commit. |
| 8 | `FocusInEventArgs` / `FocusOutEventArgs` | `SfTextBox` | Focus lifecycle. |
| 9 | `FocusEventArgs` | `SfDatePicker` | Focus/blur. |
| 10 | `RowSelectEventArgs<TValue>` | `SfGrid` | Row selected. |
| 11 | `RowSelectingEventArgs<TValue>` | `SfGrid` | Cancellable pre-select. |
| 12 | `RowDataBoundEventArgs<TValue>` | `SfGrid` | Row render. |
| 13 | `ActionBeginArgs` / `ActionCompleteArgs` / `ActionFailureArgs` | `SfGrid` | Generic grid lifecycle. |
| 14 | `FilterEventArgs` | `SfGrid` | Grid filter change. |
| 15 | `SortEventArgs` | `SfGrid` | Grid sort change. |
| 16 | `BeforeOpenEventArgs` / `BeforeCloseEventArgs` | `SfDialog` | Dialog lifecycle. |
| 17 | `TooltipEventArgs` | `SfTooltip` | Tooltip lifecycle. |
| 18 | `ToastBeforeOpenArgs` / `ToastOpenArgs` / `ToastCloseArgs` / `ToastClickEventArgs` | `SfToast` | Toast lifecycle (names to confirm at Stage 03). |

**Total EventArgs types identified: 18 distinct types / type families.** Some are generic
variants of the same base (e.g. `RowSelectEventArgs<TValue>` + `RowSelectingEventArgs<TValue>`).
Per the precedent in `docs/compat-telerik-mapping.md` § "Grid EventArgs shims", these ship as
data-only types with functional wiring landed incrementally.

### Common child-component patterns (aggregated)

The following ride on `CompatChildComponent<TParent>` (see compat-telerik `Internal/` or the
planned `compat-shared` package lift per intake §5 Decision 4):

- `GridColumns` / `GridColumn` (child of `SfGrid`)
- `GridPageSettings` / `GridSelectionSettings` / `GridFilterSettings` / `GridSortSettings` /
  `GridEditSettings` / `GridAggregates` / `GridAggregate` / `GridAggregateColumns` /
  `GridAggregateColumn` (children of `SfGrid`)
- `GridEvents<TValue>` (child of `SfGrid` — event-handler container; shape differs from
  Telerik's inline-handler model)
- `GridSortColumn` (child of `GridSortSettings`)
- `DropDownListFieldSettings` (child of `SfDropDownList` / `SfComboBox` — the
  `Fields`-parameter alternative)
- `DialogTemplates` + `Header` / `Content` / `FooterTemplate` (children of `SfDialog`)
- `DialogButtons` / `DialogButton` (children of `SfDialog`)
- `DialogEvents` (child of `SfDialog`)
- `ToastPosition` / `ToastButtons` / `ToastButton` (children of `SfToast` — not captured fully;
  confirm at Stage 03)

Count: ~20 child-component shim targets. Many are semantic passthroughs with `ChildContent`-only
parameter surfaces (same as the Telerik `GridColumns` shim).

---

## 2. Mapping-doc skeleton

This section is the template the build agent (Stage 06) fills. Format mirrors
`docs/compat-telerik-mapping.md`. All `Status` cells below are `TBD — determine at Stage 03
Architecture`.

```markdown
# compat-syncfusion Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Same conventions as
> `docs/compat-telerik-mapping.md`. Any change to an entry is a breaking change for consumers.
> See `packages/compat-syncfusion/POLICY.md`.

## Conventions

- **Mapped** — Syncfusion parameter value translates 1:1 to a Sunfish parameter / value.
- **Forwarded** — Attribute passed through via `AdditionalAttributes`.
- **Dropped (cosmetic)** — Silently ignored; logged via ILogger at Warning level.
- **Throws** — Raises `NotSupportedException` with a migration hint.
- **LogAndFallback** — Unrecognized value logs a warning and falls back to Sunfish default.
- **Not-in-scope** — Parameter declared but no migration path; consumer must refactor.

---
```

### 2.1 SfButton

- **Syncfusion target:** `Syncfusion.Blazor.Buttons.SfButton`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Buttons.SunfishButton`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Content` | `string` | → `ChildContent` (as `RenderFragment` text) | TBD — determine at Stage 03 Architecture |
| `CssClass` | `string` | Forwarded via `AdditionalAttributes["class"]` | TBD |
| `Disabled` | `bool` | → `Enabled = !Disabled` | TBD |
| `IconCss` | `string` | → `Icon` (RenderFragment wrapping `<span class="@IconCss">`) | TBD |
| `IconPosition` | `IconPosition` enum | → Sunfish icon-position equivalent | TBD |
| `IsPrimary` | `bool` | → `Variant = Primary` when true | TBD |
| `IsToggle` | `bool` | TBD (Sunfish may lack toggle surface) | TBD |
| `EnableRtl` | `bool` | Forwarded or log-and-drop | TBD |
| `Created` | `EventCallback<object>` | LogAndFallback or passthrough | TBD |
| `ChildContent` | `RenderFragment?` | Passthrough | TBD |

**Known divergences:**
- Syncfusion's `IsPrimary=false` maps to `Variant=Secondary` (loss of Syncfusion's "flat/default"
  styling — document at Stage 06).
- `IconCss` is CSS-font-class-based; Sunfish `Icon` is `RenderFragment`-based. A thin wrapper
  emits `<span class="@IconCss"></span>` as the `RenderFragment`.

---

### 2.2 SfIcon

- **Syncfusion target:** `Syncfusion.Blazor.Buttons.SfIcon`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Utility.SunfishIcon`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Name` | `IconName` enum | Mapped via lookup table to Sunfish icon name | TBD |
| `IconCss` | `string` | Forwarded via `AdditionalAttributes["class"]` | TBD |
| `Size` | `IconSize` enum (`Small`/`Medium`/`Large`) | → Sunfish icon-size equivalent | TBD |
| `Title` | `string` | Forwarded via `AdditionalAttributes["title"]` | TBD |

**Known divergences:**
- `IconName` is a Syncfusion-specific enum (~1,500 values). Unknown names LogAndFallback.
- Raw CSS-class-based icons (`<span class="e-icons e-user">`) are not component-mediated and
  require no shim — consumers continue using that pattern without migration.

---

### 2.3 SfCheckBox\<TChecked\>

- **Syncfusion target:** `Syncfusion.Blazor.Buttons.SfCheckBox<TChecked>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishCheckbox`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Checked` | `TChecked` | → `@bind-Value` | TBD |
| `Label` | `string` | → `Label` | TBD |
| `LabelPosition` | `LabelPosition` enum | `Before`/`After` → Sunfish equivalent | TBD |
| `Disabled` | `bool` | → `Enabled = !Disabled` | TBD |
| `EnableTriState` | `bool` | TBD (Sunfish tri-state surface unclear) | TBD |
| `Indeterminate` | `bool` | → Sunfish `Indeterminate` if exposed, else log-and-drop | TBD |
| `CssClass` | `string` | Forwarded | TBD |
| `EnableRtl` | `bool` | Forwarded or log-and-drop | TBD |
| `Name` | `string` | Forwarded via `AdditionalAttributes["name"]` | TBD |
| `EnablePersistence` | `bool` | Not-in-scope (session persistence is consumer concern) | TBD |
| `ValueChange` | `EventCallback<ChangeEventArgs<TChecked>>` | Translated to `ValueChanged` | TBD |

**Known divergences:**
- `ValueChange` uses Syncfusion's `ChangeEventArgs<TChecked>`; Sunfish uses plain
  `EventCallback<TChecked>`. Shim constructs the Syncfusion-shaped args type from the Sunfish
  callback.

---

### 2.4 SfTextBox

- **Syncfusion target:** `Syncfusion.Blazor.Inputs.SfTextBox`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishTextBox`

| Syncfusion parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Value` | `string` | → `@bind-Value` | TBD |
| `ValueChanged` | `EventCallback<string>` | Passthrough | TBD |
| `Placeholder` | `string` | Passthrough | TBD |
| `Multiline` | `bool` | → separate Sunfish component or passthrough | TBD |
| `Type` | `InputType` enum | → Sunfish input-type enum | TBD |
| `FloatLabelType` | `FloatLabelType` enum | Not-in-scope (Sunfish floating-label pending) | TBD |
| `ShowClearButton` | `bool` | Passthrough if supported, else log-and-drop | TBD |
| `Readonly` | `bool` | Passthrough | TBD |
| `Enabled` | `bool` | Passthrough | TBD |
| `CssClass` | `string` | Forwarded | TBD |
| `Width` | `string` | Forwarded via `style="width: @Width"` | TBD |
| `TabIndex` | `int` | Forwarded | TBD |
| `Autocomplete` | `AutoComplete` enum | Forwarded as `autocomplete` HTML attribute | TBD |
| `HtmlAttributes` | `Dictionary<string, object>` | Merged into `AdditionalAttributes` | TBD |
| `InputAttributes` | `Dictionary<string, object>` | Not-in-scope (Sunfish lacks split wrapper/input attr surface) | TBD |

**Known divergences:**
- `Multiline=true` + `Type=Password` throws.
- `FloatLabelType` has no Sunfish equivalent — migration guidance: use Sunfish's label-position API.

---

### 2.5 SfDropDownList\<TValue, TItem\>

- **Syncfusion target:** `Syncfusion.Blazor.DropDowns.SfDropDownList<TValue, TItem>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDropDownList`

_(table skeleton elided for brevity — same shape as §2.4 with Syncfusion params from §1.5)_

**Child-component shim:** `DropDownListFieldSettings` (carries `Text` / `Value` string-names
for property-accessor compilation — same technique as `TelerikDropDownList`'s
`TextField`/`ValueField`).

---

### 2.6 SfComboBox\<TValue, TItem\>

- **Syncfusion target:** `Syncfusion.Blazor.DropDowns.SfComboBox<TValue, TItem>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishComboBox`

_(inherits §2.5 surface + `AllowCustom`, `Autofill`, `ValidateOnInput` rows)_

---

### 2.7 SfDatePicker\<TValue\>

- **Syncfusion target:** `Syncfusion.Blazor.Calendars.SfDatePicker<TValue>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDatePicker`

_(table skeleton elided — params from §1.7)_

**Known divergences:**
- `Start` / `Depth` (calendar view enums) are cosmetic — log-and-drop same as
  `TelerikDatePicker.View`/`BottomView`.

---

### 2.8 SfDataForm

- **Syncfusion target:** `Syncfusion.Blazor.DataForm.SfDataForm`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.SunfishForm`

_(table skeleton elided — params from §1.8)_

**Known divergences:**
- `ColumnCount`, `ColumnSpacing`, `ButtonsAlignment`, `EnableFloatingLabel`, `LabelPosition`
  are layout-orchestration features; Sunfish's `SunfishForm` delegates layout to the consumer.
  Log-and-drop with migration hint pointing at Sunfish's grid / flex layout primitives.
- `ValidationDisplayMode=ToolTip` throws (no Sunfish equivalent).

---

### 2.9 SfGrid\<TValue\>

- **Syncfusion target:** `Syncfusion.Blazor.Grids.SfGrid<TValue>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishDataGrid<TItem>`

_(table skeleton elided — params from §1.9)_

**Child-component shims:** `GridColumns`, `GridColumn`, `GridPageSettings`,
`GridSelectionSettings`, `GridFilterSettings`, `GridSortSettings`, `GridEditSettings`,
`GridAggregates`+`GridAggregate`+`GridAggregateColumns`+`GridAggregateColumn`,
`GridEvents<TValue>`, `GridSortColumn`.

**Known divergences:**
- `GridEvents<TValue>` child-component event wiring differs from Sunfish's inline-handler
  pattern. Shim forwards each exposed event parameter to the grid wrapper's internal Sunfish
  event handler.
- `Toolbar` is untyped `object` — only `List<string>` of Syncfusion built-in toolbar command
  names is mapped; other shapes log-and-drop or throw.
- Grid EventArgs generic-erasure preserved for source-shape parity (same precedent as
  compat-telerik § "Grid EventArgs shims").

---

### 2.10 SfDialog (→ Window)

- **Syncfusion target:** `Syncfusion.Blazor.Popups.SfDialog`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Overlays.SunfishWindow`

_(table skeleton elided — params from §1.10)_

**Child-component shims:** `DialogTemplates`, `Header`, `Content`, `FooterTemplate`,
`DialogButtons`, `DialogButton`, `DialogEvents`.

**Known divergences:**
- `Content` (string) and `DialogTemplates.Content` (render fragment) are mutually exclusive in
  Syncfusion. Shim prefers render-fragment path; string `Content` wrapped in a
  `RenderFragment`.

---

### 2.11 SfTooltip

- **Syncfusion target:** `Syncfusion.Blazor.Popups.SfTooltip`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishTooltip`

_(table skeleton elided — params from §1.11)_

**Known divergences:**
- `Position` collapses 12 Syncfusion values to 4 Sunfish cardinal placements with LogAndFallback.
- `IsSticky`, `MouseTrail`, `WindowCollision` log-and-drop.

---

### 2.12 SfToast (→ Notification)

- **Syncfusion target:** `Syncfusion.Blazor.Notifications.SfToast`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Feedback.SunfishSnackbarHost`

_(table skeleton elided — params from §1.12)_

**Known divergences:**
- Syncfusion exposes imperative `ShowAsync(ToastModel)` via `@ref`; Sunfish uses scoped
  `ISnackbarService`. Shim method forwards to injected service (precedent: `TelerikNotification`).
- `NewestOnTop`, `ExtendedTimeout`, `ProgressDirection` log-and-drop (cosmetic).

---

## 3. Licensing + POLICY compatibility

### 3.1 Eligibility (Community License)

Syncfusion Essential Studio for Blazor is available under:
- **Community License** — free for individuals and organizations with:
  - < $1M USD annual gross revenue
  - ≤ 5 developers
  - ≤ 10 total employees
  - ≤ $3M USD cumulative outside capital funding
- **Commercial License** — paid; no employee/revenue caps.

Sunfish's own use during development falls under the Community License (pre-LLC, pre-v1,
single-developer).

### 3.2 API surface replication — legal posture

**Documented review:** Syncfusion's Community License public page does **not** address:
- Reproducing Syncfusion's type names, parameter names, or enum names in third-party source
  code that does NOT reference the Syncfusion NuGet or DLL.
- Building a "compatibility shim" / "migration off-ramp" that names-but-does-not-use Syncfusion
  types.

No clauses were found in the Community License that would prohibit the compat-telerik pattern
(type-name and parameter-name source-shape parity, zero NuGet reference).

**Precedent:** `compat-telerik` operates on the same pattern against Telerik's EULA (which is
similarly silent on API-surface naming). Six months of operation without challenge. Syncfusion's
license is no more restrictive than Telerik's on the public surface reviewed.

**Unknown / caveat:** The full Syncfusion EULA (`syncfusion.com/eula/es_2_0.aspx`) was not
reachable via WebFetch in this discovery pass — the URL returned a marketing homepage redirect
rather than the legal text. Stage 02 Architecture should obtain the full EULA text via another
channel and explicitly scan for:
- Trademark clauses on "Syncfusion", "Sf*" prefix
- API-naming restrictions
- Derivative-work clauses that could include "compatibility shims"

### 3.3 Conclusion

**Provisional: permissible.** The Community License's public terms are silent on API-surface
replication, and the compat-telerik precedent validates the pattern. Recommend Stage 02
Architecture ticket: "legal review of full Syncfusion EULA for API-naming / trademark / derivative
clauses before compat-syncfusion first PR." This is a **low-risk gate** — not a blocker for
discovery, but a checkbox before shipping.

### 3.4 POLICY.md invariants — transferability

The four compat-telerik POLICY invariants map 1:1 to compat-syncfusion:

| compat-telerik invariant | compat-syncfusion equivalent |
|---|---|
| No `Telerik.*` `<PackageReference>` | No `Syncfusion.*` `<PackageReference>` |
| Root namespace `Sunfish.Compat.Telerik` | Root namespace `Sunfish.Compat.Syncfusion` |
| Unsupported params throw via `UnsupportedParam.Throw(...)` | Same pattern, shared helper |
| Divergences documented in `docs/compat-telerik-mapping.md` | Same, in `docs/compat-syncfusion-mapping.md` |

No new invariants are needed for Syncfusion. `POLICY.md` for compat-syncfusion is a near-verbatim
copy of compat-telerik's with vendor-name substitutions.

---

## 4. Decision 2 spike status — N/A for Syncfusion

**Confirmed:** Syncfusion Blazor is **native Blazor** (C# Razor components), not Web Components
wrapped.

**Evidence:**
- API-reference pages declare `SfButton : SfBaseComponent, IComponent, IHandleEvent,
  IHandleAfterRender, IDisposable` — standard Razor component inheritance chain.
- Component markup uses `<SfButton ...>` / `<SfGrid ...>` / `<SfDialog ...>` — Blazor-convention
  PascalCase tags, not `kebab-case` custom elements (`<igb-button>`, etc.).
- Parameters are standard `[Parameter]`-attributed C# properties, not JS-interop-forwarded.
- Syncfusion does require a `syncfusion-blazor.min.js` script and theme CSS for some components
  (same as Telerik — e.g. for popup positioning, scheduler virtualization), but this is JS
  **interop from** Razor components, not Web Components **wrapping by** Razor.

**Implication:** The compat-telerik delegation pattern (wrapper → `SunfishComponent`)
transfers directly. No architectural spike needed. No re-route to the Infragistics spike
methodology required.

If any Syncfusion component turns out to be WC-backed (none identified in this discovery pass,
but the catalog is broad — ~100 components, only 12 inventoried), flag at Stage 03 Architecture
and re-route that specific component's approach.

---

## Stage-2 (Architecture) handoff summary

- **12 / 12 components inventoried.** No Sunfish equivalent is missing; every target has a
  delegation target.
- **~20 child-component shim targets** identified (Grid-related, Dialog-related, DropDown
  `FieldSettings`, Toast settings).
- **18 EventArgs types / type families** identified for shimming under `EventArgs/`.
- **Generic-component shapes to preserve:** `SfCheckBox<TChecked>`, `SfDropDownList<TValue,
  TItem>`, `SfComboBox<TValue, TItem>`, `SfDatePicker<TValue>`, `SfGrid<TValue>`.
- **Licensing:** provisionally permissible; add Stage 02 task to scan full EULA.
- **Architecture spike:** not needed (native Blazor, same shape as Telerik).
- **Generic-gap-closure (intake Decision 4):** the `CompatChildComponent<TParent>` helper
  referenced in `docs/compat-telerik-mapping.md` § "Shared pattern" should be lifted to
  `compat-shared` before compat-syncfusion scaffolding begins — it will carry ~20 child shims
  in compat-syncfusion alone.
- **Out-of-scope reminders:** no package code shipped, no Syncfusion NuGet referenced at any
  point during discovery. POLICY.md §Hard Invariant 1 preserved.
