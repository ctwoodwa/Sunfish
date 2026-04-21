---
uid: block-businesscases-overview
title: Business Cases — Overview
description: Bundle activation, edition selection, and entitlement resolution for multi-tenant Sunfish hosts.
---

# Business Cases — Overview

## What this block is

`Sunfish.Blocks.BusinessCases` turns Sunfish's bundle catalog into a tenant-aware entitlement
surface. It answers three linked questions for any tenant:

1. **Which bundle is active?** — recorded in `BundleActivationRecord`.
2. **Which edition is active?** — one of the editions declared by the bundle's
   `EditionMappings`.
3. **Which modules and feature values flow from that bundle + edition?** — materialised as a
   `TenantEntitlementSnapshot` and surfaced to
   `Sunfish.Foundation.FeatureManagement` through an `IEntitlementResolver`.

The block is the canonical bridge between a static bundle manifest and runtime feature
evaluation. If your app calls `IFeatureEvaluator.IsEnabledAsync("modules.rent.enabled")`,
that call flows through `BundleEntitlementResolver`, which flows through
`IBusinessCaseService`, which reads `BundleActivationRecord`.

## Package

- Package: `Sunfish.Blocks.BusinessCases`
- Source: `packages/blocks-businesscases/`
- Namespace roots:
  - `Sunfish.Blocks.BusinessCases.Models`
  - `Sunfish.Blocks.BusinessCases.Services`
  - `Sunfish.Blocks.BusinessCases.FeatureManagement`
  - `Sunfish.Blocks.BusinessCases.Data`
  - `Sunfish.Blocks.BusinessCases.DependencyInjection`

## When to use it

Use `Sunfish.Blocks.BusinessCases` when your host:

- Needs to record which bundle a tenant has activated (and at what edition).
- Needs entitlements (feature flags + module enablement) to derive from a tenant's active
  bundle rather than from static config.
- Uses `Sunfish.Foundation.FeatureManagement` and wants bundle-driven defaults before
  falling through to other resolvers.

## Key pieces

- `IBundleProvisioningService` — the **write** path: activate, deactivate a bundle for a tenant.
  See [bundle-provisioning-service.md](bundle-provisioning-service.md).
- `IBusinessCaseService` — the **read** path: get a tenant's snapshot, list editions,
  resolve modules for a given edition.
- `BundleEntitlementResolver` — the `IEntitlementResolver` implementation that participates
  in the feature-management pipeline. See [entitlement-resolver.md](entitlement-resolver.md).
- `BundleActivationRecord` / `TenantEntitlementSnapshot` — the two persistence shapes.
  See [entity-model.md](entity-model.md).
- `BusinessCasesEntityModule` — the `ISunfishEntityModule` contribution that registers
  EF Core entity configurations into Bridge's shared `DbContext` per ADR 0015.

## Diagnostic block

`EntitlementSnapshotBlock.razor` is a Blazor diagnostic view. It renders the current
tenant's active bundle, edition, active modules, and resolved feature values. Drop it into
an admin page to see exactly what the entitlement pipeline has evaluated to.

## Related ADRs

- [ADR 0015 — Module-Entity Registration](../../docs/adrs/0015-module-entity-registration.md):
  `BusinessCasesEntityModule` implements the canonical `ISunfishEntityModule` pattern.
- [ADR 0009 — Foundation Feature Management](../../docs/adrs/0009-foundation-featuremanagement.md):
  `BundleEntitlementResolver` is the bundle-aware resolver in the feature-management chain.
- [ADR 0007 — Bundle Manifest Schema](../../docs/adrs/0007-bundle-manifest-schema.md): the
  bundle manifest shape (`RequiredModules`, `EditionMappings`, `FeatureDefaults`) that this
  block reads from.
- [ADR 0008 — Foundation MultiTenancy](../../docs/adrs/0008-foundation-multitenancy.md):
  `BundleActivationRecord` implements `IMustHaveTenant`, so Bridge's central tenant filter
  scopes every read and write.

## Related pages

- [Entitlement Resolver](entitlement-resolver.md)
- [Bundle Provisioning Service](bundle-provisioning-service.md)
- [Entity Model](entity-model.md)
- [DI Wiring](di-wiring.md)
