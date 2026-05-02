---
id: 65
title: Wayfinder System + Standing Order Contract (bundled)
status: Proposed
date: 2026-05-01
tier: foundation
concern: []
composes:
  - 9
  - 28
  - 49
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0065 — Wayfinder System + Standing Order Contract (bundled)

**Status:** Proposed
**Date:** 2026-05-01
**Authors:** XO research session
**Pipeline variant:** `sunfish-feature-change`
**Council posture:** standard adversarial + WCAG/a11y subagent (mandatory; per W#34 hardening)
**Consumer scope:** all eight Wayfinder configuration layers; every Sunfish package that has settings

---

## Status

Proposed. Bundled per W#34 §6.1: defining `StandingOrder` requires the Wayfinder system context (who issues, who validates, who consumes); defining the Wayfinder system requires the `StandingOrder` shape (what events flow through it). The two cannot be split without a chicken-and-egg sequencing problem. Pre-merge council canonical (per cohort lesson — 18-of-18 substrate amendments needed council fixes; running council before merge eliminates the post-acceptance amendment cycle).

---

## Context

Sunfish has **eight scattered configuration layers** (per W#34 discovery §3): user preferences, tenant config, feature management (ADR 0009), capability declarations (ADRs 0033/0062/0063), domain config (ADRs 0055/0056), integration config, security config, account-identity. Each layer today has its own ad-hoc storage, validation, distribution, and audit story — or none at all. The W#34 discovery identified this as the **most load-bearing follow-on**: every other Wayfinder ADR (~0066 Helm/identity Atlas, ~0067 integration-config, ~0068 tenant security policy) consumes the contract this ADR defines, and the ADR 0009 amendment (5th-concept extension) does too.

The discovery's verdict-table coverage (§5) showed:

- **3 Specified layers**: feature management (ADR 0009), capability declarations (ADRs 0033/0062/0063), domain config (ADRs 0055/0056)
- **3 Partial layers**: tenant config (storage exists, no UX/validation/audit), integration config (per-block ad hoc), account-identity (split across ADR 0046)
- **2 Gap layers**: user preferences (no surface at all), security config (W#37 / ~ADR 0068)

The asymmetry between Specified and Partial/Gap is *not* a missing data type — it is a missing **system contract**: how a configuration change is captured, validated, attributed, audited, distributed, and conflict-resolved. The Specified layers each invented their own story; the Partial/Gap layers must invent yet more. Without a unified system, every new block adds another bespoke configuration surface.

**Wayfinder** (the system) and **Standing Order** (the event-type primitive that flows through it) provide that contract. The naming is locked per W#34 brainstorm (Helm = live state pane; Atlas = deep-config user-facing UI; Standing Order = internal config-change record / event type). Atlas is *what users see*; Standing Orders are *what the system records*. Composes naturally with ADR 0049 audit (every Standing Order is an audit event by construction) and ADR 0028 CRDT (Standing Orders are append-only operations under per-tenant log compaction semantics).

This ADR specifies the data model, CRDT semantics, validation pipeline, audit emission, Atlas materialized-view contract, system DI surface, and WCAG 2.2 AA conformance specification.

---

## Decision drivers

1. **Load-bearing for ≥4 downstream ADRs.** ~0066 (Helm + identity Atlas), ~0067 (Atlas integration-config), ~0068 (tenant security policy), and the ADR 0009 amendment all consume the Standing Order contract. Late changes propagate.
2. **Audit-by-construction.** A configuration change that is not audited is a compliance gap. Every Standing Order MUST emit an audit event; the contract enforces this at the type level (the issuance API takes the audit-emitter dependency, not optional).
3. **CRDT-native.** Two operators issuing concurrent Standing Orders on overlapping paths must reconcile deterministically — without operator-visible "merge conflict" surfaces. ADR 0028's `ICrdtEngine` carries the merge semantics; this ADR specifies how Standing Orders compose.
4. **Dual-surface (form ↔ JSON) without surface drift.** VSCode's pattern: settings.json and the Settings UI share one backing store. Sunfish replicates this; the Atlas form view and the JSON view are two projections of the same Standing Order log.
5. **WCAG 2.2 AA conformance as a contract, not a goal.** Per W#34 hardening: cognitive-function tests are forbidden in MFA UX (3.3.8); error-prevention requires reversible/checked/confirmed for legal/financial commitments (3.3.7); JSON-edit views built on Monaco/CodeMirror have known SR gaps and require an accessible-alternative; EN 301 549 procurement compliance is a Bridge tenant requirement in EU jurisdictions. Council dispatches WCAG/a11y subagent on every UI-bearing follow-on.
6. **Schema-driven, with deep search.** JetBrains' pattern: every settable surface is described by a schema; the Atlas search index is built from schemas, not from values. Search-as-you-type latency target: P95 ≤ 100ms over a 10K-setting catalog.
7. **Diff-preview before commit.** Stripe Dashboard's pattern: show what is about to change before the user commits. Reduces fat-finger errors on legal/financial settings.

---

## Considered options

### Option A — One Standing Order per setting, individual events

Each Wayfinder mutation is its own `StandingOrder` event. Simple. Audit-natural. Aligns with ADR 0049's per-event grain. **Rejected** because real configuration changes are often *bundled* (turning on feature X also requires three companion settings); per-event breaks atomicity and produces partially-applied configurations during replay.

### Option B — Per-tenant Standing Order log with bundled-issuance support **[RECOMMENDED]**

Each `StandingOrder` carries one or more atomic `(path, oldValue, newValue)` triples; the issuance pipeline either commits all or none; the per-tenant log is append-only and CRDT-merge-aware. Atlas projects from the ordered log. Audit emission is per-StandingOrder (one audit event per issuance — possibly bundling N triples), preserving operator intent in the audit record.

### Option C — Standing Order is a shape on ADR 0049's `AuditRecord`

Re-use `AuditRecord` directly; add `StandingOrderPayload` as one of its payload types. **Rejected** — ADR 0049's `AuditRecord` is the durable observation; `StandingOrder` is the operator intent. Conflating the two loses the distinction between "what was attempted" and "what was recorded." Standing Orders that are *rejected* still need to be issued + validated + audited, but they are not configuration changes that get applied. Two distinct shapes preserves the asymmetry.

**Decision: Option B.**

---

## Decision

### 1. Standing Order data model

`Sunfish.Foundation.Wayfinder.StandingOrder` (new package: `foundation-wayfinder`):

```csharp
public sealed record StandingOrder(
    StandingOrderId Id,
    TenantId TenantId,
    ActorId IssuedBy,
    Instant IssuedAt,
    StandingOrderScope Scope,
    IReadOnlyList<StandingOrderTriple> Triples,
    string Rationale,
    ApprovalChain? ApprovalChain,
    AuditRecordId AuditRecordId,
    StandingOrderState State
);

public sealed record StandingOrderTriple(
    string Path,           // dotted path within Scope, e.g. "anchor.maui.theme"
    JsonNode? OldValue,    // null when the path was previously unset
    JsonNode? NewValue     // null when the new state is "unset"
);

public enum StandingOrderScope { User, Tenant, Platform, Integration, Security }

public enum StandingOrderState { Issued, Validated, Applied, Rescinded, Rejected, Conflicted }

public sealed record ApprovalChain(IReadOnlyList<ApprovalStep> Steps);
public sealed record ApprovalStep(ActorId Approver, Instant ApprovedAt, string? Comment);

public readonly record struct StandingOrderId(Guid Value);
public readonly record struct AuditRecordId(Guid Value);
```

### 2. Standing Order CRDT semantics

`StandingOrder` is **append-only per-tenant**. The per-tenant log composes via `Sunfish.Kernel.Crdt.ICrdtEngine` using last-writer-wins-by-IssuedAt-then-IssuedBy at the per-`(Scope, Path)` grain. Concurrent issuances on disjoint paths merge cleanly; concurrent issuances on the same path produce a `Conflicted` state for the loser, which the validation pipeline surfaces to the issuing operator without overwriting the winner.

Loro and YDotNet (per ADR 0028 §A6.1) carry the underlying CRDT; this ADR adds no new CRDT primitives. The Standing Order log is materialized into a per-tenant Loro document under the document-id `wayfinder/standing-orders/{tenantId}` — created via `ICrdtEngine.CreateDocument(documentId)` (fresh) or hydrated from snapshot via `ICrdtEngine.OpenDocument(documentId, snapshot)`. The repository keeps one open document per tenant; document containers (`GetMap`, `GetList`) are accessed lazily as Standing Orders accumulate. Schema epoch follows `Sunfish.Kernel.Crdt`'s shared kernel epoch.

**Conflict resolution is operator-visible** — losing-side operators see their `StandingOrder.State == Conflicted` in the Atlas UI, with a one-click "amend and re-issue" path. This is the *only* conflict UX; we do not present three-way merge dialogs (per W#34 §5.7 — that pattern fails WCAG 3.3.7 error-prevention for non-technical users).

### 3. Validation pipeline

`IStandingOrderValidator` chain. Implementations registered via DI; chain order is deterministic (priority enum):

```csharp
public interface IStandingOrderValidator
{
    StandingOrderValidatorPriority Priority { get; }
    ValueTask<StandingOrderValidationResult> ValidateAsync(
        StandingOrder order, StandingOrderContext context, CancellationToken ct);
}

public enum StandingOrderValidatorPriority
{
    Schema = 100,        // shape; required keys; type check; range
    Policy = 200,        // tenant policy; e.g. "production never has theme=experimental"
    Authority = 300,     // ICapabilityGraph.HasCapability(Principal, CapabilityAction) per Sunfish.Foundation.Capabilities
    Conflict = 400       // detect concurrent issuances; mark conflict state
}

public sealed record StandingOrderValidationResult(
    bool Accepted,
    IReadOnlyList<StandingOrderValidationIssue> Issues);

public sealed record StandingOrderValidationIssue(
    StandingOrderValidationSeverity Severity,
    string Path,
    string Message,
    string? RemediationHint);

public enum StandingOrderValidationSeverity { Info, Warning, Error, Block }
```

Any `Block`-severity issue rejects the order; `State` flips to `Rejected`; the rejection still emits an audit event. `Error` reduces to `Block` if the issuing capability is below `Tenant.Admin`. `Warning`/`Info` annotate the order without rejecting.

### 4. Audit emission (composes ADR 0049)

Every issuance, amendment, rescission, conflict, and rejection emits a record via `Sunfish.Kernel.Audit.IAuditTrail`. The audit emission is **non-optional** at the type level — the issuance API requires the `IAuditTrail` dependency, not as a callback registration:

```csharp
public interface IStandingOrderIssuer
{
    Task<StandingOrder> IssueAsync(
        StandingOrderDraft draft,
        ActorId issuedBy,
        IAuditTrail auditTrail,             // required, not optional
        CancellationToken ct);

    Task<StandingOrder> RescindAsync(
        StandingOrderId id,
        ActorId rescindedBy,
        string rationale,
        IAuditTrail auditTrail,
        CancellationToken ct);
}
```

This ADR introduces **5 new `AuditEventType` static-readonly constants** on `Sunfish.Kernel.Audit.AuditEventType` (which is a `readonly record struct(string Value)` per the existing kernel-audit package — same pattern as `KeyRecoveryInitiated` / `KeyRecoveryAttested` / etc.):

```csharp
// added to Sunfish.Kernel.Audit.AuditEventType
public static readonly AuditEventType StandingOrderIssued = new("StandingOrderIssued");
public static readonly AuditEventType StandingOrderAmended = new("StandingOrderAmended");
public static readonly AuditEventType StandingOrderRescinded = new("StandingOrderRescinded");
public static readonly AuditEventType StandingOrderRejected = new("StandingOrderRejected");
public static readonly AuditEventType StandingOrderConflictResolved = new("StandingOrderConflictResolved");
```

The issuer internally constructs an `AuditRecord(...)` (with `EventType =` one of the above) and calls `IAuditTrail.AppendAsync(record, ct)`. `StandingOrderConflictResolved` is emitted once per concurrent-issuance pair, citing both `StandingOrderId` values. Exactly one audit record per issuance (audit emission is at the issuance grain, not the triple grain).

**Rescission semantics.** `RescindAsync` emits a *new* audit record (`StandingOrderRescinded`) referencing the rescinded `StandingOrderId`; it does NOT redact the original `StandingOrderIssued` audit record. Audit immutability per ADR 0049 is preserved. The rescission nullifies the *future* effect of the rescinded order on the Atlas projection — downstream effects already realized (e.g., a license issued under the rescinded policy, an operator granted access, a payment authorized) remain and require independent reversal via their own domain mechanisms. The 30-day reversibility window is a UI affordance ("amend within 30 days"), not a transactional rollback contract.

### 5. Atlas materialized-view contract

`IAtlasProjector` projects the per-tenant Standing Order log into a queryable settings catalog:

```csharp
public interface IAtlasProjector
{
    ValueTask<AtlasView> ProjectAsync(
        TenantId tenantId,
        StandingOrderScope? scopeFilter,
        CancellationToken ct);

    IAsyncEnumerable<AtlasSearchHit> SearchAsync(
        TenantId tenantId,
        string query,
        int limit,
        CancellationToken ct);
}

public sealed record AtlasView(
    TenantId TenantId,
    Instant ProjectedAt,
    IReadOnlyDictionary<string, AtlasSettingSnapshot> SettingsByPath);

public sealed record AtlasSettingSnapshot(
    string Path,
    JsonNode? CurrentValue,
    StandingOrderId LastIssuedBy,
    Instant LastIssuedAt,
    AtlasSchemaDescriptor Schema);

public sealed record AtlasSchemaDescriptor(
    JsonNode JsonSchema,        // RFC draft 2020-12
    string DisplayName,
    string DescriptionMarkdown,
    AtlasSettingKind Kind);

public enum AtlasSettingKind { String, Number, Boolean, Enum, JsonObject, Secret }

public sealed record AtlasSearchHit(
    string Path,
    string DisplayName,
    string MatchSnippet,
    double Score);
```

**Search target:** P95 ≤ 200ms cold-projection (after a fresh hydrate from snapshot or partition recovery); P95 ≤ 100ms with warm projection cache. Empirically validated against a 10K-setting catalog before Phase 3 close. Implementation is left to consumers; the contract requires `IAsyncEnumerable` for streaming hit-by-hit incremental display.

**Dual-surface (form ↔ JSON):** the same `AtlasView` is presented in both surfaces. Form-surface mutations and JSON-surface mutations both produce `StandingOrderDraft` → `IStandingOrderIssuer.IssueAsync`; there is no JSON-side bypass. The JSON view in the Atlas UI uses a syntax-highlighted editor (Monaco / CodeMirror), with a mandatory **accessible alternative** form view per WCAG 2.2 AA (see §WCAG below).

### 6. Wayfinder system DI surface

The new `foundation-wayfinder` package registers via `AddSunfishWayfinder()`:

```csharp
public static class WayfinderServiceExtensions
{
    public static IServiceCollection AddSunfishWayfinder(this IServiceCollection services)
    {
        services.AddSingleton<IStandingOrderRepository, CrdtStandingOrderRepository>();
        services.AddSingleton<IStandingOrderIssuer, DefaultStandingOrderIssuer>();
        services.AddSingleton<IAtlasProjector, DefaultAtlasProjector>();
        // Validators are registered separately by consumers via AddStandingOrderValidator<T>()
        return services;
    }

    public static IServiceCollection AddStandingOrderValidator<TValidator>(
        this IServiceCollection services)
        where TValidator : class, IStandingOrderValidator
    {
        services.AddSingleton<IStandingOrderValidator, TValidator>();
        return services;
    }
}
```

`AddSunfishWayfinder()` requires `AddSunfishKernelAudit()` and `AddSunfishKernelCrdt()` to be registered first (composition guard). Failure to register them throws `InvalidOperationException` at first issuance, with a remediation message naming the missing call.

### 7. WCAG 2.2 AA conformance specification

This ADR is **bound** to WCAG 2.2 AA conformance per W#34 §5.7. The following are contract requirements (not goals), and the council's WCAG/a11y subagent is mandatory:

- **3.3.7 Redundant Entry** — none of the Atlas form fields require re-entry of information already supplied in the same session for the same setting.
- **3.3.8 Accessible Authentication (No Cognitive Function Test)** — the Atlas authority-check UI MUST NOT impose cognitive function tests on the operator (no CAPTCHAs, no remembered-secret challenges). MFA enrollment and re-confirm flows fall under ~ADR 0068 and inherit this constraint.
- **3.3.9 Accessible Authentication (Enhanced)** — applies to Tier 2/3 setting categories ("Security" scope, "Integration secrets"). At-issuance step-up MAY use device-attestation but MUST NOT use cognitive recall.
- **3.3.7-Error-Prevention (Legal/Financial)** — for `StandingOrderScope.Tenant` and `StandingOrderScope.Security` settings, every issuance presents a **diff-preview** with explicit confirm step (Stripe-pattern). The confirmation UI MUST list the changing path(s), prior value(s), new value(s), and the issuing actor's identity. Reversible by `RescindAsync` within 30 days.
- **JSON-edit accessible alternative** — Monaco/CodeMirror have known screen-reader gaps. The form view is the **accessible alternative** required by WCAG 2.1 conformance level. The dual-surface toggle defaults to the form view for users whose UA reports `prefers-reduced-motion: reduce` OR who have an active screen-reader detected via the platform a11y API (ADR 0048's UIA / NSAccessibility / UIAccessibility / AccessibilityNodeInfo / ARIA).
- **Search a11y** — the search-as-you-type Atlas surface uses an `aria-live="polite"` region for hit count + first-three-hit announcement; full results render in a focusable list (`role="listbox"` with `aria-activedescendant`). Latency target announcements: at P95>500ms, an "in progress" announcement fires once.
- **Diff-preview a11y** — the diff-preview is a structured table (`<table>` with proper `<thead>`/`<tbody>` semantics) with a header row `Path | Prior | New`; values that are JSON objects render with an expandable structure, never as raw text in a single cell.
- **EN 301 549 procurement compliance** — Bridge tenants in EU jurisdictions require EN 301 549 conformance; this ADR's contract is a superset. Conformance reports are produced per release per `apps/docs/wcag/wayfinder.md` (new file added in Phase 6).

### 8. §A0 — self-audit limitation block (per ADR 0062-A1.14 cohort discipline)

The author of this ADR ran the standard 3-direction self-audit on every cited Sunfish.* symbol but acknowledges:

- **§A0.1 Negative-existence**: verified `Sunfish.Foundation.Wayfinder.*` and `IStandingOrderRepository` *do not yet exist on origin/main* and are introduced by this ADR's Phase 1 build. **Council F1 correction (2026-05-01):** the original draft incorrectly listed `Sunfish.Foundation.Capabilities.*` as not-yet-existing — it DOES exist (in `packages/foundation/Capabilities/` under namespace `Sunfish.Foundation.Capabilities`, not as a separate `foundation-capabilities` package). See §A0.2.
- **§A0.2 Positive-existence**: verified `Sunfish.Kernel.Audit.IAuditTrail`, `Sunfish.Kernel.Audit.AuditEventType` (a `readonly record struct(string Value)` with static-field constants, NOT an enum), `Sunfish.Kernel.Audit.AuditRecord`, `Sunfish.Kernel.Crdt.ICrdtEngine` (with API `CreateDocument(string documentId)` + `OpenDocument(string documentId, ReadOnlyMemory<byte> snapshot)`), `Sunfish.Foundation.MultiTenancy.TenantId`, `Sunfish.Foundation.Identity.ActorId`, `Sunfish.Foundation.Capabilities.{Principal, CapabilityAction, CapabilityProof, ICapabilityGraph, MutationResult, CapabilityClosure, CapabilityOp, Resource}` (all in `packages/foundation/Capabilities/`), `NodaTime.Instant` exist on origin/main as cited.
- **§A0.3 Structural-citation correctness**: per cohort discipline (5-of-5 prior structural-citation failures), this draft was self-corrected for: (a) `AuditRecord.EventType` field name verified correct, (b) `IAuditTrail.AppendAsync(AuditRecord record, CancellationToken ct)` is the actual issuance signature — the issuer constructs the `AuditRecord` then calls `AppendAsync`, NOT a `(AuditEventType, payload, ct)` overload, (c) `AuditEventType` is `readonly record struct`, so the 5 new constants are `public static readonly AuditEventType StandingOrderIssued = new("StandingOrderIssued")` static fields, not enum values, (d) WCAG 2.2 AA SC numbers verified 3.3.7/3.3.8/3.3.9 (not 3.3.5/3.3.6). Council MUST still spot-check (e) `ICrdtEngine`'s document-key API and (f) any field-name drift in `AuditRecord` since cohort merge.

The §A0 self-audit is *necessary but not sufficient* — council remains canonical defense per the cohort batting average of 11-of-17 prior structural-citation failures NOT caught by §A0.

---

## Consequences

### Positive

1. **Eliminates the ad-hoc-config tax.** Every block stops inventing its own settings story. ~0066/~0067/~0068 + the ADR 0009 amendment all consume one contract.
2. **Audit-by-construction.** Every configuration change is auditable, attributable, and reversible (within 30 days). Compliance regimes (HIPAA / GDPR / SOC 2 / PCI DSS / EU AI Act per ADR 0064) can prove configuration provenance.
3. **CRDT-native distribution.** Multi-anchor and Bridge-hosted tenants converge automatically; no operator-visible "merge dialogs."
4. **Search-discoverability.** A 10K-setting catalog stays navigable via the JetBrains-pattern search.
5. **Form/JSON parity with no surface drift.** VSCode-pattern dual surface; both go through the same `StandingOrderIssuer`.
6. **WCAG 2.2 AA + EN 301 549 conformance is a contract.** The council's WCAG/a11y subagent enforces; Bridge EU tenants are not procurement-blocked.
7. **Sequencing for ~0066/~0067/~0068.** Once this ADR lands and W#42 builds Phase 1, the downstream Wayfinder ADRs can author against a stable contract.

### Negative

1. **Surface scope is large.** Phase 1 alone is ~16-20h sunfish-PM time across 5-7 PRs. Estimated total to full Atlas UI parity: ~80h across the 4 downstream ADRs.
2. **CRDT engine semantics inherited from ADR 0028.** Bugs in YDotNet/Loro merge logic propagate here. We mitigate via per-tenant log isolation (no cross-tenant merge surface) and a property-based test suite for concurrent-issuance scenarios.
3. **Validator-chain ordering is a foot-gun.** Two policy validators that emit on the same path can produce confusing operator-visible messages. Mitigation: chain order is deterministic by `Priority` enum, and operator UI shows the first-blocking issue with a "see all issues" expansion.
4. **Schema-driven surface requires schema discipline.** Every block that issues settings must publish its schema (RFC draft 2020-12 JSON Schema) at registration. Blocks without schemas are non-discoverable in Atlas search. We add a build-time analyzer (Phase 4) that warns on `IServiceCollection.AddSunfish*()` calls that don't register an `AtlasSchemaDescriptor`.
5. **Diff-preview adds friction for power users.** Stripe-pattern confirm step for Tenant/Security scope adds 1-2 keystrokes. Justified by error-prevention (3.3.7); not bypassable.
6. **`StandingOrderConflictResolved` audit emission can be noisy in degraded-network multi-anchor topologies.** A burst of conflicting issuances during partition recovery produces a burst of audit records. Mitigation: audit subscribers MAY apply a `dedupe-by-PairId` filter; the contract is an audit record per *pair*, not per *operator-visible surface*.

### Trust impact

- **Trust expanded:** the Wayfinder system is trusted to enforce the validator chain. Validators registered by application code have access to the full `StandingOrderContext` (including `IAuditTrail`); a malicious validator could log spurious audit records or block legitimate issuances. Mitigation: validators are registered via DI by application code (not by tenant), and the platform-trust boundary is the same as for any other DI-registered service. We do *not* allow tenants to register validators dynamically; that would be a separate ADR.
- **Trust contracted:** the Atlas projection is **read-derived** from the Standing Order log. The log is the source of truth; projection bugs cannot corrupt durable state. Trust on `IAtlasProjector` is read-only.
- **Capability boundary clarified:** issuance of `StandingOrderScope.Security` requires `Capability.SecurityAdmin` (subject to ~ADR 0068's full security model). Issuance of `StandingOrderScope.Platform` requires platform-tenancy (Bridge platform-admin, not regular Tenant.Admin).

---

## Compatibility plan

- **Backward compatibility:** none required. This ADR introduces a new package (`foundation-wayfinder`) and a new event-type contract; it does not modify existing settings storage. Existing per-block ad-hoc settings (e.g., ADR 0055's dynamic-forms config) remain valid; migration to the Wayfinder/StandingOrder model is a per-block, per-ADR call (not bulk).
- **Forward compatibility:** the `StandingOrder` shape is open to future extension via additive `Triples` per-issuance (the `Triples` collection is unbounded). Schema-evolution within `JsonNode? NewValue` is per-block (each block owns its setting schema; ADR 0001 schema-registry governs cross-version compatibility).
- **Migration of existing config:** consumed-block-by-block via per-ADR opt-in. The ADR 0009 amendment is the first such opt-in; ~0066 / ~0067 / ~0068 follow.
- **Schema epoch:** Wayfinder shares the kernel schema epoch (`Sunfish.Kernel.Crdt`'s shared epoch). A bump to the kernel epoch invalidates previous Standing Order CRDT state; per-tenant log replay rebuilds Atlas projections.

---

## Implementation checklist

### Phase 1 — Foundation.Wayfinder package + Standing Order types (~5h)

- [ ] Create `packages/foundation-wayfinder/` (csproj + Directory.Build.props)
- [ ] Define `StandingOrder`, `StandingOrderTriple`, `StandingOrderScope`, `StandingOrderState`, `ApprovalChain`, `ApprovalStep`, `StandingOrderId`, `AuditRecordId` (final shapes)
- [ ] Define `IStandingOrderRepository`, `IStandingOrderIssuer`, `IStandingOrderValidator`, `StandingOrderValidatorPriority`, `StandingOrderValidationResult`, `StandingOrderValidationIssue`, `StandingOrderValidationSeverity`
- [ ] Define `WayfinderServiceExtensions.AddSunfishWayfinder()` + `AddStandingOrderValidator<T>()`
- [ ] Add 5 new `AuditEventType` constants to `Sunfish.Kernel.Audit.AuditEventType`
- [ ] Reference `Sunfish.Kernel.Audit` + `Sunfish.Kernel.Crdt` + `Sunfish.Foundation.MultiTenancy` + `Sunfish.Foundation.Identity` + `NodaTime`
- [ ] Unit tests: 12 tests covering shape round-trip + canonical-JSON serialization (per ADR 0028 §A7.8 camelCase)

### Phase 2 — CRDT-backed repository + issuer (~4h)

- [ ] Implement `CrdtStandingOrderRepository` over `ICrdtEngine` (per-tenant Loro document at `wayfinder/standing-orders/{tenantId}`)
- [ ] Implement `DefaultStandingOrderIssuer` calling validator chain in priority order; flipping `State` to `Rejected` on `Block`-severity issue
- [ ] Implement audit emission per issuance (5 new event types)
- [ ] Implement `RescindAsync` with 30-day reversibility window
- [ ] Property tests: 8 tests covering concurrent-issuance CRDT merge + conflict-resolution + audit emission count

### Phase 3a — Atlas projector + search basics (~4h)

- [ ] Implement `DefaultAtlasProjector` projecting from per-tenant log
- [ ] Define `AtlasView`, `AtlasSettingSnapshot`, `AtlasSchemaDescriptor`, `AtlasSearchHit`, `AtlasSettingKind`
- [ ] Implement `SearchAsync` with target P95 ≤ 200ms cold / ≤ 100ms warm over a 10K-setting catalog (use `MemoryMappedFile` or in-memory inverted index; final choice in scaffolding stage)

### Phase 3b — Schema-registration analyzer + perf tests (~3h)

- [ ] Schema-registration analyzer: NEW `Sunfish.Wayfinder.Analyzers` Roslyn-analyzer package; severity Warning; warns at build time on `AddSunfish*()` invocations that don't register an `AtlasSchemaDescriptor`
- [ ] Performance tests: 4 tests covering search latency at 1K / 5K / 10K / 50K settings; cold + warm projection scenarios both measured

### Phase 4 — Cross-package wiring + apps/docs (~2h)

- [ ] Wire `AddSunfishWayfinder()` into `apps/kitchen-sink` to demonstrate one form-view setting
- [ ] Wire `AddSunfishWayfinder()` into `apps/docs/main` and add `apps/docs/blocks/foundation-wayfinder.md` documentation
- [ ] Add `apps/docs/wcag/wayfinder.md` WCAG 2.2 AA + EN 301 549 v3.2.1 conformance report (initial baseline; iterates per release)
- [ ] Cross-link from `_shared/product/architecture-principles.md` (the "Wayfinder system" section becomes a real link)

### Phase 5 — Ledger flip + close W#42 (~30min)

- [ ] Update `icm/_state/active-workstreams.md` row 36: `design-in-flight` → `built`
- [ ] Add row note: PR list + new package list + new AuditEventType list
- [ ] Update memory `project_workstream_36_*.md` with shipped scope

**Note (Council F4):** ADR 0009 amendment authoring (5th-concept feature-management consumer) is **NOT in W#42 scope** per cohort discipline (substrate vs consumer separation). Filed as separate workstream row pending CO disposition.

**Total estimate:** ~18-25h sunfish-PM time across 6-7 PRs (council-revised from initial ~16-18h estimate; cohort precedent of 3-5h per phase). Pre-merge council canonical (Stage 1.5 + WCAG/a11y subagent BEFORE any phase commit).

---

## Open questions

1. **Schema-registration enforcement.** Should the Phase 3 build-time analyzer be `warning` or `error`? Recommendation: `warning` for Phase 1 (gives blocks time to migrate), `error` after Phase 2 of W#37 ships (security-policy schemas must be discoverable).
2. **Cross-tenant Standing Order visibility.** Bridge platform-admin needs read access to tenant Standing Orders for support/compliance audits. This is gated on capability `Capability.PlatformAdmin` (not yet defined; W#37 territory). Open: does the Atlas projector expose a cross-tenant view to platform-admins, or do they query per-tenant only?
3. **Standing Order amendment vs. rescind+re-issue.** The contract has `RescindAsync` but no `AmendAsync`. Is amendment a separate event type, or does it model as `Rescind(old) + Issue(new)`? Recommendation: model as `AmendAsync` returning the new `StandingOrder`; both old and new emit audit records linked by `StandingOrderId`. Defer to scaffolding stage.
4. **JSON edit view a11y baseline.** Monaco vs. CodeMirror vs. simple `<textarea>` for the JSON view. Monaco has the richer authoring UX; CodeMirror has a slightly better SR story; `<textarea>` has the best raw a11y. Recommendation: `<textarea>` first for Phase 1 (form view is the canonical path; JSON view is escape-hatch); upgrade to CodeMirror in a later workstream.
5. **`StandingOrderConflictResolved` dedup window.** How long is the dedup window for the partition-recovery noise? Recommendation: 5 minutes (enough for typical Headscale partition recovery per ADR 0061; small enough to not lose distinct conflict events).
6. **Atlas search latency under concurrent issuance load.** P95 ≤ 100ms target — is this still met under 100 concurrent issuance/second? Probably yes (issuance writes to log, projection rebuilds incrementally), but property tests must verify before Phase 3 close.

---

## Revisit triggers

- **CRDT engine swap.** If ADR 0028 supersedes YDotNet/Loro with a different engine, this ADR's CRDT semantics need verification.
- **Audit substrate change.** If ADR 0049 changes `IAuditTrail` signature, this ADR's audit emission contract needs amendment.
- **WCAG 2.2 → 2.3 / 3.0.** Future WCAG-version uplift; this ADR's §7 SC numbers and conformance contract need re-baselining.
- **Cross-tenant Atlas exposure.** If platform-admin Atlas access is granted (open question 2), the trust impact section needs amendment.
- **First incident.** If a Standing Order is mis-validated and applied to production with operator-visible damage, the validator chain order + diff-preview UX requires revisit.

---

## References

### Predecessor and sister ADRs
- ADR 0009 — Foundation.FeatureManagement (extended by 5th-concept amendment)
- ADR 0028 — CRDT engine selection (composition: Standing Order CRDT semantics)
- ADR 0049 — Audit trail substrate (composition: audit-by-construction)
- ADR 0048 — Anchor multi-backend MAUI (referenced for platform a11y APIs)

### Architectural precedent
- ADR 0046 — Key-loss recovery scheme (precedent for `ApprovalChain` shape)
- ADR 0055 — Dynamic forms substrate (precedent for schema-driven UI)
- ADR 0056 — Foundation.Taxonomy substrate (precedent for `AddSunfishX()` DI extensions)
- ADR 0064 — Runtime regulatory / jurisdictional policy evaluation (consumer of Standing Orders for Tier 2/3 setting categories)

### Discovery and intake
- W#34 discovery: `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` §5.1 / §6.1 / §7
- W#42 intake: `icm/00_intake/output/2026-05-01_wayfinder-system-and-standing-order-intake.md`
- W#34 naming memory: `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_34_wayfinder_naming.md`

### Cohort discipline
- Council batting average: 18-of-18 substrate amendments needed council fixes (canonical: pre-merge council)
- Structural-citation failure rate: ~65% of XO-authored ADRs had ≥1 structural-citation failure that §A0 self-audit did NOT catch — council remains canonical defense

### External
- WCAG 2.2 AA — W3C Recommendation, October 2023
- EN 301 549 v3.2.1 (2021) — Accessibility requirements for ICT products and services (EU procurement compliance)
- VSCode dual-surface settings pattern: <https://code.visualstudio.com/docs/getstarted/settings>
- JetBrains schema-driven settings pattern: <https://www.jetbrains.com/help/idea/configuring-project-and-ide-settings.html>
- Stripe Dashboard diff-preview pattern (industry observation, no canonical link)
- RFC draft 2020-12 (JSON Schema): <https://json-schema.org/draft/2020-12>
- RFC 8785 (canonical JSON): <https://datatracker.ietf.org/doc/html/rfc8785>
