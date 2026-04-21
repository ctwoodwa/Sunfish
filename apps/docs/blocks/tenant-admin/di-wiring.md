---
uid: block-tenant-admin-di-wiring
title: Tenant Admin — DI Wiring
description: How to register blocks-tenant-admin services and its ADR-0015 entity module.
keywords:
  - tenant-admin
  - di
  - registration
  - entity-module
  - ef-core
  - adr-0015
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

## Verifying the registration

```csharp
var provider = services.BuildServiceProvider();

// Service is available
var svc = provider.GetRequiredService<ITenantAdminService>();
Assert.IsType<InMemoryTenantAdminService>(svc);

// Entity module is registered
var modules = provider.GetServices<ISunfishEntityModule>();
Assert.Contains(modules, m => m is TenantAdminEntityModule);
```

The `tests/TenantAdminEntityModuleTests.cs` fixture asserts the module key and that the configurations apply to a test `DbContext` without throwing. Copy the pattern into your host's pre-deploy smoke tests if you want a guardrail.

## Combined host pattern

A shell-style host typically wires `blocks-tenant-admin` alongside `blocks-businesscases` so `BundleActivationPanel` can render the catalog. A representative stack:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddSunfishFoundation()
    .AddSunfishBridge(options => options.UseNpgsql(cnxn))
    .AddInMemoryBusinessCases()         // IBundleCatalog
    .AddInMemoryTenantAdmin()           // this block
    .AddSunfishBlazorUi();              // for the block's UI components

// Swap to persisted implementations if you have them
builder.Services.Replace(ServiceDescriptor.Singleton<ITenantAdminService, EfTenantAdminService>());

var app = builder.Build();
app.Run();
```

`AddSunfishBlazorUi()` is the UI adapter registration that brings in `SunfishScheduler`, theme provider, and friends — `TenantProfileBlock`, `TenantUsersListBlock`, and `BundleActivationPanel` render cleanly under its theme.

## Replacing InMemoryBusinessCases

If your host has a custom bundle catalog (e.g. pulled from a CMS), replace `IBundleCatalog` rather than re-implementing `AddInMemoryBusinessCases`:

```csharp
builder.Services.Replace(ServiceDescriptor.Singleton<IBundleCatalog, MyCmsBundleCatalog>());
```

The tenant-admin block does not care about the implementation — it consumes the interface only through `BundleActivationPanel`.

## Troubleshooting

- **"Cannot resolve IBundleCatalog"** — `AddInMemoryBusinessCases()` (or equivalent) was not called. Only the UI panel needs it; the `ITenantAdminService` itself does not.
- **"Unable to resolve ITenantAdminService"** — the `AddInMemoryTenantAdmin()` call was omitted or overridden after the `Replace` above.
- **Entity configurations not applied** — `AddSunfishBridge()` was not called. The module registers fine but never sees a `ModelBuilder` without Bridge.
- **Cross-tenant data leaks** — check that `ITenantContext` is correctly scoped per request. The in-memory service does not enforce cross-checks between the ambient tenant and the `TenantId` in a request parameter; the persistence-backed path relies on Bridge's global query filter.

## Related

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- ADR 0015 — `docs/adrs/0015-module-entity-registration.md`
- ADR 0008 — `docs/adrs/0008-foundation-multitenancy.md`
- ADR 0007 — `docs/adrs/0007-bundle-manifest-schema.md`
