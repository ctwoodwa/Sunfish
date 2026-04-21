---
uid: block-tenant-admin-di-wiring
title: Tenant Admin — DI Wiring
description: How to register blocks-tenant-admin services and its ADR-0015 entity module.
---

# Tenant Admin — DI Wiring

## Overview

`blocks-tenant-admin` is wired into a host application through a single extension method on `IServiceCollection`. The extension registers both the in-memory service (for demos and tests) and the ADR-0015 entity module (so the block's entities participate in the shared Bridge `DbContext`). Production hosts replace the `ITenantAdminService` binding while keeping the entity module.

## Extension method

```csharp
public static IServiceCollection AddInMemoryTenantAdmin(this IServiceCollection services)
{
    ArgumentNullException.ThrowIfNull(services);
    services.AddSingleton<ITenantAdminService, InMemoryTenantAdminService>();
    services.AddSingleton<ISunfishEntityModule, TenantAdminEntityModule>();
    return services;
}
```

Exposed on `Sunfish.Blocks.TenantAdmin.DependencyInjection.TenantAdminServiceCollectionExtensions`.

## What gets registered

| Registration | Lifetime | Purpose |
|---|---|---|
| `ITenantAdminService` → `InMemoryTenantAdminService` | Singleton | Core service. In-memory, thread-safe, non-persistent. |
| `ISunfishEntityModule` → `TenantAdminEntityModule` | Singleton | ADR-0015 entity module; contributes the block's EF Core configurations to the shared Bridge `DbContext`. |

## Typical host wiring

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddSunfishFoundation()
    .AddSunfishBridge()            // registers the shared DbContext + picks up ISunfishEntityModules
    .AddInMemoryTenantAdmin();     // this block
```

The Bridge registration consumes every registered `ISunfishEntityModule` when building the model. As long as both live in the container before the first request, the block's entity configurations are applied.

## Dependencies for the UI blocks

- `TenantProfileBlock` injects `ITenantAdminService`.
- `TenantUsersListBlock` injects `ITenantAdminService`.
- `BundleActivationPanel` injects both `IBundleCatalog` (from `blocks-businesscases`) and `ITenantAdminService`.

Ensure `blocks-businesscases` is also wired before using `BundleActivationPanel`, or `IBundleCatalog` will fail to resolve.

## Swapping to a persistence-backed service

```csharp
builder.Services.AddInMemoryTenantAdmin();
builder.Services.AddSingleton<ITenantAdminService, MyEfTenantAdminService>();
```

The second registration overrides the first. The `ISunfishEntityModule` registration is additive (one per block), so Bridge continues to see it and apply the entity configurations.

## Multi-tenancy

`TenantProfile`, `TenantUser`, and `BundleActivation` all implement `IMustHaveTenant`. The ambient `TenantId` is resolved from Foundation's tenancy primitives (typically `ITenantContext`). Calls that take a `TenantId` explicitly (e.g. `GetTenantProfileAsync(tenantId)`) should agree with the ambient tenant — the in-memory service does not currently cross-check this, but persistence-backed implementations will via Bridge's global query filter.

## Related

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- ADR 0015 — `docs/adrs/0015-module-entity-registration.md`
