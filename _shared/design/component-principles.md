# Component Principles

**Status:** Accepted
**Last reviewed:** 2026-04-19
**Governs:** Every component in `packages/ui-core`, `packages/ui-adapters-*`, and component surfaces in `blocks-*` that ship UI.
**Companion docs:** [tokens-guidelines.md](tokens-guidelines.md), [adapter-parity.md](../engineering/adapter-parity.md), [architecture-principles.md](../product/architecture-principles.md).
**Agent relevance:** Loaded by agents working on ui-core, ui-adapters-*, or UI surfaces in blocks. High-frequency for UI work.

Sunfish's framework-agnostic claim rests on a clean split between what components *are* (UI Core contracts) and how they *render* (UI adapters + provider themes). This document codifies the rules that keep that split honest.

## Three layers, three responsibilities

```
┌──────────────────────────────────────────────────────────────────┐
│ Provider themes          CSS variables, icon sets, visual tokens │
│ (FluentUI, Bootstrap,    (live inside each adapter, per-provider) │
│  Material, …)                                                     │
├──────────────────────────────────────────────────────────────────┤
│ UI Adapters              Framework-specific rendering             │
│ (Blazor, React, …)       Components that consume UI-Core contracts│
├──────────────────────────────────────────────────────────────────┤
│ UI Core                  Headless contracts, state models,        │
│ (Sunfish.UICore)         interaction semantics, accessibility     │
│                          shapes, provider interfaces, render-     │
│                          agnostic data types                      │
└──────────────────────────────────────────────────────────────────┘
```

- **UI Core never knows about Blazor or React.** It compiles on any .NET target; no `Microsoft.AspNetCore.Components` references, no `@inject`, no `RenderFragment` (that's Blazor-specific). UI Core exposes things like `ISunfishCssProvider`, `ISunfishIconProvider`, `ISunfishJsInterop`, `ISunfishRenderer`, `IClientSubscription<T>`, `IClientTask<T>` — pure C# contracts.
- **Adapters import UI Core and one framework.** `Sunfish.UIAdapters.Blazor` references `Sunfish.UICore` + `Microsoft.AspNetCore.App` framework. It implements UI-Core contracts for Blazor and provides the actual component library under `Components/{Buttons, Charts, DataDisplay, Editors, Feedback, Forms, Layout, Navigation, Overlays, Utility}`.
- **Providers style the adapter.** Within an adapter, provider sub-packages (`Providers/FluentUI`, `Providers/Bootstrap`, `Providers/Material`) ship CSS and icon sets. Tenants / apps pick a provider at startup; components call into the provider via `ISunfishCssProvider`.

## Core principles

### 1. Headless-first

Every UI-Core contract is describable without rendering. State transitions, event shapes, accessibility roles, and data models live in UI Core. What a "grid column resize" means — when it starts, what arguments it carries, what it updates — is a UI-Core concern. How it looks on screen is an adapter concern.

When in doubt: if a contract can only be expressed in Blazor or React terms, it doesn't belong in UI Core.

### 2. Contracts over components in ui-core

UI Core ships **interfaces and data records**, not components. There is no `SunfishButton` in UI Core. There is an `IButtonState` or `ButtonOptions`-style contract that the Blazor adapter's `SunfishButton` implements and the React adapter's `<SunfishButton>` implements.

### 3. Component family organization

Adapters group components into a small, stable set of families. The current families in `ui-adapters-blazor/Components/`:

| Family | Contents |
|---|---|
| Buttons | `SunfishButton`, `SunfishButtonGroup`, `SunfishChip`, `SunfishFab`, `SunfishIconButton`, `SunfishSegmentedControl`, `SunfishToggleButton`, `SunfishSmartPasteButton`, `SunfishSpeechToTextButton`, `SunfishSplitButton` |
| Charts | `SunfishChart`, `SunfishStockChart`, related axes/series/tooltip types |
| DataDisplay | `SunfishDataGrid`, `SunfishGridColumn`, `SunfishCard`, list/tree views |
| Editors | Input controls |
| Feedback | Alerts, toasts, progress |
| Forms | `SunfishForm` composite |
| Layout | Shell, stack, grid-layout |
| Navigation | Menu, tabs, breadcrumb |
| Overlays | Dialog, popover, drawer |
| Utility | Tokens, loaders, misc |

New components land in an existing family. Introducing a new family needs a deliberate decision — if you're thinking about `Components/MyThing/MyThing.razor`, it almost certainly belongs under one of the above.

### 4. Parameters are typed records, not loose primitives

When a component takes ≥3 primitives, wrap them in a typed record. `SunfishGridColumn`'s configuration surface is a declarative record consumed by the grid, not a constructor with 12 positional arguments.

### 5. Events are named, typed records

EventCallbacks carry a single record argument that describes the event. Examples from the repo:

- `RowDragDropEventArgs`
- `ColumnReorderEventArgs`
- `GridEditEventArgs`

Rules:

- Named `<Component><Action>EventArgs`.
- Record, not class.
- Contains enough context for the consumer to act without re-querying state.
- Cancelable events expose a `Cancel` property (or a `CancelableEventArgs`-style base).

### 6. State lives where it's authored, not where it's rendered

The component owns minimal state — typically just the interaction state (is-hovered, is-resizing, focus position). Data state (rows, columns, selection) is a parameter, updated by parent via events. Two-way binding (`@bind-*` in Blazor) is the exception, not the rule — prefer explicit `ValueChanged` callbacks.

### 7. Accessibility is a contract, not a decoration

Each component's public contract includes:

- ARIA role and appropriate attributes
- Keyboard interaction map
- Focus behavior (initial focus, focus-trapping for overlays, focus-restore on dismissal)
- Screen-reader expectations

These belong on the component's docs or a sibling `Accessibility.md` — not left as implementation details. When building the parity test harness (ADR 0014 follow-up), these contracts are what adapters are verified against.

### 8. JS interop lives in adapters, never in ui-core

UI Core defines `ISunfishJsInterop` — the interface. The Blazor adapter implements it via `IJSRuntime`. React has no JS interop concept because it's native JS. UI Core doesn't know how interop happens; it only declares what interop is needed.

When a component needs JS (drag-drop, clipboard, measurement), the Blazor adapter owns both the `.razor`/`.razor.cs` consumer and the `wwwroot/js/*.js` implementation. A single JS module per component family is preferred (`marilo-datagrid.js`, `sunfish-datasheet.js`, etc.) to keep the interop surface scoped.

### 9. Composition over monolith

Compound components expose sub-components that configure the parent. `SunfishDataGrid` + `SunfishGridColumn` is the canonical example: the grid is the container; the columns are children that declaratively define the shape. Never flatten that into a giant `SunfishDataGrid` with 40 parameters.

Rules:

- The parent owns state; children register with the parent via cascading value or render tree inspection.
- Children are declarative (no imperative methods called from outside).
- Adding a feature is adding a new child type, not a new parameter on the parent, when the feature is column-specific or row-specific.

### 10. Provider theming at the CSS-variable layer

Visual differences (FluentUI vs. Bootstrap vs. Material) are expressed through CSS custom properties the components consume. Components never hardcode colors, spacing, or font stacks. See [tokens-guidelines.md](tokens-guidelines.md) for the token conventions.

The `ISunfishCssProvider` interface names the provider-scope a component should use. Components call `CssProvider.ResolveClass("data-grid__cell--locked")` (or similar) instead of hardcoding class names — the provider maps the semantic class to its own naming.

### 11. Icons are a provider surface

Components consume icons through `ISunfishIconProvider.GetIcon("icon.name")`. Icon sets ship as their own projects (`Icons/Tabler`, `Icons/Legacy`). Components never `<svg>` inline themselves; they render whatever the provider returns.

### 12. Parity is mandatory; exceptions are registered

Per [ADR 0014](../../docs/adrs/0014-adapter-parity-policy.md), a component change lands in every first-party adapter in the same PR, or registers an explicit, time-boxed exception in [`adapter-parity.md`](../engineering/adapter-parity.md). G37 `SunfishDataGrid` is an active bootstrap-phase exception — the grid exists in Blazor today and will add a React implementation when the React adapter lands.

## Framework-agnostic boundary

Principles 1–2 set the direction; this section names the boundary in concrete type-reference terms so reviewers can reject a leak without having to argue about it.

### What may live in `Sunfish.UICore`

- Pure C# types: `record`, `class`, `interface`, `enum`, `readonly record struct`.
- Types from `System.*`, `System.Collections.Generic.*`, `System.Threading.*`, `System.Threading.Tasks.*`.
- `Microsoft.Extensions.*` — DI, logging, options, configuration. Explicitly allowed because it's framework-neutral and consumed everywhere in .NET.
- Types declared elsewhere in `Sunfish.Foundation.*` and `Sunfish.UICore.*` itself.
- Accessibility contracts as C# records (WAI-ARIA role names as strings, keyboard maps as arrays), *not* as DOM or JSX constructs.

### What must not appear in `Sunfish.UICore`

- **Blazor**: `Microsoft.AspNetCore.Components.*`, `RenderFragment`, `RenderFragment<T>`, `EventCallback`, `EventCallback<T>`, `IJSRuntime`, `IJSObjectReference`, `ElementReference`, `[Parameter]`, `[CascadingParameter]`, `@inject`, `ComponentBase`, anything under `Microsoft.AspNetCore.*`.
- **React / any JS framework**: no TypeScript, no JSX-flavored types. (Moot in .NET, but worth stating — contracts that can only be expressed via `ReactNode`/`JSX.Element` don't belong here either.)
- **Any web-platform type** that presupposes a DOM: `HtmlString`, `MarkupString`, raw CSS class names baked into contracts, selectors, stylesheet imports.
- **File-extension-coupled types**: `.razor`, `.cshtml`, `.tsx`, `.jsx`, `.vue` — UI Core is `.cs` only.

### Slot-style rendering without `RenderFragment`

When a component needs adapter-provided content in a slot position (header, cell, empty-state), UI Core names the *slot* as a strongly-typed key + optional payload record; the adapter maps that to its framework's rendering primitive:

- Blazor → `RenderFragment` or `RenderFragment<T>`.
- React → `ReactNode` or a render-prop callback.
- Future adapters → whatever that framework's equivalent is.

UI Core's contract describes *what* the slot is for and *what data* it carries, never *how* it is rendered. See `ISunfishRenderer` in `packages/ui-core/Contracts/` for the seam shape.

### `.editorconfig` / analyzer enforcement (tracked follow-up)

Reviewer vigilance is the current gate. A Roslyn analyzer that flags `Microsoft.AspNetCore.Components.*` references inside `Sunfish.UICore` is a tracked follow-up once the parity test harness lands ([adapter-parity.md](../engineering/adapter-parity.md)). Until then, the rules above are cited in PR review per [code-review.md](../engineering/code-review.md) item #1 (architectural fit).

## Component lifecycle contract

Components go through these lifecycle states; adapters implement them per framework:

| State | Rule |
|---|---|
| **Constructed** | Parameters applied; no rendering yet. |
| **Initialized** | First render scheduled; no JS interop yet. |
| **Rendered** | DOM exists; JS interop allowed. |
| **Parameters-updated** | Re-render against new inputs; preserve focus / scroll / selection state unless parameters specify otherwise. |
| **Disposing** | Clean up JS handles, subscriptions, cancellation tokens. Components must be idempotent here — dispose can be called more than once. |

## Anti-patterns

- **Blazor-isms in UI Core.** `RenderFragment`, `EventCallback`, `IJSRuntime`, `Microsoft.AspNetCore.Components.*` — none of these belong in `Sunfish.UICore`.
- **Inline styles for anything semantic.** Color, spacing, typography go through provider tokens. Positional styles (inline flex-grow, width) can be inline when they're genuinely per-instance.
- **Hardcoded icon paths.** `<img src="/icons/foo.svg">` bypasses `ISunfishIconProvider`.
- **Giant parameter surfaces.** If a component has >10 parameters, split it or introduce a typed options record.
- **Global state as a dependency.** A component that reads from a static singleton is untestable. Inject `ICascadingValue<T>` or a parameter instead.
- **Framework-specific names in adapter contracts.** A Blazor-flavored event like `BlazorGridInteropArgs` should just be `GridInteropArgs`.
- **Shared state between adapters** (e.g. a `ui-shared` package between Blazor and React). Adapters are independent; shared needs live in UI Core.
- **JS interop from non-adapter code.** Domain modules never interop; they consume adapter components.
- **Mutation of input parameters.** Treat parameters as immutable inputs. If you need to edit, it's a two-way bound value with an explicit event, not an in-place mutation.

## When to write a new contract in UI Core

The headless-first rule means most new components start in UI Core. Ask:

1. **What state does this component own?** That goes in UI Core as a record or interface.
2. **What events does it raise?** Those go in UI Core as event-args records.
3. **What accessibility expectations does it carry?** Those go in UI Core as documented contract.
4. **What does it render?** That goes in each adapter.
5. **What does it look like?** That goes in provider themes via CSS variables.

If the answer to 1–3 is "none / trivial," the component may not warrant a UI-Core contract and can live adapter-only with a parity exception — but that exception must be logged per ADR 0014.

## Cross-references

- [tokens-guidelines.md](tokens-guidelines.md) — design-token conventions that provider themes implement.
- [adapter-parity.md](../engineering/adapter-parity.md) — ADR 0014 parity policy + exception register.
- [architecture-principles.md](../product/architecture-principles.md) §1 — framework-agnostic-core rule.
- `packages/ui-core/Contracts/` — current canonical headless contracts.
- `packages/ui-adapters-blazor/Components/DataDisplay/DataGrid/` — the richest component family to study composition patterns, JS interop, and accessibility contracts.
