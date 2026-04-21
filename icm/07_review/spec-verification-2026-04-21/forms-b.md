# Forms family — half B (complex/list/editor inputs) — Spec Verification

**Date:** 2026-04-21
**Pipeline:** 07_review / spec-verification-2026-04-21
**Scope:** 8 components in `forms` category (ADR 0022 Tier 1) — the complex/list-driven/editor inputs
**Mode:** Read-only audit against `apps/docs/component-specs/**`

---

## Summary

| Spec dir | Impl component | Verdict | Missing | Bug | Incomplete | Covered | Severity (max) |
|---|---|---|---:|---:|---:|---:|---|
| colorgradient | SunfishColorGradient | **needs-work** | 1 | 0 | 3 | 5 | missing |
| colorpalette | SunfishColorPalette | **needs-work** | 2 | 0 | 2 | 5 | missing |
| colorpicker | SunfishColorPicker (+ ColorPickerGradientView / ColorPickerPaletteView) | **needs-work** | 2 | 1 | 6 | 6 | bug |
| flatcolorpicker | SunfishFlatColorPicker | **needs-work** | 3 | 1 | 3 | 3 | bug |
| combobox | SunfishComboBox (generic) | **downgrade-to-partial** | 11 | 1 | 5 | 6 | missing |
| multiselect | SunfishMultiSelect (generic) + MultiSelectSettings/PopupSettings | **needs-work** | 2 | 1 | 3 | 12 | bug |
| listbox | SunfishListBox (non-generic, object-based) | **downgrade-to-partial** | 10 | 1 | 2 | 2 | missing |
| slider | SunfishSlider (non-generic, `double`) | **downgrade-to-partial** | 9 | 1 | 2 | 3 | missing |
| **Totals** | | **0 verified / 4 needs-work / 4 downgrade-to-partial** | **40** | **6** | **26** | **42** | |

**Bottom-line:** 0 of 8 verified. 4 flagged **needs-work** (color-family + multiselect), 4 flagged **downgrade-to-partial** (combobox, listbox, slider, and mapping for combobox/multiselect/listbox in `component-mapping.json` is outdated — points at `SunfishSelect`/`SunfishList` but real impls exist).

---

## colorgradient (SunfishColorGradient)

### Gaps

| # | Severity | Spec ref | Gap |
|---|---|---|---|
| 1 | missing | accessibility/wai-aria-support.md | Root `.k-colorgradient` element has NO `role=textbox`, NO top-level `aria-label`, NO `tabindex=0`, NO `aria-invalid` — only the inner canvas has `tabindex=0`/`aria-label`. |
| 2 | incomplete | accessibility/wai-aria-support.md | Drag handles lack `role=slider`. HSV drag handle doesn't emit `aria-valuetext` nor `aria-orientation=undefined`. |
| 3 | incomplete | accessibility/wai-aria-support.md | Per-channel NumericTextBoxes missing explicit `aria-label` wiring. |
| 4 | incomplete | overview.md | CSS prefix is `mar-colorgradient` throughout; spec uses `k-colorgradient`. Style-override parity broken. |
| 5 | covered | overview.md | `Value`, `ValueFormat`, `Format`, `Formats`, `ShowOpacityEditor`, `Enabled`, `Class`, `FormatChanged`, `ValueChanged` all present. |

### Verdict

**needs-work** — Core data contract is sound; accessibility and CSS-class prefix are the blockers.

- **Impl:** `packages/ui-adapters-blazor/Components/Forms/Inputs/SunfishColorGradient.razor`
- **Demo:** `apps/kitchen-sink/Pages/Components/Editors/ColorGradient/Overview/` (also legacy `Pages/Components/ColorGradient/`)
- **Specs reviewed:** `overview.md`, `events.md`, `accessibility/wai-aria-support.md`

---

## colorpalette (SunfishColorPalette)

### Gaps

| # | Severity | Spec ref | Gap |
|---|---|---|---|
| 1 | missing | accessibility/wai-aria-support.md | Root `.k-colorpalette` needs `aria-activedescendant` pointing at focused tile's `id` — impl uses CSS class `mar-colorpalette__tile--focused` with no `id` attribute on tiles; screen readers cannot track focus. |
| 2 | missing | events.md | `OnChange` event NOT IMPLEMENTED (only `ValueChanged`). |
| 3 | incomplete | events.md | `OnBlur` NOT IMPLEMENTED. |
| 4 | incomplete | overview.md | CSS prefix `mar-colorpalette` not `k-colorpalette` — style-override parity broken. |
| 5 | covered | overview.md | `Value`, `Colors`, `Columns`, `TileHeight`, `TileWidth`, `Enabled`, `Id`, `TabIndex` all present. `ColorPalettePresets` class exists with `Office` default. |

### Verdict

**needs-work** — Rendering and keyboard navigation are spec-shaped (arrow/Home/End/Enter); events and ARIA identity need filling in.

- **Impl:** `packages/ui-adapters-blazor/Components/Forms/Inputs/SunfishColorPalette.razor`
- **Demo:** `apps/kitchen-sink/Pages/Components/Editors/ColorPalette/Overview/` (also legacy `Pages/Components/ColorPalette/`)
- **Specs reviewed:** `overview.md`, `custom-colors.md`, `presets.md`, `events.md`, `accessibility/wai-aria-support.md`

---

## colorpicker (SunfishColorPicker + ColorPickerGradientView / ColorPickerPaletteView)

### Gaps

| # | Severity | Spec ref | Gap |
|---|---|---|---|
| 1 | missing | accessibility/wai-aria-support.md | Root `.k-colorpicker` missing `role=combobox` + `aria-controls` + `tabindex=0`. Trigger button has `aria-haspopup=dialog`/`aria-expanded` but it's not the root. |
| 2 | missing | appearance.md | `Size`/`Rounded`/`FillMode` are declared as `string` (plain values); spec demands `Sunfish.Blazor.ThemeConstants.ColorPicker.{Size|Rounded|FillMode}` constants. No ThemeConstants nested class for ColorPicker. |
| 3 | bug | overview.md | `AdaptiveMode` parameter is declared but NEVER consumed by the render template. Consumers setting it get silently ignored. |
| 4 | incomplete | overview.md | `Icon` typed as `RenderFragment?` — spec signature is `object` (font icon identifier). |
| 5 | incomplete | overview.md | CSS prefix is `mar-colorpicker`/`mar-colorpicker--{size}` etc., spec uses `k-`. |
| 6 | incomplete | events.md | `OnOpen` has `IsCancelled` support, `OnClose` does — OK. `OnChange` fires on Apply — matches spec. But `ViewChanged` does not fire when the parent sets `View` externally (only on user-driven SwitchView). |
| 7 | incomplete | accessibility/wai-aria-support.md | AdaptiveMode ActionSheet fallback spec not implemented. |
| 8 | incomplete | views.md | `ColorPickerGradientView` / `ColorPickerPaletteView` exist and register via `IColorPickerViewHost` cascade — good. Parameters on the view classes match spec. |
| 9 | covered | overview.md | Methods `Open`, `Close`, `FocusAsync` present. |
| 10 | covered | overview.md | Popup uses `role=dialog`, trigger has correct ARIA. |
| 11 | covered | overview.md | `Value`, `ValueFormat`, `View`, `ViewChanged`, `ColorPickerViews`, `ShowPreview`, `ShowButtons`, `ShowClearButton` all present. |
| 12 | covered | views.md | HSV canvas + hue slider + opacity slider + format inputs all present in shared gradient child. |
| 13 | covered | overview.md | Palette view composes `SunfishColorPalette` correctly. |
| 14 | covered | events.md | `OnChange`, `ValueChanged`, `OnOpen(ColorPickerOpenEventArgs)`, `OnClose(ColorPickerCloseEventArgs)` fire at the right lifecycle points. |

### Verdict

**needs-work** — By far the most comprehensive impl in this batch; blocked on root-element ARIA, Icon type, ThemeConstants, and the `AdaptiveMode` bug.

- **Impl:** `packages/ui-adapters-blazor/Components/Forms/Inputs/SunfishColorPicker.razor` (parent), `ColorPickerGradientView.cs`, `ColorPickerPaletteView.cs`, `ColorPickerViewBase.cs`, `IColorPickerViewHost.cs`
- **Demo:** `apps/kitchen-sink/Pages/Components/Editors/ColorPicker/{Overview,Accessibility,Appearance,Events}/`
- **Specs reviewed:** `overview.md`, `appearance.md`, `events.md`, `views.md`, `accessibility/wai-aria-support.md`

---

## flatcolorpicker (SunfishFlatColorPicker)

### Gaps

| # | Severity | Spec ref | Gap |
|---|---|---|---|
| 1 | missing | views.md | Spec requires declarative child components `FlatColorPickerGradientView` / `FlatColorPickerPaletteView` (parallel to ColorPicker's view host pattern). Impl uses a non-declarative `IEnumerable<ColorPickerView>? Views` enum parameter instead — spec-shape violation. |
| 2 | missing | events.md | `OnChange` event NOT IMPLEMENTED. Spec requires it (fires on Apply / immediate-commit when ShowButtons=false). Impl only exposes `ValueChanged`. |
| 3 | missing | accessibility/wai-aria-support.md | Root `.k-flatcolorpicker` missing `role=textbox`, `aria-label`, `tabindex=0`. |
| 4 | bug | overview.md | `ViewChanged` invoked only from user-driven `SwitchViewAsync`; when the parent changes `View` parameter, no event fires, so 2-way binding is broken in the param-push direction. |
| 5 | incomplete | overview.md | No `ThemeConstants.FlatColorPicker.*` appearance API (Size/Rounded/FillMode absent entirely). |
| 6 | incomplete | overview.md | CSS prefix `mar-flatcolorpicker` vs spec `k-flatcolorpicker`. |
| 7 | incomplete | views.md | Same Icon/ThemeConstants/AdaptiveMode story as ColorPicker applies. |
| 8 | covered | overview.md | Value, Format, Formats, ShowOpacityEditor, ShowPreview, ShowButtons, ShowClearButton, Enabled all present. Apply/Cancel/Clear wiring correct. |
| 9 | covered | views.md | View toggle buttons + both gradient and palette child embeds render correctly when visible. |
| 10 | covered | overview.md | Pending/Committed preview boxes present. |

### Verdict

**needs-work** — Surface works for the happy path but the view-child API shape diverges materially from the ColorPicker sibling and from the spec.

- **Impl:** `packages/ui-adapters-blazor/Components/Forms/Inputs/SunfishFlatColorPicker.razor`
- **Demo:** `apps/kitchen-sink/Pages/Components/Forms/FlatColorPicker/Overview/` (also legacy `Pages/Components/FlatColorPicker/`)
- **Specs reviewed:** `overview.md`, `events.md`, `views.md`, `accessibility/wai-aria-support.md`

---

## combobox (SunfishComboBox<TItem, TValue>)

### Gaps

| # | Severity | Spec ref | Gap |
|---|---|---|---|
| 1 | missing | events.md | No `OnOpen`/`OnClose` events (spec requires `PopupEventArgs` with IsCancelled). |
| 2 | missing | events.md | No `OnRead` event (async server-side paging / filtering). |
| 3 | missing | events.md | No `OnBlur` event. |
| 4 | missing | events.md | No `OnItemRender` event (per-item class/disabled callback). |
| 5 | missing | grouping.md | No `GroupField` parameter (spec shows one-level grouping with sticky headers). |
| 6 | missing | virtualization.md | No virtualization — no `ScrollMode`, `ItemHeight`, `PageSize`, `ValueMapper`. |
| 7 | missing | templates.md | Missing `HeaderTemplate`, `FooterTemplate`, `NoDataTemplate`. Only `ItemTemplate` + `ValueTemplate` parameters exist (ValueTemplate is declared but not rendered). |
| 8 | missing | overview.md | No `DebounceDelay` (spec default 150 ms). |
| 9 | missing | overview.md | No `AdaptiveMode`, `InputMode`, `LoaderShowDelay`, `Title`, `TabIndex`, `Id`, `Filterable` (always-on), `MinLength`. |
| 10 | missing | overview.md | No `ComboBoxSettings` / `ComboBoxPopupSettings` child-component API. |
| 11 | missing | overview.md | No `ThemeConstants.ComboBox.{Size|Rounded|FillMode}` surface. |
| 12 | missing | overview.md | No public methods `Open`, `Close`, `Rebind`, `FocusAsync`. |
| 13 | bug | overview.md | `CloseDropdownDelayed` uses a fire-and-forget `Task.Delay(200)` instead of `@onblur` with proper cancellation — race condition on rapid focus/click. |
| 14 | incomplete | filter.md | `FilterMode` parameter uses `ComboBoxFilterMode` enum; spec calls the parameter `FilterOperator` with `StringFilterOperator` enum. Naming mismatch ripples to consumer code. |
| 15 | incomplete | accessibility/wai-aria-support.md | Root `<div>` lacks role/aria — only the `<input>` has `role=combobox`/`aria-expanded`/`aria-autocomplete=list`. Missing `aria-controls`, `aria-activedescendant`, `aria-busy`. |
| 16 | incomplete | overview.md | Popup has no id wiring, so `aria-controls`/`aria-activedescendant` can't be set even if added to the input. |
| 17 | incomplete | accessibility/wai-aria-support.md | Listbox items have no `id` — cannot point `aria-activedescendant`. |
| 18 | covered | overview.md | Generic `<TItem, TValue>`; `Data`/`TextField`/`ValueField`/`Value`/`Placeholder`/`Disabled`/`ReadOnly`/`IsInvalid`/`AllowCustom`/`ShowClearButton` parameters present. |
| 19 | covered | overview.md | Keyboard: ArrowDown/Up/Enter/Escape wired. |
| 20 | covered | accessibility/wai-aria-support.md | `<ul role=listbox>`, `<li role=option>` with `aria-selected`. |

### Verdict

**downgrade-to-partial** — Impl is a basic filterable dropdown. It covers ~25% of the spec surface; whole feature areas (OnRead, grouping, virtualization, templates, events, settings-child-API, ThemeConstants) are absent. Mapping JSON currently says combobox → `SunfishSelect` (wrong) and the real `SunfishComboBox` is far short of "implemented."

- **Impl:** `packages/ui-adapters-blazor/Components/Forms/Inputs/SunfishComboBox.razor`
- **Demo:** `apps/kitchen-sink/Pages/Components/Editors/ComboBox/{Overview,Accessibility,Appearance,Events}/` and `Pages/Components/ComboBox/ComboBox/`
- **Specs reviewed:** 12 md: `overview.md`, `data-bind.md`, `events.md`, `filter.md`, `grouping.md`, `templates.md`, `virtualization.md`, `custom-value.md`, `pre-select-item.md`, `refresh-data.md`, `appearance.md`, `accessibility/wai-aria-support.md`

---

## multiselect (SunfishMultiSelect<TItem, TValue>)

### Gaps

| # | Severity | Spec ref | Gap |
|---|---|---|---|
| 1 | missing | overview.md | `TagMode` enum in impl is `MultiSelectTagMode.{Single, Multiple}` but spec calls for `{Single, Multiple, Summarized}` (3 members). Impl hybrids Summarized into Single by showing a summary via `SummaryTagTemplate` — semantically close but enum shape differs. |
| 2 | missing | overview.md | `MaxAllowedTags` parameter NOT IMPLEMENTED — impl uses `MaxVisibleTags` (different semantics: hides beyond N with "+X more" chip, not a hard cap on selection). |
| 3 | bug | overview.md | `_listboxId` field-initializer runs once per instance as a static-style `$"ms-{Guid.NewGuid():N}"` captured into a `readonly` field — but this field is assigned during component construction, so the same id is stable within an instance. Not a bug. **Real bug:** settings child components cascade `IsFixed=true` — means settings values are captured on first render and subsequent parameter changes on `MultiSelectSettings` won't propagate. |
| 4 | incomplete | filter.md | `FilterOperator` uses `ComboBoxFilterMode` enum (reused) not the spec's `StringFilterOperator`. |
| 5 | incomplete | grouping.md | Grouping works only in non-virtualized path — virtualized path silently ignores `GroupField` (see code comment). Spec doesn't carve this out. |
| 6 | incomplete | accessibility/wai-aria-support.md | Root has `role=combobox`, `aria-haspopup=listbox`, `aria-expanded`, `aria-activedescendant`, `aria-multiselectable` — all good. Missing `aria-describedby` wiring to tag list for SR tag-count announcement. |
| 7 | covered | overview.md | Generic `<TItem, TValue>`; `Data`/`TextField`/`ValueField`/`Value`/`ValueChanged` wired. |
| 8 | covered | overview.md | Templates: `ItemTemplate`, `TagTemplate`, `SummaryTagTemplate`, `HeaderTemplate`, `FooterTemplate`, `NoDataTemplate` all present. |
| 9 | covered | events.md | `OnOpen`/`OnClose` (PopupEventArgs with IsCancelled), `OnRead` (MultiSelectReadEventArgs), `OnItemRender`, `OnBlur`, `OnChange`, `ValueChanged`, `OnFilter` all present. |
| 10 | covered | virtualization.md | `EnableVirtualization`, `ItemHeight`, `PageSize`, `ValueMapper` present. |
| 11 | covered | overview.md | `ComboBox`-style settings: `MultiSelectSettings` + `MultiSelectPopupSettings` cascade child-component API via `IMultiSelectSettingsSink` — impl-specific but spec-shape parity present. |
| 12 | covered | overview.md | `AutoClose`, `Filterable`, `MinLength`, `PersistFilterOnSelect`, `DebounceDelay`, `ShowArrowButton`, `ShowClearButton`, `AllowCustom` all present. |
| 13 | covered | overview.md | Public methods `Open`, `Close`, `Rebind`, `Refresh` all present. |
| 14 | covered | accessibility/wai-aria-support.md | Listbox has `role=listbox`, items have `role=option`, `aria-selected`, `aria-disabled`, item ids `{_listboxId}-opt-{idx}`. |
| 15 | covered | overview.md | `AdaptiveMode` parameter + `EffectiveAdaptiveMode` internal — BUT: flagged by the implementation comments as "plumbing, not currently consumed." Same issue as ColorPicker. |

### Verdict

**needs-work** — Of the forms-b impls this is the most thorough. Main blockers: `TagMode` enum shape (add Summarized), `MaxAllowedTags` semantics, `AdaptiveMode` not rendered, enum naming.

- **Impl:** `packages/ui-adapters-blazor/Components/Forms/Inputs/SunfishMultiSelect.razor`, `MultiSelectModels.cs`, `MultiSelectSettings.cs`
- **Demo:** `apps/kitchen-sink/Pages/Components/Editors/MultiSelect/{Overview,Accessibility,Appearance,Events}/` and `Pages/Components/MultiSelect/MultiSelect/`
- **Specs reviewed:** 13 md: `overview.md`, `data-bind.md`, `events.md`, `filter.md`, `grouping.md`, `templates.md`, `virtualization.md`, `custom-values.md`, `tags-mode.md`, `pre-select-items.md`, `refresh-data.md`, `appearance.md`, `accessibility/wai-aria-support.md`

---

## listbox (SunfishListBox)

### Gaps

| # | Severity | Spec ref | Gap |
|---|---|---|---|
| 1 | missing | overview.md | Not generic — declared as `SunfishListBox` with `object` Data/Value/SelectedItems. Spec: `SunfishListBox<T>` with `TItem` type inference. No `TItem` parameter. |
| 2 | missing | toolbar.md | No toolbar at all. Spec requires `<ListBoxToolBarSettings>` + `<ListBoxToolBar>` with built-in tools: `ListBoxToolBarMoveUpTool`, `MoveDownTool`, `TransferToTool`, `TransferFromTool`, `TransferAllToTool`, `TransferAllFromTool`, `RemoveTool`, `CustomTool`. Zero of these exist. |
| 3 | missing | toolbar.md | No `ToolBarPosition` parameter (spec: `ListBoxToolBarPosition` enum with Top/Right/Bottom/Left). |
| 4 | missing | events.md | No `OnReorder`, `OnRemove`, `OnTransfer`, `OnDrop` events. |
| 5 | missing | connect.md | No `ConnectedListBoxId` — cannot link ListBoxes. |
| 6 | missing | drag-drop.md | No drag-and-drop — spec requires `Draggable` (declared but unused), `DropSources` (missing), `OnDrop` (missing). Impl has `Draggable`/`Reorderable` params as decoration only. |
| 7 | missing | overview.md | No `Rebind()` public method for programmatic data changes. |
| 8 | missing | templates.md | No `NoDataTemplate` (spec requires one). Impl has a simple `Placeholder` string. |
| 9 | missing | overview.md | No `Size` appearance parameter / `ThemeConstants.ListBox.Size`. |
| 10 | missing | overview.md | No `AriaLabel` / `AriaLabelledBy` parameters. No `Width` parameter. |
| 11 | bug | overview.md | `SelectionMode.Single` sets `Value` via `ValueChanged`; but `SelectedItems` two-way binding also fires via `SelectedItemsChanged`. In Single mode, setting `Value` does not also update `SelectedItems`, creating split-state ambiguity. |
| 12 | incomplete | accessibility/wai-aria-support.md | Root has `role=listbox` + `aria-multiselectable` — correct. Missing `.k-list-ul` target (role is on the outer div, not on a `<ul>`). Items lack `tabindex=0` on focused, `-1` on others. No keyboard navigation handler at all. |
| 13 | incomplete | overview.md | Field lookup uses raw reflection with string equality on `ToString()` values — breaks on null/non-string value fields. |
| 14 | covered | overview.md | `Data`, `SelectedItems`/`SelectedItemsChanged`, `TextField`, `ValueField`, `SelectionMode` (Single/Multiple) enum, `Height`, `Enabled`, `ItemTemplate` present. |

### Verdict

**downgrade-to-partial** — Toolbar, drag/drop, connect, events, Rebind, and generic type parameter are all absent; this is a selectable list wrapper, not a ListBox per spec.

- **Impl:** `packages/ui-adapters-blazor/Components/Forms/Inputs/SunfishListBox.razor`
- **Demo:** `apps/kitchen-sink/Pages/Components/DataDisplay/ListBox/Overview/` and legacy `Pages/Components/ListBox/`
- **Specs reviewed:** 8 md: `overview.md`, `selection.md`, `toolbar.md`, `connect.md`, `drag-drop.md`, `templates.md`, `events.md`, `accessibility/wai-aria-support.md`

---

## slider (SunfishSlider)

### Gaps

| # | Severity | Spec ref | Gap |
|---|---|---|---|
| 1 | missing | overview.md | Not generic — hard-typed to `double`. Spec: `SunfishSlider<TValue>` generic over `int`/`decimal`/`double`. |
| 2 | missing | overview.md | No `SmallStep` / `LargeStep` params — has single `Step` + optional `LargeStep`. Spec: **both** SmallStep and LargeStep are required. |
| 3 | missing | overview.md | No `TickPosition` parameter (`SliderTickPosition` enum: Before/After/Both/None). Impl always renders ticks under the track when LargeStep is set. |
| 4 | missing | decimals.md | No `Decimals` parameter (precision control for floating-point rounding). |
| 5 | missing | overview.md | No `ThemeConstants.Slider.{Size|Rounded}` appearance API. |
| 6 | missing | events.md | No `OnChange` event — spec requires it in addition to `ValueChanged` so consumers can opt out of continuous-drag re-renders. |
| 7 | missing | accessibility/wai-aria-support.md | Uses native HTML `<input type=range>`. Drag handle not realized as `.k-draghandle` with `role=slider`, `aria-label`, `aria-valuenow/min/max/text`, `tabindex=0`. The browser's native range input provides implicit semantics, but this fails the spec's explicit selector/attribute contract (style override & `.k-draghandle` targeting won't match). |
| 8 | missing | accessibility/wai-aria-support.md | Decrement/Increment buttons missing `aria-hidden=true`, `tabindex=-1` — spec says these should be excluded from AT/tab order. Current `aria-label="Decrease"/"Increase"` exposes them. |
| 9 | missing | label-template.md | `LabelTemplate` binds to `double` context; would need generic TValue to match spec's `RenderFragment<TValue>`. |
| 10 | missing | overview.md | No `Width` parameter explicitly; relies on AdditionalAttributes / CombineStyles. |
| 11 | bug | overview.md | Tick rendering loop uses `while (tickValue <= Max)` with `double` arithmetic — will accumulate rounding error, causing missed last tick exactly on Max (related to the Decimals discussion in spec). |
| 12 | incomplete | overview.md | CSS prefix `sf-slider` / `mar-slider` — spec uses `k-slider`. |
| 13 | covered | overview.md | `Value`, `Min`, `Max`, `Orientation` (Horizontal/Vertical), `LabelTemplate`, `Enabled`, `ShowButtons` present. |
| 14 | covered | events.md | `ValueChanged` fires continuously — matches spec's "continuous while dragging" behavior. |

### Verdict

**downgrade-to-partial** — Core data binding works via the native range input, but the spec's full drag-handle selector/ARIA contract, generic TValue, Decimals/SmallStep+LargeStep model, TickPosition, and OnChange are all missing. Current mapping status "implemented" is too generous.

- **Impl:** `packages/ui-adapters-blazor/Components/Forms/Inputs/SunfishSlider.razor`
- **Demo:** `apps/kitchen-sink/Pages/Components/Editors/Slider/{Overview,Accessibility,Appearance,Events}/` and `Pages/Components/Slider/`
- **Specs reviewed:** 6 md: `overview.md`, `steps.md`, `decimals.md`, `label-template.md`, `events.md`, `accessibility/wai-aria-support.md`

---

## Next actions (Tier 2 priority order)

1. **Fix component-mapping.json entries.** `combobox` and `multiselect` currently point at `SunfishSelect`; `listbox` points at `SunfishList`. Real impls exist (`SunfishComboBox`, `SunfishMultiSelect`, `SunfishListBox`). Correct the mapping and re-assess `status` for each (multiselect → `implemented`, combobox/listbox/slider → `partial`).
2. **SunfishSlider generic rewrite.** Convert to `SunfishSlider<TValue>`, add `SmallStep`/`LargeStep`/`Decimals`/`TickPosition`/`OnChange`, add `.k-draghandle` custom-handle rendering with full ARIA. Fix tick-loop rounding.
3. **SunfishListBox toolbar + events expansion.** Add generic `<T>`, toolbar child-components (`ListBoxToolBar` + 7 built-in tools + CustomTool), `ToolBarPosition`, `OnReorder`/`OnRemove`/`OnTransfer`/`OnDrop` events, `ConnectedListBoxId` + `DropSources` + `Draggable` wiring, `Rebind()`, `NoDataTemplate`, `AriaLabel`/`AriaLabelledBy`.
4. **SunfishComboBox feature parity with SunfishMultiSelect.** Port `OnOpen`/`OnClose`/`OnRead`/`OnBlur`/`OnItemRender` events, settings child-components, grouping, virtualization, `HeaderTemplate`/`FooterTemplate`/`NoDataTemplate`, `Debounce`, `Open/Close/Rebind/FocusAsync` methods, `ThemeConstants.ComboBox.*`, fix the `CloseDropdownDelayed` race. Rename `FilterMode` → `FilterOperator` (`StringFilterOperator` enum).
5. **SunfishColorPicker `AdaptiveMode` + Icon + ThemeConstants.** Consume `AdaptiveMode` in the template (ActionSheet fallback), switch `Icon` type from `RenderFragment` to `object` (font-icon identifier), add `ThemeConstants.ColorPicker.{Size,Rounded,FillMode}` static constants. Add root-element `role=combobox` + `aria-controls` + `tabindex=0`.
6. **SunfishFlatColorPicker declarative view children.** Introduce `FlatColorPickerGradientView` / `FlatColorPickerPaletteView` child components (mirroring the `IColorPickerViewHost` pattern). Add `OnChange` event. Fix the `ViewChanged` param-push path. Add root `role=textbox` + `tabindex=0`.
7. **SunfishMultiSelect `TagMode` extension.** Add `Summarized` enum member, add `MaxAllowedTags` parameter (hard cap), wire `AdaptiveMode` to render. Fix `IsFixed=true` on the settings cascade.
8. **SunfishColorPalette `OnChange`/`OnBlur` + aria-activedescendant.** Assign stable `id` to tiles, wire `aria-activedescendant` on the grid root. Add `OnChange`/`OnBlur` events.
9. **CSS class prefix alignment.** All color-family + slider + multiselect need a `k-`-prefixed shim (either rename `mar-` → `k-`, or add `k-` class alongside) to satisfy theme-override spec text and compat-telerik matching.
10. **SunfishColorGradient root-element ARIA.** Wrap root in `role=textbox` + `tabindex=0` + `aria-label`/`aria-invalid`.

---

**Methodology note:** Audit is read-only; no code edits. Severity taxonomy: `missing` = spec surface has no impl / no wiring; `bug` = impl exists but wrong behavior/type; `incomplete` = shape right but attribute/detail divergence; `covered` = matches spec.
