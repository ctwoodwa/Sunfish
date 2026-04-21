---
uid: block-businesscases-bundle-provisioning-service
title: Business Cases — Bundle Provisioning Service
description: The write-side contract for activating and deactivating bundles for a Sunfish tenant.
---

# Business Cases — Bundle Provisioning Service

## Overview

`IBundleProvisioningService` is the **write** side of the business-cases block. It is the
only supported entry point for changing a tenant's bundle activation state. Reads go
through `IBusinessCaseService`.

Separating reads and writes keeps the API surface small, makes implementations easy to
test in isolation, and lets permissioned admin flows target just the provisioning contract.

Source: `packages/blocks-businesscases/Services/IBundleProvisioningService.cs`

## Contract

```csharp
public interface IBundleProvisioningService
{
    ValueTask<BundleActivationRecord> ProvisionAsync(
        TenantId tenantId,
        string bundleKey,
        string edition,
        CancellationToken cancellationToken = default);

    ValueTask DeprovisionAsync(
        TenantId tenantId,
        string bundleKey,
        CancellationToken cancellationToken = default);
}
```

### `ProvisionAsync`

Activates `bundleKey` at `edition` for `tenantId`. The default implementation validates:

- The bundle exists in `IBundleCatalog`.
- The supplied `edition` appears in the bundle's `EditionMappings`.
- No currently-active record exists for the same `(tenantId, bundleKey)` pair.

On success, a new `BundleActivationRecord` is persisted with `ActivatedAt = UtcNow` and
`DeactivatedAt = null`, and the record is returned.

`InvalidOperationException` is thrown when any of the above checks fail.

### `DeprovisionAsync`

Deactivates `bundleKey` for `tenantId`. No-op when no active record exists for the pair.
Implementations should either set `DeactivatedAt` on the existing record or remove it — the
contract leaves this to the implementation.

## Deployment-mode considerations

The implementation is expected to consult deployment mode where relevant — for example, a
shared-tenant "Cloud" deployment might require admin role in addition to the contract call,
while a lite-mode / single-tenant deployment can provision freely. The contract itself does
not encode authorization; that sits above in the host's authorization pipeline.

## Feature-default seeding

Implementations are free to trigger feature-default seeding on successful provisioning —
for example, by publishing an event or by eagerly populating a cache of resolved feature
values for the tenant. The contract does not require it; the bundle-aware
`BundleEntitlementResolver` already resolves feature defaults lazily from the manifest, so
seeding is an optimisation rather than a correctness concern.

## Typical workflow

1. Administrator selects a bundle + edition for the tenant in the admin UI.
2. Admin UI calls `ProvisionAsync(tenantId, "sunfish.propertymgmt", "Pro")`.
3. The service validates against the catalog, creates a `BundleActivationRecord`, and
   returns it.
4. On the next feature evaluation for that tenant, `BundleEntitlementResolver` picks up
   the new record and the feature-management pipeline reports the new entitlement values.
5. To deactivate: call `DeprovisionAsync(tenantId, "sunfish.propertymgmt")`.

## Default implementation

`InMemoryBundleProvisioningService` is registered by `AddInMemoryBusinessCases`. It is
backed by a shared `InMemoryBundleActivationStore` so that `IBundleProvisioningService` and
`IBusinessCaseService` see consistent state.

## Related pages

- [Overview](overview.md)
- [Entitlement Resolver](entitlement-resolver.md)
- [Entity Model](entity-model.md)
- [DI Wiring](di-wiring.md)
