---
uid: foundation-featuremanagement-entitlement-resolver
title: Feature Management — Entitlement Resolver
description: Bundle-driven entitlement resolution — how tenant bundle selections map to feature values.
keywords:
  - entitlement resolver
  - IEntitlementResolver
  - bundle entitlement
  - FeatureEvaluationContext
  - edition gating
  - ADR 0009
---

# Feature Management — Entitlement Resolver

## Purpose

Entitlements answer the question "**what has this tenant paid for?**" in terms feature-management can evaluate. An `IEntitlementResolver` takes a `FeatureKey` and the current `FeatureEvaluationContext`, consults the tenant's active bundles and edition, and returns a `FeatureValue` — or `null` when the tenant's entitlement state does not determine a value for that feature.

```csharp
public interface IEntitlementResolver
{
    ValueTask<FeatureValue?> TryResolveAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default);
}
```

The resolver sits in the middle of the evaluation chain: after provider overrides, before catalog defaults. When a tenant has no explicit provider override and has not opted into a bundle that declares the feature, the catalog's default wins.

## How bundles drive entitlements

A tenant's entitlement state is derived from the bundles the tenant has activated and the edition it runs on. Each `BusinessCaseBundleManifest` (see [Catalog — Bundle Manifests](../catalog/bundle-manifests.md)) carries two relevant shapes:

- `FeatureDefaults : IReadOnlyDictionary<string, string>` — the feature values the bundle applies when activated.
- `EditionMappings : IReadOnlyDictionary<string, IReadOnlyList<string>>` — the module keys a given edition activates.

A bundle-backed resolver combines the incoming context's `ActiveBundleKeys` and `Edition` fields with the catalog's manifest state to produce the right value for the current feature key.

## Resolver shape a bundle-backed adapter would take

```csharp
public sealed class BundleManifestEntitlementResolver : IEntitlementResolver
{
    private readonly IBundleCatalog _bundles;

    public BundleManifestEntitlementResolver(IBundleCatalog bundles) => _bundles = bundles;

    public ValueTask<FeatureValue?> TryResolveAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var bundleKey in context.ActiveBundleKeys)
        {
            if (!_bundles.TryGet(bundleKey, out var manifest)) continue;
            if (manifest.FeatureDefaults.TryGetValue(key.Value, out var raw))
            {
                return ValueTask.FromResult<FeatureValue?>(new FeatureValue { Raw = raw });
            }
        }

        return ValueTask.FromResult<FeatureValue?>(null);
    }
}
```

A richer adapter would consider edition precedence (later editions overriding earlier ones), add-on entitlements layered on top of a base bundle, and per-tenant overrides authored through Bridge admin surfaces.

## The shipped default is a no-op

`NoOpEntitlementResolver` is the resolver registered by `AddSunfishFeatureManagement`. It always returns `null`, which means the evaluation chain falls through to the catalog default when no provider override exists. This is the right default for tests, demos, and lite-mode deployments, and it leaves the richer bundle-backed adapter as a P2 follow-up that ships with `blocks-businesscases`.

Replacing the default is a one-liner at startup:

```csharp
services.AddSingleton<IEntitlementResolver, BundleManifestEntitlementResolver>();
```

## Edition resolution

`IEditionResolver` returns the edition key for a tenant. The `FixedEditionResolver` returns the same key for every tenant — useful for demos and lite-mode. A database-backed resolver plugs in when Bridge moves its tenant registry from in-memory seed to Postgres.

```csharp
public interface IEditionResolver
{
    ValueTask<string?> ResolveEditionAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}
```

Hosts typically call `IEditionResolver` once per request to populate `FeatureEvaluationContext.Edition` before evaluating features.

## Related

- [Overview](overview.md)
- [Feature Evaluator](feature-evaluator.md)
- [Catalog — Bundle Manifests](../catalog/bundle-manifests.md)
