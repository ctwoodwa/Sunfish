---
sort_order: 71
number: 62
slug: blocks-properties-property-unit-substrate
title: "PropertyUnit substrate — additive extension of `blocks-properties`; ships `PropertyUnit` entity + `IPropertyUnitRepository`; unblocks W#29 Phase 1.5 (real property-detail aggregation) and WorkOrder.PropertyId FK"
status: "ready-to-build"
status_cell: "`ready-to-build` — hand-off authored 2026-05-16; no prerequisites; immediately buildable; ~8-12h / 3-4 PRs"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/blocks-properties-property-unit-substrate-stage06-handoff.md`"
---

## Notes

**Hand-off ready 2026-05-16.** `Property.cs` first-slice explicitly deferred `PropertyUnit`
to a follow-up hand-off (see comment in the file). W#62 delivers that follow-up.

**Root cause discovered in W#29 Phase 2:** `blocks-leases` (`Lease.UnitId: EntityId`) and
`blocks-inspections` (`Inspection.UnitId: EntityId`) reference units, but there is no
`IPropertyUnitRepository` to resolve `PropertyId → UnitId[]`. W#29 Phase 2 shipped with
stubbed aggregation fields pending W#62.

Phase 1: `UnitStatus` enum + `PropertyUnit` entity (`EntityId`-based ID with scheme `"unit"`)
+ `IPropertyUnitRepository` + `InMemoryPropertyUnitRepository` + EFCore entity-module +
DI registration + 5 unit tests (~3-4h).

Phase 2 (W#29 Phase 1.5): Upgrade `PropertyDetailEndpoint.cs` to real aggregation via
`IPropertyUnitRepository.ListByPropertyAsync` → lease + inspection in-memory joins (~2-3h).

Phase 3: `WorkOrder.PropertyId?` nullable FK + `ListWorkOrdersQuery.PropertyId` filter +
open-WO count wired in cockpit (~2-3h).

Phase 4: Docs + ledger flip (~30min).

**Unblocks:** W#29 Phase 1.5 (property-detail real aggregation) + W#29 Phase 5 (dashboard
property-level KPIs). Also enables future W#22/W#25/W#27 joins on PropertyUnit.
