# ADR 0064 — Runtime Regulatory / Jurisdictional Policy Evaluation

**Status:** Proposed (2026-04-30 — auto-merge intentionally DISABLED until Stage 1.5 council reviewed including a Pedantic-Lawyer perspective subagent per the W#33 §5.9 hardening precedent)
**Date:** 2026-04-30
**Author:** XO (research session)
**Pipeline variant:** `sunfish-feature-change` (introduces new cross-cutting policy contract)

**Resolves:** [`icm/00_intake/output/2026-04-30_runtime-regulatory-policy-evaluation-intake.md`](../../icm/00_intake/output/2026-04-30_runtime-regulatory-policy-evaluation-intake.md); fifth and final item in W#33 Mission Space Matrix follow-on authoring queue per [`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`](../../icm/01_discovery/output/2026-04-30_mission-space-matrix.md) §7.2.

---

> **Reader caution (Pedantic-Lawyer hardening pass; carried forward from W#33 §5.9 + the parent intake):** specific statutory citations in this ADR have not been verified against current Official Code text and may use practitioner shorthand. **General counsel MUST engage before Stage 06 build of any concrete enforcement behavior.** This ADR specifies the *substrate-tier policy-evaluation framework*; the *content* of the policy rules per jurisdiction is a legal-review work product not produced in this ADR. Several explicit halt-conditions (see §"Halt conditions for Stage 06") gate enforcement-side build on legal sign-off.

---

## Context

The W#33 Mission Space Matrix discovery doc (§5.9) classifies regulatory/jurisdictional handling as a **Gap** — the paper acknowledges regulatory factors (paper §20.4, §16) but does not specify a runtime substrate; per-domain ADRs (ADR 0057 FHA documentation-defense; ADR 0060 Right-of-Entry per-jurisdiction rules) handle their slices but do not generalize. The Mission Space Matrix §6.3 names this gap as a **commercial launch-blocker** for any non-US-residential-property tenant.

The substrate to fix this exists — almost. ADR 0062 (Mission Space Negotiation Protocol; landed post-A1 via PR #406) introduces a `Regulatory` dimension on `MissionEnvelope` with `(jurisdiction, consentVersion, appliedPolicies)` triples and a `RegulatorySpec` slot in the spec layer (per ADR 0063 post-A1). ADR 0064 fills in the substrate **above** ADR 0062's regulatory dimension:

- A jurisdictional probe with composite confidence (IP-geo, user declaration, tenant config)
- A per-jurisdiction policy-evaluation rule engine matching ADR 0057's documentation-defense pattern + ADR 0060's per-jurisdiction explicit-citation pattern
- A canonical regime-acknowledgment surface naming which regulatory regimes Sunfish targets at substrate tier (HIPAA / GDPR / PCI-DSS / SOC 2 / EU AI Act / FHA) vs which are out-of-scope for the open-source reference implementation (FedRAMP / ITAR commercial-productization-only)
- A data-residency enforcement contract for record-class-aware residency rules
- A sanctions handling contract (OFAC SDN/sectoral lists; EU consolidated sanctions list) with explicit operator+legal-counsel decision points
- An EU AI Act tier-classification placeholder for future Sunfish AI/ML features (none today, but the framework is shape-aware)

ADR 0064 sits at the **runtime regulatory layer**, downstream of ADR 0062 + ADR 0063 + the per-domain ADRs. ADR 0064 does NOT replace per-domain ADRs (0057, 0060); it provides the cross-cutting substrate they consume.

W#33 §6.3 names this ADR as the **highest commercial priority** of the four follow-on ADRs.

---

## A0 cited-symbol audit

Per the cohort-discipline pattern (ADR 0028-A8.11 / ADR 0062-A1.14 / ADR 0063-A1.15) — **AND** the post-ADR-0063-council lesson that §A0 self-audit is necessary but not sufficient (catch rate 0-of-4 on ADR 0063). Council remains canonical defense.

**Existing on `origin/main` (verified 2026-04-30):**

- ADR 0057 (FHA documentation-defense; per-domain regulatory rule precedent) — verified Accepted
- ADR 0060 (Right-of-Entry per-jurisdiction rules; per-jurisdiction citation precedent) — verified Accepted
- ADR 0062 (Mission Space Negotiation Protocol; post-A1) — landed via PR #406; provides `MissionEnvelope.Regulatory: RegulatoryCapabilities`, `IDimensionProbe<RegulatoryCapabilities>`, `IFeatureGate<TFeature>`, `FeatureVerdict`, `DegradationKind`, `EnvelopeChange`, `DimensionChangeKind` (10-value enum), `LocalizedString`, `FeatureVerdictSurfaced` audit constant, `ForceEnablePolicy.OverridableWithCaveat` (specifically applies to the regulatory dimension per ADR 0062-A1.9)
- ADR 0063 (Mission Space Requirements; post-A1) — landed via PR #411; provides `RegulatorySpec(AllowedJurisdictions, ProhibitedJurisdictions, RequiredConsents)` slot in the install-time spec
- ADR 0049 (audit substrate) — telemetry emission target
- ADR 0009 (Edition / IEditionResolver) — provides the commercial-tier surface that ADR 0064 cross-references
- ADR 0031 (Bridge hybrid multi-tenant SaaS) — Bridge-tier data-residency boundary
- Paper §20.4 (Regulatory factors as architectural filter)
- Paper §16 (IT governance posture)
- W#22 Leasing Pipeline (Phase 6 compliance half currently deferred — direct downstream consumer)
- W#28 Public Listings (jurisdiction-aware rendering — direct downstream consumer)
- W#31 Foundation.Taxonomy (jurisdictional classification taxonomies — direct downstream consumer)
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` — encoding contract
- `Sunfish.Kernel.Audit.AuditEventType` — telemetry surface
- `Sunfish.Foundation.Taxonomy.ITaxonomyResolver` (per ADR 0056) — consumed for jurisdiction taxonomy lookups

**Introduced by ADR 0064:**

- New types in `Sunfish.Foundation.MissionSpace` (extends ADR 0062's package): `JurisdictionProbe`, `JurisdictionalPolicy`, `JurisdictionalPolicyRule`, `IPolicyEvaluator`, `PolicyVerdict`, `RegimeAcknowledgment`, `DataResidencyConstraint`, `IDataResidencyEnforcer`, `ISanctionsScreener`, `SanctionsScreeningResult`, `RegulatoryRegime` (enum: `HIPAA`, `GDPR`, `PCI_DSS_v4`, `SOC2`, `EU_AI_Act`, `FHA`, `CCPA`, `Other`), `RegulatoryRegimeStance` (enum: `InScope`, `OutOfScopeOpenSource`, `CommercialProductOnly`)
- 7 new `AuditEventType` constants (per §"Telemetry shape"): `PolicyEvaluated`, `PolicyEnforcementBlocked`, `JurisdictionProbedWithLowConfidence`, `DataResidencyViolation`, `SanctionsScreeningHit`, `RegimeAcknowledgmentSurfaced`, `EuAiActTierClassified`
- New apps/docs surface: `apps/docs/foundation/regulatory-policy/` walkthrough
- New `Sunfish.Foundation.Taxonomy` consumer: `Sunfish.Regulatory.Jurisdictions@1.0.0` taxonomy charter (mirrors `Sunfish.Leasing.JurisdictionRules@1.0.0` per ADR 0057 — same shape; cross-domain consumer)

**Removed by ADR 0064:**

- None (additive).

---

## Decision drivers

- **General counsel is part of the work, not optional.** The substrate-tier framework is XO authority; the *content* of policy rules per jurisdiction is legal-review authority. Separation is explicit; halt-conditions name the boundary.
- **Per-domain ADRs are precedents, not replacements.** ADR 0057's FHA documentation-defense pattern + ADR 0060's per-jurisdiction explicit-citation pattern compose into ADR 0064's framework. Existing per-domain ADRs continue to own their domain-specific rules; ADR 0064 provides the cross-cutting substrate they share.
- **ADR 0062's `Regulatory` dimension is the consumer; ADR 0063's `RegulatorySpec` is the gate; ADR 0064 provides the engine.** Architectural layering: probe (ADR 0062) → spec (ADR 0063) → rule engine (ADR 0064) → enforcement.
- **Substrate must be jurisdiction-agnostic; rules are jurisdiction-specific.** The framework defines *how* to evaluate policy; the *content* of policy rules per jurisdiction is data, not code. Jurisdiction taxonomies live in `Sunfish.Foundation.Taxonomy` per ADR 0056.
- **Probe confidence is real.** IP-geolocation is unreliable (VPNs, mobile roaming); user declaration is high-confidence but stale on travel; tenant-config is most reliable but operator-controlled. Composite probe with confidence score is the pattern; per-feature minimum-confidence is the contract.
- **Rules engine must be deterministic + auditable.** Same `(jurisdiction, feature, envelope)` triple → same `PolicyVerdict`. Audit emission per evaluation is mandatory (regulatory defense requires demonstrable consistency).
- **Open-source-OSS framing matters.** Sunfish-the-framework is open-source; some regulatory regimes (FedRAMP, ITAR) require commercial productization to engage authentically. ADR 0064 distinguishes `RegulatoryRegimeStance.InScope` (substrate aspires to enable conformance) vs `OutOfScopeOpenSource` (substrate explicitly does not aspire) vs `CommercialProductOnly` (substrate is shape-aware but conformance ships with commercial productization).

---

## Considered options

### Option A — Substrate framework + per-jurisdiction rule engine + general-counsel-engaged content layer [RECOMMENDED]

ADR 0064 ships the substrate (probe + rule engine + evaluator + enforcer + screener + audit constants) without specifying any concrete policy rules. Per-jurisdiction rule content is authored as a separate work product engaging general counsel; the rule-content layer ships as data files (taxonomy entries + JSON-encoded policy rules) consumed by the substrate.

- **Pro:** Clean XO/legal separation; substrate is testable with synthetic rules; legal review is bounded to the rule-content layer
- **Pro:** Substrate can ship at Phase 1 without legal-review gate; rule-content lands per-regime as legal sign-off completes
- **Pro:** Rule-content is data, not code — non-engineers (legal counsel) can review the rule-content directly without reading C#
- **Con:** Two-layer authoring; coordination cost between substrate and rule-content layers
- **Con:** Substrate without rules is not enforcing anything; consumers may misread Phase 1 as "regulatory-compliant" when it's not

**Verdict:** Recommended. Matches ADR 0057's substrate-vs-content separation pattern; XO/legal-counsel boundary is auditable.

### Option B — Inline regulatory rules in ADR 0064 itself

ADR 0064 enumerates concrete policy rules per regulatory regime (HIPAA / GDPR / etc.) in the ADR text.

- **Pro:** Single-document scope; no two-layer coordination
- **Con:** Policy rules become ADR-text; updates require new ADR amendments (slow + heavy process for what should be content updates)
- **Con:** Legal review of an ADR is a different audit posture than legal review of policy rules — conflates substrate decisions with policy content
- **Con:** Sunfish.Regulatory.Jurisdictions taxonomy already exists as the right home (ADR 0056-shape) for jurisdictional content; duplicating in ADR 0064 fragments

**Verdict:** Rejected. Conflates substrate with content; raises legal-review costs.

### Option C — Defer regulatory framework entirely; rely on per-domain ADRs

Skip cross-cutting substrate; each per-domain ADR (0057, 0060, future ADRs) handles its own regulatory rules in isolation.

- **Pro:** Status quo; zero new authoring
- **Con:** Status quo's failure mode (cross-cutting concerns reinvented per domain) is preserved
- **Con:** Defeats W#33 §6.3 commercial-launch-blocker framing
- **Con:** Per-domain ADRs already share patterns (FHA + Right-of-Entry); the substrate exists implicitly; ADR 0064 makes it explicit

**Verdict:** Rejected. ADR 0064's existence is justified by W#33 §6.3.

### Option D — Federated rule engines per regulatory regime

Each regime (HIPAA, GDPR, etc.) gets its own rule engine; ADR 0064 ships only the federation (the dispatcher across regime engines). Per-regime engines are authored separately as their legal review completes.

- **Pro:** Maximum legal-review parallelism (HIPAA review can proceed independently from GDPR review)
- **Pro:** Each regime engine can have regime-specific evaluation logic (HIPAA's administrative/physical/technical safeguards triad doesn't fit the same shape as GDPR Article 22's automated-decision opt-out)
- **Con:** Substantially more complex; coordination cost across N regime engines; cross-regime conflict resolution becomes its own substrate concern
- **Con:** Phase 1 substrate would ship the dispatcher with no engines — even less useful than Option A's substrate-without-rules

**Verdict:** Considered but rejected for v0; Option D is the *long-term* migration target if Option A's single rule engine becomes overloaded. Tracked as OQ-0064.4.

---

## Decision

**Option A — substrate framework + per-jurisdiction rule engine + general-counsel-engaged content layer.** Substrate ships in Phase 1; per-jurisdiction rule content ships in subsequent phases as legal sign-off completes, per-regime.

### Initial contract surface

Located in `Sunfish.Foundation.MissionSpace` (extends ADR 0062's package; same DI extension):

```csharp
namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Composite jurisdictional probe with per-source confidence.
/// </summary>
public sealed record JurisdictionProbe(
    string?         IpGeolocatedJurisdiction,        // e.g., "US-CA"; null if probe failed
    string?         UserDeclaredJurisdiction,        // explicit user/operator setting; null if not declared
    string?         TenantConfigJurisdiction,        // operator-config; null if not configured
    Confidence      OverallConfidence,
    DateTimeOffset  ProbedAt,
    ProbeStatus     Status                           // per ADR 0062-A1.10 — Healthy / Stale / Failed / PartiallyDegraded / Unreachable
);

public enum Confidence { High, Medium, Low }

/// <summary>
/// A single per-jurisdiction policy rule. Loaded from rule-content data files; not authored in C#.
/// </summary>
public sealed record JurisdictionalPolicyRule(
    string                  JurisdictionId,           // e.g., "US-CA"; matches Sunfish.Regulatory.Jurisdictions taxonomy
    RegulatoryRegime        Regime,
    string                  RuleId,                   // unique within (Jurisdiction, Regime); e.g., "ccpa-data-residency-export"
    LocalizedString         Description,              // human-readable; for audit + diagnostic surfaces
    PolicyEvaluationKind    Kind,                     // see enum below
    string                  StatutoryCitation,        // free-form; e.g., "California Civil Code §1798.83"; reader-caution per top-of-ADR
    PolicyEnforcementAction OnViolation,
    Confidence              MinimumProbeConfidence    // e.g., High for jurisdictions with strong enforcement; Medium for soft regimes
);

public enum PolicyEvaluationKind
{
    DataResidencyConstraint,
    DataExportConstraint,
    UserConsentRequirement,
    AutomatedDecisionGate,        // GDPR Art. 22-shaped
    SanctionsScreening,
    FeatureAvailabilityGate,      // generic "feature F is unavailable in jurisdiction J"
    NotificationRequirement       // e.g., "tenant must be notified of X"
}

public enum PolicyEnforcementAction
{
    Block,                        // feature returns DegradationKind.HardFail
    BlockWithExplanation,         // feature returns DegradationKind.DisableWithExplanation
    ReadOnly,                     // feature returns DegradationKind.ReadOnly
    AuditOnly,                    // feature proceeds; audit event emitted
    PromptUserConsent,            // feature gates on consent dialog
    OperatorOverridable           // feature blocks but operator may force-enable per ADR 0062-A1.9 OverridableWithCaveat
}

public interface IPolicyEvaluator
{
    /// <summary>
    /// Evaluates all relevant policy rules for the given (envelope, feature) pair.
    /// Returns aggregate verdict + per-rule details for audit + UX.
    /// </summary>
    ValueTask<PolicyVerdict> EvaluateAsync(
        MissionEnvelope envelope,
        string featureKey,
        CancellationToken ct = default
    );
}

public sealed record PolicyVerdict(
    PolicyVerdictState                   State,
    IReadOnlyList<PolicyRuleEvaluation>  RuleEvaluations,
    LocalizedString?                     UserMessage,
    LocalizedString?                     OperatorRecoveryAction,
    DegradationKind                      RecommendedDegradation     // matches ADR 0062 DegradationKind
);

public enum PolicyVerdictState { Pass, FailWithEnforcement, FailAuditOnly, IndeterminateProbeFailure }

public sealed record PolicyRuleEvaluation(
    string                      RuleId,
    string                      JurisdictionId,
    RegulatoryRegime            Regime,
    bool                        Passed,
    PolicyEnforcementAction     EnforcementApplied,
    LocalizedString             EvaluationNotes
);
```

### Regime acknowledgment surface

ADR 0064 ships an explicit list of regimes Sunfish acknowledges:

```csharp
public sealed record RegimeAcknowledgment(
    RegulatoryRegime         Regime,
    RegulatoryRegimeStance   Stance,
    LocalizedString          Description,
    string?                  ReaderCaution                 // free-form note; cited from this ADR's reader-caution preamble
);

public enum RegulatoryRegime
{
    HIPAA,
    GDPR,
    PCI_DSS_v4,
    SOC2,
    EU_AI_Act,
    FHA,
    CCPA,
    Other                   // catch-all for jurisdictions where Sunfish has rule-content but not a named regime
}

public enum RegulatoryRegimeStance
{
    InScope,                       // substrate aspires to enable conformance; rule-content layer authors policy rules per regime
    OutOfScopeOpenSource,          // substrate explicitly does not aspire (e.g., FedRAMP, ITAR — commercial-productization gates)
    CommercialProductOnly          // substrate is shape-aware; conformance ships with commercial productization (e.g., HIPAA BAA-required configurations)
}
```

Default regime stances (subject to general-counsel review):

| Regime | Stance | Notes (subject to legal review) |
|---|---|---|
| HIPAA | `CommercialProductOnly` | Substrate enables conformance shape (encrypted-field substrate per ADR 0046; audit substrate per ADR 0049); BAA + commercial productization required for actual HIPAA-covered deployments |
| GDPR | `InScope` | Substrate-tier consent + data-residency mechanisms ship in this ADR; downstream rule-content layer per-MS-jurisdiction |
| PCI_DSS_v4 | `CommercialProductOnly` | Substrate enables tokenization shape; PCI-DSS scope-reduction posture requires commercial productization |
| SOC2 | `InScope` | Audit substrate (ADR 0049) + identity (ADR 0032) + recovery (ADR 0046) compose into SOC 2-relevant controls; substrate-tier ships in this ADR |
| EU_AI_Act | `InScope (placeholder)` | No Sunfish AI/ML features today; substrate ships the tier-classification surface for future use |
| FHA | `InScope` | Per ADR 0057 — already substrate-tier-supported; ADR 0064 cross-cuts |
| CCPA | `InScope` | Substrate-tier + jurisdictional rule-content per California-specific provisions |

### Data-residency enforcement contract

```csharp
public sealed record DataResidencyConstraint(
    string                      RecordClassKey,           // e.g., "lease.tenant_demographics"
    IReadOnlySet<string>        AllowedJurisdictions,     // e.g., { "US-*" }
    IReadOnlySet<string>        ProhibitedJurisdictions,  // e.g., { "RU-*", "IR-*" }
    PolicyEnforcementAction     OnViolation
);

public interface IDataResidencyEnforcer
{
    ValueTask<EnforcementVerdict> EvaluateAsync(
        DataResidencyConstraint constraint,
        MissionEnvelope envelope,
        string recordIdentifier,
        CancellationToken ct = default
    );
}

public sealed record EnforcementVerdict(
    bool                        Allowed,
    PolicyEnforcementAction     ActionApplied,
    LocalizedString?            UserMessage,
    LocalizedString?            OperatorRecoveryAction
);
```

When `EnforcementVerdict.Allowed == false`, the consumer (typically a feature gate or write-path interceptor) MUST honor the `ActionApplied` per the `PolicyEnforcementAction` enum semantics (Block / ReadOnly / AuditOnly / etc.).

**Bridge-tier residency.** ADR 0031's Bridge accelerator is a hosted-SaaS surface; tenants on Bridge MAY have residency constraints that prohibit Bridge-tier processing for some record classes. The data-residency enforcer at the Bridge boundary applies BEFORE ciphertext touches Bridge storage — the constraint is an upstream gate, not a downstream filter.

### Sanctions handling

```csharp
public sealed record SanctionsScreeningResult(
    string                      ScreenedIdentifier,        // tenant ID, party ID, etc.
    bool                        HasMatch,
    IReadOnlyList<SanctionsListEntry> Matches,
    Confidence                  MatchConfidence,
    DateTimeOffset              ScreenedAt
);

public sealed record SanctionsListEntry(
    string                      ListSource,                // e.g., "OFAC SDN"; "EU consolidated"
    string                      MatchedFieldName,
    string                      MatchedValue,
    LocalizedString             ProvenanceNotes
);

public interface ISanctionsScreener
{
    /// <summary>
    /// Screens an identifier against configured sanctions lists.
    /// Returns SanctionsScreeningResult; consumer decides enforcement based on operator + legal-counsel policy.
    /// </summary>
    ValueTask<SanctionsScreeningResult> ScreenAsync(
        string identifier,
        IReadOnlyDictionary<string, string> screenableFields,
        CancellationToken ct = default
    );
}
```

**Sanctions screening is OPERATOR-decision territory, NOT substrate-default territory.** A match does NOT automatically block; the operator + legal counsel decides per-match what to do. ADR 0064's substrate emits `SanctionsScreeningHit` audit events on every match; the rule-content layer (or tenant-config) governs whether matches block, warn, or merely log.

### EU AI Act tier-classification placeholder

```csharp
public enum EuAiActTier
{
    Prohibited,                    // Art. 5 prohibited practices
    HighRisk,                      // Annex III high-risk systems
    LimitedRisk,                   // Art. 50 transparency obligations (chatbot disclosure, etc.)
    MinimalRisk,                   // No specific obligations beyond general
    NotApplicable                  // not an AI/ML system
}

public sealed record EuAiActTierClassification(
    string                      FeatureKey,
    EuAiActTier                 Tier,
    LocalizedString             ClassificationNotes,
    DateTimeOffset              ClassifiedAt
);
```

Today no Sunfish feature carries an `EuAiActTier ≠ NotApplicable`. The placeholder ships so future AI/ML feature additions have a substrate-tier classification surface.

### Probe mechanics

The jurisdictional probe combines three signals with confidence weighting:

| Signal | Source | Confidence | Cost class | Cache TTL |
|---|---|---|---|---|
| IP geo | Local IP-API or bundled MaxMind GeoLite2 | Low (VPNs / mobile roaming) | Medium (local query against bundled DB) | 5 minutes |
| User declaration | Explicit `MissionEnvelope.User.declaredJurisdiction` | High (when set; null otherwise) | Low (local) | 1 hour |
| Tenant config | Bridge subscription metadata + Anchor operator setting | High | Medium (cached Bridge fetch) | 1 hour |

**Composite confidence rule:**

- If user-declared OR tenant-config is `High`: composite is `High`; use that signal.
- If only IP-geo: composite is `Low`; verdict is `IndeterminateProbeFailure` for `MinimumProbeConfidence: High` rules, `Pass` for `Low`-min rules.
- If two signals disagree: composite is `Medium`; emit `JurisdictionProbedWithLowConfidence` audit; surface UX to user requesting explicit declaration.

The composite probe runs as part of ADR 0062's `IDimensionProbe<RegulatoryCapabilities>` implementation; ADR 0064's substrate provides the composite logic; ADR 0062's coordinator handles caching + change events per the post-A1 contract.

### Telemetry shape

7 new `AuditEventType` constants (per ADR 0049 emission contract; dedup per ADR 0028-A6.5.1 pattern):

| Event | Trigger | Payload | Dedup window |
|---|---|---|---|
| `PolicyEvaluated` | Each `IPolicyEvaluator.EvaluateAsync` call | `(feature_key, jurisdiction, regime_set, verdict_state, rule_count, latency_ms)` | 5-min per `(feature_key, jurisdiction)` |
| `PolicyEnforcementBlocked` | Verdict triggers `Block` / `BlockWithExplanation` / `ReadOnly` enforcement | `(feature_key, jurisdiction, regime, rule_id, enforcement_action)` | None (per-attempt) |
| `JurisdictionProbedWithLowConfidence` | Composite probe yields `Low` or `Medium` confidence | `(probed_jurisdiction, signal_breakdown, composite_confidence)` | 1-hour per `(jurisdiction, signal_breakdown_hash)` |
| `DataResidencyViolation` | Write-path attempts to ship a record-class to a prohibited jurisdiction | `(record_class_key, attempted_jurisdiction, prohibited_jurisdictions, action_applied)` | None (per-attempt; security-relevant) |
| `SanctionsScreeningHit` | Sanctions screener returns a match | `(screened_identifier, list_source, match_confidence, match_count)` | 1-day per `(screened_identifier, list_source)` |
| `RegimeAcknowledgmentSurfaced` | Regime acknowledgment surface rendered to user | `(regime, stance, locale)` | 7-day per `(regime, locale)` |
| `EuAiActTierClassified` | AI/ML feature tier classification recorded | `(feature_key, tier, classifier_id)` | 30-day per `(feature_key)` |

Capability-cohort analytics aggregate at the audit substrate per ADR 0049's snapshot mechanism (out of ADR 0064 scope).

### Rule-content layer (deferred to coordinated work products)

Per Option A's separation, the rule content lives in:

- `Sunfish.Regulatory.Jurisdictions@1.0.0` taxonomy charter (mirrors `Sunfish.Leasing.JurisdictionRules@1.0.0` per ADR 0057 — same shape; cross-domain consumer)
- Per-jurisdiction `JurisdictionalPolicyRule` JSON files at `data/regulatory-rules/{jurisdiction-id}/{regime}/{rule-id}.json`
- Rule-content authoring is **gated on general counsel sign-off per regime per jurisdiction** — substrate Phase 1 ships with empty rule-content; rule-content lands in subsequent phases as legal review completes per regime

---

## Consequences

### Positive

- **Cross-cutting regulatory substrate.** Per-domain ADRs (0057, 0060) compose with ADR 0064; future regulated-domain features inherit the substrate.
- **XO/legal-counsel separation.** Substrate is XO authority; rule-content is legal authority. Boundary is auditable.
- **Probe confidence is explicit.** Consumers reason about confidence; rules can require minimum confidence; high-confidence-required rules don't accidentally fire on weak IP-geo data.
- **Regime stance is explicit.** Sunfish-the-OSS-framework's stance vs HIPAA / FedRAMP / etc. is documented at substrate tier, not implied.
- **Data residency is enforceable.** Bridge-tier residency gating prevents prohibited-jurisdiction processing.
- **Sanctions screening is operator-decision-aware.** Substrate provides screening; operator+counsel decides enforcement; no silent false-positive blocks.
- **EU AI Act placeholder.** Substrate is shape-aware for future AI/ML features.

### Negative

- **Phase 1 substrate ships without rule content.** Consumers may misread substrate-only as "regulatory-compliant"; the substrate Phase 1 hand-off + apps/docs page MUST explicitly disclaim ("substrate is the framework; conformance requires rule-content + legal sign-off").
- **General counsel engagement is async + per-regime.** The rule-content layer's pace is governed by legal review, not engineering velocity; rule-content phases may stretch over months.
- **Substrate-vs-content separation has a coordination cost.** Engineers and counsel must agree on the substrate's shape before rule-content authoring starts; one-time cost.
- **Reader-caution preamble is repeated discipline.** Every consumer of ADR 0064 MUST surface the reader-caution; copy-paste discipline.

### Trust impact / Security & privacy

- **Audit trail is comprehensive.** Every policy evaluation emits `PolicyEvaluated`; every enforcement-block emits `PolicyEnforcementBlocked`; sanctions hits emit `SanctionsScreeningHit`. Regulatory defense (demonstrating consistent evaluation) is supported.
- **Data residency at Bridge-tier prevents ciphertext leakage.** Even encrypted-field data classified as residency-restricted does not transit prohibited Bridge regions.
- **Probe confidence prevents over-blocking.** Low-confidence jurisdictional probes trigger UX prompts (request explicit declaration), not silent blocks.
- **Force-enable composes with ADR 0062.** Operators may force-enable a regulated feature per ADR 0062-A1.9 `OverridableWithCaveat` policy; UX names the legal/regulatory consequence ("Force-enable acknowledges the operator assumes responsibility for jurisdictional non-compliance"); audit per `FeatureForceEnabled`.

---

## Compatibility plan

### Existing callers

No existing callers — substrate-tier introduction. Per-domain ADRs (0057, 0060) continue to work; migration to ADR 0064's substrate is opt-in.

**Migration order (recommended):**

1. **Phase 1 (substrate-only):** ship `Sunfish.Foundation.MissionSpace.Regulatory` substrate (types + interfaces + default `IPolicyEvaluator` consuming empty rule-content + 7 audit constants + DI extension). No rule content shipped.
2. **Phase 2 (jurisdiction taxonomy):** ship `Sunfish.Regulatory.Jurisdictions@1.0.0` taxonomy charter with seed of major jurisdictions (US-*, EU-*, UK, CA, AU, JP). No rule content.
3. **Phase 3 (per-regime rule content; legal-review-gated):** as legal sign-off completes per regime, ship rule-content JSON for that regime. ORDER: GDPR (most-mature legal review available); CCPA (single-jurisdiction; small scope); FHA (already supported via ADR 0057 — formalize in this layer); HIPAA (`CommercialProductOnly` — substrate-only Phase 3); PCI-DSS (`CommercialProductOnly` — substrate-only Phase 3); SOC 2 (`InScope` — engagement letter required); EU AI Act (placeholder; defer until first AI/ML feature).
4. **Phase 4 (per-domain ADR cross-cutting refactor):** ADR 0057 + ADR 0060 grow `IPolicyEvaluator` consumers; their existing rule logic migrates to rule-content data files.
5. **Phase 5+ (commercial productization gates):** HIPAA + PCI-DSS + FedRAMP commercial-productization work — out-of-scope for this ADR; tracked as future revisit.

### Affected packages

- New: `packages/foundation-mission-space-regulatory/` (or extends `foundation-mission-space/` per ADR 0062 precedent) — substrate types + interfaces + DI extension.
- Modified: `packages/foundation-taxonomy/` consumer — `Sunfish.Regulatory.Jurisdictions@1.0.0` charter authored.
- New: `data/regulatory-rules/` directory tree for rule-content data files (Phase 3+).
- Modified (Phase 4): `packages/blocks-property-leasing-pipeline/` (consumes ADR 0057 → ADR 0064); `packages/blocks-property-...` for any other regulated domains.
- Modified: `apps/docs/foundation/regulatory-policy/` walkthrough.

### Migration

Existing per-domain ADRs continue to work without modification. Phase 4's cross-cutting refactor is opt-in per domain. The substrate-only Phase 1 ships; Phase 3 rule-content lands per-regime as legal review completes; Phase 4 refactors land per-domain as those domain owners choose.

---

## Implementation checklist

- [ ] `Sunfish.Foundation.MissionSpace.Regulatory` substrate types per §"Initial contract surface"
- [ ] `IPolicyEvaluator` interface + `DefaultPolicyEvaluator` implementation (consumes rule-content data files; empty in Phase 1)
- [ ] `IDataResidencyEnforcer` + `DefaultDataResidencyEnforcer` (Bridge-boundary aware; consumes operator config + tenant subscription)
- [ ] `ISanctionsScreener` + `DefaultSanctionsScreener` (consumes operator-configured sanctions list source; OFAC SDN + EU consolidated as Phase 3 wiring)
- [ ] `RegimeAcknowledgment` records + default `regime_stances.json` data file per the Decision Drivers table
- [ ] DI extension: `AddSunfishRegulatoryPolicy()` registering the evaluator + enforcer + screener + audit factories
- [ ] 7 new `AuditEventType` constants per §"Telemetry shape"
- [ ] `RegulatoryAuditPayloadFactory` (mirrors ADR 0062-A1 + ADR 0028-A6.6 patterns)
- [ ] Audit dedup at the emission boundary per ADR 0028-A6.5.1 pattern
- [ ] `Sunfish.Regulatory.Jurisdictions@1.0.0` taxonomy charter + seed (Phase 2)
- [ ] `data/regulatory-rules/` directory tree skeleton + per-regime README placeholders (rule-content authoring is per-regime + legal-review-gated)
- [ ] Test coverage:
  - Composite-probe confidence-weighting test (3 signals × 3 confidence levels = 27 cases)
  - `IPolicyEvaluator` empty-rule-content test (substrate proves it doesn't crash with no rules)
  - `IPolicyEvaluator` synthetic-rule test (HIPAA-shaped rule + GDPR-shaped rule + FHA-shaped rule)
  - Data-residency Bridge-boundary test (record-class with `ProhibitedJurisdictions = { "Bridge-EU" }` blocks at upstream gate)
  - Sanctions screener match-emission test
  - Force-enable + audit shape test (composes with ADR 0062 `FeatureForceEnabled` + ADR 0062-A1.9 OverridableWithCaveat)
  - Reader-caution UX-surface test (apps/docs walkthrough renders the caution explicitly; `RegimeAcknowledgmentSurfaced` audit fires)
- [ ] Cited-symbol verification per the 3-direction spot-check rule (positive + negative + structural-citation per A7 lesson + ADR 0063-A1.15 lesson on §A0 audit insufficiency)
- [ ] `apps/docs/foundation/regulatory-policy/` walkthrough page (with reader-caution preamble surfaced prominently)

---

## Open questions

- **OQ-0064.1:** Should `RegimeAcknowledgment` be loadable from operator config (per-deployment override of default stances)? Recommend: yes — operators with commercial-productization may set `HIPAA = InScope` for their deployment after BAA + commercial productization gates are met. Default is the table above; operator override is per-deployment.
- **OQ-0064.2:** Should the rule-content data file format be JSON or YAML? Recommend JSON (matches CanonicalJson convention; avoids YAML's "Norway problem" with unquoted strings); accept slight loss of human-friendliness in trade.
- **OQ-0064.3:** Should `IDataResidencyEnforcer` cache verdicts? Recommend: yes — same `(record_class_key, jurisdiction)` evaluation should produce identical verdict; cache per ADR 0062 Medium cost class (5-minute TTL).
- **OQ-0064.4:** Federation pattern (Option D) — when is it triggered? Recommend: revisit when single-engine evaluation latency exceeds P95 100ms OR when regime-specific evaluation logic diverges (e.g., GDPR Article 22 evaluation has substantively different shape than HIPAA Security Rule evaluation).
- **OQ-0064.5:** Sanctions list update cadence — daily? weekly? Recommend: daily for OFAC SDN (updated daily by Treasury); weekly for EU consolidated; configurable per deployment.
- **OQ-0064.6:** EU AI Act Annex III high-risk systems — when do we activate the substrate? Recommend: when first Sunfish AI/ML feature is proposed; not before. Until then, all features carry `EuAiActTier.NotApplicable`.
- **OQ-0064.7:** Rule-content versioning — how do rule-updates ship? Recommend: rule-content data files carry semver in their filename or metadata; substrate consumes the latest version per `(jurisdiction, regime, rule-id)` triple; deprecation per the version field.

---

## Halt conditions for Stage 06

ADR 0064 Stage 06 build is gated on ALL of the following:

1. **General counsel engagement letter or equivalent legal-review framework agreement** with a qualified attorney covering the regimes named with `RegulatoryRegimeStance.InScope`. **Phase 1 substrate-only build does NOT halt on this; Phase 3+ rule-content authoring does halt.**
2. **`feedback_decision_discipline.md` Rule 6 verification** of every cited Sunfish.* symbol per the 3-direction spot-check rule (per ADR 0063-A1.15 lesson — §A0 self-audit failed all 4 of ADR 0063's structural-citation findings; council remains canonical).
3. **Reader-caution preamble surfaced in every `apps/docs/foundation/regulatory-policy/` page** + the substrate Phase 1 hand-off + every PR description that touches `data/regulatory-rules/`.
4. **Pedantic-Lawyer perspective subagent dispatched as part of Stage 1.5 council** for ADR 0064 itself (not just for the rule-content per regime). Precedent: W#33 §5.9 hardening pass + the parent intake's reader-caution.
5. **Bridge-tier data-residency enforcer wired BEFORE first record-class with prohibited-jurisdictions ships** — the upstream gate must be in place before any data path that depends on it goes live.

If any halt-condition is unsatisfied, the Phase 3+ rule-content build paused; Phase 1 substrate may continue.

---

## Revisit triggers

- **Federation pattern need surfaces.** Per OQ-0064.4 — single-engine latency or regime-specific divergence drives revisit.
- **First AI/ML feature is proposed.** Per OQ-0064.6 — EU AI Act activation drives revisit.
- **Commercial productization gates are met.** HIPAA / PCI-DSS / FedRAMP commercial productization work activates the substrate-aware-but-not-conforming regimes.
- **Rule-content authoring at scale exposes substrate gaps.** As legal sign-off generates rule-content per regime, missing substrate primitives surface; ADR 0064-A_n amendments pick them up.
- **Sanctions list update cadence inadequate.** Per OQ-0064.5 — operator-configurable cadence may need lower bound.

---

## References

- W#33 Mission Space Matrix discovery: [`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`](../../icm/01_discovery/output/2026-04-30_mission-space-matrix.md) §5.9 + §6.3 + §7.2
- Intake: [`icm/00_intake/output/2026-04-30_runtime-regulatory-policy-evaluation-intake.md`](../../icm/00_intake/output/2026-04-30_runtime-regulatory-policy-evaluation-intake.md)
- ADR 0057 (FHA documentation-defense; structural pattern reusable; per-domain regulatory rule precedent)
- ADR 0060 (Right-of-Entry per-jurisdiction rules; concrete per-jurisdiction citation pattern)
- ADR 0062 (Mission Space Negotiation Protocol; post-A1) — `Regulatory` dimension consumer
- ADR 0063 (Mission Space Requirements; post-A1) — `RegulatorySpec` install-time gate consumer
- ADR 0049 (audit substrate) — telemetry emission target
- ADR 0009 (Edition / IEditionResolver) — commercial-tier surface; `RegulatoryRegimeStance.CommercialProductOnly` cross-references
- ADR 0031 (Bridge hybrid multi-tenant SaaS) — Bridge-tier residency boundary
- ADR 0056 (Foundation.Taxonomy) — jurisdictional taxonomy home
- Paper [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) §20.4 (regulatory factors as architectural filter) + §16 (IT governance posture)
- W#22 Leasing Pipeline (Phase 6 compliance half deferred — direct downstream consumer)
- W#28 Public Listings (jurisdiction-aware rendering — direct downstream consumer)
- GDPR Articles 22 / 44 / 45 / 46 (transfers + automated decision-making)
- HIPAA Privacy Rule (45 CFR §§164.500–164.534) + Security Rule (Subpart C: 45 CFR §§164.302–164.318) — administrative / physical / technical safeguards
- PCI-DSS v4.0
- EU AI Act (Regulation EU 2024/1689; Arts. 5–6 + Annex III)
- OFAC SDN / sectoral lists; EU consolidated sanctions list

**Reader caution applies to all statutory citations above.**

---

## Sibling amendment dependencies named

- **ADR 0056 (Foundation.Taxonomy) consumer:** `Sunfish.Regulatory.Jurisdictions@1.0.0` taxonomy charter Phase 2 work product; not a separate ADR amendment but a coordinated taxonomy charter.
- **ADR 0057 (FHA documentation-defense):** Phase 4 cross-cutting refactor migrates ADR 0057's existing rule logic to ADR 0064's substrate; coordinated A_n amendment to ADR 0057 acknowledging the refactor.
- **ADR 0060 (Right-of-Entry):** Phase 4 cross-cutting refactor; coordinated A_n amendment to ADR 0060 acknowledging the refactor.
- **W#22 Phase 6 compliance half:** ADR 0064 unblocks the deferred compliance half of W#22; coordination via the W#22 hand-off addendum (post ADR 0064 Phase 1 + relevant Phase 3 rule-content per the FHA/CCPA/FCRA regimes).

---

## Cohort discipline

Per `feedback_decision_discipline.md` cohort batting average (14-of-14 substrate amendments needing council fixes; structural-citation failure rate 10-of-14 (~71%) XO-authored amendments; §A0 self-audit catch-rate 0-of-4 on ADR 0063):

- **Pre-merge council canonical** for ADR 0064. Auto-merge intentionally DISABLED until a Stage 1.5 council subagent reviews. The council MUST include a **Pedantic-Lawyer perspective subagent** in addition to the standard 4-perspective adversarial council (precedent: W#33 §5.9 hardening pass; the parent intake's reader-caution explicitly names this).
- Council should specifically pressure-test:
  - **Reader-caution discipline** — does every consumer-facing surface (apps/docs, hand-off, PR description) reproduce the reader-caution? Is the mechanism for enforcing this auditable?
  - **XO/legal-counsel boundary** — is the Phase 1-vs-Phase 3 separation cleanly defensible, or does Phase 1 substrate-only inadvertently make conformance claims?
  - **Composite-confidence rule for jurisdictional probe** — does the rule produce reasonable verdicts in all 27 cases (3 signals × 3 confidence levels)? Edge cases: VPN-shifted IP-geo + truthful user-declaration; tenant-config staleness on operator transition.
  - **Sanctions screening's operator-decision-aware shape** — is the substrate genuinely operator-decision-aware, or does it produce false-positives that silently block at runtime?
  - **EU AI Act placeholder** — is the placeholder structurally correct (Art. 5 / Annex III tier classifications) or does it need a real lawyer's review even at placeholder stage?
  - **Statutory citation hygiene** — every statutory citation reads with reader-caution; council surfaces any citation that has practitioner shorthand vs Official Code text.
  - **Force-enable + ADR 0062 OverridableWithCaveat composition** — does the runtime UX correctly surface the legal/regulatory caveat, or is the caveat inadvertently softened by translation/localization?
  - **The 7 new audit-event constants** — collision check against existing `AuditEventType` constants in `packages/kernel-audit/`.
- **Cited-symbol verification** per §A0 + 3-direction spot-check at draft time, AND council-side spot-check (per ADR 0063-A1.15 lesson)
- **Standing rung-6 spot-check** within 24h of ADR 0064 merging (per ADR 0028-A4.3 + A7.12 + A8.12 + ADR 0062-A1.15 + ADR 0063-A1.16 commitment)
- **Pedantic-Lawyer perspective is REQUIRED for the council**, not optional (per the parent intake explicit halt-condition + W#33 §5.9 precedent).
