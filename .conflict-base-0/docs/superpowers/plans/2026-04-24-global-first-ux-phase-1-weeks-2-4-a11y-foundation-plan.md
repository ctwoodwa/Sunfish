# Global-First UX — Phase 1 Weeks 2-4 A11y Foundation Cascade

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cascade the Week-1 Storybook accessibility harness pattern from the 3 pilot components to the full `ui-core` inventory (~40 components), stand up the `ui-adapters-blazor.A11y` bUnit-to-axe bridge project mandated by ADR 0034, cascade the harness to `ui-adapters-react`, and land the production `@storybook/test-runner` axe-injection hook so every component has a machine-enforced accessibility contract by the Week-4 gate.

**Architecture:** Two parallel Week-2 workstreams — Workstream A builds the highest-risk engineering item (the bUnit-to-axe bridge) in isolation so its failure does not block the Node-side cascade; Workstream B lands the production test-runner hook and CVD/RTL matrix expansion on `ui-core`. Week 3 runs the cascade across `ui-core` and mirrors it into `ui-adapters-react`. Week 4 adds polish (screen-reader audit runbook, reduced-motion variants, `SUNFISH_A11Y_001` analyzer) and validates end-to-end that all three adapters emit green axe runs for the 3 pilot components at minimum, with the `ui-core` inventory green as the stretch.

**Tech stack:** Storybook 8.x + `@storybook/addon-a11y`, `@storybook/test-runner` v0.19+, `@axe-core/playwright` 4.10+, Web Test Runner + Playwright 1.59.1 (chromium headless), Vitest for React adapter, bUnit 1.31+ on .NET 11 preview, Playwright-dotnet 1.59+ for the bridge's real-browser host, xUnit, Roslyn analyzer SDK (`Microsoft.CodeAnalysis.CSharp.Workspaces`) and `ts-eslint` for the `SUNFISH_A11Y_001` companion analyzer, Chrome DevTools Protocol emulation for CVD + prefers-reduced-motion.

**Scope boundary:** This plan covers Phase 1 Weeks 2-4 ONLY (15 business days). It does NOT cover:
- `ui-core` component *authoring* beyond the 3 Week-1 pilots — this plan cascades the harness onto components that already exist or are being landed by other workstreams; new component authoring is tracked separately.
- CI gate wiring (required-status-check enforcement, `.github/workflows/global-ux-gate.yml`, matrix sharding configuration) — that is Plan 5.
- Localization wrapper injection into components — that is Plan 2 (loc-infra cascade). Plan 4 and Plan 2 touch separate files and can run fully in parallel.
- Phase 2 screen-reader audit on non-pilot components — Plan 4 lands the runbook and performs pilot-component re-verification; fleet audits roll up in Plan 6.

**Parent spec:** [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../specs/2026-04-24-global-first-ux-design.md) §6, §7
**Predecessor plan:** [`2026-04-24-global-first-ux-phase-1-week-1-plan.md`](./2026-04-24-global-first-ux-phase-1-week-1-plan.md) (complete — GO verdict 2026-04-24)
**Parallel plan (orthogonal, no file overlap):** [`2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md`](./2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md)
**Follow-on plan:** Plan 5 (CI Gates) — wires Plan 4's harnesses into required-status-check mode.
**Key ADR:** [`docs/adrs/0034-a11y-harness-per-adapter.md`](../../adrs/0034-a11y-harness-per-adapter.md)

---

## Context & Why

Week 1 validated the Storybook + `@axe-core/playwright` harness on 3 pilot Web Components and measured runtime at ~2 s/scenario under production axe-injection, forecasting ~12 minutes on 4-shard parallelization for the full `ui-core` inventory (see [`waves/global-ux/week-1-runtime-measurement.md`](../../../waves/global-ux/week-1-runtime-measurement.md)). The Week-1 go/no-go gate passed GREEN, unlocking cascade.

Weeks 2-4 turn the pilot into production infrastructure: every `ui-core` component has a `*.stories.ts` file with a `parameters.a11y.sunfish` contract block; every contract is exercised across the 36-scenario matrix (3 themes × 2 light/dark × 2 LTR/RTL × 3 CVD simulations) on every PR; the `ui-adapters-blazor.A11y` bridge project exists, renders bUnit fragments into a real browser, and returns zero axe violations for the 3 pilot components; the React adapter runs stock Storybook-for-React against the same `parameters.a11y.sunfish` contract. Week 4 adds the screen-reader audit runbook (NVDA/Firefox and VoiceOver/Safari pairings), reduced-motion variants per §6, and the `SUNFISH_A11Y_001` analyzer that warns when a component file has no sibling `*.stories.ts`.

The bUnit-to-axe bridge is the single highest-risk engineering item in the cascade. ADR 0034 explicitly calls it out as new engineering, and the spec's FAILED condition §3 permits skipping the Blazor a11y gate as a named fallback if the bridge cannot ship in Week 2. This plan schedules the bridge as Workstream A so it can fail fast without blocking the Node-side cascade.

---

## Success Criteria

### PASSED — proceed to Plan 5 (CI Gates)

- All ~40 `ui-core` components have a sibling `*.stories.ts` file with a valid `parameters.a11y.sunfish` contract block conforming to the ADR 0034 shape.
- `pnpm --filter @sunfish/ui-core test:a11y` runs green across the 36-scenario matrix for all ~40 components; p95 wall time ≤ 15 minutes on 4-shard parallelization per the Week-1 runtime budget.
- `ui-adapters-blazor.A11y` bridge project exists at `packages/ui-adapters-blazor.A11y/`, renders bUnit `IRenderedFragment` output into a Playwright-hosted HTML page, and returns zero axe violations at impact ≥ moderate for the 3 pilot components (`sunfish-button`, `sunfish-dialog`, `sunfish-syncstate-indicator`).
- `ui-adapters-react` Storybook configuration imports `ui-core` stories where semantics are identical; React-only wrappers (e.g., controlled-form patterns) have their own stories. `pnpm --filter @sunfish/ui-adapters-react test:a11y` green on the 3 pilots.
- `@storybook/test-runner` `postVisit` hook invokes `@axe-core/playwright` AND Sunfish-specific assertions from `parameters.a11y.sunfish` (`focus.initial`, `focus.trap`, `keyboardMap`, `directionalIcons` — `expectFocusTrapped`, `expectKeyboardMap`, `expectIconMirroredInRtl` helpers exported from `ui-core/src/test-helpers/`).
- Screen-reader audit runbook published at `waves/global-ux/a11y-screen-reader-runbook.md`; the 3 pilot components re-verified on NVDA-2026.1/Firefox-126 AND VoiceOver-macOS15/Safari-17 within the last 14 days; audit log entries present in each pilot's `screenReaderAudit` contract block.
- Reduced-motion variants (§6): every component declaring a `transition` or `animation` CSS property has a Storybook variant running with `data-motion="reduced"` on host AND a test asserting no animation resolves; kitchen-sink settings switcher smoke-tested.
- `SUNFISH_A11Y_001` analyzer emits Warning (→Error in Phase 2) on any `packages/ui-core/src/components/*/` `.ts` file lacking a sibling `*.stories.ts`; Roslyn twin emits `SUNFISH_A11Y_001` on `packages/ui-adapters-blazor/Components/*/*.razor` lacking a sibling `*.stories.ts` pointer.

### FAILED — triggers a scope cut, not a Phase 1 abort

- bUnit-to-axe bridge cannot ship in Week 2: named fallback is the ADR 0034 Option B — skip the Blazor a11y build gate, publish the accessibility-debt register alongside each release, defer bridge to Phase 2. Plan 4 still passes on `ui-core` + React coverage alone.
- `ui-core` cascade cannot reach 40 components by Week-3 end because a cluster (e.g., complex composite components like `sunfish-data-grid`) lacks a clean story-authoring seam: document as deferred-cascade, scoped to Plan 6; Week-3 partial-success requires ≥ 30 of ~40 components green.
- Production axe-injection runtime exceeds 2.5 s/scenario such that 4-shard parallelization breaks the 15-minute CI budget: fallback per spec §7 — either expand to 8-shard or move CVD × 3 simulation axis off the per-commit matrix into a nightly job (scenario count drops 3×).
- Screen-reader audit for one of the 3 pilot components fails (real bug found): do not treat as Plan 4 failure — fix the bug, re-audit, log decision in `decisions.md`. Only fail the plan if the audit runbook itself is unworkable (e.g., no reviewer with access to matching NVDA version).
- `SUNFISH_A11Y_001` false-positive rate > 5% on existing internal-only files: tune the rule (narrow to `packages/ui-core/src/components/` only, exempt `src/test-helpers/`, `src/internal/`) or demote to Suggestion severity until tuning lands in Plan 5.

### Kill trigger (30-day timeout)

If Plan 4 has not landed all PASSED criteria by **2026-05-24** (30 days from Week-1 GO), escalate to BDFL for scope cut: named options are (a) drop the bUnit bridge per ADR 0034 Option B, ship `ui-core` + React only; (b) cut CVD simulation axis from per-commit matrix to nightly; (c) defer reduced-motion variant auditing to Plan 6, keep just the per-component `data-motion="reduced"` smoke test.

---

## Assumptions & Validation

| Assumption | VALIDATE BY | IMPACT IF WRONG |
|---|---|---|
| bUnit 1.31+ `IRenderedFragment.Markup` serialization is deterministic and includes all shadow-DOM content emitted by Sunfish's Lit-backed Web Component wrappers | Task 1.3 — snapshot test against 3 pilot components' bUnit output, diff against Storybook-for-web output | If non-deterministic or shadow-DOM content stripped, bridge cannot assert on shadow-internal content; fallback is ADR 0034 Option B (skip Blazor gate) |
| `@axe-core/playwright` runs correctly against a Playwright page loaded with `setContent()` (in-memory HTML, no real origin) | Task 1.4 — bridge integration test renders bUnit output via `page.setContent(...)` and runs axe | If axe requires a real origin for some rules (cross-origin iframe, service-worker), scope bridge to single-document components; dialog/overlay cases may fail |
| Production axe-injection runtime stays ≤ 2.5 s/scenario at full-matrix load (36 scenarios per component × 40 components = 1,440 scenarios) | Task 2.4 — re-measure on 5 representative components after production `postVisit` hook lands | If > 2.5 s, 4-shard parallelization breaks the 15-min CI budget; fallback is 8-shard or CVD-to-nightly |
| Chrome DevTools Protocol CVD emulation (`Emulation.setEmulatedVisionDeficiency`) is stable under Playwright headless chromium 1.59.1 | Task 2.5 — smoke test 3 pilots under all 3 CVD modes, capture screenshot, visual-diff for expected shifts | If unstable or missing, fallback is `chromatic-sh` CSS filter overlay (lower fidelity but deterministic) |
| NVDA-2026.1 is available on the a11y lead's test hardware and matches the version string declared in the Week-1 pilots' contract blocks | Task 3.1 — a11y lead captures `nvda --version` output in the runbook before re-auditing | Version drift means the 12-month audit freshness rule in §7 fails; fix is either upgrade audit entry's version string or get access to NVDA 2026.1 |
| `SUNFISH_A11Y_001` analyzer does not false-positive on `ui-core/src/test-helpers/`, `src/internal/`, `src/index.ts` barrel files, or non-component `.ts` files | Task 4.3 — run analyzer across existing `ui-core/src/` after cascade; measure false-positive rate | If > 5%, narrow rule to `src/components/*/` with filename matching `<kebab-name>.ts`; exempt barrel files |
| Reduced-motion CSS assertion via `getComputedStyle(el).animation === 'none 0s ease 0s 1 normal none running'` is reliable across chromium | Task 4.2 — test against 3 pilot components' reduced-motion variants | If non-deterministic, switch to checking `transition-duration` ≤ 10 ms and `animation-duration` ≤ 10 ms as the criterion |

---

## File Structure (Weeks 2-4 deliverables)

```
packages/ui-core/
  .storybook/
    test-runner.ts                                          ← Production postVisit hook (Task 2.1)
    preview.ts                                              ← CVD + RTL + theme decorators (Task 2.2)
    main.ts                                                 ← Adds a11y addon CVD mode config (Task 2.5)
  src/
    test-helpers/
      expectFocusTrapped.ts                                 ← Focus-trap assertion (Task 2.3)
      expectKeyboardMap.ts                                  ← Keyboard-map assertion (Task 2.3)
      expectIconMirroredInRtl.ts                            ← Directional-icon assertion (Task 2.3)
      expectReducedMotionRespected.ts                       ← Reduced-motion assertion (Task 4.2)
      index.ts                                              ← Barrel export
    components/<each-of-~40-components>/
      <component>.stories.ts                                ← Cascade target (Task 3.2)
      <component>.reduced-motion.stories.ts                 ← §6 reduced-motion variants where applicable (Task 4.2)

packages/ui-adapters-react/
  .storybook/
    main.ts                                                 ← Imports ui-core stories (Task 3.4)
    test-runner.ts                                          ← Mirrors ui-core test-runner (Task 3.4)
  src/
    test-helpers/                                           ← Re-exports from ui-core (Task 3.4)
    components/<react-only-wrappers>/
      <component>.stories.tsx                               ← React-specific stories (Task 3.4)

packages/ui-adapters-blazor.A11y/                           ← NEW PROJECT (Task 1.1)
  Sunfish.UIAdapters.Blazor.A11y.csproj
  BunitToAxeBridge.cs                                       ← Core bridge implementation (Task 1.2)
  SunfishA11yAssertions.cs                                  ← Focus/keyboard/RTL assertions in .NET (Task 1.5)
  ContractReader.cs                                         ← Reads parameters.a11y.sunfish JSON export (Task 1.6)
  PlaywrightPageHost.cs                                     ← Playwright-dotnet page lifecycle (Task 1.2)
  README.md                                                 ← Bridge architecture + debug runbook
  tests/
    Sunfish.UIAdapters.Blazor.A11y.Tests.csproj
    BunitToAxeBridgeTests.cs                                ← Against 3 pilot components (Task 1.7)
    PilotComponentA11yTests.cs                              ← End-to-end pilot assertions (Task 1.7)

packages/analyzers/accessibility/                           ← NEW analyzer sub-package (Task 4.3)
  Sunfish.Analyzers.Accessibility.csproj
  ComponentMissingStoryAnalyzer.cs                          ← SUNFISH_A11Y_001 (Razor side)
  tests/
    Sunfish.Analyzers.Accessibility.Tests.csproj
    ComponentMissingStoryAnalyzerTests.cs

tooling/eslint-plugin-sunfish-a11y/                         ← NEW ts-eslint plugin (Task 4.3)
  package.json
  src/rules/component-missing-story.ts                      ← SUNFISH_A11Y_001 (TS side)
  tests/component-missing-story.test.ts

waves/global-ux/
  week-2-bunit-bridge-report.md                             ← Task 1.8 output
  week-2-production-axe-hook-report.md                      ← Task 2.6 output
  week-3-cascade-coverage-report.md                         ← Task 3.5 output
  a11y-screen-reader-runbook.md                             ← Task 4.1 output
  week-4-integration-report.md                              ← Task 4.5 output
```

---

## Week 2 — Infrastructure (two parallel workstreams)

### Workstream A: `ui-adapters-blazor.A11y` bUnit-to-axe bridge — HIGHEST RISK

> **Risk note:** This workstream is scheduled first and in isolation so that failure does not cascade to Workstream B. The named fallback is ADR 0034 Option B (skip Blazor a11y gate, publish accessibility-debt register). Decision point: end-of-day-Thursday of Week 2. If the bridge cannot render at least one pilot component through to a green axe run by then, invoke fallback and re-scope Workstream A to Plan 6.

#### Task 1.1: Scaffold the bridge project

**Files:**
- Create: `packages/ui-adapters-blazor.A11y/Sunfish.UIAdapters.Blazor.A11y.csproj`
- Create: `packages/ui-adapters-blazor.A11y/tests/Sunfish.UIAdapters.Blazor.A11y.Tests.csproj`
- Modify: `Directory.Packages.props` — pin `Microsoft.Playwright`, `bunit`, `xunit`, `Microsoft.NET.Test.Sdk` versions centrally.

**Why:** ADR 0034 mandates a new project. Keeping it as a sibling to `ui-adapters-blazor` (not inside it) prevents the bridge's test dependencies from leaking into the adapter's nuget surface.

- [ ] **Step 1:** Target `net11.0`. PackageReferences: `bunit` (1.31+), `Microsoft.Playwright` (1.59.1 — match Node side for parity), `Microsoft.Extensions.Configuration.Json`, `System.Text.Json`.
- [ ] **Step 2:** Tests project PackageReferences: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, project-reference to both the bridge and `packages/ui-adapters-blazor`.
- [ ] **Step 3:** Wire into `Sunfish.sln`. Path-scoped `git add packages/ui-adapters-blazor.A11y/ Directory.Packages.props Sunfish.sln` only.

#### Task 1.2: Playwright page host

**Files:**
- Create: `packages/ui-adapters-blazor.A11y/PlaywrightPageHost.cs`

**Why:** The bridge needs to own the Playwright browser + page lifecycle so test classes don't each spin up their own (which would blow the xUnit test runtime budget).

- [ ] **Step 1:** Implement `PlaywrightPageHost : IAsyncLifetime`. On `InitializeAsync`, call `Playwright.CreateAsync()`, launch chromium headless, create a single `BrowserContext` with `LocaleAsync("en-US")` default.
- [ ] **Step 2:** Expose `Task<IPage> NewPageAsync(CultureInfo culture, bool rtl, string theme, CvdMode cvd)` — creates a fresh page per scenario with context-level options set (language + `ColorScheme` + CDP `Emulation.setEmulatedVisionDeficiency` for CVD).
- [ ] **Step 3:** `DisposeAsync` closes browser cleanly. Static `AsyncLazy<PlaywrightPageHost>` singleton shared across the xUnit test assembly via a `[CollectionDefinition]`.

#### Task 1.3: bUnit markup determinism test

**Files:**
- Create: `packages/ui-adapters-blazor.A11y/tests/BunitDeterminismTests.cs`

**Why:** Validate the critical assumption: bUnit's `IRenderedFragment.Markup` is byte-stable across runs for the same input, and includes all shadow-DOM content emitted by Sunfish Lit-backed Web Component wrappers.

- [ ] **Step 1:** For each of 3 pilot components, render 100 times in a tight loop via `ctx.RenderComponent<TComponent>(...)`, capture `.Markup`, hash each, assert all 100 hashes equal.
- [ ] **Step 2:** Visual diff `.Markup` against the Storybook-rendered HTML for the same args (exported as a fixture from Workstream B's storybook static build). Allowable diffs: whitespace only. Fail on semantic diffs.
- [ ] **Step 3:** If Step 2 reveals shadow-DOM content loss, document in `waves/global-ux/week-2-bunit-bridge-report.md` and invoke ADR 0034 Option B.

#### Task 1.4: Core bridge implementation

**Files:**
- Create: `packages/ui-adapters-blazor.A11y/BunitToAxeBridge.cs`

- [ ] **Step 1:** Signature: `public static async Task<AxeResult> RunAxeAsync(IRenderedFragment fragment, IPage page, AxeOptions? options = null)`.
- [ ] **Step 2:** Serialize `fragment.Markup` into a full HTML5 document (inject `<!doctype html><html><head>...</head><body>` wrapper with theme CSS loaded from `packages/ui-core/themes/<theme>.css`).
- [ ] **Step 3:** `await page.SetContentAsync(fullHtml)`. Inject `axe-core` via `await page.AddScriptTagAsync(new() { Path = "node_modules/axe-core/axe.min.js" })`.
- [ ] **Step 4:** Invoke axe via `await page.EvaluateAsync<JsonElement>("async () => await axe.run(document, {...options})")`. Deserialize to strongly-typed `AxeResult`.
- [ ] **Step 5:** Return `AxeResult`; caller asserts `.Violations.Where(v => v.Impact >= Impact.Moderate).Should().BeEmpty()`.

#### Task 1.5: Sunfish-specific assertions

**Files:**
- Create: `packages/ui-adapters-blazor.A11y/SunfishA11yAssertions.cs`

**Why:** ADR 0034 says each harness asserts the same `parameters.a11y.sunfish` invariants in its native idiom. Axe alone covers WCAG; Sunfish contract checks (focus order, keyboard map, directional icons) need .NET-side assertions.

- [ ] **Step 1:** `AssertFocusInitialAsync(IPage page, string selector)` — queries `:focus`, compares to declared `focus.initial`.
- [ ] **Step 2:** `AssertFocusTrapAsync(IPage page, string containerSelector)` — presses Tab N+1 times (N = count of focusable descendants), asserts focus returns to first focusable.
- [ ] **Step 3:** `AssertKeyboardMapAsync(IPage page, KeyboardMap[] map)` — for each `{ keys, action }`, dispatches the key chord, asserts an observable side effect (usually a data attribute or event dispatch on host).
- [ ] **Step 4:** `AssertDirectionalIconsMirroredAsync(IPage page, string[] iconSelectors)` — sets `dir=rtl` via `page.EvaluateAsync("document.documentElement.dir = 'rtl'")`, reads computed `transform` on each icon, asserts non-identity mirror (or identity for icons declared `non-directional`).

#### Task 1.6: Contract reader

**Files:**
- Create: `packages/ui-adapters-blazor.A11y/ContractReader.cs`
- Create: `packages/ui-core/scripts/export-a11y-contracts.mjs` (companion)

**Why:** The contract lives in `.stories.ts` files (TypeScript). The .NET bridge can't parse TypeScript. Solution: Node-side build script exports every component's `parameters.a11y.sunfish` block as JSON; the .NET bridge reads JSON.

- [ ] **Step 1:** Write `export-a11y-contracts.mjs` that loads each `.stories.ts` via Storybook's CSF parser (`@storybook/csf-tools`), extracts `parameters.a11y.sunfish`, emits `packages/ui-core/dist/a11y-contracts.json` keyed by component tag name.
- [ ] **Step 2:** Hook into `ui-core` build: `pnpm build` runs the export script after Vite. Commit `a11y-contracts.json`? NO — it's a build artifact, gitignored; .NET tests call `pnpm --filter @sunfish/ui-core build` in their fixture setup.
- [ ] **Step 3:** `ContractReader.Load(string tagName): SunfishA11yContract` reads the JSON, deserializes into a strongly-typed record matching the ADR 0034 shape.

#### Task 1.7: Pilot-component end-to-end test

**Files:**
- Create: `packages/ui-adapters-blazor.A11y/tests/PilotComponentA11yTests.cs`
- Create: `packages/ui-adapters-blazor.A11y/tests/BunitToAxeBridgeTests.cs`

**Why:** Prove the bridge works end-to-end on all 3 pilot components before cascading any assumptions.

- [ ] **Step 1:** Parameterized xUnit theory: 3 pilot components × (LTR, RTL) × (light, dark) × 3 CVD = 36 scenarios. For each: bUnit-render, bridge to Playwright page, run axe, assert zero violations at impact ≥ moderate.
- [ ] **Step 2:** Add Sunfish-contract assertions: `SunfishA11yAssertions.AssertFocusInitialAsync(...)` etc. driven by `ContractReader.Load(...)`.
- [ ] **Step 3:** Run: `dotnet test packages/ui-adapters-blazor.A11y/tests/`. All 108 assertions green before committing.

#### Task 1.8: Bridge report + decision point

**Files:**
- Create: `waves/global-ux/week-2-bunit-bridge-report.md`

- [ ] **Step 1:** Document: bridge implementation status, pilot-test pass rate, any shadow-DOM or CDP-emulation issues observed, estimated per-component runtime (target: < 5 s per component × 36 scenarios = ≤ 3 min for 40 components on 2-shard dotnet test).
- [ ] **Step 2:** Binary verdict: BRIDGE-READY (proceed to Week 3 Blazor cascade) OR BRIDGE-FAILED (invoke ADR 0034 Option B, log in `decisions.md`, re-scope Plan 4 to `ui-core` + React only).
- [ ] **Step 3:** If BRIDGE-FAILED, publish accessibility-debt register at `waves/global-ux/a11y-debt-register.md` listing every Razor component and its next-review date.

---

### Workstream B: Production test-runner hook + matrix expansion on `ui-core`

#### Task 2.1: `@storybook/test-runner` postVisit hook

**Files:**
- Create: `packages/ui-core/.storybook/test-runner.ts`
- Modify: `packages/ui-core/package.json` — add `test:a11y` script, add `@storybook/test-runner` + `@axe-core/playwright` devDependencies.

**Why:** Week-1 measurement used the default smoke harness. Production config is the critical Week-2 deliverable — it is what turns stories into assertions.

- [ ] **Step 1:** Export `postVisit` hook. Inside: load axe via `injectAxe(page)`, run `checkA11y(page, '#storybook-root', { detailedReport: true })` at impact threshold `moderate`.
- [ ] **Step 2:** Read `story.parameters.a11y.sunfish`. If present, invoke Sunfish-specific assertions via `expectFocusTrapped`, `expectKeyboardMap`, `expectIconMirroredInRtl` from `src/test-helpers/`.
- [ ] **Step 3:** Handle the 3 pilot components as a smoke gate — if any of them fails, fail the entire test-runner (not just that story); prevents silent regression of pilots during cascade.

#### Task 2.2: Matrix decorators (theme × light/dark × LTR/RTL)

**Files:**
- Modify: `packages/ui-core/.storybook/preview.ts`

- [ ] **Step 1:** Add `decorators` for: theme (3 themes: `sunfish-default`, `sunfish-high-contrast`, `sunfish-print`), color scheme (light, dark), direction (ltr, rtl). Each decorator wraps story root with appropriate `data-*` attributes or CSS class.
- [ ] **Step 2:** Add `globalTypes` entries so Storybook UI switcher exposes all three axes; test-runner iterates all combinations via `parameters.chromatic.modes` or an equivalent matrix-expansion config.
- [ ] **Step 3:** Verify in Storybook dev server: 3 × 2 × 2 = 12 variants per story visible; matrix toggling works; no visual regressions on 3 pilot components.

#### Task 2.3: Test-helper assertions

**Files:**
- Create: `packages/ui-core/src/test-helpers/expectFocusTrapped.ts`
- Create: `packages/ui-core/src/test-helpers/expectKeyboardMap.ts`
- Create: `packages/ui-core/src/test-helpers/expectIconMirroredInRtl.ts`
- Create: `packages/ui-core/src/test-helpers/index.ts`

- [ ] **Step 1:** `expectFocusTrapped(el, { tabCount?: number })` — presses Tab N+1 times via Playwright, asserts `document.activeElement` returns to first focusable descendant. Playwright only; not a DOM-only helper.
- [ ] **Step 2:** `expectKeyboardMap(el, map: { keys: string[], action: string }[])` — each entry dispatches key chord, asserts observable side effect (host attribute mutation, custom-event dispatch captured on a listener attached before dispatch).
- [ ] **Step 3:** `expectIconMirroredInRtl(el, { iconSelector })` — sets `dir=rtl` on `document.documentElement`, reads computed `transform` on icon, asserts mirrored (matrix with negative x-scale) for directional icons OR identity for non-directional.
- [ ] **Step 4:** Barrel export from `index.ts`. Unit test each helper in isolation against tiny fixture stories under `src/test-helpers/__tests__/`.

#### Task 2.4: Production-axe runtime re-measurement

**Files:**
- Create: `waves/global-ux/week-2-production-axe-hook-report.md`

**Why:** Re-measure against the production hook to validate the Week-1 2 s/scenario estimate. If significantly over, invoke matrix-sharding fallback.

- [ ] **Step 1:** Run `pnpm --filter @sunfish/ui-core test:a11y` with the production hook enabled on 5 representative components (the 3 pilots + 2 more once authored by other workstreams, or re-run pilots 5× for a statistical sample).
- [ ] **Step 2:** Capture p50 and p95 per-scenario runtime. Compare to Week-1 floor (0.75 s/scenario smoke) and Week-1 ceiling estimate (2 s/scenario with axe).
- [ ] **Step 3:** Project full-`ui-core` wall time (40 components × 36 scenarios × measured p95). Compute required shard count to hit 15-min CI budget. Record verdict: GREEN (fits 4-shard), YELLOW (fits 8-shard), RED (move CVD axis to nightly).

#### Task 2.5: CVD emulation via CDP

**Files:**
- Modify: `packages/ui-core/.storybook/test-runner.ts`
- Modify: `packages/ui-core/.storybook/main.ts`

**Why:** CVD simulation (deuteranopia, protanopia, tritanopia) via Chrome DevTools Protocol `Emulation.setEmulatedVisionDeficiency` — native chromium feature, deterministic.

- [ ] **Step 1:** In `test-runner.ts` `preVisit` hook, read `story.parameters.cvd` (default: iterate all three). Call `await page.context().newCDPSession(page).then(cdp => cdp.send('Emulation.setEmulatedVisionDeficiency', { type: 'deuteranopia' | 'protanopia' | 'tritanopia' | 'none' }))`.
- [ ] **Step 2:** `postVisit` runs axe once per CVD mode in the matrix. Assertion: contrast-based axe rules pass under each emulation. This catches deuteranopia-invisible red/green state-indicator bugs.
- [ ] **Step 3:** Smoke-test on 3 pilots: each must pass axe under all 3 CVD modes. If `sunfish-syncstate-indicator` fails on deuteranopia (likely — it's a red/green indicator by default), that's a real bug — file to `waves/global-ux/decisions.md` and fix (add a shape cue or pattern fill).

#### Task 2.6: Production hook report

**Files:**
- Modify: `waves/global-ux/week-2-production-axe-hook-report.md` (complete)

- [ ] Document: `postVisit` hook implementation status, pilot pass rate under production config, matrix-expansion verdict from Task 2.4, any CVD bugs surfaced by Task 2.5.

---

## Week 3 — Cascade

### Task 3.1: Inventory `ui-core` components

**Files:**
- Create: `waves/global-ux/week-3-cascade-inventory.md`

- [ ] **Step 1:** Enumerate `packages/ui-core/src/components/*/`. Inventory baseline: the 3 Week-1 pilot components plus components being landed in parallel workstreams by other plans (expected final count ~40). List each with: directory path, has-story-yet (Y/N), story-authoring owner, composedOf dependencies.
- [ ] **Step 2:** For each component lacking a story, classify: SIMPLE (copy pilot pattern), COMPOSITE (references other components' stories via `composedOf`), HARD (no clear story seam — e.g., imperative data-grid virtualization). Target: ≤ 5 HARD.
- [ ] **Step 3:** Group into ~5 clusters of ~8 components each for subagent dispatch. Cluster boundaries align with component family: `primitives` (button, icon, typography), `overlays` (dialog, popover, toast, tooltip), `forms` (input, select, checkbox, radio, switch), `data-display` (syncstate, badge, avatar, card), `navigation` (tabs, menu, breadcrumb).

### Task 3.2: Cascade story authoring on `ui-core`

**Files:**
- Modify: each `packages/ui-core/src/components/<name>/` — add `<name>.stories.ts` with `parameters.a11y.sunfish` block.

**Why:** This is the expensive Week 3 work. Each component gets a story conforming to the pilot pattern. Component source files are NOT modified; we only add story files.

- [ ] **Step 1:** Dispatch one subagent per cluster. Each subagent's deliverable per component: `<name>.stories.ts` with `Default` story at minimum, `parameters.a11y.sunfish` contract block (name, role, keyboardMap, focusOrder, reducedMotion, rtlIconMirror, screenReaderAudit stubs), `play` function invoking at least one Sunfish-helper assertion where applicable.
- [ ] **Step 2:** For COMPOSITE components, populate `composedOf: [<child-tag-names>]` so Storybook's composition viewer traverses; the composite story does not duplicate child contracts.
- [ ] **Step 3:** For each cluster, one commit with path-scoped `git add packages/ui-core/src/components/<cluster-directories>/`. Subagents MUST NOT run `git add .` or batch unrelated files — the prior-session GitButler bug mandates path-scoping discipline.
- [ ] **Step 4:** Reviewer agent (spec-compliance + code-quality, serial) gates each cluster commit before the next dispatches. Reviewer checks: (a) contract block conforms to ADR 0034 shape; (b) `play` function runs clean in Storybook dev server; (c) axe clean under production test-runner; (d) no changes outside `src/components/<cluster>/`.

### Task 3.3: HARD-component workshop

**Files:**
- Modify: `waves/global-ux/week-3-cascade-inventory.md` (HARD section)

**Why:** Components classified HARD in Task 3.1 need bespoke story-authoring decisions; batching them at end-of-Week-3 as a focused workshop prevents subagent churn on ambiguous cases.

- [ ] **Step 1:** Solo-by-Claude session: for each HARD component, decide (a) authorable-with-extra-decorators, (b) deferred to Plan 6 (documented reason), (c) scope-cut (merge with another component).
- [ ] **Step 2:** Author stories for (a)-category components. Log (b) and (c) decisions in `decisions.md`.
- [ ] **Step 3:** HARD-total cap: ≤ 5 components deferred. If > 5, re-scope with BDFL.

### Task 3.4: Cascade to `ui-adapters-react`

**Files:**
- Modify: `packages/ui-adapters-react/.storybook/main.ts` — add `stories: ['../../ui-core/src/**/*.stories.ts', './src/**/*.stories.tsx']`.
- Create: `packages/ui-adapters-react/.storybook/test-runner.ts` — mirror `ui-core` config.
- Create: `packages/ui-adapters-react/src/test-helpers/index.ts` — re-export from `ui-core/src/test-helpers/`.
- Create: `packages/ui-adapters-react/src/components/<react-only>/*.stories.tsx` — for any wrappers whose React surface differs semantically from `ui-core` (e.g., controlled-form `onChange` pattern).

**Why:** React adapter reuses stories where semantics match, per ADR 0034. The React-only wrappers get their own stories.

- [ ] **Step 1:** Install devDependencies: `@storybook/react-vite`, `@storybook/test-runner`, `@axe-core/playwright`. Smoke-test: `pnpm --filter @sunfish/ui-adapters-react storybook` serves.
- [ ] **Step 2:** Identify React-only wrappers. Expected: small number (< 5) — the Web Component already supplies behavior; React wrappers mostly forward props.
- [ ] **Step 3:** Run `pnpm --filter @sunfish/ui-adapters-react test:a11y`. Target: 3 pilot components green; full inventory green as stretch (matches `ui-core` 40-component coverage via imported stories).

### Task 3.5: Cascade coverage report

**Files:**
- Create: `waves/global-ux/week-3-cascade-coverage-report.md`

- [ ] Report: `ui-core` components covered (target: ≥ 35 / ~40), `ui-adapters-react` coverage (target: pilots-green minimum, full-green stretch), `ui-adapters-blazor.A11y` pilot status (from Workstream A), HARD-deferred list with reasons, matrix runtime measured against CI budget.

---

## Week 4 — Polish + end-to-end validation

### Task 4.1: Screen-reader audit runbook

**Files:**
- Create: `waves/global-ux/a11y-screen-reader-runbook.md`

**Why:** Spec §7 mandates `screenReaderAudit` provenance with 12-month freshness. The runbook makes the audit reproducible — named hardware, exact version strings, step-by-step narration.

- [ ] **Step 1:** Document tooling: NVDA-2026.1 on Firefox-126 (Windows 11), VoiceOver-macOS15 on Safari-17 (macOS 15.x), optional JAWS-2024/Chrome-125 for high-value components. Exact version strings captured via `nvda --version`, screenshot of VoiceOver About.
- [ ] **Step 2:** Per-component audit template: pre-audit checklist (component loaded in kitchen-sink in `ar-SA` and `en-US`), narrated steps (SR announces role, SR announces name, SR announces state change on interaction), pass/fail log, audit entry updated in the story's `parameters.a11y.sunfish.screenReaderAudit` block.
- [ ] **Step 3:** Re-audit 3 pilot components this week; record results in each pilot's story. CI rule added in Plan 5: any `screenReaderAudit` entry older than 12 months fails the build.

### Task 4.2: Reduced-motion variant auditing

**Files:**
- Modify: each `ui-core` component declaring `transition`/`animation` CSS — add `<name>.reduced-motion.stories.ts` or inline `ReducedMotion` export on the main story file.
- Create: `packages/ui-core/src/test-helpers/expectReducedMotionRespected.ts`

**Why:** Spec §6 requires every animated component to have a `data-motion="reduced"` variant with a test asserting no animation resolves. Combined with `prefers-reduced-motion: reduce` emulation via CDP.

- [ ] **Step 1:** Implement `expectReducedMotionRespected(el)` helper: reads `getComputedStyle(el).animationName` and `transitionDuration` on host + key descendants; asserts `animationName === 'none'` AND `transitionDuration <= 10ms`.
- [ ] **Step 2:** Inventory `ui-core` components with motion. Expected: dialog (enter/exit), toast, popover, any skeleton/loader, syncstate indicator (pulse). ~8-12 components.
- [ ] **Step 3:** For each, add a `ReducedMotion` story export that sets `data-motion="reduced"` on host AND uses `parameters.globals.prefersReducedMotion: 'reduce'` (which the preview decorator translates to CDP `Emulation.setEmulatedMedia({ features: [{name:'prefers-reduced-motion',value:'reduce'}] })`). `play` function invokes `expectReducedMotionRespected`.
- [ ] **Step 4:** Kitchen-sink settings switcher smoke-test: toggle reduced-motion pref, visually confirm dialog/toast/popover render without entrance animations.

### Task 4.3: `SUNFISH_A11Y_001` analyzer

**Files:**
- Create: `packages/analyzers/accessibility/Sunfish.Analyzers.Accessibility.csproj`
- Create: `packages/analyzers/accessibility/ComponentMissingStoryAnalyzer.cs`
- Create: `packages/analyzers/accessibility/tests/ComponentMissingStoryAnalyzerTests.cs`
- Create: `tooling/eslint-plugin-sunfish-a11y/package.json`
- Create: `tooling/eslint-plugin-sunfish-a11y/src/rules/component-missing-story.ts`
- Modify: `Directory.Packages.props` — central package management entry.
- Modify: `.editorconfig` — register `dotnet_diagnostic.SUNFISH_A11Y_001.severity` entry.

**Why:** Spec §8 requires the analyzer. Two targets: Roslyn on `.razor` side, ts-eslint on `.ts` side, same diagnostic ID to keep contributor mental-model unified.

- [ ] **Step 1 (Roslyn):** Analyzer inspects `AdditionalFiles` matching `packages/ui-adapters-blazor/Components/**/*.razor`. For each `.razor`, checks whether a sibling `<name>.stories.ts` exists (peek at filesystem via `AdditionalFiles` metadata or a follow-on build target that emits a manifest). If missing, emits `SUNFISH_A11Y_001` Warning. (Phase 2 will promote to Error per spec §8.)
- [ ] **Step 2 (ts-eslint):** ts-eslint plugin inspects `packages/ui-core/src/components/*/` and `packages/ui-adapters-react/src/components/*/` directories. For each `.ts`/`.tsx` component file matching `<kebab-name>.ts(x)`, checks for sibling `*.stories.ts(x)`. If missing, emits rule violation with code `SUNFISH_A11Y_001`.
- [ ] **Step 3:** False-positive calibration (Assumption): run both analyzers across current repo state. Target ≤ 5% false-positive. Tune rules to narrow file-match pattern (exempt barrels, test-helpers, internal-only). Record any exempt paths in an `.editorconfig` override.
- [ ] **Step 4:** Diagnostic registration: add `SUNFISH_A11Y_001` entry to `docs/diagnostic-codes.md` (or create if doesn't exist — confirm location).

### Task 4.4: Integration test against 3 adapters

**Files:**
- Create: `waves/global-ux/week-4-three-adapter-e2e-report.md`

**Why:** Prove the contract-is-enforced-thrice promise from ADR 0034. A real contract change should flow through all three harnesses.

- [ ] **Step 1:** Modify `sunfish-dialog`'s `parameters.a11y.sunfish.keyboardMap` — add a new `Enter: confirm` binding (or similar benign addition). Run `pnpm test:a11y` on `ui-core` — expect failure because dialog doesn't yet handle Enter. Fix the component, re-run, expect green.
- [ ] **Step 2:** Run `pnpm --filter @sunfish/ui-adapters-react test:a11y` — expect the same assertion to pass (React wrapper inherits contract).
- [ ] **Step 3:** Run `dotnet test packages/ui-adapters-blazor.A11y/tests/` — expect the same assertion to pass in the bridge. Log evidence of three-way enforcement.
- [ ] **Step 4:** If Workstream A failed (Option B invoked), document that Step 3 is N/A; Blazor assertion deferred to Phase 2 per debt register.

### Task 4.5: Integration report + go/no-go for Plan 5 entry

**Files:**
- Create: `waves/global-ux/week-4-integration-report.md`
- Modify: `waves/global-ux/status.md` (end-of-Weeks-2-4 update)

- [ ] **Step 1:** Compile the week's deliverables: bridge report + production-axe-hook report + cascade coverage report + SR runbook + three-adapter-E2E report.
- [ ] **Step 2:** Score against Plan 4 success criteria table; record each as PASS / FAIL / DEFERRED with evidence link.
- [ ] **Step 3:** Binary verdict: PROCEED to Plan 5 (CI Gates — wires these harnesses as required status checks) OR RE-PLAN weeks 2-4 with named fallback.

---

## Verification

### Automated

- `pnpm --filter @sunfish/ui-core test:a11y` — 36-scenario matrix green across all ~40 components (or ≥ 35 with HARD deferrals logged)
- `pnpm --filter @sunfish/ui-adapters-react test:a11y` — green on 3 pilots minimum, full inventory as stretch
- `dotnet test packages/ui-adapters-blazor.A11y/tests/` — 108 pilot assertions green (or BRIDGE-FAILED with Option B invoked)
- `dotnet test packages/analyzers/accessibility/tests/` — analyzer unit tests green
- `pnpm --filter eslint-plugin-sunfish-a11y test` — ts-eslint plugin tests green
- Build-time: `SUNFISH_A11Y_001` emits Warning on any component file missing a sibling story (expected count: 0 after cascade)

### Manual

- Kitchen-sink reduced-motion toggle: dialog/toast/popover render without entrance animations
- NVDA re-audit of 3 pilots: role + name + state-change announcements match contract
- VoiceOver re-audit of 3 pilots: same
- Storybook dev server: matrix toggler (theme × light-dark × LTR-RTL × CVD) exposes all 72 per-story combinations; pilot stories render correctly in each
- Three-adapter E2E (Task 4.4): introduce a deliberate contract change; all three harnesses catch it (or bridge deferred per Option B)

### Ongoing Observability

- Post-Week-4: CI metric — `test:a11y` p95 wall time per shard; alert if trends above 15 min threshold
- Post-Week-4: CI metric — `SUNFISH_A11Y_001` diagnostic count per PR; alert if a component lands without a story
- Screen-reader audit freshness: monthly report of entries approaching the 12-month cutoff; owner: a11y lead

---

## Conditional sections

### Rollback Strategy

- **bUnit bridge failure (Workstream A):** Invoke ADR 0034 Option B — skip Blazor a11y gate; publish `waves/global-ux/a11y-debt-register.md`. Plan 4 still passes on `ui-core` + React only. Timeline cost: –3 days on Week 2 (bridge work re-scoped to Plan 6).
- **Production axe-injection over-budget:** Matrix sharding to 8-shard, or move CVD axis to nightly (reduces per-commit scenarios by 3×). Named in spec §7. Timeline cost: ≤ 1 day re-plumbing.
- **Cascade stuck on a component family:** Document as deferred-cascade; Plan 6 picks it up. Week-3 declared partial-success if ≥ 35 of ~40 `ui-core` components land.
- **Analyzer false-positive storm:** Narrow rule scope, demote to Suggestion severity, or disable pending Plan 5 tuning. Warn-not-error severity already mitigates.
- **Screen-reader audit blocked by hardware access:** Defer re-audit to Plan 6; flag pilots with a `screenReaderAudit: { pending: 'hardware-access' }` marker so CI's freshness check treats them as pending-not-expired.

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| bUnit `IRenderedFragment.Markup` strips shadow-DOM content or is non-deterministic | Medium-High | High (Workstream A blocker) | Task 1.3 validates before bridge core work; Option B fallback named in ADR 0034 |
| `@axe-core/playwright` requires real origin for some rules when loaded via `page.setContent()` | Low-Medium | Medium (scopes bridge to single-document components) | Task 1.4 integration test flags; workaround is `page.goto('data:text/html,...')` instead of `setContent` |
| Production axe-injection wall time > 2.5 s/scenario | Medium | Medium (CI budget breach) | Task 2.4 re-measurement early; spec §7 already names 8-shard + CVD-nightly fallbacks |
| CDP CVD emulation unstable across Playwright 1.59.1 chromium builds | Low | Medium | Task 2.5 smoke; CSS-filter fallback documented |
| Subagent-driven cascade over-reaches commits (GitButler bug pattern) | Medium | High (branch pollution) | Path-scoped `git add` mandatory per cluster; reviewer-agent gate |
| `SUNFISH_A11Y_001` noisy on internal-only files | Low-Medium | Low | Task 4.3 false-positive calibration; narrow rule; warn-not-error severity |
| Screen-reader re-audit reveals real bug on pilot component | Low-Medium | Low-Medium (component fix, not plan failure) | Plan 4 success criteria permits bug fix + re-audit without failing the plan |
| bUnit version drift breaks bridge between Week 2 and Week 4 | Low | Medium | Pin `bunit` in `Directory.Packages.props`; bridge README §Maintenance documents upgrade process |

### Dependencies & Blockers

- **Depends on:** Plan 1 complete (Tasks ADR 0034 accepted, 3 pilot stories landed, Week-1 runtime measurement published) ✅
- **Blocks:** Plan 5 (CI Gates) — cannot wire required status checks until harnesses exist on all three adapters
- **Blocks:** Plan 6 Phase 2 cascade — end-user accessibility work depends on component-level contract enforcement
- **Parallel to:** Plan 2 (loc-infra cascade) — zero file overlap; both can run in the same Week-2-to-Week-4 window
- **External dependency:** bUnit 1.31+, Microsoft.Playwright 1.59.1 parity with Node side (both public, unlikely blocker); NVDA-2026.1 download (public, ~100 MB); MacOS 15 + Safari 17 hardware access for VoiceOver (a11y lead must have or borrow)

### Delegation & Team Strategy

- **Solo-by-agent for Week 2:** 2 subagents, one per workstream (A = bridge, B = production hook + matrix). Each has a narrow brief and path-scoped commit mandate. Workstream A runs in isolation from B — a bridge failure must not block the Node-side cascade.
- **Subagent-fleet for Week 3 cascade:** 5 subagents, one per component cluster, dispatched in two waves (primitives + overlays first; forms + data-display + navigation second). Reviewer-agent runs serial between cluster commits. HARD workshop is solo-by-Claude.
- **Solo-by-Claude for Week 4 polish:** Analyzer work (Roslyn + ts-eslint state-machine reasoning), SR runbook authoring, three-adapter E2E validation — all benefit from foreground Claude context. Reduced-motion cascade can be subagent-delegated one cluster wide.

### Incremental Delivery

- **End of Week 2:** Production `postVisit` hook on `ui-core` + matrix decorators usable by devs; bridge pilot-green (or BRIDGE-FAILED with Option B); SR runbook drafted.
- **End of Week 3:** ~40 `ui-core` components story-wrapped; `ui-adapters-react` inherits; Blazor bridge covers 3 pilots (if BRIDGE-READY).
- **End of Week 4:** Analyzer lands; 3 pilots re-audited; reduced-motion variants green; three-adapter E2E proves contract flows end-to-end.

### Reference Library

- [Spec §6 (Reduced Motion) and §7 (Accessibility Contracts via Storybook)](../specs/2026-04-24-global-first-ux-design.md)
- [ADR 0034 — A11y Harness Per Adapter](../../adrs/0034-a11y-harness-per-adapter.md)
- [Plan 1 (Week 1 Pilot)](./2026-04-24-global-first-ux-phase-1-week-1-plan.md)
- [Plan 2 (parallel; orthogonal; loc-infra cascade)](./2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md)
- [Week 1 runtime measurement](../../../waves/global-ux/week-1-runtime-measurement.md)
- [decisions.md (rollback log)](../../../waves/global-ux/decisions.md)
- Storybook `@storybook/addon-a11y`: https://storybook.js.org/docs/writing-tests/accessibility-testing
- `@storybook/test-runner`: https://github.com/storybookjs/test-runner
- `@axe-core/playwright`: https://github.com/dequelabs/axe-core-npm/tree/develop/packages/playwright
- bUnit: https://bunit.dev/
- Playwright-dotnet: https://playwright.dev/dotnet/
- Chrome DevTools Protocol `Emulation.setEmulatedVisionDeficiency`: https://chromedevtools.github.io/devtools-protocol/tot/Emulation/#method-setEmulatedVisionDeficiency
- WCAG 2.2 AA: https://www.w3.org/TR/WCAG22/
- WAI-ARIA Authoring Practices: https://www.w3.org/WAI/ARIA/apg/

### Learning & Knowledge Capture

- Document in `waves/global-ux/decisions.md` on any fallback invoked (Option B, shard count change, CVD-to-nightly move, HARD-component deferrals).
- End-of-Week-4 retrospective in `waves/global-ux/week-4-integration-report.md`: what surprised us (bUnit markup quirks? CDP CVD flakiness? subagent commit-scoping regressions?), what cost more than expected, what the follow-on Plan 5 (CI Gates) needs to know to avoid re-learning.

### Replanning Triggers

- Week 2 Workstream A (bridge) fails Task 1.3 determinism test: immediately invoke ADR 0034 Option B; do not spend more than 2 days attempting repair. Re-scope to Plan 6.
- Week 2 Workstream B Task 2.4 shows axe runtime > 2.5 s/scenario: schedule sharding-fallback work before cascade begins (delays Week 3 start by 1 day; acceptable).
- Week 3 cascade hits < 30 components by Friday: declare Week-3 partial; cut analyzer (Task 4.3) and reduced-motion variants (Task 4.2) to Plan 5; scope Week-4 to three-adapter E2E (Task 4.4) + SR runbook (Task 4.1) only.
- Any CVD deuteranopia failure on `sunfish-syncstate-indicator` or other red/green component: that is a real accessibility bug, not a plan failure — fix the component (add shape/pattern cue), re-audit, log in `decisions.md`.

---

## Cold Start Test

A fresh agent walking into this plan should be able to execute Task 1.1 without further context by:
1. Reading this plan.
2. Reading Plan 1 for the commit-style and subagent-driven pattern.
3. Reading ADR 0034 for the bridge mandate and Option B fallback definition.
4. Reading `waves/global-ux/week-1-runtime-measurement.md` for the baseline numbers.
5. Reading `waves/global-ux/decisions.md` for any prior rollback pivots.

No additional context should be required. If any step requires out-of-band knowledge not in one of those five documents, that is a plan-hygiene bug — file an issue and update this plan before executing.
