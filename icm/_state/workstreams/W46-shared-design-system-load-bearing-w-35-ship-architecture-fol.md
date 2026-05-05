---
sort_order: 49
number: 46
slug: shared-design-system-load-bearing-w-35-ship-architecture-fol
title: "**Shared Design System** (ADR 0077; `sunfish-feature-change` pipeline) — load-bearing W#35 Ship Architecture follow-on; sequences first per W#35 §9.2"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0077 Accepted 2026-05-05 via PR #543; Stage 06 hand-off authored 2026-05-05; sunfish-PM may begin Phase 1 when COB capacity opens)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/shared-design-system-stage06-handoff.md` + `docs/adrs/0077-shared-design-system.md` (PR #543 merged)"
---

## Notes

**Hand-off ready 2026-05-05.** ADR 0077 Accepted; triple pre-merge council complete. New packages: `foundation-ship-common` (`ShipRole` + `IPermissionResolver` + deck registry) + `foundation-design-tokens` (W3C tokens + codegen + CI contrast gate) + `Sunfish.UICore` extensions (Primitives + FirstAid + Conformance). ~28-38h sunfish-PM / 6 phases / ~8-10 PRs. **Pre-merge council canonical per ADR 0069 D1 for every phase.** WCAG/a11y + security subagents mandatory for Phases 1/3/4. Critical dependency: `IStandingOrderEventStream` not yet built (ADR 0065-A1 spec-only); Phase 1 DefaultPermissionResolver uses 60s TTL cache instead of subscribe-before-load (halt-condition C). **Hard prerequisite for ALL downstream W#35 cohort ADRs** (Quarterdeck / Engine Room / Tactical / Sick Bay / Ship's Office / OOD-Watch).
