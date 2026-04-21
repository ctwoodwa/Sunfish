---
uid: block-businesscases-entity-model
title: Business Cases — Entity Model
description: BundleActivationRecord and TenantEntitlementSnapshot — the persistence and read-model shapes of the business-cases block.
keywords:
  - sunfish
  - businesscases
  - bundle-activation
  - entitlement-snapshot
  - entity-model
  - multitenancy
  - ef-core
---

# Business Cases — Entity Model

## Overview

The business-cases block exposes two records:

- `BundleActivationRecord` — the **persisted** shape. One per `(tenant, bundle)` pair.
- `TenantEntitlementSnapshot` — the **read-model** shape. A computed, point-in-time view
  derived from the active bundle manifest.

## BundleActivationRecord

A tenant's current activation of a specific bundle. Implements `IMustHaveTenant`
([ADR 0008](../../docs/adrs/0008-foundation-multitenancy.md)), so Bridge's central tenant
query-filter scopes every read and write to the current tenant.

Source: `packages/blocks-businesscases/Models/BundleActivationRecord.cs`

| Field          | Type                         | Notes |
|----------------|------------------------------|-------|
| `Id`           | `BundleActivationRecordId`   | Unique identifier for the activation. |
| `TenantId`     | `TenantId`                   | Owning tenant (required; enforces `IMustHaveTenant`). |
| `BundleKey`    | `string`                     | Bundle key from `IBundleCatalog` (reverse-DNS style). |
| `Edition`      | `string`                     | Edition selected from the bundle's `EditionMappings`. |
| `ActivatedAt`  | `DateTimeOffset`             | UTC timestamp when the bundle was activated. |
| `DeactivatedAt`| `DateTimeOffset?`            | UTC deactivation timestamp, or `null` while active. |

A tenant may hold multiple active bundles simultaneously — there is one record per
`(tenant, bundle)` pair rather than one per tenant.

## TenantEntitlementSnapshot

A computed, read-only view of a tenant's resolved entitlements. The snapshot is produced
by `IBusinessCaseService.GetSnapshotAsync` — it is not persisted directly. It is the
primary input for the `EntitlementSnapshotBlock` diagnostic component.

Source: `packages/blocks-businesscases/Models/TenantEntitlementSnapshot.cs`

| Field                    | Type                              | Notes |
|--------------------------|-----------------------------------|-------|
| `TenantId`               | `TenantId`                        | The tenant this snapshot describes. |
| `ActiveBundleKey`        | `string?`                         | Active bundle, or `null` when no bundle is active. |
| `ActiveEdition`          | `string?`                         | Edition of the active bundle, or `null`. |
| `ActiveModules`          | `IReadOnlyList<string>`           | Union of `RequiredModules` and edition-mapped modules. |
| `ResolvedFeatureValues`  | `IReadOnlyDictionary<string,string>` | Feature key → value from the bundle manifest's `FeatureDefaults`. |
| `ResolvedAt`             | `DateTimeOffset`                  | UTC timestamp when the snapshot was produced. |

The snapshot is a value — each call to `GetSnapshotAsync` may return a new instance
reflecting the manifest state at that moment. It is not a live observable view.

## EF Core configuration module

`BusinessCasesEntityModule` is the block's `ISunfishEntityModule` contribution. It
registers every `IEntityTypeConfiguration<T>` declared in the assembly (today this is
`BundleActivationRecordEntityConfiguration`) into Bridge's shared `DbContext` per
[ADR 0015](../../docs/adrs/0015-module-entity-registration.md).

The module exposes a stable key: `"sunfish.blocks.businesscases"`.

```csharp
public sealed class BusinessCasesEntityModule : ISunfishEntityModule
{
    public const string Key = "sunfish.blocks.businesscases";
    public string ModuleKey => Key;

    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BusinessCasesEntityModule).Assembly);
    }
}
```

## Relationships

```
TenantId  <──  BundleActivationRecord  ── references ──>  BundleKey (in IBundleCatalog)
                                        ── references ──>  Edition  (in manifest.EditionMappings)

TenantEntitlementSnapshot  derived from  BundleActivationRecord + BundleManifest
```

## EF Core mapping

`BundleActivationRecordEntityConfiguration` maps the record to a stable table named
`businesscases_bundle_activation_records`. Key mappings:

| Column / index | Notes |
|---|---|
| `Id` | `BundleActivationRecordId` → `string` via value converter, max length 64. |
| `TenantId` | `TenantId` → `string` via value converter, max length 64, required. |
| `BundleKey` | `string`, max length 256, required. |
| `Edition` | `string`, max length 64, required. |
| `ActivatedAt` | required `DateTimeOffset`. |
| `DeactivatedAt` | nullable `DateTimeOffset`. |
| Unique index `ix_businesscases_activation_tenant_bundle` | `(TenantId, BundleKey)` — enforces "one record per (tenant, bundle)" at the database level. |

Because the entity module applies configurations from the containing assembly, any new
EF-mapped record declared inside `Sunfish.Blocks.BusinessCases.Data` is picked up
automatically without further wiring.

## Snapshot semantics

`TenantEntitlementSnapshot` is a _read-through_ computation, not a cached projection. Each
call to `IBusinessCaseService.GetSnapshotAsync`:

1. Fetches the active `BundleActivationRecord` for the tenant.
2. Looks up the bundle manifest from `IBundleCatalog`.
3. Unions `RequiredModules` with `EditionMappings[edition]` for `ActiveModules`.
4. Copies `FeatureDefaults` verbatim into `ResolvedFeatureValues`.
5. Stamps `ResolvedAt` with the current UTC instant.

The snapshot is cheap to compute and does not mutate any state. Treat it as a
point-in-time value; pin it in a variable if you need to render it twice.

## Related pages

- [Overview](overview.md)
- [Entitlement Resolver](entitlement-resolver.md)
- [Bundle Provisioning Service](bundle-provisioning-service.md)
- [DI Wiring](di-wiring.md)
