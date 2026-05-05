---
sort_order: 34
number: 36
slug: wayfinder-system-standing-order-contract-promoted-from-w-34
title: "**Wayfinder System + Standing Order Contract** (`sunfish-feature-change` pipeline) — promoted from W#34 follow-on"
status: "built"
status_cell: "`built` (ADR 0065 authored as **Proposed** 2026-05-05 — CO acceptance flip pending; W#42 substrate shipped 2026-05-04 across PRs #503/#504/#505/#510/#513/#514)"
owner: "research"
owner_cell: "research (XO) ✓"
reference_cell: "`docs/adrs/0065-wayfinder-system-and-standing-order-contract.md` (Proposed — CO acceptance flip pending) + W#42 per-workstream file (substrate built)"
---

## Notes

**XO ADR authoring complete.** ADR 0065 authored and on `origin/main` as `status: Proposed`
(CO acceptance flip pending). W#42 substrate build shipped 2026-05-04 across 6 PRs
(#503/#504/#505/#510/#513/#514): `foundation-wayfinder` + `foundation-wayfinder-analyzers`
packages; `CrdtStandingOrderRepository` + `DefaultStandingOrderIssuer` + `IAtlasProjector` +
`SchemaRegistrationAnalyzer` (SUNFISH_WAYFINDER001). All W#35 Ship Architecture downstream ADRs
(W#46/W#49/W#50/W#51/W#52/W#53/W#54/W#55) depend on ADR 0065's `IStandingOrderIssuer` surface.
ADR 0065 acceptance flip unblocks IStandingOrderEventStream wiring across all cohort workstreams.
