---
sort_order: 47
number: 44
slug: extensionfields-feature-evaluation-hook-adr-0009-follow-up-5
title: "**ExtensionFields feature-evaluation hook** (`sunfish-api-change` pipeline) — ADR 0009 follow-up #5; `Sunfish.Foundation.Catalog.ExtensionFields` feature-key gating"
status: "built"
status_cell: "`built` (substrate + gating wiring + tests + docs all merged 2026-05-05 → 2026-05-06)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`docs/adrs/0075-extensionfields-feature-evaluation-hook.md` (Accepted) + `icm/_state/handoffs/extension-fields-feature-gate-stage06-handoff.md`"
---

## Notes

**Built.** PRs: P1 #587 (FeatureGateOffPolicy + GateState + MaterializedExtensionField + ExtensionFieldRedactionDeniedException substrate) → P2 #593 (gating wiring + audit factory + DI; ExtensionFieldGateAuditPayloads) → P3 #594 (10 ExtensionFieldGatingTests cases) → P4 #596 (docs + changelog + ledger flip). New types in `packages/foundation-catalog/ExtensionFields/` now in main. Prereqs that landed first: ADR 0075 spec #508 + canonical council #509; ADR 0028 A11 SequestrationFlagKind.FeatureGateOff #512; ADR 0075 council amendments + Accepted #567.
