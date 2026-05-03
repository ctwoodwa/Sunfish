---
uid: block-workflow-fluent-builder
title: Workflow — Fluent Builder
description: WorkflowDefinitionBuilder — StartAt, Transition, Terminal, OnTransition, Build, and build-time reachability validation.
keywords:
  - workflow-builder
  - fluent-api
  - reachability
  - transition-table
  - start-at
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

## Common build-time errors

| Error | Cause | Fix |
|---|---|---|
| `InvalidOperationException: StartAt must be called before Build.` | You forgot `StartAt(...)`. | Add it. |
| `ArgumentException: Transition (A, X) already maps to B; cannot remap to C.` | You registered two edges with the same `(from, on)` key. | Remove one, or rename the trigger. |
| `InvalidOperationException: State 'X' is reachable but has no outgoing transitions and is not terminal.` | A state is a dead-end but you forgot `Terminal(X)`. | Mark it terminal, or add an outgoing transition. |

The reachability check runs a BFS from `InitialState` and visits every state reachable via the transition table. Only *reachable* states must be non-dead; unreachable states are ignored. This means you can declare triggers that lead to legal-but-never-used states without tripping the validator, which is occasionally useful for forward-compatible enum additions.

## Testing a builder

`tests/WorkflowDefinitionBuilderTests.cs` exercises every error path. A small excerpt:

```csharp
[Fact]
public void Transition_DuplicateEdgeThrows()
{
    var builder = new WorkflowDefinitionBuilder<S, T, C>()
        .StartAt(S.Start)
        .Transition(S.Start, T.Go, S.Mid);

    Assert.Throws<ArgumentException>(() =>
        builder.Transition(S.Start, T.Go, S.End));
}

[Fact]
public void Build_DeadEndStateThrows()
{
    var builder = new WorkflowDefinitionBuilder<S, T, C>()
        .StartAt(S.Start)
        .Transition(S.Start, T.Go, S.Mid);
    // S.Mid is reachable but has no outgoing edge and is not terminal.

    Assert.Throws<InvalidOperationException>(() => builder.Build());
}
```

## Caching the built definition

Definitions are immutable and thread-safe, so they can (and should) be cached. A common pattern is to store them in a singleton factory:

```csharp
public static class MaintenanceWorkflow
{
    private static readonly IWorkflowDefinition<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>
        Definition = DemoMaintenanceWorkflow.Build();

    public static IWorkflowDefinition<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext> Instance
        => Definition;
}
```

Callers retrieve `MaintenanceWorkflow.Instance` without rebuilding. Because `Build()` does work (reachability BFS, dictionary construction), caching is worth the small static-init cost.

## Hook signature rationale

The `OnTransition` hook takes five parameters:

- **from / to** — the states involved. Useful for hook code that needs to know where the transition is going (e.g. "only fire an email on `Approved → InProgress`").
- **trigger** — the trigger that caused the transition. Lets hooks differentiate between different paths into the same state.
- **context** — the per-instance context. The hook may mutate the context object (it is not a `readonly` record by constraint).
- **ct** — a cancellation token. Honoured during the await if the hook is async I/O.

Keeping the hook signature rich means consumers rarely need to bypass the runtime to do per-transition side effects.

## Related

- [Overview](overview.md)
- [State Machine Primitives](state-machine-primitives.md)
- [Runtime](runtime.md)
