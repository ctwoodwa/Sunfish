---
uid: foundation-featuremanagement-feature-evaluator
title: Feature Management — Feature Evaluator
description: The IFeatureEvaluator surface, its evaluation order, and the context inputs it consumes.
---

# Feature Management — Feature Evaluator

## Surface

`IFeatureEvaluator` exposes two methods. `EvaluateAsync` returns a `FeatureValue` for features that carry a typed value; `IsEnabledAsync` is boolean sugar over the same pipeline.

```csharp
public interface IFeatureEvaluator
{
    ValueTask<FeatureValue> EvaluateAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default);

    ValueTask<bool> IsEnabledAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default);
}
```

`EvaluateAsync` throws `InvalidOperationException` in two cases:

- The feature is not registered in `IFeatureCatalog` — the caller is evaluating a feature the platform has not declared, which is treated as a bug, not a miss.
- No layer in the chain produced a value — the catalog has no default, the provider declined, and the entitlement resolver returned null.

## Evaluation order

`DefaultFeatureEvaluator` runs the three layers in this order and returns the first non-null `FeatureValue`:

1. **Catalog lookup** — `IFeatureCatalog.TryGetFeature(key, out spec)`. If the feature is not in the catalog the evaluator throws immediately.
2. **Provider override** — `IFeatureProvider.TryGetAsync(key, context, ct)`. Providers represent out-of-band overrides (ops toggles, A/B tests, kill switches). A provider that declines to evaluate returns `null`.
3. **Entitlement resolver** — `IEntitlementResolver.TryResolveAsync(key, context, ct)`. Entitlements represent declared tenant bundle selections (see [Entitlement Resolver](entitlement-resolver.md)).
4. **Catalog default** — `FeatureSpec.DefaultValue`. If non-null, wrapped as a `FeatureValue { Raw = default }`.

Providers override entitlements override defaults. That ordering reflects a practical preference: operators running a live system can pull a kill switch without waiting for a bundle-manifest update, and each tenant's declared plan still wins over the platform's generic default.

## The evaluation context

`FeatureEvaluationContext` is a `sealed record` — callers usually construct one per evaluation (or per request). Every field is optional; the evaluator does not require a tenant to be present.

| Field | Type | Notes |
|---|---|---|
| `TenantId` | `TenantId?` | Current tenant, when known. |
| `Edition` | `string?` | `lite`, `standard`, `enterprise`, … Often filled from `IEditionResolver`. |
| `ActiveBundleKeys` | `IReadOnlyList<string>` | Bundles the tenant has installed. |
| `ActiveModuleKeys` | `IReadOnlyList<string>` | Modules (via bundle activation) that are live. |
| `UserId` | `string?` | Caller identity, when known. |
| `Environment` | `string` | Defaults to `production`; providers often key off this. |
| `Attributes` | `IReadOnlyDictionary<string, string>` | Region, locale, device class, A/B cohort — anything a provider or resolver reads. |

## Reading values

```csharp
public sealed class ExportButton
{
    private readonly IFeatureEvaluator _features;

    public ExportButton(IFeatureEvaluator features) => _features = features;

    public async ValueTask<bool> IsVisibleAsync(FeatureEvaluationContext ctx, CancellationToken ct)
        => await _features.IsEnabledAsync(FeatureKey.Of("ui.exports.visible"), ctx, ct);

    public async ValueTask<int> MaxBatchSizeAsync(FeatureEvaluationContext ctx, CancellationToken ct)
    {
        var value = await _features.EvaluateAsync(FeatureKey.Of("exports.batch.max"), ctx, ct);
        return value.AsInt32();
    }
}
```

`FeatureValue` is a raw string that the caller casts with typed accessors. `AsBoolean`, `AsInt32`, `AsDecimal` throw if the raw value cannot be parsed in the declared `FeatureValueKind`. `AsString` is a no-op accessor returning the raw text (useful for `Json` and free-form `String` features).

## Providers

`IFeatureProvider` is intentionally narrow — one `TryGetAsync` method that returns `FeatureValue?`. Adapters plug OpenFeature, Microsoft.FeatureManagement, LaunchDarkly, flagd, and bespoke stores into the same slot. A provider that declines to evaluate returns `null`, and the next layer takes over. The platform ships `InMemoryFeatureProvider` for tests and accelerator seed data.

## Related

- [Overview](overview.md)
- [Entitlement Resolver](entitlement-resolver.md)
