---
title: Context Menu
page_title: AllocationScheduler - Context Menu
description: Right-click context menu commands for the SunfishAllocationScheduler — built-in transformations and the extension model for custom commands.
slug: allocation-scheduler-context-menu
tags: sunfish,blazor,allocation-scheduler,context-menu,commands
published: True
position: 40
components: ["allocation-scheduler"]
---

# AllocationScheduler Context Menu

The `SunfishAllocationScheduler` exposes a right-click context menu that surfaces transformation commands scoped to the user's current selection. The menu is available at both the `AuthoritativeLevel` editing grain and at coarser rollup cells, with each command enabled or disabled based on whether its scope is valid for the current selection.

Enable the context menu with `EnableContextMenu="true"` (the default). Disable it to build a fully custom interaction surface on top of the component's events.

## Invocation

The menu opens when the user:

- Right-clicks on a cell, a row, or a column header.
- Presses the context-menu key on the keyboard while a cell or range is focused.
- Long-presses on a touch device.

The menu closes when the user picks a command, presses `Escape`, or clicks outside the menu surface.

## Built-in Commands

All built-in commands are listed below. Each command fires a typed event (see [AllocationScheduler Events](slug:allocation-scheduler-events)) that the host can handle to persist, intercept, or override the resulting mutation.

| Command                                  | Scope                   | Event raised           |
| ---------------------------------------- | ----------------------- | ---------------------- |
| Set values for selected date range       | Authoritative cells     | `OnRangeEdited`        |
| Clear values for selected date range     | Authoritative cells     | `OnRangeEdited`        |
| Shift values forward                     | Task row, date range    | `OnShiftValues`        |
| Shift values backward                    | Task row, date range    | `OnShiftValues`        |
| Move values to another task              | Task row                | `OnMoveValues`         |
| Move values to another resource          | Resource row            | `OnMoveValues`         |
| Spread total evenly across selection     | Date range              | `OnRangeEdited`        |
| Distribute period total to sub-buckets   | Coarser rollup cell     | `OnDistributeRequested`|
| Set desired total for task               | Task row                | `OnTargetChanged`      |
| Set desired total for resource           | Resource row            | `OnTargetChanged`      |
| Show/hide delta from desired total       | Global toggle           | — (client-only state)  |

### Enable / Disable Logic

Before showing each command, the component raises the `CanExecuteAction` event with a `CanExecuteActionArgs` payload describing the current selection, the resource, the task, and the target command. Set `args.Enabled = false` in your handler to grey out the command in the menu. This gives hosts full control over command availability without reimplementing the menu.

## Extension Model

Append custom commands to the built-in menu by passing an `IEnumerable<AllocationMenuDescriptor>` to the `ContextMenuItems` parameter.

```csharp
public class AllocationMenuDescriptor
{
    public string  Id        { get; set; }    // stable identifier
    public string  Title     { get; set; }    // label shown in the menu
    public string? Icon      { get; set; }    // optional icon name
    public string? Group     { get; set; }    // optional group header
    public bool    Separator { get; set; }    // render as a divider row
    public Func<CanExecuteActionArgs, bool>? CanExecute { get; set; }
    public EventCallback<ContextMenuActionArgs> OnInvoke { get; set; }
}
```

Custom commands appear below the built-in commands by default. Use the `Group` field to inject them into a named group, or set `Separator = true` to render a divider between built-in and custom groups.

## Example

```razor
<SunfishAllocationScheduler Resources="@Team"
                             Allocations="@Plan"
                             AuthoritativeLevel="TimeGranularity.Week"
                             ContextMenuItems="@CustomCommands"
                             OnContextMenuAction="@HandleCommand"
                             CanExecuteAction="@HandleCanExecute" />

@code {
    private List<AllocationMenuDescriptor> CustomCommands { get; set; } = new()
    {
        new AllocationMenuDescriptor
        {
            Id    = "normalize-to-target",
            Title = "Normalize to target",
            Icon  = "svg-icon-gauge",
            OnInvoke = EventCallback.Factory.Create<ContextMenuActionArgs>(
                this, NormalizeToTarget)
        }
    };

    private void HandleCanExecute(CanExecuteActionArgs args)
    {
        // Disable shift-backward before the visible range start.
        if (args.CommandId == "shift-backward"
            && args.Selection.First().BucketStart <= VisibleStart)
        {
            args.Enabled = false;
        }
    }

    private async Task HandleCommand(ContextMenuActionArgs args)
    {
        // Persist or log the transformation.
        await Audit.LogAsync(args);
    }

    private Task NormalizeToTarget(ContextMenuActionArgs args)
    {
        // Custom command implementation.
        return Task.CompletedTask;
    }
}
```

## See Also

- [AllocationScheduler Overview](slug:allocation-scheduler-overview)
- [AllocationScheduler Events](slug:allocation-scheduler-events)
- [Analysis and Targets](slug:allocation-scheduler-analysis-targets)
- [Editing Grain Design Decision](slug:allocation-scheduler-editing-grain)
