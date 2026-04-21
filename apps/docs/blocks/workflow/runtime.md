---
uid: block-workflow-runtime
title: Workflow — Runtime
description: IWorkflowRuntime, InMemoryWorkflowRuntime, concurrency model, and hook-error semantics.
keywords:
  - workflow-runtime
  - in-memory
  - per-instance-lock
  - hook-semantics
  - deterministic-state
---

# Workflow — Runtime

## Overview

`IWorkflowRuntime` is the imperative surface of `blocks-workflow`: it starts new instances, fires triggers against existing ones, and retrieves current snapshots. The shipped implementation — `InMemoryWorkflowRuntime` — stores instances in a `ConcurrentDictionary` keyed by `WorkflowInstanceId` and uses per-instance `SemaphoreSlim` locks to serialise concurrent `FireAsync` calls on the same instance.

## IWorkflowRuntime contract

```csharp
public interface IWorkflowRuntime
{
    ValueTask<WorkflowInstance<TState, TTrigger, TContext>> StartAsync<TState, TTrigger, TContext>(
        IWorkflowDefinition<TState, TTrigger, TContext> definition,
        TContext initialContext,
        CancellationToken ct = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum;

    ValueTask<WorkflowInstance<TState, TTrigger, TContext>> FireAsync<TState, TTrigger, TContext>(
        WorkflowInstanceId id,
        TTrigger trigger,
        CancellationToken ct = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum;

    ValueTask<WorkflowInstance<TState, TTrigger, TContext>?> GetAsync<TState, TTrigger, TContext>(
        WorkflowInstanceId id,
        CancellationToken ct = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum;
}
```

### StartAsync

Creates a new instance in `definition.InitialState`, stores it in the runtime, and returns the snapshot. The instance's `IsTerminal` flag is true when the initial state is already a terminal state (a degenerate but legal case).

### FireAsync

Advances the instance identified by `id` using `trigger`:

- Throws `KeyNotFoundException` when no instance with that id exists.
- Throws `InvalidOperationException` when the instance's type arguments don't match the requested `<TState, TTrigger, TContext>`.
- Throws `InvalidOperationException` when the instance is in a terminal state.
- Throws `InvalidOperationException` when the trigger is not permitted from the current state.

On success, returns the updated snapshot with `CurrentState` advanced, `LastTransitionAtUtc` set to `DateTimeOffset.UtcNow`, and `TransitionCount` incremented.

### GetAsync

Returns the current snapshot or `null` if no instance with that id exists. Throws `InvalidOperationException` when the type arguments don't match.

## Concurrency model

From the `InMemoryWorkflowRuntime` docs:

> Each instance has a dedicated `SemaphoreSlim(1, 1)` that serializes concurrent `FireAsync` calls on the same instance. Calls on different instances run fully in parallel with no cross-instance locking.

Practically:

- Two threads calling `FireAsync(sameId, …)` contend for that instance's lock; the second waits, then sees the updated state.
- Two threads calling `FireAsync(differentIds, …)` proceed independently.
- The dictionary write that commits the new state is done inside the lock, before the transition hook is awaited.

## Hook-error semantics

This is the most subtle part of the runtime. From the in-memory implementation:

> **Hook-error semantics:**
> If `IWorkflowDefinition.OnTransitionAsync` throws, the state change is already committed to the dictionary before the hook is awaited — the exception propagates to the `FireAsync` caller but the transition is NOT rolled back. Callers must treat hook exceptions as advisory.

Specifically, the code path inside the per-instance lock is:

1. Validate that the instance is not terminal.
2. Look up the next state via `definition.Next(current, trigger)`.
3. Build the updated `WorkflowInstance<…>` record.
4. **Commit** — write the updated record to the storage slot.
5. **Await** `definition.OnTransitionAsync(current, next, trigger, context, ct)`.
6. Release the lock and return the updated record.

If step 5 throws, steps 1–4 are still in effect. The thrown exception propagates to the `FireAsync` caller, and the returned state is not observed by that caller. A subsequent `GetAsync` (or `FireAsync`) on the same instance will see the new state.

This design prioritises deterministic state over atomic side-effects. If your hook performs I/O that must succeed for the transition to be meaningful, you have two options:

- Call the side effect from the caller of `FireAsync` *before* firing, so the transition is only fired when the side effect has already succeeded.
- Build compensating triggers — if the hook fails, fire a compensating trigger to walk the instance back to a safe state.

## Typical workflow

```csharp
var definition = DemoMaintenanceWorkflow.Build();

var instance = await runtime.StartAsync(definition, new DemoMaintenanceContext(), ct);
Console.WriteLine(instance.CurrentState);  // Submitted

instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
    instance.Id, DemoMaintenanceTrigger.Approve, ct);
Console.WriteLine(instance.CurrentState);  // Approved

instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
    instance.Id, DemoMaintenanceTrigger.Start, ct);
Console.WriteLine(instance.CurrentState);  // InProgress

instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
    instance.Id, DemoMaintenanceTrigger.Complete, ct);
Console.WriteLine(instance.IsTerminal);     // true
```

## DI wiring

```csharp
services.AddInMemoryWorkflow();
```

Registers a singleton `IWorkflowRuntime` → `InMemoryWorkflowRuntime`. The runtime is expected to be long-lived; instance state lives in the runtime for the lifetime of the process.

## Persistence

There is no persistence. A process crash or restart loses every running instance. A durable runtime backed by a database or event store is a future pass.

## Error surface reference

| Call | Condition | Exception |
|---|---|---|
| `StartAsync` | none under normal operation | — |
| `FireAsync` | instance not found | `KeyNotFoundException` |
| `FireAsync` | stored instance's type arguments do not match `<TState, TTrigger, TContext>` | `InvalidOperationException` |
| `FireAsync` | instance in terminal state | `InvalidOperationException` |
| `FireAsync` | trigger not permitted from the current state | `InvalidOperationException` |
| `FireAsync` | hook threw (transition already committed) | `Exception` from hook (propagates) |
| `GetAsync` | type arguments don't match | `InvalidOperationException` |
| `GetAsync` | instance not found | returns `null` (no throw) |

Notably `GetAsync` returns `null` rather than throwing, matching the conventional "Try-Get" shape for look-ups. `FireAsync` throws on every error because an invalid fire is always a bug — either a race, a protocol violation, or a type mismatch.

## Tests as executable spec

`tests/InMemoryWorkflowRuntimeTests.cs` covers:

- **StartAsync** returns an instance with `CurrentState == InitialState`, `TransitionCount == 0`, `StartedAtUtc == LastTransitionAtUtc`.
- **FireAsync** with a known valid trigger advances state, increments `TransitionCount`, updates `LastTransitionAtUtc`.
- **FireAsync** with an unknown trigger throws `InvalidOperationException` and leaves state unchanged.
- **FireAsync** against a terminal instance throws.
- **FireAsync** with type-argument mismatch throws.
- **Concurrent FireAsync** on the same instance serialises (no lost updates to `TransitionCount`).
- **Concurrent FireAsync** on different instances runs in parallel (observed through wall-clock timing).
- **Hook exceptions** propagate to the caller but the state change is already observable via `GetAsync`.

Copy the concurrency fixture into a custom runtime you build on top, changing only the implementation under test — the assertions should hold.

## Strong-typing the FireAsync call site

Because `FireAsync<TState, TTrigger, TContext>` is generic, the call sites are verbose. A common ergonomic pattern is a thin extension method per definition:

```csharp
public static class MaintenanceWorkflowRuntimeExtensions
{
    public static ValueTask<WorkflowInstance<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>>
        FireAsync(
            this IWorkflowRuntime runtime,
            WorkflowInstanceId id,
            DemoMaintenanceTrigger trigger,
            CancellationToken ct = default)
        => runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(id, trigger, ct);
}
```

Callers then write `runtime.FireAsync(id, DemoMaintenanceTrigger.Approve)` with the type arguments inferred. The trade-off is one extension per definition; worth it for workflows with many call sites.

## Designing for durability now

Even without durable runtime today, you can write your workflow code in a way that makes durability cheap to add later:

- **Keep `TContext` serializable.** Prefer records with public init properties. Avoid delegates, open streams, or handles.
- **Make hooks idempotent.** The durable pass will retry hooks on restart; idempotent hooks are safe.
- **Record every trigger you fire.** An event log (even a simple append-only table) captures the business-level history independent of whether the in-memory instance survives.
- **Don't depend on `LastTransitionAtUtc` precision.** Treat it as "wall-clock approximation"; durable runtimes typically persist a coarser timestamp.

These habits let you swap runtimes without rewriting business logic.

## Related

- [Overview](overview.md)
- [State Machine Primitives](state-machine-primitives.md)
- [Fluent Builder](fluent-builder.md)
