# Intake — ADR 0009 Amendment: Extend FeatureManagement to Tenant-Config Policy (5th Concept)

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#34 Wayfinder configuration UX discovery)
**Request:** Amend ADR 0009 (Foundation.FeatureManagement) with a new amendment specifying *tenant-config policy* as a fifth concept alongside the existing four (technical flags / product features / entitlements / editions). Specify Standing Order shape for tenant-config changes; clarify the boundary between FeatureManagement (which Sunfish owns) and Wayfinder (which composes on it).
**Pipeline variant:** `sunfish-api-change` (extends an existing public contract; small additive change)
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

The Wayfinder discovery (W#34) §5.2 identifies Layer 2 (tenant configuration) as **Partial coverage**: ADR 0009 (FeatureManagement) covers per-tenant flags / entitlements / editions but explicitly distinguishes those from *operational policy* (e.g., "all signatures require notarization in CA jurisdiction"; multi-actor permissions matrix; tenant-locale-specific defaults). The fourth-concept separation in ADR 0009 lines 13–18 doesn't accommodate tenant policy as a first-class concept; the discovery recommends extending it to a fifth concept.

## Predecessor

**Clean amendment slot:** ADR 0009 — Foundation.FeatureManagement.

**Why amendment, not new ADR**: tenant-config policy belongs in the same conceptual space as flags / features / entitlements / editions — a *fifth concept* alongside the existing four, not a separate system. ADR 0009's bundle-manifest-as-authoring-source pattern (lines 71–72) extends naturally.

## Scope

- **Fifth-concept addition**: extend ADR 0009's four-concept separation:
  - Technical flags — runtime booleans (existing)
  - Product features — named capabilities (existing)
  - Entitlements — what a tenant is allowed to use (existing)
  - Editions / tiers — named product configurations (existing)
  - **Tenant-config policy** *(new)* — operational rules a tenant administrator sets (locale defaults; jurisdiction; permissions matrix; branding; default values for domain entities)
- **`ITenantPolicyResolver` contract** — analog to `IEntitlementResolver`; reads policy from Standing Orders; composes into `IFeatureEvaluator`'s resolution order
- **Standing Order shape for tenant-config changes** — composes ~ADR 0065 contract; tenant-policy changes emit elevated audit events per ADR 0049
- **Bundle manifest extension** — ADR 0007 bundle-manifest schema gains `tenantPolicyDefaults` alongside existing `featureDefaults` + `editionMappings`
- **Resolution order extension** — `IFeatureEvaluator` resolution becomes: catalog → provider → entitlements → **policy** → default
- **Clarify the Wayfinder boundary** — Atlas surface for tenant-config policy is Wayfinder's responsibility (~ADR 0065); FeatureManagement provides the data model + resolution

## Industry prior-art

- Spring Boot profiles (`@Profile` per tenant / environment) — closest analog to tenant-policy-as-fifth-concept
- Microsoft Entra ID Conditional Access — tenant policy + entitlement composition
- Open Policy Agent (OPA) — policy as data, evaluable at runtime; could compose under `ITenantPolicyResolver`

## Dependencies and Constraints

- **Soft prerequisite**: ~ADR 0065 (Wayfinder + Standing Order contract) — Standing Order shape for tenant-config changes references ~0065's data model. This amendment can be drafted before ~0065 lands but the Standing Order section is provisional until ~0065 is final.
- **Effort estimate:** medium (~8–12h authoring + council review)
- **Council review posture:** standard adversarial + **WCAG / a11y subagent** (Bridge admin feature-flag UX inherits Atlas a11y baseline per W#34 Stage 1.5 hardening output)

## Affected Areas

- foundation-featuremanagement: extend with `ITenantPolicyResolver`
- foundation (ADR 0007 bundle manifest): extend schema with `tenantPolicyDefaults`
- ui-core: tenant-policy Atlas surface contract (defers to ~ADR 0065 Wayfinder system)
- accelerators/bridge: Bridge admin tenant-policy editing surface

## Downstream Consumers

- All Wayfinder follow-on ADRs (~0065 / ~0066 / ~0067 / ~0068) reference tenant-policy as the fifth concept
- W#22 Leasing Pipeline — tenant-policy for FHA enforcement (signature notarization rules per CA jurisdiction)
- W#28 Public Listings — tenant-policy for jurisdiction-aware listing display rules
- W#31 Foundation.Taxonomy — taxonomy authority/governance regimes are tenant-policy
- Phase 2 commercial MVP — multi-actor permissions matrix is tenant-policy

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery. Recommend **second-of-five** authoring sequence per W#34 §7.2 (small scope; resolves Layer 2 partial coverage; unblocks tenant-policy Standing Order shape for downstream Wayfinder ADRs).

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` §5.2 + §6.5 + §7
- Active workstream: W#34 in `icm/_state/active-workstreams.md`
- Sibling intake: `icm/00_intake/output/2026-05-01_wayfinder-system-and-standing-order-intake.md` (~ADR 0065; soft prerequisite)
- ADR 0009 — Foundation.FeatureManagement (the amended ADR)
- ADR 0007 — Bundle manifest schema (extension target)
