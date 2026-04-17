# Implementation Notes: SunfishDataGrid, SunfishGridColumn, SunfishGridToolbar

## Design Decisions

### Pass 1 (2026-03-31)

1. **GridState made public** — Exposes `CurrentPage`, `PageSize`, `SortDescriptors`, `FilterDescriptors`, `GroupDescriptors`, `TotalCount`, and `SelectedKeys`. Consumers can save/restore state via `OnStateInit`/`OnStateChanged`.

2. **OnRead dual-mode pattern** — When `OnRead` is bound, the grid does NOT perform client-side sort/filter/page. It passes the current state via `GridReadEventArgs<TItem>` and expects the consumer to set `Data` and `Total`. When not bound, full client-side processing applies.

3. **Row events separated from selection** — `OnRowClick` is a public event independent of selection. When `ShowCheckboxColumn` is true, row clicks fire the event but do NOT toggle selection (checkboxes handle that). When `ShowCheckboxColumn` is false and `SelectionMode != None`, row clicks both toggle selection and fire the event.

4. **OnRowRender/OnCellRender as Action, not EventCallback** — These fire synchronously during render for each row/cell. Using `Action` avoids unnecessary async overhead and aligns with the pattern used in other Blazor component libraries.

5. **Height implementation** — Uses a wrapper `<div class="mar-datagrid-content">` with `max-height` and `overflow:auto` around the `<table>`. This keeps the toolbar and pager outside the scrollable area.

6. **GridToolbar kept non-generic** — The toolbar is a simple container. Making it generic would require consumers to specify `TItem` on the toolbar tag, which adds friction for no benefit. Command button integration will be handled via a separate `GridCommandButton` component that captures the grid via cascading parameter.

## Approach

- Multi-pass resolution: Pass 1 covers the highest-severity API gaps (state, events, OnRead, scrolling).
- Pass 2 will focus on editing (CRUD) which is the largest remaining gap area.
- Pass 3 will add virtual scrolling, column resize/reorder, and grouping.

## Code Notes

### Files Modified
- `GridState.cs` — Changed from `internal` to `public`, added `GroupDescriptors`, `SelectedKeys`, XML docs
- `SunfishDataGrid.razor` — Major expansion: 420 → ~520 lines. Added 15+ parameters, dual-mode data processing, row/cell render callbacks, checkbox selection, page-size dropdown
- `SunfishGridColumn.razor` — Added `Visible`, `EditorTemplate`, `FooterTemplate`, `OnCellRender` parameters
- `SunfishGridToolbar.razor` — Added ARIA attributes

### Files Created
- `GridEventArgs.cs` — 5 event args classes for row click, read, row render, cell render, and state change events
