# Starter Taxonomy Charter v1.0 — `Sunfish.Leasing.JurisdictionRules@1.0.0`

**Status:** Draft (sunfish-PM authored as part of W#22 Phase 4; awaiting CO sign-off)
**Date:** 2026-04-30
**Composes:** [ADR 0056 — Foundation.Taxonomy substrate](../../../docs/adrs/0056-foundation-taxonomy-substrate.md), [ADR 0057 — Leasing pipeline](../../../docs/adrs/0057-leasing-pipeline-fair-housing.md), [ADR 0060 — Right-of-entry compliance](../../../docs/adrs/0060-right-of-entry-compliance.md)
**Author:** sunfish-PM session (W#22 Phase 4 hand-off)
**ICM stage:** 00_intake (taxonomy product charter)
**Pipeline variant:** sunfish-feature-change

---

## Purpose

`Sunfish.Leasing.JurisdictionRules@1.0.0` enumerates the fair-housing + tenant-protection + consumer-financial-protection rules that the leasing pipeline + showing-compliance code paths must observe. Each node represents a jurisdiction-scoped rule family that downstream consumers (e.g., `IApplicationDecisioner`, `IJurisdictionPolicyResolver` from ADR 0060, the FCRA workflow from W#22 Phase 3) reference via `TaxonomyClassification`.

Authoritative regime — Sunfish ships the canonical seed; civilians may **clone** to derive their own variant and **extend** with locally-scoped child nodes (e.g., a city ordinance), but cannot **alter** the Sunfish-shipped node set.

The rules captured here are **deliberately abstract** — they identify *what* compliance applies, not *how* to apply it. The `IJurisdictionPolicyResolver` interface (ADR 0060) consumes this taxonomy + maps each node to executable policy at the consumption site.

## Identity

- **System:** `Sunfish.Leasing.JurisdictionRules@1.0.0`
- **Owner:** Sunfish (authoritative)
- **Governance regime:** Authoritative

## Root nodes (parent: null)

| code | display | description |
|---|---|---|
| `us-fed.fha` | US Fair Housing Act | Title VIII of the Civil Rights Act; bans discrimination in housing on the basis of seven protected classes. |
| `us-fed.fcra` | US Fair Credit Reporting Act | Federal regulation governing consumer-report use in housing decisions. Drives the adverse-action notice + dispute-window workflow. |
| `us-fed.fha-source-of-income` | US Source-of-Income Rules | HUD interpretive guidance + 14+ state-level extensions covering Section 8 voucher / housing-assistance discrimination prohibitions. |
| `us-state.ca.unruh` | California Unruh Civil Rights Act | California's broad civil-rights law extending FHA-style protections to additional protected classes. |
| `us-state.ca.fehc` | California Fair Employment & Housing Council Regs | California regulations operationalizing FHA + Unruh; specifies prohibited questions + tenant-screening criteria. |
| `us-state.ny.tpa` | New York Tenant Protection Act | NY 2019 rent-regulation + tenant-screening reforms (e.g., 30-day notice rules, application-fee caps). |
| `us-state.ny.adverse-action-extended-window` | NY Extended Adverse-Action Window | NY-state extension of the FCRA dispute window (90 days for source-of-income decisions). |

## Children — `us-fed.fha` protected-class enumeration

| code | parent | display | description |
|---|---|---|---|
| `us-fed.fha.race` | `us-fed.fha` | Race | Protected under FHA §3604(a–f). |
| `us-fed.fha.color` | `us-fed.fha` | Color | Protected under FHA §3604(a–f). |
| `us-fed.fha.religion` | `us-fed.fha` | Religion | Protected under FHA §3604(a–f). |
| `us-fed.fha.sex` | `us-fed.fha` | Sex | Protected under FHA §3604(a–f); HUD interprets to include gender identity + sexual orientation since 2021. |
| `us-fed.fha.familial-status` | `us-fed.fha` | Familial Status | Children-in-household protection per FHA §3602(k). |
| `us-fed.fha.national-origin` | `us-fed.fha` | National Origin | Protected under FHA §3604(a–f). |
| `us-fed.fha.disability` | `us-fed.fha` | Disability | Protected under FHA §3604(f); reasonable-accommodation duty applies. |

## Children — `us-fed.fcra` workflow rules

| code | parent | display | description |
|---|---|---|---|
| `us-fed.fcra.adverse-action-notice` | `us-fed.fcra` | Adverse-Action Notice Requirement | §615(a) mandatory notice when decision is based on a consumer report. Sunfish ships the `MandatoryFcraStatement` (W#22 Phase 3). |
| `us-fed.fcra.dispute-window-60d` | `us-fed.fcra` | 60-Day Dispute Window | §612(a) right to free report + dispute within 60 days. Default `FcraAdverseActionNoticeGenerator.DefaultDisputeWindow`. |
| `us-fed.fcra.consent-required` | `us-fed.fcra` | Background-Check Consent Requirement | §604(b) requires written consent for consumer-report procurement; satisfied by `consent-background-check` signature scope. |
| `us-fed.fcra.permissible-purpose` | `us-fed.fcra` | Permissible-Purpose Requirement | §604(a) limits consumer-report use to enumerated purposes (tenant-screening qualifies). |

## Children — `us-state.ca.unruh` additional protected classes

These extend FHA's seven; cited verbatim in CA Civil Code §51.

| code | parent | display | description |
|---|---|---|---|
| `us-state.ca.unruh.sexual-orientation` | `us-state.ca.unruh` | Sexual Orientation | CA Civil Code §51(b). |
| `us-state.ca.unruh.gender-identity` | `us-state.ca.unruh` | Gender Identity | CA Civil Code §51(e). |
| `us-state.ca.unruh.marital-status` | `us-state.ca.unruh` | Marital Status | CA Civil Code §51(b). |
| `us-state.ca.unruh.ancestry` | `us-state.ca.unruh` | Ancestry | CA Civil Code §51(b). |
| `us-state.ca.unruh.medical-condition` | `us-state.ca.unruh` | Medical Condition | CA Civil Code §51(b); incl. genetic information. |

## Children — `us-state.ca.fehc` prohibited-question list (operational rules)

| code | parent | display | description |
|---|---|---|---|
| `us-state.ca.fehc.prohibited-question-list` | `us-state.ca.fehc` | Prohibited-Question List | Cal. Code Regs. §12181 — questions an operator may not ask during application/screening. |
| `us-state.ca.fehc.application-fee-cap` | `us-state.ca.fehc` | Application Fee Cap | CA Civil Code §1950.6 — fee cap; refund duty. |

## Children — `us-state.ny.tpa` operational rules

| code | parent | display | description |
|---|---|---|---|
| `us-state.ny.tpa.application-fee-cap-20usd` | `us-state.ny.tpa` | $20 Application Fee Cap | RPL §238-a — application fees capped at $20. |
| `us-state.ny.tpa.security-deposit-cap-1mo` | `us-state.ny.tpa` | 1-Month Security Deposit Cap | RPL §7-108 — security deposit + last month's rent capped at one month's rent. |
| `us-state.ny.tpa.tenant-blacklist-prohibition` | `us-state.ny.tpa` | Tenant Blacklist Prohibition | RPL §227-f — prohibits use of tenant-blacklist databases (Housing Court records). |

## Children — `us-fed.fha-source-of-income`

| code | parent | display | description |
|---|---|---|---|
| `us-fed.fha-source-of-income.section-8-voucher` | `us-fed.fha-source-of-income` | Section 8 Voucher Acceptance | HUD interpretive guidance + state-level mandates that Section 8 vouchers must be accepted on equal terms. |
| `us-fed.fha-source-of-income.housing-assistance` | `us-fed.fha-source-of-income` | Other Housing Assistance | Disability/veterans' housing programs covered by similar state-level rules. |

## Out of v1.0 (deferred to v1.1 minor or v2.0 major)

- **Per-city local ordinances** (e.g., Seattle FIT Act, Berkeley source-of-income, NYC Local Law 71). v1.0 stops at state-level granularity; cities may extend their own civilian-regime taxonomy.
- **Non-US jurisdictions** — Sunfish v1.0 is US-only by scope; international expansion deferred to v2.0.
- **Sub-state county-level rules** (e.g., Cook County, Westchester) — same rationale as cities.
- **Section 8 administrative-plan specifics** — vary per local Public Housing Authority; deferred to civilian-regime extensions.

## Why these nodes specifically

The W#22 leasing pipeline hand-off + ADR 0057 explicitly call out:
- **FHA seven protected classes** as the FHA-defense layout's anchor — quarantining `DemographicProfile` from decisioning is THE structural defense the FHA codifies.
- **FCRA §615 + §612 + §604** as the workflow drivers — the `FcraAdverseActionNoticeGenerator` (W#22 Phase 3) is the policy materialization of these nodes.
- **California Unruh + FEHC** because CA is the first MVP state (BDFL's property business per `project_phase_2_commercial_scope`).
- **NY TPA** because the second commercial expansion is NY-coast (per same project memory).
- **Source-of-income rules** because they're a 14+ state-level patchwork that downstream consumers need a stable reference for.

The node set is **deliberately small** — additional jurisdictions land via minor version bumps (1.1, 1.2, …) as MVP expands. Each downstream `IJurisdictionPolicyResolver` implementation maps the nodes it cares about to policy code; missing nodes don't break the resolver, they just produce "no rule applies."

## Versioning rules + deprecation

Per the v1.0 Charters (`starter-taxonomies-v1-charters-2026-04-29.md`):
- MAJOR: any node `code` removed or renamed
- MINOR: new nodes added (additive)
- PATCH: display revisions, parent reorganization within same `code` set, tombstones with successor mappings

Tombstoning is the deprecation marker; removing a tombstoned node requires major bump.

## Cross-references

- ADR 0057 (Leasing pipeline) — drives FHA-defense layout + FCRA workflow
- ADR 0060 (Right-of-entry compliance) — drives `IJurisdictionPolicyResolver` consumer
- ADR 0056 (Foundation.Taxonomy substrate) — the shape this charter slots into
- W#22 Phase 3 PR #328 (FCRA workflow) — `MandatoryFcraStatement` is the policy materialization of `us-fed.fcra.adverse-action-notice`
- `Sunfish.Signature.Scopes@1.0.0` charter — `consent-background-check` + `consent-credit-check` nodes complement `us-fed.fcra.consent-required`
