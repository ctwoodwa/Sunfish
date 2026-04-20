# ADR 0017 — Web Components (via Lit) as UI Technical Basis; JS and WASM Consumption Tracks

**Status:** Accepted
**Date:** 2026-04-20
**Resolves:** Promote Web Components from implicit implementation detail to stated architectural commitment. Clarify the two consumer tracks (JavaScript and WebAssembly), the typed-code preference, the polyglot-via-API commitment, and the migration plan for existing Razor-authored UI components.

---

## Context

[ADR 0014](0014-adapter-parity-policy.md) states that "framework-agnostic UI" is a first-class commitment — any front-end UI is a legitimate target. In practice, every component shipped today in `packages/ui-adapters-blazor/Components/` is authored as a Razor (`.razor`) file. The Blazor adapter is therefore the *de facto* specification, and the "React adapter is on the roadmap" line in the vision commits work that cannot begin until UI Core contracts are actually renderable on a non-Blazor stack.

The product vision ([_shared/product/vision.md](../../_shared/product/vision.md)) further commits to:

- **Any front-end UI** as a technical reality, not a marketing claim.
- **Two consumption tracks** for typed code in the browser: JavaScript (React, Angular, Vue, vanilla TypeScript) and WebAssembly (Blazor as one example; Rust, Go, and others are peer targets).
- **Typed code as the default** across both tracks.
- **Polyglot server participation via API layer** — any backend language can participate through Sunfish's HTTP / gRPC / WebSocket surfaces without reaching the UI component layer.
- **Mobile and desktop delivery** through WebView-based paths (PWA, .NET MAUI Blazor Hybrid, Tauri 2, Capacitor, Electron).

Web Components (Custom Elements, Shadow DOM, HTML Templates, ES Modules) are the W3C-standard primitives that satisfy all of these commitments simultaneously. A Custom Element runs in every framework's render tree, every WebView, and every typed-code-to-WASM toolchain that binds to the DOM. Adopting Web Components as the UI authoring primitive is how "any front-end UI" becomes literally true rather than aspirational.

Browser-platform features maturing through 2026 strengthen the case:

- **Declarative Shadow DOM** (`shadowrootmode` attribute on `<template>`) enables server-rendered components without runtime JavaScript — natural for Blazor Server and any SSR-first host.
- **Scoped Custom Element Registries** (enabled by default in Chromium-based browsers in 2026) let multiple component libraries coexist in one app without name collisions.

The remaining question is *which authoring framework*, and how the migration from the current Razor-authored library proceeds.

---

## Decision

### 1. Authoring framework: Lit + TypeScript

Sunfish's UI components are authored as Web Components using **[Lit](https://lit.dev)** with **TypeScript**.

- Lit has the broadest adoption in the Web Components ecosystem (Google-sponsored but framework-neutral in posture), a ~5 KB runtime, and template-literal syntax that produces consistently high-quality output from AI assistants.
- TypeScript is the typed-code default on the JavaScript side (vision Pillar 3 and "Principles behind the vision" both make this explicit).
- FAST (Microsoft) was considered and rejected on ecosystem breadth and AI training-data density. Stencil and vanilla Custom Elements were considered and rejected on tooling complexity and authoring boilerplate, respectively.

### 2. New neutral authoring package: `packages/ui-components-web/`

Web Components are authored in a new package that does not target .NET and is not a `.csproj`:

```
packages/ui-components-web/
├── package.json                  ← NPM package
├── tsconfig.json
├── vite.config.ts                ← or equivalent build
├── src/
│   ├── buttons/
│   │   ├── sunfish-button.ts
│   │   └── sunfish-button.test.ts
│   ├── data-display/
│   │   ├── sunfish-data-grid.ts
│   │   └── …
│   └── …
├── dist/                         ← built ES modules (ignored by git; produced by `npm run build`)
└── README.md
```

- Built with Vite (or equivalent) to ES modules.
- Tested with Web Test Runner + Playwright.
- NPM-publishable so JS-track consumers (React adapter, vanilla JS apps, third parties) can consume without a .NET dependency.
- Not included in `Sunfish.slnx` (it's a JavaScript package, not a .NET project).
- The build produces JS modules that adapter packages import via standard ES module URLs.

### 3. Two consumption tracks (peer status)

Web Components authored once serve both major tracks:

#### WASM track

Compiled typed languages consuming Web Components in the browser. Examples:

- **Blazor** (C# / .NET compiled to WebAssembly) — the reference adapter because it's closest to Sunfish's .NET core and the founding team's depth. **This is the first-class WASM consumer; Blazor's first-class status is because it's the best-known typed-WASM path, not because Blazor is uniquely preferred.**
- **Rust** via `wasm-bindgen` or `yew` — an equally valid path; Rust WASM apps consume Sunfish Custom Elements via `web-sys` bindings.
- **Go** via TinyGo, **Kotlin/Wasm**, and other typed-language-to-WASM toolchains — supported wherever they produce viable browser consumption.

Blazor is the example of the WASM track in the near term; WASM broadly is the principle.

#### JavaScript track

- **React, Angular, Vue, Solid, Svelte, vanilla TypeScript** — use Web Components directly through their framework's standard integration (React props/refs, Angular native element support, Vue v-model, Solid's native HTML-element-like handling).
- Each framework may ship an optional `ui-adapters-<framework>` wrapper package (typed props, framework-native event wiring) — but this is an ergonomic convenience, not a requirement. A React app can consume `@sunfish/ui-components-web` directly.

### 4. Typed code is the default

Both tracks produce typed code. TypeScript on the JS side; C#, Rust, Go on the WASM side. Untyped JavaScript is not rejected, but:

- Sunfish's own codebase ships no untyped JS.
- Reference examples, templates, and documentation assume typed code.
- AI-generated snippets (artifacts from Claude or similar) are expected to be TypeScript, C#, or Rust in the canonical patterns.

### 5. Polyglot server participation via API layer

Any language that can't reach Web Components directly — Python, Ruby, Java, legacy .NET Framework, older Go, anything server-side — participates through Sunfish's HTTP / gRPC / WebSocket API surfaces. This ADR does not add new API infrastructure; it records that the API layer is the universal participation mechanism for non-WC-consumer languages.

### 6. Adapter shape change

Adapter packages become **thin wrappers** around the authoring-layer Custom Elements:

- **`ui-adapters-blazor`** — continues to ship Razor components, but each component is now a wrapper around a Custom Element imported from `ui-components-web`. The wrapper handles: typed `[Parameter]` plumbing, `EventCallback` bindings to DOM events, lifecycle hooks, JS interop for imperative method calls. No component logic lives in the Razor file beyond wiring.
- **`ui-adapters-react`** (future, P6) — ships React-idiomatic wrappers around the same Custom Elements. Typed props, React-style events, hooks where natural.

Provider themes and icon providers are **unchanged**. CSS custom properties penetrate Shadow DOM via host selectors; `::part()` pseudo-elements expose styling seams on Custom Elements. Tenants switching providers sees no component change.

### 7. Delivery paths (mobile + desktop)

Web Components are browser primitives; every WebView on every platform hosts them. Sunfish-built apps deploy via:

- **PWA** (Progressive Web App) — default path; installable on iOS, Android, Windows, macOS, Linux.
- **.NET MAUI Blazor Hybrid** — native iOS / Android / Windows / macOS rendering Razor (wrapping Web Components) via `BlazorWebView`. Natural fit for .NET-heavy teams and the recommended native-app path.
- **Tauri 2** (January 2025) — Rust-backed cross-platform desktop and mobile shells with a small footprint.
- **Capacitor** (Ionic) — mature web-to-native wrapper for iOS/Android hybrid apps with rich native-plugin ecosystem.
- **Electron** — traditional desktop shell where a heavier JavaScript runtime is already part of the team's tooling.

Touch-target sizing (WCAG 2.2 AA, ≥24×24 CSS px) and responsive density are enforced through provider tokens; no per-component mobile-vs-desktop code.

### 8. Testing strategy

- **Web Components:** Web Test Runner with Playwright for native browser-level component tests; accessibility assertions via axe-core.
- **Blazor wrappers:** bUnit continues as-is for Razor wrapper tests.
- **React wrappers (future):** React Testing Library + Playwright for rendered behavior.
- **Parity harness** (future, ADR 0014 follow-up): the same UI Core contract exercised through every adapter produces equivalent observable behavior.

---

## Migration plan (phased)

### Phase M0 — Land this ADR; scaffold the WC package

- ADR 0017 merges.
- `packages/ui-components-web/` scaffolds with `package.json`, `tsconfig.json`, Lit dependency, build tooling, and a README.
- Two example components (a simple one and a moderate one — `sunfish-button` and `sunfish-chip`) are authored WC-first in the new package and consumed by the Blazor adapter as wrappers. This proves the pattern end-to-end.

### Phase M1 — Finish G37 SunfishDataGrid as Razor

G37 is mid-flight. Stopping to rewrite would blow up the current work. G37 completes on its current trajectory (Razor-authored); it merges; this is the last Razor-native large component.

### Phase M2 — Migrate SunfishDataGrid to WC-first

Immediately after G37 merges, SunfishDataGrid (and its collaborators `SunfishGridColumn`, column menu, export integrations) migrate to Lit + TypeScript in `ui-components-web`. The Blazor adapter becomes a thin wrapper. This is the largest single migration effort; it gets priority because subsequent migrations of smaller components can follow the pattern the grid establishes.

### Phase M3 — Migrate remaining components in dependency order

Families migrate roughly from leaf to root (fewer dependents first, more dependents last):

1. Buttons (leaf family, many consumers but few dependencies)
2. Editors (simple controls)
3. Feedback (alerts, toasts)
4. Charts (self-contained)
5. Forms (composes simpler controls)
6. DataDisplay (cards, lists, trees — grid already done)
7. Overlays (dialogs, popovers, drawers)
8. Navigation (menus, tabs, breadcrumbs)
9. Layout (shell, stack, grid-layout — churns everyone else; done last)

Each family migrates as a self-contained PR with a parity checklist against ADR 0014.

### Phase M4 — Blazor adapter = thin wrapper layer

By the end of M3, every component in `ui-adapters-blazor/Components/` is a thin Razor wrapper around a Lit Custom Element. The Blazor adapter's role is reduced to typed `[Parameter]` surfaces, `EventCallback` plumbing, and JS interop for imperative calls.

### Phase M5 — React adapter (P6 of the roadmap)

`ui-adapters-react/` scaffolds with React-idiomatic wrappers around the same Custom Elements. Parity test harness validates equivalent behavior to Blazor wrappers. Parity matrix ([`_shared/engineering/adapter-parity.md`](../../_shared/engineering/adapter-parity.md)) updates from all-exceptions to actual coverage.

---

## Consequences

### Positive

- **"Any front-end UI" becomes technically true.** A consumer can use Sunfish components from React, Vue, Angular, vanilla JS, Blazor, Rust+WASM, or Web Components–only apps without adapter-specific reimplementation.
- **Typed code preference is enforced.** Components are TypeScript; WASM consumers bring their own type systems; untyped JavaScript is possible but not canonical.
- **Single codebase for mobile, desktop, and server-rendered apps.** WC + Declarative Shadow DOM + SSR-friendly Blazor means the same component ships to every surface.
- **AI prompt density improves.** Lit + TypeScript has far more training-data volume than Razor for most AI assistants; prompts produce better output.
- **Commercial vendor compat surfaces remain intact.** `compat-telerik` and planned compat packages (Kendo, Infragistics, Syncfusion, Oracle JET) continue to work because compat is about API surface, not underlying authoring.
- **Scoped Custom Element Registries make multi-library composition safe.** A bundle combining Sunfish components + third-party components + customer components doesn't collide.

### Negative

- **JavaScript build tooling enters the repo.** npm, TypeScript, Vite (or equivalent) become part of the Sunfish build pipeline. Contributors need to know both toolchains; CI grows.
- **Blazor adapter becomes less self-contained.** Razor files no longer own the component logic; debugging crosses a JS-interop boundary. Offset by simpler Razor files that do less.
- **G37 migration work is non-trivial.** SunfishDataGrid is the largest component in the library; its rewrite is a substantial effort (estimate: 2–4 focused weeks).
- **Server-side rendering requires Declarative Shadow DOM discipline.** Blazor Server consumers need to emit DSD-compatible markup; not all existing patterns cleanly translate.

### Follow-ups

1. **ADR 0019** — NPM publish + versioning process for `ui-components-web`. Triggers when the first external JS-track consumer needs it.
2. **ADR 0020** — React adapter scaffolding choices (reconciler integration, CSS strategy, state primitives). Triggers at P6 start.
3. **Parity test harness** — ADR 0014 follow-up. Triggers when the React adapter ships its first component.
4. **Declarative Shadow DOM + SSR pattern guide** — a design doc (not an ADR) documenting how Sunfish components emit SSR-compatible markup.
5. **Accessibility and i18n expanded docs** — vision Pillar 5 flagged follow-up docs under `_shared/design/` (accessibility.md, internationalization.md).

---

## References

- [ADR 0006](0006-bridge-is-saas-shell.md) — Bridge Is a Generic SaaS Shell (Bridge consumes the adapter layer).
- [ADR 0014](0014-adapter-parity-policy.md) — UI Adapter Parity Policy (the policy this ADR makes technically feasible).
- [Lit documentation](https://lit.dev) — the authoring framework adopted.
- [MDN Web Components](https://developer.mozilla.org/en-US/docs/Web/API/Web_components) — browser primitives Sunfish components are built on.
- [Declarative Shadow DOM (web.dev)](https://web.dev/articles/declarative-shadow-dom) — SSR-enabling shadow-tree syntax.
- [Scoped Custom Element Registries (WICG)](https://wicg.github.io/webcomponents/proposals/Scoped-Custom-Element-Registries.html) — multi-library composition mechanism.
- [Why You Should Use Web Components Now (Proud Commerce)](https://www.proudcommerce.com/web-components/why-you-should-use-webcomponents-now) — framing reference cited in the vision.
- [Tauri 2.0](https://v2.tauri.app/) — cross-platform desktop + mobile shell path.
- [Blazor Hybrid with .NET MAUI (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app) — native app path for the .NET stack.
- [Web Test Runner](https://modern-web.dev/docs/test-runner/overview/) — WC component test framework.
- [axe-core](https://github.com/dequelabs/axe-core) — accessibility testing engine (per vision Pillar 5).
- [_shared/product/vision.md §Pillar 3](../../_shared/product/vision.md) — the product-vision commitments this ADR realizes.
