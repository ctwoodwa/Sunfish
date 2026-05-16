---
sort_order: 6
number: 7
slug: phase-1-g7-conformance-scan
title: "Phase 1 G7 conformance scan"
status: "blocked"
status_cell: "`blocked` — gated on **W#63 built** (G6 Anchor Recovery UI + RecoveryHostedService); hand-off authored 2026-05-16 at `icm/_state/handoffs/phase-1-g7-conformance-scan-stage07-handoff.md`; immediately runnable once gate clears"
owner: "research"
owner_cell: "research (XO session — runs the scan; produces report)"
reference_cell: "`icm/_state/handoffs/phase-1-g7-conformance-scan-stage07-handoff.md` + `icm/05_implementation-plan/output/business-mvp-phase-1-plan-2026-04-26.md`"
---

## Notes

**Scan deliverable:** `icm/01_discovery/output/g7-conformance-baseline-2026-Q2.md`

Verification pass against G1-G6 Phase 1 acceptance criteria. G1 (`AnchorSyncHostedService`) + G4 (`ManagedRelayPeerDiscovery`) + G5 (`AnchorBackupService`) substrate files all exist on main. G6 backend (`RecoveryCoordinator` + `RotateKeyAsync`) shipped via W#8 (PRs #178 + #185). G6 UI + wiring (`RecoveryHostedService` + 5 Razor pages) ships via W#63. G2 two-node in-process test may not exist yet — scan will document.

**Gate:** W#63 must reach `built` before scan begins.
