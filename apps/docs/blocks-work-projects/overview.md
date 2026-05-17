# blocks-work-projects

Project management slice of the `blocks-work-*` cluster per
[ADR 0066](../../../docs/adrs/0066-work-cluster-package-split.md) §1.
Ships project lifecycles, milestones, budgets, time tracking, and the
remodel-capitalization workflow.

## Overview

This package provides:

- **`Project`** — bounded body of work with an 8-state lifecycle
  (`Draft` → `Planned` → `InProgress` → `OnHold`/`Blocked`/`Completed`/`Cancelled` →
  `Closed`).
- **`ProjectMilestone`** — payment-trigger or schedule-landmark
  checkpoint with optional invoice-trigger handoff to the AR cluster.
- **`ProjectBudget`** + **`ProjectBudgetLine`** — planned cost by
  category with auto-superseding revision history.
- **`TimeEntry`** + **`ITimeApprovalService`** — labor tracking,
  append-only after approval, period-gated submission (gate wiring
  deferred to the host's composition root).
- **`ProjectActual`** — event-sourced from `Financial.JournalEntryPosted`;
  rebuildable from the upstream event log.
- **`RemodelProject`** + **`RemodelPhase`** — capital-improvement
  specialization with a capitalization handoff to
  `blocks-financial-ledger` via `Work.RemodelCapitalized`.

## Naming

`blocks-work-projects` is distinct from `blocks-workflow` (engine) and
`blocks-work-orders` (execution slice). Namespace:
`Sunfish.Blocks.WorkProjects`.

## Quickstart

```csharp
services.AddBlocksWorkProjects();
// ... in a request handler ...
var project = await projectService.CreateAsync(
    tenantId, "Whitney Unit 5B Remodel", ProjectKind.Remodel,
    Priority.High, ownerPartyId, currentUserId);
```

The DI extension registers every in-memory service the cluster ships.
Cross-cluster contract dependencies (`IDomainEventPublisher`,
`IPartyReadModel`) use `TryAddSingleton` so downstream sweeps from
`foundation-events`, `blocks-people-foundation`, and
`blocks-financial-periods` cleanly override via plain `AddSingleton`
without throwing.

## Cross-cluster contracts emitted

| Event | Idempotency key | Notes |
|---|---|---|
| `Work.ProjectCreated` | `project-created:{projectId}` | one-shot |
| `Work.ProjectStatusChanged` | `project-status:{projectId}:{occurredAtTicks}` | multi-fire safe |
| `Work.MilestoneCreated` | `milestone-created:{milestoneId}` | one-shot |
| `Work.MilestoneAchieved` | `milestone-achieved:{milestoneId}` | one-shot |
| `Work.MilestoneInvoiceTriggered` | `milestone-invoice:{milestoneId}` | one-shot, fires only when `TriggersInvoice = true` |
| `Work.TimeEntrySubmitted` | `time-entry-submitted:{timeEntryId}` | one-shot |
| `Work.TimeEntryApproved` | `time-entry-approved:{timeEntryId}` | one-shot |
| `Work.RemodelPhaseCompleted` | `remodel-phase-completed:{phaseId}` | one-shot |
| `Work.RemodelCapitalized` | `remodel-capitalized:{remodelProjectId}` | one-shot |

Full catalog:
[`_shared/engineering/cross-cluster-event-bus-design.md`](../../../_shared/engineering/cross-cluster-event-bus-design.md)
§3.2.

## Cross-cluster contracts consumed

- `Financial.JournalEntryPosted` — consumed by
  `JournalEntryPostedHandler` (via `IProjectActualProjector`); filters
  lines carrying `Dimensions["projectId"]`, projects one
  `ProjectActual` row per `(projectId, sourceKind, sourceRefId,
  glAccountId)` tuple.

## Algorithms

- **Status state machine:** see `ProjectStatusMachine` (Stage 02 §2.2).
- **Budget vs actual reconciliation:** `IProjectActualProjector` +
  `IProjectActualReader.GetTotalsAsync` (Stage 02 §4.2).
- **Milestone invoice trigger:** Stage 02 §4.5; emits
  `Work.MilestoneInvoiceTriggered` consumed by `blocks-financial-ar`.
- **Time-entry approval:** Stage 02 §4.6; emits `Work.TimeEntryApproved`
  consumed by `blocks-financial-ledger`.
- **Remodel capitalization:** Stage 02 §2.8; emits
  `Work.RemodelCapitalized` consumed by `blocks-financial-ledger` to
  post the capital-asset JE.

## Related packages

- `blocks-work-orders` (sibling — execution slice)
- `blocks-financial-ledger` (consumes `Work.TimeEntryApproved`,
  `Work.RemodelCapitalized`)
- `blocks-financial-ar` (consumes `Work.MilestoneInvoiceTriggered`)
- `blocks-financial-periods` (provides `IPeriodResolver` for
  time-entry period-gating)
- `blocks-people-foundation` (provides `IPartyReadModel`)
- `foundation-events` (provides canonical `IDomainEventPublisher` +
  event-store substrate)

## Deferred surfaces

- `ValidateCompletionAsync(projectId)` — cross-entity gate against
  `IWorkOrderReadModel` ("no open WOs before Completed"). Deferred to
  a follow-on hand-off; depends on `blocks-work-orders` exposing
  `IWorkOrderReadModel`.
- `RecomputeRollupsAsync(projectId)` — aggregate `ProjectBudget` vs
  `ProjectActual` totals onto `Project.BudgetedAmount` /
  `Project.ActualAmount`. Deferred — call `Project.UpdateRollups`
  directly until the orchestrator surface is added.
- `AddBudgetRevisionAsync` — currently call `IProjectBudgetRepository.InsertRevisionAsync`
  directly. Service-layer convenience deferred.
