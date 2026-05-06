---
sort_order: 50
number: 48
slug: atlas-integration-config-ui-surface
title: "**Atlas Integration-Config UI Surface** (ADR 0067; W#34 follow-on; `sunfish-feature-change` pipeline)"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0067 Accepted 2026-05-05 via PR #539; Stage 06 hand-off at `icm/_state/handoffs/atlas-integration-config-stage06-handoff.md`; **W#53 Phase 1a merged 2026-05-06 via PR #630 — `IAtlasProvider<T>` now on origin/main; sunfish-PM may begin Phase 1 immediately**)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/atlas-integration-config-stage06-handoff.md` + `docs/adrs/0067-atlas-integration-config-surface.md` (PR #539 merged) + `packages/ui-core/Wayfinder/IAtlasProvider.cs` (W#53 P1a — `IAtlasProvider<T>` substrate landed)"
---

## Notes

**Hard prerequisites:** ADR 0065 W#42 built ✓; ADR 0066 Stage 06 Phase 1a
**now landed** (W#53 P1a / PR #630 merged 2026-05-06) — `IAtlasProvider<T>`
+ Helm widget contract surface on origin/main. **W#48 Phase 1 is no
longer blocked.** Triple council + re-council mechanical amendments
applied 2026-05-05.

**`IAtlasProvider<TView>` is invariant** (W#53 P1a council resolution —
hand-off cited `out TView` but C# CS1961 rejects on `Task<T>` returns).
Concrete W#48 `IIntegrationAtlasProvider` derives directly from
`IAtlasProvider<IntegrationAtlasView>` without covariant downcast.

Key new types: `IIntegrationAtlasProvider` + `IIntegrationAtlasContext`
+ `IntegrationProviderSchema` + `IIntegrationSchemaProvider` +
`IIntegrationProviderValidator` + `IValidationStatusStore` +
`IDecryptCapabilityProvider` + `IntegrationCapabilityPurposes`. No new
package (additive to `packages/ui-core/Wayfinder/Integrations/`).

5 build phases: P1 contract surface (gated on W#53 P1 — **CLEARED**);
P2 reference impl + audit + SUNFISH_INTEGRATION_AUDIT001 analyzer;
P3a/3b adapter schema providers + validators; P4 Anchor+Bridge
rendering; P5 ledger flip + docs. ~23-35h / ~6-8 PRs.
