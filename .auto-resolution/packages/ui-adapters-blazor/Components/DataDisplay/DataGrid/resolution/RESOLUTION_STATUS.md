---
component: SunfishDataGrid, SunfishGridColumn, SunfishGridToolbar
phase: 2
status: complete
complexity: multi-pass
priority: high
owner: "claude"
last-updated: 2026-04-01
depends-on: [SunfishThemeProvider, SunfishPagination, SunfishForm]
external-resources:
  - name: "Blazor Virtualize"
    url: "https://learn.microsoft.com/aspnet/core/blazor/components/virtualization"
    license: "MIT (framework)"
    approved: true
---

# Resolution Status: SunfishDataGrid, SunfishGridColumn, SunfishGridToolbar

## Current Phase
Phase 2: Complete. Remaining work tracked in `../GAP_ANALYSIS.md` (Phases A–D).

## Gap Summary
SunfishDataGrid had 44 gaps. SunfishGridColumn had 8 gaps. SunfishGridToolbar had 2 gaps.

## Pass 1 Resolutions (2026-03-31)

### SunfishDataGrid — Resolved Gaps

| # | Gap | Severity | Resolution |
|---|-----|----------|------------|
| 1 | `GridState` internal-only | **High** | Made `GridState` public with full doc comments. Added `GetState()` public method. |
| 2 | No `OnStateInit` / `OnStateChanged` events | **Medium** | Added `OnStateInit` (fires once on init) and `OnStateChanged` (fires on every state mutation with `GridStateChangedEventArgs`). |
| 3 | No `PageChanged` event | **High** | Added `PageChanged` EventCallback<int> with two-way bindable `Page` parameter. |
| 4 | No `PageSizeChanged` event | **High** | Added `PageSizeChanged` EventCallback<int> and `PageSizes` parameter for page-size dropdown UI. |
| 5 | `OnRead` declared but never invoked | **High** | Implemented full `OnRead` data flow. When `OnRead` is bound, grid delegates sort/filter/page to consumer via `GridReadEventArgs<TItem>`. |
| 6 | No `Height` parameter | **High** | Added `Height` parameter. Wraps table in scrollable `<div>` with `max-height` and `overflow:auto`. |
| 7 | No `Width` parameter | **Medium** | Added `Width` parameter applied to root element. |
| 8 | No public `OnRowClick` event | **High** | Added `OnRowClick` EventCallback with `GridRowClickEventArgs<TItem>` (Item, Field, EventArgs). |
| 9 | No `OnRowDoubleClick` event | **Medium** | Added `OnRowDoubleClick` EventCallback with same args type. |
| 10 | No `OnRowContextMenu` event | **Medium** | Added `OnRowContextMenu` EventCallback with same args type. |
| 11 | No `OnRowRender` callback | **Medium** | Added `OnRowRender` Action<GridRowRenderEventArgs<TItem>> for per-row CSS customization. |
| 12 | No checkbox selection column | **Medium** | Added `ShowCheckboxColumn` parameter with select-all header checkbox. |
| 13 | No `Rebind` method | **Medium** | Added public `Rebind()` method to force data refresh. |
| 14 | No `Navigable` parameter | **High** | Added `Navigable` parameter (placeholder for keyboard nav implementation). |
| 15 | No `Page` bindable parameter | **High** | Added bindable `Page` parameter (1-based). |
| 16 | `EndsWith` filter operator not supported | **Low** | Added `EndsWith` case to client-side filter logic. |

### SunfishGridColumn — Resolved Gaps

| # | Gap | Severity | Resolution |
|---|-----|----------|------------|
| 1 | No `Visible` parameter | **Medium** | Added `Visible` parameter (default true). Grid filters by `_visibleColumns`. |
| 2 | No `EditorTemplate` | **High** | Added `EditorTemplate` RenderFragment<TItem> parameter. |
| 3 | No `FooterTemplate` | **Low** | Added `FooterTemplate` RenderFragment parameter. |
| 4 | No `OnCellRender` event | **Medium** | Added `OnCellRender` Action<GridCellRenderEventArgs<TItem>> for per-cell CSS customization. |

### SunfishGridToolbar — Resolved Gaps

| # | Gap | Severity | Resolution |
|---|-----|----------|------------|
| 1 | No ARIA role | **Low** | Added `role="toolbar"` and `aria-label`. |

### New Files Created (Pass 1)

| File | Purpose |
|------|---------|
| `GridEventArgs.cs` | Event args: `GridRowClickEventArgs<T>`, `GridReadEventArgs<T>`, `GridRowRenderEventArgs<T>`, `GridCellRenderEventArgs<T>`, `GridStateChangedEventArgs` |

## Pass 2 Resolutions (2026-03-31)

### SunfishDataGrid — Resolved Gaps

| # | Gap | Severity | Resolution |
|---|-----|----------|------------|
| 1 | No editing support (CRUD) | **High** | Implemented Inline, InCell, and Popup edit modes with full CRUD lifecycle. |
| 2 | No CUD events | **High** | Added OnAdd, OnCreate, OnUpdate, OnDelete, OnEdit, OnCancel, OnModelInit, OnCommand EventCallbacks. |
| 3 | No command column | **High** | Inline/Popup modes render Edit/Delete and Save/Cancel buttons in command cell. |
| 4 | No DetailTemplate / hierarchy | **Medium** | Added DetailTemplate RenderFragment<TItem>, expand/collapse button, OnRowExpand/OnRowCollapse events. |
| 5 | No FilterMenu mode | **Medium** | Added FilterMenu with operator dropdown (11 operators), value input, Apply/Clear actions. |
| 6 | Extended filter operators incomplete | **Low** | All 11 FilterOperator values now work: Contains, Equals, NotEquals, StartsWith, EndsWith, GT, GTE, LT, LTE, IsNull, IsNotNull with IComparable type-aware comparison. |
| 7 | Single-sort only | **Low** | Multi-sort via Ctrl+Click with ThenBy/ThenByDescending chaining and sort-order indicator. |
| 8 | No loading animation | **Low** | Added `IsLoading` parameter with overlay (`aria-busy`, loading spinner). |
| 9 | No virtual scrolling | **Medium** | Added `EnableVirtualization` and `VirtualizeOverscanCount` parameters using Blazor `<Virtualize>`. |
| 10 | No footer rendering | **Low** | Added `<tfoot>` section that renders `FooterTemplate` from each column. |
| 11 | InCell double-click editing | **Medium** | InCell mode: double-click cell to edit, inline ✓/✗ save/cancel buttons per cell. |

### New Files Created (Pass 2)

| File | Purpose |
|------|---------|
| `GridCommandTypes.cs` | `GridCommandDefinition`, `GridCommandPlacement` enum, `GridEditEventArgs<T>`, `GridModelInitEventArgs<T>`, `GridCommandEventArgs<T>` |
| `SunfishGridCommandButton.razor` | Reusable command button component with CascadingParameter to parent grid |
| `SunfishDataGrid.Editing.cs` | Partial class: BeginEdit, BeginCellEdit, BeginAdd, SaveEdit, CancelEdit, DeleteItem, ExecuteCommand, ToggleDetailRow |
| `SunfishDataGrid.Data.cs` | Partial class: ProcessDataAsync, ApplyFilter (11 operators), ApplySort (multi), event handlers |
| `SunfishDataGrid.Rendering.cs` | Partial class: RenderDataRow, RenderEditRow, RenderFilterMenu, row/cell render callbacks |

## Pass 3 Resolutions (2026-04-01)

### SunfishDataGrid — Resolved Gaps

| # | Gap | Severity | Resolution |
|---|-----|----------|------------|
| 1 | No grouping support | **High** | Full grouping: `Groupable` parameter, `GroupBy`/`Ungroup`/`UngroupAll` API, multi-level nested groups, collapsible group headers, `GroupHeaderTemplate`/`GroupFooterTemplate`, group state in GridState. |
| 2 | `GroupDescriptor` missing sort direction | **Medium** | Added `Direction` property (SortDirection) to GroupDescriptor. Groups are sorted by key. |
| 3 | No `AutoGenerateColumns` | **Medium** | Added `AutoGenerateColumns` parameter. When true and no explicit columns defined, generates columns from TItem's public simple-type properties via reflection. Titles auto-split from camelCase. |
| 4 | No column resize | **High** | Added `Resizable` parameter. JS interop adds drag handles to header cells. Fires `OnColumnResize` event. |
| 5 | No column reorder | **High** | Added `Reorderable` parameter. JS interop enables HTML5 drag-and-drop on header cells. Fires `OnColumnReorder` event. |
| 6 | No keyboard navigation | **Medium** | Full keyboard nav when `Navigable` is true: Arrow keys, Home/End (with Ctrl), Enter/Space to activate. JS interop manages focus tracking across cells. |
| 7 | No `NoDataTemplate` | **Medium** | Added `NoDataTemplate` RenderFragment for customizable empty state. Falls back to "No data available." text. |
| 8 | No `RowTemplate` | **Low** | Added `RowTemplate` RenderFragment<TItem> for full custom row rendering. |
| 9 | No search/filter box | **Medium** | Added `ShowSearchBox` parameter with `SearchBoxPlaceholder`. Global text search across all visible columns. |
| 10 | No CSV export | **Low** | Added `ExportToCsv()` public method. Generates CSV from all filtered/sorted data (not just current page). Proper escaping. |
| 11 | `GridState` missing fields | **Medium** | Added `SearchFilter`, `EditItem`, `OriginalEditItem`, `InsertedItem`, `ExpandedItems`, `CollapsedGroups`, `ColumnStates` to GridState. Added `GridColumnState` class. |

### New Files Created (Pass 3)

| File | Purpose |
|------|---------|
| `SunfishDataGrid.Interop.cs` | Partial class: JS interop for column resize, column reorder, keyboard navigation. `ColumnResizeEventArgs`, `ColumnState` types. |

### New Types Added (Pass 3)

| Type | Purpose |
|------|---------|
| `GridGroupRow<TItem>` | Represents a group of items with key, depth, child groups |
| `GridGroupHeaderContext<TItem>` | Context for GroupHeaderTemplate/GroupFooterTemplate |
| `ColumnResizeEventArgs` | Event args for OnColumnResize |
| `ColumnState` | Runtime column visual state |
| `GridColumnState` | Persisted column state in GridState |

## Remaining Work

All remaining gaps are tracked in `../GAP_ANALYSIS.md` with phased task breakdowns:

| Phase | Description | Task Count |
|-------|-------------|------------|
| A | Pure C# features (validation, stacked columns) | ~10 |
| B | JS interop features (row drag-to-reorder, frozen columns, sticky header) | ~15 |
| C | Advanced features (Excel/PDF export, column menu/chooser, cell selection) | ~25 |
| D | Future/out-of-scope (AI, popup templates, toolbar tools, adaptive mode) | ~21 |
