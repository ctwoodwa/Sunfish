---
sort_order: 72
number: 63
slug: anchor-recovery-host-integration
title: "G6 — Anchor Recovery Host Integration + Razor UI (ADR 0046 Phase 1 UX; completes MASTER-PLAN G-1 Phase 1)"
status: "built"
status_cell: "`built` — PRs #866 (P1, 2026-05-16) + #867 (P2+3, 2026-05-16); 5 Razor recovery pages + `RecoveryGracePollingService` + `AddAnchorRecoveryHost()` all on main; G6 PARTIAL (see W#65+W#66 for remaining surfaces)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/anchor-recovery-host-integration-stage06-handoff.md`"
---

## Notes

**Completes MASTER-PLAN G-1 Phase 1.** The two remaining Phase 1 items are:
1. G6 host integration + Razor UI (this workstream)
2. G7 conformance baseline scan (gated on G6)

**Foundation.Recovery substrate is fully built** (W#15 + W#32 both `built`). `IRecoveryCoordinator`, `RecoveryCoordinator`, `InMemoryRecoveryStateStore`, `PaperKeyDerivation`, and the full `RecoveryEvent` surface are stable and ready to consume.

**What this ships:**
- Phase 1: DI registration of `IRecoveryCoordinator` in Anchor + 5 Razor pages (`TrusteeSetupPage`, `InitiateRecoveryPage`, `ApproveRecoveryPage`, `RecoveryStatusPage`, `PaperKeyPage`) under `accelerators/anchor/Components/Pages/Recovery/`
- Phase 2: `RecoveryHostedService.cs` — handles `RecoveryCompleted` event → `SqlCipherKeyDerivation.RotateKeyAsync()` + `InMemoryAuditTrail` emission (kernel-audit substrate deferred)
- Phase 3: 4 unit tests + ledger flip

**Unblocks:** G7 conformance baseline scan (the last MASTER-PLAN Phase 1 gate).
