---
sort_order: 51
number: 49
slug: ood-watch-rotation
title: "**OOD Watch Rotation** (ADR 0078; W#35 Ship Architecture follow-on; `sunfish-feature-change` pipeline)"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0078 Accepted 2026-05-05 via PR #571; Stage 06 hand-off at `icm/_state/handoffs/ood-watch-rotation-stage06-handoff.md`; sunfish-PM may begin Phase 1 when capacity opens)"
owner: "research"
owner_cell: "research (XO) ✓"
reference_cell: "`docs/adrs/0078-ood-watch-rotation.md` (PR #571 merged) + `icm/_state/handoffs/ood-watch-rotation-stage06-handoff.md`"
---

## Notes

Hard prerequisites: ADR 0065 W#42 built ✓; ADR 0049 built ✓; ADR 0077 Accepted ✓. Key types: `OodWatch` record + `OodWatchId` / `OodRole` / `OodWatchState`; `IOodWatchRepository` (+ `GetExpiredCandidatesAsync`); `IOodWatchService` (`StartWatchAsync` / `HandoverWatchAsync` / `GetActiveWatchAsync`); 3 new `AuditEventType` constants; additive `StandingOrder.IssuedDuringWatchId` extension. **Binary-compat halt:** `StandingOrder.IssuedDuringWatchId` MUST be added inside W#42 Phase 1 PR before any NuGet binary ships. W#49 is a prerequisite for W#50 (Engine Room) Damage Control EOOW-check wiring. 3-phase build: ~6-9h / ~3 PRs. Pre-merge council canonical (WCAG/a11y + security subagents mandatory per §7 + §Trust).
