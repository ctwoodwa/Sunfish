# ADR 0064 — Runtime Regulatory / Jurisdictional Policy Evaluation — Council Review

**Date:** 2026-04-30
**Reviewer:** research session (XO; adversarial council, UPF Stage 1.5)
**Subject:** ADR 0064 v. 2026-04-30 (Proposed; auto-merge intentionally DISABLED per cohort discipline + parent-intake halt-condition)
**ADR under review:** [`docs/adrs/0064-runtime-regulatory-policy-evaluation.md`](../../../docs/adrs/0064-runtime-regulatory-policy-evaluation.md) on branch `docs/adr-0064-runtime-regulatory-policy`; PR **#415**.
**Companion intake:** [`icm/00_intake/output/2026-04-30_runtime-regulatory-policy-evaluation-intake.md`](../../00_intake/output/2026-04-30_runtime-regulatory-policy-evaluation-intake.md)
**Driver discovery:** [`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`](../../01_discovery/output/2026-04-30_mission-space-matrix.md) §5.9 + §6.3 + §7.2 — fifth and final item in the W#33 Mission Space Matrix follow-on authoring queue.
**Companion artifacts read:** ADR 0064 (568 lines, single document); ADR 0057 (FHA documentation-defense; A1 amendment landed 2026-04-30); ADR 0060 (Right-of-Entry; A1–A5 landed 2026-04-29); ADR 0062 post-A1 surface (`MissionEnvelope.RegulatoryCapabilities`, `OverridableWithCaveat`, `IFeatureGate<TFeature>`, `FeatureVerdict`, `DegradationKind`, `EnvelopeChange`, `DimensionChangeKind`, `LocalizedString`, `FeatureForceEnabled`); ADR 0063 post-A1 (`RegulatorySpec(AllowedJurisdictions, ProhibitedJurisdictions, RequiredConsents)`); ADR 0049 (audit substrate); ADR 0009 (`Foundation.FeatureManagement`; `Edition` / `IEditionResolver`); ADR 0031 (Bridge hybrid multi-tenant SaaS — Zone C); ADR 0056 (`Foundation.Taxonomy`); paper §20.4 (Filter 3: Connectivity and Operational Environment); paper §16 (IT Governance and Enterprise Deployment); `packages/kernel-audit/AuditEventType.cs`; W#22 active-workstreams row (Phase 6 compliance half deferred); prior cohort councils 0061 / 0062 / 0063 / 0028-A1 / A6 / A8 / 0046-A2 / A4 / 0048-A1.

**Council perspectives applied (5 — Pedantic-Lawyer required per parent intake halt-condition #4 + W#33 §5.9 hardening precedent):** (1) Distributed-systems / runtime-substrate reviewer; (2) Industry-prior-art reviewer; (3) Cited-symbol / cohort-discipline reviewer (3-direction spot-check per the A7 / 0063 lesson — positive-existence, negative-existence, structural-citation correctness); (4) Forward-compatibility / migration reviewer; (5) **Pedantic-Lawyer perspective** — statutory citation hygiene, regime-stance framing, liability-language risk, OFAC/sanctions framing, GDPR transfer-Articles cross-reference accuracy.

---

## 1. Verdict

**Accept-with-amendments — grade B (mid-B).** ADR 0064 is doing a **necessary** thing — W#33 §6.3 names cross-cutting regulatory substrate as the single highest commercial-launch-blocker for any non-US-residential-property tenant, and the substrate-vs-content separation (Option A) is the right architectural taste: it preserves XO/legal-counsel boundaries while letting Phase 1 substrate ship without legal-review gating. The reader-caution preamble + the explicit halt-conditions for Stage 06 (general-counsel engagement letter, Pedantic-Lawyer council, reader-caution carry-forward) demonstrate that the author internalized the "the substrate is XO authority; the content is legal authority" boundary rigorously.

The §A0 self-audit shows discipline learned from the ADR 0063 lesson — the cited symbols are *substantially* better than 0063's (no invented type-name failures caught in this review's spot-check; the post-A1 surface citations all positively verify, including the A1.9 / A1.10 / A1.14 amendment references; ADR 0009/0031/0049/0056/0057/0060/0062/0063 references all positively-verify on `origin/main`; W#22 active-workstreams Phase 6 deferred status correctly cited; paper §20.4 + §16 both verified-existing on origin/main). The §A0 self-audit catch-rate appears to be **substantially higher than ADR 0063's 0-of-4** — encouraging cohort-discipline signal. However, **the council still finds 3 structural-citation findings** of a different shape than 0063's (affected-package naming F5; rule-content data file path divergence F6; one paper §-citation framing F7) — confirming the cohort lesson that §A0 self-audit catches some but not all structural failures and council remains canonical, especially on cross-cutting structural choices that aren't pure type-name verification.

The Pedantic-Lawyer perspective contributes the highest finding density of any single perspective in this review (**5 findings** — F8 GDPR Article 25 absence, F9 PCI-DSS framing, F11 sanctions-emit-without-enforce knew-or-should-have-known risk, F13 force-enable liability-language softening, F14 disputed-jurisdictions naming gap), confirming the parent-intake halt-condition was load-bearing. The standard-perspective findings cluster around three substantive design gaps: composite-confidence rule under-specifies tied-disagreement and probe-status interactions (F1), `IPolicyEvaluator` rule-keying mechanism is unspecified (F2), and audit-retention-period spec is missing (F12).

None of the 17 findings block W#33 §7.2 closure of this ADR's substrate-tier intake; all should land before any rule-content data file or any `apps/docs/foundation/regulatory-policy/` text ships, because rule-content authoring is legal-review-gated and the substrate surface that legal counsel reviews must be stable.

---

## 2. Findings (severity-tagged)

### F1 — Composite-confidence rule under-specifies tied-disagreement + ProbeStatus interaction (Major, AP-3 vague success criteria)

ADR 0064 lines 366–370 declare the composite confidence rule:

> *"If user-declared OR tenant-config is `High`: composite is `High`; use that signal.*
> *If only IP-geo: composite is `Low`; verdict is `IndeterminateProbeFailure` for `MinimumProbeConfidence: High` rules, `Pass` for `Low`-min rules.*
> *If two signals disagree: composite is `Medium`; emit `JurisdictionProbedWithLowConfidence` audit; surface UX to user requesting explicit declaration."*

The rule is shape-correct but covers ~6 of the 27 cases the §"Test coverage" checklist (line 470) explicitly names ("3 signals × 3 confidence levels = 27 cases"). Concrete unspecified cases:

- **(a) User-declared = High AND tenant-config = High AND they disagree** ("operator says US-CA; user says US-NY"). Rule says "use that signal" but doesn't name which High signal wins. Tenant-config typically dominates per Sunfish's operator-trust model (ADR 0008), but the ADR doesn't say so.
- **(b) User-declared = High AND IP-geo agrees AND tenant-config disagrees** — three signals, two agree at High, one disagrees at Low. Rule says "two signals disagree → Medium" but the High-agreeing pair should arguably outweigh the Low-disagreeing solo signal; current rule produces Medium.
- **(c) ProbeStatus = `Stale` interaction** — ADR 0064 cites `ProbeStatus per ADR 0062-A1.10`, but the cache TTLs (5 min IP-geo, 1 hour user, 1 hour tenant-config) imply Stale state is reachable. What is the verdict when user-declared signal is `Stale`? Cached signal should arguably remain High-confidence within TTL but the Status≠Healthy needs explicit verdict-impact rule.
- **(d) `Failed` ProbeStatus** — IP-geo source unreachable. Rule says "If only IP-geo: composite is Low" but if IP-geo *failed* AND user-declared exists at High, the composite should be User-declared High (one signal failed should not poison the composite).

Stage 06 implementer cannot ship `IPolicyEvaluator` without picking one disposition for each. **Major** — same AP-3 shape as ADR 0061 council F3 and ADR 0063 council F8. Mechanical fix: explicit truth-table or algorithm pseudocode covering all 27 cases (3 signals × 3 confidence states) PLUS ProbeStatus interactions (Healthy / Stale / Failed / Partial / Unreachable per ADR 0062). One paragraph + one table.

### F2 — `IPolicyEvaluator.EvaluateAsync(envelope, featureKey, ct)` rule-keying mechanism is unspecified (Major, AP-1 unvalidated assumption)

ADR 0064 line 196–202 declares:

```csharp
public interface IPolicyEvaluator
{
    ValueTask<PolicyVerdict> EvaluateAsync(
        MissionEnvelope envelope,
        string featureKey,
        CancellationToken ct = default
    );
}
```

The evaluator must select **which** `JurisdictionalPolicyRule` instances to evaluate against the `(envelope, featureKey)` pair. ADR 0064 doesn't specify the selection mechanism. Three plausible mechanisms:

- **(a) Feature-keyed** — rules carry `IReadOnlyList<string> AppliesTo` (feature keys); evaluator filters rules by `AppliesTo.Contains(featureKey)`.
- **(b) Jurisdiction-keyed** — evaluator filters rules by `rule.JurisdictionId == envelope.Regulatory.Jurisdiction` (or `*` wildcard match) AND a separate per-feature mapping.
- **(c) PolicyEvaluationKind-keyed** — feature carries metadata declaring which `PolicyEvaluationKind` values it triggers; evaluator filters rules by intersection.

ADR 0064's `JurisdictionalPolicyRule` record (line 159–168) has `JurisdictionId`, `Regime`, `RuleId`, `Kind`, `OnViolation`, `MinimumProbeConfidence` — but **no `AppliesTo` field on the rule and no per-feature manifest** declaring which rules apply. The substrate cannot route between feature and rule.

This is the same AP-1 shape as ADR 0061 council F1 (`ITransportSelector` selection rule unspec'd) and ADR 0063 council F8 (`OverallVerdict` Informational-dimension rule unspec'd). **Major** — Stage 06 implementer cannot ship `DefaultPolicyEvaluator` without picking one. Mechanical fix: add either an `IReadOnlySet<string> AppliesToFeatureKeys` field on `JurisdictionalPolicyRule` (Option a) OR a separate `IFeaturePolicyManifest` interface declaring per-feature rule-kind subscriptions (Option c, more extensible). Option (a) is simpler; recommend it for v0.

### F3 — Cache invalidation when probe transitions Healthy → Stale is unspecified (Major, AP-3 / cache coherence)

ADR 0064 line 360–365 declares per-signal cache TTLs (5 min IP-geo / 1 hour user-declared / 1 hour tenant-config) but does NOT specify what happens to a `PolicyVerdict` cached against an `(envelope, featureKey)` pair when the underlying probe signal transitions Healthy→Stale.

OQ-0064.3 (line 486) acknowledges the question for `IDataResidencyEnforcer` ("Should `IDataResidencyEnforcer` cache verdicts? Recommend: yes — same `(record_class_key, jurisdiction)` evaluation should produce identical verdict; cache per ADR 0062 Medium cost class (5-minute TTL)") but doesn't extend the answer to `IPolicyEvaluator`.

Concrete scenario: a leasing-pipeline feature does `EvaluateAsync(envelope, "leasing.background-check")` → verdict cached for 5 minutes. At t+3min, the IP-geo probe transitions Healthy → Stale. At t+4min, a *second* `EvaluateAsync` with same args fires. Does:

- **(a)** Cache hit returns the original verdict (until TTL expires)?
- **(b)** Cache invalidates immediately on probe transition (cache coherence with probe)?
- **(c)** Cache returns verdict with `IndeterminateProbeFailure` status overlay, leaving the original rule-evaluations cached?

Same AP-3 shape as ADR 0061 council F3 partial-failure-undefined. **Major** — mechanical fix: add a §"Verdict caching policy" subsection naming (recommend (b): on `EnvelopeChange` events with `RegulatoryCapabilities` in `ChangedDimensions`, the evaluator invalidates all cached verdicts; this leverages ADR 0062's existing `EnvelopeChange` event stream and matches the post-install regression pattern from ADR 0063 §"Re-evaluation cost class").

### F4 — Bridge-tier residency upstream-gate code-path is named but not located (Major, AP-3 / architectural-ambiguity)

ADR 0064 line 298 declares:

> *"**Bridge-tier residency.** ADR 0031's Bridge accelerator is a hosted-SaaS surface; tenants on Bridge MAY have residency constraints that prohibit Bridge-tier processing for some record classes. The data-residency enforcer at the Bridge boundary applies BEFORE ciphertext touches Bridge storage — the constraint is an upstream gate, not a downstream filter."*

The intent is correct (gate residency at upload-write to Bridge, not at server-side after ciphertext lands). But **where the gate physically lives in code is unspecified**. Three plausible locations:

- **(a)** Anchor-side write-path interceptor that consults `IDataResidencyEnforcer` before `HttpClient.PostAsync` to Bridge — gate runs on Anchor before ciphertext leaves the device.
- **(b)** Bridge-side ingest controller that rejects with HTTP 403 and emits `DataResidencyViolation` audit before persisting ciphertext.
- **(c)** Both — Anchor-side gate is the primary defense; Bridge-side gate is defense-in-depth.

(c) is correct from a security-posture standpoint, but ADR 0064 names neither path. **Major** — Stage 06 implementer building Bridge wiring cannot pick. Mechanical fix: add to §"Data-residency enforcement contract" a paragraph naming "the enforcer runs on **both** sides per defense-in-depth: Anchor-side write-path interceptor consults `IDataResidencyEnforcer` before transmitting ciphertext upstream; Bridge-side ingest controller consults the same enforcer before persisting and emits `DataResidencyViolation` if the Anchor-side gate was bypassed (compromised client; bug; etc.). The two-sided check is critical because the OS/network-stack between them is untrusted."

### F5 — Affected-packages list cites `packages/foundation-mission-space-regulatory/` OR `extends foundation-mission-space/`; pick one (Minor, structural-citation)

ADR 0064 line 444 declares:

> *"New: `packages/foundation-mission-space-regulatory/` (or extends `foundation-mission-space/` per ADR 0062 precedent) — substrate types + interfaces + DI extension."*

The `(or extends ...)` framing punts a structural decision to Stage 02. ADR 0062 lives at `packages/foundation-mission-space/` (verified — ADR 0062's affected-packages line on `origin/main`). Two structural choices have different downstream cost:

- **(a) Extend `foundation-mission-space/`** — same package, regulatory namespace `Sunfish.Foundation.MissionSpace.Regulatory`. Coherent: regulatory IS a dimension of mission space per ADR 0062's `MissionEnvelope.Regulatory` slot. ADR 0064's own decision text (line 137) names `Sunfish.Foundation.MissionSpace.Regulatory` namespace — matches Option (a).
- **(b) New `packages/foundation-mission-space-regulatory/` package** — separate package; cross-package dependency on `foundation-mission-space`. Cleaner separation of concerns but adds an artifact + DI-wiring boundary.

**Minor** — the namespace already commits to (a); the package layout should match. Mechanical fix: drop the `(or extends ...)` parenthetical; commit to "Modified: `packages/foundation-mission-space/` — adds `Sunfish.Foundation.MissionSpace.Regulatory` namespace + types + interfaces + DI extension. No new package."

### F6 — Rule-content data file path divergence: ADR cites `data/regulatory-rules/` while §"Affected packages" implies it lives in a package (Minor, structural-citation)

ADR 0064 line 395:

> *"Per-jurisdiction `JurisdictionalPolicyRule` JSON files at `data/regulatory-rules/{jurisdiction-id}/{regime}/{rule-id}.json`"*

vs. line 446:

> *"New: `data/regulatory-rules/` directory tree for rule-content data files (Phase 3+)."*

`data/` at the repo root is unconventional in Sunfish — no existing precedent. ADR 0056's taxonomy charters live at `taxonomy/charters/{charter-id}.json` (per ADR 0056 affected-packages), not `data/`. ADR 0057's leasing jurisdiction rules ship in `taxonomy/charters/sunfish-leasing-jurisdiction-rules.json` per the W#22 Phase 4 evidence on `origin/main`.

ADR 0064's rule-content should live under the **same** layout convention — recommend `taxonomy/charters/sunfish-regulatory/{jurisdiction-id}/{regime}/{rule-id}.json` (or similar; sibling of the existing leasing-jurisdiction-rules charter). The `data/regulatory-rules/` path divergence creates a fork in directory conventions.

**Minor** — same structural-correctness shape as ADR 0063 council F5 (`foundation-bundles` vs `foundation-catalog`). Mechanical fix: replace `data/regulatory-rules/` with the taxonomy-charter-path convention; cross-reference ADR 0057's charter pattern explicitly.

### F7 — Paper §20.4 framing as "regulatory factors as architectural filter" is partially accurate (Minor, structural-citation)

ADR 0064 line 48 + 530 cite paper §20.4 as "regulatory factors as architectural filter." Verified the actual paper §20.4 heading on `origin/main`:

```
### 20.4 Filter 3: Connectivity and Operational Environment
```

Paper §20.4 is **Filter 3 (connectivity + operational environment)**, NOT regulatory factors. The Architecture Selection Framework (paper §20) does include regulatory considerations as one of the filters, but it is not §20.4 specifically. Three options:

- **(a)** ADR 0064 means a different filter — likely §20.5 or §20.6 if the framework lists ~6 filters in numeric order. Verify the actual filter numbering and re-cite.
- **(b)** ADR 0064 means the broader §20 Architecture Selection Framework with regulatory factors *as* a filter (not strictly §20.4).
- **(c)** ADR 0064 conflates §20.4 with a regulatory-specific subsection that doesn't exist by that number.

Same shape as ADR 0061's paper-§-citation imprecision and ADR 0028-A6.2 / A8.5 paper-§-cite-correctness lessons. **Minor** — practitioner-shorthand citation; not load-bearing for substrate code but load-bearing for the Pedantic-Lawyer-required reader-caution discipline (statutory-citation hygiene shouldn't be paired with paper-citation-imprecision). Mechanical fix: re-read paper §20 (Architecture Selection Framework, line 636) and re-cite the correct filter number; or cite §20 broadly as "the Architecture Selection Framework's regulatory filter."

Note also: paper §16 IS verified-existing ("16. IT Governance and Enterprise Deployment", line 502); that citation stands.

### F8 — GDPR Article 25 (privacy-by-design) absence is flag-worthy alongside Articles 22 / 44 / 45 / 46 (Major, Pedantic-Lawyer / statutory-citation hygiene)

ADR 0064 line 533 cites:

> *"GDPR Articles 22 / 44 / 45 / 46 (transfers + automated decision-making)"*

The cited Articles are accurate for what they cover (Art. 22 = automated decision-making + profiling; Arts. 44–46 = transfers to third countries). But **Article 25 (Data Protection by Design and by Default)** is the structurally-load-bearing GDPR article for a substrate-tier ADR — Article 25 is exactly what ADR 0064 IS: a substrate framework that bakes data-protection considerations into the system shape rather than as bolted-on policy.

GDPR Article 25 obligation paraphrased: controllers "implement appropriate technical and organisational measures... in an effective manner and to integrate the necessary safeguards into the processing." A substrate-tier policy-evaluation framework + per-record-class data-residency + per-feature consent-requirement is **exactly** the technical-organisational-measure shape Article 25 contemplates. ADR 0064 does the work of Article 25 without citing it.

The omission is flag-worthy because: (a) regulators (DPAs) reading Sunfish's ADR-trail will look for Article 25 acknowledgment specifically; (b) absence reads as Sunfish-doesn't-know-Article-25 even though the substrate IS Article-25-shaped; (c) Article 25 framing is the cleanest legal-discourse handle for "why Sunfish ships substrate-vs-content separation."

**Major** — substrate-tier framing gap that costs Sunfish nothing to fix. Mechanical fix: add `GDPR Article 25 (Data Protection by Design and by Default)` to the References list (line 533), and add one sentence to §"Decision drivers" naming it: "ADR 0064's substrate-tier framework is structurally aligned with GDPR Article 25's 'data protection by design' obligation; Article 25 is the cleanest legal-discourse handle for the substrate-vs-content separation."

### F9 — PCI-DSS framing "Substrate enables tokenization shape" is imprecise (Major, Pedantic-Lawyer / statutory-citation hygiene)

ADR 0064 line 262 declares the PCI-DSS regime stance:

> *"PCI_DSS_v4 | CommercialProductOnly | Substrate enables tokenization shape; PCI-DSS scope-reduction posture requires commercial productization"*

Two issues:

- **(a)** "PCI-DSS scope-reduction posture" is industry-shorthand; PCI-DSS v4.0 actually frames this as "reducing the cardholder data environment (CDE)" or "scope minimization." Tokenization is a **scope-reduction technique**, but the regime stance conflates technique with posture.
- **(b)** "Substrate enables tokenization shape" doesn't name what tokenization Sunfish enables. ADR 0046 (encrypted-field substrate) provides format-preserving + envelope-encrypted fields, NOT tokenization in the PCI-DSS sense (which requires a stable, irreversible-by-design surrogate that an external token vault issues). ADR 0064 implies Sunfish has tokenization substrate when it has *encryption* substrate — a different PCI-DSS control.

Reading the table cell at face value, a PCI-DSS auditor would ask "show me the token vault's compliance attestation" and there is none. The right framing is: ADR 0046's encrypted-field substrate + per-record-class encryption can *reduce CDE scope* if the cardholder data is held only in an encrypted-field that meets PCI-DSS's "rendering PAN unreadable" provision (Requirement 3.5.1; v4.0). That's a scope-reduction posture, not tokenization.

**Major** — Pedantic-Lawyer perspective. Statutory framing that overpromises substrate capability. Mechanical fix: rewrite the table cell to: "Substrate enables CDE scope-reduction shape via ADR 0046 encrypted-field substrate (rendering PAN unreadable per PCI-DSS v4.0 Req 3.5.1); commercial productization required for token-vault integration AND for the QSA assessment + Report on Compliance / Self-Assessment Questionnaire that any PCI-DSS-covered deployment requires."

### F10 — EU AI Act Annex III / Art. 5 framing is shape-correct but cites Regulation EU 2024/1689 without sub-numbering (Minor, Pedantic-Lawyer / statutory-citation hygiene)

ADR 0064 line 536 cites:

> *"EU AI Act (Regulation EU 2024/1689; Arts. 5–6 + Annex III)"*

The citation is accurate at the regulation-level (the EU AI Act IS Regulation (EU) 2024/1689; entered into force 2024-08-01). The Article range "Arts. 5–6" is correct for prohibited practices (Art. 5) and high-risk-system classification (Art. 6 + Annex III).

But: "Arts. 5–6 + Annex III" is incomplete for the substrate ADR 0064 is shipping. The `EuAiActTier` enum (line 337–344) declares 5 tiers — `Prohibited / HighRisk / LimitedRisk / MinimalRisk / NotApplicable`. The LimitedRisk tier is governed by **Article 50** (transparency obligations for AI systems interacting with natural persons — chatbot disclosure, deepfake disclosure, emotion-recognition disclosure). The MinimalRisk tier has no specific Article but is the residual category.

ADR 0064's enum doc-comment on line 341 already names Art. 50 ("LimitedRisk, // Art. 50 transparency obligations (chatbot disclosure, etc.)") — so the *enum* knows Article 50 is the relevant Article — but the References list on line 536 omits it. **Minor** — citation-completeness gap. Mechanical fix: extend the References citation to "Arts. 5, 6, 50 + Annex III."

### F11 — Sanctions emit-without-enforce framing is "knew-or-should-have-known" risk for the operator (Major, Pedantic-Lawyer / liability-language)

ADR 0064 line 332 declares:

> *"Sanctions screening is OPERATOR-decision territory, NOT substrate-default territory. A match does NOT automatically block; the operator + legal counsel decides per-match what to do. ADR 0064's substrate emits SanctionsScreeningHit audit events on every match; the rule-content layer (or tenant-config) governs whether matches block, warn, or merely log."*

The architectural decision (operator + counsel decide per-match enforcement) is **correct** for a substrate framework. But the framing has a Pedantic-Lawyer-flagged subtlety: **OFAC's enforcement posture is "knew-or-should-have-known."** Once Sunfish emits a `SanctionsScreeningHit` audit event with a match, the operator is *on notice* that a transaction-counterparty appears on the SDN list. If the operator then proceeds without blocking, OFAC's strict-liability framework can hold the operator liable for the unblocked transaction even if the operator's "policy" was "merely log."

This isn't a substrate bug — it's a substrate-framing risk. The ADR's "merely log" option presents a defensible-sounding choice that may not actually be defensible under OFAC enforcement. The operator who chooses "log only" because Sunfish offered it as an option may be in a worse legal posture than the operator who never installed Sunfish's sanctions screener at all.

The substrate cannot enforce OFAC for the operator (substrate-vs-content separation is correct). But the substrate can:

- **(a)** Default `OnViolation` for sanctions to `Block` (operator must explicitly downgrade to `AuditOnly` per-deployment, with an attorney-acknowledgment surface).
- **(b)** When sanctions match fires, the substrate UX surface includes a "this match places you on notice; OFAC enforcement is strict-liability; consult counsel before proceeding" caveat *every* match, not buried in the ADR.
- **(c)** Document explicitly in ADR 0064 that "merely log" is NOT a defensible default for OFAC — operators choosing it should do so with documented legal-counsel sign-off.

**Major** — Pedantic-Lawyer perspective. Substrate framing should not present "merely log" as one neutral option among three; it's the most legally-perilous of the three for OFAC matches specifically. Mechanical fix: add §"OFAC enforcement posture caveat" subsection naming the knew-or-should-have-known framework; default sanctions `OnViolation` to `Block` (matching SDN-strict-liability framework); require explicit operator-config + counsel-acknowledgment to downgrade.

### F12 — Audit-retention period spec is missing for the 7 new events (Major, AP-3 / spec gap)

ADR 0064 line 376–386 declares 7 new audit events with dedup windows but **no retention period**. The retention period is regulatory-load-bearing because:

- **GDPR data-minimization (Art. 5(1)(c) + (e))** — audit events containing personal data (`screened_identifier`, `attempted_jurisdiction`, `regime` in `RegimeAcknowledgmentSurfaced`) must be retained only as long as necessary.
- **HIPAA audit retention (45 CFR §164.316(b)(2)(i))** — minimum 6 years for HIPAA-covered audit logs.
- **PCI-DSS v4.0 Req 10.5.1** — minimum 1 year audit-log retention; 3 months immediately available.
- **OFAC recordkeeping** — 5 years for transactions involving SDN matches.

The dedup windows ADR 0064 names (5-min / 1-hour / 1-day / 7-day / 30-day) are *emission-rate* dedup, not retention. ADR 0049 (audit substrate) does not specify retention either; this is a substrate-tier gap surfacing at this ADR.

**Major** — substrate-tier gap. Two options for resolution:

- **(a)** Defer to ADR 0049 amendment naming a default retention (e.g., 7 years to satisfy HIPAA + OFAC + GDPR-bounded combined).
- **(b)** ADR 0064 names retention per-event-type (e.g., `SanctionsScreeningHit` retained 5 years per OFAC; `DataResidencyViolation` retained 6 years per HIPAA-covered shape; `PolicyEvaluated` retained 1 year for diagnostic).

Mechanical fix: add a §"Retention" subsection naming the default + per-event overrides; or declare ADR 0049-A1 sibling amendment dependency.

### F13 — Force-enable "operator assumes responsibility" framing is fact-disclosure, not liability transfer (Major, Pedantic-Lawyer / liability-language)

ADR 0064 line 424 + the embedded ADR 0062 quote declare:

> *"Force-enable acknowledges the operator assumes responsibility for jurisdictional non-compliance"*

The wording reads as "click-through indemnification" — the operator clicks force-enable, and Sunfish-the-vendor is shielded because the operator "assumed responsibility." This framing is legally weak in two directions:

- **(a) Vendor-side:** consumer-protection regimes (FTC §5 deceptive practices in the US; Unfair Commercial Practices Directive in the EU) generally don't honor click-through liability transfer for substrate-tier non-compliance the vendor *enabled the override of*. Saying "operator assumed responsibility" doesn't prevent regulator action against Sunfish-the-vendor for shipping the override path.
- **(b) Operator-side:** the operator who clicks "force-enable" hasn't *transferred* their primary liability to anyone — they're the data controller (GDPR), the covered entity (HIPAA), the merchant (PCI-DSS); their primary liability stays with them regardless of click. The substrate UI saying they "assumed responsibility" implies the responsibility was elsewhere before the click, which is factually incorrect.

The *correct* framing is **fact-disclosure**: the substrate informs the operator that this configuration may put them in non-compliance with one or more regimes; the operator's legal posture is unchanged by the click; the substrate is documenting that the operator was put on notice. This is ADR 0064 doing audit-trail work, not liability-transfer work.

**Major** — Pedantic-Lawyer perspective. Mechanical fix: rewrite the canonical force-enable caveat language to: "Force-enable applies override; substrate informs operator that the configuration may not satisfy applicable regulatory requirements in the active jurisdiction. Operator's primary regulatory obligations are unchanged by this override. Audit event `FeatureForceEnabled` documents the override and operator-attributable click. Consult counsel before proceeding for any regime where regulatory non-compliance carries material liability." Sibling amendment ADR 0062-A2 (or A1.17) propagates the rewrite to the source-of-truth.

### F14 — Disputed-jurisdictions naming is unspecified (Encouraged, Pedantic-Lawyer / framing)

ADR 0064 declares jurisdiction IDs in the `JurisdictionalPolicyRule.JurisdictionId: string` field with examples like `"US-CA"`, `"RU-*"`, `"IR-*"`. The taxonomy charter is named `Sunfish.Regulatory.Jurisdictions@1.0.0` — but the ADR does not specify how the framework handles **disputed jurisdictions**: Crimea (claimed by both UA and RU; OFAC sanctions specific to the region post-2014); Western Sahara (MA / non-self-governing-territory ambiguity); Taiwan (TW / CN ambiguity per ISO 3166 vs UN membership); Northern Cyprus; Kosovo (XK / Serbia ambiguity).

The choice matters for two reasons: (a) sanctions screening — Crimea-specific OFAC Executive Orders apply differently than Russia-wide SDN; (b) ISO 3166-1 vs ISO 3166-2 vs UN-listing differences will produce inconsistent jurisdiction-ID matching depending on which standard the taxonomy charter adopts.

**Encouraged** — Pedantic-Lawyer perspective. Not blocking for substrate Phase 1 (the substrate is jurisdiction-string-agnostic), but the taxonomy charter (Phase 2) authoring needs an explicit policy. Mechanical fix: add an OQ-0064.8 ("Disputed-jurisdiction naming policy — does the taxonomy charter follow ISO 3166-1 + ISO 3166-2, UN-listing, or a hybrid? How are sub-regional sanctions (Crimea, Donetsk-Luhansk) encoded?") and defer to taxonomy-charter Phase 2 work product with general-counsel input.

### F15 — Affirmative legal-advice disclaimer is absent (Major, Pedantic-Lawyer / framing)

ADR 0064 has the **reader-caution preamble** (line 12) covering statutory citations + general-counsel-engagement. But it is missing an explicit **legal-advice disclaimer** of the shape: "This ADR does not constitute legal advice. Sunfish provides substrate; consult qualified legal counsel for any regulatory determination."

The reader-caution preamble names "specific statutory citations may use practitioner shorthand" — that's a citation-accuracy disclaimer. It does NOT say "this whole document, including the regime-stance table, is not legal advice." The omission matters because:

- **(a)** The regime-stance table (line 258–266) makes affirmative claims about HIPAA / GDPR / PCI-DSS / SOC 2 / EU AI Act / FHA / CCPA stances. A regulator or counsel reading this table can read it as Sunfish's *legal opinion* on what each regime requires.
- **(b)** Operators reading "Substrate enables tokenization shape" for PCI-DSS may rely on that framing as a substitute for QSA assessment. The "this is not legal advice" disclaimer is what shifts the reliance posture.

**Major** — Pedantic-Lawyer perspective. Mechanical fix: prepend to the reader-caution preamble (line 12) one sentence: "This ADR is engineering documentation, not legal advice. The regime-stance table and statutory citations are best-effort summaries by engineers; legal determinations under any regime require qualified counsel."

### F16 — Forward-compatibility migration: Phase 4 cross-cutting refactor mechanical-vs-rewriting is unspecified (Minor, AP-3 / migration-spec)

ADR 0064 line 439 declares Phase 4:

> *"Phase 4 (per-domain ADR cross-cutting refactor): ADR 0057 + ADR 0060 grow IPolicyEvaluator consumers; their existing rule logic migrates to rule-content data files."*

ADR 0057's `IInquiryValidator` + `IBackgroundCheckRequester` substrate plus its `Sunfish.Leasing.JurisdictionRules@1.0.0` taxonomy charter (per W#22 evidence on `origin/main`) already encodes per-jurisdiction rules. Phase 4 says the rules "migrate" — but is that:

- **(a) Mechanical:** ADR 0057's existing `Sunfish.Leasing.JurisdictionRules@1.0.0` charter is renamed/re-pathed to fit ADR 0064's rule-content layout, with the existing rule data preserved.
- **(b) Rewriting:** ADR 0057's rules are re-authored against ADR 0064's `JurisdictionalPolicyRule` schema (which has different fields: `Kind`, `OnViolation`, `MinimumProbeConfidence` — none of which exist on ADR 0057's existing rule shape).

(b) is the actual cost. Stage 06 implementer needs to know this is a re-authoring-not-renaming migration. **Minor** — Phase 4 cost-class spec gap. Mechanical fix: name Phase 4 explicitly as "re-authoring against ADR 0064's `JurisdictionalPolicyRule` schema; ADR 0057's existing charter is the *seed* for the new layout but is not preserved as-is." Consider deferring Phase 4 to a separate sibling-ADR amendment to ADR 0057 (and ADR 0060) so the cost-class is captured at intake-time.

### F17 — Sanctions list reload mechanism is unspecified per OQ-0064.5 (Minor, AP-3)

OQ-0064.5 (line 488) acknowledges the question:

> *"Sanctions list update cadence — daily? weekly? Recommend: daily for OFAC SDN (updated daily by Treasury); weekly for EU consolidated; configurable per deployment."*

But the **mechanism** for reloading is unspecified. Three plausible mechanisms:

- **(a) Pull:** `ISanctionsScreener` implementation polls a configured upstream URL on a timer; reload-failure triggers a `SanctionsListReloadFailed` event (8th audit constant — not currently declared).
- **(b) Push:** operator runs a CLI command to ingest a new sanctions list snapshot; substrate reloads on next `ScreenAsync` call.
- **(c) Bridge-tier:** Bridge accelerator pushes sanctions list updates as part of its tenant-config sync.

Each has different operational characteristics + failure modes. ADR 0064 doesn't pick. **Minor** — OQ-0064.5 names the cadence but not the mechanism. Mechanical fix: extend OQ-0064.5 to also pin the mechanism (recommend (b) for v0; (a) + (c) deferred to commercial-productization).

### Verification-pass findings (no issue; cohort spot-check evidence)

**F-VP1 — All 8 cited ADRs verified Accepted on `origin/main`.** ADR 0009 / 0028 / 0031 / 0049 / 0056 / 0057 / 0060 / 0062 / 0063 confirmed via `git ls-tree origin/main docs/adrs/`. None vapourware. **Pass.**

**F-VP2 — ADR 0062-A1.9 (`OverridableWithCaveat`) verified existing.** Line 710 of ADR 0062 on `origin/main` declares "A1.9 — `ForceEnablePolicy` taxonomy per dimension"; the `OverridableWithCaveat` value applies specifically to the Regulatory dimension per A1.9. ADR 0064 line 188 + 424 cite `OverridableWithCaveat` correctly. **Pass.**

**F-VP3 — ADR 0062-A1.10 (`ProbeStatus`) verified existing.** Line 726 of ADR 0062 declares "A1.10 — `ProbeStatus` + `EnvelopeChangeSeverity.ProbeUnreliable`"; the 5-value enum (`Healthy / Stale / Failed / PartiallyDegraded / Unreachable`) matches ADR 0064 line 151 exactly. **Pass.**

**F-VP4 — ADR 0063-A1.15 + A1.16 verified existing.** Line 708 of ADR 0063 declares "A1.15 — §A0 cited-symbol audit lesson"; line 716 declares "A1.16 — Cohort discipline log." ADR 0064 line 37 + 477 + 499 + 567 cites A1.15 + A1.16 correctly. **Pass.**

**F-VP5 — ADR 0028-A4.3 + A7.12 + A8.11 + A8.12 verified existing.** ADR 0028 line 442 (A4.3), line 1300 (A7.12), line 883 (A8.11), line 909 (A8.12) all confirmed on `origin/main`. ADR 0064's standing-rung-6 reference at line 567 ("per ADR 0028-A4.3 + A7.12 + A8.12 + ADR 0062-A1.15 + ADR 0063-A1.16 commitment") is structurally accurate. **Pass.**

**F-VP6 — All 7 new `AuditEventType` constants verified non-colliding.** `PolicyEvaluated`, `PolicyEnforcementBlocked`, `JurisdictionProbedWithLowConfidence`, `DataResidencyViolation`, `SanctionsScreeningHit`, `RegimeAcknowledgmentSurfaced`, `EuAiActTierClassified` checked against `packages/kernel-audit/AuditEventType.cs` on `origin/main`; no token collision with the existing ADR-0049 base set, the ADR-0028-A6 + A7 + A8 additions, the ADR-0062-A1 9-constant additions, or the W#22 leasing-pipeline 12 additions. **Pass.**

**F-VP7 — W#22 Phase 6 deferred status correctly cited.** ADR 0064 line 50 + 531 + 548 cite W#22 Phase 6 compliance half as deferred; verified by `icm/_state/active-workstreams.md` row 22 on `origin/main` ("Phase 6 compliance half deferred to ADR 0060 Stage 06") — note: ADR 0064's text says deferred-pending-ADR-0064 which is consistent with the active-workstreams row's broader framing (deferred pending compliance substrate, of which ADR 0064 is a part). **Pass.**

**F-VP8 — Paper §16 (IT Governance and Enterprise Deployment) verified existing.** Line 502 of paper on `origin/main` reads "## 16. IT Governance and Enterprise Deployment". ADR 0064 line 49 + 530 cite this correctly. **Pass.**

**F-VP9 — `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` verified existing.** Per ADR 0023 + the `packages/foundation-crypto/` package on `origin/main`, the symbol exists. ADR 0064 line 53 cites it as encoding contract; not directly used in the contract surface but referenced as cohort-discipline. **Pass.**

**F-VP10 — `RegulatoryCapabilities` envelope dimension verified.** ADR 0062 line 252 (post-A1.8) + the `MissionEnvelope.Regulatory: RegulatoryCapabilities` slot confirmed; ADR 0064 line 43 + 137 + 198 cite it correctly. **Pass.**

---

## 3. Recommended amendments

### A1 (Required) — Composite-confidence rule explicit truth-table covering all 27 cases + ProbeStatus interaction (resolves F1)

Replace lines 366–370 with an explicit algorithm:

> *"Composite confidence is computed by the following rule, in order:*
>
> *1. **Filter** signals where `ProbeStatus ∈ { Failed, Unreachable }` (failed probes are excluded; the composite is computed over remaining signals).*
> *2. **If zero signals remain:** composite is `Low`; verdict for any rule is `IndeterminateProbeFailure`.*
> *3. **If one signal remains at confidence X:** composite is X; use that signal's jurisdiction value.*
> *4. **If two or more signals remain and all agree on jurisdiction value:** composite is the highest of the agreeing confidences.*
> *5. **If two or more signals remain and disagree on jurisdiction value:** apply tenant-config-dominates-user-declared-dominates-IP-geo precedence; composite confidence is `Medium`; emit `JurisdictionProbedWithLowConfidence` audit; surface UX requesting explicit declaration.*
> *6. **If any signal is `Stale`:** treat as that signal's last-known confidence (not Low); but emit `JurisdictionProbedWithLowConfidence` audit with `signal_breakdown` field naming the stale source."*

Add an explicit table or test matrix of the 27 (3 signals × 3 confidence) cases × ProbeStatus interactions for the §"Test coverage" checklist. **Required because F1 is Major.**

### A2 (Required) — Rule-keying mechanism on `JurisdictionalPolicyRule` (resolves F2)

Add an `IReadOnlySet<string>? AppliesToFeatureKeys` field to `JurisdictionalPolicyRule`:

```csharp
public sealed record JurisdictionalPolicyRule(
    string                  JurisdictionId,
    RegulatoryRegime        Regime,
    string                  RuleId,
    LocalizedString         Description,
    PolicyEvaluationKind    Kind,
    string                  StatutoryCitation,
    PolicyEnforcementAction OnViolation,
    Confidence              MinimumProbeConfidence,
    IReadOnlySet<string>?   AppliesToFeatureKeys     // null = applies to all features in scope; set = explicit subscription
);
```

Document the matching rule: `IPolicyEvaluator` evaluates a rule iff `AppliesToFeatureKeys is null || AppliesToFeatureKeys.Contains(featureKey)` AND the rule's `JurisdictionId` matches `envelope.Regulatory.Jurisdiction` (or wildcards). **Required because F2 is Major.**

### A3 (Required) — Verdict caching + invalidation policy (resolves F3)

Add a §"Verdict caching policy" subsection:

> *"`PolicyVerdict` may be cached against `(envelope.Regulatory, featureKey)` pairs by the caller (typical caller: a feature-gate or write-path interceptor). Cache TTL: 5 minutes for `Pass`/`FailWithEnforcement`; 1 minute for `IndeterminateProbeFailure`. The evaluator publishes a verdict-invalidation signal subscribed to ADR 0062 `EnvelopeChange` events; on any `EnvelopeChange` with `Regulatory ∈ ChangedDimensions`, all cached verdicts are invalidated. Stale verdicts MUST NOT be returned across an invalidation event."*

**Required because F3 is Major (cache-coherence).**

### A4 (Required) — Bridge-tier residency two-sided-gate naming (resolves F4)

Replace line 298 with: *"**Bridge-tier residency.** ADR 0031's Bridge accelerator is a hosted-SaaS surface. Residency-restricted record classes are gated on **both** sides per defense-in-depth: (i) Anchor-side write-path interceptor consults `IDataResidencyEnforcer` before transmitting ciphertext upstream — the primary gate; ciphertext for prohibited-jurisdiction record classes never leaves the device; (ii) Bridge-side ingest controller consults the same enforcer before persisting — the secondary gate; if the Anchor-side gate is bypassed (compromised client; bug; protocol mismatch), the Bridge-side gate rejects with HTTP 403 and emits `DataResidencyViolation`. The two-sided check is critical because the OS/network-stack between them is untrusted."* **Required because F4 is Major.**

### A5 (Encouraged) — Resolve affected-packages parenthetical; commit to extending `foundation-mission-space/` (resolves F5)

Replace line 444 with: *"Modified: `packages/foundation-mission-space/` (per ADR 0062) — adds `Sunfish.Foundation.MissionSpace.Regulatory` namespace + types + interfaces + DI extension. No new package; the regulatory subsystem is a sub-namespace of the mission-space substrate consistent with `MissionEnvelope.Regulatory` being a dimension of the envelope."* **Encouraged.**

### A6 (Required) — Rule-content data file path under taxonomy-charter convention (resolves F6)

Replace `data/regulatory-rules/{jurisdiction-id}/{regime}/{rule-id}.json` with the taxonomy-charter convention; coordinate with the `Sunfish.Regulatory.Jurisdictions@1.0.0` taxonomy charter so rule content is sibling to or sub-charter of the jurisdiction taxonomy. Cross-reference ADR 0057's existing `Sunfish.Leasing.JurisdictionRules@1.0.0` charter pattern explicitly. **Required because F6 is Minor structural-citation but cross-cutting (sets convention for all future regulatory rule-content).**

### A7 (Required) — Paper §-citation precision (resolves F7)

Re-read paper §20 (Architecture Selection Framework, line 636 on `origin/main`) and re-cite the correct filter number for "regulatory factors as architectural filter"; or cite §20 broadly as "the Architecture Selection Framework's regulatory filter (per the paper's filter taxonomy)." Drop the specific `§20.4` claim; §20.4 is "Filter 3: Connectivity and Operational Environment," not regulatory. **Required because F7 is Minor structural-citation but Pedantic-Lawyer-flag-worthy alongside the statutory-citation hygiene discipline.**

### A8 (Required) — GDPR Article 25 acknowledgment (resolves F8)

Add `GDPR Article 25 (Data Protection by Design and by Default)` to the References list. Add one sentence to §"Decision drivers" naming Article 25 as the "cleanest legal-discourse handle for the substrate-vs-content separation; ADR 0064's substrate-tier framework is structurally aligned with Article 25's 'data protection by design' obligation." **Required because F8 is Major (substrate-tier framing gap).**

### A9 (Required) — PCI-DSS framing precision (resolves F9)

Replace the PCI-DSS regime-stance table cell (line 262) with: *"Substrate enables CDE scope-reduction shape via ADR 0046 encrypted-field substrate (rendering PAN unreadable per PCI-DSS v4.0 Req 3.5.1); commercial productization required for token-vault integration AND for the QSA assessment + Report on Compliance / Self-Assessment Questionnaire that any PCI-DSS-covered deployment requires."* **Required because F9 is Major Pedantic-Lawyer.**

### A10 (Encouraged) — EU AI Act Art. 50 citation completeness (resolves F10)

Extend the References citation (line 536) to "EU AI Act (Regulation EU 2024/1689; Arts. 5, 6, 50 + Annex III)." **Encouraged.**

### A11 (Required) — Sanctions OFAC strict-liability framing + default Block (resolves F11)

Add §"OFAC enforcement posture caveat" naming the knew-or-should-have-known framework. Default `OnViolation` for sanctions rules to `Block` (operator must explicitly downgrade to `AuditOnly` per-deployment, with operator-config + counsel-acknowledgment surface). Surface in every sanctions-match UX a "this match places you on notice; OFAC enforcement is strict-liability; consult counsel before proceeding" caveat. **Required because F11 is Major Pedantic-Lawyer (substrate framing risk).**

### A12 (Required) — Audit retention spec for the 7 new events (resolves F12)

Add a §"Retention" subsection. Recommend Option (a): default 7-year retention to satisfy the union of HIPAA (6 years) + OFAC (5 years) + PCI-DSS (1 year), bounded by GDPR data-minimization (records purged on tenant-deletion + lawful-basis-revocation per ADR 0049 redaction primitives). Cite ADR 0049-A1 sibling amendment dependency if retention spec belongs at the audit-substrate tier rather than per-ADR. **Required because F12 is Major.**

### A13 (Required) — Force-enable wording from "operator assumes responsibility" → fact-disclosure shape (resolves F13)

Rewrite the canonical force-enable caveat language to: "Force-enable applies override; substrate informs operator that the configuration may not satisfy applicable regulatory requirements in the active jurisdiction. Operator's primary regulatory obligations are unchanged by this override. Audit event `FeatureForceEnabled` documents the override and operator-attributable click. Consult counsel before proceeding for any regime where regulatory non-compliance carries material liability." Sibling amendment ADR 0062-A2 (or A1.17) propagates to source-of-truth. **Required because F13 is Major Pedantic-Lawyer.**

### A14 (Encouraged) — Disputed-jurisdictions OQ + deferral to taxonomy charter (resolves F14)

Add OQ-0064.8 naming the disputed-jurisdiction policy question; defer to the `Sunfish.Regulatory.Jurisdictions@1.0.0` taxonomy charter Phase 2 work product with general-counsel input. **Encouraged.**

### A15 (Required) — Affirmative legal-advice disclaimer (resolves F15)

Prepend to the reader-caution preamble (line 12) one sentence: *"This ADR is engineering documentation, not legal advice. The regime-stance table and statutory citations are best-effort summaries by engineers; legal determinations under any regime require qualified counsel."* **Required because F15 is Major Pedantic-Lawyer.**

### A16 (Encouraged) — Phase 4 migration cost-class precision (resolves F16)

Name Phase 4 explicitly as "re-authoring against ADR 0064's `JurisdictionalPolicyRule` schema; ADR 0057's existing charter is the *seed* for the new layout but is not preserved as-is. Cost class: per-ADR coordinated amendment (sibling A_n on ADR 0057 + ADR 0060)." **Encouraged.**

### A17 (Encouraged) — Sanctions reload mechanism in OQ-0064.5 (resolves F17)

Extend OQ-0064.5 to pin the mechanism: recommend (b) operator-CLI ingest for v0; (a) timer-pull and (c) Bridge-push deferred to commercial-productization. **Encouraged.**

---

## 4. Quality rubric grade

**Grade: B (mid-B).** Path to A is mechanical (A1–A4 + A6–A9 + A11–A13 + A15 land — the 11 Required amendments).

- **C threshold (Viable):** All 5 CORE present (Context, Decision drivers, Considered options A–D, Decision, Consequences); multiple CONDITIONAL sections (Compatibility plan, Implementation checklist, Open questions, Halt conditions, Revisit triggers, References, Sibling amendment dependencies, Cohort discipline, §A0 cited-symbol audit). No critical *planning* anti-patterns. **Pass.**
- **B threshold (Solid):** Stage 0 sparring present (4 options A–D triangulated honestly with named rejection rationale for B/C/D); FAILED conditions present (5 revisit triggers); explicit halt-conditions (5 enumerated); Cold Start Test plausible *with the 11 Required amendments applied* — without them, a Stage 06 implementer reading the contract surface alone hits unspecified rule-keying (F2), unspecified composite-confidence rule (F1), and unspecified caching policy (F3); Reader-caution preamble + Pedantic-Lawyer council requirement embedded explicitly. **Pass.**
- **A threshold (Excellent):** Misses on five counts:
  - **(1)** §A0 self-audit caught the type-name surfaces well (no false-claims of the 0063-F1 / F2 / F3 shape) but missed paper-§-citation imprecision (F7), affected-packages parenthetical structural ambiguity (F5), and rule-content path divergence (F6). The §A0 pattern handles type-existence well; structural choices that aren't pure type-existence still need council.
  - **(2)** Pedantic-Lawyer-required findings (F8 + F9 + F11 + F13 + F15) are all substantive and would have been missed by a standard-4-perspective council; the parent-intake's Pedantic-Lawyer requirement was load-bearing and validates W#33 §5.9 as cohort precedent.
  - **(3)** Force-enable wording (F13) is Pedantic-Lawyer-flag-worthy across two ADRs (0062 + 0064); sibling amendment to ADR 0062 needed.
  - **(4)** Composite-confidence rule (F1) leaves 21 of 27 cases unspecified after the explicit rule.
  - **(5)** Audit retention (F12) is a substrate-tier gap that surfaces at this ADR but probably belongs at ADR 0049; sibling-amendment ADR 0049-A1 may be the right fix-locus.

A grade of **B with required amendments A1–A4 + A6–A9 + A11–A13 + A15 applied (11 Required) promotes to A.** A5 + A10 + A14 + A16 + A17 (5 Encouraged) may land during Stage 02 or as Stage 06 implementation guidance.

---

## 5. Council perspective notes

- **Distributed-systems / runtime-substrate reviewer:** "The composite-confidence rule's coverage gap is the substrate's largest correctness risk. ADR 0064 names 27 cases in the test-coverage checklist but specifies behavior for ~6; the rest go to Stage-06-implementer-judgment which will produce inconsistent verdicts across implementations. The `IPolicyEvaluator.EvaluateAsync(envelope, featureKey, ct)` rule-keying is unspecified — there's no way for the evaluator to select rules from `(envelope.Regulatory.Jurisdiction, featureKey)` without an `AppliesToFeatureKeys` field on the rule record (or equivalent). Cache invalidation when probe transitions Healthy→Stale is unspecified; ADR 0062's `EnvelopeChange` event stream is the natural invalidation source but ADR 0064 doesn't subscribe to it explicitly. Bridge-tier residency upstream-gate code-path naming is correct in intent ('upstream gate, not downstream filter') but doesn't say where the gate physically lives — both Anchor + Bridge sides per defense-in-depth is the right answer; ADR 0064 names neither. None of these are architectural redesign; all are spec-fill. Eight 1-paragraph fixes." Drives F1 + F2 + F3 + F4.

- **Industry-prior-art reviewer:** "Per-domain ADR 0057 + ADR 0060 generalize cleanly into ADR 0064's substrate — the pattern (per-jurisdiction explicit citation + operator-decision territory for hard cases) is well-trodden. **Missing rule-engine prior-art:** Open Policy Agent (OPA / Rego) is the canonical OSS rule-engine for policy-as-data; AWS Cedar (recently open-sourced; designed for authorization but applicable to compliance) is closer to ADR 0064's typed-rule shape; XACML is the legacy XML-flavored prior-art. ADR 0064 ships its own minimal rule engine (correct call for v0; cost-of-OPA-integration > cost-of-substrate-rule-engine for Sunfish's scope), but should cite these as prior-art so the choice-of-not-using-them is documented. **Missing sanctions SDK prior-art:** OFAC publishes XML/CSV downloads but no SDK; commercial sanctions-screening SDKs (Refinitiv World-Check; Dow Jones Risk & Compliance; ComplyAdvantage) are the de-facto industry standard. ADR 0064's `ISanctionsScreener` is correctly framed as operator-pluggable (substrate doesn't ship a screener implementation; operator wires their preferred SDK), but the prior-art should be cited. **Missing cloud-residency products:** Microsoft EU Data Boundary (announced 2022; GA 2024); AWS Regional services (residency-by-region); GCP regional services. ADR 0064's data-residency-as-record-class-aware constraint is more granular than these (cloud-residency is per-tenant; ADR 0064 is per-record-class), which is *good* — but the cloud-residency products should be cited as the substrate-vs-cloud-residency boundary." Drives F8 (GDPR Art. 25 prior-art) + adjacent prior-art gaps; surfaces in §6 cohort discipline scorecard as prior-art completeness.

- **Cited-symbol / cohort-discipline reviewer:** "Three-direction spot-check ran on every cited symbol. **Positive-existence:** all 8 cited ADRs verified Accepted on `origin/main` (clean). The post-A1 surface citations (`OverridableWithCaveat` per A1.9; `ProbeStatus` per A1.10; A1.14 §A0 audit pattern; A1.15 + A1.16 on ADR 0063) all positively-verify (unlike ADR 0063's invented `MinimumSpecDimension`). The 7 new `AuditEventType` constants don't collide. W#22 active-workstreams Phase 6 deferred status correctly cited. **Negative-existence:** found 0 invented type names in the contract surface — encouraging signal that the §A0 self-audit caught the type-name surface this time. **Structural-citation correctness:** found 3 (F5 affected-packages parenthetical, F6 rule-content path divergence, F7 paper §-citation imprecision). Lower than ADR 0063's 4 of 4 but still nonzero. The §A0 self-audit pattern handles type-existence well and structural choices around namespace + path + paper-§ are still council-canonical. **Cohort-discipline scorecard:** ADR 0064's structural-citation count (3) is better than ADR 0063's (4) and worse than ADR 0061's (1). Net: §A0 self-audit ratchets up the floor on type-existence; council remains canonical on cross-cutting structural choices." Drives F5 + F6 + F7 + cohort scorecard updates.

- **Forward-compatibility / migration reviewer:** "Phase 1 substrate-only deployability is genuinely deployable per Option A — substrate ships without rule content; consumers know it. But the substrate-without-rules failure mode the ADR names ('consumers may misread Phase 1 as regulatory-compliant when it's not') is a real risk; Phase 1 hand-off + apps/docs page must explicitly disclaim, and the reader-caution preamble must propagate forward (mechanical fix exists in halt-condition #3). Rule-content data file format is OQ-0064.2 (recommend JSON) — the OQ should be closed at this ADR (don't leave format choice for Stage 06 implementer). Phase 4 cross-cutting refactor mechanical-vs-rewriting matters: ADR 0057's rules don't fit ADR 0064's `JurisdictionalPolicyRule` schema as-is; Phase 4 is re-authoring not renaming; cost-class should be named explicitly. Rule-content versioning per OQ-0064.7 — recommendation is filename or metadata semver; this is also closeable at this ADR. Sanctions list reload mechanism per OQ-0064.5 — daily-cadence is named but not the mechanism (pull / push / Bridge); recommend operator-CLI ingest for v0." Drives F16 + F17 + adjacency on OQ-closure encouragement.

- **Pedantic-Lawyer perspective (REQUIRED per parent-intake halt-condition + W#33 §5.9):** "Reader-caution discipline rigor is high — the preamble + 5 halt-conditions + reader-caution applied to all statutory citations is excellent for engineering documentation. Findings cluster at five framing risks. **(1) GDPR Article 25 absence (F8)** — substrate-tier framework that does Article-25 work without citing Article 25 is missing the cleanest legal-discourse handle; mechanical fix. **(2) PCI-DSS framing 'enables tokenization shape' (F9)** is industry-shorthand that overpromises substrate capability; Sunfish has encrypted-field substrate (ADR 0046) not tokenization; the framing implies a token vault that doesn't exist; rewrite to 'CDE scope-reduction shape via ADR 0046 + Req 3.5.1.' **(3) Sanctions emit-without-enforce framing (F11)** is the highest-stakes finding: OFAC's knew-or-should-have-known framework means once `SanctionsScreeningHit` fires, the operator is on notice; if substrate offers 'merely log' as a neutral option, the operator who picks it may be in worse legal posture than the operator who never installed Sunfish's screener. Default `OnViolation` for sanctions to `Block`; require explicit operator-config + counsel-ack to downgrade. **(4) Force-enable 'operator assumes responsibility' (F13)** reads as click-through indemnification; consumer-protection regimes don't honor that; the right framing is fact-disclosure (substrate informs operator; operator's primary liability is unchanged). Sibling amendment to ADR 0062 needed because the canonical wording lives there. **(5) Affirmative legal-advice disclaimer absent (F15)** — the reader-caution covers citation accuracy but doesn't say 'this whole document, including the regime-stance table, is not legal advice.' One sentence prepended to the preamble fixes it. **EU AI Act citation (F10)** is shape-correct but missing Art. 50 (limited-risk transparency obligations); citation-completeness gap. **OFAC SDN vs broader OFAC consolidated list:** ADR 0064 cites 'OFAC SDN/sectoral lists' (correct framing — SDN is the canonical primary list; sectoral lists are the secondary lists like SSI under EOs 13662/13685); the broader OFAC consolidated list framing isn't used; this is fine. **EU consolidated sanctions list:** correctly cited (line 537). **Disputed-jurisdictions naming (F14):** taxonomy-charter Phase 2 work product; not blocking. The Pedantic-Lawyer-required council found 5 substantive Major-class findings + 2 Encouraged that the standard-4-perspective council would not have surfaced; the parent-intake's halt-condition was load-bearing." Drives F8 + F9 + F11 + F13 + F15 + adjacent F10 + F14 on Pedantic-Lawyer review density.

---

## 6. Cohort discipline scorecard

| Cohort baseline (before this review) | This council review |
|---|---|
| **Substrate-amendment council batting average:** 14-of-14 needing council fixes | **15-of-15** if A1–A4 + A6–A9 + A11–A13 + A15 (11 Required) land. Cohort lesson holds: every substrate ADR/amendment so far has needed council fixes. |
| **Council false-claim rate:** 2-of-12 prior councils | **0-of-17 spot-checks fired in this review.** All findings positive-existence / negative-existence / structural-citation correctness verified twice before publishing. F1's 27-case gap verified by re-counting the rule + ProbeStatus interaction matrix. F2's rule-keying verified by reading `JurisdictionalPolicyRule` record fields directly. F3's caching gap verified by full-text search for "cache" / "invalidat" in ADR 0064. F5/F6/F7 verified by `git ls-tree` + `grep` against actual paper §20 heading text. F8/F9/F11/F13/F15 are framing findings derived from the Pedantic-Lawyer perspective; structural verification is N/A but the framing claims (Article 25 = privacy-by-design; PCI-DSS Req 3.5.1; OFAC strict-liability) are accurate. |
| **Structural-citation failure rate (XO-authored):** 10-of-14 amendments (~71%) | **3-of-14 + this one's 3 = 13-of-15 amendments having structural-citation findings** (~87%) — but this ADR's count (3) is **lower than ADR 0063's (4)** and **higher than ADR 0061's (1)**. Net signal: §A0 self-audit pattern catches type-existence well (no invented types this time vs 4 in ADR 0063); structural choices around namespace + path + paper-§ remain council-canonical. **Encouraging discipline trend.** |
| **§A0 self-audit catch rate (post-ADR-0063 baseline):** 0-of-4 on ADR 0063 | **Substantially better than 0063 baseline.** The §A0 audit on ADR 0064 caught: positive-existence of all 8 cited ADRs (clean); post-A1 amendment surface citations (A1.9 + A1.10 + A1.14 + A1.15 + A1.16 — all positively verify); 7 new audit constants non-collision. The 3 structural-citation findings (F5 / F6 / F7) the council surfaced are NOT type-existence claims — they're cross-cutting structural choices (package convention / path convention / paper §-precision). The §A0 audit's design-target (catch invented type names) succeeded; the council remains canonical for the structural-choice class. |
| **Severity profile** | 0 Critical (none of F1–F17 reach the `dotnet build`-fails-at-compile bar); **10 Major** (F1, F2, F3, F4, F8, F9, F11, F12, F13, F15); **6 Minor** (F5, F6, F7, F10, F16, F17); **1 Encouraged** (F14) = **17 findings** + **10 verification-pass findings** for 27 total. **Pedantic-Lawyer-driven findings: 5 (F8, F9, F11, F13, F15) — highest single-perspective contribution this cohort.** |
| **Pre-merge council vs post-merge council** | **Pre-merge** — auto-merge intentionally DISABLED per ADR 0064's own §"Cohort discipline" + parent-intake halt-condition #4. Pre-merge cost: 2–3 hours XO ADR editing + ADR 0062 sibling-amendment for force-enable wording (F13). Post-merge cost would be: rewrite framing across regime-stance table + sanctions UX + force-enable language + paper §-citation + 4 spec-fill subsections = ~5–6 hours XO + Pedantic-Lawyer re-review + risk of partial-fix landing in tranches. **Pre-merge is dramatically cheaper.** |
| **Pedantic-Lawyer-required council validation** | The parent-intake halt-condition #4 + W#33 §5.9 hardening precedent required the 5th perspective. **5 of 17 findings** (29%) were Pedantic-Lawyer-driven; standard-4-perspective council would have missed F8 + F9 + F11 + F13 + F15. **Validates the parent-intake halt-condition as load-bearing for ADR 0064's class of work.** Cohort-discipline recommendation: every future ADR in the W#33 follow-on queue with regulatory framing engages a Pedantic-Lawyer perspective subagent. |

The cohort lesson holds: every substrate ADR/amendment so far has needed council fixes; pre-merge council is dramatically cheaper than post-merge; structural-citation correctness is the most-frequent failure mode; the §A0 audit ratchets the floor up on type-existence (encouraging trend from 0063 → 0064) but cross-cutting structural choices + framing risks remain council-canonical; Pedantic-Lawyer-perspective is load-bearing for regulatory-class ADRs.

---

## 7. Closing recommendation

**Accept ADR 0064 with required amendments A1–A4 + A6–A9 + A11–A13 + A15 (11 Required) applied before any rule-content data file or `apps/docs/foundation/regulatory-policy/` text ships.** The architectural decision (substrate-vs-content separation; Option A; per-domain ADR composition; reader-caution + halt-conditions for legal-counsel engagement) is correct and consistent with substrate-cohort design taste. The 11 required mechanical fixes are 2–3 hours of XO work plus a sibling-amendment ADR 0062-A2 (or A1.17) for the force-enable wording propagation; the 5 Pedantic-Lawyer findings are framing-and-disclaimer fixes (low engineering cost, high legal-discourse value); the 3 structural-citation findings are search-and-replace fixes; the 4 spec-fill subsections (composite-confidence truth-table; rule-keying; caching policy; Bridge two-sided gate) are each one paragraph + one table.

Do **NOT** promote to `Accepted` until A1–A4 + A6–A9 + A11–A13 + A15 land. The 5 Pedantic-Lawyer findings (F8 GDPR Art. 25 / F9 PCI-DSS framing / F11 sanctions OFAC strict-liability / F13 force-enable wording / F15 legal-advice disclaimer) directly affect how regulators, counsel, and operators read this ADR — the framing risks are not "compile-fail" gateable but they ARE "regulator-reads-this-and-concludes-Sunfish-doesn't-know-its-regulatory-substrate" gateable. Estimated rewrite cost: 2–3 hours of ADR editing + 30 min ADR 0062 sibling-amendment, zero code changes (Phase 1 substrate not yet built), zero downstream-intake-rework.

If A1–A4 + A6–A9 + A11–A13 + A15 do not land within ~1 working day of council acceptance, the right move is **Reject and re-propose** rather than letting framing risks ship to Stage 02 — the council made this same call on ADR 0063, and the cohort batting average says it's cheaper than partial-fix post-merge. The Pedantic-Lawyer-perspective requirement embedded in the halt-conditions means *future* council reviews of any ADR that touches regulatory framing must include the perspective; that discipline begins with this ADR landing the 5 Pedantic-Lawyer-driven findings cleanly.

The §A0 cited-symbol audit pattern continues to evolve. ADR 0064's §A0 caught type-existence well (substantially better than ADR 0063's 0-of-4); the 3 structural-citation findings the council surfaced are cross-cutting structural choices (package convention; path convention; paper §-precision) rather than type-existence — a different failure class than the §A0 audit's design-target. **Recommendation for cohort discipline going forward (per A18-equivalent):** the §A0 audit's design-target is type-existence + post-amendment surface citations; structural-choice cross-cutting decisions remain council-canonical. The pattern is necessary-but-still-not-sufficient; council remains canonical for substrate ADRs in the W#33 lineage; Pedantic-Lawyer perspective is REQUIRED for any ADR in this lineage that touches regulatory framing.

W#33 §7.2 follow-on queue ordering: ADR 0064 was the fifth and final item; with A1–A4 + A6–A9 + A11–A13 + A15 applied, the W#33 §7.2 follow-on authoring queue closes. The cohort lessons from this review carry forward to the rule-content authoring layer (Phase 3+; legal-review-gated): the Pedantic-Lawyer perspective applies *especially* to per-regime rule-content authoring; statutory-citation hygiene + framing-risk discipline + reader-caution propagation become inputs to the legal-review framework engagement.

