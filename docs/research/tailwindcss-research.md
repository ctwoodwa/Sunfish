# Tailwind CSS v4 — Technical Research for Sunfish

**Version covered:** Tailwind CSS v4.x (2025+, stable)  
**Evaluated:** 2026-04-20  
**v3 status:** Maintenance-only. Upgrade via `npx @tailwindcss/upgrade` (requires Node 20+)

---

## Summary

Tailwind v4 is a ground-up rewrite (Rust-powered "Oxide" engine) that moves all configuration into CSS via a new `@theme` directive, eliminates the need for a `tailwind.config.js`, and provides fine-grained CSS layer control. The changes are directly relevant to Sunfish's design token system and React adapter.

**Critical finding for Blazor:** `@apply` and `@layer` are **broken in `.razor.css` isolated stylesheets** due to how Blazor processes scoped CSS independently of Tailwind's build pipeline. Use utility classes in Razor markup and `var()` in isolated CSS instead.

---

## v3 → v4 Breaking Changes

| Area | v3 | v4 |
|---|---|---|
| Config format | `tailwind.config.js` | CSS-first `@theme` directive |
| Entry point | `@tailwind base/components/utilities` | `@import "tailwindcss"` |
| PostCSS plugin | `tailwindcss` | `@tailwindcss/postcss` |
| Autoprefixer | Required separately | Built-in (remove it) |
| postcss-import | Required separately | Built-in |
| Content scanning | `content: [...]` array | Automatic + `@source` in CSS |
| `corePlugins` | Supported | **Removed** |
| `prefix: 'tw-'` | Config key | `@import "tailwindcss" prefix(tw)` |
| `shadow-sm` | Small shadow | Renamed `shadow-xs`; `shadow` → `shadow-sm` |
| `ring` | 3px ring | Must now be `ring-3` explicitly |
| `outline-none` | Hides outline visually | Sets `outline: none` — use `outline-hidden` for old behavior |
| `bg-opacity-*` | Opacity utility | Replaced by slash modifier: `bg-black/50` |
| Prefix class syntax | `tw-flex` | `tw:flex` (variant-style) |

---

## CSS-First Architecture (`@theme`)

```css
/* Before (v3): tailwind.config.js */
module.exports = {
  theme: { extend: { colors: { brand: '#0ea5e9' } } }
}

/* After (v4): your main CSS file */
@import "tailwindcss";
@theme {
  --color-brand: #0ea5e9;
}
```

Every `@theme` variable:
1. Becomes a CSS custom property on `:root` (globally accessible via `var()`)
2. Generates corresponding utility classes (`bg-brand`, `text-brand`, etc.)

### Token Namespace → Utility Class Mapping

| CSS variable prefix | Utilities generated |
|---|---|
| `--color-*` | `text-*`, `bg-*`, `border-*`, `ring-*` |
| `--font-*` | `font-*` (family) |
| `--text-*` | `text-*` (size) |
| `--spacing-*` | `p-*`, `m-*`, `gap-*`, `w-*`, `h-*` |
| `--radius-*` | `rounded-*` |
| `--shadow-*` | `shadow-*` |
| `--breakpoint-*` | responsive variants (`3xl:`) |
| `--ease-*` | `ease-*` |

---

## Design Token Pattern for Sunfish (Key Recommendation)

### The Two-Layer Architecture

```css
/* Layer 1: Runtime semantic tokens — defined per theme */
:root,
[data-theme="light"] {
  --sunfish-color-primary:    oklch(0.55 0.20 264);
  --sunfish-color-surface:    oklch(0.99 0 0);
  --sunfish-color-on-surface: oklch(0.10 0 0);
}

[data-theme="dark"] {
  --sunfish-color-primary:    oklch(0.70 0.18 264);
  --sunfish-color-surface:    oklch(0.15 0 0);
  --sunfish-color-on-surface: oklch(0.95 0 0);
}

/* Layer 2: Bridge — maps Sunfish tokens into Tailwind utility surface */
@theme inline {
  --color-primary:    var(--sunfish-color-primary);
  --color-surface:    var(--sunfish-color-surface);
  --color-on-surface: var(--sunfish-color-on-surface);
}
```

The `@theme inline` modifier forces utility classes to reference the variable rather than resolve to its value — meaning `bg-primary` generates `background-color: var(--sunfish-color-primary)` and **reacts to `data-theme` changes without JavaScript**.

- **Blazor adapter:** references `var(--sunfish-color-primary)` directly in isolated CSS or in `style` bindings
- **React adapter:** uses `bg-primary`, `text-primary` Tailwind utilities
- **Both adapters react to the same `data-theme` attribute** — no separate theming code per adapter

---

## Blazor CSS Isolation Compatibility

### What breaks in `.razor.css` files

| Feature | Status | Reason |
|---|---|---|
| `@apply` | ❌ Broken | Isolated CSS is processed independently — Tailwind's utility definitions are unknown at that stage |
| `@layer` | ❌ Broken | Same isolation problem |
| `var(--color-*)` references | ✅ Works | CSS variables resolve at runtime, no build-time dependency |
| `@theme` custom properties | ✅ Works | Emitted to `:root`, globally available |
| Utility classes in `.razor` markup | ✅ Works | Tailwind's content scanner reads `.razor` files |

### Correct Blazor pattern

```razor
@* Good: utility classes in markup *@
<button class="bg-primary text-on-surface rounded-md px-4 py-2 hover:bg-primary/90">
    Click me
</button>
```

```css
/* Good: var() references in .razor.css */
.my-component {
    background-color: var(--sunfish-color-surface);
    color: var(--sunfish-color-on-surface);
}

/* Bad: will fail at build time */
.my-component {
    @apply bg-surface text-on-surface; /* ❌ */
}
```

---

## Build Pipeline Integration

### React adapter — use Vite plugin

```ts
// vite.config.ts
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [tailwindcss(), react()], // tailwindcss() must come first
});
```

No separate PostCSS config, no autoprefixer, no postcss-import needed.

### Blazor — use PostCSS path

```js
// postcss.config.js
export default { plugins: { "@tailwindcss/postcss": {} } };
// Do NOT add autoprefixer or postcss-import — v4 includes these
```

```xml
<!-- .csproj integration -->
<Target Name="BuildTailwind" BeforeTargets="Build">
  <Exec Command="npm run build:css" WorkingDirectory="$(ProjectDir)" />
</Target>
```

### Monorepo content scanning

Without explicit `@source` directives, Tailwind's auto-detection may miss classes used only inside packages:

```css
@import "tailwindcss";
@source "../../packages/ui-adapters-react/src/**/*.tsx";
@source "../../packages/blocks-*/src/**/*.tsx";
@source "../**/*.razor";
```

---

## CSS Layers (Explicitly Exposed in v4)

```css
@layer theme, base, components, utilities;
@import "tailwindcss/theme.css"     layer(theme);
@import "tailwindcss/preflight.css" layer(base);
@import "tailwindcss/utilities.css" layer(utilities);
```

Sunfish can define a shared layer order in the foundation package and both adapters import it first. Tailwind's layers can sit alongside Sunfish's own `sunfish-base`, `sunfish-components` layers.

**Warning:** Unlayered CSS (from Telerik, third-party libs) wins over layered CSS regardless of source order — watch for cascade conflicts with external stylesheets.

---

## Component Variant Pattern (CVA Integration)

No API changes for CVA in v4 — it works identically:

```ts
import { cva } from "class-variance-authority";

const button = cva(
  "inline-flex items-center rounded-md font-medium",
  {
    variants: {
      intent: {
        primary: "bg-primary text-on-surface hover:bg-primary/90",
        ghost:   "bg-transparent text-primary hover:bg-primary/10",
      },
      size: {
        sm: "px-3 py-1.5 text-sm",
        md: "px-4 py-2 text-base",
      },
    },
    defaultVariants: { intent: "primary", size: "md" },
  }
);
```

**Note:** `tailwind-merge` must be on v3+ to understand v4's renamed classes (shadow, ring, etc.). Old versions silently merge incorrectly.

---

## Custom Variants for Component State

```css
/* Define once in foundation CSS */
@custom-variant aria-selected (&[aria-selected="true"]);
@custom-variant data-active    (&[data-active]);
@custom-variant data-error     (&[data-error]);
@custom-variant data-disabled  (&[data-disabled]);
```

React components can then use `data-active:bg-primary/10` without any JS conditional class logic.

---

## Library Distribution — Prefix Isolation

When shipping Sunfish components into host apps that may have their own Tailwind:

```css
@import "tailwindcss" prefix(sf);
```

All utilities become `sf:bg-primary`, `sf:text-sm`, etc. CSS variables get `--sf-color-primary`. Prevents class name collisions entirely.

---

## Sharp Edges

| Issue | Severity | Detail |
|---|---|---|
| `@apply` in `.razor.css` | Critical | Completely broken — remove from coding standards |
| `@layer` in `.razor.css` | Critical | Same isolation problem |
| `corePlugins` removed | High | No replacement for disabling utilities; use `source(none)` + explicit `@source` |
| Node.js ≥ 20 required | High | Hard blocker if build agents run older Node |
| Prefix syntax change | High | v3 `tw-flex` → v4 `tw:flex`; breaks hardcoded strings in C#/Razor |
| `shadow-sm`/`ring` renames | Medium | Affects any component with shadows or focus rings |
| `outline-none` behavior change | Medium | Accessibility hazard — old behavior is now `outline-hidden` |
| `safelist` in `@config` compat | Medium | Not supported in v4's compatibility path; use `@source unsafe-inline` |
| Oxide cold-start | Low | First CI build is slow; warm builds are fast |

---

## Sunfish Adoption Recommendations

1. **Adopt the two-layer token pattern** as the canonical token architecture: `--sunfish-*` runtime vars + `@theme inline` bridge for the React adapter, `var()` references for Blazor.

2. **Never use `@apply` or `@layer` in `.razor.css` files.** Add this to `coding-standards.md`. Use utility classes in markup and `var()` in isolated CSS.

3. **Two separate build paths:** `@tailwindcss/vite` for React, `@tailwindcss/postcss` for Blazor. Share one token CSS file (in `packages/foundation`).

4. **Use `prefix(sf)` when distributing components as a library** to prevent class collisions with host app Tailwind installations.

5. **Use `source(none)` + explicit `@source`** in the monorepo to control scanning precisely.

6. **Define `@custom-variant` entries for Sunfish component states** (selected, active, error, disabled) in the foundation CSS package.

7. **Adopt OKLCH color space** for design tokens — v4's palette uses it, and it provides perceptually uniform ramps for better dark-mode palette derivation.
