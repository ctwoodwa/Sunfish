---
sort_order: 55
number: 53
slug: helm-identity-atlas-surface-stage-06-build-of-load-bearing-p
title: "**Helm + Identity Atlas Surface** (ADR 0066; W#34 follow-on; `sunfish-feature-change` pipeline) — Stage 06 build of `IHelmWidget` + `IHelmWidgetRegistry` + `IAtlasProvider<T>` + `IIdentityAtlasSurface`; **load-bearing prerequisite for W#48 Phase 1**"
status: "building"
status_cell: "`building` (Phase 1a merged 2026-05-06 via PR #630 — `IAtlasProvider<T>` on origin/main + Helm widget contract surface; Phase 1b pending — KeyFingerprint + IIdentityAtlasSurface)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/helm-identity-atlas-stage06-handoff.md` + `docs/adrs/0066-helm-composition-and-identity-atlas-surface.md` (PR #529 merged) + `packages/ui-core/Wayfinder/` (P1a merged)"
---

## Notes

**Phase 1a merged 2026-05-06 via PR #630.** New `Sunfish.UICore.Wayfinder`
namespace shipped: `IAtlasProvider<TView>` (invariant — hand-off cited
`out TView` but C# compiler rejects on `Task<T>` return type per CS1961;
concrete W#48 `IIntegrationAtlasProvider` derives directly without
covariant downcast); `IHelmWidget` interface + 5 records
(HelmWidgetMetadata / HelmWidgetViewState / HelmWidgetAction /
HelmRenderContext) + 2 `[JsonStringEnumConverter]` enums (HelmSlot /
HelmActionInvocationKind) + `HelmOptions` (PeriodicRefreshInterval
default 1m); `IHelmWidgetRegistry` + `internal sealed
DefaultHelmWidgetRegistry` (Slot then OrderHint sort; LINQ stable);
`AddSunfishHelm()` + `AddHelmWidget<TWidget>()` DI extensions.

**Two hand-off divergences (council-validated)**:
1. `out TView` → invariant `TView` (C# CS1961 — Task<T> not covariant).
2. `TeamId? → Guid?` for HelmRenderContext.ActiveTeamId — kernel-runtime
   already references ui-core (line 23 of its csproj); reverse dep
   would form cycle. Consumers wrap Guid back into TeamId at the
   kernel-runtime boundary.

DateTimeOffset over NodaTime (cohort precedent W#46/W#49/W#50/W#54/W#55).

**Pre-merge council** (standard 4-perspective adversarial; Opus + xhigh)
returned **READY-TO-MERGE** with no findings. Both hand-off divergences
validated (CS1961 reproduced; cycle confirmed at kernel-runtime:23).
§A0 three-direction structural-citation audit clean. **Cohort batting
average: 30-of-36** (W#53 P1a was the cleanest substrate — 0 Major,
0 Minor; only PR description amendments noted).

**Tests**: 11 new `HelmWidgetRegistryTests` (slot+orderHint sort +
tie-stability + GetSlot filter + DI defaults + AddHelmWidget + null
guards + enum cardinalities + IAtlasProvider class-constraint
reflection). 47/47 ui-core tests pass overall.

**Phase 1a-immediate unblock**: W#48 P1 (Atlas Integration-Config UI
Surface) was gated on `IAtlasProvider<T>` landing on origin/main. With
PR #630 merged, **W#48 Phase 1 is now ready for COB pickup**.

**Phase 1b remaining** (~3-4h, 1 PR): `KeyFingerprint` (additive to
`packages/foundation-recovery/`) + `IIdentityAtlasSurface` + view-model
records + `RecoveryContact`. Cleared halts: H1+H2 from hand-off; the
new ui-core dependencies are landed.

**Phase 2 remaining** (~12-19h, 3-4 PRs; gated on Phase 1 merged):
6 canonical Helm widgets + Blazor/React adapter renderers + WCAG tests.
Pre-merge WCAG/a11y subagent mandatory.

**Phase 3 deferred** to W#54 (identity Atlas implementations).
