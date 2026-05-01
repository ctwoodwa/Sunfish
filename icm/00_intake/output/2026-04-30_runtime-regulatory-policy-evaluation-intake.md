# Intake — Runtime Regulatory / Jurisdictional Policy Evaluation

**Date:** 2026-04-30
**Requestor:** XO research session (synthesis output of W#33 Mission Space Matrix discovery)
**Request:** New ADR ~0064 specifying runtime regulatory/jurisdictional policy evaluation — how a Sunfish deployment determines its current jurisdiction, evaluates per-jurisdiction policy rules at feature-invocation time, and enforces data-residency / sanctions / industry-compliance constraints.
**Pipeline variant:** `sunfish-feature-change` (introduces new cross-cutting policy contract)
**Stage:** 00 — pending CO promotion to active

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

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery. **General counsel engagement required** before Stage 02 Architecture.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-04-30_mission-space-matrix.md` §5.9 + §6.3 + §7
- Active workstream: W#33 in `icm/_state/active-workstreams.md`
- ADRs 0057 + 0060 (concrete domain-specific precedents)
- Mission Space plan: `~/.claude/plans/this-looks-pretty-comprehensive-concurrent-floyd.md`
