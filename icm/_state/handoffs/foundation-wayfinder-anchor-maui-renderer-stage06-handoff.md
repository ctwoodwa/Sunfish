# Foundation.Wayfinder Anchor MAUI ISystemRequirementsRenderer — Stage 06 hand-off

**From:** XO research session
**To:** sunfish-PM (COB) session
**Workstream:** W#42 follow-on (Anchor MAUI concrete `ISystemRequirementsRenderer`)
**Spec:** [ADR 0063](../../docs/adrs/0063-mission-space-requirements.md) §A1.1 (Accepted; PR #411 merged) + [ADR 0065](../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md) §Decision §7 WCAG mandate (Accepted; PR #479 merged 2026-05-02) + [ADR 0048](../../docs/adrs/0048-anchor-multi-backend-maui.md) (Accepted; A1+A2 mobile-scope amendments landed 2026-04-30) + [ADR 0032](../../docs/adrs/0032-multi-team-anchor-workspace-switching.md) (Accepted)
**Pipeline variant:** `sunfish-feature-change`
**Estimated effort:** 13–18 hours focused sunfish-PM time
**Estimate basis:** mirrors the per-adapter UI-surface phase shape of `foundation-mission-space-requirements-stage06-handoff.md` (W#41) Phase 4 (~3h substrate-only) extrapolated to a full per-adapter renderer + a11y + DI + tests; calibrated against `property-leasing-pipeline-stage06-handoff.md` Phase 5 UI-surface phase (~6–8h) and `foundation-wayfinder-stage06-handoff.md` Phase 4 docs+wiring (~2h). First-of-kind Anchor MAUI renderer; estimate range widened to ±35% per ADR 0073 estimate-honesty rule for novel-surface workstreams.
**Decomposition:** 5 phases shipping as ~5–6 PRs
**Prerequisites:**
- W#42 substrate ✓ (built 2026-05-04 across PRs #503/#504/#505/#510/#513/#514; ledger row 42 reads `built` on origin/main)
- ADR 0063 + A1 ✓ (Accepted; substrate types `MinimumSpec`, `SystemRequirementsResult`, `DimensionEvaluation`, `ISystemRequirementsRenderer`, `ISystemRequirementsSurface`, `SystemRequirementsRenderMode` all on origin/main in `packages/foundation-mission-space/`)
- ADR 0065 §Decision §7 WCAG 2.2 AA + EN 301 549 v3.2.1 mandate ✓ (Accepted; `apps/docs/foundation/wayfinder/wcag.md` baseline established by PR #514)
- ADR 0048 ✓ (Accepted; Anchor MAUI multi-backend; Win + MacCatalyst Phase 1 RIDs; iOS/Android deferred to MAUI 11 Mono-runtime stabilization per ADR 0048-A1)
- ADR 0032 ✓ (Accepted; multi-team Anchor workspace switching; `IActiveTeamAccessor` in DI today)
- ADR 0034 ✓ (Accepted; per-adapter a11y harness; `Sunfish.UIAdapters.Blazor.A11y` available as test-only ProjectReference)
- ADR 0049 ✓ (Accepted; audit substrate; `Sunfish.Kernel.Audit.IAuditTrail` + `AuditRecord` + `AuditEventType` + `AuditPayload` all on origin/main)
- W#41 Foundation.MissionSpace.Requirements ✓ (built; `IMinimumSpecResolver` reference impl + 5 `AuditEventType` constants — `MinimumSpecEvaluated` / `InstallBlocked` / `InstallWarned` / `PostInstallSpecRegression` / `InstallForceEnabled` — already shipped in `Sunfish.Kernel.Audit.AuditEventType`)

**Status:** `ready-to-build`

---

## Scope summary

Build the **Anchor MAUI** concrete implementation of `ISystemRequirementsRenderer`. The substrate (W#42 / W#41) ships the platform-agnostic interface + data types; this hand-off ships the per-adapter UX that mounts `SystemRequirementsResult` onto Anchor's Razor component tree (Anchor is **MAUI Blazor Hybrid** per `accelerators/anchor/Sunfish.Anchor.csproj` `<UseMaui>true</UseMaui>` + `builder.Services.AddMauiBlazorWebView()` in `MauiProgram.cs`; the renderer is therefore a Razor-component implementation, not pure XAML).

The renderer must serve all three `SystemRequirementsRenderMode` values per ADR 0063-A1.1:

1. **`PreInstallFullPage`** — full-page UX before installer commits (Steam-style System Requirements page).
2. **`PostInstallInlineExplanation`** — inline explanation panel (e.g., embedded in a settings/about screen).
3. **`PostInstallRegressionBanner`** — banner that appears when a previously-passing dimension regresses post-install (`PostInstallSpecRegression` audit-event-driven re-render trigger).

Each mode renders the same `SystemRequirementsResult` (overall verdict + 10 per-dimension `DimensionEvaluation` records + optional `OperatorRecoveryAction`) but with platform-conventional layout, copy density, and a11y affordances. Phase 1 ships the `PreInstallFullPage` mode end-to-end; Phases 2–3 add the other two modes; Phase 4 wires DI + cross-platform native-a11y bridging; Phase 5 is the ledger close.

The renderer is composition-aware per ADR 0032: the active team's `MissionEnvelope` may differ from another team's, so the resolver is invoked through `IActiveTeamAccessor`-scoped DI (already in Anchor's `MauiProgram.cs` via `AddSunfishDefaultTeamRegistrar` + `AddSunfishTeamStoreActivator`). Audit emission is the **substrate's** responsibility (the resolver emits `MinimumSpecEvaluated` / `InstallBlocked` / `InstallWarned`); this renderer **MUST NOT** emit any `AuditRecord` of its own — render is a presentation-layer activity, not a domain-event-emitting one (per ADR 0049 audit immutability + the Wayfinder substrate's audit-by-construction discipline).

### NOT in scope

- **Bridge React renderer** — separate per-adapter Stage 06 (W#42 follow-on, future hand-off; deferred until Anchor MAUI renderer ships and informs the React shape).
- **iOS SwiftUI renderer** — separate per-adapter Stage 06 (W#42 follow-on, future hand-off; tracked alongside W#23 iOS field-capture roster).
- **Android-native renderer** — same: future per-adapter Stage 06.
- **Force-install operator override surface** — per ADR 0063-A1.11 the force-install audit + UX is `IInstallForceEnableSurface` (already shipped in `packages/foundation-mission-space/Services/IInstallForceEnableSurface.cs` + `DefaultInstallForceEnableSurface.cs`); this hand-off renders a "Force install" affordance only when `Overall == OverallVerdict.Block`, but invokes the existing surface — it does NOT re-implement force-enable logic.
- **`MinimumSpec` authoring UX** — bundle authors declare `MinimumSpec` in `BusinessCaseBundleManifest.requirements` (ADR 0063 + ADR 0007 amendment); this hand-off only consumes the resolved `SystemRequirementsResult`, not authoring it.
- **Atlas integration** — the Wayfinder Atlas projector (W#42 P3a) renders Standing Orders, NOT system requirements; the two surfaces are siblings, not nested. The renderer makes no `IAtlasProjector` calls.
- **Probe extension or new dimensions** — the 10 dimensions are fixed by ADR 0062. This hand-off displays the 10; any new dimension requires an ADR amendment.
- **Localization beyond en-US** — the Phase 1 string roster lands in `Resources/Localization/SharedResource.resx` only; satellite locales (es-419, pt-BR, fr, de, ja, zh-Hans, ar-SA, hi, he-IL, fa-IR, ko) follow the Anchor 12-locale roster as a follow-up via the `tooling/locale-completeness-check` cadence — not in this hand-off.

---

## Phases

### Phase 1 — `PreInstallFullPage` Razor component + DimensionEvaluation rows (~4–5h)

**What to build:**

- `accelerators/anchor/Components/Pages/SystemRequirements.razor` (+ `.razor.css`):
  - `@page "/system-requirements/{BundleId}"` route; injectable via `<NavLink>` from any block-install affordance.
  - Receives a `[Parameter] string BundleId` (route param) and resolves `SystemRequirementsResult` via `IMinimumSpecResolver.EvaluateAsync(spec, envelope, platformKey, ct)` where `platformKey` is derived from `DeviceInfo.Platform` (MAUI built-in: `"WinUI"` → `"windows-desktop"`; `"MacCatalyst"` → `"macos-desktop"`; `"iOS"` → `"ios"`; `"Android"` → `"android"`; per ADR 0063 §163 platform-key convention).
  - Renders the overall verdict banner at the top: green check (`OverallVerdict.Pass`), amber warning (`WarnOnly`), red block (`Block`). Banner text from `IStringLocalizer<SharedResource>` keys `sysreq.verdict.pass` / `sysreq.verdict.warn` / `sysreq.verdict.block`.
  - For each `DimensionEvaluation` in `result.Dimensions`, renders a row component (`SystemRequirementsDimensionRow.razor`, see below) with:
    - Status icon: ✓ for `Pass`, ⚠ for `Fail` + `Recommended` policy, ✗ for `Fail` + `Required` policy, — for `Unevaluated`.
    - Dimension display name (localized via `sysreq.dimension.{kind}.name` keys, 10 entries — Hardware/User/Regulatory/Runtime/FormFactor/Edition/Network/Trust/SyncState/VersionVector).
    - Policy badge (`Required` / `Recommended` / `Informational` / `Unevaluated`).
    - `Detail` text from the `DimensionEvaluation.Detail` field.
    - Optional `OperatorRecoveryAction` block when present (`AsString()` of the action + a localized "Try this" header).
  - Footer actions: when `Overall == Block`, render a `<button>` whose label is `sysreq.action.force_install` and whose handler delegates to `IInstallForceEnableSurface.RequestAsync(...)`; when `Overall == WarnOnly`, render a `<button>` for `sysreq.action.install_anyway`; when `Overall == Pass`, render a primary `<button>` for `sysreq.action.continue`.
- `accelerators/anchor/Components/SystemRequirementsDimensionRow.razor` (+ `.razor.css`):
  - Pure presentational component receiving `[Parameter] DimensionEvaluation Eval`.
  - WCAG 2.2 AA contract: `role="listitem"` with parent `role="list"`; status-icon has `aria-label` set to the localized status (`sysreq.status.pass` / `.warn` / `.block` / `.unevaluated`) — the icon is decorative-only with `aria-hidden="true"` if the surrounding text already conveys the state, OR labeled if standalone (per ADR 0034 SC 1.1.1 contract).
- `accelerators/anchor/Resources/Localization/SharedResource.resx` (en-US) — add the string roster:
  - `sysreq.verdict.pass`, `sysreq.verdict.warn`, `sysreq.verdict.block` (3 keys)
  - `sysreq.dimension.{Hardware,User,Regulatory,Runtime,FormFactor,Edition,Network,Trust,SyncState,VersionVector}.name` (10 keys)
  - `sysreq.policy.{Required,Recommended,Informational,Unevaluated}` (4 keys)
  - `sysreq.status.{pass,warn,block,unevaluated}` (4 keys, used as `aria-label` text)
  - `sysreq.action.{force_install,install_anyway,continue}` (3 keys)
  - `sysreq.recovery.try_this` (1 key, the localized "Try this" header)
  - `sysreq.title.preinstall` (1 key, the page H1)

  Total: 26 new resource entries. Per Anchor's localization convention the bare `.resx` (no locale suffix) is en-US baseline; satellite locales arrive via the Global-First UX 12-locale roster cadence (out of scope per §NOT in scope).
- `accelerators/anchor/Sunfish.Anchor.csproj` — add `<ProjectReference Include="../../packages/foundation-mission-space/Sunfish.Foundation.MissionSpace.csproj" />` if not already present (verify via `dotnet list reference`).
- Tests in `accelerators/anchor/tests/` (bUnit-based; mirror existing Anchor test conventions if `accelerators/anchor/tests/` already contains bUnit infra; otherwise stub the test project per `apps/local-node-host/Tests/`-style precedent):
  - `SystemRequirementsTests.cs` — 6 unit tests:
    - 1: `Pass` verdict renders green banner + zero `Fail` rows.
    - 1: `WarnOnly` verdict renders amber banner + at least one `Recommended`+`Fail` row.
    - 1: `Block` verdict renders red banner + at least one `Required`+`Fail` row + `force_install` button visible.
    - 1: `Block` verdict + missing `OperatorRecoveryAction` renders the row without a recovery block.
    - 1: localization key resolution (assert `IStringLocalizer<SharedResource>` is invoked for every visible label; use NSubstitute to capture calls per Decision Discipline Rule 5).
    - 1: per-platform key derivation maps `DeviceInfo.Platform` correctly (NSubstitute substitute for `IDeviceInfo`-equivalent; verify the key string passed into `IMinimumSpecResolver.EvaluateAsync`).

**Estimated effort:** 4–5h (within the W#41 Phase 4 + W#42 Phase 4 cohort precedent envelope per the §1 estimate basis citation).

**PR title:** `feat(anchor): SystemRequirements PreInstallFullPage Razor renderer (W#42 follow-on P1)`

**Gate:** `dotnet build -c Release` clean for `Sunfish.Anchor.csproj` on Win + Mac; 6 new bUnit tests pass; `accelerators/anchor/Components/Pages/SystemRequirements.razor` renders all three verdict cases under bUnit's `IRenderedFragment.Markup` snapshot; localization roster has 26 keys present in `SharedResource.resx`.

---

### Phase 2 — `PostInstallInlineExplanation` mode + render-mode dispatch (~2–3h)

**What to build:**

- `accelerators/anchor/Components/SystemRequirementsInlinePanel.razor` — collapsed-by-default `<details>` element rendering a single dimension's `DimensionEvaluation` inline; mounts via `<SystemRequirementsInlinePanel BundleId="@bundleId" Dimension="DimensionChangeKind.Hardware" />`.
- `accelerators/anchor/Services/AnchorMauiSystemRequirementsRenderer.cs` — concrete `ISystemRequirementsRenderer` implementation. The class mediates the three modes:
  - `PreInstallFullPage` → navigates `NavigationManager` to `/system-requirements/{bundleId}` (full-page Razor route from Phase 1).
  - `PostInstallInlineExplanation` → exposes a `RenderInlineFragment(ISystemRequirementsSurface, DimensionChangeKind)` method that consumers compose into their own page; the renderer itself does not auto-navigate.
  - `PostInstallRegressionBanner` → Phase 3.
- `accelerators/anchor/Services/AnchorMauiSystemRequirementsSurface.cs` — concrete `ISystemRequirementsSurface` with `Platform` returning the `DeviceInfo.Platform` key; carries the `NavigationManager` reference + a `Microsoft.AspNetCore.Components.RenderFragment?` slot for inline mounts.
- Tests: 4 unit tests on `AnchorMauiSystemRequirementsRenderer`:
  - `PreInstallFullPage` mode triggers `NavigationManager.NavigateTo("/system-requirements/{bundleId}")`.
  - `PostInstallInlineExplanation` mode does NOT trigger navigation; sets the inline `RenderFragment` on the surface.
  - Surface's `Platform` matches the constructed `DeviceInfo.Platform` key.
  - All three render modes accepted by the contract (parametric assertion mirroring `SystemRequirementsRendererTests.RenderMode_AllValues_AcceptedByContract`).

**Estimated effort:** 2–3h.

**PR title:** `feat(anchor): SystemRequirements PostInstallInlineExplanation mode + renderer dispatch (W#42 follow-on P2)`

**Gate:** `dotnet build` clean; 4 new unit tests pass cumulatively with Phase 1 tests; `AnchorMauiSystemRequirementsRenderer` exists and implements `Sunfish.Foundation.MissionSpace.ISystemRequirementsRenderer`.

---

### Phase 3 — `PostInstallRegressionBanner` mode + envelope-change subscription (~2h)

**What to build:**

- `accelerators/anchor/Components/SystemRequirementsRegressionBanner.razor` — top-of-shell banner rendered by `MainLayout.razor` when an `EnvelopeChange` from `IMissionEnvelopeProvider` flips a previously-passing `Required` dimension to `Fail`. Banner uses the `aria-live="assertive"` live-region pattern per WCAG 2.2 SC 4.1.3 + ADR 0034 `liveRegion: "assertive"` contract.
- `accelerators/anchor/Services/SystemRequirementsRegressionObserver.cs` — implements `IMissionEnvelopeObserver` (per ADR 0062 §A1.4); subscribes via `IMissionEnvelopeProvider.Subscribe(this)` on bootstrap; on each `OnChangedAsync` event, re-evaluates the cached `MinimumSpec` for the active bundle(s) and raises a `PostInstallSpecRegression`-shaped re-render trigger to the banner via a `Channel<DimensionChangeKind>` or equivalent observable. The observer does NOT emit audit (the resolver does — `PostInstallSpecRegression` is already a registered `AuditEventType` from W#41).
- `accelerators/anchor/MauiProgram.cs` — register `AnchorMauiSystemRequirementsRenderer` + `SystemRequirementsRegressionObserver` as singletons; the observer subscribes to `IMissionEnvelopeProvider` at construction (NOT inside `OnInitializedAsync` of a Razor component, which would re-subscribe on every navigation).
- Tests: 5 unit tests:
  - 1: regression banner renders when `EnvelopeChange` causes a `Required`+`Pass` dimension to flip to `Required`+`Fail`.
  - 1: regression banner does NOT render when an `Informational` dimension flips (per ADR 0063 A1.8 explicit Informational rule).
  - 1: `PostInstallRegressionBanner` mode dispatch routes to the banner's render path.
  - 1: `IMissionEnvelopeObserver.OnChangedAsync` is invoked on subscription (NSubstitute-asserted).
  - 1: `aria-live="assertive"` attribute present on the banner element (bUnit `Markup` regex assertion or `IElement.GetAttribute("aria-live")` check).

**Estimated effort:** 2h.

**PR title:** `feat(anchor): SystemRequirements PostInstallRegressionBanner + envelope-change observer (W#42 follow-on P3)`

**Gate:** `dotnet build` clean; 5 new unit tests pass cumulatively with P1+P2; `aria-live="assertive"` attribute confirmed in markup snapshot; observer registered as singleton in `MauiProgram.cs`.

---

### Phase 4 — DI wiring + native-a11y per-platform composition + a11y harness tests (~3–4h)

**What to build:**

- `accelerators/anchor/Services/AnchorMauiServiceCollectionExtensions.cs` — adds `AddAnchorSystemRequirementsRenderer(this IServiceCollection services)` extension following the cohort `AddSunfishX()` DI pattern (per W#42 hand-off cohort patterns; mirrors `AddSunfishWayfinder()` shape from `packages/foundation-wayfinder/WayfinderServiceExtensions.cs`). The extension registers:
  - `ISystemRequirementsRenderer` → `AnchorMauiSystemRequirementsRenderer` (singleton)
  - `ISystemRequirementsSurface` → `AnchorMauiSystemRequirementsSurface` (scoped — the Razor component scope owns it; `NavigationManager` is also scoped)
  - `SystemRequirementsRegressionObserver` (singleton; subscribes on construction)
- `accelerators/anchor/MauiProgram.cs` — call `builder.Services.AddAnchorSystemRequirementsRenderer();` after `AddSunfish()` + `AddSunfishBootstrap()` (mirrors the Wave 6.3.F + Wave 6.7 registration order pattern in the existing file).
- **Per-platform native-a11y bridging** per ADR 0048's per-backend native-a11y mandate. MAUI BlazorWebView surfaces accessibility via the host platform's accessibility API:
  - **WinUI / Windows-desktop** — UIA tree exposed via the hosted WebView2; the Razor markup's ARIA roles map automatically via Edge Chromium's UIA bridge. No additional code; verify via Phase 5 a11y harness.
  - **MacCatalyst / macos-desktop** — NSAccessibility tree exposed via WKWebView; ARIA→NSAccessibility mapping is automatic. No additional code; verify via Phase 5 a11y harness.
  - **iOS** — UIAccessibility via WKWebView; deferred until ADR 0048-A1 mobile-scope unblocks (commented-out target frameworks in `Sunfish.Anchor.csproj`).
  - **Android** — AccessibilityNodeInfo via Android WebView; deferred until ADR 0048-A1 mobile-scope unblocks.

  This phase therefore exercises Win + MacCatalyst only (Phase 1 RIDs of ADR 0048); iOS / Android validation deferred to a follow-up phase that fires when ADR 0048-A1's Mono-runtime gate clears.
- A11y harness tests in `accelerators/anchor/tests/A11y/` using `Sunfish.UIAdapters.Blazor.A11y` (ADR 0034) + Playwright + axe-core/playwright. Three test scenarios:
  - **`SystemRequirementsPreInstallFullPageA11yTests`** — bUnit renders the page; `Sunfish.UIAdapters.Blazor.A11y.PlaywrightPageHost` hosts the markup; `AxeRunner` runs axe-core 4.x against WCAG 2.2 AA tags + EN 301 549 v3.2.1 mapping; `SunfishA11yAssertions.AssertContractAsync` verifies the `SunfishA11yContract` block (`name`, `role`, `keyboard`, `focusOrder`, `liveRegion: "off"` for full-page mode, `reducedMotion: "respects"`, `rtlIconMirror: "non-directional"` for status icons).
  - **`SystemRequirementsInlinePanelA11yTests`** — same harness, smaller scope; `liveRegion: "off"`; `keyboard.keys: ["Enter", "Space"]` for the `<details>` toggle.
  - **`SystemRequirementsRegressionBannerA11yTests`** — `liveRegion: "assertive"`; assert the banner mounts with `role="alert"` per WCAG 2.2 SC 4.1.3 (status-message role).
- New ProjectReference: `accelerators/anchor/tests/Anchor.Tests.csproj` (or whatever the existing test project is named) gains `<ProjectReference Include="../../../packages/ui-adapters-blazor-a11y/Sunfish.UIAdapters.Blazor.A11y.csproj" />`.
- `apps/docs/foundation/wayfinder/wcag.md` — append a "**Per-adapter conformance: Anchor MAUI**" sub-section pointing at the three a11y test classes + the SC mapping table; do NOT claim "conformant" — claim "baseline established for `PreInstallFullPage` / `PostInstallInlineExplanation` / `PostInstallRegressionBanner` modes on Win + MacCatalyst" per the W#42 P4 docs cohort precedent.

**Estimated effort:** 3–4h.

**PR title:** `feat(anchor): SystemRequirementsRenderer DI + WCAG 2.2 AA a11y harness (W#42 follow-on P4)`

**Gate:** `dotnet build` clean; 3 new a11y harness tests pass on Win + MacCatalyst (skip iOS/Android; the CI-tracked RIDs match `Sunfish.Anchor.csproj`'s active `TargetFrameworks` per ADR 0048); `AddAnchorSystemRequirementsRenderer` extension registers all three services (verify via `ServiceProvider.GetRequiredService<ISystemRequirementsRenderer>()` returns `AnchorMauiSystemRequirementsRenderer`); `apps/docs/foundation/wayfinder/wcag.md` appended sub-section visible in `apps/docs` build.

---

### Phase 5 — Ledger flip + close (~30min)

**What to build:**

- `icm/_state/active-workstreams.md` — add a NEW row for "W#42 follow-on Anchor MAUI renderer" with status `built` and PR list. Use the row-naming convention "W#42 follow-on (Anchor MAUI ISystemRequirementsRenderer)" — the bare W#42 row stays at `built` (the substrate is shipped); this new row is a follow-on, not a replacement.
- `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_42_anchor_maui_renderer_built.md` — memory entry summarizing the shipped scope (5 PRs + new types + a11y baseline) for future-session auto-load.
- Optional: short note in `icm/_state/research-inbox/` (`cob-resumed-...md` if COB had previously gone idle on this) per CLAUDE.md inbox protocol.

**Estimated effort:** 30 minutes.

**PR title:** `chore(icm): flip W#42 follow-on Anchor MAUI renderer row to built (P5)`

**Gate:** `icm/_state/active-workstreams.md` row reads `built`; memory file written; XO can read the new ledger row + memory and confirm closure.

---

## Halt conditions

(Required halt conditions per ADR 0073 §4 plus workstream-specific items.)

1. **Required (per ADR 0073):** if any prerequisite listed in §1 is not yet `built` when its phase is needed → halt; write `cob-question-2026-05-XXTHH-MMZ-w42-renderer-{slug}.md` naming the unmet prerequisite. Specifically: verify on origin/main at session-start `but status` / `git log --all --oneline -10` that ledger row 42 reads `built` AND that ADR 0063 + ADR 0065 + ADR 0048 + ADR 0032 + ADR 0034 + ADR 0049 all read Status: Accepted on origin/main (canonical via `gh api repos/ctwoodwa/Sunfish/contents/docs/adrs/0063-mission-space-requirements.md` `frontmatter.status` field).

2. **Required (per ADR 0073):** if the active-workstreams.md row for this workstream does not read `ready-to-build` when COB begins → halt; write `cob-question-*` naming the discrepancy.

3. **WCAG/a11y subagent pre-merge canonical (per ADR 0065 §Decision §7).** ADR 0065 mandates a WCAG/a11y subagent on **every** UI-bearing follow-on. Phases 1, 2, 3, 4 all carry a UI surface; pre-merge council MUST include a WCAG/a11y perspective BEFORE any phase commit. This is non-negotiable — mirrors the W#42 substrate hand-off Phase 3a/4 cohort posture (council batting average 22-of-22; substrate cohort needed amendments on every UI-bearing phase). If the WCAG/a11y subagent flags an issue, halt the phase + apply mechanical amendments per Decision Discipline Rule 3 (auto-accept rename / fix-citation / scope-tightening); raise non-mechanical findings to XO via `cob-question-*`.

4. **MAUI version compatibility.** Anchor pins MAUI 11 preview (`net11.0-windows10.0.19041.0` + `net11.0-maccatalyst`) per `Sunfish.Anchor.csproj`. If, at Phase 1 start, the MAUI workload version has bumped (e.g., MAUI 11 GA stabilized), proceed with the GA SDK but pin tests against the GA workload version explicitly; if any preview-only API used in the renderer (e.g., `BlazorWebViewDeveloperTools` extension) has changed signature, halt + raise `cob-question-*`.

5. **Cross-platform parity — Win + MacCatalyst Phase 1, mobile deferred.** Phase 4's a11y harness exercises Win + MacCatalyst RIDs only (per ADR 0048's active `TargetFrameworks` lines). If `Sunfish.Anchor.csproj` has had the iOS / Android `TargetFrameworks` lines uncommented (signaling ADR 0048-A1 mobile-scope clearance has landed) by the time Phase 4 begins, halt + raise `cob-question-*` — the renderer should add iOS UIAccessibility + Android AccessibilityNodeInfo bridging tests as a sixth-phase scope expansion, NOT silently include them in P4.

6. **Audit double-emission discipline (per ADR 0049 immutability).** The renderer **MUST NOT** call `IAuditTrail.AppendAsync(...)`. Audit emission for `MinimumSpecEvaluated` / `InstallBlocked` / `InstallWarned` / `PostInstallSpecRegression` / `InstallForceEnabled` is the resolver's responsibility (W#41 substrate). If any phase's implementation has the renderer constructing an `AuditRecord`, halt + raise `cob-question-*` — this is an architectural defect mirroring the ADR 0046 council finding pattern. The renderer is presentation-layer; no audit emission.

7. **Active-team-switcher composition (per ADR 0032).** `MissionEnvelope` evaluation may differ per active team — different teams may have different bundle installs and different runtime envelopes. The renderer MUST resolve `IMinimumSpecResolver` through scoped DI such that `IActiveTeamAccessor`-bound services flow correctly. If, at Phase 1 build time, `IMinimumSpecResolver` is registered as singleton (rather than scoped or transient), team-switching will leak cached evaluations across teams. Halt + raise `cob-question-*`. (Default per W#41 cohort precedent: `IMinimumSpecResolver` is registered transient OR scoped at `AddSunfishMissionSpace()`; verify via `ServiceCollectionExtensions.cs` source on origin/main BEFORE Phase 1 commit.)

---

## Acceptance criteria (cumulative)

- [ ] `accelerators/anchor/Components/Pages/SystemRequirements.razor` exists and renders all three `SystemRequirementsRenderMode` cases (P1 + P2 + P3 cumulative).
- [ ] `accelerators/anchor/Services/AnchorMauiSystemRequirementsRenderer.cs` exists and implements `Sunfish.Foundation.MissionSpace.ISystemRequirementsRenderer`.
- [ ] `accelerators/anchor/Services/AnchorMauiSystemRequirementsSurface.cs` exists and implements `Sunfish.Foundation.MissionSpace.ISystemRequirementsSurface` with `Platform` returning the correct `DeviceInfo.Platform`-derived key.
- [ ] `accelerators/anchor/Services/SystemRequirementsRegressionObserver.cs` exists and implements `Sunfish.Foundation.MissionSpace.IMissionEnvelopeObserver`.
- [ ] `accelerators/anchor/Services/AnchorMauiServiceCollectionExtensions.cs` exposes `AddAnchorSystemRequirementsRenderer()` extension; `MauiProgram.cs` calls it.
- [ ] `accelerators/anchor/Resources/Localization/SharedResource.resx` contains all 26 new keys per the §Phase 1 string roster.
- [ ] `dotnet build -c Release` clean for `Sunfish.Anchor.csproj` on Win + MacCatalyst targets.
- [ ] All new unit tests pass: 6 (P1) + 4 (P2) + 5 (P3) + 3 (P4 a11y harness) = 18 tests, plus all existing Anchor + foundation-mission-space tests still green.
- [ ] WCAG 2.2 AA + EN 301 549 v3.2.1 baseline established for `PreInstallFullPage` + `PostInstallInlineExplanation` + `PostInstallRegressionBanner` modes on Win + MacCatalyst per `Sunfish.UIAdapters.Blazor.A11y` harness output (no axe-core violations of impact `Serious` or `Critical`).
- [ ] No `IAuditTrail.AppendAsync` call exists anywhere under `accelerators/anchor/` from this hand-off's deliverables (audit emission stays in W#41 resolver).
- [ ] `apps/docs/foundation/wayfinder/wcag.md` appended with "Per-adapter conformance: Anchor MAUI" sub-section.
- [ ] WCAG/a11y subagent pre-merge council fired on every UI-bearing phase (P1, P2, P3, P4) per ADR 0065 §Decision §7; mechanical amendments applied per Decision Discipline Rule 3; non-mechanical findings raised to XO via `cob-question-*`.
- [ ] `icm/_state/active-workstreams.md` row for "W#42 follow-on Anchor MAUI renderer" reads `built` with PR list and PR URLs.
- [ ] Memory entry written at `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_42_anchor_maui_renderer_built.md`.

---

## Total decomposition table

| Phase | Subject | Hours | PR title prefix |
|---|---|---|---|
| 1 | `PreInstallFullPage` Razor + DimensionEvaluation rows | 4–5 | `feat(anchor): SystemRequirements PreInstallFullPage Razor renderer (W#42 follow-on P1)` |
| 2 | `PostInstallInlineExplanation` mode + dispatch class | 2–3 | `feat(anchor): SystemRequirements PostInstallInlineExplanation mode + renderer dispatch (W#42 follow-on P2)` |
| 3 | `PostInstallRegressionBanner` + envelope-change observer | 2 | `feat(anchor): SystemRequirements PostInstallRegressionBanner + envelope-change observer (W#42 follow-on P3)` |
| 4 | DI wiring + WCAG 2.2 AA a11y harness | 3–4 | `feat(anchor): SystemRequirementsRenderer DI + WCAG 2.2 AA a11y harness (W#42 follow-on P4)` |
| 5 | Ledger flip + close | 0.5 | `chore(icm): flip W#42 follow-on Anchor MAUI renderer row to built (P5)` |
| **Total** | | **11.5–14.5h** | **5 PRs** |

(The §1 `Estimated effort` range "13–18h" widens the table sum by ~+25% to absorb the WCAG/a11y subagent council loop on each UI-bearing phase, which the per-phase hours do not explicitly bake in. Per ADR 0073 estimate-honesty rule: §1 wins on disagreement; the table is the sub-budget. If the council loop runs >2× per phase, halt + escalate per halt-condition #3.)

---

## Cohort patterns to follow

This hand-off mirrors the patterns established by the W#33-derived substrate cohort (W#34 / W#35 / W#36 / W#37 / W#39 / W#40 / W#41 / W#42) plus the per-adapter UI-surface precedent in `property-leasing-pipeline-stage06-handoff.md`. Specifically:

- **`AddSunfishX()` / `AddAnchorX()` DI extension naming.** The new extension is `AddAnchorSystemRequirementsRenderer` (Anchor-prefixed because it's an accelerator-tier surface, not a foundation/blocks substrate). Mirrors `AddSunfishWayfinder()` shape from `packages/foundation-wayfinder/WayfinderServiceExtensions.cs`.
- **`JsonStringEnumConverter` on every enum.** Inherited from W#42 substrate; the Anchor renderer doesn't author new JSON-roundtrippable enums but DOES depend on `OverallVerdict` / `DimensionPassFail` / `DimensionPolicyKind` / `SystemRequirementsRenderMode` already converter-attributed in `packages/foundation-mission-space/Models/RequirementsEnums.cs`.
- **NSubstitute for test doubles.** Industry-default per Decision Discipline Rule 5; mirrors W#42 P1+P2+P3a test conventions.
- **`IStringLocalizer<SharedResource>` + `Resources/Localization/SharedResource.resx` localization.** Mirrors `accelerators/anchor/Components/Pages/Onboarding.razor` + `TeamSwitcherPage.razor` Anchor cohort precedent.
- **`Sunfish.UIAdapters.Blazor.A11y` harness for a11y tests.** Mirrors the ADR 0034 contract + the `tests/AI/Sunfish*A11yTests.cs` test-class shape in `packages/ui-adapters-blazor-a11y/tests/`.
- **No audit emission from presentation layer.** Inherited from W#42 substrate's audit-by-construction discipline + ADR 0049 immutability.
- **Pre-merge council canonical for UI-bearing phases.** Per `feedback_council_before_automerge.md` + ADR 0065 §Decision §7 — council fires BEFORE PR creation, not in parallel with auto-merge. Mirrors W#42 P3a + P3b + P4 cohort discipline.
- **`apps/docs/foundation/wayfinder/wcag.md` baseline-not-conformance language.** Inherited from W#42 P4 cohort; never claim "WCAG conformant" — claim "baseline established for [scope]."

---

## Open questions

These are explicitly punted by the authoring spec or anticipated by XO; COB resolves via beacon if encountered:

1. **`MinimumSpec` source at render time.** The renderer needs to know which bundle's `MinimumSpec` to evaluate. Phase 1 ships with a route param (`/system-requirements/{BundleId}`) and assumes a `IMinimumSpecProvider` (or equivalent) resolves bundle ID → spec. If no such provider exists yet on origin/main, halt + raise `cob-question-*` — the answer may be "stub a `MinimumSpecLookupStub` that returns a hardcoded spec for kitchen-sink demo purposes; real provider lands in the Wayfinder Atlas integration phase (out of scope here)."

2. **Force-install audit shape.** The `force_install` button delegates to `IInstallForceEnableSurface.RequestAsync(...)`; the request shape is `InstallForceEnableRequest` per ADR 0063-A1.11 + W#41 substrate. If the request shape requires fields the renderer cannot supply at the UI level (e.g., a justification text field that ADR 0063 requires for audit), halt + raise `cob-question-*` — the answer may be "render a textbox bound to `Request.Reason`; assert non-empty on form submit."

3. **Reduced-motion preference.** ADR 0034's `reducedMotion: "respects"` contract requires the page to honor `prefers-reduced-motion` media query. If the verdict-banner uses any animation (e.g., expand/collapse on the inline panel), halt: confirm the CSS gates animations behind `@media (prefers-reduced-motion: no-preference)`.

4. **RTL icon mirror policy on status icons.** The status icons (✓ / ⚠ / ✗) are non-directional per Unicode + ADR 0034's `rtlIconMirror: "non-directional"` default; verify via the WCAG/a11y subagent that no directional icon (e.g., chevron) is used for status. Use of `<` / `>` chevrons would require `rtlIconMirror: "mirrors"` — halt if discovered.

---

## References

- [ADR 0032](../../docs/adrs/0032-multi-team-anchor-workspace-switching.md) — multi-team Anchor workspace switching
- [ADR 0034](../../docs/adrs/0034-a11y-harness-per-adapter.md) — per-adapter a11y harness
- [ADR 0048](../../docs/adrs/0048-anchor-multi-backend-maui.md) — Anchor multi-backend MAUI
- [ADR 0049](../../docs/adrs/0049-audit-trail-substrate.md) — audit-trail substrate
- [ADR 0063](../../docs/adrs/0063-mission-space-requirements.md) — mission-space requirements (substrate spec for `ISystemRequirementsRenderer`)
- [ADR 0065](../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md) — Wayfinder System + Standing Order Contract (W#42 substrate; §Decision §7 WCAG mandate)
- [ADR 0073](../../docs/adrs/0073-stage06-handoff-template-contract.md) — Stage-06 hand-off template contract (this hand-off conforms)
- W#41 hand-off: [`foundation-mission-space-requirements-stage06-handoff.md`](./foundation-mission-space-requirements-stage06-handoff.md) — substrate cohort precedent + estimate basis
- W#42 substrate hand-off: [`foundation-wayfinder-stage06-handoff.md`](./foundation-wayfinder-stage06-handoff.md) — direct parent; cohort patterns + DI naming + a11y baseline language
- Sibling per-adapter UI hand-off precedent: [`property-leasing-pipeline-stage06-handoff.md`](./property-leasing-pipeline-stage06-handoff.md) — UI-surface phase shape + WCAG harness wiring
- Substrate code on origin/main: `packages/foundation-mission-space/Services/ISystemRequirementsRenderer.cs` + `packages/foundation-mission-space/Models/Requirements.cs` (`SystemRequirementsResult` + `DimensionEvaluation`) + `packages/foundation-mission-space/Models/RequirementsEnums.cs` (`OverallVerdict` + `SystemRequirementsRenderMode` + `DimensionPolicyKind` + `DimensionPassFail` + `SpecPolicy`)
- Existing tests precedent: `packages/foundation-mission-space/tests/SystemRequirementsRendererTests.cs` (substrate-level interface contract tests; the Anchor renderer's tests SHOULD mirror the parametric `RenderMode_AllValues_AcceptedByContract` shape)
- Anchor MAUI shell: `accelerators/anchor/MauiProgram.cs` + `accelerators/anchor/Sunfish.Anchor.csproj` + `accelerators/anchor/Components/Pages/Onboarding.razor` + `accelerators/anchor/Components/Pages/TeamSwitcherPage.razor` (cohort patterns for Razor page authoring + localization + DI registration order)
- A11y harness substrate: `packages/ui-adapters-blazor-a11y/SunfishA11yContract.cs` + `SunfishA11yAssertions.cs` + `AxeRunner.cs` + `PlaywrightPageHost.cs`

---

## Notes for COB

- This is a **per-adapter UI** hand-off (presentation-layer); the substrate (W#42 + W#41) is shipped. No new substrate types; no audit emission from this layer.
- Pre-merge council canonical: dispatch the standard 4-perspective council subagent + a WCAG/a11y subagent BEFORE any UI-bearing phase commit (P1, P2, P3, P4). Cohort batting average is 22-of-22 — every UI-bearing substrate has needed council fixes; pre-merge is dramatically cheaper than post-merge per `feedback_council_before_automerge.md`.
- §A0 self-audit pattern is necessary but NOT sufficient for cited Sunfish.* symbols. Spot-check three directions per `feedback_council_can_miss_spot_check_negative_existence.md`: (1) negative-existence — does the cited symbol exist on origin/main? (2) positive-existence — does the cited namespace match? (3) structural-citation — do the cited fields / signatures match the actual file? The council's 22-of-22 batting average is a useful tail.
- If COB hits a halt-condition or has a design question, file `cob-question-2026-05-XXTHH-MMZ-w42-renderer-{slug}.md` in `icm/_state/research-inbox/` + halt the workstream + add a note in the active-workstreams.md row + ScheduleWakeup 1800s.
- After Phase 5 closes the row, drop `cob-resumed-2026-05-XXTHH-MMZ-w42-renderer-built.md` to research-inbox if XO had a beacon waiting; otherwise continue with the rung-1/rung-2 fallback per CLAUDE.md fallback work order.
