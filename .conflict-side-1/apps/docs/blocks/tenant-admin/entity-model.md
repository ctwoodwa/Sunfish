---
uid: block-tenant-admin-entity-model
title: Tenant Admin — Entity Model
description: EF Core entity configurations contributed by blocks-tenant-admin via ADR-0015 module registration.
keywords:
  - tenant-admin
  - entity-model
  - ef-core
  - soft-delete
  - adr-0015
  - multi-tenancy
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

## Composite-key note on `TenantProfile`

`TenantProfile` is keyed by `TenantId` alone — there is one profile per tenant. The `IEntityTypeConfiguration<TenantProfile>` sets `TenantId` as the primary key directly:

```csharp
builder.HasKey(p => p.TenantId);
builder.Property(p => p.DisplayName).IsRequired();
```

Unlike `TenantUser` and `BundleActivation` which have their own surrogate keys, `TenantProfile` uses the tenant id itself. This is intentional — the "one profile per tenant" cardinality is enforced at the schema level.

## Strong-typed ids and converters

`TenantUserId` and `BundleActivationId` are Sunfish strong-typed id structs. They round-trip through EF Core via converters registered in Foundation's entity-model extensions, so entity configurations can use them as key types without per-id boilerplate. The entity configuration files invoke:

```csharp
builder.HasKey(u => u.Id);
// No explicit HasConversion call — the global converter picks this up.
```

## Migrations plan

The block's entities merge into the shared Bridge `DbContext`. A single migration stream covers every block contributing to Bridge, which keeps version control simple at the cost of making it the host's job to re-scaffold after a block upgrade that changes entities.

Recommended workflow when bumping the block:

```bash
dotnet ef migrations add UpgradeTenantAdmin --context BridgeDbContext
dotnet ef database update --context BridgeDbContext
```

Follow the same naming convention used for other blocks (`UpgradeXxxBlock`).

## Test coverage

`tests/TenantAdminEntityModuleTests.cs` asserts:

- `ModuleKey` returns the canonical `sunfish.blocks.tenant-admin`.
- `Configure(ModelBuilder)` runs without throwing on a fresh model builder.
- Every expected entity type has a key configured after `Configure(...)` is called.
- The `TenantProfile` key configuration survives (since it is the unusual one).

These fixtures double as a template for downstream tests that extend the entity surface.

## Related

- [Overview](overview.md)
- [DI Wiring](di-wiring.md)
- ADR 0015 — `docs/adrs/0015-module-entity-registration.md`
- ADR 0008 — `docs/adrs/0008-foundation-multitenancy.md`
