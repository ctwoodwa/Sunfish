---
uid: block-tasks-service-contract
title: Tasks — Service Contract
description: Public surface of TaskBoardBlock and how callers supply task data without a service layer.
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

## Related

- [Overview](overview.md)
- [State Machine](state-machine.md)
