---
uid: block-workflow-overview
title: Workflow — Overview
description: Introduction to the blocks-workflow package — declarative state-machine primitives, a fluent builder, and an in-memory runtime.
---

# Workflow — Overview

## Overview

The `blocks-workflow` package provides a compact, type-safe state-machine toolkit for building domain workflows. It is three layers stacked:

1. **Primitives** — `IWorkflowDefinition<TState, TTrigger, TContext>` (immutable definition) and `WorkflowInstance<…>` (a running instance snapshot).
2. **Fluent builder** — `WorkflowDefinitionBuilder<…>` with `StartAt`, `Transition`, `Terminal`, `OnTransition`, and `Build` methods; produces a frozen, validated definition.
3. **Runtime** — `IWorkflowRuntime` and `InMemoryWorkflowRuntime` for starting, firing triggers against, and querying running instances.

The package is framework-agnostic. It lives in `packages/blocks-workflow/src/` and ships no UI — it is a pure state-machine library designed to be wrapped by higher-level blocks (maintenance requests, subscription lifecycles, etc.) that want deterministic transitions.

## Package path

`packages/blocks-workflow` — assembly `Sunfish.Blocks.Workflow`.

## When to use it

- You have a domain entity with a small-to-medium set of lifecycle states and a clear transition table (maintenance requests, tenancy applications, inspection reports).
- You want reachability validation at build time — no orphan states, no dead-ends unless explicitly terminal.
- You want per-instance concurrency (serialised transitions on the same instance, parallel across instances) without writing locking yourself.

If you need durable workflows that survive process restarts, multi-step compensations, or saga orchestration, the in-memory runtime is a starting point only — a persistence-backed runtime is a future pass.

## Key types

- **`IWorkflowDefinition<TState, TTrigger, TContext>`** — immutable definition (initial state, terminal set, transition table, optional transition hook). Safe to share across threads.
- **`WorkflowDefinitionBuilder<TState, TTrigger, TContext>`** — fluent builder. `Build` validates reachability and returns a frozen definition.
- **`IWorkflowRuntime`** — `StartAsync`, `FireAsync`, `GetAsync`.
- **`InMemoryWorkflowRuntime`** — in-memory implementation with per-instance `SemaphoreSlim` locking.
- **`WorkflowInstance<TState, TTrigger, TContext>`** — immutable snapshot record: `Id`, `CurrentState`, `Context`, `IsTerminal`, `StartedAtUtc`, `LastTransitionAtUtc`, `TransitionCount`.
- **`WorkflowInstanceId`** — strong-typed id.

## Generic type parameters

All types are generic over three parameters:

- `TState : struct, Enum` — the workflow's state enum.
- `TTrigger : struct, Enum` — the event/command enum that drives transitions.
- `TContext` — a mutable context object carried by each instance; no type constraint, so you can use any class/record.

This design ensures state and trigger values cannot be typo'd by consumers (the compiler rejects non-enum types) while keeping the context arbitrarily rich.

## DI wiring

```csharp
services.AddInMemoryWorkflow();
```

Registers a singleton `IWorkflowRuntime` backed by `InMemoryWorkflowRuntime`. State is in-process only — a process crash or restart loses every running instance.

## Reference example

The package's tests include a reference workflow (`DemoMaintenanceWorkflow`) modelling a maintenance request:

```
Submitted ──Approve──▶ Approved ──Start──▶ InProgress ──Complete──▶ Completed (terminal)
    │                     │                     │
    ├─Reject─▶ Rejected    └─Cancel─▶ Cancelled  └─Cancel─▶ Cancelled
    └─Cancel─▶ Cancelled
```

With `Rejected`, `Cancelled`, and `Completed` marked `Terminal(...)`.

## Status and deferred items

- No persistence — all instances live in memory. Durable runtime is a future pass.
- No built-in compensation or saga pattern; hook-error semantics are advisory (the transition is committed before the hook is awaited).
- No time-based triggers or scheduled transitions; only external triggers drive the state machine.
- No visualization helpers. Consumers who want a diagram render one themselves from the transition table.

## Related

- [State Machine Primitives](state-machine-primitives.md)
- [Fluent Builder](fluent-builder.md)
- [Runtime](runtime.md)
