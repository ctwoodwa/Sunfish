---
uid: block-businesscases-entitlement-resolver
title: Business Cases — Entitlement Resolver
description: How BundleEntitlementResolver maps a tenant's active bundle + edition to feature values in the Sunfish feature-management pipeline.
keywords:
  - sunfish
  - businesscases
  - entitlement-resolver
  - feature-management
  - bundle
  - edition-mapping
---

# Business Cases — Entitlement Resolver

## Overview

`BundleEntitlementResolver` implements `Sunfish.Foundation.FeatureManagement.IEntitlementResolver`.
It is the link between a tenant's `BundleActivationRecord` and the feature-management
pipeline. When the pipeline asks "is feature X enabled for tenant T?", this resolver
answers by reading the tenant's active bundle manifest.

Source: `packages/blocks-businesscases/FeatureManagement/BundleEntitlementResolver.cs`

## Contract

```csharp
ValueTask<FeatureValue?> TryResolveAsync(
    FeatureKey key,
    FeatureEvaluationContext context,
    CancellationToken cancellationToken = default);
```

The resolver returns:

- `null` when it cannot determine the feature value — letting the feature-management
  pipeline fall through to other resolvers or a static default.
- A `FeatureValue` wrapping the resolved value when the bundle manifest answers the key.

## Dependencies

The resolver takes two collaborators via constructor injection:

- `IBundleCatalog` — looks up a bundle manifest by key.
- `IBusinessCaseService` — looks up the tenant's active `BundleActivationRecord`.

Both are registered by `AddInMemoryBusinessCases` (see [di-wiring.md](di-wiring.md)).

## Resolution algorithm

1. **No tenant context** — return `null`. The resolver is tenant-scoped; without a tenant
   it has nothing to say.

2. **No active bundle** — return `null`. A tenant with no bundle yields no bundle-sourced
   entitlements.

3. **Bundle not in catalog** — return `null`. The activation record references a bundle key
   that is not registered; treat as "unknown" and defer.

4. **Feature default hit** — if the manifest's `FeatureDefaults` dictionary contains the
   raw key, return `FeatureValue.Of(manifest.FeatureDefaults[key])`.

5. **Module-enabled probe** — if the key has the form `modules.<moduleKey>.enabled`, the
   resolver decomposes it and checks whether `<moduleKey>` is active for the tenant's
   bundle + edition. A module is active when:

   - It appears in the manifest's `RequiredModules` list (always on for the bundle), **or**
   - It appears in the manifest's `EditionMappings[edition]` list (conditional on edition).

   The resolver returns `FeatureValue.Of(true)` if active, `FeatureValue.Of(false)` if not.

6. **Everything else** — return `null`. Unknown keys that don't match either shape are
   deferred to the next resolver in the chain.

## Key-shape conventions

The resolver recognises two conventions:

- Raw keys (e.g. `"rent.auto-invoice"`) — looked up directly in `FeatureDefaults`.
- Structured module probes (e.g. `"modules.rent.enabled"`) — decomposed into the module
  key `rent` and matched against `RequiredModules` ∪ `EditionMappings[edition]`.

Prefix and suffix are string-compared with `StringComparison.Ordinal` — case-sensitive.

## Typical flow

```
IFeatureEvaluator.IsEnabledAsync("modules.rent.enabled", tenantContext)
  → BundleEntitlementResolver.TryResolveAsync
  → IBusinessCaseService.GetActiveRecordAsync(tenant)
  → record.BundleKey + record.Edition
  → IBundleCatalog.TryGet(bundleKey) → manifest
  → manifest.RequiredModules ∪ manifest.EditionMappings[edition] → contains "rent"?
  → FeatureValue.Of(true)
```

## Worked examples (from resolver tests)

The `BundleEntitlementResolverTests` fixture registers a manifest with these feature
defaults and edition mappings:

- `FeatureDefaults = { "leases.enabled" = "true", "leases.max-count" = "50" }`
- `EditionMappings["standard"] = [ "sunfish.blocks.leases" ]`
- `EditionMappings["enterprise"] = [ "sunfish.blocks.leases", "sunfish.blocks.maintenance" ]`
- `RequiredModules = [ "sunfish.blocks.identity" ]`

With `tenant-a` provisioned on the `standard` edition:

| Key | Result | Reason |
|---|---|---|
| `leases.enabled` | `"true"` | `FeatureDefaults` hit. |
| `modules.sunfish.blocks.leases.enabled` | `true` | In `EditionMappings["standard"]`. |
| `modules.sunfish.blocks.identity.enabled` | `true` | In `RequiredModules` (edition-independent). |
| `modules.sunfish.blocks.maintenance.enabled` | `false` | Not in standard edition's mapping. |
| `billing.max-invoices` | `null` | Unknown key shape; resolver defers. |

These are pinned by the resolver tests and should hold for any persistence-backed
implementation that honours the same `IBundleCatalog` contract.

## Feature key shapes

The resolver recognises two key shapes:

- **Raw keys** (`"<subsystem>.<flag>"` style): looked up directly in
  `BundleManifest.FeatureDefaults`. Example: `"leases.enabled"`, `"billing.allow-ach"`.
- **Module-probe keys** (`"modules.<moduleKey>.enabled"`): decomposed into the literal
  prefix `modules.`, a module key, and the literal suffix `.enabled`. Example:
  `"modules.sunfish.blocks.leases.enabled"` → module key `sunfish.blocks.leases`.

String matching is `StringComparison.Ordinal` — case-sensitive. If you adopt a different
module-key convention (e.g. hyphenated), stay consistent across the manifest and the
caller side of `IFeatureEvaluator.IsEnabledAsync`.

## Fallthrough semantics

Returning `null` is the resolver's only way to say "I don't know." The feature-management
pipeline treats `null` as a signal to try the next resolver. A resolver that returns a
concrete `FeatureValue` ends the search; make sure you _never_ return `FeatureValue.Of(false)`
when you mean "ask someone else" — that would pin the feature to `false` and prevent any
downstream resolver from overriding.

## Related pages

- [Overview](overview.md)
- [Bundle Provisioning Service](bundle-provisioning-service.md)
- [Entity Model](entity-model.md)
- [DI Wiring](di-wiring.md)
