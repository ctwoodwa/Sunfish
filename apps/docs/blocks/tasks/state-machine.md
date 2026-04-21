---
uid: block-tasks-state-machine
title: Tasks — State Machine
description: TaskBoardState and the canonical Backlog → Todo → InProgress → Done lifecycle with permitted reverts.
---

# Tasks — State Machine

## Overview

`TaskBoardState` is the small, validating state machine that enforces the canonical task lifecycle. It is deliberately non-throwing — callers receive a boolean and an `out` parameter so that invalid transitions can be rejected silently in UI code without an exception path.

## Lifecycle

The canonical flow is:

```
Backlog → Todo → InProgress → Done
```

with two explicit reverts:

- `InProgress → Todo` (pull back from in-progress)
- `Todo → Backlog` (deprioritise)

Any no-op transition (`from == to`) is also permitted. Everything else — including going backwards from `Done` or skipping states forward (e.g. `Backlog → InProgress`) — is rejected.

## Transition table

| From | Trigger (target) | Allowed? |
|---|---|---|
| `Backlog` | `Todo` | yes |
| `Todo` | `InProgress` | yes |
| `InProgress` | `Done` | yes |
| `InProgress` | `Todo` | yes (revert) |
| `Todo` | `Backlog` | yes (revert) |
| any | same state | yes (no-op) |
| `Done` | anything else | no |
| everything else | everything else | no |

Implemented in `TaskBoardState.IsValid(TaskStatus from, TaskStatus to)` as a C# switch expression.

## Public API

```csharp
public sealed class TaskBoardState
{
    public bool TryTransition(
        TaskItem item,
        TaskStatus target,
        out TaskItem updated);
}
```

`TryTransition` returns `true` and sets `updated` to a new `TaskItem` with the target status (via `item with { Status = target }`) when the transition is valid. When invalid, it returns `false` and sets `updated` to the original `item` unchanged.

## Typical workflow

```csharp
var state = new TaskBoardState();
var task = new TaskItem
{
    Id = "t-1",
    Title = "Write docs",
    Status = TaskStatus.Backlog,
};

if (state.TryTransition(task, TaskStatus.Todo, out var promoted))
{
    task = promoted;  // now Todo
}

if (!state.TryTransition(task, TaskStatus.Done, out _))
{
    // Direct Todo → Done is rejected; caller handles the UI feedback.
}
```

## Terminal status

`Done` is effectively terminal — once an item reaches `Done`, no outgoing transition is valid. There is no explicit `Terminal` marker on the state machine (unlike the more general `blocks-workflow` runtime); the rejection is purely driven by the transition table.

## Extending the lifecycle

The lifecycle is hard-coded. The file's own comment flags this as a known follow-up: a registry or a consumer-defined enum would let other teams plug in their own statuses. Until then, consumers who need different states should fork the state machine or wrap it.

## Related

- [Overview](overview.md)
- [Service Contract](service-contract.md)
