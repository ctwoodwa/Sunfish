---
uid: block-maintenance-service-contract
title: Maintenance — Service Contract
description: The IMaintenanceService public surface — vendors, requests, RFQ/quote flow, and work-order management.
keywords:
  - sunfish
  - maintenance
  - service-contract
  - imaintenance-service
  - accept-quote
  - concurrency
---

# Maintenance — Service Contract

## Overview

`IMaintenanceService` is the single public contract for the maintenance block. It groups
its operations by concern: vendors, maintenance requests, RFQ / quote, and work orders.
Lifecycle transitions are enforced — see [workflow.md](workflow.md) for the allowed
transition tables.

Source: `packages/blocks-maintenance/Services/IMaintenanceService.cs`

## Vendor methods

```csharp
ValueTask<Vendor> CreateVendorAsync(CreateVendorRequest request, CancellationToken ct = default);
ValueTask<Vendor?> GetVendorAsync(VendorId id, CancellationToken ct = default);
IAsyncEnumerable<Vendor> ListVendorsAsync(ListVendorsQuery query, CancellationToken ct = default);
```

`ListVendorsQuery.Empty` returns all vendors.

## Maintenance-request methods

```csharp
ValueTask<MaintenanceRequest> SubmitRequestAsync(
    SubmitMaintenanceRequest request, CancellationToken ct = default);

ValueTask<MaintenanceRequest> TransitionRequestAsync(
    MaintenanceRequestId id,
    MaintenanceRequestStatus newStatus,
    CancellationToken ct = default);

ValueTask<MaintenanceRequest?> GetRequestAsync(
    MaintenanceRequestId id, CancellationToken ct = default);

IAsyncEnumerable<MaintenanceRequest> ListRequestsAsync(
    ListRequestsQuery query, CancellationToken ct = default);
```

- `SubmitRequestAsync` always returns a request in `Submitted`.
- `TransitionRequestAsync` enforces the status graph
  ([Workflow — Maintenance request lifecycle](workflow.md#maintenance-request-lifecycle))
  and throws `InvalidOperationException` for forbidden transitions.

## RFQ / quote methods

```csharp
ValueTask<Rfq> SendRfqAsync(SendRfqRequest request, CancellationToken ct = default);
ValueTask<Quote> SubmitQuoteAsync(SubmitQuoteRequest request, CancellationToken ct = default);
ValueTask<Quote> AcceptQuoteAsync(QuoteId id, CancellationToken ct = default);
IAsyncEnumerable<Quote> ListQuotesAsync(MaintenanceRequestId requestId, CancellationToken ct = default);
```

- `SendRfqAsync` creates an RFQ in `RfqStatus.Sent`.
- `SubmitQuoteAsync` creates a quote in `QuoteStatus.Submitted`.
- **`AcceptQuoteAsync` is the atomic quote-to-work-order fan-out:**
  - Transitions the chosen quote to `Accepted`.
  - Declines every other quote for the same `MaintenanceRequestId`.
  - Creates a new `WorkOrder` linked to the accepted quote.

  The entire operation is serialized under a per-request lock so concurrent
  `AcceptQuoteAsync` calls on different quotes for the same request converge to exactly
  one accepted quote.

## Work-order methods

```csharp
ValueTask<WorkOrder> CreateWorkOrderAsync(
    CreateWorkOrderRequest request, CancellationToken ct = default);

ValueTask<WorkOrder> TransitionWorkOrderAsync(
    WorkOrderId id, WorkOrderStatus newStatus, CancellationToken ct = default);

ValueTask<WorkOrder?> GetWorkOrderAsync(
    WorkOrderId id, CancellationToken ct = default);

IAsyncEnumerable<WorkOrder> ListWorkOrdersAsync(
    ListWorkOrdersQuery query, CancellationToken ct = default);
```

- `CreateWorkOrderAsync` always returns a work order in `Draft`. (Work orders created
  indirectly via `AcceptQuoteAsync` are also in `Draft`.)
- `TransitionWorkOrderAsync` enforces the status graph
  ([Workflow — Work-order lifecycle](workflow.md#work-order-lifecycle)).

## Typical end-to-end workflow

1. **Register a vendor**: `CreateVendorAsync(new CreateVendorRequest(...))`.
2. **Submit a tenant-reported issue**: `SubmitRequestAsync(...)` → request in `Submitted`.
3. **Review**: `TransitionRequestAsync(id, UnderReview)`, then `Approved`.
4. **Send an RFQ to two plumbers**: `SendRfqAsync(new SendRfqRequest(...))`.
5. **Plumbers submit quotes**: `SubmitQuoteAsync(...)` for each.
6. **Accept the best quote**: `AcceptQuoteAsync(winningQuoteId)` — the losing quote is
   `Declined` automatically; a `WorkOrder` is created in `Draft`.
7. **Send and schedule**: `TransitionWorkOrderAsync(woId, Sent)`, then `Accepted`, then
   `Scheduled`.
8. **Execute**: `TransitionWorkOrderAsync(woId, InProgress)`, then `Completed`.

## Deferred follow-ups

- Deficiency → work-order auto-rollup (requires event-bus wiring).
- Vendor portal UI (quote submission and work-order acceptance by vendor).
- Quote-comparison UI helpers.
- Offline mobile work-order capture and photo/signature attachments.
- `BusinessRuleEngine` hookup.

## Default implementation

`InMemoryMaintenanceService` is registered by `AddInMemoryMaintenance`. State is held in
process; replace with a persistence-backed implementation for production.

## Query filters

| Query | Fields | Combinator |
|---|---|---|
| `ListVendorsQuery` | `Specialty?`, `Status?` | AND across all non-null fields. |
| `ListVendorsQuery.Empty` | — | Returns all vendors. |
| `ListRequestsQuery` | `PropertyId?`, `Priority?`, `Status?` | AND across all non-null fields. |
| `ListWorkOrdersQuery` | `RequestId?`, `AssignedVendorId?`, `Status?` | AND across all non-null fields. |
| `ListQuotesAsync(requestId)` | Request-scoped by parameter. | — |

`ListVendorsAsync(new ListVendorsQuery { Specialty = VendorSpecialty.Plumbing })` is
pinned by `ListVendorsAsync_FilterBySpecialty_ReturnsMatchingOnly`; the active/suspended
split is pinned by `ListVendorsAsync_FilterByActiveStatus_ExcludesSuspended`.

## Concurrency

The in-memory service protects each mutable collection with a lock. `AcceptQuoteAsync` uses
a per-request lock so a race on the same maintenance request produces exactly one accepted
quote. Two pinning tests cover this:

- `AcceptQuoteAsync_AcceptsTarget_DeclinesOthers_SpawnsWorkOrder` — single accept call:
  target → `Accepted`, all siblings → `Declined`, one `WorkOrder` in `Draft`.
- `AcceptQuoteAsync_IsAtomic_ConcurrentCallsConvergeToExactlyOneAccepted` — fires two
  parallel accepts on different quotes; asserts exactly one `Accepted`, at least one
  `Declined`, exactly one `WorkOrder` spawned. At most one call may throw (losing-racer
  sees its target already-declined).

## Error semantics

- Invalid lifecycle transitions throw `InvalidOperationException` with a message of the
  form `"Cannot transition {entity} from {from} to {to}. Allowed targets from {from}:
  [...]."`. See [Workflow — Transition-guard error messages](workflow.md#transition-guard-error-messages).
- `GetVendorAsync`, `GetRequestAsync`, `GetWorkOrderAsync` return `null` for unknown ids
  rather than throwing.
- `null` requests throw `ArgumentNullException` at the entry point.

## Registering a replacement

```csharp
services.AddInMemoryMaintenance();                                     // optional baseline
services.AddSingleton<IMaintenanceService, MyEfMaintenanceService>();  // wins by last-registered rule
```

A persistence-backed implementation must preserve the transition-guard semantics
(invalid target → `InvalidOperationException`) and the atomicity of `AcceptQuoteAsync`.
The two concurrency tests above are the primary conformance gates.

## Related pages

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- [Workflow](workflow.md)
