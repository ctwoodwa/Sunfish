# Intake Note — UI Architecture Migration (ADR 0017, revised)

**Date:** 2026-04-20
**Revised:** 2026-04-21 — ADR 0017 was inverted from WC-first to spec-first with three peer consumption tracks (Blazor adapter, React adapter, Web Components adapter). This intake tracks that revised plan.
**Requestor:** Christopher Wood (BDFL)
**Request:** Migrate Sunfish's UI layer from today's Razor-only-with-implicit-contracts shape to the spec-first shape already captured in CLAUDE.md's Framework-Agnostic Design Principle: `ui-core` owns the contracts (semantic, accessibility, styling, interaction); native first-class adapters implement them per framework; a Web-Components track ships as a peer for plain-JS / framework-agnostic consumers. Per [ADR 0017](../../../docs/adrs/0017-web-components-lit-technical-basis.md) as revised 2026-04-21.

## Problem Statement

[Vision Pillar 3](../../../_shared/product/vision.md) commits to framework-agnostic UI with multi-framework consumption. [CLAUDE.md §Framework-Agnostic Design Principle](../../../CLAUDE.md) says how: contracts in `ui-core`, implementations in adapters, adapters must not drive the design. Today the commitment is aspirational:

- All first-party UI components live in `packages/ui-adapters-blazor` as Razor files.
- `packages/ui-core` holds only weak contracts (data-request/sort/filter). Rich contracts — semantic props, keyboard model, ARIA shape, token surface, interaction rules — live implicitly in the Razor files.
- The Blazor adapter is therefore the *de facto* specification, and the "React adapter is on the roadmap" line commits work that cannot begin until those contracts are extracted.

[ADR 0017](../../../docs/adrs/0017-web-components-lit-technical-basis.md) (Accepted 2026-04-20, revised 2026-04-21) establishes spec-first as the canonical model and names three peer consumption tracks: `ui-adapters-blazor` (native), `ui-adapters-react` (native, to be scaffolded), `ui-components-web` (Lit + TypeScript, npm-published, for plain-JS consumers). The intent of this intake is to spin up the ICM work that turns that plan into executed milestones.

Three signals make now the right moment to launch:

1. **G37 SunfishDataGrid** — the largest and most complex component — just reached a natural checkpoint (build green, committed, pushed). Its contract is the worst-case test for the four-contract shape and lands in M1.
2. **Anchor** — the MAUI Blazor Hybrid desktop accelerator — was scaffolded as a **second consumer shape** alongside Bridge's Blazor Server/WebAssembly. With the revised ADR, Anchor continues to consume `ui-adapters-blazor` directly; it does not depend on the WC track being scaffolded first.
3. **Icon rendering boundary fix** — the `GetIcon` → `GetIconMarkup` sweep across 25 files surfaced the exact kind of Razor-only friction (implicit contract, adapter-specific translation) that spec-first contracts make explicit and reviewable.

## Affected Sunfish Areas

Impact markers are approximate — Stage 01 Discovery will refine.

| Area | Impact | Note |
|---|---|---|
| `packages/ui-core` | **affected (large)** | Grows from weak data-request contracts to ~40 component contracts × 4 slices each (semantic, a11y, styling, interaction). This is the largest conceptual migration in the plan. |
| `packages/ui-adapters-blazor` | **affected (refactor)** | Stays native Blazor; no DOM-wrapper layer introduced. Each Razor component asserts its implementation against the `ui-core` contract rather than inheriting the contract implicitly. |
| `packages/ui-adapters-react` | **new** | Scaffolded at M2. Native React implementation of `ui-core` contracts. ADR 0020 captures scaffolding choices. |
| `packages/ui-components-web` | **new (npm package, not .csproj)** | Scaffolded at M4. Lit + TypeScript implementation of `ui-core` contracts. Published to npm as `@sunfish/ui-components-web`. A **peer** consumption track, not the canonical source. |
| `packages/compat-telerik` | **unaffected** | Compat is about API surface — wraps the adapter layer. Contracts don't change the wrapped shape. |
| `packages/blocks-*` (14 pkgs) | **possible** | Consume adapter components; isolated by adapter contract. Parity tests may regress during migration. |
| `packages/ui-adapters-blazor/Icons/*` | **possible** | Icon rendering boundary already modernized this session; no re-migration expected unless the styling contract forces token surface changes. |
| `apps/kitchen-sink` | **affected** | Becomes the parity demo surface — same component rendered through all three tracks (Blazor, React, WC) for side-by-side comparison. |
| `apps/docs` | **affected** | DocFX API docs plus per-track consumption examples. |
| `accelerators/bridge` | **affected (minimal)** | Continues consuming `ui-adapters-blazor`. No direct dependency on the new React or WC tracks. |
| `accelerators/anchor` | **affected (minimal)** | Continues consuming `ui-adapters-blazor` via `BlazorWebView`. Validates that spec-first contracts don't regress the MAUI path. |
| `tooling/scaffolding-cli` | **affected** | Templates grow: now generate contract-first scaffolding. New templates for React and WC components. |
| `.github/workflows/*.yml` | **affected** | CI adds TypeScript, React toolchain, Lit toolchain, parity harness runner. |

## Selected Pipeline Variant

- [x] **`sunfish-feature-change`** — execution variant, per milestone (M0, M1, M2, M3, M4, M5 each run through 00–08 independently).
- [x] **`sunfish-gap-analysis`** — the **Stage 01 Discovery** is itself a gap analysis (inventory what contracts exist, what's missing, what blocks parity harness). Mixed-variant routing: gap-analysis shape for Discovery; feature-change shape from Architecture forward.

The migration is **not** an `sunfish-api-change` at the consumer-surface level. Consumer-facing contracts on `ui-core` grow significantly but do so additively — each new contract is new surface, not a break of an existing one. If any existing `ui-core` type must change shape mid-migration, that spawns a standalone `sunfish-api-change` intake.

## Dependencies and Constraints

### Dependencies (inbound)

- [ADR 0017 (revised 2026-04-21)](../../../docs/adrs/0017-web-components-lit-technical-basis.md) — the spec-first model this intake executes.
- [ADR 0014](../../../docs/adrs/0014-adapter-parity-policy.md) — parity policy. The parity harness this migration requires is ADR 0014's enforcement mechanism; it lands with M3.
- [CLAUDE.md §Framework-Agnostic Design Principle](../../../CLAUDE.md) — the principle the revised ADR finally implements.
- **`universal-planning.md`** ([UPF rule file](../../../.claude/rules/universal-planning.md)) — Stage 01 Discovery and Stage 02 Architecture apply UPF's three-stage + optional hardening pass in full.

### Dependencies (outbound — this migration authors)

- **ADR 0019** — NPM publish + versioning process for `ui-components-web` (WC consumption track). Triggers near M4 completion.
- **ADR 0020** — React adapter scaffolding choices (reconciler integration, CSS strategy, state primitives). Triggers at M2 start.
- **Design doc — Parity test harness** (ADR 0014 follow-up). Lands in M2 so M3 can implement it. Non-negotiable: the harness is what makes spec-first safe.
- **Design doc — Contract authoring guide.** How a four-contract spec is written, reviewed, and evolved. Lands in M0 alongside the proof-point component.
- **Design doc — Declarative Shadow DOM + SSR patterns** for the WC track. Triggers at M4.

### Constraints

- **No breaking changes to existing `ui-core` types mid-migration.** New contracts are additive.
- **Three consumer shapes must stay green at every milestone boundary.** Bridge (Blazor Server + WebAssembly via `ui-adapters-blazor`), Anchor (MAUI BlazorWebView via `ui-adapters-blazor`), and — from M2 onward — any React test app built against `ui-adapters-react`. From M4 onward, a plain-JS consumer of `ui-components-web`.
- **Build-tool toolchain** gains npm + TypeScript + React at M2 and Lit + Web Test Runner + Vite at M4. CI gates grow accordingly. Contributors who previously needed only `dotnet` now need node + npm locally from M2. Documented in `_shared/engineering/ci-quality-gates.md` before M2 begins.
- **MAUI preview instability** — the preview-3 package gap (Microsoft.Extensions.ServiceDiscovery, Npgsql.EFCore) currently blocking the Bridge full build is unrelated to this migration but worth flagging: the Blazor adapter doesn't rely on those packages.
- **Pre-release private repo posture** — no external-contributor friction during migration. Public release remains gated on LLC formation.
- **Pre-community BDFL bus factor** — the migration is a single-maintainer effort. UPF's Cold Start Test (Stage 2) is load-bearing: a future contributor must be able to resume at any milestone boundary.

### Sequencing

- **M0 — Extract contracts into `ui-core`.** Audit every Razor component; author four-contract specs; prove the pattern on one component. No adapter changes.
- **M1 — Finish G37 SunfishDataGrid as Razor; retrofit contracts.** G37 completes its Razor trajectory, merges, then its four-contract spec lands in `ui-core/Contracts/DataDisplay/` as the worst-case validation.
- **M2 — Scaffold `ui-adapters-react`.** ADR 0020 picks build tool, CSS strategy, state primitive. First component implemented natively in React against the `ui-core` contract.
- **M3 — Parity harness stands up.** Cross-adapter equivalence tests run in CI across Blazor and React. Harness is the merge gate from M3 forward.
- **M4 — Scaffold `ui-components-web`.** Lit + TypeScript + Vite. Third consumption track joins the parity harness. ADR 0019 captures NPM publish process.
- **M5 — Fan out remaining components across three tracks** in leaf-to-root dependency order. Each family's migration is a coordinated triple (Blazor port + React port + WC port) with parity as the merge gate.

### Kill Criteria (UPF anti-pattern #11: zombie projects)

The migration is explicitly cancellable if any of the following fires:

1. **M0 contract extraction proves too expensive.** If after one month of M0 work the contracts-per-week rate predicts more than six months to cover the full library, pause and scope down: commit to contract-extraction for the top-20 most-used components, defer the long tail.
2. **Parity harness (M3) cannot cleanly enforce cross-track equivalence.** If the harness requires per-component adapter-specific hacks that aren't derivable from the contract, the contract is under-specified — pause and revisit M0.
3. **React adapter (M2) cannot implement a meaningful component against the contract without contract churn.** Expected small churn is normal; if every contract needs revision when React hits it, the contract shape is wrong and M0's principles need revisiting.
4. **WC track (M4) breaks the parity harness repeatedly.** If Shadow DOM encapsulation, attribute serialization, or DSD SSR produces material differences from the Blazor/React tracks that can't be reconciled, the WC track de-scopes to a subset of components — not all components need all three tracks.

Absent those conditions, the migration runs to M5 completion.

## Next Steps

Proceed to **Stage 01 Discovery** with a `sunfish-gap-analysis`-shaped deliverable:

- Inventory every Razor component in `packages/ui-adapters-blazor/Components/**/*.razor`, categorize by complexity, tag each with its M-milestone target.
- Inventory every implicit contract currently encoded in Razor — `@IconProvider` DI couplings, typed child-slot patterns, `RenderFragment<T>` generic contexts, interaction hooks — that the four-contract spec must capture.
- Identify the parity-test-harness gap — what test shape proves "the same contract, three implementations, equivalent output."
- Identify the M0 proof-point component (likely `SunfishButton` or `SunfishSearchBox`) and its four-contract draft.
- Identify the M2 build-tool decision — Vite vs. esbuild vs. Rollup for the React adapter.
- Identify the CSS strategy — tokens-only vs. CSS-Modules vs. emotion for React; `::part` exposure for the WC track.
- Identify the contract authoring guide scope — what a new component contract author needs to know.
- Identify accessibility regression risk — the `_shared/design/accessibility.md` WCAG 2.2 AA contract must survive the migration; axe-core CI gate covers the WC track, React Testing Library a11y assertions cover the React track, bUnit + a11y helpers cover the Blazor track.
- Identify kill-criteria telemetry — how do we observe the conditions above at milestone boundaries?

**Expected Stage 01 output:** `01_discovery/output/ui-architecture-migration-discovery-2026-04-21.md` (or later date at landing). UPF Stage 0 quality bar — the Decompose-Suspend-Validate principle applies; list assumptions with "VALIDATE BY → IMPACT IF WRONG" shape before advancing to Stage 02 Architecture.

## Cross-References

- [ADR 0017 (revised) — Spec-First UI Contracts with Native Framework Adapters and an Optional Web-Components Consumption Track](../../../docs/adrs/0017-web-components-lit-technical-basis.md) — the decision being executed.
- [ADR 0014 — UI Adapter Parity Policy](../../../docs/adrs/0014-adapter-parity-policy.md) — the parity guarantee the migration enforces via its harness.
- [CLAUDE.md §Framework-Agnostic Design Principle](../../../CLAUDE.md) — the principle ADR 0017 now implements.
- [`_shared/product/vision.md` §Pillar 3](../../../_shared/product/vision.md) — the framework-agnostic commitment this migration makes real.
- [`_shared/engineering/planning-framework.md`](../../../_shared/engineering/planning-framework.md) — UPF adoption; applies to Stages 01–02.
- [`_shared/design/accessibility.md`](../../../_shared/design/accessibility.md), [`_shared/design/internationalization.md`](../../../_shared/design/internationalization.md), [`_shared/design/tokens-guidelines.md`](../../../_shared/design/tokens-guidelines.md) — design contracts that must survive the migration.
- [`accelerators/anchor/README.md`](../../../accelerators/anchor/README.md) — the MAUI Blazor Hybrid consumer shape; continues consuming `ui-adapters-blazor` natively.
- [`icm/pipelines/sunfish-feature-change/routing.md`](../../pipelines/sunfish-feature-change/routing.md) — execution-variant routing for M0–M5.
- [`icm/pipelines/sunfish-gap-analysis/routing.md`](../../pipelines/sunfish-gap-analysis/routing.md) — Discovery-variant routing.
