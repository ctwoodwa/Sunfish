---
sort_order: 47
number: 44
slug: extensionfields-feature-evaluation-hook-adr-0009-follow-up-5
title: "**ExtensionFields feature-evaluation hook** (`sunfish-api-change` pipeline) — ADR 0009 follow-up #5; `Sunfish.Foundation.Catalog.ExtensionFields` feature-key gating"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0075 Accepted via PR #567; Stage 06 hand-off authored 2026-05-05; sunfish-PM may begin Phase 1 when COB capacity opens)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/extension-fields-feature-gate-stage06-handoff.md` + `docs/adrs/0075-extensionfields-feature-evaluation-hook.md`"
---

## Notes

**ADR 0075 Accepted 2026-05-05 (PR #567). Stage 06 hand-off authored 2026-05-05.** Adds `FeatureGateOffPolicy` enum + `GateState` + `MaterializedExtensionField` + `GetFieldsAsync` overload to `foundation-catalog`; 5 new `AuditEventType` constants in `kernel-audit`; `ExtensionFieldRedactionDeniedException`; `AddExtensionFieldCatalogWithFeatureGating()` DI extension. ~11-15h / 4 PRs. Pre-merge council mandatory (api-change pipeline). W#43 build is NOT a prerequisite for COB to begin W#44 (foundation-catalog does not depend on foundation-wayfinder at build time).
