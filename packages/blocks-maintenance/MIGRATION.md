# Sunfish.Blocks.Maintenance — Migration Guide

## v0.x → v1.0 (W#19 Phase 5; ADR 0053 A6)

**Type:** api-change-shape (MAJOR version bump per [`docs/adrs/0053-work-order-domain-model.md`](../../docs/adrs/0053-work-order-domain-model.md) amendment A6).

`WorkOrder` migrated from a positional record to an init-only record. The schema gained tenant attribution, the post-completion audit trail, and the Phase 3 child entities; it dropped the `MaintenanceRequestId RequestId` FK in favor of an audit-record-driven source resolver (Phase 5.1).

### Breaking changes

#### 1. `WorkOrder` constructor: positional → init-only

Before:

```csharp
new WorkOrder(
    Id: WorkOrderId.NewId(),
    RequestId: requestId,
    AssignedVendorId: vendorId,
    Status: WorkOrderStatus.Draft,
    ScheduledDate: new DateOnly(2026, 5, 1),
    CompletedDate: null,
    EstimatedCost: 100m,
    ActualCost: null,
    Notes: null,
    CreatedAtUtc: Instant.Now);
```

After:

```csharp
new WorkOrder
{
    Id = WorkOrderId.NewId(),
    Tenant = tenantId,                                // NEW (per IMustHaveTenant)
    AssignedVendorId = vendorId,
    Status = WorkOrderStatus.Draft,
    ScheduledDate = new DateOnly(2026, 5, 1),
    CompletedDate = null,
    EstimatedCost = Money.Usd(100m),                 // was decimal; now Money? (ADR 0051)
    TotalCost = null,                                 // was ActualCost: decimal?
    Notes = null,
    CreatedAtUtc = Instant.Now,
    UpdatedAt = DateTimeOffset.UtcNow,                // NEW
};
```

#### 2. `RequestId` dropped

`WorkOrder.RequestId` no longer exists. The originating `MaintenanceRequest` rides in the first `WorkOrderCreated` audit record's payload body (`source_kind` + `source_id`). W#19 Phase 5.1 wires `WorkOrderListBlock` to surface the source via an audit query.

`CreateWorkOrderRequest.RequestId` still exists — that's the caller's signal, captured into the audit trail. `ListWorkOrdersQuery.RequestId` filter is removed (no field to filter on); Phase 5.1 will reintroduce source-based filtering via audit query.

#### 3. Cost fields migrate to `Money`

| Before | After |
|---|---|
| `decimal EstimatedCost` (required) | `Money? EstimatedCost` (nullable; per ADR 0051) |
| `decimal? ActualCost` | `Money? TotalCost` (renamed) |
| `CreateWorkOrderRequest.EstimatedCost: decimal` (required) | `CreateWorkOrderRequest.EstimatedCost: Money?` (nullable) |

Use `Money.Usd(amount)` shorthand when constructing USD-denominated costs.

#### 4. New optional fields on `WorkOrder`

These are Phase 3 child-entity surfaces; default to null/empty:

- `Equipment: EquipmentId?` — optional FK to `blocks-property-equipment.Equipment`
- `Appointment: WorkOrderAppointment?`
- `CompletionAttestation: WorkOrderCompletionAttestation?`
- `EntryNotices: IReadOnlyList<WorkOrderEntryNotice>` — defaults to empty
- `AuditTrail: IReadOnlyList<Guid>` — defaults to empty; populated by audit emission
- `UpdatedAt: DateTimeOffset` — required; bumped on every state-mutating operation

`PrimaryThread: ThreadId?` (per ADR 0052) is added in **W#19 Phase 6** (cross-package wiring), not this PR.

#### 5. `WorkOrderListBlock.razor` — source column changed

The `Priority` column (which read from the originating `MaintenanceRequest`) is replaced by a `Source` column rendering `<small class="text-muted">see audit trail</small>` as a placeholder. **W#19 Phase 5.1 will restore source rendering via an audit-query roundtrip** — see the in-file `TODO Phase 5.1` comment.

### Why these changes

ADR 0053 amendment A6 reframes the work-order domain so:
- Tenant attribution is uniform across all entities (per `IMustHaveTenant`).
- The originating source is polymorphic (a maintenance request, a periodic schedule, a vendor proposal, a tenant request, etc.) — modeled via the audit trail rather than a typed FK.
- Costs are currency-bound (per ADR 0051 Payments substrate).
- The post-completion lifecycle (sign-off → invoice → payment → close) is durable and audit-emitted.

### Migration checklist for downstream consumers

- [ ] Update every `new WorkOrder(...)` constructor call to use init-only syntax.
- [ ] Add `Tenant` parameter at every `CreateWorkOrderRequest` build site.
- [ ] Replace `decimal EstimatedCost` with `Money? EstimatedCost` (use `Money.Usd(...)` or pass `null` if not yet estimated).
- [ ] Replace `wo.ActualCost` with `wo.TotalCost`.
- [ ] Drop any code reading `wo.RequestId` — resolve via audit query instead (Phase 5.1 pattern).
- [ ] Drop any code passing `RequestId` to `ListWorkOrdersQuery` — the filter no longer exists.
- [ ] If you render Work-Order priority from the originating request, port to the audit-query approach when Phase 5.1 ships.
