# ADR 0060 (Right-of-Entry Compliance Framework) — Council Review

**Reviewer:** research session (adversarial council, UPF Stage 1.5)
**Date:** 2026-04-29
**Subject:** ADR 0060 v. 2026-04-29 (Proposed) — `Foundation.JurisdictionPolicy` substrate + 8-jurisdiction default seed + override-stack semantics + `IEntryComplianceChecker` + 5 audit event types
**Companion artifacts read:** ADR text; `docs/adrs/0053-work-order-domain-model.md` (consumer; defines `WorkOrderEntryNotice` + `JurisdictionPolicyId` opaque reference); `docs/adrs/0056-foundation-taxonomy-substrate.md` (`TaxonomyClassification`, `IdentityRef`, `TenantId` types it consumes); `docs/adrs/0049-audit-trail-substrate.md` (audit pattern); `docs/adrs/0008-foundation-multitenancy.md` (multi-tenancy semantics — note ambiguous "tenant" usage); `docs/adrs/0013-foundation-integrations.md` (provider-neutrality for the rejection of Option C); `_shared/product/local-node-architecture-paper.md` §6.3 (CP/AP positioning), §13 (kernel boundaries).

---

## 1. Verdict

**Accept with amendments — five required, three optional.** The architectural shape is correct: foundation-tier, data-not-code, override-stack, audit-emission discipline, conservative `US-DEFAULT` fallback. Substrate composition with ADR 0049 / 0053 / 0056 is principled. **The load-bearing problems are not in the architecture — they are in the default seed (which the ADR itself flags as paralegal-grade), the override-stack semantics (which conflate "more-restrictive" across two non-comparable axes), and the emergency-exception surface (which is the widest single bypass in the substrate and has only post-hoc audit as a brake).** Five amendments below close those gaps; none alter Option A. ADR is otherwise well-scaffolded — rubric grade B+, with a clear A path.

---

## 2. Anti-pattern findings (21-AP sweep)

| AP | Severity | Where it fires |
|---|---|---|
| **AP-21 Assumed facts without sources** | **High** | The default seed admits to being paralegal-grade, but two specific citations look wrong on a quick read: **(a)** Texas Property Code §92.0081 is the *repair-and-deduct* statute; the relevant Texas entry rule is the contractual default under §92.351 and the implied-covenant case law (no statutory 24h notice in Texas — that's a policy default, not a statutory one; the "TPC §92.0081" cite would mislead an attorney pass). **(b)** 24 CFR §966.4 is the **public housing** lease-clause regulation; HUD-subsidized housing under §8 vouchers is governed by a different framework (24 CFR §982 series + state law), and the "48h" claim isn't drawn from §966.4's text. **(c)** RCW 59.18.150 (WA) does say 48h notice for entry but the "8a–6p" hours bound is not statutory in Washington — that's an operator-imposed reasonable-hours interpretation, not the statute. The ADR labels these `Citation` as if they are the binding statutory source; for paralegal-grade defaults that's overpromising precision. |
| **AP-1 Unvalidated assumption** | **High** | Override stack: "most-restrictive wins; can't loosen below jurisdiction floor." The "most-restrictive" axis is **multi-dimensional** (notice-hours, permitted-hours-of-day, permitted-purposes, emergency-exception). Two policies can be incomparable: HUD requires more-restrictive disclosure (more-restrictive on the *content* axis) but state law requires shorter-hours-of-day window (more-restrictive on the *time* axis). The substrate's `EntryPolicy` is a single value object — there is no defined merge function for "most-restrictive across vector dimensions." The ADR asserts a total order where there is only a partial order. |
| **AP-1 Unvalidated assumption** | **High** | "`TenantId`" is used in the API (`ResolveForAsync(JurisdictionId, TenantId tenant, PropertyId? property, ...)`) but in property-management context the word "tenant" is overloaded: ADR 0008's `TenantId` is the **multi-tenancy boundary** (the BDFL's organization), while in this ADR's domain "tenant" colloquially means the **renter occupying the unit** (the party being notified). The override stack's "per-tenant override" — does that mean "this BDFL organization's default policy" or "this specific renter's medical-needs policy"? The ADR text seems to mean *both* in different paragraphs; the API uses `TenantId` (org-scope) but the override-stack table shows "this leaseholder has medical reasons" (renter-scope, distinct from `TenantId`). Stage 02 implementer cannot distinguish without re-reading three times. |
| **AP-3 Vague success criteria** | Med | "8 default-jurisdiction policies parse correctly" is a tautology (a record literal will always parse). The actual quality gate — *do these defaults reflect the real jurisdiction's binding law?* — is acknowledged as out-of-scope until attorney-pass milestone but no measurable acceptance for "we shipped paralegal-grade and that's good enough" is stated (e.g., "no policy default is more *permissive* than the binding statute on any axis" — which would be the conservative-correctness invariant). |
| **AP-13 Confidence without evidence** | Med | `EmergencyExceptionPolicy.AllowEntryWithoutNotice = true` + `EmergencyDefinition` (free-text string) + `RequirePostHocNotice` (bool) + `PostHocNoticeWithinHours` (int?) is the **single widest bypass** in the entire substrate. The ADR claims compliance is "enforced at substrate boundary" but the `IsEmergency: true` field on `EntryAttempt` is **operator-asserted**, not substrate-validated. The only brake is the audit record `EntryEmergencyOverride` (post-hoc). For a tenant lawsuit defense, "we wrote 'water leak' in the EmergencyDefinition field" is not a defense — it's an *assertion*. Substrate cannot distinguish a real emergency from a fabricated one; the audit captures the assertion, not the truth. ADR doesn't acknowledge this asymmetry. |
| **AP-19 Missing tool fallbacks** | Med | What happens when `PostHocNoticeWithinHours` is set (say, 24h) and the operator misses the window? The ADR specifies an emergency-override audit emission but no **structural penalty or follow-up enforcement** — there is no "stale post-hoc obligation" check, no notification to the operator, no audit-projection that surfaces unfulfilled post-hoc-notice obligations. The compliance posture quietly degrades to "the audit record exists but the obligation went unmet" with no detection path. |
| **AP-9 Skipping Stage 0 / first-idea-unchallenged** | Low | Three options analyzed (foundation substrate / inline / third-party). Triangulation is real; survives. |
| **AP-15 Premature precision** | Low | `JurisdictionId` examples in §Initial-contract-surface include `"US-NY-NYC"`, `"US-UT-SLC"`, `"US-FED-HUD"` — three different naming conventions in three lines. `US-FED-HUD` is not a geographical subdivision, it's a regulatory program; mixing those into the same identifier namespace is a conceptual conflation that will surface the first time a New York property is in HUD-subsidized housing (now you need both `US-NY-NYC` *and* `US-FED-HUD` to apply, but `JurisdictionId` is a single value). |
| **AP-18 Unverifiable gates** | Low | "Default seed is paralegal-grade pending attorney-pass milestone" — no acceptance criterion for what constitutes "paralegal-grade adequate-for-Phase-2.1." A working definition: "no default is more permissive than the binding statute we cite, on any axis, and citations are correctly named and linkable." If that's what's meant, say so. |

No critical AP fires. AP-21 (citation accuracy) + AP-1 (override-stack vector-vs-scalar + `TenantId` overload) + AP-13 (emergency surface) are the three load-bearing findings.

---

## 3. Top 3 risks (highest impact first)

### Risk 1 (HIGH) — Citation errors in paralegal-grade defaults

The ADR explicitly admits the seed is paralegal-grade. But three of the eight citations look wrong on a quick read:

- **TX:** §92.0081 is *repair-and-deduct*, not entry. Texas has no statutory 24h notice rule for entry; the doctrine is "reasonable notice" via case law and lease terms. The default policy may be defensible as *operator practice*, but the citation is misleading.
- **HUD:** 24 CFR §966.4 is **public housing** (PHA-managed), not §8 voucher housing. If the BDFL's HUD-subsidized property is voucher-based, §966.4 doesn't apply; the operator could be sued under the wrong-statute defense.
- **WA:** RCW 59.18.150 is correctly cited for 48h notice. The "8a–6p" hours-of-day is **not statutory** in WA — it's an operator-imposed reasonable-hours interpretation. Marking it as `RCW §59.18.150`'s rule risks an attorney pointing out the statute doesn't say that.

Why this matters at the substrate level: the ADR positions defaults as "paralegal-grade pending attorney pass" — that's an honest disclaimer, but the substrate's compliance check throws `EntryNoticeComplianceException` with `CitedRules` populated from these strings. The citations end up in audit records as if they were binding sources. A lawsuit defense built on "we cited TPC §92.0081 in our compliance record" doesn't survive a 30-second statutory check by opposing counsel. **The substrate is engineered to be defensible; the seed undercuts that engineering.**

Mitigation in this PR: amend the seed to either (a) cite the correct sections, or (b) use placeholder citations like `"Texas: reasonable-notice doctrine; substrate-default 24h pending attorney pass"` that don't pretend to be statutory pin-cites. The conservative defaults are still safe; only the authority claim needs softening.

### Risk 2 (HIGH) — Override-stack "most-restrictive wins" is a partial order, not a total order

"Most-restrictive wins" presumes you can compare two policies and pick the tighter one. For scalar axes (notice hours: 24h vs 48h → 48h wins) this is well-defined. For vector axes the comparison is ill-defined:

- HUD overlay says "additional disclosure language required in the notice template."
- State law says "8a–5p only."
- HUD says nothing about hours-of-day.

The "most-restrictive" merge of these two has to keep both: HUD's content rule + state's hours rule. That's a **union** operation, not a max. The ADR's `EntryPolicy` is a single value record; merging two `EntryPolicy` records via "most-restrictive" requires a per-field merge function that the ADR doesn't specify. If you implement it naively as "pick the policy with the longer notice window," you'll silently drop HUD's content disclosure on properties where state notice happens to exceed HUD's.

This is most acute on the **`PermittedPurposes`** axis: HUD's permitted-purpose set may be narrower than state's. Most-restrictive-wins on a *set* axis is "intersection." Most-restrictive on `PermittedHours` is "intersection of time intervals." Most-restrictive on `MinimumNoticeHours` is "max." Most-restrictive on `EmergencyExceptions` — does the operator inherit *both* emergency-exception policies, the more-restrictive of the two, or HUD's overrides state's? The ADR doesn't say.

Stage 02 implementer will pick *some* merge semantics; without ADR-level guidance, that choice becomes load-bearing for compliance-defense, and may be wrong. This is the single highest-leverage amendment in the review.

### Risk 3 (HIGH) — Emergency exception is a self-asserted bypass with audit-only enforcement

`EntryAttempt.IsEmergency` is an operator-set bool. `EmergencyDefinition` is a free-text string. The substrate cannot distinguish:

- A genuine 3am water leak (legitimate emergency)
- A "we needed to show the unit and didn't have time to notice" (illegitimate, dressed up as emergency)
- A retaliatory entry where the operator wrote "tenant complaint follow-up" (illegitimate, narrowly fabricated)

The only brakes are:
1. Audit emission of `EntryEmergencyOverride` with the (operator-supplied) `EmergencyDefinition`
2. `RequirePostHocNotice` workflow obligation
3. The §Trust-impact line: "can't claim 'emergency' without naming the condition"

But (1) records the assertion, doesn't validate it; (2) is bypassable per Risk-related AP-19 (no enforcement of the post-hoc window); (3) is the same constraint as (1) — naming a condition isn't the same as the condition being real.

The legal risk here is real: in California specifically, `CCP §1954(b)` allows entry without notice "in case of emergency" but case law has narrowly construed "emergency" to mean *imminent threat to property or person, not mere convenience*. An operator who routes 30% of entries through the emergency exception is at risk of a statutory-damages award per §1954(g) regardless of what the audit says, because the audit doesn't establish the emergency was real.

The substrate can't validate emergency-truth (only a court can). But the ADR can add **structural friction** that makes wholesale emergency-pattern abuse visible:
- A rate metric (`EmergencyEntryFrequencyPerProperty` projected from audit) with a default threshold (e.g., >3 emergency entries in 30 days on the same unit triggers a `JurisdictionPolicyOverrideApplied` warning record)
- An audit-projection that surfaces unfulfilled post-hoc-notice obligations (closes Risk-related AP-19)
- A required `WitnessedBy` field on the `EntryAttempt` for emergency cases (third-party identity, e.g., the responding plumber, fire marshal, etc.) so the audit captures more than the operator's own assertion

None of these prevents abuse, but they raise the audit record from "operator self-attestation" to "structurally corroborated." That's the difference between a defensible compliance posture and a paper trail.

---

## 4. Top 3 strengths

1. **`US-DEFAULT` conservative fallback is the right design discipline.** When the BDFL doesn't configure a jurisdiction, the substrate fails *closed* (48h, 9a–5p, conservative permitted-purposes) rather than *open* (no policy → no compliance check). This is the right safety posture and the right paper-§13-aligned kernel-boundary discipline: substrate enforces the floor; operator can be more-restrictive; operator cannot accidentally bypass via misconfiguration.
2. **Foundation-tier substrate consumed by W#19 / W#22 / W#25 is the architecturally correct level.** The same compliance check fires for work-order entry, leasing-pipeline showings, and inspection visits — which is exactly right because the binding law doesn't care which Sunfish workflow is doing the entering. Inlining into `blocks-maintenance` (Option B) would have left leasing and inspections to re-derive the policy, with drift inevitable. The package count cost is real but minor; the consolidation benefit is large.
3. **Reuses ADR 0049 audit substrate + ADR 0056 taxonomy substrate without inventing new primitives.** Five new `AuditEventType` constants follow the established W#31 pattern; `JurisdictionAuditPayloadFactory` mirrors the established factory pattern; `Sunfish.Entry.Purposes@1.0.0` is a taxonomy charter under ADR 0056 rather than a hardcoded enum. The substrate is composed, not invented. This is the kind of cross-ADR composition that makes the architecture pay off (Manager view: this is the cumulative dividend of having ADR 0049 + 0056 in place first).

---

## 5. Required amendments (Accept-with-amendments)

| # | Severity | One-line amendment |
|---|---|---|
| **A1** | **Critical** | **Citation accuracy pass on default seed.** Either (a) replace TX `§92.0081` with the correct entry-rule cite or a "no statutory entry rule; reasonable-notice doctrine" placeholder; (b) clarify HUD `24 CFR §966.4` applies to public housing only, with a separate note that Section 8 voucher housing is governed elsewhere (or restrict the `US-FED-HUD` JurisdictionId to public-housing scope and add `US-FED-S8` later); (c) qualify the WA `8a–6p` hours-of-day as "operator-default within reasonable-hours doctrine, not statutory." Net effect: the audit's `CitedRules` field becomes legally defensible rather than misleadingly precise. |
| **A2** | **Critical** | **Per-axis merge semantics for the override stack.** Specify that "most-restrictive wins" decomposes per-field: `MinimumNoticeHours = max(...)`, `PermittedHours = intersection(...)`, `PermittedPurposes = intersection(...)`, `EmergencyExceptions` policy named (recommend: HUD overlay's emergency policy *replaces* state's only when stricter on each sub-field; otherwise per-field merge), `CitationReference` accumulates (`["CCP §1954", "24 CFR §966.4"]`). State the merge function as a named operation (`EntryPolicy.MergeMostRestrictive(IEnumerable<EntryPolicy>) → EntryPolicy`) and acceptance-test it explicitly. Without this, Stage 02 implementer picks merge semantics ad-hoc; that choice becomes load-bearing for compliance defense. |
| **A3** | **Critical** | **`TenantId` overload disambiguation.** The current API signature `ResolveForAsync(JurisdictionId, TenantId tenant, PropertyId? property, ...)` reads as if "tenant" is the renter, but ADR 0008's `TenantId` is the BDFL-org boundary. Either (a) rename the parameter to `OperatorTenantId` and add a separate `LeaseholderId? leaseholder` parameter to capture the renter-side override, or (b) reorganize the override-stack signature as `Resolve(JurisdictionId, OperatorTenantId, PropertyId?, LeaseholderId?)` with explicit naming. Update the §Tenant-override-mechanism subsection to use the disambiguated terms throughout. This is a 30-line edit and unblocks every consumer ADR from re-reading the override stack three times. |
| **A4** | **Major** | **Emergency-exception structural friction.** Add three substrate-level brakes: (a) require `EntryAttempt.IsEmergency = true` to carry an `EmergencyWitnessedBy: IdentityRef?` field (third-party identity such as responding contractor / fire marshal / police case number — nullable in v1, required in Phase 2.2 per a new revisit trigger); (b) project `EmergencyEntryFrequencyPerProperty` from the audit substrate with a default warning threshold (e.g., >3 emergency entries / 30-day rolling window on the same `PropertyId` emits a `JurisdictionPolicyOverrideApplied`-style audit record visible to the operator); (c) project `UnfulfilledPostHocNoticeObligations` so missed post-hoc windows are detectable, not silently degraded. These don't prevent abuse but raise the audit posture from self-attestation to structurally-corroborated. |
| **A5** | **Major** | **`JurisdictionId` namespace conflation.** `US-FED-HUD` is a regulatory-program identifier mixed into a geographical-jurisdiction namespace. Recommend either: (a) split into two registry axes — `GeographicalJurisdictionId` (`US-CA`, `US-NY-NYC`) + `RegulatoryProgramId` (`HUD-PUBLIC-HOUSING`, `HUD-S8-VOUCHER`) — and let the override stack take both; or (b) keep a single `JurisdictionId` but document the format as `Region-Authority-Program` with explicit grammar (`US-FED-HUD-PHA` vs `US-FED-HUD-S8`). Without this, the first time a NYC property in HUD-subsidized housing surfaces, the operator has to pick between `US-NY-NYC` and `US-FED-HUD` and silently lose the other set of rules. Closes the partial-order problem in A2 by making the stack more legible. |

Optional but encouraged:

| # | Severity | One-line amendment |
|---|---|---|
| **A6** | Minor | **Conservative-correctness invariant as an acceptance test.** Add to §Implementation-checklist: "for every default in `Sunfish.JurisdictionPolicy.Defaults@1.0.0`, the policy is no more permissive than the binding statute on any axis; verified by jurisdiction-by-jurisdiction comparison table maintained alongside the seed file." This makes the paralegal-grade-but-conservative claim measurable, closes AP-3 + AP-18, and makes the attorney-pass milestone an additive change (tighten where statutes are tighter than defaults), not a corrective one. |
| **A7** | Minor | **Tenant-side acknowledgment as a v1 substrate hook (not just a Phase 3+ revisit trigger).** ADR mentions tenant-side acknowledgment in revisit triggers; recommend adding an optional `TenantAcknowledgedAt: DateTimeOffset?` to `WorkOrderEntryNotice` (or wherever the consumer holds it) and a `EntryNoticeAcknowledgedByTenant` audit event in v1 — even if no UI surfaces it yet — so when Phase 3 surfaces a UI, the substrate doesn't need a schema migration. Costs ~5 lines, removes a future migration. |
| **A8** | Minor | **`EntryAttempt.NotifyingParty` IdentityRef constraints.** The field is declared but the substrate doesn't enforce that the notifying party has a capability (per ADR 0032 macaroon) to enter the property. A bookkeeper with a financial-only macaroon shouldn't be a valid `NotifyingParty`. Recommend documenting that `IEntryComplianceChecker` validates `NotifyingParty` has `EntryNotice.Send` capability, or explicitly defer to caller (and name the consumer's responsibility in each consumer ADR). Closes AP-19-adjacent. |

A1, A2, A3 should land before W#19 Phase 6 wiring or any consumer ADR (W#22 leasing showings; W#25 inspections future) advances to Stage 02. A4, A5 can land in parallel with consumer-ADR Stage 02 work but before any audit-derived metric is built. A6, A7, A8 are fast-follow polish.

---

## 6. Quality rubric grade

**B+ (Solid, with clear A path).**

Rationale:

- **C floor cleared.** All 5 CORE sections present (Context, Decision drivers, Considered options, Decision, Consequences). Multiple CONDITIONAL sections (Compatibility plan, Open questions, Revisit triggers, References, Pre-acceptance audit). No critical AP fires.
- **B floor cleared.** Stage 0 evident (three options, explicit verdicts, conservative-default reasoning). FAILED conditions / kill triggers present as five revisit triggers, each externally-observable. Confidence Level declared (MEDIUM, with calibrated reasoning that names policy-data accuracy as the load-bearing risk surface). Cold Start Test stated; the implementation checklist is concrete (10 items).
- **A ceiling missed by:** No structured Assumptions table (per UPF Stage 1; the ADR's reasoning is implicit, not surfaced as Assumption → VALIDATE BY → IMPACT IF WRONG). Default-seed accuracy is acknowledged as paralegal-grade but no measurable acceptance test for "good-enough Phase 2.1" (closed by A6). Override-stack semantics are stated as a slogan, not a function (closed by A2). Emergency-exception surface is the widest substrate bypass and has no structural friction beyond audit (closed by A4).
- **Calibrative confidence.** ADR's self-declared MEDIUM is correctly calibrated — the council confirms MEDIUM, not HIGH, primarily because of A1+A2+A3 (citation accuracy + merge semantics + identifier overload). With the five required amendments landed, confidence rises to HIGH and rubric to A.

---

## 7. Foundational-paper alignment check

The ADR is paper-§13-faithful (kernel boundary enforces the floor; substrate consumers can be more-restrictive; substrate cannot be bypassed by reviewer discipline). It does not contradict any paper claim.

One paper-adjacent observation: the local-first-offline requirement (§5, §11) is correctly invoked to reject Option C (third-party API). Worth noting that the default seed shipping with the substrate means the substrate is fully operational under Anchor's offline mode — that's an additional pro for Option A that the ADR could surface, but it's not required.

The CP-class positioning of `EntryPolicy` in §Revisit-triggers ("jurisdiction overrides shouldn't conflict under partition") is paper-§6.3 correct. The override stack as a deterministic function (once A2 lands) is also CP-class-coherent — partition can't reorder override precedence because the precedence is total per (`OperatorTenantId`, `PropertyId`, `LeaseholderId`) tuple.

---

## 8. Reviewer's bottom line for the CTO

ADR 0060 is the right substrate at the right tier with the right composition discipline. **The architectural shape is not the problem; the seed accuracy, the override-merge semantics, and the emergency-bypass surface are.** Three of those (A1, A2, A3) are corrections that make existing claims actually true; the other two (A4, A5) close gaps that consumer ADRs will surface as Stage 02 ambiguity if not closed now.

Estimated rewrite cost: **2–4 hours of ADR editing** (citation pass requires the most external lookup; merge-semantics function definition is the most architecturally-load-bearing edit; `TenantId` rename is mechanical), **0 code changes** (substrate hasn't been built yet). Worth doing before flipping to Accepted because three downstream consumer ADRs (ADR 0053 W#19 Phase 6 wiring, ADR 0057 leasing showings, future inspections ADR) all bind to the merge semantics + override-stack signature.

If A1–A3 don't land within ~1 working day, the right move is **Reject and re-propose** rather than ship paralegal-grade citations into audit records, an ill-defined merge function into compliance code, and an overloaded `TenantId` parameter into every consumer's API surface. A4–A5 can land in parallel with consumer-ADR Stage 02 work; A6–A8 are fast-follow.

The default seed's "paralegal-grade pending attorney pass" disclaimer is correct policy and should remain. But "paralegal-grade" still means "the citations are accurate as paralegal work product." The current seed has at least three citations that wouldn't survive a paralegal's own QA pass — fix those and the disclaimer becomes honest rather than apologetic.
