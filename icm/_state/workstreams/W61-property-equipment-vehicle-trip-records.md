---
sort_order: 70
number: 61
slug: property-equipment-vehicle-trip-records
title: "**Vehicle Equipment Subtype + Trip Records** (additive extension of `blocks-property-equipment`; `sunfish-feature-change` pipeline) — `VehicleMetadata`, `TripRecord`, `ITripStore`, `MileageRecorded`; unblocks W#23.5 iOS Mileage capture"
status: "built"
status_cell: "`built` — Phase 1 + 2 in one PR. `VehicleMetadata` + `TripRecord` + `TripRecordId` + `MileageRecorded` lifecycle event; `ITripStore` + `InMemoryTripStore` (negative-miles guard + parent-odometer update); EFCore `TripRecord` configuration with `(TenantId, EquipmentId)` + `(TenantId, PropertyId)` indexes + `VehicleMetadata` owned complex type on Equipment; DI registers `ITripStore`. 6 trip-store unit tests + 39 full equipment suite (no regressions)."
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/property-equipment-vehicle-trip-records-stage06-handoff.md` + `apps/docs/blocks/property-equipment/vehicle-trip-records.md`"
---

## Notes

Additive extension to `blocks-property-equipment` (W#24, built). `EquipmentClass.Vehicle` was a reserved discriminator with a doc-comment promising "full subtype (VIN, mileage Trip events) gated on follow-up hand-off." W#61 ships that follow-up.

**Surface added:**

- `Sunfish.Blocks.PropertyEquipment.Models.VehicleMetadata` — VIN, Make, Model, Year, LicensePlate, CurrentOdometer. Optional on `Equipment.VehicleData`; non-null only when `Class == Vehicle`.
- `Sunfish.Blocks.PropertyEquipment.Models.TripRecord` — `IMustHaveTenant`; append-only mileage log keyed on `EquipmentId` + `PropertyId`; computed `Miles` clamped to ≥ 0.
- `Sunfish.Blocks.PropertyEquipment.Models.TripRecordId` — string-backed strong type with `NewId()` + `JsonConverter`.
- `EquipmentLifecycleEventType.MileageRecorded` — new event-type discriminator.
- `Sunfish.Blocks.PropertyEquipment.Services.ITripStore` + `InMemoryTripStore` — append-only contract with negative-miles guard; parent vehicle's `CurrentOdometer` follows the latest `EndOdometer`.
- `Data/TripRecordEntityConfiguration` — `property_equipment_trip_record` table; string-converted ids; indexes on `(TenantId, EquipmentId)` + `(TenantId, PropertyId)`.
- `Data/EquipmentEntityConfiguration` extended — `VehicleMetadata` mapped via `OwnsOne` (precision 12,2 on `CurrentOdometer`).

**DI:** `AddInMemoryPropertyEquipment()` now also registers `ITripStore` → `InMemoryTripStore`.

**Tests:** 6 W#61 unit tests in `TripRecordStoreTests` (append + get-for-equipment; Miles positive case; Miles guard case; GetAsync null on unknown; CurrentOdometer follows latest; AppendAsync throws on Start>End). 39/39 full property-equipment suite passes (no regression).

**Unblocks:** W#23.5 iOS Mileage capture flow (gated on this surface).

**Deferred:** EFCore migration script (no migration tooling in the package today).

**Pipeline:** ICM `sunfish-feature-change` variant; built in ~3h (1 PR with code + docs + ledger).
