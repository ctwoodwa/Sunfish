# Sunfish.Blocks.Tasks

Task-board state-machine block — opinionated composition over `SunfishDataGrid` and `SunfishCard` for kanban-style task views.

## What this ships

### Models

- **`TaskItem`** — task entity with title, description, assignee, priority, due date, parent task ref.
- **`TaskStatus`** — enum lifecycle (Backlog / Todo / InProgress / Blocked / Review / Done / Cancelled).

### Services

- **`ITaskService`** + `InMemoryTaskService` — CRUD + status transitions + assignment + dependency tracking.

### UI

- Razor components composing `SunfishDataGrid` (list view) + `SunfishCard` (board view) into a kanban-style task board.

## DI

```csharp
services.AddInMemoryTasks();
```

## Cluster role

Horizontal task-management primitive. Generic enough to consume from any block needing a "do these things in this order" UX.

## ADR map

- [ADR 0015](../../docs/adrs/0015-module-entity-registration.md) — module-entity registration

## See also

- [apps/docs Overview](../../apps/docs/blocks/tasks/overview.md)
- [State Machine](../../apps/docs/blocks/tasks/state-machine.md)
- [Service Contract](../../apps/docs/blocks/tasks/service-contract.md)
