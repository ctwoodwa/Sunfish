# Sunfish Design Language

**Status:** Accepted
**Last reviewed:** 2026-05-03
**Governs:** All UI surfaces in `packages/ui-core`, `packages/ui-adapters-*`, `blocks-*`, accelerators, and apps.
**Source docs consolidated:** [component-principles.md](component-principles.md), [tokens-guidelines.md](tokens-guidelines.md), [accessibility.md](accessibility.md), [internationalization.md](internationalization.md), [css-conventions.md](../engineering/css-conventions.md), [global-first-ux-design.md](../../docs/superpowers/specs/2026-04-24-global-first-ux-design.md).

---

## Philosophy

Sunfish's design language rests on four commitments that shape every decision below:

1. **Framework-agnostic.** UI contracts are defined in pure C#; rendering is an adapter concern. No Blazor or React types leak into `Sunfish.UICore`.
2. **Semantic-token-driven.** Visual properties are semantic names resolved at runtime by provider themes. Components never hardcode colors, spacing, or motion values.
3. **Inclusive by default.** WCAG 2.2 AA is the floor, not a compliance pass. RTL, 12-locale support, and reduced-motion handling are built into the foundation, not added on top.
4. **Composition over monolith.** Complex surfaces are assembled from small, focused components with explicit contracts — not a single component with dozens of parameters.

---

## Architecture

The design system is organized in three layers with strict dependency flow:

```
┌───────────────────────────────────────────────────────────────┐
│ Provider Themes                                               │
│ (FluentUI, Bootstrap, Material)                               │
│ CSS variables, icon sets, SCSS-compiled output                │
├───────────────────────────────────────────────────────────────┤
│ UI Adapters                                                   │
│ (Blazor, React)                                               │
│ Framework-specific components that implement UI Core contracts│
├───────────────────────────────────────────────────────────────┤
│ UI Core                                                       │
│ (Sunfish.UICore)                                              │
│ Headless contracts, state models, event shapes,               │
│ accessibility shapes, render-agnostic data types              │
└───────────────────────────────────────────────────────────────┘
```

- **UI Core** — pure `.cs` only. No `Microsoft.AspNetCore.Components.*`, no `RenderFragment`, no DOM types. Defines `ISunfishCssProvider`, `ISunfishIconProvider`, `ISunfishJsInterop`, `ISunfishRenderer`, and all event-arg records.
- **Adapters** — implement UI Core contracts per framework and ship component families under `Components/{Buttons, Charts, DataDisplay, Editors, Feedback, Forms, Layout, Navigation, Overlays, Utility}`.
- **Providers** — sub-packages per adapter (`Providers/FluentUI`, `Providers/Bootstrap`, `Providers/Material`). Deliver `_tokens.scss`, `_tokens-dark.scss`, per-component SCSS, and compiled CSS. A provider swap is a startup registration change; no component rebuild.

---

## Design Tokens

### Naming

`--sf-<category>-<role>[-<state>]`

- `--sf-` prefix is mandatory on every Sunfish token. No exceptions — it prevents collision with host-app or vendor tokens.
- **Category:** `color`, `font`, `space`, `radius`, `shadow`, `motion`, `z`, `breakpoint`.
- **Role:** semantic purpose, never a raw value (`primary`, `surface`, `text`, `danger-light`).
- **State** (optional): `hover`, `active`, `focus`, `disabled`.

Rules:
- Kebab-case only. `--sf-color-primary-hover`, never camelCase.
- Semantic before raw. `--sf-color-primary` not `--sf-color-blue-500`.
- No component names in token names. `--sf-color-primary`, not `--sf-color-button-primary`.
- No provider names in token names. `--sf-color-primary` is the same token in every provider; the value differs.

### Token categories

**Color**

| Group | Tokens |
|---|---|
| Primary | `--sf-color-primary`, `-hover`, `-active`, `-light` |
| Secondary | `--sf-color-secondary`, … |
| Semantic | `--sf-color-success/-light`, `--sf-color-warning/-light`, `--sf-color-danger/-light`, `--sf-color-info/-light` |
| Neutral | `--sf-color-bg`, `--sf-color-surface`, `--sf-color-text`, `--sf-color-text-muted`, `--sf-color-border` |

Raw palette tokens (`--sf-palette-blue-500`) exist but are provider-internal only. Components never consume `--sf-palette-*`.

**Typography**

```
--sf-font-family-base  --sf-font-family-mono
--sf-font-size-xs/sm/md/lg/xl/2xl
--sf-font-weight-regular/medium/semibold/bold
--sf-line-height-tight/base/relaxed
```

**Spacing**

`--sf-space-0/1/2/3/4/6/8/12/16/24/32` — 4px or 8px base scale; providers resolve to their native scale.

**Radius**

`--sf-radius-none/sm/md/lg/xl/pill/full`

**Elevation / Shadow**

`--sf-elevation-0/1/2/3/4/overlay` — Material-style elevation ladder; FluentUI maps its own shadow scheme.

**Motion**

```
--sf-motion-duration-fast/base/slow
--sf-motion-ease-standard/decelerate/accelerate
```

Components suppress these tokens under reduced-motion — see [Motion](#motion--animation).

**Z-index**

`--sf-z-dropdown/overlay/modal/toast/tooltip` — semantic stacking layers; providers maintain the same order.

**Breakpoints**

Consumed as SCSS variables (`$sf-bp-sm/md/lg/xl`) — CSS variables don't resolve inside media queries.

### Dark mode

Dark-mode values live in `_tokens-dark.scss`, scoped to `[data-sf-theme='dark']` on the root element. The `IThemeService` / theme picker toggles the attribute; components require no dark-mode awareness.

### Adding a token

A new semantic token is a design decision. Checklist before merging:
1. Is it semantic, not component-specific or brand-specific?
2. Does every provider have a reasonable value for it?
3. Is it in the right category?
4. Are `_tokens.scss` and `_tokens-dark.scss` updated in every provider in the same PR?
5. Is semantic intent documented? ("The outline shown on focusable elements when keyboard-navigating," not just the color.)

---

## CSS Conventions

### Two-tier class system (ADR 0025)

**Tier 1 — Public API (`sf-<component>`):** consumer-facing, stable across minor versions, BEM-structured.

| Shape | Purpose | Example |
|---|---|---|
| `sf-<component>` | Component root | `sf-button`, `sf-datagrid` |
| `sf-<component>__<slot>` | Element / slot inside root | `sf-dialog__title`, `sf-datagrid__row` |
| `sf-<component>--<modifier>` | Modifier on root | `sf-button--primary`, `sf-datagrid--size-small` |
| `sf-<component>__<slot>--<modifier>` | Modifier on slot | `sf-datagrid__row--selected`, `sf-datagrid__cell--selected` |

Double-underscore (`__`) for slots; double-hyphen (`--`) for modifiers.

**Tier 2 — Provider-internal (`sf-<provider>-<component>`):** implementation detail, freely renamed, consumers must not target these.

| Prefix | Provider |
|---|---|
| `sf-bs-` | Bootstrap |
| `sf-fluent-` | FluentUI |
| `sf-material-` | Material |

Provider-internal classes go through `ISunfishCssProvider` methods — never hardcoded in shared Razor.

### CSS logical properties

Physical direction properties (`margin-left`, `padding-right`, `left`, `right`, `text-align: left`) are **banned** in component CSS. Use logical equivalents:

| Physical | Logical |
|---|---|
| `padding-left/right` | `padding-inline-start/end` |
| `margin-left/right` | `margin-inline-start/end` |
| `left/right` (in `position`) | `inset-inline-start/end` |
| `text-align: left/right` | `text-align: start/end` |
| `border-top-left-radius` | `border-start-start-radius` |

When `html[dir="rtl"]` is set (Arabic, Hebrew, Farsi), browsers mirror logical properties automatically. No per-component overrides needed.

**What does not mirror under RTL:** numerals, brand logos, media controls (play icon), clocks.

### Component CSS rules

```scss
.sf-button--primary {
  background-color: var(--sf-color-primary);
  border-radius:    var(--sf-radius-md);
  padding:          var(--sf-space-2) var(--sf-space-4);
  font-size:        var(--sf-font-size-md);
  transition:       background-color var(--sf-motion-duration-fast) var(--sf-motion-ease-standard);
}
```

- Never `#hex`, `rgb()`, or magic numbers. If a value lacks a token, add the token first.
- Never hardcoded transition timings.
- Never inline styles for theming — always a CSS class + token-driven styles.
- Never `<svg>` inline — always `ISunfishIconProvider.GetIcon(name)`.

---

## Component Principles

### Core rules

1. **Headless-first.** State transitions, event shapes, accessibility roles, and data models live in UI Core. What a thing *means* is a UI Core concern; how it renders is an adapter concern.
2. **Contracts over components.** UI Core ships interfaces and records, not components. There is no `SunfishButton` in UI Core — there is a `ButtonOptions` record the adapters implement.
3. **Parameters are typed records.** When a component takes ≥3 primitives, wrap them in a typed record.
4. **Events are named records.** `<Component><Action>EventArgs`, record type, enough context to act without re-querying, cancelable events expose a `Cancel` property.
5. **State lives where it's authored.** Components own minimal interaction state (is-hovered, focus position). Data state is a parameter updated by the parent via events.
6. **Composition over monolith.** `SunfishDataGrid` + `SunfishGridColumn` is the pattern. The parent owns state; children register declaratively. Adding a feature is adding a child type, not a new parameter on the parent.
7. **JS interop stays in adapters.** UI Core defines `ISunfishJsInterop`; Blazor implements it via `IJSRuntime`. One JS module per component family preferred.
8. **Parity is mandatory; exceptions are registered.** Per ADR 0014, a change lands in every adapter in the same PR or registers a time-boxed exception in `adapter-parity.md`.

### Component families

| Family | Contents |
|---|---|
| Buttons | Button, ButtonGroup, Chip, Fab, IconButton, SegmentedControl, ToggleButton, SmartPasteButton, SpeechToTextButton, SplitButton |
| Charts | Chart, StockChart, axes/series/tooltip types |
| DataDisplay | DataGrid, GridColumn, Card, list/tree views |
| Editors | Input controls |
| Feedback | Alerts, toasts, progress |
| Forms | Form composite |
| Layout | Shell, stack, grid-layout |
| Navigation | Menu, tabs, breadcrumb |
| Overlays | Dialog, popover, drawer |
| Utility | Tokens, loaders, misc |

New components land in an existing family. A new family requires a deliberate decision.

---

## Accessibility

### Baseline

Sunfish targets **WCAG 2.2 Level AA** across the component library, all provider themes, and every user-facing accelerator surface. AA is the floor — AAA is aspirational per component where practical.

Key criteria for Sunfish components:

| Criterion | Requirement |
|---|---|
| 1.4.3 Contrast (Minimum) | 4.5:1 normal text / 3:1 large text, per provider + dark mode |
| 1.4.11 Non-text Contrast | 3:1 for component borders, focus rings, meaningful icons |
| 2.1.1 Keyboard | Every action reachable without a pointer |
| 2.4.7 Focus Visible | Focus ring always visible in the active provider |
| 2.4.11 Focus Not Obscured *(new 2.2)* | Sticky elements never hide the focused element |
| 2.5.7 Dragging Movements *(new 2.2)* | Every drag has a keyboard-accessible alternative |
| 2.5.8 Target Size *(new 2.2)* | ≥ 24×24 CSS px desktop; ≥ 44×44 mobile surfaces |
| 4.1.2 Name, Role, Value | Every interactive element has accessible name + correct role |

### Per-component contract

Every component's documentation carries a mandatory **Accessibility** section. Eight required entries:

1. ARIA role + attributes (cite the WAI-ARIA APG pattern)
2. Keyboard interaction map
3. Focus behavior (initial focus, trap, restore)
4. Screen-reader expectations (accessible-name source, live-region, SR matrix: NVDA+Firefox/Chrome, JAWS+Chrome, VoiceOver+Safari)
5. Reduced-motion adaptation
6. Color-contrast budget
7. Target-size compliance
8. Shadow DOM exposure (reflective ARIA or ElementInternals for forms)

Incomplete contracts fail review.

### Shadow DOM rules

- Open shadow roots only (`{ mode: 'open' }`). Closed shadow roots are prohibited.
- `aria-labelledby` and `aria-describedby` cannot cross shadow roots — use reflective ARIA (`el.ariaLabel`, `el.role`) or expose a typed string property instead.
- Form-associated components use `ElementInternals` with `attachInternals()`.

### Automated gate

axe-core (`@axe-core/playwright`) runs against every component. **Any violation at impact ≥ `moderate` fails the build.** `minor` findings are reported but non-blocking. Known pre-existing violations are tracked in `_shared/engineering/a11y-baseline.md` with owner and target date; unknown violations fail.

---

## Internationalization

### Baseline commitments

- **Unicode UTF-8 end-to-end.** Every source file, DB column, wire format, HTTP body, log record.
- **BCP-47 locale tags everywhere.** `en-US`, `es-419`, `zh-Hans`, `ar-SA`. Never ad-hoc strings like `"english"` or `"us"`.
- **Resource-file strings only.** No hardcoded English in component markup, templates, or notification bodies — Roslyn analyzer `SUNFISH_I18N_002` enforces this.
- **RTL-aware layout by default.** CSS logical properties (above) plus `:dir(rtl)` selectors for direction-specific rules.
- **Explicit timezones everywhere.** Naive `DateTime.Now` is banned; `TimeProvider` injection is mandatory.

### Supported locales

12 locales at v1, declared in `i18n/locales.json`:

| Tier | Locales |
|---|---|
| Complete (95%+ strings, 100% layout) | `en-US`, `es-419`, `pt-BR`, `fr`, `de`, `ja`, `zh-Hans`, `ar-SA`, `hi` |
| Bake-in (40%+ strings, 100% layout) | `he-IL`, `fa-IR`, `ko` |

Arabic (`ar-SA`) bakes in early to catch RTL layout regressions before they compound.

### Locale resolution chain

1. `?culture=` query override (test/demo only)
2. Per-user preference (user profile)
3. Per-tenant default (`TenantMetadata`)
4. `Accept-Language` header
5. Platform default (`en-US`)

### Formatting rules

- **Dates.** Store ISO-8601 UTC with offset; display via `CultureInfo` (.NET) / `Intl.DateTimeFormat` (JS) with explicit timezone.
- **Numbers.** Never string-interpolate raw numbers — use `ToString("N", culture)` or ICU `{count, number}` parameters.
- **Currency.** Store as `{ amount (minor units), currency (ISO-4217) }`; render via `Intl.NumberFormat` or `ToString("C", culture)`. Currency code and locale are independent.
- **Sort order.** Locale-aware collation (`CultureInfo.CompareInfo`, `Intl.Collator`) for any user-visible list. Ordinal comparison only for machine identifiers.
- **Pluralization.** ICU MessageFormat everywhere plural semantics matter. Never English-style ternary. Arabic has six plural categories; Slavic languages have four — the template must expose every category.

---

## Motion & Animation

### Reduced-motion policy

Every component that animates must honor two signals:
- `@media (prefers-reduced-motion: reduce)` — OS-level setting.
- `html[data-motion="reduced"]` — Sunfish per-user preference (`MotionPreference.Reduced`).

```css
.sf-panel__enter {
  transition: opacity var(--sf-motion-duration-base) var(--sf-motion-ease-standard);
}

@media (prefers-reduced-motion: reduce) {
  html:not([data-motion="full"]) .sf-panel__enter {
    transition: none;
    animation: none;
  }
}

html[data-motion="reduced"] .sf-panel__enter {
  transition: none;
  animation: none;
}
```

`html:not([data-motion="full"])` lets users who have OS reduced-motion set still opt back in explicitly.

| Suppressed | Preserved |
|---|---|
| Decorative fades, slides, scale transitions | State-indicating animation (spinner) |
| Parallax, auto-play video/GIF, marquees | Essential motion on explicit user action |
| Non-essential rotation | Information-conveying animation (chart render, progress bar) |
| Auto-scrolling banners | Focus transitions (become instant, not removed) |

---

## Icon System

Icons are a provider surface, not component-embedded assets.

```csharp
@inject ISunfishIconProvider Icons
<span>@Icons.GetIcon("icon.trash")</span>
```

- Icon names use dotted category prefix: `icon.action.save`, `icon.status.warning`, `icon.nav.home`.
- Icon sets are sub-packages under `ui-adapters-blazor/Icons/` (e.g., `Icons/Tabler`). Each implements `ISunfishIconProvider` and ships SVGs.
- Components never `<svg>` inline — always via the provider interface.
- Swapping the icon set is a startup registration change: `AddSunfishIcons<TablerIconProvider>()`.

**RTL mirroring.** Directional icons (chevrons, arrows, progress bars, breadcrumb separators, drawer-open direction) mirror under `:dir(rtl)` via `scaleX(-1)`. Non-directional icons (numerals, logos, media controls, clocks) do not mirror.

---

## SyncState Multimodal Encoding

Sync state uses four independent signals (color + shape + text + ARIA) so that any one signal failure does not lose the information.

| State | Color (light) | Color (dark) | Icon | Short label | ARIA role |
|---|---|---|---|---|---|
| Healthy | `#27ae60` | `#2ecc71` | `check_circle` | "Synced" | `status` |
| Stale | `#3498db` | `#5dade2` | `schedule` | "2h ago" | `status` |
| Offline | `#7f8c8d` | `#95a5a6` | `cloud_off` | "Offline" | `status` |
| ConflictPending | `#e67e22` | `#f39c12` | `call_split` | "Conflict" | `alert` |
| Quarantine | `#c0392b` | `#ff6b6b` | `do_not_disturb_on` | "Held" | `alert` |

Color pairs are validated to ΔE2000 ≥ 11.0 (CVD-safe) and APCA Lc ≥ 45 for non-text UI. `tooling/theme-validator/` re-runs this check on any provider-theme PR.

`SunfishSyncStateIndicator` exposes no icon-only variant and does not expose the text label or ARIA as optional props — all four signals are mandatory.

---

## Quality Gates (Forward Gate)

Every component PR must pass before merge:

- [ ] `IStringLocalizer<T>` injected; all user-visible strings in `.resx`
- [ ] Storybook story present; `parameters.a11y.sunfish` contract section current
- [ ] axe-core zero violations at impact ≥ moderate
- [ ] RTL snapshot passing for layout-bearing components
- [ ] Reduced motion honored for any declared animation
- [ ] CSS logical properties only (no physical direction properties)
- [ ] All CSS values via `--sf-*` tokens — no hardcoded hex, px literals, or transition timings
- [ ] Icons via `ISunfishIconProvider` — no inline SVG or hardcoded `<img>` icon paths
- [ ] Public-API CSS classes follow `sf-<component>__<slot>--<modifier>` BEM shape
- [ ] Provider-internal classes go through `ISunfishCssProvider`, not hardcoded in shared Razor
- [ ] Component lifecycle: dispose is idempotent; JS handles released on dispose

---

## Cross-references

| Document | What it adds |
|---|---|
| [component-principles.md](component-principles.md) | Full headless contract rules, anti-patterns, lifecycle |
| [tokens-guidelines.md](tokens-guidelines.md) | Token authoring in providers, dark mode, SCSS patterns |
| [accessibility.md](accessibility.md) | Full a11y contract template, Shadow DOM rules, manual audit workflow, CI gate detail |
| [internationalization.md](internationalization.md) | Full locale resolution, SmartFormat.NET, XLIFF pipeline, translation workflow |
| [css-conventions.md](../engineering/css-conventions.md) | BEM authoring checklist, deprecated prefixes, CSS custom property tier rules |
| [global-first-ux-design.md](../../docs/superpowers/specs/2026-04-24-global-first-ux-design.md) | Phase 1/2 rollout plan, Storybook harness spec, Roslyn analyzer package, CI workflow YAML |
| [adapter-parity.md](../engineering/adapter-parity.md) | ADR 0014 parity policy + exception register |
| [ADR 0014](../../docs/adrs/0014-adapter-parity-policy.md) | Parity policy decision |
| [ADR 0017](../../docs/adrs/0017-web-components-lit-technical-basis.md) | Lit / Web Components authoring basis |
| [ADR 0025](../../docs/adrs/0025-css-class-prefix-policy.md) | CSS class prefix decision |
