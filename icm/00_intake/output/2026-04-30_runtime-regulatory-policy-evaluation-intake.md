# Intake — Runtime Regulatory / Jurisdictional Policy Evaluation

**Date:** 2026-04-30
**Requestor:** XO research session (synthesis output of W#33 Mission Space Matrix discovery)
**Request:** New ADR ~0064 specifying runtime regulatory/jurisdictional policy evaluation — how a Sunfish deployment determines its current jurisdiction, evaluates per-jurisdiction policy rules at feature-invocation time, and enforces data-residency / sanctions / industry-compliance constraints.
**Pipeline variant:** `sunfish-feature-change` (introduces new cross-cutting policy contract)
**Stage:** 02 — ADR 0064 drafted (Proposed); awaiting general-counsel engagement + ONR-led research before Status: Accepted
**Owner:** ONR (Office of Naval Research session) — transferred from XO per CO directive 2026-05-06
**ADR:** [`docs/adrs/0064-runtime-regulatory-policy-evaluation.md`](../../docs/adrs/0064-runtime-regulatory-policy-evaluation.md) — Status: Proposed; council + general-counsel sign-off required before Accepted

> **Reader caution (Pedantic-Lawyer hardening pass output, carried forward from Mission Space Matrix §5.9):** specific statutory citations in this intake have not been verified against current Official Code text and may use practitioner shorthand. The downstream ADR MUST engage qualified general counsel before specifying enforcement behavior. This intake describes a *gap*, not a *solution*; the solution requires legal review.

---

## Problem Statement

Sunfish has no cross-cutting runtime regulatory evaluation layer. Per-domain ADRs (0057 FHA documentation-defense; 0060 Right-of-Entry per-jurisdiction rules) handle their slices but don't generalize. The Mission Space Matrix (W#33) identifies this as a **genuine gap** — §5.9 Regulatory/jurisdictional, with recommendation: "New ADR ~0064 — runtime regulatory/jurisdictional policy evaluation." This gap is a **commercial launch-blocker** for any non-US-residential-property tenant (per discovery §6.3).

## Predecessor

**No clean predecessor.** Adjacent: ADR 0057 (FHA documentation-defense; structural pattern reusable); ADR 0060 (Right-of-Entry per-jurisdiction rules; concrete per-jurisdiction citation pattern); paper §20.4 (regulatory factors as architectural filter — *"Regulated data residency requirements (GDPR, HIPAA, FedRAMP, ITAR) | Local-first or on-premises"*); paper §16 (IT governance; mentions regulated-industry posture but does not enumerate). None of these specify runtime jurisdiction probing or cross-cutting policy evaluation.

## Industry prior-art

Per discovery §5.9 (Pedantic-Lawyer-hardened citations):
- **GDPR Articles 22 (automated decision-making), 44 (general principle for transfers), 45 (transfers via adequacy decision), 46 (transfers subject to appropriate safeguards such as SCCs/BCRs)** — primary law for EU data-protection runtime gates
- **HIPAA Privacy Rule (45 CFR §§164.500–164.534) + Security Rule (Subpart C: 45 CFR §§164.302–164.318)** — administrative / physical / technical safeguards triad (§164.308 / §164.310 / §164.312)
- **PCI-DSS v4.0** (PCI Security Standards Council; merchant-tier classifications are card-brand-defined, not PCI-DSS-defined)
- **EU AI Act** (Regulation EU 2024/1689; Arts. 5–6 + Annex III tier classification)

## Scope

- **Runtime jurisdictional probe** — IP-geolocation (unreliable), explicit user declaration, tenant-config (most reliable but stale on travel); composite probe with confidence score
- **Per-jurisdiction policy evaluation rule engine** — given runtime jurisdiction = J and feature = F, is F available? Rule-engine shape consistent with FHA documentation-defense pattern (ADR 0057) and per-jurisdiction explicit citation pattern (ADR 0060)
- **Cross-cutting regulatory regime acknowledgment** — explicitly name which regimes Sunfish targets (HIPAA / GDPR / PCI-DSS / SOC 2 / EU AI Act / FHA) and which it does *not* (e.g., Sunfish open-source-OSS reference implementation does not aspire to FedRAMP without commercial productization)
- **Data-residency enforcement** — when a record's residency requirement conflicts with deployment's current location, runtime behavior: read-only, hide, refuse-to-sync, hard-fail
- **Sanctions handling** — OFAC SDN/sectoral lists + EU consolidated sanctions list applicability (fact-specific; counsel review required)
- **EU AI Act tier-classification placeholder** — Sunfish features that incorporate AI/ML (none yet, but future) would need tier classification

## Dependencies and Constraints

- **Soft dependency**: jurisdiction probe is part of ~ADR 0063 (Mission Space Negotiation Protocol) probe mechanics. Authorable in either order.
- **Hard requirement**: general counsel engagement before specifying enforcement behavior. Recommend dedicated "Pedantic Lawyer" subagent perspective in council review (precedent: Phase 3 hardening pass on Mission Space Matrix §5.9).
- **Effort estimate:** large (~18–24h authoring + extended council review including legal-perspective subagent).
- **Council review posture:** pre-merge canonical + Pedantic-Lawyer perspective added.

## Affected Areas

- foundation: jurisdictional-probe + policy-evaluation contract
- ui-core: regulatory-blocked-feature UX surface
- blocks-property-leasing-pipeline (W#22): consumes for FCRA tenant SSN handling, FHA enforcement
- blocks-public-listings (W#28): consumes for jurisdiction-restricted listings
- accelerators/bridge: data-residency enforcement at relay layer

## Downstream Consumers

- **W#22 Leasing Pipeline** — Phase 6 compliance half (currently deferred per active-workstreams.md row 22)
- **W#28 Public Listings** — jurisdiction-aware rendering
- **W#31 Foundation.Taxonomy** — jurisdictional classification taxonomies
- **Phase 2 commercial MVP** — jurisdiction-aware feature surface

## Authoring resources

ONR should start from the canonical pre-legal research prompt at
`_shared/engineering/pre-legal-research-prompt.md`. Replace `FEATURE / DOCUMENT NAME`
with "ADR 0064 — Runtime Regulatory Policy Evaluation" and `jurisdiction` with
the proposed jurisdictional scope (US Federal as default; expand if scope demands).

---

## Authoring expectations (CO directive 2026-05-06)

ADR 0064 is transferred from the XO author backlog to **ONR** (Office of Naval Research session) per CO directive 2026-05-06. The ADR is drafted (Status: Proposed) but requires ONR-led research-backed legal engagement before it can reach Status: Accepted. ONR's mandate:

### Research-backed legal data

ONR must produce citations to actual statutes / regulations / court cases / agency guidance — not "general principles" or practitioner shorthand. Concrete, jurisdiction-specific text references. Examples of the expected citation depth:

- GDPR Article 22(1) text + EDPB Guidelines 05/2020 on automated decision-making (adopted 4 February 2021) + relevant CJEU rulings
- HIPAA 45 CFR §164.308(a)(1)(ii)(A) exact text (risk analysis requirement) vs 45 CFR §164.312(a)(2)(iv) (encryption/decryption) — distinguish addressable vs required
- OFAC 31 CFR Part 501 (Reporting, Procedures and Penalties) vs SDN list legal basis (IEEPA, TWEA, UNPA) — sanctions screening legal authority chain

The ADR's current citations are Pedantic-Lawyer-flagged as unverified practitioner shorthand (see reader-caution block at top of ADR 0064). ONR must audit every statutory citation in §A0 + the Decision section against current Official Code text.

### Pedantic-Lawyer perspective

Apply the Pedantic-Lawyer adversarial lens both during ONR's §A0 self-audit and during the pre-merge council review (precedent: W#33 §5.9 hardening pass). Specifically:

- Every statute cited must include: jurisdiction, official code section, most recent amendment date, and a note on which edition of the code was consulted.
- Every agency guidance cited must include: issuing body, document identifier, publication date, and current effectiveness status (superseded? effective?).
- Distinguish between "black-letter law," "regulatory guidance," "enforcement posture," and "industry best practice" — these are not equivalent legal authorities.

### General-counsel engagement plan

ONR drafts → cohort council (with Pedantic-Lawyer perspective subagent) → **general counsel as final-mile reviewer before Status: Accepted**. This is non-negotiable per the original intake's reader-caution block and per ADR 0064's internal halt-conditions. The PR description for any Accepted flip must document:

- Name/role of general counsel reviewer
- Date of review
- Scope of review (which jurisdictions, which regimes)
- Any conditions / carve-outs imposed by counsel

If general counsel is not yet engaged, ONR's deliverable is a **research memo** (not a Status: Accepted flip) that equips CO to engage counsel. The research memo becomes the ADR 0064 §"Rule content" foundation.

### Jurisdictional scope

The ADR currently names: HIPAA / GDPR / PCI-DSS v4.0 / SOC 2 / EU AI Act / FHA (in-scope); FedRAMP / ITAR (out-of-scope, commercial-productization-only). ONR must:

1. Propose a concrete jurisdictional scope declaration — which legal jurisdictions does the runtime probe and policy engine actually need to handle at Phase 1?
2. Separate "regulatory regime" (HIPAA, GDPR) from "legal jurisdiction" (US Federal, US state-by-state, EU member states) — these are orthogonal dimensions.
3. Identify which US state-level laws matter at Phase 1 (e.g., CCPA/CPRA California; VCDPA Virginia; CPA Colorado) vs which are deferred.
4. Submit jurisdictional scope proposal to CO for confirmation before drafting rule-content.

### Deliverable shape

ADR 0064 is different from a typical Sunfish substrate ADR. ONR's deliverables may include some or all of:

1. **Regulatory landscape survey** — per-regime summary of current enforcement posture, recent material developments, and Sunfish-specific applicability analysis
2. **Per-jurisdiction policy table** — machine-readable (or table-formatted) mapping of `(jurisdiction × regime × feature-class) → policy verdict + legal authority citation`
3. **Risk register** — ranked list of regulatory risks the Phase 1 substrate must handle vs defer; probability × impact × legal-authority chain
4. **Runtime-policy-evaluation contract surface** — refined type surface for `IPolicyEvaluator`, `PolicyVerdict`, `RegulatoryRegimeStance` that reflects ONR's legal research (may amend current ADR 0064 § "Initial contract surface")
5. **General-counsel briefing package** — compressed deck / memo suitable for handing to outside counsel; scopes what legal review is needed; defines what "sign-off" means per regime

ONR proposes which deliverables are in scope for Phase 1; CO confirms.

---

## Next Steps

ADR 0064 is in Status: Proposed. ONR owns the path to Status: Accepted:

1. ONR reads ADR 0064 in full; audits all statutory citations against current Official Code text
2. ONR proposes jurisdictional scope (see above) → CO confirms
3. ONR produces research memo (or research-backed ADR amendment) covering the deliverables above
4. Pre-merge cohort council with Pedantic-Lawyer perspective subagent
5. General counsel final-mile review
6. CO flips Status: Accepted; amends ADR 0064 frontmatter accordingly

**General counsel engagement required before any concrete enforcement behavior ships (Stage 06 build).** This gate is internal to ADR 0064's halt-conditions; ONR does not need to re-obtain CO permission to pause at this gate.

## Cross-references

- **ADR 0064 (Proposed):** `docs/adrs/0064-runtime-regulatory-policy-evaluation.md`
- Parent discovery: `icm/01_discovery/output/2026-04-30_mission-space-matrix.md` §5.9 + §6.3 + §7
- Active workstream: W#33 in `icm/_state/active-workstreams.md`
- ADRs 0057 + 0060 (concrete domain-specific precedents)
- Mission Space plan: `~/.claude/plans/this-looks-pretty-comprehensive-concurrent-floyd.md`
- Owner memory: `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_adr_0064_regulatory_onr_owned.md`
