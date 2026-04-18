---
title: Refresh Data
page_title: AllocationScheduler Refresh Data
description: How to refresh the Sunfish Blazor AllocationScheduler after data changes — Rebind method, Refresh method, observable data, and new collection reference.
slug: allocation-scheduler-refresh-data
tags: sunfish,blazor,allocation-scheduler,refresh,rebind,data-binding,observable
published: True
position: 22
components: ["allocation-scheduler"]
---

# AllocationScheduler Refresh Data

The AllocationScheduler renders from three data parameters — `Resources`, `Allocations`, and `Targets`. When the underlying data changes (a new allocation is saved, a resource is renamed, a target is updated), the component needs a signal to re-read the data and re-render. Blazor does not auto-detect collection mutations, so host code must tell the component to refresh using one of three approaches.

This page documents the three supported approaches, when to use each, and their performance characteristics.

## Approach Summary

| Approach | When to use | Cost | Preserves internal state |
|---|---|---|---|
| **Rebind method** | Explicit, on-demand refresh after a known mutation. | Low — single re-read of current parameter references. | Yes (selection, active cell, scroll, edit state). |
| **Observable data** | Frequent, small mutations driven by another part of the app. | Per-mutation — propagates each add/remove. | Yes. |
| **New collection reference** | Full dataset swap (e.g. after a server fetch). | Medium — forces full recompute of buckets + header groups. | No (clears selection if bucket boundaries change). |
| **Refresh method** | Visual redraw without touching data. | Minimum — single `StateHasChanged`. | Yes. |

## Rebind Method

The `Rebind()` method is the recommended way to refresh the scheduler after a known mutation. It re-reads the current `Resources`, `Allocations`, and `Targets` references and triggers a render.

```razor
<SunfishAllocationScheduler @ref="_scheduler"
                           Resources="@_resources"
                           Allocations="@_allocations"
                           AuthoritativeLevel="TimeGranularity.Week" />

<button @onclick="AddAllocation">Add allocation</button>

@code {
    private SunfishAllocationScheduler _scheduler = default!;
    private List<Resource> _resources = new();
    private List<AllocationRecord> _allocations = new();

    private async Task AddAllocation()
    {
        _allocations.Add(new AllocationRecord { /* … */ });
        await _scheduler.Rebind();
    }
}
```

**When to use:**
- You mutated an existing `List<T>` or other non-observable collection in place.
- You know exactly when the data changed and want an explicit refresh point.
- You want to preserve selection, active cell, scroll position, and edit state across the refresh.

**Internal behavior:** `Rebind()` recomputes `_effectiveAllocations`, recomputes `_visibleBuckets`, recomputes `_headerGroups`, then calls `InvokeAsync(StateHasChanged)`. Selection and active cell are preserved as long as the referenced cells still exist after the rebind.

**Return value:** `Task`. Always `await` it — the method dispatches through `InvokeAsync` for dispatcher safety (see the dispatcher-safe state rule in the component's design notes).

## Refresh Method

The `Refresh()` method re-renders the component without re-reading data. Use it when the visual output depends on state outside the data parameters — for example, after you update CSS variables, change the current theme, or mutate a template-bound dictionary.

```csharp
await _scheduler.Refresh();
```

**When to use:**
- Theme switching.
- Toggling a template that reads from an external state bag.
- After resizing a parent container (though the internal `ResizeObserver` usually handles this automatically).

**Do not use `Refresh()` to see data changes** — it does not recompute `_effectiveAllocations` or `_visibleBuckets`. Use `Rebind()` for data changes.

## Observable Data

The AllocationScheduler honors `System.Collections.ObjectModel.ObservableCollection<T>` subscribers. When `Resources` or `Allocations` is bound to an `ObservableCollection<T>`, the component subscribes to `INotifyCollectionChanged` and refreshes automatically on add, remove, or replace.

```razor
<SunfishAllocationScheduler Resources="@_resources"
                           Allocations="@_allocations"
                           AuthoritativeLevel="TimeGranularity.Week" />

@code {
    private ObservableCollection<Resource> _resources = new();
    private ObservableCollection<AllocationRecord> _allocations = new();

    protected override void OnInitialized()
    {
        _allocations.Add(new AllocationRecord { /* … */ });
        // No need to call Rebind — the component refreshes automatically.
    }
}
```

**When to use:**
- The underlying dataset is driven by another part of the app (SignalR push, background job, `IObservable` pipeline).
- Mutations are fine-grained (one allocation at a time) and happen frequently.
- You do not want host code to be responsible for calling `Rebind()` after every change.

**Notes:**
- `ObservableCollection<T>` only detects add/remove/replace at the collection level. If you mutate a field inside an existing `AllocationRecord` (e.g. change `Value`), the collection does not raise a change event. You must either replace the record (`_allocations[i] = newRecord`) or call `Rebind()` manually.
- The component unsubscribes from the old collection and subscribes to the new one if the parameter reference changes. No memory leak.
- If your collection implements `INotifyCollectionChanged` via some other type (not `ObservableCollection<T>`), it is supported as long as it raises the standard events.

## New Collection Reference

Assigning a brand new collection reference to `Resources` or `Allocations` triggers `OnParametersSet`, which re-reads everything and fully recomputes internal state.

```csharp
private async Task ReloadFromServer()
{
    var fresh = await _api.GetAllocationsAsync();
    _allocations = fresh.ToList();  // new reference
    StateHasChanged();               // let OnParametersSet fire
}
```

**When to use:**
- Full dataset swap after a server fetch.
- Scenario switching (e.g. "show me the 'optimistic' allocation set vs the 'pessimistic' one" from `AllocationSets`).
- You do not care about preserving selection or edit state (both may be cleared if the new data has different bucket boundaries).

**Caveat:** This approach re-runs the full `OnParametersSet` path, including bucket recomputation and header-group recomputation. If the new data covers a different date range or if `ViewGrain` changed, selection is cleared. This is the most expensive refresh path.

## OnRead-style Server Paging

The AllocationScheduler does not currently expose an `OnRead` event the way `SunfishDataGrid` does. Server-side data loading is handled at the host-page level: fetch into a local collection, then use one of the three approaches above. Track [grid OnRead parity](../grid/refresh-data.md) for the pattern — if server-paging is added, it will follow the same `GridReadEventArgs<T>` shape with `Filter`, `CancellationToken`, `Data`, and `Total` properties.

## Entity Framework Data

When the data comes from Entity Framework Core, the Change Tracker can surface mutations that the AllocationScheduler cannot see by itself. Two patterns work:

**Pattern 1 — Save then Rebind.** After `SaveChangesAsync`, rebind:

```csharp
private async Task CommitEdit(CellEditedArgs args)
{
    args.Record.Value = args.NewValue;
    await _dbContext.SaveChangesAsync();
    await _scheduler.Rebind();
}
```

**Pattern 2 — Save then replace.** After `SaveChangesAsync`, re-query and replace the collection:

```csharp
private async Task CommitEdit(CellEditedArgs args)
{
    args.Record.Value = args.NewValue;
    await _dbContext.SaveChangesAsync();
    _allocations = await _dbContext.Allocations.AsNoTracking().ToListAsync();
    StateHasChanged();
}
```

Pattern 1 is cheaper and preserves state; prefer it unless you have a reason to re-query.

**EF Change Tracking trap.** If you use EF's default change tracking and mutate `args.Record.Value` before calling `SaveChangesAsync`, EF sees the change and writes it. If you accidentally call `Rebind()` before `SaveChangesAsync`, the mutation is still in memory but not in the database — a subsequent re-query will show the stale value. Always `SaveChangesAsync` before re-querying.

## OnVisibleRangeChanged Event

The AllocationScheduler fires `OnVisibleRangeChanged` when the user scrolls, zooms, or calls a navigation method — this is related but distinct from data refresh.

```razor
<SunfishAllocationScheduler OnVisibleRangeChanged="@HandleRangeChanged" ... />

@code {
    private async Task HandleRangeChanged(VisibleRangeChangedArgs args)
    {
        // args.NewStart, args.NewEnd, args.ViewGrain
        var fresh = await _api.GetAllocationsAsync(args.NewStart, args.NewEnd);
        _allocations = fresh.ToList();
        // OnParametersSet will pick up the new reference automatically.
    }
}
```

Use this event to implement **windowed server loading** — fetch only the visible range from the server, re-fetch when the user scrolls outside it.

## Preventing Rebind Cycles

A common mistake is calling `Rebind()` inside an edit handler that also mutates the data source:

```csharp
private async Task HandleCellEdited(CellEditedArgs args)
{
    args.Record.Value = args.NewValue;        // mutation
    await _scheduler.Rebind();                // unnecessary — OnCellEdited already caused a re-render
}
```

The component already re-renders after it raises `OnCellEdited` / `OnRangeEdited` — you do not need to call `Rebind()` from inside these handlers. Call `Rebind()` only when the data changes **outside** an event that the scheduler itself raised (server push, parallel job, external button click).

If you find yourself calling `Rebind()` from inside a handler, check whether it is actually needed. Unnecessary rebinds flash the UI and can discard transient state.

## Examples

### Explicit rebind after form save

```razor
<SunfishAllocationScheduler @ref="_scheduler" Resources="@_resources" Allocations="@_allocations" ... />
<EditForm Model="_draft" OnValidSubmit="SaveDraft">
    <!-- form fields -->
</EditForm>

@code {
    private async Task SaveDraft()
    {
        _allocations.Add(_draft.ToRecord());
        _draft = new();
        await _scheduler.Rebind();
    }
}
```

### Observable collection auto-refresh

```razor
<SunfishAllocationScheduler Resources="@_resources" Allocations="@_allocations" ... />

@code {
    private ObservableCollection<AllocationRecord> _allocations = new();

    protected override void OnInitialized()
    {
        _signalR.OnAllocationPushed += record => _allocations.Add(record);
    }
}
```

### Full dataset reload with visible-range-driven fetch

```razor
<SunfishAllocationScheduler Resources="@_resources"
                           Allocations="@_allocations"
                           OnVisibleRangeChanged="@LoadWindow" ... />

@code {
    private List<AllocationRecord> _allocations = new();

    private async Task LoadWindow(VisibleRangeChangedArgs args)
    {
        _allocations = (await _api.GetAllocationsAsync(args.NewStart, args.NewEnd)).ToList();
    }
}
```

## See Also

- [Data Binding](data-binding.md)
- [Editing](editing.md)
- [Events](events.md)
- [Business Objects](allocation-scheduler-business-objects.md)
