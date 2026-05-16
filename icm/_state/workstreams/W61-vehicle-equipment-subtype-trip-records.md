---
sort_order: 70
number: 61
slug: vehicle-equipment-subtype-trip-records
title: "Vehicle Equipment Subtype + Trip Records — additive extension of `blocks-property-equipment`; ships `VehicleMetadata`, `TripRecord`, `ITripStore`, `MileageRecorded` lifecycle event type; unblocks W#23.5 iOS Mileage capture flow"
status: "ready-to-build"
status_cell: "`ready-to-build` — hand-off authored 2026-05-15; no prerequisites; immediately buildable; ~3-5h / 2 PRs"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/property-equipment-vehicle-trip-records-stage06-handoff.md`"
---

## Notes

**Hand-off ready 2026-05-15.** Additive extension to `blocks-property-equipment` (W#24, built).
`EquipmentClass.Vehicle` was reserved in the first-slice hand-off with a note that
"Vehicle subtype + Trip events gated on follow-up hand-off." This is that follow-up.

Phase 1: `VehicleMetadata` record + `VehicleData` field on `Equipment` + `TripRecordId` +
`TripRecord` entity + `ITripStore` + `InMemoryTripStore` + `MileageRecorded` enum value +
EFCore entity-module extension + DI registration + 5 unit tests (~2-3h).
Phase 2: docs + ledger flip (~30min).

**Unblocks:** W#23.5 (iOS Mileage capture flow hand-off can be authored once this ships).
