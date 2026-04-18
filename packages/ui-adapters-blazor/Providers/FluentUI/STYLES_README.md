# Marilo FluentUI Provider — Styles Architecture

## Folder Structure

```
Styles/
├── foundation/    Design tokens, Sass maps, CSS custom properties, helpers
├── patterns/      Cross-component patterns (focus, field chrome, overlays, validation)
├── components/    One file per Marilo component (selectors, slots, variants, states)
├── _index.scss    Import aggregator (foundation → patterns → components)
└── marilo-fluentui.scss   Build entrypoint
```

## What Belongs Where

### `foundation/`
- **Design tokens**: CSS custom properties (`--marilo-*`) and provider-local Sass maps (`$fluent-*`)
- **Color system**: Light + dark theme token definitions
- **Typography, spacing, radius, elevation, motion**: Scale definitions
- **Functions & mixins**: Reusable Sass helpers
- **`_tokens.scss`**: Fluent UI design system variables, z-index, focus ring, box-sizing reset

### `patterns/`
- **`_interactive-states.scss`**: Focus rings, hover/pressed/disabled states
- **`_field-base.scss`**: Shared input field chrome (TextField, TextArea, Select)
- **`_overlay.scss`**: Overlay/scrim rules (Dialog, Drawer, Popover, Tooltip)
- **`_validation.scss`**: Form validation visual styles
- **`_density.scss`**: Density mode support

### `components/`
- One SCSS partial per Marilo component
- Selectors, slots, variants, and states owned by that component
- **No raw token definitions** — consume foundation tokens via CSS custom properties
- **No broad category files** — every component gets its own file

## Import Order

The `_index.scss` uses this exact order:

1. **Foundation** (A): colors → spacing → typography → radius → elevation → motion → functions → mixins → tokens
2. **Patterns** (B): interactive-states → field-base → overlay → validation → density
3. **Components** (C): Alphabetical by file name

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Folders | lowercase, singular | `foundation/`, `patterns/`, `components/` |
| SCSS files | underscore partial, kebab-case | `_text-field.scss`, `_data-grid.scss` |
| CSS custom properties | `--marilo-*` namespace | `--marilo-color-primary`, `--marilo-spacing-4` |
| Sass variables | `$fluent-*` prefix | `$fluent-colors`, `$fluent-radius` |
| Mixins/functions | `marilo-*` kebab-case | `marilo-focus-ring()`, `marilo-disabled-state()` |
| Selectors | Preserve existing `mar-*` classes | `.mar-button`, `.mar-data-grid` |

## Adding a New Component Partial

1. Create `components/_my-component.scss`
2. Add `@forward 'components/my-component';` to `_index.scss` in alphabetical position within section C
3. Use `var(--marilo-*)` tokens — do not hardcode colors, spacing, or radii
4. If a needed token doesn't exist, add it to the appropriate foundation file first

## Token Mapping

All provider tokens map to `--marilo-*` CSS custom properties, which are consumed by `MariloThemeProvider` for runtime theme switching. Provider-local Sass maps (`$fluent-*`) are used only at compile time for structure and defaults.

## Build

```bash
npm run scss:build:fluentui
npm run scss:watch:fluentui
```
