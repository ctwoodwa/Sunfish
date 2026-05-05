---
sort_order: 50
number: 48
slug: atlas-integration-config-ui-surface
title: "**Atlas Integration-Config UI Surface** (ADR 0067; W#34 follow-on; `sunfish-feature-change` pipeline)"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0067 Accepted 2026-05-05 via PR #539; Stage 06 hand-off at `icm/_state/handoffs/atlas-integration-config-stage06-handoff.md`; sunfish-PM may begin Phase 1 when W#53 Phase 1 lands)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/atlas-integration-config-stage06-handoff.md` + `docs/adrs/0067-atlas-integration-config-surface.md` (PR #539 merged)"
---

## Notes

**Hard prerequisites:** ADR 0065 W#42 built ✓; **ADR 0066 Stage 06 Phase 1 (W#53) — `IAtlasProvider<T>` not yet on origin/main; W#48 Phase 1 BLOCKED until W#53 Phase 1 lands.** Triple council + re-council mechanical amendments applied 2026-05-05. Key new types: `IIntegrationAtlasProvider` + `IIntegrationAtlasContext` + `IntegrationProviderSchema` + `IIntegrationSchemaProvider` + `IIntegrationProviderValidator` + `IValidationStatusStore` + `IDecryptCapabilityProvider` + `IntegrationCapabilityPurposes`. No new package (additive to `packages/ui-core/Wayfinder/Integrations/`). 5 build phases: P1 contract surface (gated on W#53 P1); P2 reference impl + audit + SUNFISH_INTEGRATION_AUDIT001 analyzer; P3a/3b adapter schema providers + validators; P4 Anchor+Bridge rendering; P5 ledger flip + docs. ~23-35h / ~6-8 PRs.
