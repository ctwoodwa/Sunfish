# ADR 0064 — Council Review (Stage 1.5 Adversarial)

**Date:** 2026-04-30
**Reviewer:** XO (research session) authoring in-thread after two subagent dispatches stalled (stream watchdog timeout). 5-perspective council per parent intake's halt-condition + W#33 §5.9 hardening precedent.
**ADR under review:** [ADR 0064 — Runtime Regulatory / Jurisdictional Policy Evaluation](../../../docs/adrs/0064-runtime-regulatory-policy-evaluation.md) (PR #415, branch `docs/adr-0064-runtime-regulatory-policy`, auto-merge intentionally DISABLED pre-council per cohort discipline)
**Companion intake:** [`2026-04-30_runtime-regulatory-policy-evaluation-intake.md`](../../00_intake/output/2026-04-30_runtime-regulatory-policy-evaluation-intake.md) (W#33 §7.2 fifth and final follow-on item)
**Driver discovery:** Mission Space Matrix §5.9 (`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`)

**Council perspectives (5):** distributed-systems / runtime-substrate; industry-prior-art; cited-symbol / cohort-discipline; forward-compatibility / migration; **Pedantic-Lawyer (REQUIRED per parent intake halt-condition)**.

> **Reader caution (carried forward):** specific statutory citations in this review have not been verified against current Official Code text. The findings note the *substrate's* citation hygiene; downstream legal review is still required per ADR 0064's halt-conditions for Stage 06.

---

## 1. Verdict

**Accept with amendments. Grade: B (Solid).** Path to A is mechanical (A1–A8 land + Pedantic-Lawyer-driven A9–A12 land).

The substrate-tier shape is correct: tuple-typed jurisdictional probe + composite-confidence rule + data-driven `IPolicyEvaluator` + per-regime acknowledgment surface + force-enable composing with ADR 0062's `OverridableWithCaveat` is the right architectural decision. The Option A separation (substrate Phase 1 ships without rule content; per-regime rule content lands as legal sign-off completes) is auditable and matches the precedent set by ADR 0057's substrate-vs-content separation. The 7 new `AuditEventType` constants pass collision check; the cited ADR set passes positive-existence verification on `origin/main`.

The substantive gaps are: (1) Pedantic-Lawyer perspective surfaces a missing GDPR Article 25 (privacy-by-design) citation — the substrate's whole posture rests on Art. 25 conformance, but only Articles 22/44/45/46 are cited; (2) the reader-caution preamble lacks an affirmative legal-advice disclaimer ("Sunfish does not provide legal advice; consult counsel before relying on this substrate for regulatory conformance") — currently fact-statement only; (3) sanctions screening's "operator-decision-aware emit-without-enforce" shape risks "knew-or-should-have-known" OFAC liability — substrate should declare an operator opt-out path explicitly; (4) Bridge-tier data-residency upstream gate code-path is unnamed (which middleware? which package?); (5) audit retention period for regulatory defense is unspecified — GDPR has variable retention, HIPAA has 6-year retention; substrate must declare; (6) `IPolicyEvaluator` evaluation cost class at scale is undeclared, and the rule-keying mechanism by featureKey is implicit; (7) industry rule-engine prior-art (Open Policy Agent / AWS Cedar / XACML) uncited; (8) industry sanctions screening libraries uncited; (9) Phase 1 substrate-only-without-rule-content deployability risks consumers misreading as "compliant."

Twelve required amendments + five encouraged. None block ADR 0064 Stage 02 design (all are mechanical to apply); ALL should land before Stage 06 substrate-build emits its first `PolicyEvaluated` audit event. The Pedantic-Lawyer-driven amendments specifically (A9–A12) are critical — they protect Sunfish-the-OSS-framework from inadvertent legal-conformance claims. The cohort lesson holds: pre-merge council (with Pedantic-Lawyer included) is dramatically cheaper than post-merge for substrate-tier ADRs in the W#33 lineage.

---

## 2. Findings (severity-tagged)

### F1 — GDPR Article 25 ("data protection by design and by default") absent from cited articles (Critical, Pedantic-Lawyer)

ADR 0064 cites GDPR Articles 22 (automated decision-making), 44–46 (transfers) — the "obvious four" for runtime-gate framing. **Article 25 is missing.** Article 25 is canonically cited alongside these four for substrate-tier posture: it requires controllers to "implement appropriate technical and organisational measures... [for] data protection by design and by default." The substrate ADR 0064 ships IS a "technical measure" within the Article 25 frame; its absence from References is structurally significant.

Practitioner-vs-Official-Code note: Article 25 lives in Chapter IV (Controller and Processor) Section 1 (General obligations), at GDPR (Regulation EU 2016/679) Article 25(1) and 25(2). The substrate's "force-enable acknowledges operator assumes responsibility" framing (ADR 0064 §"Trust impact / Security & privacy") is precisely the kind of operator-controller obligation Article 25 governs.

**Critical because:** the substrate's posture rests on Article 25; without the citation, a future reviewer (auditor; legal counsel; regulator) cannot trace the substrate's design back to its load-bearing statutory anchor.

### F2 — Reader-caution preamble lacks affirmative legal-advice disclaimer (Critical, Pedantic-Lawyer)

ADR 0064's reader-caution preamble (top of file) is a fact-statement: *"specific statutory citations in this ADR have not been verified against current Official Code text and may use practitioner shorthand."* This is necessary but not sufficient. The Pedantic-Lawyer flag: a fact-statement is not a disclaimer; an affirmative disclaimer ("Sunfish does not provide legal advice; the substrate is not a substitute for qualified counsel; consult an attorney before relying on this substrate for regulatory conformance in any specific deployment") shifts the reader into the right legal posture.

The current preamble is silent on the unstated assumption that downstream consumers might use ADR 0064 as a guide-to-conformance. An affirmative disclaimer prevents that misreading.

**Critical because:** the substrate ADR will be cited downstream by per-domain ADRs and per-feature documentation; without an affirmative disclaimer, the citation chain may aggregate into something that reads as legal-advice-by-aggregation.

### F3 — Sanctions screening's emit-without-enforce shape may create OFAC liability (Critical, Pedantic-Lawyer + distributed-systems)

ADR 0064 specifies: *"A match does NOT automatically block; the operator + legal counsel decides per-match what to do."* Substrate emits `SanctionsScreeningHit` audit; operator decides enforcement. This is **operator-decision-aware shape**, which is correct for OSS substrate. But:

- OFAC enforcement guidance places weight on *"the totality of circumstances"* including whether a person *"knew or had reason to know"* of a match. A substrate that emits matches into the audit trail without a default-block creates a paper trail of "we knew."
- Without a substrate-tier opt-out path explicitly named (e.g., a `ScreeningPolicy.AdvisoryOnly` mode that suppresses the audit emission with explicit operator+counsel sign-off), the substrate's default behavior is "log-but-don't-act" — which is exactly the posture OFAC enforcement guidance treats as aggravating.

**Critical because:** Sunfish-the-framework should not ship a substrate that may inadvertently increase regulatory liability for its consumers. The operator-decision-aware shape is right; the absence of an explicit opt-out path is wrong. Recommend A3 amendment names the path.

### F4 — Bridge-tier data-residency upstream gate code-path is unnamed (Critical, distributed-systems)

ADR 0064 specifies: *"The data-residency enforcer at the Bridge boundary applies BEFORE ciphertext touches Bridge storage — the constraint is an upstream gate, not a downstream filter."* But which code path? Which middleware? Which package?

- ADR 0031 Bridge accelerator has request-handling middleware (per the typical ASP.NET Core pipeline pattern); ADR 0064 doesn't name where the residency-enforcer hooks in.
- "BEFORE ciphertext touches Bridge storage" implies hooking BEFORE `EncryptedField` storage operations (per ADR 0046) on the Bridge side — but the Bridge handles encrypted payloads it doesn't decrypt. Where does the residency check happen?
- Without naming the code-path, Stage 06 implementation diverges from ADR intent.

**Critical because:** the ADR's residency-enforcement claim cannot be verified at Stage 06 build without knowing the gate's location.

### F5 — Audit retention period for regulatory defense is unspecified (Major, Pedantic-Lawyer)

`PolicyEvaluated` audits emit per evaluation; the substrate consumes ADR 0049's audit substrate. ADR 0064 §"Telemetry shape" specifies dedup windows (5-min / 1-hour / 24-hour / 1-day / 7-day / 30-day) but **does not specify retention period**.

- GDPR has variable retention requirements depending on lawful basis (Art. 5(1)(e) "storage limitation"); typical ranges are 6 months to 6 years.
- HIPAA has 6-year retention for audit trails (45 CFR §164.316(b)(2)(i)).
- PCI-DSS has 1-year retention with 3 months immediately accessible (PCI-DSS v4.0 Req. 10.5.1).

ADR 0064 says nothing about retention. ADR 0049 (the audit substrate) ships without per-event-type retention — retention is a deployment-config concern. But for regulatory defense, retention IS load-bearing; the substrate ADR should declare per-regime retention guidance, even if implementation is deferred.

### F6 — `IPolicyEvaluator` cost class at scale undeclared + rule-keying implicit (Major, distributed-systems)

`IPolicyEvaluator.EvaluateAsync(envelope, featureKey, ct)` consumes featureKey. Per-rule data is keyed on `(jurisdiction, regime, ruleId)`. **How does the evaluator find rules relevant to featureKey?** The shape is implicit:

- Either the rule-content has a `RelevantFeatures: Set<string>` field on each rule (not in the ADR's rule schema)
- Or the rule-content has implicit feature-key matching (regex? glob?) — also not in the ADR
- Or every evaluation runs every rule regardless of featureKey (cost grows linearly with rule count)

**Cost class is undeclared.** ADR 0062 declared probe cost classes (Low/Medium/High/Live); ADR 0064 should declare evaluator cost class. With N rules × M jurisdictions and per-evaluation latency target P95 < 100ms (per ADR 0062-A1.6's billing-cycle precedent), the evaluator's complexity matters.

### F7 — Cache invalidation on probe-status transition (Major, distributed-systems)

If jurisdictional probe transitions `Healthy → Stale` (per ADR 0062-A1.10 ProbeStatus), do enforcement decisions made under `Healthy` get re-evaluated? ADR 0064 says nothing about cache invalidation when probe status changes. Bridge-tier data-residency verdicts cached under stale probe data may be wrong — and re-running enforcement against stale envelope is also wrong.

Spec the rule explicitly: cached verdicts persist iff `envelope.Regulatory.ProbeStatus == Healthy`; transitions to `Stale / Failed / PartiallyDegraded / Unreachable` invalidate cached verdicts AND trigger UX surfacing (e.g., "Regulatory probe degraded; some features may behave inconsistently").

### F8 — Phase 1 substrate-only-without-rule-content deployability (Major, forward-compatibility)

Phase 1 ships the substrate types + interfaces + 0 rule content. `IPolicyEvaluator` evaluates against an empty rule set → silent-pass-everything verdict. **Consumers may misread as "regulatory-compliant."**

- The substrate Phase 1 hand-off + apps/docs Page MUST explicitly disclaim ("Phase 1 substrate ships the framework only; conformance requires rule-content + legal sign-off per regime; Phase 1 deployments are NOT regulatory-compliant by virtue of the substrate alone").
- Without the disclaimer, the silent-pass behavior is a foot-gun.

### F9 — Industry rule-engine prior-art uncited (Major, industry-prior-art)

ADR 0064 ships its own data-driven rule engine (`JurisdictionalPolicyRule` + `IPolicyEvaluator`) without engaging industry rule-engine prior-art:

- **Open Policy Agent (OPA / Rego)** — declarative policy DSL with mature tooling; CNCF graduated; cross-platform. Closest engineering analog.
- **AWS Cedar** — Amazon's open-source policy language with formal verification; designed for ABAC/RBAC.
- **XACML 3.0** — older but well-known; OASIS standard.

ADR 0064's `JurisdictionalPolicyRule` shape is a custom JSON schema. At Phase 3+ rule-content authoring, the substrate may benefit from adopting OPA/Rego or Cedar instead of custom JSON. The decision-drivers section should engage with these prior arts, even to deliberately reject them.

### F10 — Industry sanctions screening libraries uncited (Major, industry-prior-art)

`ISanctionsScreener` interface ships with no implementation guidance; the ADR mentions OFAC SDN + EU consolidated sanctions list but doesn't engage with the canonical SDK ecosystem:

- **ComplyAdvantage SDK** — major commercial sanctions/PEP screening API
- **Refinitiv World-Check One** — Reuters/LSEG product
- **Dow Jones Risk & Compliance**

A real Phase 3+ implementation will need vendor adapters; ADR 0064 should at minimum cite the SDK landscape as prior-art for the substrate consumer pattern.

### F11 — Force-enable's "operator assumes responsibility" caveat is fact-disclosure not liability transfer (Major, Pedantic-Lawyer)

ADR 0064 (composing with ADR 0062-A1.9 OverridableWithCaveat): force-enable produces "DegradedAvailable + UX surface naming legal/regulatory consequence ('Force-enable acknowledges the operator assumes responsibility for jurisdictional non-compliance')."

**This UX-side caveat does not shift legal liability.** A substrate-tier UX cannot grant indemnity to Sunfish-the-framework or to the operator's downstream consumers. The caveat is a **fact-disclosure** ("the substrate has been overridden by an operator with ostensibly informed consent"); it is NOT a liability transfer.

The ADR's framing reads as if the caveat-acceptance shifts responsibility. It doesn't. The Pedantic-Lawyer flag: reword to make clear the caveat is *informational* (the operator is on notice; the substrate's audit trail records the override) NOT *contractual* (no party is granting any other party indemnity by accepting the caveat).

### F12 — HIPAA Security Rule § range is practitioner-shorthand (Major, Pedantic-Lawyer)

ADR 0064 cites *"HIPAA Privacy Rule (45 CFR §§164.500–164.534) + Security Rule (Subpart C: 45 CFR §§164.302–164.318)"*.

The Security Rule subpart range §§164.302–164.318 includes:
- §164.302 (header — applicability)
- §164.304 (definitions)
- §164.306 (security standards: general rules)
- §164.308 (administrative safeguards)
- §164.310 (physical safeguards)
- §164.312 (technical safeguards)
- §164.314 (organizational requirements)
- §164.316 (policies/procedures + documentation; **also where retention lives**)
- §164.318 (compliance dates — historical, partly moot)

The practitioner-shorthand "Subpart C: §§164.302–164.318" lumps these together. For citation hygiene, the substrate ADR should cite §§164.308 / 164.310 / 164.312 / 164.316 specifically (as the W#33 §5.9 Pedantic-Lawyer hardening pass already did for §164.308/.310/.312 — the new §164.316 citation is needed because audit-retention specifically lives there).

### F13 — PCI-DSS framing as `CommercialProductOnly` may be too generous for OSS (Major, Pedantic-Lawyer)

ADR 0064 stances PCI-DSS v4.0 as `CommercialProductOnly`. The Pedantic-Lawyer flag: PCI-DSS scope is broader than commercial productization implies. Sunfish-OSS-the-framework processing payment card data in any way (even encrypted-at-rest via ADR 0046's substrate) brings the substrate into PCI-DSS scope. A more honest stance might be `OutOfScopeOpenSource` (the OSS framework explicitly does not aspire to PCI-DSS conformance under any deployment shape; commercial productization is a fork) — vs `CommercialProductOnly` (the OSS framework is conformance-shape-aware; conformance ships with productization).

The choice between these stances has legal-posture consequences:
- `CommercialProductOnly` reads as "we built it conformance-aware; productize and you're conformant."
- `OutOfScopeOpenSource` reads as "we explicitly did not aspire to PCI-DSS at the OSS layer; productize at your own risk and engagement."

The latter is safer for Sunfish-OSS; legal counsel should review and decide.

### F14 — `RegulatoryRegimeStance.OutOfScopeOpenSource` framing vs "explicitly disclaimed" (Major, Pedantic-Lawyer)

The stance value name `OutOfScopeOpenSource` reads as a passive "we don't aspire." The Pedantic-Lawyer flag: an active "explicit disclaimer" posture has different legal meaning. If Sunfish-OSS *explicitly disclaims* FedRAMP / ITAR aspiration (vs *not aspiring*), downstream consumers can't argue silent acquiescence.

Recommend the stance value be renamed `ExplicitlyDisclaimedOpenSource` (or similar) AND the per-stance UX surface includes a fact-statement to the effect of "Sunfish-OSS does NOT aspire to <regime> conformance under any deployment shape; commercial productization is a separate work product."

### F15 — Rule-content data file format unspec'd (Major, forward-compatibility)

ADR 0064 mentions per-jurisdiction `JurisdictionalPolicyRule` JSON files at `data/regulatory-rules/{jurisdiction-id}/{regime}/{rule-id}.json`. The actual JSON schema is not spec'd. At Phase 3 rule-content authoring, multiple authors will diverge on:
- File structure (flat fields vs nested)
- Localization key naming conventions
- Versioning representation (per OQ-0064.7 — needs more spec)

A canonical JSON schema (or JSON Schema document) for `JurisdictionalPolicyRule` should ship in Phase 1 substrate alongside the C# type — even if no rules are authored against it yet.

### F16 — Composite-confidence rule's 27-case coverage edge cases (Minor, distributed-systems)

ADR 0064 specifies the composite-confidence rule for jurisdictional probe (3 signals × 3 levels = 27 cases). Edge cases:
- VPN + truthful user-declaration + stale tenant-config → 2 of 3 signals say one jurisdiction; 1 disagrees. The composite rule says "if two signals agree, composite is High." But if user-declared = truthful AND tenant-config is stale (= jurisdiction-where-tenant-was-but-isn't-now), the agreement is actually wrong. The rule needs a tie-breaker: which signal trumps? Recommend user-declaration > tenant-config > IP-geo (truthfulness ordering).

The 27-case test matrix should explicitly cover this.

### F17 — Rule-content versioning per OQ-0064.7 needs more spec (Minor, forward-compatibility)

OQ-0064.7 acknowledges rule-content versioning is open. Recommend the OQ resolves at Phase 1 substrate hand-off: rule-content data files carry semver in metadata; substrate consumes the latest version per `(jurisdiction, regime, rule-id)` triple; deprecation grace period is 90 days (Phase 3 default; tunable per regime).

### F18 — Sanctions list reload mechanism (sync vs async) unspec'd (Minor, forward-compatibility)

OQ-0064.5 acknowledges sanctions list update cadence (daily for OFAC; weekly for EU). Recommend the substrate ship an async background-priority reload mechanism (matching ADR 0049 audit-substrate's append-only pattern); sync reload is insufficient (OFAC SDN updates aren't predictable; missing an update by sync-deadline produces stale-screen results).

### F19 — Disputed-jurisdictions naming in `Sunfish.Regulatory.Jurisdictions@1.0.0` taxonomy charter (Minor, Pedantic-Lawyer)

The `Sunfish.Regulatory.Jurisdictions@1.0.0` taxonomy charter ships with a seed of major jurisdictions. Disputed jurisdictions (Taiwan; Western Sahara; Crimea/Sevastopol; Palestinian territories; Kashmir) are themselves political acts in their naming. The Pedantic-Lawyer flag: the seed authoring should engage qualified counsel BEFORE Phase 2 ships, because choosing one naming convention vs another has downstream legal exposure (e.g., naming Taiwan as `TW` vs `CN-TW` has Trade-Sanctions implications).

### F20 — Reader-caution discipline enforceability (Encouraged, Pedantic-Lawyer)

The reader-caution preamble + repeats are author-discipline. Without an automated mechanism, downstream consumers may strip them for brevity. Recommend an automated apps/docs build-step that fails the build if a `regulatory-policy/` page lacks the canonical caution string.

### F21 — Regime-stance defaults ordering (Encouraged)

The default regime-stance table in ADR 0064 has a specific ordering: HIPAA / GDPR / PCI_DSS_v4 / SOC2 / EU_AI_Act / FHA / CCPA. Recommend reorder for chart-readability: alphabetize OR cluster by stance (`InScope` first; `CommercialProductOnly` second; `OutOfScopeOpenSource`/`ExplicitlyDisclaimedOpenSource` third).

### F22 — `EvaluateAsync(envelope, featureKey, ct)` signature precedent (Encouraged, distributed-systems)

The `IPolicyEvaluator.EvaluateAsync` signature mirrors `ICapabilityGate<TFeature>.EvaluateAsync(envelope, ct)` from ADR 0062-A1.2. Note: ADR 0062's gate is keyed on the type parameter `TFeature`, NOT on a `string featureKey`. Question: should ADR 0064's evaluator be `IPolicyEvaluator<TFeature>` to match? Recommend keeping `string featureKey` (rule-content references feature keys as strings; type parameters don't compose with data files); BUT document the divergence.

### F-VP1 (verification-pass) — ADR 0009 `IEditionResolver` exists structurally

`git show origin/main:docs/adrs/0009-foundation-featuremanagement.md | grep IEditionResolver` confirms `IEditionResolver` defined as "Resolves `TenantId → edition key`. Ships with `FixedEditionResolver` for demos." Citation correct. **No finding.**

### F-VP2 (verification-pass) — ADR 0036 sync states are canonical

ADR 0036 (sync-state encoding contract) confirms 5-state set: `healthy / stale / offline / conflict / quarantine`. ADR 0064 doesn't directly cite the sync-state names; consumes the dimension via ADR 0062's `SyncStateSnapshot`. Citation correct. **No finding.**

### F-VP3 (verification-pass) — All cited ADRs Accepted on origin/main

| ADR | Status | Verdict |
|---|---|---|
| 0009 foundation-featuremanagement | Accepted | Pass |
| 0028 crdt-engine-selection (post-A8) | Accepted; A1+A2+A3+A4+A5+A6+A7+A8 landed | Pass |
| 0031 bridge-hybrid-multi-tenant-saas | Accepted (2026-04-23) | Pass |
| 0036 syncstate-multimodal-encoding-contract | Accepted | Pass |
| 0049 audit-trail-substrate | Accepted | Pass |
| 0056 foundation-taxonomy-substrate | Accepted | Pass |
| 0057 leasing-pipeline-fair-housing | Accepted | Pass |
| 0060 right-of-entry-compliance-framework | Accepted | Pass |
| 0062 mission-space-negotiation-protocol (post-A1) | Accepted | Pass |
| 0063 mission-space-requirements (post-A1) | Accepted | Pass |

All cited ADRs verified Accepted on `origin/main`. **No finding.**

### F-VP4 (verification-pass) — 7 new `AuditEventType` constants no-collision

Verification: `grep -E "Policy|Sanctions|Jurisdiction|Residency|Regime|EuAi" packages/kernel-audit/AuditEventType.cs` returned 0 results. The 7 new constants ADR 0064 introduces (`PolicyEvaluated`, `PolicyEnforcementBlocked`, `JurisdictionProbedWithLowConfidence`, `DataResidencyViolation`, `SanctionsScreeningHit`, `RegimeAcknowledgmentSurfaced`, `EuAiActTierClassified`) are all unique. Naming is unambiguous. **No finding.**

### F-VP5 (verification-pass) — W#22 Phase 6 deferred status verified

Active-workstreams.md row 22: *"Built 2026-04-30 (Phase 6 compliance half deferred to ADR 0060 Stage 06)"* — confirms ADR 0064's claim that W#22 Phase 6 compliance half is deferred. Direct downstream consumer of ADR 0064's substrate. **No finding.**

### F-VP6 (verification-pass) — Paper §20.4 framing verified

Paper §20.4 *"Filter 3: Connectivity and Operational Environment"* contains the cited "GDPR, HIPAA, FedRAMP, ITAR" tuple at row 5 of the environment-vs-model table. Citation correct. **No finding.**

### F-VP7 (verification-pass) — Per-domain ADR precedent fidelity

ADR 0057 (FHA documentation-defense) + ADR 0060 (Right-of-Entry per-jurisdiction) both Accepted with the per-domain regulatory rule patterns ADR 0064 cites as precedents. The claim "ADR 0064 generalizes their patterns" is structurally consistent with the per-domain ADRs' actual rule-engine shapes. **No finding.**

---

## 3. Recommended amendments

### A1 (Required) — Add GDPR Article 25 to References + reframe substrate posture (resolves F1)

Add to §"References":
> - GDPR Article 25 (data protection by design and by default) — Regulation EU 2016/679 Chapter IV Section 1; the substrate-tier statutory anchor under which ADR 0064's framework is the "technical and organisational measure"

Add to §"Decision drivers" a new bullet:
> - **GDPR Article 25 anchor.** The substrate's design-by-default + privacy-by-design posture is the load-bearing statutory anchor. Force-enable + override paths are operator-controller obligations under Article 25(1)+(2); the audit trail (per ADR 0049) is the documented "appropriate technical measure."

**Required because F1 is Critical (Pedantic-Lawyer).**

### A2 (Required) — Affirmative legal-advice disclaimer in reader-caution preamble (resolves F2)

Replace the reader-caution preamble (top of file) with:

> **Reader caution + legal-advice disclaimer (Pedantic-Lawyer hardening pass; carried forward from W#33 §5.9 + parent intake):** specific statutory citations in this ADR have not been verified against current Official Code text and may use practitioner shorthand. **Sunfish does not provide legal advice; this substrate is not a substitute for qualified counsel.** General counsel MUST engage before Stage 06 build of any concrete enforcement behavior. Consult an attorney before relying on this substrate for regulatory conformance in any specific deployment. This ADR specifies the *substrate-tier policy-evaluation framework*; the *content* of policy rules per jurisdiction is a legal-review work product not produced in this ADR.

**Required because F2 is Critical (Pedantic-Lawyer).**

### A3 (Required) — Substrate-tier sanctions screening opt-out path (resolves F3)

Add to §"Sanctions handling" after the operator-decision-aware paragraph:

> **Operator opt-out path (substrate-tier):** Deployments requiring an advisory-only sanctions posture (e.g., Sunfish-OSS reference deployments where no commercial sanctions-conformance program exists) MAY register a `ScreeningPolicy.AdvisoryOnly` mode that suppresses `SanctionsScreeningHit` audit emission AND surfaces an explicit operator+counsel sign-off record (`SanctionsAdvisoryOnlyConfigured` audit event with operator_id + justification + counsel_attestation_required: bool + scoped_lists + expires_at). The opt-out path is itself audited and time-bounded; it is NOT a default mode.
>
> **Why:** OFAC enforcement guidance places weight on whether a person "knew or had reason to know" of a match. A substrate that emits matches into the audit trail without offering an explicit advisory-only opt-out creates an aggravating paper trail; the opt-out path lets deployments choose between "screening + counsel review of every match" (operator commits to enforcement workflow) and "advisory-only with sign-off" (operator explicitly declines the workflow with attestation).

Add 8th `AuditEventType`: `SanctionsAdvisoryOnlyConfigured`.

**Required because F3 is Critical (legal liability concern).**

### A4 (Required) — Name the Bridge-tier data-residency upstream gate code-path (resolves F4)

Add to §"Data-residency enforcement contract":

> **Implementation: where the upstream gate hooks.** The data-residency check runs as ASP.NET Core middleware in the Bridge accelerator's request pipeline (per ADR 0031), positioned BEFORE any `EncryptedField` storage operation per ADR 0046. Concretely: `Sunfish.Bridge.Middleware.DataResidencyEnforcerMiddleware` runs after authentication but before any handler that writes to `IEncryptedFieldStore`. Implementation hand-off (Stage 06 work) wires this middleware in the Bridge `Program.cs` request pipeline configuration. The middleware reads the inbound request's record-class metadata + the active `MissionEnvelope.Regulatory.jurisdiction` + the `IDataResidencyEnforcer.EvaluateAsync` verdict; on `Allowed: false` it returns HTTP 451 (Unavailable for Legal Reasons; RFC 7725) with the operator-recovery action.

**Required because F4 is Critical (substrate cannot be implemented without naming the path).**

### A5 (Required) — Audit retention period per regime (resolves F5)

Add new sub-section §"Audit retention":

> Audit retention for regulatory defense varies by regime:
>
> | Regime | Recommended retention | Statutory anchor |
> |---|---|---|
> | HIPAA | 6 years | 45 CFR §164.316(b)(2)(i) |
> | GDPR | Per lawful basis (Art. 5(1)(e) "storage limitation") — typically 6 months to 6 years | GDPR Art. 5(1)(e) |
> | PCI-DSS v4.0 | 1 year (3 months immediately accessible) | PCI-DSS v4.0 Req. 10.5.1 |
> | SOC 2 | Per Trust Service Criteria; typically 1 year | TSC Common Criteria CC7.2 |
> | FHA | Per HUD recordkeeping requirements; typically 3 years | 24 CFR §100.500 |
> | CCPA | 24 months for verification records | Cal. Civ. Code §1798.130(a)(7) |
> | EU AI Act | Per Article 19 (logs), typically 6 months minimum | EU AI Act Art. 19 |
>
> **Substrate behavior:** ADR 0049's audit substrate ships without per-event-type retention — retention is a deployment-config concern. ADR 0064's substrate Phase 1 ships the retention-recommendation table; Stage 06 deployment-config hand-off wires per-deployment retention.
>
> **Reader caution applies.** Specific retention periods are subject to legal review per regime per jurisdiction; the table is practitioner-shorthand starting point.

**Required because F5 is Major (regulatory defense via audit is incomplete without retention).**

### A6 (Required) — Spec `IPolicyEvaluator` cost class + rule-keying (resolves F6)

Add to §"Initial contract surface" near `IPolicyEvaluator`:

> **Cost class (per ADR 0062-A1.6 precedent):** `IPolicyEvaluator.EvaluateAsync` is `Medium`-cost (matches ADR 0062's medium class — cached evaluation against in-memory rule set; cache TTL 5 minutes). Per-evaluation P95 latency target: < 50ms for ≤100 rules per jurisdiction × 3 jurisdictions resolved.
>
> **Rule-keying:** `JurisdictionalPolicyRule` gains a `RelevantFeatures: IReadOnlySet<string>?` field. When non-null, the rule is consulted only for evaluations matching at least one feature key. When null (default), the rule is consulted for every evaluation (broad-effect rules — e.g., a jurisdiction-wide data-residency rule). The evaluator's filtering applies `RelevantFeatures` as the first filter (cheap set-membership test) before rule-body evaluation.

**Required because F6 is Major (substrate without cost class doesn't deploy at scale).**

### A7 (Required) — Cache invalidation on probe-status transition (resolves F7)

Add to §"Probe mechanics" after the cache-TTL discussion:

> **Cache invalidation on probe-status transition.** Cached `IPolicyEvaluator` and `IDataResidencyEnforcer` verdicts persist iff the underlying `MissionEnvelope.Regulatory.ProbeStatus == Healthy`. Transitions to `Stale / Failed / PartiallyDegraded / Unreachable` invalidate cached verdicts AND surface UX per ADR 0062-A1.10's `EnvelopeChangeSeverity.ProbeUnreliable` severity (persistent banner; "Regulatory probe degraded; some features may behave inconsistently; open diagnostics").
>
> **Re-evaluation cost:** invalidated verdicts trigger fresh evaluation on next consumer access; the substrate does NOT pre-emptively re-run all enforcement decisions on the workspace.

**Required because F7 is Major (stale-probe verdicts are wrong; consumers must know).**

### A8 (Required) — Phase 1 substrate-only deployability disclaimer (resolves F8)

Add to §"Compatibility plan / Migration order / Phase 1":

> **Phase 1 deployability disclaimer.** Phase 1 substrate ships the framework only. With zero rule content, `IPolicyEvaluator` evaluates against an empty rule set → silent-pass-everything verdict. **Phase 1 deployments are NOT regulatory-compliant by virtue of the substrate alone.** Conformance requires rule content per regime per jurisdiction + legal sign-off (Phase 3+). The Phase 1 hand-off + apps/docs walkthrough page MUST surface this disclaimer prominently. Any consumer reading "ADR 0064 substrate landed" as "Sunfish is regulatory-conformant" is misreading; the substrate landing is necessary but not sufficient for conformance.

**Required because F8 is Major (silent-pass behavior is a foot-gun; explicit disclaimer mitigates).**

### A9 (Required) — Engage industry rule-engine prior-art in Decision Drivers (resolves F9)

Add to §"Decision drivers" as a new bullet:

> **Industry rule-engine prior-art (deliberately rejected for v0).** The canonical industry options are Open Policy Agent (OPA / Rego — CNCF graduated; declarative DSL with mature tooling), AWS Cedar (Amazon OSS policy language with formal verification; designed for ABAC/RBAC), and XACML 3.0 (older OASIS standard). v0 ships a custom JSON `JurisdictionalPolicyRule` schema rather than adopting these prior arts because: (a) rule-content is per-jurisdiction-per-regime data files authored by legal counsel — NOT engineering DSL territory; legal counsel reads JSON, not Rego; (b) custom JSON allows tight coupling to ADR 0064's `PolicyEvaluationKind` + `PolicyEnforcementAction` enums; (c) Phase 3+ migration to OPA-or-Cedar is the long-term target if rule-content authoring at scale exposes substrate gaps. Track at OQ-0064.8 (added below).

Add OQ-0064.8:

> **OQ-0064.8:** When does Sunfish migrate from custom JSON rule-content to OPA/Rego or Cedar? Trigger candidates: (a) rule-content authoring exceeds ~100 rules per regime; (b) rule-content gains operator-controllable predicates (DSL territory); (c) cross-regime conflict resolution requires formal verification.

**Required because F9 is Major (decision drivers section should engage prior-art, even to reject).**

### A10 (Required) — Cite sanctions-screening SDK landscape (resolves F10)

Add to §"Sanctions handling":

> **Industry SDK landscape (substrate consumer pattern).** The substrate `ISanctionsScreener` interface ships without vendor implementation. Phase 3+ vendor adapters land per `providers-*` package convention (per ADR 0013 provider-neutrality). Canonical SDK landscape: **ComplyAdvantage** (REST API; PEP + sanctions + adverse media; SaaS-only); **Refinitiv World-Check One** (Reuters/LSEG; on-prem + SaaS); **Dow Jones Risk & Compliance** (factiva); **OpenSanctions** (open data; Python tooling; usable for non-commercial without API). Vendor selection is per-deployment + per-tenant; the substrate does not endorse a vendor.

**Required because F10 is Major (real implementations need vendor adapters; landscape citation is the substrate-consumer pattern).**

### A11 (Required) — Reframe force-enable caveat as fact-disclosure not liability transfer (resolves F11)

Update the force-enable UX surface text in §"Trust impact / Security & privacy":

> **Force-enable + ADR 0062 OverridableWithCaveat composition (revised).** Operators may force-enable a regulated feature per ADR 0062-A1.9 `OverridableWithCaveat` policy. The UX surface displays a **fact-disclosure** ("This feature is regulated under <regime>; the substrate has been overridden by an operator-level decision; the override has been recorded in the audit trail"). The fact-disclosure is **NOT a liability transfer**: substrate-tier UX cannot grant indemnity to Sunfish-the-framework or to the operator's downstream consumers. The audit trail records the override as evidence; the operator's actual legal posture remains with the operator and their counsel.

**Required because F11 is Major (Pedantic-Lawyer; substrate must not imply legal-posture transfer).**

### A12 (Required) — HIPAA Security Rule § range citation hygiene (resolves F12)

Update the HIPAA citation in the Considered Options + References:

> - **HIPAA Privacy Rule** (45 CFR §§164.500–164.534) + **Security Rule** (specific subparts: §164.308 administrative safeguards; §164.310 physical safeguards; §164.312 technical safeguards; §164.316 policies/procedures + audit documentation including retention; §164.314 organizational requirements). Practitioner-shorthand "Subpart C: 45 CFR §§164.302–164.318" is **deprecated** in this ADR — the explicit § citations above are canonical going forward.

**Required because F12 is Major (citation hygiene).**

### A13 (Required) — Reframe `RegulatoryRegimeStance` values + PCI-DSS stance (resolves F13 + F14)

Two coupled changes:

(i) Rename `RegulatoryRegimeStance.OutOfScopeOpenSource` → `ExplicitlyDisclaimedOpenSource`. Add UX-surface fact-statement: "Sunfish-OSS does NOT aspire to <regime> conformance under any deployment shape; commercial productization is a separate work product."

(ii) Reconsider PCI-DSS stance: change from `CommercialProductOnly` → `ExplicitlyDisclaimedOpenSource` (subject to legal-counsel review). Rationale: any PCI-DSS scope brings the OSS substrate into scope; productization-aware-substrate framing is too generous.

Update the default regime stance table accordingly. **Subject to legal-counsel review** — A13's specific stance values may flip during Stage 1.5 council with the legal-counsel engagement letter; XO ships A13 substrate and counsel reviews the stance values.

**Required because F13 + F14 are Major (Pedantic-Lawyer; stance framing has legal consequences).**

### A14 (Required) — Ship canonical `JurisdictionalPolicyRule` JSON schema (resolves F15)

Phase 1 substrate hand-off MUST include a canonical JSON schema document at `data/regulatory-rules/jurisdictional-policy-rule.schema.json` (using JSON Schema Draft 2020-12). The schema serializes the C# `JurisdictionalPolicyRule` record's structure including `LocalizedString` references for `Description` and the `RelevantFeatures` field added in A6. Phase 3+ rule-content authoring validates against this schema.

**Required because F15 is Major (multi-author divergence is bounded by canonical schema).**

### A15 (Encouraged) — Composite-confidence tie-breaker rule (resolves F16)

Add to §"Probe mechanics / Composite confidence rule":

> **Tie-breaker:** When 2 of 3 signals agree but reflect stale state (e.g., user-declaration = truthful + tenant-config = stale), preference order is: **user-declaration > tenant-config > IP-geo** (truthfulness ordering; user-declaration is the most-recent operator-controlled signal; tenant-config is operator-controlled but lags; IP-geo is unreliable). Document the tie-breaker explicitly in the 27-case test matrix.

**Encouraged.**

### A16 (Encouraged) — Resolve OQ-0064.7 + OQ-0064.5 (resolves F17 + F18)

Resolve OQ-0064.7 (rule-content versioning): rule-content data files carry semver in metadata; substrate consumes latest version per `(jurisdiction, regime, rule-id)`; deprecation grace period 90 days default (tunable per regime).

Resolve OQ-0064.5 (sanctions list reload): async background-priority reload at substrate-tier (matches ADR 0049 audit-substrate's append-only pattern); cadence configurable per deployment (default daily for OFAC; weekly for EU; consumer adapters may override).

**Encouraged (both OQs are deferred; A16 makes them substantive).**

### A17 (Encouraged) — Disputed-jurisdiction naming legal review (resolves F19)

Add halt-condition to §"Halt conditions for Stage 06":

> **6. Disputed-jurisdiction naming legal review.** `Sunfish.Regulatory.Jurisdictions@1.0.0` taxonomy charter (Phase 2) MUST engage qualified counsel BEFORE shipping the seed. Disputed jurisdictions (Taiwan; Western Sahara; Crimea/Sevastopol; Palestinian territories; Kashmir) name themselves as political acts; counsel selects naming conventions with downstream sanctions-trade-implication awareness.

**Encouraged.**

### A18 (Encouraged) — Reader-caution discipline enforcement mechanism (resolves F20)

Add to §"Halt conditions for Stage 06" — automated enforcement:

> **7. Reader-caution discipline enforcement.** Phase 1 substrate hand-off includes an automated apps/docs build-step that fails the build if any page in `apps/docs/foundation/regulatory-policy/` lacks the canonical reader-caution string. Implementation: simple grep-or-regex in the build pipeline.

**Encouraged.**

### A19 (Encouraged) — Reorder regime stance table + clarify featureKey divergence (resolves F21 + F22)

(i) Reorder default regime-stance table by stance-cluster: `InScope` first (alphabetized within: CCPA, EU_AI_Act, FHA, GDPR, SOC2); `CommercialProductOnly` second (alphabetized within: HIPAA, PCI_DSS_v4 — pending A13 review of PCI-DSS); `ExplicitlyDisclaimedOpenSource` third (no current entries; reserved for future regimes).

(ii) Add a doc-comment to `IPolicyEvaluator.EvaluateAsync(envelope, featureKey, ct)` clarifying the featureKey-string-vs-TFeature-type-parameter divergence from ADR 0062's gate signature. Justification: rule-content references feature keys as strings; type parameters don't compose with data files.

**Encouraged.**

---

## 4. Quality rubric grade

**Grade: B (Solid).** Path to A is mechanical (A1–A14 land + Pedantic-Lawyer A2/A3/A11/A12/A13 land before Phase 3 rule-content authoring begins).

- **C threshold (Viable):** All 5 CORE present (Context with reader-caution, §A0, Decision drivers, Considered options A–D, Decision); multiple CONDITIONAL sections (Compatibility plan, Implementation checklist, Open questions, Halt conditions, Sibling amendment dependencies, Cohort discipline). No critical *planning* anti-patterns. **Pass.**
- **B threshold (Solid):** Stage 0 sparring evident in OQ-0064.1–0064.7 (seven explicit deferrals; A16 raises that to a resolved subset); FAILED conditions named in §"Halt conditions for Stage 06" (5 named); Cold Start Test plausible — Stage 06 implementer can read §"Implementation checklist" + the substrate types and know what to scaffold; companion-amendment dependencies explicit (§"Sibling amendment dependencies named"). **Pass.**
- **A threshold (Excellent):** Misses on:
  1. **F1 (Critical):** GDPR Article 25 absent from cited articles — substrate's load-bearing statutory anchor isn't cited.
  2. **F2 (Critical):** Reader-caution preamble lacks affirmative legal-advice disclaimer — fact-statement only.
  3. **F3 (Critical):** Sanctions screening's emit-without-enforce shape may create OFAC liability — no operator opt-out path named.
  4. **F4 (Critical):** Bridge-tier data-residency upstream gate code-path unnamed — substrate cannot be implemented as ADR'd.
  5. **F5–F8 (Major):** Audit retention; cost class; cache invalidation; Phase 1 deployability disclaimer.
  6. **F9–F15 (Major):** Industry rule-engine prior-art uncited; sanctions SDK landscape uncited; force-enable framing; HIPAA citation hygiene; stance value reframe; rule-content schema unspec'd.

A grade of **B with required A1–A14 applied promotes to A**, conditional on legal-counsel sign-off on the Pedantic-Lawyer-driven amendments (A2 / A3 / A11 / A12 / A13).

---

## 5. Council perspective notes (compressed)

- **Distributed-systems / runtime-substrate reviewer:** "The composite-confidence rule is well-named but the 27-case coverage has at least one edge case (truthful-user + stale-tenant-config) that the explicit tie-breaker rule should resolve. `IPolicyEvaluator` cost class and rule-keying mechanism are both substrate-tier concerns — undeclared cost class produces unpredictable evaluation latency at scale; implicit rule-keying produces O(N×M) per evaluation. A6 amendment closes both. The Bridge-tier data-residency upstream gate is the load-bearing implementation gap — without naming the code-path (middleware in ASP.NET Core pipeline), Stage 06 build will diverge from ADR intent. A4 closes this; A7 closes cache-invalidation on probe-status transition. The 27-case test matrix should ship in Phase 1 substrate test coverage." Drives F4 + F6 + F7 + F16 + F22; amendments A4, A6, A7, A15, A19.

- **Industry-prior-art reviewer:** "ADR 0064 ships its own data-driven rule engine without engaging Open Policy Agent (Rego), AWS Cedar, or XACML 3.0 — the canonical industry options. The substrate's choice (custom JSON for legal-counsel-readability) is defensible but the prior-art rejection should be explicit. A9 amendment adds the engagement. Sanctions-screening libraries (ComplyAdvantage, Refinitiv, Dow Jones, OpenSanctions) similarly uncited; A10 adds the SDK landscape. Cloud data-residency products (MS EU Data Boundary, AWS regional, GCP) are also uncited but lower-priority — the substrate's Bridge-tier residency framing doesn't directly map to those products. GDPR Article 25 is the most consequential missing citation; F1 / A1 covers." Drives F9 + F10; amendments A9, A10.

- **Cited-symbol / cohort-discipline reviewer:** "Spot-checked all 10 cited ADRs in three directions per the post-ADR-0063 lesson (§A0 self-audit pattern is necessary but NOT sufficient; council remains canonical). All 10 ADRs verified Accepted on origin/main (F-VP3). The 7 new `AuditEventType` constants pass collision check (F-VP4). The W#22 Phase 6 deferred status verified (F-VP5). Paper §20.4 GDPR/HIPAA/FedRAMP/ITAR framing verified (F-VP6). ADR 0009 `IEditionResolver` exists at the cited surface (F-VP1; the related `EditionCapabilities` shape is a downstream wrapper consuming ADR 0009's edition-key string). ADR 0036's 5 sync states are canonical (F-VP2). Per-domain ADR 0057 + ADR 0060 precedent fidelity verified (F-VP7). NO structural-citation failures found in this review — improvement over ADR 0063 council's 4-of-4 structural-citation count. Cohort batting average: 15-of-15 (this council); structural-citation failure rate (XO-authored) holds at 10-of-14 + 0 in this ADR = 10-of-15 ~67% (down from 71%)." No drives; verification-pass findings F-VP1 through F-VP7.

- **Forward-compatibility / migration reviewer:** "Phase 1 substrate-only deployability is the most consequential forward-compatibility concern: with zero rule content, `IPolicyEvaluator` silent-passes every evaluation. Consumers may misread Phase 1 as 'regulatory-compliant'; A8 amendment adds the explicit disclaimer. Rule-content data file format (JSON shape) unspec'd — at Phase 3 multi-author divergence is bounded only by the C# type; A14 amendment ships canonical JSON Schema in Phase 1. Rule-content versioning + sanctions list reload (OQ-0064.7 + OQ-0064.5) need substantive resolution; A16 promotes both from OQ to substrate-spec'd. Phase 4 cross-cutting refactor (ADR 0057 + 0060 migrate to ADR 0064 substrate) is mechanically scoped; rewriting the per-domain ADRs' enforcement logic is the actual work; mechanical-vs-rewrite ratio is unclear without prototyping." Drives F8 + F11 (forward-compat aspect) + F15 + F17 + F18; amendments A8, A14, A16.

- **Pedantic-Lawyer perspective (REQUIRED 5th):** "ADR 0064 is the substrate's most legally-exposed work product in the W#33 lineage. Five Critical-or-Major findings here are Pedantic-Lawyer-driven: F1 (GDPR Article 25 absence — the substrate rests on Article 25; missing the citation breaks the statutory anchor chain); F2 (reader-caution preamble lacks affirmative legal-advice disclaimer — the fact-statement preamble is necessary but not sufficient against legal-advice-by-aggregation); F3 (sanctions screening's emit-without-enforce risks OFAC 'knew-or-should-have-known' liability — substrate must offer explicit advisory-only opt-out path with operator+counsel attestation); F11 (force-enable caveat is fact-disclosure not liability transfer — UX cannot grant indemnity); F12 (HIPAA Security Rule § range is practitioner-shorthand — explicit § citations including §164.316 for retention are canonical); F13 + F14 (PCI-DSS stance + `OutOfScopeOpenSource` framing — both have legal-posture consequences; counsel should review). Three Encouraged-tier findings: F19 (disputed-jurisdictions naming in `Sunfish.Regulatory.Jurisdictions` taxonomy charter); F20 (reader-caution discipline enforcement mechanism). The substrate ADR is structurally sound; the Pedantic-Lawyer fixes are mechanical to apply (A1, A2, A3, A11, A12, A13, A17, A18). Counsel engagement letter remains the Stage 06 halt-condition." Drives F1 + F2 + F3 + F11 + F12 + F13 + F14 + F19 + F20; amendments A1, A2, A3, A11, A12, A13, A17, A18.

---

## 6. Cohort discipline scorecard

| Cohort baseline | This amendment |
|---|---|
| 14 prior substrate amendments needed council fixes | Will be 15-of-15 if A1–A14 fixes apply pre-merge per current auto-merge-disabled approach |
| Cited-symbol verification — both directions standard | This amendment: 0 false-positive + 0 false-negative + 0 structural-citation failures (F-VP1–F-VP7 are explicit positive-existence + structural verifications). **Improvement over ADR 0063's 4-of-4 structural-citation count.** |
| Council false-claim rate (all three directions): 2-of-12 | This council: 0 false claims (F-VP1–F-VP7 pass cleanly; council's findings are F1–F22 against the ADR, not against the council itself) |
| Council pre-merge vs post-merge | Pre-merge with **5-perspective Pedantic-Lawyer-included** council per parent intake's halt-condition — correct call given the legal-exposure surface |
| Severity profile | 4 Critical (F1–F4) + 11 Major (F5–F15) + 4 Minor (F16–F19) + 3 Encouraged (F20–F22) + 7 verification-passes (F-VP1–F-VP7) |
| Pedantic-Lawyer-driven findings | **8 of 22** substantive findings (F1, F2, F3, F11, F12, F13, F14, F19) — high-water mark for the legal-perspective contribution |
| Structural-citation failure rate (XO-authored) | Was 10-of-14 (~71%) post-ADR-0063; ADR 0064 contributes 0 — rate becomes 10-of-15 (~67%); §A0 self-audit caught all positive-existence claims this round |

The cohort lesson holds: every substrate-tier amendment in the W#33 lineage has needed council fixes (15-of-15). Pedantic-Lawyer perspective addition (per W#33 §5.9 precedent + parent intake halt-condition) was vindicated — 8 of 22 findings are legal-perspective-specific that the standard 4-perspective council would not have surfaced.

---

## 7. Closing recommendation

**Accept ADR 0064 with required amendments A1–A14 applied before Phase 3 rule-content authoring begins.** The substrate-tier shape is correct; the substantive gaps are:

1. **GDPR Article 25 anchor** (F1 / A1) — load-bearing statutory citation.
2. **Affirmative legal-advice disclaimer** (F2 / A2) — preamble hardening.
3. **Sanctions screening operator opt-out path** (F3 / A3) — OFAC liability mitigation.
4. **Bridge-tier data-residency code-path naming** (F4 / A4) — substrate implementability.
5. **Audit retention per regime** (F5 / A5) — regulatory defense via audit.
6. **Cost class + rule-keying** (F6 / A6) — substrate scalability.
7. **Cache invalidation on probe-status** (F7 / A7) — stale-verdict correctness.
8. **Phase 1 deployability disclaimer** (F8 / A8) — silent-pass foot-gun mitigation.
9. **Industry rule-engine prior-art engagement** (F9 / A9) — Decision Drivers rigor.
10. **Sanctions SDK landscape citation** (F10 / A10) — substrate consumer pattern.
11. **Force-enable caveat reframe** (F11 / A11) — UX cannot transfer legal liability.
12. **HIPAA Security Rule citation hygiene** (F12 / A12) — § specificity including §164.316.
13. **`RegulatoryRegimeStance` value reframe + PCI-DSS stance** (F13 + F14 / A13) — legal-posture-aware naming; counsel review.
14. **Canonical `JurisdictionalPolicyRule` JSON schema** (F15 / A14) — Phase 1 substrate must ship the schema document.

A1–A14 are mechanical-on-the-amendment-text but legally-shaping. All fourteen are 4–6h of XO work pre-merge.

**Stage 02 design** can begin immediately on the architectural decision; **Phase 1 substrate Stage 06 build** gates on A1–A14 + the legal-counsel engagement letter for InScope regimes.

**Standing rung-6 task (per ADR 0028-A4.3 + A7.12 + ADR 0062-A1.15 + ADR 0063-A1.16 commitment):** XO spot-checks A1's added/modified citations (per the post-A14 surface) within 24h of merge. If any A1-added claim turns out to be incorrect, file an A2 retraction matching the prior cohort retraction patterns.

**Cohort milestone:** ADR 0064 closes W#33 §7.2 follow-on authoring queue (5/5 ADRs landed/in-flight). Cohort batting average: 15-of-15 substrate amendments needed council fixes; structural-citation failure rate held at ~67% but ADR 0064 contributed 0 (improvement over ADR 0063's 4-of-4); Pedantic-Lawyer perspective contribution: 8 of 22 substantive findings.

