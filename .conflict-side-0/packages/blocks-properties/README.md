# Sunfish.Blocks.Properties

Block for the property-domain spine — the root `Property` entity that all property-operations cluster siblings reference.

Phase 1 first-slice per ADR 0057 / 0058 / 0059 / 0060 cluster reframing (PR #210, merged 2026-04-28).

## What this ships

### Models

- **`Property`** — root entity (`IMustHaveTenant`); has `PropertyId`, `Name`, `PropertyKind`, `Address` (`PostalAddress` value object).
- **`PropertyId`** — string-wrapper record struct.
- **`PropertyKind`** — enum (SingleFamily / Duplex / Triplex / Fourplex / Apartment / Townhouse / Condo / MobileHome / Commercial / MixedUse / Land / Other).
- **`PostalAddress`** — value object owned by `Property` (5 fields: Line1 / Line2 / City / StateOrProvince / PostalCode + ISO `Country` defaulting to `"US"`).

### Services

- **`IPropertyRepository`** + `InMemoryPropertyRepository` — CRUD + tenant enumeration + slug lookup.
- **`PropertiesEntityModule`** — `ISunfishEntityModule` contribution (ADR 0015) registering `Property` + `PostalAddress` for persistence.

## DI

```csharp
services.AddInMemoryProperties();
```

## Cluster role

`blocks-properties` is the **cluster spine** — all sibling property-operations blocks reference `PropertyId` as the FK target:

- `blocks-property-equipment.Equipment.Property` — equipment ↔ property
- `blocks-property-leasing-pipeline.Application.Listing` (transitively via `blocks-public-listings.PublicListing.Property`)
- `blocks-inspections.Inspection.Property`
- `blocks-leases.Lease.Property`

Per UPF Rule 4 (the `Equipment` rename precedent), cluster siblings use the `blocks-property-*` prefix; `blocks-properties` is the unprefixed cluster root.

## Deferred follow-ups

The first-slice scope deliberately omits:

- `PropertyUnit` (tracked separately for multi-unit properties)
- Ownership log + ownership-transfer events
- Money type (referenced by future financial fields; deferred per OQ #2)
- Kitchen-sink seed page (precedent: matches `blocks-property-equipment` first-slice)

## ADR map

- [ADR 0015](../../docs/adrs/0015-module-entity-registration.md) — module-entity registration pattern
- Property-operations cluster reconciliation: `icm/07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md`

## See also

- [apps/docs Overview](../../apps/docs/blocks/properties/overview.md)
