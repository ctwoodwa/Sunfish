# Intake Note — Web Components Migration (ADR 0017)

**Date:** 2026-04-20
**Requestor:** Christopher Wood (BDFL)
**Request:** Migrate Sunfish UI components from Razor-first authoring to Web-Components-first authoring (via Lit), per the blueprint already accepted in [ADR 0017](../../../docs/adrs/0017-web-components-lit-technical-basis.md).

## Problem Statement

[Vision Pillar 3](../../../_shared/product/vision.md) commits to framework-agnostic UI with multi-framework consumption — today that commitment is aspirational. All first-party UI components live in `packages/ui-adapters-blazor` as Razor files; the React adapter does not exist; there is no canonical Web Components source of truth. [ADR 0017](../../../docs/adrs/0017-web-components-lit-technical-basis.md) (Accepted) names Lit + TypeScript as the authoring basis with JS and WASM as consumption tracks, and sketches an **M0 → M5** migration roadmap. The intent of this intake is to spin up the formal ICM work that turns that roadmap into executed milestones.

Three signals make now the right moment to launch the migration plan:
1. **G37 SunfishDataGrid** — the largest and most complex component — just reached a natural checkpoint (build green, committed, pushed).
2. **Anchor** — the MAUI Blazor Hybrid desktop accelerator — was scaffolded as a **second consumer shape** alongside Bridge's Blazor Server/WebAssembly. Two shapes make the cost of Blazor-only components visibly higher; one shape hid it.
3. **Icon rendering boundary fix** — the `GetIcon` → `GetIconMarkup` sweep across 25 files surfaced the exact kind of Razor-only friction (string vs `MarkupString`, adapter-to-DOM boundary drift) that Web Components eliminate by making the component itself the custom element. The bug was a preview of the cost of deferring this migration.

## Affected Sunfish Areas

Impact markers are approximate — Stage 01 (Discovery) will refine.

| Area | Impact | Note |
|---|---|---|
| `packages/ui-core` | **possible** | Contracts stay framework-agnostic; may grow WC-specific conventions (CSS custom-property naming, custom-event shape, attribute reflection). |
| `packages/ui-adapters-blazor` | **affected** | Becomes a thin wrapper over Web Components; most authoring moves out. ~40+ components. |
| `packages/ui-adapters-react` | **affected** | New package. Authors the React wrapper layer over the same WC source of truth. (Informally reserved ADR 0020 per ADR 0017 follow-ups.) |
| `packages/ui-components-web` | **affected** | **New package.** The Lit/TypeScript-authored WC source of truth. (Informally reserved ADR 0019 per ADR 0017 follow-ups for NPM publish and versioning.) |
| `packages/compat-telerik` | **possible** | Wraps the adapter layer; adapter shape changes flow through. |
| `packages/blocks-*` (14 pkgs) | **possible** | Consume adapter components; mostly isolated by adapter contract but parity tests may regress during migration. |
| `packages/ui-adapters-blazor/Icons/*` | **affected** | Icon rendering boundary already modernized this session; remaining Razor-specific surface migrates with the component bodies. |
| `apps/kitchen-sink` | **affected** | Parity demo surface — must render the same output through Blazor, React, and plain-JS WC consumers. |
| `apps/docs` | **affected** | DocFX-authored API docs need WC consumption examples alongside Razor. |
| `accelerators/bridge` | **affected** | Blazor Server + WebAssembly consumer. Must continue working at every milestone boundary. |
| `accelerators/anchor` | **affected** | MAUI `BlazorWebView` consumer. Validates that the WC migration doesn't only satisfy Bridge's shape. |
| `tooling/scaffolding-cli` | **affected** | Component templates consume the adapter shape; templates regenerate when WC authoring lands. |
| `.github/workflows/*.yml` | **affected** | CI picks up npm + TypeScript + Vite (or equivalent) steps alongside `dotnet build`. |

## Selected Pipeline Variant

- [x] **`sunfish-feature-change`** — the execution variant, per milestone (M0, M1, M2, M3, M4, M5 each run through 00–08 independently).
- [x] **`sunfish-gap-analysis`** — the **Stage 01 Discovery** is itself a gap analysis (inventory what exists, what's missing, what blocks WC authoring). Mixed-variant routing: gap-analysis shape for Discovery; feature-change shape from Architecture forward.

The migration is **not** an `sunfish-api-change` at the public-surface level. Consumer-facing contracts on `ui-core` are preserved wherever feasible; internal adapter-implementation shape is what migrates. If any ui-core contract is forced to change mid-migration, that spawns a standalone `sunfish-api-change` intake.

## Dependencies and Constraints

### Dependencies (inbound)

- [ADR 0017](../../../docs/adrs/0017-web-components-lit-technical-basis.md) — technical basis (Accepted).
- [ADR 0014](../../../docs/adrs/0014-adapter-parity-policy.md) — parity policy; Stage 01 identifies the parity-test-harness follow-up that must land with M0.
- [ADR 0021](../../../docs/adrs/0021-reporting-pipeline-policy.md) — reporting contracts that adapters consume remain framework-agnostic under migration; no collision.
- **`universal-planning.md`** ([UPF rule file](../../../.claude/rules/universal-planning.md)) — Stage 01 Discovery and Stage 02 Architecture are large enough that UPF's three-stage + optional hardening pass applies in full.

### Dependencies (outbound — ADRs this migration will author)

- **Reserved ADR 0019** — NPM publish + versioning process for `ui-components-web` (per ADR 0017 follow-up).
- **Reserved ADR 0020** — React adapter scaffolding choices (reconciler integration, CSS strategy, state primitives) (per ADR 0017 follow-up).
- **Design doc (not ADR)** — Declarative Shadow DOM + SSR patterns for Bridge's Blazor Server render path (per ADR 0017 follow-up).
- **Design doc (not ADR)** — Parity test harness for adapter equivalence (per ADR 0014 follow-up).

### Constraints

- **No breaking changes to `ui-core` contracts mid-migration.** Bundles depend on them. If a breaking change becomes unavoidable, it escalates to a standalone `sunfish-api-change` intake and M0 pauses.
- **Two consumer shapes must stay green at every milestone boundary.** Bridge (Blazor Server + WebAssembly) and Anchor (MAUI BlazorWebView). If a milestone breaks either shape beyond the in-flight migration window, it rolls back.
- **Build-tool toolchain gains npm, TypeScript, and a web bundler** (Vite, esbuild, or Rollup — M0 picks one). Contributors who previously needed only `dotnet` now need node + npm locally. Documented in `_shared/engineering/ci-quality-gates.md` before M0 begins.
- **MAUI 10 preview instability** — the Mono runtime package gap that blocked Anchor's mobile targets may also affect WC-on-MAUI validation. Stage 01 Discovery tracks whether MAUI preview churn is a migration risk that delays M5 (Anchor validation).
- **Pre-release private repo posture** — no external contributor friction during migration. Public release remains gated on LLC formation (memory: `project_sunfish_private_until_llc.md`), which is likely to happen during or after the WC migration runs.
- **Pre-community BDFL bus factor** — the migration is a single-maintainer effort. UPF's Cold Start Test (Stage 2 Meta-validation) matters: can a future contributor resume at any milestone boundary if the maintainer is unavailable?

### Sequencing

- **M0 and M1** are the foundation. They land `ui-components-web` scaffolding + `SunfishIcon` + one simple component (Button or SearchBox) fully migrated, proving the round trip.
- **M2–M4** fan out to the rest of the 40+ components by category (inputs, layout, feedback, data-display). Each is its own `sunfish-feature-change` run.
- **M5** re-enables mobile targets and validates Anchor.
- Reference ADR 0017 §Migration plan for the M-level breakdown.

### Kill Criteria (UPF anti-pattern #11: zombie projects)

The migration is explicitly cancellable if any of the following fires:

1. **M0 POC cannot reach parity.** If three months into M0/M1 the SunfishDataGrid (or whichever leading component M0 picks) cannot demonstrate parity with its Razor version across the two consumer shapes (Bridge + Anchor), pause the migration and revisit Lit-vs-alternative in a new ADR.
2. **Build-tool overhead dominates.** If the combined dotnet + npm + TypeScript + bundler toolchain causes the BDFL to spend >50% of contribution time on tooling versus components for two consecutive milestones, the JS-track scope reconsiders — JS consumers may move behind a trigger rather than ship from day one.
3. **Federation / LocalFirst constraint conflict.** If Declarative Shadow DOM or the WebView SSR path discovered during Stage 01 materially conflicts with Pillar 1 (local-first) or Pillar 2 (federation), kick back to architecture.

Absent those conditions, the migration runs to M5 completion.

## Next Steps

Proceed to **Stage 01 Discovery** with a `sunfish-gap-analysis`-shaped deliverable:

- Inventory every Razor component in `packages/ui-adapters-blazor/Components/**/*.razor`, categorize by complexity, tag each with its M-milestone target.
- Inventory every direct `@IconProvider`-style DI coupling in component bodies and catalog which become WC attributes, which become custom events, and which become slotted content.
- Identify the parity-test-harness gap — what test shape proves "the same output" across Blazor, React, and plain-JS WC consumers.
- Identify the build-tool decision — Vite vs. esbuild vs. Rollup for M0; document the tradeoff.
- Identify the CSS strategy — Shadow DOM encapsulation, `::part` surface, token integration with `_shared/design/tokens-guidelines.md`.
- Identify the SSR strategy — Declarative Shadow DOM on Blazor Server, initial HTML on MAUI BlazorWebView.
- Identify the accessibility regression risk — the `_shared/design/accessibility.md` WCAG 2.2 AA contract must survive the migration; axe-core CI gate is the test.
- Identify kill-criteria telemetry — how do we observe the conditions above at milestone boundaries?

**Expected Stage 01 output:** `01_discovery/output/web-components-migration-discovery-2026-04-20.md` (or later date at landing). UPF Stage 0 quality bar — the Decompose-Suspend-Validate principle applies; list assumptions with "VALIDATE BY → IMPACT IF WRONG" shape before advancing to Stage 02 Architecture.

## Cross-References

- [ADR 0017 — Web Components (via Lit) Technical Basis](../../../docs/adrs/0017-web-components-lit-technical-basis.md) — the decision being executed.
- [ADR 0014 — UI Adapter Parity Policy](../../../docs/adrs/0014-adapter-parity-policy.md) — the parity guarantee the migration must preserve.
- [`_shared/product/vision.md` §Pillar 3](../../../_shared/product/vision.md) — the framework-agnostic commitment this migration makes real.
- [`_shared/engineering/planning-framework.md`](../../../_shared/engineering/planning-framework.md) — UPF adoption; applies to Stages 01–02.
- [`_shared/design/accessibility.md`](../../../_shared/design/accessibility.md), [`_shared/design/internationalization.md`](../../../_shared/design/internationalization.md), [`_shared/design/tokens-guidelines.md`](../../../_shared/design/tokens-guidelines.md) — design contracts that must survive the migration.
- [`accelerators/anchor/README.md`](../../../accelerators/anchor/README.md) — the second consumer shape whose scaffolding made this migration's timing evident.
- [`icm/pipelines/sunfish-feature-change/routing.md`](../../pipelines/sunfish-feature-change/routing.md) — the execution-variant routing for M0–M5.
- [`icm/pipelines/sunfish-gap-analysis/routing.md`](../../pipelines/sunfish-gap-analysis/routing.md) — the Discovery-variant routing.
