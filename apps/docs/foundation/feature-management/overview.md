---
uid: foundation-featuremanagement-overview
title: Feature Management — Overview
description: Declarative feature flags and bundle-driven entitlements, backed by a pluggable provider seam.
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

## Registering the defaults

```csharp
using Sunfish.Foundation.FeatureManagement;

services.AddSunfishFeatureManagement();
```

`AddSunfishFeatureManagement` wires the default stack: `InMemoryFeatureCatalog`, `InMemoryFeatureProvider`, `NoOpEntitlementResolver`, and `DefaultFeatureEvaluator`. Callers replace individual services as they adopt richer providers (a real OpenFeature adapter, a bundle-manifest-backed entitlement resolver).

## Related

- [Feature Evaluator](feature-evaluator.md)
- [Entitlement Resolver](entitlement-resolver.md)
- [Catalog — Bundle Manifests](../catalog/bundle-manifests.md)
