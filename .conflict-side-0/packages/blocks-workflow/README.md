# Sunfish.Blocks.Workflow

Generic state-machine workflow primitives — declarative definition, fluent builder, and an in-memory runtime.

**Slice 1 (current):** in-memory runtime over a declarative DSL. Defers durable execution, Temporal/Elsa/Dapr integration, BPMN serialization, and event-bus dispatch to slice 2.

## What this ships

### DSL + builder

- **`WorkflowDefinition`** — declarative workflow description (states, transitions, guards, side-effects).
- **`WorkflowBuilder`** — fluent API for building `WorkflowDefinition` instances.
- **`StateMachine<TState, TEvent>`** — generic state-machine primitive used inside a workflow.

### Runtime

- **`IWorkflowRuntime`** + `InMemoryWorkflowRuntime` — in-memory execution engine; advances workflow instances on event arrival; computes state-transition validity against the definition's guards.

### Examples / integration patterns

- Reference workflows (e.g., approval chains) demonstrate the DSL.
- Block-local consumers (`blocks-maintenance.WorkOrderStatus`, `blocks-leases.LeasePhase`) keep their own state-machine code paths but follow the same conceptual shape — this block is the canonical primitive other blocks can adopt.

## DI

```csharp
services.AddInMemoryWorkflow();
```

## Slice 2 deferrals

- Durable persistence (database-backed workflow state)
- Temporal / Elsa / Dapr integration
- BPMN 2.0 import/export
- Event-bus integration (currently in-process only)
- Visual designer

## See also

- [apps/docs Overview](../../apps/docs/blocks/workflow/overview.md)
- [Sunfish.Blocks.Maintenance](../blocks-maintenance/README.md) — `WorkOrderStatus` consumer of `TransitionTable<TState>`
- [Sunfish.Blocks.Leases](../blocks-leases/README.md) — `LeasePhase` lifecycle
