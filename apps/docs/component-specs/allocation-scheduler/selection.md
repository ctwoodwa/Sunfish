---
title: Selection
page_title: AllocationScheduler Selection
description: Cell and range selection in the Sunfish Blazor AllocationScheduler — modes, API, events, interaction patterns, and examples.
slug: allocation-scheduler-selection
tags: sunfish,blazor,allocation-scheduler,selection,cell,range
published: True
position: 20
components: ["allocation-scheduler"]
---

# AllocationScheduler Selection

The AllocationScheduler supports selecting cells in the timeline grid, either as a single active cell or as a contiguous range. Selection is the foundation for bulk editing, copy/paste, context menu commands (distribute, shift, move), and keyboard navigation.

Unlike a row-oriented grid, the AllocationScheduler has no "row selection" concept — selection always targets one or more allocation cells identified by a `(ResourceKey, TaskId, BucketStart, BucketEnd)` tuple.

## Basics

Enable selection by setting the `SelectionMode` parameter. The default is `Range`, which means users can click a single cell or drag to select a contiguous block of cells across resources and time buckets.

```razor
<SunfishAllocationScheduler Resources="@resources"
                           Allocations="@allocations"
                           AuthoritativeLevel="TimeGranularity.Week"
                           SelectionMode="AllocationSelectionMode.Range"
                           OnSelectionChanged="@HandleSelectionChanged" />
```

## Selection Modes

The `SelectionMode` parameter accepts three values from the `AllocationSelectionMode` enum:

| Value | Behavior |
|---|---|
| `None` | Selection is disabled. Cells are still focusable for keyboard navigation, but no visual selection state is applied and `OnSelectionChanged` does not fire. Use this when the component is a read-only rollup view. |
| `Cell` | A single cell is selected at a time. Clicking a new cell replaces the selection. `Ctrl+Click` is a no-op. `Shift+Click` is a no-op. |
| `Range` (default) | A contiguous rectangle of cells is selected. Supports drag-to-select, `Shift+Click` to extend, `Shift+Arrow` to grow, and `Ctrl+A` to select all visible cells. |

## Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `SelectionMode` | `AllocationSelectionMode` | `Range` | Controls what users can select — `None`, `Cell`, or `Range`. |
| `AllowBulkEdit` | `bool` | `true` | When `true`, committing a value into one cell of an active selection applies it to every selected cell and fires `OnRangeEdited`. When `false`, only the active cell is edited. |

## Events

### OnSelectionChanged

Fires whenever the user changes the set of selected cells — click, drag, keyboard extension, or programmatic `ClearSelection()`.

| Property | Type | Description |
|---|---|---|
| `SelectedCells` | `IReadOnlyList<AllocationCellRef>` | The full set of cells in the new selection. Ordered by resource, then by time bucket. Empty when the selection was cleared. |
| `SelectionMode` | `AllocationSelectionMode` | The currently active selection mode. |

```razor
<SunfishAllocationScheduler ... OnSelectionChanged="@HandleSelectionChanged" />

@code {
    private async Task HandleSelectionChanged(SelectionChangedArgs args)
    {
        Console.WriteLine($"{args.SelectedCells.Count} cells selected");
        foreach (var cell in args.SelectedCells)
        {
            Console.WriteLine($"  {cell.ResourceKey} / {cell.TaskId} @ {cell.BucketStart:d}");
        }
    }
}
```

## AllocationCellRef

Every selected cell is represented by an `AllocationCellRef` record:

| Property | Type | Description |
|---|---|---|
| `ResourceKey` | `object` | The stable key of the resource row. Discovered from `Resources` via reflection using the configured `Id`/`Key` property. |
| `TaskId` | `object` | The stable key of the task or project track the cell belongs to. `null` if the scheduler is resource-only (no task breakdown). |
| `BucketStart` | `DateTime` | Inclusive start of the time bucket at the current `ViewGrain`. |
| `BucketEnd` | `DateTime` | Exclusive end of the time bucket. `BucketEnd - BucketStart` equals one bucket at `ViewGrain`. |

The same cell is uniquely identified by `(ResourceKey, BucketStart)` at a given `ViewGrain` — `BucketEnd` and `TaskId` are carried for convenience so handlers do not need to recompute them.

## Selection API

Two public methods on `SunfishAllocationScheduler` let host code inspect and mutate the selection programmatically:

### GetSelectedCells()

Returns the current selection as a materialized list.

```csharp
IReadOnlyList<AllocationCellRef> selected = scheduler.GetSelectedCells();
```

Returns an empty list when nothing is selected or `SelectionMode` is `None`. The returned list is a snapshot — mutating it does not affect the component's internal state.

### ClearSelection()

Clears the selection and fires `OnSelectionChanged` with an empty `SelectedCells` list.

```csharp
await scheduler.ClearSelection();
```

Returns a `Task` because it dispatches through `InvokeAsync(StateHasChanged)` for dispatcher safety.

## User Interaction

| Gesture | Result |
|---|---|
| **Single click** on a cell | Replaces the selection with that one cell. Sets the active cell (fill handle target). |
| **Click + drag** across cells | Starts a range selection. The rectangle grows as the cursor moves. Release commits the range. Only active when `SelectionMode` is `Range`. |
| **Shift + Click** | Extends the existing selection to form a rectangle from the first selected cell to the clicked cell. Active cell moves to the click target. |
| **Ctrl + Click** | Reserved. In current builds, Ctrl+Click is a no-op — non-contiguous selection is not supported because bulk-edit semantics assume a rectangular range. |
| **Shift + Arrow** | Extends the selection one cell in the arrow direction. See [keyboard-navigation.md](keyboard-navigation.md) for the full keyboard reference. |
| **Ctrl + A** | Selects all cells at the current `ViewGrain` within `VisibleStart` … `VisibleEnd`. Only active when `SelectionMode` is `Range`. |
| **Click on empty header or resource column** | Clears the selection. |
| **`Esc`** | Clears the selection. If an edit is in progress, `Esc` first cancels the edit, then a second `Esc` clears the selection. |

## Active Cell vs. Selection

The AllocationScheduler distinguishes the **selection** (a set of cells) from the **active cell** (a single cell that receives focus, the fill handle, and single-cell keyboard commands).

- The active cell is always part of the current selection (or is the only selected cell in `Cell` mode).
- The fill handle renders on the active cell only when `AllowDragFill` is `true`, the cell is editable, and the cell is not disabled.
- When the selection is cleared, the active cell is also cleared.
- Single-cell events (`OnCellEdited`, cell-level context menu) fire against the active cell, not the full selection. Range events (`OnRangeEdited`, `OnDistributeRequested`) fire against the full selection.

## Pre-selecting Cells

There is no `SelectedCells` / `SelectedCellsChanged` two-way binding parameter in the current release. Selection is an internal state owned by the component. Host code can react to selection via `OnSelectionChanged` and can clear it via `ClearSelection()`, but cannot push a starting selection in via a parameter.

If you need a cell to be visually highlighted on initial render, consider instead:
- Using a custom cell template (`CellTemplate`) that reads a "highlighted" flag from your data model.
- Scrolling to a specific date with `NavigateTo(DateTime date)` so the user's attention lands on the right area.

## Integration with Other Features

**Editing.** Selection is the primary input to bulk editing. When `AllowBulkEdit` is `true` and the user types a value or pastes into an active range, the value is applied to every cell and `OnRangeEdited` fires once with `AffectedRecords` covering the whole rectangle. See [editing.md](editing.md).

**Drag-fill.** Drag-fill starts from the active cell and extends along one axis (horizontal or vertical). It does not consume or replace the current selection — after the fill, the selection is updated to the filled rectangle.

**Copy / Paste.** `Ctrl+C` reads the current selection as TSV and writes it to the clipboard via JS interop. `Ctrl+V` pastes TSV into the rectangle anchored at the active cell, expanding or clipping to fit. Both operations require `SelectionMode` to be `Cell` or `Range`.

**Context menu.** The right-click context menu operates on the current selection. If the menu is opened on a cell that is not part of the current selection, the component replaces the selection with that single cell before opening the menu. See [context-menu.md](context-menu.md).

**Splitter and frozen pane.** The resource-column pane is the frozen region of the splitter layout. Selection is scoped to the timeline pane — clicking a resource row header does not start a row selection.

**Virtualization and scrolling.** When the user scrolls outside the current selection, the selection is preserved but the off-screen cells are not rendered. Re-scrolling back restores the visual selection. `GetSelectedCells()` returns the full logical selection regardless of what is in view.

**Zoom / grain change.** Changing `ViewGrain` re-buckets time, which invalidates the current selection because bucket boundaries change. When the user zooms in/out, the component clears the selection and fires `OnSelectionChanged` with an empty list.

## Accessibility

- The timeline grid has `role="grid"` and each cell has `role="gridcell"`.
- The active cell carries `aria-selected="true"`.
- Cells in an active range carry `aria-selected="true"` as well.
- The component maintains a single roving tabindex on the active cell — off-cell elements are `tabindex="-1"` so Tab moves through the grid as a single focus stop and arrow keys navigate within.
- Screen reader announcements fire on selection change via an `aria-live="polite"` region at the component root.

See [accessibility.md](accessibility.md) for the full a11y surface.

## Examples

### Single-cell selection, read-only

```razor
<SunfishAllocationScheduler Resources="@resources"
                           Allocations="@allocations"
                           AuthoritativeLevel="TimeGranularity.Week"
                           SelectionMode="AllocationSelectionMode.Cell"
                           AllowBulkEdit="false"
                           OnSelectionChanged="@ShowCellDetails" />

@code {
    private AllocationCellRef? _focused;

    private void ShowCellDetails(SelectionChangedArgs args)
    {
        _focused = args.SelectedCells.FirstOrDefault();
    }
}
```

### Range selection with bulk edit

```razor
<SunfishAllocationScheduler Resources="@resources"
                           Allocations="@allocations"
                           AuthoritativeLevel="TimeGranularity.Week"
                           SelectionMode="AllocationSelectionMode.Range"
                           AllowBulkEdit="true"
                           OnRangeEdited="@HandleRangeEdit" />

@code {
    private async Task HandleRangeEdit(RangeEditedArgs args)
    {
        foreach (var record in args.AffectedRecords)
            await _repository.SaveAsync(record);
    }
}
```

### Programmatic clear on external action

```razor
<SunfishAllocationScheduler @ref="_scheduler" ... />
<button @onclick="ClearAll">Clear selection</button>

@code {
    private SunfishAllocationScheduler _scheduler = default!;

    private Task ClearAll() => _scheduler.ClearSelection();
}
```

## See Also

- [Keyboard Navigation](keyboard-navigation.md)
- [Editing](editing.md)
- [Events](events.md)
- [Context Menu](context-menu.md)
- [Accessibility](accessibility.md)
