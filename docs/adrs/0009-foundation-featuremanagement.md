---
id: 9
title: Foundation.FeatureManagement (Flags vs. Entitlements vs. Editions)
status: Accepted
date: 2026-04-19
tier: foundation
concern:
  - configuration
  - commercial
  - multi-tenancy
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: [A1]
---
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
6. **Operator-issued feature toggles** (Amendment A1) — extends this ADR with a fifth concept; see `## Amendment A1` below.

---

## References

- ADR 0005 — Type-Customization Model (catalog-required rule, extensibility primitives).
- ADR 0006 — Bridge Is a Generic SaaS Shell.
- ADR 0007 — Bundle Manifest Schema (`featureDefaults`, `editionMappings`).
- ADR 0008 — Foundation.MultiTenancy (`TenantId`, `ITenantContext`).
- OpenFeature specification — vendor-neutral provider model that `IFeatureProvider` mirrors.
- Microsoft.FeatureManagement.AspNetCore — currently referenced by Bridge; in this model becomes an `IFeatureProvider` implementation rather than the authoritative feature surface.

---

## Amendment A1 — Operator-Issued Feature Toggles (Wayfinder Consumer)

**Amendment date:** 2026-05-02
**Authors:** XO research session
**Workstream:** W#43
**Pipeline variant:** `sunfish-api-change`
**Prerequisite:** ADR 0065 (Wayfinder System + Standing Order Contract) — Status: Accepted

---

### A1.1 Context

ADR 0009 establishes four orthogonal concepts for feature evaluation:

1. **Technical flags** — runtime booleans / variants for rollouts and kill-switches.
2. **Product features** — named capabilities the product exposes, potentially variant-valued.
3. **Entitlements** — what a tenant is allowed to use based on bundle, edition, and add-ons.
4. **Editions / tiers** — named product configurations that bundle a set of capabilities.

The W#34 Wayfinder configuration UX discovery (§6.1) identified a fifth concept that the four-concept model does not accommodate: **operator-issued feature toggles** — durable, audited, tenant-scoped overrides that an operator sets via the Wayfinder/Atlas interface and that persist across restarts, deployments, and multi-anchor topologies.

The gap matters because each of the four existing concepts serves a different temporal scope and authority level:

| Concept | Set by | Scope | Durability |
|---|---|---|---|
| Technical flags | Engineering deploy / CI | Platform-wide | Ephemeral (env/config) |
| Product features | Product catalog | Platform-wide | Static (bundle manifest) |
| Entitlements | Bundle/edition subscription | Per-tenant | Semi-durable (subscription) |
| Editions | Subscription management | Per-tenant | Semi-durable (subscription) |
| **Operator toggles** | **Tenant operator via Atlas** | **Per-tenant** | **Durable (Standing Order log)** |

Without the fifth concept, operators who want to override a product default for their tenant must either (a) mutate entitlement state (wrong — entitlements are subscription-derived, not operator-configured), or (b) reach into vendor-specific flag APIs that bypass the audit trail (wrong — every configuration change that affects product behavior must be attributed, audited, and reversible). This amendment closes the gap.

**Relationship to ADR 0065:** ADR 0065 specifies the Wayfinder substrate (Standing Orders, `IStandingOrderIssuer`, `IAtlasProjector`, CRDT semantics, audit-by-construction). This amendment specifies how `Sunfish.Foundation.FeatureManagement` *consumes* that substrate — the bounded surface, the mapping from Standing Order paths to feature keys, and the migration story for the existing four-concept evaluator chain.

---

### A1.2 Decision drivers

1. **Audit-by-construction.** An operator changing a feature toggle for a production tenant must produce a Standing Order with an audit record per ADR 0065's §4. Bespoke feature-override storage would require separate audit wiring; consuming the Wayfinder substrate gets it for free.
2. **CRDT-native durability.** Multi-anchor Sunfish deployments (ADR 0032) share a CRDT-replicated state via ADR 0028's `ICrdtEngine`. An operator's toggle issued on one anchor must propagate to all anchors deterministically. Standing Orders use the same CRDT log — the fifth concept inherits convergence without a bespoke sync layer.
3. **Additive, no migration required.** The existing four-concept evaluator chain (§Resolution order) is unchanged. The fifth concept plugs in as a new `IFeatureProvider` implementation — a named `WayfinderFeatureProvider` — that the host application registers. Existing deployments without the Wayfinder substrate continue to work as-is.
4. **Single Atlas surface.** Feature toggles set by operators should be discoverable, searchable, and reversible via the Atlas UI — the same surface that manages every other Wayfinder-backed setting. Consuming `IAtlasProjector` rather than building a parallel feature-toggle store keeps Atlas as the single pane.
5. **Path-to-feature-key convention.** Standing Orders identify settings by dotted path within a scope. Feature keys in `Sunfish.Foundation.FeatureManagement` are already dotted identifiers (e.g., `sunfish.blocks.leases.renewals.autoReminders`). The mapping is bijective; no translation layer is needed.

---

### A1.3 Considered options

#### Option A — Parallel feature-toggle store

Introduce a separate `IFeatureToggleStore` backed by bespoke per-tenant storage. Feature evaluation reads the store as a separate layer outside the Wayfinder system.

**Rejected.** Breaks audit-by-construction (separate wiring needed); adds a third storage surface for configuration; operator UX splits across Atlas (Wayfinder settings) and a separate toggle UI; CRDT sync must be reinvented.

#### Option B — Extend `IEntitlementResolver` to read from Wayfinder

Reuse the entitlement layer to project operator toggles.

**Rejected.** Entitlements are subscription-derived (bundle manifest → edition → module enablement → feature defaults). Operator overrides are a different authority level; conflating them produces the same auditability problem described in ADR 0009's context section — "why is feature X on for tenant Y?" would require disambiguating whether the answer is "because of their subscription" or "because an operator set it."

#### Option C — New `IFeatureProvider` implementation backed by `IAtlasProjector` [RECOMMENDED]

Introduce `WayfinderFeatureProvider : IFeatureProvider` that calls `IAtlasProjector.ProjectAsync` and resolves feature values from the Atlas materialized view. Plugs into the existing evaluator chain as a provider. No changes to `DefaultFeatureEvaluator`, `IFeatureCatalog`, `IEntitlementResolver`, or `IEditionResolver`.

**Chosen.** Preserves the four-concept evaluator chain. Inherits audit, CRDT durability, and Atlas discoverability from ADR 0065. Provider returns `null` when no Standing Order covers a feature — the evaluator falls through to entitlements and catalog defaults as designed.

---

### A1.4 Decision

#### The fifth concept

**Operator-issued feature toggles** are the fifth concept in `Sunfish.Foundation.FeatureManagement`. An operator-issued toggle is a `StandingOrder` (ADR 0065 §1) under `StandingOrderScope.Tenant` whose path addresses a registered `FeatureKey`. Toggles are:

- Set via the Atlas UI (or programmatically via `IStandingOrderIssuer.IssueAsync`) — not via the feature-management API itself.
- Durable in the per-tenant Standing Order log; survive restarts and deployments.
- CRDT-replicated across multi-anchor topologies (ADR 0028).
- Audited by construction: every issuance, rescission, and conflict produces an `AuditRecord` via `IAuditTrail.AppendAsync` (ADR 0049 / ADR 0065 §4).

The feature-management package is a **read-only consumer** of the Wayfinder substrate. It does not issue Standing Orders; it reads them via `IAtlasProjector`.

#### Path-to-feature-key mapping convention

Standing Orders address settings by `(scope, path)`. For operator-issued feature toggles:

- **Scope:** `StandingOrderScope.Tenant` (always; feature toggles are per-tenant operator decisions).
- **Path:** `features.<FeatureKey>`, where `<FeatureKey>` is the exact string value of the `FeatureKey` identifier (e.g., `features.sunfish.blocks.leases.renewals.autoReminders`).

The `features.` prefix namespaces feature toggles within the Tenant-scope Standing Order log, isolating them from other Wayfinder settings (theme, locale, security posture, etc.) without requiring a separate scope value.

#### `WayfinderFeatureProvider` interface

New type in `Sunfish.Foundation.FeatureManagement`:

```csharp
/// <summary>
/// <see cref="IFeatureProvider"/> backed by the Wayfinder/Atlas projection.
/// Resolves operator-issued feature toggles from the per-tenant Standing Order log
/// via <see cref="IAtlasProjector"/>. Returns <c>null</c> for any feature key that
/// has no Standing Order at the canonical path <c>features.{key}</c>.
/// </summary>
public sealed class WayfinderFeatureProvider : IFeatureProvider
{
    private readonly IAtlasProjector _projector;

    public WayfinderFeatureProvider(IAtlasProjector projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        _projector = projector;
    }

    /// <inheritdoc />
    public async ValueTask<FeatureValue?> TryGetAsync(
        FeatureKey key,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.TenantId is not { } tenantId)
        {
            // No tenant context — Wayfinder toggles are per-tenant; pass through.
            return null;
        }

        var path = $"features.{key.Value}";

        var atlasView = await _projector.ProjectAsync(
            tenantId,
            scopeFilter: StandingOrderScope.Tenant,
            cancellationToken).ConfigureAwait(false);

        if (!atlasView.SettingsByPath.TryGetValue(path, out var snapshot))
        {
            return null;
        }

        if (snapshot.CurrentValue is null)
        {
            return null;
        }

        // AtlasSettingSnapshot.CurrentValue is a JsonNode; feature values are
        // stored as their raw string representation per FeatureValue conventions.
        var raw = snapshot.CurrentValue.ToString();
        return new FeatureValue { Raw = raw };
    }
}
```

**Notes on the implementation:**

- `IAtlasProjector` is defined in `Sunfish.Foundation.Wayfinder` (ADR 0065 §5). `WayfinderFeatureProvider` takes a `ProjectReference` on `Sunfish.Foundation.Wayfinder` — this dependency is additive; existing deployments that do not register `WayfinderFeatureProvider` take no new transitive dependency.
- `FeatureKey.Value` is the string value of the `FeatureKey` record struct. The `FeatureKey` type already exists in `Sunfish.Foundation.FeatureManagement`.
- `FeatureEvaluationContext.TenantId` is `TenantId?` — null when evaluation is outside a tenant context (e.g., a platform-wide technical flag). The provider returns `null` in that case, letting the evaluator fall through to entitlements and defaults.
- `AtlasSettingSnapshot.CurrentValue` is `JsonNode?` (ADR 0065 §5). Feature toggle values stored as boolean `true`/`false` in the Standing Order will arrive as JSON `true`/`false`; `JsonNode.ToString()` on a boolean node produces the canonical `"true"`/`"false"` string that `FeatureValue.AsBoolean()` consumes correctly (same as `FeatureValue.Of(bool)` encoding).
- The provider does **not** cache the `AtlasView` internally. Caching at the Atlas projection layer is `DefaultAtlasProjector`'s responsibility (warm-projection cache per ADR 0065 §F9 mechanical fix). The provider's job is routing, not caching.
- For all five `FeatureValueKind` values, `JsonNode.ToString()` produces the raw value that `FeatureValue`'s typed accessors expect: `String` nodes produce the unquoted string value; `Number` nodes produce the number literal (e.g., `"42"`); `Boolean` nodes produce `"true"`/`"false"` (lowercase); `Json` nodes produce canonical JSON. The round-trip is correct for all supported kinds.

#### `FeatureKey.Value` structural note

`FeatureKey` in the existing codebase is declared as:

```csharp
namespace Sunfish.Foundation.FeatureManagement;

public readonly record struct FeatureKey(string Value);
```

The `.Value` accessor is the correct field to use when constructing the Standing Order path. This amendment does not modify `FeatureKey`.

#### Resolution order (updated)

The default evaluator chain is **unchanged**. `WayfinderFeatureProvider` is a named `IFeatureProvider` implementation; hosts that register it replace `InMemoryFeatureProvider` or wrap it. The canonical resolution order with the Wayfinder provider registered as the provider slot:

1. **Catalog lookup.** If `key` is not registered, throw.
2. **Provider (now `WayfinderFeatureProvider`).** Calls `IAtlasProjector.ProjectAsync(tenantId, Tenant)` and looks up `features.{key}`. If a Standing Order covers this path for this tenant, returns the `FeatureValue`. Otherwise returns `null`.
3. **Entitlements.** `IEntitlementResolver.TryResolveAsync(key, ctx)`.
4. **Catalog default.** `FeatureSpec.DefaultValue`.
5. **Error.** `InvalidOperationException`.

The provider slot remains a single `IFeatureProvider` — a host wanting both `WayfinderFeatureProvider` and another backend (e.g., a LaunchDarkly adapter) composes them via a `CompositeFeatureProvider` (see §A1.6 follow-ups). This amendment does not ship `CompositeFeatureProvider`; the `IFeatureProvider` seam is sufficient for v1.

#### DI registration

Two new extension overloads on `ServiceCollectionExtensions`:

```csharp
/// <summary>
/// Replaces the default <see cref="InMemoryFeatureProvider"/> with
/// <see cref="WayfinderFeatureProvider"/> backed by the registered
/// <see cref="IAtlasProjector"/>. Requires <c>AddSunfishWayfinder()</c>
/// to be registered on the same <see cref="IServiceCollection"/>.
/// </summary>
public static IServiceCollection AddSunfishFeatureManagementWithWayfinder(
    this IServiceCollection services)
{
    services.AddSunfishFeatureManagement();
    // Replace the default InMemoryFeatureProvider with the Wayfinder-backed provider.
    // NOTE: must be called after any other IFeatureProvider registrations;
    // Microsoft DI uses last-wins semantics for multiple registrations of the same
    // service type when resolved as a single instance (not IEnumerable<T>).
    services.AddSingleton<IFeatureProvider, WayfinderFeatureProvider>();
    return services;
}

/// <summary>
/// Registers only the <see cref="WayfinderFeatureProvider"/> as the
/// <see cref="IFeatureProvider"/> implementation. Use when the caller has
/// already registered <see cref="AddSunfishFeatureManagement"/> manually
/// and wants to swap the provider independently.
/// </summary>
public static IServiceCollection AddWayfinderFeatureProvider(
    this IServiceCollection services)
{
    services.AddSingleton<IFeatureProvider, WayfinderFeatureProvider>();
    return services;
}
```

`AddSunfishFeatureManagementWithWayfinder()` calls `AddSunfishFeatureManagement()` first (which registers `InMemoryFeatureProvider` as the `IFeatureProvider`), then re-registers `WayfinderFeatureProvider` — the last-wins semantics of `AddSingleton` with the same service type means `WayfinderFeatureProvider` is the active implementation. This is the same override pattern used in other Sunfish packages for swappable implementations.

Guard: `AddSunfishFeatureManagementWithWayfinder()` does **not** call `AddSunfishWayfinder()` itself — that remains the host's responsibility (same composition-guard pattern as ADR 0065 §6). If `IAtlasProjector` is not registered, the DI container will throw at first `WayfinderFeatureProvider` resolution with a clear missing-service message.

#### Bundle manifest `featureDefaults` unchanged

ADR 0007's `featureDefaults` field feeds `FeatureSpec.DefaultValue` (step 4 in the resolution order). This amendment does not change that relationship. Bundle defaults remain the static baseline; operator toggles (step 2) override them on a per-tenant basis.

---

### A1.5 Consequences

#### Positive

1. **Fifth concept completes the configuration model.** The audit gap (operator-driven feature changes not attributed to an operator action) is closed. Every feature override now has provenance: who set it, when, from what Atlas path, under which Standing Order.
2. **Zero migration cost.** Existing deployments continue to work. `WayfinderFeatureProvider` is opt-in via `AddSunfishFeatureManagementWithWayfinder()`. No breaking change to `IFeatureProvider`, `IFeatureEvaluator`, `DefaultFeatureEvaluator`, or any existing interface.
3. **CRDT durability inherited.** Toggles survive restarts, survive anchor-to-anchor replication, and converge under concurrent issuance per ADR 0065 §2 conflict-resolution semantics. The feature-management package adds no new replication concern.
4. **Atlas discoverability.** Feature toggles appear in the Atlas search surface alongside all other Wayfinder settings. Operators can search `features.` to see all active feature overrides for a tenant.
5. **Resolution order transparency preserved.** An operator can reason about why a feature is on for their tenant: either a Standing Order covers `features.{key}` (check the Atlas view), or entitlements cover it (check the bundle), or the catalog default applies.

#### Negative

1. **Wayfinder substrate dependency.** Hosts using `WayfinderFeatureProvider` now take a transitive dependency on `Sunfish.Foundation.Wayfinder` (W#42 Phase 1 substrate). If Wayfinder is not yet shipped, `WayfinderFeatureProvider` is unavailable. Mitigation: the provider is additive; `InMemoryFeatureProvider` remains the default.
2. **Cold-projection latency on first evaluation.** Per ADR 0065 §F9, cold-projection latency is P95 ≤ 200ms. Feature evaluation on the hot path (e.g., per-request middleware) SHOULD warm the Atlas projection cache at startup or at tenant-onboarding time, not on the first evaluation. The host is responsible for warming; `WayfinderFeatureProvider` does not auto-warm.
3. **Null context is a silent pass-through.** When `FeatureEvaluationContext.TenantId` is null, the provider returns `null` and evaluation falls through to entitlements/defaults — the expected behavior for platform-wide technical flags. But a tenant evaluation that inadvertently passes a null `TenantId` will silently miss all operator toggles. Hosts should validate `TenantId` at the request boundary, not at evaluation time. This is the same constraint as `IEntitlementResolver.TryResolveAsync` — consistent with the existing contract.
4. **`CompositeFeatureProvider` not shipped.** Hosts wanting both a Wayfinder-backed operator toggle layer and a vendor-backed (LaunchDarkly, OpenFeature) technical-flag layer must compose manually today. The follow-ups section notes `CompositeFeatureProvider` as the natural next step.

#### Trust impact

- `WayfinderFeatureProvider` is a read-only consumer of `IAtlasProjector`. It cannot issue or modify Standing Orders. Trust surface is read-only projection.
- Operator authority to set feature toggles is governed by the Standing Order `StandingOrderScope.Tenant` issuance rules in ADR 0065 §3 (validation pipeline, `Authority = 300` check via `ICapabilityGraph.HasCapability`). The feature-management package inherits that authority model; it does not define a new one.

---

### A1.6 Follow-ups

1. **`CompositeFeatureProvider`** — chains multiple `IFeatureProvider` implementations in priority order. Needed when a host wants both Wayfinder operator toggles (highest priority) and a vendor-backed flag system (lower priority). File as a separate workstream when a concrete consumer demands it.
2. **`apps/docs/blocks/foundation-featuremanagement.md` cross-link** — add a "Operator-issued toggles via Wayfinder" section cross-linking to `apps/docs/foundation/wayfinder/overview.md` (Phase 4 of W#42) and describing the `AddSunfishFeatureManagementWithWayfinder()` registration pattern.
3. **Atlas schema registration for feature keys** — `WayfinderFeatureProvider` is a consumer of Atlas projections; it should also register an `AtlasSchemaDescriptor` per feature key so the Roslyn analyzer (ADR 0065 Phase 3b / W#42) does not emit warnings for the `AddSunfishFeatureManagementWithWayfinder()` call. Implementation: a `WayfinderFeatureSchemaRegistrar` that reads the `IFeatureCatalog` at startup and registers one `AtlasSchemaDescriptor` per `FeatureSpec` under `features.{key}`. Ship with W#42 Phase 3b or as a standalone follow-up workstream.
4. **Warm-projection guidance** — document the recommended `IHostedService` warm-up pattern (call `IAtlasProjector.ProjectAsync` at tenant-onboarding time or at `IHostedService.StartAsync`) in `apps/docs/foundation/wayfinder/feature-toggles.md`.

---

### A1.7 Implementation checklist

- [ ] Add `WayfinderFeatureProvider.cs` to `packages/foundation-featuremanagement/`
- [ ] Add `AddSunfishFeatureManagementWithWayfinder()` + `AddWayfinderFeatureProvider()` overloads to `ServiceCollectionExtensions.cs`
- [ ] Add `ProjectReference` to `Sunfish.Foundation.Wayfinder` in `Sunfish.Foundation.FeatureManagement.csproj` (conditional or optional, so existing consumers do not pull the Wayfinder dep unless using the new extension)
- [ ] Unit tests: 8 tests covering — (a) `TryGetAsync` returns `FeatureValue` when Standing Order covers `features.{key}` for the tenant, (b) returns `null` when path not in AtlasView, (c) returns `null` when `TenantId` is null, (d) returns `null` when `CurrentValue` is null (rescinded toggle), (e) `AsBoolean()` on the returned value is correct for `JsonNode` boolean serialization, (f) evaluator chain resolves via Wayfinder then falls through to entitlement when no toggle, (g) `AddSunfishFeatureManagementWithWayfinder()` registers `WayfinderFeatureProvider` as the active `IFeatureProvider`, (h) missing `IAtlasProjector` registration throws at resolution time with clear message
- [ ] Ledger flip: W#43 row `design-in-flight` → `built`

**Estimated effort:** ~3-5h sunfish-PM time (1 PR; all additive; no behavior change to existing path).

---

### A1.8 §A0 — self-audit limitation block (per cohort discipline; three-direction)

The author of this amendment ran the standard 3-direction self-audit on every cited `Sunfish.*` symbol:

- **§A0.1 Negative-existence**: `WayfinderFeatureProvider`, `AddSunfishFeatureManagementWithWayfinder()`, and `AddWayfinderFeatureProvider()` are introduced by this amendment and verified **NOT yet present** in `packages/foundation-featuremanagement/` on `origin/main`. `Sunfish.Foundation.Wayfinder.*` (including `IAtlasProjector`, `AtlasView`, `AtlasSettingSnapshot`, `StandingOrderScope`) is introduced by W#42 build phases and verified **NOT yet present** in `packages/` on `origin/main`. Council should re-verify if W#42 Phase 1 PRs have landed concurrently.

- **§A0.2 Positive-existence**: verified on `origin/main` — `IFeatureProvider` (namespace `Sunfish.Foundation.FeatureManagement`; `TryGetAsync(FeatureKey, FeatureEvaluationContext, CancellationToken)` signature) ✓; `FeatureEvaluationContext.TenantId` is `TenantId?` ✓; `TenantId` is `readonly record struct(string Value)` in namespace `Sunfish.Foundation.Assets.Common` ✓; `FeatureValue` with `Raw` property and `AsBoolean()` method ✓; `DefaultFeatureEvaluator` with 3-arg constructor `(IFeatureCatalog, IFeatureProvider, IEntitlementResolver)` ✓; `ServiceCollectionExtensions.AddSunfishFeatureManagement()` ✓; `FeatureSpec.DefaultValue` is `string?` ✓.

- **§A0.3 Structural-citation correctness**: (a) `FeatureKey.Value` — `FeatureKey` is a `readonly record struct(string Value)` per `packages/foundation-featuremanagement/FeatureKey.cs`; `.Value` is the positional primary constructor property — verified correct. (b) `FeatureEvaluationContext.TenantId` is `TenantId?` (nullable) per `packages/foundation-featuremanagement/FeatureEvaluationContext.cs` — used correctly in the `if (context.TenantId is not { } tenantId)` guard. (c) `IAtlasProjector.ProjectAsync(TenantId, StandingOrderScope?, CancellationToken)` — signature taken verbatim from ADR 0065 §5 decision text; not yet in code (negative-existence above). Council must verify this signature matches the W#42 Phase 1 implementation if it has landed. (d) `AtlasView.SettingsByPath` is `IReadOnlyDictionary<string, AtlasSettingSnapshot>` per ADR 0065 §5 — path key is the dotted path string; dictionary access by `path` string is structurally correct. (e) `AtlasSettingSnapshot.CurrentValue` is `JsonNode?` per ADR 0065 §5 — `JsonNode.ToString()` on a JSON boolean produces the canonical `"true"`/`"false"` lowercase string; `FeatureValue.Raw` is consumed by `AsBoolean()` which calls `bool.Parse(Raw)` (verified in `FeatureValue.cs`) — round-trip is correct for boolean toggles. (f) `new FeatureValue { Raw = raw }` object-initializer form — verified against `FeatureValue.cs` which uses `public required string Raw { get; init; }` — the `required` modifier means the object-initializer form is mandatory, not optional; `new FeatureValue { Raw = raw }` is the correct and only construction form.

The §A0 self-audit is *necessary but not sufficient*. Council's primary structural-citation risk is item (c) — if W#42 Phase 1 has landed concurrently and the actual `IAtlasProjector` signature differs from ADR 0065 §5, this amendment's API shape needs a mechanical fix. Council must spot-check this direction explicitly.

---

### A1.9 References

- ADR 0065 — Wayfinder System + Standing Order Contract (W#42; `IAtlasProjector`, `StandingOrderScope`, `AtlasView`, `AtlasSettingSnapshot`; prerequisite).
- ADR 0028 — CRDT engine selection (convergence semantics inherited by Standing Orders).
- ADR 0049 — Audit trail substrate (`IAuditTrail`; Standing Orders emit via this substrate).
- W#34 discovery — `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` §6.1 (5th-concept identification).
- W#43 intake — `icm/00_intake/output/2026-05-01_adr-0009-amendment-fifth-concept-wayfinder-consumer-intake.md`.
- ADR 0065 council file — `icm/07_review/output/adr-audits/0065-council-review-2026-05-01.md` §F4 (substrate-vs-consumer scoping discipline that motivated this separate workstream).
