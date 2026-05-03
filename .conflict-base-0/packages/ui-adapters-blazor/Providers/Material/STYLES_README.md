# Sunfish Material 3 Provider — Styles Architecture

> Based on [Material Design 3](https://m3.material.io/) token-first theming philosophy, adapted for Sunfish's provider architecture.

## Folder Structure

```
Styles/
├── foundation/    M3 reference tokens, semantic tokens, CSS custom properties, helpers
├── patterns/      Cross-component patterns (state layers, field chrome, overlays)
├── components/    One file per Sunfish component (consuming semantic tokens)
├── _index.scss    Import aggregator (foundation → patterns → components)
└── sunfish-material.scss   Build entrypoint
```

## What Belongs Where

### `foundation/`
- **`_colors.scss`**: M3 two-layer color architecture
  - Layer 1: `$material-ref-palette` — raw reference palette (Sass map with 6 tonal palettes)
  - Layer 2: CSS custom properties — semantic roles mapped from reference tokens (light `:root` + dark `[data-sf-theme="dark"]`)
- **`_typography.scss`**: `$material-type-scale` (15 M3 type roles) + `--sf-font-*` tokens
- **`_radius.scss`**: `$material-shape` (M3 shape system: none → extra-large → full)
- **`_elevation.scss`**: M3 elevation levels 0-5 with shadow + surface tint tokens
- **`_motion.scss`**: M3 easing curves (emphasized, standard) + duration scale
- **`_spacing.scss`**: 4px base unit scale
- **`_functions.scss`**: `material-palette()`, `material-type()`, `material-shape()`, `rem()` helpers
- **`_mixins.scss`**: `sf-type-role()`, `sf-elevation()`, `sf-shape()`, `sf-state-layer()`
- **`_focus.scss`**: `sf-focus-ring()` — M3 3px secondary outline + 2px offset (Phase 2B, SYNTHESIS Theme 5)
- **`_tokens.scss`**: Z-index, focus ring, disabled opacity, box-sizing reset

### `patterns/`
- **`_interactive-states.scss`**: M3 state layer system (hover/focus/pressed/dragged overlays)
- **`_field-base.scss`**: M3 filled/outlined text field token contract
- **`_overlay.scss`**: Scrim and backdrop rules
- **`_validation.scss`**: Error/success validation color aliases
- **`_density.scss`**: M3 density scale (-3 to 0)

### `components/`
- One SCSS partial per Sunfish component
- Component partials consume semantic tokens — never reference raw palette values directly
- `_button.scss` serves as the reference implementation showing M3 token consumption patterns

## Material 3 Token Layers

```
Reference Tokens (raw)          →  $material-ref-palette Sass maps
        ↓
System/Semantic Tokens          →  :root CSS custom properties (--sf-color-primary, etc.)
        ↓
Component Tokens (consumption)  →  Component partials use var(--sf-*) tokens
```

### M3 → Sunfish Token Mapping

| M3 Concept | Sunfish Token | Example |
|-----------|-------------|---------|
| Primary | `--sf-color-primary` | `#6750A4` (light) / `#D0BCFF` (dark) |
| On Primary | `--sf-color-on-primary` | `#FFFFFF` / `#381E72` |
| Primary Container | `--sf-color-primary-container` | `#EADDFF` / `#4F378B` |
| Surface | `--sf-color-surface` | `#FEF7FF` / `#141218` |
| Surface Container | `--sf-color-surface-container` | `#F3EDF7` / `#211F26` |
| Outline | `--sf-color-outline` | `#79747E` / `#938F99` |
| Outline Variant | `--sf-color-outline-variant` | `#CAC4D0` / `#49454F` |
| Error | `--sf-color-error` | `#B3261E` / `#F2B8B5` |

### Currently Implemented M3 Token Subset

**Colors**: primary, secondary, tertiary, error (with on-*, *-container, on-*-container), surface system (5 container levels), outline, inverse, scrim, shadow, plus Sunfish semantic aliases (success, warning, danger, info)

**Typography**: All 15 M3 type scale roles (display, headline, title, body, label × large/medium/small)

**Shape**: 7 M3 shape roles (none, extra-small, small, medium, large, extra-large, full)

**Elevation**: Levels 0-5 with shadow values + surface tint support

**Motion**: M3 emphasized/standard easing + 4-tier duration scale

## Import Order

1. **Foundation** (A): colors → spacing → typography → radius → elevation → motion → functions → mixins → tokens
2. **Patterns** (B): interactive-states → field-base → overlay → validation → density
3. **Components** (C): Alphabetical by file name

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Folders | lowercase, singular | `foundation/`, `patterns/`, `components/` |
| SCSS files | underscore partial, kebab-case | `_text-field.scss`, `_data-grid.scss` |
| CSS custom properties | `--sf-*` namespace | `--sf-color-primary`, `--sf-radius-md` |
| Sass variables | `$material-*` prefix | `$material-ref-palette`, `$material-type-scale` |
| Mixins/functions | `sf-*` kebab-case | `sf-state-layer()`, `sf-elevation()` |

## Adding a New Component Partial

1. Create `components/_my-component.scss`
2. Add `@forward 'components/my-component';` to `_index.scss` in alphabetical position
3. Consume semantic tokens: `var(--sf-color-primary)`, `var(--sf-radius-md)`, etc.
4. Use M3 state layers for interaction states (see `_button.scss` for reference)
5. If a semantic token doesn't exist yet, add it to the appropriate foundation file first — do not hardcode values

## Extending the Token Hierarchy

To add new M3 token roles without breaking the hierarchy:

1. If it's a new reference palette, add to `$material-ref-palette` in `_colors.scss`
2. If it's a new semantic role, add the CSS custom property to the `:root` and `[data-sf-theme="dark"]` blocks
3. If it's a new component-level token, define it in the component partial using existing semantic tokens
4. Always map through the layer hierarchy: reference → semantic → component

## Token Mapping

All provider tokens map to `--sf-*` CSS custom properties consumed by `SunfishThemeProvider` for runtime theme switching. The `$material-ref-palette` Sass map provides compile-time color access; runtime consumers use CSS variables exclusively.

## Build

```bash
npm run scss:build:material
npm run scss:watch:material
```
