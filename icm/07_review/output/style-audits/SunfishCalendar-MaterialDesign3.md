# Styling-Completeness Audit — SunfishCalendar vs Material Design 3

**Component:** `SunfishCalendar` (Blazor)
**Skin under audit:** Material 3 (`Providers/Material`)
**Reference:** m3.material.io/components/date-pickers/overview, material-web `MdDatepickerFoundation`
**Date:** 2026-04-22
**Auditor:** automated styling-completeness review

---

## 1. Executive Summary

The Sunfish Material provider ships **no Material 3 styles** for `SunfishCalendar`. The only `sf-calendar*` rules in the repo live in the Bootstrap and FluentUI providers; the Material provider's `components/_date-picker.scss` is a five-line placeholder with a `TODO: Placeholder — no Material 3 styles defined yet`. The SunfishCalendar markup emits a rich class surface (`sf-calendar`, `sf-calendar__header`, `sf-calendar__nav-btn`, `sf-calendar__title`, `sf-calendar__grid`, `sf-calendar__day-header`, `sf-calendar__weeknum[-header]`, `sf-calendar__cell` with eight modifier states, `sf-calendar__day`, `sf-calendar__year-grid/month-cell`, `sf-calendar__decade-grid/year-cell`, `sf-calendar__century-grid/decade-cell`, and the `sf-calendar--horizontal/--vertical` orientation flags), but when the Material skin is active every one of those classes is unstyled and falls back to user-agent defaults.

Material 3 defines four date-picker flavors (docked, modal, modal-input, plus the range variant of each). **SunfishCalendar maps to the inline/docked surface only** — there is no wrapping dialog or text-field overlay component in Sunfish's Blazor adapter. The modal and modal-input variants are an architectural gap, not a CSS gap, and are out of scope for the calendar-surface itself but belong in a paired `SunfishDatePicker` / `SunfishDateRangePicker` audit.

The foundation tokens required to implement M3 parity already exist in `sunfish-material.css`: all color roles (`--sf-color-surface-container-high`, `primary`, `on-primary`, `primary-container`, `on-surface-variant`, `outline`), the M3 elevation ladder, the full motion/easing set, typography and shape scales, dark-mode overrides, and the shared `.sf-state-layer` mechanism with `--sf-state-hover/focus/pressed-opacity` at 8/12/12. **This is a pure CSS-authorship task, not a token or architecture task.** The single largest risk is that the `.sf-state-layer` helper is applied as a class, not baked into `[class^=sf-]` button/cell pseudo-elements, so every interactive calendar element must either opt in to the helper or re-implement the overlay pattern locally.

---

## 2. Prioritized Gap List

Priority key: **P0** = blocks shipping the Material skin for any surface that embeds SunfishCalendar; **P1** = parity break vs M3 spec visible to any designer review; **P2** = polish/motion/density; **P3** = nice-to-have or advanced variant.

### P0 — Blockers

| # | Gap | Evidence |
|---|---|---|
| P0-1 | **No Material skin rules for `sf-calendar` exist.** Every class emitted by `SunfishCalendar.razor` renders unstyled under Material. | `Providers/Material/Styles/components/_date-picker.scss` is a 5-line TODO; `Grep sf-calendar` in Material wwwroot returns zero hits. |
| P0-2 | No container role, no corner radius, no elevation. Docked calendar must sit on `surface-container-high` with `corner-extra-large` (28 px) and elevation-0; modal calendar would be elevation-3, but modal is scope-excluded. | `--sf-color-surface-container-high` and `--sf-radius-xl: 28px` already exist but are not applied. |
| P0-3 | Day cells have no shape. M3 mandates `corner-full` (circle) cells. SunfishCalendar renders a `<td>` with no inner shaped button. | `--sf-radius-full: 9999px` exists but is not bound to `.sf-calendar__cell` or `.sf-calendar__day`. |
| P0-4 | Selected-day color role is missing. M3 requires `primary` fill + `on-primary` label. No rule applies `background: var(--sf-color-primary)` to `.sf-calendar__cell--selected`. | Bootstrap has the right idea (`$primary !important`), FluentUI likewise; Material has nothing. |
| P0-5 | Today indicator is missing. M3 spec: 1 px ring in `primary` role with no fill when the day is not selected. | `.sf-calendar__cell--today` has no Material-skin rule. |

### P1 — Spec parity

| # | Gap | Evidence / Fix |
|---|---|---|
| P1-1 | **No state-layer overlays on day cells.** M3 requires 8/10/12 % overlays in role color for hover/focus/pressed. Sunfish has the `.sf-state-layer` utility but `SunfishCalendar.razor` does not apply it to cells; the markup also lacks a `::before` target inside `.sf-calendar__cell` because cells render as `<td>` without a shaped inner button. | Either (a) wrap the cell contents in a `<button class="sf-calendar__day sf-state-layer">` and scope the `::before` to the circular child, or (b) add a Material-scoped rule mirroring `.sf-state-layer` directly on `.sf-calendar__cell:not(.sf-calendar__cell--disabled)`. See § 3 for required overlay table. |
| P1-2 | Range fills missing. M3: range start/end = `primary` fill, in-range = `primary-container`. Only `.sf-calendar__cell--selected` and `.sf-calendar__cell--range-edge` classes exist; there is no `--in-range` class emitted between the edges. | Two fixes: (i) emit `sf-calendar__cell--in-range` from the razor for dates strictly between `Start` and `End`; (ii) style edges with `primary`, interior with `primary-container`. The `IsSelected` Range branch currently returns true for every date in range — acceptable provided the `--range-edge` modifier distinguishes endpoints, which it does, but the interior still needs its own softer fill. |
| P1-3 | Day-of-week header typography not set. M3: `label-medium` (11 px, 500 weight) in `on-surface-variant`. `.sf-calendar__day-header` has no rule. | Bind to `--sf-font-size-xs` + `--sf-font-weight-medium` + `--sf-color-on-surface-variant`. |
| P1-4 | Month/year title (header) typography not set. M3: `title-small` / `label-large` in `on-surface`. `.sf-calendar__title` has no rule, and the default button styling leaks. | Bind to `title-small` role; remove native button chrome. |
| P1-5 | Nav chevron buttons unstyled. M3: 40 × 40 icon button in `on-surface-variant`; uses icon-button state layers. | Should compose the `sf-icon-button` pattern or mirror its styles. Current markup uses `&lsaquo;`/`&rsaquo;` text glyphs, not proper icons — see P3-2. |
| P1-6 | Focus indicator missing. M3: 3 px outer stroke in `outline` role with 2 px offset. The global `--sf-focus-ring: 0 0 0 2px bg, 0 0 0 4px primary` exists but is not applied to `.sf-calendar__cell--focused`, and the razor emits the modifier but no rule consumes it. | Apply `outline: 3px solid var(--sf-color-outline)` with `outline-offset: 2px` on `:focus-visible` and on the `--focused` modifier (the latter is driven by keyboard nav and may fire without DOM focus). |
| P1-7 | Disabled cell lacks M3 opacity and interaction lock. M3: 38 % on-surface for text, 12 % container. The `--sf-disabled-opacity: 0.38` and `--sf-disabled-container-opacity: 0.12` tokens exist but are not consumed. | Bind to `.sf-calendar__cell--disabled`. Razor already sets `aria-disabled` and suppresses click — CSS just needs to visualize it. |
| P1-8 | Other-month days have no de-emphasis. M3: render at 38 % on-surface-variant or omit. Razor emits `.sf-calendar__cell--other-month` but no rule exists. | Either apply 0.38 opacity or map color to `on-surface-variant`. |
| P1-9 | Year-view grid (`.sf-calendar__year-grid` + `.sf-calendar__month-cell`), decade-view (`.sf-calendar__decade-grid` + `.sf-calendar__year-cell`), century-view (`.sf-calendar__century-grid` + `.sf-calendar__decade-cell`) are entirely unstyled. M3 reference treats the year chooser as a scrollable list of pill-shaped buttons; Sunfish exposes deeper drill-down (decade/century) that M3 does not specify but must still visually cohere with the month view. | At minimum, style as a 3- or 4-column grid of `corner-full` buttons in `body-large`, with the same selected/current/state-layer semantics as month cells. `--current` and `--selected` modifiers already exist on each tier. |
| P1-10 | Orientation variant `.sf-calendar--vertical` is unstyled. Horizontal is the default; vertical is used by `SunfishDateRangePicker` for the two-panel stacked layout. | Define vertical flex direction and panel spacing. Acceptable to punt to a range-picker audit, but at least stub the selector so consumers do not render broken. |
| P1-11 | Week-number column (`.sf-calendar__weeknum-header`, `.sf-calendar__weeknum`) is unstyled. M3 does not define a week-number column (ISO-style weeks are not in the M3 spec); Sunfish adds it for Telerik parity. | Style as a 32 × 32 muted cell in `on-surface-variant` at `label-small`. Flag as an intentional Sunfish-over-M3 extension in the style comment. |

### P2 — Polish, motion, density

| # | Gap | Evidence / Fix |
|---|---|---|
| P2-1 | No view-transition motion. M3: header & grid cross-fade on month/year/decade switch at `duration-medium2` (300 ms) with `easing-emphasized-decelerate`. Tokens exist: `--sf-motion-duration-medium2: 300ms`, `--sf-motion-easing-emphasized-decel: cubic-bezier(0.05, 0.7, 0.1, 1.0)`. | Requires a wrapping `<div key="@View">` or a CSS-only approach via `@view-transition` / staged opacity. |
| P2-2 | Arrow-nav button press/ripple not wired. The `.sf-ripple` helper exists but the razor emits no ripple element. | Apply ripple or state-layer pattern on nav buttons. |
| P2-3 | Density parity. Sunfish has a `patterns/density` partial; the calendar has no density scaling (cell size is unset, so it inherits user-agent). M3 comfortable = 40 px day cells, compact = 32 px. | Use `--sf-calendar-day-size` custom property (FluentUI already uses this at 32 px default) and swap under `[data-sf-density]`. |
| P2-4 | Range-edge corner shape. M3: range start = `corner-full` on leading half + flat trailing; range end mirrored; in-range cells flat. Current classes cannot express this without extra razor logic (start vs end distinguished only by equality to `Start` or `End`). | Add `sf-calendar__cell--range-start` / `--range-end` (split what `--range-edge` currently folds together) and style with asymmetric radii. |
| P2-5 | Hover on disabled cell must be suppressed. `.sf-state-layer` base rules would light up a disabled day without `:not(.sf-calendar__cell--disabled)`. | Include the not() selector when the state-layer rules are authored. |
| P2-6 | Reduced-motion already zeros transitions globally (`prefers-reduced-motion: reduce` block at line 204). Verify any per-calendar transitions we add go through the shared tokens so they collapse to 0 ms. | Authorship guideline only; no CSS yet to audit. |

### P3 — Advanced / out-of-scope-but-worth-noting

| # | Gap | Notes |
|---|---|---|
| P3-1 | Modal and modal-input date-picker flavors. M3 defines them; Sunfish has `SunfishDatePicker.razor` but that is a field-with-popup input, not an M3 modal. Full modal parity is an architectural expansion, not a calendar-surface style gap. Track separately. |
| P3-2 | Nav glyphs. `&lsaquo;` / `&rsaquo;` characters render inconsistently across fonts. M3 uses Material Symbols chevron-left/right. Swap to `SunfishIcon` with `chevron_left` / `chevron_right` at the same time the button styling lands. |
| P3-3 | `HeaderTemplate` override path. When the user supplies a custom header, the Material skin cannot enforce typography/color — consumer-owned. Document that custom headers must inherit the container’s role colors. |
| P3-4 | Right-to-left (RTL) support. M3 requires mirrored chevrons and reversed weekday order. The razor respects culture FirstDayOfWeek but the skin has no `[dir=rtl]` mirror rule. Track with the global RTL audit rather than here. |
| P3-5 | High-contrast forced-colors mode. Windows high-contrast would collapse all role colors; M3 expects system-color fallbacks. Track with the a11y audit. |

---

## 3. State-Layer Audit — Day Cells

M3 state-layer opacities apply on top of the cell's current base color. "Base role" is the fill/ink before state layers; the overlay color is the ink color the state layer tints with (usually the label/ink role, matching M3 tokens).

| Cell state | Base container | Base label | Overlay ink (state layer) | Hover (8 %) | Focus (10 %) | Pressed (12 %) | Sunfish status |
|---|---|---|---|---|---|---|---|
| **Default (enabled, in month, not selected, not today)** | transparent | `on-surface` | `on-surface` | `on-surface @ 8%` | `on-surface @ 10%` | `on-surface @ 12%` | P0 — unimplemented |
| **Today (not selected)** | transparent + 1 px ring `primary` | `primary` | `primary` | `primary @ 8%` | `primary @ 10%` | `primary @ 12%` | P0 — unimplemented |
| **Selected (single)** | `primary` | `on-primary` | `on-primary` | `on-primary @ 8%` | `on-primary @ 10%` | `on-primary @ 12%` | P0 — unimplemented |
| **Range start / end** | `primary` | `on-primary` | `on-primary` | `on-primary @ 8%` | `on-primary @ 10%` | `on-primary @ 12%` | P0 — class emitted (`--range-edge`), no style |
| **In-range (interior)** | `primary-container` | `on-primary-container` | `on-primary-container` | `on-primary-container @ 8%` | `on-primary-container @ 10%` | `on-primary-container @ 12%` | P0/P1-2 — no class emitted yet |
| **Other-month day (if shown)** | transparent | `on-surface-variant` @ 38% | `on-surface` | same rules, softer | same | same | P1-8 — unimplemented |
| **Disabled** | transparent | `on-surface` @ 38% | — (no overlay) | suppress | suppress | suppress | P1-7 — unimplemented |
| **Keyboard-focused** (razor modifier `--focused`) | inherits current | inherits current | inherits current | — | 3 px `outline` stroke, 2 px offset | — | P1-6 — unimplemented |

**Implementation note:** The shared `.sf-state-layer` `::before` already encodes the 8/12/12 opacities (`--sf-state-hover/focus/pressed-opacity` in `sunfish-material.css` lines 235–263). M3's canonical hover/focus/pressed for cells is 8/10/12; Sunfish's existing tokens are 8/12/12 — the focus value is 2 pp higher than M3 spec. This is global, not calendar-specific, and any deviation should be resolved at the token layer in a separate audit — flagging here for visibility.

---

## 4. Focus-Area Coverage Table

Status legend: **none** = no Material-skin rule; **partial** = class emitted but key properties unset; **ok** = matches M3; **N/A** = not applicable to this component.

| Area | M3 requirement | Sunfish class(es) | Material-skin rule? | Status | Priority |
|---|---|---|---|---|---|
| **Container** | `surface-container-high`, `corner-extra-large` (28 px), padding per density, elevation-0 (docked) | `.sf-calendar`, `.sf-calendar--horizontal`, `.sf-calendar--vertical` | no | none | P0 |
| **Header (title + chevrons row)** | Flex row, on-surface inks, spacing per density | `.sf-calendar__header` | no | none | P0 |
| **Month/year title button** | `title-small` in `on-surface`, transparent background, clickable to switch view, state-layer 8/10/12 | `.sf-calendar__title` | no | none | P1 |
| **Nav chevron buttons** | 40 × 40 icon-button in `on-surface-variant` with role-color state layers; proper Material Symbols glyphs | `.sf-calendar__nav-btn` | no | none | P1 |
| **Day-of-week header row** | `label-medium` in `on-surface-variant`, centered; no weekday column for week-numbers cell | `.sf-calendar__day-header`, `.sf-calendar__weeknum-header` | no | none | P1 |
| **Day cells — geometry** | `corner-full` (circle), square bounding box at density-dependent size (≈40 px comfortable) | `.sf-calendar__cell`, `.sf-calendar__day` | no | none | P0 |
| **Day cells — base color** | `on-surface` ink, transparent container | `.sf-calendar__cell` | no | none | P0 |
| **Day cells — selected** | `primary` fill, `on-primary` ink | `.sf-calendar__cell--selected` | no | none | P0 |
| **Day cells — today** | `primary` ring 1 px, no fill when unselected | `.sf-calendar__cell--today` | no | none | P0 |
| **Day cells — range edges** | `primary` fill | `.sf-calendar__cell--range-edge` | no | none | P1 |
| **Day cells — in-range interior** | `primary-container` fill | — (not emitted) | no | none | P1 |
| **Day cells — disabled** | 38 % on-surface ink, no state layer, no pointer events | `.sf-calendar__cell--disabled` | no | none | P1 |
| **Day cells — other-month** | 38 % on-surface-variant ink (or hidden) | `.sf-calendar__cell--other-month` | no | none | P1 |
| **Day cells — state layers** | 8/10/12 overlays in role-appropriate ink | — (needs `sf-state-layer` wiring) | no | none | P1 |
| **Week-number column** | Not in M3; Sunfish extension — render in `on-surface-variant` at `label-small` | `.sf-calendar__weeknum[-header]` | no | none | P1 |
| **Empty-slot cells** | no box, no hover | `.sf-calendar__cell--empty` | no | none | P2 |
| **Year view — month grid** | Pill buttons, `body-large`, `corner-full`, selected = `primary` + `on-primary`, current = outline ring | `.sf-calendar__year-grid`, `.sf-calendar__month-cell[--selected\|--current]` | no | none | P1 |
| **Decade view — year grid** | Pill buttons, mirroring month grid semantics | `.sf-calendar__decade-grid`, `.sf-calendar__year-cell[--selected\|--current]` | no | none | P1 |
| **Century view — decade grid** | Pill buttons; M3 does not define, treat as extension | `.sf-calendar__century-grid`, `.sf-calendar__decade-cell[--current]` | no | none | P2 |
| **Typography — day numbers** | `body-large` (16 px / 24 lh / 0.5 ls) | `.sf-calendar__day` | no | none | P1 |
| **Typography — day-of-week** | `label-medium` (11 px / 500) | `.sf-calendar__day-header` | no | none | P1 |
| **Typography — title** | `title-small` (14 px / 500 / 20 lh) | `.sf-calendar__title` | no | none | P1 |
| **Typography — nav label** | `label-large` (icon; 40 × 40 hit target) | `.sf-calendar__nav-btn` | no | none | P1 |
| **Shape — container** | 28 px (`corner-extra-large`) | `.sf-calendar` | no | none | P0 |
| **Shape — day cells** | 100 % (`corner-full`) | `.sf-calendar__cell`, `.sf-calendar__day` | no | none | P0 |
| **Shape — year/month buttons** | 100 % (`corner-full`) | `.sf-calendar__month-cell`, `.sf-calendar__year-cell`, `.sf-calendar__decade-cell` | no | none | P1 |
| **Motion — view transition** | `duration-medium2` 300 ms + `easing-emphasized-decelerate` | view switch between Month/Year/Decade/Century | no | none | P2 |
| **Motion — state-layer fade** | `duration-short` easing-linear (≈150 ms) | `.sf-state-layer::before` transition is `var(--sf-transition-fast)` globally | global only | partial | P2 |
| **Motion — reduced-motion** | Respect `prefers-reduced-motion` | global block at line 204 zeros transitions | yes | ok | — |
| **Focus indicator** | 3 px outer stroke in `outline`, 2 px offset, visible on `:focus-visible` and on razor `--focused` modifier | `.sf-calendar__cell--focused`, `.sf-calendar:focus` | no | none | P1 |
| **Dark mode** | Standard M3 role remap; all roles re-bind under `[data-sf-theme=dark]` | tokens remap at line 59 of `sunfish-material.css` | global tokens exist | **ok for tokens**, but no component rules consume them yet | P0 once component CSS lands |
| **Density** | Comfortable vs compact day size and padding | `patterns/density` partial exists; no calendar hooks | no | none | P2 |
| **Orientation** | Horizontal (default) vs vertical (range picker) | `.sf-calendar--horizontal`, `.sf-calendar--vertical` | no | none | P1 |
| **Elevation — docked** | elevation-0 (flat, no shadow) | container | no | none (defaults to no shadow, so coincidentally correct) | — |
| **Elevation — modal** | elevation-3 (24 dp ≈ `--sf-shadow-lg`) | N/A to calendar; belongs to dialog wrapper | N/A | N/A | P3 |

---

## 5. Recommended Authorship Sequence

1. Replace the `components/_date-picker.scss` placeholder with a full calendar + picker partial (one file is fine; keep the existing filename so `_index.scss` already picks it up).
2. Implement P0 container + cell shape + today + selected (unblocks kitchen-sink screenshots).
3. Add P1 state layers, typography, focus indicator, other-month and disabled modifiers, day-of-week header.
4. Emit `sf-calendar__cell--in-range` / `--range-start` / `--range-end` from `SunfishCalendar.razor` and pair with P1-2 / P2-4 CSS. (Small razor change; call out in review.)
5. Style Year/Decade/Century grids (P1-9).
6. Layer in P2 motion and density.
7. Document Sunfish-specific extensions (week-numbers column, Decade/Century views) in an inline comment referencing this audit.

---

## 6. Sources

- m3.material.io/components/date-pickers/overview — color roles, typography scale, shape tokens, elevation, state-layer percentages (8/10/12), focus-indicator stroke width and offset, docked vs modal variants.
- material-web `MdDatepickerFoundation` — canonical class surface and cell-state enumeration for web implementations.
- `packages/ui-adapters-blazor/Components/DataDisplay/SunfishCalendar.razor` — emitted class list.
- `packages/ui-adapters-blazor/Components/DataDisplay/SunfishCalendar.razor.cs` — modifier semantics, keyboard map, range-selection logic.
- `packages/ui-adapters-blazor/Providers/Material/wwwroot/css/sunfish-material.css` — available foundation tokens.
- `packages/ui-adapters-blazor/Providers/Material/Styles/components/_date-picker.scss` — confirmed placeholder.
- `packages/ui-adapters-blazor/Providers/Bootstrap/Styles/components/_date-picker.scss` and `Providers/FluentUI/Styles/components/_date-picker.scss` — cross-skin reference for the class surface consumers expect.

---

*End of audit.*
