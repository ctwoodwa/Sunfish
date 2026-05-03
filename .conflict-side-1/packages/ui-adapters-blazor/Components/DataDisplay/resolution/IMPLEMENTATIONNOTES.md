# SunfishGantt Implementation Notes

## Design Decisions (Pass 2: E1–E8)

### E1 — Milestone Rendering
- A milestone is a task where `GetStart(item) == GetEnd(item)` (same DateTime).
- Rendered as `mar-gantt__milestone` with inner `mar-gantt__milestone-diamond` span (◆ Unicode).
- Positioned absolutely using `GetPixelOffset(taskStart)` and `top: rowIndex * RowHeight + RowHeight/2 - 6`.
- Applied in both view-engine and legacy DayWidth timeline loops.

### E2 — Summary Task Auto-Calculation
- Summary task = any node with `Children.Count > 0`.
- `ComputeSummaryValues()` runs bottom-up (`OrderByDescending` Depth) after `BuildTree()`.
- Sets `ComputedStart = min children start`, `ComputedEnd = max children end`, `ComputedPercentComplete = duration-weighted average`.
- Razor reads `node.ComputedStart ?? taskStart` for bar positioning and adds `mar-gantt__bar--summary` CSS modifier.

### E3 — GanttState Phase 2
- `GanttState<TItem>` includes: `EditItem`, `OriginalEditItem`, `InsertedItem`, `EditField`, `ParentItem`.
- `OnStateInit`/`OnStateChanged` `EventCallback<GanttStateEventArgs<TItem>>` wired in `OnParametersSetAsync`.
- `FireStateChanged(string propertyName)` method fires after sort, filter, expand, and view changes.

### E4 — Hierarchical Data Binding
- `ItemsField` parameter enables hierarchical mode (children embedded in parent items).
- `HasChildrenField` parameter enables lazy-loading indicators.
- `BuildTreeHierarchical` uses `CreateNodeRecursive` for recursive child resolution.
- When `ItemsField` is set, `ParentIdField` is ignored.

### E5 — In-cell Edit Mode
- `GanttTreeListEditMode.Incell` enables click-to-edit on individual cells.
- `_editingRowIndex` + `_editingField` track active cell.
- `BeginCellEdit(rowIdx, field)` populates `_editValues` from current item.
- `HandleCellKeyDown` handles Tab (next cell), Enter (commit), Escape (cancel).
- Tab advances through editable columns left-to-right, wrapping to next row.

### E6 — Filter Menu
- `GanttFilterMode.FilterMenu` renders funnel button per column header.
- `_filterMenuField` tracks which column's menu is open (null = closed).
- `_filterMenuValue` holds pending input before Apply.
- `ApplyFilterMenu()` writes to `_filterValues` dict and rebuilds flat visible.
- `ClearFilterMenu()` removes the filter for that column.

### E7 — Gantt SCSS
- Both FluentUI and Bootstrap providers emit `mar-gantt__*` structural classes.
- SCSS includes `prefers-reduced-motion` and `forced-colors` media queries.

### E8 — Spec Updates
- All spec files under `docs/component-specs/gantt/` updated to reflect E1–E7 implementations.

## Deferred Gaps (from gap analysis)

### JS-Dependent (blocked on shared infrastructure)
- **Column reorder**: Requires drag interop (marilo-drag.ts)
- **Column resize**: Requires drag interop (marilo-drag.ts)
- **Timeline bar drag-move**: Requires drag interop (marilo-drag.ts)
- **Timeline bar resize**: Requires drag interop (marilo-drag.ts)
- **Drag-specific screen reader announcements**: Requires drag infrastructure

### Component Model Gaps — RESOLVED in Pass 3
- ~~**OriginalEditItem clone**~~: E9 — IGanttCloneable + JSON fallback
- ~~**GanttDependencies component**~~: E10 — component model stub (no SVG)
- ~~**Screen reader announcements**~~: E11 — aria-live region for non-drag actions
- ~~**Filter checkbox list**~~: E12 — Drawer-hosted + E16 SunfishPopup option

### Popup/Overlay Gaps — RESOLVED in Pass 3
- ~~**Popup edit mode**~~: E15 — GanttTreeListEditMode.Popup using SunfishPopup
- ~~**Filter popup anchoring**~~: E16 — GanttFilterPopupMode.Popup using SunfishPopup
- ~~**Column chooser**~~: E17 — ShowColumnChooser + SunfishPopup panel

## Design Decisions (Pass 3: E9–E17)

### E9 — OriginalEditItem Clone
- `IGanttCloneable<T>` opt-in interface in same namespace as GanttState.
- `GanttCloneHelper.DeepClone<TItem>()` uses interface if available, JSON roundtrip otherwise.
- Clone stored in `_originalItem` field during BeginEdit/BeginCellEdit, cleared on commit/cancel.
- No constructor constraint on TItem — keeps `where TItem : class` unchanged.

### E10 — GanttDependencies Component Model
- `GanttDependency` record: Id, PredecessorId, SuccessorId, Type (GanttDependencyType enum).
- `SunfishGanttDependencies<TItem>` child component using CascadingParameter to find parent.
- Registered via `RegisterDependencies`/`UnregisterDependencies` (same pattern as columns/views).
- `GanttDependenciesSlot` RenderFragment parameter for declaration.
- Existing `DependsOnField` SVG rendering preserved (backward compat).

### E11 — Screen Reader Announcements
- `_announcement` string field + `Announce(message)` synchronous helper.
- `mar-gantt__announcer` div with `aria-live="polite"`, visually hidden via sr-only CSS.
- Wired into: ToggleExpanded, HandleKeyDown (row focus), BeginCellEdit, CommitEdit, CancelEdit.
- Synchronous setter avoids invalidating bUnit event handler IDs mid-dispatch.

### E12 — Filter Checkbox List
- `GanttColumnFilterType` enum (Text/CheckboxList) on GanttColumn.
- `OpenCheckboxFilter(field)` collects distinct values, pre-checks all.
- Pipe-delimited storage in `_filterValues` dict for checkbox mode.
- `NodeMatchesAllFilters` updated to split on `|` for set-membership matching.

### E13 — marilo-drag CDW Workspace
- Design-only: workspace scaffold at ICM/workspaces/marilo-drag/.
- API design doc specifies TypeScript API (initDrag/disposeDrag) + .NET IJSInteropDragService.
- Consumer contracts for Gantt (5 drag scenarios), Window, Splitter, DataGrid.

### E14 — SunfishPopup Primitive
- Stub implementation: absolute positioning, no JS scroll tracking.
- Inline JS eval for outside-click detection (same pattern as SunfishWindow).
- Parameters: IsOpen, AnchorId, Placement, Offset, FocusTrap, CloseOnEscape, OnOutsideClick.
- PopupPlacement enum: Top, Bottom, Left, Right, Auto.

### E15 — Popup Edit Mode
- `GanttTreeListEditMode.Popup` added to enum.
- `BeginPopupEdit(rowIdx, field)` opens SunfishPopup with form for all editable columns.
- Cells get `id="mar-gantt-cell-{rowIdx}-{field}"` for anchor positioning.
- CommitPopupEdit/CancelPopupEdit delegate to existing commit/cancel logic.

### E16 — Filter Popup Mode
- `GanttFilterPopupMode` enum (Drawer/Popup) with parameter.
- Drawer remains default for accessibility.
- Popup mode uses SunfishPopup anchored to filter button (id="mar-gantt-filter-{field}").

### E17 — Column Chooser
- `ShowColumnChooser` parameter shows hamburger button in toolbar.
- Popup contains checkbox per column, toggling `Visible` property.
- `VisibleColumns` added to GanttState for state persistence.
- GetState/SetStateAsync wired for column visibility.

## Design Decisions (Pass 4: Stage 05 — S05-A through S05-D)

### S05-A — InsertedItem/ParentItem State Wiring
- Added `_insertedItem` and `_parentItem` private fields to SunfishGantt.
- `GetState()` now includes `InsertedItem` and `ParentItem` in the snapshot.
- `SetStateAsync()` applies InsertedItem/ParentItem from incoming state.
- `HandleCommandAdd()` sets `_insertedItem` transiently during `OnCreate` callback (cleared after).
- `_parentItem` is set to null for root-level adds; consumers can set via `SetStateAsync` for child-level inserts.
- Limitation: InsertedItem/ParentItem are transient — the Gantt does not manage data mutations itself. Consumers handle data via event callbacks.

### S05-B — GanttDependencies Field Mapping
- Added `IdField`, `PredecessorIdField`, `SuccessorIdField`, `TypeField` parameters to `SunfishGanttDependencies`.
- Default values match `GanttDependency` record properties for zero-config with strongly typed data.
- Field mapping parameters enable future use with arbitrary model types without requiring `GanttDependency` records.
- Added convenience properties to `GanttDependencyCreateEventArgs` (PredecessorId, SuccessorId, Type) matching spec examples.
- Added `Item` convenience property to `GanttDependencyDeleteEventArgs` for consistent access pattern.
- `GetDependencies()` internal method added for parent Gantt to consume dependency data.

### S05-C — Accessibility Announcements (Test Coverage)
- All non-drag announcement points already implemented in Pass 3 (E11).
- Added 4 tests validating: CommitEdit announcement, keyboard navigation announcement, skip-link rendering, empty initial announcer.

### S05-D — Filter Checkbox List (Test Coverage)
- Checkbox filter implementation already complete from Pass 3 (E12 + E16).
- Added 3 tests validating: GetState reflects applied filter, select-all equals no filter, default FilterType is Text.
