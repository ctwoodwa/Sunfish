---
uid: block-property-equipment-vehicle-trip-records
title: Property Equipment — Vehicle subtype + trip records
description: VehicleMetadata extension on Equipment plus the append-only TripRecord log for mileage tracking.
keywords:
  - sunfish
  - property-equipment
  - vehicle
  - mileage
  - trip
  - odometer
---

# Property Equipment — Vehicle subtype + trip records

`EquipmentClass.Vehicle` reserves the discriminator; W#61 adds the matching field surface:

- `VehicleMetadata` on `Equipment` carries VIN, make, model, year, license plate, and the current odometer reading
- `TripRecord` + `ITripStore` capture an append-only mileage log
- `MileageRecorded` enriches the `EquipmentLifecycleEventType` enum
- iOS Mileage capture (W#23.5) reads/writes this surface

## VehicleMetadata

Non-null only when `Equipment.Class == Vehicle`; null on every other class.

| Field | Type | Notes |
|---|---|---|
| `Vin` | `string?` | 17-char VIN; not validated |
| `Make` | `string?` | e.g. `"Ford"` |
| `Model` | `string?` | e.g. `"F-150"` |
| `Year` | `int?` | e.g. `2019` |
| `LicensePlate` | `string?` | Conventionally `"{state-abbr} {plate}"` |
| `CurrentOdometer` | `decimal` | Updated by `ITripStore.AppendAsync` |

## TripRecord

An append-only record of a vehicle trip. Miles are computed as `EndOdometer − StartOdometer`, clamped to ≥ 0.

| Field | Type | Notes |
|---|---|---|
| `Id` | `TripRecordId` | Use `TripRecordId.NewId()` |
| `TenantId` | `TenantId` | Required (`IMustHaveTenant`) |
| `EquipmentId` | `EquipmentId` | FK; must reference a Vehicle |
| `PropertyId` | `PropertyId` | FK to property the trip terminated at |
| `TripDate` | `DateTimeOffset` | UTC date |
| `StartOdometer` | `decimal` | Miles at trip start |
| `EndOdometer` | `decimal` | Miles at trip end |
| `Miles` | `decimal` | Computed; `Max(0, End − Start)` |
| `Purpose` | `string?` | e.g. `"maintenance"`, `"inspection"`, `"delivery"` |
| `Notes` | `string?` | Free text |

## ITripStore

```csharp
public interface ITripStore
{
    Task<TripRecord?> GetAsync(TripRecordId id, CancellationToken ct = default);
    Task<IReadOnlyList<TripRecord>> GetForEquipmentAsync(EquipmentId equipmentId, CancellationToken ct = default);
    Task AppendAsync(TripRecord record, CancellationToken ct = default);
}
```

`AppendAsync` MUST reject records where `StartOdometer > EndOdometer` (negative-miles guard) by throwing `ArgumentException`. The in-memory implementation additionally updates the parent vehicle's `VehicleMetadata.CurrentOdometer` to `EndOdometer` when the trip extends the latest known reading.

## DI

```csharp
services.AddInMemoryPropertyEquipment();
// Now registered:
//   IEquipmentLifecycleEventStore → InMemoryEquipmentLifecycleEventStore
//   IEquipmentRepository          → InMemoryEquipmentRepository
//   ITripStore                    → InMemoryTripStore
//   ISunfishEntityModule          → PropertyEquipmentEntityModule
```

The `TripRecordEntityConfiguration` is picked up automatically via the entity module's `ApplyConfigurationsFromAssembly` (table: `property_equipment_trip_record`; indexes on `(TenantId, EquipmentId)` and `(TenantId, PropertyId)`).

## Lifecycle event

Appending a trip emits a `MileageRecorded` event on `IEquipmentLifecycleEventStore` so downstream consumers (audit, reporting) can react. The event carries the trip id; resolve the full payload via `ITripStore.GetAsync`.

## See also

- [Property aggregation guide](xref:block-properties-property-aggregation) — per-property data aggregation including equipment
- W#23 iOS Field app — `Mileage` capture flow (W#23.5) consumes this surface
