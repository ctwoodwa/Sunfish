---
sort_order: 14
number: 15
slug: foundation-recovery-package-split
title: "Foundation.Recovery package split (ADR 0046 + 0049 reconciliation)"
status: "built"
status_cell: "`built` (split shipped)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "https://github.com/ctwoodwa/Sunfish/pull/202 (Phase 1 inventory) + https://github.com/ctwoodwa/Sunfish/pull/223 (Phases 2–6)"
---

## Notes

**Split shipped 2026-04-29 (PR #223).** All 20 source types in `packages/kernel-security/Recovery/` moved to new `packages/foundation-recovery/`; namespace `Sunfish.Kernel.Security.Recovery` → `Sunfish.Foundation.Recovery`; BIP-39 wordlist LogicalName updated; `AddSunfishRecoveryCoordinator` DI extension extracted to foundation-recovery (avoids circular dep with kernel-security; not explicitly named in inventory but caught by Phase 3 "watch for circular dep"). 1 stale XML doc reference patched in foundation-localfirst. ADR 0046 amended with new "Package placement (added 2026-04-29)" section; ADR 0049 implementation-checklist patched; roadmap row → `scaffolded`. 51/51 foundation-recovery tests + 50/50 kernel-security tests passing (no regressions). Resolves audit finding C-2 in `CONSOLIDATED-HUMAN-REVIEW.md`. Unblocks G6 host-integration `RecoveryCompleted → SqlCipher rekey + persist to kernel-audit` (Phase 1 G6 not yet started per `project_business_mvp_phase_1_progress`). UPF-graded A; api-change pipeline.
