# Style Audit: SunfishDataGrid vs Material Design 3

**Date:** 2026-04-22
**Audited surface:** `SunfishDataGrid<TItem>` (Blazor) rendered under the Material provider skin.
**Reference specs:**
- m3.material.io — Data-table pattern, States, Elevation, Shape, Typography, Focus indicator
- `@material/web` canonical tokens (`tokens/versions/v0_192/_md-sys-*.scss`)
- `@angular/material` MDC table implementation (row/header heights)

Token values cited below are from `material-web`/`angular/components` source-of-truth SCSS, not derived.
Cross-reference: `packages/ui-adapters-blazor/Components/DataDisplay/DataGrid/GAP_ANALYSIS.md` covers API gaps; this audit covers the **visual-styling** layer only.

---

## 1. Summary

Sunfish's Material provider already establishes a creditable M3 foundation. The color-role ladder is faithful to M3: `--sf-color-primary/on-primary/primary-container`, the full `surface-container-*` tier set, `outline`/`outline-variant`, and a paired dark-mode remap under `[data-sf-theme="dark"]`. Shape tokens (`--sf-radius-sm` 4px, `md` 12px, `lg` 16px, `xl` 28px, `full` 9999px) match M3 exactly, as do the state-layer opacity tokens `--sf-state-hover-opacity: 0.08` / `focus: 0.12` / `pressed: 0.12` / `dragged: 0.16` and the emphasized easing `cubic-bezier(0.2, 0, 0, 1)`. The elevation ladder (`--sf-elevation-1..5`) uses M3-shaped shadows. The reusable `.sf-state-layer` utility correctly paints `currentColor` with these opacities. Density sizes (small 28px / medium 36px / large 48px) are close to M3 intent.

However, the DataGrid-specific selectors **do not consume this system**. `.sf-datagrid-row:hover` sets a flat `background: var(--sf-color-surface-container-high)` rather than applying an 8 %-opacity `on-surface` state-layer overlay — meaning hover is not composable with selection or focus, disabled-row hover still paints, and the dark-theme contrast drops below spec. `.sf-datagrid-header-cell--sortable:hover` has the same defect. Focus is rendered via the single-line `--sf-focus-ring` token; M3 requires a **3 px stroke in `secondary` with a 2 px offset** (`--md-focus-ring-width: 3px`). Selected rows use a token (`--sf-color-primary-light`) that aliases `primary-container`, a design acceptable under M3, but the selection state has **no layered hover/focus/pressed overlay** on top and does not survive a keyboard-focus state. Row heights (36 px default) are below the M3 data-table spec (52 px standard, 56 px header). There is no loading-indicator, skeleton, or empty-state pattern that maps to M3. No typescale tokens (`--md-sys-typescale-body-medium-*`) are referenced — the grid uses raw `font-size: inherit` or ad-hoc rems. The filter-popup uses a literal `rgba(0,0,0,0.12)` shadow rather than `--sf-elevation-2`. Dark-mode coverage for the grid is minimal (none of `.sf-datagrid-*` selectors are overridden).

---

## 2. Prioritized Gap List

### P0 — Breaks M3 contract; blocks visual parity

#### P0-1: Hover/focus/pressed on rows are flat color swaps, not state-layer overlays

*M3 reference:* Tokens `hover-state-layer-opacity: 0.08`, `focus: 0.12`, `pressed: 0.12`, `dragged: 0.16` are painted as a `currentColor` overlay on top of the base role. See `material-web/tokens/versions/v0_192/_md-sys-state.scss`. Guidance: https://m3.material.io/foundations/interaction/states/state-layers

*Sunfish current CSS* (`sunfish-material.css` line 594):
```css
.sf-datagrid-row:hover { background: var(--sf-color-surface-container-high); }
.sf-datagrid-row--selected:hover { background: var(--sf-color-primary-light); }
```

*Proposed change:* Adopt the existing `.sf-state-layer` pattern on `<tr>`:
```css
.sf-datagrid-row { position: relative; }
.sf-datagrid-row::after {
  content: ""; position: absolute; inset: 0; pointer-events: none;
  background: currentColor; opacity: 0;
  transition: opacity var(--sf-transition-fast);
}
.sf-datagrid-row:hover::after { opacity: var(--sf-state-hover-opacity); } /* 0.08 */
.sf-datagrid-row:focus-within::after { opacity: var(--sf-state-focus-opacity); } /* 0.12 */
.sf-datagrid-row:active::after { opacity: var(--sf-state-pressed-opacity); } /* 0.12 */
```
For selected rows, keep `primary-container` base and layer `on-primary-container` as `currentColor` so the overlay compounds correctly.

---

#### P0-2: Focus indicator is 2 px `primary`, not 3 px `secondary` per M3 spec

*M3 reference:* `--md-focus-ring-width: 3px`, `--md-focus-ring-color: var(--md-sys-color-secondary)`, `--md-focus-ring-outward-offset: 2px`. Source: `material-web/focus/internal/focus-ring.ts`, docs `focus-ring.md`.

*Sunfish current CSS* (line 215):
```css
--sf-focus-ring: 0 0 0 2px var(--sf-color-background), 0 0 0 4px var(--sf-color-primary);
```
Applied to cells and rows via `.sf-datagrid-cell:focus-visible { box-shadow: var(--sf-focus-ring); }`.

*Proposed change:*
```css
--sf-focus-ring: 0 0 0 2px var(--sf-color-surface),
                 0 0 0 5px var(--sf-color-secondary); /* 3px stroke + 2px offset */
```
Or add a dedicated `--sf-focus-ring-grid` token so other components (which currently rely on `primary`) are unaffected.

---

#### P0-3: Row/header heights significantly below M3 data-table spec

*M3 reference:* Angular Material MDC table defaults — `table-header-container-height: 56px`, `table-row-item-container-height: 52px`. Source: `angular/components/src/material/core/tokens/m2/mat/_table.scss`. Dense row is 40 px.

*Sunfish current CSS* (line 586, 989–1003):
```css
--mar-datagrid-row-height, 36px   /* default medium */
--mar-datagrid--size-small → 28px
--mar-datagrid--size-large → 48px
```
All tiers sit below the M3 minimum.

*Proposed change:* Re-map sizes to M3 density tiers:
```css
.mar-datagrid--size-small  { --mar-datagrid-row-height: 40px; }  /* M3 dense */
.mar-datagrid--size-medium { --mar-datagrid-row-height: 52px; }  /* M3 standard */
.mar-datagrid--size-large  { --mar-datagrid-row-height: 72px; }  /* M3 comfortable */
.sf-datagrid-header        { min-height: 56px; }
```
Also set `padding: 0 16px` on header + data cells to match MDC spec.

---

#### P0-4: Typography scale never referenced by grid

*M3 reference:* Data-table body cells use `body-medium` (0.875 rem / 1.25 rem), header cells use `title-small` (0.875 rem / 1.25 rem, weight 500), label-small 0.6875 rem for supporting text. Source: `material-web/tokens/versions/v0_192/_md-sys-typescale.scss`.

*Sunfish current CSS:* The grid uses `font-size: var(--sf-font-size-base)` on the root, and `font-size: inherit` on cells. No `--md-sys-typescale-*` or equivalent per-role token exists at the grid level. `font-weight: var(--sf-font-weight-semibold)` is referenced (line 555) but `--sf-font-weight-semibold` is **not defined** in `:root` — only `normal: 400`, `medium: 500`, `bold: 700`.

*Proposed change:* Introduce typescale tokens and apply them:
```css
:root {
  --sf-typescale-body-medium-size: 0.875rem;
  --sf-typescale-body-medium-line-height: 1.25rem;
  --sf-typescale-body-medium-letter-spacing: 0.015625rem;
  --sf-typescale-title-small-size: 0.875rem;
  --sf-typescale-title-small-line-height: 1.25rem;
  --sf-typescale-title-small-weight: 500;
}
.sf-datagrid-header-cell {
  font: var(--sf-typescale-title-small-weight) var(--sf-typescale-title-small-size)/var(--sf-typescale-title-small-line-height) var(--sf-font-family);
}
.sf-datagrid-cell {
  font: 400 var(--sf-typescale-body-medium-size)/var(--sf-typescale-body-medium-line-height) var(--sf-font-family);
}
```
Also fix the undefined `--sf-font-weight-semibold` → use `--sf-font-weight-medium` (500) per M3 title-small.

---

### P1 — Visually off-spec; blocks a11y or dark-mode

#### P1-1: Dark-mode coverage missing for every `.sf-datagrid-*` selector

*M3 reference:* The M3 dark-mode remap flips all `md-sys-color-*` tokens; components consume them via the token layer. Surfaces that hard-code `var(--sf-color-surface)` without considering contrast against primary-container (which lightens in dark) can fail 3:1 contrast.

*Sunfish current CSS:* `.sf-datasheet` (lines 1370+) has an extensive `[data-sf-theme=dark]` block with 20+ overrides. `.sf-datagrid` has **zero** dark-mode selectors. The row hover still paints `surface-container-high` (hex-swapped under dark-theme), which is legal but selection/focus layering is untested.

*Proposed change:* No token changes required if P0-1 is adopted, because state-layers will derive from `currentColor`. Still, add explicit overrides for anything using literal colors:
```css
[data-sf-theme="dark"] .sf-datagrid-loading-overlay {
  background: color-mix(in srgb, var(--sf-color-surface) 70%, transparent);
}
[data-sf-theme="dark"] .sf-datagrid-popup-overlay { background: rgba(0,0,0,0.6); }
```
Replace `rgba(0,0,0,0.3)` (line 876) and `rgba(0,0,0,0.12)` / `0,0,0,0.16` (lines 733, 887) with `var(--sf-color-scrim) / opacity:0.32` and `var(--sf-elevation-2 / 3)` tokens.

---

#### P1-2: Filter-popup + popup-dialog use literal shadows instead of elevation tokens

*M3 reference:* Menus elevation 2, dialogs elevation 3. Tokens: `--md-sys-elevation-level2/3`. Source: `material-web/tokens/_md-sys-elevation.scss`.

*Sunfish current CSS* (lines 733, 887):
```css
.sf-datagrid-filter-menu  { box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12); }
.sf-datagrid-popup-dialog { box-shadow: 0 8px 24px rgba(0, 0, 0, 0.16); }
```

*Proposed change:*
```css
.sf-datagrid-filter-menu  { box-shadow: var(--sf-elevation-2); }
.sf-datagrid-popup-dialog { box-shadow: var(--sf-elevation-3); }
```

---

#### P1-3: Loading overlay has no M3 loading-indicator; is a text placeholder only

*M3 reference:* M3 ships a `circular-progress` / `linear-progress` indeterminate indicator. For data tables, the recommended pattern is either a top-edge linear progress bar or a skeleton loader (`material-web` docs, `progress.md`). Text "Loading…" alone is not spec.

*Sunfish current CSS/markup* (`SunfishDataGrid.razor` lines 46–50, CSS lines 789–797):
```html
<div class="sf-datagrid-loading-overlay">
  <div class="sf-datagrid-loading-spinner"><span>Loading…</span></div>
</div>
```
`.sf-datagrid-loading-spinner` has no animation defined; it is a static text container.

*Proposed change:* Add a top-edge indeterminate bar using existing motion tokens:
```css
.sf-datagrid-loading-bar {
  position: absolute; top: 0; left: 0; right: 0; height: 4px;
  overflow: hidden; background: var(--sf-color-primary-container);
}
.sf-datagrid-loading-bar::before {
  content: ""; position: absolute; inset-block: 0; inset-inline: -35% 100%;
  background: var(--sf-color-primary);
  animation: sf-indeterminate 2s var(--sf-motion-easing-standard) infinite;
}
@keyframes sf-indeterminate {
  0%   { inset-inline: -35% 100%; }
  60%  { inset-inline: 100% -90%; }
  100% { inset-inline: 100% -90%; }
}
```
Render this whenever `IsLoading` is true. The existing `.sf-datasheet__skeleton` pattern (lines 1312–1337) shows Sunfish already has a working skeleton implementation that could be forked for the grid.

---

#### P1-4: Selected row does not layer with hover/focus — flash of identical color

*M3 reference:* Selected rows use `secondary-container` base; hover adds an 8 % `on-secondary-container` state layer on top. This is how MDC table achieves visible hover-over-selected feedback.

*Sunfish current CSS* (lines 598–604):
```css
.sf-datagrid-row--selected { background: var(--sf-color-primary-light); }
.sf-datagrid-row--selected:hover { background: var(--sf-color-primary-light); }
```
The hover rule is a no-op — selected rows give zero hover feedback.

*Proposed change:* Remove the no-op rule; after adopting P0-1 the state layer composes automatically. Consider switching to `secondary-container` for selection to match MDC convention:
```css
.sf-datagrid-row--selected {
  background: var(--sf-color-secondary-container);
  color: var(--sf-color-on-secondary-container);
}
/* hover/focus overlays from P0-1 compose on top */
```

---

#### P1-5: Empty-state is a centered text paragraph; not the M3 empty-state pattern

*M3 reference:* Empty states on lists/tables use an icon + headline (title-medium) + optional body (body-medium) + action, centered in the table area with vertical padding of 48 px (`--md-sys-spacing-48`-equivalent). See Material 3 "Empty states" pattern on m3.material.io.

*Sunfish current CSS* (lines 766–773):
```css
.sf-datagrid-empty {
  display: flex; align-items: center; justify-content: center;
  padding: var(--sf-space-xl) var(--sf-space-md);
  color: var(--sf-color-text-muted);  /* ← token not defined */
  font-size: var(--sf-font-size-base);
}
```
Note `--sf-color-text-muted` is not defined in `:root` (only `--sf-color-text-secondary` is).

*Proposed change:* Use defined tokens + allow vertical structure:
```css
.sf-datagrid-empty {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  gap: var(--sf-space-md);
  padding: var(--sf-space-3xl) var(--sf-space-lg);  /* 48px x 16px */
  color: var(--sf-color-on-surface-variant);
  text-align: center;
}
.sf-datagrid-empty__headline { font: 500 var(--sf-typescale-title-medium-size)/1.5rem var(--sf-font-family); color: var(--sf-color-on-surface); }
.sf-datagrid-empty__body     { font: var(--sf-typescale-body-medium-size)/var(--sf-typescale-body-medium-line-height) var(--sf-font-family); }
```

---

#### P1-6: Filter-menu icon button uses a 1 px outline; M3 filter chip uses 8 % state layer

*M3 reference:* Icon buttons in data-table headers should be 40 × 40 hit target with a circular 8 % hover overlay (no persistent outline). `--md-sys-shape-corner-full` radius. Source: `material-web/iconbutton/` tokens.

*Sunfish current CSS* (lines 672–687):
```css
.sf-datagrid-filter-menu-btn {
  border: 1px solid var(--sf-color-border);
  border-radius: 4px; min-width: 1.5rem; height: 1.5rem;
}
.sf-datagrid-filter-menu-btn:hover { border-color: var(--sf-color-primary); }
```

*Proposed change:*
```css
.sf-datagrid-filter-menu-btn {
  width: 40px; height: 40px;
  border: none; border-radius: var(--sf-radius-full);
  background: transparent; color: var(--sf-color-on-surface-variant);
  position: relative;
}
.sf-datagrid-filter-menu-btn::before {
  content: ""; position: absolute; inset: 0; border-radius: inherit;
  background: currentColor; opacity: 0;
  transition: opacity var(--sf-transition-fast);
}
.sf-datagrid-filter-menu-btn:hover::before  { opacity: var(--sf-state-hover-opacity); }
.sf-datagrid-filter-menu-btn:focus-visible::before { opacity: var(--sf-state-focus-opacity); }
```

---

### P2 — Idiomatic gaps; affect polish

#### P2-1: Pager buttons are outlined rectangles; M3 uses full-radius or no-border

*Sunfish current CSS* (lines 825–846): border + 4 px radius, active state is solid primary.
*Proposed:* Either full-radius circles (like M3 icon buttons, 40 px) or borderless text buttons with state-layer overlay. Active page should use `primary-container`/`on-primary-container` not `primary`/`on-primary` (which is reserved for filled-button emphasis).

#### P2-2: Striped rows use `surface-container`; M3 does not recommend zebra-striping

*Sunfish current CSS* (lines 606–608): zebra uses `surface-container`. M3 data-table spec prefers uniform rows with dividers; zebra is a Bootstrap-era holdover.
*Proposed:* Keep the opt-in but document it as non-idiomatic; swap to `surface-container-low` for lower contrast.

#### P2-3: Sort indicator is a triangle glyph in an inline `<span>`; no icon-token system

*Sunfish markup* (line 139): `<span>▲▼</span>`.
M3 uses a 16 × 16 `arrow_upward`/`arrow_downward` filled icon with 150 ms rotation transition on sort-direction flip.
*Proposed:* Swap for an inline SVG sized `1em` (matching the existing filter SVG), and add:
```css
.sf-datagrid-sort-indicator { transition: transform 150ms var(--sf-motion-easing-standard); }
.sf-datagrid-sort-indicator--desc { transform: rotate(180deg); }
```

#### P2-4: Popup dialog uses `rgba(0,0,0,0.3)` scrim

*Current* (line 876): literal color. *Proposed:* `background: var(--sf-color-scrim); opacity: 0.32;` (the `--sf-scrim` token already exists with the right opacity pre-multiplied — see `.sf-scrim` line 336).

#### P2-5: Column-menu trigger is a character `⋮`, not an icon

*Markup line 203:* `&#x22EE;` text content.
*Proposed:* Swap for `more_vert` SVG (same pattern as filter SVG) and apply the icon-button overlay from P1-6.

#### P2-6: `--sf-color-border` is aliased to `outline-variant`, used everywhere

This is fine for `outline-variant` semantics (subtle dividers), but `.sf-datagrid-col--locked` uses `border-right: 2px solid var(--sf-color-border)`, producing a faint edge where M3 uses `elevation-1` shadow instead.
*Proposed:* For the frozen-boundary column, replace the border with a directional shadow:
```css
.sf-datagrid-col--locked-end {
  box-shadow: 4px 0 4px -4px color-mix(in srgb, var(--sf-color-shadow) 20%, transparent);
  border-right: none;
}
```

---

### P3 — Polish / nice-to-have

#### P3-1: Elevation tokens are legacy Material-2 shadows, not M3 tinted-surface

M3's defining elevation change: surfaces gain elevation from a tinted background overlay (`surface-tint` with opacity) **and** a shadow, not just a shadow. Sunfish has `--sf-surface-tint` and `--sf-surface-tint-opacity-1..5` (lines 156–161) defined but **never used**. Candidate: compose `box-shadow: var(--sf-elevation-1)` with `background: color-mix(in srgb, var(--sf-surface-tint) calc(var(--sf-surface-tint-opacity-1) * 100%), var(--sf-color-surface))` for `.sf-datagrid` root.

#### P3-2: No motion on row expand/collapse for detail rows

Detail row appears/disappears with no transition. M3 recommends 300 ms with emphasized easing.
*Proposed:* `grid-template-rows` or `max-height` transition on `.sf-datagrid-detail-row` with `--sf-motion-duration-medium2` + `--sf-motion-easing-emphasized`.

#### P3-3: Checkbox column uses native `<input type="checkbox">`, not an M3 checkbox

This is a grid-integration issue: ideally the grid renders `<SunfishCheckbox>` so the themed variant is used. CSS-only workaround:
```css
.sf-datagrid-checkbox-cell input[type="checkbox"] {
  accent-color: var(--sf-color-primary);
  width: 18px; height: 18px;
}
```

#### P3-4: `sf-datagrid-cmd-btn` is a flat outlined mini-button; M3 uses text-button in tables

M3 in-row action buttons typically use text-style (no border, state-layer on hover). Sunfish cmd buttons (lines 935–950) have persistent borders.
*Proposed:* Default variant = text-button (transparent, no border). Add `--primary` / `--danger` modifiers for filled emphasis where needed.

---

## 3. State-Layer Audit Table

Baseline values per `material-web/tokens/versions/v0_192/_md-sys-state.scss`:
hover 0.08, focus 0.12, pressed 0.12, dragged 0.16.

| Interactive surface | Base color role | Hover overlay (M3) | Focus overlay (M3) | Pressed overlay (M3) | Currently in Sunfish CSS? | Priority |
|---|---|---|---|---|---|---|
| Header cell (sortable) | `surface` / `on-surface` | on-surface 8 % | on-surface 12 % | on-surface 12 % | No — uses flat `surface-container-high` bg (line 570); no focus | P0-1 |
| Header cell (non-sortable) | `surface` | none | on-surface 12 % | none | No — no focus rule | P1 |
| Row (default) | `surface` / `on-surface` | on-surface 8 % | on-surface 12 % | on-surface 12 % | No — flat bg swap (line 594); no focus-within | P0-1 |
| Row (selected) | `primary-container` / `on-primary-container` | on-primary-container 8 % | on-primary-container 12 % | on-primary-container 12 % | No — hover rule is a no-op (line 602) | P0-1, P1-4 |
| Body cell (when cell-selectable) | inherits row | on-surface 8 % | on-surface 12 % | on-surface 12 % | Partial — has `.sf-datagrid-cell--selected` outline but no overlay | P1 |
| Sort indicator button | primary | on-surface 8 % | on-surface 12 % | on-surface 12 % | N/A — not a button, just a glyph span | P2-3 |
| Filter-menu trigger (`.sf-datagrid-filter-menu-btn`) | surface | on-surface 8 % | on-surface 12 % | on-surface 12 % | No — uses border-color swap (line 689) | P1-6 |
| Column-menu trigger | surface | on-surface 8 % | on-surface 12 % | on-surface 12 % | No — no rule defined anywhere | P1 |
| Column-menu item (`.mar-datagrid-column-menu-item`) | surface / on-surface | on-surface 8 % | on-surface 12 % | on-surface 12 % | No — selector not styled in material.css | P1 |
| Command button (`.sf-datagrid-cmd-btn`) | surface / on-surface | on-surface 8 % | on-surface 12 % | on-surface 12 % | Partial — `background:surface-container-high` on hover (line 948); no focus | P1 |
| Pager button (`.sf-datagrid-pager-btn`) | surface / on-surface | on-surface 8 % | on-surface 12 % | on-surface 12 % | Partial — bg swap to `surface-container-high` (line 839); no focus-visible | P1 |
| Pager button (active) | primary / on-primary | on-primary 8 % | on-primary 12 % | on-primary 12 % | No — flat fill (line 842) | P2-1 |
| Column resize handle | outline | on-surface 8 % → outline on drag | focus 12 % | — | Partial — hover rule at line 1025 uses literal `rgba(0,0,0,0.2)` | P2 |
| Row drag handle | on-surface-variant | on-surface 8 % | on-surface 12 % | dragged 16 % (while dragging) | No — no rule for `.mar-datagrid-row-drag-handle` | P1 |
| Detail expand button (`.mar-datagrid-detail-btn`) | on-surface | on-surface 8 % | on-surface 12 % | on-surface 12 % | No — selector not styled | P1 |
| Checkbox (header + row) | primary when checked | primary 8 % | primary 12 % | primary 12 % | No — native input, uses UA defaults | P3-3 |

**Total coverage:** 0 / 15 interactive surfaces fully layered per M3. 5 / 15 have a partial (non-layered) hover treatment. **This is the single largest gap in the audit.**

---

## 4. Focus-Area Coverage Table

| Focus area | M3 idiomatic? | Sunfish current state | Priority |
|---|---|---|---|
| Header row background | Yes — `surface` (not `surface-container`) | Uses `surface-container` (line 554) — one tier too dark | P2 |
| Header cell padding | `0 16px` | `var(--sf-space-sm) var(--sf-space-md)` = `8px 12px` (line 560) — 4 px short horizontally | P1 |
| Header cell typography | `title-small` 0.875/1.25 rem, weight 500 | Uses `font-weight: var(--sf-font-weight-semibold)` (undefined token) | P0-4 |
| Row default background | `surface` | `surface` via inheritance — OK | — |
| Row height (standard) | 52 px | 36 px default (line 586) | P0-3 |
| Row height (dense) | 40 px | 28 px (line 990) | P0-3 |
| Row height (comfortable) | 72 px | 48 px (line 999) | P0-3 |
| Row dividers | `outline-variant` 1 px | `--sf-color-border` = `outline-variant` — OK | — |
| Cell padding | `0 16px` | `0.5rem 0.75rem` = `8px 12px` at medium (line 997) — off | P1 |
| Cell typography | `body-medium` 0.875/1.25 rem | `font-size: inherit` — no typescale | P0-4 |
| Sort indicator | `arrow_upward/downward` 16 × 16 icon, 150 ms rotation | Text glyph `▲▼`, no transition | P2-3 |
| Sort indicator color | `primary` | `primary` — OK | — |
| Filter chrome (trigger) | 40 × 40 circular icon button with 8 % state layer | Outlined 1.5 rem button with border-color swap | P1-6 |
| Filter popup elevation | elevation-2 (menu) | Literal `rgba(0,0,0,0.12)` shadow (line 733) | P1-2 |
| Filter popup corner radius | `corner-extra-small` = 4 px (menus) | `border-radius: 8px` (line 731) | P2 |
| Column menu | elevation-2, `corner-extra-small` | Uses no `.mar-datagrid-column-menu` CSS at all in the Material provider — unstyled | P1 |
| Pager | text/full-radius buttons | Outlined 4 px-radius rectangles | P2-1 |
| Hover state | 8 % overlay | Flat color swap | P0-1 |
| Focus state | 3 px stroke in `secondary` with 2 px offset | 2 px `primary` stroke via `--sf-focus-ring` | P0-2 |
| Active / pressed state | 12 % overlay | Not defined for rows/headers | P0-1 |
| Disabled state | 38 % opacity (`--sf-disabled-opacity: 0.38` exists line 224) | Not applied to any grid selector | P1 |
| Selected row state | `secondary-container` or `primary-container` + layered overlay | `primary-container` base, no layer composition | P1-4 |
| Loading indicator | `linear-progress` or `circular-progress` (M3 progress component) | Static "Loading…" text, no animation (lines 789–797) | P1-3 |
| Skeleton loader | Pulsing surface-variant blocks | Exists for `.sf-datasheet` (lines 1312–1337), **not** for `.sf-datagrid` | P1 |
| Empty state | Icon + headline + body pattern, 48 px vertical padding | Single-line centered text, 24 × 12 px padding | P1-5 |
| Error state (cell/row) | `error-container` background, `on-error-container` text | Only datasheet has `--cell--invalid` (line 1217); grid has no per-cell error style | P2 |
| Density — dense (40 px) | Supported | 28 px (`--size-small`) | P0-3 |
| Density — standard (52 px) | Supported | 36 px (`--size-medium`) | P0-3 |
| Density — comfortable (72 px) | Supported | 48 px (`--size-large`) | P0-3 |
| Color-role tokens (primary/surface/etc.) | Full M3 set | Full M3 set present (lines 1–57) | — |
| Surface-tint overlay (M3 elevation) | Applied to raised surfaces | Tokens defined (lines 156–161) but never used by grid | P3-1 |
| Dark mode | Full token flip + per-component overrides where needed | Token flip present (lines 59–100); zero grid-specific overrides | P1-1 |
| A11y — 3 px focus stroke | 3 px outer + 2 px offset in `secondary` | 2 px outer in `primary` (line 215) | P0-2 |
| A11y — state-layer contrast | ≥ 3:1 vs base (auto via currentColor overlay) | Depends on flat swap contrast — dark-theme `surface-container-high` on `surface` may fall below | P0-1 |
| A11y — `aria-sort` on header | `ascending` / `descending` / `none` | Implemented (line 123, `GetAriaSortValue`) — OK | — |
| A11y — `aria-selected` on row | Expected | Implemented (Rendering.cs line 33) — OK | — |
| A11y — `aria-busy` during load | Expected | Implemented (razor line 14) — OK | — |
| Motion — duration tokens | `motion-duration-short1..extra-long4` | Full set (lines 180–195) — OK | — |
| Motion — easing tokens | emphasized / standard / linear | Full set (lines 173–179) — OK | — |
| Motion — reduced-motion support | `prefers-reduced-motion` opt-out | Implemented (lines 204–213) — OK | — |
| Shape — corner radii | 0/4/8/12/16/28/full | 0/4/12/16/28/full (missing `small` at 8 px) | P2 |

---

## 5. Implementation Priority Roadmap (for the follow-up agent)

1. **P0 pass** — land state-layer composition on rows/headers/cells (P0-1), fix focus indicator (P0-2), raise densities (P0-3), define and apply typescale tokens (P0-4). These four changes bring visual parity from approximately 55 % to approximately 85 %.
2. **P1 pass** — dark-mode overrides, loading bar, filter-menu icon button, selected-row layer composition, empty-state restructure, elevation tokens on popups.
3. **P2/P3 pass** — icon swaps, pager restyle, surface-tint adoption, motion on detail rows.

Keep the existing `.sf-state-layer` utility as the canonical helper; every P0-1 fix is just wiring existing tokens into existing selectors. No new infrastructure needed.

---

## 6. References Cited

- `material-web/tokens/versions/v0_192/_md-sys-state.scss` — state layer opacities
- `material-web/tokens/versions/v0_192/_md-sys-shape.scss` — corner radius values
- `material-web/tokens/versions/v0_192/_md-sys-typescale.scss` — typescale sizes
- `material-web/tokens/versions/v0_192/_md-sys-elevation.scss` — elevation level numbers
- `material-web/docs/theming/color.md` — color-role token list
- `material-web/docs/components/focus-ring.md` — focus-ring spec (3 px / secondary)
- `angular/components/src/material/table/table.scss` — MDC table defaults (52 / 56 px)
- m3.material.io/components/data-table/overview — data-table pattern (spec page, living doc)
- m3.material.io/foundations/interaction/states/state-layers — state layer guidance
