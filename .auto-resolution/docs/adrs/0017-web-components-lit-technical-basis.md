---
id: 17
title: Spec-First UI Contracts with Native Framework Adapters and an Optional Web-Components Consumption Track
status: Accepted
date: 2026-04-20
tier: ui-core
concern:
  - ui
  - accessibility
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0017 — Spec-First UI Contracts with Native Framework Adapters and an Optional Web-Components Consumption Track

**Status:** Accepted
**Date:** 2026-04-20
**Revised:** 2026-04-21 — Inverted the original WC-first decision after an architectural review (see §Revision note). Previous revision had `ui-components-web` as the canonical source of truth and Blazor/React as "thin wrappers"; that drifted from the framework-agnostic principle already captured in [CLAUDE.md](../../CLAUDE.md) and biased the architecture toward DOM/custom-element semantics in ways that fight Blazor's `RenderFragment`, React's children composition, virtualization, and native/mobile paths.
**Resolves:** Establish what the canonical, framework-agnostic UI asset actually is in Sunfish, how native framework adapters consume it, and where Web Components sit in the tracks of consumption. Clarify the typed-code preference, the polyglot-via-API commitment, and the migration plan from today's Razor-only library.

---

## Context

[ADR 0014](0014-adapter-parity-policy.md) commits to "framework-agnostic UI" as a first-class guarantee — any front-end UI is a legitimate target. [CLAUDE.md §Framework-Agnostic Design Principle](../../CLAUDE.md) makes the mechanism explicit: *define the contract in foundation/ui-core (framework-agnostic types, interfaces), then implement in adapters, then compose in blocks, and don't let adapters drive the design*. The product vision ([_shared/product/vision.md](../../_shared/product/vision.md) §Pillar 3) commits the same way.

In practice, every component shipped today in `packages/ui-adapters-blazor/Components/` is authored as a Razor (`.razor`) file. `ui-core` holds only weak contracts (mostly data-request/sort/filter types); the rich contracts a component needs — semantic props, event shapes, keyboard model, ARIA roles, focus behavior, state transitions — live implicitly in the Razor file. The Blazor adapter is therefore the *de facto* specification, and the "React adapter is on the roadmap" line commits work that cannot begin until those contracts are actually extracted.

There are two realistic ways to close this gap, and ADR 0017's previous revision picked the wrong one:

**Option A (previous revision): Web-Components-as-canonical.** Author every component once in Lit + TypeScript under `packages/ui-components-web/`. Blazor and React become thin wrappers that translate custom-element attributes and DOM events into their framework's idioms.

**Option B (this revision): Spec-first contracts with native adapters.** `ui-core` holds the contracts. Every adapter is a native first-class implementation. Web Components become one of the consumption tracks, not the canonical source.

Option A buys exactly one thing Option B doesn't: a framework-agnostic *consumption* path for a plain-JS or no-npm-dependency consumer who drops `<sunfish-datagrid>` into their app. That path matters and Option B preserves it — but as a peer implementation, not a canonical one.

Option A loses — and loses enough that it fails the "adapters should feel first-class, not wrapped" bar Sunfish set:

- **Blazor `RenderFragment` and typed child slots** do not survive translation to HTML slots. A component like `<SunfishDataGrid>` whose columns are `<SunfishGridColumn TField="OrderDate">` declarative children in Razor would become attribute-serialized data through a custom element boundary. Generics, typed `TItem` child contexts, and C#-typed event payloads all degrade.
- **React children composition and render props** similarly collapse to slot-attributes. React's strength in composing small pieces with cheap re-render isn't expressible through a custom-element shell.
- **Virtualization** (Blazor `<Virtualize>`, `react-virtual`) operates on framework render trees. A WC virtualizer has to re-implement framework-side hooks rather than participate in them.
- **SSR** with Declarative Shadow DOM is workable but not seamless; Blazor Server's pre-render and React SSR both need hydration gymnastics that native Blazor/React don't.
- **Mobile and native paths** still require WebView sandwiching. MAUI-native, WinUI, AvaloniaUI, React Native, and any future pure-native track cannot consume custom elements. Anchor (the scaffolded MAUI Blazor Hybrid accelerator) only works because it embeds a browser.

Option B keeps native Blazor and React code idiomatic. Parity across adapters is enforced by a test harness (ADR 0014 follow-up) exercising the same spec, not by a shared DOM artifact. A third consumption track (Lit + TypeScript + npm) ships for plain-JS consumers — peer to Blazor and React, not the root.

Browser-platform features maturing through 2026 — Declarative Shadow DOM, Scoped Custom Element Registries — remain relevant: they lower the cost of the WC consumption track. They do not change which track is canonical.

### Revision note

The original revision of this ADR adopted Option A. That decision stood for one day of planning. Review surfaced that:

1. The "thin wrapper" adapter framing was an admission that Blazor and React were no longer first-class — an inversion of the adapter-parity guarantee.
2. The canonical-DOM-artifact approach locked out native desktop and mobile targets that don't use WebViews.
3. CLAUDE.md's framework-agnostic principle already specified spec-first contracts as the mechanism; the WC-first ADR drifted from it without replacing the principle.

No code shipped against the Option A decision. The `packages/ui-components-web/` folder was never scaffolded; no Blazor components were rewritten. Reverting the ADR costs only documentation updates. That is why this ADR is revised in place rather than superseded — the decision is being corrected during planning, not replaced after implementation.

---

## Decision

### 1. `ui-core` is the canonical spec layer (framework-agnostic)

Every Sunfish UI component has, in `packages/ui-core/`, a set of **four contracts** that together define what the component is — independent of any rendering technology:

- **Semantic contract** — props, events, slots/regions, async states, generic parameters where applicable. Expressed as framework-neutral TypeScript-like / C# interfaces + records.
- **Accessibility contract** — roles, keyboard model, labels, live-region announcements, focus management, touch-target sizing (per [accessibility.md](../../_shared/design/accessibility.md) WCAG 2.2 AA).
- **Styling contract** — design-token surface, size/color/state variants, the public CSS custom-property names, the `::part` exposure map (for adapters that emit Shadow DOM).
- **Interaction contract** — focus, validation, overlay positioning, selection, drag, virtualization hooks, keyboard-within-group rules.

These contracts are the shared asset. They are what every adapter implements and what every parity test asserts. They are published as part of `Sunfish.UICore`.

### 2. Native framework adapters are first-class implementations

**`ui-adapters-blazor`** — native Blazor implementation. Razor files own component logic. `RenderFragment<T>`, typed `[Parameter]`, `EventCallback<T>`, JS interop modules, generic component type parameters all stay native. No DOM-wrapper translation layer.

**`ui-adapters-react`** — native React implementation (new package, to be scaffolded at M2). Functional components, children props, JSX, `useRef` / `useEffect`, proper React-event types. No custom-element wrappers under the hood.

Each adapter:

- Targets its framework's idioms directly.
- Consumes `ui-core` contracts for semantic shape, a11y behavior, styling tokens, and interaction rules.
- Ships its own tests (bUnit for Blazor, React Testing Library for React) that exercise the contract in-framework.

### 3. Web Components are a third peer consumption track

**`ui-components-web`** (new package, Lit + TypeScript, npm-published) — a Lit-based implementation of the same `ui-core` contracts, shipped as an npm package for consumers who want framework-agnostic custom elements (plain-JS apps, React apps without the Sunfish React adapter, Vue/Angular/Svelte via their native WC integration, legacy WebView shells).

This package is a **peer** to `ui-adapters-blazor` and `ui-adapters-react`, not their canonical source:

- Authored in Lit + TypeScript, built with Vite (or equivalent) to ES modules.
- Not included in `Sunfish.slnx` (JavaScript package, not a `.csproj`).
- Tested with Web Test Runner + Playwright; a11y asserted via axe-core.
- Ships independently to npm under `@sunfish/ui-components-web`.
- Implements the same `ui-core` contract; parity-tested against Blazor and React.

Declarative Shadow DOM, Scoped Custom Element Registries, and modern WC tooling all apply to this track — they just don't cross into the Blazor and React adapters.

### 4. Parity harness is what enforces cross-track equivalence

The parity-test infrastructure originally envisioned as "ADR 0014 follow-up" becomes load-bearing here: it takes the same `ui-core` contract and asserts all three tracks (Blazor, React, WC) produce equivalent observable behavior — semantic DOM structure, ARIA shape, keyboard handling, rendered styling tokens, event payloads.

Parity failures are bugs against the spec: fix the adapter, not the spec, unless the spec itself is wrong.

### 5. Authoring framework for the WC track: Lit + TypeScript

When we do author the WC consumption track, we use **[Lit](https://lit.dev)** + **TypeScript**:

- Broadest Web Components ecosystem adoption; ~5 KB runtime.
- Google-sponsored but framework-neutral in posture.
- Template-literal syntax produces consistently high-quality output from AI assistants.
- FAST (Microsoft) was considered and rejected on ecosystem breadth. Stencil and vanilla Custom Elements were considered and rejected on tooling complexity and authoring boilerplate.

The Lit choice is scoped to `ui-components-web` only. Blazor and React adapters do not use Lit.

### 6. Typed code is the default across every track

Typed code is the default across all three tracks:

- TypeScript in `ui-core` (for cross-framework TS/JS consumers), `ui-adapters-react`, and `ui-components-web`.
- C# in `ui-adapters-blazor`.
- Rust / Go / Kotlin / other typed-WASM consumers reaching `ui-components-web` bring their own type systems via `web-sys` bindings or equivalent.
- Untyped JavaScript is not forbidden for consumers but Sunfish ships no untyped JS itself.

### 7. Polyglot server participation via API layer (unchanged)

Any language that can't reach a browser UI directly — Python, Ruby, Java, older .NET — participates through Sunfish's HTTP / gRPC / WebSocket API surfaces. This ADR does not add API infrastructure; it records that the API layer is the universal participation mechanism for non-UI-consumer languages.

### 8. Delivery paths (mobile + desktop)

Three adapter tracks give three delivery stories, and they can mix:

- **PWA via the WC track or React track** — installable on iOS, Android, Windows, macOS, Linux.
- **.NET MAUI Blazor Hybrid** (the Anchor accelerator) — native iOS / Android / Windows / macOS via `BlazorWebView` consuming `ui-adapters-blazor` directly. Does **not** require the WC track to be scaffolded; the Blazor adapter is first-class.
- **Tauri 2** or **Capacitor** shells — Rust- or Ionic-backed cross-platform desktop/mobile consuming the WC track or the React track.
- **Electron** — traditional JS-runtime desktop shell consuming WC or React.
- **Future native tracks** (MAUI-native, AvaloniaUI, WinUI, React Native) — reachable later because spec-first keeps those paths open; WC-canonical would have closed them.

Touch-target sizing (WCAG 2.2 AA ≥24×24 CSS px) and responsive density are enforced through provider tokens in the styling contract; no per-component mobile-vs-desktop code.

### 9. Testing strategy

- **`ui-core`** — contract-level unit tests (pure TS/C# logic that lives in the spec layer).
- **`ui-adapters-blazor`** — bUnit, as today.
- **`ui-adapters-react`** — React Testing Library + Playwright for rendered behavior.
- **`ui-components-web`** — Web Test Runner + Playwright; axe-core for a11y.
- **Parity harness** — cross-track equivalence against `ui-core` contracts; the ADR 0014 follow-up this ADR elevates.

---

## Migration plan (phased)

### Phase M0 — Extract contracts into `ui-core`

Audit `packages/ui-adapters-blazor/Components/**/*.razor` and author the four-contract spec per component into `packages/ui-core/Contracts/`. This is the largest conceptual migration — it moves the de-facto spec out of Razor and into an explicit, reviewable artifact. No runtime change; Razor implementations remain untouched.

One component (probably `SunfishButton` or `SunfishSearchBox`) is the proof point: extract its contracts, assert the existing Razor implementation against them via a new parity-harness-readiness test, confirm the contract shape is workable.

**Exit criteria:** at least 10% of components have explicit `ui-core` contracts and existing Blazor implementations assert against them.

### Phase M1 — Finish G37 SunfishDataGrid as Razor, retrofit contracts

G37 is mid-flight. It completes on its current Razor trajectory. Once it merges, its contracts are retrofitted into `ui-core/Contracts/DataDisplay/SunfishDataGrid.*` — the same four-contract shape M0 establishes, but for the largest and most complex component in the library. This proves the contract model handles the worst case.

### Phase M2 — Scaffold `ui-adapters-react`

New package `packages/ui-adapters-react/` (reserved ADR 0020 is this ADR's follow-up). Select a build tool (Vite vs. esbuild vs. Rollup — M2 picks one), a CSS strategy (CSS Modules vs. tokens-only vs. emotion — M2 picks one), and a state primitive preference (hooks-only vs. optional store integration — M2 picks one).

First component ported: the same one M0 proved — `SunfishButton` or `SunfishSearchBox`. React implementation reads the `ui-core` contract, implements natively, passes a contract-level parity test against the Blazor implementation.

### Phase M3 — Parity harness

A shared harness lives in (or near) `packages/ui-core/` test assets. It runs adapter-specific harness adapters that render each contract in its target framework and assert equivalence — same ARIA shape, same keyboard model, same token-rendered styling, same event payloads.

Harness runs in CI across Blazor and React as soon as React ships its first component. WC track joins at M4.

### Phase M4 — Scaffold `ui-components-web` (third consumption track)

`packages/ui-components-web/` scaffolds with Lit + TypeScript + Vite. First component implemented: the same one from M0/M2. Parity harness expanded to cover the WC track. The package is npm-publishable so plain-JS / framework-agnostic consumers can install `@sunfish/ui-components-web` directly.

### Phase M5 — Fan out remaining components across three tracks

Component families migrate in dependency order (leaf → root), each as a self-contained PR with parity coverage in all three tracks:

1. Buttons (leaf)
2. Editors (simple controls)
3. Feedback (alerts, toasts)
4. Charts (self-contained)
5. Forms (composes simpler controls)
6. DataDisplay (cards, lists, trees — grid already done in M1)
7. Overlays (dialogs, popovers, drawers)
8. Navigation (menus, tabs, breadcrumbs)
9. Layout (shell, stack, grid-layout — churns everything; done last)

Each family's migration is a coordinated triple: Blazor port (usually a thin refactor of existing Razor against the now-explicit contract), React port (new), WC port (new). Parity tests are the merge gate.

---

## Consequences

### Positive

- **"Any front-end UI" stays technically true.** Blazor, React, and the WC track all consume the same spec; a plain-JS consumer uses `@sunfish/ui-components-web` directly; frameworks not yet adapted can consume the WC track in the meantime.
- **Native idiomatic fit per framework.** Blazor keeps `RenderFragment`, typed child slots, generics, `EventCallback`, JS interop modules. React keeps children, JSX, hooks, render props. Neither fights a DOM-wrapper boundary.
- **Future native tracks remain reachable.** A MAUI-native, AvaloniaUI, WinUI, or React Native adapter can implement the same spec without needing a WebView. WC-canonical would have closed that door.
- **Contract becomes a first-class reviewable artifact.** Accessibility, keyboard, token surface, event shape — all explicit in `ui-core` rather than implicit in 40+ Razor files.
- **Parity harness pays for itself.** ADR 0014's adapter-parity policy becomes enforceable — the harness is the enforcer, not a downstream aspiration.
- **Commercial compat surfaces** (`compat-telerik` and planned Kendo/Infragistics/Syncfusion/Oracle JET) stay unchanged. Compat is about API surface; contracts don't change that.
- **AI prompt density** stays strong — each track uses the dominant pattern for its framework (Razor for Blazor, TSX for React, Lit for WC), all with ample training-data volume.

### Negative

- **Three implementations per component instead of one-plus-two-wrappers.** Roughly 40 components × 3 tracks = ~120 implementations over the life of the migration. Blazor is mostly a refactor of what already exists; React and WC are new. Offset: each implementation is natively idiomatic and therefore simple.
- **Parity harness is load-bearing.** If it's incomplete or flaky, cross-track drift goes undetected. M3 standup of the harness is a priority, not a nice-to-have.
- **`ui-core` grows considerably.** From a handful of data-request types today to ~40 component contracts with four slices each. Contract churn during M0–M5 is real — stabilize as fast as practical.
- **Contributors learn three toolchains.** C# + Razor + bUnit; TypeScript + React + RTL; TypeScript + Lit + Web Test Runner. The WC track is the newest to this codebase; M4's scaffolding includes the CI wiring and contributor docs.
- **JavaScript build tooling enters the repo** (at M2 for React, M4 for WC). npm, TypeScript, Vite/esbuild/Rollup become part of CI. Scaffolding-CLI gets new templates.

### Follow-ups

1. **ADR 0019** — NPM publish + versioning process for `ui-components-web`. Triggers when M4 nears completion.
2. **ADR 0020** — React adapter scaffolding choices (reconciler integration, CSS strategy, state primitives). Triggers at M2 start.
3. **Design doc — Parity test harness** (ADR 0014 follow-up). Priority: M3 is a hard requirement, so the design doc lands in M2 so M3 can implement.
4. **Design doc — Declarative Shadow DOM + SSR patterns** for the WC track on Blazor-Server-hosted pages. Triggers at M4.
5. **Design doc — Contract authoring guide.** How a four-contract spec is written, reviewed, and evolved. Lands in M0 as part of the proof-point component.
6. **Update CLAUDE.md** — the Framework-Agnostic Design Principle already describes spec-first; add an explicit link to this ADR and a note that `ui-components-web` is a peer consumption track.

---

## References

- [ADR 0006](0006-bridge-is-saas-shell.md) — Bridge Is a Generic SaaS Shell (Bridge consumes adapters, not contracts directly).
- [ADR 0014](0014-adapter-parity-policy.md) — UI Adapter Parity Policy (this ADR makes parity enforceable via contracts + harness).
- [ADR 0015](0015-module-entity-registration.md) — Module-entity registration (prior example of `foundation-*` package split for concerns that need their own tier).
- [CLAUDE.md §Framework-Agnostic Design Principle](../../CLAUDE.md) — the principle this ADR finally implements.
- [_shared/product/vision.md §Pillar 3](../../_shared/product/vision.md) — framework-agnostic commitment this ADR realizes.
- [Lit documentation](https://lit.dev) — authoring framework for the WC track.
- [MDN Web Components](https://developer.mozilla.org/en-US/docs/Web/API/Web_components) — browser primitives the WC track builds on.
- [Declarative Shadow DOM (web.dev)](https://web.dev/articles/declarative-shadow-dom) — SSR-enabling shadow-tree syntax for the WC track.
- [Scoped Custom Element Registries (WICG)](https://wicg.github.io/webcomponents/proposals/Scoped-Custom-Element-Registries.html) — multi-library composition for WC consumers.
- [Tauri 2.0](https://v2.tauri.app/) — cross-platform shell path consuming the WC or React track.
- [Blazor Hybrid with .NET MAUI (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app) — native-app path for the Blazor track (Anchor's substrate).
- [Web Test Runner](https://modern-web.dev/docs/test-runner/overview/) — WC-track test framework.
- [axe-core](https://github.com/dequelabs/axe-core) — accessibility testing engine.
