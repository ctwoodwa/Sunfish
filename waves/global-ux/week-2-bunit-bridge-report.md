# Week 2 bUnit-to-axe Bridge Determinism Report

**Date:** 2026-04-24 (Plan 4 Task 1.3 — the binary BRIDGE-READY / BRIDGE-FAILED go/no-go gate).
**Wave:** Plan 4 Workstream A Task 1.3.
**Artifact commit:** `dec4bbc3` (Task 1.1 scaffold) + this wave's determinism tests.
**Scope of the gate:** Does bUnit 2.7.2 render Razor components deterministically across repeated invocations? Without byte-identical markup, the bridge's whole architecture is dead — axe-core would see a different DOM every test run, and hash-based change detection in CI would flake.

---

## Binary verdict: **BRIDGE-READY** ✓

All three fixture components render byte-identically across 100 iterations each:

| Fixture | Exercises | Iterations | Unique SHA-256 hashes | Result |
|---|---|---|---|---|
| `SimpleTextFixture.razor` | Plain text + two string attributes | 100 | 1 | ✓ deterministic |
| `AttributedFixture.razor` | Mixed attribute types (string, bool, int, enum), computed CSS class | 100 | 1 | ✓ deterministic |
| `ChildContentFixture.razor` | `RenderFragment ChildContent` composition | 100 | 1 | ✓ deterministic |

Total test runtime: 429 ms (6 tests: 3 bridge scaffolding + 3 determinism).

---

## What the gate actually proves

The three fixtures exercise the rendering paths Sunfish Blazor components rely on:

1. **Plain attribute-driven markup** — baseline, covers trivial components.
2. **Multiple attribute types + computed values** — catches non-determinism in attribute ordering, type-to-string conversion, or enum formatting.
3. **`RenderFragment` composition** — the critical path for Sunfish's compositional components (SunfishDialog contains SunfishButton children, etc.) where non-deterministic child-render-tree ordering would surface.

Each fixture's 100-render loop captures `IRenderedFragment.Markup`, SHA-256-hashes it, and inserts the hash into a `HashSet<string>`. A single hash in the set after 100 renders means every render was byte-identical.

If any fixture's set had more than one hash, the gate would fail.

---

## Implementation notes

### bUnit 2.x API surface drift

Plan 4 Task 1.3 Step 1 assumed `ctx.RenderComponent<TComponent>(...)` would work. In bunit 2.7.2 that API is `[Obsolete]`; the replacement is `ctx.Render<TComponent>(...)`. Similarly `TestContext` is obsolete in favour of `BunitContext`. Both swapped; tests green on the new API. Noting for future plan-authoring that bunit 1.x → 2.x migration is assumed across the Sunfish Blazor surface.

### Razor SDK for the test project

Test projects that compile `.razor` fixtures require `Sdk="Microsoft.NET.Sdk.Razor"`. The initial scaffold used `Sdk="Microsoft.NET.Sdk"` and failed to find the fixture types; swapped.

### Shared BunitContext fixture

A single `BunitContext` is shared across the test class via `IClassFixture` rather than a per-test `new BunitContext()`. Each context construction allocates a full Blazor service provider; fixture-sharing cuts setup overhead and lets the 300 total render iterations (3 fixtures × 100) run in < 0.5 s.

---

## Scope of what this gate does NOT prove

The determinism gate is necessary but not sufficient for `BRIDGE-READY`:

1. **Does not test real Sunfish components.** Fixtures are minimal synthetics. Real components (e.g., `SunfishButton` with its `CssProvider` injection dependency) may surface non-determinism through their service-injection layer. The real-component test lives in **Plan 4 Task 1.7** (36-scenario pilot matrix); Task 1.3's scope is the bUnit engine itself.
2. **Does not test shadow-DOM serialisation.** Blazor doesn't use shadow DOM natively; components that opt in (via JS interop) would test this separately. Deferred to Task 1.7 if any pilot component uses it.
3. **Does not exercise axe-core, Playwright, or the full bridge assertion path.** Those land in Tasks 1.4 – 1.5 (axe invocation + Sunfish contract assertions).

`BRIDGE-READY` at this gate means **the bUnit foundation is stable enough to build the rest of the bridge on**. A future `BRIDGE-FAILED` verdict is still possible if Task 1.7 surfaces non-determinism in real components; ADR 0034 Option B remains the fallback.

---

## Known limitations + follow-ups

### Fixtures, not pilots

Sunfish's designated 3 pilot components for the a11y harness are the ui-core Web Components (`sunfish-button`, `sunfish-dialog`, `sunfish-syncstate-indicator`) — NOT Blazor. When formal Blazor pilots land (expected Plan 4 Task 1.7 scope or a follow-up), swap the fixtures for real components in this gate. The determinism test pattern is reusable.

### Plan 4 Task 1.3 Step 2 (Storybook visual-diff) deferred

The plan specifies visual-diffing `.Markup` against the Storybook-rendered HTML for the same args. That requires Storybook's static build as an input fixture, which in turn requires Workstream B's `ui-core` build to complete for this wave's branch. Deferred to a follow-up task once the cross-workstream build integration lands (Plan 4 Task 1.6 contract export).

### Potential non-determinism sources not exercised by fixtures

These did not trigger failure here, but remain risks to watch for when real components land:

- `@key` with non-stable keys (e.g., `Guid.NewGuid()` in a loop) — would surface as hash drift on re-render.
- Dictionary enumeration order in `@attributes` splatting — .NET preserves insertion order for `Dictionary<TKey,TValue>` since .NET Core 3.0, but a `HashSet`-based source would be non-deterministic.
- Injected services whose state mutates between renders (timestamps, counters).

Each is a design smell in production components and would be caught by Task 1.7's real-component matrix.

---

## Gate outcome: proceed with bridge engineering

Plan 4 Workstream A moves forward to:
- **Task 1.2** — `PlaywrightPageHost` lifecycle (shared chromium headless singleton).
- **Task 1.4** — `AxeRunner.RunAxeAsync` (full-HTML wrapping + axe-core injection + result deserialisation).
- **Task 1.5** — `SunfishA11yAssertions` (focus / keyboard / directional-icon contract assertions).
- **Task 1.6** — `export-a11y-contracts.mjs` + `ContractReader.Load` (Node → .NET contract bridge).
- **Task 1.7** — 36-scenario pilot matrix; the next gate where real-component non-determinism (if any) would surface.
- **Task 1.8** — final bridge-ready report + handoff to Plan 4 Workstream B.

No ADR 0034 Option B invocation needed. Debt register (`waves/global-ux/a11y-debt-register.md`) remains unnecessary at this point.
