---
uid: block-leases-service-contract
title: Leases — Service Contract
description: The ILeaseService public surface — create, get, and list leases with optional phase and tenant filters.
keywords:
  - sunfish
  - leases
  - service-contract
  - ilease-service
  - list-leases-query
  - in-memory
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

## Argument validation

The default implementation validates arguments eagerly:

- `CreateAsync(null)` throws `ArgumentNullException` — pinned by
  `CreateAsync_ThrowsOnNull_Request`.
- `ListAsync(null)` throws `ArgumentNullException` on the first enumeration step — pinned
  by `ListAsync_ThrowsOnNull_Query`.

`GetAsync` returns `null` for unknown ids rather than throwing — callers drive control flow
off the nullability rather than catching a `KeyNotFoundException`. This matches the
pattern used by `IInspectionsService.GetInspectionAsync`.

## Concurrency

`InMemoryLeaseService` stores leases in a thread-safe dictionary. Concurrent `CreateAsync`
calls never lose a record — pinned by `ConcurrentCreates_AreAllPersisted`, which fires 20
parallel creates and asserts all 20 are retrievable via `ListAsync`. There is no
per-lease lock because the first-pass surface has no mutating operations after create.

```csharp
var tasks = Enumerable.Range(0, 20)
    .Select(i => svc.CreateAsync(MakeRequest($"unit-{i}")).AsTask())
    .ToArray();

await Task.WhenAll(tasks);

// ListAsync returns all 20, in unspecified order.
```

## Cancellation

All methods accept a `CancellationToken`. The in-memory implementation checks the token at
method entry — a cancelled token causes the operation to throw
`OperationCanceledException` before any mutation. Persistence-backed implementations
should honour the same contract and forward the token to their I/O.

## Registering a replacement

To swap in a persistence-backed implementation, register it after `AddInMemoryLeases` (or
instead of it):

```csharp
services.AddInMemoryLeases();                          // optional baseline
services.AddSingleton<ILeaseService, MyEfLeaseService>(); // wins by last-registered rule
```

Or register only the replacement and skip `AddInMemoryLeases` entirely. The interface is
the only contract consumers (including `LeaseListBlock`) depend on.

## Related pages

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- [Demo — Lease List](demo-lease-list.md)
