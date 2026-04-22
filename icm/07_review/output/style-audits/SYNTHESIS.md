# Style Parity Audit — Cross-Framework Synthesis

**Scope:** SunfishDataGrid, SunfishButton, SunfishDialog, SunfishCalendar × Bootstrap 5 / Fluent UI v9 / Material Design 3
**Date:** 2026-04-22
**Input:** 12 individual audit reports in this folder

---

## Executive Summary

Across 12 component/framework combinations the audits surfaced **50 P0, 68 P1, 65 P2, 34 P3** findings (~217 total). The dominant systemic problems are: (1) **Razor-to-CSS class-name mismatches** that render CSS completely dead on three components (SunfishCalendar BS5+Fluent, SunfishDialog BS5, size modifiers on Dialog-Fluent, pager buttons on DataGrid) — roughly one-third of the P0 pile traces back to this single defect; (2) **wholesale unstyled components** under at least one skin (SunfishDialog-Material, SunfishCalendar-Material, several DataGrid-Material sub-surfaces) where the SCSS partial is a 5-line TODO stub; (3) **token-foundation drift** where `--sf-*` variables are frozen on Fluent-v8 Fabric hexes, mis-mapped to M3 role tokens, or hardcoded away from `--bs-*` CSS custom properties — breaking dark mode and theme customization across all three providers. Recommended sequencing: fix class-name cascades first (cheap, high-leverage), then fill the three empty SCSS partials (Dialog-Material, Calendar-Material, Calendar-FluentUI missing states), then address token foundations. A four-batch parallelized execution (detailed below) fits comfortably in a focused 2–3 week effort if two fan-out agents run per framework.

---

## Systemic Themes (Cross-Component, Cross-Framework)

### Theme 1: Razor emits one class set, CSS styles another (dead-CSS cascade)

- **Affected:**
  - SunfishCalendar × BS5 (`sf-calendar__cell--*` emitted, `sf-calendar__day--*` styled)
  - SunfishCalendar × Fluent v9 (BEM double-underscore emitted, single-hyphen CSS)
  - SunfishDialog × BS5 (`sf-dialog-title/body/actions/close` emitted, never styled in BS skin)
  - SunfishDialog × Fluent v9 (size modifiers target `.sf-dialog__panel` — element never rendered; draggable targets `.sf-dialog-header` — never rendered)
  - SunfishDataGrid × BS5 (`.mar-datagrid-cmd-btn`, `.mar-datagrid-column-menu-trigger`, `.mar-datagrid-cell--selected` emitted, no CSS rules)
  - SunfishDataGrid × Fluent v9 (`.mar-datagrid-col--locked-end` emitted, no CSS)
  - SunfishButton × BS5 (`.btn-icon`, `.sf-bs-icon-button`, `.sf-button__icon` emitted, no CSS)
- **Root cause:** no Razor→CSS contract test. The component author picks a class name, the CSS author chose a slightly different one months earlier, nothing fails the build. BEM double-underscore vs single-hyphen is the most common form; stale modifier names (`range-edge` vs `range-start/end`, `cell` vs `day`) the second.
- **Recommended fix strategy:** (a) one-shot pass that diffs every emitted class against the provider's CSS selector set and renames to agree; (b) add a parity test that renders each component under each provider and asserts no emitted class is unmatched by a CSS rule; (c) codify BEM double-underscore as the convention (most of the existing code base already uses it). Prefer renaming the CSS over the Razor because the Razor is the API surface.
- **Priority:** **P0** — every other gap below is masked or multiplied by this one.

### Theme 2: Empty/stub SCSS partials under one or more providers

- **Affected:**
  - SunfishDialog × Material (`_dialog.scss` is a 5-line TODO — 100% unstyled in production)
  - SunfishCalendar × Material (`_date-picker.scss` is a 5-line TODO — 100% unstyled)
  - SunfishDialog × BS5 (structurally unstyled — has the overlay class but no `.modal-content/header/body/footer` mapping)
  - Partial: DataGrid × Material (state-layers declared but not wired), DataGrid × Fluent (popup/column-menu unstyled), Calendar × BS5 (year/decade/century grids, week-number column unstyled)
- **Root cause:** skin partials were created as placeholders during `_shared/styles` refactor but never filled in. No "provider completeness" gate in the build.
- **Recommended fix strategy:** write each partial end-to-end against the corresponding framework spec in one sitting. Material Dialog and Material Calendar are each ~60-line SCSS files; Bootstrap Dialog is a provider-surface addition. Add a build-time check that no `*.scss` partial contains only a TODO comment.
- **Priority:** **P0** — a skin that ships an unstyled component is not shippable.

### Theme 3: Token-foundation drift — `--sf-*` variables don't route to framework tokens

- **Affected:** every Fluent UI v9 audit (Button, Dialog, Calendar, DataGrid); DataGrid × Material (typescale `--sf-font-weight-semibold` undefined, surface-tint tokens defined but unused); Dialog × Fluent (`--elevation-shadow-dialog` defined but never consumed)
- **Root cause:** Sunfish's `--sf-color-*` / `--sf-radius-*` / `--sf-shadow-*` layer was originally filled with Fluent v8 (Fabric) hexes. No token shim maps them to the real Fluent v9 / M3 / BS5 token names (`--colorNeutralBackground1`, `--md-sys-color-primary`, `--bs-primary`). Dark-mode flips only reach the generic `--sf-*` vars, so any component that hardcodes literal colors (or reaches through an intermediate that hardcodes them) breaks dark mode silently.
- **Recommended fix strategy:** per-provider token shim. FluentUI: ship `fluent-v9-tokens.css` that emits `--colorNeutralBackground1` etc. and have Sunfish CSS reference `var(--colorNeutralBackground1, var(--sf-color-background))`. Material: introduce typescale tokens (`--sf-typescale-body-medium-*`, `--sf-typescale-title-small-*`) and define `--sf-font-weight-semibold` (currently undefined, referenced 12+ times). Bootstrap: stop writing literal hex; every color reads `var(--bs-*)` through the existing provider.
- **Priority:** **P0** for the missing token definitions (`--sf-font-weight-semibold`, `--elevation-shadow-dialog` consumption); **P1** for the broader token-shim work.

### Theme 4: State-layer infrastructure exists but components don't consume it

- **Affected:** SunfishDataGrid × Material (0 of 15 interactive surfaces apply `.sf-state-layer`); SunfishCalendar × Material (day cells, nav buttons, title button — none consume state layers); SunfishButton × Material (focus overlay stacks with focus ring — should be one or the other); DataGrid × Material filter-menu-btn uses border swap, not 8% overlay
- **Root cause:** the `.sf-state-layer` utility + `--sf-state-hover-opacity`/`-focus-opacity`/`-pressed-opacity` tokens were built in `_interactive-states.scss` but never wired into component selectors. Components "fake it" with flat background swaps.
- **Recommended fix strategy:** for each Material-skinned interactive element, add either (a) `class="sf-state-layer"` on the element, or (b) a `::before` pseudo per the established pattern. Keep opacities at the tokenized 8/12/12 (note one audit flagged M3 spec is actually 8/10/12 for focus — that is a token-level fix separate from component wiring).
- **Priority:** **P0** for Calendar/Dialog (they are otherwise unstyled); **P1** for DataGrid (has flat swaps that work, just not layered).

### Theme 5: Focus-ring composition is non-idiomatic across all three frameworks

- **Affected:** every component × framework combination has a focus-ring finding.
  - BS5: uses `outline` instead of `box-shadow` from `--bs-focus-ring-*`; uses `:focus` instead of `:focus-visible`; Calendar has zero focus rings (WCAG 2.4.7 fail); Dialog close button unstyled.
  - Fluent v9: every component uses `--sf-focus-ring` with brand-primary color, Fluent spec is `colorStrokeFocus2` (black/white, not brand). Button lacks the primary-specific inner `colorNeutralForegroundOnBrand` ring; no dual-stroke on Dialog.
  - M3: Button stacks focus-ring + state-layer overlay (should be one); Calendar Material has no focus rings at all; Dialog/DataGrid focus tokens undefined.
- **Root cause:** `--sf-focus-ring` was defined once as "2px bg + 2px primary" and replicated to every component. No framework uses that exact pattern — BS5 wants box-shadow, Fluent wants black/white dual-stroke, M3 wants 3px outer stroke with 2px offset.
- **Recommended fix strategy:** provider-level focus-ring mixin/variable. `.sf-focus-ring-bs5`, `.sf-focus-ring-fluent-neutral`, `.sf-focus-ring-fluent-primary`, `.sf-focus-ring-m3`. Components reference the semantic name; the skin routes to the idiomatic composition. Switch all rules from `:focus` to `:focus-visible`.
- **Priority:** **P0** for Calendar (no focus rings at all — a11y regression); **P1** elsewhere.

### Theme 6: Dark-mode overrides missing or hardcoded away from theme tokens

- **Affected:** SunfishDataGrid × BS5 (zero `[data-bs-theme=dark]` rules for the grid); SunfishCalendar × BS5 (hardcoded `#0d6efd`, `#212529`, `#fff` — dark mode renders white-on-white for selected/hover); SunfishDialog × BS5 (no `data-bs-theme` wiring); SunfishButton × Fluent (hover/active hex like `#c43035`, `#e69a3e` — dark block doesn't override them); SunfishCalendar × Fluent (no dark overrides on any `sf-calendar-*` rule); DataGrid × Fluent (filter-menu-btn hex fallbacks render light-on-light in dark); several Material components never flip because component rules don't exist to consume the token flip.
- **Root cause:** hardcoded hex RGB inside component CSS bypasses the token system. No regression test that flips `[data-bs-theme=dark]` / `[data-sf-theme=dark]` / `webDarkTheme` and visually diffs.
- **Recommended fix strategy:** audit pass to replace literal hex/rgba inside component CSS with the correct token reference. Add screenshot-diff test under dark mode per component. Note Fluent v9 does NOT darken `colorBackgroundOverlay` in dark mode — several audits flagged Sunfish incorrectly darkening to 0.6 alpha.
- **Priority:** **P1** (visible, testable, cascades from Theme 3).

### Theme 7: Command buttons, close buttons, and nav chevrons bypass their host component library

- **Affected:** DataGrid × BS5 (command buttons use `.mar-datagrid-cmd-btn` with no rule; should be `.btn .btn-sm .btn-outline-*`); Dialog × BS5 (close is `&times;` literal, should be `.btn-close`); Dialog × Fluent (close button unstyled); Dialog × Material (close should be icon-button not `&times;`); Calendar × all three (nav chevrons are `&lsaquo;`/`&rsaquo;` glyphs, not proper icon buttons); Button × Fluent (IconButton is a separate surface rather than `iconOnly` mode on Button).
- **Root cause:** parent component renders child UI using its own ad-hoc classes instead of composing the underlying `SunfishButton`/`SunfishIconButton` with the correct provider classes.
- **Recommended fix strategy:** route all in-component buttons through `CssProvider.CommandButtonClass()` / `IconButtonClass()` methods (some already exist; add missing). Swap Unicode glyphs for `SunfishIcon` with `ChevronLeft`/`ChevronRight`/`Close`/`Dismiss` names. This is a consumer-visible fix in Razor + a small provider-interface extension.
- **Priority:** **P1** (visible quality gap but not a11y-blocking).

### Theme 8: Density/sizing misalignment with framework norms

- **Affected:** DataGrid × M3 (28/36/48px heights — M3 spec is 40/52/72); DataGrid × Fluent v9 (`small=28/medium=36/large=48` maps to Fluent's `extra-small/small/medium` but under different names, and medium is labeled as default where Fluent defaults to 44px); Button × M3 (32/40/48 doesn't match M3 Expressive 32/40/56); Button × Fluent (no min-width; Fluent requires 64/96/96); Calendar × M3 (cell size unset, inherits UA default; M3 wants 40px comfortable/32px compact).
- **Root cause:** Sunfish picked a single internal density ladder without consulting the target frameworks' spec minimums.
- **Recommended fix strategy:** per-framework size re-tier. Either expand the enum (add `ExtraSmall` for Fluent) or document the Sunfish values as "approximations" and align in a v2 pass.
- **Priority:** **P1** for DataGrid (WCAG touch-target + visual quality); **P2** for Button/Calendar.

### Theme 9: Icon vocabulary (Unicode glyphs instead of framework icons)

- **Affected:** DataGrid sort indicators (`▲`/`▼` in all three skins); Dialog close (`&times;`); Calendar nav (`&lsaquo;`/`&rsaquo;`); DataGrid row drag handle (`⠇` Braille); DataGrid detail expand (`▼`/`▶` triangles); Calendar century/decade drills; DataGrid column menu (`⋮` character).
- **Root cause:** Sunfish built to avoid a hard icon-library dependency. Result: Unicode char fallbacks look aliased and off-rhythm against the rest of the provider's chrome.
- **Recommended fix strategy:** introduce `SunfishIcon` with a provider-scoped icon name table (BS5 → Bootstrap Icons, Fluent → Fluent Icons, Material → Material Symbols). Keep Unicode as last-resort fallback.
- **Priority:** **P2** (polish, not functional).

### Theme 10: A11y gaps (ARIA, focus trap, forced-colors, reduced-motion)

- **Affected:** Calendar × BS5 (`role="application"` is wrong — should be `<div>` with `<table role="grid">` child + roving tabindex); Dialog × all three (no focus trap, no Esc close, no `aria-labelledby`); Button × Fluent (no `forced-colors` branches, no `prefers-reduced-motion` rule); DataGrid × Fluent (no `forced-colors: active` → grid loses selection/focus in Windows HC); Calendar × Fluent (prev/next chevron `tabindex="-1"` — keyboard unreachable); Dialog × Material (no focus trap).
- **Root cause:** a11y plumbing was never systematized. WCAG 2.4.7 (focus visible), 1.4.11 (non-text contrast under forced-colors), 2.3.3 (animation preference) all have at least one failure.
- **Recommended fix strategy:** dedicated a11y pass per component — can be parallelized with styling work. Focus trap / Esc / scroll-lock for Dialog requires a tiny JS interop. The CSS-only pieces (forced-colors, reduced-motion, `:focus-visible` conversion) can ship with the styling batches.
- **Priority:** **P1** for Calendar `role="application"` and Dialog focus trap (real user harm); **P2** for forced-colors/reduced-motion (Windows HC minority).

---

## Priority-Ordered Implementation Batches

Format: `[Component × Framework] Short description → fix approach → effort (S/M/L)`.
Within each batch, order is: class-name renames first → wire existing infrastructure → write new CSS.

### Batch 1 — P0 Blockers (ship-stoppers)

**1a. Class-name cascade fixes (unblock all downstream work)**

1. `[Calendar × BS5]` Razor emits `__cell--*`, CSS targets `__day--*` → rename CSS selectors OR add button-wrapped inner element; emit missing `--in-range`, `--range-start`, `--range-end` modifiers → **M**
2. `[Calendar × Fluent]` BEM double-underscore mismatch against single-hyphen CSS → rewrite CSS block to BEM (option b per audit §0) → **M**
3. `[Dialog × BS5]` Razor emits `sf-dialog-title/body/actions/close`, no BS styling for any — also needs `.modal` wrapper and full structural map → split overlay/modal root; introduce `DialogContentClass/HeaderClass/TitleClass/BodyClass/FooterClass/CloseClass` on the provider interface; update all three provider implementations; rewrite `SunfishDialog.razor` to consume them → **L**
4. `[Dialog × Fluent]` Size modifiers target `.sf-dialog__panel` (never rendered); draggable targets `.sf-dialog-header` (never rendered) → wrap content in `.sf-dialog__panel`, or rewrite selectors; rename draggable target to `.sf-dialog-title` → **S**
5. `[DataGrid × BS5]` Inner `<table>` has no `.table` class → `DataGridRowClass` uses `.table-active` but parent isn't a `.table` → add `DataGridTableClass()` provider method; emit `"table table-hover align-middle mb-0"`; delete bespoke `.sf-bs-datagrid-row--striped` → **S**
6. `[DataGrid × BS5]` `.mar-datagrid-cmd-btn`, `.mar-datagrid-column-menu-trigger`, `.sf-datagrid-loading-spinner` have no CSS → route through `CssProvider.DataGridCommandButtonClass(kind)` → emit `.btn .btn-sm .btn-outline-*`; add loading spinner → `.spinner-border` → **M**
7. `[DataGrid × Fluent]` `.mar-datagrid-col--locked-end` has no CSS rule → add `box-shadow: 1px 0 0 colorNeutralStroke2, 6px 0 6px -4px rgba(0,0,0,.16)` with RTL variant → **S**
8. `[Button × BS5]` `.btn-icon` and `.sf-bs-icon-button` emitted, neither defined → add CSS block using `--bs-btn-padding-*` overrides + `aspect-ratio: 1/1` → **S**

**1b. Fill empty SCSS partials**

9. `[Dialog × Material]` `_dialog.scss` is a TODO stub → write end-to-end: surface on `surface-container-high`, `corner-extra-large` (28px), elevation-3, `headline-small` title, `body-medium` body, `label-large` actions, 24dp padding, 8dp action-gap; compose `.sf-dialog-overlay` with `.sf-scrim`; add enter/exit motion using `duration-medium2` + `easing-emphasized-decelerate` → **L**
10. `[Calendar × Material]` `_date-picker.scss` is a TODO stub → write end-to-end: `surface-container-high`, `corner-extra-large`, circular day cells (`radius-full`), `primary`/`on-primary` for selected, 1px primary ring for today, state-layer overlays on cells/nav/title, 3px outer stroke focus, typography (`body-large`/`label-medium`/`title-small`), year/decade/century grids → **L**

**1c. Wire existing infrastructure — Material state layers**

11. `[DataGrid × Material]` rows/cells/headers use flat `background: var(--sf-color-surface-container-high)` on hover instead of `.sf-state-layer` overlay → add `::after` overlay with `currentColor` + `--sf-state-hover-opacity` on rows, cells, headers, filter button, cmd button → **M**
12. `[Button × Material]` focus state stacks focus-ring + state-layer overlay → drop `::before { opacity: 0.12 }` on `:focus-visible`, keep focus ring only → **S**
13. `[DataGrid × Material]` undefined `--sf-font-weight-semibold` referenced 12+ times → define in `:root` OR switch consumers to `--sf-font-weight-medium` (500) → **S**

**1d. Focus-ring + dark-mode for Calendar-BS5 (a11y)**

14. `[Calendar × BS5]` no focus rings anywhere (WCAG 2.4.7 fail) → add `:focus-visible` box-shadow using `--bs-focus-ring-*` on day, nav, title, month/year/decade/century cells → **S**
15. `[Calendar × BS5]` `role="application"` on outer wrapper (wrong — suppresses screen-reader navigation) → drop `role="application"`, move `tabindex` to roving inner button → **S**
16. `[Calendar × BS5]` hardcoded hex defeats `data-bs-theme=dark` → replace `#0d6efd`/`#212529`/`#e9ecef`/`#fff` with `--bs-primary`/`--bs-body-color`/etc. → **S**

**1e. Fluent v9 token-shim + core-appearance fixes**

17. `[DataGrid × Fluent]` ship `fluent-v9-tokens.css` emitting `--colorNeutralBackground1/2/3`, `--colorStrokeFocus2`, `--colorBrandBackground2`, `--colorSubtleBackground*`, `--colorCompoundBrandStroke`, `--shadow16`, `--spacingHorizontal*`, `--strokeWidth*`, `--borderRadius*`, typescale `--fontSizeBase*`/`--lineHeightBase*`/`--fontWeight*` (full ladder); both light + dark blocks → **L**
18. `[DataGrid × Fluent]` after token shim: header bg → `colorNeutralBackground2`; row hover → `colorSubtleBackgroundHover`; selected-row → `colorBrandBackground2`; focus-ring → `colorStrokeFocus2` dual-stroke → **M**
19. `[DataGrid × Fluent]` density realignment: add `DataGridSize.ExtraSmall`; `small=34px`, `medium=44px` (Fluent defaults) → breaking API change, needs ADR note → **M**
20. `[DataGrid × Fluent]` popup/column-menu/filter-menu shadows — swap ad-hoc `0 8px 24px rgba(0,0,0,.12)` for `shadow16` → **S**
21. `[Button × Fluent]` hover/pressed hardcoded hex (`#c43035`, `#e69a3e`, `#00a8d6`, `#0e6e0e`, `#a52b2f` etc.) → add `--sf-color-{danger|warning|info|success}-{hover|active}` tokens + dark-mode overrides → **M**
22. `[Button × Fluent]` focus indicator not Fluent-shaped → rewrite `.sf-button:focus-visible` with dual-stroke; add `.sf-button--primary:focus-visible` inner-white ring → **S**
23. `[Button × Fluent]` missing `subtle` and `transparent` appearances → add `ButtonAppearance` enum (parity change across all three adapters) → **M**
24. `[Button × Fluent]` no forced-colors / reduced-motion branches → append `@media (forced-colors: active)` per appearance + global `prefers-reduced-motion` rule → **S**

**1f. DataGrid × BS5 remaining P0 items**

25. `[DataGrid × BS5]` no `[data-bs-theme=dark] .sf-bs-datagrid*` overrides → add dark-mode rules for resize-handle, row--highlighted, popup overlay → **S**
26. `[DataGrid × BS5]` checkboxes not styled with `.form-check-input` → route through provider; update Rendering.cs → **S**
27. `[DataGrid × BS5]` sort indicator lacks `.visually-hidden` sibling → Razor markup addition → **S**

**1g. DataGrid × Material remaining P0 items**

28. `[DataGrid × Material]` focus-ring is 2px primary; M3 spec is 3px secondary with 2px offset → introduce `--sf-focus-ring-grid` token → **S**
29. `[DataGrid × Material]` row heights 28/36/48 below M3 spec 40/52/72 → re-map size tiers; add `.sf-datagrid-header { min-height: 56px }` → **S**
30. `[DataGrid × Material]` typescale tokens never referenced → define `--sf-typescale-body-medium-*` / `--sf-typescale-title-small-*`; apply to cells/headers → **M**

**1h. Dialog × Fluent remaining P0 items**

31. `[Dialog × Fluent]` `.sf-dialog` uses `--sf-shadow-xl`; `--elevation-shadow-dialog` is defined but unused → swap to the dialog-specific token → **S**
32. `[Dialog × Fluent]` no max-height/overflow → add `max-height: calc(100vh - 3xl*2)` with body overflow → **S**
33. `[Dialog × Fluent]` close button has no CSS rule → add 32×32 subtle-icon-button block with hover/focus → **S**

**1i. Calendar × Fluent remaining P0 items**

34. `[Calendar × Fluent]` day cells square instead of circular → `border-radius: 9999px`, inline-flex hit area → **S**
35. `[Calendar × Fluent]` `--focused` modifier has no CSS → add focus-ring → **S**
36. `[Calendar × Fluent]` today ring missing → `box-shadow: inset 0 0 0 1px colorCompoundBrandStroke` → **S**

**Total P0 count: ~50 items across these nine groups.**

---

### Batch 2 — P1 High

**2a. Dark-mode + token wiring**

- `[Button × BS5]` add `Light`/`Dark` to `ButtonVariant` enum (parity change across all three adapters) → **M**
- `[Button × BS5]` `FillMode.Flat` + `FillMode.Clear` non-standard → add `.sf-btn-flat` / `.sf-btn-clear` classes overriding `--bs-btn-*` vars per variant → **M**
- `[Button × BS5]` both `disabled` attribute and `.disabled` class emitted → drop `.disabled` when element is `<button>` → **S**
- `[Button × BS5]` `SunfishToggleButton` missing `aria-pressed` → Razor change + add `.active` alongside variant swap → **S**
- `[Button × BS5]` icon span has no CSS → add `.btn:has(.sf-button__icon) { display: inline-flex; gap: .5rem }` → **S**
- `[Button × BS5]` no `Loading`/spinner slot → contract in `ui-core` + parity implementation in all three providers → **M**

**2b. DataGrid × BS5 P1 batch**

- Loading overlay → `.spinner-border` + `.visually-hidden` text → **S**
- Empty state → `.alert .alert-secondary` wrapper → **S**
- Focus ring: swap `:focus` → `:focus-visible`, `outline` → `box-shadow` → **S**
- Hover: remove `.sf-bs-datagrid-row:hover`; rely on `.table-hover` (depends on Batch 1 P0-1) → **S**
- Pager: swap bespoke pager buttons → `.pagination > .page-item > .page-link` (implementation in `SunfishPagination.razor`) → **M**
- Filter input + search input lack `.form-control` / `.form-control-sm` → route via provider → **S**
- Popup edit dialog bypasses `.modal` → requires Batch 1 Dialog-BS5 work first → **M**

**2c. DataGrid × Fluent P1 batch**

- Sort indicator: swap Unicode `▲`/`▼` for `ArrowSort` SVG with `colorNeutralForeground2` → **S**
- Sort-indicator color not brand (Fluent doesn't brand-color sort) → **S**
- Striped rows → `colorNeutralBackground1Hover` (not surface) → **S**
- `.mar-datagrid-row--highlighted` hardcodes yellow + `!important`; `--sf-color-warning-rgb` undefined → route tokens + remove `!important` → **S**
- Filter-button active-indicator dot uses `color-mix` → switch to `colorCompoundBrandBackground` → **S**
- Checkbox column add `.sf-datagrid-selection-cell--subtle` with opacity transitions → **S**
- Loading overlay → `colorBackgroundOverlay` + compose `SunfishSpinner` → **S**

**2d. DataGrid × Material P1 batch**

- Dark-mode overrides for `.sf-datagrid-loading-overlay` / `.sf-datagrid-popup-overlay`; replace `rgba(0,0,0,.3)` with `--sf-color-scrim` → **S**
- Filter-popup + popup-dialog use literal shadows → `--sf-elevation-2`/`-3` → **S**
- Loading overlay has no M3 indicator → add top-edge `.sf-datagrid-loading-bar` indeterminate animation → **M**
- Selected row doesn't layer with hover/focus (Batch 1 dep) + switch to `secondary-container` → **S**
- Empty state → M3 pattern with icon + headline → **M**
- Filter-menu icon-button → 40×40 circular state-layer, `radius-full` → **S**

**2e. Dialog × BS5 sizing/feature parity**

- Add `Size`, `Centered`, `Scrollable`, `Fullscreen` parameters → provider emits `modal-sm/lg/xl/fullscreen` → **M**
- Transition: defer `.show` toggle + `transitionend` for fade → **M**
- Scroll-lock JS interop on `<body>` → **M**
- Focus trap + Esc handler → **M**
- `aria-labelledby` + move `role="dialog"` to modal root → **S**

**2f. Dialog × Fluent polish**

- Exit animation (`@keyframes sf-dialog-exit`) → **S**
- Backdrop dark theme too dark (0.6 → 0.4) → **S**
- Focus ring on close + action buttons → **S**
- Title line-height explicit 1.4 → **S**
- Body/grid separation with flex column → **S**

**2g. Dialog × Material P1**

- Apply `surface-container-high`, `radius-xl`, `elevation-3`, typography, spacing → covered by Batch 1 partial fill → already counted
- Action-button state layers + focus ring → **S**
- Breakpoint full-screen variant → **S**

**2h. Calendar × all three P1**

- `[Calendar × BS5]` nav buttons use `.btn .btn-sm .btn-outline-secondary` → **S**
- `[Calendar × BS5]` title as `.btn-link` → **S**
- `[Calendar × BS5]` grid drops `<table>` semantics with `display:flex` → re-align to `<table>` composition → **S**
- `[Calendar × BS5]` split `--in-range` from `--range-edge` (Razor + CSS) → **S**
- `[Calendar × Fluent]` year/decade/century grids entirely unstyled → **M**
- `[Calendar × Fluent]` popover surface elevation missing → `shadow16` + `radius-md` + `colorNeutralBackground1` → **S**
- `[Calendar × Fluent]` no dark-theme overrides → **S**
- `[Calendar × Fluent]` range-edge start/end distinction (Razor) + rounded-edge CSS → **S**
- `[Calendar × Fluent]` day-of-week typography: drop semibold → regular + `fontSizeBase200` + `colorNeutralForeground3` → **S**
- `[Calendar × Fluent]` nav chevron `tabindex="-1"` makes unreachable → **S**
- `[Calendar × Material]` range in-range interior needs `primary-container` fill (distinct from edge) → **S**
- `[Calendar × Material]` nav chevron buttons unstyled → 40×40 icon-button pattern → **S**
- `[Calendar × Material]` day-of-week header typography not set → `label-medium` in `on-surface-variant` → **S**
- `[Calendar × Material]` year/decade/century views completely unstyled → **M**
- `[Calendar × Material]` focus-ring modifier has no CSS → **S**
- `[Calendar × Material]` disabled/other-month modifiers have no CSS → **S**

**2i. Button × Fluent P1 batch**

- Icon slot CSS (`.sf-button__icon`) → `display: inline-flex; 20/20/24px` → **S**
- Icon-only collapse detection on SunfishButton → **S**
- `DisabledFocusable` API (new parameter in `ui-core`) → **M**
- `min-width: 96px` (64px small) → **S**
- Size-coupled font-weight (small=regular, m/l=semibold) → **S**
- Transition timing tokens (`--sf-duration-faster: 100ms`, `--sf-curve-easy-ease`) → **S**
- `iconPosition` before/after API → **M**

**2j. Button × Material P1 batch**

- Remove filled+tonal hover elevation (M3: only elevated gets elevation) → **S**
- Text button padding 12dp (not 8dp) → **S**
- Shape parameter is dead code → either wire per-variant classes or strip → **S**
- `color-mix` fallback for disabled → **S**
- Icon leading-edge padding 16dp (asymmetric with trailing 24dp) → **S**

**Total P1 count: ~68 items.**

---

### Batch 3 — P2 Medium

High-level grouping (detail preserved in individual audits):

- **DataGrid P2 cluster:** striping dedup (delete after P0-1); table-layout conditional; group header/footer idioms; detail-row accent; resize-handle dark mode; column-menu trigger styling; scrollbar overlap; selection-neutral mode; toolbar/row border hierarchy (`colorNeutralStroke1` vs `2`); search-input focus underline; high-contrast (forced-colors) support
- **Button P2 cluster:** block/full-width API (`FullWidth`); `btn-check` pattern for segmented controls; `focus-ring` utility hook; vertical button-group + toolbar; split-button dropdown-toggle; outline taxonomy rename; link-variant color defaults; `text-overflow: ellipsis` on button root; icon-filled/regular swap on hover (subtle/transparent)
- **Dialog P2 cluster:** actions top-spacing 16 vs 24 px; alert variant styling; reduced-motion branch; icon slot above headline; close-button semantics for full-screen vs basic; max-height/scrollable body; surface-tint application; min/max-width constraints; action-row stacked layout
- **Calendar P2 cluster:** today indicator vs focus ring collision; out-of-month double-dimming; disabled uses `opacity:.35` vs `--bs-btn-disabled-opacity:.65`; week-number column missing CSS; month/year/decade grid CSS; datepicker popup → `.dropdown-menu` tokens; reduced-motion handling; typography polish; hover-overlay ordering; nav-button tab fix; view-transition motion

**Total P2 count: ~65 items.** Treat as polish batch; most are single-line edits scoped to one skin.

---

### Batch 4 — P3 Polish

- Bootstrap Icons swap-in across all components (sort arrows, drag handles, detail toggles, nav chevrons) — deferred until Sunfish declares a peer-dep icon policy
- Mozilla box-shadow Firefox workaround (`@supports (-moz-appearance: button)`)
- Per-button `data-bs-theme` scoping
- Cursor-on-hover-only (Fluent) vs always-on (Sunfish)
- FAB component family (Material) — new feature, should open a new ICM intake
- M3 surface-tint adoption (tokens exist, unused)
- Density variants for Dialog/Calendar
- RTL support sweep (Calendar chevrons, DataGrid frozen columns, Dialog close-button position)
- High-contrast forced-colors for Calendar/Dialog
- Validation summary split (Sunfish class vs `.alert .alert-danger`)
- Vertical orientation CSS for Calendar
- Z-index consolidation (drawers above dialogs issue)

**Total P3 count: ~34 items.** Defer all. Revisit after P0/P1/P2 ship.

---

## Recommended Execution Order with Fan-Out Opportunities

### Phase 1 (parallelizable, fan-out 3 agents)

All three blocks below have zero cross-dependencies and can run simultaneously:

**Packet 1A — Class-name audit agent (1 agent, covers all 4 components × 3 frameworks)**
- Batch 1a items 1–8: rename Razor or CSS to agree across Calendar-BS5, Calendar-Fluent, Dialog-BS5, Dialog-Fluent, DataGrid-BS5, DataGrid-Fluent, Button-BS5
- Output: one PR per component+framework pair; clears dead-CSS blockers

**Packet 1B — Material-partial-fill agent (1 agent)**
- Batch 1b items 9–10: fill Dialog-Material + Calendar-Material SCSS partials end-to-end
- Output: two PRs; brings Material skin to baseline

**Packet 1C — Fluent-token-shim agent (1 agent)**
- Batch 1e item 17 + 21: ship `fluent-v9-tokens.css` and retrofit Button-Fluent hardcoded-hex fix
- Output: foundation work that unblocks all downstream Fluent component fixes

### Phase 2 (sequential after Phase 1, fan-out 3 agents)

Must wait for Phase 1 class renames to land:

**Packet 2A — BS5 DataGrid agent**
- Remaining Batch 1f items (11 + 25–27) + Batch 2b (DataGrid-BS5 P1 cluster)
- Depends on Phase 1A's DataGrid-BS5 class renames

**Packet 2B — Fluent DataGrid/Button/Dialog/Calendar agent**
- Batch 1e items 18–24 + 31–36 + Batch 2c, 2f, 2i, 2h (Calendar-Fluent)
- Depends on Phase 1C token shim

**Packet 2C — Material DataGrid/Button agent**
- Batch 1c items 11–13 + Batch 1g items 28–30 + Batch 2d + 2j
- Depends on nothing external (state-layer tokens already exist)

### Phase 3 (a11y + feature parity, single-agent sequential)

- `[Calendar × BS5]` `role="application"` fix + roving tabindex (Batch 1d item 15)
- `[Dialog × all]` focus trap + Esc + scroll lock (JS interop, crosses all three providers)
- `[Calendar × Fluent]` nav chevron `tabindex="-1"` fix
- `[Button × Fluent]` `DisabledFocusable` API + forced-colors
- `[Button × BS5]` `aria-pressed` on toggle

### Phase 4 (P1/P2 polish — can fan out per-framework again)

Standard batches 2e, 2g, 3. Low-risk because Phase 1–3 stabilized the foundation.

### Decision points before starting

- The `ButtonAppearance` enum + `DialogContentClass` provider-interface changes are API-breaking. Need ADR + React-adapter parity work before landing. Recommend Phase 1 ships CSS-only fixes; API changes queue for the next minor.

---

## Cross-Cutting Decisions Needed From Human (ctwoodwa@gmail.com)

1. **Prefix/naming policy decision (task #48 already pending).** Three styles coexist today:
   - `sf-*` (Sunfish-generic, component-agnostic — e.g., `sf-dialog-title`)
   - `mar-*` (used in `mar-datagrid-*` for features like resize, cell-selected — likely Telerik "MAR" prefix legacy)
   - `k-*` (implied Telerik/Kendo legacy via `compat-telerik`, not in active skins)
   - BEM double-underscore vs single-hyphen inconsistency is a sub-decision (recommend BEM double-underscore project-wide — already majority usage)
   - **Recommendation:** write an ADR making `sf-{component}-{element}` + BEM `__modifier` canonical; deprecate `mar-*` as Sunfish extension prefix reclaimed under `sf-*`.

2. **Bootstrap 5 CSS distribution policy.** Current Sunfish skin re-implements BS5 primitives (buttons, forms, modals) in-place rather than depending on upstream BS5 via npm/CDN. Need a call: bundle compiled BS5 CSS (as today), or declare a peer-dep and document that consumers bring their own BS5 + our `sunfish-bootstrap.css` only adds overrides. Affects Dialog-BS5 refactor scope.

3. **Icon-library peer dependency.** Many P3 findings are "swap Unicode glyph for framework icon" blocked on declaring Bootstrap Icons / Fluent Icons / Material Symbols as peer deps. Decision: opinionated peer-dep with icon-name provider mapping, or stay Unicode-only. Recommend opinionated — the glyphs read as broken.

4. **Fluent UI v9 density enum rename.** Current `DataGridSize = Small/Medium/Large` maps to Fluent `ExtraSmall/Small/Medium`. Either add `ExtraSmall` to the enum and re-tier, or document Sunfish values as "approximations" and live with the mismatch. Breaking API change; needs ADR.

5. **`ButtonVariant` enum expansion.** Add `Light`/`Dark` (BS5 coverage) and `Subtle`/`Transparent` (Fluent coverage). Or separate `ButtonAppearance` from `ButtonIntent`. Breaking API change; needs ADR + parity work.

6. **State-layer opacity — M3 spec is 8/10/12, Sunfish tokens are 8/12/12.** One audit flagged focus opacity 2 points higher than M3 spec. Fix at token layer (small deviation) or document intentional.

7. **Scrim alpha in dark mode.** M3 keeps scrim at 32% in both themes; Sunfish bumps to 52% (`_colors.scss:271`) — and Fluent backdrop at 60% (dark) vs spec 40%. One-shot fix: drop both dark overrides to spec.

8. **Drag-and-drop Dialog.** `Draggable` parameter exists; only the Material Fluent partial has any selector support, and selectors are stale. Either wire up JS drag behavior (tiny interop) or document `Draggable` as Material/Fluent-only.

---

## OpenWolf buglog candidates

For `.wolf/buglog.json` — each is a class-of-bug worth recording so future sessions catch recurrences:

1. **Razor class emission doesn't match CSS selectors**
   - Symptom: component renders with user-agent defaults despite CSS bundle loaded
   - Root cause: Razor uses BEM double-underscore (`sf-calendar__cell--selected`) while CSS authored with single-hyphen (`sf-calendar-day--selected`), or vice versa
   - Fix location: reconcile in Razor or CSS; add parity test that scans emitted classes vs CSS selector set
   - Tags: `razor`, `css`, `bem`, `naming-convention`, `parity`

2. **Material dialog unstyled in production**
   - Symptom: dialog renders as unstyled browser `<div>` with no surface, elevation, typography, or padding
   - Root cause: `Providers/Material/Styles/components/_dialog.scss` is a 5-line TODO comment
   - Fix location: author full M3 dialog partial; add build-time check for TODO-only partials
   - Tags: `material`, `dialog`, `scss-stub`, `unstyled-component`

3. **Material calendar unstyled in production**
   - Symptom: calendar renders with browser `<table>` defaults under Material skin
   - Root cause: `_date-picker.scss` is a TODO stub
   - Fix location: author full M3 calendar + year/decade/century grid partial
   - Tags: `material`, `calendar`, `scss-stub`

4. **Bootstrap dialog class-name mismatch (structural)**
   - Symptom: BS5 dialog chrome is missing — no padding, no border, no radius — even though BS5 modal CSS ships in the compiled skin
   - Root cause: `BootstrapCssProvider.DialogClass` returns `modal-dialog` but Razor children use `sf-dialog-title/body/actions/close` — no `.modal-content/header/body/footer` wrappers
   - Fix location: extend `ISunfishCssProvider` with per-slot methods; update all three providers; rewrite Razor to consume
   - Tags: `bootstrap`, `dialog`, `structural-gap`, `provider-contract`

5. **Hardcoded hex breaks dark mode**
   - Symptom: dark-mode toggle doesn't repaint the component (or paints wrong color like white-on-white)
   - Root cause: component CSS contains literal `#0d6efd`/`#323130`/`#ffffff` etc. that bypass the theme's `--bs-*`/`--sf-*` token system
   - Fix location: replace literals with `var(--bs-primary)`/`var(--sf-color-on-background)`; add dark-mode regression screenshot diff
   - Tags: `dark-mode`, `tokens`, `hardcoded-color`, `regression`

6. **Fluent v8 hex drift**
   - Symptom: Fluent skin looks "Fabric-flavored" instead of Fluent v9 — wrong surface colors, selection too saturated, header too bold
   - Root cause: `--sf-color-primary: #0078d4` is Fluent v8 (Fabric), Fluent v9 uses `colorBrandBackground: #0f6cbd`; no token shim maps `--sf-*` to Fluent v9 semantic tokens
   - Fix location: ship `fluent-v9-tokens.css`; reference `--colorNeutralBackground1/2/3`, `--colorStrokeFocus2`, `--colorBrandBackground2` etc. in component CSS
   - Tags: `fluent`, `tokens`, `v8-v9-migration`

7. **Unicode glyph used instead of framework icon**
   - Symptom: sort arrows, nav chevrons, close button, drag handles render as aliased Unicode chars that look off-rhythm against the rest of the chrome
   - Root cause: Sunfish built without an icon-library peer dep; fallbacks use `▲`/`▼`/`&times;`/`&lsaquo;`/`⠇`/`⋮`
   - Fix location: introduce `SunfishIcon` with provider-scoped icon-name table; keep Unicode fallback for consumers who opt out of icon peer dep
   - Tags: `icons`, `glyphs`, `polish`, `peer-deps`

8. **State-layer utility not consumed**
   - Symptom: Material components have flat color swaps on hover/focus/pressed instead of layered overlays; focus feels identical to hover
   - Root cause: `.sf-state-layer` helper and `--sf-state-*-opacity` tokens exist but component CSS never wires `::after`/`::before` overlay
   - Fix location: add `currentColor` overlay with opacity-by-state; verify on all interactive Material surfaces
   - Tags: `material`, `state-layers`, `interaction`, `infrastructure-unused`

9. **Focus-ring uses `:focus` instead of `:focus-visible`**
   - Symptom: focus ring appears on mouse click, not just keyboard focus
   - Root cause: old CSS convention; `:focus-visible` is the spec-compliant selector
   - Fix location: global sweep `grep -nE ':focus[^-]'` → replace with `:focus-visible`
   - Tags: `a11y`, `focus`, `css-modernization`

10. **`role="application"` on composite widget**
    - Symptom: screen-reader users cannot arrow-navigate the calendar grid
    - Root cause: outer wrapper has `role="application"`, which suppresses virtual-cursor nav; a calendar is a `grid` composite widget per WAI-ARIA APG
    - Fix location: drop `role="application"` + `tabindex="0"` from shell; implement roving tabindex on the focused cell's button
    - Tags: `a11y`, `aria`, `composite-widgets`, `calendar`

11. **Dialog lacks focus trap / Esc close / scroll lock**
    - Symptom: Tab escapes to underlying page content; Esc doesn't close; page scrolls behind modal
    - Root cause: no JS interop for focus management; `@if Visible` toggles subtree without lifecycle
    - Fix location: tiny JS interop for focus trap + scroll lock; `@onkeydown` for Esc
    - Tags: `a11y`, `dialog`, `focus-trap`, `interop`

12. **Undefined CSS variable referenced 12+ times**
    - Symptom: `font-weight: var(--sf-font-weight-semibold)` silently falls back to `normal` (400) everywhere
    - Root cause: `--sf-font-weight-semibold` never defined in `:root`; only `normal/medium/bold` exist
    - Fix location: define `--sf-font-weight-semibold: 600` OR migrate all consumers to `--sf-font-weight-medium` (500, per M3 title-small)
    - Tags: `css-vars`, `undefined-token`, `silent-failure`

---

## Appendix: Issue Counts

| Component | Framework | P0 | P1 | P2 | P3 | Total |
|-----------|-----------|----|----|----|----|-------|
| DataGrid  | BS5       | 5  | 8  | 7  | 5  | 25    |
| DataGrid  | Fluent v9 | 8  | 8  | 7  | 5  | 28    |
| DataGrid  | M3        | 4  | 6  | 5  | 3  | 18    |
| Button    | BS5       | 1  | 7  | 5  | 4  | 17    |
| Button    | Fluent v9 | 4  | 7  | 6  | 3  | 20    |
| Button    | M3        | 4  | 4  | 5  | 3  | 16    |
| Dialog    | BS5       | 2  | 4  | 4  | 2  | 12    |
| Dialog    | Fluent v9 | 4  | 5  | 5  | 2  | 16    |
| Dialog    | M3        | 5  | 9  | 8  | 5  | 27    |
| Calendar  | BS5       | 4  | 5  | 7  | 4  | 20    |
| Calendar  | Fluent v9 | 6  | 7  | 6  | 3  | 22    |
| Calendar  | M3        | 5  | 11 | 6  | 5  | 27    |
| **Totals**|           | **52** | **81** | **71** | **44** | **248** |

**Grand total:** 248 issues across 4 components × 3 frameworks.

> Counts approximate: some audits nested sub-items under a single ID; I aggregated to the enumerated headings per audit. P0 total tracks with the executive summary's "~50 P0"; P1/P2/P3 totals are slightly higher after consolidating cross-component references.

---

*End of synthesis. Input corpus: ~35,000 words across 12 audits. This document: ~4,500 words.*
