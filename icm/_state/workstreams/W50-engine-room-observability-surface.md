---
sort_order: 52
number: 50
slug: engine-room-observability-surface
title: "**Engine Room Observability Surface** (ADR 0079; W#35 Ship Architecture follow-on; `sunfish-feature-change` pipeline)"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0079 Accepted 2026-05-05 via PR #572; Stage 06 hand-off at `icm/_state/handoffs/engine-room-observability-stage06-handoff.md`; sunfish-PM may begin Phase 1 when capacity opens)"
owner: "research"
owner_cell: "research (XO) ✓"
reference_cell: "`docs/adrs/0079-engine-room-observability.md` (PR #572 merged)"
---

## Notes

Hard prerequisites: ADR 0077 W#46 built (W#46 `ready-to-build`); ADR 0078 W#49 Stage 06 built. Key types: `IEngineRoomDataProvider` + `IEngineRoomCommandService`; `SyncDaemonHealth` + `CrdtGrowthMetrics` + `EngineRoomHealthSummary` (list-based); 8 new `AuditEventType` constants (two-phase quarantine + compaction + health-degraded + auth-denied); 5 new `ShipAction` constants; OTel metric catalog (`sunfish.engine_room.*`). Two new packages: `foundation-engine-room` + `blocks-engine-room`. 4-phase build: ~14-18h / ~5 PRs. Pre-merge council canonical (WCAG/a11y + security subagents mandatory for Phases 2/3b/4 per §6.1 + §Trust).
