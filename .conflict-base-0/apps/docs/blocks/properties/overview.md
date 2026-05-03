---
uid: block-properties-overview
title: Properties — Overview
description: Property root entity, in-memory repository, and ISunfishEntityModule contribution for the property-operations vertical.
keywords:
  - sunfish
  - blocks
  - properties
  - real-estate
  - property-management
  - multitenancy
---

# Properties — Overview

## What this block is

`Sunfish.Blocks.Properties` is the **root entity** of the property-operations vertical.
Every downstream domain in the cluster — Assets, Inspections, Leases, Work Orders,
Receipts, Public Listings, and the Owner Cockpit — FKs to a `Property`.

This is the first-slice scope. The block ships:

- `Property` — the root entity, `IMustHaveTenant`-scoped.
- `PostalAddress` — locale-tolerant address value object embedded in `Property`.
- `PropertyId` — opaque, JSON-round-trippable strong-typed identifier.
- `PropertyKind` — coarse classification (`SingleFamily | MultiUnit | Mixed | Land`).
- `IPropertyRepository` + `InMemoryPropertyRepository` — Get / List / Upsert / SoftDelete.
- `PropertiesEntityModule` — the `ISunfishEntityModule` contribution that registers
  EF Core entity configurations into Bridge's shared `DbContext` per ADR 0015.

## Package

- Package: `Sunfish.Blocks.Properties`
- Source: `packages/blocks-properties/`
- Namespace roots:
  - `Sunfish.Blocks.Properties.Models`
  - `Sunfish.Blocks.Properties.Services`
  - `Sunfish.Blocks.Properties.Data`
  - `Sunfish.Blocks.Properties.DependencyInjection`

## Field reference — `Property`

| Field | Type | Required | Notes |
|---|---|---|---|
| `Id` | `PropertyId` | yes | Opaque; `PropertyId.NewId()` for fresh records. |
| `TenantId` | `TenantId` | yes | Owning LLC; `IMustHaveTenant` enforced by persistence adapters. |
| `DisplayName` | `string` | yes | Human-friendly name (often the street address). |
| `Address` | `PostalAddress` | yes | Embedded value object (`OwnsOne` in EF). |
| `ParcelNumber` | `string?` | no | Assessor parcel number / APN. |
| `Kind` | `PropertyKind` | yes | `SingleFamily | MultiUnit | Mixed | Land`. |
| `AcquisitionCost` | `decimal?` | no | USD assumed; will migrate to typed `Money` after ADR 0051 acceptance. |
| `AcquiredAt` | `DateTimeOffset?` | no | Pairs with `AcquisitionCost`. |
| `YearBuilt` | `int?` | no | Null for land or unknown. |
| `TotalSquareFeet` | `decimal?` | no | Total interior sqft across units. |
| `TotalBedrooms` | `int?` | no | Sum across units for multi-unit. |
| `TotalBathrooms` | `decimal?` | no | Allows half-baths (e.g. `1.5`). |
| `Notes` | `string?` | no | Free-text operator notes. |
| `PrimaryPhotoBlobRef` | `string?` | no | Opaque blob FK; ingest pipeline gated on cluster OQ3. |
| `CreatedAt` | `DateTimeOffset` | yes | Immutable after first persist. |
| `DisposedAt` | `DateTimeOffset?` | no | Soft-delete marker (sale/transfer/demolition). |
| `DisposalReason` | `string?` | no | Free-text disposition reason. |

## What's not in this slice (deferred to follow-up hand-offs)

The cluster intake includes scope this block intentionally defers:

- **`PropertyUnit` child entity** — unit-level fields (number, sqft, beds/baths, current
  lease ref). Open question: separate entity vs flattened JSON column (intake OQ-P1).
  Ships in `property-properties-unit-entity-handoff.md`.
- **`PropertyOwnershipRecord` event log** — acquisition / disposition / refinance / transfer
  events; immutable; sourced for tax basis. Gated on `kernel-audit` Tier 2 retrofit.
  Ships in `property-properties-ownership-log-handoff.md`.
- **Cross-tenant ownership** — whether holding-co LLC tenant has cross-tenant read into
  child LLC tenants' properties (intake OQ-P3). Resolves alongside workstream #1
  multi-tenancy types.
- **GeoJSON polygon for parcel boundary** (intake OQ-P2).
- **Photo storage / blob handling** — `PrimaryPhotoBlobRef` is a string-shaped FK; the
  blob ingest pipeline is gated on Bridge's blob-ingest API (cluster cross-cutting OQ3).
- **Migration import tool** from BDFL's existing spreadsheet — Phase 2 onboarding work.

## Wiring

```csharp
using Sunfish.Blocks.Properties.DependencyInjection;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Foundation.Assets.Common;

services.AddInMemoryProperties();

var repo   = serviceProvider.GetRequiredService<IPropertyRepository>();
var tenant = new TenantId("acme-llc");

await repo.UpsertAsync(new Property
{
    Id = PropertyId.NewId(),
    TenantId = tenant,
    DisplayName = "123 Main St",
    Address = new PostalAddress
    {
        Line1 = "123 Main St",
        City = "Salt Lake City",
        Region = "UT",
        PostalCode = "84101",
        CountryCode = "US",
    },
    Kind = PropertyKind.SingleFamily,
    CreatedAt = DateTimeOffset.UtcNow,
});

var live = await repo.ListByTenantAsync(tenant);
```

## Related ADRs

- [ADR 0008 — Foundation MultiTenancy](../../../docs/adrs/0008-foundation-multitenancy.md):
  `Property` implements `IMustHaveTenant` so persistence adapters enforce tenant scoping.
- [ADR 0015 — Module-Entity Registration](../../../docs/adrs/0015-module-entity-registration.md):
  `PropertiesEntityModule` follows the canonical `ISunfishEntityModule` pattern.

## Related intake

- [Properties domain intake](../../../icm/00_intake/output/property-properties-intake-2026-04-28.md)
- [Property-operations cluster INDEX](../../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md)
