---
uid: block-properties-property-aggregation
title: Properties — Aggregating data across a property
description: Pattern for aggregating leases, inspections, and work orders for a single property via the PropertyUnit join layer.
keywords:
  - sunfish
  - blocks
  - properties
  - property-unit
  - aggregation
  - cockpit
---

# Properties — Aggregating data across a property

This guide explains how to roll lease, inspection, and work-order data up to a single `Property` for surfaces like the owner cockpit (W#29 dashboard, property detail) and reporting.

## The join layer

Lease and inspection records key off `EntityId UnitId`; work orders key off `PropertyId?`. Property-level aggregation walks two different paths depending on the source:

```text
Property (PropertyId)
  │
  ├─ IPropertyUnitRepository.ListByPropertyAsync  ──▶ PropertyUnit[]
  │     │                                              │
  │     └─ UnitId[] ──▶  ILeaseService.ListAsync       (filter Lease.UnitId ∈ UnitId[])
  │                       IInspectionsService          (filter Inspection.UnitId ∈ UnitId[])
  │
  └─ ListWorkOrdersAsync(PropertyId = …)               (direct: WorkOrder.PropertyId)
```

`Equipment` is a third path — `IEquipmentRepository.ListByPropertyAsync(tenant, propertyId)` keys directly off `PropertyId`.

## Property-detail aggregation

```csharp
var property = await _properties.GetByIdAsync(tenant, propertyId, ct);
if (property is null) return TypedResults.NotFound();

var equipment = await _equipment.ListByPropertyAsync(tenant, propertyId, includeDisposed: false, ct);

var units   = await _units.ListByPropertyAsync(tenant, propertyId, ct);
var unitIds = units.Select(u => u.Id).ToHashSet();

// Active lease — first match wins
LeaseSummaryDto? activeLease = null;
if (unitIds.Count > 0)
{
    await foreach (var lease in _leases.ListAsync(
        new ListLeasesQuery { Phase = LeasePhase.Active }, ct))
    {
        if (!unitIds.Contains(lease.UnitId)) continue;
        activeLease = MapLeaseSummary(lease);
        break;
    }
}

// Last inspection across all units
Inspection? latest = null;
if (unitIds.Count > 0)
{
    await foreach (var inspection in _inspections.ListInspectionsAsync(
        new ListInspectionsQuery(), ct))
    {
        if (!unitIds.Contains(inspection.UnitId)) continue;
        if (latest is null || inspection.ScheduledDate > latest.ScheduledDate)
            latest = inspection;
    }
}

// Open work-order count
var openWorkOrderCount = 0;
await foreach (var wo in _maintenance.ListWorkOrdersAsync(
    new ListWorkOrdersQuery { PropertyId = propertyId }, ct))
{
    if (wo.Status is WorkOrderStatus.Closed or WorkOrderStatus.Cancelled) continue;
    openWorkOrderCount++;
}
```

## Dashboard aggregation

The cockpit dashboard reuses the same join layer with four extra rollups:

| Metric | Source |
|---|---|
| **Vacancy rate** | `units.Count(u => u.Status == UnitStatus.Available) / units.Count` |
| **Renewal radar (30/60/90)** | Active leases on units in property, bucket by `EndDate − today` |
| **WO rollup** | `ListWorkOrdersAsync(PropertyId = …)` grouped by status (`Open`/`InProgress`/`Blocked` collapse the 13 native states) |
| **Overdue inspections** | Units with no inspection OR latest inspection > 12 months ago |

## Performance notes

For Phase 1 the lease + inspection paths post-filter on `UnitId` because `ListLeasesQuery` and `ListInspectionsQuery` do not yet carry a multi-unit filter. This is acceptable for the property volumes the cockpit serves (single-LLC owner, ≤20 properties); a `UnitIds: IReadOnlyList<EntityId>` filter on both query records is the natural upgrade path when volumes grow.

The work-order path already filters by `PropertyId` at the service so it does not have this cost.

## See also

- [PropertyUnit reference](xref:block-properties-property-unit) — entity + repository surface
- [Owner cockpit overview](xref:block-cockpit-overview) — consumer surfaces (property detail, dashboard)
