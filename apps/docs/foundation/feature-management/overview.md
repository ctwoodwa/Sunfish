---
uid: foundation-featuremanagement-overview
title: Feature Management — Overview
description: Declarative feature flags and bundle-driven entitlements, backed by a pluggable provider seam.
keywords:
  - feature management
  - feature flags
  - entitlements
  - feature evaluator
  - OpenFeature
  - ADR 0009
---

# Feature Management — Overview

## What this package gives you

`Sunfish.Foundation.FeatureManagement` is the evaluation surface for feature flags and bundle entitlements. It lets modules ask "is this feature enabled for this caller?" or "what value does this feature have in this context?" and receive a deterministic answer from a chained evaluation pipeline.

Nothing in the package assumes a specific flag backend. It ships an OpenFeature-style provider seam so hosts can plug in Microsoft.FeatureManagement, LaunchDarkly, flagd, or a bespoke config-file store. The package source lives at `packages/foundation-featuremanagement/`.

## Core ideas

Features are **declared** in a catalog and **evaluated** through a chain. Every feature has a `FeatureKey` (case-sensitive identifier), a declared value-kind (`Boolean`, `String`, `Integer`, `Decimal`, `Json`), and an optional default. Evaluating a feature not in the catalog is a hard error — the evaluator throws rather than guess, which prevents silent drift across modules.

Evaluation happens against a `FeatureEvaluationContext` that carries the tenant, edition, active bundle and module keys, caller identity, environment, and free-form attributes. Every layer of the pipeline reads from the same context shape.

## Evaluation chain

`DefaultFeatureEvaluator` chains three layers and returns the first non-null value:

1. **Provider override** — `IFeatureProvider.TryGetAsync` consults the external flag backend.
2. **Entitlement resolver** — `IEntitlementResolver.TryResolveAsync` checks bundle-based tenant entitlements.
3. **Catalog default** — `FeatureSpec.DefaultValue` from the registered `IFeatureCatalog` entry.

If none of the three produces a value, the evaluator throws `InvalidOperationException`. This "fail loud" default protects against shipping a feature without a resolution path.

## Key types

| Type | Purpose |
|---|---|
| [`FeatureKey`](xref:Sunfish.Foundation.FeatureManagement.FeatureKey) | Case-sensitive, ordinal-compared identifier for a feature. |
| [`FeatureValueKind`](xref:Sunfish.Foundation.FeatureManagement.FeatureValueKind) | Declared value-type enum. |
| [`FeatureSpec`](xref:Sunfish.Foundation.FeatureManagement.FeatureSpec) | Catalog entry: key, kind, default value, owner key, description. |
| [`FeatureValue`](xref:Sunfish.Foundation.FeatureManagement.FeatureValue) | Raw string value with typed accessors (`AsBoolean`, `AsInt32`, `AsDecimal`, `AsString`). |
| [`FeatureEvaluationContext`](xref:Sunfish.Foundation.FeatureManagement.FeatureEvaluationContext) | Tenant + edition + bundles + modules + user + environment + attributes. |
| [`IFeatureCatalog`](xref:Sunfish.Foundation.FeatureManagement.IFeatureCatalog) | Registry of declared features. |
| [`IFeatureProvider`](xref:Sunfish.Foundation.FeatureManagement.IFeatureProvider) | OpenFeature-style external-backend seam. |
| [`IEntitlementResolver`](xref:Sunfish.Foundation.FeatureManagement.IEntitlementResolver) | Bundle-driven tenant entitlement resolution. |
| [`IEditionResolver`](xref:Sunfish.Foundation.FeatureManagement.IEditionResolver) | Returns the edition key (`lite`, `standard`, `enterprise`, …) for a tenant. |
| [`IFeatureEvaluator`](xref:Sunfish.Foundation.FeatureManagement.IFeatureEvaluator) | Top-level consumer surface. |

## `FeatureKey` and `FeatureValue`

`FeatureKey` is a `readonly record struct` wrapping a non-empty string; the `Of` factory rejects null, empty, and whitespace-only values, and the type provides an implicit conversion to `string` for logging and dictionary keys. Feature keys are reverse-DNS style by convention (`ui.exports.visible`, `exports.batch.max`) so they sort cleanly and stay globally unique.

`FeatureValue` wraps a single raw string and provides typed accessors:

```csharp
public sealed record FeatureValue
{
    public required string Raw { get; init; }

    public bool    AsBoolean();     // throws if Raw is not a boolean literal
    public int     AsInt32();       // invariant-culture parse
    public decimal AsDecimal();     // invariant-culture parse
    public string  AsString() => Raw;

    public static FeatureValue Of(bool value);
    public static FeatureValue Of(int value);
    public static FeatureValue Of(decimal value);
    public static FeatureValue Of(string value);
}
```

Casts use invariant culture so a value written in one region decodes identically in another. Callers choose their accessor based on the catalog's declared `FeatureValueKind`.

## Evaluation context

`FeatureEvaluationContext` is a `sealed record` — typically constructed per evaluation or cached per request:

| Field | Type | Notes |
|---|---|---|
| `TenantId` | `TenantId?` | Current tenant, when known. |
| `Edition` | `string?` | `lite`, `standard`, `enterprise`, … Often filled from `IEditionResolver`. |
| `ActiveBundleKeys` | `IReadOnlyList<string>` | Bundles the tenant has installed. |
| `ActiveModuleKeys` | `IReadOnlyList<string>` | Modules (via bundle activation) that are live. |
| `UserId` | `string?` | Caller identity, when known. |
| `Environment` | `string` | Defaults to `"production"`; providers often key off this. |
| `Attributes` | `IReadOnlyDictionary<string, string>` | Region, locale, device class, A/B cohort. |

Every field is optional. The evaluator does not require a tenant; features that reference tenant state rely on the provider or resolver to check.

## Registering the defaults

```csharp
using Sunfish.Foundation.FeatureManagement;

services.AddSunfishFeatureManagement();
```

`AddSunfishFeatureManagement` wires the default stack: `InMemoryFeatureCatalog`, `InMemoryFeatureProvider`, `NoOpEntitlementResolver`, and `DefaultFeatureEvaluator`. Callers replace individual services as they adopt richer providers (a real OpenFeature adapter, a bundle-manifest-backed entitlement resolver). Because every entry is a singleton, replacing one does not disturb the others — a host can keep the in-memory catalog for static feature declarations while pointing `IFeatureProvider` at a cloud-hosted flag backend.

## Related

- [Feature Evaluator](feature-evaluator.md)
- [Entitlement Resolver](entitlement-resolver.md)
- [Catalog — Bundle Manifests](../catalog/bundle-manifests.md)
- [ADR 0009 — Foundation.FeatureManagement](xref:adr-0009-foundation-featuremanagement)
</content>
</invoke>