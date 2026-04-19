# SunfishDataGrid — Phased Gap Analysis & Implementation Tracker

Generated: 2026-03-30 | Last Updated: 2026-04-01
Spec Source: `/docs/component-specs/grid/` (78 files)
Research: `resolution/RESEARCH_LOG.md` — patterns from Radzen, QuickGrid, MudBlazor, Tabulator, AG Grid

---

## Current Implementation Status

**Estimated API Coverage: ~55–60%**

### Completed (Pass 1 — 2026-03-31)
- Public GridState with OnStateInit/OnStateChanged
- OnRead server-side data binding
- Height/Width parameters
- Row events (OnRowClick, OnRowDoubleClick, OnRowContextMenu, OnRowRender)
- Bindable Page parameter, PageChanged, PageSizeChanged, PageSizes dropdown
- Checkbox selection column, multi-select
- Column: Visible, EditorTemplate, FooterTemplate, OnCellRender
- Rebind() public method
- Navigable parameter (placeholder — no keyboard logic yet)

### Completed (Pass 2 — 2026-03-31)
- Three edit modes: Inline, InCell (double-click), Popup
- All CRUD events: OnAdd, OnCreate, OnUpdate, OnDelete, OnEdit, OnCancel, OnModelInit, OnCommand
- SunfishGridCommandButton component (Add/Edit/Save/Cancel/Delete + custom commands)
- GridCommandTypes: GridCommandDefinition, GridCommandPlacement enum
- DetailTemplate with OnRowExpand/OnRowCollapse
- FilterMenu mode with operator dropdown (11 operators) and Apply/Clear
- Extended filter operators: all 11 FilterOperator values with IComparable type-aware comparison
- Multi-sort with Ctrl+Click
- IsLoading overlay
- EnableVirtualization / VirtualizeOverscanCount
- Footer row (`<tfoot>`) with FooterTemplate rendering
- Partial file architecture (Rendering.cs, Data.cs, Editing.cs)

### Architecture
```
SunfishDataGrid.razor           — Markup (~240 lines)
SunfishDataGrid.razor.cs        — Parameters, state, lifecycle (~300 lines)
SunfishDataGrid.Rendering.cs    — RenderTreeBuilder: rows, cells, filter menu (~360 lines)
SunfishDataGrid.Data.cs         — Filter/sort/page pipeline, event handlers (~430 lines)
SunfishDataGrid.Editing.cs      — CRUD operations, detail expansion (~130 lines)
SunfishGridColumn.razor         — Column definition component
SunfishGridCommandButton.razor  — Command button component
SunfishGridToolbar.razor        — Toolbar container
GridState.cs                   — State persistence model
GridCommandTypes.cs            — Command/edit event arg types
GridEventArgs.cs               — Row/cell/read event arg types
```

---

## Phase A — Pure C# Features (No JS Interop)

*Estimated effort: Medium. All tasks are self-contained Blazor/C# work.*

### A1: Grouping
Spec: `grouping/overview.md`, `grouping/aggregates.md`, `grouping/load-on-demand.md`
Reference: RESEARCH_LOG.md §1 — Radzen `GroupByMany` recursive tree, collapse-by-exception pattern

- [ ] A1.1 — Add `Groupable` parameter to `SunfishGridColumn` (default: true)
- [ ] A1.2 — Create `GroupResult<TItem>` type (Key, Count, Items, Subgroups) for recursive group tree
- [ ] A1.3 — Implement `GroupByMany` recursive grouping in `ProcessDataClientSide()` using `GroupDescriptors`
- [ ] A1.4 — Render group header rows with collapse/expand toggle (full-width `<tr>` with colspan)
- [ ] A1.5 — Track collapsed groups via **collapse-by-exception**: `HashSet<string>` of collapsed group key paths (all default expanded)
- [ ] A1.6 — Add `GroupHeaderTemplate` RenderFragment<GroupHeaderContext<TItem>> to grid
- [ ] A1.7 — Add `GroupFooterTemplate` RenderFragment<GroupFooterContext<TItem>> to grid
- [ ] A1.8 — Implement aggregate functions (Count, Sum, Average, Min, Max) for group footers; recompute only affected branch on data change
- [ ] A1.9 — Add `CollapsedGroups` to `GridState` for state persistence (keyed by path string, not index)
- [ ] A1.10 — Include `GroupDescriptors` in `GridReadEventArgs` so OnRead consumers can group server-side (avoid MudBlazor #9295 pitfall)
- [ ] A1.11 — Support `HideGroupedColumn` bool parameter
- [ ] A1.12 — Filter leaf rows first, then hide empty groups (AG Grid pattern)

### A2: AutoGenerateColumns
Spec: `columns/auto-generated.md`

- [x] A2.1 — Add `AutoGenerateColumns` bool parameter (default: false)
- [x] A2.2 — On init, reflect `typeof(TItem)` public properties to auto-create column definitions
- [x] A2.3 — Respect `[Display]` and `[Editable]` attributes for Title/Editable
- [x] A2.4 — Allow explicit `SunfishGridColumn` children to override auto-generated columns
- [x] A2.5 — Skip navigation properties and complex types

### A3: SearchBox Filter
Spec: `filtering/searchbox.md`
Reference: RESEARCH_LOG.md §8 — MudBlazor programmatic filter API

- [x] A3.1 — Add `ShowSearchBox` bool parameter
- [x] A3.2 — Render search input in toolbar area
- [x] A3.3 — Implement global text search across all string-type visible columns
- [x] A3.4 — Add `SearchFilter` string to `GridState` for persistence
- [x] A3.5 — Debounce input (configurable `SearchDelay` parameter, default 300ms)
- [x] A3.6 — Expose `AddFilter(FilterDescriptor)` / `ClearFilters()` public methods on grid for programmatic filter control

### A4: Additional Templates
Spec: `templates/no-data.md`, `templates/row.md`

- [x] A4.1 — Add `NoDataTemplate` RenderFragment parameter
- [x] A4.2 — Render `NoDataTemplate` when `_displayedItems` is empty (instead of empty table body)
- [x] A4.3 — Add `RowTemplate` RenderFragment<TItem> parameter for full custom row rendering
- [x] A4.4 — When `RowTemplate` is set, bypass default cell rendering and use the template

### A5: Column Enhancements
Spec: `columns/bound.md`, `columns/frozen.md`, `columns/visible.md`

- [x] A5.1 — Add `Editable` bool parameter to `SunfishGridColumn` (default: true)
- [x] A5.2 — Respect `Editable` in Rendering.cs — skip EditorTemplate for non-editable columns
- [x] A5.3 — Add `HeaderClass` string parameter to `SunfishGridColumn`
- [x] A5.4 — Add `Id` string parameter to `SunfishGridColumn`
- [x] A5.5 — Add `ShowColumnMenu` bool parameter to `SunfishGridColumn` (default: false)
- [x] A5.6 — Add `VisibleInColumnChooser` bool parameter (default: true)

### A6: GridState Enrichment
Spec: `state.md`
Reference: RESEARCH_LOG.md §1 (group state), §7 (QuickGrid decoupled pagination)
Note: Radzen uses a `DataGridSettings` object with `SaveSettings()`/`LoadSettings()` for full state round-tripping. Sunfish uses event-based `OnStateChanged` instead; a `SaveSettings()`/`LoadSettings()` helper is deferred as a potential A6.7+ follow-up task (not implemented here — event-based approach is sufficient for round-tripping).
Note (A6.5): `ColumnState` is named `GridColumnState` in code (same namespace); uses `Order` instead of `OrderIndex` — contract-compatible superset.
Note (A6.1–A6.3): `GridState` remains non-generic (`object?` typed) to avoid breaking consumer contracts. `TItem`-typed round-tripping is achievable via cast at the consumer call site.
Note (A6.9/A6.10): Editing operations (`BeginEdit`, `BeginCellEdit`, `BeginAdd`, `SaveEdit`, `CancelEdit`, `DeleteItem`) now call `NotifyStateChanged` so `OnStateChanged` fires with the enriched state. `ToggleDetailRow` already did this.

- [x] A6.1 — Add `EditItem` property to GridState
- [x] A6.2 — Add `OriginalEditItem` property to GridState
- [x] A6.3 — Add `InsertedItem` property to GridState
- [x] A6.4 — Add `ExpandedItems` HashSet to GridState
- [x] A6.5 — Add `ColumnStates` list (order, width, visible) to GridState — each entry: `GridColumnState { Field, Order, Width, Visible }`
- [x] A6.6 — Add `SearchFilter` string to GridState (was already present from A3 — verified)
- [ ] A6.7 — Add `Skip` int to GridState (for virtual scroll position)
- [ ] A6.8 — Add `TableWidth` string to GridState
- [x] A6.9 — Sync editing state (_editingItem, _originalItem) to GridState on changes (NotifyStateChanged added to all edit operations)
- [x] A6.10 — Sync _expandedDetailItems to GridState.ExpandedItems on changes (already done via ToggleDetailRow)
- [ ] A6.11 — Add `CancellationToken` to `GridReadEventArgs` for long-running server queries (QuickGrid pattern)

### A7: Highlighting & Size
Spec: `highlighting.md`, `sizing.md`

- [ ] A7.1 — Add `Size` parameter (enum: Small, Medium, Large) with CSS class mapping
- [ ] A7.2 — Add CSS variables for each size tier (font-size, padding, row-height)
- [ ] A7.3 — Add `HighlightedItems` IEnumerable<TItem> parameter
- [ ] A7.4 — Apply `mar-datagrid-row--highlighted` CSS class to highlighted rows in rendering

### A8: CSV Export
Spec: `export/csv.md`, `export/events.md`
Reference: RESEARCH_LOG.md §2 (Radzen) — Radzen keeps export logic outside the grid; grid exposes `ColumnsCollection` + current query state so export is composable. Consider exposing visible columns + current filter/sort as public API so export can be implemented as extension method or separate service.

- [ ] A8.1 — Add `ExportToCsv()` public method
- [ ] A8.2 — Expose `GetExportData()` internal method that returns visible columns + current data (respecting sort/filter)
- [ ] A8.3 — Generate CSV from visible columns and current data
- [ ] A8.4 — Trigger browser download via JS interop (`URL.createObjectURL` + click)
- [ ] A8.5 — Add `OnBeforeExport` / `OnAfterExport` EventCallback parameters
- [ ] A8.6 — Support export of all data (not just current page) via `ExportAllPages` option

---

## Phase B — JS Interop Features

*Estimated effort: High. All tasks require a shared `marilo-datagrid.js` module and `IJSRuntime` injection.*
*Community consensus: column resize + reorder are "table stakes" — prioritize B2/B3 first.*

### B0: JS Interop Infrastructure
- [ ] B0.1 — Create `wwwroot/js/marilo-datagrid.js` ES module
- [ ] B0.2 — Register JS module in `SunfishDataGrid` via `IJSRuntime.InvokeAsync<IJSObjectReference>("import", ...)`
- [ ] B0.3 — Add `IAsyncDisposable` implementation to dispose JS module reference
- [ ] B0.4 — Add `[Inject] IJSRuntime JS { get; set; }` to SunfishDataGrid.razor.cs
- [ ] B0.5 — Add `<colgroup>/<col>` elements to markup for efficient column width management (Radzen pattern — enables resize without full re-render)

### B1: Keyboard Navigation
Spec: `keyboard-navigation.md`
Reference: RESEARCH_LOG.md §5 — Radzen split: C# owns logical index state, JS owns DOM focus

- [ ] B1.1 — Add `tabindex="0"` and `@onkeydown` handler to grid root `<div>`
- [ ] B1.2 — Track focused cell position in C#: `_focusedRow` (int), `_focusedCol` (int)
- [ ] B1.3 — JS: `focusCell(gridId, rowIndex, cellIndex)` — queries DOM rows/cells, sets focus, returns `int[]` with actual new position back to .NET
- [ ] B1.4 — Arrow keys: call JS focusCell, update C# indices from return value
- [ ] B1.5 — Enter: begin edit on focused cell (InCell) or focused row (Inline); on header row trigger sort
- [ ] B1.6 — Escape: cancel edit
- [ ] B1.7 — Tab/Shift+Tab: move between editable cells
- [ ] B1.8 — Alt+ArrowDown on filter-enabled header: open filter popup
- [ ] B1.9 — Add `aria-activedescendant` for screen reader support
- [ ] B1.10 — Add `CustomKeyboardShortcuts` Dictionary parameter for user-defined shortcuts

### B2: Column Resizing ⚡ (table stakes)
Spec: `columns/resize.md`
Reference: RESEARCH_LOG.md §2 — Radzen `<colgroup>/<col>` pattern, single JS→.NET callback on mouseup

- [ ] B2.1 — Add `AllowColumnResize` bool parameter on grid (default: false)
- [ ] B2.2 — Add `Resizable` bool parameter to `SunfishGridColumn` (default: true when grid allows)
- [ ] B2.3 — Add `MinResizableWidth` / `MaxResizableWidth` string parameters on column
- [ ] B2.4 — Render drag handle `<div>` in header cells for resizable columns
- [ ] B2.5 — JS: `startColumnResize(gridId, elementRef, colIndex, clientX)` — mousedown captures initial state, mousemove updates `<col>` widths directly in DOM (no .NET calls during drag)
- [ ] B2.6 — JS→.NET: `[JSInvokable] OnColumnResized(int colIndex, double newWidth)` fires **once on mouseup** — updates column width and fires event
- [ ] B2.7 — Persist widths in `GridState.ColumnStates`
- [ ] B2.8 — Add `OnColumnResized` EventCallback<DataGridColumnResizedEventArgs> parameter

### B3: Column Reordering ⚡ (table stakes)
Spec: `columns/reorder.md`
Reference: RESEARCH_LOG.md §3 — Radzen visual clone + cancelable pre-event

- [ ] B3.1 — Add `AllowColumnReorder` bool parameter on grid (default: false)
- [ ] B3.2 — Add `Reorderable` bool parameter to `SunfishGridColumn` (default: true when grid allows)
- [ ] B3.3 — Add `OrderIndex` (int?) property to `SunfishGridColumn` for persistence
- [ ] B3.4 — JS: `initColumnReorder(gridId, elementRef, dotNetRef)` — HTML5 drag-and-drop with visual clone `<th>` positioned absolutely
- [ ] B3.5 — .NET: cancelable `OnColumnReordering` event fires before move; `OnColumnReordered(OldIndex, NewIndex)` fires after
- [ ] B3.6 — Remove column from `_columns` and re-insert at target index; call `SetOrderIndex` on all columns
- [ ] B3.7 — Persist column order in `GridState.ColumnStates`

### B4: Row Drag-and-Drop
Spec: `row-drag-drop.md`

- [ ] B4.1 — Add `RowDraggable` bool parameter
- [ ] B4.2 — JS: `initRowDrag(elementRef, dotNetRef)` — HTML5 drag-and-drop on rows
- [ ] B4.3 — Render drag handle element in rows when enabled
- [ ] B4.4 — .NET callback: `OnRowDropped(int sourceIndex, int destIndex)`
- [ ] B4.5 — Add `OnRowDrop` EventCallback<GridRowDropEventArgs<TItem>>
- [ ] B4.6 — Create `GridRowDropEventArgs<TItem>` (Item, DestinationItem, DestinationIndex, DropPosition)

### B5: Frozen/Locked Columns
Spec: `columns/frozen.md`
Reference: RESEARCH_LOG.md §4 — Radzen stacked `position: sticky` offsets, boundary shadow classes

- [ ] B5.1 — Add `Locked` bool parameter to `SunfishGridColumn` (default: false)
- [ ] B5.2 — Add `FrozenPosition` enum (Left, Right) parameter on column (default: Left)
- [ ] B5.3 — Add `Lockable` bool parameter to `SunfishGridColumn` (default: true)
- [ ] B5.4 — Apply `position: sticky` with computed `inset-inline-start`/`inset-inline-end` — sum widths of preceding frozen columns for offset
- [ ] B5.5 — CSS classes: `mar-datagrid-col--locked`, `mar-datagrid-col--locked-end` (shadow on boundary column)
- [ ] B5.6 — Z-index layering: 2 for header+frozen, 1 for body+frozen
- [ ] B5.7 — JS: recalculate offsets on column resize/reorder

---

## Phase C — Advanced Features (May Require External Dependencies)

*Estimated effort: High. Some features need NuGet packages or complex UI.*

### C1: Excel Export
Spec: `export/excel.md`
Dependency: ClosedXML (MIT) or similar

- [ ] C1.1 — Add ClosedXML NuGet reference (MIT license)
- [ ] C1.2 — Add `ExportToExcel()` public method
- [ ] C1.3 — Generate .xlsx from visible columns and data
- [ ] C1.4 — Trigger browser download via JS interop
- [ ] C1.5 — Respect Format strings for cell formatting in Excel

### C2: PDF Export
Spec: `export/pdf.md`
Dependency: QuestPDF (MIT) or similar

- [ ] C2.1 — Add QuestPDF NuGet reference (MIT license)
- [ ] C2.2 — Add `ExportToPdf()` public method
- [ ] C2.3 — Generate table layout matching grid columns
- [ ] C2.4 — Trigger browser download via JS interop

### C3: Column Menu
Spec: `columns/menu.md`

- [ ] C3.1 — Create `SunfishGridColumnMenu` component (dropdown popup)
- [ ] C3.2 — Render menu trigger button in header when `ShowColumnMenu` is true
- [ ] C3.3 — Menu items: Sort Ascending, Sort Descending, Clear Sort, Filter, Lock/Unlock
- [ ] C3.4 — Add click-outside-to-close behavior (JS interop)

### C4: Column Chooser
Spec: `templates/column-chooser.md`

- [ ] C4.1 — Create `SunfishGridColumnChooser` component (dialog/popup)
- [ ] C4.2 — List all columns with `VisibleInColumnChooser == true`
- [ ] C4.3 — Checkbox per column to toggle `Visible`
- [ ] C4.4 — Add toolbar button or API to open column chooser
- [ ] C4.5 — Add `ColumnChooserTemplate` RenderFragment parameter

### C5: CheckBoxList Filter Mode
Spec: `filtering/checkboxlist.md`

- [ ] C5.1 — Add `GridFilterMode.CheckBoxList` enum value
- [ ] C5.2 — Render distinct values as checkbox list in filter popup
- [ ] C5.3 — Apply multi-value filter (IN operator semantics)
- [ ] C5.4 — Support async loading of distinct values for large datasets

### C6: Multi-Column Headers
Spec: `columns/multi-column-headers.md`, `columns/stacked.md`

- [ ] C6.1 — Create `SunfishGridColumnGroup` component
- [ ] C6.2 — Render `<thead>` with multiple `<tr>` rows for nested headers
- [ ] C6.3 — Calculate `colspan` for parent header cells
- [ ] C6.4 — Support arbitrary nesting depth

### C7: Cell Selection
Spec: `selection/cells.md`

- [ ] C7.1 — Add `GridSelectionMode.Cell` enum value
- [ ] C7.2 — Track `_selectedCells` as HashSet<(TItem, string)>
- [ ] C7.3 — Add `SelectedCells` / `SelectedCellsChanged` parameters
- [ ] C7.4 — Render `aria-selected` on selected cells
- [ ] C7.5 — Support Ctrl+Click for multi-cell selection

### C8: Editing Validation
Spec: `editing/validation.md`

- [ ] C8.1 — Wrap inline/popup editors in `EditForm` with `DataAnnotationsValidator`
- [ ] C8.2 — Show `ValidationMessage` per field
- [ ] C8.3 — Block `SaveEdit()` when validation fails
- [ ] C8.4 — Add `ValidationMessageTemplate` RenderFragment parameter

---

## Phase D — Future / Out of Scope

*These are documented in the spec but have low priority or require significant infrastructure.*

### D1: AI Features
Spec: `ai/` (9 files)
*Requires AI service integration, out of scope for core grid.*

- [ ] D1.1 — Define `ISunfishAIService` interface
- [ ] D1.2 — AI Column (auto-fill suggestions)
- [ ] D1.3 — AI Highlight (anomaly detection)
- [ ] D1.4 — AI Search (natural language query)
- [ ] D1.5 — AI Smart Box (contextual actions)

### D2: Popup Editing Templates
Spec: `templates/popup-form.md`, `templates/popup-buttons.md`

- [ ] D2.1 — Add `PopupFormTemplate` RenderFragment<TItem> parameter
- [ ] D2.2 — Add `PopupButtonsTemplate` RenderFragment parameter
- [ ] D2.3 — Add popup width/height configuration

### D3: Pager Template
Spec: `templates/pager.md`

- [ ] D3.1 — Add `PagerTemplate` RenderFragment parameter
- [ ] D3.2 — Provide pager context (CurrentPage, TotalPages, PageSize) to template

### D4: Toolbar Built-in Tools
Spec: `toolbar/toolbar.md` (13 tools)

- [ ] D4.1 — Create individual `GridToolBar*` components (Add, Search, Export, ColumnChooser, etc.)
- [ ] D4.2 — Wire each tool to corresponding grid action
- [ ] D4.3 — Support custom toolbar item positioning

### D5: Advanced Column Types
Spec: `columns/virtual.md`, `columns/checkbox.md`

- [ ] D5.1 — Add `FilterEditorType` / `FilterEditorFormat` parameters
- [ ] D5.2 — Add `FieldType` parameter for ExpandoObject / DataTable support
- [ ] D5.3 — Add `FilterCellTemplate` / `FilterMenuTemplate` parameters
- [ ] D5.4 — Virtual column (non-data-bound computed column)
- [ ] D5.5 — GridCheckboxColumn dedicated component

### D6: AdaptiveMode
Spec: `overview.md`

- [ ] D6.1 — Add `AdaptiveMode` enum parameter
- [ ] D6.2 — Switch to card layout on narrow viewports
- [ ] D6.3 — JS: viewport size detection and breakpoint management

---

## Progress Summary

| Phase | Total Tasks | Completed | Status |
|-------|------------|-----------|--------|
| A — Pure C# | 49 | 0 | Not Started |
| B — JS Interop | 35 | 0 | Not Started |
| C — Advanced | 29 | 0 | Not Started |
| D — Future | 21 | 0 | Not Started |
| **Total** | **134** | **0** | — |

## Session Log

| Date | Session | Phase/Tasks | Notes |
|------|---------|-------------|-------|
| 2026-03-31 | Pass 1 | Pre-phase | 16 grid gaps + 4 column gaps + 1 toolbar gap resolved |
| 2026-03-31 | Pass 2 | Pre-phase | Editing modes, DetailTemplate, FilterMenu, loading, footer, multi-sort |
| | | | |

*Update this table at the start/end of each session to track progress across iterations.*
