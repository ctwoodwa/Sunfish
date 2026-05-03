# Research Log: SunfishDataGrid, SunfishGridColumn, SunfishGridToolbar

Last Updated: 2026-04-01

## Sources Evaluated

| Source | License | Relevance |
|--------|---------|-----------|
| Radzen Blazor DataGrid (`radzenhq/radzen-blazor`) | MIT | High — closest Blazor enterprise-style grid; patterns for grouping, resize, reorder, frozen cols, keyboard nav |
| QuickGrid (`dotnet/aspnetcore`) | MIT | Medium — clean minimal architecture; ItemsProvider model, decoupled pagination, virtualization |
| MudBlazor DataGrid (`MudBlazor/MudBlazor`) | MIT | Medium — programmatic filter API, server-side grouping gaps to avoid, group expand state management |
| Tabulator | MIT | Medium — JS virtual DOM rendering with row recycling and padding-based scroll; column virtualization |
| AG Grid Community | MIT (community) | Low (reference only) — row grouping model, aggregate computation, community vs enterprise feature scoping |

---

## Findings by Feature Area

### 1. Grouping

**Applies to: A1 (Grouping)**

**Radzen pattern (recommended):**
- Uses a recursive `GroupByMany` extension on `IQueryable<T>` that produces a tree of `GroupResult` objects (`Key`, `Count`, `Items`, `Subgroups`).
- Collapse state uses a **collapse-by-exception** pattern: a `Dictionary<GroupRow, bool>` only stores collapsed items (all groups default to expanded). This is simpler and more memory-efficient than tracking both states.
- Group header/footer rendering uses dedicated `RadzenDataGridGroupRow<TItem>` component.
- Parameters: `AllowGrouping` (bool), `HideGroupedColumn` (bool), `AllGroupsExpanded` (bool?, two-way bindable), `GroupHeaderTemplate`, `GroupFooterTemplate`, per-column `Groupable` (bool).
- Events: `Group` (fires on group/ungroup), `GroupRowExpand`, `GroupRowCollapse`.

**AG Grid pattern (reference):**
- Column-driven: set `rowGroup: true` on column defs. Grid auto-generates group rows.
- Group nodes vs leaf nodes are distinct types in the row model — group nodes hold children + aggregation results.
- Aggregation uses tree-path recalculation: only the affected branch is recomputed on data change, not the entire tree.
- Filtering applies to leaf rows first; empty groups are hidden after.

**MudBlazor pitfall to avoid:**
- Server-side grouping is broken in MudBlazor (#9295) because `GridState` doesn't include `GroupDefinitions` in the server callback. **Our design should include GroupDescriptors in `GridReadEventArgs` from day one** so OnRead consumers can group server-side.
- MudBlazor's group expand state is keyed by path (`group.KeyPath`), not index — correct for nested grouping.

**Adopted approach:**
- Implement `GroupByMany`-style recursive grouping producing `GroupResult<TItem>` tree
- Collapse-by-exception tracking (HashSet of collapsed group keys, keyed by path string)
- Include `GroupDescriptors` in `GridReadEventArgs` for server-side grouping support
- Support `IEnumerable<IGrouping<string, TItem>>` return type from OnRead for pre-grouped data

### 2. Column Resizing

**Applies to: B2 (Column Resizing)**

**Radzen pattern (recommended):**
- Uses `<colgroup>/<col>` elements with id-based targeting. Resize updates `<col>` widths directly in the DOM — avoids re-rendering the entire grid.
- JS: `Radzen.startColumnResize(gridId, elementRef, columnIndex, clientX)` captures initial state on mousedown, updates widths on mousemove, and calls back to .NET via `[JSInvokable]` **only once on mouseup** with `(columnIndex, newWidth)`.
- .NET side: `column.SetWidth($"{Math.Round(value)}px")` and fires `ColumnResized` event.
- Parameters: grid-level `AllowColumnResize` (bool), per-column `Resizable` (bool, default true).

**Adopted approach:**
- Use `<colgroup>/<col>` pattern for efficient DOM-only width updates during drag
- Single JS→.NET callback on mouseup (not per-mousemove) to minimize interop overhead
- Grid-level `AllowColumnResize` + per-column `Resizable` with `MinResizableWidth`/`MaxResizableWidth`

### 3. Column Reordering

**Applies to: B3 (Column Reordering)**

**Radzen pattern (recommended):**
- JS creates a draggable visual clone `<th>` positioned absolutely during drag.
- On drop, .NET removes column from list and re-inserts at target index, then calls `SetOrderIndex` on every column.
- Cancelable `ColumnReordering` event fires before the move; `ColumnReordered` fires after with `OldIndex`/`NewIndex`.
- Each column has an `OrderIndex` (int?) that persists through settings save/load.

**Adopted approach:**
- HTML5 drag-and-drop with visual clone for drag feedback
- Cancelable pre-event + post-event pattern
- `OrderIndex` on each column, persisted in `GridState.ColumnStates`

### 4. Frozen/Locked Columns

**Applies to: B5 (Frozen/Locked Columns)**

**Radzen pattern (recommended):**
- Uses CSS `position: sticky` with computed `inset-inline-start` / `inset-inline-end`.
- `GetStackedStyleForFrozen()` sums widths of preceding frozen columns to calculate offset — each frozen column's offset = sum of all prior frozen column widths.
- Six CSS classes: `rz-frozen-cell-left`, `rz-frozen-cell-left-end` (shadow on boundary), `rz-frozen-cell-left-inner`, and right equivalents.
- Z-index layering: 2 for header+frozen, 1 for body+frozen.
- Parameters: per-column `Frozen` (bool), `FrozenPosition` (enum: Left | Right).

**Adopted approach:**
- `position: sticky` with stacked offset calculation (sum of prior frozen widths)
- Shadow class on boundary column for visual separation
- Z-index: header frozen > body frozen > normal cells
- Support both Left and Right frozen positions via `FrozenPosition` enum on column

### 5. Keyboard Navigation

**Applies to: B1 (Keyboard Navigation)**

**Radzen pattern (recommended):**
- Grid root `<div>` has `tabindex="0"` and `@onkeydown` handler.
- C# tracks logical position: `focusedIndex` (row) and `focusedCellIndex` (column).
- JS function `Radzen.focusTableRow(gridId, rowIndex, cellIndex)` queries actual DOM table rows/cells, sets focus, and returns `int[]` with the new `[rowIndex, cellIndex]` back to .NET.
- **Key split: C# owns logical state, JS owns DOM focus.** This avoids race conditions.
- Arrow keys move focus. Enter/Space on header triggers sort; on data rows triggers selection. ArrowLeft/Right on expandable rows toggles detail. Alt+ArrowDown on filter-enabled headers opens filter popup. PageUp/Down/Home/End supported.

**Adopted approach:**
- C# maintains `_focusedRow`/`_focusedCol` logical indices
- JS handles actual DOM focus + returns updated indices to .NET
- `aria-activedescendant` for screen reader support
- Key bindings: arrows (navigate), Enter (edit/select), Escape (cancel), Tab (next editable cell), Alt+Down (open filter)

### 6. Virtual Scrolling

**Applies to: existing `EnableVirtualization` parameter**

**Tabulator pattern (reference):**
- Renders visible rows + buffer of 1x table height above and below (~3x visible rows in DOM).
- **Row recycling:** detached rows are cached, not destroyed. If data unchanged, cached element is reattached without re-render.
- **Padding-based scrollbar:** top/bottom padding on container equals height of non-rendered rows. No placeholder elements.
- **Two scroll modes:** incremental (small scroll — add/remove one row from buffer edges) vs jump (large scroll > window height — full window rebuild).
- Horizontal virtualization also supported (render only visible columns per row).
- **Requires explicit height** to activate.

**Current state:** We use Blazor's built-in `<Virtualize>` component which handles most of this. No action needed unless performance issues arise with 100k+ rows, at which point a custom virtualizer with row recycling could be considered.

### 7. Data Provider Model

**Applies to: existing `OnRead` parameter**

**QuickGrid pattern (reference):**
- `GridItemsProvider<TGridItem>` delegate accepts `GridItemsProviderRequest<TGridItem>` and returns `ValueTask<GridItemsProviderResult<TGridItem>>`.
- Request carries: `StartIndex`, `Count`, sorting metadata, `CancellationToken`.
- Utility methods: `ApplySorting()` (apply sort to IQueryable), `GetSortByProperties()` (extract for manual queries).
- Three data source modes: in-memory IQueryable, EF Core IQueryable, remote ItemsProvider.
- **Pagination is decoupled** into a standalone `PaginationState` object — can be placed anywhere on the page.
- Virtualization and pagination are mutually exclusive.

**QuickGrid sort pattern:**
- Expression-based via `GridSort<T>` with fluent chaining: `GridSort<T>.ByAscending(x => x.Name).ThenDescending(x => x.Date)`.
- Strongly typed — no string-based field names for sort expressions.

**Relevance:** Our `OnRead` + `GridReadEventArgs` already follows this pattern. Consider adding `CancellationToken` to `GridReadEventArgs` for long-running server queries. Expression-based sorting is a nice-to-have but string-based field names are simpler for our current architecture.

### 8. Programmatic Filter API

**Applies to: A3 (SearchBox), existing filter infrastructure**

**MudBlazor pattern (reference):**
- Filters are first-class `FilterDefinition<T>` objects: `Id` (Guid), `Title`, `Column`, `Operator` (string), `Value`.
- Grid exposes async methods on `@ref`: `AddFilterAsync(def)`, `ClearFiltersAsync()`, `ToggleFiltersMenu()`.
- For `ColumnFilterMenu` mode, `column.FilterContext.FilterDefinition` must stay in sync with programmatic state.

**Relevance:** Our `FilterDescriptor` already has `Field`, `Operator`, `Value`. Consider exposing `AddFilter(FilterDescriptor)`/`ClearFilters()` public methods on the grid for programmatic control (useful for SearchBox implementation and toolbar integrations).

### 9. Feature Scoping (Community Priorities)

**Applies to: overall roadmap prioritization**

**AG Grid community (free/MIT) includes:** Sorting, filtering, pagination, cell editing, custom cell renderers, row/column virtualization, theming, keyboard navigation, ARIA accessibility.

**AG Grid enterprise (paid) includes:** Row grouping, aggregation, pivoting, master/detail, server-side row model, integrated charting, advanced filters, Excel export.

**Blazor community consensus ("table stakes"):**
- Sorting (multi-column), filtering (column-level), paging or virtual scrolling
- Column resizing and reordering
- Row selection (single and multi)
- IQueryable/EF Core data binding
- Responsive layout, basic cell templates

**"Nice to have" per community:**
- Export (Excel, CSV, PDF)
- Inline/cell editing
- Row grouping and aggregation
- Column pinning/freezing
- Keyboard navigation and accessibility
- State persistence

**Common OSS pain points:**
- QuickGrid is intentionally minimal — no editing, grouping, or advanced filtering
- MudBlazor struggles with large datasets (basic virtualization)
- No single OSS grid matches commercial grids' full feature set (Telerik, Syncfusion)
- Radzen comes closest but advanced tooling requires paid tier

**Impact on our roadmap:** Column resize/reorder (Phase B) should be prioritized — they are "table stakes" per community. Grouping (Phase A) is high-value differentiator since most OSS grids don't include it free.

---

## Adopted Code

No external code adopted. All implementations will be original, informed by the patterns documented above.

## License Compatibility Notes

All external resources must use MIT, Apache-2.0, BSD-2-Clause, or BSD-3-Clause compatible licenses.

| Library | License | Use |
|---------|---------|-----|
| Radzen Blazor | MIT | Pattern reference only (no code copied) |
| QuickGrid | MIT | Pattern reference only |
| MudBlazor | MIT | Pattern reference only |
| Tabulator | MIT | Pattern reference only |
| AG Grid Community | MIT | Pattern reference only |
| ClosedXML (future) | MIT | Potential dependency for Excel export (Phase C1) |
| QuestPDF (future) | MIT | Potential dependency for PDF export (Phase C2) |
