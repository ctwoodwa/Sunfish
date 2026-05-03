# SunfishDataGrid vs. Fluent UI v9 — Deep Style-Completeness Audit

Generated: 2026-04-22
Sources audited:
- `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\DataGrid\SunfishDataGrid.razor`
- `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\DataGrid\SunfishDataGrid.Rendering.cs`
- `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\DataGrid\SunfishGridColumnMenu.razor`
- `C:\Projects\Sunfish\packages\ui-adapters-blazor\Providers\FluentUI\wwwroot\css\sunfish-fluentui.css` (lines 1–200, 2597–2972)
- `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\DataGrid\GAP_ANALYSIS.md` (functional gaps tracker; referenced, not duplicated)

Fluent v9 references:
- TableRow styles: `microsoft/fluentui` → `packages/react-components/react-table/library/src/components/TableRow/useTableRowStyles.styles.ts`
- TableHeaderCell: same repo, `TableHeaderCell/useTableHeaderCellStyles.styles.ts`
- TableCell: same repo, `TableCell/useTableCellStyles.styles.ts`
- DataGrid component API: <https://storybooks.fluentui.dev/react?path=/docs/components-datagrid--docs>
- Spacing tokens: <https://storybooks.fluentui.dev/react?path=/docs/theme-spacing--docs>
- Border-radius tokens: <https://storybooks.fluentui.dev/react?path=/docs/theme-border-radii--docs>
- Focus indicator composition: <https://storybooks.fluentui.dev/react?path=/docs/concepts-developer-accessibility-focus-indicator--docs>
- Design-token styling handbook: <https://github.com/microsoft/fluentui/blob/master/docs/react-v9/contributing/rfcs/react-components/styles-handbook.md>

---

## 1. Summary

The SunfishDataGrid Fluent skin is a **Fluent v8-flavoured approximation running on a Sunfish-native CSS-variable layer (`--sf-*`)**. It uses Fluent-looking colors (`#0078d4`, `#e1dfdd`, `#323130`) and Fluent-ish spacing (`0.5rem 0.75rem` header padding), but it does **not** mirror Fluent v9 token semantics. The root problem is a two-layer indirection: the skin maps `--sf-color-*` → hard-coded Fluent-v8 hex values (e.g. `--sf-color-primary: #0078d4`, `--sf-color-surface: #f3f2f1`) rather than to Fluent v9 tokens. Fluent v9 uses a layered semantic palette (`colorNeutralBackground1` for the default canvas, `colorNeutralBackground2` for the header strip, `colorSubtleBackgroundHover` for row hover, `colorSubtleBackgroundSelected` for selection), and several state compositions (especially focus, selection, and density) have no counterpart in the Sunfish CSS.

The largest divergences are in **focus-indicator composition**, **selection colors**, **row density**, and **elevation/shadow use for popups and column-menus**. Sunfish draws focus with a `2px solid` primary-colored outline (inset offset -2px). Fluent v9 draws focus with `colorStrokeFocus2` at `strokeWidthThicker` (2px) *combined with `borderRadiusMedium` (4px)* — i.e. a rounded rectangle — inside an outer `colorStrokeFocus1` ring when composed via `createFocusOutlineStyle`. Sunfish selection paints a light blue `--sf-color-primary-light: #deecf9` (v8 blue-tint). Fluent v9 defaults to `selectionAppearance: "brand"` which renders `colorBrandBackground2` (a dedicated, near-white tint of the brand ramp with `colorTransparentStrokeInteractive` top/bottom) and exposes a `neutral` mode via `colorSubtleBackgroundSelected`. Sunfish's "small/medium/large" size tiers map to the wrong axis: Fluent v9 DataGrid uses `small` / `medium` / `extra-small` (44px / 36px / 24px row minimums), and compact modes are paired with `fontSizeBase200` / `lineHeightBase200`. Finally, Sunfish's filter and column-menu popups use ad-hoc box-shadows (`0 8px 24px rgba(0,0,0,.12)`) rather than `shadow16` (popup elevation) / `shadow8` (hover overlays).

---

## 2. Prioritized Gap List

### P0 — must-fix for parity

**P0-1. No mapping from `--sf-*` variables to Fluent v9 tokens; the skin is frozen on Fluent v8 hexes.**
Fluent v9 reference: `import { tokens } from '@fluentui/react-components'`; the web-provider emits CSS variables of the form `--colorNeutralBackground1`, `--colorStrokeFocus2`, `--spacingHorizontalS`. See <https://storybooks.fluentui.dev/react?path=/docs/concepts-developer-theming--docs>.
Sunfish current (`sunfish-fluentui.css` lines 1–30):
```css
--sf-color-primary: #0078d4;
--sf-color-surface: #f3f2f1;
--sf-color-border: #e1dfdd;
--sf-color-text-secondary: #605e5c;
```
These are Fluent **v8** (Fabric) hexes. v9 webLightTheme shifts to `colorNeutralBackground1: #ffffff`, `colorNeutralBackground2: #fafafa`, `colorNeutralStroke2: #e0e0e0`, `colorNeutralForeground2: #424242`.
Proposed change: add a Fluent-v9 token layer (e.g. `--fluent-color-neutral-background-1: #ffffff`) and route `--sf-color-surface` / `--sf-color-border` to those. A single `[data-sf-theme="fluent-web-light"]` selector block can hold the webLightTheme ramp; dark mode becomes `webDarkTheme` equivalents.

**P0-2. Header background uses `--sf-color-surface` (v8 `#f3f2f1`) instead of `colorNeutralBackground2` (`#fafafa`).**
Fluent reference: `useTableHeaderCellStyles.styles.ts` + `DataGridHeader` has `backgroundColor: tokens.colorNeutralBackground2` in default theme composition.
Sunfish current (`sunfish-fluentui.css` line 2611–2615):
```css
.sf-datagrid-header {
  background: var(--sf-color-surface);
  font-weight: var(--sf-font-weight-semibold);
  border-bottom: 1px solid var(--sf-color-border);
}
```
Proposed change:
```css
.sf-datagrid-header {
  background: var(--colorNeutralBackground2, #fafafa);
  border-bottom: var(--strokeWidthThin, 1px) solid var(--colorNeutralStroke2, #e0e0e0);
  font-weight: var(--fontWeightRegular, 400); /* v9 header uses regular, not semibold */
}
```
Note Fluent v9 TableHeaderCell uses `fontWeightRegular` — weight is carried by `colorNeutralForeground1` contrast, not bolding. Sunfish currently bolds headers which is a v7/v8 pattern.

**P0-3. Row focus ring and cell focus ring are non-idiomatic.**
Fluent reference (`useTableRowStyles.styles.ts`):
```
outline: `${strokeWidthThicker} solid ${colorStrokeFocus2}`,
borderRadius: tokens.borderRadiusMedium, // 4px
```
combined via `createCustomFocusIndicatorStyle` so the ring only shows on keyboard focus (`[data-keyboard-nav] :focus-visible`).
Sunfish current (lines 2917–2928):
```css
.sf-datagrid-cell:focus-visible,
.sf-datagrid-header-cell:focus-visible {
  outline: 2px solid var(--sf-color-focus-ring, var(--sf-color-primary));
  outline-offset: -2px;
}
.sf-datagrid-row--focused {
  outline: 2px solid var(--sf-color-focus-ring, var(--sf-color-primary));
  outline-offset: -1px;
}
```
Problems: (a) Uses `--sf-color-primary` (accent blue `#0078d4`) instead of the dedicated focus token `colorStrokeFocus2` (which is `#000000` on light, `#ffffff` on dark) — Fluent v9 deliberately does **not** brand-color the focus ring because it must compose with selected-brand rows; (b) no `border-radius`, so the ring is square on rounded containers; (c) no paired outer `colorStrokeFocus1` ring.
Proposed change:
```css
.sf-datagrid-cell:focus-visible,
.sf-datagrid-header-cell:focus-visible,
.sf-datagrid-row:focus-visible {
  outline: var(--strokeWidthThicker, 2px) solid var(--colorStrokeFocus2, #000);
  outline-offset: -2px;
  border-radius: var(--borderRadiusMedium, 4px);
  box-shadow: inset 0 0 0 var(--strokeWidthThin, 1px) var(--colorStrokeFocus1, #fff);
}
```

**P0-4. Selected-row color is v8 blue-tint, not `colorBrandBackground2`.**
Fluent reference (`useTableRowStyles.styles.ts`, `brand` appearance default):
```
backgroundColor: tokens.colorBrandBackground2,
borderTop: `${strokeWidthThin} solid ${colorTransparentStrokeInteractive}`,
borderBottom: `${strokeWidthThin} solid ${colorTransparentStrokeInteractive}`,
```
`colorBrandBackground2` is a near-white tinted-brand (webLightTheme: `#ebf3fc`), distinct from `colorBrandBackground` (the button fill).
Sunfish current (lines 2631–2637):
```css
.sf-datagrid-row--selected {
  background: var(--sf-color-primary-light); /* #deecf9 — v8 Fabric NeutralLighter blue */
}
.sf-datagrid-row--selected:hover {
  background: var(--sf-color-primary-light);
}
```
Problems: (a) too-saturated, matches Fabric not Fluent v9; (b) hover collapses back to the same value instead of darkening to `colorBrandBackground2Hover`; (c) no top/bottom interactive stroke. Also Sunfish lacks the `selectionAppearance="neutral"` alternative that v9 exposes.
Proposed change:
```css
.sf-datagrid-row--selected {
  background: var(--colorBrandBackground2, #ebf3fc);
  border-top: var(--strokeWidthThin, 1px) solid var(--colorTransparentStrokeInteractive, transparent);
  border-bottom: var(--strokeWidthThin, 1px) solid var(--colorTransparentStrokeInteractive, transparent);
}
.sf-datagrid-row--selected:hover {
  background: var(--colorBrandBackground2Hover, #cfe4fa);
}
.sf-datagrid-row--selected:active {
  background: var(--colorBrandBackground2Pressed, #b4d1f0);
}
/* New: neutral selection mode */
.sf-datagrid--selection-neutral .sf-datagrid-row--selected {
  background: var(--colorSubtleBackgroundSelected, #f0f0f0);
}
```

**P0-5. Row hover uses `--sf-color-surface` (same as header) — no `colorSubtleBackgroundHover`.**
Fluent reference (`useTableRowStyles.styles.ts`):
```
':hover': {
  backgroundColor: tokens.colorSubtleBackgroundHover, // #f5f5f5
  color: tokens.colorNeutralForeground1Hover,
}
```
Sunfish current (lines 2627–2629):
```css
.sf-datagrid-row:hover { background: var(--sf-color-surface); }
```
This makes hover rows identical to the header strip — breaks visual affordance.
Proposed change:
```css
.sf-datagrid-row:hover {
  background: var(--colorSubtleBackgroundHover, #f5f5f5);
  color: var(--colorNeutralForeground1Hover, #242424);
}
.sf-datagrid-row:active {
  background: var(--colorSubtleBackgroundPressed, #e0e0e0);
}
```

**P0-6. Cell padding is too large for Fluent v9.**
Fluent reference (`useTableHeaderCellStyles` + `useTableCellStyles`): `padding: 0 tokens.spacingHorizontalS` (= `0 8px`). Row height drives vertical space, not cell padding.
Sunfish current (lines 2937–2941):
```css
.mar-datagrid--size-medium {
  --mar-datagrid-font-size: 0.875rem;
  --mar-datagrid-cell-padding: 0.5rem 0.75rem; /* 8px 12px */
  --mar-datagrid-row-height: 36px;
}
```
The 12px horizontal padding is ~50% over Fluent's 8px `spacingHorizontalS`. Proposed:
```css
.mar-datagrid--size-medium {
  --mar-datagrid-cell-padding: 0 var(--spacingHorizontalS, 8px);
  --mar-datagrid-row-height: 36px; /* matches Fluent "small" DataGrid */
  --mar-datagrid-font-size: var(--fontSizeBase300, 14px);
  --mar-datagrid-line-height: var(--lineHeightBase300, 20px);
}
```

**P0-7. Density naming is off-by-one vs. Fluent v9.**
Fluent v9 `DataGrid` size prop: `"extra-small"` (~24px min-height, `fontSizeBase200`), `"small"` (~34px min-height, `fontSizeBase300`), `"medium"` (~44px min-height, default, `fontSizeBase300`). Sunfish uses `small=28px / medium=36px / large=48px`, which maps most closely to Fluent `extra-small / small / medium`.
Proposed change: add a `DataGridSize.ExtraSmall` enum value and realign CSS tiers:
```css
.mar-datagrid--size-extra-small { --mar-datagrid-row-height: 24px; --mar-datagrid-font-size: var(--fontSizeBase200, 12px); }
.mar-datagrid--size-small       { --mar-datagrid-row-height: 34px; --mar-datagrid-font-size: var(--fontSizeBase300, 14px); }
.mar-datagrid--size-medium      { --mar-datagrid-row-height: 44px; --mar-datagrid-font-size: var(--fontSizeBase300, 14px); }
```
(The existing `large` tier can remain as a Sunfish-specific extension but should be flagged "non-Fluent".)

**P0-8. Popup, column menu, and filter menu use ad-hoc shadows, not Fluent elevation tokens.**
Fluent reference: Popover/Menu uses `boxShadow: tokens.shadow16` (popup overlays), elevated surfaces use `shadow8`; hover cards use `shadow4`. See <https://storybooks.fluentui.dev/react?path=/docs/theme-shadows--docs>.
Sunfish current (line 2766): `box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);` — a hand-rolled shadow that does not adapt to dark theme (dark Fluent `shadow16` uses `shadowBrandBackground`/inverted composition).
Proposed change:
```css
.sf-datagrid .sf-datagrid-filter-menu,
.mar-datagrid-column-menu,
.sf-datagrid-popup-dialog {
  box-shadow: var(--shadow16, 0 8px 16px rgba(0,0,0,0.14), 0 0 2px rgba(0,0,0,0.12));
  border: var(--strokeWidthThin, 1px) solid var(--colorTransparentStroke, transparent);
  border-radius: var(--borderRadiusMedium, 4px);
  background: var(--colorNeutralBackground1, #fff);
}
```

### P1 — Fluent-visual correctness

**P1-1. Sort indicator uses Unicode arrows (▲/▼), not the Fluent `ArrowSortRegular` icon.**
Fluent DataGridHeaderCell renders an `<svg>` sort icon from `@fluentui/react-icons` (`ArrowSortUpRegular` / `ArrowSortDownRegular`) inside a `__sortIcon` slot and colors it with `colorNeutralForeground2`. Sunfish (SunfishDataGrid.razor line 139–146) uses `"▲"` / `"▼"` which render in the system font and are visually heavier than Fluent's 12-px line icons. Recommend replacing with inline SVG matching `ArrowSort` geometry and applying `color: var(--colorNeutralForeground2)`.

**P1-2. `sf-datagrid-sort-indicator` color is `--sf-color-primary`. Fluent v9 does NOT brand-color the sort indicator.**
Current (line 2855–2861): `color: var(--sf-color-primary);` — makes sorted header visually shout.
Fluent v9 sorted columns tint the *text* via `colorNeutralForeground1` and show the icon in `colorNeutralForeground2`. The "sorted" state is communicated by indicator presence, not color. Proposed:
```css
.sf-datagrid-sort-indicator { color: var(--colorNeutralForeground2, #424242); }
.sf-datagrid-header-cell--sorted { color: var(--colorNeutralForeground1, #242424); /* not primary */ }
```

**P1-3. Striped rows use `--sf-color-surface` — same as header.**
Fluent v9 does not ship a striped-rows pattern by default; when apps implement zebra striping they should use `colorNeutralBackground1Hover` (a barely-tinted neutral), not `colorNeutralBackground2`. Proposed `.sf-datagrid-row--striped:nth-child(even) { background: var(--colorNeutralBackground1Hover, #f5f5f5); }`.

**P1-4. `mar-datagrid-row--highlighted` hard-codes warning-yellow with `!important`.**
Current (lines 2949–2953):
```css
.mar-datagrid-row--highlighted {
  background-color: rgba(var(--sf-color-warning-rgb, 255, 193, 7), 0.18) !important;
  outline: 1px solid rgba(var(--sf-color-warning-rgb, 255, 193, 7), 0.5);
}
```
`!important` is a code smell and `--sf-color-warning-rgb` is undefined in the provider root. Fluent v9 pattern: use `colorPaletteYellowBackground1` / `colorPaletteYellowBorderActive` from the Fluent status palette. Remove `!important`, route `--sf-color-warning-rgb` from the theme, and switch to palette tokens.

**P1-5. Frozen-column boundary shadow is missing.**
Sunfish marks the boundary via the `mar-datagrid-col--locked-end` class but the class name has **no corresponding CSS rule** in `sunfish-fluentui.css` (Grep confirms: no rule for `mar-datagrid-col--locked-end`). The boundary drop-shadow that separates frozen from scrollable content is absent. Fluent's react-table-contrib virtualized grid uses `boxShadow: 6px 0 6px -4px rgba(0,0,0,0.16)` on the last sticky column. Proposed:
```css
.mar-datagrid-col--locked-end {
  box-shadow: var(--strokeWidthThin, 1px) 0 0 var(--colorNeutralStroke2, #e0e0e0),
              6px 0 6px -4px rgba(0,0,0,0.16);
}
[dir="rtl"] .mar-datagrid-col--locked-end {
  box-shadow: calc(-1 * var(--strokeWidthThin, 1px)) 0 0 var(--colorNeutralStroke2, #e0e0e0),
              -6px 0 6px -4px rgba(0,0,0,0.16);
}
```

**P1-6. Filter button active indicator dot is hard-coded 6px with inline accent positioning.**
Current (lines 2739–2748): `::after { width: 6px; height: 6px; background: var(--sf-color-primary); }` — the pattern is non-Fluent. Fluent pattern: render a dot using `colorCompoundBrandForeground1` at `1px` offset with `borderRadiusCircular`. Also, the active-button background uses `color-mix` which is not widely compatible — Fluent v9 uses `colorCompoundBrandBackground` instead.

**P1-7. Checkbox column has no dedicated selection-cell styling.**
Fluent v9 ships `TableSelectionCell` with `width: 44px`, centered checkbox, and a `subtle` mode that hides the checkbox until hover/focus. Sunfish renders a raw `<input type="checkbox">` in a 40-px `<th>`/`<td>` (lines 92–102 of the razor) with no wrapper class for styling. Add `.sf-datagrid-selection-cell` and `.sf-datagrid-selection-cell--subtle` with opacity transitions: `opacity: 0; transition: opacity .1s;` and `.sf-datagrid-row:hover .sf-datagrid-selection-cell--subtle, .sf-datagrid-selection-cell--subtle:has(input:checked) { opacity: 1; }`.

**P1-8. Loading overlay uses `color-mix(in srgb, var(--sf-color-surface) 70%, transparent)`.**
Fluent v9 uses `colorBackgroundOverlay` (`rgba(255,255,255,0.4)` light / `rgba(0,0,0,0.4)` dark). Sunfish's approach relies on `color-mix` which is Chrome 111+/Safari 16.4+ only. Proposed:
```css
.sf-datagrid-loading-overlay { background: var(--colorBackgroundOverlay, rgba(255,255,255,0.4)); }
```
Sunfish also ships no real `Spinner` — the overlay renders plain text "Loading…". Fluent v9 wraps `<Spinner size="medium" />` which uses its own token set. Recommend composing with the existing `SunfishSpinner`.

### P2 — Polish & dark-theme

**P2-1. Dark-theme remap is incomplete.**
The `[data-sf-theme=dark]` block (lines 119–147) only overrides `--sf-color-*` but the DataGrid-specific hard-coded fallbacks in `.sf-datagrid .sf-datagrid-filter-menu-btn` (`#c8c6c4`, `#605e5c`, `#ffffff`, line 2711–2713) are not remapped. In dark mode these render as light on light. Replace every inline hex fallback with a CSS variable that the dark block overrides.

**P2-2. Pager buttons use `--sf-color-primary` fill for active — Fluent v9 uses text-underline style.**
Fluent DataGrid traditionally pairs with the `react-table-contrib` pager which is text-only (no full accent fill). Current Sunfish pager active style is closer to Bootstrap. Consider a `--fluent` variant that uses `color: var(--colorBrandForeground1); background: transparent; border-bottom: 2px solid var(--colorBrandStroke1);`.

**P2-3. Missing `colorNeutralForeground2` (secondary text) for supporting header chrome.**
Filter menu labels currently use `var(--sf-color-text, #323130)` which is `colorNeutralForeground1`. Secondary labels (placeholders, counts, disabled-pager) should route to `colorNeutralForeground3` / `colorNeutralForeground4` per Fluent v9 token ladder.

**P2-4. Detail-row / group-header rows use the same `--sf-color-surface` as the header.**
Fluent pattern: detail rows use `colorNeutralBackground3` (one tier below header) to stay distinguishable when both are visible. Group header rows use `colorNeutralBackground2` + `fontWeightSemibold` + bottom `strokeWidthThin colorNeutralStroke2`.

**P2-5. Toolbar uses a plain bottom border.**
Fluent v9 Toolbar uses `border-bottom: strokeWidthThin colorNeutralStroke1` (stronger than the row `colorNeutralStroke2`) to separate toolbar from data. Sunfish uses `--sf-color-border` for both — no visual hierarchy.

**P2-6. Search box focus ring uses `border-color: var(--sf-color-primary)`.**
Fluent v9 input focus uses `borderBottomColor: colorCompoundBrandStroke` + `borderBottomWidth: strokeWidthThick` (2px accent underline) instead of a full-border primary color. Align the `.sf-datagrid-searchbox input:focus` to the Fluent Input pattern.

**P2-7. No high-contrast-mode forced-colors support.**
Fluent v9 ships `@media (forced-colors: active)` rules for focus / selection / border. Sunfish DataGrid CSS has none — in Windows High Contrast the grid loses selection and focus affordance.

### P3 — Optional alignment

**P3-1.** Replace the Unicode `⠇` drag-handle (`⠇`, used at SunfishDataGrid.Rendering.cs:52) with Fluent `ReOrderDotsVertical20Regular` geometry.
**P3-2.** Detail expand uses `▼`/`▶` triangles; Fluent uses `ChevronRight20Regular`/`ChevronDown20Regular`.
**P3-3.** Incell-edit save/cancel uses `✓` / `✗` (heavy checks). Fluent uses `CheckmarkRegular` / `DismissRegular` at `colorCompoundBrandForeground1` / `colorNeutralForeground2` respectively.
**P3-4.** `sf-datagrid-cmd-btn` (lines implicitly inherit from toolbar button): no Fluent v9 `Button` shape parity. Fluent v9 Button composes `borderRadiusMedium`, `fontWeightSemibold`, `spacingHorizontalM`, per-variant colors. Consider having command buttons use the same `SunfishButton` component the rest of Sunfish uses, rather than inline CSS.
**P3-5.** No support for the Fluent v9 `DataGrid` `subtleSelection` prop. Since the grid already tracks selected rows, the CSS hook is trivial to add (hide checkbox + brand tint until hover).

---

## 3. Token-Mapping Table

| Fluent v9 token | Value (webLightTheme) | Where it should appear in Sunfish CSS | Current Sunfish value | Recommended mapping |
|---|---|---|---|---|
| `colorNeutralBackground1` | `#ffffff` | body cells, empty-state bg | `var(--sf-color-background)` = `#ffffff` | rename or alias `--sf-color-background` to `--colorNeutralBackground1` |
| `colorNeutralBackground1Hover` | `#f5f5f5` | `.sf-datagrid-row--striped:nth-child(even)` | `var(--sf-color-surface)` = `#f3f2f1` | switch to `#f5f5f5` |
| `colorNeutralBackground2` | `#fafafa` | `.sf-datagrid-header`, `.sf-datagrid-toolbar`, `.sf-datagrid-footer-row`, group header | `var(--sf-color-surface)` = `#f3f2f1` | switch to `#fafafa` |
| `colorNeutralBackground3` | `#f5f5f5` | `.sf-datagrid-detail-row` | `var(--sf-color-surface)` | add separate var |
| `colorSubtleBackgroundHover` | `#f5f5f5` | `.sf-datagrid-row:hover` | `var(--sf-color-surface)` | P0-5 fix |
| `colorSubtleBackgroundPressed` | `#e0e0e0` | `.sf-datagrid-row:active` | none | new rule |
| `colorSubtleBackgroundSelected` | `#f0f0f0` | `.sf-datagrid--selection-neutral .sf-datagrid-row--selected` | none | new rule (P0-4) |
| `colorBrandBackground2` | `#ebf3fc` | `.sf-datagrid-row--selected` (brand appearance) | `var(--sf-color-primary-light)` = `#deecf9` | P0-4 fix |
| `colorBrandBackground2Hover` | `#cfe4fa` | `.sf-datagrid-row--selected:hover` | `var(--sf-color-primary-light)` | P0-4 fix |
| `colorBrandBackground2Pressed` | `#b4d1f0` | `.sf-datagrid-row--selected:active` | none | new rule |
| `colorNeutralStroke1` | `#d1d1d1` | outer `.sf-datagrid` border, toolbar bottom-border | `var(--sf-color-border)` = `#e1dfdd` | switch |
| `colorNeutralStroke2` | `#e0e0e0` | row bottom-border, filter menu border | `var(--sf-color-border)` = `#e1dfdd` | switch |
| `colorNeutralStrokeOnBrand` | `#ffffff` | selected row top/bottom borders | none | new |
| `colorTransparentStrokeInteractive` | `transparent` | selected-row borders (brand mode) | none | new (P0-4) |
| `colorNeutralForeground1` | `#242424` | cell text, header text | `var(--sf-color-on-background)` = `#323130` | switch (v8→v9 darker grey) |
| `colorNeutralForeground1Hover` | `#242424` | row-hover text | none | new |
| `colorNeutralForeground2` | `#424242` | sort icon color, pager-disabled hints | `var(--sf-color-primary)` | P1-2 fix |
| `colorNeutralForeground3` | `#616161` | placeholder/secondary labels | `var(--sf-color-text-secondary)` = `#605e5c` | switch |
| `colorNeutralForeground4` | `#707070` | disabled pager text | `var(--sf-color-text-muted)` | switch |
| `colorCompoundBrandForeground1` | `#0f6cbd` | active filter-dot, "Save" action check | `var(--sf-color-primary)` | clarify |
| `colorCompoundBrandStroke` | `#0f6cbd` | search input focus bottom-border | `var(--sf-color-primary)` | clarify |
| `colorStrokeFocus1` | `#ffffff` | inset inner focus ring | none | new (P0-3) |
| `colorStrokeFocus2` | `#000000` | `:focus-visible` outline on cell/row/header | `var(--sf-color-focus-ring, var(--sf-color-primary))` = `#0078d4` | P0-3 fix |
| `colorStatusDangerBackground1` | `#fdf3f4` | validation summary bg | `color-mix(... danger 8%, surface)` | switch |
| `colorStatusDangerForeground1` | `#bc2f32` | validation summary text | `var(--sf-color-danger, #d92626)` | switch |
| `colorBackgroundOverlay` | `rgba(255,255,255,0.4)` | `.sf-datagrid-loading-overlay`, `.sf-datagrid-popup-overlay` | `color-mix(... surface 70%, transparent)` / `rgba(0,0,0,0.3)` | P1-8 fix |
| `spacingHorizontalXXS` | `2px` | — | hard `0.25rem` | add token |
| `spacingHorizontalXS` | `4px` | `.sf-datagrid-filter-cell` padding | `var(--sf-space-xs)` = `4px` | alias OK |
| `spacingHorizontalS` | `8px` | cell padding, pager gap | `var(--sf-space-sm)` = `8px` in some places, `0.75rem` in others | unify via `spacingHorizontalS` |
| `spacingHorizontalM` | `12px` | toolbar gap, pager padding | `var(--sf-space-md)` = `12px` | alias OK |
| `spacingVerticalXXS` | `2px` | sort-icon top-padding | hard `0` | add |
| `spacingVerticalS` | `8px` | cell vertical padding (medium size) | `0.5rem` | switch to token |
| `strokeWidthThin` | `1px` | all borders | implicit `1px` | add token |
| `strokeWidthThick` | `2px` | search-input focus underline | none | new (P2-6) |
| `strokeWidthThicker` | `3px` (spec) / `2px` (focus composition) | focus outline | hard `2px` | use token |
| `borderRadiusNone` | `0` | header cells | implicit | add |
| `borderRadiusSmall` | `2px` | pager buttons | `var(--sf-radius-sm)` | alias |
| `borderRadiusMedium` | `4px` | focus-ring radius, filter-menu-btn, popups | `4px` / `6px` mixed | standardize on `borderRadiusMedium` |
| `borderRadiusXLarge` | `8px` | filter popup container | `8px` | alias |
| `fontFamilyBase` | `"Segoe UI", …` | grid font | `var(--sf-font-family)` = `"Segoe UI"` | alias |
| `fontSizeBase200` | `12px` | extra-small size cells | `0.75rem` | alias |
| `fontSizeBase300` | `14px` | default cell/header font | `0.875rem` | alias |
| `fontWeightRegular` | `400` | header cells (v9) | Sunfish uses `semibold` | P0-2 fix |
| `fontWeightSemibold` | `600` | group header, footer-row | `var(--sf-font-weight-semibold)` | alias |
| `lineHeightBase200` | `16px` | extra-small cells | implicit | add |
| `lineHeightBase300` | `20px` | default cells | implicit | add |
| `shadow4` | `0 2px 4px rgba(0,0,0,0.14)…` | hover cards | none | new |
| `shadow8` | `0 4px 8px rgba(0,0,0,0.14)…` | raised toolbar | none | new |
| `shadow16` | `0 8px 16px rgba(0,0,0,0.14)…` | filter popup, column menu, popup-edit dialog | `0 8px 24px rgba(0,0,0,0.12)` | P0-8 fix |

---

## 4. Focus-Area Coverage Table

| Focus area | Fluent v9 idiomatic? | Sunfish state | Priority |
|---|---|---|---|
| Header background | No — uses v8 `#f3f2f1` surface | `.sf-datagrid-header { background: var(--sf-color-surface) }` | **P0-2** |
| Header font weight | No — bolds; v9 uses `fontWeightRegular` | `font-weight: var(--sf-font-weight-semibold)` | **P0-2** |
| Header padding | Close (`0.5rem 0.75rem`) but 12-px horiz > Fluent 8-px | `--mar-datagrid-cell-padding: 0.5rem 0.75rem` | **P0-6** |
| Header sort indicator | Partial — Unicode arrow instead of icon | `▲`/`▼` with `color: var(--sf-color-primary)` | **P1-1, P1-2** |
| Header hover | OK conceptually | `.sf-datagrid-header-cell--sortable:hover { background: var(--sf-color-surface-hover) }` | P2 — should route to `colorSubtleBackgroundHover` |
| Header focus-visible | **No** — primary-colored outline, no radius, no inner ring | `outline: 2px solid var(--sf-color-focus-ring, --sf-color-primary)` | **P0-3** |
| Row background | OK (white) | `.sf-datagrid-row` (inherits from table) | — |
| Row bottom border | OK shape | `border-bottom: 1px solid var(--sf-color-border)` | P1 — switch to `colorNeutralStroke2` |
| Row hover | **No** — uses header's surface color | `.sf-datagrid-row:hover { background: var(--sf-color-surface) }` | **P0-5** |
| Row active/pressed | Missing | no rule | **P0-5** (extension) |
| Row selected (brand) | **No** — v8 blue | `var(--sf-color-primary-light)` `#deecf9` | **P0-4** |
| Row selected (neutral mode) | Missing | no rule | **P0-4** (extension) |
| Row selected + hover | Doesn't change | `:selected:hover` same as `:selected` | **P0-4** |
| Row focus | **No** — primary-colored | `.sf-datagrid-row--focused { outline: 2px solid var(--sf-color-primary) }` | **P0-3** |
| Row disabled | Missing | no rule | P2 — add `.sf-datagrid-row[aria-disabled="true"]` with `colorNeutralForegroundDisabled` + reduced opacity |
| Cell padding | Too large (12-px H) | `0.5rem 0.75rem` | **P0-6** |
| Cell focus-visible | Close shape, wrong color/radius | see P0-3 | **P0-3** |
| Cell selected | Partial — `.mar-datagrid-cell--selected` class applied (Rendering.cs:111) but **no CSS rule defined** | class referenced, rule missing | **P0-4** |
| Sort indicator color | **No** — primary blue | `.sf-datagrid-sort-indicator { color: var(--sf-color-primary) }` | **P1-2** |
| Sort indicator shape | **No** — Unicode ▲/▼ | `▲` / `▼` | P1-1 |
| Multi-sort order indicator | Non-Fluent — raw `<sub>` | `<sub class="sf-datagrid-sort-order">` with no CSS | P2 — needs Fluent-styled pill |
| Filter-menu button | OK functional shape, wrong tokens | inline hex fallbacks | P2-1 (dark mode broken) |
| Filter-menu-btn active | Non-Fluent — `color-mix` + custom dot | `::after` 6-px dot | P1-6 |
| Filter popup | Partial — has shadow but wrong tokens | `box-shadow: 0 8px 24px rgba(0,0,0,0.12)` | **P0-8** |
| Filter popup radius | Off (`8px`) — Fluent uses `borderRadiusMedium` (4px) | `border-radius: 8px` | P2 |
| FilterRow input | No Fluent Input parity | plain `<input>` with no class | P2 — wrap in `SunfishInput` |
| Column menu | Partial — class exists, ad-hoc shadow | `mar-datagrid-column-menu` | **P0-8** |
| Column resize handle | Close; uses `rgba(0,0,0,0.2)` hover | `.mar-datagrid-col-resize-handle` | P2 — should use `colorNeutralStrokeAccessibleHover` |
| Frozen boundary shadow | **Missing CSS rule** | class `.mar-datagrid-col--locked-end` referenced, no style | **P1-5** |
| Pager | OK functional, wrong hover token | `--sf-color-surface-hover` | P2-2 |
| Pager active button | Non-Fluent — filled primary | `.sf-datagrid-pager-btn--active { background: var(--sf-color-primary) }` | P2-2 |
| Pager disabled state | OK | `cursor: not-allowed` + `--sf-color-text-muted` | — |
| Loading overlay | Non-Fluent composition | `color-mix(…surface 70%…)` + text only | **P1-8** |
| Empty state | Minimal | `.sf-datagrid-empty { color: var(--sf-color-text-muted) }` | P2 — add illustration slot |
| Error / validation summary | Close | uses `--sf-color-danger` + `color-mix` | P2 — switch to `colorStatusDanger*` |
| Density — compact | Misaligned | `size-small` = 28px | **P0-7** |
| Density — default | Misaligned | `size-medium` = 36px (Fluent default is 44px) | **P0-7** |
| Density — extra-small | Missing | no enum value | **P0-7** |
| Theme tokens | Tokens exist but on v8 hexes | `--sf-color-*` bound to Fabric `#0078d4`, `#f3f2f1` etc. | **P0-1** |
| Dark mode | Partial — theme block present, but DataGrid-local hex fallbacks bypass it | `data-sf-theme=dark` block only covers `--sf-color-*` | **P2-1** |
| A11y — aria-sort | Correct | `aria-sort="ascending/descending"` (razor line 123) | — |
| A11y — aria-selected on row | Correct | Rendering.cs:33 | — |
| A11y — aria-rowcount/colcount | Correct | razor line 12–13 | — |
| A11y — aria-busy on loading | Correct | razor line 14 | — |
| A11y — focus-visible only (keyboard) | No — uses `:focus-visible` but no `[data-keyboard-nav]` guard | lines 2917–2927 | P2 — wrap in `createCustomFocusIndicatorStyle` analogue |
| A11y — forced-colors mode | Missing | no `@media (forced-colors: active)` | **P2-7** |
| A11y — high-contrast selected/hover override | Missing | — | **P2-7** |
| RTL (frozen columns, icons) | Partial — component uses `FrozenPosition.Start/End` logically but CSS uses `border-right` | `.sf-datagrid-col--locked { border-right: 2px … }` (line 2890) | P2 — switch to `border-inline-end` |
| Scrollbar styling | Missing | inherits browser default | P3 — optional Fluent-matched scrollbar |
| Detail-row background | Uses same `--sf-color-surface` as header | line 2879 | P2-4 |
| Group header | Uses `--sf-color-surface`, `font-weight: semibold` — close | line 2693–2697 | P2 minor — add bottom border |

---

### Implementation path (recommended sequencing)

1. **Ship a Fluent v9 token shim** (`fluent-v9-tokens.css`) that emits `--colorNeutralBackground1` … `--colorStrokeFocus2` etc. via a single selector block. This unblocks every P0 at once because the DataGrid rules only need to swap their values from `--sf-color-*` to `--colorXxx`.
2. Apply **P0-2, P0-5, P0-4** — header, hover, selected — as one commit; together they're the 80% visible difference.
3. Apply **P0-3** focus ring — the biggest a11y-visible gap.
4. Apply **P0-7** density realignment; this changes `DataGridSize` enum, so it needs an API-change note.
5. Apply **P0-8** shadows + P1-5 frozen boundary in one "elevation" commit.
6. P1/P2 items can then land incrementally without risk of regression because the token layer makes them mechanical.

### Deferred to gap-analysis (functional, not styling)

Items already tracked in `GAP_ANALYSIS.md` that are **functional** (not styling) and therefore out of scope for this audit:
- B1 Keyboard Navigation (arrow keys, Enter-to-edit)
- C4 Column Chooser
- C6 Multi-Column Headers
- C7 Cell Selection API
- D4 Toolbar built-in tools

Word count: ~3,700.
