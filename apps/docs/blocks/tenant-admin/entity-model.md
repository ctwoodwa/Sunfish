---
uid: block-tenant-admin-entity-model
title: Tenant Admin — Entity Model
description: EF Core entity configurations contributed by blocks-tenant-admin via ADR-0015 module registration.
---

# Tenant Admin — Entity Model

## Overview

`blocks-tenant-admin` contributes its EF Core entity configurations to the shared Bridge `DbContext` through the ADR-0015 module-entity registration pattern. It declares no `DbContext` of its own; instead, `TenantAdminEntityModule` implements `ISunfishEntityModule` and Bridge picks up the block's configurations alongside every other module.

## Entity module

```csharp
public sealed class TenantAdminEntityModule : ISunfishEntityModule
{
    public string ModuleKey => "sunfish.blocks.tenant-admin";

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantAdminEntityModule).Assembly);
    }
}
```

The `ApplyConfigurationsFromAssembly` call picks up every `IEntityTypeConfiguration<T>` in the block's assembly.

## Configured entities

The block ships three `IEntityTypeConfiguration<T>` implementations in the `Data/` folder:

| File | Entity |
|---|---|
| `TenantProfileEntityConfiguration.cs` | `TenantProfile` |
| `TenantUserEntityConfiguration.cs` | `TenantUser` |
| `BundleActivationEntityConfiguration.cs` | `BundleActivation` |

## Entities and fields

### TenantProfile (tenant-scoped)

| Field | Type | Notes |
|---|---|---|
| `TenantId` | `TenantId` | Primary key — one profile per tenant. `IMustHaveTenant`. |
| `DisplayName` | `string` | Required. |
| `ContactEmail` | `string?` | Optional. |
| `ContactPhone` | `string?` | Optional, free-form. |
| `CreatedAt` | `DateTime` | UTC. |
| `BundleKey` | `string?` | Optional primary bundle pointer. |

### TenantUser (tenant-scoped)

| Field | Type | Notes |
|---|---|---|
| `Id` | `TenantUserId` | Primary key. |
| `TenantId` | `TenantId` | `IMustHaveTenant`. |
| `Email` | `string` | Primary invitation address and display identity. |
| `DisplayName` | `string?` | Optional. |
| `Role` | `TenantRole` | Coarse role enum. |
| `InvitedAt` | `DateTime` | UTC. |
| `AcceptedAt` | `DateTime?` | `null` while pending. |

### BundleActivation (tenant-scoped)

| Field | Type | Notes |
|---|---|---|
| `Id` | `BundleActivationId` | Primary key. |
| `TenantId` | `TenantId` | `IMustHaveTenant`. |
| `BundleKey` | `string` | Matches `BusinessCaseBundleManifest.Key`. |
| `Edition` | `string` | Matches a key from the bundle's `EditionMappings`. |
| `ActivatedAt` | `DateTime` | UTC. |
| `DeactivatedAt` | `DateTime?` | Soft-delete marker. `null` = active. |

## Relationships

- `TenantUser` / `TenantProfile` / `BundleActivation` are all keyed by (or contain) `TenantId` — the Bridge global query filter restricts reads to the ambient tenant.
- `BundleActivation.BundleKey` is a loose string reference to `blocks-businesscases` bundle manifests; there is no database-level FK between the two blocks (they live in different modules and are independently registered).
- `TenantUser` does not reference an external identity provider directly. The authoritative identity record lives outside this block.

## Multi-tenancy filter

All three entities implement `IMustHaveTenant`, so the Bridge `DbContext` global query filter applies automatically. Ordinary LINQ queries on `TenantProfile`, `TenantUser`, and `BundleActivation` are tenant-safe without extra filtering.

## Soft delete convention

`BundleActivation` uses soft-delete: `DeactivatedAt` is set instead of the row being removed. This is an explicit choice for audit-history retention. The block's query methods (`ListActiveBundlesAsync`) filter to `DeactivatedAt == null` at the service layer rather than at the EF Core level, so custom queries have visibility into the history if they want it.

## Related

- [Overview](overview.md)
- [DI Wiring](di-wiring.md)
- ADR 0015 — `docs/adrs/0015-module-entity-registration.md`
