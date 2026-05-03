# Gap Analysis Part 2 -- Navigation Components

## 1. SunfishPagination.razor vs Pager Spec

**Spec name:** `SunfishPager` | **Our name:** `SunfishPagination`

- **Missing `Total` + `PageSize` model:** Spec uses `Total` (item count) and `PageSize` to compute pages automatically; we require the caller to pre-compute `TotalPages` manually.
- **Missing `Page` parameter:** Spec uses `Page` (1-based, two-way bindable); we use `CurrentPage` / `CurrentPageChanged` -- naming diverges from spec.
- **Missing `PageSizes` dropdown:** Spec provides a `PageSizes` list that renders a DropDownList for the user to change page size; we have no page-size selector at all.
- **Missing `ButtonCount`:** Spec has `ButtonCount` to control visible page buttons; we have `MaxVisiblePages` (same idea, different name).
- **Missing features:** `InputType` (numeric input mode), `Responsive` (adaptive hiding of elements), `ShowInfo` (page info text), `AdaptiveMode`, `Size` parameter -- none implemented.

## 2. SunfishTimeRangeSelector.razor -- No Spec

Custom/internal component with no matching spec.

- Hardcoded range options (`Now`, `1h`, `24h`, `7d`, `30d`) -- should accept a `Ranges` parameter for customization.
- Active-state CSS class `marilo-time-range--active` is hardcoded inline instead of going through `CssProvider`.
- Two callbacks (`SelectedRangeChanged` + `OnRangeChanged`) fire on every selection -- redundant; one is two-way binding, the other is an event. Consider consolidating.

## 3. SunfishToolbar.razor vs Toolbar Spec

- **Missing `OverflowMode`:** Spec supports `ToolBarOverflowMode.Menu` to show overflowing items in a popup; we have no overflow handling.
- **Missing `Size` parameter:** Spec provides `Size` for layout scaling; not implemented.
- **Missing scroll adaptive mode:** Spec has `ScrollButtonsPosition` and `ScrollButtonsVisibility`; not implemented.
- **Child component naming diverges:** Spec uses `ToolBarButton`, `ToolBarToggleButton`, `ToolBarButtonGroup`, `ToolBarSeparator`; we use `SunfishToolbarButton`, `SunfishToolbarGroup`, `SunfishToolbarSeparator`, `SunfishToolbarToggleButton`.

## 4. SunfishToolbarButton.razor (part of Toolbar)

- **Missing `Icon` parameter:** Spec toolbar buttons accept an `Icon` (`ISvgIcon`); we have no icon support.
- **Missing `Overflow` parameter:** Spec supports `ToolBarItemOverflow` (`Auto`, `Always`, `Never`); not implemented.
- **Missing `OverflowText`:** Spec allows different text in overflow popup; not implemented.

## 5. SunfishToolbarGroup.razor (part of Toolbar)

- **Missing `SelectionMode`:** Spec `ToolBarButtonGroup` supports `ButtonGroupSelectionMode.Single` / `Multiple` for toggle behavior; we have no selection logic.
- Current implementation is a simple `div` wrapper with no interactive behavior.

## 6. SunfishToolbarSeparator.razor (part of Toolbar)

- Minimal component with `role="separator"` -- functionally adequate.
- **Missing spacer variant:** Spec mentions both separators and spacers (flexible space); we only have separators.

## 7. SunfishToolbarToggleButton.razor (part of Toolbar)

- **Missing `Icon` parameter:** Same gap as `SunfishToolbarButton`.
- **Parameter naming:** We use `IsActive` / `IsActiveChanged`; spec uses `Selected` / `@bind-Selected`. Should align.
- **Missing `Overflow` and `OverflowText`:** Not implemented.

## 8. SunfishTreeItem.razor (part of TreeView)

- ~~**Checkbox uses `GetItemId()` returning `Title`:** Fragile -- items with duplicate titles will collide. Spec uses a proper `Id` field.~~ ✅ RESOLVED — `Id` parameter added
- ~~**Missing `Icon` support:** Spec data model supports per-item icons (`ISvgIcon`); our tree item has no icon rendering.~~ ✅ RESOLVED — `Icon` parameter + `IconProvider` rendering added
- ~~**Missing link/navigation support:** Spec supports URL navigation per node; not implemented.~~ ✅ RESOLVED — `Url` parameter renders `<a>` tag

## 9. SunfishTreeView.razor vs TreeView Spec

### Original Gaps (All Implemented)

- ~~**Missing `ExpandedItems` two-way binding:** Spec binds `@bind-ExpandedItems` to `IEnumerable<object>`; we use internal `HashSet<string> _expandedIds` with no external binding.~~ ✅ IMPLEMENTED
- ~~**Missing `SelectedItems` binding:** Spec provides selection via `SelectedItems`; our component has no selection state exposed.~~ ✅ IMPLEMENTED
- ~~**Missing `Size` parameter:** Spec supports `"sm"`, `"md"`, `"lg"`; not implemented.~~ ✅ IMPLEMENTED
- ~~**Missing drag-and-drop:** Spec supports full drag-and-drop between trees with `OnDrag`, `OnDrop`, `DragThrottleInterval`; not implemented.~~ ✅ IMPLEMENTED (basic HTML5 DnD)
- ~~**Missing `Rebind` method and `@ref` support:** Spec exposes methods like `Rebind()` and `GetItemFromDropIndex()`; we have no public API surface.~~ ✅ IMPLEMENTED

### New Gaps from Source Review (16 additional gaps)

> Sources reviewed: Radzen Tree, MudBlazor TreeView, BlazorVirtualTreeView, Fancytree, excubo-ag Blazor.TreeViews, Fluent UI Blazor TreeView, jsTree, Syncfusion TreeView, Telerik TreeView. Full analysis in `resolution/RESOLUTION_STATUS.md` and `resolution/IMPLEMENTATION_NOTES.md`.

#### Phase 1 — Core Behavioral (5 gaps)

- **Missing tri-state checkbox propagation (Gap 7):** No parent-child checkbox cascading. Radzen uses `AllowCheckChildren`/`AllowCheckParents` booleans with recursive `GetAllChildValues()` and `bool?` return for indeterminate state. Current `CheckBoxMode` enum needs a `Multiple` value; checking logic needs hierarchical walk.
- **Missing `CheckedItems` two-way binding (Gap 8):** `_checkedIds` is internal-only. Consumers cannot observe or set checked state. Should mirror existing `ExpandedItems`/`SelectedItems` pattern with `CheckedItems`/`CheckedItemsChanged`.
- **Missing multi-selection mode (Gap 9):** `SelectItem()` always clears `_selectedIds` (single-select hardcoded). MudBlazor provides `SelectionMode` enum (`Single`/`Multiple`/`Toggle`). MudBlazor issue #9411 confirms this must be independent of checkbox state.
- **Missing lazy loading callback (Gap 10):** `HasChildrenField` and loading indicator exist but no `LoadChildrenAsync` callback. Fluent UI Blazor uses `LazyLoadItems`; MudBlazor uses `ServerData`; BlazorVirtualTreeView uses `LoadChildren`. MudBlazor discussion #4906 shows 4500-node trees fail without proper lazy loading.
- **Missing keyboard navigation (Gap 11):** No keyboard support at all. Fancytree and WAI-ARIA TreeView pattern define: ArrowUp/Down for traversal, Left/Right for collapse/expand, Enter/Space for activation, Home/End for boundaries. Tree should be single tab-stop with internal arrow key navigation.

#### Phase 2 — Enhanced Interaction (6 gaps)

- **Missing `ExpandOnClick` / `ExpandOnDoubleClick` (Gap 12):** Only toggle button expands nodes. MudBlazor supports clicking anywhere on the item header to expand.
- **Missing `SingleExpand` accordion mode (Gap 13):** No auto-collapse of siblings. Radzen's `SingleExpand` boolean collapses sibling items when one expands.
- **Missing `AutoExpand` for programmatic selection (Gap 14):** Setting `SelectedItems` externally doesn't expand ancestors. MudBlazor's `AutoExpand` automatically reveals selected children.
- **Missing `ExpandAllAsync` / `CollapseAllAsync` (Gap 15):** Only `Rebind()` is exposed. MudBlazor and BlazorVirtualTreeView both offer batch expand/collapse methods.
- **Missing `FilterFunc` / search (Gap 16):** No filtering. MudBlazor provides async predicate filtering. Fancytree/jsTree support search with ancestor-chain visibility preservation.
- **Missing `Disabled` / `ReadOnly` (Gap 17):** No way to disable interaction. MudBlazor provides both parameters with distinct semantics (disabled = no render interaction; readonly = shows state, prevents changes).

#### Phase 3 — Advanced / Enterprise (5 gaps)

- **Missing virtualization (Gap 18):** All nodes render to DOM. BlazorVirtualTreeView proves flatten-and-virtualize pattern works with Blazor `<Virtualize>`. Critical for 1000+ node trees.
- **Missing programmatic navigation `SelectNodeAsync` (Gap 19):** No way to programmatically expand ancestors and scroll to a specific node. BlazorVirtualTreeView exposes this.
- **Missing `OnItemContextMenu` event (Gap 20):** No right-click integration. Radzen, jsTree, and Fancytree all support context menu events per node.
- **Missing `CheckboxTemplate` (Gap 21):** Checkbox rendering is hardcoded `<input type="checkbox">`. excubo-ag's `CheckboxTemplate` delegate pattern allows custom checkbox components.
- **Missing inline node editing (Gap 22):** No inline rename. jsTree, Fancytree, and Syncfusion support double-click or F2 to edit node text in-place.
