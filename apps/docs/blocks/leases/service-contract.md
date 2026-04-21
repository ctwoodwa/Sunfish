---
uid: block-leases-service-contract
title: Leases — Service Contract
description: The ILeaseService public surface — create, get, and list leases with optional phase and tenant filters.
---

# Leases — Service Contract

## Overview

`ILeaseService` is a first-pass contract. It covers three operations: create, get by id,
and list with optional filters. Phase transitions, DocuSign envelope dispatch, signature
state management, and the rest of the §6.1 workflow surface are deferred to follow-up
passes.

Source: `packages/blocks-leases/Services/ILeaseService.cs`

## Methods

```csharp
public interface ILeaseService
{
    ValueTask<Lease> CreateAsync(
        CreateLeaseRequest request, CancellationToken ct = default);

    ValueTask<Lease?> GetAsync(
        LeaseId id, CancellationToken ct = default);

    IAsyncEnumerable<Lease> ListAsync(
        ListLeasesQuery query, CancellationToken ct = default);
}
```

### `CreateAsync`

Creates a new lease and returns the persisted record. The new lease is always in
`LeasePhase.Draft` — there is no way to create a lease directly in `Executed` or `Active`
in this pass.

#### `CreateLeaseRequest`

| Field        | Type                     | Notes |
|--------------|--------------------------|-------|
| `UnitId`     | `EntityId`               | The unit to be covered. |
| `Tenants`    | `IReadOnlyList<PartyId>` | At least one required. |
| `Landlord`   | `PartyId`                | Landlord party. |
| `StartDate`  | `DateOnly`               | Lease term start. |
| `EndDate`    | `DateOnly`               | Lease term end. |
| `MonthlyRent`| `decimal`                | Monthly rent. |

### `GetAsync`

Returns the lease with the given `LeaseId`, or `null` when no such lease exists.

### `ListAsync`

Streams leases matching the query. Pass `ListLeasesQuery.Empty` to return every lease.

#### `ListLeasesQuery`

Filters are AND-combined. A `null` filter means "no filter on that field".

| Field     | Type           | Notes |
|-----------|----------------|-------|
| `Phase`   | `LeasePhase?`  | Restrict to a specific phase. |
| `TenantId`| `PartyId?`     | Restrict to leases that include this tenant party. |

`ListLeasesQuery.Empty` is a shared singleton that sets no filters.

## Typical workflow

1. **Create the parties** out of band (party creation is not yet part of this block's
   surface — consumers hold their own `Party` records).
2. **Create the lease**:
   ```csharp
   var lease = await leaseService.CreateAsync(new CreateLeaseRequest
   {
       UnitId      = new EntityId("unit:acme/3B"),
       Tenants     = new[] { tenant1Id, tenant2Id },
       Landlord    = landlordId,
       StartDate   = new DateOnly(2026, 5, 1),
       EndDate     = new DateOnly(2027, 4, 30),
       MonthlyRent = 1800m,
   });
   ```
3. **Retrieve or list** as needed — for example, to populate the admin view:
   ```csharp
   await foreach (var l in leaseService.ListAsync(new ListLeasesQuery { Phase = LeasePhase.Draft }))
       ...;
   ```

## Default implementation

`InMemoryLeaseService` is registered by `AddInMemoryLeases`. It is suitable for
development, tests, and kitchen-sink demos. Replace with a persistence-backed
implementation for production workloads.

## Related pages

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- [Demo — Lease List](demo-lease-list.md)
