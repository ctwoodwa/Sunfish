# Sunfish.Blocks.PropertyEquipment

Block for the equipment-management spine of the property-operations cluster — physical-equipment tracking, lifecycle events, and condition-assessment integration with `blocks-inspections`.

Renamed from `blocks-property-assets` per UPF Rule 4 (2026-04-28) to disambiguate from foundation-tier `Sunfish.Foundation.Assets.Common`. The cluster's physical-equipment entity is now named `Equipment` (industry-standard for facilities management).

## What this ships

### Models

- **`Equipment`** — root entity (`IMustHaveTenant`); FK to `Sunfish.Blocks.Properties.Property` via `PropertyId`.
- **`EquipmentId`** — Guid-wrapper record struct.
- **`EquipmentClass`** — enum discriminator (HVAC / Plumbing / Electrical / Appliance / Structural / Landscaping / Security / Fire / Pool / Other).
- **`EquipmentLifecycleEvent`** — append-only log entry (Installed / Serviced / Replaced / Decommissioned / Inspected); referenced by `WorkOrder` completion attestations.
- **`EquipmentCondition`** — enum projection (Good / Fair / Poor / Failed).

### Services

- **`IEquipmentRepository`** + `InMemoryEquipmentRepository` — CRUD + per-property enumeration.
- **`IEquipmentLifecycleLog`** + `InMemoryEquipmentLifecycleLog` — append-only event store + projection.
- **`PropertyEquipmentEntityModule`** — `ISunfishEntityModule` contribution.

## DI

```csharp
services.AddInMemoryPropertyEquipment();
```

## Cluster role

Consumed by:

- **`blocks-inspections`** — `EquipmentConditionAssessment` child entity references `EquipmentId` (PR #222 extension).
- **`blocks-maintenance`** — `WorkOrder.Equipment` FK target; entry-notice + completion-attestation tied to specific equipment.

## Deferred follow-ups

- Vehicle subtype + Trip events (mileage tracking; planned per the property-ops cluster intake)
- OCR ingest for receipt/invoice → equipment lifecycle event
- Kitchen-sink seed page

## ADR map

- [ADR 0015](../../docs/adrs/0015-module-entity-registration.md) — module-entity registration pattern
- [ADR 0053](../../docs/adrs/0053-work-order-domain-model.md) — Equipment-Field on WorkOrder + state-machine composition
- UPF Rule 4 (Equipment rename): `icm/07_review/output/property-ops-cluster-naming-upf-review-2026-04-28.md`

## See also

- [apps/docs Overview](../../apps/docs/blocks/property-equipment/overview.md)
- [Sunfish.Blocks.Properties](../blocks-properties/README.md) — cluster spine
- [Sunfish.Blocks.Maintenance](../blocks-maintenance/README.md) — work-order consumer
