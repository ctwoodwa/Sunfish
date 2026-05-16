---
sort_order: 71
number: 62
slug: blocks-properties-property-unit-substrate
title: "**PropertyUnit substrate** (blocks-properties additive extension; `sunfish-feature-change` pipeline) — closes the Lease/Inspection FK gap; unblocked W#29 Phase 1.5 + Phase 5"
status: "built"
status_cell: "`built` — All 4 phases merged. PR 1 (#860) PropertyUnit entity + IPropertyUnitRepository + InMemory + EFCore + DI. PR 2 (#861) wired W#29 Phase 1.5 PropertyDetail aggregation (active lease + last inspection via PropertyUnit join). PR 3 (#862) added WorkOrder.PropertyId FK + ListWorkOrdersQuery.PropertyId + cockpit open-WO count. PR 4 (this) docs + ledger flip."
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/blocks-properties-property-unit-substrate-stage06-handoff.md` + `apps/docs/blocks/properties/property-unit.md` + `apps/docs/blocks/properties/property-aggregation.md`"
---

## Notes

**Authored 2026-05-16 in response to the W#29 PR 2 halt** (`Lease`/`Inspection`/`WorkOrder` lacked the FKs needed to aggregate per property). `Property.cs` doc-comment explicitly deferred `PropertyUnit` to a follow-up; W#62 is that follow-up.

**Surface added:**

- `Sunfish.Blocks.Properties.Models.PropertyUnit` — `IMustHaveTenant`; `EntityId` scheme `"unit"`; `PropertyId` FK; `UnitNumber` + `Bedrooms?` + `Bathrooms?` + `SquareFootage?` + `Status` (Available/Occupied/MaintenanceHold) + `CreatedAt` + `Notes?`. Static `NewId(tenant)` factory matches the `EntityId UnitId` FK shape already used by `blocks-leases` and `blocks-inspections`.
- `Sunfish.Blocks.Properties.Services.IPropertyUnitRepository` — `GetByIdAsync`, `ListByPropertyAsync`, `ListByTenantAsync`, `UpsertAsync`; tenant-scoped on every call.
- `InMemoryPropertyUnitRepository` — `ConcurrentDictionary<(TenantId, EntityId), PropertyUnit>` backing.
- `PropertyUnitEntityConfiguration` — `properties_property_unit` table; string-converted `EntityId` / `PropertyId` / `TenantId`; indexes on `(TenantId, PropertyId)` + `(TenantId)`.

**API-change surface (additive, no breaking):**

- `WorkOrder.PropertyId` nullable (existing call sites + AcceptQuote auto-WO path compile unchanged).
- `CreateWorkOrderRequest.PropertyId` nullable; `InMemoryMaintenanceService.CreateWorkOrderAsync` flows it.
- `ListWorkOrdersQuery.PropertyId` nullable filter; honored by `InMemoryMaintenanceService.ListWorkOrdersAsync`.

**Consumer:** Bridge `PropertyDetailEndpoint` (W#29 PR 2 + 1.5 + 3 upgrade path) and `DashboardEndpoint` (W#29 PR 5) both walk the new join layer. Equipment continues to query directly via `IEquipmentRepository.ListByPropertyAsync`.

**Tests:** 5/5 unit tests on `IPropertyUnitRepository` (PR 1); 3/3 cockpit aggregation tests (PR 2 — active lease + last inspection + cross-property isolation); 1 cockpit count test (PR 3 — Draft/Sent count; Cancelled excluded; other-property excluded); 121/121 blocks-maintenance tests passed without regression after `WorkOrder.PropertyId` addition.

**Deferred:** EFCore index on `WorkOrder.(TenantId, PropertyId)` — blocks-maintenance has no EFCore module today (in-memory only); index lands alongside the module when introduced.

**Pipeline:** ICM `sunfish-feature-change` variant; built 4 PRs in ~6h.
