---
uid: block-businesscases-di-wiring
title: Business Cases — DI Wiring
description: How AddInMemoryBusinessCases registers the services, entitlement resolver, and entity module into a host composition root.
keywords:
  - sunfish
  - businesscases
  - dependency-injection
  - addinmemorybusinesscases
  - entity-module
---

# Business Cases — DI Wiring

## Overview

The block ships one DI extension method: `AddInMemoryBusinessCases`. It registers the full
in-memory stack needed to exercise the block end-to-end in development, tests, and
kitchen-sink demos.

Source: `packages/blocks-businesscases/DependencyInjection/BusinessCasesServiceCollectionExtensions.cs`

## Usage

```csharp
using Sunfish.Blocks.BusinessCases.DependencyInjection;

builder.Services.AddInMemoryBusinessCases();
```

## What gets registered

| Service                              | Lifetime   | Implementation                          |
|--------------------------------------|------------|------------------------------------------|
| `InMemoryBundleActivationStore`      | Singleton  | Shared in-memory store.                  |
| `IBusinessCaseService`               | Singleton  | `InMemoryBusinessCaseService`            |
| `IBundleProvisioningService`         | Singleton  | `InMemoryBundleProvisioningService`      |
| `ISunfishEntityModule`               | Singleton  | `BusinessCasesEntityModule`              |
| `IEntitlementResolver`               | Singleton  | `BundleEntitlementResolver`              |

The `InMemoryBundleActivationStore` is registered first so both the read-side and write-side
services depend on the same instance — reads and writes see consistent state.

## Production deployments

Replace the in-memory registrations with a persistence-backed implementation:

- A persistence-backed `IBusinessCaseService` and `IBundleProvisioningService` reading and
  writing `BundleActivationRecord` through Bridge's `DbContext`.
- Keep `BundleEntitlementResolver` — it is framework-agnostic and has no in-memory state.
- Keep `BusinessCasesEntityModule` — it is the ADR-0015 hook for registering the entity
  configurations into the shared `DbContext`.

A typical production wiring might look like:

```csharp
services.AddScoped<IBusinessCaseService, EfBusinessCaseService>();
services.AddScoped<IBundleProvisioningService, EfBundleProvisioningService>();
services.AddSingleton<ISunfishEntityModule, BusinessCasesEntityModule>();
services.AddSingleton<IEntitlementResolver, BundleEntitlementResolver>();
```

Lifetime selection: the in-memory services are singletons because they hold the shared
in-memory store. The EF-backed services are typically `Scoped` to align with the
`DbContext` lifetime.

## Collaborators required upstream

The extension does **not** register `IBundleCatalog` — that belongs to
`Sunfish.Foundation.Catalog` and must already be registered by the host. The resolver
depends on `IBundleCatalog` + `IBusinessCaseService`; if either is missing from the
container, DI will fail at resolution time.

## Order of registrations in tests and demos

When the kitchen-sink or a test project wires a full stack, the order that produces a
working container is:

```csharp
services.AddSingleton<IBundleCatalog>(catalogWithManifests);   // from foundation-catalog
services.AddInMemoryBusinessCases();                           // this block
services.AddFeatureManagement();                               // foundation-featuremanagement
```

`IBundleCatalog` must be visible to the container before `BundleEntitlementResolver` is
resolved. `AddFeatureManagement` can come before or after `AddInMemoryBusinessCases` — the
order of `IEntitlementResolver` registrations is not significant inside the feature
pipeline.

## Troubleshooting

- **`InvalidOperationException: No service for type IBundleCatalog`** — the host forgot to
  register the bundle catalog. Register it via `Sunfish.Foundation.Catalog` before calling
  `AddInMemoryBusinessCases` (or at least before the first feature evaluation).
- **Resolver returns `null` unexpectedly** — confirm that
  (a) a tenant context is set on the `FeatureEvaluationContext`, (b) a bundle has been
  activated for that tenant, and (c) the bundle key in the activation record matches a
  manifest key registered in the catalog.
- **EF migrations don't see the table** — `BusinessCasesEntityModule` is picked up through
  `ISunfishEntityModule` registration; verify Bridge's model-builder flow calls every
  registered module. See ADR 0015 for the expected shape.

## Related pages

- [Overview](overview.md)
- [Entitlement Resolver](entitlement-resolver.md)
- [Bundle Provisioning Service](bundle-provisioning-service.md)
- [Entity Model](entity-model.md)
