# compat-devexpress Surface Inventory — Stage 01 Discovery

**Date:** 2026-04-22
**Task:** #103 — Stage 01 Discovery (DevExpress lane)
**Parent intake:** [`icm/00_intake/output/compat-expansion-intake.md`](../../00_intake/output/compat-expansion-intake.md)
**Pattern reference:** [`packages/compat-telerik/POLICY.md`](../../../packages/compat-telerik/POLICY.md), [`docs/compat-telerik-mapping.md`](../../../docs/compat-telerik-mapping.md)

---

## 0. Research method & sources

- Primary: `https://docs.devexpress.com/Blazor/*` component API reference pages (WebFetch, ~17 calls).
- Licensing: `https://www.devexpress.com/Support/EULAs/universal.xml` Universal EULA, Section 7 (verbatim-quoted below).
- Context7 MCP quota exhausted on first call — fell back to WebFetch for all component research.
- DevExpress component catalog: `https://docs.devexpress.com/Blazor/400725/blazor-components`.
- All inventory drawn from doc pages; depth items marked **TBD — Stage 03** will resolve during implementation when the exact Sunfish-side delegation target is opened.

---

## 1. Component surface inventory

DevExpress uses a `Dx*` prefix. Most editors are generic over their bound value type; `DxGrid` itself is non-generic (uses `IEnumerable` + `KeyFieldName`). DevExpress lacks a dedicated `DxIcon` component — icons are specified on other components via the `IconCssClass` string pattern (CSS-class-based; Bootstrap-Icons/Font-Awesome style). DevExpress's tooltip story is unusual — see §1.11.

### 1.1 Button

- **DevExpress type:** `DxButton` (non-generic; inherits from `DxButtonBase`).
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.Buttons.SunfishButton` — direct map (same shape as `TelerikButton`).
- **Major parameters:**
  | DX parameter | Type | Notes |
  |---|---|---|
  | `Text` | `string` | Button label text (alternative to ChildContent). |
  | `RenderStyle` | `ButtonRenderStyle` enum | `Primary`/`Secondary`/`Success`/`Info`/`Warning`/`Danger`/`Light`/`Dark`/`Link` — maps to Sunfish `ButtonVariant`. |
  | `RenderStyleMode` | `ButtonRenderStyleMode` enum | `Contained`/`Outline`/`Text` — maps to Sunfish `FillMode` (`Solid`/`Outline`/`Link`). |
  | `IconCssClass` | `string` | CSS class for button icon. Translation to `SunfishButton.Icon` is icon-provider-dependent — LogAndFallback to a `<i class="…">` fragment via `CompatIconAdapter`. |
  | `IconPosition` | `ButtonIconPosition` enum | `Left`/`Right`/`Top`/`Bottom` — partial Sunfish coverage (likely Left/Right supported). |
  | `Enabled` | `bool` | Passthrough. |
  | `Visible` | `bool` | Passthrough; `false` → not rendered. |
  | `SubmitFormOnClick` | `bool` | Maps to Sunfish `ButtonType.Submit`. |
  | `NavigateUrl` | `string` | No direct Sunfish analog — wrapper renders `<a>` or forwards as `onclick` navigation helper. **TBD — Stage 03.** |
  | `SizeMode` | `SizeMode` enum | `Small`/`Medium`/`Large` — 1:1 to Sunfish `ButtonSize`. |
  | `Title` | `string` | HTML `title` attribute (tooltip on hover). Forwarded via `AdditionalAttributes`. |
  | `CssClass` | `string` | Forwarded. |
  | `Click` | `EventCallback<MouseEventArgs>` | Passthrough to `SunfishButton.OnClick`. |
  | `ChildContent` | `RenderFragment` | Passthrough. |
- **Child components:** none (leaf).
- **EventArgs:** `MouseEventArgs` (standard Blazor).

### 1.2 Icon

- **DevExpress type:** **no dedicated `DxIcon` component exists.** DevExpress uses the `IconCssClass` string pattern on consuming components (`DxButton`, `DxToolbar`, `DxTreeView`, grid columns, etc.).
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.Utility.SunfishIcon`.
- **Recommendation:** ship `DxIcon` as a **synthetic compat component** (has no DevExpress type-name peer, but matches the `Telerik*` / `Sf*` / `Igb*` pattern and gives migrators an obvious on-ramp for any custom icon markup). Document in mapping doc as "compat-synthetic: no DevExpress equivalent; pairs with `IconCssClass` translation done by `CompatIconAdapter`." Alternatively, drop `DxIcon` from the 12-count and slot in `DxSvgImage` or a `DxCheckBox`-paired synthetic — **decision TBD — Stage 02 Architecture.**
- **Major parameters (proposed synthetic):** `CssClass`, `Name`, `ChildContent`.
- **Cross-cutting:** `IconCssClass` normalization logic must live in `CompatIconAdapter` and be reused by `DxButton`, `DxGridCommandColumn`, `DxToolbar*`, etc.

### 1.3 CheckBox

- **DevExpress type:** `DxCheckBox<T>` (generic over checked-value type; supports `bool`, `bool?`, `string`, numeric, enum, custom).
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishCheckbox` (note Sunfish `Checkbox` one-word, DevExpress `CheckBox` two-word — same divergence already handled in `TelerikCheckBox`).
- **Major parameters:**
  | DX parameter | Type | Notes |
  |---|---|---|
  | `Checked` | `T` | Current state. For `T=bool/bool?` → passthrough to `SunfishCheckbox.Value`. For non-bool `T` (tri-state via `ValueChecked`/`ValueUnchecked`/`ValueIndeterminate`) → **TBD — Stage 03**, possibly translate to `bool?` with Log+Fallback. |
  | `CheckedChanged` | `EventCallback<T>` | Passthrough when T is bool-like. |
  | `CheckedExpression` | `Expression<Func<T>>` | Passthrough via `@bind-Checked`. |
  | `ValueChecked` / `ValueUnchecked` / `ValueIndeterminate` | `T` | Not in Sunfish — LogAndFallback when `T != bool/bool?`. |
  | `AllowIndeterminateStateByClick` | `bool` | Passthrough if Sunfish exposes; else LogAndFallback. |
  | `CheckType` | `CheckType` enum | DX-specific appearance — Dropped (cosmetic). |
  | `Enabled` | `bool` | Passthrough. |
  | `ReadOnly` | `bool` | Passthrough. |
  | `Alignment` | `CheckBoxContentAlignment` enum | Dropped (cosmetic). |
  | `LabelPosition` | `LabelPosition` enum | **TBD — Stage 03** (may map to Sunfish label placement). |
  | `Density` | `SizeMode` enum | Maps to Sunfish sizing. |
  | `CssClass` | `string` | Forwarded. |
- **Child components:** none.
- **EventArgs:** `ValueChangedEventArgs<T>` (DX-specific) — consider a compat shim under `EventArgs/`.

### 1.4 TextBox

- **DevExpress type:** `DxTextBox` (non-generic; `string` only).
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishTextBox`.
- **Major parameters:**
  | DX parameter | Type | Notes |
  |---|---|---|
  | `Text` | `string` | Passthrough via `@bind-Text` → `SunfishTextBox.Value`. |
  | `TextChanged` | `EventCallback<string>` | Passthrough. |
  | `TextExpression` | `Expression<Func<string>>` | Passthrough. |
  | `NullText` | `string` | Maps to `SunfishTextBox.Placeholder`. |
  | `Password` | `bool` | Forwarded — flips input type. |
  | `MaxLength` | `int` | Forwarded via `AdditionalAttributes["maxlength"]`. |
  | `Enabled` | `bool` | Passthrough. |
  | `ReadOnly` | `bool` | Passthrough. |
  | `BindValueMode` | `BindValueMode` enum | `OnLostFocus`/`OnInput`/`OnDelayedInput` — Sunfish likely only supports `OnInput`. LogAndFallback. |
  | `InputDelay` | `int` | Same as Telerik `DebounceDelay` — Dropped (cosmetic) until Sunfish exposes. |
  | `SizeMode` | `SizeMode` enum | Maps to Sunfish sizing. |
  | `ClearButtonDisplayMode` | `DataEditorClearButtonDisplayMode` enum | `Never`/`Always`/`Auto` — **TBD — Stage 03** (may throw if Sunfish lacks the concept). |
  | `CssClass` | `string` | Forwarded. |
- **Child components:** none.
- **EventArgs:** `EventCallback<string>` (generic).

### 1.5 DropDownList

- **DevExpress type:** DevExpress has no component literally named `DxDropDownList`. The equivalent (non-editable, fixed-selection dropdown) is **`DxComboBox<TData, TValue>` with `AllowUserInput="false"`**.
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDropDownList`.
- **Recommended shim approach:** ship `DxDropDownList<TData, TValue>` as a **compat-synthetic** (matches the 12-count target, mirrors the `SfDropDownList` / `TelerikDropDownList` pattern) that delegates to `SunfishDropDownList`. Internally identical to how a consumer would write `DxComboBox` with `AllowUserInput="false"`. Document in mapping doc. Alternative: drop `DxDropDownList` from the count and slot `DxListBox` — **TBD — Stage 02 Architecture.**
- **Major parameters (synthetic, mirroring `DxComboBox` parameter names):** `Data`, `Value`, `ValueChanged`, `ValueFieldName`, `TextFieldName`, `NullText`, `NullValue`, `Enabled`, `ReadOnly`, `SizeMode`, `CssClass`.

### 1.6 ComboBox

- **DevExpress type:** `DxComboBox<TData, TValue>` (generic over data item and value).
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishComboBox`.
- **Major parameters:**
  | DX parameter | Type | Notes |
  |---|---|---|
  | `Data` | `IEnumerable<TData>` | Passthrough. |
  | `Value` | `TValue` | Passthrough via `@bind-Value`. |
  | `ValueChanged` | `EventCallback<TValue>` | Passthrough. |
  | `TextFieldName` | `string` | Reflection-based accessor (same pattern as `TelerikDropDownList`). |
  | `ValueFieldName` | `string` | Reflection-based accessor. |
  | `NullText` | `string` | Maps to placeholder. |
  | `NullValue` | `TValue` | Passthrough. |
  | `FilteringMode` | `ListSearchMode` enum | Maps to Sunfish filter mode; LogAndFallback. |
  | `AllowUserInput` | `bool` | Passthrough. |
  | `Virtualize` | `bool` | Passthrough if Sunfish exposes; else LogAndFallback. |
  | `ItemTemplate` | `RenderFragment<TData>` | Passthrough. |
  | `Enabled` / `ReadOnly` / `SizeMode` / `ClearButtonDisplayMode` / `CssClass` | various | Same treatment as `DxTextBox`. |
- **Child components:** `DxListEditorColumn` (multi-column layout) — compat-synthetic child via `CompatChildComponent<TParent>`.
- **EventArgs:** `SelectedDataItemChangedEventArgs<TData>` — shim under `EventArgs/`.

### 1.7 DatePicker

- **DevExpress type:** `DxDateEdit<T>` (generic over `DateTime` / `DateTimeOffset` / `DateOnly` / nullable variants).
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDatePicker`.
- **Major parameters:**
  | DX parameter | Type | Notes |
  |---|---|---|
  | `Date` | `T` | Passthrough via `@bind-Date`. |
  | `DateChanged` | `EventCallback<T>` | Passthrough. |
  | `DateExpression` | `Expression<Func<T>>` | Passthrough. |
  | `Format` | `string` | Passthrough. |
  | `DisplayFormat` | `string` | Alternative display format; passthrough. |
  | `NullText` | `string` | Maps to placeholder. |
  | `MinDate` / `MaxDate` | `DateTime` | Passthrough. |
  | `PickerDisplayMode` | `DatePickerDisplayMode` enum | `Auto`/`Calendar`/`Default` — Dropped (cosmetic). |
  | `FirstDayOfWeek` | `DayOfWeek` | Passthrough. |
  | `Enabled` / `ReadOnly` / `SizeMode` / `ClearButtonDisplayMode` / `CssClass` | various | Standard treatment. |
  | `TimeSectionSettings` | object | Sunfish lacks time-picker composition — LogAndFallback. |
  | `Mask` | `DateTimeMask` | Dropped (cosmetic). |
  | `CalendarViewMode` | `CalendarViewMode` enum | Dropped (cosmetic). |
- **Related:** `DxDateRangePicker<T>` out-of-scope (not in 12-count).
- **EventArgs:** `EventCallback<T>` (generic).

### 1.8 Form

- **DevExpress type:** `DxFormLayout` (non-generic).
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.Forms.SunfishForm` — **translation required, not direct**. DevExpress `DxFormLayout` is closer to a grid/stack layout than to Telerik's `TelerikForm` (model-bound + EditContext). Sunfish's `SunfishForm` is EditContext-based. **Mapping is lossy** — `DxFormLayout.Data`/`CaptionPosition`/column-span parameters have no Sunfish-side equivalent.
- **Recommendation:** narrow shim — pass through `Data`, `Enabled`, `ReadOnly`, `Visible`, `CssClass`, `ChildContent`. Drop column-span / caption parameters as cosmetic. Document as high-divergence wrapper; migrators should expect to restructure form markup. **TBD — Stage 02 Architecture.**
- **Major parameters:** `Data` (`object`), `Enabled`, `ReadOnly`, `CssClass`, `Visible`, `ChildContent`, `CaptionPosition` (enum, Dropped), `BeginRow` (bool, Dropped), `ColSpanXs`–`ColSpanXxl` (int, Dropped on parent; may need surfacing on child).
- **Child components:** `DxFormLayoutItem`, `DxFormLayoutGroup`, `DxFormLayoutTabPages`, `DxFormLayoutTabPage` — all compat-synthetic children under `CompatChildComponent<TParent>`. `DxFormLayoutItem` is the non-trivial one (caption/content slot).
- **EventArgs:** none on `DxFormLayout` itself.

### 1.9 Grid

- **DevExpress type:** `DxGrid` (**non-generic** — unlike Telerik `TelerikGrid<TItem>`, DevExpress uses `IEnumerable` + `KeyFieldName` string).
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishDataGrid<TItem>` — **generic-erasure translation required**. Either:
  - **(a) erase** — wrapper takes `IEnumerable` and binds to `SunfishDataGrid<object>`, or
  - **(b) reflect** — wrapper discovers `TItem` from `Data` at render time and delegates to `SunfishDataGrid<TItem>` via `DynamicComponent`.
  **TBD — Stage 02 Architecture** (likely (b) for type-safe event args; (a) is simpler).
- **Major parameters:**
  | DX parameter | Type | Notes |
  |---|---|---|
  | `Data` | `IEnumerable` | Passthrough (with generic inference per above). |
  | `KeyFieldName` | `string` | Reflection-based accessor — translates to Sunfish `Key` selector. |
  | `ShowFilterRow` | `bool` | Maps to Sunfish `Filterable` (Telerik parity). |
  | `FilterMenuButtonDisplayMode` | enum | **TBD — Stage 03**. |
  | `PageSize` | `int` | Passthrough. |
  | `PagerVisible` | `bool` | Passthrough via `Pageable`. |
  | `AllowSort` | `bool` | Maps to Sunfish `Sortable`. |
  | `SortMode` | `GridColumnSortMode` enum | Maps to Sunfish sort mode. |
  | `SelectionMode` | `GridSelectionMode` enum | Maps. `None`/`Single`/`Multiple` — 1:1 expected. |
  | `SelectedDataItem` / `SelectedDataItems` | `object` / `IList` | Passthrough with generic erasure. |
  | `EditMode` | `GridEditMode` enum | `None`/`PopupEditForm`/`EditRow`/`EditCell`/`EditForm`/`Batch` — Sunfish coverage partial. **TBD — Stage 03** (most modes likely LogAndFallback or Throw). |
  | `EditModelSaving` | `EventCallback<GridEditModelSavingEventArgs>` | **Throws** in Phase 1 (wiring complexity matches Telerik `OnRead`). |
  | `CustomizeElement` | `EventCallback<GridCustomizeElementEventArgs>` | Dropped (cosmetic); logged. |
  | `ShowGroupPanel` | `bool` | Sunfish grouping: **TBD — Stage 03**. |
  | `ChildContent` | `RenderFragment` | Passthrough (columns cascade). |
- **Child components (critical surface):**
  - `DxGridDataColumn` — primary bound column (see parameter table below).
  - `DxGridSelectionColumn` — selection checkbox column.
  - `DxGridCommandColumn` — command buttons (edit/delete).
  - `DxGridSpinEditColumn` — numeric editor column.
  - `DxGridDateEditColumn` — date editor column.
  - `DxGridComboBoxColumn<T>` — combobox editor column (**generic**).
  - All compat-synthetic children via `CompatChildComponent<SunfishDataGrid>`.
- **`DxGridDataColumn` key parameters:** `FieldName`, `Caption`, `Width`, `MinWidth`, `Visible`, `VisibleIndex`, `SortIndex`, `SortOrder` (`GridColumnSortOrder`), `AllowSort`, `AllowGroup`, `AllowFilter`, `TextAlignment` (`GridTextAlignment`), `HeaderAlignment`, `DisplayFormat`, `CellDisplayTemplate` (`RenderFragment`), `EditSettings`, `HeaderTemplate`, `FooterTemplate`, `GroupFooterTemplate`, `FilterRowEditorVisible`.
- **EventArgs shims required (under `EventArgs/`):**
  - `SelectedDataItemsChangedEventArgs`
  - `CustomizeCellDisplayTextEventArgs`
  - `CustomizeElementEventArgs` (aka `GridCustomizeElementEventArgs`)
  - `GridEditModelSavingEventArgs`
  - `GridDataItemDeletingEventArgs`
  - `GridRowClickEventArgs` (parity with Telerik shim)

### 1.10 Window

- **DevExpress type:** `DxWindow` (non-generic; non-modal — allows background interaction, supports drag/resize).
- **Companion type:** `DxPopup` (modal — traps focus). DevExpress ships **both**; for the 12-count we map `Window` → `DxWindow`. `DxPopup` is out-of-scope for this pass.
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.Overlays.SunfishWindow`.
- **Major parameters:**
  | DX parameter | Type | Notes |
  |---|---|---|
  | `Visible` | `bool` | Passthrough via `@bind-Visible`. |
  | `VisibleChanged` | `EventCallback<bool>` | Passthrough. |
  | `HeaderText` | `string` | Maps to `Title`. |
  | `ShowHeader` | `bool` | Passthrough if Sunfish exposes; else LogAndFallback. |
  | `ShowFooter` | `bool` | Passthrough. |
  | `FooterText` | `string` | Maps to footer content or `FooterTemplate`. |
  | `AllowResize` | `bool` | **TBD — Stage 03** (Sunfish may not model resize; LogAndFallback expected). |
  | `AllowDrag` | `bool` | **TBD — Stage 03**. |
  | `CloseOnEscape` | `bool` | Passthrough. |
  | `CloseOnOutsideClick` | `bool` | Passthrough (likely modal-only in Sunfish). |
  | `ShowCloseButton` | `bool` | Passthrough. |
  | `Width` / `Height` | `string` | Forwarded via `style`. |
  | `HeaderTemplate` / `BodyTemplate` / `FooterTemplate` | `RenderFragment` | Passthrough. |
- **Child components:** none.
- **EventArgs:** `WindowShowingEventArgs`, `WindowShownEventArgs`, `WindowClosingEventArgs`, `WindowClosedEventArgs`, `WindowDragStartedEventArgs`, `WindowDragCompletedEventArgs`, `WindowResizeStartedEventArgs`, `WindowResizeCompletedEventArgs` — all require shims under `EventArgs/` if the events are surfaced. For Phase 1, likely ship only `WindowClosingEventArgs` / `WindowClosedEventArgs` and throw on the rest.

### 1.11 Tooltip

- **DevExpress type:** **no `DxTooltip` / `DxHint` / `DxPopover` exists as a general-purpose tooltip component in DevExpress Blazor.** DevExpress ships:
  - `DxTooltipSettings` — configuration class for *visualization* components only (`DxBarGauge`, `DxSankey`, `DxSparkline`). Not a Razor component; not rendered via markup.
  - `DxFlyout` — the closest general-purpose popover (`HeaderText`/`BodyText`/`IsOpen`/`Position`/`PositionTarget`).
  - HTML `title` attribute on interactive components (`DxButton.Title`).
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishTooltip`.
- **Recommendation:** **compat-synthetic `DxTooltip`** that delegates to `SunfishTooltip`. Document as "DevExpress-Blazor has no direct tooltip component; this compat shim provides a migration target for consumers who built tooltip abstractions of their own." Parameters borrowed from `DxFlyout`:
  | Proposed parameter | Type | Notes |
  |---|---|---|
  | `PositionTarget` | `string` | CSS selector → `SunfishTooltip.TargetSelector`. |
  | `Position` | `FlyoutPosition` enum | Maps to Sunfish `Placement`. |
  | `HeaderText` | `string` | Optional header. |
  | `BodyText` | `string` | Primary content. |
  | `HeaderTemplate` / `BodyTemplate` | `RenderFragment` | Passthrough. |
  | `Visible` / `VisibleChanged` | `bool` / `EventCallback<bool>` | Passthrough. |
  | `CloseOnEscape` / `CloseOnOutsideClick` | `bool` | Passthrough. |
  | `ShowCloseButton` | `bool` | Passthrough. |
- **Alternative:** drop `Tooltip` from the 12-count for DevExpress and slot `DxFlyout` (acknowledges the actual DX surface). **Decision TBD — Stage 02 Architecture.**
- **EventArgs:** none routinely required.

### 1.12 Notification

- **DevExpress type:** `DxToast` (single toast instance) + `DxToastProvider` (host/container — required in the layout). Two-component pattern.
- **Sunfish delegate target:** `Sunfish.UIAdapters.Blazor.Components.Feedback.SunfishSnackbarHost` (host) + `ISnackbarService` (imperative show/hide).
- **Major parameters on `DxToast`:**
  | DX parameter | Type | Notes |
  |---|---|---|
  | `Text` | `string` | Primary content → snackbar message. |
  | `Title` | `string` | Secondary label. |
  | `Visible` | `bool` | Passthrough. |
  | `RenderStyle` | `ToastRenderStyle` enum | `Success`/`Information`/`Warning`/`Danger`/`Primary`/`Secondary`/`Light`/`Dark` — maps to Sunfish severity. |
  | `ThemeMode` | `ToastThemeMode` enum | `Light`/`Dark`/`Pastel` — Dropped (cosmetic). |
  | `AnimationType` | `ToastAnimationType` enum | Dropped (cosmetic). |
  | `DisplayTime` | `int` | ms before auto-close; maps to Sunfish duration. |
  | `AutoHide` | `bool` | Passthrough. |
  | `ShowCloseButton` | `bool` | Passthrough. |
  | `ActionText` / `Action` | `string` / `EventCallback` | Action-button pair — Sunfish parity **TBD — Stage 03**. |
  | `HorizontalAlignment` / `VerticalAlignment` | `HorizontalEdge` / `VerticalEdge` | Forwarded via host parameters. |
  | `IconCssClass` / `ShowIcon` | `string` / `bool` | Via `CompatIconAdapter`. |
  | `Template` | `RenderFragment` | Passthrough. |
- **Major parameters on `DxToastProvider`:** `VerticalAlignment`, `RenderStyle` (default), `ThemeMode` (default), plus defaults for all child toasts.
- **Child components:** `DxToastProvider` hosts zero-or-more `DxToast` children (imperative-show or declarative). Compat shim pattern: `DxToastProvider` wraps `SunfishSnackbarHost`; `DxToast` is declarative-only sugar that forwards to `ISnackbarService.Show()` when made visible.
- **EventArgs:** `Action` is `EventCallback` — no custom EventArgs required.

---

## 2. Mapping-doc skeleton

Skeleton for `docs/compat-devexpress-mapping.md`. Follows `docs/compat-telerik-mapping.md` structure. Status column values: **Supported** / **LogAndFallback** / **Throws** / **Not-in-scope** / **TBD — Stage 03**.

---

### DxButton

- **DevExpress target:** `DevExpress.Blazor.DxButton`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Buttons.SunfishButton`

| DX parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Text` | `string` | `ChildContent` (text node) | TBD — Stage 03 |
| `RenderStyle` | `ButtonRenderStyle` | `Variant` (`ButtonVariant`) | TBD — Stage 03 |
| `RenderStyleMode` | `ButtonRenderStyleMode` | `FillMode` | TBD — Stage 03 |
| `IconCssClass` | `string` | `Icon` via `CompatIconAdapter` | TBD — Stage 03 |
| `IconPosition` | `ButtonIconPosition` | — | TBD — Stage 03 |
| `Enabled` | `bool` | `Enabled` | TBD — Stage 03 |
| `Visible` | `bool` | conditional render | TBD — Stage 03 |
| `SubmitFormOnClick` | `bool` | `ButtonType.Submit` | TBD — Stage 03 |
| `NavigateUrl` | `string` | synthetic `<a>` wrap | TBD — Stage 03 |
| `SizeMode` | `SizeMode` | `Size` | TBD — Stage 03 |
| `Click` | `EventCallback<MouseEventArgs>` | `OnClick` | TBD — Stage 03 |
| `ChildContent` | `RenderFragment` | `ChildContent` | TBD — Stage 03 |

Known divergences (bullets):
- DevExpress `Text` is a string parameter; Sunfish uses `ChildContent`. Consumers setting both are ambiguous — specify precedence in shim.
- `NavigateUrl` has no Sunfish equivalent — shim must render `<a>` or use a `NavigationManager` hop.
- `RenderStyleMode=Text` → Sunfish `FillMode.Link`, not a new variant.

---

### DxIcon (compat-synthetic)

- **DevExpress target:** *none — compat-synthetic to match 12-count surface.*
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Utility.SunfishIcon`

| DX parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `CssClass` | `string` | `SunfishIcon.Name` via `CompatIconAdapter` | TBD — Stage 03 |
| `Name` | `string` | `SunfishIcon.Name` | TBD — Stage 03 |
| `ChildContent` | `RenderFragment` | `ChildContent` | TBD — Stage 03 |

Known divergences:
- DevExpress has no `DxIcon` type. This shim is a **compat-synthetic** — consumers who migrated via `DxButton.IconCssClass` never referenced a `DxIcon` type in their code. The shim exists as a migration target for consumers who abstracted icons into a custom wrapper.
- Stage 02 Architecture may choose to drop `DxIcon` from the 12-count for DevExpress. Open decision.

---

### DxCheckBox\<T\>

- **DevExpress target:** `DevExpress.Blazor.DxCheckBox<T>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishCheckbox`

| DX parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Checked` | `T` | `Value` (when `T=bool/bool?`) | TBD — Stage 03 |
| `CheckedChanged` | `EventCallback<T>` | `ValueChanged` | TBD — Stage 03 |
| `CheckedExpression` | `Expression<Func<T>>` | `@bind-Value` | TBD — Stage 03 |
| `ValueChecked` / `ValueUnchecked` / `ValueIndeterminate` | `T` | — | TBD — Stage 03 (LogAndFallback expected) |
| `AllowIndeterminateStateByClick` | `bool` | `SunfishCheckbox` tri-state | TBD — Stage 03 |
| `CheckType` | `CheckType` | — | TBD — Stage 03 (Dropped cosmetic) |
| `Enabled` | `bool` | `Enabled` | TBD — Stage 03 |
| `ReadOnly` | `bool` | `ReadOnly` | TBD — Stage 03 |
| `Density` | `SizeMode` | `Size` | TBD — Stage 03 |

Divergences:
- Typical DevExpress codebases use `DxCheckBox<bool>` or `DxCheckBox<bool?>`. For any other `T`, the wrapper falls back and logs — consumers must migrate to `SunfishCheckbox` directly.

---

### DxTextBox

- **DevExpress target:** `DevExpress.Blazor.DxTextBox`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishTextBox`

| DX parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Text` | `string` | `Value` | TBD — Stage 03 |
| `TextChanged` | `EventCallback<string>` | `ValueChanged` | TBD — Stage 03 |
| `TextExpression` | `Expression<Func<string>>` | `@bind-Value` | TBD — Stage 03 |
| `NullText` | `string` | `Placeholder` | TBD — Stage 03 |
| `Password` | `bool` | type=password | TBD — Stage 03 |
| `MaxLength` | `int` | `AdditionalAttributes` | TBD — Stage 03 |
| `BindValueMode` | `BindValueMode` | — | TBD — Stage 03 (LogAndFallback) |
| `InputDelay` | `int` | — | TBD — Stage 03 (Dropped cosmetic) |
| `SizeMode` | `SizeMode` | `Size` | TBD — Stage 03 |
| `ClearButtonDisplayMode` | `DataEditorClearButtonDisplayMode` | — | TBD — Stage 03 |

Divergences:
- Same `InputDelay`-dropped story as Telerik `DebounceDelay`.

---

### DxDropDownList\<TData, TValue\> (compat-synthetic)

- **DevExpress target:** *none — synthetic shim that mirrors `DxComboBox<TData, TValue>` with `AllowUserInput="false"` semantics.*
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDropDownList`

| DX parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Data` | `IEnumerable<TData>` | `Data` | TBD — Stage 03 |
| `Value` | `TValue` | `Value` | TBD — Stage 03 |
| `ValueChanged` | `EventCallback<TValue>` | `ValueChanged` | TBD — Stage 03 |
| `TextFieldName` | `string` | reflected accessor | TBD — Stage 03 |
| `ValueFieldName` | `string` | reflected accessor | TBD — Stage 03 |
| `NullText` | `string` | placeholder | TBD — Stage 03 |
| `NullValue` | `TValue` | — | TBD — Stage 03 |

Divergences:
- DevExpress does not ship a `DxDropDownList` type; Stage 02 may choose to drop this from the 12-count or accept the synthetic as the migration-shape target.

---

### DxComboBox\<TData, TValue\>

- **DevExpress target:** `DevExpress.Blazor.DxComboBox<TData, TValue>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishComboBox`

| DX parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Data` | `IEnumerable<TData>` | `Data` | TBD — Stage 03 |
| `Value` | `TValue` | `Value` | TBD — Stage 03 |
| `ValueChanged` | `EventCallback<TValue>` | `ValueChanged` | TBD — Stage 03 |
| `TextFieldName` / `ValueFieldName` | `string` | reflection | TBD — Stage 03 |
| `FilteringMode` | `ListSearchMode` | Sunfish filter mode | TBD — Stage 03 |
| `AllowUserInput` | `bool` | passthrough | TBD — Stage 03 |
| `Virtualize` | `bool` | passthrough | TBD — Stage 03 |
| `ItemTemplate` | `RenderFragment<TData>` | `ItemTemplate` | TBD — Stage 03 |
| `NullText` / `NullValue` / `Enabled` / `ReadOnly` / `SizeMode` | various | standard | TBD — Stage 03 |

Divergences:
- `DxListEditorColumn` child support (multi-column dropdown) not in Phase 1.

---

### DxDateEdit\<T\>

- **DevExpress target:** `DevExpress.Blazor.DxDateEdit<T>`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDatePicker`

| DX parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Date` | `T` | `Value` | TBD — Stage 03 |
| `DateChanged` | `EventCallback<T>` | `ValueChanged` | TBD — Stage 03 |
| `DateExpression` | `Expression<Func<T>>` | `@bind-Value` | TBD — Stage 03 |
| `Format` / `DisplayFormat` | `string` | `Format` | TBD — Stage 03 |
| `NullText` | `string` | placeholder | TBD — Stage 03 |
| `MinDate` / `MaxDate` | `DateTime` | `Min` / `Max` | TBD — Stage 03 |
| `FirstDayOfWeek` | `DayOfWeek` | passthrough | TBD — Stage 03 |
| `PickerDisplayMode` / `Mask` / `CalendarViewMode` | enums | — | TBD — Stage 03 (Dropped cosmetic) |
| `TimeSectionSettings` | object | — | TBD — Stage 03 (LogAndFallback) |

Divergences:
- Naming: DX uses `Date`/`DateChanged`/`DateExpression`; Sunfish uses `Value`/`ValueChanged`. Shim surface keeps DX names.
- `DxDateEdit<T>` spans `DateTime`, `DateTimeOffset`, `DateOnly` + nullables — Sunfish likely only covers `DateTime?`. LogAndFallback for other T.

---

### DxFormLayout

- **DevExpress target:** `DevExpress.Blazor.DxFormLayout`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.SunfishForm`

| DX parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Data` | `object` | `Model` | TBD — Stage 03 |
| `Enabled` / `ReadOnly` / `Visible` / `CssClass` | various | passthrough | TBD — Stage 03 |
| `CaptionPosition` | enum | — | TBD — Stage 03 (Dropped cosmetic) |
| `BeginRow` | `bool` | — | TBD — Stage 03 (Dropped — child concern) |
| `ColSpanXs`..`ColSpanXxl` | `int` | — | TBD — Stage 03 (Dropped — column model mismatch) |
| `ChildContent` | `RenderFragment` | `ChildContent` | TBD — Stage 03 |

Child-component shims (compat-synthetic):
- `DxFormLayoutItem` — the real lifting; Sunfish parity **TBD — Stage 03**.
- `DxFormLayoutGroup` / `DxFormLayoutTabPages` / `DxFormLayoutTabPage`.

Divergences:
- `DxFormLayout` is primarily a **layout** component with caption/column-span concerns; `SunfishForm` is a **validation** component bound to `EditContext`. This is the most lossy wrapper in the set. Migrators should expect layout restructuring on top of using-swap.

---

### DxGrid

- **DevExpress target:** `DevExpress.Blazor.DxGrid` (non-generic)
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishDataGrid<TItem>` (generic)

| DX parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Data` | `IEnumerable` | `Data` (with TItem inference) | TBD — Stage 03 |
| `KeyFieldName` | `string` | key accessor | TBD — Stage 03 |
| `ShowFilterRow` | `bool` | `Filterable` | TBD — Stage 03 |
| `PageSize` / `PagerVisible` | `int` / `bool` | `PageSize` / `Pageable` | TBD — Stage 03 |
| `AllowSort` / `SortMode` | `bool` / `GridColumnSortMode` | `Sortable` | TBD — Stage 03 |
| `SelectionMode` / `SelectedDataItem` / `SelectedDataItems` | enum / object / IList | `SelectionMode` / `SelectedItems` | TBD — Stage 03 |
| `EditMode` | `GridEditMode` | — | TBD — Stage 03 (Throws or LogAndFallback on most modes) |
| `EditModelSaving` | `EventCallback<GridEditModelSavingEventArgs>` | — | TBD — Stage 03 (Throws in Phase 1) |
| `ShowGroupPanel` | `bool` | — | TBD — Stage 03 |
| `CustomizeElement` | `EventCallback<GridCustomizeElementEventArgs>` | — | TBD — Stage 03 (Dropped) |
| `ChildContent` | `RenderFragment` | `ChildContent` | TBD — Stage 03 |

Child-component shims:
- `DxGridDataColumn` (primary).
- `DxGridSelectionColumn`, `DxGridCommandColumn`, `DxGridSpinEditColumn`, `DxGridDateEditColumn`, `DxGridComboBoxColumn<T>` — follow-up PRs under policy gate.

Divergences:
- DevExpress `DxGrid` is non-generic; Sunfish `SunfishDataGrid<T>` is generic. The shim erases or reflects — see §1.9.
- Generic-erasure applies to selection properties (`SelectedDataItem` as `object`).
- Most edit modes throw or LogAndFallback in Phase 1 (large complexity surface).

---

### DxWindow

- **DevExpress target:** `DevExpress.Blazor.DxWindow`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Overlays.SunfishWindow`

| DX parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Visible` / `VisibleChanged` | `bool` / `EventCallback<bool>` | `@bind-Visible` | TBD — Stage 03 |
| `HeaderText` | `string` | `Title` | TBD — Stage 03 |
| `ShowHeader` / `ShowFooter` / `ShowCloseButton` | `bool` | passthrough | TBD — Stage 03 |
| `FooterText` | `string` | `FooterTemplate` text | TBD — Stage 03 |
| `AllowResize` / `AllowDrag` | `bool` | — | TBD — Stage 03 (LogAndFallback) |
| `CloseOnEscape` / `CloseOnOutsideClick` | `bool` | passthrough | TBD — Stage 03 |
| `Width` / `Height` | `string` | style forwarded | TBD — Stage 03 |
| `HeaderTemplate` / `BodyTemplate` / `FooterTemplate` | `RenderFragment` | passthrough | TBD — Stage 03 |

Divergences:
- DevExpress ships both `DxWindow` (non-modal) and `DxPopup` (modal). The 12-count maps to `DxWindow`. Migrators using `DxPopup` must manually swap or wait for a follow-up PR.
- Resize/drag are Sunfish-unknown — expect LogAndFallback.

---

### DxTooltip (compat-synthetic)

- **DevExpress target:** *none — DevExpress-Blazor has no general-purpose tooltip. The closest native peer is `DxFlyout`. This shim is compat-synthetic.*
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishTooltip`

| Proposed parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `PositionTarget` | `string` | `TargetSelector` | TBD — Stage 03 |
| `Position` | `FlyoutPosition` enum | `Placement` | TBD — Stage 03 |
| `HeaderText` / `BodyText` | `string` | `ChildContent` composition | TBD — Stage 03 |
| `HeaderTemplate` / `BodyTemplate` | `RenderFragment` | passthrough | TBD — Stage 03 |
| `Visible` / `VisibleChanged` | `bool` / `EventCallback<bool>` | passthrough | TBD — Stage 03 |

Divergences:
- Stage 02 Architecture may drop `DxTooltip` from the 12-count and slot `DxFlyout` (which is real) or a different component.

---

### DxToast + DxToastProvider

- **DevExpress target:** `DevExpress.Blazor.DxToast` + `DevExpress.Blazor.DxToastProvider`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Feedback.SunfishSnackbarHost` + `ISnackbarService`

| DX parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Text` / `Title` | `string` | snackbar message/title | TBD — Stage 03 |
| `Visible` | `bool` | imperative `Show`/`Hide` | TBD — Stage 03 |
| `RenderStyle` | `ToastRenderStyle` enum | severity enum | TBD — Stage 03 |
| `DisplayTime` / `AutoHide` | `int` / `bool` | duration | TBD — Stage 03 |
| `ShowCloseButton` | `bool` | passthrough | TBD — Stage 03 |
| `ActionText` / `Action` | `string` / `EventCallback` | — | TBD — Stage 03 |
| `HorizontalAlignment` / `VerticalAlignment` | enum | host-level | TBD — Stage 03 |
| `IconCssClass` | `string` | `CompatIconAdapter` | TBD — Stage 03 |
| `ThemeMode` / `AnimationType` | enum | — | TBD — Stage 03 (Dropped cosmetic) |

Divergences:
- Two-component pattern: `DxToastProvider` hosts, `DxToast` is the instance. Sunfish uses a single `SunfishSnackbarHost` + imperative `ISnackbarService`. Shim maps `DxToastProvider` → host; `DxToast` forwards declaratively to the service when `Visible=true`.

---

## 3. Licensing + POLICY compatibility

### 3.1 Subscription gating — **no free tier for Blazor**

- DevExpress Blazor ships under the **"ASP.NET and Blazor Subscription"** ($1,099.99/yr, renewal $549.99/yr). No free community tier for Blazor exists (DevExpress has a free WinForms/WPF community offering but it does **not** extend to Blazor).
- Implication for compat-devexpress: migrators off DevExpress Blazor are by definition paying commercial customers leaving the stack. There's no free-tier funnel to worry about, but there is an implicit "DevExpress notices" factor.

### 3.2 EULA analysis — Universal EULA Section 7

Full EULA: `https://www.devexpress.com/Support/EULAs/universal.xml` (note: other EULAs per SKU may override; Universal is the baseline).

Three clauses examined verbatim:

**Section 2 — Reverse engineering:**
> "You may not reverse engineer, decompile, create derivative works or disassemble the SOFTWARE DEVELOPMENT PRODUCT(S)."

This is a **standard reverse-engineering clause**. The compat-telerik pattern does not decompile or disassemble — it reproduces the public API surface from the *documentation* pages (which DevExpress publishes openly) plus demo-site observation. **No triggering activity** for Section 2.

**Section 7 — "API to the products":**
> "The LICENSEE shall not develop software applications that provide an application programming interface to the SOFTWARE DEVELOPMENT PRODUCT(S)"

Reads as: licensee cannot build an API that *exposes* / *wraps* DevExpress. compat-devexpress does not wrap DevExpress — it presents a DevExpress-shaped surface that delegates to Sunfish. **Arguably out of scope**, but worth legal review. The critical phrasing is "to the SOFTWARE DEVELOPMENT PRODUCTS" — our shim is not an API "to" DevExpress, it is an API whose shape *resembles* DevExpress.

**Section 7 — Non-compete:**
> "AT NO TIME MAY LICENSEE CREATE ANY TOOL, REDISTRIBUTABLE, OR PRODUCT THAT DIRECTLY OR INDIRECTLY COMPETES WITH ANY DEVEXPRESS PRODUCT(S), INCLUDING BUT NOT LIMITED TO THE SOFTWARE DEVELOPMENT PRODUCT(S), BY UTILIZING ALL OR ANY PORTION OF THE DEVEXPRESS SOFTWARE DEVELOPMENT PRODUCT(S)"

The triggering clause is **"BY UTILIZING ALL OR ANY PORTION OF THE DEVEXPRESS SOFTWARE DEVELOPMENT PRODUCT(S)"**. compat-devexpress, per [`packages/compat-telerik/POLICY.md`](../../../packages/compat-telerik/POLICY.md) Hard Invariant 1 (extended to all vendors), will carry **no DevExpress NuGet / DLL reference** — it does not "utilize" DevExpress software. The clause is not triggered.

### 3.3 Licensing conclusion — **not a workstream-halting finding, but a Stage 02 legal-review flag**

- **Not halting.** The compat-telerik pattern (no vendor NuGet reference, public-docs-only inventory, clean-room source reproduction, delegation to Sunfish canonical components) does not violate any verbatim EULA clause reviewed. Section 7's non-compete is scoped to "by utilizing DevExpress software", which we explicitly don't do. Section 2's reverse-engineering clause targets decompilation, not API-shape cloning.
- **But:** Section 7(a)'s "API to the SOFTWARE DEVELOPMENT PRODUCTS" clause is ambiguously worded. Safer reading: it prohibits wrapping DevExpress with a new API. Aggressive reading: it could prohibit *any* third-party API that interfaces with DevExpress-shaped code. The compat pattern threads the needle (we interface with *consumer* code that happened to target DevExpress, not with DevExpress itself), but this is the single legal ambiguity worth a lawyer touching before compat-devexpress ships.
- **DevExpress's historical aggressiveness:** DevExpress has not, to public knowledge, sued over compatibility shims of the kind we plan. They have been aggressive on DLL-level IP — their `Compiler Options Pack` and `Code Rush` have been on the offensive in the IDE/tooling space. Pattern-clone lawsuits in the Blazor component space would be unusual and highly visible.
- **Recommendation:** Stage 02 Architecture should include **a brief legal-review spike** (≤0.5 day) to confirm Section 7(a)'s scope. If legal counsel flags it, the fallback is to ship compat-devexpress with an **explicit EULA-disclaimer** in `POLICY.md` stating the shim does not utilize DevExpress software, reproduces only publicly documented API names for migration-off-ramp purposes, and is not a derivative work.
- **Worst case (if legal blocks):** drop compat-devexpress from the workstream. The other three vendors (Syncfusion, Infragistics/Ignite UI, Telerik already shipped) remain unaffected. Not a workstream-halting finding for the broader compat-expansion workstream; only vendor-halting for the DevExpress lane.

### 3.4 POLICY.md parity

All compat-telerik [`POLICY.md`](../../../packages/compat-telerik/POLICY.md) Hard Invariants extend to compat-devexpress identically:

1. **No DevExpress NuGet dependency.** Non-negotiable (also reinforces §3.3 analysis).
2. **All wrappers in root namespace `Sunfish.Compat.DevExpress`** (not nested), mirroring DevExpress's flat `DevExpress.Blazor.*` layout.
3. **Unsupported parameters throw** via `UnsupportedParam.Throw` for functional (non-cosmetic) impact.
4. **Divergences documented** in `docs/compat-devexpress-mapping.md`, same-PR as code change.

Policy gate (CODEOWNER approval per PR) carries over unchanged.

---

## 4. Decision 2 spike status (N/A for DevExpress)

**Confirmed: DevExpress Blazor is native Blazor**, not a Web Components wrapper.

- All 12 target components are Razor components compiled from `.razor` files with C# / IL behavior (not thin JS-interop wrappers over a WC library).
- JS interop is used for specific interactions (drag/resize on `DxWindow`, keyboard handling on `DxGrid`) but is **not the primary delivery mechanism** — the component tree is native Blazor.
- Decision 2 (intake §5) applies only to **Infragistics Ignite UI**, not DevExpress. No spike required in this lane.

The compat pattern validated on compat-telerik (`.razor` wrapper → delegates to `SunfishFoo`) applies directly to compat-devexpress with no architectural deviation.

---

## 5. Summary table

| # | Target | DX type | Sunfish target | Child components | Notes |
|---|---|---|---|---|---|
| 1 | Button | `DxButton` | `SunfishButton` | none | `IconCssClass` translation via `CompatIconAdapter`. |
| 2 | Icon | **no DX type** | `SunfishIcon` | none | **compat-synthetic**; Stage 02 may drop. |
| 3 | CheckBox | `DxCheckBox<T>` | `SunfishCheckbox` | none | Generic T; tri-state fallback. |
| 4 | TextBox | `DxTextBox` | `SunfishTextBox` | none | Standard shape. |
| 5 | DropDownList | **no DX type** | `SunfishDropDownList` | none | **compat-synthetic**; mirrors `DxComboBox` w/ `AllowUserInput=false`. |
| 6 | ComboBox | `DxComboBox<TData, TValue>` | `SunfishComboBox` | `DxListEditorColumn` | Standard shape. |
| 7 | DatePicker | `DxDateEdit<T>` | `SunfishDatePicker` | none | Broad T support lossy to Sunfish `DateTime?`. |
| 8 | Form | `DxFormLayout` | `SunfishForm` | `DxFormLayoutItem/Group/TabPages/TabPage` | **Most lossy wrapper** — layout vs validation mismatch. |
| 9 | Grid | `DxGrid` (non-generic) | `SunfishDataGrid<T>` (generic) | `DxGridDataColumn/SelectionColumn/CommandColumn/SpinEditColumn/DateEditColumn/ComboBoxColumn<T>` | Generic-erasure translation required. 6 child shims. |
| 10 | Window | `DxWindow` | `SunfishWindow` | none | `DxPopup` out-of-scope in Phase 1. |
| 11 | Tooltip | **no DX type** | `SunfishTooltip` | none | **compat-synthetic**; Stage 02 may slot `DxFlyout`. |
| 12 | Notification | `DxToast` + `DxToastProvider` | `SunfishSnackbarHost` + `ISnackbarService` | `DxToast` inside `DxToastProvider` | Two-component pattern. |

**EventArgs types requiring shims under `compat-devexpress/EventArgs/`:**
1. `ValueChangedEventArgs<T>` (CheckBox)
2. `SelectedDataItemChangedEventArgs<TData>` (ComboBox)
3. `SelectedDataItemsChangedEventArgs` (Grid)
4. `CustomizeCellDisplayTextEventArgs` (Grid)
5. `CustomizeElementEventArgs` / `GridCustomizeElementEventArgs` (Grid)
6. `GridEditModelSavingEventArgs` (Grid)
7. `GridDataItemDeletingEventArgs` (Grid)
8. `GridRowClickEventArgs` (Grid, parity with Telerik shim)
9. `WindowClosingEventArgs` (Window)
10. `WindowClosedEventArgs` (Window)
11. (Drag/resize args — deferred; ship only if event is wired)

**Total: ~10 initial EventArgs shims** (plus 6+ deferred).

---

## 6. Stage 01 Discovery handoff notes

- **12-count completeness:** 9 of 12 have direct DevExpress type peers; 3 (`DxIcon`, `DxDropDownList`, `DxTooltip`) are **compat-synthetic** — no DevExpress type peer. Stage 02 Architecture to decide: keep synthetics for uniform 12-count, or substitute real DX peers (`DxFlyout` for tooltip, `DxListBox` for dropdown, drop icon and slot e.g. `DxMemo`).
- **Most divergent wrapper:** `DxFormLayout` → `SunfishForm`. Layout-component vs validation-component mismatch. Migrators should expect markup restructuring beyond using-swap.
- **Most complex wrapper:** `DxGrid`. Non-generic → generic translation; 6 child column shims; large EventArgs surface; most edit modes likely throw in Phase 1.
- **Compat-shared reuse:** all child-component shims (`DxGridDataColumn`, `DxFormLayoutItem`, etc.) ride on `CompatChildComponent<TParent>` — the intake §5 Decision 4 prerequisite already landed in compat-telerik. No new infra required.
- **Legal-review flag:** EULA Section 7(a) language is ambiguous. Stage 02 Architecture should schedule a ≤0.5-day legal-spike before compat-devexpress scaffolding begins. Not workstream-halting.
- **Research budget:** used 17 of ~30 WebFetch allowance. Deferred-depth items (full `DxGridDataColumn` parameter set, `DxToastProvider` providers behavior, full `DxWindow` event surface) tagged `TBD — Stage 03` and will be resolved when the wrapper is actually scaffolded against the Sunfish delegation target.

---

_Discovery complete. Ready for Stage 02 Architecture dispatch once all three vendor discovery artifacts (Syncfusion, DevExpress, Infragistics) are present._
