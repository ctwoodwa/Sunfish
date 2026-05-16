---
uid: block-properties-property-unit
title: Properties — PropertyUnit
description: The PropertyUnit child entity of Property; tenant-scoped unit substrate that closes the Lease/Inspection FK join.
keywords:
  - sunfish
  - blocks
  - properties
  - property-unit
  - units
  - multitenancy
---

# Properties — PropertyUnit

`PropertyUnit` is the rentable / manageable child entity of a [`Property`](xref:block-properties-overview). It carries the unit-level fields that downstream domains key off — `Bedrooms`, `Bathrooms`, `SquareFootage`, operational `Status` — and serves as the **join layer** between properties and unit-scoped data in `blocks-leases` and `blocks-inspections`.

Shipped in W#62 Phase 1.

## Why a child entity

`Property` ships with a `Units?` summary collection on the parent, but lease and inspection records do not FK directly to `Property`. They reference an `EntityId UnitId` (wire form `unit:{authority}/{localPart}`). Without a `PropertyUnit` entity + repository, there is no way to walk:

```text
Property → (?) → Lease
Property → (?) → Inspection
```

`PropertyUnit` provides that missing layer:

```text
Property ──IPropertyUnitRepository.ListByPropertyAsync──▶ PropertyUnit[]
                                                              │
                                                              ▼
                                       Lease.UnitId / Inspection.UnitId
```

## Model

The minimum fields a unit carries:

| Field | Type | Notes |
|---|---|---|
| `Id` | `EntityId` | Scheme = `"unit"`; use `PropertyUnit.NewId(tenant)` |
| `TenantId` | `TenantId` | Required (`IMustHaveTenant`); persistence adapters reject default |
| `PropertyId` | `PropertyId` | FK to parent property |
| `UnitNumber` | `string` | Human-readable label (`"101"`, `"2B"`, `"Main"`, `"Garage"`) |
| `Bedrooms` | `int?` | Optional; null for non-residential or unknown |
| `Bathrooms` | `decimal?` | Allows half-baths (`1.5`) |
| `SquareFootage` | `decimal?` | Interior square feet |
| `Status` | `UnitStatus` | `Available` / `Occupied` / `MaintenanceHold` |
| `CreatedAt` | `DateTimeOffset` | Immutable after first persist |
| `Notes` | `string?` | Free text |

`UnitStatus`:

```csharp
public enum UnitStatus
{
    Available,
    Occupied,
    MaintenanceHold,
}
```

## Generating ids

```csharp
var unitId = PropertyUnit.NewId(tenantId);
// → EntityId { Scheme = "unit", Authority = tenantId.Value, LocalPart = guid("N") }
// → string form: "unit:acme-rentals/550e8400e29b41d4a716446655440000"
```

The `"unit"` scheme matches the `EntityId UnitId` already declared by `blocks-leases/Lease.UnitId` and `blocks-inspections/Inspection.UnitId`, so PropertyUnit→Lease / PropertyUnit→Inspection joins are an O(1) `HashSet<EntityId>.Contains` check.

## Repository

```csharp
public interface IPropertyUnitRepository
{
    Task<PropertyUnit?> GetByIdAsync(TenantId tenant, EntityId id, CancellationToken ct = default);
    Task<IReadOnlyList<PropertyUnit>> ListByPropertyAsync(TenantId tenant, PropertyId propertyId, CancellationToken ct = default);
    Task<IReadOnlyList<PropertyUnit>> ListByTenantAsync(TenantId tenant, CancellationToken ct = default);
    Task UpsertAsync(PropertyUnit unit, CancellationToken ct = default);
}
```

Tenant-scoped on every call; the repository never returns units from other tenants.

## DI

```csharp
services.AddInMemoryProperties();
// Now registered:
//   IPropertyRepository      → InMemoryPropertyRepository
//   IPropertyUnitRepository  → InMemoryPropertyUnitRepository
//   ISunfishEntityModule     → PropertiesEntityModule
```

The `PropertyUnitEntityConfiguration` mapping lands automatically via the entity module's `ApplyConfigurationsFromAssembly` (table: `properties_property_unit`; indexes on `(TenantId, PropertyId)` and `(TenantId)`).

## See also

- [Property aggregation guide](xref:block-properties-property-aggregation) — how to walk Property → Unit → {Lease, Inspection, WorkOrder}
- [Owner cockpit overview](xref:block-cockpit-overview) — the consumer of the unit substrate
