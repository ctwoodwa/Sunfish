# ADR 0009 — Foundation.FeatureManagement (Flags vs. Entitlements vs. Editions)

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Sunfish needs a single, catalog-required feature-evaluation surface that distinguishes technical flags from product entitlements from subscription editions, without binding to any specific feature-flag vendor.

---

## Context

Sunfish bundles declare `featureDefaults` and `editionMappings` (ADR 0007); tenants subscribe to editions and optional add-ons (future `blocks-subscriptions`); Bridge needs to evaluate at runtime "is this capability available for this tenant in this request?" Today the repo has no unified feature surface. `Microsoft.FeatureManagement.AspNetCore` is referenced for Bridge but that ships a flag-only model that does not understand editions, entitlements, bundles, or tenant scope.

Four distinct concepts get conflated in the literature and in common implementations:

- **Technical flags** — runtime booleans / variants, often for rollouts or kill-switches, evaluated per request.
- **Product features** — named capabilities the product exposes (e.g. `leases.renewals.autoReminders`), potentially variant-valued.
- **Entitlements** — what a tenant is allowed to use based on their bundle, edition, and add-ons.
- **Editions / tiers** — named product configurations (lite, standard, enterprise) that bundle a set of capabilities.

Conflating these causes well-known failures:

- Treating entitlements as flags loses auditability (why is feature X on for tenant Y? — can't answer by reading flag config).
- Treating flags as entitlements leads to rollout accidents (a technical canary exposing unreleased paid features).
- Treating editions as feature flags makes edition upgrades feel like routine ops, hiding revenue-critical state transitions.

This ADR establishes four separate primitives, composed into one evaluator, behind an **OpenFeature-style provider seam** so the runtime flag backend remains pluggable.

---

## Decision

Introduce **`Sunfish.Foundation.FeatureManagement`** with the following shape.

### Primitives

| Type | Purpose |
|---|---|
| `FeatureKey` | Case-sensitive string identifier (e.g. `sunfish.blocks.leases.renewals.autoReminders`). |
| `FeatureValueKind` | Enum: `Boolean`, `String`, `Integer`, `Decimal`, `Json`. |
| `FeatureValue` | String-raw value with typed accessors (`AsBoolean`, `AsInt32`, …) and factory helpers (`Of(bool)`, `Of(int)`, `Of(string)`). |
| `FeatureSpec` | Catalog entry: key, kind, default raw value, optional description, owning module or bundle. |
| `FeatureEvaluationContext` | Input to evaluation: `TenantId?`, `Edition?`, `ActiveBundleKeys`, `ActiveModuleKeys`, `UserId?`, `Environment`, free-form `Attributes`. |

### Layered evaluators

| Interface | Responsibility |
|---|---|
| `IFeatureCatalog` | Declares known features (`FeatureSpec`s). Evaluating a feature not in the catalog is an error — prevents silent drift, mirrors the extension-field catalog rule from ADR 0005. |
| `IFeatureProvider` | **OpenFeature-style seam.** Returns a value if it has an opinion, `null` otherwise. Host adapters (OpenFeature, LaunchDarkly, flagd, Microsoft.FeatureManagement) plug in here. Ships with `InMemoryFeatureProvider` for tests and demos. |
| `IEntitlementResolver` | Computes entitlements from `(tenant, edition, bundles, modules)`. Returns a value if the entitlement rules determine one for this feature, `null` otherwise. Ships with `NoOpEntitlementResolver`; real tenants get a bundle-manifest-backed impl in P2. |
| `IEditionResolver` | Resolves `TenantId → edition key`. Ships with `FixedEditionResolver` for demos. |
| `IFeatureEvaluator` | Top-level read. Default impl chains the above. |

### Resolution order (default evaluator)

For `EvaluateAsync(FeatureKey, ctx)`:

1. **Catalog lookup.** If `key` is not registered, throw. Retrieve the `FeatureSpec`.
2. **Provider.** Call `IFeatureProvider.TryGetAsync(key, ctx)`. If non-null, return.
3. **Entitlements.** Call `IEntitlementResolver.TryResolveAsync(key, ctx)`. If non-null, return.
4. **Default.** If `FeatureSpec.DefaultValue` is non-null, wrap and return.
5. **Error.** Throw `InvalidOperationException` — catalog author must supply a default or an entitlement/provider must cover this context.

`IsEnabledAsync` is a sugar wrapper that evaluates and calls `AsBoolean()` on the result.

### OpenFeature correspondence

`IFeatureProvider` maps 1:1 to OpenFeature's `Provider` concept. An OpenFeature-backed adapter (`OpenFeatureFeatureProvider`) is a follow-up package that delegates to any OpenFeature-compatible backend. Sunfish never takes a direct dependency on an OpenFeature SDK or any vendor SDK. The seam is what matters.

### What this ADR does not do

- Does **not** define subscription, plan, usage meter, or billing types. Those live in `blocks-subscriptions` (P1) and `blocks-billing` (P2). This package only evaluates features given an already-resolved subscription context.
- Does **not** specify the bundle-manifest-backed entitlement resolver; that arrives in P2 alongside `blocks-businesscases`. The `IEntitlementResolver` interface is stable; the impl evolves.
- Does **not** wire into Bridge. That's a P1 follow-up.

### Package layout

- `packages/foundation-featuremanagement/Sunfish.Foundation.FeatureManagement.csproj`
- Root namespace: `Sunfish.Foundation.FeatureManagement`.
- ProjectReference to `Sunfish.Foundation` (for `TenantId`).
- Added to `Sunfish.slnx` under `/foundation/feature-management/`.

---

## Consequences

### Positive

- Flags, entitlements, and editions are three separate concepts with a single resolution path that composes them. No more ad-hoc "is feature X on" helpers scattered around modules.
- Bundle manifests (ADR 0007) become the authoring source for entitlements — `featureDefaults` and `editionMappings` map directly onto `FeatureSpec.DefaultValue` and `IEntitlementResolver` rules.
- Catalog-required rule prevents silent feature drift across modules.
- OpenFeature seam keeps vendor choice deferrable and swappable.
- Microsoft.FeatureManagement.AspNetCore can become a `IFeatureProvider` implementation if Bridge wants it, rather than the authoritative feature surface.

### Negative

- Four abstractions to hold in head instead of "just flags." Documentation and examples must explain the decomposition.
- `NoOpEntitlementResolver` means the first real multi-tenant evaluation needs a bundle-backed resolver — tracked as P2 follow-up.
- `FeatureValue` as raw string with typed accessors trades compile-time type safety for catalog-declared type safety. Compensated by catalog-required rule and runtime validation.

### Follow-ups

1. **Bundle-manifest-backed entitlement resolver** in `blocks-businesscases` (P2). Reads `IBundleCatalog` + tenant's active bundle + edition, resolves `editionMappings` → module enablement → feature defaults.
2. **OpenFeature adapter package** (`Sunfish.Foundation.FeatureManagement.OpenFeature`) as a separate csproj when a real OpenFeature backend is picked.
3. **Microsoft.FeatureManagement provider adapter** — optional; Bridge may prefer this over OpenFeature initially.
4. **Persistent feature catalog** — a database-backed `IFeatureCatalog` when Bridge manages per-tenant feature overrides beyond startup seed.
5. **Feature evaluation hook** into `Sunfish.Foundation.Catalog.ExtensionFields` — when an extension field is gated by a feature key, evaluate before materializing.

---

## References

- ADR 0005 — Type-Customization Model (catalog-required rule, extensibility primitives).
- ADR 0006 — Bridge Is a Generic SaaS Shell.
- ADR 0007 — Bundle Manifest Schema (`featureDefaults`, `editionMappings`).
- ADR 0008 — Foundation.MultiTenancy (`TenantId`, `ITenantContext`).
- OpenFeature specification — vendor-neutral provider model that `IFeatureProvider` mirrors.
- Microsoft.FeatureManagement.AspNetCore — currently referenced by Bridge; in this model becomes an `IFeatureProvider` implementation rather than the authoritative feature surface.
