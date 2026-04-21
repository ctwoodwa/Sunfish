---
uid: block-workflow-fluent-builder
title: Workflow — Fluent Builder
description: WorkflowDefinitionBuilder — StartAt, Transition, Terminal, OnTransition, Build, and build-time reachability validation.
---

# Workflow — Fluent Builder

## Overview

`WorkflowDefinitionBuilder<TState, TTrigger, TContext>` is the public surface for constructing a workflow. It offers a small fluent API, enforces duplicate-edge checks immediately, and validates reachability when you call `Build()`. The returned definition is immutable and thread-safe.

## Public surface

```csharp
public sealed class WorkflowDefinitionBuilder<TState, TTrigger, TContext>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    public WorkflowDefinitionBuilder<TState, TTrigger, TContext> StartAt(TState state);
    public WorkflowDefinitionBuilder<TState, TTrigger, TContext> Transition(TState from, TTrigger on, TState to);
    public WorkflowDefinitionBuilder<TState, TTrigger, TContext> Terminal(params TState[] states);
    public WorkflowDefinitionBuilder<TState, TTrigger, TContext> OnTransition(
        Func<TState, TState, TTrigger, TContext, CancellationToken, ValueTask> handler);

    public IWorkflowDefinition<TState, TTrigger, TContext> Build();
}
```

Each method returns `this`, so calls chain fluently.

### StartAt

Sets the initial state for new instances. Calling `Build()` without calling `StartAt` throws `InvalidOperationException`.

### Transition

Registers an allowed edge `(from, on) → to`. **Duplicate edges throw immediately** — calling `Transition(A, X, B)` and then `Transition(A, X, C)` raises `ArgumentException` at the second call, with a message that names both the old and new target. This catches typos and accidental overwrites at build time rather than runtime.

### Terminal

Marks one or more states as terminal. Instances in a terminal state reject further triggers at runtime (the in-memory runtime throws `InvalidOperationException`). The builder accepts `params TState[]`, so:

```csharp
builder.Terminal(
    DemoMaintenanceState.Rejected,
    DemoMaintenanceState.Cancelled,
    DemoMaintenanceState.Completed);
```

### OnTransition

Registers an optional side-effect hook:

```csharp
Func<TState, TState, TTrigger, TContext, CancellationToken, ValueTask> handler;
```

The hook is invoked **after** a transition is committed. Hook exceptions propagate to the `FireAsync` caller, but the transition itself is **not** rolled back — see [Runtime](runtime.md) for the hook-error semantics.

## Build and reachability validation

```csharp
public IWorkflowDefinition<TState, TTrigger, TContext> Build();
```

`Build()` performs two checks before freezing the definition:

1. **`StartAt` required** — throws if `StartAt` was never called.
2. **Reachability** — BFS from the initial state. Every reachable state must be either terminal *or* have at least one outgoing transition. Any state that is reachable but has no outgoing edges and is not marked terminal raises `InvalidOperationException` with a message identifying the offending state and suggesting either adding transitions or marking it `Terminal(...)`.

Unreachable states are allowed (the BFS never visits them). States declared only in `Terminal(...)` but never appearing as a transition target are also allowed — they are legal-but-useless additions.

Once `Build()` succeeds, the returned `IWorkflowDefinition<…>` is immutable and thread-safe; it may be cached in a singleton and reused by any number of concurrent `FireAsync` calls.

## Full example — reference maintenance workflow

From `packages/blocks-workflow/tests/Fixtures/DemoMaintenanceWorkflow.cs`:

```csharp
public static IWorkflowDefinition<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>
    Build(
        Func<DemoMaintenanceState, DemoMaintenanceState, DemoMaintenanceTrigger,
            DemoMaintenanceContext, CancellationToken, ValueTask>? onTransition = null)
{
    var builder = new WorkflowDefinitionBuilder<
        DemoMaintenanceState,
        DemoMaintenanceTrigger,
        DemoMaintenanceContext>()
        .StartAt(DemoMaintenanceState.Submitted)
        .Transition(DemoMaintenanceState.Submitted,   DemoMaintenanceTrigger.Approve,  DemoMaintenanceState.Approved)
        .Transition(DemoMaintenanceState.Submitted,   DemoMaintenanceTrigger.Reject,   DemoMaintenanceState.Rejected)
        .Transition(DemoMaintenanceState.Submitted,   DemoMaintenanceTrigger.Cancel,   DemoMaintenanceState.Cancelled)
        .Transition(DemoMaintenanceState.Approved,    DemoMaintenanceTrigger.Start,    DemoMaintenanceState.InProgress)
        .Transition(DemoMaintenanceState.Approved,    DemoMaintenanceTrigger.Cancel,   DemoMaintenanceState.Cancelled)
        .Transition(DemoMaintenanceState.InProgress,  DemoMaintenanceTrigger.Complete, DemoMaintenanceState.Completed)
        .Transition(DemoMaintenanceState.InProgress,  DemoMaintenanceTrigger.Cancel,   DemoMaintenanceState.Cancelled)
        .Terminal(
            DemoMaintenanceState.Rejected,
            DemoMaintenanceState.Cancelled,
            DemoMaintenanceState.Completed);

    if (onTransition is not null)
        builder.OnTransition(onTransition);

    return builder.Build();
}
```

Every reachable state is either terminal (`Rejected`, `Cancelled`, `Completed`) or has outgoing edges (`Submitted`, `Approved`, `InProgress`), so `Build()` succeeds.

## Related

- [Overview](overview.md)
- [State Machine Primitives](state-machine-primitives.md)
- [Runtime](runtime.md)
