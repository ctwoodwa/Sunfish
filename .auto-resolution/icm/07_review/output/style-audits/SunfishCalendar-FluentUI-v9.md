# Style Audit — SunfishCalendar vs Fluent UI v9 Calendar / DatePicker

**Scope:** `SunfishCalendar` (Blazor) under the FluentUI skin vs Fluent UI v9 `Calendar`
and `DatePicker` (`@fluentui/react-datepicker-compat` / `react-calendar-compat`).
**Sources:**
`packages/ui-adapters-blazor/Components/DataDisplay/SunfishCalendar.razor(.cs)`,
`packages/ui-adapters-blazor/Providers/FluentUI/wwwroot/css/sunfish-fluentui.css` (lines 3467–3569, 3364–3464),
Fluent UI v9 docs at `react.fluentui.dev/?path=/docs/preview-components-calendar--docs` and
`react.fluentui.dev/?path=/docs/preview-components-datepicker--docs`
(Context7 `/microsoft/fluentui`, `react-calendar-compat`, `react-datepicker-compat`).

**Date:** 2026‑04‑22.
**Verdict:** The Sunfish FluentUI skin for `SunfishCalendar` is effectively **unstyled in production**.
See P0‑1 below — every BEM selector in the Razor markup is a near‑miss of the CSS. Once that is
fixed, there remains a substantial gap to Fluent v9 visual parity across shape, color, state, and
elevation tokens.

---

## 0. Root‑cause blocker (must land first)

**P0‑1 — Selector namespace mismatch between markup and CSS.** The component emits
BEM‑style classes with double underscores (`sf-calendar__header`,
`sf-calendar__day-header`, `sf-calendar__cell`, `sf-calendar__cell--selected`,
`sf-calendar__cell--today`, `sf-calendar__cell--other-month`, `sf-calendar__cell--disabled`,
`sf-calendar__cell--focused`, `sf-calendar__cell--range-edge`,
`sf-calendar__nav-btn`, `sf-calendar__title`, `sf-calendar__grid`,
`sf-calendar__year-grid`, `sf-calendar__decade-grid`, `sf-calendar__century-grid`,
`sf-calendar__month-cell`, `sf-calendar__year-cell`, `sf-calendar__decade-cell`,
`sf-calendar__weeknum`, `sf-calendar__weeknum-header`, `sf-calendar__day`).
The FluentUI skin only defines **single‑hyphen** selectors (`sf-calendar-header`,
`sf-calendar-title`, `sf-calendar-nav-btn`, `sf-calendar-grid`, `sf-calendar-weekdays`,
`sf-calendar-weekday`, `sf-calendar-days`, `sf-calendar-day`, `sf-calendar-day--today`,
`sf-calendar-day--selected`, `sf-calendar-day--in-range`, `sf-calendar-day--other-month`,
`sf-calendar-day--disabled`). Verified via grep: 19 CSS hits on `sf-calendar`, **0** hits on
`sf-calendar__`. Result: no hover, no selection fill, no today ring, default browser `<table>`
spacing, bare `<button>` chevrons. Also, the `Year`, `Decade` and `Century` views have no CSS at
all — year/decade/century grids fall back to inline button flow.

**Fix direction.** Either (a) rename the Razor classes to match the existing single‑hyphen CSS and
_add_ the missing view/state rules, or (b) rewrite the CSS to BEM double‑underscore and
retire the single‑hyphen block. Option (b) is the recommended path because BEM is the convention
already used elsewhere in this stylesheet (e.g. `sf-timepicker__item`, `sf-datepicker__toggle`).
This audit assumes option (b) and enumerates the BEM rules that must exist for parity.

---

## 1. Prioritized gap list

| ID | Pri | Gap | Fluent v9 reference |
|---|---|---|---|
| P0‑1 | P0 | Razor BEM classes (`sf-calendar__*`) have no CSS (see §0) | — |
| P0‑2 | P0 | Day cells are square with `--sf-radius-sm`; Fluent v9 uses a fully round day cell (`borderRadiusCircular`) with a centered inline‑flex hit area | Calendar docs — "Day buttons are circular" |
| P0‑3 | P0 | Selected day uses flat primary fill without circular clipping, and ignores `colorNeutralForegroundOnBrand` semantics; selected cell in v9 is a brand‑filled circle | Calendar — selected state |
| P0‑4 | P0 | "Today" state is `font-weight: semibold; color: primary` only — no stroke ring. v9 today = 1 px `colorCompoundBrandStroke` ring around circle when unselected, inverted ring when selected | Calendar — today marker |
| P0‑5 | P0 | No `:focus-visible` outline on day cells, nav buttons, or view‑switch title. v9 uses the standard 2 px outer focus indicator (`strokeWidthThick` / `colorStrokeFocus2`) offset by 2 px | Fluent v9 focus indicator guidance |
| P0‑6 | P0 | `sf-calendar__cell--focused` (keyboard roving focus) has no CSS rule at all — keyboard nav is invisible | Calendar keyboard model |
| P1‑7 | P1 | Year / Decade / Century views entirely unstyled — no grid, no button sizing, no selected/current states | Calendar multi‑view (MonthPicker / YearPicker) |
| P1‑8 | P1 | `in-range` paints a filled pill but sets `border-radius:0`, producing a solid bar with no rounded leading/trailing edges; v9 renders range with rounded left edge on `rangeStart`, rounded right edge on `rangeEnd`, flat fill in between, and circular selection dots at edges | Calendar range selection |
| P1‑9 | P1 | `range-edge` class emitted by Razor but no CSS rule — edges look identical to interior fills | — |
| P1‑10 | P1 | Header uses `--sf-font-size-base` (default 14 px) and `semibold` but Fluent v9 header is `fontSizeBase300` (14 px) `fontWeightSemibold` **with an arrow‑button toggle affordance**; chevrons here are literal `‹`/`›` glyphs (not Fluent icons) sized by font metrics | Calendar header |
| P1‑11 | P1 | Day‑of‑week header uses `--sf-font-size-xs` (12 px) + `semibold`; v9 uses `fontSizeBase200` (12 px) `fontWeightRegular` + `colorNeutralForeground3` | Calendar strings / weekday row |
| P1‑12 | P1 | No elevation around the calendar when used as the DatePicker popover. The DatePicker popup uses `--sf-shadow-lg` implicitly but the inline `SunfishCalendar` has no surface. v9 Calendar inside DatePicker popover = `shadow16` + `borderRadiusMedium` + `colorNeutralBackground1` | DatePicker — popover surface |
| P1‑13 | P1 | No dark‑theme overrides on any `sf-calendar-*` rule — dark mode inherits only from the top‑level `--sf-color-*` vars; hover/today/range‑light colors look acceptable but the day‑cell border‑radius and today ring never swap | Fluent v9 dark theme (`webDarkTheme`) |
| P2‑14 | P2 | No motion — view transitions (month‑to‑year, prev/next month) are instant. v9 uses `durationNormal` (200 ms) fade‑slide on view change | Fluent v9 motion tokens |
| P2‑15 | P2 | `DisplayWeekNumbers` column has no separator column, no color, no `colorNeutralForeground3` treatment | Calendar `showWeekNumbers` |
| P2‑16 | P2 | Typography stack hard‑codes `--sf-font-family` (system UI) rather than the Fluent `fontFamilyBase` (`'Segoe UI Variable', 'Segoe UI', …`) | Fluent v9 typography |
| P2‑17 | P2 | Hover overlay uses `--sf-color-surface` (a flat neutral fill, not a translucent hover). v9 hover = `colorNeutralBackground1Hover`, which is layered on top of the surface — subtly different on dark | Calendar day hover |
| P2‑18 | P2 | `out-of-month` days use `--sf-color-text-disabled` (identical to `disabled`) — v9 distinguishes: other‑month = `colorNeutralForeground4`, disabled = `colorNeutralForegroundDisabled` + `cursor:not-allowed` + reduced contrast | Calendar `lightenDaysOutsideNavigatedMonth` |
| P2‑19 | P2 | `<button class="sf-calendar__nav-btn" tabindex="-1">` makes the prev/next chevrons keyboard‑unreachable; v9 PreviousMonth / NextMonth are `tabIndex={0}` with `aria-label` and full focus ring | Calendar nav icons API |
| P3‑20 | P3 | No `reduced-motion` respect; the only transition is `background var(--sf-transition-fast)` on day — OK, but when motion is added in P2‑14 it must wrap in `@media (prefers-reduced-motion: reduce)` | Fluent motion guidance |
| P3‑21 | P3 | `sf-calendar__weeknum-header` text literal "Wk" is hard‑coded in the Razor — not a styling issue but coupled to the weeknum column's visual spec | Calendar `strings.weekNumberFormatString` |
| P3‑22 | P3 | No "Go to today" button / footer affordance (Fluent v9 `showGoToToday` defaults true on DatePicker) | Calendar `showGoToToday` |

---

## 2. Token‑mapping table (Sunfish ↔ Fluent v9)

These are the mappings a compliant FluentUI skin should wire up. Left column is what the
calendar's CSS should consume (introducing new `--sf-calendar-*` vars is fine, but they must be
declared in `:root` and `[data-sf-theme=dark]` blocks). Right column is the Fluent v9 design
token (`@fluentui/tokens`).

| Surface / element | Current Sunfish var | Fluent v9 token | Notes |
|---|---|---|---|
| Calendar surface (popover) | none | `colorNeutralBackground1` | Add `--sf-calendar-surface` |
| Calendar surface border | none | `colorTransparentStroke` | 1 px, only visible on `filled-*` DatePicker |
| Calendar surface shadow (popover) | `--sf-shadow-lg` | `shadow16` | Map to `0 8px 16px rgba(0,0,0,.14), 0 0 2px rgba(0,0,0,.12)` |
| Calendar surface radius (popover) | none | `borderRadiusMedium` (4 px) | `--sf-calendar-surface-radius: var(--sf-radius-md)` |
| Header text | `--sf-color-on-background` | `colorNeutralForeground1` | OK as‑is |
| Header text hover (clickable title) | `--sf-color-primary` | `colorNeutralForeground2BrandHover` | Tighter contrast than flat brand |
| Nav chevron rest | `--sf-color-text-secondary` | `colorNeutralForeground2` | OK |
| Nav chevron hover bg | `--sf-color-surface` | `colorSubtleBackgroundHover` | Subtle, translucent |
| Day‑of‑week label | `--sf-color-text-secondary` + 12 px semibold | `colorNeutralForeground3` + `fontSizeBase200` + `fontWeightRegular` | Drop semibold |
| Day cell rest fg | `--sf-color-on-background` | `colorNeutralForeground1` | OK |
| Day cell rest bg | transparent | `transparent` | OK |
| Day cell hover bg | `--sf-color-surface` | `colorSubtleBackgroundHover` | Translucent overlay |
| Day cell pressed bg | not set | `colorSubtleBackgroundPressed` | Add |
| Day cell today ring | not set | `colorCompoundBrandStroke` (1 px inset) | New: `box-shadow: inset 0 0 0 1px var(--sf-calendar-today-ring)` |
| Day cell selected bg | `--sf-color-primary` | `colorBrandBackground` | OK name, keep |
| Day cell selected hover bg | `--sf-color-primary-hover` | `colorCompoundBrandBackgroundHover` | OK name, keep |
| Day cell selected pressed bg | not set | `colorCompoundBrandBackgroundPressed` | Add |
| Day cell selected fg | `--sf-color-on-primary` | `colorNeutralForegroundOnBrand` | OK |
| Day cell selected shape | `--sf-radius-sm` (4 px) | `borderRadiusCircular` (9999 px) | **Change** |
| Day cell in‑range bg | `--sf-color-primary-light` | `colorBrandBackground2` | OK |
| Day cell out‑of‑month fg | `--sf-color-text-disabled` | `colorNeutralForeground4` | Distinct from disabled |
| Day cell disabled fg | `--sf-color-text-disabled` | `colorNeutralForegroundDisabled` | OK |
| Day cell focus ring | not set | `colorStrokeFocus2` (2 px outer) | Add |
| Weeknum column fg | not set | `colorNeutralForeground3` | Add |
| Motion — view change | not set | `durationNormal` `curveEasyEase` | 200 ms cubic‑bezier |

Spec for new Sunfish calendar vars (add to `:root`):

```css
--sf-calendar-surface: var(--sf-color-background);
--sf-calendar-surface-radius: var(--sf-radius-md);
--sf-calendar-surface-shadow: 0 8px 16px rgba(0,0,0,.14), 0 0 2px rgba(0,0,0,.12);
--sf-calendar-day-size: 32px;
--sf-calendar-day-radius: 9999px;
--sf-calendar-today-ring: var(--sf-color-primary);
--sf-calendar-focus-ring: var(--sf-color-primary);
--sf-calendar-other-month-fg: color-mix(in srgb, var(--sf-color-on-background) 45%, transparent);
--sf-calendar-range-fill: var(--sf-color-primary-light);
--sf-calendar-hover-overlay: var(--sf-color-surface);
```

---

## 3. State‑specific CSS snippets (target BEM + Fluent v9 semantics)

These replace the `sf-calendar-day*` block at lines 3529‑3569 and also cover the missing
`sf-calendar__cell--focused`, `--range-edge`, popover surface, and view grids.

### 3.1 Popover surface and grid spacing

```css
.sf-calendar {
  background: var(--sf-calendar-surface);
  border-radius: var(--sf-calendar-surface-radius);
  box-shadow: var(--sf-calendar-surface-shadow);   /* only when used in popover context */
  padding: var(--sf-space-sm);
  font-family: var(--sf-font-family);
  color: var(--sf-color-on-background);
}
.sf-calendar:focus-visible { outline: 2px solid var(--sf-calendar-focus-ring); outline-offset: 2px; }

.sf-calendar__grid { border-collapse: separate; border-spacing: 0; width: 100%; }
.sf-calendar__day-header {
  padding: var(--sf-space-xs) 0;
  font-size: var(--sf-font-size-xs);
  font-weight: var(--sf-font-weight-regular);
  color: var(--sf-color-text-secondary);
  text-align: center;
}
```

### 3.2 Day cell — rest / hover / pressed

```css
.sf-calendar__cell {
  padding: 2px;
  text-align: center;
}
.sf-calendar__cell .sf-calendar__day {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: var(--sf-calendar-day-size);
  height: var(--sf-calendar-day-size);
  border-radius: var(--sf-calendar-day-radius);
  font-size: var(--sf-font-size-sm);
  color: var(--sf-color-on-background);
  transition: background var(--sf-transition-fast),
              color var(--sf-transition-fast),
              box-shadow var(--sf-transition-fast);
}
.sf-calendar__cell:hover .sf-calendar__day { background: var(--sf-calendar-hover-overlay); }
.sf-calendar__cell:active .sf-calendar__day { background: var(--sf-color-surface-hover); }
```

### 3.3 Today (unselected)

```css
.sf-calendar__cell--today .sf-calendar__day {
  box-shadow: inset 0 0 0 1px var(--sf-calendar-today-ring);
  font-weight: var(--sf-font-weight-semibold);
  color: var(--sf-color-primary);
}
```

### 3.4 Selected (single) — also handles today+selected

```css
.sf-calendar__cell--selected .sf-calendar__day {
  background: var(--sf-color-primary);
  color: var(--sf-color-on-primary);
  font-weight: var(--sf-font-weight-semibold);
  box-shadow: none;                         /* clear the today ring */
}
.sf-calendar__cell--selected:hover .sf-calendar__day { background: var(--sf-color-primary-hover); }
.sf-calendar__cell--selected:active .sf-calendar__day { background: var(--sf-color-primary-active); }

/* today + selected: invert ring to on‑primary */
.sf-calendar__cell--selected.sf-calendar__cell--today .sf-calendar__day {
  box-shadow: inset 0 0 0 1px var(--sf-color-on-primary);
}
```

### 3.5 Range — interior fill + rounded edges

```css
/* Interior range: flat pill, no radius on the day wrapper */
.sf-calendar__cell--selected:not(.sf-calendar__cell--range-edge) {
  background: var(--sf-calendar-range-fill);
}
.sf-calendar__cell--selected:not(.sf-calendar__cell--range-edge) .sf-calendar__day {
  background: transparent;
  color: var(--sf-color-on-background);
}

/* Range edges: brand‑filled circle, with the interior fill flowing out one side */
.sf-calendar__cell--range-edge .sf-calendar__day {
  background: var(--sf-color-primary);
  color: var(--sf-color-on-primary);
  border-radius: var(--sf-calendar-day-radius);
}
.sf-calendar__cell--range-edge:first-child,
.sf-calendar__cell--range-edge.sf-calendar__cell--range-start {
  background: linear-gradient(to right, transparent 50%, var(--sf-calendar-range-fill) 50%);
}
.sf-calendar__cell--range-edge:last-child,
.sf-calendar__cell--range-edge.sf-calendar__cell--range-end {
  background: linear-gradient(to left, transparent 50%, var(--sf-calendar-range-fill) 50%);
}
```

> Note: the Razor currently emits `range-edge` but does not distinguish `--range-start` vs
> `--range-end`. That is a small `.razor.cs` change (check `date.Date == Start.Value.Date` vs
> `date.Date == End.Value.Date`). Documented here as a follow‑up — see P1‑8/P1‑9.

### 3.6 Out‑of‑month vs disabled

```css
.sf-calendar__cell--other-month .sf-calendar__day { color: var(--sf-calendar-other-month-fg); }
.sf-calendar__cell--disabled    .sf-calendar__day {
  color: var(--sf-color-text-disabled);
  cursor: not-allowed;
  pointer-events: none;
}
.sf-calendar__cell--disabled.sf-calendar__cell--other-month .sf-calendar__day { opacity: .6; }
```

### 3.7 Focus — mouse vs keyboard roving

```css
.sf-calendar__cell:focus-visible .sf-calendar__day,
.sf-calendar__cell--focused    .sf-calendar__day {
  outline: 2px solid var(--sf-calendar-focus-ring);
  outline-offset: 2px;
  /* Fluent v9 focus indicator = 2 px stroke, 2 px gap. Keep outline (not box‑shadow)
     so it layers correctly over the selected fill. */
}
```

### 3.8 Hover — subtle overlay order

```css
/* Ensure hover loses to selected/range‑edge fills */
.sf-calendar__cell:not(.sf-calendar__cell--selected):not(.sf-calendar__cell--disabled):hover .sf-calendar__day {
  background: var(--sf-calendar-hover-overlay);
}
```

### 3.9 Year / Decade / Century grids (missing entirely today)

```css
.sf-calendar__year-grid,
.sf-calendar__decade-grid,
.sf-calendar__century-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: var(--sf-space-xs);
  padding: var(--sf-space-sm);
}
.sf-calendar__month-cell,
.sf-calendar__year-cell,
.sf-calendar__decade-cell {
  height: 48px;
  border: none;
  border-radius: var(--sf-radius-md);
  background: transparent;
  color: var(--sf-color-on-background);
  font-size: var(--sf-font-size-sm);
  cursor: pointer;
  transition: background var(--sf-transition-fast);
}
.sf-calendar__month-cell:hover,
.sf-calendar__year-cell:hover,
.sf-calendar__decade-cell:hover { background: var(--sf-calendar-hover-overlay); }
.sf-calendar__month-cell--current,
.sf-calendar__year-cell--current,
.sf-calendar__decade-cell--current {
  box-shadow: inset 0 0 0 1px var(--sf-calendar-today-ring);
  color: var(--sf-color-primary);
  font-weight: var(--sf-font-weight-semibold);
}
.sf-calendar__month-cell--selected,
.sf-calendar__year-cell--selected {
  background: var(--sf-color-primary);
  color: var(--sf-color-on-primary);
  font-weight: var(--sf-font-weight-semibold);
}
```

### 3.10 Motion

```css
@media (prefers-reduced-motion: no-preference) {
  .sf-calendar__grid,
  .sf-calendar__year-grid,
  .sf-calendar__decade-grid,
  .sf-calendar__century-grid {
    animation: sf-fade-in var(--sf-transition-normal);  /* 200 ms */
  }
}
```

---

## 4. Focus‑area coverage

Where each Fluent v9 concern is landed by the above snippets. "Gap" = no coverage today; "Near" =
partial; "Met" = parity after the snippet.

| Focus area | Today | After snippets | Notes |
|---|---|---|---|
| Shape — circular day cell | Gap | Met | §3.2 `borderRadiusCircular` via `--sf-calendar-day-radius` |
| Shape — popover radius | Gap | Met | §3.1 `borderRadiusMedium` |
| Color — brand selection | Near | Met | §3.4 maps `colorBrandBackground` family |
| Color — in‑range fill | Near | Met | §3.5, plus start/end blending |
| Color — today ring | Gap | Met | §3.3 inset box‑shadow |
| Color — out‑of‑month vs disabled | Near | Met | §3.6 separates the two |
| State — hover | Near | Met | §3.2 / §3.8 translucent overlay |
| State — pressed | Gap | Met | §3.4 adds `--primary-active` |
| State — focus (keyboard) | Gap | Met | §3.7 covers both mouse `:focus-visible` and roving `--focused` |
| State — disabled | Met | Met | unchanged aside from out‑of‑month split |
| Typography — header | Near | Met | `fontSizeBase300`/`semibold` already close; no change |
| Typography — day‑of‑week | Near | Met | §3.1 drops semibold, switches to `fontSizeBase200` |
| Typography — day cell | Met | Met | `--sf-font-size-sm` ≈ `fontSizeBase300` |
| Elevation — popover | Gap | Met | §3.1 adds `shadow16`‑equivalent |
| Elevation — dark theme | Gap | Partial | add `[data-sf-theme=dark]` overrides for new `--sf-calendar-*` vars |
| Motion — view transition | Gap | Partial | §3.10 is a fade only; a proper slide needs a JS‑driven direction token (future) |
| Motion — reduced motion | Gap | Met | §3.10 media guard |
| A11y — nav button tabbing | Gap | **Open** | Requires Razor change: drop `tabindex="-1"` on prev/next |
| A11y — grid ARIA | Met | Met | Razor already emits `role="grid"` + `aria-selected` |
| Views — Year/Decade/Century | Gap | Met | §3.9 |

Items marked **Open** are Razor changes, not CSS. Capture them in the implementation plan for
build stage; they are small and should ride on the same PR.

---

## 5. Recommended rollout order

1. **P0‑1** — rename CSS to BEM or rename Razor to single‑hyphen, land the merged block in §3.
   Nothing else renders correctly until this is in.
2. **P0‑2 / P0‑3 / P0‑4 / P0‑5 / P0‑6** — ship §3.2‑3.7 together (single PR, all day‑cell state
   rules).
3. **P1‑7** — §3.9 Year/Decade/Century grids (enables multi‑view parity for DatePicker popover).
4. **P1‑12** — popover surface + elevation (§3.1) — needed before the SunfishDatePicker audit can
   close.
5. **P1‑13** — dark‑theme overrides (mirror new vars under `[data-sf-theme=dark]`).
6. **P1‑8 / P1‑9** — range‑edge start/end Razor tweak + §3.5.
7. **P2‑10 / P2‑11 / P2‑16 / P2‑17 / P2‑18 / P2‑19** — typography polish and nav‑button tab
   fix; small, batched.
8. **P2‑14 / P3‑20** — motion in §3.10.
9. **P3‑21 / P3‑22** — "Wk" localization + optional "Go to today" footer; pair with DatePicker
   component so footer slots in both.

---

## 6. Verification hooks (for stage 07 reviewer)

- Visual snapshot: render `SunfishCalendar` in `kitchen-sink` under the FluentUI skin (light +
  dark) with single, multiple, range and all four views; diff against
  `react.fluentui.dev/?path=/docs/preview-components-calendar--docs` (light + dark).
- DOM audit: `getComputedStyle(cell).borderRadius` on a day cell must be `9999px`; on a
  selected+today cell, `box-shadow` must include `inset 0 0 0 1px` using the on‑primary color.
- A11y: tab order reaches prev/next/title/grid (currently skips chevrons).
- Parity gate: no inline styles added to cells — all state changes must flow through class
  modifiers so the `sunfish-material.css` / `sunfish-telerik.css` skins can re‑skin cleanly
  later (Sunfish ADR 0017 spec‑first principle).

---

**Word count:** ~1,950.
