# Plan 4 Workstream A — BRIDGE-READY Report

**Date:** 2026-04-24 (Plan 4 Task 1.8 — final verdict on the bUnit-to-axe accessibility bridge).
**Wave:** Plan 4 Workstream A Tasks 1.1–1.8 complete.
**Verdict:** **BRIDGE-READY.** Proceed to Plan 4 Workstream B (cascade across `ui-adapters-blazor` + `ui-adapters-react` + `ui-core` Storybook harness).
**Test totals:** 63 tests green / 0 failed / 0 skipped, ~8 s wall clock for the full suite.

---

## Tasks complete

| Task | Status | Commit | Highlights |
|---|---|---|---|
| 1.1 — Project scaffold | ✓ | `dec4bbc3` | netstandard2.0 csproj + bunit/Playwright deps + slnx wiring |
| 1.2 — `PlaywrightPageHost` | ✓ | `c882b5ea` | Lazy singleton; per-scenario context (locale + RTL + theme + CVD via CDP) |
| 1.3 — Determinism gate | ✓ | `1f1d5618` | 100×3 SHA-256 hashes equal; bUnit deterministic ✓ |
| 1.4 — `AxeRunner` | ✓ | `c882b5ea` | a11y-clean wrapper; auto-discovers `axe.min.js` from pnpm store |
| 1.5 — `SunfishA11yAssertions` | ✓ | `da0245cc` | focus initial / focus trap / keyboard map / RTL icon mirror |
| 1.6 — Contracts JSON pipeline | ✓ | `73bcd278` | tsx export + `ContractReader.Load`; round-trips real generated JSON |
| 1.7 — 36-scenario matrix | ✓ | this wave | 3 fixtures × 2 LTR/RTL × 2 light/dark × 3 CVD = 36 scenarios, all green |
| 1.8 — This report | ✓ | this wave | BRIDGE-READY verdict |

---

## What works (and what was non-obvious)

### bUnit determinism — the highest-risk bet

The single biggest go/no-go hinged on bUnit producing byte-identical markup across repeated renders. It did. Three fixture components (plain text, mixed attributes, RenderFragment composition) each rendered 100 times produced exactly one SHA-256 hash. No non-determinism in attribute ordering, no implicit ids drifting, no `@key` instability.

This was the right gate to run first; if it had failed the whole bridge architecture was dead.

### bUnit 2.x API drift caught during execution

Plan 4's narrative assumed `IRenderedFragment`, `TestContext`, and `RenderComponent<T>`. bunit 2.7.2 has obsoleted all three:

- `TestContext` → `BunitContext`
- `RenderComponent<T>(...)` → `Render<T>(...)`
- `IRenderedFragment` (non-generic) → `IRenderedComponent<T>` (generic)

All swapped inline; the `AxeRunner` API was redesigned to accept a markup string instead of a bunit type, decoupling the runner from any specific render framework.

### axe-core 4.11.3 emits non-string tags

`AxeResult.violations[].tags` was specced as `string[]` in axe docs. In practice the 4.11.3 build occasionally emits non-string entries (`actIds` reference objects). `Tags` was relaxed to `JsonElement` with a `GetTagStrings()` projection — forward-compat for any future axe surface drift.

### HTML wrapper had to be a11y-clean by construction

Initial `WrapInHtmlDocument` produced violations of its own (`html-has-lang`, `landmark-one-main`, `region`) that masked the fragment under test. Wrapper now sets `lang`, wraps in `<main>`, and uses a non-empty `<title>`. Wrapper itself contributes ZERO violations, and axe's scope is narrowed to `<main>` so page-level best-practice rules (`page-has-heading-one`, `bypass`) don't apply to component fragments.

### Node→.NET contract bridge via tsx, not AST parsing

Plan 4's design assumed `@storybook/csf-tools` AST parsing. In practice that requires either runtime execution of `.stories.ts` (compilation hop) or static AST traversal that breaks on non-literal contracts. tsx 4.21.0 dynamic-imports `.stories.ts` natively (esbuild loader), reads `default.parameters.a11y.sunfish` at runtime, dumps the actual values. Simpler, more correct, fewer moving parts.

The pipeline round-trips end-to-end for all 3 pilot stories; .NET deserializes the real JSON via `System.Text.Json` with case-insensitive property matching.

---

## What's NOT proved by Workstream A

**Realistic Razor pilots have not been audited.** The 36-scenario matrix runs against three internal fixture components (Plain text / Attributed / ChildContent) rather than `SunfishButton.razor` / `SunfishDialog.razor` / `SunfishSyncStateIndicator.razor` (the last doesn't exist in `ui-adapters-blazor` yet). The fixtures exercise the bridge's machinery — bUnit rendering + Playwright hosting + axe injection + CVD/RTL/theme emulation — without depending on real components that aren't ready.

This is a deliberate design call: the bridge VALIDATES at this gate (machinery works); ENFORCEMENT against real components is Workstream B's job once the cascade begins.

When real Razor pilots land:

- The matrix's `RenderFixtureMarkup` switch swaps fixture types for component types.
- `SunfishA11yAssertions.AssertContractAsync` becomes the loaded contract from `ContractReader.Default.Load(...)` — ready to use.

**Storybook visual-diff (Plan 4 Task 1.3 Step 2) deferred.** The plan specified diffing bUnit `.Markup` against the Storybook-rendered HTML for the same args. That requires the cross-workstream `ui-core` build to complete, which lives in Plan 2 Workstream B (Storybook + a11y addon already done; `pnpm build` + Vite still needed for actual static output to diff against). Carry forward as a Week 3 follow-up; not blocking the bridge.

**Shadow-DOM serialization** is technically supported by bUnit but not exercised by these fixtures. Sunfish's `ui-core` Web Components use open shadow DOM; the Blazor adapter wraps them but doesn't define new shadow trees. If a future Razor component opts into shadow DOM via JS interop, re-test in Workstream B.

---

## Performance budget vs. actuals

| Metric | Budget (Plan 4 Task 1.8) | Measured |
|---|---|---|
| Per-scenario runtime | < 5 s | ~150–200 ms (per matrix theory case after hot start) |
| Full 36-scenario matrix | < 3 min | **~7 s** wall clock |
| Full bridge test suite (63 tests) | budget for ~5 min in CI | **~8 s** local |
| Per-component projection (40 × 36 scenarios) | < 3 min on 2 shards | **~5 min** sequential, **~2.5 min** on 2 shards |

The matrix is **15× faster than the budget**. This is the local-dev measurement on a warm cache; CI startup overhead will add ~30 s per shard, which still leaves comfortable headroom.

If Plan 5's CI gate exceeds the < 15 min p95 budget at full ui-core scale, the four-shard parallelization already specified in Plan 4 Task 4.4 / Plan 5 has plenty of margin.

---

## Risks deferred to Workstream B

1. **Real Razor component a11y debt** — `ui-adapters-blazor`'s 50+ Razor components have never been audited. First cascade run will surface real violations; the bridge correctly reports them, but the components themselves need fixes. Plan a remediation backlog.
2. **`SunfishButton.razor` injection dependency on `CssProvider`** — bUnit-render of real components requires the Sunfish DI graph to be set up in the test context. `MatrixFixture` would need a `BunitContext` populated with provider mocks. Pattern is straightforward but adds setup overhead per component family.
3. **CVD via CDP is chromium-only** — Firefox / WebKit can't emulate vision deficiencies. Sunfish's a11y matrix is chromium-only; documented in `mt-backends.md`-style fashion in the bridge README. If WebKit-specific bugs emerge, they're out of CVD-scope.
4. **Per-locale axe coverage** — axe-core itself doesn't change behavior by locale; the locale knob is for layout / RTL CSS verification. The matrix sets `Accept-Language` and uses `<html lang>` correctly, but doesn't verify that Sunfish's translations actually appear (that's Plan 2's loc-cascade job).

---

## Handoff

Workstream B (Plan 4 §3) inherits a working bridge:

- `PlaywrightPageHost.GetAsync()` — lazy singleton, scenario-isolated pages.
- `AxeRunner.RunAxeAsync(markup, page, options?)` — markup → typed `AxeResult`.
- `SunfishA11yAssertions.AssertContractAsync(page, contract)` — contract-driven enforcement.
- `ContractReader.Default.Load(tagName)` — typed contract from generated JSON.

Workstream B's first job: enumerate the 50+ Razor components, identify which need contracts authored on the ui-core Storybook side (so `ContractReader.Load` returns something), and run the matrix against each in CI per Plan 5's gate spec.

No ADR 0034 Option B fallback needed. Accessibility-debt register (`waves/global-ux/a11y-debt-register.md`) remains unnecessary — the bridge is real, performant, and ready.

---

## References

- [ADR 0034 — A11y harness per adapter](../../docs/adrs/0034-a11y-harness-per-adapter.md)
- [Plan 4 — A11y Foundation cascade](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-a11y-foundation-plan.md)
- [Week 2 determinism gate report](./week-2-bunit-bridge-report.md) (Task 1.3)
- [Week 1 runtime measurement](./week-1-runtime-measurement.md) — original budget basis
- [bUnit 2.x migration guide](https://bunit.dev/docs/migrations) — for the API drift caught above
- [axe-core 4.x API](https://github.com/dequelabs/axe-core/blob/develop/doc/API.md)
- [Playwright .NET](https://playwright.dev/dotnet/)
