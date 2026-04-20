# Design Tokens Guidelines

**Status:** Accepted
**Last reviewed:** 2026-04-19
**Governs:** Provider theme packages (`packages/ui-adapters-blazor/Providers/{FluentUI,Bootstrap,Material}`), component CSS consumption, and any future adapter's theming layer.
**Companion docs:** [component-principles.md](component-principles.md), [adapter-parity.md](../engineering/adapter-parity.md).

Design tokens are Sunfish's answer to multi-provider theming. Every visual property a component renders (color, spacing, typography, radius, shadow, motion) is expressed as a semantic token that a provider resolves. Components never hardcode visual values. Provider swap is a CSS-variable swap, not a rebuild.

## Token naming

`--sf-<category>-<role>[-<state>]`

- `--sf-` prefix marks every Sunfish token. Prevents collision with host-app or vendor tokens.
- **Category** names the axis: `color`, `font`, `space`, `radius`, `shadow`, `motion`, `z`, `breakpoint`.
- **Role** names the purpose: `primary`, `surface`, `text`, `danger-light`, `heading`, `sm`, `lg`, `elevation-2`, …
- **State** qualifies the role when relevant: `hover`, `active`, `focus`, `disabled`.

Examples pulled from the shipped Bootstrap provider:

```
--sf-color-primary
--sf-color-primary-hover
--sf-color-primary-active
--sf-color-primary-light
--sf-color-secondary
--sf-color-success
--sf-color-success-light
--sf-color-warning
--sf-color-danger
--sf-color-info
--sf-color-bg
--sf-color-surface
--sf-color-text
```

### Rules

- **Kebab-case.** `--sf-color-primary-hover`, never `--sf-colorPrimaryHover`.
- **Semantic before raw.** `--sf-color-primary` not `--sf-color-blue-500`. Providers resolve the semantic to whatever hex value fits.
- **No component names in token names.** `--sf-color-button-primary` is wrong. `--sf-color-primary` is right. Components consume semantic tokens; they don't get their own.
- **No provider names in token names.** `--sf-color-fluentui-primary` is wrong. Every provider exposes the same token set; the value differs.

## Token categories

### Color

Palette roles:

- **Primary:** `--sf-color-primary`, `-hover`, `-active`, `-light`
- **Secondary:** `--sf-color-secondary`, …
- **Semantic:** `--sf-color-success`, `-light`; `--sf-color-warning`, `-light`; `--sf-color-danger`, `-light`; `--sf-color-info`, `-light`
- **Neutral:** `--sf-color-bg` (page background), `--sf-color-surface` (elevated surfaces), `--sf-color-text` (primary text), `--sf-color-text-muted`, `--sf-color-border`

### Typography

- `--sf-font-family-base`, `--sf-font-family-mono`
- `--sf-font-size-xs`, `-sm`, `-md`, `-lg`, `-xl`, `-2xl`
- `--sf-font-weight-regular`, `-medium`, `-semibold`, `-bold`
- `--sf-line-height-tight`, `-base`, `-relaxed`

### Spacing

`--sf-space-0`, `-1`, `-2`, `-3`, `-4`, `-6`, `-8`, `-12`, `-16`, `-24`, `-32`.
Scale is typically 4px or 8px based; providers resolve to their own native scale.

### Radius

`--sf-radius-none`, `-sm`, `-md`, `-lg`, `-xl`, `-pill`, `-full`.

### Shadow / elevation

`--sf-elevation-0`, `-1`, `-2`, `-3`, `-4`, `-overlay`.
Material-style elevation; FluentUI maps its own shadow scheme.

### Motion

`--sf-motion-duration-fast`, `-base`, `-slow`; `--sf-motion-ease-standard`, `-decelerate`, `-accelerate`.

### Z-index

`--sf-z-dropdown`, `-overlay`, `-modal`, `-toast`, `-tooltip`. Providers keep the same stacking order; components consume semantic layers.

### Breakpoints

Consumed via media queries, not CSS variables directly (browsers don't resolve `var()` in media queries). Each provider exposes them as SCSS variables (`$sf-bp-sm`, `-md`, …) for authors who need to media-query.

## Semantic before raw

Tokens are semantic first. When a raw palette color is needed (e.g., for a branded marketing surface), providers *may* expose raw tokens with a distinct prefix:

```
--sf-palette-blue-500
--sf-palette-neutral-200
```

But components never consume `--sf-palette-*`. Only semantic `--sf-color-*` tokens cross the component boundary. If a component author needs a raw color, they're probably missing a semantic role — propose one instead.

## Provider implementation

Each provider is a sub-package under `ui-adapters-blazor/Providers/`. Current providers:

- `Providers/FluentUI/` — Fluent-UI-flavored, Microsoft design language.
- `Providers/Bootstrap/` — Bootstrap-Sass-compiled, derived from Bootstrap variables.
- `Providers/Material/` — Material-Design-flavored.

### Standard layout per provider

```
Providers/<Name>/
├── Sunfish.Providers.<Name>.csproj
├── <Name>CssProvider.cs              ← ISunfishCssProvider implementation
├── <Name>IconProvider.cs             ← ISunfishIconProvider implementation
├── <Name>JsInterop.cs                ← ISunfishJsInterop implementation (if interop differs)
├── Styles/
│   ├── _tokens.scss                  ← CSS custom properties (light mode)
│   ├── _tokens-dark.scss             ← dark-mode overrides
│   ├── _variables.scss               ← provider-internal Sass variables
│   ├── _index.scss                   ← entry point, imports everything
│   ├── components/
│   │   └── _<component>.scss         ← per-component styles
│   └── _bridge-*.scss                ← Bridge-accelerator-specific overrides
└── wwwroot/
    └── css/
        └── sunfish-<name>.css        ← compiled output
```

### Writing tokens in a provider

Derive from the provider's native scale, then expose as Sunfish tokens:

```scss
// _tokens.scss (Bootstrap provider)
:root {
  --sf-color-primary:        #{$primary};
  --sf-color-primary-hover:  #{shade-color($primary, 20%)};
  --sf-color-primary-active: #{shade-color($primary, 25%)};
  --sf-color-primary-light:  #{tint-color($primary, 80%)};

  --sf-color-success:        #{$success};
  --sf-color-success-light:  #{tint-color($success, 80%)};
  // …
}
```

Never hardcode hex values — derive from the provider's own palette so native updates propagate.

### Dark mode

Dark-mode values live in `_tokens-dark.scss`, scoped to a `[data-sf-theme='dark']` attribute on the document element (or similar hook the provider picks):

```scss
[data-sf-theme='dark'] {
  --sf-color-bg:      #{$dark-body-bg};
  --sf-color-surface: #{$dark-surface};
  --sf-color-text:    #{$dark-body-color};
  // …
}
```

The Sunfish `IThemeService` / theme picker toggles the attribute; CSS variables resolve automatically. Components don't need to know about dark mode.

## Component consumption

Components use `var(--sf-*)` directly in CSS:

```scss
.sf-button--primary {
  background-color: var(--sf-color-primary);
  color: var(--sf-color-text-on-primary);
  border-radius: var(--sf-radius-md);
  padding: var(--sf-space-2) var(--sf-space-4);
  font-size: var(--sf-font-size-md);
  transition: background-color var(--sf-motion-duration-fast) var(--sf-motion-ease-standard);
}

.sf-button--primary:hover {
  background-color: var(--sf-color-primary-hover);
}
```

Rules:

- **Never `#hex` or `rgb()` literals in component CSS.** If a value isn't available as a token, add the token first — don't inline.
- **Never magic-number spacing.** `padding: 8px 16px` becomes `padding: var(--sf-space-2) var(--sf-space-4)`.
- **Never hardcoded transition timings.** `200ms ease-in` becomes `var(--sf-motion-duration-base) var(--sf-motion-ease-standard)`.
- **One provider per runtime.** Don't write CSS that references tokens from multiple providers simultaneously. The `ISunfishCssProvider` injection picks the active one.

## Adding a new token

A new semantic token is a design decision. Checklist:

1. **Is it actually semantic?** "A darker blue for the pricing page" isn't — that's a branding override. "A warning accent for destructive actions pre-confirmation" is.
2. **Does every provider have a value for it?** If only FluentUI has it natively, defining it Sunfish-wide forces other providers to invent a mapping. Sometimes OK; usually means the token is too specific.
3. **Is it in the right category?** Don't stuff a motion token under color.
4. **Cross-reference provider implementations.** When the token merges, every provider's `_tokens.scss` and `_tokens-dark.scss` update in the same PR.
5. **Document semantic intent.** What does `--sf-color-focus-ring` mean semantically (not what color)? "The outline shown on focusable elements when the user is keyboard-navigating." Makes provider implementers' job obvious.

## Icons

Icons are a parallel token-like concern. Every icon is a named identifier consumed via `ISunfishIconProvider`:

```csharp
@inject ISunfishIconProvider Icons
<span>@Icons.GetIcon("icon.trash")</span>
```

### Rules

- Icon names use dotted category prefix: `icon.action.save`, `icon.status.warning`, `icon.nav.home`.
- Icon sets are sub-packages under `ui-adapters-blazor/Icons/` (`Icons/Tabler`, `Icons/Legacy`). Each implements the interface and ships SVGs.
- Components never `<svg>` inline. They call `IconProvider.GetIcon(name)` which returns a `MarkupString` (Blazor) or equivalent.
- Swapping the icon set is a startup registration change: `AddSunfishIcons<TablerIconProvider>()`.

## Anti-patterns

- **Hardcoded values in component CSS.** Every `#3b82f6`, every `8px`, every `200ms` is a missing token.
- **Component-scoped custom properties.** `.sf-button { --button-primary-color: blue; }` — don't. Use `--sf-*` semantic tokens and let providers fill them.
- **Cross-provider references.** A component shouldn't try to read FluentUI-specific or Bootstrap-specific variables. If the design genuinely depends on one provider's capability, open a parity exception (ADR 0014).
- **Inline styles for theming.** `style="color: blue"` in a `.razor` file bypasses the token system. Use a CSS class and token-driven styles.
- **SCSS variables substituted in place of CSS variables for runtime tokens.** Sass variables resolve at compile; CSS variables resolve at runtime (necessary for dark-mode toggle, multi-tenant theming). Use CSS variables unless the value genuinely is compile-time-fixed.
- **Icon paths or inline SVG markup in components.** Always through the provider interface.

## When a provider can't express a token

Sometimes a provider's native system doesn't have an equivalent for a Sunfish-defined token (e.g., a Material-specific elevation level). Options, in preference order:

1. **Map the closest approximation.** Elevation-2 in FluentUI maps to whatever FluentUI calls its middle shadow.
2. **Add a provider-specific token** for the gap, documented. Rare; avoid when possible.
3. **Register a parity exception** if the visual truly can't be achieved. Per ADR 0014.

## Cross-references

- [component-principles.md](component-principles.md) — how components consume tokens via `ISunfishCssProvider`.
- [adapter-parity.md](../engineering/adapter-parity.md) — parity policy for visual differences.
- `packages/ui-adapters-blazor/Providers/Bootstrap/Styles/_tokens.scss` — a shipped reference for token definitions.
- `packages/ui-adapters-blazor/Providers/FluentUI/Styles/_tokens.scss` — alternative provider for comparison.
- `packages/ui-adapters-blazor/Providers/Material/Styles/_tokens.scss` — third provider.
- `packages/ui-adapters-blazor/Contracts/ISunfishCssProvider.cs` — the runtime contract.
