---
id: 75
title: ExtensionFields Feature-Evaluation Hook
status: Accepted
date: 2026-05-04
tier: foundation
pipeline_variant: sunfish-api-change

concern:
  - configuration
  - dev-experience
  - audit

enables:
  - feature-gated-extension-fields
  - operator-runtime-controllable-field-gating
  - sequester-on-gate-off-policy
  - audited-extension-field-materialization

composes:
  - 9    # FeatureManagement (with Amendment A1 Wayfinder consumer)
  - 49   # Audit substrate

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

amendments: []
---

# ADR 0075 — ExtensionFields Feature-Evaluation Hook

**Status:** Accepted
**Date:** 2026-05-04
**Resolves:** ADR 0009 follow-up #5 (originally proposed 2026-04-19) — "Feature evaluation hook into `Sunfish.Foundation.Catalog.ExtensionFields` — when an extension field is gated by a feature key, evaluate before materializing." Workstream W#44.

---

## §A0 — Self-audit limitation block (per ADR 0069 D2 + D3)

`tier: foundation` ⇒ full ADR 0069 D1+D2+D3 discipline applies. Cohort batting average at draft time: high single-to-double-digit count of substrate amendments needed council-sourced fixes (zero counter-examples in the 2026-04/05 cohort per ADR 0069 §Cohort batting average); the §A0 self-audit catch rate for structural-citation failures is empirically below 50% (per the same source). **§A0 is necessary-but-not-sufficient; council remains canonical defense.**

### §A0.1 Negative-existence (introduced by this ADR's Phase 1 build — absent on origin/main 2026-05-04)

- `Sunfish.Foundation.Catalog.ExtensionFields.FeatureGateOffPolicy` (new enum: `Hide` / `Sequester` / `Redact`).
- `ExtensionFieldSpec.FeatureKey` and `ExtensionFieldSpec.FeatureGateOffPolicy` (two new optional record parameters appended to the existing 8-parameter record → 10 parameters total).
- `Sunfish.Kernel.Audit.AuditEventType.{ExtensionFieldGated, ExtensionFieldFiltered, ExtensionFieldSequestered, ExtensionFieldRedacted, ExtensionFieldGateEvaluationFailed}` static-readonly fields (5 constants; `ExtensionFieldGateEvaluationFailed` records fail-closed evaluator errors per §Gate-evaluator failure semantics below).
- New `IExtensionFieldCatalog.GetFieldsAsync(Type, FeatureEvaluationContext, CancellationToken)` overload (additive; does not modify the existing 1-arg `GetFields`).
- New `MaterializedExtensionField` record + `GateState` enum (returned by the new overload).
- New `Sunfish.Foundation.Catalog.ExtensionFields.ExtensionFieldRedactionDeniedException` (parallel to `Sunfish.Foundation.Recovery.Crypto.FieldDecryptionDeniedException`; thrown by the catalog's `Redact` path when the capability check fails). Constructor: `(string action, string entityTypeFullName, string fieldKey, string reason)`.
- New private static-readonly `CapabilityAction` constant on the concrete `ExtensionFieldCatalog`: `private static readonly CapabilityAction RedactExtensionFieldAction = new("redact-extension-field");`. Per the `CapabilityAction(string Name)` `readonly record struct` ctor pattern verified in §A0.2 below — NOT a new static member added to the `CapabilityAction` type itself (the type's own static-readonly slots `Read`/`Write`/`Delete`/`Delegate`/`Sign` stay closed; consumer-defined verbs are constructed at the consumer per the type's xml-doc explicitly authorising arbitrary domain-specific action strings, e.g. `sign_inspection`).

### §A0.2 Positive-existence (cited as existing — verified on origin/main 2026-05-04)

- `Sunfish.Foundation.Catalog.ExtensionFields.ExtensionFieldSpec` — `packages/foundation-catalog/ExtensionFields/ExtensionFieldSpec.cs`. Positional `sealed record` with 8 parameters: `(ExtensionFieldKey Key, Type ValueType, ExtensionFieldScope Scope, ExtensionStorage Storage, bool IsRequired, bool IsSearchable, string? DisplayName, string? Description)`.
- `IExtensionFieldCatalog` (3 members: `Register`, `GetFields`, `TryGetField`) and concrete `ExtensionFieldCatalog` — same package.
- `Sunfish.Foundation.Extensibility.ExtensionFieldKey` — `readonly record struct(string Value)` with `Of(string)` factory.
- `Sunfish.Foundation.FeatureManagement.{IFeatureEvaluator, FeatureKey, FeatureValue, FeatureEvaluationContext}` — `packages/foundation-featuremanagement/`. `FeatureKey` is `readonly record struct(string Value)`. `FeatureEvaluationContext` carries `TenantId?` plus edition/bundles/modules/user/environment/attributes.
- `Sunfish.Kernel.Audit.{IAuditTrail, AuditEventType, AuditRecord}` — `packages/kernel-audit/`. `IAuditTrail.AppendAsync(AuditRecord, CancellationToken)` is the issuance signature. `AuditEventType` is `readonly record struct(string Value)` (NOT enum); new event types are `public static readonly AuditEventType` fields.
- `Sunfish.Foundation.Migration.{ISequestrationStore, SequesteredRecord, SequestrationFlagKind}` — `packages/foundation-migration/{Services,Models}/`. `SequesterAsync(string nodeId, string recordId, SequestrationFlagKind flag, CancellationToken)` is the canonical signature; field-level convention encodes `recordId = "{recordId}#{fieldName}"`.
- `SequestrationFlagKind` enum values: `FormFactorFilteredOut`, `StorageBudgetExceeded`, `PlaintextSequestered`, `CiphertextSequestered`, `FormFactorQuorumIneligible`, `FeatureGateOff` — `FeatureGateOff` added by ADR 0028-A11 (PR #512) per §Open question 2 resolution.
- `Sunfish.Foundation.Capabilities.{ICapabilityGraph, CapabilityAction, Resource, Principal}` — `packages/foundation/Capabilities/`. `ICapabilityGraph.QueryAsync(PrincipalId subject, Resource resource, CapabilityAction action, DateTimeOffset asOf, CancellationToken ct)` returning `ValueTask<bool>` is the canonical authority-check signature consumers see (NOT `HasCapability` — that name belongs to the internal static `CapabilityClosure.HasCapability(...)` graph-walk used inside `InMemoryCapabilityGraph`). `CapabilityAction` is `readonly record struct(string Name)` with a public ctor accepting any string; the type's xml-doc explicitly authorises consumer-defined domain-specific actions (precedent verb `sign_inspection`). `Resource` is `readonly record struct(string Id)`.
- `Sunfish.Foundation.Crypto.{PrincipalId, IOperationSigner}` — `packages/foundation/Crypto/`. `PrincipalId` is the 32-byte Ed25519 public-key wrapper used as the universal subject identifier in `ICapabilityGraph` and as `SignedOperation<T>.IssuerId`. `IOperationSigner` exposes `PrincipalId IssuerId { get; }` — the canonical "current signing principal" surface available to any audit-emitting foundation component, and is already required by this ADR for audit-payload signing per §A0.3 / Council pressure-test point #4.
- `Sunfish.Foundation.Recovery.Crypto.FieldDecryptionDeniedException` — `packages/foundation-recovery/Crypto/FieldDecryptionDeniedException.cs`. `sealed class : Exception` with constructor `(string capabilityId, string reason)` and read-only properties `CapabilityId` + `Reason`. Used by `IFieldDecryptor.DecryptAsync` per ADR 0046-A2/A3. This ADR uses it as the **shape precedent** for the new `ExtensionFieldRedactionDeniedException` (which carries action + entity-type-fullname + field-key + reason, since the catalog gate has no per-call `capabilityId` — it constructs an authority query against the graph rather than presenting a pre-issued capability proof).

### §A0.3 Structural-citation correctness (5-of-5 prior failure rate ⇒ paranoid)

- `ExtensionFieldSpec` is a **positional sealed record**; appending two parameters with default values preserves all existing call sites (positional-with-fewer-than-8 → still works via defaults; named-arguments → already addressed by name; positional-with-all-8 → still works since new ones are appended).
- `IExtensionFieldCatalog.GetFields(Type)` is **synchronous** and returns `IReadOnlyList<ExtensionFieldSpec>` directly — no context, no token. This ADR does **not** modify it; the new gating overload is a sibling `GetFieldsAsync(...)` returning `ValueTask<IReadOnlyList<MaterializedExtensionField>>`.
- The catalog csproj currently `ProjectReference`s only `Sunfish.Foundation`. This ADR adds `ProjectReference`s to `Sunfish.Foundation.FeatureManagement`, `Sunfish.Kernel.Audit`, and `Sunfish.Foundation.Migration`. Runtime dependencies are nullable (lazy DI); compile-time dependencies are unconditional.
- `AuditRecord` requires `(Guid AuditId, TenantId, AuditEventType, DateTimeOffset OccurredAt, SignedOperation<AuditPayload> Payload, IReadOnlyList<AttestingSignature> AttestingSignatures, int FormatVersion = 0)` — the catalog package will need an `IOperationSigner` and access to `Sunfish.Foundation.Crypto.SignedOperation<T>`. `Sunfish.Foundation.Crypto.SignedOperation<T>` lives in the `Sunfish.Foundation` package (sub-namespace, not a separate package); the catalog's existing `ProjectReference` to `Sunfish.Foundation` already provides access. Only the `IOperationSigner` injection is new — that requires a DI registration, not a project-graph change. **Council pressure-test point #4 below.**
- The audit emission factory `ExtensionFieldGateAuditPayloads` is parallel to the existing `MigrationAuditPayloads` (`packages/foundation-migration/Audit/MigrationAuditPayloads.cs`) — proven pattern.
- The ADR 0009 §Resolution-order chain (catalog → provider → entitlements → catalog default → throw) is preserved verbatim. This ADR adds a **separate** evaluation site at the catalog boundary — it does not modify `DefaultFeatureEvaluator`.
- `ICapabilityGraph` consumer-facing authority check is `QueryAsync(PrincipalId, Resource, CapabilityAction, DateTimeOffset, CancellationToken) → ValueTask<bool>`. The Redact gate calls `await _capabilityGraph.QueryAsync(_signer.IssuerId, redactResource, RedactExtensionFieldAction, _clock.UtcNow, ct)` — `IOperationSigner.IssuerId` is the only canonical "current principal" surface available at a foundation-tier component (no `IUserContext` exists; `FeatureEvaluationContext.UserId` is `string?`, not a `PrincipalId`). The `redactResource` is `new Resource($"extension-field#{entityType.FullName}#{spec.Key.Value}")` — same shape as the §Sequester composition's catalog-level sentinel `recordId` so the two sequestration paths address the same conceptual resource.
- The catalog's csproj must add `ProjectReference` to `Sunfish.Foundation` (accessible via the direct `ProjectReference` to `Sunfish.Foundation` — where `Sunfish.Foundation.Capabilities` and `Sunfish.Foundation.Crypto` are sub-namespaces of the foundation package — NOT transitively via `Sunfish.Kernel.Audit`). Both namespaces live inside the `Sunfish.Foundation` package — no new csproj `ProjectReference` is needed beyond what §A0.3 already records.

### §A0.4 Cross-ADR claims (guilty-until-proven-innocent per ADR 0069 D3)

- ADR 0009 Amendment A1 (W#43; merged 2026-05-04 as PR #486) introduces `WayfinderFeatureProvider : IFeatureProvider`, canonical Standing Order path `features.{key}`. **Verified** by reading `docs/adrs/0009-foundation-featuremanagement.md` Amendment A1.
- ADR 0049 establishes `IAuditTrail` as the canonical kernel-tier audit substrate. **Verified.**
- ADR 0028-A5.4 / A8.3 specifies the sequestration partition contract. **Verified** via the xml-doc on `ISequestrationStore`.
- ADR 0069 D1 mandates pre-merge council canonical for substrate-tier ADRs. `tier: foundation` qualifies. **Verified.**

### §A0.5 Council pressure-test points

1. Parallel-overload footgun — does the ungated `GetFields(Type)` silently bypass gating in real call sites?
2. `Sequester` flag provenance: `SequestrationFlagKind.PlaintextSequestered` was semantically wrong (form-factor-driven, not operator-gate-driven). **Resolved by ADR 0028-A11 (PR #512):** `FeatureGateOff` added with explicit xml-doc citing distinct provenance per ADR 0049 audit-by-construction.
3. Lazy-DI optionality — null-evaluator silently bypasses gating; risk in incomplete-DI deployments.
4. Audit envelope constructability — does catalog have access to `SignedOperation<T>` + an `IOperationSigner` via the new ProjectReference graph?
5. `Redact` is a one-way door — what prevents accidental issuance? Capability proof + explicit operator confirmation gate?
6. Multi-tenancy + singleton catalog — gating decision is per-evaluation, but is there any tenant-affecting cache?
7. ADR 0028-A5 form-factor migration composition — gates compose cleanly with form-factor sequestration?

---

## Context

ADR 0009 (Foundation.FeatureManagement) declared six follow-ups at its 2026-04-19 acceptance. Five are landed or scheduled; **follow-up #5 — the feature-evaluation hook into `Sunfish.Foundation.Catalog.ExtensionFields` — is the last open promise** from that ADR. It was deliberately held until:

1. **ADR 0065 (W#42, Wayfinder System + Standing Order Contract)** — operator-issued configuration substrate.
2. **ADR 0009 Amendment A1 (W#43, `WayfinderFeatureProvider`; merged 2026-05-04 as PR #486)** — wires Wayfinder Standing Orders into the `IFeatureProvider` chain so feature evaluation is operator-runtime-controllable, not just startup-bound.

Pre-W#42/W#43 a feature-gate hook was implementable but startup-bound — operators could not toggle a field gate without a host restart. Post-W#42/W#43 the hook becomes runtime-controllable: an operator issues a Standing Order flipping the feature OFF, the next call to `GetFieldsAsync(Type, ctx)` sees the gate flip, the configured `FeatureGateOffPolicy` runs, the audit trail records the decision, and (for `Sequester` policy) data is preserved per ADR 0028's sequestration substrate.

The hook closes the loop between four parallel substrates:

- **`Sunfish.Foundation.Catalog.ExtensionFields`** — declares which fields exist on which entities (ADR 0005 catalog-required rule).
- **`Sunfish.Foundation.FeatureManagement`** — declares which features are enabled for whom (ADR 0009 evaluator chain + Amendment A1 operator-runtime layer).
- **`Sunfish.Kernel.Audit`** — records every consequential decision (ADR 0049 audit-by-construction).
- **`Sunfish.Foundation.Migration`** — preserves data when the active surface contracts (ADR 0028-A5/A8 + W#35's `ISequestrationStore`).

The decision is small in surface area but large in trust posture: extension-field materialization is the boundary at which user-entered data either appears or disappears in the application. Failure modes this ADR exists to prevent: UI surfaces a gated-off field; persistence writes to a gated-off column; an operator's gate-off action destroys data without an audit record.

---

## Decision drivers

- **Operator-runtime control.** Post-Amendment-A1, feature gates can be flipped by an operator via the Atlas UI without a restart. The catalog hook MUST consult `IFeatureEvaluator` at materialization time (per call), not at registration time (startup-bound). Static gate evaluation (cache the gate at `Register(...)` time) would break operator-runtime control and reduce ADR 0009 Amendment A1 to a documentation no-op for extension fields.
- **Audit-by-construction (ADR 0049).** Every gating decision that affects user-visible data is consequential and MUST emit an `AuditRecord`. Filtering a field from a UI render without auditing produces the "why is field X invisible?" question the audit substrate exists to answer.
- **Data preservation when gates flip OFF (ADR 0028-A5 / W#35 sequestration).** Operators flip features OFF for many reasons: regulatory pressure, edition downgrade, beta-program termination, security incident. **The default policy MUST be non-destructive**: hiding the field while preserving the underlying data is the safest reversible action. A separate `Sequester` policy composes with W#35's `ISequestrationStore` for cases where the data must be preserved AND tracked in the sequestration partition (e.g., for capability-graph reasoning, regulated-tenant compliance reports). A `Redact` policy is included for last-resort cases (e.g., a field-level legal hold requires destruction); it is destructive and MUST require explicit operator confirmation.
- **No breaking change to existing call sites.** The current `IExtensionFieldCatalog.GetFields(Type)` synchronous 1-arg signature is consumed by persistence adapters, UI generators, and validators across the codebase. A breaking change would require simultaneous updates across all of these. The hook is delivered as a **new overload** (async, takes `FeatureEvaluationContext`); the old overload continues to return all registered specs without gating. Migration is opt-in, at each call site, when the call site has access to a `FeatureEvaluationContext`.
- **No mandatory new transitive dependency.** Catalog's csproj currently `ProjectReference`s only `Sunfish.Foundation`. Adding `Sunfish.Foundation.FeatureManagement` is acceptable (the FeatureManagement csproj only depends on `Sunfish.Foundation` itself) but the runtime dependency must be optional — `IFeatureEvaluator` is constructor-injected as nullable; null-evaluator → no gating, regardless of `FeatureKey` presence on the spec. This preserves backward compatibility for hosts that do not register a feature evaluator.
- **Trust impact is tiered.** `Hide` (default) keeps data in storage; `Sequester` preserves auditability and reversibility via W#35; `Redact` is destructive and requires explicit operator capability + audit. The default is the safest; the trust-impact-increasing options must be explicit opt-ins per spec.

---

## Considered options

### Option A — Filter at catalog `GetFieldsAsync(Type, ctx)` [RECOMMENDED]

Add an async overload to `IExtensionFieldCatalog` that takes a `FeatureEvaluationContext`, evaluates each registered spec's `FeatureKey?` against `IFeatureEvaluator`, applies the configured `FeatureGateOffPolicy`, emits audit records, and (for `Sequester`) calls `ISequestrationStore.SequesterAsync(...)`. The synchronous 1-arg `GetFields(Type)` is preserved unchanged for backward compatibility.

- **Pro:** Single decision point — the catalog is THE place that declares "these are the fields on this entity"; gating here means every consumer (persistence, UI, validation) inherits gating without per-consumer wiring.
- **Pro:** Audit emission is co-located — ADR 0049's audit-by-construction pattern is preserved.
- **Pro:** Backward-compatible — new overload + old overload preserved → opt-in migration.
- **Pro:** Composes cleanly with Amendment A1 — calling `IFeatureEvaluator` inherits the entire ADR 0009 chain (Wayfinder provider, entitlements, defaults).
- **Con:** N feature evaluations per call (one per gated spec); caching is the consumer's responsibility.
- **Con:** Catalog gains nullable runtime dependencies on FeatureManagement / Audit / Migration substrates.

**Verdict:** Recommended — simplest place that produces the right outcome.

### Option B — Filter at materialization in repository layer [REJECTED]

Each persistence adapter (EFCore, in-memory, future Anchor SQLite) consults `IFeatureEvaluator` directly when materializing entities.

- **Pro:** repositories already carry tenant context; storage-boundary location.
- **Con:** every adapter implements gating independently → multiplied audit-emission code, parity-test burden, drift risk (the ADR 0028 cohort lesson).
- **Con:** UI generators don't go through the repository — they would still see gated-OFF fields unless they too consult the evaluator. Defeats "catalog as single source of truth."

**Verdict:** Rejected — surface-area multiplier is the wrong shape.

### Option C — Evaluate per-record at read time [REJECTED]

Catalog returns all specs unchanged; the materializer filters per record using the read-time `FeatureEvaluationContext`.

- **Pro:** highest operator-fidelity (feature flips during a request take effect mid-request).
- **Con:** N records × M gated fields × `EvaluateAsync` per render — unbounded perf cost; defeats caching at every layer; unfeasible for list views, bulk export, IRS tax export.
- **Con:** the fidelity gain is illusory — ADR 0065 Standing Orders are CRDT-replicated with eventual consistency; sub-request consistency is not the design point.

**Verdict:** Rejected — fidelity gain is not worth the perf cost.

### Option D — Push-not-pull (event-driven invalidation) [REJECTED]

The feature evaluator pushes invalidation signals to the catalog materializer when a gate flips; the catalog reacts to push events rather than polling `IFeatureEvaluator` on each call.

- **Pro:** eliminates per-call evaluation overhead; materializer always has a current snapshot.
- **Con:** introduces bi-directional dependency between `foundation-featuremanagement` and `foundation-catalog`. Currently the dependency flows one way: the catalog consumes `IFeatureEvaluator`. Push-based invalidation requires `foundation-featuremanagement` to know about (and call back into) the catalog, creating a circular package dependency.
- **Con:** the pull model (Option A, current choice) preserves the single-direction dependency and tolerates brief staleness within the ADR 0065 §F9 P95 200ms replication budget — a constraint the system is already designed around.

**Verdict:** Rejected — circular dependency defeats the layering contract; the pull model is sufficient given the ADR 0065 replication-latency budget.

---

## Decision

**Adopt Option A.**

### Initial contract surface

#### `ExtensionFieldSpec` (extended)

`ExtensionFieldSpec` gains two new optional positional record parameters appended at the end:

```csharp
namespace Sunfish.Foundation.Catalog.ExtensionFields;

public sealed record ExtensionFieldSpec(
    ExtensionFieldKey Key,
    Type ValueType,
    ExtensionFieldScope Scope,
    ExtensionStorage Storage,
    bool IsRequired = false,
    bool IsSearchable = false,
    string? DisplayName = null,
    string? Description = null,
    // NEW — added by ADR 0075
    Sunfish.Foundation.FeatureManagement.FeatureKey? FeatureKey = null,
    FeatureGateOffPolicy FeatureGateOffPolicy = FeatureGateOffPolicy.Hide);
```

`FeatureKey` defaults to `null` → field is ungated; behavior preserved. `FeatureGateOffPolicy` defaults to `Hide` → safest behavior when a gate is set but the policy is omitted.

#### `FeatureGateOffPolicy` (new enum)

```csharp
namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Policy applied when an <see cref="ExtensionFieldSpec.FeatureKey"/> evaluates
/// to OFF. Default is <see cref="Hide"/>; <see cref="Sequester"/> composes
/// the W#35 / ADR 0028-A5.4 sequestration partition; <see cref="Redact"/> is
/// destructive and requires explicit operator capability per ADR 0046.
/// </summary>
public enum FeatureGateOffPolicy
{
    /// <summary>Default. Field is excluded from materialization; underlying data is preserved in storage but not surfaced. Reversible: when the gate flips ON, the field reappears with its prior value.</summary>
    Hide = 0,

    /// <summary>Field is excluded from materialization AND its underlying data is registered with <c>Sunfish.Foundation.Migration.ISequestrationStore</c> (W#35). Composes ADR 0028-A5.4. Reversible.</summary>
    Sequester = 1,

    /// <summary>Field is excluded from materialization AND its underlying data is destroyed (tombstoned). Requires an explicit operator capability and emits a destructive audit record. NOT reversible — the data is gone.</summary>
    Redact = 2,
}
```

#### `IExtensionFieldCatalog` (new overload)

A new async overload that performs gate evaluation. The synchronous `GetFields(Type)` is preserved; documentation explicitly directs new call sites to the async overload.

```csharp
namespace Sunfish.Foundation.Catalog.ExtensionFields;

public interface IExtensionFieldCatalog
{
    // Existing — unchanged.
    void Register(Type entityType, ExtensionFieldSpec spec);
    IReadOnlyList<ExtensionFieldSpec> GetFields(Type entityType);
    bool TryGetField(Type entityType, ExtensionFieldKey key, out ExtensionFieldSpec? spec);

    // NEW — added by ADR 0075.
    /// <summary>
    /// Returns the materialized field set in the given context. Each spec
    /// with a non-null <c>FeatureKey</c> is evaluated against
    /// <c>IFeatureEvaluator</c>; gated-OFF specs are filtered / sequestered
    /// / redacted per <c>FeatureGateOffPolicy</c>; every decision emits an
    /// audit record. Null-evaluator host ⇒ gating is skipped (all specs
    /// returned as <c>GateState.Ungated</c>).
    /// </summary>
    ValueTask<IReadOnlyList<MaterializedExtensionField>> GetFieldsAsync(
        Type entityType,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default);
}
```

#### Synchronous overload deprecation note

The synchronous `GetFields(Type)` overload is marked `[Obsolete("Use GetFieldsAsync which supports feature-gate evaluation. GetFields does not apply FeatureGateOffPolicy.")]` as of Phase 1 build. The Roslyn analyzer pattern (ADR 0065 Phase 3b, `SUNFISH_WAYFINDER001` precedent) applies: callers are warned at compile time when they use the ungated synchronous overload. This is an informational `[Obsolete]` (not an error); call sites that genuinely do not have a `FeatureEvaluationContext` (e.g., startup-time metadata builders, schema-generation tooling) are expected to suppress the warning with a brief explanatory comment. The purpose of the warning is to prompt call-site authors to consider whether feature-gate semantics are needed, not to force a migration.

#### `MaterializedExtensionField` (new record)

```csharp
namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Result of <see cref="IExtensionFieldCatalog.GetFieldsAsync"/>: a registered
/// spec plus the gate-evaluation outcome and the policy applied. Consumers
/// switch on <see cref="GateState"/> to decide whether to surface the field
/// (Ungated or GatedOn), hide it (Hidden), or render the sequestered /
/// redacted indicator (Sequestered, Redacted).
/// </summary>
public sealed record MaterializedExtensionField(
    ExtensionFieldSpec Spec,
    GateState GateState);

public enum GateState
{
    /// <summary>Spec has no FeatureKey; field is unconditionally visible.</summary>
    Ungated = 0,

    /// <summary>Spec has a FeatureKey and the gate evaluated ON.</summary>
    GatedOn = 1,

    /// <summary>Spec is gated OFF and policy is Hide; field is excluded from materialization.</summary>
    Hidden = 2,

    /// <summary>Spec is gated OFF and policy is Sequester; field is excluded but tracked in ISequestrationStore.</summary>
    Sequestered = 3,

    /// <summary>Spec is gated OFF and policy is Redact; underlying data has been tombstoned.</summary>
    Redacted = 4,
}
```

For policies `Hidden`, `Sequestered`, `Redacted`, the field is **excluded** from the returned list when the consumer wants the "what's visible right now?" projection; the policy semantics are captured by the audit emission (next section). However, callers that need to render UI affordances for sequestered/redacted state (e.g., "this field is currently disabled by your subscription tier") may opt in to receive those entries via an alternative `GetFieldsWithGatesAsync(...)` query — see §Open questions item 5.

#### Audit emission

Five new `AuditEventType` static-readonly fields on `Sunfish.Kernel.Audit.AuditEventType`:

```csharp
// In packages/kernel-audit/AuditEventType.cs (new section, ADR 0075).
public static readonly AuditEventType ExtensionFieldGated = new("ExtensionFieldGated");
public static readonly AuditEventType ExtensionFieldFiltered = new("ExtensionFieldFiltered");
public static readonly AuditEventType ExtensionFieldSequestered = new("ExtensionFieldSequestered");
public static readonly AuditEventType ExtensionFieldRedacted = new("ExtensionFieldRedacted");
public static readonly AuditEventType ExtensionFieldGateEvaluationFailed = new("ExtensionFieldGateEvaluationFailed");
```

Semantics:

| Event | Emitted when |
|---|---|
| `ExtensionFieldGated` | Spec has a `FeatureKey`; gate evaluated to ON; field appears in the materialized list. (Lower frequency: emitted at gate-evaluation time, NOT per call — see §Open questions item 4 for sampling/aggregation policy.) |
| `ExtensionFieldFiltered` | Spec has a `FeatureKey`; gate evaluated to OFF; policy is `Hide`; field excluded from list. |
| `ExtensionFieldSequestered` | Spec has a `FeatureKey`; gate evaluated to OFF; policy is `Sequester`; field excluded AND `ISequestrationStore.SequesterAsync(...)` was called. |
| `ExtensionFieldRedacted` | Spec has a `FeatureKey`; gate evaluated to OFF; policy is `Redact`; field excluded AND underlying data was tombstoned. |
| `ExtensionFieldGateEvaluationFailed` | `IFeatureEvaluator.IsEnabledAsync` threw; gate treated as OFF (fail-closed); exception message and `FeatureKey` captured in payload. |

Audit payloads use a new factory helper `ExtensionFieldGateAuditPayloads` (parallel to `MigrationAuditPayloads` in `packages/foundation-migration/Audit/`) that constructs the `AuditPayload` + `SignedOperation<AuditPayload>` envelope from the gating outcome. The factory is in the catalog package; signing is delegated via constructor-injected `IOperationSigner` (already a foundation-tier abstraction).

#### `Sequester` composition with W#35

When `FeatureGateOffPolicy.Sequester` applies, the catalog implementation calls:

```csharp
await _sequestrationStore.SequesterAsync(
    nodeId: _nodeIdProvider.LocalNodeId,
    recordId: $"{recordIdProvider.Resolve(entityType, ctx)}#{spec.Key.Value}",
    flag: SequestrationFlagKind.FeatureGateOff,  // ADR 0028-A11 — audit-by-construction requires distinct provenance
    cancellationToken).ConfigureAwait(false);
```

The `SequesterAsync` call requires a record-level `recordId`. For catalog-level "this field is gated for this tenant" semantics — which doesn't have a specific record id — the implementation registers a **catalog-level sentinel** record with `recordId = $"catalog-gate#{entityType.FullName}#{spec.Key.Value}"`. This is a known wart; §Open questions item 8 tracks a cleaner contract that admits catalog-level sequestration.

#### Lazy-DI optionality

The concrete `ExtensionFieldCatalog` implementation accepts:

```csharp
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;

public ExtensionFieldCatalog(
    IFeatureEvaluator? featureEvaluator = null,
    IAuditTrail? auditTrail = null,
    ISequestrationStore? sequestrationStore = null,
    ICapabilityGraph? capabilityGraph = null,
    IOperationSigner? signer = null,
    INodeIdProvider? nodeIdProvider = null,
    IRecordIdResolver? recordIdResolver = null,
    TimeProvider? clock = null)
```

When `featureEvaluator` is `null`: gating is skipped; `GetFieldsAsync(Type, ctx)` returns every registered spec with `GateState.Ungated`. `auditTrail`, `sequestrationStore`, and `capabilityGraph` are independently nullable. Substrate-required throws:

- **`Sequester` policy with a null `sequestrationStore`** ⇒ `InvalidOperationException` at first encounter ("Sequester policy requires `ISequestrationStore` registration; see ADR 0075 §Lazy-DI optionality").
- **`Redact` policy with a null `capabilityGraph` OR null `signer`** ⇒ `InvalidOperationException` at first encounter ("Redact policy requires `ICapabilityGraph` + `IOperationSigner` registration"). The Redact path cannot fail-open: a host that has registered a `Redact` policy but not the capability substrate is mis-configured, and the catalog is the canonical place to reject the misconfiguration.
- **Audit emission with a null `auditTrail`** ⇒ silent skip. Audit-by-construction degrades gracefully when audit is not wired (matches the existing `TenantKeyProviderFieldDecryptor` pattern in `Sunfish.Foundation.Recovery.Crypto`).

This preserves the no-mandatory-new-dependency posture from §Decision drivers — hosts that do not register feature management continue to work unchanged. Hosts that register `Redact`-policy specs MUST also register the capability substrate; the substrate-required throws catch this at first call rather than silently fail-open.

#### `Redact` capability gate (full spec)

The `Redact` path is destructive and one-way. Before invoking the tombstone path, the catalog MUST consult the capability graph for explicit operator authority. The check uses the existing `ICapabilityGraph` consumer surface (verified in §A0.2): `QueryAsync(PrincipalId subject, Resource resource, CapabilityAction action, DateTimeOffset asOf, CancellationToken ct)` returning `ValueTask<bool>`.

```csharp
namespace Sunfish.Foundation.Catalog.ExtensionFields;

internal sealed partial class ExtensionFieldCatalog : IExtensionFieldCatalog
{
    // Action verb — local to this consumer. Constructed via the public
    // CapabilityAction(string Name) ctor, NOT a static field added to the
    // CapabilityAction type itself (the type's authoritative static slots
    // are Read/Write/Delete/Delegate/Sign and stay closed). The verb name
    // mirrors the ADR 0046 convention for domain-specific actions
    // (e.g. "sign_inspection").
    private static readonly CapabilityAction RedactExtensionFieldAction =
        new("redact-extension-field");

    private async ValueTask AssertRedactAuthorisedAsync(
        Type entityType,
        ExtensionFieldSpec spec,
        CancellationToken ct)
    {
        // Substrate-required for Redact policy — see Lazy-DI optionality block.
        if (_capabilityGraph is null || _signer is null)
        {
            throw new InvalidOperationException(
                "Redact policy requires ICapabilityGraph + IOperationSigner registration; " +
                "see ADR 0075 §Lazy-DI optionality.");
        }

        // The "current acting principal" for a foundation-tier component is
        // IOperationSigner.IssuerId — the same principal used to sign the
        // accompanying audit record. No IUserContext exists at the foundation
        // tier; FeatureEvaluationContext.UserId is a string?, not a PrincipalId,
        // and is therefore not authoritative.
        PrincipalId actor = _signer.IssuerId;

        // Resource shape mirrors the §Sequester composition's catalog-level
        // sentinel so the two sequestration paths address the same conceptual
        // resource.
        var resource = new Resource(
            $"extension-field#{entityType.FullName}#{spec.Key.Value}");

        var asOf = (_clock ?? TimeProvider.System).GetUtcNow();

        bool authorised = await _capabilityGraph
            .QueryAsync(actor, resource, RedactExtensionFieldAction, asOf, ct)
            .ConfigureAwait(false);

        if (!authorised)
        {
            throw new ExtensionFieldRedactionDeniedException(
                action: RedactExtensionFieldAction.Name,
                entityTypeFullName: entityType.FullName ?? entityType.Name,
                fieldKey: spec.Key.Value,
                reason: "capability-graph denied");
        }
    }
}

/// <summary>
/// Thrown when <see cref="IExtensionFieldCatalog.GetFieldsAsync"/> evaluates
/// a spec whose <see cref="FeatureGateOffPolicy"/> is <see cref="FeatureGateOffPolicy.Redact"/>
/// to OFF, but the configured <see cref="ICapabilityGraph"/> denies the
/// <c>redact-extension-field</c> action for the current
/// <see cref="IOperationSigner.IssuerId"/>. Parallel in shape to
/// <see cref="Sunfish.Foundation.Recovery.Crypto.FieldDecryptionDeniedException"/>;
/// every denial emits an <see cref="AuditEventType.ExtensionFieldRedacted"/>
/// audit record (with the denial captured in the payload) before the
/// exception propagates.
/// </summary>
public sealed class ExtensionFieldRedactionDeniedException : Exception
{
    public ExtensionFieldRedactionDeniedException(
        string action,
        string entityTypeFullName,
        string fieldKey,
        string reason)
        : base($"Extension-field redaction denied " +
               $"(action='{action}', entity='{entityTypeFullName}', " +
               $"field='{fieldKey}'): {reason}")
    {
        Action = action;
        EntityTypeFullName = entityTypeFullName;
        FieldKey = fieldKey;
        Reason = reason;
    }

    public string Action { get; }
    public string EntityTypeFullName { get; }
    public string FieldKey { get; }
    public string Reason { get; }
}
```

Three properties of this design:

1. **No new exception type on `Sunfish.Foundation.Capabilities`.** A previous draft cited `CapabilityRequiredException` — that type does NOT exist on `origin/main` (verified 2026-05-04 by `grep -r "CapabilityRequiredException" packages/`). This ADR introduces a domain-specific `ExtensionFieldRedactionDeniedException` whose shape is the existing `FieldDecryptionDeniedException` precedent rather than inventing a generic foundation-tier exception (which would over-reach this ADR's scope; see §Open questions item 9 if a generic mechanism is later wanted across multiple Redact-style consumers).
2. **No new static slot on `CapabilityAction`.** The `CapabilityAction` type's xml-doc explicitly invites consumers to construct domain-specific verbs locally — `RedactExtensionFieldAction` is a private constant on `ExtensionFieldCatalog`, NOT a public addition to `CapabilityAction.{Read,Write,Delete,Delegate,Sign}`. The closed type stays closed; the consumer owns its verbs.
3. **Principal lookup is `IOperationSigner.IssuerId`.** No `IUserContext` exists at the foundation tier (verified by `grep -rn "IUserContext|ICurrentPrincipal|IPrincipalAccessor|ICurrentUser" packages/` returning zero hits). The signer is already injected for audit-payload signing; reusing it as the principal source means the audit record and the capability check attribute the same identity — which is exactly the ADR 0049 audit-by-construction story.

### Substrate / layering notes

- The catalog package gains `ProjectReference` to `Sunfish.Foundation.FeatureManagement` and `Sunfish.Kernel.Audit`. Both are optional at runtime (nullable injection); compile-time, the package depends on both.
- The catalog package gains `ProjectReference` to `Sunfish.Foundation.Migration` for `ISequestrationStore` and `SequestrationFlagKind`. This is a new cross-foundation dependency; is acceptable because all involved packages are foundation-tier and `Sunfish.Foundation.Migration` is the canonical home for sequestration semantics.
- The `WayfinderFeatureProvider` from ADR 0009 Amendment A1 is the canonical operator-runtime provider; this ADR does NOT couple to it directly. Instead, the catalog calls `IFeatureEvaluator`, which (via DI configuration) resolves through the entire ADR 0009 chain — `WayfinderFeatureProvider` is one of several providers in that chain. **The catalog has no knowledge of the operator-runtime layer; it only knows the foundation evaluator surface.** This preserves the decoupling promised in ADR 0009 Amendment A1 §A1.4.

### Gate-evaluator failure semantics

If `IFeatureEvaluator.IsEnabledAsync` throws, the materializer treats the gate as OFF (fail-closed). The failure is recorded via a new `ExtensionFieldGateEvaluationFailed` audit event type (5th constant, parallel to the existing 4; added to §A0.1 negative-existence list and the Phase 2 implementation checklist). Fail-closed behavior prevents feature-gate errors from silently exposing gated fields. The audit record captures the exception message and the spec's `FeatureKey` so operators can diagnose evaluator failures without examining application logs directly.

### Trust impact — operator-flip / GetFieldsAsync race

The operator-flip / `GetFieldsAsync` race is bounded by Standing-Order replication latency (P95 200ms per ADR 0065 §F9). During the race window, stale materializer results may expose or hide fields inconsistently. For `FeatureGateOffPolicy.Redact`, a MUST requirement is added: the materializer MUST require Standing Order quorum confirmation (not just local-projection observation) before applying the Redact partition — local-only projection is insufficient for a security-sensitive hide operation.

---

## Consequences

### Positive

- **Closes the ADR 0009 follow-up #5 promise** that has been outstanding since 2026-04-19.
- **Operator-runtime field gating** without per-consumer wiring; UI generators, persistence adapters, and validators all inherit gating by adopting the new async overload.
- **Audit-by-construction** for every gating decision — answers "why is field X invisible for tenant Y on date Z?" via the audit trail.
- **Reversible default policy** (Hide) is the safest behavior; data is preserved in storage and reappears when the gate flips back ON.
- **Composes with W#35 sequestration** for regulated tenants who require gating decisions to participate in their broader sequestration / form-factor migration story.
- **Composes with ADR 0009 Amendment A1** without coupling to it directly — the catalog only consumes `IFeatureEvaluator`, not `WayfinderFeatureProvider`.

### Negative

- **N feature evaluations per `GetFieldsAsync` call** for an entity with N gated specs. For a list view of M records, the catalog is called once (specs are entity-type-keyed, not record-keyed); the cost is acceptable. For bulk export over heterogeneous entity types, the cost may add up; mitigation is `DefaultFeatureEvaluator`-level caching (not introduced here).
- **Two parallel overloads** (`GetFields` and `GetFieldsAsync`) on the same interface is a known footgun — call sites that use the synchronous overload silently bypass gating. Mitigation: documentation; the §Implementation checklist includes a sweep of existing call sites with a determination of which should migrate.
- **Catalog now indirectly depends on FeatureManagement, Audit, and Migration** at compile time. The constructor's lazy-DI optionality preserves runtime backward compatibility, but the project graph is more entangled.
- **Sequester policy uses `SequestrationFlagKind.FeatureGateOff`** (added by ADR 0028-A11 / PR #512), providing distinct audit-trail provenance from form-factor-driven sequestration (`PlaintextSequestered`). The flag's xml-doc explicitly notes the feature-gate-off semantic is operator-controlled, not form-factor-capability-driven.

### Trust impact / Security & privacy

- **Hide (default)** is non-destructive and the safest. Data is preserved in storage; a UI consumer simply sees a smaller list. Reversibility is intrinsic.
- **Sequester** preserves auditability via W#35's `SequesteredRecord`. The data is tracked in the sequestration partition, the gate-flip-OFF event is auditable, the gate-flip-ON event triggers a release path. Trust boundary: ISequestrationStore and IAuditTrail are now both transitively required by every consumer that adopts the new overload.
- **Redact** is destructive. The data is tombstoned. **Re-flipping the gate ON does NOT resurrect the data** — it surfaces an empty field. This is the correct behavior for legal-hold scenarios, but it is a one-way door. The implementation MUST consult `ICapabilityGraph.QueryAsync(actor, resource, RedactExtensionFieldAction, asOf, ct)` (see §`Redact` capability gate (full spec) above) before invoking the tombstone path; on a `false` reply the catalog throws `ExtensionFieldRedactionDeniedException` (this ADR's new exception, parallel in shape to `FieldDecryptionDeniedException`) and emits an `ExtensionFieldRedacted` audit record carrying the denial in the payload. The actor is `IOperationSigner.IssuerId` — the same signing principal that attests to the audit record, so the capability check and the audit attestation refer to the same identity.
- **Audit storage is append-only (ADR 0049)** — every gating decision is permanent. Tenants who want to limit audit-volume for routine `ExtensionFieldGated` events should adopt the §Open questions item 4 sampling policy; the substrate itself does not filter.

---

## Compatibility plan

### Affected packages

- `packages/foundation-catalog/` — new types (`FeatureGateOffPolicy`, `MaterializedExtensionField`, `GateState`), extended `ExtensionFieldSpec`, new `IExtensionFieldCatalog.GetFieldsAsync(...)` overload, new `ExtensionFieldGateAuditPayloads` factory. New csproj `ProjectReference`s to `Sunfish.Foundation.FeatureManagement`, `Sunfish.Kernel.Audit`, `Sunfish.Foundation.Migration`.
- `packages/kernel-audit/` — five new `AuditEventType` static-readonly fields (including `ExtensionFieldGateEvaluationFailed` per §Gate-evaluator failure semantics).
- `packages/foundation-migration/` — `SequestrationFlagKind.FeatureGateOff` enum value added (ADR 0028-A11 / PR #512; W#35 amendment resolved §Open questions item 2).

### Migration path

1. **No-op for ungated fields.** Existing `ExtensionFieldSpec` registrations without `FeatureKey` are unchanged; behavior is preserved.
2. **No-op for callers using `GetFields(Type)`.** The synchronous overload returns every registered spec, including gated ones, without applying gating. This is the **explicit backward-compatibility seam** — call sites migrate at their own pace.
3. **Opt-in adoption per call site.** Each consumer (UI generator, persistence adapter, validator, kitchen-sink demo) migrates from `GetFields(Type)` to `GetFieldsAsync(Type, ctx)` independently. The migration requires a `FeatureEvaluationContext` at the call site; consumers without tenant context (e.g., startup-time metadata builders) stay on the synchronous overload.
4. **Eventual deprecation of the synchronous overload?** Not in this ADR. The synchronous overload may be useful indefinitely for metadata-only inspection (e.g., schema generation tooling). A future ADR may revisit if the parallel-overload footgun produces real bugs in production.

### Affected accelerators / blocks

- **`accelerators/anchor/`** and **`accelerators/bridge/`** — host applications eventually register `IFeatureEvaluator`, `IAuditTrail`, `ISequestrationStore` and migrate UI generators to the async overload. Not required at first-merge.
- **`blocks-property-*`** — extension-field registrations may opt into `FeatureKey` for edition-tier or beta-program gating. Not required at first-merge.
- **`apps/kitchen-sink`** — demonstrates `GetFieldsAsync(Type, ctx)` with a sample feature-gated extension field. Required at first-merge per Stage 06 deliverable convention.

---

## Implementation checklist

- [ ] **Phase 1 — substrate types.** Add `FeatureGateOffPolicy` enum, `MaterializedExtensionField` record, `GateState` enum to `packages/foundation-catalog/ExtensionFields/`. Extend `ExtensionFieldSpec` record with two new optional positional parameters (`FeatureKey` and `FeatureGateOffPolicy`).
- [ ] **Phase 2 — audit-event-type constants.** Add five new `public static readonly AuditEventType` fields (`ExtensionFieldGated`, `ExtensionFieldFiltered`, `ExtensionFieldSequestered`, `ExtensionFieldRedacted`, `ExtensionFieldGateEvaluationFailed`) to `packages/kernel-audit/AuditEventType.cs` under a new `// ===== ADR 0075 — extension-field feature-gate hook =====` section header. `ExtensionFieldGateEvaluationFailed` records fail-closed evaluator errors per §Gate-evaluator failure semantics.
- [ ] **Phase 3 — audit-payload factory.** Create `packages/foundation-catalog/ExtensionFields/Audit/ExtensionFieldGateAuditPayloads.cs` (parallel to `MigrationAuditPayloads`). Constructs `AuditPayload` + `SignedOperation<AuditPayload>` envelopes for all five event types (including `ExtensionFieldGateEvaluationFailed`).
- [ ] **Phase 4 — catalog wiring.** Add `IExtensionFieldCatalog.GetFieldsAsync(Type, FeatureEvaluationContext, CancellationToken)` overload. Update concrete `ExtensionFieldCatalog` to accept nullable `IFeatureEvaluator`, `IAuditTrail`, `ISequestrationStore`, and signer / node-id / record-id-resolver providers. Implement gate evaluation, policy application, audit emission, and sequestration-store integration.
- [ ] **Phase 5 — DI registration.** Add `ServiceCollectionExtensions.AddExtensionFieldCatalogWithFeatureGating(...)` overload that wires the dependencies. The existing `AddExtensionFieldCatalog()` method is preserved unchanged.
- [ ] **Phase 6 — csproj updates.** Add `ProjectReference` to `Sunfish.Foundation.FeatureManagement`, `Sunfish.Kernel.Audit`, and `Sunfish.Foundation.Migration` in `packages/foundation-catalog/Sunfish.Foundation.Catalog.csproj`.
- [ ] **Phase 7 — tests.** Unit tests for: (a) ungated spec returns `GateState.Ungated` regardless of evaluator state; (b) gated-on spec returns `GateState.GatedOn` and emits `ExtensionFieldGated`; (c) gated-off Hide returns no entry and emits `ExtensionFieldFiltered`; (d) gated-off Sequester emits `ExtensionFieldSequestered` AND calls `ISequestrationStore.SequesterAsync`; (e) gated-off Redact: with an `ICapabilityGraph` stub returning `true` for `(_signer.IssuerId, Resource("extension-field#…"), RedactExtensionFieldAction, …)` ⇒ tombstone path runs and emits `ExtensionFieldRedacted`; with the same stub returning `false` ⇒ throws `ExtensionFieldRedactionDeniedException` AND emits `ExtensionFieldRedacted` with the denial in the payload; with a null `_capabilityGraph` or null `_signer` AND a Redact-policy spec ⇒ throws `InvalidOperationException` per §Lazy-DI optionality; (f) null-evaluator short-circuits to all-ungated; (g) `CapabilityAction` constant reference: assert `RedactExtensionFieldAction.Name == "redact-extension-field"` and that `CapabilityAction.{Read,Write,Delete,Delegate,Sign}` static slots remain unmodified by this ADR.
- [ ] **Phase 8 — Stage 06 deliverables.** Kitchen-sink demo registers a feature-gated extension field; apps/docs page documents the hook; XML docs on every public API; changelog entry citing ADR 0075 + W#44.
- [x] **Phase 9 — pre-merge council.** Per ADR 0069 D1, dispatch four-perspective adversarial council (Outside Observer, Pessimistic Risk Assessor, Pedantic Lawyer, Skeptical Implementer) at `high` effort, Opus 4.7. Pressure-test points enumerated in §A0.5.
- [ ] **Phase 10 — open-question resolution.** Resolve §Open questions items 1, 2, 3, 4 either inline in the ADR (mechanical fixes) or as follow-up amendments before flipping `Status: Accepted`.

---

## Open questions

1. **`IFeatureEvaluator` mandatory vs. optional.** Current: optional (lazy DI). Risk: deployments that should gate but forget to register the evaluator silently bypass gating. Mitigation: `MustGateOption` flag in `AddExtensionFieldCatalog(...)` that throws on `GetFieldsAsync` if no evaluator is registered. Council to decide.
2. **`SequestrationFlagKind` value.** ~~Resolved~~ via ADR 0028-A11 (PR #512): `FeatureGateOff` was added to `SequestrationFlagKind` in `packages/foundation-migration/Models/Enums.cs`, with xml-doc noting distinct provenance from form-factor-driven sequestration. §Sequester composition updated to use `FeatureGateOff`.
3. **`WayfinderFeatureProvider` interaction.** Current: catalog calls `IFeatureEvaluator` (full ADR 0009 chain), not `WayfinderFeatureProvider` directly — preserves the decoupling promised in Amendment A1 §A1.4. A "Wayfinder-only" mode would couple — probably not, but worth pressure-testing.
4. **Audit-volume management for `ExtensionFieldGated`.** High-traffic SaaS at 10 gated fields × 10K renders/day = 100K daily redundant records. Options: (a) emit always, (b) emit on state-change only (requires stateful change-detector), (c) sampling. **Recommended default: (b) state-change only** — emit an audit record only when a gate changes the outcome compared to the previous evaluation for the same `(entityType, FeatureKey, TenantId)` tuple. Regulated tenants who need a full per-evaluation record can opt in to `MustEmitEveryEvaluation = true` via the feature spec. This reduces the common-case audit volume to near-zero while preserving full audit fidelity when operators need it. **Trust impact (regulated bundles):** Regulated bundles should default to `FeatureGateOffPolicy.Sequester` (not `Hide`) — `Hide` is silent and untraceable in the audit trail; `Sequester` preserves the data with an audit record of the gate state. Callers targeting regulated tenants should be warned (via an analyzer diagnostic, following the `SUNFISH_WAYFINDER001` pattern) if they configure `Hide` on a regulated bundle's extension field.
5. **`GetFieldsWithGatesAsync(...)` query.** Some UI consumers want to render "field disabled by your tier" affordances for sequestered/redacted fields. Default: ship `GetFieldsAsync` only in v1; track follow-up Amendment if the kitchen-sink demo motivates it.
6. **`Redact` tombstone shape.** Options: (a) remove field from extension JSON entirely (no tombstone); (b) replace value with `RedactedFieldMarker` sentinel preserving field-name presence; (c) mark entire entity redacted (overkill). Default: option (b). Council to confirm.
7. **Field-level vs. record-level sequestration.** `SequesteredRecord` supports both via `"{recordId}#{fieldName}"`. For feature-gate sequestration the natural granularity is field-level — confirm composition with W#35's existing record-level form-factor sequestration is conflict-free.
8. **`recordId` resolver for catalog-level gating.** Catalog gates per `(entityType, FeatureKey)`; no specific record id at the gate-evaluation site. Current encoding: synthetic `"catalog-gate#{entityType.FullName}#{spec.Key.Value}"`. Should `ISequestrationStore` admit a catalog-level entry shape? (Tracks back to item 2.)
9. **Generic Redact-denial exception.** Current: this ADR introduces `ExtensionFieldRedactionDeniedException` local to the catalog package, parallel in shape to `Sunfish.Foundation.Recovery.Crypto.FieldDecryptionDeniedException`. Cleaner future option if more Redact-style consumers emerge: a generic `Sunfish.Foundation.Capabilities.CapabilityDeniedException(Resource resource, CapabilityAction action, PrincipalId actor, string reason)` type that any consumer can throw. Deferred to keep this ADR's scope tight; the local exception is shape-compatible with a future generic refactor.
10. **`FeatureEvaluationContext.UserId` vs. `PrincipalId`.** Current: catalog uses `IOperationSigner.IssuerId` for the actor in the capability query because `FeatureEvaluationContext.UserId` is `string?`, not a `PrincipalId`, and is therefore not authoritative for capability-graph queries. If a future ADR widens `FeatureEvaluationContext` to carry an authoritative `PrincipalId? Actor` slot, the Redact path could prefer `ctx.Actor ?? _signer.IssuerId` (caller-supplied identity wins, signer is fallback). Track here so it's not lost.

---

## Revisit triggers

- ADR 0009 is superseded → re-author this ADR atop the successor.
- W#35 (`ISequestrationStore`) substrate changes contract → §Open questions items 2, 7, 8 may need re-resolution.
- ADR 0028 form-factor-migration semantics evolve → the composition between feature-gate sequestration and form-factor sequestration may need reconciliation.
- First regulated-tenant operator-gating incident produces a defect → review the choice between Hide / Sequester / Redact defaults.
- Per-render audit volume becomes a scaling problem → revisit §Open questions item 4 sampling policy.
- A future ADR introduces a "schema epoch" boundary that interacts with field-level gating differently from form-factor gating → reconcile the two.

---

## References

### Predecessor and sister ADRs

- [ADR 0005](./0005-type-customization-model.md) — catalog-required rule (origin of `IExtensionFieldCatalog`)
- [ADR 0007](./0007-bundle-manifest-schema.md) — bundle manifests; `featureDefaults` sourced here
- [ADR 0009](./0009-foundation-featuremanagement.md) — parent ADR; this is follow-up #5; Amendment A1 (W#43) is the immediate runtime-control consumer
- [ADR 0028](./0028-crdt-engine-selection.md) — A5.4 / A8.3 sequestration partition substrate
- [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — key-loss recovery scheme; `FieldDecryptionDeniedException` shape precedent for `ExtensionFieldRedactionDeniedException`
- [ADR 0049](./0049-audit-trail-substrate.md) — kernel-tier audit substrate; canonical `IAuditTrail` consumer
- [ADR 0065](./0065-wayfinder-system-and-standing-order-contract.md) — Wayfinder substrate; W#42 prerequisite for operator-runtime control
- [ADR 0069](./0069-adr-authoring-discipline.md) — D1 + D2 + D3 disciplines applied to this ADR

### Roadmap and specifications

- ADR 0009 §Follow-ups item 5 — the long-standing promise this ADR fulfills (drafted 2026-04-19; closed 2026-05-04)
- W#44 ledger row — to be added on PR open
- `icm/_state/handoffs/property-extension-fields-feature-gate-stage06-handoff.md` — Stage 06 hand-off (to be authored after Council acceptance)

### Existing code / substrates

- `packages/foundation-catalog/ExtensionFields/ExtensionFieldSpec.cs`
- `packages/foundation-catalog/ExtensionFields/IExtensionFieldCatalog.cs`
- `packages/foundation-catalog/ExtensionFields/ExtensionFieldCatalog.cs`
- `packages/foundation-featuremanagement/IFeatureEvaluator.cs`
- `packages/foundation-featuremanagement/FeatureKey.cs`
- `packages/foundation-featuremanagement/FeatureEvaluationContext.cs`
- `packages/kernel-audit/IAuditTrail.cs`
- `packages/kernel-audit/AuditEventType.cs`
- `packages/kernel-audit/AuditRecord.cs`
- `packages/foundation-migration/Services/ISequestrationStore.cs`
- `packages/foundation-migration/Models/SequesteredRecord.cs`
- `packages/foundation-migration/Models/Enums.cs` (for `SequestrationFlagKind`)
- `packages/foundation-migration/Audit/MigrationAuditPayloads.cs` (factory pattern reference)
- `packages/foundation/Capabilities/ICapabilityGraph.cs` (consumer-facing `QueryAsync`)
- `packages/foundation/Capabilities/CapabilityAction.cs` (`readonly record struct(string Name)` ctor pattern; closed static slots)
- `packages/foundation/Capabilities/Resource.cs` (`readonly record struct(string Id)`)
- `packages/foundation/Crypto/IOperationSigner.cs` (`PrincipalId IssuerId { get; }` — current-principal surface)
- `packages/foundation/Crypto/PrincipalId.cs` (32-byte Ed25519 public-key wrapper; subject identifier)
- `packages/foundation-recovery/Crypto/FieldDecryptionDeniedException.cs` (shape precedent for `ExtensionFieldRedactionDeniedException`)

### External

- OpenFeature specification — Provider concept (`IFeatureProvider` mirrors)

---

## Pre-acceptance audit (5-minute self-check)

- [ ] **AHA pass.** Considered Option B (per-repository) and Option C (per-record) before settling on Option A. Option B fails on surface-area multiplication; Option C fails on per-render perf. Documented in §Considered options.
- [ ] **FAILED conditions / kill triggers.** Named in §Revisit triggers (ADR 0009 supersession, W#35 contract change, regulated-tenant incident, audit-volume scaling problem).
- [ ] **Rollback strategy.** Both new optional `ExtensionFieldSpec` parameters default to non-gating behavior; the new `GetFieldsAsync` overload coexists with the unchanged `GetFields` overload. Reverting the change is a matter of removing the new types and overload; existing call sites are unaffected. The audit-event-type additions are append-only (ADR 0049's substrate is append-only by design).
- [ ] **Confidence level.** MEDIUM. The contract surface is clean and the substrate dependencies are verified, but §Open questions items 1, 2, 3, 4 (lazy DI, sequestration flag, audit volume, Wayfinder coupling) all carry council-class risk. Pre-merge council is the right gate.
- [ ] **Cited-symbol verification.** §A0.1 + §A0.2 + §A0.3 enumerate every Sunfish.* symbol and verify existence + structural-citation. Cohort lesson (5-of-5 prior structural-citation failures NOT caught by §A0) means council is canonical.
- [ ] **Anti-pattern scan.** AP-1 (unvalidated assumption: lazy-DI optionality is right) — flagged as Open question 1. AP-3 (vague success): success criteria are "every gating decision audited; backward compatibility preserved; W#35 composition exercised"; verifiable. AP-9 (skip Stage 0): NOT skipped — three options considered. AP-12 (timeline fantasy): no timeline asserted. AP-21 (assumed facts): every Sunfish.* claim is in §A0.
- [ ] **Revisit triggers.** Named in §Revisit triggers.
- [ ] **Cold Start Test.** Implementation checklist Phases 1-10 are file-by-file actionable; a fresh COB session can execute without asking the author for clarification, modulo the Open questions which are flagged for council resolution.
- [ ] **Sources cited.** ADR 0009 § citations, ADR 0049 § citations, ADR 0028-A5.4/A8.3, ADR 0065 §A0 cohort discipline, ADR 0069 D1/D2/D3 — all present in §References.

---

*This ADR runs the ADR 0069 D1 + D2 + D3 disciplines in full. The §A0 self-audit catches what XO can catch from a draft-time mental model; the pre-merge council is canonical defense. Cohort batting average: high single-to-double-digit substrate amendments needed council fixes (per ADR 0069 §Cohort batting average); this ADR will not be the counter-example.*
