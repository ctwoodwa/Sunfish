# blocks-work-orders

Work-order execution + preventive-maintenance scheduling + contractor
projection for the Sunfish Anchor native work cluster.

## Overview

This package is the day-to-day operational layer of the
`blocks-work-*` cluster per
[ADR 0088 §1](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md).
It provides:

- **`WorkOrder`** — repair / task / preventive-maintenance /
  turnover / inspection-followup execution unit; `Number` is
  derived as `WO-{yyyyMMdd}-{first 7 hex of id}` (collision-free
  under CRDT — the id is UUIDv7 time-prefixed).
- **`WorkOrderLine`** — cost-component lines (labor / material /
  equipment / subcontract / fee / reimbursable). Auto-derives
  estimated amount from quantity × unit-price when not explicitly
  passed.
- **`RepairTicket`** — lightweight tenant-facing repair request that
  converts to a full `WorkOrder` one-shot.
- **`WorkOrderStatusMachine`** — allowed-transitions guard per
  Stage 02 §2.6. The application layer enforces the state diagram
  on every `WorkOrder.Transition` call; CRDT merges fall back to
  last-write-wins.
- **`MaintenanceSchedule`** + **`MaintenanceTask`** — recurring
  preventive-maintenance schedules + the per-occurrence task
  sidecars. Generated `WorkOrder`s carry kind =
  `PreventiveMaintenance`.
- **`Contractor`** — vendor / contractor projection over
  `blocks-people-*.Party` carrying contractor-specific fields
  (insurance, license, trades, ratings, preferred flag).
- **`IDeficiencyRaisedHandler`** — inbound consumer for
  `Work.DeficiencyRaised` events from `blocks-property-*`;
  idempotent on `DeficiencyId`, severity-mapped, SLA-aware.

## Status state machine

| From → To | Notes |
|---|---|
| `New → Triaged \| Cancelled` | Triage assigns priority + severity + owner. |
| `Triaged → Estimated \| Scheduled \| Cancelled` | Skipping Estimated is allowed for simple jobs. |
| `Estimated → Approved \| Cancelled` | Awaits approver sign-off. |
| `Approved → Scheduled` | Schedule window assigned. |
| `Scheduled → InProgress \| OnHold \| Cancelled` | Stamps `StartedAt` on InProgress. |
| `InProgress → OnHold \| Blocked \| Completed` | Stamps `CompletedAt` on Completed. |
| `OnHold / Blocked → InProgress \| Cancelled` | Resume or abandon. |
| `Completed → Verified \| InProgress` | Verification or re-open. |
| `Verified → Invoiced \| Closed` | Bill if tenant-rebillable. |
| `Invoiced → Closed` | Terminal. |
| `Closed / Cancelled → ∅` | Terminal; subsequent transitions throw. |

## CRDT discipline

`WorkOrder.Status` follows **Pattern A — Last-write-wins with
app-layer guard** per
`_shared/engineering/crdt-friendly-schema-conventions.md` §7. The
service throws `InvalidStatusTransitionException` on illegal local
transitions; CRDT merges of legal-from-each-side transitions land
last-writer-wins; a future
`IWorkOrderStatusReconciler` catches illegal merged states + emits
`Work.WorkOrderStatusConflict` for operator resolution (deferred
to a follow-on hand-off).

`WorkOrder` rows are **posted-then-mutable** — `Status`,
`AssignedToPartyId`, `EstimatedAmount`, and lifecycle timestamps
all mutate post-create; `Version` increments on every mutation.

## Quickstart

```csharp
services.AddBlocksWorkOrders();

// In a request handler:
var wo = await workOrderService.CreateAsync(
    tenantId:  tenant,
    title:     "Replace bathroom faucet — Unit 3B",
    kind:      WorkOrderKind.Repair,
    priority:  Priority.Normal,
    createdBy: principal.UserId,
    severity:  WorkOrderSeverity.Minor,
    propertyId: property.Id,
    unitId:     unit.Id);

// Triage → schedule → in-progress → completed → verified → closed.
await workOrderService.TransitionAsync(tenant, wo.Id, WorkOrderStatus.Triaged, principal.UserId);
```

## Cross-cluster events

Emitted (per
`_shared/engineering/cross-cluster-event-bus-design.md` §3.2):

| Event | Trigger | Idempotency key |
|---|---|---|
| `Work.WorkOrderCreated` | `IWorkOrderService.CreateAsync` | `wo-created:{workOrderId}` |
| `Work.WorkOrderAssigned` | `IWorkOrderService.AssignAsync` | `wo-assigned:{workOrderId}:{occurredAtTicks}` |
| `Work.WorkOrderCompleted` | `IWorkOrderService.TransitionAsync(Completed)` | `wo-completed:{workOrderId}` |

Consumed:

| Event | Handler |
|---|---|
| `Work.DeficiencyRaised` | `IDeficiencyRaisedHandler` — idempotent on `DeficiencyId` |

## Related packages

- `blocks-workflow` — state-machine engine (DIFFERENT package — do
  NOT confuse). `blocks-work-orders` is the domain; `blocks-workflow`
  is infrastructure.
- `blocks-people-foundation` — once shipped, provides the canonical
  `IPartyReadModel` that replaces this package's local stub (one-line
  re-namespace sweep).
- `blocks-work-projects` (deferred) — `Project` + `Milestone` +
  `RemodelProject` + budget / actual; references this package's
  `WorkOrder` as a cross-entity anchor.
