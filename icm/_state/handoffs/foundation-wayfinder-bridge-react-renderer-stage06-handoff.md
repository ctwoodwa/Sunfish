# Foundation.Wayfinder Bridge React `ISystemRequirementsRenderer` — Stage 06 hand-off

**From:** XO research session
**To:** sunfish-PM (COB) session
**Workstream:** W#56 (W#42 follow-on — Bridge React per-adapter UI surface; sibling to W#47 Anchor MAUI)
**Spec:** [ADR 0063](../../docs/adrs/0063-mission-space-requirements.md) §A1.1 (Accepted; PR #411 merged) + [ADR 0065](../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md) §Decision §7 WCAG mandate (Accepted; PR #479 merged 2026-05-02) + [ADR 0030](../../docs/adrs/0030-react-adapter-scaffolding.md) (Accepted) + [ADR 0014](../../docs/adrs/0014-adapter-parity-policy.md) (Accepted) + [ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) (Accepted) + [ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md) (Accepted)
**Pipeline variant:** `sunfish-feature-change`
**Estimated effort:** 14–19 hours focused sunfish-PM time
**Estimate basis:** Sibling to W#47 Anchor MAUI hand-off (13–18h, 5 phases). The Bridge React shape adds a Phase 1 .NET → JSON projection-contract step that the MAUI hand-off does NOT need (MAUI Blazor Hybrid consumes `SystemRequirementsResult` in-process; Bridge React must cross the API boundary). The added Phase 1 lifts the floor by ~1h. Calibrated against `packages/ui-adapters-react` Wave 3.5 cohort velocity (PoC components averaged ~2.5h each per ADR 0030 §Outcome notes) plus W#41 P4 substrate-only (~3h) plus W#42 P4 docs+wiring (~2h). First-of-kind React renderer for a Sunfish foundation contract; estimate range widened to ±35% per ADR 0073 estimate-honesty rule for novel-surface workstreams.
**Decomposition:** 5 phases shipping as ~5–6 PRs
**Prerequisites:**
- W#42 substrate ✓ (built 2026-05-04 across PRs #503/#504/#505/#510/#513/#514; ledger row 42 reads `built` on origin/main)
- W#41 Foundation.MissionSpace.Requirements ✓ (built; substrate types `MinimumSpec` / `SystemRequirementsResult` / `DimensionEvaluation` / `ISystemRequirementsRenderer` / `ISystemRequirementsSurface` / `SystemRequirementsRenderMode` / 5 enums all on origin/main in `packages/foundation-mission-space/`)
- W#47 Anchor MAUI renderer hand-off ✓ (authored 2026-05-04; `icm/_state/handoffs/foundation-wayfinder-anchor-maui-renderer-stage06-handoff.md`; sibling cohort precedent for phase shape, halt-conditions, audit-double-emission discipline, force-install delegation, render-mode dispatch)
- ADR 0063 + A1.1 ✓ (Accepted; substrate `SystemRequirementsRenderMode` enum + `ISystemRequirementsRenderer` interface on origin/main)
- ADR 0065 §Decision §7 WCAG 2.2 AA + EN 301 549 v3.2.1 mandate ✓ (Accepted; per-adapter mandate applies to React equally)
- ADR 0030 ✓ (Accepted; React adapter scaffolding; `packages/ui-adapters-react/` exists with Vite + Vitest + RTL + Storybook + 3 PoC components — Button, DataGrid, Dialog)
- ADR 0014 ✓ (Accepted; adapter parity policy — Blazor ↔ React; every UI surface in one adapter must be in the other)
- ADR 0031 ✓ (Accepted; Bridge as hybrid Zone C SaaS; `accelerators/bridge/Sunfish.Bridge` ASP.NET host + `Sunfish.Bridge.Client` Blazor Server today; React surfaces consume the React adapter via the API boundary defined in this hand-off's Phase 1)
- ADR 0006 ✓ (Accepted; Bridge is the SaaS shell; the React adapter is the renderer of choice for any future Bridge React surface AND for any external React consumer of the Sunfish foundation contract)
- ADR 0049 ✓ (Accepted; audit substrate; renderer MUST NOT emit audit — emission stays at the .NET resolver per the W#47 sibling discipline)
- ADR 0034 ✓ (Accepted; per-adapter a11y harness — Blazor harness lives at `packages/ui-adapters-blazor-a11y/`; the React side has Storybook + axe-core via `tooling/a11y-audit-runner` + `@storybook/addon-a11y` (Wave global-ux Plan 5 CI gate); this hand-off Phase 4 wires the renderer into that harness)

**Status:** `ready-to-build`

---

## Scope summary

Build the **React** concrete implementation of `ISystemRequirementsRenderer`. The substrate (W#42 / W#41) ships the platform-agnostic interface + data types as **.NET-side contracts only**; the React adapter has no TypeScript projection of these contracts today. This hand-off ships the per-adapter UX in `packages/ui-adapters-react/` AND the **first** .NET → JSON projection contract bridging foundation-mission-space to its TypeScript consumers. Future per-React-surface integrations (Bridge React shells, external React apps embedding Sunfish blocks) consume the contract authored here.

The Anchor MAUI sibling (W#47) consumes the substrate **in-process** because Anchor is a MAUI Blazor Hybrid app that runs the .NET resolver and the Razor renderer in the same process. The React adapter has NO equivalent in-process .NET runtime — React consumers fetch `SystemRequirementsResult` over an HTTP/JSON boundary (Bridge endpoint, federation API, or static fixture). Phase 1 of THIS hand-off therefore ships a JSON-shape contract first; Phases 2–4 ship the React renderer modes against that contract; Phase 5 closes the row.

The renderer must serve all three `SystemRequirementsRenderMode` values per ADR 0063-A1.1 (verbatim sibling-cohort with W#47):

1. **`PreInstallFullPage`** — full-page UX before installer commits (Steam-style System Requirements page).
2. **`PostInstallInlineExplanation`** — inline explanation panel (e.g., embedded in a settings/about screen).
3. **`PostInstallRegressionBanner`** — banner that appears when a previously-passing dimension regresses post-install (`PostInstallSpecRegression` audit-event-driven re-render trigger; the regression signal arrives over the API boundary as a server-pushed event or a polling-derived stale flag).

Each mode renders the same `SystemRequirementsResult` JSON payload (overall verdict + 10 per-dimension `DimensionEvaluation` records + optional `OperatorRecoveryAction`) but with React-conventional layout, copy density, and a11y affordances.

The renderer is **multi-tenant-composition-aware** per ADR 0031: a Bridge React surface may serve multiple tenants, and the active tenant's `MissionEnvelope` flows through the API boundary as part of the `SystemRequirementsResult` payload (the resolver upstream resolves the active tenant; the renderer trusts the payload). Audit emission is the **substrate's** responsibility — the .NET resolver emits `MinimumSpecEvaluated` / `InstallBlocked` / `InstallWarned`; the React renderer **MUST NOT** call any audit-emission shim (e.g., a hypothetical `/api/audit/append` endpoint) — render is presentation-layer, not domain-event-emitting (per ADR 0049 audit immutability + the Wayfinder substrate's audit-by-construction discipline).

### NOT in scope

- **Anchor MAUI renderer** — sibling W#47; already hand-off'd 2026-05-04 (PR #555 merged).
- **iOS SwiftUI renderer** — separate per-adapter Stage 06 (W#42 follow-on, future hand-off; tracked alongside W#23 iOS field-capture roster).
- **Android-native renderer** — same: future per-adapter Stage 06.
- **Bridge ASP.NET endpoint authoring (the server side that maps `IMinimumSpecResolver` to JSON).** The serialization-contract artifact (Phase 1) defines the wire shape on both sides; the .NET endpoint that produces that shape is a Bridge-side concern (`accelerators/bridge/Sunfish.Bridge/Features/SystemRequirements/`) and is part of THIS hand-off as Phase 1's second half. The endpoint registration follows Bridge's existing `MapXxxEndpoints` cohort pattern (e.g., `MapListingsEndpoints`, `MapFieldEndpoints`).
- **Force-install operator override server-side flow.** The `force_install` button in the React renderer issues an HTTP request to `IInstallForceEnableSurface`'s server-side endpoint (which already exists as a .NET interface in `packages/foundation-mission-space/Services/IInstallForceEnableSurface.cs`); the wire request shape mirrors `InstallForceEnableRequest`. This hand-off renders the affordance + dispatches the request, but does NOT re-implement the force-enable server-side logic.
- **`MinimumSpec` authoring UX** — bundle authors declare `MinimumSpec` in `BusinessCaseBundleManifest.requirements` (ADR 0063 + ADR 0007 amendment); this hand-off only consumes the resolved `SystemRequirementsResult`, not authoring it.
- **Atlas integration** — the Wayfinder Atlas projector (W#42 P3a) renders Standing Orders, NOT system requirements; the two surfaces are siblings, not nested. The renderer makes no `IAtlasProjector` calls.
- **Probe extension or new dimensions** — the 10 dimensions are fixed by ADR 0062. This hand-off displays the 10; any new dimension requires an ADR amendment.
- **Localization beyond en-US** — the Phase 2 string roster lands as TypeScript constants only (en-US baseline). Locale-completeness via `tooling/locale-completeness-check` is out of scope for this hand-off; the 12-locale roster cadence picks it up.
- **Component-parity matrix update.** Adding an `ISystemRequirementsRenderer` React component IS a parity-matrix entry per ADR 0014, but the matrix file (`_shared/engineering/adapter-parity.md`) update is part of Phase 5 (ledger close), not its own work product.
- **Bridge.Client (Blazor Server) renderer.** Bridge.Client is Blazor today (per `accelerators/bridge/Sunfish.Bridge.Client/` structure on origin/main); the W#47 Anchor MAUI hand-off's Razor components could in principle be lifted into Bridge.Client as a future scope expansion, but that is NOT this hand-off — this hand-off's renderer is the **React adapter** in `packages/ui-adapters-react/`, consumed by external React apps and any future Bridge React surface.

---

## Phases

### Phase 1 — Serialization contract + Bridge endpoint (~3–4h)

**Why this phase exists:** the W#47 Anchor MAUI sibling does NOT have this phase; MAUI consumes `SystemRequirementsResult` in-process via direct .NET reference. The React adapter has no .NET runtime — every renderer prop must cross an HTTP/JSON boundary. Ship the boundary FIRST so subsequent phases have a stable shape to render against.

**What to build:**

- `packages/ui-adapters-react/src/contracts/SystemRequirements.ts` — TypeScript port of the substrate's wire types. Mirror the `[JsonPropertyName]` attributes verbatim from `packages/foundation-mission-space/Models/Requirements.cs` + `RequirementsEnums.cs`:
  - `SpecPolicy` string-literal union: `'Required' | 'Recommended' | 'Informational'` (matches the C# enum values exactly, NOT camelCase — `JsonStringEnumConverter<SpecPolicy>` on the C# side preserves PascalCase enum names).
  - `OverallVerdict`: `'Pass' | 'WarnOnly' | 'Block'`.
  - `DimensionPolicyKind`: `'Required' | 'Recommended' | 'Informational' | 'Unevaluated'`.
  - `DimensionPassFail`: `'Pass' | 'Fail' | 'Unevaluated'`.
  - `SystemRequirementsRenderMode`: `'PreInstallFullPage' | 'PostInstallInlineExplanation' | 'PostInstallRegressionBanner'`.
  - `DimensionChangeKind`: 10-value string-literal union (`'Hardware' | 'User' | 'Regulatory' | 'Runtime' | 'FormFactor' | 'Edition' | 'Network' | 'Trust' | 'SyncState' | 'VersionVector'`) — verify the exact set against `packages/foundation-mission-space/Models/EnvelopeChange.cs` `DimensionChangeKind` enum on origin/main BEFORE Phase 1 commit (positive-existence verification per Decision Discipline Rule 6).
  - Interfaces: `OperatorRecoveryAction { actionKey: string; argumentMap?: Record<string, string> }`, `DimensionEvaluation { dimension: DimensionChangeKind; policy: DimensionPolicyKind; outcome: DimensionPassFail; operatorRecoveryAction?: OperatorRecoveryAction; detail?: string }`, `SystemRequirementsResult { overall: OverallVerdict; dimensions: DimensionEvaluation[]; operatorRecoveryAction?: OperatorRecoveryAction; evaluatedAt: string /* ISO-8601 from DateTimeOffset */ }`.
  - **Forward-compat clause:** the C# substrate carries `[JsonExtensionData] UnknownFields`; the TypeScript contract MUST use `interface` (not `type`) declarations + permit unknown additional properties via TypeScript's structural-typing default. Add a doc-comment on each interface naming the C# source-of-truth file path so future ADR amendments propagate.
- `packages/ui-adapters-react/src/contracts/SystemRequirements.test.ts` — round-trip JSON parsing tests:
  - Parse a hand-authored fixture matching the C# `JsonStringEnumConverter` PascalCase output for each enum.
  - Reject malformed fixture (missing required `overall` field) — TypeScript types are erased at runtime so the test uses `Zod` (already a transitive dep via `@testing-library/jest-dom` … verify; if not, prefer hand-rolled parse-or-throw helpers over adding a new runtime dep) OR runtime structural-validation via a hand-rolled `parseSystemRequirementsResult(json: unknown): SystemRequirementsResult` function. Pick the hand-rolled approach (zero new deps; matches ADR 0030 "minimize infra creep" decision driver).
- `accelerators/bridge/Sunfish.Bridge/Features/SystemRequirements/SystemRequirementsEndpoints.cs`:
  - `MapSystemRequirementsEndpoints(this IEndpointRouteBuilder app)` extension method following the cohort `MapListingsEndpoints` / `MapFieldEndpoints` pattern (`grep -rn "MapListingsEndpoints" /accelerators/bridge/Sunfish.Bridge/Listings/` for the exact shape).
  - Endpoints:
    - `GET /api/system-requirements/{bundleId}?platform={platformKey}` — invokes `IMinimumSpecResolver.EvaluateAsync(...)`; returns `SystemRequirementsResult` as JSON via the default ASP.NET Core minimal-API JSON serialization (System.Text.Json with `JsonStringEnumConverter` configured per the cohort's `Sunfish.Foundation.Crypto.CanonicalJson` pattern OR the standard ASP.NET serializer if that's already configured at the host level — verify `Program.cs:1-86` for `AddJsonOptions` configuration before committing).
    - `POST /api/system-requirements/{bundleId}/force-install` — accepts an `InstallForceEnableRequest` body (existing type from W#41 substrate; verify the shape on origin/main); invokes `IInstallForceEnableSurface.RequestAsync(...)`; returns 204 No Content on success, 4xx on validation failure.
  - `Program.cs` — add `app.MapSystemRequirementsEndpoints();` next to `app.MapListingsEndpoints();` and `app.MapFieldEndpoints();` (line 145 / 150 cohort).
- Tests in `accelerators/bridge/tests/Sunfish.Bridge.Tests/Features/SystemRequirements/SystemRequirementsEndpointsTests.cs` — 4 tests via `WebApplicationFactory<Program>`:
  - `GET` returns 200 + valid JSON for a known-Pass spec.
  - `GET` returns 200 + valid JSON for a known-Block spec; the JSON's `overall` field is `"Block"` (PascalCase, sanity check on the JsonStringEnumConverter wiring).
  - `GET` returns 404 for an unknown bundle ID.
  - `POST .../force-install` invokes `IInstallForceEnableSurface.RequestAsync` once (NSubstitute substitute; assert `Received(1)`).

**Estimated effort:** 3–4h.

**PR title:** `feat(bridge,ui-adapters-react): SystemRequirements JSON contract + Bridge endpoint (W#56 P1)`

**Gate:** `dotnet build -c Release` clean; `cd packages/ui-adapters-react && pnpm typecheck && pnpm test` clean; 4 new Bridge endpoint tests pass; the TypeScript contract file exists with the verified enum string-unions and interfaces; `Program.cs` has the new endpoint registration line.

---

### Phase 2 — `PreInstallFullPage` React component + DimensionEvaluation rows (~3–4h)

**What to build:**

- `packages/ui-adapters-react/src/components/SystemRequirements/SystemRequirements.tsx` — top-level component:
  - Props: `{ result: SystemRequirementsResult; mode: 'PreInstallFullPage' | 'PostInstallInlineExplanation' | 'PostInstallRegressionBanner'; onForceInstall?: () => void; onInstallAnyway?: () => void; onContinue?: () => void; bundleId: string }`. Phase 2 implements the `PreInstallFullPage` branch only; Phases 3–4 add the other two modes.
  - Renders the overall-verdict banner at the top: `OverallVerdict.Pass` → green check icon + "Your system meets all requirements" copy; `WarnOnly` → amber warning icon + "Some recommendations not met" copy; `Block` → red block icon + "Your system does not meet all requirements" copy. All copy strings are TypeScript constants in the file's top-of-file `STRINGS` object (en-US baseline only per §NOT in scope; locale-completeness is a separate cadence).
  - For each `DimensionEvaluation` in `result.dimensions`, renders a `<SystemRequirementsDimensionRow>` (see below) component as a list-item.
  - Footer actions: when `result.overall === 'Block'`, render a `<button>` with `aria-label={STRINGS.actions.forceInstall}` and `onClick={onForceInstall}`; when `result.overall === 'WarnOnly'`, render a `<button>` for `STRINGS.actions.installAnyway`; when `result.overall === 'Pass'`, render a primary `<button>` for `STRINGS.actions.continue`.
  - Consumes `useCssProvider()` for icon class resolution and primary/secondary button classes (matches existing `SunfishButton.tsx` provider-binding pattern).
- `packages/ui-adapters-react/src/components/SystemRequirements/SystemRequirementsDimensionRow.tsx` — pure presentational component:
  - Props: `{ eval: DimensionEvaluation }`.
  - WCAG 2.2 AA contract: `role="listitem"` with parent `role="list"` on the container; status-icon has `aria-label` set to a localized status string (`STRINGS.status.pass` / `.warn` / `.block` / `.unevaluated`) — the icon is decorative-only with `aria-hidden={true}` if the surrounding row text already conveys the state, OR labeled if standalone (per ADR 0034 SC 1.1.1 contract).
- `packages/ui-adapters-react/src/components/SystemRequirements/SystemRequirements.strings.ts` — string roster (en-US baseline):
  - `verdict.pass`, `verdict.warn`, `verdict.block` (3 keys)
  - `dimension.{Hardware,User,Regulatory,Runtime,FormFactor,Edition,Network,Trust,SyncState,VersionVector}.name` (10 keys)
  - `policy.{Required,Recommended,Informational,Unevaluated}` (4 keys)
  - `status.{pass,warn,block,unevaluated}` (4 keys, used as `aria-label` text)
  - `actions.{forceInstall,installAnyway,continue}` (3 keys)
  - `recovery.tryThis` (1 key, the localized "Try this" header)
  - `title.preInstall` (1 key, the page H1)

  Total: 26 string entries — exact parity with the W#47 sibling's `SharedResource.resx` roster (cohort discipline).
- `packages/ui-adapters-react/src/components/SystemRequirements/index.ts` — barrel exports.
- `packages/ui-adapters-react/src/index.ts` — append `export { SystemRequirements, type SystemRequirementsProps } from './components/SystemRequirements';` and re-export the contract types.
- Tests in `packages/ui-adapters-react/src/components/SystemRequirements/SystemRequirements.test.tsx` — 6 unit tests via Vitest + React Testing Library:
  - `Pass` verdict renders green banner + zero `Fail` rows.
  - `WarnOnly` verdict renders amber banner + at least one `Recommended`+`Fail` row.
  - `Block` verdict renders red banner + at least one `Required`+`Fail` row + `forceInstall` button visible.
  - `Block` verdict + missing `OperatorRecoveryAction` renders the row without a recovery block.
  - String constants resolve (assert visible text matches the `STRINGS` object — sanity check that the component is using its own roster, not literal strings).
  - `onForceInstall` / `onInstallAnyway` / `onContinue` callback prop fires once when the corresponding button is clicked (`vi.fn()` + `fireEvent.click(screen.getByRole('button', { name: ... }))`; mirrors the `SunfishDialog.test.tsx` `onClose` pattern).

**Estimated effort:** 3–4h.

**PR title:** `feat(ui-adapters-react): SystemRequirements PreInstallFullPage component (W#56 P2)`

**Gate:** `cd packages/ui-adapters-react && pnpm typecheck && pnpm test && pnpm build` all clean; 6 new Vitest tests pass; the component is exported from the package barrel; visual smoke via the new Storybook story (see Phase 4) renders all three verdict cases.

---

### Phase 3 — `PostInstallInlineExplanation` + `PostInstallRegressionBanner` modes (~3–4h)

**What to build:**

- `packages/ui-adapters-react/src/components/SystemRequirements/SystemRequirementsInlinePanel.tsx` — collapsed-by-default `<details>` element rendering a single dimension's `DimensionEvaluation` inline. Mounts via `<SystemRequirementsInlinePanel result={result} dimension="Hardware" />`. Composable into any settings/about page.
- `packages/ui-adapters-react/src/components/SystemRequirements/SystemRequirementsRegressionBanner.tsx` — top-of-shell banner with `aria-live="assertive"` live-region pattern per WCAG 2.2 SC 4.1.3 + ADR 0034 `liveRegion: "assertive"` contract. Props: `{ result: SystemRequirementsResult; previousResult?: SystemRequirementsResult; onDismiss?: () => void }`. The banner renders only when `previousResult` is present AND a `Required` + `Pass` dimension in `previousResult` has flipped to `Required` + `Fail` in `result` (per ADR 0063 A1.8 explicit Informational rule — Informational dimension regressions are NOT banner-worthy).
- Update `SystemRequirements.tsx` to dispatch on the `mode` prop: `PreInstallFullPage` → existing implementation; `PostInstallInlineExplanation` → renders `<SystemRequirementsInlinePanel>` for each dimension in a flat list (consumer can also import the panel directly); `PostInstallRegressionBanner` → renders `<SystemRequirementsRegressionBanner>` with `previousResult` derived from a `result` history (this hand-off does NOT bake in a history store; the component takes the previous result as a prop and trusts the caller to manage history — caller-side state per React idiom + ADR 0014 framework-agnostic contracts).
- `packages/ui-adapters-react/src/hooks/useSystemRequirements.ts` — optional helper hook:
  - `useSystemRequirements(bundleId: string, platformKey: string): { result: SystemRequirementsResult | null; loading: boolean; error: Error | null; refresh: () => void }`.
  - Default impl uses `fetch('/api/system-requirements/{bundleId}?platform={platformKey}')` + the Phase 1 wire-format parser; the hook is opt-in (callers can pass `result` directly to the component if they have their own data flow). This is the minimal adapter-side hook for fetching the Phase 1 endpoint payload; not a full data-management story.
- Tests — 5 new Vitest tests (cumulative 11 with Phase 2):
  - `<SystemRequirementsInlinePanel>` toggles open on click (assert `<details open>` attribute).
  - `<SystemRequirementsRegressionBanner>` does NOT render when `previousResult` is undefined.
  - `<SystemRequirementsRegressionBanner>` renders when `Required`+`Pass` flips to `Required`+`Fail`.
  - `<SystemRequirementsRegressionBanner>` does NOT render when an `Informational` dimension flips (per ADR 0063 A1.8 explicit Informational rule).
  - `<SystemRequirementsRegressionBanner>` element has `aria-live="assertive"` attribute (`expect(banner).toHaveAttribute('aria-live', 'assertive')`).

**Estimated effort:** 3–4h.

**PR title:** `feat(ui-adapters-react): SystemRequirements PostInstall modes (inline + regression banner) (W#56 P3)`

**Gate:** `pnpm typecheck && pnpm test && pnpm build` clean; 5 new Vitest tests pass cumulatively; `aria-live="assertive"` confirmed; the dispatch logic in `SystemRequirements.tsx` covers all three `SystemRequirementsRenderMode` values.

---

### Phase 4 — Storybook stories + axe-core a11y harness wiring + WCAG baseline (~3–4h)

**What to build:**

- `packages/ui-adapters-react/src/components/SystemRequirements/SystemRequirements.stories.tsx` — Storybook stories:
  - `Default_Pass` — `result.overall === 'Pass'`, all 10 dimensions Pass.
  - `WarnOnly_Recommended_Fail` — `WarnOnly`; one Recommended dimension fails.
  - `Block_Required_Fail` — `Block`; one Required dimension fails; `forceInstall` button visible.
  - `Block_With_Recovery_Action` — `Block` + populated `operatorRecoveryAction`; recovery block rendered.
  - `Inline_Hardware_Panel` — `<SystemRequirementsInlinePanel dimension="Hardware">` standalone story.
  - `Regression_Banner_Hardware_Flip` — `<SystemRequirementsRegressionBanner>` story with prior-Pass / current-Fail Hardware dimension.
  - `Regression_Banner_Suppressed_Informational` — banner story that should NOT render (Informational dimension flip; story body shows the empty banner OR a "no banner expected" placeholder).
- All stories must include the standard `@storybook/addon-a11y` config (mirrors existing `SunfishButton.stories.tsx` cohort pattern); axe-core runs automatically against each story via the `tooling/a11y-audit-runner` Plan-5 CI gate.
- `packages/ui-adapters-react/src/components/SystemRequirements/SystemRequirements.a11y.test.tsx` — explicit a11y assertions complementing the Storybook gate. Use `axe-core` directly via `@axe-core/playwright` if it's already available, OR via in-process axe-core via Vitest (reuse the pattern from the closest existing a11y test in the repo; if none, the Storybook a11y addon + the audit-runner is sufficient — skip the .a11y.test.tsx in that case; the Phase 4 §Gate names the Storybook gate as the canonical a11y check). Three scenarios:
  - `PreInstallFullPage` — assert `liveRegion: "off"` for the full-page mode (no `aria-live` attribute on the page-level container).
  - `PostInstallInlineExplanation` — assert `liveRegion: "off"`; assert `<details>` toggle is reachable via Tab + Enter.
  - `PostInstallRegressionBanner` — assert `liveRegion: "assertive"`; assert `role="alert"` per WCAG 2.2 SC 4.1.3 status-message role.
- `apps/docs/foundation/wayfinder/wcag.md` — append a "**Per-adapter conformance: Bridge React (`@sunfish/ui-adapters-react`)**" sub-section pointing at the new Storybook stories + the SC mapping table; do NOT claim "conformant" — claim "**baseline established for `PreInstallFullPage` / `PostInstallInlineExplanation` / `PostInstallRegressionBanner` modes via Storybook + axe-core CI gate**" per the W#42 P4 + W#47 cohort precedent (cohort discipline: never claim conformance, always claim baseline).
- `packages/ui-adapters-react/README.md` — append a "Components" section line for `SystemRequirements` (mirrors the existing `SunfishButton` / `SunfishDataGrid` / `SunfishDialog` pattern; brief one-liner + link to the story file).

**Estimated effort:** 3–4h.

**PR title:** `feat(ui-adapters-react): SystemRequirements Storybook + WCAG 2.2 AA a11y baseline (W#56 P4)`

**Gate:** `pnpm build-storybook` clean; `node tooling/a11y-audit-runner/bin/run.mjs --shard 0 --total-shards 1` passes for the SystemRequirements stories (no `Serious` or `Critical` axe violations); `apps/docs/foundation/wayfinder/wcag.md` updated; `packages/ui-adapters-react/README.md` updated.

---

### Phase 5 — Adapter-parity matrix update + ledger flip + close (~30min)

**What to build:**

- `_shared/engineering/adapter-parity.md` — add `SystemRequirements` row to the parity matrix (per ADR 0014 strict-parity rule). The row reads: Blazor: ✓ (W#47 ships Anchor MAUI consumer; the Razor renderer is in `accelerators/anchor/Components/Pages/SystemRequirements.razor`, NOT in `packages/ui-adapters-blazor/` — which is the canonical site for the parity-matrix entry; flag this in the matrix as "consumer-tier (Anchor) — substrate parity at `Sunfish.Foundation.MissionSpace.ISystemRequirementsRenderer`"); React: ✓ (W#56 ships `@sunfish/ui-adapters-react`'s `SystemRequirements` component); parity status: "**substrate-parity** (both adapters consume the same `ISystemRequirementsRenderer` contract; per-adapter UX divergence is intentional per ADR 0014 platform-conventional layout exemption)."
- `icm/_state/workstreams/W56-w-42-follow-on-bridge-react-concrete-per-adapter-ui-surface.md` — flip status to `built` with the 5-PR list. NB: Per the per-workstream-files pattern (PRs #585+#588), edit this file directly + run `python3 tools/icm/render-ledger.py` + verify `--check` exits 0; do NOT hand-edit `icm/_state/active-workstreams.md`.
- `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_55_bridge_react_renderer_built.md` — memory entry summarizing the shipped scope (5 PRs + new types + a11y baseline + serialization contract + Bridge endpoint) for future-session auto-load.
- Optional: short note in `icm/_state/research-inbox/` (`cob-resumed-...md` if COB had previously gone idle on this) per CLAUDE.md inbox protocol.

**Estimated effort:** 30 minutes.

**PR title:** `chore(icm): flip W#56 Bridge React renderer row to built (P5)`

**Gate:** `tools/icm/render-ledger.py --check` exits 0; `_shared/engineering/adapter-parity.md` row exists; memory file written.

---

## Halt conditions

(Required halt conditions per ADR 0073 §4 plus workstream-specific items. Numbering aligned with the W#47 Anchor MAUI sibling cohort where applicable; React-specific items appended.)

1. **Required (per ADR 0073):** if any prerequisite listed in §1 is not yet `built` when its phase is needed → halt; write `cob-question-2026-05-XXTHH-MMZ-w56-renderer-{slug}.md` naming the unmet prerequisite. Specifically: verify on origin/main at session-start `git log --all --oneline -10` that ledger row 42 reads `built` AND that ADR 0063 + ADR 0065 + ADR 0030 + ADR 0014 + ADR 0031 + ADR 0006 + ADR 0049 + ADR 0034 all read Status: Accepted on origin/main (canonical via `gh api repos/ctwoodwa/Sunfish/contents/docs/adrs/...` `frontmatter.status` field, or via reading the `status:` line in each ADR's frontmatter directly).

2. **Required (per ADR 0073):** if the `icm/_state/workstreams/W56-*.md` file's `status:` field does not read `ready-to-build` when COB begins → halt; write `cob-question-*` naming the discrepancy.

3. **WCAG/a11y subagent pre-merge canonical (per ADR 0065 §Decision §7).** ADR 0065 mandates a WCAG/a11y subagent on **every** UI-bearing follow-on. Phases 2, 3, 4 all carry a UI surface; pre-merge council MUST include a WCAG/a11y perspective BEFORE any phase commit. This is non-negotiable — mirrors the W#42 substrate hand-off Phase 3a/4 cohort posture (council batting average 22-of-22; substrate cohort needed amendments on every UI-bearing phase). If the WCAG/a11y subagent flags an issue, halt the phase + apply mechanical amendments per Decision Discipline Rule 3 (auto-accept rename / fix-citation / scope-tightening); raise non-mechanical findings to XO via `cob-question-*`.

4. **Substrate-prereq verification — TypeScript projection contract is NEW.** No prior PR has authored a TypeScript projection of `SystemRequirementsResult` / `DimensionEvaluation` / the 5 enums. Phase 1 establishes that contract from scratch. If, at Phase 1 start, the substrate types' `[JsonPropertyName]` attributes have changed since this hand-off was authored (verify via `git log -p --since=2026-05-04 -- packages/foundation-mission-space/Models/Requirements.cs packages/foundation-mission-space/Models/RequirementsEnums.cs packages/foundation-mission-space/Models/EnvelopeChange.cs`), halt + raise `cob-question-*` — both this hand-off AND the W#47 sibling may need updates.

5. **No JSON projection at the resolver yet — first-of-kind boundary.** `IMinimumSpecResolver.EvaluateAsync` returns a `SystemRequirementsResult` in-process today. There is NO existing Bridge endpoint or REST-tier serialization shim for it. Phase 1 of this hand-off is the **first** crossing of that boundary. If, at Phase 1 commit time, `Sunfish.Bridge/Program.cs` already has a `MapSystemRequirementsEndpoints();` line (signaling another session has authored a parallel endpoint), halt + raise `cob-question-*`. (Negative-existence verification at Phase 1 start: `grep -rn "SystemRequirements" /accelerators/bridge/Sunfish.Bridge/` should return zero matches BEFORE Phase 1 commit; if non-zero, halt.)

6. **Audit double-emission discipline (per ADR 0049 immutability).** The React renderer **MUST NOT** call any audit-emission endpoint. Audit emission for `MinimumSpecEvaluated` / `InstallBlocked` / `InstallWarned` / `PostInstallSpecRegression` / `InstallForceEnabled` is the .NET resolver's responsibility (W#41 substrate). If any phase's implementation has the React component (or the `useSystemRequirements` hook) calling an audit endpoint, halt + raise `cob-question-*` — this is an architectural defect mirroring the ADR 0046 council finding pattern. The renderer is presentation-layer; no audit emission.

7. **JsonStringEnumConverter PascalCase wire-format alignment.** The C# substrate uses `JsonStringEnumConverter<TEnum>` which emits enum names as PascalCase (e.g., `"Pass"`, `"WarnOnly"`, `"Block"`). The TypeScript contract MUST use PascalCase string literals to match — NOT camelCase. If the .NET-side host configuration overrides `JsonStringEnumConverter` to a camelCase variant (e.g., `JsonNamingPolicy.CamelCase` applied to enum names) in `Program.cs:1-86` or any startup configuration, halt + raise `cob-question-*` — the wire format must be consistent across both adapters or the TypeScript contract becomes a source of bugs. (Verify at Phase 1 start: `grep -rn "AddJsonOptions\|JsonNamingPolicy\|JsonStringEnumConverter" /accelerators/bridge/Sunfish.Bridge/Program.cs` for the active configuration.)

8. **Multi-tenant composition (per ADR 0031).** Bridge serves multiple tenants concurrently per ADR 0031. The Phase 1 endpoint MUST scope `IMinimumSpecResolver` resolution to the active tenant — Bridge's existing tenant-context middleware (verify on origin/main; likely `TenantContextMiddleware.cs` or similar in `accelerators/bridge/Sunfish.Bridge/Middleware/`) flows the active tenant via DI, and `IMinimumSpecResolver` must be registered scoped (NOT singleton) to inherit that context. If `IMinimumSpecResolver` is registered singleton in Bridge's DI on origin/main, halt + raise `cob-question-*` — singleton registration would leak a tenant's evaluation across tenants. (Default per W#41 cohort precedent: `IMinimumSpecResolver` is registered transient OR scoped at `AddSunfishMissionSpace()`; verify via `Sunfish.Foundation.MissionSpace/MissionSpaceServiceCollectionExtensions.cs` source on origin/main BEFORE Phase 1 commit.)

9. **Adapter-parity matrix entry must reference the existing Anchor MAUI consumer.** Per ADR 0014 strict-parity rule, the parity matrix entry for `SystemRequirements` must reference both adapters. The Anchor MAUI side is **not in `packages/ui-adapters-blazor/`** — it's an Anchor-tier consumer in `accelerators/anchor/Components/Pages/SystemRequirements.razor`. Phase 5's matrix update must surface this as a "consumer-tier (Anchor) — substrate parity at `Sunfish.Foundation.MissionSpace.ISystemRequirementsRenderer`" entry, not a "missing Blazor adapter" entry. If the matrix already has a misleading "missing Blazor" claim, halt + raise `cob-question-*` (this is XO-decision territory; CO may want a `packages/ui-adapters-blazor/Components/SystemRequirements.razor` shim for parity-cohort discipline, OR may decline given the Anchor-tier consumer + ADR 0014 platform-conventional exemption suffices).

---

## Acceptance criteria (cumulative)

- [ ] `packages/ui-adapters-react/src/contracts/SystemRequirements.ts` exists with all 6 string-literal enum unions + 4 interfaces matching the verified C# substrate.
- [ ] `packages/ui-adapters-react/src/components/SystemRequirements/` directory exists with `SystemRequirements.tsx` + `SystemRequirementsDimensionRow.tsx` + `SystemRequirementsInlinePanel.tsx` + `SystemRequirementsRegressionBanner.tsx` + `SystemRequirements.strings.ts` + `index.ts`.
- [ ] `packages/ui-adapters-react/src/index.ts` re-exports the new component + types.
- [ ] `accelerators/bridge/Sunfish.Bridge/Features/SystemRequirements/SystemRequirementsEndpoints.cs` exists with `MapSystemRequirementsEndpoints` extension; `Program.cs` calls it.
- [ ] `cd packages/ui-adapters-react && pnpm typecheck && pnpm test && pnpm build` all clean.
- [ ] `dotnet build -c Release` clean for `Sunfish.Bridge.csproj` (Phase 1 endpoint).
- [ ] All new tests pass: 4 (P1 Bridge endpoint) + 6 (P2 Vitest) + 5 (P3 Vitest cumulative) = 15 cumulative tests, plus all existing Bridge + ui-adapters-react tests still green.
- [ ] All 7 Storybook stories under `SystemRequirements.stories.tsx` render under `pnpm build-storybook`.
- [ ] WCAG 2.2 AA + EN 301 549 v3.2.1 baseline established for `PreInstallFullPage` + `PostInstallInlineExplanation` + `PostInstallRegressionBanner` modes per the `tooling/a11y-audit-runner` axe-core CI gate (no `Serious` or `Critical` violations).
- [ ] No audit-emission call from any React component or hook (no fetch to `/api/audit/*`; no direct invocation of an audit endpoint).
- [ ] `apps/docs/foundation/wayfinder/wcag.md` appended with "Per-adapter conformance: Bridge React" sub-section.
- [ ] `packages/ui-adapters-react/README.md` appended with `SystemRequirements` component entry.
- [ ] `_shared/engineering/adapter-parity.md` parity-matrix row added per Halt-condition #9 guidance.
- [ ] WCAG/a11y subagent pre-merge council fired on every UI-bearing phase (P2, P3, P4) per ADR 0065 §Decision §7; mechanical amendments applied per Decision Discipline Rule 3; non-mechanical findings raised to XO via `cob-question-*`.
- [ ] `icm/_state/workstreams/W56-*.md` `status:` field reads `built` with PR list and PR URLs; `tools/icm/render-ledger.py --check` exits 0.
- [ ] Memory entry written at `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_55_bridge_react_renderer_built.md`.

---

## Total decomposition table

| Phase | Subject | Hours | PR title prefix |
|---|---|---|---|
| 1 | Serialization contract + Bridge endpoint | 3–4 | `feat(bridge,ui-adapters-react): SystemRequirements JSON contract + Bridge endpoint (W#56 P1)` |
| 2 | `PreInstallFullPage` React component + DimensionEvaluation rows | 3–4 | `feat(ui-adapters-react): SystemRequirements PreInstallFullPage component (W#56 P2)` |
| 3 | `PostInstallInlineExplanation` + `PostInstallRegressionBanner` modes | 3–4 | `feat(ui-adapters-react): SystemRequirements PostInstall modes (inline + regression banner) (W#56 P3)` |
| 4 | Storybook stories + axe-core a11y harness + WCAG baseline | 3–4 | `feat(ui-adapters-react): SystemRequirements Storybook + WCAG 2.2 AA a11y baseline (W#56 P4)` |
| 5 | Adapter-parity matrix update + ledger flip + close | 0.5 | `chore(icm): flip W#56 Bridge React renderer row to built (P5)` |
| **Total** | | **12.5–16.5h** | **5 PRs** |

(The §1 `Estimated effort` range "14–19h" widens the table sum by ~+15% to absorb the WCAG/a11y subagent council loop on each UI-bearing phase, which the per-phase hours do not explicitly bake in. Per ADR 0073 estimate-honesty rule: §1 wins on disagreement; the table is the sub-budget. If the council loop runs >2× per phase, halt + escalate per halt-condition #3.)

---

## Cohort patterns to follow

This hand-off mirrors the patterns established by the W#33-derived substrate cohort (W#34 / W#35 / W#36 / W#37 / W#39 / W#40 / W#41 / W#42) plus the Anchor MAUI sibling W#47 plus the Wave 3.5 React adapter scaffolding cohort (PoC: SunfishButton / SunfishDataGrid / SunfishDialog). Specifically:

- **`useCssProvider()` provider context.** Inherits from the Wave 3.5 adapter scaffolding; new components consume the existing `CssProviderContext` + the `ICssProvider` interface; no new provider methods authored by this hand-off (system-requirements components use existing button/dialog provider methods + plain CSS for verdict-banner colors).
- **String constants in a co-located `.strings.ts` file.** Mirrors the existing PoC component pattern (no `IStringLocalizer` equivalent in React; constants are TypeScript top-of-file objects; locale-completeness is a separate cadence).
- **PascalCase wire-format alignment with C# `JsonStringEnumConverter`.** The TypeScript contract MUST use PascalCase string-literal unions for every enum (verbatim cohort discipline; `JsonStringEnumConverter` is the canon on the C# side per the W#42 substrate's `RequirementsEnums.cs` `[JsonConverter(typeof(JsonStringEnumConverter<TEnum>))]` attribute).
- **Vitest + RTL test conventions.** Mirrors `SunfishDialog.test.tsx`: `vi.fn()` for callbacks, `screen.getBy*` for queries, `fireEvent.*` for interactions, no NSubstitute (NSubstitute is the .NET-side cohort; React side uses Vitest's built-in `vi.fn()` per Decision Discipline Rule 5 industry-default for the platform).
- **Storybook + axe-core for a11y.** Inherits from Wave global-ux Plan 5 CI gate (`tooling/a11y-audit-runner`); no new a11y harness package authored.
- **`MapXxxEndpoints` Bridge cohort pattern.** Phase 1's Bridge endpoint follows `MapListingsEndpoints` / `MapFieldEndpoints` shape (verbatim cohort precedent; minimal-API `app.MapGet` / `app.MapPost`).
- **No audit emission from presentation layer.** Inherited from W#42 substrate's audit-by-construction discipline + ADR 0049 immutability (cohort discipline shared with W#47).
- **Pre-merge council canonical for UI-bearing phases.** Per `feedback_council_before_automerge.md` + ADR 0065 §Decision §7 — council fires BEFORE PR creation, not in parallel with auto-merge. Mirrors W#42 P3a + P3b + P4 + W#47 cohort discipline.
- **`apps/docs/foundation/wayfinder/wcag.md` baseline-not-conformance language.** Inherited from W#42 P4 + W#47 cohort; never claim "WCAG conformant" — claim "baseline established for [scope]."
- **Per-workstream-files pattern for ledger.** Per CLAUDE.md update note (PRs #585+#588): edit `icm/_state/workstreams/W56-*.md` directly + run `python3 tools/icm/render-ledger.py`; do NOT hand-edit `icm/_state/active-workstreams.md`. CI check enforces.

---

## Open questions

These are explicitly punted by the authoring spec or anticipated by XO; COB resolves via beacon if encountered:

1. **`MinimumSpec` source at endpoint time.** The Bridge endpoint needs to know which bundle's `MinimumSpec` to evaluate. Phase 1 ships with a route param (`/api/system-requirements/{bundleId}`) and assumes a `IMinimumSpecProvider` (or equivalent) resolves bundle ID → spec on the .NET side. If no such provider exists yet on origin/main, halt + raise `cob-question-*` — the answer may be "stub a `MinimumSpecLookupStub` that returns a hardcoded spec for kitchen-sink demo purposes; real provider lands in the Wayfinder Atlas integration phase (out of scope here)." (Mirrors the W#47 Anchor MAUI sibling Open Question #1.)

2. **Force-install audit shape.** The `force_install` button in the React component issues a POST to `/api/system-requirements/{bundleId}/force-install`; the request body is `InstallForceEnableRequest` per ADR 0063-A1.11 + W#41 substrate. If the request shape requires fields the renderer cannot supply at the UI level (e.g., a justification text field that ADR 0063 requires for audit), halt + raise `cob-question-*` — the answer may be "render a textbox bound to `request.reason`; assert non-empty on form submit; mirror the W#47 sibling's force-install affordance pattern."

3. **Reduced-motion preference.** ADR 0034's `reducedMotion: "respects"` contract requires components to honor `prefers-reduced-motion` media query. If any animation is added (e.g., `<details>` panel's expand/collapse transition, or banner slide-in), gate the CSS behind `@media (prefers-reduced-motion: no-preference)`. The Storybook a11y addon does NOT auto-test this; the WCAG/a11y subagent pre-merge council per Halt-condition #3 must verify visually.

4. **RTL icon mirror policy on status icons.** Status icons (✓ / ⚠ / ✗) are non-directional per Unicode + ADR 0034's `rtlIconMirror: "non-directional"` default; verify via the WCAG/a11y subagent that no directional icon (e.g., chevron) is used for status. Use of `<` / `>` chevrons would require `rtlIconMirror: "mirrors"` — halt if discovered.

5. **React 18 vs 19 peer-dep consideration.** `packages/ui-adapters-react/package.json` declares `"react": "^18.0.0 || ^19.0.0"` peer deps. Phase 2's component MUST work under both — no React-19-only APIs (e.g., `use()` hook, Server Components, `useFormState`); Phase 2 sticks to React 18 idioms (`useEffect`, `useState`, plain functional components). If a future revision wants React-19-only features, that's a separate ADR.

6. **`useSystemRequirements` hook's fetch story.** Phase 3 ships a thin `useSystemRequirements(bundleId, platformKey)` hook that uses `fetch`. If the consumer wants integration with React Query, SWR, or RTK Query, that's an opt-in adapter layer the hook does NOT prescribe; the hook is the **minimal** opt-in helper — the component itself takes `result` as a prop and is data-source-agnostic. Document this in the hook's JSDoc.

---

## §A0 self-audit — three-direction structural-citation discipline

Per `feedback_council_can_miss_spot_check_negative_existence.md` + the cohort batting average 22-of-22, three-direction discipline applied during authoring:

**Positive-existence (17 cited Sunfish.* symbols verified on origin/main):** `ISystemRequirementsRenderer` (`packages/foundation-mission-space/Services/ISystemRequirementsRenderer.cs:31`); `ISystemRequirementsSurface` (same file:51); `SystemRequirementsResult` (`Models/Requirements.cs:263`); `DimensionEvaluation` (:241); `OperatorRecoveryAction` (:231); `MinimumSpec` (:31); `SystemRequirementsRenderMode` (`Models/RequirementsEnums.cs:50`); `OverallVerdict` (:20); `DimensionPolicyKind` (:33); `DimensionPassFail` (:42); `SpecPolicy` (:7); `IMinimumSpecResolver`; `IInstallForceEnableSurface`; `packages/ui-adapters-react/` (Vite+Vitest+Storybook+RTL per ADR 0030); PoC components `SunfishButton`/`SunfishDataGrid`/`SunfishDialog`; `tooling/a11y-audit-runner/`; Bridge `MapListingsEndpoints`+`MapFieldEndpoints` cohort in `Program.cs:145,150`.

**Negative-existence (no parallel work):** `gh pr list --state open` zero matches for Bridge React renderer; `grep -rn "SystemRequirements" /accelerators/bridge/Sunfish.Bridge/` zero matches (no existing endpoint); `grep -rn "SystemRequirements" /packages/ui-adapters-react/` zero matches (no TypeScript projection); `W56-*.md` does not exist on origin/main (W#54 soft-reserved by W#53 for Phase 3 Atlas implementations; W#56 used here).

**Structural-citation (signatures match):** `ISystemRequirementsRenderer.RenderAsync(SystemRequirementsResult, ISystemRequirementsSurface, SystemRequirementsRenderMode, CancellationToken)` matches source verbatim; `SystemRequirementsResult.[JsonPropertyName]` attributes (`overall`/`dimensions`/`operatorRecoveryAction`/`evaluatedAt`) and `DimensionEvaluation.[JsonPropertyName]` (`dimension`/`policy`/`outcome`/`operatorRecoveryAction`/`detail`) match the Phase 1 TypeScript interface; `OverallVerdict` values (`Pass`, `WarnOnly`, `Block`) match the TypeScript union; every enum carries `[JsonConverter(typeof(JsonStringEnumConverter<...>))]` confirming PascalCase wire format (Halt #7 enforces); ADR 0014 strict-parity policy + ADR 0030 PoC scope (Button+DataGrid+Dialog) — `SystemRequirements` is the 4th React component, justified by W#42 per-adapter mandate + W#47 sibling cohort.

---

## References

- [ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md) — Bridge is the SaaS shell
- [ADR 0014](../../docs/adrs/0014-adapter-parity-policy.md) — UI Adapter Parity Policy (Blazor ↔ React)
- [ADR 0030](../../docs/adrs/0030-react-adapter-scaffolding.md) — React Adapter Scaffolding (this hand-off's PoC-cohort parent)
- [ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) — Bridge as Hybrid Multi-Tenant SaaS (Zone C)
- [ADR 0034](../../docs/adrs/0034-a11y-harness-per-adapter.md) — per-adapter a11y harness
- [ADR 0049](../../docs/adrs/0049-audit-trail-substrate.md) — audit-trail substrate (renderer MUST NOT emit)
- [ADR 0063](../../docs/adrs/0063-mission-space-requirements.md) — Mission Space Requirements (substrate spec for `ISystemRequirementsRenderer`)
- [ADR 0065](../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md) — Wayfinder System + Standing Order Contract (W#42 substrate; §Decision §7 WCAG mandate)
- [ADR 0073](../../docs/adrs/0073-stage06-handoff-template-contract.md) — Stage-06 hand-off template contract (this hand-off conforms)
- W#41 hand-off: [`foundation-mission-space-requirements-stage06-handoff.md`](./foundation-mission-space-requirements-stage06-handoff.md) — substrate cohort precedent
- W#42 substrate hand-off: [`foundation-wayfinder-stage06-handoff.md`](./foundation-wayfinder-stage06-handoff.md) — direct parent; cohort patterns + DI naming + a11y baseline language
- W#47 sibling per-adapter hand-off: [`foundation-wayfinder-anchor-maui-renderer-stage06-handoff.md`](./foundation-wayfinder-anchor-maui-renderer-stage06-handoff.md) — Anchor MAUI sibling; phase shape + halt-conditions + audit-double-emission discipline + force-install delegation
- Substrate code on origin/main: `packages/foundation-mission-space/Services/ISystemRequirementsRenderer.cs` + `packages/foundation-mission-space/Models/Requirements.cs` + `packages/foundation-mission-space/Models/RequirementsEnums.cs` + `packages/foundation-mission-space/Models/EnvelopeChange.cs` (`DimensionChangeKind`)
- Existing tests precedent: `packages/foundation-mission-space/tests/SystemRequirementsRendererTests.cs` (substrate-level interface contract tests)
- React adapter scaffolding: `packages/ui-adapters-react/src/components/SunfishButton/` + `SunfishDataGrid/` + `SunfishDialog/` (PoC cohort patterns: `*.tsx` + `*.test.tsx` + `*.stories.tsx` + `index.ts`)
- A11y harness substrate: `tooling/a11y-audit-runner/` (Storybook + axe-core CI gate; per Plan 5 Wave global-ux)
- Bridge endpoint cohort: `accelerators/bridge/Sunfish.Bridge/Listings/ListingsEndpoints.cs` + `accelerators/bridge/Sunfish.Bridge/Field/FieldEndpoints.cs` (verify exact paths via `grep -rn "MapListingsEndpoints" /accelerators/bridge/Sunfish.Bridge/`)

---

## Notes for COB

- This is a **per-adapter UI** hand-off (presentation-layer); the substrate (W#42 + W#41) is shipped. No new substrate types; no audit emission from this layer. The hand-off DOES author a new wire-format contract (Phase 1) — that is presentation-tier infrastructure, not substrate.
- This is the **second** per-adapter renderer in the W#42 follow-on chain (W#47 Anchor MAUI was first, shipped 2026-05-04 as PR #555). The phase shape mirrors W#47 verbatim except for Phase 1 (which W#47 does not need because MAUI Blazor Hybrid is in-process).
- Pre-merge council canonical: dispatch the standard 4-perspective council subagent + a WCAG/a11y subagent BEFORE any UI-bearing phase commit (P2, P3, P4). Cohort batting average is 22-of-22 — every UI-bearing substrate has needed council fixes; pre-merge is dramatically cheaper than post-merge per `feedback_council_before_automerge.md`.
- §A0 self-audit pattern is necessary but NOT sufficient for cited Sunfish.* symbols. Spot-check three directions per `feedback_council_can_miss_spot_check_negative_existence.md`: (1) negative-existence — does the cited symbol exist on origin/main? (2) positive-existence — does the cited namespace match? (3) structural-citation — do the cited fields / signatures match the actual file? The council's 22-of-22 batting average is a useful tail.
- If COB hits a halt-condition or has a design question, file `cob-question-2026-05-XXTHH-MMZ-w56-renderer-{slug}.md` in `icm/_state/research-inbox/` + halt the workstream + add a note in the per-workstream file (`icm/_state/workstreams/W56-*.md` `## Notes` section) + ScheduleWakeup 1800s.
- After Phase 5 closes the row, drop `cob-resumed-2026-05-XXTHH-MMZ-w56-renderer-built.md` to research-inbox if XO had a beacon waiting; otherwise continue with the rung-1/rung-2 fallback per CLAUDE.md fallback work order.
