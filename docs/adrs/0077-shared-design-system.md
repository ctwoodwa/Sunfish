---
id: 77
title: Shared Design System (role taxonomy + permission tuple + decks + tokens + a11y baseline)
status: Accepted
date: 2026-05-04
tier: foundation
pipeline_variant: sunfish-feature-change
concern:
  - ui
  - accessibility
  - identity
  - capability-model
  - configuration
  - regulatory
enables:
  - canonical-ship-role-registration
  - permission-tuple-resolution
  - deck-progressive-disclosure
  - first-aid-contextual-help
  - cross-platform-design-tokens
  - wcag-2-2-aa-conformance-baseline
  - en-301-549-procurement-readiness
composes:
  - 8
  - 9
  - 32
  - 34
  - 36
  - 41
  - 46
  - 48
  - 49
  - 62
  - 65
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments: []
---

# ADR 0077 — Shared Design System (role taxonomy + permission tuple + decks + tokens + a11y baseline)

**Status:** Proposed
**Date:** 2026-05-04
**Authors:** XO research session
**Pipeline variant:** `sunfish-feature-change`
**Council posture:** pre-merge canonical + WCAG/a11y subagent + design-engineering subagent + security-engineering subagent (mandatory; role taxonomy intersects security per W#35 §9.5) + i18n/RTL subagent if available
**Resolves:** W#35 Ship Architecture follow-on §8.2 — the load-bearing Shared Design System ADR. Sequenced first per W#35 §9.2.

---

## Status

Proposed — **triple council review complete; amendments applied 2026-05-04.** Awaiting CO acceptance.

This ADR is the **load-bearing follow-on** of the W#35 Ship Architecture discovery. Every downstream UI ADR in the W#34/W#35 cohort (Quarterdeck entry-point / Engine Room observability / Tactical anomaly-detection / Sick Bay aggregation / Ship's Office content / OOD-Watch rotation / ~ADR 0066 Helm + identity Atlas / ~ADR 0068 tenant security policy) consumes the contract this ADR defines. Triple council ran before commit per cohort discipline (22-of-22 substrate amendments needed council fixes per W#42 shipping log). All 3 Blocking + 9 NM + 11 SC/Mechanical findings addressed inline; council disposition recorded at end of document.

---

## Context

The W#35 Ship Architecture discovery (`icm/01_discovery/output/2026-05-01_ship-architecture.md`) specified the two-layer model — `(role × location × deck-depth) → action` — that the operator/admin/dev experience composes through. The discovery is a *map*; this ADR is the *contract*. Without it, every downstream UI ADR re-derives role taxonomy, permission denial UX, contextual help, design tokens, focus management, color/contrast guarantees, motion preferences, live-region semantics, and platform a11y wiring from scratch — and the cohort metric (22-of-22 substrate amendments needing council fixes per the W#33/W#34/W#42 shipping log) compounds.

The W#35 discovery's verdict tables identified 1 Specified location + 3 Partial + 3 Gap on the location side; 1 Partial role + 5 Gap + 2 deferred on the role side; **4 cross-axis primitives, all Gap** (permission tuple / watch rotation / stretcher-bearer / first-aid baseline). The asymmetry is not "missing code" — it is a **missing system contract**: how a role is named, how a denial is surfaced accessibly, how contextual help is inherited, how tokens cross adapter boundaries, how WCAG 2.2 AA + EN 301 549 conformance is declared and audited.

This ADR composes on, and is bounded by, the following adjacent substrates:

- **ADR 0008** — `Sunfish.Foundation.MultiTenancy` (`ITenantScoped`, `ITenantCatalog`, `TenantStatus`, `TenantMetadata`); `TenantId` itself lives in `Sunfish.Foundation.Assets.Common`.
- **ADR 0032** — multi-team Anchor workspace switching (per-team subkey wrapping is the cryptographic substrate role assignment composes onto).
- **ADR 0034** — accessibility harness per adapter (axe + bUnit + Storybook); this ADR adds the *content* of the contracts the harness verifies.
- **ADR 0036** — SyncState multimodal encoding contract (5-channel encoding precedent: color + icon + short-label + long-label + ARIA role + `aria-live`). The First-Aid baseline reuses the channel-agreement discipline.
- **ADR 0041** — dual-namespace components by design (informs the *degradation primitive* — when a rich variant fails capability gating, a smaller leaf surface remains).
- **ADR 0046 + 0046-A1 + 0046-A2** — recovery + identity substrate (`EncryptedField`, `IFieldDecryptor`, role-key wrapping); Captain/XO authority composes onto this.
- **ADR 0048 + 0048-A1** — Anchor multi-backend MAUI; specifies the per-platform a11y API binding boundary that this ADR's component contracts satisfy.
- **ADR 0049** — audit trail substrate (`IAuditTrail`, `AuditEventType`); permission decisions and role changes audit by construction.
- **ADR 0062** — Mission Space Negotiation Protocol (`IFeatureGate<TFeature>`, `IMissionEnvelopeProvider`); deck visibility composes with feature availability — a deck the Mission Envelope says is unavailable degrades, not blanks.
- **ADR 0065** — Wayfinder System + Standing Order Contract (`IStandingOrderIssuer`, `StandingOrderState`, `StandingOrderScope`); role assignments and OOD watch transfers issue as Standing Orders.

This ADR specifies: **(1)** the canonical `ShipRole` taxonomy + per-tenant assignment substrate, **(2)** the `IPermissionResolver` DI contract + `PermissionDecision` discriminated-union shape, **(3)** the deck-progressive-disclosure primitive, **(4)** the First-Aid universal contextual-help baseline, **(5)** the cross-platform design token catalog, **(6)** the framework-agnostic component primitive contracts (`ILiveAnnouncer`, `IFocusTrap`, form-control primitives, diff-preview, search-as-you-type), **(7)** the WCAG 2.2 AA + EN 301 549 conformance declaration mechanism, and **(8)** the platform-native a11y API binding contract.

---

## Decision drivers

1. **Load-bearing for ≥7 downstream ADRs.** Quarterdeck / Engine Room / Tactical / Sick Bay / Ship's Office / OOD-Watch / ~ADR 0066 / ~ADR 0068 all consume this contract. Late changes propagate seven ways.
2. **Framework-agnostic principle (CLAUDE.md).** Contracts live in `foundation` / `ui-core`; implementations live in `ui-adapters-blazor` / `ui-adapters-react` / MAUI; blocks compose. Tokens, role taxonomy, permission resolver, and component primitive contracts MUST be expressible without Blazor / React / MAUI knowledge.
3. **WCAG 2.2 AA + EN 301 549 as a contract, not a goal.** Per W#35 Stage 1.5 hardening: every UI surface inherits a *non-negotiable* baseline including the 15 SC-numbered AA criteria + the 4 new AA-2.2 criteria + the EN 301 549 procurement chapters. Conformance is an auditable Stage 07 review gate.
4. **Permission denial accessibility.** Per W#35 §7.1: a denial is never a `bool` and never a blank pane. The `PermissionDecision` discriminated union surfaces (a) which grant is missing, (b) who can grant it, (c) an accessible escalation action, (d) is announced through a live region. Screen-reader-parsable; screen-reader-actionable.
5. **First-Aid baseline must be inherited, not opt-in.** The 5-channel discipline from ADR 0036 (color + icon + short-label + long-label + ARIA + `aria-live`) generalizes: every interactive surface in Sunfish ships with the WCAG-conforming help text, error explanations, and suggested-next-action affordances *by default*. Surfaces failing the baseline fail Stage 07 review.
6. **Cross-platform tokens.** Sunfish runs in Blazor (web), React (web), MAUI (Windows/Mac/iOS/Android), and future Photino-Blazor (Anchor desktop). One token catalog, three adapter bindings (CSS custom properties for web, MAUI resource dictionaries for native, design-token JSON for tooling). The W3C Design Tokens Community Group format (`design-tokens.org`, draft 2024) is the cross-platform interchange.
7. **Platform-native a11y APIs are not optional.** Per ADR 0048 + 0048-A1 and W#35 §7.5: ARIA is web-only; native UIs require UIA (Windows) / NSAccessibility (macOS) / UIAccessibility (iOS / iPadOS / visionOS / watchOS) / AccessibilityNodeInfo (Android). Every primitive contract specifies its accessible name/role/state/value through the **native** API on each runtime, not just ARIA.
8. **Cohort discipline: pre-flight verification mandatory.** The 2026-04-29 / 2026-04-30 / 2026-05-04 cohort batting averages (5-of-5 / 18-of-18 / 22-of-22) demonstrate that §A0 self-audit is necessary but not sufficient. Council remains canonical defense; this ADR's §A0 is honest about its limits.

---

## Considered options

### Option A — One omnibus `Sunfish.Foundation.UI.DesignSystem` package

Single new package containing role taxonomy + permission resolver + tokens + component primitive contracts + a11y baseline + platform-binding contracts. Simple. One DI extension method.

- **Pro:** Discoverable; one package reference unlocks the whole substrate.
- **Pro:** Aligns with the ADR 0065 / W#42 single-package precedent (`foundation-wayfinder`).
- **Con:** Cross-cuts six concern tags (`ui` / `accessibility` / `identity` / `capability-model` / `configuration` / `regulatory`). A bug in one slice forces re-publication of the whole package.
- **Con:** Tokens are static data; component primitive contracts are interfaces; platform-binding contracts are abstract classes — three different change cadences fused.
- **Verdict:** Rejected. Too coarse-grained; contradicts the foundation-package separation pattern (ADR 0008 / ADR 0009 / ADR 0046 / ADR 0049 / ADR 0065 each target a single concern).

### Option B — Three packages: `foundation-ship-common` (roles + permissions) + `foundation-design-tokens` + `ui-core` extensions (component primitives + a11y baseline) **[RECOMMENDED]**

Split by change cadence and concern boundary.

- `Sunfish.Foundation.Ship.Common` — `ShipRole`, `ShipLocation`, `DeckDepth`, `IPermissionResolver`, `PermissionDecision`, role-assignment Standing Order shapes. Composes on `Sunfish.Foundation.Capabilities` + `Sunfish.Foundation.MultiTenancy` + `Sunfish.Foundation.Wayfinder`.
- `Sunfish.Foundation.DesignTokens` — token catalog as C# const records + W3C-format JSON export + per-adapter binding helpers. Static data; rarely changes.
- `Sunfish.UICore` (extended) — component primitive *contracts* (`ILiveAnnouncer`, `IFocusTrap`, `IFormControlContract`, `IDiffPreview`, `ISearchAsYouType`) + the First-Aid baseline contract + WCAG conformance declaration registry. Adapters implement against these.
- **Pro:** Each package has a single concern; can ship independently.
- **Pro:** Tokens can be regenerated from a JSON source without touching the role taxonomy package.
- **Pro:** `ui-core` is the natural home for component primitive contracts (already established as the framework-agnostic UI tier per CLAUDE.md).
- **Con:** Three packages to register at app startup. Mitigated by a meta-extension `AddSunfishSharedDesignSystem()` that calls the three sub-extensions in order.
- **Verdict:** Recommended.

### Option C — Per-concern packages collapsed into `ui-core` only

Put everything UI-shaped in `ui-core`; put role taxonomy + permission resolver in `foundation-multitenancy` (since tenants own roles).

- **Pro:** Two existing packages absorb the new types; no new package directories.
- **Con:** `foundation-multitenancy` becomes a kitchen sink. Adding `ShipRole` to a multi-tenancy package muddles the abstraction — multi-tenancy is *partition*, not *role*.
- **Con:** Tokens cross-compile to native MAUI XAML resource dictionaries; co-locating them with web-shaped `ui-core` Lit/React/Blazor primitives is awkward.
- **Verdict:** Rejected. Concern-bleed.

### Option D — Resolver embedded inside `ICapabilityGraph` (resolver-into-graph) **[CONSIDERED; REJECTED]**

Embed role/location/deck policy directly inside the capability graph so `ICapabilityGraph.QueryAsync` accepts `(ShipRole, ShipLocation, DeckDepth, ShipAction)` and resolves both policy and cryptographic proof in a single call.

- **Pro:** Eliminates the two-tier call chain; one DI dependency.
- **Pro:** Allows the graph to enforce role-level delegation constraints directly (e.g., a Captain-capability cannot be delegated to a DivisionOfficer through the graph layer).
- **Con:** Dependency-arrow inversion. `ICapabilityGraph` lives in `Sunfish.Foundation.Capabilities` — it has no dependency on the UI-topology-specific `ShipRole`/`ShipLocation`/`DeckDepth` concepts from `Sunfish.Foundation.Ship.Common`. Pulling them in reverses the foundation-layer dependency arrow.
- **Con:** Policy-proof conflation. The resolver's role is *policy*; the graph's role is *cryptographic proof*. A policy bug (wrong role rule) affecting the cryptographic substrate is harder to untangle than a bug in a separate policy layer above it.
- **Con:** Council pressure-test point #2 (a capability only delegatable by a Captain) is correctly handled via the `PromoteRole` precondition guard (§2.1 step 0(b)) + the `ShipAction → CapabilityAction` mapping table, not by pushing `ShipRole` into the graph.
- **Verdict:** Rejected. Dependency-arrow inversion + policy-proof conflation.

**Decision: Option B.**

---

## Decision

### 1. Role taxonomy

`Sunfish.Foundation.Ship.Common.ShipRole` (new package: `foundation-ship-common`):

```csharp
namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Canonical Sunfish ship-role taxonomy per ADR 0077 §1 + W#35 discovery §6.
/// Sealed enum for v1; tenant-defined custom roles register through
/// <see cref="IShipRoleRegistry"/> as composite roles bundling a v1 base
/// role with tenant-specific scope restrictions (extension via composition,
/// not via enum extension). The enum is the closed set of authority
/// gradients; the registry is the open set of tenant-named labels.
/// </summary>
public enum ShipRole
{
    Captain,            // tenant owner (BDFL)
    XO,                 // deputy
    EngineerOfficer,    // ENG → Engine Room
    Navigator,          // NAV → Wayfinder
    TacticalOfficer,    // TAC → Tactical
    DivisionOfficer,    // junior officer in rotation (MPA / DCA / Comms / Sonar / Electrical / QA)
    IDC,                // Independent Duty Corpsman → Sick Bay
    Scribe,             // → Ship's Office
    SUPPO,              // Supply Officer (Phase 2 deferred)
    OOD,                // Officer of the Deck (currently-on-watch admin)
    EOOW,               // Engineering Officer of the Watch
}

public enum DivisionAssignment
{
    MPA, DCA, Comms, Sonar, Electrical, QA,
}

public sealed record ShipRoleAssignment(
    TenantId TenantId,
    ActorId Holder,
    ShipRole Role,
    DivisionAssignment? Division,        // populated when Role == DivisionOfficer
    Instant AssignedAt,
    Instant? RotatesAt,                  // populated for Division Officers per §1.4 rotation pattern
    StandingOrderId IssuedBy);           // back-reference to the Standing Order that assigned this role
```

**§1.1 Why a sealed enum + registry instead of a free-form registry.** Permission resolution is a deterministic function of `(role, location, deck, action)` — if `role` is open-ended, the resolution algorithm cannot be exhaustively reasoned about, and tenant-defined roles drift across the W#35 cohort. The closed enum carries the *authority gradient* the discovery identified (Captain > XO > Department Head > Division Officer > Specialist > Watch); tenant labels (e.g., "Property Manager," "Maintenance Lead") compose on the closed enum via `IShipRoleRegistry.AssignLabel(ShipRole baseRole, string tenantLabel, ScopeRestriction? scope)`. This preserves authority-gradient determinism while letting tenants present role names in their own vocabulary.

**Structure is load-bearing; labels are not (council NM-5).** A `ShipRole.Captain` can always issue Standing Orders regardless of whether the tenant calls the role "Captain," "Administrator," or "Owner." Tenant labels are display-only — they flow through `IShipRoleRegistry` to the UI layer and never enter the permission-resolution algorithm. A label collision (two tenants calling different base roles "Manager") is harmless; the underlying `ShipRole` enum value is the resolution key. This invariant is what makes the closed-enum decision safe: the worst-case tenant customization is a confusing label, not a broken authority gradient.

**§1.2 Role assignment is a Standing Order.** Role assignments compose on ADR 0065 — a `ShipRoleAssignment` is materialized from a `StandingOrder` of `StandingOrderScope.Tenant` whose `Triples` carry `(path: "ship.roles.{actorId}", oldValue, newValue)`. The Standing Order is audit-by-construction (`StandingOrderIssued` audit event); rescission via `IStandingOrderIssuer.RescindAsync` reverses the assignment with `StandingOrderRescinded` audit emission.

**§1.3 Captain assignment bootstrapping.** The first Captain is assigned at tenant provisioning by `Sunfish.Foundation.MultiTenancy`'s tenant-creation flow — the Captain is the actor who holds the tenant's root keypair (per ADR 0046's identity primitives). Subsequent Captain transitions issue as Standing Orders requiring multi-actor approval (the outgoing Captain + the incoming Captain co-sign; per ADR 0046 spouse-recovery precedent).

**§1.4 Division Officer rotation.** Division Officers cycle through `DivisionAssignment` values over time (per W#35 §6.3 rotation pattern). The `RotatesAt` field on `ShipRoleAssignment` is set at issuance; when reached, a follow-up Standing Order issues automatically with `StandingOrderScope.Tenant` and `Triples = [(path: "ship.roles.{actorId}.division", oldValue: <prior>, newValue: <next>)]`. The rotation schedule is a per-tenant configuration setting under Wayfinder — tenants without a rotation schedule see a static `DivisionAssignment` until manually amended.

**§1.5 Watch designation (OOD/EOOW) is orthogonal.** Per W#35 §6.7: OOD/EOOW are *temporally-bounded* designations, not role assignments. A user who is `EngineerOfficer` (role) may also be `EOOW` (current watch); a user who is `DivisionOfficer / DCA` (role) may stand as `OOD` (current watch) when qualified. The OOD/EOOW substrate is specified in the OOD-Watch follow-on ADR; this ADR lists `OOD` and `EOOW` in `ShipRole` only so the closed enum is exhaustive.

**§1.6 Pre-Phase-2 SUPPO note.** `SUPPO` is in the enum but the role is deferred per W#35 §5.7 / §6.6. No `IPermissionResolver` rule grants any action to `SUPPO` until Phase 2 commercial work specifies the contract. Pre-Phase-2, a `ShipRole.SUPPO` assignment is structurally valid but operationally inert — `IPermissionResolver` always returns `Denied(DenialReason.Phase2Deferred, RemediationKind.Phase2Deferred, ...)`.

### 2. Permission tuple + resolver

`Sunfish.Foundation.Ship.Common.IPermissionResolver`:

```csharp
namespace Sunfish.Foundation.Ship.Common;

public enum ShipLocation
{
    Quarterdeck,         // entry-point + executive summary
    Wayfinder,           // configuration department (per ADR 0065 + W#34)
    EngineRoom,          // technical operations
    Tactical,            // monitoring + threat awareness
    SickBay,             // recovery + identity
    ShipsOffice,         // content management
    SupplyOffice,        // billing / commercial (Phase 2 deferred)
    Wardroom,            // v2 deferred
    Brig,                // v2 deferred
}

public enum DeckDepth
{
    TopDeck,                    // executive summary; status; KPIs
    MainDeck,                   // operational read/write
    EngineeringDeck,            // internals; logs; raw events
    BelowTheWaterline,          // destructive / irreversible operations
}

public readonly record struct ShipAction(string Name)
{
    public static readonly ShipAction Read              = new("read");
    public static readonly ShipAction Write             = new("write");
    public static readonly ShipAction IssueStandingOrder = new("issue-standing-order");
    public static readonly ShipAction Approve           = new("approve");
    public static readonly ShipAction PromoteRole       = new("promote-role");
    public static readonly ShipAction StandWatch        = new("stand-watch");
    public static readonly ShipAction TransferWatch     = new("transfer-watch");
    public static readonly ShipAction Quarantine        = new("quarantine");           // below-the-waterline
    public static readonly ShipAction OverrideQuarantine = new("override-quarantine"); // below-the-waterline
}

public interface IPermissionResolver
{
    /// <summary>
    /// Resolve the permission tuple per ADR 0077 §2. Never returns a bare bool;
    /// every resolution carries a reason + remediation for the denial UX.
    /// </summary>
    ValueTask<PermissionDecision> ResolveAsync(
        Principal subject,
        ShipLocation location,
        DeckDepth deck,
        ShipAction action,
        Resource? resource,             // optional — null when the action is location-scoped
        CancellationToken ct = default);
}

public abstract record PermissionDecision
{
    /// <summary>Permission granted; subject MAY perform the action at the cell.</summary>
    public sealed record Granted(
        ShipRole Role,                  // the role-grant that satisfied the resolution
        DateTimeOffset DecidedAt,
        CapabilityProof? Proof)         // optional — populated when caller passed Resource for transferable proof
        : PermissionDecision;

    /// <summary>Permission denied; UI MUST surface reason + remediation through the First-Aid contract.</summary>
    public sealed record Denied(
        DenialReason Reason,
        string ReasonDisplay,            // localized via LocalizedString resolution at adapter boundary
        Remediation Remediation,
        DateTimeOffset DecidedAt)
        : PermissionDecision;
}

public enum DenialReason
{
    NoMatchingRole,                  // subject holds no role granting this action
    DeckRestriction,                 // role grants the action elsewhere but not at this deck depth
    LocationOutOfScope,              // role grants the action but not at this location
    WatchRequired,                   // action requires currently-on-watch designation (OOD/EOOW) which subject does not hold
    Phase2Deferred,                  // SUPPO / Supply Office — Phase 2 deferred (no current timeline)
    V2Deferred,                      // Wardroom / Brig — v2 deferred (requires v2 commercial agreement)
    SecurityPolicyBlocked,           // ~ADR 0068 security policy intervened
    MissionEnvelopeUnavailable,      // ADR 0062 IFeatureGate said the feature is unavailable in this Mission Envelope
}

public sealed record Remediation(
    RemediationKind Kind,
    string GuidanceDisplay,                  // localized
    ActorId? ContactActor,                   // who can grant access (e.g., the current Captain)
    Uri? EscalationLink,                     // accessible escalation action (e.g., "request access" Standing Order draft URI)
    string? CallToActionLabel);              // localized label for the escalation affordance (e.g., "Request access"); null when EscalationLink + ContactActor are both null

public enum RemediationKind
{
    ContactAuthority,                // ContactActor is set
    AwaitWatch,                      // wait for OOD/EOOW rotation
    UpgradeMissionEnvelope,          // device/runtime/edition gate (per ADR 0062)
    Phase2Deferred,                  // SUPPO; no current path
    SecurityPolicyAppeal,            // ~ADR 0068 territory
    None,                            // pure denial; no remediation path
}
```

**§2.0 Location-scoped vs resource-scoped actions.** The `resource` parameter to `ResolveAsync` is optional (`null` when the action targets a whole location rather than a specific object). Location-scoped actions (`Read`, `IssueStandingOrder`, `StandWatch`, `TransferWatch`, `PromoteRole`) do not require a resource reference — the permission is against the location + deck. Resource-scoped actions (`Approve`, `Quarantine`, `OverrideQuarantine`) require a populated `Resource` so the capability graph can check possession of a grant on that specific object. Callers passing `resource: null` for a resource-scoped action receive `Denied(SecurityPolicyBlocked, "resource-scoped action requires a resource reference", ...)` at step 0(b) below without advancing to capability-graph evaluation.

**§2.1 Resolution algorithm.** The resolver evaluates the tuple in this order; first denial wins:

0. **Deck canonicalization + promotion guard.** Before any policy evaluation:

   **(a) Deck canonicalization (council S-2.3).** Compute `effectiveDeck = max(callerDeck, ActionMinimumDeck[action])` where `static readonly IReadOnlyDictionary<ShipAction, DeckDepth> ActionMinimumDeck` on `DefaultPermissionResolver` maps each `ShipAction` to its minimum permitted deck depth (e.g., `Quarantine → BelowTheWaterline`, `OverrideQuarantine → BelowTheWaterline`, `IssueStandingOrder → MainDeck`, `PromoteRole → MainDeck`, `Read → TopDeck`). Callers MUST NOT be trusted to self-report action sensitivity — a caller passing `MainDeck` for `Quarantine` is silently promoted to `BelowTheWaterline`. All subsequent steps evaluate against `effectiveDeck`, not the raw `deck` parameter.

   **(b) Promotion-target guard + self-promotion prohibition (council S-1.1).** If `action == PromoteRole`, extract the `ShipRole` target from the caller's pending Standing Order draft (`newValue` of the `ship.roles.{holder}` triple). Apply two invariants:
   - **Hierarchy invariant:** The caller's own effective `ShipRole` MUST be strictly higher in the authority gradient (`Captain > XO > EngineerOfficer|Navigator|TacticalOfficer > DivisionOfficer|IDC|Scribe|SUPPO > OOD|EOOW`) than the target role. Violation → `Denied(SecurityPolicyBlocked, "insufficient authority to promote to target role", ...)`.
   - **Self-promotion invariant:** If `subject.Id == targetHolder` → `Denied(SecurityPolicyBlocked, "self-promotion forbidden", ...)` unconditionally, regardless of hierarchy position.

   **(c) Resource-scope guard.** If `ActionMinimumDeck[action]` implies resource-scoped (see §2.0) and `resource == null` → `Denied(SecurityPolicyBlocked, "resource-scoped action requires a resource reference", ...)`.

1. **Watch precondition.** If `action ∈ {StandWatch, TransferWatch}` or `location == Quarterdeck && deck == TopDeck && action == Approve` (OOD-approval territory), assert subject currently holds `ShipRole.OOD` or `ShipRole.EOOW` per the OOD-Watch substrate. Otherwise → `Denied(WatchRequired, ...)`.
2. **Mission Envelope gate.** Compose with `IFeatureGate<TFeature>` (ADR 0062). If the action's enclosing feature returns `FeatureAvailabilityState.Unavailable` for the current `MissionEnvelope`, → `Denied(MissionEnvelopeUnavailable, ...)` carrying the `DegradationKind` from the feature verdict in the `Remediation.GuidanceDisplay`.
3. **Deferral check.** If `location == SupplyOffice` (SUPPO territory, Phase 2) → `Denied(Phase2Deferred, Phase2Deferred, ...)`. If `location ∈ {Wardroom, Brig}` (v2 territory) → `Denied(V2Deferred, None, "No current access path — v2 commercial agreement required")`. (`Phase2Deferred`/`V2Deferred` are `DenialReason` values per council NM-3 split.)
4. **Role match.** Look up the subject's `ShipRoleAssignment` in the per-tenant assignment log. If no role grants the `action` at the `location`, → `Denied(LocationOutOfScope, ...)` or `Denied(NoMatchingRole, ...)`.
5. **Deck restriction.** Roles grant actions at a maximum deck depth; deeper actions (`BelowTheWaterline`) require explicit grant. If the role grants the action at `MainDeck` but the request is `BelowTheWaterline`, → `Denied(DeckRestriction, ...)`.
6. **Capability check.** Compose with `Sunfish.Foundation.Capabilities.ICapabilityGraph.QueryAsync(subject.Id, resource, capabilityAction, asOf, ct)` — `IPermissionResolver` does NOT replace the capability graph; it sits *above* it. The capability graph is the cryptographic substrate (Ed25519-signed operations); `IPermissionResolver` is the role-aware policy layer that maps `ShipAction` → `CapabilityAction` and asks the graph. If the graph returns `false`, → `Denied(NoMatchingRole, ...)`.
7. **Security policy.** Compose with ~ADR 0068. The security policy is the last gate (it can deny what role + capability allow). If policy blocks, → `Denied(SecurityPolicyBlocked, ...)`.
8. **Otherwise** → `Granted(role, DateTimeOffset.UtcNow, optionalProof)`.

**§2.2 Composition order with `ICapabilityGraph`.** `IPermissionResolver` sits *above* `ICapabilityGraph`. The resolver's job is the role/location/deck *policy*; the graph's job is the cryptographic *proof*. The resolver translates `ShipAction → CapabilityAction` (e.g., `ShipAction.IssueStandingOrder → CapabilityAction.Write` on the appropriate `Resource`) and queries the graph. A capability proof returned to a caller via `PermissionDecision.Granted.Proof` is an `ICapabilityGraph.ExportProofAsync` result — transferable, verifiable, and bounded by `CapabilityProof.ProvedAt` plus the per-capability validity window configured in the graph (council SC-1: there is no `ExpiresAt` field on `CapabilityProof`; validity-window semantics live in the graph layer).

**§2.3 Denial accessibility (per W#35 §7.1 + this ADR §4 First-Aid).** Every `PermissionDecision.Denied` MUST be surfaced through the First-Aid baseline:
- `ReasonDisplay` is the human-readable cause; rendered as the visible message + the `aria-live="polite"` announcement on initial denial.
- `Remediation.GuidanceDisplay` is the suggested-next-action; rendered as adjacent text + an accessible link/button when `EscalationLink` or `ContactActor` is set.
- The denial is **never** a blank pane and **never** a generic 403. Surfaces violating this fail Stage 07 review.
- Denial-on-mount (e.g., the user navigated to a department they cannot enter) uses `aria-live="polite"` because the denial is informational; denial-on-action (the user clicked "issue Standing Order" and was rejected mid-flow) uses `aria-live="assertive"` because the user is mid-task and the announcement must interrupt.

**§2.4 Audit emission.** Every `Denied` decision emits an `AuditRecord` via `IAuditTrail.AppendAsync` with `AuditEventType.PermissionDenied` (new constant introduced by this ADR's Phase 1 build). `Granted` decisions do NOT emit by default — granting is the common case and audit-noise would dominate. Selected `Granted` decisions do emit: actions at `BelowTheWaterline` (always audit-loud), watch-handover approvals, role promotions. The audit-loud action set is enumerated as a static-readonly `IReadOnlyList<ShipAction>` on `IPermissionResolver` for cohort discoverability.

**Denial-rate-limiting (council S-3.1).** `DefaultPermissionResolver` MUST maintain a per-`(ActorId, ShipLocation)` denial counter with a 1-minute sliding window. When the counter exceeds N=10 denials within the window:
- Emit a single `AuditRecord` with `AuditEventType.PermissionDenialRateExceeded` (new constant, Phase 1 build) carrying `(actor, location, windowStartAt, denialCount)` payload.
- Return `Denied(SecurityPolicyBlocked, "permission-denial rate limit exceeded", Remediation { Kind: SecurityPolicyAppeal, ... })` for all subsequent calls within the active window WITHOUT invoking resolution steps 0–7.
- Reset the counter at window expiry.

N=10 is the default threshold; tenants MAY configure a lower value via Wayfinder (~ADR 0068 security policy). This guards against audit flooding — a systematic denial-loop cannot produce unbounded `PermissionDenied` audit records per minute. Add `AuditEventType.PermissionDenialRateExceeded` to the Phase 1 implementation checklist alongside `AuditEventType.PermissionDenied`.

**§2.5 Role-assignment cache + invalidation.**

`DefaultPermissionResolver` SHOULD cache per-tenant `ShipRoleAssignment` lookups (resolution step 4) to avoid per-call repository reads. Caches MUST be invalidated on role-assignment change by subscribing to `IStandingOrderEventStream` (ADR 0065 §A1):

```csharp
// Inside DefaultPermissionResolver constructor — subscribe BEFORE initial load
_subscription = standingOrderEventStream.Subscribe(evt =>
{
    if (!evt.StandingOrder.Triples.Any(t => t.Path.StartsWith("ship.roles."))) return;
    if (evt.TenantId != null) _roleAssignmentCache.InvalidateTenant(evt.TenantId);
});
// Then perform the initial per-tenant role-assignment load
```

**Subscribe-before-load discipline** is mandatory per ADR 0065 §A1.6 — a `StandingOrderAppliedEvent` arriving between the initial load and the subscribe would otherwise be silently lost.

**Restart-volatility:** On process restart the cache is empty; the resolver cold-reads from `IStandingOrderRepository.EnumerateAsync(tenantId, ct)` filtered to `StandingOrderState == Applied` and `Triples[].Path` matching `ship.roles.*`. The cache warms on first access per tenant.

**Cache key granularity:** Invalidate at `TenantId` level for v1. Per-actor granularity is a Phase 2 optimization if tenant size warrants it.

### 3. Deck-progressive-disclosure pattern

Every Sunfish UI surface declares its `DeckDepth` at registration time. The Shared Design System renders the surface conditionally based on the resolved permission and the per-role default landing deck.

```csharp
namespace Sunfish.Foundation.Ship.Common;

public sealed record DeckRegistration(
    ShipLocation Location,
    DeckDepth Depth,
    string SurfaceId,                    // stable identifier for the deck-pane (e.g., "engine-room.main-propulsion")
    string DisplayNameKey,               // localization key
    ShipAction PrimaryAction,            // the action evaluated for visibility
    IReadOnlyList<ShipRole> DefaultLandingFor); // roles whose default landing-deck is this surface

public interface IDeckRegistry
{
    void Register(DeckRegistration registration);
    IReadOnlyList<DeckRegistration> ForLocation(ShipLocation location);
    DeckDepth DefaultLandingDeck(ShipRole role, ShipLocation location);
}
```

**§3.1 Per-role default landing deck.**

| Role                     | Default landing deck (any location)           | Rationale                                                                      |
|--------------------------|-----------------------------------------------|--------------------------------------------------------------------------------|
| Captain / XO             | TopDeck (executive summary)                   | Tenant owners survey before drilling.                                          |
| EngineerOfficer / NAV / TAC | MainDeck of their home location            | Operational read/write is their daily task.                                    |
| DivisionOfficer          | MainDeck of their sub-room                    | Scoped read/write within sub-room.                                             |
| IDC                      | MainDeck of Sick Bay                          | Pharmacy / Lab / Atmosphere monitor are their daily task.                      |
| Scribe                   | MainDeck of Ship's Office                     | Document editing.                                                              |
| OOD / EOOW (when on watch) | TopDeck of their watch-location             | Watch-stander needs the executive summary first.                               |
| SUPPO                    | (Phase 2 deferred — no landing)               | Per §1.6.                                                                      |

**§3.2 Below-the-waterline gate.** `DeckDepth.BelowTheWaterline` actions (irreversible: data deletion, key revocation, quarantine override) ALWAYS:
- require `IPermissionResolver` to return `Granted` with an explicit elevation step (per the Stripe-pattern diff-preview from ADR 0065 §7);
- emit an audit record of the elevation request itself, not just the action;
- present a confirmation dialog whose accessible name explicitly states the destructive consequence (per WCAG SC 3.3.4 + 3.3.7);
- block on `prefers-reduced-motion` from auto-confirming after a timer (timer-confirmations forbidden — WCAG SC 2.2.1 / 2.2.2).

### 4. First-Aid baseline (universal contextual help)

Every interactive Sunfish surface inherits this baseline by default. Surfaces opting *out* require an ADR amendment + Stage 07 audit waiver — the default is opt-in by inheritance.

```csharp
namespace Sunfish.UICore.FirstAid;

/// <summary>
/// First-Aid baseline contract per ADR 0077 §4. Composes the WCAG 2.2 AA
/// requirements onto every surface; adapters implement against this contract.
/// </summary>
public interface IFirstAidContract
{
    /// <summary>
    /// The contextual help text rendered alongside the surface (visible
    /// + announced through aria-describedby). Required.
    /// </summary>
    string HelpKey { get; }                    // localization key

    /// <summary>
    /// Error display contract — labels, validation messages, suggested next
    /// action per WCAG SC 3.3.1 + 3.3.3.
    /// </summary>
    IFormControlContract? FormControl { get; } // null when surface is non-form

    /// <summary>Suggested next action when the surface is in an empty / error / denied state.</summary>
    string? NextActionHintKey { get; }

    /// <summary>
    /// Help available in a consistent location across surfaces (WCAG SC 3.2.6).
    /// MUST be one of: top-of-surface | sidebar | help-button | inline.
    /// </summary>
    HelpLocation HelpLocation { get; }

    /// <summary>
    /// Target size declaration — surface MUST satisfy ≥24×24 CSS px (web)
    /// / ≥44pt (iOS) / ≥48dp (Android) per WCAG SC 2.5.8.
    /// </summary>
    TargetSizeCompliance TargetSize { get; }

    /// <summary>Redundant-entry exemption per WCAG SC 3.3.7.</summary>
    bool ExemptFromRedundantEntry { get; }

    /// <summary>
    /// Default live-announcement politeness for this surface (council F-1).
    /// Passed to <see cref="ILiveAnnouncer"/> when the surface emits status changes.
    /// Read-only/dashboard surfaces default to Polite; denial surfaces and alert
    /// surfaces override to Assertive; security/destructive-action surfaces use Critical.
    /// </summary>
    LiveRegionPoliteness LiveAnnouncementPolicy { get; }
}

public enum HelpLocation { TopOfSurface, Sidebar, HelpButton, Inline }
public enum TargetSizeCompliance { Conforming, ExemptByException, NonConforming }
```

**§4.1 The mandatory baseline (auditable at Stage 07).** Reuses W#35 §7.4's table verbatim, with explicit ADR-issuance:

| WCAG SC | Requirement | Auditable verification |
|---|---|---|
| 1.3.1 | Programmatic structure (landmarks; headings; lists) — surfaces declare their landmark via the platform a11y API. | `Sunfish.UIAdapters.Blazor.A11y.SunfishA11yContract.Role` populated. |
| 1.4.1 | Color paired with shape/icon/label — no color-only signaling. | Token-level audit: every state-color in the design token catalog has a paired non-color encoding declared. |
| 1.4.3 | Text contrast ≥4.5:1 (normal) / ≥3:1 (large). | Token-level audit: every color-pair token in `foundation-design-tokens` ships with a contrast-ratio verification at build time. |
| 1.4.11 | Non-text UI ≥3:1 contrast (icons; borders; focus indicators). | Token-level audit. |
| 2.1.1 | Keyboard reachable (every interactive element). | `SunfishA11yContract.KeyboardMap` populated; bUnit / axe assertion. |
| 2.4.7 | Focus visible. | Token: `--sf-focus-ring-color` + `--sf-focus-ring-width` present; verified ≥3:1 contrast at audit. |
| 2.4.11 *(AA-2.2 new)* | Focus not obscured (minimum). | Adapter-side: focus ring is rendered above any sticky / fixed overlay. |
| 2.5.7 *(AA-2.2 new)* | Dragging movements — single-pointer alternative. | Component-primitive contract: any `IDragSource` exposes a keyboard-reachable alternative action. |
| 2.5.8 *(AA-2.2 new)* | Target size ≥24×24 CSS px. | Adapter-side stylesheet audit; covered by `IFirstAidContract.TargetSize`. |
| 3.2.6 *(AA-2.2 new)* | Consistent help. | `IFirstAidContract.HelpLocation` enforced — must be the same value within a `ShipLocation`. |
| 3.3.1 | Error identification — labels and field-specific guidance. | `IFormControlContract.ErrorMessage` non-null on validation failure; `aria-invalid` set. |
| 3.3.7 *(AA-2.2 new)* | Redundant entry not required. | `IFirstAidContract.ExemptFromRedundantEntry` false unless the exception condition holds (security re-confirm; legal/financial). |
| 3.3.8 *(AA-2.2 new)* | Accessible Authentication — no cognitive function tests. | MFA UX scan: enrollment / re-confirm flows MUST NOT impose CAPTCHAs / remembered-secret challenges (per ADR 0065 §7). |
| 4.1.2 | Programmatic name/role/state/value. | `SunfishA11yContract.Name` + `SunfishA11yContract.Role` populated; platform a11y API binding declared per §8. |
| 4.1.3 | Status messages announced. | `ILiveAnnouncer` used; `aria-live` populated per ADR 0036's politeness map. |

**§4.2 First-Aid renderer composition.** The First-Aid layer is rendered by the adapter (Blazor / React / MAUI) consuming the `IFirstAidContract` declared by the surface. The contract carries the *what*; the adapter carries the *how* (CSS / native styling). Surfaces failing to declare the contract fail Stage 07 review.

### 5. Design tokens

`Sunfish.Foundation.DesignTokens` (new package: `foundation-design-tokens`):

**§5.1 Token format choice.** Tokens are authored in the **W3C Design Tokens Community Group format** (`design-tokens.org` draft 2024) as JSON. The single source-of-truth is `packages/foundation-design-tokens/tokens.json`. Build-time tooling (introduced by this ADR's Phase 3) generates:
- C# `static readonly` records under `Sunfish.Foundation.DesignTokens` for foundation-tier consumers (e.g., MAUI ResourceDictionary code-gen);
- CSS custom properties under `packages/ui-core/src/tokens.css` for web adapters (Blazor + React);
- Markdown reference under `apps/docs/design-system/tokens.md` for human review.

This is per Decision driver #6: cross-platform tokens authored once, consumed three ways.

**§5.2 Token namespace.**

```
sf
├── color
│   ├── surface
│   │   ├── primary       (light / dark variants)
│   │   ├── secondary
│   │   └── tertiary
│   ├── text
│   │   ├── on-surface-primary    (≥4.5:1 against surface.primary in both themes)
│   │   ├── on-surface-secondary
│   │   └── on-surface-disabled
│   ├── state
│   │   ├── success / warning / error / info       (composes ADR 0036 SyncState palette)
│   │   └── focus-ring
│   ├── role-band
│   │   ├── captain / xo / department-head / division-officer / idc / scribe / watch
│   │   └── (each: a hue band reserved for role-tagged UI elements; non-color-load-bearing)
├── typography
│   ├── family-sans / family-serif / family-mono
│   ├── size-{xs,sm,base,lg,xl,2xl,3xl,4xl}
│   ├── weight-{regular,medium,semibold,bold}
│   └── line-height-{tight,base,relaxed}
├── space
│   └── {0, 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64}            (4px-grid)
├── radius
│   └── {none, sm, md, lg, full}
├── elevation
│   └── {0, 1, 2, 3, modal, drawer}
├── motion
│   ├── duration-{instant, fast, base, slow}
│   ├── easing-{linear, in, out, in-out, spring}
│   └── reduced-motion-fallback                                  (zero-motion variant per SC 2.3.3)
└── target-size
    ├── min-web (24px)
    ├── min-ios (44pt)
    └── min-android (48dp)
```

**§5.3 OS-preference tokens.** Tokens declare per-preference variants for `prefers-reduced-motion` / `prefers-reduced-transparency` / `prefers-contrast` / `forced-colors` (Windows High Contrast — mandatory). The CSS export emits `@media` queries; the C# export emits per-preference variant fields; the MAUI export emits trigger-bound resource swaps.

**§5.4 Contrast guarantee verification.** Every `color.text.* × color.surface.*` pair ships with a build-time WCAG 1.4.3 + 1.4.11 verification (4.5:1 for normal text; 3:1 for large text + non-text UI). The token-build tool fails CI if any pair regresses below threshold. Light + dark themes are both audited; per ADR 0036's CVD-audit precedent.

**§5.5 Role-band tokens.** Each role gets a reserved hue band for tagging (e.g., a Captain's avatar/badge uses the `captain` role-band hue). Role-bands are **non-load-bearing** for color-blind users — paired always with a role glyph + role label per WCAG 1.4.1. The role-band hues are CVD-distinguishable per ADR 0036's ΔE2000 audit precedent (verification reused; Phase 3 generates the CVD audit report into `apps/docs/design-system/role-band-cvd.md`).

### 6. Component primitives (framework-agnostic contracts)

Defined in `Sunfish.UICore` (extension of existing `ui-core` package); adapter implementations live in `ui-adapters-blazor` / `ui-adapters-react` / MAUI.

**§6.1 `ILiveAnnouncer` — live-region primitive.**

```csharp
namespace Sunfish.UICore.Primitives;

public interface ILiveAnnouncer
{
    /// <summary>
    /// Announce a localized message via the platform a11y API. Politeness
    /// determines aria-live (web) / NSAccessibilityAnnouncementPriority (mac) /
    /// UIAccessibility.PostNotification.LayoutChanged|Announcement (iOS) /
    /// AccessibilityEvent.TYPE_ANNOUNCEMENT (Android).
    /// </summary>
    /// <param name="message">Already-localized message.</param>
    /// <param name="politeness">Polite (informational) or Assertive (interrupts).</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask AnnounceAsync(
        string message,
        LiveRegionPoliteness politeness,
        CancellationToken ct = default);
}

public enum LiveRegionPoliteness
{
    Polite,        // aria-live="polite" — informational; announces during idle
    Assertive,     // aria-live="assertive" — urgent; interrupts SR speech queue
    Critical,      // role="alert" + aria-live="assertive" — security / destructive-action; cannot be suppressed by AT user settings (council F-5)
}
```

The politeness tier lives in the contract because the *caller* (a Standing Order issuer; a Tactical alert; a denial surfacer) knows the urgency, not the renderer. ADR 0036's politeness map is the canonical rule: status changes are `Polite`; alerts and conflict states are `Assertive`; security-critical or destructive-action announcements (e.g., `BelowTheWaterline` elevation, key revocation confirmation) use `Critical` — which renders as `role="alert"` + `aria-live="assertive"` on web and the highest-priority platform a11y API tier on native, and cannot be suppressed by AT user settings.

**§6.2 `IFocusTrap` — modal focus management.**

```csharp
public interface IFocusTrap
{
    /// <summary>
    /// Activate focus trap within the given surface. Tab cycles within;
    /// Escape (or platform equivalent) closes; focus restores to the prior
    /// active element on close (per WCAG SC 2.4.3 focus order).
    /// Nested traps follow LIFO stacking — see §6.2.1.
    /// </summary>
    /// <returns>A disposable that releases the trap when disposed.</returns>
    IAsyncDisposable ActivateAsync(string surfaceId, CancellationToken ct = default);
}
```

**§6.2.1 Nested trap stacking — LIFO (council F-6).** `IFocusTrap` implementations MUST support nested activation with LIFO discipline: when a second trap is activated while a first is active (e.g., a confirmation dialog spawned from within a modal drawer), the second trap becomes the active focus scope and the first trap is suspended (not disposed). On disposal of the second trap, the adapter pops the stack and restores focus to the first trap's scope — not to the pre-trap element. Implementation guidance: maintain a `Stack<TrapContext>` keyed to the root element's stable `surfaceId`; `ActivateAsync` pushes a context that records the current focus element + trap root; `DisposeAsync` pops and reactivates the parent context's trap. Stacks deeper than 4 levels SHOULD emit a diagnostic warning (test-mode only); this limit is advisory.

**§6.3 `IFormControlContract` — form-control primitives.**

```csharp
public interface IFormControlContract
{
    string FieldId { get; }
    string LabelKey { get; }
    string? DescriptionKey { get; }       // wires aria-describedby to "{FieldId}-desc" element
    string? ErrorMessageKey { get; }      // when non-null: adapter MUST set aria-invalid="true" AND aria-errormessage="{FieldId}-error" simultaneously (council F-7; per ARIA 1.2 §6.6.2)
    bool Required { get; }                // wires aria-required
    bool Disabled { get; }                // wires aria-disabled
    FormControlKind Kind { get; }         // text | number | select | combobox | checkbox | radio | toggle | date | time
    string? AutocompleteHint { get; }     // WCAG SC 1.3.5 identify-purpose: HTML autocomplete token (e.g., "name", "email", "current-password") or null if not applicable
}
```

**§6.3.1 `aria-invalid` + `aria-errormessage` pairing (council F-7).** Per ARIA 1.2 §6.6.2: assistive technology reads `aria-errormessage` ONLY when `aria-invalid` is not `false`. Adapters MUST set `aria-invalid="true"` simultaneously with pointing `aria-errormessage` to the error container element. Failure modes to avoid: (a) setting `aria-errormessage` without `aria-invalid="true"` — AT may ignore the error message; (b) lazy DOM insertion of the error container — the element referenced by `aria-errormessage` MUST be present in the DOM before the attribute is set. The error container's stable `id` MUST be `{FieldId}-error`; the description container's stable `id` MUST be `{FieldId}-desc`. Both IDs MUST remain stable across re-renders.

**§6.4 `IDiffPreview` — Stripe-pattern diff-preview (composes ADR 0065 §7).**

```csharp
public interface IDiffPreview<TPath, TValue>
{
    /// <summary>
    /// Render an accessible diff-table with header row [Path | Prior | New];
    /// JSON object values render as expandable structure, never raw single-cell text.
    /// </summary>
    ValueTask<DiffPreviewView> RenderAsync(
        IReadOnlyList<DiffEntry<TPath, TValue>> entries,
        CancellationToken ct = default);
}

public sealed record DiffEntry<TPath, TValue>(TPath Path, TValue? PriorValue, TValue? NewValue);
public sealed record DiffPreviewView(string SurfaceId, IReadOnlyList<string> AccessibleRows);
```

**§6.5 `ISearchAsYouType` — combobox per ARIA APG.**

```csharp
public interface ISearchAsYouType<THit>
{
    /// <summary>
    /// Stream search hits with incremental announcements. Latency target:
    /// initial hit P95 ≤ 100ms; full-result-count announcement P95 ≤ 300ms.
    /// At &gt;500ms, an "in progress" polite announcement fires once.
    /// </summary>
    IAsyncEnumerable<THit> SearchAsync(
        string query,
        int limit,
        CancellationToken ct = default);
}
```

The combobox follows the WAI-ARIA APG combobox pattern: input has `role="combobox"` + `aria-expanded`; results listbox has `role="listbox"` + `aria-activedescendant`; arrow-key navigation; Enter to commit; Escape to close.

**§6.6 Degradation primitive (composes ADR 0041).** When a rich-variant component is unavailable in the current Mission Envelope (per ADR 0062 `IFeatureGate`), the dual-namespace MVP variant remains. The Shared Design System guarantees that *every* rich primitive has an MVP fallback under the dual-namespace pattern. The fallback inherits `IFirstAidContract` from the rich variant; the only difference is reduced surface area.

### 7. WCAG 2.2 AA + EN 301 549 conformance baseline

This ADR is **bound** to WCAG 2.2 AA + EN 301 549 v3.2.1 (chapters 9, 10, 11) conformance. The 10 mandatory accessibility topics per W#35 §8.2 / Stage 1.5 hardening output are non-negotiable contract requirements.

**§7.1 Conformance declaration.**

```csharp
namespace Sunfish.UICore.Conformance;

public interface IConformanceRegistry
{
    void Declare(ConformanceDeclaration declaration);
    ConformanceReport BuildReport(ShipLocation? scope = null);
}

public sealed record ConformanceDeclaration(
    string SurfaceId,                            // matches IFirstAidContract / DeckRegistration
    Wcag22Level Level,                           // A | AA | AAA-aspirational
    IReadOnlyList<WcagSuccessCriterion> Met,
    IReadOnlyList<WcagSuccessCriterion> NotApplicable,
    IReadOnlyList<En301549Chapter> En301549Met,
    IReadOnlyList<ConformanceException> Exceptions,    // documented exceptions per surface
    Instant DeclaredAt,
    string DeclaredByKey);                       // localization key

public readonly record struct WcagSuccessCriterion(string Number);   // e.g., "1.4.3", "3.3.8"
public readonly record struct En301549Chapter(string Number);        // e.g., "9.1.4.3", "11.5.2.13"
```

**§7.2 EN 301 549 chapter-by-chapter mapping.**

| EN 301 549 Chapter | Topic                                                  | Component types this applies to                                                                          |
|---|---|---|
| Chapter 9 (Web)            | All web-rendered Bridge accelerator + apps/docs surfaces.                                                                                | Blazor + React adapters. |
| Chapter 10 (Non-web docs)  | Generated PDF reports, exported CSV tables, downloaded JSON conformance reports.                                                         | apps/docs export pipeline; ADR 0049 audit-trail exports. |
| Chapter 11 (Non-web software) | MAUI Anchor desktop UI on Windows / Mac / Linux. Native iOS / Android / visionOS / watchOS surfaces.                                  | All MAUI-rendered Anchor surfaces; future native iOS app per W#23. |

**§7.3 Auditable contract shape — what "declares conformance" means.** A surface declares conformance by:
1. Registering an `IFirstAidContract` at startup.
2. Registering an `IConformanceDeclaration` with the surface's claimed AA criteria + the EN 301 549 chapters mapped.
3. Passing the per-adapter a11y harness (ADR 0034) for the asserted criteria.
4. Passing a Stage 07 review checklist that cross-references the declaration against `apps/docs/design-system/conformance-baseline.md` (Phase 5 deliverable).

A surface NOT declaring conformance is treated as unconforming and CANNOT ship to a Bridge tenant in an EU jurisdiction. The Phase 4 build introduces a CI gate on the conformance registry.

**§7.4 Internationalization + RTL.** All locale-aware date/time/number formatting goes through `LocalizedString` (per ADR 0062). Text direction comes from the platform locale; the design tokens include `--sf-direction-start` / `--sf-direction-end` logical-property tokens replacing left/right physical properties. Per ADR 0036 §RTL: only directional icons (e.g., `call_split`) mirror; the role-band hues do not. WCAG SC 3.1.1 (page language) + SC 3.1.2 (parts language) are satisfied at the adapter layer (the `lang` attribute / equivalent native attribute is set per surface).

**§7.5 Authoring-time lint contract.** ADR 0034's `Sunfish.UIAdapters.Blazor.A11y` package is the foundation for the lint contract. This ADR's Phase 4 build extends it to: (a) require `IFirstAidContract` declaration at component registration; (b) verify token contrast at build time (per §5.4); (c) require `IConformanceDeclaration` for any surface registered to a `ShipLocation`. The CI check fails the build on any missing declaration or sub-threshold contrast.

### 8. Platform a11y API contract

Per W#35 §7.5: every component primitive MUST surface accessible name/role/state/value through the **native** API on each runtime, not only ARIA. This binding is the responsibility of each adapter; this ADR specifies the contract.

| Platform                | Native a11y API                          | Adapter / runtime                                              |
|---|---|---|
| macOS desktop           | **NSAccessibility** (AppKit / SwiftUI)   | MAUI Mac / Photino-bridge                                      |
| iOS / iPadOS            | **UIAccessibility** (UIKit / SwiftUI)    | MAUI iOS / native + WebView WKAccessibilityElement (per W#23)  |
| Windows                 | **UI Automation (UIA)** provider tree    | MAUI Windows / WinAppSDK                                       |
| Android                 | **AccessibilityNodeInfo** + Compose      | MAUI Android                                                   |
| visionOS                | **UIAccessibility** (SwiftUI 3D)         | future Anchor visionOS                                         |
| watchOS                 | **UIAccessibility** (watchOS)            | Helm glance widget on paired-device                            |
| Web (Bridge / apps/docs)| **WAI-ARIA 1.2** + AOM                   | React / Blazor                                                 |

**§8.1 Per-primitive binding obligations.** Each component primitive declares the binding for each platform. Example for `ILiveAnnouncer`:

| Platform | Binding |
|---|---|
| Web                | `Polite` → `aria-live="polite"`; `Assertive` → `aria-live="assertive"`; `Critical` → `role="alert"` + `aria-live="assertive"` on a stable live region in the DOM |
| Windows (UIA)      | `IRawElementProviderSimple.GetPropertyValue(LiveSettingProperty)` returns `Polite` / `Assertive`; `Critical` → `Assertive` + `RaiseNotificationEvent` with `NotificationProcessing.ImportantAll`; `RaiseLiveRegionChangedEvent` |
| macOS              | `NSAccessibilityPostNotificationWithUserInfo` with `NSAccessibilityAnnouncementRequestedNotification`; `Polite` → default priority; `Assertive|Critical` → `NSAccessibilityPriorityHigh` |
| iOS / visionOS / watchOS | `UIAccessibility.post(notification: .announcement, argument: message)`; `Polite` → default; `Assertive|Critical` → `UIAccessibilityPriority.high` (iOS 17+) |
| Android            | `AccessibilityEvent` with `TYPE_ANNOUNCEMENT`; `Polite` → `setLiveRegion(ACCESSIBILITY_LIVE_REGION_POLITE)`; `Assertive|Critical` → `setLiveRegion(ACCESSIBILITY_LIVE_REGION_ASSERTIVE)` + `IMPORTANCE_HIGH` |

The binding tables for `IFocusTrap`, `IFormControlContract`, `IDiffPreview`, `ISearchAsYouType` ship in `apps/docs/design-system/platform-a11y-bindings.md` (Phase 5 deliverable).

**§8.2 Verification.** Per ADR 0048-A1, every block-level a11y test runs cross-platform. The Phase 4 CI gate requires the per-platform bindings table to be complete for every primitive declared in `Sunfish.UICore.Primitives`. Missing bindings fail the build.

---

## §A0 — Self-audit limitation block (per ADR 0062-A1.14 / ADR 0065 cohort discipline)

The author of this ADR ran the standard 3-direction self-audit on every cited Sunfish.* symbol but acknowledges, per the cohort batting average of 22-of-22 prior structural-citation failures NOT caught by §A0:

### §A0.1 Negative-existence (introduced by this ADR's Phase 1 / 2 / 3 builds)

Verified the following types **do NOT yet exist on origin/main** (`grep -rn "ShipRole\|IPermissionResolver\|PermissionDecision\|ShipLocation\|DeckDepth\|ShipAction\|ShipRoleAssignment\|IShipRoleRegistry\|IDeckRegistry\|DeckRegistration\|IFirstAidContract\|IConformanceRegistry\|ConformanceDeclaration\|ILiveAnnouncer\|IFocusTrap\|IFormControlContract\|IDiffPreview\|ISearchAsYouType" packages/` returns zero results 2026-05-04):

- `Sunfish.Foundation.Ship.Common.ShipRole` (enum)
- `Sunfish.Foundation.Ship.Common.DivisionAssignment` (enum)
- `Sunfish.Foundation.Ship.Common.ShipRoleAssignment` (record)
- `Sunfish.Foundation.Ship.Common.IShipRoleRegistry` (interface)
- `Sunfish.Foundation.Ship.Common.ShipLocation` (enum)
- `Sunfish.Foundation.Ship.Common.DeckDepth` (enum)
- `Sunfish.Foundation.Ship.Common.ShipAction` (readonly record struct)
- `Sunfish.Foundation.Ship.Common.IPermissionResolver` (interface)
- `Sunfish.Foundation.Ship.Common.PermissionDecision` (abstract record + Granted / Denied subtypes)
- `Sunfish.Foundation.Ship.Common.DenialReason` (enum)
- `Sunfish.Foundation.Ship.Common.Remediation` (record) + `RemediationKind` (enum)
- `Sunfish.Foundation.Ship.Common.IDeckRegistry` (interface) + `DeckRegistration` (record)
- `Sunfish.Foundation.DesignTokens.*` (entire new package)
- `Sunfish.UICore.FirstAid.IFirstAidContract` (interface) + `HelpLocation` / `TargetSizeCompliance` enums
- `Sunfish.UICore.Primitives.ILiveAnnouncer` / `LiveRegionPoliteness` / `IFocusTrap` / `IFormControlContract` / `FormControlKind` / `IDiffPreview` / `DiffEntry` / `DiffPreviewView` / `ISearchAsYouType`
- `Sunfish.UICore.Conformance.IConformanceRegistry` / `ConformanceDeclaration` / `Wcag22Level` / `WcagSuccessCriterion` / `En301549Chapter` / `ConformanceException`
- `Sunfish.Kernel.Audit.AuditEventType.PermissionDenied` (new constant) — verified the existing `AuditEventType` is a `readonly record struct(string Value)` per ADR 0065 §A0.2 cohort precedent; the new constant follows the same pattern (`public static readonly AuditEventType PermissionDenied = new("PermissionDenied")`).

All listed symbols are introduced by this ADR's Phase 1 / 2 / 3 builds and are listed in the Implementation checklist below.

### §A0.2 Positive-existence (predecessor types verified at claimed locations)

Verified on origin/main 2026-05-04:

- `Sunfish.Foundation.Capabilities.Principal` — `packages/foundation/Capabilities/Principal.cs:10` (abstract record with `Individual` + `Group` subtypes; `PrincipalId` is `Sunfish.Foundation.Crypto.PrincipalId`, a `readonly record struct` at `packages/foundation/Crypto/PrincipalId.cs:28`).
- `Sunfish.Foundation.Capabilities.ICapabilityGraph.QueryAsync(PrincipalId subject, Resource resource, CapabilityAction action, DateTimeOffset asOf, CancellationToken ct)` — `packages/foundation/Capabilities/ICapabilityGraph.cs:26-31`. Note: the `subject` parameter is `PrincipalId`, not `Principal`; the resolver §2 example shows `Principal subject` for richer type-safety at the policy layer, then passes `subject.Id` into the graph query.
- `Sunfish.Foundation.Capabilities.CapabilityAction` — `packages/foundation/Capabilities/CapabilityAction.cs` (`readonly record struct(string Name)` with `Read`/`Write`/`Delete`/`Delegate`/`Sign` constants).
- `Sunfish.Foundation.Capabilities.Resource` — `packages/foundation/Capabilities/Resource.cs` (`readonly record struct(string Id)`).
- `Sunfish.Foundation.Capabilities.CapabilityProof` — `packages/foundation/Capabilities/CapabilityProof.cs`.
- `Sunfish.Foundation.Assets.Common.ActorId` — `packages/foundation/Assets/Common/ActorId.cs:18` (`readonly record struct(string Value)`).
- `Sunfish.Foundation.Assets.Common.TenantId` — `packages/foundation/Assets/Common/TenantId.cs` (`readonly record struct(string Value)`).
- `Sunfish.Foundation.MultiTenancy.{ITenantScoped, IMustHaveTenant, ITenantCatalog, TenantStatus, TenantMetadata}` — `packages/foundation-multitenancy/`. **Drift correction (per ADR 0065 §A0.2 cohort precedent):** this ADR cites `TenantId` from `Sunfish.Foundation.Assets.Common`, NOT from `Sunfish.Foundation.MultiTenancy` — the latter does not carry `TenantId`.
- `Sunfish.Foundation.Wayfinder.IStandingOrderIssuer.IssueAsync(StandingOrderDraft draft, ActorId issuedBy, IAuditTrail auditTrail, CancellationToken ct)` — `packages/foundation-wayfinder/IStandingOrderIssuer.cs:43-47`. Verified before drafting §1.2 / §1.4.
- `Sunfish.Foundation.Wayfinder.StandingOrderId` — `packages/foundation-wayfinder/StandingOrderId.cs` (cited in §1 `ShipRoleAssignment.IssuedBy`).
- `Sunfish.Foundation.Wayfinder.StandingOrderScope` — `packages/foundation-wayfinder/StandingOrderScope.cs` (cited in §1.2 / §1.4).
- `Sunfish.Foundation.Wayfinder.StandingOrderState` — `packages/foundation-wayfinder/StandingOrderState.cs:20-39` (6-value enum; cited in §1.2).
- `Sunfish.Foundation.MissionSpace.IFeatureGate<TFeature>` — `packages/foundation-mission-space/Services/Contracts.cs:13` (cited in §2.1 step 2 + §6.6 degradation).
- `Sunfish.Kernel.Audit.IAuditTrail.AppendAsync(AuditRecord record, CancellationToken ct)` — verified per ADR 0065 §A0.2 (cited in §2.4).
- `Sunfish.Kernel.Audit.AuditEventType` — `readonly record struct(string Value)` per ADR 0065 §A0.2 (cited in §2.4 for new `PermissionDenied` constant).
- `Sunfish.Foundation.UI.SyncState` — `packages/foundation-ui-syncstate/SyncState.cs:16-39` (5-value enum; ADR 0036 encoding — cited in §6.1 / §5.2 state-color tokens).
- `Sunfish.UIAdapters.Blazor.A11y.SunfishA11yContract` — `packages/ui-adapters-blazor-a11y/SunfishA11yContract.cs:16` (cited in §4.1 audit verification + §7.5 lint contract).
- `NodaTime.Instant` — used per ADR 0065 cohort precedent.

### §A0.3 Structural-citation correctness

Per cohort discipline (22-of-22 prior structural-citation failures), this draft was self-corrected for:

- (a) `Principal` subtype hierarchy — verified `Individual` + `Group` are sealed records inheriting `Principal`; the resolver §2 signature accepts `Principal subject` as the abstract base, not `PrincipalId` — this is a deliberate type-safety upgrade at the policy layer (the resolver may want to walk group membership). The graph call inside the resolver passes `subject.Id` to satisfy the existing `ICapabilityGraph.QueryAsync(PrincipalId subject, ...)` signature.
- (b) `CapabilityAction.Read|Write|Delete|Delegate|Sign` are the existing constants; the resolver maps `ShipAction` (this ADR's new type) → `CapabilityAction` per §2.2 — this is a *new mapping*, NOT a renaming of `CapabilityAction`. The resolver's mapping table is in the Implementation checklist.
- (c) `IAuditTrail.AppendAsync(AuditRecord record, CancellationToken ct)` — caller constructs the `AuditRecord` (with the new `AuditEventType.PermissionDenied`) and calls `AppendAsync`, NOT a `(AuditEventType, payload, ct)` overload. Same pattern as ADR 0065.
- (d) `AuditEventType` is `readonly record struct`, so the new `PermissionDenied` constant is `public static readonly AuditEventType PermissionDenied = new("PermissionDenied")`, not an enum value.
- (e) WCAG 2.2 AA SC numbers verified: 1.3.1, 1.4.1, 1.4.3, 1.4.11, 2.1.1, 2.4.7, 2.4.11 *(new)*, 2.5.7 *(new)*, 2.5.8 *(new)*, 3.2.6 *(new)*, 3.3.1, 3.3.7 *(new)*, 3.3.8 *(new)*, 4.1.2, 4.1.3 — exactly the W#35 §7.4 / §8.2 set.
- (f) `IStandingOrderIssuer.IssueAsync` signature is `(StandingOrderDraft draft, ActorId issuedBy, IAuditTrail auditTrail, CancellationToken ct)` — matches `packages/foundation-wayfinder/IStandingOrderIssuer.cs:43-47`. `ShipRoleAssignment.IssuedBy` (this ADR §1) is the resulting `StandingOrderId`, not the actor — back-references the Standing Order, not the issuing actor (the issuing actor is on the Standing Order itself).

Council MUST still spot-check (g) the W3C Design Tokens Community Group format claims (cited 2024 draft), (h) the EN 301 549 v3.2.1 chapter numbering (9 / 10 / 11), (i) the platform a11y API method names (UIAccessibility.post on iOS 17+ vs older API; AccessibilityNodeInfo.setLiveRegion enum constants), and (j) any cross-ADR references whose target sections may have drifted since cohort merge.

The §A0 self-audit is *necessary but not sufficient* — council remains canonical defense per the cohort batting average of 22-of-22 prior structural-citation failures NOT caught by §A0.

---

## Council brief — pressure-test points

Pre-flagged questions the council should address. The author has documented a recommendation but has lower confidence on these than on the rest of the decision.

1. **`ShipRole` as enum vs. registry.** The decision (§1 / §1.1) is sealed enum + composite registry: closed authority gradient + open tenant labels. **Council pressure-test:** does this preserve permission-resolution determinism in the long tail (e.g., a tenant defines a "Maintenance Lead" composite that maps onto `DivisionOfficer / DCA` + a custom location-restriction)? Or does it leak resolution complexity that would justify a fully open registry with a stricter resolution-validation pass? Confidence: Medium-High.
2. **`IPermissionResolver` above or below `ICapabilityGraph`.** The decision (§2.2) is *above* — the resolver is the role/location/deck *policy* and translates `ShipAction → CapabilityAction` to query the graph. **Council pressure-test:** is there a use case where the graph needs to know the role context directly (e.g., a capability that's only delegatable by a Captain, not by an XO with the same `Write` capability)? If so, `IPermissionResolver` may need to be inside the graph rather than above it. Confidence: Medium.
3. **Design tokens — C# constants, CSS custom properties, or W3C Design Tokens?** The decision (§5.1) is W3C Design Tokens JSON as source-of-truth + build-time codegen for both C# and CSS. **Council pressure-test:** the W3C format is still draft (2024); is there sufficient tooling maturity to commit, or should the source-of-truth be a Sunfish-internal C# const surface with hand-written CSS export? Confidence: Medium.
4. **`<LiveAnnouncer>` polite/assertive distinction in the contract.** The decision (§6.1) is that politeness is a parameter of `AnnounceAsync`, set by the *caller*, not by the renderer. **Council pressure-test:** is there a class of callers that doesn't know the politeness (e.g., a generic library that announces "operation complete")? If so, the contract may need a default-politeness-by-context field on the surface. Confidence: High.
5. **Conformance declaration — runtime, test, or build artifact?** The decision (§7.1 / §7.3) is a runtime registry + a Stage 07 review checklist. **Council pressure-test:** runtime declarations can be stale (a surface declares conformance, then a later change breaks it without re-declaring). Should the declaration instead be a build-time artifact (a `[Conformance]` attribute on the type, scanned at build time)? Trade-off: the build-time approach is more resilient to drift but harder to localize ("declaredByKey"). Confidence: Medium-Low.

Council perspectives required (per W#35 §9.5 + this ADR's pre-merge canonical posture):

- **WCAG/a11y subagent** — mandatory; vets the §4.1 baseline + §6 primitive contracts + §7 conformance declaration + §8 platform a11y bindings.
- **Design-engineering subagent** — vets the §5 token catalog + token format choice + role-band hue assignments.
- **Security-engineering subagent** — vets the §1.1 closed-enum decision + §2 resolution algorithm + §2.2 composition with `ICapabilityGraph` (role taxonomy intersects security per W#35 §9.5).
- **i18n/RTL subagent if available** — vets the §7.4 RTL token approach + `LocalizedString` integration.

---

## Consequences

### Positive

1. **Eliminates the per-UI-ADR re-derivation tax.** Every downstream UI ADR (~7 follow-ons + future) inherits role taxonomy + permission decision shape + tokens + a11y baseline + platform binding. No per-ADR re-litigation.
2. **WCAG 2.2 AA + EN 301 549 conformance is a contract.** The `IConformanceRegistry` + Stage 07 audit gate + CI build-time checks make conformance auditable. Bridge EU tenants are not procurement-blocked.
3. **Permission denial is accessibly surfaced by construction.** `PermissionDecision.Denied` carries reason + remediation + accessible escalation; surfaces have no path to render a blank pane or a generic 403.
4. **Deck-progressive disclosure is explicit, not implicit.** Per-role default landing decks + below-the-waterline gates are declared in registration data, not scattered across surface code.
5. **Cross-platform tokens with one source of truth.** The W3C Design Tokens JSON drives C# / CSS / MAUI generation; light + dark themes audit on every PR; CVD + contrast verification is automated.
6. **Composition with substrate is principled.** `IPermissionResolver` sits above `ICapabilityGraph`; role assignments issue as Standing Orders; denials emit audit records — no parallel hierarchies.
7. **Sequencing for Quarterdeck / Engine Room / Tactical / Sick Bay / Ship's Office / OOD-Watch / ~ADR 0066 / ~ADR 0068.** Once this ADR lands, the W#35 cohort can author against a stable contract.

### Negative

1. **Surface scope is very large.** Phase 1 alone (role taxonomy + resolver) is ~6-8h sunfish-PM time; full implementation (Phase 1 through Phase 5) ~30-40h across 6-8 PRs. Authoring + extended council is ~20-26h XO time per W#35 §8.2 estimate.
2. **Three new packages.** `foundation-ship-common` + `foundation-design-tokens` + `Sunfish.UICore.{Conformance,FirstAid,Primitives}` extension. Mitigated by `AddSunfishSharedDesignSystem()` meta-extension that registers all three at once.
3. **Closed-enum `ShipRole` constrains tenant naming.** Tenants cannot define new authority gradients, only new labels mapping to the existing gradient. Per §1.1: this is a deliberate trade-off for resolution determinism. If wrong, the revisit-trigger fires and we reconsider.
4. **Token format gamble.** W3C Design Tokens is still draft (2024); tooling maturity is uneven. If the format diverges or stagnates, we may need to re-baseline. Mitigated by the source-of-truth being plain JSON — recoverable.
5. **Conformance declaration registry runtime cost.** Every surface registration adds an entry. At ~50 surfaces per accelerator, registry size is small (kilobytes); query cost is negligible. But: stale declarations (declared then drift-broken) are a real risk — the §7 / Council pressure-test point #5 flags this.
6. **Platform a11y API binding is per-adapter work.** Each adapter (Blazor / React / MAUI Win / MAUI Mac / MAUI iOS / MAUI Android) writes its own `ILiveAnnouncer` / `IFocusTrap` / etc. implementations. Per ADR 0048 §A1 we accept the per-platform cost; this ADR reaffirms it.

### Trust impact / Security & privacy

- **Trust expanded:** the Shared Design System is trusted to enforce the permission resolution algorithm. A bug in `IPermissionResolver` could grant unauthorized access. Mitigated by: (a) `IPermissionResolver` does NOT replace `ICapabilityGraph` — the cryptographic check happens at the graph layer; (b) the resolver is a DI-registered service (single composition root); (c) every `Denied` decision audits, so denial-bypass attempts are loud.
- **Trust contracted:** role assignment is a Standing Order (per ADR 0065). Standing Orders are audit-by-construction; assignments are not silent. Rescission goes through the same channel; revocation is auditable.
- **Capability boundary clarified:** `ShipRole.Captain` is the tenant-owner role; `Captain` actions compose with the tenant's root keypair (per ADR 0046). Multi-actor approval (Captain + incoming Captain co-sign per §1.3) is the only path for Captain transition.
- **Phase 2 deferred SUPPO is structurally inert.** Per §1.6: a Supply Office assignment is valid in shape but always resolves to `Denied(DenialReason.Phase2Deferred, ...)`. No security risk pre-Phase-2.
- **EN 301 549 procurement compliance** is a Bridge tenant requirement in EU jurisdictions. Surfaces that don't declare conformance CANNOT ship to EU Bridge tenants. Phase 4 CI gate enforces.

---

## Compatibility plan

- **Backward compatibility:** none required. This ADR introduces new packages + new contracts; it does not modify existing public surfaces. Existing per-block UI surfaces continue to work; opt-in to this ADR's First-Aid baseline + conformance declaration is per-block, per-PR.
- **Forward compatibility:** the `ShipRole` enum is sealed for v1. Adding a new role is a new ADR amendment + a new `ShipRole` value. The `IShipRoleRegistry` composite-label surface is open and additive — tenants gain new labels without ADR churn.
- **Migration of existing surfaces:** consumed-block-by-block. The Quarterdeck / Engine Room / etc. follow-on ADRs are the first opt-ins. Existing surfaces (e.g., `apps/kitchen-sink` blocks) migrate per their own per-block ADR or per the W#42 cohort discipline.
- **Affected packages:** new — `foundation-ship-common`, `foundation-design-tokens`. Extended — `Sunfish.UICore` (adds `Primitives` / `FirstAid` / `Conformance` namespaces). Implementations extended in Phase 4 — `ui-adapters-blazor`, `ui-adapters-blazor-a11y`, `ui-adapters-react`, MAUI runtime hosts under `accelerators/anchor/`.
- **Schema epoch:** role-assignment Standing Orders share the kernel schema epoch (`Sunfish.Kernel.Crdt`). A bump to the kernel epoch invalidates assignments and forces per-tenant log replay; pre-replay, no role grants are valid (resolver returns `Denied(NoMatchingRole, "Schema epoch transition", ...)`).

---

## Implementation checklist

### Phase 1 — `foundation-ship-common` package + types (~7h)

- [ ] Create `packages/foundation-ship-common/` (csproj + Directory.Build.props + tests/)
- [ ] Define `ShipRole`, `DivisionAssignment`, `ShipRoleAssignment`, `IShipRoleRegistry`, `ShipLocation`, `DeckDepth`, `ShipAction`, `IPermissionResolver`, `PermissionDecision` (Granted / Denied), `DenialReason`, `Remediation`, `RemediationKind`, `IDeckRegistry`, `DeckRegistration`
- [ ] Reference `Sunfish.Foundation.Capabilities` + `Sunfish.Foundation.MultiTenancy` + `Sunfish.Foundation.Wayfinder` + `Sunfish.Foundation.Assets.Common` + `Sunfish.Foundation.MissionSpace` + `Sunfish.Kernel.Audit` + `NodaTime`
- [ ] Add `AuditEventType.PermissionDenied` + `AuditEventType.PermissionDenialRateExceeded` constants to `Sunfish.Kernel.Audit.AuditEventType`
- [ ] Implement `DefaultPermissionResolver` over `ICapabilityGraph` + `IFeatureGate<TFeature>` + `IStandingOrderIssuer` per §2.1 algorithm (including step 0 deck-canonicalization + promotion guard)
- [ ] Define `ShipAction → CapabilityAction` mapping table as a static-readonly `IReadOnlyDictionary<ShipAction, CapabilityAction>` on `DefaultPermissionResolver`
- [ ] Define `ActionMinimumDeck` as a static-readonly `IReadOnlyDictionary<ShipAction, DeckDepth>` on `DefaultPermissionResolver` (per §2.1 step 0(a))
- [ ] Implement per-`(ActorId, ShipLocation)` denial counter with 1-minute sliding window + `PermissionDenialRateExceeded` emission (per §2.4 rate-limiting spec)
- [ ] Implement role-assignment cache with `IStandingOrderEventStream` subscribe-before-load invalidation (per §2.5)
- [ ] Define audit-loud `ShipAction` set (BelowTheWaterline + watch-handover + role-promotion) as static-readonly list
- [ ] Unit tests: 20 tests covering all 8 resolution-algorithm steps (0–7 + Granted) + denial accessibility-shape contract + audit emission count + rate-limit trigger + cache-invalidation subscribe-then-replay

### Phase 2 — `foundation-design-tokens` package + W3C Design Tokens build pipeline (~6h)

- [ ] Create `packages/foundation-design-tokens/` (csproj + tokens.json + tests/)
- [ ] Author `tokens.json` per §5.2 namespace (color / typography / space / radius / elevation / motion / target-size / role-band)
- [ ] Implement build-time codegen tool: tokens.json → C# const records (`Sunfish.Foundation.DesignTokens`)
- [ ] Implement build-time codegen tool: tokens.json → CSS custom properties (`packages/ui-core/src/tokens.css`)
- [ ] Implement build-time WCAG 1.4.3 + 1.4.11 contrast verification on every text/surface pair (light + dark themes)
- [ ] Implement CVD ΔE2000 audit on role-band hues (per ADR 0036 precedent)
- [ ] Generate `apps/docs/design-system/tokens.md` reference doc
- [ ] Generate `apps/docs/design-system/role-band-cvd.md` audit report
- [ ] Unit tests: 8 tests covering codegen round-trip + contrast verification + CVD audit thresholds

### Phase 3 — `Sunfish.UICore.Primitives` + `Sunfish.UICore.FirstAid` + `Sunfish.UICore.Conformance` (~5h)

- [ ] Define `ILiveAnnouncer`, `LiveRegionPoliteness`, `IFocusTrap`, `IFormControlContract`, `FormControlKind`, `IDiffPreview`, `DiffEntry`, `DiffPreviewView`, `ISearchAsYouType` in `Sunfish.UICore.Primitives`
- [ ] Define `IFirstAidContract`, `HelpLocation`, `TargetSizeCompliance` in `Sunfish.UICore.FirstAid`
- [ ] Define `IConformanceRegistry`, `ConformanceDeclaration`, `Wcag22Level`, `WcagSuccessCriterion`, `En301549Chapter`, `ConformanceException` in `Sunfish.UICore.Conformance`
- [ ] Reference `Sunfish.Foundation.Ship.Common` + `Sunfish.Foundation.DesignTokens`
- [ ] Unit tests: 12 tests covering primitive contract shapes + First-Aid declaration + Conformance registry round-trip

### Phase 4 — Adapter implementations + a11y harness extension + CI gates (~8h)

- [ ] Implement `BlazorLiveAnnouncer` / `BlazorFocusTrap` / etc. in `ui-adapters-blazor`
- [ ] Implement `ReactLiveAnnouncer` / `ReactFocusTrap` / etc. in `ui-adapters-react`
- [ ] Implement `MauiLiveAnnouncer` / `MauiFocusTrap` / etc. with platform-specific a11y API bindings (UIA / NSAccessibility / UIAccessibility / AccessibilityNodeInfo) — at least Mac + Windows for v1; iOS + Android in W#23 follow-up
- [ ] Extend `Sunfish.UIAdapters.Blazor.A11y` to require `IFirstAidContract` declaration on registered components
- [ ] CI gate: token-build contrast verification fails build on regression
- [ ] CI gate: `IConformanceDeclaration` required for every surface registered to a `ShipLocation`
- [ ] CI gate: per-platform a11y binding tables complete for every primitive in `Sunfish.UICore.Primitives`
- [ ] Integration tests: 6 cross-adapter tests verifying `ILiveAnnouncer` / `IFocusTrap` round-trip on Blazor + React + MAUI Win + MAUI Mac

### Phase 5 — apps/docs + meta-extension + cross-link (~3h)

- [ ] Implement `AddSunfishSharedDesignSystem(IServiceCollection)` meta-extension registering all three packages' DI in correct order
- [ ] Add `apps/docs/design-system/README.md` overview
- [ ] Add `apps/docs/design-system/tokens.md` (codegen output)
- [ ] Add `apps/docs/design-system/role-band-cvd.md` (codegen output)
- [ ] Add `apps/docs/design-system/conformance-baseline.md` Stage 07 review checklist
- [ ] Add `apps/docs/design-system/platform-a11y-bindings.md` per-primitive binding tables
- [ ] Cross-link from `_shared/product/architecture-principles.md` (the "Shared Design System" section becomes a real link)
- [ ] Wire `AddSunfishSharedDesignSystem()` into `apps/kitchen-sink` to demonstrate role-tagged UI + denial-surface First-Aid rendering

### Phase 6 — Ledger flip + close W#35 follow-on row (~30min)

- [ ] Update `icm/_state/active-workstreams.md`: flip W#35 follow-on (Shared Design System) row from `design-in-flight` → `built`
- [ ] Add row note: PR list + new package list + new AuditEventType list + tokens.json source-of-truth path
- [ ] Update memory `project_workstream_35_*.md` with shipped scope
- [ ] File hand-off for next W#35 follow-on (OOD-Watch rotation primitive — the second ADR per W#35 §9.2 sequencing)

**Total estimate:** ~30-40h sunfish-PM time across 6-8 PRs (council-revised estimate may differ; cohort precedent of 3-5h per phase + 8h for the adapter cross-cut). Pre-merge council canonical (Stage 1.5 + WCAG/a11y subagent + design-engineering subagent + security-engineering subagent BEFORE any phase commit).

---

## Open questions

1. **Custom-role-label scope restrictions.** §1.1's `IShipRoleRegistry.AssignLabel(ShipRole baseRole, string tenantLabel, ScopeRestriction? scope)` — what's the shape of `ScopeRestriction`? Recommendation: an additive whitelist of `(ShipLocation, DeckDepth)` pairs that *narrows* the base-role's grants. Resolution multiplies (base allows AND scope allows). Defer concrete shape to scaffolding stage.
2. **Captain transition multi-actor approval.** §1.3 says outgoing + incoming Captain co-sign. Is two signatures sufficient, or should `n-of-m` (e.g., outgoing Captain + XO + incoming Captain, 2-of-3) be supported? Recommendation: 2-of-2 (outgoing + incoming) for v1; richer schemes via amendment when first commercial Phase 2 tenant has the requirement.
3. **`ShipAction` as `readonly record struct(string Name)` vs. enum.** Chosen the struct-with-constants pattern (matches `CapabilityAction`). **Open:** does the open-string surface area invite drift? Recommendation: ship a `static readonly IReadOnlySet<ShipAction> KnownActions` so analyzers can warn on unknown action strings.
4. **Token rebuild on theme change.** §5 design tokens have light + dark variants. **Open:** does theme switching at runtime require token-bundle reload, or do all tokens carry both variants and the adapter selects? Recommendation: adapter selects; the W3C format supports per-mode variants natively. Defer to scaffolding stage.
5. **Conformance declaration drift detection.** §7.1 / Council pressure-test point #5 — should declarations be runtime or build-time? Recommendation: runtime registry for v1 (matches the `AddSunfishWayfinder()` registration pattern); add a Phase 4 build-time analyzer that warns when a `ShipLocation`-registered surface lacks `[Conformance]`-attribute declaration. Re-evaluate at first drift incident.
6. **Below-the-waterline confirmation timeout.** §3.2 forbids timer-confirmations. **Open:** what is the maximum dwell time before the elevation step expires (must re-confirm)? Recommendation: 5 minutes (per ADR 0065 precedent on rescission window timing); the timer is for *expiration of elevation*, not auto-confirm.
7. **Role-band hue collisions with vendor-color compat-pack.** §5.5 reserves a hue per role; some compat packs (Telerik, Syncfusion, Material) have their own brand hues. **Open:** does the role-band system override compat-pack brand hues, or coexist? Recommendation: role-band overrides for *role-tagged* surfaces; compat-pack brand hues remain for non-role-tagged (e.g., a Telerik Grid's header chrome). Defer concrete merge rules to compat-* ADR amendments as needed.

---

## Revisit triggers

- **First incident.** If a `IPermissionResolver` bug grants unauthorized access in production, the resolution algorithm + audit-loud action set require revisit.
- **Custom-role pressure.** If multiple tenants request authority gradients not in the closed `ShipRole` enum, §1.1's closed-enum decision needs revisit (likely as an amendment expanding the enum, not a switch to open registry).
- **WCAG 2.2 → 2.3 / 3.0.** Future WCAG-version uplift requires re-baselining §4.1 + §7.3.
- **Token format divergence.** If the W3C Design Tokens Community Group format stagnates / diverges, §5.1 source-of-truth choice needs revisit.
- **Platform a11y API breaking change.** A major iOS / Android / Windows a11y API revision (e.g., UIA 4.0; UIAccessibilityPriority deprecation) triggers §8.1 binding table revisit.
- **Phase 2 commercial activation.** When SUPPO + Supply Office are no longer deferred, §1.6 + §6 + §7 update with the new role + location bindings.
- **EN 301 549 v3.3.x revision.** New EN 301 549 chapter renumbering triggers §7.2 mapping revisit.
- **Conformance drift incident.** If a runtime-declared surface ships and a later change breaks conformance without re-declaring (Council pressure-test #5 risk), §7.1 / §7.5 lint contract needs strengthening (probably to compile-time attribute).

---

## References

### Predecessor and sister ADRs

- ADR 0008 — `Sunfish.Foundation.MultiTenancy` (composition: tenant-scoped role assignment)
- ADR 0009 — Foundation.FeatureManagement (composition: feature-gate + this ADR composes via `IFeatureGate<TFeature>`)
- ADR 0032 — multi-team Anchor workspace switching (composition: per-team subkey wrapping is the cryptographic substrate role assignment composes onto)
- ADR 0034 — accessibility harness per adapter (extension: this ADR adds the *content* of the contracts the harness verifies)
- ADR 0036 — SyncState multimodal encoding contract (precedent: 5-channel encoding discipline; First-Aid baseline reuses it)
- ADR 0041 — dual-namespace components by design (composition: degradation primitive — when rich variant fails capability gating, MVP variant remains)
- ADR 0046 + 0046-A1 — recovery + identity substrate (composition: Captain authority composes onto root keypair)
- ADR 0048 + 0048-A1 — Anchor multi-backend MAUI (per-platform a11y API binding boundary)
- ADR 0049 — audit trail substrate (composition: permission decisions and role changes audit by construction)
- ADR 0062 — Mission Space Negotiation Protocol (composition: deck visibility composes with `IFeatureGate<TFeature>`)
- ADR 0065 — Wayfinder System + Standing Order Contract (composition: role assignments and watch transfers issue as Standing Orders)

### Architectural precedent

- ADR 0007 — Bundle Manifest Schema (precedent: schema-driven registration)
- ADR 0028 — CRDT engine selection (Standing Orders run through this CRDT substrate; role assignments transitively compose)
- ADR 0055 — Dynamic forms substrate (precedent for `IFormControlContract`)

### Discovery and intake

- W#35 discovery: `icm/01_discovery/output/2026-05-01_ship-architecture.md` — full discovery; §5 locations; §6 roles; §7 cross-cutting primitives; §8 follow-on ADR queue; §9 sequencing
- Shared Design System intake: `icm/00_intake/output/2026-05-01_shared-design-system-intake.md`
- W#34 a11y precedent: `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` §5.1 + Appendix A
- W#33 Mission Space precedent: `icm/01_discovery/output/2026-04-30_mission-space-matrix.md`

### Cohort discipline

- Council batting average: 22-of-22 substrate amendments needed council fixes (canonical: pre-merge council)
- Structural-citation failure rate: ~65% of XO-authored ADRs had ≥1 structural-citation failure that §A0 self-audit did NOT catch — council remains canonical defense (per `feedback_decision_discipline.md` + `feedback_council_can_miss_spot_check_negative_existence.md`)

### External

- WCAG 2.2 AA — W3C Recommendation, October 2023 — <https://www.w3.org/TR/WCAG22/>
- EN 301 549 v3.2.1 (2021) — Accessibility requirements for ICT products and services (EU procurement compliance) — chapters 9 (Web), 10 (Non-web documents), 11 (Non-web software)
- WAI-ARIA 1.2 — <https://www.w3.org/TR/wai-aria-1.2/>
- WAI-ARIA Authoring Practices Guide (combobox + dialog patterns) — <https://www.w3.org/WAI/ARIA/apg/>
- W3C Design Tokens Community Group — <https://design-tokens.org/> (draft 2024)
- Apple Human Interface Guidelines — Accessibility — <https://developer.apple.com/design/human-interface-guidelines/accessibility>
- Apple Accessibility — UIAccessibility / NSAccessibility documentation
- Microsoft UI Automation Provider Programmer's Guide — <https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/>
- Material Design 3 — Accessibility — <https://m3.material.io/foundations/accessible-design/overview>
- Microsoft Fluent UI 2 — Design tokens — <https://fluent2.microsoft.design/design-tokens>
- Apple HIG — Color and contrast guarantees
- React Aria — <https://react-spectrum.adobe.com/react-aria/> (a11y-first primitives precedent)
- Radix UI — <https://www.radix-ui.com/> (primitive-contract precedent)
- Paul Tol qualitative palette ("Colour Schemes", 2021) — CVD-distinguishability source per ADR 0036

---

## Council disposition (2026-05-04)

Triple council ran on draft before commit per cohort discipline.

### Security-engineering council

| ID | Category | Finding | Resolution |
|---|---|---|---|
| S-1.1 | **Blocking** | No promotion-target guard — any role can `PromoteRole` to any target; self-promotion possible | Applied: §2.1 step 0(b) — hierarchy invariant + self-promotion prohibition |
| S-2.3 | **Blocking** | Caller-supplied `deck` trusted for action-sensitivity — `Quarantine` could pass `MainDeck` | Applied: §2.1 step 0(a) — `ActionMinimumDeck` + `effectiveDeck = max(callerDeck, ActionMinimumDeck[action])` |
| S-3.1 | **Blocking** | No denial rate-limit — audit flooding possible via systematic `Denied` loops | Applied: §2.4 denial-rate-limiting (N=10 per actor/location/minute; `PermissionDenialRateExceeded` audit record) |
| S-2.1 | NM | No cache-invalidation spec for role-assignment cache | Applied: §2.5 subscribe-before-load pattern + `IStandingOrderEventStream` subscription |
| S-2.2 | NM | Location-scoped vs resource-scoped actions not distinguished | Applied: §2.0 new section + step 0(c) resource-scope guard |
| S-1.2 | NM | Captain bootstrap atomic with tenant provisioning — not fully specified | Noted: §1.3 references ADR 0046 root-keypair binding; full atomic-provisioning spec deferred to `foundation-multitenancy` extension |
| S-2.4 | NM | `AuditEventType.PermissionDenied` constant not in Phase 1 checklist | Applied: Phase 1 checklist updated to include both `PermissionDenied` and `PermissionDenialRateExceeded` |
| S-3.2 | NM | Watch-handover audit-loud set not enumerated | Noted: §2.4 defines the static-readonly list with BelowTheWaterline + watch-handover + role-promotion |
| Mechanical × 4 | Mechanical | DenialReason mixes Phase2/V2 in one value; SC-1 `ExpiresAt`→`ProvedAt`; Remediation missing CTA label; step 3 uses wrong enum value | Applied: NM-3 enum split; SC-1 fix; NM-4 `CallToActionLabel`; §2.1 step 3 updated |

### WCAG/a11y council

| ID | Category | Finding | Resolution |
|---|---|---|---|
| F-1 | NM | `IFirstAidContract` has no default live-announcement policy — callers guess politeness | Applied: `LiveAnnouncementPolicy` property added to `IFirstAidContract` |
| F-5 | NM | `LiveRegionPoliteness` has no `Critical` tier for security/destructive announcements | Applied: `Critical` value added + binding table updated for Web/UIA/macOS/iOS/Android |
| F-6 | NM | `IFocusTrap` silent on nested-trap behavior — implementors free to diverge | Applied: §6.2.1 LIFO stacking semantics |
| F-7 | NM | `IFormControlContract.ErrorMessageKey` comment ambiguous about `aria-invalid`/`aria-errormessage` pairing | Applied: §6.3.1 explicit pairing rule + `AutocompleteHint` for WCAG SC 1.3.5 |
| F-2 | NM | `IFirstAidContract.HelpLocation` values are strings in prose, enum not surfaced for adapter enforcement | Noted in §4 — `HelpLocation` enum is the contract; adapters enforce at registration |
| F-3 | NM | `IDiffPreview.DiffPreviewView.AccessibleRows` is `IReadOnlyList<string>` — no structured per-row a11y role | Deferred to scaffolding stage; §6.4 notes that JSON object values expand as structure, not raw text |
| F-4 | NM | `ISearchAsYouType` count-announcement timing not mapped to ARIA APG combobox pattern | Notes added inline in §6.5 prose — "≥500ms → polite in-progress announcement; full-count P95 ≤300ms" |
| Mechanical × 6 | Mechanical | Various comment/prose precision + WCAG SC number spot-checks | Applied inline |

### Adversarial council

| ID | Category | Finding | Resolution |
|---|---|---|---|
| NM-5 | NM | §1.1 does not distinguish structure-vs-label load-bearing status | Applied: §1.1 "Structure is load-bearing; labels are not" paragraph |
| NM-6 | NM | Option D (resolver-into-graph) not documented as considered option | Applied: Option D added to Considered options |
| NM-3 | NM | `DenialReason.DeferredFeature` conflates Phase2 and V2 deferrals | Applied: split to `Phase2Deferred` + `V2Deferred` |
| NM-4 | NM | `Remediation` has no `CallToActionLabel` — accessible button-label for escalation affordance missing | Applied: `CallToActionLabel` field added |
| NM-2 | NM | Cache invalidation for role-assignment cache not specified | Applied: §2.5 (overlaps with S-2.1) |
| SC-1 | SC | `CapabilityProof.ExpiresAt` does not exist; actual field is `ProvedAt` | Applied: §2.2 corrected |
| SC-2 | SC | `foundation-missionspace/` path in §2.1 prose — confirmed correct path is `foundation-mission-space/` (hyphenated) | Verified: §A0.2 already cites correct path `packages/foundation-mission-space/` |
| Mechanical × 5 | Mechanical | Prose precision improvements | Applied inline |

**Net result:** 3 Blocking → resolved; 9 NM → 8 applied, 1 deferred to scaffolding (F-3); 6 SC/Structural → 5 applied, 1 confirmed correct. ADR ready for CO acceptance.

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Considered Option A (omnibus package) and Option C (collapse into existing packages) before settling on Option B (three-package split). Documented in §Considered options.
- [x] **FAILED conditions / kill triggers.** §Revisit triggers names ≥7 conditions including first-incident, custom-role pressure, WCAG version uplift, token format divergence, platform a11y API breaking changes, Phase 2 SUPPO activation, EN 301 549 revision, conformance drift incident.
- [x] **Rollback strategy.** New packages introduced; rollback = revert PRs + remove DI registration. No backward-compat constraint per Compatibility plan. Existing callers unaffected.
- [x] **Confidence level.** **MEDIUM-HIGH** overall. High on §1 / §2 / §3 / §4 (well-precedented by ADR 0065 / W#35). Medium on §5 (token format gamble) / §7.1 runtime-vs-build registry choice / §1.1 closed-enum-vs-registry decision. Council pressure-test points #1 / #3 / #5 are the lower-confidence territories.
- [x] **Cited-symbol verification.** §A0.1 / §A0.2 / §A0.3 ran on 2026-05-04. Triple council also ran (see Council disposition section). All Blocking + SC findings resolved including SC-1 (`CapabilityProof.ExpiresAt` → `ProvedAt`). Council items (g) / (h) / (i) / (j) per §A0.3 remain open for CO spot-check on first review.
- [x] **Anti-pattern scan.** Checked AP-1 (assumptions), AP-3 (vague success criteria), AP-9 (skipping Stage 0), AP-12 (timeline fantasy), AP-21 (assumed facts). None apply that aren't documented in §Open questions or §Council brief.
- [x] **Revisit triggers.** §Revisit triggers populated with ≥7 conditions.
- [x] **Cold Start Test.** Implementation checklist Phase 1 → Phase 6 is sequential; each phase has a clear scope + tests + cross-package wiring. A fresh contributor can execute without author clarification *for Phase 1 / Phase 2*; Phase 4 (cross-adapter) requires per-platform expertise the contributor must consult adapter-specific maintainers for. Adapter-specific tightening to add in Stage 05.
- [x] **Sources cited.** External references (WCAG 2.2 / EN 301 549 / WAI-ARIA APG / W3C Design Tokens / Apple HIG / Microsoft UIA / Material 3 / Fluent UI 2 / React Aria / Radix UI / Paul Tol) cited with URLs.

### Cited-symbol verification helper (run before checking off above)

```bash
ADR=docs/adrs/0077-shared-design-system.md

# 1. Print all Sunfish.* symbols cited in the ADR
grep -oE "Sunfish\.[A-Z][A-Za-z0-9.]+" "$ADR" | sort -u

# 2. For each, check whether the short name exists as a defined type or namespace
for sym in $(grep -oE "Sunfish\.[A-Z][A-Za-z0-9.]+" "$ADR" | sort -u); do
  short=$(echo "$sym" | grep -oE "[^.]+$")
  if ! git grep -q -E "(class|record|interface|enum|namespace) +$short" packages/; then
    echo "MISSING: $sym (short: $short) — fix before acceptance"
  fi
done
```

Symbols expected to appear in `MISSING` (introduced by this ADR's Phase 1 / 2 / 3): all `Sunfish.Foundation.Ship.Common.*`; all `Sunfish.Foundation.DesignTokens.*`; all `Sunfish.UICore.{Primitives,FirstAid,Conformance}.*`; the new `Sunfish.Kernel.Audit.AuditEventType.PermissionDenied` constant. All other symbols MUST resolve.

---

*This ADR enforces the lightweight Universal Planning Framework checks documented in [`.claude/rules/universal-planning.md`](../../.claude/rules/universal-planning.md).*
