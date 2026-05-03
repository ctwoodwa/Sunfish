# Intake — ADR 0009 amendment (5th-concept FeatureManagement consumer of Wayfinder)

**Date:** 2026-05-01
**Requestor:** XO research session (spinoff per ADR 0065 council F4 — substrate vs consumer separation)
**Request:** Amendment to ADR 0009 (`Sunfish.Foundation.FeatureManagement`) extending the pattern with a 5th concept — feature management as a consumer of Wayfinder/Standing Order contract per W#34 §6.1 and ADR 0065 §Decision drivers.
**Pipeline variant:** `sunfish-api-change` (amends an existing ADR; touches public surface)
**Stage:** 00 — pending CO promotion to active

---

## Problem statement

ADR 0009 today defines four concepts for `Sunfish.Foundation.FeatureManagement`: feature flags, features, entitlements, editions. W#34 §6.1 identified a 5th concept needed to complete the configuration model — **operator-issued feature toggles that flow through the Wayfinder system + Standing Order log** rather than through bespoke feature-management storage. ADR 0065 specifies the Wayfinder/Standing Order substrate; this amendment specifies how `Sunfish.Foundation.FeatureManagement` *consumes* it.

## Why a separate workstream (per ADR 0065 council F4)

Substrate authoring (ADR 0065 → W#36) and consumer authoring (ADR 0009 amendment → this workstream) are different scoping units. Conflating them in W#36 was flagged by council as anti-pattern AP14 ("wrong detail distribution"). Per cohort discipline:

- Substrate ADR is contract-only; consumer ADR is API-shape + behavioral + migration plan
- Consumer cannot be authored until substrate is ratified (Status: Accepted)
- Council posture differs (substrate gets WCAG/a11y subagent; consumer gets just standard adversarial unless API surface is UI-bearing)

## Hard prerequisite

**ADR 0065 must reach Status: Accepted on origin/main before this amendment can be authored.** PR #479 is the ADR 0065 acceptance gate.

## Scope

- ADR 0009 amendment block (`## Amendment A1` — first amendment to ADR 0009)
- New concept: "Operator-issued feature toggles" (working name; final TBD)
- API shape: how `Sunfish.Foundation.FeatureManagement` registers Standing Order consumers; how feature-toggle Standing Orders are mapped into the existing FeatureManagement runtime APIs
- Migration: existing `Sunfish.Foundation.FeatureManagement` API surface remains; new consumer surface is additive
- Documentation: `apps/docs/blocks/foundation-featuremanagement.md` cross-link to Wayfinder

## Effort estimate

- ~3-5h XO authoring time (per cohort precedent for amendment authoring)
- Standard adversarial council (no WCAG/a11y subagent required — API-only amendment)
- Pre-merge council canonical

## Council posture

Standard adversarial (4 perspectives: Outside Observer, Pessimistic Risk Assessor, Skeptical Implementer, Devil's Advocate). No WCAG/a11y subagent (no UI surface in the amendment). No Pedantic Lawyer (no regulatory tier).

## Cross-references

- Parent ADR: `docs/adrs/0009-foundation-featuremanagement.md`
- Substrate ADR: `docs/adrs/0065-wayfinder-system-and-standing-order-contract.md` (PR #479)
- W#34 discovery: `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` §6.1
- ADR 0065 council finding: `icm/07_review/output/adr-audits/0065-council-review-2026-05-01.md` §F4

## Next steps

Promote to active workstream when CO confirms; gate authoring on ADR 0065 reaching Status: Accepted.
