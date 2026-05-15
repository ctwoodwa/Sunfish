---
sort_order: 55
number: 53
slug: helm-identity-atlas-surface-stage-06-build-of-load-bearing-p
title: "**Helm + Identity Atlas Surface** (ADR 0066; W#34 follow-on; `sunfish-feature-change` pipeline) — Stage 06 build of `IHelmWidget` + `IHelmWidgetRegistry` + `IAtlasProvider<T>` + `IIdentityAtlasSurface`; **load-bearing prerequisite for W#48 Phase 1**"
status: "built"
status_cell: "`built` — Phase 2 complete PRs #663+#665+#666+#744; Phases 3+ DONE via W#58; PR #814 flip merged 2026-05-13; pipeline closed"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/helm-identity-atlas-stage06-handoff.md` + `docs/adrs/0066-helm-composition-and-identity-atlas-surface.md` (PR #529 merged) + `packages/ui-core/Wayfinder/` (P1 merged)"
---

## Notes

**XO priority recommendation 2026-05-06:** highest-leverage next pickup for COB
alongside W#46. Phase 2 cascade-unblocks W#48 (Atlas Integration-Config UI Surface)
which has explicit halt-condition "begin when W#53 Phase 1 lands" — that gate is now
cleared (Phase 1 fully merged via PR #630 + #633). ADR 0066 + A1 already Accepted;
hand-off ready at `icm/_state/handoffs/helm-identity-atlas-stage06-handoff.md`.
Parallel-safe with W#46 (Shared Design System) since substrate concerns don't overlap
(Wayfinder Helm/Atlas vs Ship Architecture design tokens + UICore primitives).

**Phase 1 milestone complete 2026-05-06.** Phase 1a merged via PR #630
(IAtlasProvider<TView> + IHelmWidget contract surface); Phase 1b merged
via PR #633 (KeyFingerprint + IIdentityAtlasSurface + 8 view-model
records). All Phase 1 contract types now on origin/main.

**Phase 1a (PR #630)**: `Sunfish.UICore.Wayfinder` namespace —
`IAtlasProvider<TView>` (invariant — hand-off cited `out TView` but C#
compiler rejects on `Task<T>` return type per CS1961; concrete W#48
`IIntegrationAtlasProvider` derives directly without covariant
downcast); `IHelmWidget` interface + 5 records (HelmWidgetMetadata /
HelmWidgetViewState / HelmWidgetAction / HelmRenderContext) + 2
`[JsonStringEnumConverter]` enums (HelmSlot / HelmActionInvocationKind)
+ `HelmOptions` (PeriodicRefreshInterval default 1m);
`IHelmWidgetRegistry` + `internal sealed DefaultHelmWidgetRegistry`
(Slot then OrderHint sort; LINQ stable); `AddSunfishHelm()` +
`AddHelmWidget<TWidget>()` DI extensions.

**Phase 1b (PR #633)**: `KeyFingerprint` value type (95-char hex-with-
colons SHA-256; readonly record struct + Parse + IsValid + ToString +
JsonConverter) — relocated from foundation-recovery to
`foundation/Crypto/` to break a foundation-recovery → kernel-security →
ui-core cycle. `IIdentityAtlasSurface` 5-method projection contract +
8 view-model records (IdentityProfileEditViewModel / KeyRotationViewModel
/ RecoveryContactsViewModel / RecoveryContact / HistoricalKeysBrowseViewModel
/ HistoricalKeyEntry / ActiveTeamOverviewViewModel /
TeamMembershipEntry).

**Three hand-off divergences (all council-validated)**:
1. `out TView` → invariant `TView` (P1a) — C# CS1961 reproduced.
2. `TeamId? → Guid?` (P1a + P1b) — kernel-runtime → ui-core cycle.
3. KeyFingerprint relocation foundation-recovery → foundation/Crypto/
   (P1b) — foundation-recovery → kernel-security → ui-core cycle.

DateTimeOffset over NodaTime (cohort precedent W#46/W#49/W#50/W#54/W#55).

**Pre-merge council** (standard 4-perspective adversarial; Opus + xhigh)
returned **READY-TO-MERGE** for both P1a + P1b with no findings.
**Cohort batting average: 30-of-36** — both W#53 phases were the
cleanest substrate landings.

**Tests**:
- P1a: 11 new `HelmWidgetRegistryTests` (47/47 ui-core total).
- P1b: 12 new `KeyFingerprintTests` in foundation/tests/Crypto/ + 9 new
  `IdentityAtlasContractTests` in ui-core/tests/. 56/56 ui-core +
  279/279 foundation totals after this PR.

**W#48 unblocked** by P1a (PR #630 merged 2026-05-06); ready for COB
pickup.

**Phase 2 PR 2a shipped 2026-05-06 PR #663.** 4 GlanceBand widgets on origin/main:
`IdentityGlanceWidget` (orderHint 100) + `SyncStateWidget` (200) +
`ActiveTeamWidget` (300) + `MissionEnvelopeSummaryWidget` (400).

**H8 CLEARED 2026-05-06** — W#57 (PR #662) shipped `IStandingOrderEventStream`.
`QuickTogglesWidget` + `RecentStandingOrdersWidget` no longer need H8 periodic-fallback
workaround; full reactive subscribe-before-load is available.

**Phase 2 COMPLETE 2026-05-13.** All 4 PRs merged: 2a #663 + 2b #665 + 2c-blazor #666 + 2c-react #744. H9 TypeScript parity gate CLEARED. W#58 Phase 1 H1 gate CLEARED — `identity-atlas-implementations-stage06-handoff.md` now unblocked for COB pickup.

**Phase 3 deferred** to **W#58** (identity Atlas implementations — hand-off authored 2026-05-06 at `icm/_state/handoffs/identity-atlas-implementations-stage06-handoff.md`).
