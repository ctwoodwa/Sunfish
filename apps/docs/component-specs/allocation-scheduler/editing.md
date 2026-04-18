---
title: Editing
page_title: AllocationScheduler Editing
description: Cell-level editing in the Sunfish Blazor AllocationScheduler — authoritative level, edit modes, bulk edit, drag-fill, distribution, shift, move, events, and the controlled-component contract.
slug: allocation-scheduler-editing
tags: sunfish,blazor,allocation-scheduler,editing,cell,bulk,distribute,shift,move,controlled
published: True
position: 23
components: ["allocation-scheduler"]
---

# AllocationScheduler Editing

The AllocationScheduler supports cell-level editing of allocation values — typed input, bulk range edits, drag-fill, clipboard paste, and advanced commands (distribute, shift, move) invoked from the context menu. This page documents the full editing surface: parameters, events, edit modes, and the controlled-component contract that consumer code must honor.

This document covers *how* editing works. For the design rationale behind the single-writable-grain rule, see [Editing Grain Design Decision](editing-grain.md).

## Basics

The AllocationScheduler is a **cell-level editor** — editing acts on individual `AllocationRecord` cells, not on whole rows or appointments. The `AuthoritativeLevel` parameter picks which time granularity is editable; coarser and finer grains are read-only rollups.

```razor
<SunfishAllocationScheduler Resources="@resources"
                           Allocations="@allocations"
                           AuthoritativeLevel="TimeGranularity.Week"
                           ViewGrain="TimeGranularity.Week"
                           OnCellEdited="@HandleCellEdited"
                           OnRangeEdited="@HandleRangeEdited" />
```

A cell is editable when **all** of the following are true:

1. `AllowKeyboardEdit` is `true` (or the user is using mouse-only interactions: double-click, drag-fill, context menu).
2. The cell's bucket granularity equals `AuthoritativeLevel` — OR — `AllowZoomEdit` is `true` and the cell is at a coarser grain.
3. The cell is not flagged read-only by a `CellTemplate` or by a consumer-side disable rule.
4. `_isDisabled` is `false` for the component as a whole.

## Controlled-Component Contract

> **The AllocationScheduler is a fully controlled component.** It fires `OnCellEdited` / `OnRangeEdited` / `OnDistributeRequested` / `OnShiftValues` / `OnMoveValues` when the user commits a change, but it **never** mutates its own `Allocations` data. Consumers must update their data source in the event handler for visual changes to appear.

This is a deliberate design choice so that:

- Server-side persistence is host code, not component code.
- Validation (reject an edit) is host code.
- Optimistic updates, conflict resolution, undo/redo — all host code.
- The component has no hidden state that diverges from the consumer's data.

If you wire up an event handler that only prints to the console, the user will see their edit briefly but the cell will revert on the next render because the underlying `AllocationRecord.Value` never changed. Always mutate (or replace) the record in your handler.

```csharp
private async Task HandleCellEdited(CellEditedArgs args)
{
    args.Record.Value = args.NewValue;       // mutate
    await _repository.SaveAsync(args.Record); // persist
    // No Rebind() needed — the component already re-rendered.
}
```

## Authoritative Level

`AuthoritativeLevel` is **required**. It picks one grain from the `TimeGranularity` enum (`Day`, `Week`, `Month`, `Quarter`, `Year`) as the single writable grain. Other grains are read-only rollups computed by summing or averaging the authoritative grain's cells.

Example: `AuthoritativeLevel="TimeGranularity.Week"` means weekly cells are editable; daily cells show a computed-per-day rollup (read-only); monthly cells show the sum of the four or five weeks in the month (read-only).

**Why one grain?** Multi-grain editing leads to distribution ambiguity — if you edit the month and the week at the same time, which wins? The design rationale is in [Editing Grain Design Decision](editing-grain.md). Option A (single authoritative grain) was chosen because it makes data flow unambiguous and debuggable.

## Zoom Editing

Set `AllowZoomEdit="true"` to allow editing at coarser grains than `AuthoritativeLevel`. When a user edits a higher-level cell (e.g. a month), the component does NOT write directly — it fires `OnDistributeRequested` with a proposed distribution computed from `DefaultDistributionMode`. The consumer can accept, modify, or cancel.

```razor
<SunfishAllocationScheduler AuthoritativeLevel="TimeGranularity.Week"
                           AllowZoomEdit="true"
                           DefaultDistributionMode="DistributionMode.ProportionalToExisting"
                           OnDistributeRequested="@HandleDistribute" />

@code {
    private async Task HandleDistribute(DistributeArgs args)
    {
        if (args.ProposedDistribution.Any(r => r.Value < 0))
        {
            args.IsCancelled = true;
            return;
        }
        foreach (var r in args.ProposedDistribution)
        {
            var existing = _allocations.FirstOrDefault(/* match */);
            if (existing != null) existing.Value = r.Value;
            else _allocations.Add(r);
        }
    }
}
```

`AllowZoomEdit` is `false` by default. Enable it only when your users understand that editing a month-cell means rewriting four or five week-cells, and you have a distribution policy that matches their mental model.

## Edit Modes (How Users Enter Edit State)

The AllocationScheduler does not expose an "edit mode" enum (like `GridEditMode.InCell`/`Inline`/`Popup`). Instead, it supports multiple **entry mechanisms**, all of which lead to in-cell editing:

| Entry mechanism | Trigger | Behavior |
|---|---|---|
| **Typed input** | Any printable character while a cell is focused | Enters edit mode; replaces cell value with the typed character. Fires `OnStartTyping` internally. |
| **F2 / Enter** | `F2` or `Enter` key on a focused cell | Enters edit mode; seeds editor with current value. Fires `OnEnterEditMode` internally. |
| **Double-click** | Double-click on a cell | Enters edit mode (same as F2). |
| **Drag-fill** | Click the fill handle on the active cell and drag | Does NOT enter in-place edit; target cells are highlighted with an inset box-shadow preview. On release, the component computes fill values and fires `OnRangeEdited` with the resolved records. <!-- pending R10: preview will change to dashed outline --> |
| **Bulk selection + type** | Select a range, then type | Enters edit mode on the active cell within the range; on commit, fires `OnRangeEdited` (not `OnCellEdited`) when `AllowBulkEdit` is `true`. |
| **Paste** | `Ctrl+V` with TSV on clipboard | Parses the TSV and fires `OnPasteData`, which converts to `OnRangeEdited`. No in-place edit state. |
| **Context menu commands** | Right-click → Distribute / Shift / Move / Set Target | Fires the corresponding event (`OnDistributeRequested`, `OnShiftValues`, `OnMoveValues`, `OnTargetChanged`). No in-place edit state. |
| **Delete / Backspace** | `Delete` or `Backspace` on a focused cell | Does NOT enter edit mode; commits `CellEditedArgs` with `NewValue = 0m`. |

Inside edit mode, the editor is a plain HTML input styled to match the cell. Commit and cancel follow the keyboard contract in [keyboard-navigation.md](keyboard-navigation.md).

## Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `AuthoritativeLevel` | `TimeGranularity` | *(required)* | The single grain at which cells are editable. See [editing-grain.md](editing-grain.md). |
| `AllowKeyboardEdit` | `bool` | `true` | Master switch for keyboard-driven editing (F2, Enter, typing, Delete, paste). |
| `AllowDragFill` | `bool` | `true` | Enables the fill handle on the active cell and the drag-fill gesture. |
| `AllowBulkEdit` | `bool` | `true` | When `true`, typing or pasting into a selected range applies the value to every selected cell and fires `OnRangeEdited`. When `false`, only the active cell is edited (fires `OnCellEdited`). |
| `AllowZoomEdit` | `bool` | `false` | When `true`, cells at grains coarser than `AuthoritativeLevel` become editable via `OnDistributeRequested`. |
| `DefaultDistributionMode` | `DistributionMode` | `EvenSpread` | Default policy for distributing a higher-level value into authoritative-grain cells. Applied when the user edits at zoom and does not pick a policy explicitly. |
| `ValueMode` | `AllocationValueMode` | `Hours` | Display format — `Hours` renders decimals; `Currency` renders with the current culture's currency symbol. Does not affect `Value` type (always `decimal`). |
| `EnableContextMenu` | `bool` | `true` | Shows the right-click context menu with the built-in Distribute / Shift / Move / Set Target / Clear commands. |
| `ContextMenuItems` | `IEnumerable<AllocationMenuDescriptor>?` | `null` | Custom commands appended to the built-in menu. See [context-menu.md](context-menu.md). |

## Events

### OnCellEdited

Fires when a single cell value is committed (typed, F2+Enter, Delete-to-zero). Does NOT fire for bulk range edits — those fire `OnRangeEdited` instead.

| Property | Type | Description |
|---|---|---|
| `ResourceKey` | `object` | The resource row key. |
| `TaskId` | `object` | The task key (may be `null` for resource-only grids). |
| `BucketStart` | `DateTime` | Inclusive start of the bucket. |
| `BucketEnd` | `DateTime` | Exclusive end of the bucket. |
| `OldValue` | `decimal` | Value before the edit. |
| `NewValue` | `decimal` | Value the user committed. |
| `Record` | `AllocationRecord` | The record to mutate. May be a new record if no existing cell covered the bucket (consumer must add it to the collection). |

```csharp
private async Task HandleCellEdited(CellEditedArgs args)
{
    args.Record.Value = args.NewValue;
    if (!_allocations.Contains(args.Record))
        _allocations.Add(args.Record);
    await _repository.SaveAsync(args.Record);
}
```

### OnRangeEdited

Fires when a range of cells is edited in one operation — bulk typing with `AllowBulkEdit`, drag-fill, clipboard paste.

| Property | Type | Description |
|---|---|---|
| `AffectedRecords` | `IReadOnlyList<AllocationRecord>` | One record per affected cell, with `Value` already set to the new value. |
| `Value` | `decimal` | The value applied to all cells (only meaningful for bulk typing and drag-fill — for paste, individual records may differ). |

```csharp
private async Task HandleRangeEdited(RangeEditedArgs args)
{
    foreach (var record in args.AffectedRecords)
    {
        var existing = _allocations.FirstOrDefault(/* match */);
        if (existing != null) existing.Value = record.Value;
        else _allocations.Add(record);
    }
    await _repository.SaveBatchAsync(args.AffectedRecords);
}
```

### OnDistributeRequested

Fires when the user edits a cell at a grain coarser than `AuthoritativeLevel` (with `AllowZoomEdit="true"`) or invokes a Distribute command from the context menu. The consumer can inspect, modify, or cancel the proposed distribution.

| Property | Type | Description |
|---|---|---|
| `SourcePeriod` | `DateRange` | The period the user edited (e.g. a month). |
| `TargetValue` | `decimal` | The value the user typed / chose for the source period. |
| `TargetGranularity` | `TimeGranularity` | The grain the distribution writes into (always `AuthoritativeLevel`). |
| `Mode` | `DistributionMode` | The policy used to compute the proposed records. Starts as `DefaultDistributionMode` but can be overridden by the user via a context-menu sub-command. |
| `ProposedDistribution` | `IReadOnlyList<AllocationRecord>` | The records the component would write if the event is not cancelled. The consumer may mutate the list (add, remove, change values) before the event returns. |
| `IsCancelled` | `bool` | Set to `true` to cancel the write. |

The component does NOT write the records itself — after the event, the consumer is expected to add/update them in `_allocations` just like for `OnCellEdited`.

### OnShiftValues

Fires when the user invokes Shift Forward or Shift Backward from the context menu. Moves the values of the selected range forward or backward in time by N periods.

| Property | Type | Description |
|---|---|---|
| `ResourceKey` | `object` | The anchor resource. |
| `TaskId` | `object` | The anchor task. |
| `Direction` | `int` | `+1` for forward, `-1` for backward. |
| `Periods` | `int` | Number of buckets to shift. |
| `AffectedRecords` | `IReadOnlyList<AllocationRecord>` | The records to update — both the emptied source cells and the filled destination cells. |

### OnMoveValues

Fires when the user drags a range to a different resource row or invokes Move from the context menu. Moves values from one resource/task to another.

| Property | Type | Description |
|---|---|---|
| `SourceResourceKey` | `object` | The resource the values came from. |
| `TargetResourceKey` | `object` | The resource the values are going to. |
| `SourceTaskId` | `object?` | The task the values came from. |
| `TargetTaskId` | `object?` | The task the values are going to. |
| `AffectedRecords` | `IReadOnlyList<AllocationRecord>` | Records for both the emptied source and the filled destination. |

### OnTargetChanged

Fires when a target (desired-total) value is set or updated via the context menu. See [analysis-targets.md](analysis-targets.md) for how targets surface in the UI.

| Property | Type | Description |
|---|---|---|
| `ResourceKey` | `object` | The resource the target applies to. |
| `TaskId` | `object?` | The task the target applies to. |
| `Period` | `DateRange` | The period the target applies to. |
| `TargetValue` | `decimal` | The new desired total. |

### OnContextMenuAction

Fires when a context-menu command is invoked. For built-in commands (Distribute, Shift, Move, Set Target, Clear), this fires **before** the specialized event (`OnDistributeRequested`, etc.) and can cancel them. For custom commands from `ContextMenuItems`, this is the only event that fires.

| Property | Type | Description |
|---|---|---|
| `CommandName` | `string` | The command identifier (built-in: `distribute`, `shift-forward`, `shift-back`, `move`, `set-target`, `clear`; custom: whatever you set in `AllocationMenuDescriptor.CommandName`). |
| `TargetCells` | `IReadOnlyList<AllocationCellRef>` | The cells the command operates on (the current selection, or the right-clicked cell if nothing was selected). |
| `IsCancelled` | `bool` | Set to `true` to prevent the command from running. |

## Distribution Modes

`DistributionMode` controls how a coarser-grain value is spread across finer-grain (authoritative) cells.

| Value | Behavior |
|---|---|
| `EvenSpread` | Value / N per cell. Simplest. |
| `ProportionalToExisting` | Each cell gets `existing * targetTotal / sum(existing)`. Preserves the existing shape. If all existing values are zero, falls back to `EvenSpread`. |
| `FrontLoaded` | Heavier weight on earlier cells (linear decay from 2/N toward 0). |
| `BackLoaded` | Heavier weight on later cells (linear growth from 0 toward 2/N). |
| `WorkingDaysWeighted` | Only working days get a share. Zero-weighted on weekends and holidays. Requires consumer to provide a working-day calendar — if none is provided, behaves like `EvenSpread`. |
| `Custom` | The consumer supplies the distribution via `OnDistributeRequested` — the component emits a proposed distribution using `EvenSpread`, which the consumer replaces before the event returns. |

## Column Width and Layout

When `TimeColumnWidth` is not set, the timeline table uses fluid column sizing: each time column is sized via `calc(100% / N)` where N is the number of visible buckets. This ensures columns fill the available width evenly regardless of view grain. At Month grain with 3 months visible, each column gets ~33% of the timeline pane width; at Week grain with 12 weeks visible, each gets ~8.3%.

When `TimeColumnWidth` is set to a pixel value, columns use that fixed width and the timeline pane scrolls horizontally if the total exceeds the pane width. Users can resize individual columns when `AllowTimeColumnResize` is `true`, and double-click a header border to auto-fit to content width when `AutoFitOnDoubleClick` is `true`. Column resize events fire via `OnTimeColumnResized`.

## Validation

The AllocationScheduler does not enforce validation rules on committed values. Validation happens in the consumer's event handler — reject the edit by mutating `args.NewValue` before setting it on the record, or by ignoring the event entirely.

```csharp
private async Task HandleCellEdited(CellEditedArgs args)
{
    if (args.NewValue < 0 || args.NewValue > 40)
    {
        // reject — revert to old value
        args.Record.Value = args.OldValue;
        _toast.Show("Value must be between 0 and 40 hours.");
        return;
    }
    args.Record.Value = args.NewValue;
    await _repository.SaveAsync(args.Record);
}
```

Because the component re-renders from `_allocations` after every event, setting the record back to its old value visually reverts the edit.

## Integration with Other Features

**Selection.** Bulk editing operates on the current selection. See [selection.md](selection.md).

**Keyboard navigation.** Edit mode entry and commit are driven by keyboard shortcuts documented in [keyboard-navigation.md](keyboard-navigation.md).

**Context menu.** Advanced edit commands (Distribute, Shift, Move, Set Target, Clear) live in the right-click menu. See [context-menu.md](context-menu.md).

**Templates.** Cell templates (`CellTemplate`) render on top of the editing layer. A template can show computed visuals (icons, colors, tooltips) but does not interfere with edit entry. See [templates.md](templates.md).

**Data binding.** After an edit, the consumer is expected to mutate or replace records in `_allocations`. The component picks up the change on the next render — no explicit `Rebind()` is needed inside the handler. See [refresh-data.md](refresh-data.md).

**Events surface.** The full list of event payloads is also enumerated in [events.md](events.md).

## Examples

### Basic single-cell editing with persistence

```razor
<SunfishAllocationScheduler Resources="@_resources"
                           Allocations="@_allocations"
                           AuthoritativeLevel="TimeGranularity.Week"
                           OnCellEdited="@CommitEdit" />

@code {
    private async Task CommitEdit(CellEditedArgs args)
    {
        args.Record.Value = args.NewValue;
        if (!_allocations.Contains(args.Record)) _allocations.Add(args.Record);
        await _repository.SaveAsync(args.Record);
    }
}
```

### Bulk range editing with batch persistence

```razor
<SunfishAllocationScheduler Resources="@_resources"
                           Allocations="@_allocations"
                           AuthoritativeLevel="TimeGranularity.Week"
                           AllowBulkEdit="true"
                           OnRangeEdited="@CommitRange" />

@code {
    private async Task CommitRange(RangeEditedArgs args)
    {
        foreach (var r in args.AffectedRecords)
        {
            var existing = _allocations.FirstOrDefault(x =>
                x.ResourceKey == r.ResourceKey &&
                x.BucketStart == r.BucketStart);
            if (existing != null) existing.Value = r.Value;
            else _allocations.Add(r);
        }
        await _repository.SaveBatchAsync(args.AffectedRecords);
    }
}
```

### Zoom editing with proportional distribution

```razor
<SunfishAllocationScheduler AuthoritativeLevel="TimeGranularity.Week"
                           AllowZoomEdit="true"
                           DefaultDistributionMode="DistributionMode.ProportionalToExisting"
                           OnDistributeRequested="@AcceptDistribution" />

@code {
    private void AcceptDistribution(DistributeArgs args)
    {
        foreach (var r in args.ProposedDistribution)
        {
            var existing = _allocations.FirstOrDefault(/* match */);
            if (existing != null) existing.Value = r.Value;
            else _allocations.Add(r);
        }
    }
}
```

### Rejecting an edit

```csharp
private void RejectNegatives(CellEditedArgs args)
{
    if (args.NewValue < 0)
    {
        args.Record.Value = args.OldValue; // revert
        return;
    }
    args.Record.Value = args.NewValue;
}
```

## See Also

- [Editing Grain Design Decision](editing-grain.md) — design rationale for the single-authoritative-grain rule.
- [Selection](selection.md)
- [Keyboard Navigation](keyboard-navigation.md)
- [Context Menu](context-menu.md)
- [Refresh Data](refresh-data.md)
- [Events](events.md)
- [Analysis and Targets](analysis-targets.md)
