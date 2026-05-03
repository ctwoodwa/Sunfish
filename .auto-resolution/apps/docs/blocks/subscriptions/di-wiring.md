---
uid: block-subscriptions-di-wiring
title: Subscriptions — DI Wiring
description: How to register blocks-subscriptions services and its ADR-0015 entity module.
keywords:
  - subscriptions
  - di
  - registration
  - ef-core
  - entity-module
  - adr-0015
---

# Subscriptions — DI Wiring

## Overview

`blocks-subscriptions` is wired into a host application through a single extension method on `IServiceCollection`. The extension is designed for demos and tests (in-memory service); production hosts replace the `ISubscriptionService` registration with a persistence-backed implementation while keeping the entity-module registration.

## Extension method

```csharp
public static IServiceCollection AddInMemorySubscriptions(this IServiceCollection services)
{
    ArgumentNullException.ThrowIfNull(services);
    services.AddSingleton<ISubscriptionService, InMemorySubscriptionService>();
    services.AddSingleton<ISunfishEntityModule, SubscriptionsEntityModule>();
    return services;
}
```

The method is exposed on `Sunfish.Blocks.Subscriptions.DependencyInjection.SubscriptionsServiceCollectionExtensions`.

## What gets registered

| Registration | Lifetime | Purpose |
|---|---|---|
| `ISubscriptionService` → `InMemorySubscriptionService` | Singleton | Core service. In-memory, thread-safe, non-persistent. |
| `ISunfishEntityModule` → `SubscriptionsEntityModule` | Singleton | ADR-0015 entity module — contributes the block's EF Core configurations to the shared Bridge `DbContext`. |

Both are registered as singletons so they share state across the host's request scopes and so the entity module's `Configure(ModelBuilder)` call is stable.

## Typical host wiring

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddSunfishFoundation()
    .AddSunfishBridge()          // registers the shared DbContext
    .AddInMemorySubscriptions(); // this block
```

The order matters only in so far as the Bridge registration is what actually consumes the `ISunfishEntityModule`s — as long as both are registered before the first request, the module's configurations are applied when the `DbContext` model is built.

## Swapping to a persistence-backed service

To move beyond in-memory, replace just the `ISubscriptionService` binding:

```csharp
builder.Services.AddInMemorySubscriptions();
builder.Services.AddSingleton<ISubscriptionService, MyEfSubscriptionService>();
```

The second registration overrides the first, but the `ISunfishEntityModule` registration is additive (every block contributes one), so it remains in place and Bridge continues to pick up the entity configurations.

## Multi-tenancy

`Subscription`, `UsageMeter`, and `MeteredUsage` implement `IMustHaveTenant`. The ambient `TenantId` is resolved from Foundation's tenancy primitives (`ITenantContext`), not wired here — ensure `AddSunfishFoundation()` (or equivalent) is called before the block is used in a request.

## Verifying the registration

```csharp
var provider = services.BuildServiceProvider();

// Service is available
var svc = provider.GetRequiredService<ISubscriptionService>();
Assert.IsType<InMemorySubscriptionService>(svc);

// Entity module is registered as an ISunfishEntityModule
var modules = provider.GetServices<ISunfishEntityModule>();
Assert.Contains(modules, m => m is SubscriptionsEntityModule);
```

The `tests/SubscriptionsEntityModuleTests.cs` fixture asserts the module key and that the configurations apply to a test `DbContext` without throwing — useful as a template for adding your own pre-deploy smoke test.

## Order of registrations

The extension is idempotent in the sense that calling it twice registers the service twice, with the last one winning for the singleton binding — but the `ISunfishEntityModule` list will contain two copies of the module, which Bridge will apply twice. Avoid this by calling `AddInMemorySubscriptions()` exactly once, or by using `Replace` or `RemoveAll<ISubscriptionService>()` before re-adding.

## Production host pattern

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddSunfishFoundation()                       // tenancy, ids, Instant
    .AddSunfishBridge(options =>                  // shared DbContext
    {
        options.UseNpgsql(connectionString);
    })
    .AddInMemorySubscriptions();                  // entity module + in-memory fallback

// Replace the in-memory service with the persisted one.
builder.Services.Replace(ServiceDescriptor.Singleton<ISubscriptionService, EfSubscriptionService>());

var app = builder.Build();
app.MapSunfishBridge();                           // optional; block-specific routes live elsewhere
app.Run();
```

The `Replace` call keeps the entity-module registration intact while switching the service implementation — the recommended pattern for swapping in persistence without losing EF Core wiring.

## Troubleshooting

- **"Unable to resolve ITenantContext"** — `AddSunfishFoundation()` was not called. Tenant-scoped entities need the tenancy primitives even if the block's own code doesn't touch them directly.
- **"Cannot find a DbContext"** — `AddSunfishBridge()` was not called. The entity module is applied by Bridge; without Bridge, the `ModelBuilder` configuration never runs.
- **Duplicate entity configuration errors** — you registered the block twice. Remove the duplicate `AddInMemorySubscriptions()` call.

## Test fixture pattern

A useful regression test pattern is to spin up a host, let the block register, and assert the service + module are wired:

```csharp
public class WiringTests
{
    [Fact]
    public void AddInMemorySubscriptions_RegistersServiceAndModule()
    {
        var services = new ServiceCollection();
        services.AddInMemorySubscriptions();

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ISubscriptionService>());
        Assert.Contains(sp.GetServices<ISunfishEntityModule>(),
            m => m is SubscriptionsEntityModule);
    }
}
```

Adapt to your host's needs — for example, pair with a `DbContext` constructor to verify `Configure(ModelBuilder)` ran without error.

## Related

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- ADR 0015 — `docs/adrs/0015-module-entity-registration.md`
- ADR 0008 — `docs/adrs/0008-foundation-multitenancy.md` (tenancy primitives)
