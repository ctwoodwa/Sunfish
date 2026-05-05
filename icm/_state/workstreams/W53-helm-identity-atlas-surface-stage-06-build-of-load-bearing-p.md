---
sort_order: 55
number: 53
slug: helm-identity-atlas-surface-stage-06-build-of-load-bearing-p
title: "**Helm + Identity Atlas Surface** (ADR 0066; W#34 follow-on; `sunfish-feature-change` pipeline) — Stage 06 build of `IHelmWidget` + `IHelmWidgetRegistry` + `IAtlasProvider<T>` + `IIdentityAtlasSurface`; **load-bearing prerequisite for W#48 Phase 1**"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0066 Accepted 2026-05-05 via PR #529; Stage 06 hand-off at `icm/_state/handoffs/helm-identity-atlas-stage06-handoff.md`; sunfish-PM may begin Phase 1 immediately — prerequisites H1+H2 verified on origin/main)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/helm-identity-atlas-stage06-handoff.md` + `docs/adrs/0066-helm-composition-and-identity-atlas-surface.md` (PR #529 merged)"
---

## Notes

**Hand-off authored 2026-05-05. ~18-28h / ~5-6 PRs.** 2 build phases (Phase 3 identity Atlas implementations deferred to W#54). Phase 1: `IAtlasProvider<T>` + `IHelmWidget` + `IHelmWidgetRegistry` + `DefaultHelmWidgetRegistry` + `HelmServiceCollectionExtensions` (two-overload `AddSunfishHelm` + `AddHelmWidget<T>`) + `KeyFingerprint` (additive to `packages/foundation-recovery/`) + `IIdentityAtlasSurface` + view-model records + `RecoveryContact`. Phase 2: 6 canonical widgets + Blazor/React adapter renderers + WCAG tests. **W#48 Phase 1 unblocks when W#53 Phase 1 merges** (`IAtlasProvider<T>` on origin/main). Halt H7 (HistoricalKeysProjection absent → placeholder approach); H8 (IObservable<StandingOrderAppliedEvent> absent → periodic fallback). OQ-1/2/3/4/5/6 all resolved in hand-off.
