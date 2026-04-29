---
uid: block-property-equipment-overview
title: Property-Equipment — Overview
description: Equipment entity (FK to Property), EquipmentClass discriminator, EquipmentLifecycleEvent append-only log, in-memory repository, and ISunfishEntityModule contribution for the property-operations vertical.
keywords:
  - sunfish
  - blocks
  - property-equipment
  - equipment
  - real-estate
  - property-management
  - lifecycle
  - depreciation
  - multitenancy
---

# Property-Equipment — Overview

## What this block is

`Sunfish.Blocks.PropertyEquipment` is the **inventory backbone** of the property-operations vertical.
Each `Equipment` record (water heater, HVAC, appliance, roof, vehicle, etc.) FKs to a
[`Property`](../properties/overview.md) and provides the substrate for inspections, work orders,
receipts (acquisition cost basis), and depreciation reporting.

> **Naming note.** The cluster's physical-equipment entity is named `Equipment` (not `Asset`)
> per UPF Rule 4 to disambiguate from foundation-tier `Sunfish.Foundation.Assets.Common.EntityId`
> (the generic-entity reference). Both concepts coexist; "Asset" stays as the foundation-tier
> term, "Equipment" is the property-management term.

This is the first-slice scope. The block ships:

- `Equipment` — the inventory entity, `IMustHaveTenant`-scoped, FK to `Property`.
- `EquipmentClass` — coarse classification (`WaterHeater | HVAC | Appliance | Roof | Vehicle | Plumbing | Electrical | Other`).
- `EquipmentId` — opaque, JSON-round-trippable strong-typed identifier.
- `WarrantyMetadata` — embedded value object on `Equipment.Warranty`.
- `EquipmentLifecycleEvent` + `EquipmentLifecycleEventType` — append-only event log (Installed / Serviced / Inspected / WarrantyClaimed / Replaced / Disposed / PhotoAdded / NotesUpdated).
- `IEquipmentRepository` + `InMemoryEquipmentRepository` — Get / List (by tenant, property, class) / Upsert / SoftDelete (which also emits a `Disposed` lifecycle event).
- `IEquipmentLifecycleEventStore` + `InMemoryEquipmentLifecycleEventStore` — append + query by equipment + by property.
- `PropertyEquipmentEntityModule` — the `ISunfishEntityModule` contribution per ADR 0015.

## Package

- Package: `Sunfish.Blocks.PropertyEquipment`
- Source: `packages/blocks-property-equipment/`
- Naming convention: cluster siblings under the property-operations vertical use the `blocks-property-*` prefix (root entity stays as `blocks-properties`).
- Namespace roots:
  - `Sunfish.Blocks.PropertyEquipment.Models`
  - `Sunfish.Blocks.PropertyEquipment.Services`
  - `Sunfish.Blocks.PropertyEquipment.Data`
  - `Sunfish.Blocks.PropertyEquipment.DependencyInjection`

## Field reference — `Equipment`

| Field | Type | Required | Notes |
|---|---|---|---|
| `Id` | `EquipmentId` | yes | Opaque; `EquipmentId.NewId()` for fresh records. |
| `TenantId` | `TenantId` | yes | Owning LLC; `IMustHaveTenant`. |
| `Property` | `PropertyId` | yes | FK to parent property. No orphan equipment. |
| `Class` | `EquipmentClass` | yes | Coarse classification. |
| `DisplayName` | `string` | yes | Human-friendly name. |
| `Make` | `string?` | no | Manufacturer. |
| `Model` | `string?` | no | Model number. |
| `SerialNumber` | `string?` | no | From the nameplate. |
| `LocationInProperty` | `string?` | no | Free-text within-property location. |
| `InstalledAt` | `DateTimeOffset?` | no | |
| `AcquisitionCost` | `decimal?` | no | USD assumed; will migrate to typed `Money` after ADR 0051 acceptance. |
| `AcquisitionReceiptRef` | `string?` | no | Opaque receipt FK; will migrate to typed `ReceiptId` when the Receipts module ships. |
| `ExpectedUsefulLifeYears` | `int?` | no | Depreciation projection input (downstream). |
| `Warranty` | `WarrantyMetadata?` | no | Embedded; `OwnsOne` in EF. |
| `Notes` | `string?` | no | Free-text. |
| `PrimaryPhotoBlobRef` | `string?` | no | Opaque blob FK; ingest pipeline gated on cluster OQ3. |
| `CreatedAt` | `DateTimeOffset` | yes | Immutable after first persist. |
| `DisposedAt` | `DateTimeOffset?` | no | Soft-delete marker. |
| `DisposalReason` | `string?` | no | Free-text reason (replaced, sold, scrapped). |

## Lifecycle event types

| Type | Captured for |
|---|---|
| `Installed` | First commissioning at the property. |
| `Serviced` | Maintenance, tune-up, repair. |
| `Inspected` | Annual review, move-in/out walkthrough. |
| `WarrantyClaimed` | Warranty claim filed. |
| `Replaced` | New equipment record supersedes this one. |
| `Disposed` | Sold, scrapped, demolished — emitted automatically by `SoftDeleteAsync`. |
| `PhotoAdded` | A photo was attached. |
| `NotesUpdated` | Free-text notes were updated. |

## What's not in this slice (deferred to follow-up hand-offs)

- **Vehicle subtype + Trip events** — `EquipmentClass.Vehicle` is reserved; the full subtype (VIN, plate, base mileage at acquisition, business-use percentage, primary driver) plus `Trip` events for mileage logging ship in a follow-up Equipment hand-off.
- **`EquipmentConditionAssessment` integration** — gated on the Inspections module shipping (cluster intake #25, EXTEND disposition). First-slice has no condition field on `Equipment`; condition lives on `Inspection`.
- **OCR-ingested equipment capture from iOS** — gated on the iOS Field App intake (#23).
- **Tax-advisor depreciation projection** — gated on `blocks-tax-reporting` consumer + ADR 0051 acceptance. `ExpectedUsefulLifeYears` + `AcquisitionCost` are raw fields; the projection is downstream.
- **Schema-registry-backed `EquipmentClass`** — cluster intake OQ-A2; deferred to Phase 2.3+.
- **Equipment hierarchy (parent-child)** — cluster intake OQ-A1; flat for v1.
- **`IAuditTrail` emission per ADR 0049** — domain `EquipmentLifecycleEvent` is captured; full kernel-audit substrate emission (with `SignedOperation<AuditPayload>` envelope construction) is deferred. See PR #213 description for the OQ #2 flag.
- **`PropertyUnit` FK (`Equipment.Unit`)** — dropped from first-slice (hand-off OQ #1, option (b)); will be added when `PropertyUnit` ships in a follow-up Properties hand-off.

## Wiring

```csharp
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.DependencyInjection;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Foundation.Assets.Common;

services.AddInMemoryProperties();         // for the FK target
services.AddInMemoryPropertyEquipment();

var repo   = serviceProvider.GetRequiredService<IEquipmentRepository>();
var events = serviceProvider.GetRequiredService<IEquipmentLifecycleEventStore>();
var tenant = new TenantId("acme-llc");
var property = PropertyId.NewId();        // resolved from properties block

var equipmentId = EquipmentId.NewId();
await repo.UpsertAsync(new Equipment
{
    Id = equipmentId,
    TenantId = tenant,
    Property = property,
    Class = EquipmentClass.WaterHeater,
    DisplayName = "Master bath water heater",
    Make = "Rheem",
    Model = "XR50T06EC36U1",
    InstalledAt = DateTimeOffset.UtcNow,
    AcquisitionCost = 1_250m,
    ExpectedUsefulLifeYears = 12,
    CreatedAt = DateTimeOffset.UtcNow,
});

await events.AppendAsync(new EquipmentLifecycleEvent
{
    EventId = Guid.NewGuid(),
    Equipment = equipmentId,
    Property = property,
    TenantId = tenant,
    EventType = EquipmentLifecycleEventType.Installed,
    OccurredAt = DateTimeOffset.UtcNow,
    RecordedBy = "operator-1",
});

// Soft-delete also emits a Disposed lifecycle event automatically.
await repo.SoftDeleteAsync(tenant, equipmentId, "replaced with tankless", DateTimeOffset.UtcNow, "operator-1");
var history = await events.GetForEquipmentAsync(tenant, equipmentId);  // [Installed, Disposed]
```

## Related ADRs

- [ADR 0008 — Foundation MultiTenancy](../../../docs/adrs/0008-foundation-multitenancy.md): `Equipment` and `EquipmentLifecycleEvent` both implement `IMustHaveTenant`.
- [ADR 0015 — Module-Entity Registration](../../../docs/adrs/0015-module-entity-registration.md): `PropertyEquipmentEntityModule` follows the canonical pattern.
- [ADR 0049 — Audit-Trail Substrate](../../../docs/adrs/0049-audit-trail-substrate.md): the eventual emission target for lifecycle events; integration deferred.

## Related intake

- [Property-Assets domain intake](../../../icm/00_intake/output/property-assets-intake-2026-04-28.md) (intake retains historical name; entity renamed to `Equipment` per UPF Rule 4)
- [Property-operations cluster INDEX](../../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md)
- [UPF naming review](../../../icm/07_review/output/property-ops-cluster-naming-upf-review-2026-04-28.md) — Rule 4 (`Asset` → `Equipment` entity rename)
