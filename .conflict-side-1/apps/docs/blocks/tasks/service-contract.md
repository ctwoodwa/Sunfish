---
uid: block-tasks-service-contract
title: Tasks — Service Contract
description: Public surface of TaskBoardBlock and how callers supply task data without a service layer.
keywords:
  - tasks
  - render-fragment
  - item-template
  - kanban
  - blazor
---

# Tasks — Service Contract

## Overview

`blocks-tasks` does not ship an `ITaskService` interface or any injected service. Consumers pass the data straight into `TaskBoardBlock` through a `[Parameter]`, and any state-machine work is done in caller code using `TaskBoardState`. This page is the honest description of that surface.

## TaskBoardBlock parameters

```razor
<TaskBoardBlock Items="@tasks" ItemTemplate="@CardTemplate" />
```

| Parameter | Type | Required | Purpose |
|---|---|---|---|
| `Items` | `IReadOnlyList<TaskItem>` | yes (`[EditorRequired]`) | The tasks to display, grouped internally by `Status`. |
| `ItemTemplate` | `RenderFragment<TaskItem>?` | no | Optional custom card rendering. When `null`, a default card with title and optional assignee is rendered. |

`TaskBoardBlock` is purely a presentation component. It:

- Iterates `Enum.GetValues<TaskStatus>()` to lay out four columns.
- Filters `Items` by status for each column.
- Renders either the caller's `ItemTemplate` or a default inline card per item.

There is no click handler, no drag-and-drop, no selection model, and no service dependency.

## TaskItem

```csharp
public sealed record TaskItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required TaskStatus Status { get; init; }
    public string? Assignee { get; init; }
    public DateTime? DueDateUtc { get; init; }
}
```

The record is intentionally thin. Consumers who need richer task metadata have two options:

1. Use `TaskItem` directly and carry auxiliary data out-of-band (e.g. a sidecar dictionary keyed by `Id`).
2. Wrap their own domain model and map to `TaskItem` only at the presentation boundary, using a custom `ItemTemplate` to render richer cards.

## Typical workflow

```csharp
private IReadOnlyList<TaskItem> _tasks = new[]
{
    new TaskItem { Id = "1", Title = "Draft spec", Status = TaskStatus.Backlog },
    new TaskItem { Id = "2", Title = "Review PR", Status = TaskStatus.InProgress, Assignee = "alice" },
};

private readonly TaskBoardState _state = new();

private void MoveForward(TaskItem task)
{
    var target = task.Status switch
    {
        TaskStatus.Backlog    => TaskStatus.Todo,
        TaskStatus.Todo       => TaskStatus.InProgress,
        TaskStatus.InProgress => TaskStatus.Done,
        _ => task.Status,
    };

    if (_state.TryTransition(task, target, out var updated))
    {
        _tasks = _tasks.Select(t => t.Id == task.Id ? updated : t).ToArray();
    }
}
```

```razor
<TaskBoardBlock Items="_tasks">
    <ItemTemplate Context="task">
        <div class="sf-task-card">
            <div>@task.Title</div>
            @if (task.DueDateUtc is { } due)
            {
                <small>Due @due:d</small>
            }
            <button type="button" @onclick="() => MoveForward(task)">Advance</button>
        </div>
    </ItemTemplate>
</TaskBoardBlock>
```

## Deferred: drag-and-drop

Because the block is read-display, there is no drag-and-drop UI. A future pass would likely add an `OnTransition` callback on `TaskBoardBlock` that invokes `TaskBoardState.TryTransition` internally and surfaces the rejection back to the caller.

## Default card rendering

When `ItemTemplate` is not supplied, `TaskBoardBlock` renders a minimal card with the title and (if present) the assignee. The default is intentionally sparse — the block's visual contract is "a column-per-status layout with your tasks in the right places"; richer cards are consumer territory.

If you want to test the default path, verify with a `TaskItem` that has no `ItemTemplate` override and assert against the rendered markup — `tests/TaskBoardBlockTests.cs` has fixtures for this.

## bUnit testing

Because `TaskBoardBlock` is a pure Razor component with no DI surface, it is trivially testable under bUnit:

```csharp
using Bunit;
using Sunfish.Blocks.Tasks;
using Sunfish.Blocks.Tasks.Models;
using Xunit;

public class TaskBoardBlockTests : TestContext
{
    [Fact]
    public void RendersFourColumns()
    {
        var cut = RenderComponent<TaskBoardBlock>(parameters => parameters
            .Add(p => p.Items, Array.Empty<TaskItem>()));

        // The board renders one column per TaskStatus value (4).
        Assert.Equal(4, cut.FindAll(".sf-task-board__column").Count);
    }

    [Fact]
    public void GroupsItemsByStatus()
    {
        var items = new[]
        {
            new TaskItem { Id = "1", Title = "A", Status = TaskStatus.Backlog },
            new TaskItem { Id = "2", Title = "B", Status = TaskStatus.Backlog },
            new TaskItem { Id = "3", Title = "C", Status = TaskStatus.Done },
        };

        var cut = RenderComponent<TaskBoardBlock>(parameters => parameters
            .Add(p => p.Items, items));

        // Two cards should appear under the Backlog column.
        Assert.Equal(2, cut.FindAll(".sf-task-board__column--backlog .sf-task-card").Count);
    }
}
```

## Wrapping your own domain model

Teams that already have a richer "work item" model in their app can still use the block by mapping at the presentation boundary:

```csharp
private IReadOnlyList<TaskItem> _display =>
    _workItems.Select(w => new TaskItem
    {
        Id       = w.Id.ToString(),
        Title    = $"{w.Priority}: {w.Summary}",
        Status   = MapStatus(w.Lifecycle),
        Assignee = w.AssignedTo?.DisplayName,
        DueDateUtc = w.DueAt,
    }).ToArray();

private static TaskStatus MapStatus(WorkItemLifecycle lc) => lc switch
{
    WorkItemLifecycle.New        => TaskStatus.Backlog,
    WorkItemLifecycle.Queued     => TaskStatus.Todo,
    WorkItemLifecycle.Active     => TaskStatus.InProgress,
    WorkItemLifecycle.Done       => TaskStatus.Done,
    _ => TaskStatus.Backlog,
};
```

Render the block with the projected `_display` and the `ItemTemplate` can still access the original work-item via a sidecar lookup if it needs extra fields.

## Related

- [Overview](overview.md)
- [State Machine](state-machine.md)
