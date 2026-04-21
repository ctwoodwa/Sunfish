---
uid: block-maintenance-workflow
title: Maintenance — Workflow
description: Allowed lifecycle transitions for maintenance requests, RFQs, quotes, and work orders, plus the atomic AcceptQuoteAsync fan-out.
keywords:
  - sunfish
  - maintenance
  - workflow
  - state-machine
  - transition-table
  - atomic-accept
---

# Maintenance — Workflow

## Overview

Four lifecycle enums drive the maintenance block: `MaintenanceRequestStatus`, `RfqStatus`,
`QuoteStatus`, and `WorkOrderStatus`. The default `InMemoryMaintenanceService` enforces
every transition through a small internal helper (`TransitionTable<TState>`) that throws
a descriptive `InvalidOperationException` for forbidden targets.

## Maintenance-request lifecycle

```
        ┌─────────────┐
        │  Submitted  │
        └──────┬──────┘
               │ TransitionRequestAsync(UnderReview)
               ▼
        ┌─────────────┐
        │ UnderReview │──────── Rejected (terminal)
        └──────┬──────┘
               │ Approved
               ▼
        ┌─────────────┐
        │  Approved   │
        └──────┬──────┘
               │ InProgress
               ▼
        ┌─────────────┐
        │ InProgress  │
        └──────┬──────┘
               │ Completed (terminal)
               ▼
        ┌─────────────┐
        │  Completed  │
        └─────────────┘

   * → Cancelled (terminal), from Submitted, UnderReview, Approved, or InProgress
```

### Allowed transitions

| From          | To                                   |
|---------------|--------------------------------------|
| `Submitted`   | `UnderReview`, `Cancelled`           |
| `UnderReview` | `Approved`, `Rejected`, `Cancelled`  |
| `Approved`    | `InProgress`, `Cancelled`            |
| `InProgress`  | `Completed`, `Cancelled`             |

Terminal: `Completed`, `Rejected`, `Cancelled`.

## Work-order lifecycle

```
        ┌─────────┐
        │  Draft  │
        └────┬────┘
             │ Sent
             ▼
        ┌─────────┐    Cancelled
        │  Sent   │──────────────▶ (terminal)
        └────┬────┘
             │ Accepted
             ▼
        ┌──────────┐
        │ Accepted │
        └────┬─────┘
             │ Scheduled
             ▼
        ┌──────────┐
        │Scheduled │
        └────┬─────┘
             │ InProgress
             ▼
        ┌────────────┐      ┌────────┐
        │ InProgress │◀────▶│ OnHold │
        └─────┬──────┘      └────────┘
              │ Completed (terminal)
              ▼
        ┌───────────┐
        │ Completed │
        └───────────┘
```

### Allowed transitions

| From         | To                         |
|--------------|----------------------------|
| `Draft`      | `Sent`                     |
| `Sent`       | `Accepted`, `Cancelled`    |
| `Accepted`   | `Scheduled`                |
| `Scheduled`  | `InProgress`               |
| `InProgress` | `Completed`, `OnHold`      |
| `OnHold`     | `InProgress`               |

Terminal: `Completed`, `Cancelled`.

## RFQ lifecycle

```
Draft  →  Sent  →  Closed     (terminal)
                →  Cancelled  (terminal)
```

| From     | To                   |
|----------|----------------------|
| `Draft`  | `Sent`               |
| `Sent`   | `Closed`, `Cancelled`|

## Quote lifecycle

```
Draft  →  Submitted  →  Accepted   (terminal, triggers work-order creation)
                     →  Declined   (terminal)
                     →  Expired    (terminal)

Withdrawn is terminal and reserved for vendor-initiated withdrawal.
```

| From        | To                                 |
|-------------|------------------------------------|
| `Draft`     | `Submitted`                        |
| `Submitted` | `Accepted`, `Declined`, `Expired`  |

Terminal: `Accepted`, `Declined`, `Expired`, `Withdrawn`.

## Atomic quote acceptance

`AcceptQuoteAsync(QuoteId)` is the single entry point that ties the quote and work-order
lifecycles together. Under a per-request lock it:

1. Validates that the target quote is in `Submitted`.
2. Transitions the target quote to `Accepted`.
3. Transitions every other `Submitted` quote for the same `MaintenanceRequestId` to
   `Declined`.
4. Creates a new `WorkOrder` in `Draft` linked to the accepted quote's `VendorId`.

If two admins hit `AcceptQuoteAsync` concurrently with different quote IDs for the same
maintenance request, the lock serialises them: the first call wins and the second call
fails fast because the target quote is no longer `Submitted`.

## Transition-guard error messages

When a transition is rejected the service throws:

```
InvalidOperationException:
    "Cannot transition {entity} from {fromState} to {toState}.
     Allowed targets from {fromState}: [<list>]."
```

The `entity` label identifies which object the transition was attempted on (e.g.
`"MaintenanceRequest"`, `"WorkOrder"`), which makes the error self-describing in logs.

## Deferred: cross-block transitions

- Completing a work order will (in a future pass) transition the linked inspection
  deficiency to `Resolved`. The rollup is out of scope for the current pass to keep the
  two blocks independent and to avoid introducing an event-bus dependency.
- Emergency-priority maintenance requests will (in a future pass) auto-fast-track through
  `UnderReview`. Today they must follow the standard transition sequence.

## Worked examples

### Valid request path

```csharp
await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.UnderReview);
await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.Approved);
await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.InProgress);
await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.Completed);
```

Pinned by `TransitionRequest_ValidPath_SubmittedToCompleted`.

### Illegal skip

```csharp
// Request is in Submitted — cannot jump straight to Completed.
await Assert.ThrowsAsync<InvalidOperationException>(
    () => svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.Completed).AsTask());
```

Pinned by `TransitionRequest_InvalidTransition_ThrowsInvalidOperationException`.

### Cancel-from-anywhere

`Cancelled` is allowed from any non-terminal request state. Pinned by the
`TransitionRequest_CancelAllowedFromAnyNonTerminalState` theory:

| Starting state | Action | Result |
|---|---|---|
| `Submitted` | `TransitionRequestAsync(id, Cancelled)` | → `Cancelled` |
| `UnderReview` | `TransitionRequestAsync(id, Cancelled)` | → `Cancelled` |
| `Approved` | `TransitionRequestAsync(id, Cancelled)` | → `Cancelled` |
| `InProgress` | `TransitionRequestAsync(id, Cancelled)` | → `Cancelled` |
| Any terminal | `TransitionRequestAsync(id, *)` | `InvalidOperationException` |

### OnHold cycle

A work order in `InProgress` can be parked on `OnHold` and later resumed:

```csharp
await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.OnHold);
// …parts on order, tenant unavailable, etc.
await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.InProgress);
await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Completed);
```

`OnHold` has exactly one outbound target — back to `InProgress`. You cannot complete a
work order directly from `OnHold`.

## Test-surface cross-reference

| Test | What it pins |
|---|---|
| `SubmitRequestAsync_CreatesRequest_InSubmittedStatus` | Requests always start in `Submitted`. |
| `TransitionRequest_ValidPath_SubmittedToCompleted` | The happy path is allowed end-to-end. |
| `TransitionRequest_InvalidTransition_ThrowsInvalidOperationException` | Illegal target raises. |
| `TransitionRequest_CancelAllowedFromAnyNonTerminalState` | `Cancelled` from any non-terminal state. |
| `TransitionRequest_FromTerminalState_Throws` | Terminal states have no outbound edges. |
| `AcceptQuoteAsync_AcceptsTarget_DeclinesOthers_SpawnsWorkOrder` | Single-call fan-out invariants. |
| `AcceptQuoteAsync_IsAtomic_ConcurrentCallsConvergeToExactlyOneAccepted` | Race convergence. |
| `TransitionWorkOrder_ValidPath_DraftToCompleted` | Work-order happy path. |
| `TransitionWorkOrder_OnHoldCycle` | `OnHold ↔ InProgress` round-trip. |

## Related pages

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- [Service Contract](service-contract.md)
