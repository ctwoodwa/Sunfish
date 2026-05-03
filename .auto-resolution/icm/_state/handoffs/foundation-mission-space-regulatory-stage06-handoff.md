# Hand-off — Foundation.MissionSpace.Regulatory substrate Phase 1 (ADR 0064 A1.10 Phase 1)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-01
**Status:** `ready-to-build`
**Spec source:** [ADR 0064 + A1](../../docs/adrs/0064-runtime-regulatory-policy-evaluation.md) (post-A1 council-fixed; landed via PR #415 + post-A1 fixes)
**Approval:** ADR 0064 Accepted on origin/main; A1 amendment absorbed all 4 Required council recommendations; council batting average 18-of-18
**Estimated cost:** ~8–12h sunfish-PM (foundation-tier substrate package + ~14 type signatures + composite-confidence probe + rule engine + data-residency enforcer + sanctions screener + 10 audit constants + ~30–40 tests + DI + apps/docs page)
**Pipeline:** `sunfish-feature-change`
**Audit before build:** `ls /Users/christopherwood/Projects/Sunfish/packages/ | grep mission-space-regulatory` to confirm no collision (audit not yet run; COB confirms before commit)

---

## Reader caution (Pedantic-Lawyer carry-forward; per ADR 0064-A1.2)

**Sunfish does not provide legal advice; this substrate is not a substitute for qualified counsel.** Phase 1 substrate ships the framework only — the *content* of policy rules per jurisdiction is a legal-review work product NOT in this hand-off scope (per ADR 0064-A1.10's Phase 3 separation). General counsel MUST engage before Phase 3 rule-content authoring. Phase 1 substrate-only deployments are NOT regulatory-compliant by virtue of the substrate alone (per ADR 0064-A1.8 explicit deployability disclaimer).

---

## Context

Phase 1 lands the Foundation.MissionSpace.Regulatory substrate per ADR 0064-A1.10's 5-phase migration plan. **Phase 1 is substrate-only** — types + interfaces + default empty rule-content evaluation + 10 audit constants + DI extension. **Per-jurisdiction rule content (Phase 3) is gated on legal sign-off** and ships as a separate work product; Phase 4 cross-cutting refactor of ADR 0057 + ADR 0060 happens after Phase 3.

**Substrate scope:** `Sunfish.Foundation.MissionSpace.Regulatory` package + `IPolicyEvaluator` + `IDataResidencyEnforcer` + `ISanctionsScreener` + composite-confidence `JurisdictionProbe` + 10 new `AuditEventType` constants (per A1.7 + A1.12.5) + Bridge-tier `DataResidencyEnforcerMiddleware` (per A1.4) + DI extension + apps/docs page. Substrate-only; consumer wiring (W#22 Phase 6 compliance half + ADR 0057 + 0060 cross-cutting refactor) is separate workstreams.

This hand-off mirrors the W#34 + W#35 + W#36 substrate-only patterns COB has executed successfully.

---

## Files to create

### Package scaffold

```
packages/foundation-mission-space-regulatory/
├── Sunfish.Foundation.MissionSpace.Regulatory.csproj
├── README.md
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs        (AddInMemoryRegulatoryPolicy; mirrors W#34 P5 + W#35 P5 + W#36 P5 shape)
├── Models/
│   ├── JurisdictionProbe.cs                  (record per A1.4 composite-confidence shape)
│   ├── Confidence.cs                         (enum: High / Medium / Low)
│   ├── JurisdictionalPolicyRule.cs           (record per A1.6 rule-engine shape; with RelevantFeatures: IReadOnlySet<string>?)
│   ├── PolicyEvaluationKind.cs               (enum: DataResidencyConstraint / DataExportConstraint / UserConsentRequirement / AutomatedDecisionGate / SanctionsScreening / FeatureAvailabilityGate / NotificationRequirement)
│   ├── PolicyEnforcementAction.cs            (enum: Block / BlockWithExplanation / ReadOnly / AuditOnly / PromptUserConsent / OperatorOverridable)
│   ├── PolicyVerdict.cs                      (record per A1.6)
│   ├── PolicyVerdictState.cs                 (enum: Pass / FailWithEnforcement / FailAuditOnly / IndeterminateProbeFailure)
│   ├── PolicyRuleEvaluation.cs               (record per A1.6)
│   ├── RegimeAcknowledgment.cs               (record per A1.6 default stances table)
│   ├── RegulatoryRegime.cs                   (enum: HIPAA / GDPR / PCI_DSS_v4 / SOC2 / EU_AI_Act / FHA / CCPA / Other)
│   ├── RegulatoryRegimeStance.cs             (enum: InScope / ExplicitlyDisclaimedOpenSource / CommercialProductOnly per A1.13 reframe)
│   ├── DataResidencyConstraint.cs            (record per A1.6)
│   ├── EnforcementVerdict.cs                 (record per A1.6)
│   ├── SanctionsScreeningResult.cs           (record per A1.6)
│   ├── SanctionsListEntry.cs                 (record per A1.6)
│   ├── ScreeningPolicy.cs                    (enum: Default / AdvisoryOnly per A1.3 opt-out path)
│   └── EuAiActTierClassification.cs          (record per A1.6)
├── Services/
│   ├── IPolicyEvaluator.cs                   (per A1.6 contract)
│   ├── DefaultPolicyEvaluator.cs             (consumes empty rule-content; substrate Phase 1 silent-pass)
│   ├── IDataResidencyEnforcer.cs             (per A1.6)
│   ├── DefaultDataResidencyEnforcer.cs       (Bridge-boundary aware)
│   ├── ISanctionsScreener.cs                 (per A1.6)
│   └── DefaultSanctionsScreener.cs           (operator-decision-aware emit-only; supports ScreeningPolicy.AdvisoryOnly per A1.3)
├── Probes/
│   ├── ICompositeJurisdictionProbe.cs        (composite per A1.5; signal-weighted with tie-breaker)
│   └── DefaultCompositeJurisdictionProbe.cs  (3-signal: IP-geo + user-declaration + tenant-config)
├── Audit/
│   └── RegulatoryAuditPayloads.cs            (factory; 10 event types per A1.7 + A1.3 + A1.12.5)
├── Localization/
│   └── RegulatoryPolicyKeys.cs               (default localization keys per A1.13 + A1.5)
├── Bridge/
│   ├── IDataResidencyEnforcerMiddleware.cs   (Bridge-tier middleware contract per A1.4)
│   └── DataResidencyEnforcerMiddleware.cs    (default impl; HTTP 451 RFC 7725 response)
└── tests/
    └── Sunfish.Foundation.MissionSpace.Regulatory.Tests.csproj
        ├── JurisdictionProbeTests.cs         (composite-confidence; 27 cases per A1.15 tie-breaker rule)
        ├── PolicyEvaluatorEmptyRuleContentTests.cs  (substrate proves it doesn't crash with no rules)
        ├── PolicyEvaluatorSyntheticRuleTests.cs  (HIPAA-shaped + GDPR-shaped + FHA-shaped synthetic rules)
        ├── DataResidencyEnforcerTests.cs    (Bridge-boundary + record-class with ProhibitedJurisdictions blocks at upstream gate)
        ├── DataResidencyMiddlewareTests.cs   (HTTP 451 RFC 7725 response per A1.4)
        ├── SanctionsScreenerTests.cs         (match-emission test + ScreeningPolicy.AdvisoryOnly opt-out)
        ├── ForceEnableComposesWith0062Tests.cs  (force-enable + ADR 0062 OverridableWithCaveat composition)
        ├── AuditEmissionTests.cs              (10 AuditEventType constants emit on right triggers + dedup)
        ├── DefaultRegimeStancesTests.cs       (post-A1.13 reframed table: PCI-DSS = ExplicitlyDisclaimedOpenSource)
        └── DiExtensionTests.cs                (audit-disabled / audit-enabled overloads; both-or-neither at registration boundary)
```

### Type definitions (post-A1 surface; implement exactly per ADR 0064 + A1)

Use the types as A1.6 specs them. **Apply A1.1 (GDPR Article 25 cited), A1.2 (legal-advice disclaimer in apps/docs), A1.3 (ScreeningPolicy.AdvisoryOnly opt-out + 8th audit constant SanctionsAdvisoryOnlyConfigured), A1.4 (DataResidencyEnforcerMiddleware + HTTP 451 RFC 7725), A1.5 (audit retention table — informational; not enforced in Phase 1 code), A1.6 (cost class Medium + RelevantFeatures rule-keying), A1.7 (cache invalidation on probe-status transition), A1.13 (renamed enum + PCI-DSS reframe), A1.14 (canonical JSON schema document), A1.15 (composite-confidence tie-breaker), A1.16 (rule-content versioning + sanctions list reload).**

### Audit constants (10 per A1.7 + A1.3 + A1.12.5)

`AuditEventType` MUST gain 10 new constants in `packages/kernel-audit/AuditEventType.cs`:

```csharp
public static readonly AuditEventType PolicyEvaluated                    = new("PolicyEvaluated");
public static readonly AuditEventType PolicyEnforcementBlocked           = new("PolicyEnforcementBlocked");
public static readonly AuditEventType JurisdictionProbedWithLowConfidence = new("JurisdictionProbedWithLowConfidence");
public static readonly AuditEventType DataResidencyViolation             = new("DataResidencyViolation");
public static readonly AuditEventType SanctionsScreeningHit              = new("SanctionsScreeningHit");
public static readonly AuditEventType RegimeAcknowledgmentSurfaced       = new("RegimeAcknowledgmentSurfaced");
public static readonly AuditEventType EuAiActTierClassified              = new("EuAiActTierClassified");
public static readonly AuditEventType SanctionsAdvisoryOnlyConfigured    = new("SanctionsAdvisoryOnlyConfigured"); // A1.3
// + 2 more from A1.7 audit retention surfacing (deferred to Phase 1 acceptance review)
```

Total: 8 base + 2 from audit-retention surfacing = 10 constants. Per A1.12.5 + A1.6 collision check completed in council review (no collisions).

`RegulatoryAuditPayloads` factory mirrors `LeaseAuditPayloadFactory` shape (alphabetized keys; canonical-JSON-serialized; per ADR 0049 emission contract).

### Default regime stances (post-A1.13)

```csharp
// Default regime-stance table (subject to legal-counsel review per A1.13):
// (post-A1.13 stance reframe + A1.19 alphabetization within stance-cluster)
public static readonly IReadOnlyList<RegimeAcknowledgment> DefaultRegimeStances = new[]
{
    // InScope (alphabetized)
    new RegimeAcknowledgment(RegulatoryRegime.CCPA,        RegulatoryRegimeStance.InScope, ...),
    new RegimeAcknowledgment(RegulatoryRegime.EU_AI_Act,   RegulatoryRegimeStance.InScope, ...),  // placeholder
    new RegimeAcknowledgment(RegulatoryRegime.FHA,         RegulatoryRegimeStance.InScope, ...),
    new RegimeAcknowledgment(RegulatoryRegime.GDPR,        RegulatoryRegimeStance.InScope, ...),
    new RegimeAcknowledgment(RegulatoryRegime.SOC2,        RegulatoryRegimeStance.InScope, ...),
    // CommercialProductOnly
    new RegimeAcknowledgment(RegulatoryRegime.HIPAA,       RegulatoryRegimeStance.CommercialProductOnly, ...),
    // ExplicitlyDisclaimedOpenSource (per A1.13 reframe)
    new RegimeAcknowledgment(RegulatoryRegime.PCI_DSS_v4,  RegulatoryRegimeStance.ExplicitlyDisclaimedOpenSource, ...),
};
```

---

## Phase breakdown (~5 PRs, ~8–12h total — mirrors W#34/W#35/W#36 shape)

### Phase 1 — Substrate scaffold + core types (~2-3h, 1 PR)

- Package created at `packages/foundation-mission-space-regulatory/` with foundation-tier csproj
- All Models per the spec block above (~16 record/enum types)
- 10 new `AuditEventType` constants in `packages/kernel-audit/AuditEventType.cs`
- `JurisdictionProbe` round-trip via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` test (camelCase per ADR 0028-A7.8)
- `JsonStringEnumConverter` for all 6 enum types (`Confidence`, `PolicyEvaluationKind`, `PolicyEnforcementAction`, `RegulatoryRegime`, `RegulatoryRegimeStance`, `ScreeningPolicy`)
- `JurisdictionalPolicyRule` schema + canonical JSON Schema document at `data/regulatory-rules/jurisdictional-policy-rule.schema.json` per A1.14
- README.md + reader-caution preamble (per A1.2 affirmative legal-advice disclaimer)
- ~6–10 unit tests on Models alone

### Phase 2 — Composite-confidence probe + rule engine + data-residency enforcer (~2-3h, 1 PR)

- `ICompositeJurisdictionProbe` + `DefaultCompositeJurisdictionProbe` per A1.5 + A1.15 tie-breaker rule (user-declaration > tenant-config > IP-geo)
- `IPolicyEvaluator` + `DefaultPolicyEvaluator` per A1.6 + cost class Medium + RelevantFeatures rule-keying pre-filter
- `IDataResidencyEnforcer` + `DefaultDataResidencyEnforcer` per A1.6 + Bridge-boundary aware
- 27-case composite-confidence test matrix (3 signals × 3 confidence levels)
- 5 synthetic-rule tests (HIPAA / GDPR / FHA / SOC 2 / CCPA)
- Cache invalidation on probe-status transition per A1.7

### Phase 3 — Sanctions screener + ScreeningPolicy.AdvisoryOnly opt-out + Bridge middleware (~2-3h, 1 PR)

- `ISanctionsScreener` + `DefaultSanctionsScreener` per A1.6 + operator-decision-aware emit-only
- `ScreeningPolicy.AdvisoryOnly` opt-out path per A1.3 + `SanctionsAdvisoryOnlyConfigured` audit
- `IDataResidencyEnforcerMiddleware` + `DataResidencyEnforcerMiddleware` per A1.4 (Bridge ASP.NET Core middleware; HTTP 451 RFC 7725 response)
- Sanctions match-emission tests + opt-out registration flow tests
- Middleware integration test (synthetic Bridge request with prohibited-jurisdiction record-class)

### Phase 4 — Audit emission + dedup wiring (~1-2h, 1 PR)

- `RegulatoryAuditPayloads` factory (alphabetized keys per ADR 0049 convention)
- 10 new `AuditEventType` constants connected; per-event dedup wiring per W#34 P4 ConcurrentDictionary pattern
- Two-overload constructor (audit-disabled / audit-enabled both-or-neither) per W#32 + W#34 + W#35 precedent
- Audit dedup tests (matching the per-event dedup windows from ADR 0064-A1.7)

### Phase 5 — DI extension + apps/docs + ledger flip (~1-2h, 1 PR)

- `AddInMemoryRegulatoryPolicy()` DI extension (audit-disabled + audit-enabled overloads; both-or-neither at registration; mirrors W#34 P5 + W#35 P5 + W#36 P5)
- `apps/docs/foundation-mission-space-regulatory/overview.md` walkthrough page (cite ADR 0064 + post-A1 surface explicitly + reader-caution preamble verbatim per A1.2)
- Active-workstreams.md row 39 flipped from `building` → `built` with PR list

---

## Halt-conditions (cob-question if any of these surface)

1. **Reader-caution propagation discipline (per A1.18 + ADR 0064 halt-condition #6).** Phase 1 substrate hand-off MUST include an automated apps/docs build-step that fails the build if any page in `apps/docs/foundation-mission-space-regulatory/` lacks the canonical reader-caution string. If implementing this build-step gate is awkward, file `cob-question-*` beacon — XO will ship a pre-Phase-5 hand-off addendum specifying the exact build-step shape (likely a simple grep-or-regex via PowerShell / bash).

2. **`MinimumSpec` cross-package availability.** Per W#36/W#37 cohort lessons, this substrate may consume types from `Sunfish.Foundation.MissionSpace` (post-ADR-0062-A1 surface). If `Sunfish.Foundation.MissionSpace` is not yet built (it's queued separately), file `cob-question-*` beacon — the answer may be "ship Foundation.MissionSpace.Regulatory as a separate package consuming `Sunfish.Foundation.MissionSpace` interfaces directly via reference; the package can compile independently if the .Regulatory.csproj depends on a stub project for now."

3. **Empty rule-content silent-pass behavior (per A1.8).** Phase 1 substrate's `DefaultPolicyEvaluator` evaluates against an empty rule set and returns `Pass` for every evaluation. **DO NOT misread this as "Sunfish is regulatory-compliant by virtue of the substrate alone."** Phase 1 deployability is gated on the explicit disclaimer per A1.8. If COB feels the silent-pass behavior is awkward, file `cob-question-*` beacon — the answer is likely "this is intentional; the disclaimer in apps/docs handles the user-facing concern."

4. **Bridge-tier `DataResidencyEnforcerMiddleware` ASP.NET Core integration.** Per A1.4, the middleware lives in `Sunfish.Bridge.Middleware` namespace. If the canonical Bridge accelerator middleware path differs (e.g., it lives in `Sunfish.Bridge.AspNetCore.Middleware` or similar), file `cob-question-*` beacon — XO will document the canonical path in a hand-off addendum.

5. **HTTP 451 RFC 7725 `Retry-After` semantic.** A1.4 specifies HTTP 451 (Unavailable for Legal Reasons; RFC 7725) for residency-blocked requests. RFC 7725 recommends including a `Retry-After` header when the legal block is time-bounded (e.g., "this jurisdiction's restriction lifts on date X"). For Phase 1 substrate, the middleware MAY include a `Retry-After: never` semantic OR omit `Retry-After` entirely. If the operator-side audit shape needs a specific semantic, file `cob-question-*` beacon.

6. **`EuAiActTierClassification` placeholder usage in Phase 1.** Per A1.6 + A1.10, no Sunfish feature carries `EuAiActTier ≠ NotApplicable` today; Phase 1 ships the type but no `EuAiActTierClassified` audit emission paths. If COB tries to wire a synthetic AI/ML classification for testing, file `cob-question-*` beacon — the substrate MAY ship without a runtime emission path; tests should use synthetic in-memory classification entries.

7. **Force-enable composition with ADR 0062.** Per A1.11 council fix: force-enable surface displays a fact-disclosure UX, NOT a liability transfer. The substrate-tier `IPolicyEvaluator` + `IDataResidencyEnforcer` SHOULD compose with ADR 0062's `ForceEnablePolicy.OverridableWithCaveat` for the regulatory dimension (A1.9). If the wiring requires reaching into ADR 0062's `IFeatureGate` surface unexpectedly, file `cob-question-*` beacon.

8. **Legal-counsel engagement letter for Phase 3 work.** Per ADR 0064 §"Halt conditions for Stage 06" item #1 + A1.13 PCI-DSS stance reframe (subject to counsel review). Phase 1 substrate-only does NOT halt on counsel; Phase 3+ rule-content authoring HALTS until engagement letter exists. If COB feels Phase 1 should ship rule-content for any regime to "prove" the substrate works, file `cob-question-*` beacon — the answer is "no rule content in Phase 1; synthetic in-memory rule entries are fine for tests; production rule-content waits for counsel."

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-01):**

- ADR 0064 + A1 (PR #415 merged) — substrate spec source ✓
- ADR 0062 + A1 (PR #406 merged) — `MissionEnvelope.Regulatory` dimension surface + `IFeatureGate` ✓
- ADR 0063 + A1 (PR #411 merged) — `RegulatorySpec(AllowedJurisdictions, ProhibitedJurisdictions, RequiredConsents)` ✓
- ADR 0049 (audit substrate) ✓
- ADR 0009 (Edition / IEditionResolver) ✓
- ADR 0031 (Bridge accelerator) ✓
- ADR 0046 (encrypted-field substrate) — not directly consumed in Phase 1 ✓
- ADR 0056 (Foundation.Taxonomy) — for `Sunfish.Regulatory.Jurisdictions@1.0.0` charter (Phase 2) ✓
- ADR 0057 (FHA documentation-defense) — Phase 4 cross-cutting refactor target ✓
- ADR 0060 (Right-of-Entry per-jurisdiction) — Phase 4 cross-cutting refactor target ✓
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` ✓
- `Sunfish.Kernel.Audit.AuditEventType` ✓
- HTTP 451 RFC 7725 — IETF standard ✓

**Introduced by this hand-off** (ship in Phase 1):

- New package: `Sunfish.Foundation.MissionSpace.Regulatory`
- ~16 new types per Models block above
- 10 new `AuditEventType` constants
- `data/regulatory-rules/jurisdictional-policy-rule.schema.json` JSON Schema document per A1.14
- `Sunfish.Bridge.Middleware.DataResidencyEnforcerMiddleware` per A1.4
- Reader-caution disclaimer in apps/docs (per A1.2)

**Cohort lesson reminder (per ADR 0028-A10 + ADR 0063-A1.15):** §A0 self-audit pattern is necessary but NOT sufficient. COB should structurally verify each Sunfish.* symbol exists (read actual cited file's schema; don't grep alone) before declaring AP-21 clean.

---

## Cohort discipline

This hand-off is **not** a substrate ADR amendment; it's a Stage 06 hand-off implementing post-A1-fixed surface. Pre-merge council on this hand-off is NOT required.

- COB's standard pre-build checklist applies
- W#34 + W#35 + W#36 cohort lessons incorporated: ConcurrentDictionary dedup; two-overload constructor both-or-neither; JsonStringEnumConverter for enums; AddInMemoryX() DI extension; apps/docs/{tier}/X/overview.md page convention
- **Reader-caution discipline** (Pedantic-Lawyer-driven; A1.2 + A1.18) is NEW to this hand-off — every consumer-facing artifact MUST surface the canonical caution string

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w39-{slug}.md` in `icm/_state/research-inbox/`
- Halt the workstream + add a note in active-workstreams.md row 39
- ScheduleWakeup 1800s

If COB completes Phase 5 + drops to fallback:

- Drop `cob-idle-2026-05-XXTHH-MMZ-{slug}.md` to research-inbox
- Continue with rung-1 dependabot + rung-2 build-hygiene per CLAUDE.md fallback

---

## Cross-references

- Spec source: ADR 0064 + A1 (PR #415 merged 2026-05-01)
- Council that drove A1: PR #422 (merged); council file at `icm/07_review/output/adr-audits/0064-council-review-2026-04-30.md`
- Sibling workstreams in flight / queued: W#23 ready-to-build; W#34 built; W#35 built; W#36 building (P1+P2 shipped per #451/#453); W#37 stuck on commitlint+conflict (per CO direction); W#38 ready-to-build; W#39 (this hand-off)
- Phase 4 cross-cutting refactor targets: ADR 0057 + ADR 0060 (gated on Phase 1 + Phase 3 substrate + rule-content; not in this hand-off scope)
- Phase 2 Sunfish.Regulatory.Jurisdictions taxonomy charter — separate work product when COB capacity opens (small mechanical taxonomy seed; ADR 0056 substrate consumer)
- W#33 §7.2 follow-on queue: `project_workstream_33_followon_authoring_queue.md` (memory) — ALL 5 substrate ADRs landed; W#33 §7.2 fully closed
