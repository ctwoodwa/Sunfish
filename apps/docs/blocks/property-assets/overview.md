---
uid: block-property-assets-overview
title: Property-Assets — Overview
description: Asset entity (FK to Property), AssetClass discriminator, AssetLifecycleEvent append-only log, in-memory repository, and ISunfishEntityModule contribution for the property-operations vertical.
keywords:
  - sunfish
  - blocks
  - property-assets
  - assets
  - real-estate
  - property-management
  - lifecycle
  - depreciation
  - multitenancy
---

# Property-Assets — Overview

## What this block is

`Sunfish.Blocks.PropertyAssets` is the **inventory backbone** of the property-operations vertical.
Each `Asset` (water heater, HVAC, appliance, roof, vehicle, etc.) FKs to a
[`Property`](../properties/overview.md) and provides the substrate for inspections, work orders,
receipts (acquisition cost basis), and depreciation reporting.

This is the first-slice scope. The block ships:

- `Asset` — the inventory entity, `IMustHaveTenant`-scoped, FK to `Property`.
- `AssetClass` — coarse classification (`WaterHeater | HVAC | Appliance | Roof | Vehicle | Plumbing | Electrical | Other`).
- `AssetId` — opaque, JSON-round-trippable strong-typed identifier.
- `WarrantyMetadata` — embedded value object on `Asset.Warranty`.
- `AssetLifecycleEvent` + `AssetLifecycleEventType` — append-only event log (Installed / Serviced / Inspected / WarrantyClaimed / Replaced / Disposed / PhotoAdded / NotesUpdated).
- `IAssetRepository` + `InMemoryAssetRepository` — Get / List (by tenant, property, class) / Upsert / SoftDelete (which also emits a `Disposed` lifecycle event).
- `IAssetLifecycleEventStore` + `InMemoryAssetLifecycleEventStore` — append + query by asset + by property.
- `PropertyAssetsEntityModule` — the `ISunfishEntityModule` contribution per ADR 0015.

## Package

- Package: `Sunfish.Blocks.PropertyAssets`
- Source: `packages/blocks-property-assets/`
- Naming convention: cluster siblings under the property-operations vertical use the `blocks-property-*` prefix (root entity stays as `blocks-properties`).
- Namespace roots:
  - `Sunfish.Blocks.PropertyAssets.Models`
  - `Sunfish.Blocks.PropertyAssets.Services`
  - `Sunfish.Blocks.PropertyAssets.Data`
  - `Sunfish.Blocks.PropertyAssets.DependencyInjection`

## Field reference — `Asset`

| Field | Type | Required | Notes |
|---|---|---|---|
| `Id` | `AssetId` | yes | Opaque; `AssetId.NewId()` for fresh records. |
| `TenantId` | `TenantId` | yes | Owning LLC; `IMustHaveTenant`. |
| `Property` | `PropertyId` | yes | FK to parent property. No orphan assets. |
| `Class` | `AssetClass` | yes | Coarse classification. |
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
| `Replaced` | New asset record supersedes this one. |
| `Disposed` | Sold, scrapped, demolished — emitted automatically by `SoftDeleteAsync`. |
| `PhotoAdded` | A photo was attached. |
| `NotesUpdated` | Free-text notes were updated. |

## What's not in this slice (deferred to follow-up hand-offs)

- **Vehicle subtype + Trip events** — `AssetClass.Vehicle` is reserved; the full subtype (VIN, plate, base mileage at acquisition, business-use percentage, primary driver) plus `Trip` events for mileage logging ship in `property-assets-vehicle-trip-events-handoff.md`.
- **`AssetConditionAssessment` integration** — gated on the Inspections module shipping (cluster intake #25). First-slice has no condition field on `Asset`; condition lives on `Inspection`.
- **OCR-ingested asset capture from iOS** — gated on the iOS Field App intake (#23).
- **Tax-advisor depreciation projection** — gated on `blocks-tax-reporting` consumer + ADR 0051 acceptance. `ExpectedUsefulLifeYears` + `AcquisitionCost` are raw fields; the projection is downstream.
- **Schema-registry-backed `AssetClass`** — cluster intake OQ-A2; deferred to Phase 2.3+.
- **Asset hierarchy (parent-child)** — cluster intake OQ-A1; flat for v1.
- **`IAuditTrail` emission per ADR 0049** — domain `AssetLifecycleEvent` is captured; full kernel-audit substrate emission (with `SignedOperation<AuditPayload>` envelope construction) is deferred. See PR description for the OQ #2 flag.
- **`PropertyUnit` FK (`Asset.Unit`)** — dropped from first-slice (hand-off OQ #1, option (b)); will be added when `PropertyUnit` ships in a follow-up Properties hand-off.

## Wiring

```csharp
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyAssets.DependencyInjection;
using Sunfish.Blocks.PropertyAssets.Models;
using Sunfish.Blocks.PropertyAssets.Services;
using Sunfish.Foundation.Assets.Common;

services.AddInMemoryProperties();         // for the FK target
services.AddInMemoryPropertyAssets();

var repo   = serviceProvider.GetRequiredService<IAssetRepository>();
var events = serviceProvider.GetRequiredService<IAssetLifecycleEventStore>();
var tenant = new TenantId("acme-llc");
var property = PropertyId.NewId();        // resolved from properties block

var assetId = AssetId.NewId();
await repo.UpsertAsync(new Asset
{
    Id = assetId,
    TenantId = tenant,
    Property = property,
    Class = AssetClass.WaterHeater,
    DisplayName = "Master bath water heater",
    Make = "Rheem",
    Model = "XR50T06EC36U1",
    InstalledAt = DateTimeOffset.UtcNow,
    AcquisitionCost = 1_250m,
    ExpectedUsefulLifeYears = 12,
    CreatedAt = DateTimeOffset.UtcNow,
});

await events.AppendAsync(new AssetLifecycleEvent
{
    EventId = Guid.NewGuid(),
    Asset = assetId,
    Property = property,
    TenantId = tenant,
    EventType = AssetLifecycleEventType.Installed,
    OccurredAt = DateTimeOffset.UtcNow,
    RecordedBy = "operator-1",
});

// Soft-delete also emits a Disposed lifecycle event automatically.
await repo.SoftDeleteAsync(tenant, assetId, "replaced with tankless", DateTimeOffset.UtcNow, "operator-1");
var history = await events.GetForAssetAsync(tenant, assetId);  // [Installed, Disposed]
```

## Related ADRs

- [ADR 0008 — Foundation MultiTenancy](../../../docs/adrs/0008-foundation-multitenancy.md): `Asset` and `AssetLifecycleEvent` both implement `IMustHaveTenant`.
- [ADR 0015 — Module-Entity Registration](../../../docs/adrs/0015-module-entity-registration.md): `PropertyAssetsEntityModule` follows the canonical pattern.
- [ADR 0049 — Audit-Trail Substrate](../../../docs/adrs/0049-audit-trail-substrate.md): the eventual emission target for lifecycle events; integration deferred.

## Related intake

- [Property-Assets domain intake](../../../icm/00_intake/output/property-assets-intake-2026-04-28.md)
- [Property-operations cluster INDEX](../../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md)
