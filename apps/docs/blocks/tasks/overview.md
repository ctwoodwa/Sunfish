---
uid: block-tasks-overview
title: Tasks — Overview
description: Introduction to the blocks-tasks package — a canonical Kanban-style task board with a built-in state machine.
keywords:
  - tasks
  - kanban
  - task-board
  - state-machine
  - lifecycle
---

# Tasks — Overview

## Overview

The `blocks-tasks` package provides a deliberately thin task-board building block: a canonical `TaskItem` record, a four-state lifecycle enum, a validating state machine for transitions, and a read-only Kanban `TaskBoardBlock` that groups items by status into columns. The block is intentionally small so it can be dropped into a kitchen-sink demo, a dashboard, or a composite page without dragging in a task-management product.

## Package path

`packages/blocks-tasks` — assembly `Sunfish.Blocks.Tasks`.

## When to use it

- You want a no-frills task board in a demo or dashboard with a fixed four-stage lifecycle (`Backlog`, `Todo`, `InProgress`, `Done`).
- You want a predictable state machine (`TaskBoardState.TryTransition`) that rejects invalid lifecycle moves without throwing.
- You are fine with the canonical `TaskItem` record or plan to wrap your own domain model via the block's `ItemTemplate` render fragment.

If you need custom columns, dependencies between tasks, subtasks, or drag-and-drop UI, this block is a starting point at best; plan to extend it or roll your own.

## Key types

- **`TaskItem`** — canonical record: `Id`, `Title`, `Status`, optional `Assignee`, optional `DueDateUtc`.
- **`TaskStatus`** — lifecycle enum: `Backlog`, `Todo`, `InProgress`, `Done`.
- **`TaskBoardState`** — state machine with `TryTransition(TaskItem item, TaskStatus target, out TaskItem updated)`.
- **`TaskBoardBlock`** — Blazor component that renders a four-column Kanban board; cards use an optional `ItemTemplate<TaskItem>`, otherwise a default card.

## DI wiring

No DI registration is required. `TaskBoardBlock` takes its data through `[Parameter] Items` and has no injected services; `TaskBoardState` is a plain class that callers instantiate directly.

## Status and deferred items

- Drag-and-drop between columns is deferred — the current block is read-display.
- The lifecycle is hard-coded. Consumer-defined statuses or a registry-based extension model is a known follow-up.
- There is no persistence or service contract — this block is purely a UI composition; consumers own the data and state machine.

## Where things live in the package

| Path (under `packages/blocks-tasks/`) | Purpose |
|---|---|
| `Models/TaskItem.cs` | Canonical task record (id, title, status, assignee, due date). |
| `Models/TaskStatus.cs` | Lifecycle enum — `Backlog`, `Todo`, `InProgress`, `Done`. |
| `State/TaskBoardState.cs` | Validating state machine (`TryTransition`). |
| `TaskBoardBlock.razor` | Read-display Kanban board. |
| `_Imports.razor` | Package-wide Blazor `using` directives. |
| `tests/TaskBoardBlockTests.cs` | bUnit component tests. |

## Minimal end-to-end example

```razor
@page "/tasks"
@using Sunfish.Blocks.Tasks
@using Sunfish.Blocks.Tasks.Models
@using Sunfish.Blocks.Tasks.State

<PageTitle>Tasks</PageTitle>

<TaskBoardBlock Items="_tasks">
    <ItemTemplate Context="task">
        <div class="sf-task-card">
            <div class="sf-task-card__title">@task.Title</div>
            @if (task.Assignee is { } who)
            {
                <div class="sf-task-card__assignee">@who</div>
            }
            <button type="button" @onclick="() => Advance(task)">Advance</button>
        </div>
    </ItemTemplate>
</TaskBoardBlock>

@code {
    private IReadOnlyList<TaskItem> _tasks = new[]
    {
        new TaskItem { Id = "1", Title = "Draft spec",    Status = TaskStatus.Backlog },
        new TaskItem { Id = "2", Title = "Review PR",     Status = TaskStatus.InProgress, Assignee = "alice" },
        new TaskItem { Id = "3", Title = "Ship release",  Status = TaskStatus.Todo },
    };

    private readonly TaskBoardState _state = new();

    private void Advance(TaskItem t)
    {
        var target = t.Status switch
        {
            TaskStatus.Backlog    => TaskStatus.Todo,
            TaskStatus.Todo       => TaskStatus.InProgress,
            TaskStatus.InProgress => TaskStatus.Done,
            _ => t.Status,
        };
        if (_state.TryTransition(t, target, out var next))
        {
            _tasks = _tasks.Select(x => x.Id == t.Id ? next : x).ToArray();
        }
    }
}
```

## Relationship to `blocks-workflow`

`TaskBoardState` looks superficially like a miniature `blocks-workflow` definition — both enforce transitions. The difference:

- **`blocks-tasks`** — hard-coded, no generic types, returns `bool` on invalid moves. Optimised for a simple, opinionated UI.
- **`blocks-workflow`** — generic over `<TState, TTrigger, TContext>`, throws on invalid moves, supports hooks, build-time reachability validation, runtime instance tracking.

If you need anything beyond the canonical four-column board, prefer `blocks-workflow`. If you need *less* than that, stick with `blocks-tasks`.

## ADRs in effect

- **ADR 0022 — Example catalog + docs taxonomy.** Canonical docs layout (this page set).
- No ADR currently locks the task lifecycle — it is documented as hard-coded and extensible via fork.

## Comparison to full-featured task products

| Capability | blocks-tasks | A full task-management product |
|---|---|---|
| Column count | Fixed at 4 | Usually configurable |
| Card contents | `TaskItem` + `ItemTemplate` override | Arbitrary fields, linked issues, comments |
| Lifecycle | 4-state hard-coded | Configurable workflow engine |
| Drag-and-drop | Not implemented | Expected |
| Persistence | None (caller-supplied data) | Full relational or document store |
| Subtasks / deps | Not modelled | Expected |
| Notifications | None | Expected |

The block is deliberately on the "tiny" end of the spectrum. Trying to stretch it into a richer product usually means forking or switching to `blocks-workflow` for the lifecycle and writing your own UI.

## Related

- [State Machine](state-machine.md)
- [Service Contract](service-contract.md)
- [blocks-workflow — Overview](../workflow/overview.md) (for non-task workflows)
