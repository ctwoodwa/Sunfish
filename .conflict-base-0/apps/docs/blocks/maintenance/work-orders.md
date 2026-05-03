# Work Orders

`Sunfish.Blocks.Maintenance` v1.0 ships the full Work Order domain model per [ADR 0053](../../../docs/adrs/0053-work-order-domain-model.md). This page is the consumer-facing reference for the 13-state lifecycle, audit emission, child entities, and cross-package wiring.

> **MAJOR version bump (v0.x → v1.0).** See [`MIGRATION.md`](https://github.com/ctwoodwa/Sunfish/blob/main/packages/blocks-maintenance/MIGRATION.md) for the breaking changes (positional → init-only `WorkOrder`, dropped `RequestId`, `decimal` → `Money?` migration).

## Lifecycle

13 statuses + `Cancelled` terminal-from-anywhere-pre-`Closed`:

```text
Draft → Sent → Accepted → Scheduled → InProgress → Completed
                                       ↓               ↓
                                       OnHold          AwaitingSignOff → Invoiced
                                       (Resume)                              ↓
                                                                            Paid → Closed
                                                                              ↓
                                                                            Disputed → (Invoiced | Paid | Closed)
```

`Cancelled` is reachable from any non-terminal state. `Closed` is the final terminal.

## Audit emission (18 `AuditEventType` constants per ADR 0053 A8)

Each lifecycle event emits exactly one `AuditRecord` (when `IAuditTrail` + `IOperationSigner` are wired):

| Event type | Emitted by |
|---|---|
| `WorkOrderCreated` | `CreateWorkOrderAsync` (with `source_kind` + `source_id` body keys for the originating record) |
| 13 transition events | `TransitionWorkOrderAsync` per arrow (`WorkOrderSent` / `Accepted` / `Scheduled` / `Started` / `Held` / `Resumed` / `Completed` / `SignedOff` / `Invoiced` / `Paid` / `Disputed` / `Closed` / `Cancelled`) |
| `WorkOrderEntryNoticeRecorded` | `RecordEntryNoticeAsync` |
| `WorkOrderAppointmentScheduled` | `ProposeAppointmentAsync` |
| `WorkOrderAppointmentConfirmed` | `ConfirmAppointmentAsync` |
| `WorkOrderCompletionAttestationCaptured` | `CaptureCompletionAttestationAsync` |

Audit emission is opt-in: pass `IAuditTrail` + `IOperationSigner` + `TenantId` to the `InMemoryMaintenanceService` constructor. Without them, audit records are silently skipped.

## Child entities (Phase 3)

Three child types ride alongside `WorkOrder`:

| Entity | Purpose | Service method |
|---|---|---|
| `WorkOrderEntryNotice` | Right-of-entry notice (multiple per WO) | `RecordEntryNoticeAsync` |
| `WorkOrderCompletionAttestation` | Signature-bound completion attestation (per ADR 0054) | `CaptureCompletionAttestationAsync` |
| `WorkOrderAppointment` | Appointment slot with overlap detection (per ADR 0028; in-memory lock for Phase 1) | `ProposeAppointmentAsync` + `ConfirmAppointmentAsync` |

`AppointmentStatus` enum: `Proposed` → `Confirmed` | `Cancelled`.

## Cross-package wiring (Phase 6)

`InMemoryMaintenanceService` accepts three optional dependencies for cross-package wiring; each is null-disabled.

| Wiring | When invoked |
|---|---|
| `IThreadStore` (per ADR 0052) | `CreateWorkOrderAsync` opens a 2-party (operator + vendor) coordination thread when `request.CreateThread = true` (default); sets `WorkOrder.PrimaryThread`. |
| `IPaymentGateway` (per ADR 0051) | `TransitionWorkOrderAsync` authorizes on `Invoiced`; captures the same authorization on `Paid`. The auth handle is per-WorkOrder. |
| `ISignatureCapture` (per ADR 0054) | Available for future hand-offs that mint signature events from within the service; not wired into a transition path in Phase 6. |

`ITaxonomyResolver` (per ADR 0056) is also indirectly consumed when ADR 0054's Pattern E lands — `SignatureScope` becomes `IReadOnlyList<TaxonomyClassification>` referencing `Sunfish.Signature.Scopes@1.0.0` from W#31.

## Source resolution (Phase 5.1)

Phase 5 dropped `WorkOrder.RequestId`. The originating `MaintenanceRequest` is recovered from the first `WorkOrderCreated` audit record's payload body (`source_kind` + `source_id`). Phase 5.1 wired `WorkOrderListBlock.razor` to do this lookup; consumers can use the same pattern:

```csharp
var query = new AuditQuery(workOrder.Tenant, AuditEventType.WorkOrderCreated);
await foreach (var record in auditTrail.QueryAsync(query, ct))
{
    var body = record.Payload.Payload.Body;
    if (body.TryGetValue("work_order_id", out var v) && v is string s && s == workOrder.Id.Value)
    {
        var sourceKind = body.GetValueOrDefault("source_kind") as string;
        var sourceId = body.GetValueOrDefault("source_id") as string;
        // ... resolve via the appropriate service per sourceKind ...
        break;
    }
}
```

## Full lifecycle example

```csharp
services.AddInMemoryMaintenance();   // when the in-memory wiring extension exists; otherwise wire by hand:
var service = new InMemoryMaintenanceService(
    auditTrail, signer, tenantId,
    threadStore: messagingThreadStore,
    paymentGateway: paymentGateway,
    signatureCapture: signatureCapture);

// Submit + create
var request = await service.SubmitRequestAsync(new SubmitMaintenanceRequest { ... });
var workOrder = await service.CreateWorkOrderAsync(new CreateWorkOrderRequest
{
    Tenant = tenantId,
    RequestId = request.Id,
    AssignedVendorId = vendorId,
    ScheduledDate = new DateOnly(2026, 5, 1),
    EstimatedCost = Money.Usd(250m),
});
// workOrder.PrimaryThread is now set (operator+vendor thread opened).

// Lifecycle
await service.TransitionWorkOrderAsync(workOrder.Id, WorkOrderStatus.Sent);
// ... Accepted → Scheduled → InProgress → Completed → AwaitingSignOff → Invoiced (auto-authorizes payment) → Paid (auto-captures) → Closed.

// Child entities at any point
await service.RecordEntryNoticeAsync(new WorkOrderEntryNotice { ... }, actorId);
await service.ProposeAppointmentAsync(new WorkOrderAppointment { ... }, actorId);
await service.CaptureCompletionAttestationAsync(new WorkOrderCompletionAttestation { ... }, actorId);
```

## See also

- [ADR 0053](../../../docs/adrs/0053-work-order-domain-model.md) — Work Order Domain Model + amendments A1–A9
- [`MIGRATION.md`](https://github.com/ctwoodwa/Sunfish/blob/main/packages/blocks-maintenance/MIGRATION.md) — v0.x → v1.0 breaking changes
- [W#19 hand-off](../../../icm/_state/handoffs/property-work-orders-stage06-handoff.md) — full 8-phase decomposition
- [W#19 Phase 3 prereq addendum](../../../icm/_state/handoffs/property-work-orders-stage06-addendum.md) — Money / ThreadId stub introduction
- [W#19 Phase 5 UX addendum](../../../icm/_state/handoffs/property-work-orders-stage06-phase5-addendum.md) — `WorkOrderListBlock` Phase 5.1 pattern
- [Foundation.Integrations Payments page](../../foundation/integrations/payments.md) — `Money` + `IPaymentGateway` stub
- [Foundation.Integrations Signatures page](../../foundation/integrations/signatures.md) — `SignatureEventRef` + `ISignatureCapture` stub
- [blocks-messaging overview](../messaging/overview.md) — `IThreadStore` provider
