---
uid: block-maintenance-overview
title: Maintenance — Overview
description: Vendors, maintenance requests, RFQ / quote flow, and work orders for Sunfish-hosted property apps.
keywords:
  - sunfish
  - maintenance
  - blocks
  - vendors
  - work-orders
  - rfq
  - property-management
---

# Maintenance — Overview

## What this block is

`Sunfish.Blocks.Maintenance` is the domain block for property-maintenance workflows. It
ties together four bounded concerns:

1. **Vendors** — the contractors and service providers you may assign work to.
2. **Maintenance requests** — tenant- or manager-submitted issues.
3. **RFQ / quote flow** — soliciting prices from one or more vendors for a request and
   accepting one of the responses.
4. **Work orders** — the formal instruction to a vendor to perform the accepted work.

Each concern has its own lifecycle enum and its own state-transition guard, ensuring the
service contract rejects invalid transitions (e.g. cannot move a work order from `Draft`
directly to `Completed`). See [workflow.md](workflow.md) for the full allowed-transition
tables.

## Package

- Package: `Sunfish.Blocks.Maintenance`
- Source: `packages/blocks-maintenance/`
- Namespace roots:
  - `Sunfish.Blocks.Maintenance.Models`
  - `Sunfish.Blocks.Maintenance.Services`
  - `Sunfish.Blocks.Maintenance.DependencyInjection`
- Razor components: `WorkOrderListBlock.razor`

## When to use it

Use this block when your app needs:

- A canonical vendor record and a place to list vendors by specialty or status.
- A maintenance-request intake with a reviewed-approved-in-progress-completed flow.
- A vendor-RFQ flow with quote comparison and atomic quote acceptance that spawns a work
  order.
- Work-order state tracking with hold / resume and cancellation.

## Explicit independence from other blocks

`blocks-maintenance` has no compile-time dependency on `blocks-inspections` even though
many apps will feed deficiencies into maintenance requests. The link is carried as a plain
`MaintenanceRequest.DeficiencyReference` string. Consumer code is responsible for
translating between a real `DeficiencyId` and this string. This keeps the blocks loosely
coupled — deficiency → work-order auto-rollup is deferred and will live in the second-pass
`blocks-maintenance` update together with event-bus wiring.

## Key entities and services

- `Vendor` / `VendorSpecialty` / `VendorStatus`.
- `MaintenanceRequest` / `MaintenancePriority` / `MaintenanceRequestStatus`.
- `Rfq` / `RfqStatus`, `Quote` / `QuoteStatus`.
- `WorkOrder` / `WorkOrderStatus`.
- `IMaintenanceService` — single write/read contract covering all four concerns.

See [entity-model.md](entity-model.md), [service-contract.md](service-contract.md), and
[workflow.md](workflow.md).

## DI wiring

```csharp
using Sunfish.Blocks.Maintenance.DependencyInjection;

services.AddInMemoryMaintenance();
```

Registers `InMemoryMaintenanceService` as the singleton `IMaintenanceService`. Suitable
for development, tests, and demos. Replace with a persistence-backed implementation for
production.

## End-to-end sketch

```csharp
using Sunfish.Blocks.Maintenance.DependencyInjection;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;

services.AddInMemoryMaintenance();

var svc = serviceProvider.GetRequiredService<IMaintenanceService>();

// 1. Register a vendor.
var plumber = await svc.CreateVendorAsync(new CreateVendorRequest
{
    DisplayName = "Ace Plumbing",
    ContactEmail = "ops@ace-plumbing.example",
    Specialty = VendorSpecialty.Plumbing,
});

// 2. A tenant submits a request — always starts in Submitted.
var request = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
{
    PropertyId = new EntityId("property", "acme", "3B"),
    RequestedByDisplayName = "Jane Tenant",
    Description = "Leaky kitchen faucet",
    Priority = MaintenancePriority.Normal,
    RequestedDate = new DateOnly(2026, 5, 1),
});

// 3. Review, approve, send RFQ, accept quote → fan-out spawns a work order.
await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.UnderReview);
await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.Approved);

var rfq = await svc.SendRfqAsync(new SendRfqRequest
{
    RequestId = request.Id,
    InvitedVendors = [plumber.Id],
    ResponseDueDate = new DateOnly(2026, 5, 15),
    Scope = "Replace kitchen faucet cartridge",
});

var quote = await svc.SubmitQuoteAsync(new SubmitQuoteRequest
{
    VendorId = plumber.Id,
    RequestId = request.Id,
    Amount = 180m,
    ValidUntil = new DateOnly(2026, 6, 1),
});

await svc.AcceptQuoteAsync(quote.Id);
// ↑ Atomic: quote → Accepted, any other quotes → Declined, new WorkOrder in Draft.
```

## Operational invariants at a glance

| Invariant | Enforced by | On violation |
|---|---|---|
| Requests start in `Submitted`. | `SubmitRequestAsync`. | (No override.) |
| Request transitions follow the allowed-target graph. | `TransitionRequestAsync`. | `InvalidOperationException`. |
| Cancelling an already-terminal request is rejected. | `TransitionRequestAsync`. | Pinned by `TransitionRequest_FromTerminalState_Throws`. |
| `AcceptQuoteAsync` yields exactly one accepted quote per request. | Per-request lock. | Pinned by `AcceptQuoteAsync_IsAtomic_ConcurrentCallsConvergeToExactlyOneAccepted`. |
| Work-order transitions follow the allowed-target graph (with OnHold ↔ InProgress toggling). | `TransitionWorkOrderAsync`. | `InvalidOperationException`. |
| Completing a work order stamps `CompletedDate`. | `TransitionWorkOrderAsync`. | Pinned by `TransitionWorkOrder_ValidPath_DraftToCompleted`. |

## Related ADRs

- ADR 0015 — Module-Entity Registration (for when the persistence-backed service lands).
- ADR 0013 — Foundation.Integrations (for vendor-portal dispatch integrations).
- ADR 0022 — Example catalog + docs taxonomy. Block UID prefix is `block-maintenance-*`.

## Related pages

- [Entity Model](entity-model.md)
- [Service Contract](service-contract.md)
- [Workflow](workflow.md)
