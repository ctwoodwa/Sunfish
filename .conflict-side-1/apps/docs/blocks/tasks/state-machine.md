---
uid: block-tasks-state-machine
title: Tasks — State Machine
description: TaskBoardState and the canonical Backlog → Todo → InProgress → Done lifecycle with permitted reverts.
keywords:
  - tasks
  - state-machine
  - lifecycle
  - try-transition
  - non-throwing
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

## Wrapping with guard conditions

Adding business rules on top of `TryTransition` is a common pattern — for example, "only allow `InProgress → Done` if there's an assignee":

```csharp
public sealed class AssigneeAwareTaskBoardState
{
    private readonly TaskBoardState _inner = new();

    public bool TryTransition(TaskItem item, TaskStatus target, out TaskItem updated, out string? reason)
    {
        if (target == TaskStatus.Done && string.IsNullOrWhiteSpace(item.Assignee))
        {
            updated = item;
            reason  = "Cannot complete an unassigned task.";
            return false;
        }

        reason = null;
        return _inner.TryTransition(item, target, out updated);
    }
}
```

This composes cleanly because the inner state machine is stateless — you can hold a single instance and let the wrapper add policy on top.

## Pairing with undo

Because every successful transition produces a new `TaskItem` via `with`, undo is a matter of holding onto the previous reference:

```csharp
private readonly Stack<TaskItem> _history = new();

private void Move(TaskItem t, TaskStatus target)
{
    if (_state.TryTransition(t, target, out var next))
    {
        _history.Push(t);
        Replace(next);
    }
}

private void Undo()
{
    if (_history.Count > 0)
    {
        var prior = _history.Pop();
        Replace(prior);
    }
}

private void Replace(TaskItem updated) =>
    _tasks = _tasks.Select(x => x.Id == updated.Id ? updated : x).ToArray();
```

## Non-throwing rationale

The choice to return `bool` instead of throwing is deliberate. A Kanban UI typically reacts to a user action (a drag, a button click) and the natural response to an invalid move is a visual cue (tooltip, shake, snackbar) — not an exception. Throwing would force every UI handler into try/catch plumbing.

For contrast, the generic `blocks-workflow` runtime *does* throw on invalid triggers. That block is meant to back server-side lifecycles where exceptions compose with the pipeline's error handling. Tasks is UI-first, so the ergonomics flip.

## Exhaustiveness check

`TaskBoardState.IsValid` is implemented as a C# switch expression with all transition cases enumerated. Adding a new `TaskStatus` value without updating `IsValid` will break the `Done → NewValue` case silently (`IsValid` returns `false` by default). A future pass may convert to an exhaustive pattern with a compile-time warning for unhandled cases; today, keep the enum and the state machine updates paired.

## Related

- [Overview](overview.md)
- [Service Contract](service-contract.md)
- [blocks-workflow — State Machine Primitives](../workflow/state-machine-primitives.md) (for throwing, generic state machines)
