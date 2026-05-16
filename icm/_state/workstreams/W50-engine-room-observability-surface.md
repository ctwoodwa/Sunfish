---
sort_order: 52
number: 50
slug: engine-room-observability-surface
title: "**Engine Room Observability Surface** (ADR 0079; W#35 Ship Architecture follow-on; `sunfish-feature-change` pipeline)"
status: "built"
status_cell: "`built` — all 4 phases shipped 2026-05-13 (PRs #626+#696+#790+#797+#800+#802); MainPropulsion + DamageControl + QaWorkshop Blazor panels + Anchor wiring + Bridge wiring + docs; pipeline closed"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`docs/adrs/0079-engine-room-observability.md` (PR #572 merged) + `icm/_state/handoffs/engine-room-observability-stage06-handoff.md` + `packages/foundation-engine-room/` (P1 merged)"
---

## Notes

**Phase 1 merged 2026-05-06 via PR #626.** New `Sunfish.Foundation.EngineRoom`
package shipped: 12 data-model types (SyncDaemon* / CrdtGrowth* /
Subsystem* / EngineRoomHealthSummary with `For()` helper / Quarantine /
Release / CompactionResult / EngineRoomUnauthorizedException) + 2
contract interfaces (IEngineRoomDataProvider 5 methods, IEngineRoomCommandService
3 methods); EngineRoomMetrics OTel constant catalog (`sunfish.engine_room.*`);
`AddSunfishEngineRoom()` DI extension. Throws on §Trust-elevated denial
(NOT cohort PublishOutcome — different posture for write-class destructive
actions).

**Pre-merge council** (security-engineering subagent; Opus + xhigh) returned
MECHANICAL-AMENDMENTS-ONLY: 1 Major (Damage Control actions missing from
ResourceScopedActions — substrate-tier null-resource bypass closed) + 4
Minor (audit-emission ordering XML; DamageControlAuthorizationDenied
dual-emission with PermissionDenied; QuarantineDocument MainDeck
rationale; EngineRoomOptions package-of-origin clarification). All
applied pre-merge. **Cohort batting average: 30-of-34** substrate
amendments needed council fixes.

**Constants added**: 8 new `AuditEventType` (DocumentQuarantineRequested/
Quarantined/ReleaseRequested/Released, ManualCompactionInitiated/Completed,
EngineRoomHealthDegraded, DamageControlAuthorizationDenied) + 5 new
`ShipAction` (kebab-case per cohort: view-engine-room / view-damage-control
/ quarantine-document / release-quarantine / compact-document).

**Resolver cohort extension**: `ActionMinimumDeck` extended (5 entries;
QuarantineDocument MainDeck per §4 reversibility argument);
`MapToCapabilityAction` extended (5 entries); `ResourceScopedActions`
extended (3 entries — Major M1 fix; ViewEngineRoom + ViewDamageControl
stay location-scoped).

**Tests**: 18/18 in foundation-engine-room; 26/26 in foundation-ship-common
(was 25; +1 for Major M1 regression `ResourceScopeGuard_NullResourceForQuarantineDocument_Denied`).

**Phase 1 Phase-2 follow-up TODOs**:
- Concrete `IEngineRoomDataProvider` + `IEngineRoomCommandService` in
  `blocks-engine-room` (Phase 2)
- OTel meter registration + per-instrument observable callbacks (Phase 2)
- `EngineRoomOptions` (HeartbeatInterval default 30s) (Phase 2)
- Role-minimum enforcement (department-head / EngineerOfficer) — gated on
  W#37 / `ITenantSecurityPolicy`

**Remaining phases** (per hand-off):
- **Phase 2** (~4-5h): reference impl in `blocks-engine-room` + OTel +
  permission gating; pre-merge security-engineering subagent mandatory.
- **Phase 3a** (~3-4h): `blocks-engine-room` read-only panels (Main
  Propulsion / QA Workshop); pre-merge WCAG/a11y subagent mandatory.
- **Phase 3b** (~3-4h): Damage Control panel + QA Workshop panel; pre-
  merge WCAG/a11y + security-engineering subagents mandatory.
- **Phase 4** (~2-3h): Anchor wiring + apps/docs + ledger flip.

**W#35 Ship Architecture cohort substrate progress (4/7 on origin/main)**:
W#46 P1 ✓ + W#49 all ✓ + W#55 P1 ✓ + W#50 P1 ✓ (this PR). Remaining:
W#51 (Quarterdeck — gated on W#46 Phase 3) + W#52 (Tactical — gated on
W#46 Phase 3) + W#54 (Sick Bay — H1 cleared, H2 gated on W#53 P1, H3 on
ADR 0068 Accepted).
