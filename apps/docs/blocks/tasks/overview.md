---
uid: block-tasks-overview
title: Tasks — Overview
description: Introduction to the blocks-tasks package — a canonical Kanban-style task board with a built-in state machine.
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

## Related

- [State Machine](state-machine.md)
- [Service Contract](service-contract.md)
