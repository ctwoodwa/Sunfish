---
uid: block-leases-entity-model
title: Leases — Entity Model
description: Lease, Unit, Party, Document, and the LeasePhase lifecycle exposed by Sunfish.Blocks.Leases.
keywords:
  - sunfish
  - leases
  - entity-model
  - lease-phase
  - party
  - lease-state
---

# Leases — Entity Model

## Overview

The leases block exposes four records — `Lease`, `Unit`, `Party`, `Document` — plus a
`LeasePhase` enum. All records use `required` / `init;` properties and are immutable once
constructed.

## Lease

The canonical lease record.

| Field          | Type                      | Notes |
|----------------|---------------------------|-------|
| `Id`           | `LeaseId`                 | Unique identifier. |
| `UnitId`       | `EntityId`                | The unit covered by the lease. |
| `Tenants`      | `IReadOnlyList<PartyId>`  | All tenant parties. |
| `Landlord`     | `PartyId`                 | The landlord party. |
| `StartDate`    | `DateOnly`                | Lease term start (inclusive). |
| `EndDate`      | `DateOnly`                | Lease term end (inclusive). |
| `MonthlyRent`  | `decimal`                 | Monthly rent in the base currency. |
| `Phase`        | `LeasePhase`              | Current lifecycle phase. |

The shape is intentionally thin. Full workflow surface (signature state, execution
timestamp, renewal references, termination reason) is deferred.

### LeasePhase

| Value              | Meaning |
|--------------------|---------|
| `Draft`            | Being authored; no signatures requested yet. |
| `AwaitingSignature`| Envelope sent; waiting for all party signatures. |
| `Executed`         | All parties have signed; awaiting commencement. |
| `Active`           | Term is currently running. |
| `Renewed`          | Renewed; the renewed term is running. |
| `Terminated`       | Terminated before or at expiry. |

Transitions are **not** implemented in this pass — `ILeaseService.CreateAsync` always
produces a `Draft` lease. The enum enumerates the eventual state surface for forward
compatibility.

## Unit

A rentable unit (apartment, house, commercial space, etc.).

| Field          | Type       | Notes |
|----------------|------------|-------|
| `Id`           | `EntityId` | Canonical entity identifier (e.g. `unit:acme/3B`). |
| `Address`      | `string`   | Human-readable address or description. |
| `BedroomCount` | `int?`     | Optional. |
| `BaseRent`     | `decimal?` | Optional base asking rent. |

Property hierarchy and amenities list are deferred.

## Party

A person or entity that is a party to a lease.

| Field         | Type        | Notes |
|---------------|-------------|-------|
| `Id`          | `PartyId`   | Unique identifier. |
| `DisplayName` | `string`    | Full name or company name. |
| `Kind`        | `PartyKind` | `Tenant`, `Landlord`, `Manager`, or `Guarantor`. |

## Document

A document associated with a lease (e.g. signed PDF, disclosure form).

| Field         | Type          | Notes |
|---------------|---------------|-------|
| `Id`          | `DocumentId`  | Unique identifier. |
| `Title`       | `string`      | Human-readable title. |
| `ContentType` | `string`      | MIME type (e.g. `application/pdf`). |
| `BlobUri`     | `Uri?`        | Blob-store reference, or `null` before upload. |

`IBlobStore` integration is deferred — the record carries the URI but the block does not
write or read blobs directly.

## LeaseState

A lightweight placeholder record used by UI code to track the last phase transition time.

| Field           | Type         | Notes |
|-----------------|--------------|-------|
| `Phase`         | `LeasePhase` | Current phase. |
| `EnteredAtUtc`  | `DateTime`   | When this phase was entered (UTC). |

`LeaseState.Initial()` returns `(Draft, UtcNow)`. The real transition engine (DocuSign
envelope dispatch, commencement-date rules) is deferred.

## Relationships

```
Unit            1 ─── N  Lease           (by UnitId)
Party           N ─── N  Lease           (via Tenants + Landlord)
Lease           1 ─── N  Document        (follow-up: today Document has no LeaseId ref)
```

Today `Document` does not carry a `LeaseId` reference — document association will follow
in the workflow pass.

## Strong-typed identifiers

Each record uses a dedicated identifier struct. Identifiers are opaque strings; the
in-memory service uses GUIDs, but a persistence-backed implementation is free to use any
collision-resistant scheme.

- `LeaseId` — unique identifier for a `Lease`.
- `PartyId` — unique identifier for a `Party`.
- `DocumentId` — unique identifier for a `Document`.

`Unit` uses the canonical `EntityId` shape from `Sunfish.Foundation.Assets.Common`
(`kind:tenant/local`), not a dedicated `UnitId` — units are cross-block assets, not a
leases-owned concept.

## Usage example (drawn from tests)

The tests pin the minimum shape of a `CreateLeaseRequest`:

```csharp
var request = new CreateLeaseRequest
{
    UnitId      = new EntityId("unit", "test", "unit-1"),
    Tenants     = [new PartyId("tenant-a")],
    Landlord    = new PartyId("landlord-x"),
    StartDate   = new DateOnly(2025, 1, 1),
    EndDate     = new DateOnly(2025, 12, 31),
    MonthlyRent = 1500m,
};

var svc   = new InMemoryLeaseService();
var lease = await svc.CreateAsync(request);

// lease.Phase is always Draft on the first pass
```

## Immutability

All records use `required` / `init;` properties. Once constructed they cannot be mutated —
the future workflow pass will produce new records for phase transitions rather than
mutating existing ones. This matches the pattern used by `blocks-inspections` and
`blocks-maintenance`.

## Related pages

- [Overview](overview.md)
- [Service Contract](service-contract.md)
- [Demo — Lease List](demo-lease-list.md)
