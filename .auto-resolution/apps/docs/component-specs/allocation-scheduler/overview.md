---
title: Overview
page_title: AllocationScheduler Overview
description: Overview of the AllocationScheduler component for Blazor - a resource-centric planning component for assigning task-linked hours or budget across a navigable timeline.
slug: allocation-scheduler-overview
tags: sunfish,blazor,allocation-scheduler,overview,resource-planning,gantt
published: True
position: 0
components: ["allocation-scheduler"]
---

# Blazor AllocationScheduler Overview

The `SunfishAllocationScheduler` is a resource-centric planning component that combines configurable resource metadata columns on the left with editable time-bucket allocation cells on the right. Users assign, redistribute, and analyze task-linked hours or currency values across a navigable timeline with spreadsheet-style editing.

This component occupies its own complexity tier alongside `SunfishScheduler` and `SunfishGantt`. It is not a mode or view of the existing Scheduler — it is a distinct planning surface designed for staffing, budgeting, and allocation workflows.


## Component Description

A hybrid scheduler component for planning resource assignments over time, using a table-based layout with configurable date granularity and spreadsheet-like bulk editing for task-based hours or cost allocations.

The primary scenario is a project manager who adds a set of resources (people, roles, or teams) to a project, configures a weekly display range defaulting to today plus three months, loads budgeted hours or dollars per resource and task into each visible week, and then edits, redistributes, and analyzes those values as the plan evolves.


## Creating a Blazor AllocationScheduler

1. Use the `<SunfishAllocationScheduler>` tag.
2. Bind `Resources` to your list of schedulable entities.
3. Bind `Allocations` to your list of allocation records.
4. Set `AuthoritativeLevel` to the editing granularity — typically `TimeGranularity.Week` or `TimeGranularity.Month`.
5. Optionally set `ViewGrain`, `VisibleStart`, and `ValueMode`.
6. Define `<AllocationResourceColumns>` child tags to configure the left-side columns.

>caption Basic AllocationScheduler — weekly staffing

````RAZOR
<SunfishAllocationScheduler Resources="@Team"
                             Allocations="@Plan"
                             AuthoritativeLevel="TimeGranularity.Week"
                             ViewGrain="TimeGranularity.Week"
                             ValueMode="AllocationValueMode.Hours"
                             VisibleStart="@Today"
                             DefaultRangeLength="3"
                             DefaultRangeUnit="TimeGranularity.Month"
                             OnCellEdited="@HandleCellEdit">
    <AllocationResourceColumns>
        <AllocationResourceColumn Field="Name" Title="Resource" Width="200px" />
        <AllocationResourceColumn Field="Role" Title="Role" Width="150px" />
        <AllocationResourceColumn Field="Department" Title="Dept" Width="120px" />
    </AllocationResourceColumns>
</SunfishAllocationScheduler>

@code {
    private DateTime Today { get; set; } = DateTime.Today;

    private List<StaffResource> Team { get; set; } = new()
    {
        new StaffResource { Id = 1, Name = "Alice Chen",  Role = "Dev",     Department = "Engineering" },
        new StaffResource { Id = 2, Name = "Bob Torres",  Role = "Dev",     Department = "Engineering" },
        new StaffResource { Id = 3, Name = "Carol Singh", Role = "QA",      Department = "Engineering" },
        new StaffResource { Id = 4, Name = "David Kim",   Role = "Manager", Department = "PMO"        }
    };

    private List<AllocationRecord> Plan { get; set; } = new()
    {
        new AllocationRecord
        {
            ResourceId   = 1,
            TaskId       = 101,
            TaskName     = "Backend API",
            BucketStart  = new DateTime(2026, 4, 6),
            BucketEnd    = new DateTime(2026, 4, 12),
            Value        = 32,
            Unit         = AllocationUnit.Hours
        }
    };

    private async Task HandleCellEdit(CellEditedArgs args)
    {
        // persist change to your data source
        await SaveAsync(args.Record);
    }

    public class StaffResource
    {
        public int    Id         { get; set; }
        public string Name       { get; set; }
        public string Role       { get; set; }
        public string Department { get; set; }
    }
}
````


## Domain Model

AllocationScheduler is modeled around four primary entities.

| Entity | Purpose |
| --- | --- |
| `Resource` | The schedulable entity. Can be a person, role, team, machine, or vendor. Drives the left-side resource columns. |
| `Task` | The work item receiving the allocation. A resource can have multiple tasks. |
| `AllocationRecord` | A single value record: one resource, one task, one time bucket. This is the authoritative, persisted unit of data. |
| `AllocationTarget` | A desired total value for a resource, task, or period. Stored separately; used for delta analysis. |

A key rule: **hours or dollars are never standalone cell values**. Every value belongs to an `AllocationRecord` that carries `ResourceId`, `TaskId`, `BucketStart`, `BucketEnd`, `Value`, and `Unit`. This ensures all operations — shift, move, spread, analyze — have a reliable, auditable record to act on.


## Editing Grain (AuthoritativeLevel)

The most important configuration decision is `AuthoritativeLevel`. This sets the one time granularity at which cell editing is enabled. All coarser zoom levels display read-only aggregate sums. All finer zoom levels show stored authoritative values, not recalculated breakdowns.

See [Editing Grain Design Decision](slug:allocation-scheduler-editing-grain) for full rationale, the Option A vs Option B analysis, and how distribution commands work when a user wants to operate at a coarser level.

> **Design decision: Option A is the baseline.** `AuthoritativeLevel` is a required parameter. Only cells matching the authoritative grain are directly editable. Coarser buckets are read-only rollups. Distribution commands in the context menu let users operate on higher-level periods without bypassing this rule.


## Feature Areas

### Timeline Surface

The right side of the component renders time-bucket columns for the configured view grain. Users can navigate forward and backward, and the visible date range updates accordingly.

- View granularities: `Day`, `Week`, `Month`, `Quarter`, `Year`.
- Configurable default start and range length (e.g., today + 3 months).
- Forward/back navigation via toolbar or programmatic `NavigateTo(DateTime)`.
- Today jump.


### Resource Grid

The left side renders configurable columns for the resource entity.

- `<AllocationResourceColumn>` child tags define fields, titles, widths, and templates.
- Columns support show/hide, resize, reorder, and pinning.
- Optional sorting and filtering on resource fields.
- Optional row hierarchy for grouped or nested resources.


### Splitter and Dual-Pane Layout

The AllocationScheduler uses a draggable vertical splitter to divide the component into a left resource-grid pane and a right timeline pane, modelled on the Microsoft Project Gantt Chart interaction. Users drag the splitter to redistribute horizontal space between resource metadata columns and the navigable timeline surface. The splitter supports keyboard operation, full pane collapse and restore, and programmatic control via `SetSplitterPosition`, `CollapseSplitter`, and `RestoreSplitter` methods.

The left pane width always equals the sum of its rendered column widths; there is never blank space between the last column and the splitter handle.

See [Splitter and Dual-Pane Layout](slug:allocation-scheduler-splitter-layout).


### Current-Period Column Highlight

The timeline automatically highlights the column corresponding to the current date period. Both the header cell and the body cells for the current bucket receive the `mar-allocation-scheduler__col-current` CSS class. The FluentUI provider renders this with a subtle primary-tinted background, bold header text, and a 2px top accent border so the "today" column is immediately identifiable.

### Allocation Layer

Each resource row can show one or more task layers stacked vertically. Each layer corresponds to one task and renders its allocation values across visible buckets.

- Hours or currency display (`ValueMode`).
- Row totals and period totals.
- Over/under allocation indicators.
- Optional holiday and non-working-time shading.


### Editing

Editing applies only to cells at the `AuthoritativeLevel` zoom.

- **Single-cell edit**: click a cell and type.
- **Drag-fill**: click-and-drag across cells to broadcast or increment values.
- **Keyboard traversal**: Tab moves right, Shift+Tab moves left, Enter moves down, Arrow keys navigate.
- **Range selection**: click and drag to select a rectangle; apply a bulk value.
- **Undo/redo**: built-in for cell-level and range-level edits.


### Context Menu

A right-click context menu surfaces transformation commands. The menu is available at both the authoritative level and at coarser rollup cells, with actions scoped appropriately.

See [Context Menu](slug:allocation-scheduler-context-menu) for the full built-in command list and extension model.

**Built-in commands:**

| Command | Scope |
| --- | --- |
| Set values for selected date range | Authoritative cells |
| Clear values for selected date range | Authoritative cells |
| Shift values forward | Task row, date range |
| Shift values backward | Task row, date range |
| Move values to another task | Task row |
| Move values to another resource | Resource row |
| Spread total evenly across selection | Date range |
| Distribute period total to sub-buckets | Coarser rollup cell |
| Set desired total for task | Task row |
| Set desired total for resource | Resource row |
| Show/hide delta from desired total | Global toggle |


### Analysis and Targets

- `AllocationTarget` records define desired totals for a task, resource, or period.
- Targets are overlays — they never mutate allocation records.
- Delta display shows actual vs target with configurable variance thresholds.
- Status coloring highlights over-allocated, under-allocated, and on-target states.

See [Analysis and Targets](slug:allocation-scheduler-analysis-targets) for parameters and demo scenarios.


## AllocationScheduler Parameters

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `Resources` | `IEnumerable<TResource>` | — | The collection of schedulable entities displayed in the resource grid. |
| `Allocations` | `IEnumerable<AllocationRecord>` | — | The collection of allocation records. Each record ties a resource, a task, a time bucket, and a value together. |
| `Targets` | `IEnumerable<AllocationTarget>` | `null` | Optional desired-total records for delta analysis. |
| `AuthoritativeLevel` | `TimeGranularity` | — | **Required.** The one granularity at which cells are editable. Coarser levels are read-only rollups. |
| `ViewGrain` | `TimeGranularity` | same as `AuthoritativeLevel` | The current display granularity. Supports two-way binding. |
| `VisibleStart` | `DateTime` | `DateTime.Today` | The start of the visible date range. Supports two-way binding. |
| `VisibleEnd` | `DateTime` | derived | The end of the visible date range. Supports two-way binding. |
| `DefaultRangeLength` | `int` | `3` | **Deprecated.** Use `MinVisibleColumns` with `DefaultRangeUnit` instead. `DefaultRangeLength` now acts as a minimum floor forwarded to `MinVisibleColumns`, not an exact count. |
| `DefaultRangeUnit` | `TimeGranularity` | `Month` | Unit for the default visible range length. |
| `ValueMode` | `AllocationValueMode` | `Hours` | Whether cells display hours or currency values. |
| `ShowJumpToDate` | `bool` | `true` | Shows a date-picker input in the toolbar so the user can navigate directly to any date without stepping forward/back. When `false`, only the forward/back/today buttons render. |
| `ShowTargets` | `bool` | `false` | Renders target overlay values alongside actuals. |
| `ShowDeltas` | `bool` | `false` | Renders variance between actuals and targets. |
| `DeltaDisplayMode` | `DeltaDisplayMode` | `Value` | Show deltas as absolute values, percentage, or status icons. |
| `AllowDragFill` | `bool` | `true` | Enables click-and-drag to fill a range of cells. |
| `AllowKeyboardEdit` | `bool` | `true` | Enables Tab/arrow keyboard editing model. |
| `AllowBulkEdit` | `bool` | `true` | Enables range selection and bulk value application. |
| `EnableContextMenu` | `bool` | `true` | Shows the right-click context menu. |
| `ContextMenuItems` | `IEnumerable<AllocationMenuDescriptor>` | `null` | Custom commands appended to the built-in context menu. |
| `SelectionMode` | `AllocationSelectionMode` | `Range` | `None`, `Cell`, or `Range`. |
| `DefaultDistributionMode` | `DistributionMode` | `EvenSpread` | Default policy used when distributing a higher-level value to sub-buckets. |
| `AllowZoomEdit` | `bool` | `false` | Advanced opt-in: allows direct editing at zoom levels above `AuthoritativeLevel` using a distribution policy. Not recommended for most use cases. |
| `Height` | `string` | — | A `height` style in any supported CSS unit. |
| `Width` | `string` | — | A `width` style in any supported CSS unit. |
| `Class` | `string` | — | Custom CSS class for the root element. |
| `SplitterPosition` | `double?` | `null` | Left-pane width in pixels. Two-way bindable. |
| `DefaultSplitterPosition` | `double?` | `null` | Initial left-pane width used for state restore. When `null` (default), the pane renders at the natural sum of `AllocationResourceColumn` widths. |
| `MinLeftPaneWidth` | `double` | derived | Derived from column `MinWidth` values. Read-only; control via column `AllowResize` and `MinWidth` settings. |
| `MinRightPaneWidth` | `double` | `300` | Minimum right (timeline) pane width in pixels. |
| `AllowSplitterCollapse` | `bool` | `false` | Enables full pane collapse past the minimum widths. |
| `SplitterCssClass` | `string` | `null` | Custom CSS class for the splitter handle element. |
| `ShowComparisonPanel` | `bool` | `false` | Toggles the scenario comparison panel for side-by-side baseline vs. scenario view. |
| `ShowCriticalPath` | `bool` | `false` | Highlights the critical allocation path across resources and tasks. |
| `EnableLoaderContainer` | `bool` | `true` | Shows a loading animation for operations over 600ms. |


## AllocationScheduler Events

| Event | Args Type | Description |
| --- | --- | --- |
| `OnCellEdited` | `CellEditedArgs` | Fires when a single cell value is committed. |
| `OnRangeEdited` | `RangeEditedArgs` | Fires when a bulk range edit is committed. |
| `OnContextMenuAction` | `ContextMenuActionArgs` | Fires when a built-in or custom context menu command is invoked. |
| `OnDistributeRequested` | `DistributeArgs` | Fires when a distribution command is initiated. Host can intercept and override the proposed distribution. |
| `OnShiftValues` | `ShiftValuesArgs` | Fires when a shift-forward or shift-backward command is confirmed. |
| `OnMoveValues` | `MoveValuesArgs` | Fires when a move-to-task or move-to-resource command is confirmed. |
| `OnTargetChanged` | `TargetChangedArgs` | Fires when a desired total is set or updated via the context menu. |
| `OnVisibleRangeChanged` | `VisibleRangeChangedArgs` | Fires when the user navigates to a different date range. |
| `OnSelectionChanged` | `SelectionChangedArgs` | Fires when the user changes the selected cell or range. |
| `CanExecuteAction` | `CanExecuteActionArgs` | Called before a context menu action is shown to allow enable/disable logic. |
| `SplitterPositionChanged` | `EventCallback<double>` | Fires when the user finishes dragging the splitter. |
| `OnSplitterCollapsed` | `EventCallback<SplitterSide>` | Fires when a pane is fully collapsed. |
| `OnSplitterRestored` | `EventCallback<double>` | Fires when a collapsed pane is restored. |


## AllocationScheduler Reference and Methods

Obtain a reference with `@ref` to call methods programmatically.

| Method | Return Type | Description |
| --- | --- | --- |
| `Rebind()` | `Task` | Refreshes the component by re-reading `Resources` and `Allocations`. |
| `Refresh()` | `Task` | Re-renders the component without re-reading data. |
| `NavigateTo(DateTime date)` | `Task` | Moves the visible range so that `date` is in view. |
| `NavigateForward()` | `Task` | Advances the visible range by one `ViewGrain` unit. |
| `NavigateBack()` | `Task` | Moves the visible range back by one `ViewGrain` unit. |
| `NavigateToToday()` | `Task` | Moves the visible range to center on today. |
| `GetSelectedCells()` | `IReadOnlyList<AllocationCellRef>` | Returns the current cell selection. |
| `ClearSelection()` | `Task` | Clears the current cell selection. |
| `SetSplitterPosition(double widthPx)` | `Task` | Moves the splitter to the given width, clamped to valid range. |
| `CollapseSplitter(SplitterSide side)` | `Task` | Fully collapses the specified pane (requires `AllowSplitterCollapse` = `true`). |
| `RestoreSplitter()` | `Task` | Restores the last collapsed pane to its prior or default width. |


## Enumerations

### TimeGranularity

```csharp
public enum TimeGranularity
{
    Day,
    Week,
    Month,
    Quarter,
    Year
}
```

### AllocationValueMode

```csharp
public enum AllocationValueMode
{
    Hours,
    Currency
}
```

### DistributionMode

```csharp
public enum DistributionMode
{
    EvenSpread,           // Divide total evenly across sub-buckets
    ProportionalToExisting, // Preserve relative weighting of existing values
    FrontLoaded,          // Weight toward start of period
    BackLoaded,           // Weight toward end of period
    WorkingDaysWeighted,  // Weight by working days in each sub-bucket
    Custom                // Consumer-supplied via OnDistributeRequested
}
```

### DeltaDisplayMode

```csharp
public enum DeltaDisplayMode
{
    Value,       // Show variance as an absolute value
    Percentage,  // Show variance as a percentage of the target
    StatusIcon   // Show a colour-coded icon (over / under / on-target)
}
```

### AllocationSelectionMode

```csharp
public enum AllocationSelectionMode
{
    None,
    Cell,
    Range
}
```


## Demo Scenarios

The following scenarios represent the primary coverage targets for AllocationScheduler examples and tests.

1. **Weekly project staffing** — Add 10 people to a project, assign hours by week, navigate across the next 3 months.
2. **Budget planning** — Switch to `ValueMode = Currency`, compare planned cost against target by task.
3. **Bulk adjustment** — Drag-fill hours across a selected date range for one task.
4. **Reallocation** — Move planned effort from one resource to another after a staffing change.
5. **Schedule shift** — Shift a task's allocations forward by two weeks using the context menu.
6. **Variance review** — Set desired totals, enable `ShowDeltas`, highlight over- and under-allocated states.
7. **Read-only rollup navigation** — Set `AuthoritativeLevel = Week`, zoom to Month view, confirm month cells are read-only sums.
8. **Distribution command** — Right-click a month rollup cell, choose Distribute to weeks, confirm stored weekly records match the chosen distribution policy.
9. **Splitter resize** — Drag the vertical divider to widen the resource grid, then collapse the timeline pane and confirm the restore zone appears.


## Next Steps

* [Editing Grain Design Decision](slug:allocation-scheduler-editing-grain)
* [Context Menu Commands](slug:allocation-scheduler-context-menu)
* [Analysis and Targets](slug:allocation-scheduler-analysis-targets)
* [AllocationScheduler Events](slug:allocation-scheduler-events)
* [AllocationScheduler Accessibility](slug:allocation-scheduler-accessibility)
* [Splitter and Dual-Pane Layout](slug:allocation-scheduler-splitter-layout)


## See Also

* [Blazor Scheduler Overview](slug:scheduler-overview)
* [AllocationScheduler API Reference](slug:Sunfish.Blazor.Components.SunfishAllocationScheduler-2)
