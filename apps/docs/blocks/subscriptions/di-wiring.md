---
uid: block-subscriptions-di-wiring
title: Subscriptions — DI Wiring
description: How to register blocks-subscriptions services and its ADR-0015 entity module.
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

## Related

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- ADR 0015 — `docs/adrs/0015-module-entity-registration.md`
