# Buttons Folder - Gap Analysis

> Generated 2026-03-30. Compares component implementations against their spec docs.
> Base class `SunfishComponentBase` provides: `Class`, `Style`, `AdditionalAttributes`.

## Table of Contents

1. [SunfishButton](#1-marilobutton-gap-analysis)
2. [SunfishButtonGroup](#2-marilobuttongroup-gap-analysis)
3. [SunfishChip](#3-marilochip-gap-analysis)
4. [SunfishChipSet](#4-marilochipset-gap-analysis)
5. [SunfishIconButton](#5-mariloiconbutton-gap-analysis)
6. [SunfishFab](#6-marilofab-gap-analysis)
7. [SunfishSegmentedControl](#7-marilosegmentedcontrol-gap-analysis)
8. [SunfishSplitButton](#8-marilosplitbutton-gap-analysis)
9. [SunfishToggleButton](#9-marilotogglebutton-gap-analysis)

---

# 1. SunfishButton Gap Analysis

## Summary

The implementation covers basic button rendering with variant, size, fill mode, rounded, disabled, icon, and click handling. The spec defines several additional parameters the code does not implement.

## Spec → Code Gaps

| Gap | Severity | Details |
|-----|----------|---------|
| Missing `ButtonType` parameter | **High** | Spec defines `ButtonType` enum (Submit/Reset/Button, default Submit). Code has no `type` attribute on the `<button>`. Users cannot control form submission behavior. |
| Missing `Enabled` parameter | **Medium** | Spec uses `Enabled` (bool, default true). Code uses `Disabled` (bool, default false). Inverted semantics; spec consumers expect `Enabled`. |
| Missing `Form` parameter | **Medium** | Spec: `Form` (string) to associate a submit button with an external form. Not implemented. |
| Missing `Id` parameter | **Low** | Spec: `Id` (string) for the `id` attribute. Not implemented (could be passed via `AdditionalAttributes`, but not explicit). |
| Missing `Title` parameter | **Low** | Spec: `Title` (string) for the `title` attribute. Not implemented. |
| Missing `Visible` parameter | **Low** | Spec: `Visible` (bool, default true). Not implemented. |
| Missing `FocusAsync()` method | **Medium** | Spec documents a `FocusAsync` method via component reference. Not implemented. |
| `Icon` type mismatch | **Medium** | Spec: `Icon` is `object` (supports predefined icon objects or custom). Code: `Icon` is `RenderFragment?`. Different usage pattern. |

## Code → Spec Gaps

| Gap | Severity | Details |
|-----|----------|---------|
| `IsOutline` parameter | **Low** | Code defines `IsOutline` (bool) but it is not passed to `CssProvider` or used in rendering. Dead parameter; not in spec. |
| `FillMode` parameter | **Low** | Code exposes `FillMode` (default Solid). Spec mentions fill mode only in the appearance/styling article, not in overview parameters table. Likely intentional but underdocumented. |
| `Rounded` parameter | **Low** | Code exposes `Rounded` (default Medium). Same as FillMode -- in code but not in overview params table. |

## Recommended Changes

1. **[High]** Add `ButtonType` parameter with `type` attribute on the `<button>` element (default `submit`).
2. **[Medium]** Rename `Disabled` to `Enabled` (with inverted default) to match spec convention, or add `Enabled` as an alias.
3. **[Medium]** Add `Form` parameter for external form association.
4. **[Medium]** Implement `FocusAsync()` using `ElementReference`.
5. **[Low]** Remove dead `IsOutline` parameter or wire it into the CSS provider.

---

# 2. SunfishButtonGroup Gap Analysis

## Summary

The implementation is minimal -- only a wrapping `<div>` with `ChildContent`. The spec describes a rich component with selection modes, enabled state, width control, and child button types (`ButtonGroupButton`, `ButtonGroupToggleButton`).

## Spec → Code Gaps

| Gap | Severity | Details |
|-----|----------|---------|
| Missing `SelectionMode` parameter | **High** | Spec: `ButtonGroupSelectionMode` enum (default Single). Controls single/multiple toggle selection. Not implemented. |
| Missing `Enabled` parameter | **High** | Spec: `Enabled` (bool, default true). Not implemented. |
| Missing `Width` parameter | **Low** | Spec: `Width` (string). Not implemented. |
| Missing child components | **High** | Spec expects `ButtonGroupButton` and `ButtonGroupToggleButton` child components with their own `OnClick`, `Selected`, `SelectedChanged`, `Class`, `Enabled` parameters. None exist. |
| Missing `OnClick` event on children | **High** | Spec: each child button fires `OnClick` (MouseEventArgs). Not implemented. |
| Missing `SelectedChanged` event on children | **High** | Spec: `ButtonGroupToggleButton` fires `SelectedChanged` (bool). Not implemented. |

## Code → Spec Gaps

None. The code is a strict subset of the spec.

## Recommended Changes

1. **[High]** Create `ButtonGroupButton` and `ButtonGroupToggleButton` child components.
2. **[High]** Add `SelectionMode` parameter to `SunfishButtonGroup`.
3. **[High]** Add `Enabled` parameter.
4. **[High]** Implement `OnClick` and `SelectedChanged` events on child components.

---

# 3. SunfishChip Gap Analysis

## Summary

The implementation is a thin wrapper rendering a `<span>` with label text and click handling. The spec defines a much richer component with removability, selectability, icon support, disabled state, and custom event args.

## Spec → Code Gaps

| Gap | Severity | Details |
|-----|----------|---------|
| Missing `Text` parameter | **Medium** | Spec uses `Text` (string). Code uses `Label` (string). Naming mismatch. |
| Missing `Disabled` parameter | **High** | Spec: `Disabled` (bool). Not implemented. |
| Missing `Removable` parameter | **High** | Spec: `Removable` (bool) renders a remove icon. Not implemented. |
| Missing `RemoveIcon` parameter | **Low** | Spec: `RemoveIcon` (object). Not implemented. |
| Missing `Selectable` parameter | **Medium** | Spec: `Selectable` (bool, default true). Controls whether chip can be selected. Not implemented. |
| Missing `Icon` parameter | **Medium** | Spec: `Icon` (object) for chip icon. Not implemented. |
| Missing `ChildContent` support | **Medium** | Spec allows arbitrary `ChildContent` inside chip. Code only renders `Label` text. |
| Missing `OnRemove` event | **High** | Spec: `OnRemove` fires with `ChipRemoveEventArgs` (has `Text`, `IsCancelled`). Not implemented. |
| Missing `SelectedChanged` event | **Medium** | Spec: `SelectedChanged` (bool) for two-way binding on `Selected`. Not implemented. |
| `OnClick` event args type | **Medium** | Spec: `OnClick` uses `ChipClickEventArgs` (has `Text` property). Code uses raw `MouseEventArgs`. |
| Missing `AriaLabel` parameter | **Low** | Spec: `AriaLabel` (string). Not implemented. |
| Missing `Id` parameter | **Low** | Spec: `Id` (string). Not implemented. |
| Missing `TabIndex` parameter | **Low** | Spec: `TabIndex` (int). Not implemented. |

## Code → Spec Gaps

| Gap | Severity | Details |
|-----|----------|---------|
| `Variant` parameter | **Low** | Code defines `ChipVariant Variant` (default Default). Spec describes appearance options but uses `ThemeColor`/`FillMode` rather than a `Variant` enum. Naming divergence. |

## Recommended Changes

1. **[High]** Add `Disabled`, `Removable` parameters and `OnRemove` event with `ChipRemoveEventArgs`.
2. **[Medium]** Rename `Label` to `Text` to match spec, or add `Text` as an alias.
3. **[Medium]** Add `SelectedChanged` event callback for two-way binding.
4. **[Medium]** Add `Icon`, `Selectable`, `ChildContent` parameters.
5. **[Medium]** Change `OnClick` to use `ChipClickEventArgs` instead of `MouseEventArgs`.

---

# 4. SunfishChipSet Gap Analysis

Spec name: **ChipList** (`SunfishChipList`). Code name: **SunfishChipSet**.

## Summary

The code is a bare container (`<div>` with `role="listbox"` and `ChildContent`). The spec describes a data-driven component (`SunfishChipList<TItem>`) with data binding, selection modes, removability, and rich events.

## Spec → Code Gaps

| Gap | Severity | Details |
|-----|----------|---------|
| Component naming mismatch | **High** | Spec: `SunfishChipList` (generic `<TItem>`). Code: `SunfishChipSet` (non-generic). |
| Missing `Data` parameter | **High** | Spec: `Data` (IEnumerable<TItem>) for data-bound chip rendering. Not implemented. |
| Missing `TextField` / field mapping params | **High** | Spec: `TextField`, `IconField` etc. for binding model properties. Not implemented. |
| Missing `SelectionMode` parameter | **High** | Spec: `ChipListSelectionMode` (Single/Multiple). Not implemented. |
| Missing `SelectedItems` / `SelectedItemsChanged` | **High** | Spec: `SelectedItems` (IEnumerable<TItem>) + `SelectedItemsChanged` event. Not implemented. |
| Missing `Removable` parameter | **Medium** | Spec: `Removable` (bool). Not implemented. |
| Missing `RemoveIcon` parameter | **Low** | Spec: `RemoveIcon` (object). Not implemented. |
| Missing `OnRemove` event | **High** | Spec: `OnRemove` fires `ChipListRemoveEventArgs`. Not implemented. |
| Missing `AriaLabel` / `AriaLabelledBy` | **Low** | Spec defines both. Not implemented. |
| Missing `ItemTemplate` support | **Medium** | Spec references templates for custom chip rendering. Not implemented. |

## Code → Spec Gaps

None. The code is a strict subset.

## Recommended Changes

1. **[High]** Rename to `SunfishChipList` and make it generic (`SunfishChipList<TItem>`).
2. **[High]** Implement data binding (`Data`, `TextField`, `IconField`).
3. **[High]** Implement selection (`SelectionMode`, `SelectedItems`, `SelectedItemsChanged`).
4. **[High]** Implement removal (`Removable`, `OnRemove` with `ChipListRemoveEventArgs`).

---

# 5. SunfishIconButton Gap Analysis

No standalone spec (documented as a variant of Button).

## Summary

A simple icon-only button with `Size`, `Icon` (string), `Disabled`, and `OnClick`. Reasonable minimal implementation for a button variant.

## Observations

| Item | Severity | Details |
|------|----------|---------|
| `Icon` is `string` | **Low** | Main Button spec uses `object` for Icon. IconButton uses `string` passed to `IconProvider.GetIcon()`. Consider aligning types across button variants. |
| No `Enabled` / `Visible` / `Title` params | **Low** | Consistent with the gaps in `SunfishButton`. Should be addressed together. |
| No `FocusAsync()` method | **Low** | Same gap as SunfishButton. |

## Recommended Changes

1. **[Low]** Align `Icon` type with the convention used in `SunfishButton` or vice versa.
2. **[Low]** Add `Enabled`/`Title` parameters when SunfishButton is updated.

---

# 6. SunfishFab Gap Analysis

## Summary

The code implements a basic FAB with `Size`, `Icon`, and `OnClick`. The spec defines a much richer component with positioning, alignment, offsets, theming, enabled state, and rounded corners.

## Spec → Code Gaps

| Gap | Severity | Details |
|-----|----------|---------|
| Missing `Enabled` parameter | **High** | Spec: `Enabled` (bool, default true). Not implemented; button cannot be disabled. |
| Missing `PositionMode` parameter | **High** | Spec: `FloatingActionButtonPositionMode` enum (default Fixed). Core FAB behavior. Not implemented. |
| Missing `HorizontalAlign` parameter | **High** | Spec: `FloatingActionButtonHorizontalAlign` (default End). Not implemented. |
| Missing `VerticalAlign` parameter | **High** | Spec: `FloatingActionButtonVerticalAlign` (default Bottom). Not implemented. |
| Missing `HorizontalOffset` parameter | **Medium** | Spec: string (default "16px"). Not implemented. |
| Missing `VerticalOffset` parameter | **Medium** | Spec: string (default "16px"). Not implemented. |
| Missing `ThemeColor` parameter | **Medium** | Spec: string (default "primary"). Not implemented. |
| Missing `Rounded` parameter | **Medium** | Spec: string (default "full"). Not implemented. |
| Missing `Id` parameter | **Low** | Spec: `Id` (string). Not implemented. |
| Missing `Title` parameter | **Low** | Spec: `Title` (string). Not implemented. |
| `Size` type mismatch | **Medium** | Spec: `Size` is `string` (e.g. "md", "lg"). Code: `FabSize` enum. Different API shape. |
| `Icon` type mismatch | **Medium** | Spec: `Icon` is `object`. Code: `string`. |
| Component name mismatch | **Medium** | Spec: `SunfishFloatingActionButton`. Code: `SunfishFab`. Users following docs will not find the component. |

## Code → Spec Gaps

None.

## Recommended Changes

1. **[High]** Add positioning parameters (`PositionMode`, `HorizontalAlign`, `VerticalAlign`) -- core to FAB identity.
2. **[High]** Add `Enabled`/disabled support.
3. **[Medium]** Rename to `SunfishFloatingActionButton` or add a type alias.
4. **[Medium]** Add `ThemeColor`, `Rounded`, offset parameters.
5. **[Medium]** Align `Size` and `Icon` types with spec conventions.

---

# 7. SunfishSegmentedControl Gap Analysis

No direct spec. Appears to be a custom component combining ButtonGroup + ToggleButton semantics.

## Summary

Renders a `<div role="radiogroup">` with `<button role="radio">` items from a `List<string>`. Supports single selection via `Value`/`ValueChanged` two-way binding. This is a reasonable composite but has no spec coverage.

## Observations

| Item | Severity | Details |
|------|----------|---------|
| No spec documentation | **Medium** | Component exists only in code. Should be documented or folded into ButtonGroup with `SelectionMode.Single`. |
| `Items` is `List<string>` only | **Medium** | No support for complex models, icons, or disabled individual items. |
| No `Disabled` / `Enabled` parameter | **Medium** | Cannot disable the entire control or individual segments. |
| Reuses `ButtonGroupClass()` CSS | **Low** | Shares CSS with ButtonGroup, which may cause styling conflicts. |

## Recommended Changes

1. **[Medium]** Decide whether this should be a standalone component or merged into ButtonGroup (the spec's `SelectionMode.Single` covers this use case).
2. **[Medium]** If kept, add spec documentation and support for disabled state and complex item models.

---

# 8. SunfishSplitButton Gap Analysis

## Summary

The code implements a basic split button with primary click, chevron dropdown toggle, and a menu content area. The spec describes a richer component with structured child components, popup settings, item-level configuration, and accessibility features.

## Spec → Code Gaps

| Gap | Severity | Details |
|-----|----------|---------|
| Missing structured child components | **High** | Spec: `<SplitButtonContent>`, `<SplitButtonItems>`, `<SplitButtonItem>` child components. Code uses flat `ChildContent` + `MenuContent` RenderFragments. |
| Missing `Enabled` parameter | **High** | Spec: `Enabled` (bool, default true). Not implemented. |
| Missing `AriaLabel` parameter | **Medium** | Spec: `AriaLabel` (string). Not implemented. |
| Missing `Id` parameter | **Low** | Spec: `Id` (string) on primary button. Not implemented. |
| Missing `TabIndex` parameter | **Low** | Spec: `TabIndex` (int). Not implemented. |
| Missing `Title` parameter | **Low** | Spec: `Title` (string). Not implemented. |
| Missing popup settings | **Medium** | Spec: `<SplitButtonPopupSettings>` with `Height`, `Width`, `MaxHeight`, `MinHeight`, `AnimationDuration`, etc. Not implemented. |
| Missing item-level `Enabled` | **Medium** | Spec: each `SplitButtonItem` has `Enabled` (bool) and `Class`. Not implemented. |
| Missing `FocusAsync()` method | **Medium** | Spec documents a `FocusAsync` method. Not implemented. |
| Menu not closed on outside click | **Medium** | Code toggles `_menuOpen` only via button click. No click-outside-to-close logic. |
| Primary button missing `disabled` attr | **Medium** | The primary `<button>` does not render a `disabled` attribute (unlike IconButton/Button). |

## Code → Spec Gaps

None.

## Recommended Changes

1. **[High]** Add `Enabled` parameter and wire `disabled` attribute on both buttons.
2. **[High]** Consider implementing `SplitButtonContent`/`SplitButtonItems`/`SplitButtonItem` child components for spec-compliant API.
3. **[Medium]** Add click-outside-to-close behavior for the dropdown.
4. **[Medium]** Implement popup configuration settings.
5. **[Medium]** Implement `FocusAsync()`.

---

# 9. SunfishToggleButton Gap Analysis

## Summary

The implementation covers the core toggle behavior (Selected/SelectedChanged, OnClick, Enabled, ChildContent, aria-pressed). Several spec parameters are missing but the fundamental interaction model is correct.

## Spec → Code Gaps

| Gap | Severity | Details |
|-----|----------|---------|
| Missing `AriaLabel` parameter | **Low** | Spec: `AriaLabel` (string). Not implemented. |
| Missing `Id` parameter | **Low** | Spec: `Id` (string). Not implemented. |
| Missing `TabIndex` parameter | **Low** | Spec: `TabIndex` (int). Not implemented. |
| Missing `Title` parameter | **Low** | Spec: `Title` (string). Not implemented. |
| Missing appearance params | **Medium** | Spec mentions appearance settings (ThemeColor, FillMode, Rounded, Size) in the appearance article. Code has none of these. |
| Missing `Icon` parameter | **Medium** | Spec references icon support. Not implemented. |

## Code → Spec Gaps

| Gap | Severity | Details |
|-----|----------|---------|
| `Enabled` vs spec convention | **Low** | Code uses `Enabled` (bool, default true) which matches spec. Good alignment. No gap here. |

## Recommended Changes

1. **[Medium]** Add appearance parameters (`ThemeColor`, `FillMode`, `Rounded`, `Size`) to match the button appearance system.
2. **[Medium]** Add `Icon` parameter.
3. **[Low]** Add `AriaLabel`, `Id`, `TabIndex`, `Title` parameters.

---

# Cross-Cutting Gaps Summary

| Theme | Severity | Affected Components |
|-------|----------|---------------------|
| Missing `Enabled` parameter (spec standard) | **High** | Button (uses `Disabled` instead), ButtonGroup, Fab, SplitButton |
| Missing `Id` / `Title` / `TabIndex` params | **Low** | All components |
| Missing `FocusAsync()` method | **Medium** | Button, SplitButton |
| `Icon` type inconsistency | **Medium** | Button (`RenderFragment?`), IconButton (`string`), Fab (`string`), Spec (`object`) -- three different approaches |
| Component naming mismatches | **Medium** | `SunfishFab` vs spec `SunfishFloatingActionButton`; `SunfishChipSet` vs spec `SunfishChipList` |
| Child component architecture gaps | **High** | ButtonGroup (missing toggle/regular button children), SplitButton (missing structured items) |
| No `Visible` parameter pattern | **Low** | All components lack conditional visibility support defined in specs |
