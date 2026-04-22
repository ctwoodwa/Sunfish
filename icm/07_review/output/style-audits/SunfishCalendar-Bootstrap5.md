# SunfishCalendar vs Bootstrap 5.3 — Styling Completeness Audit

**Component:** `SunfishCalendar` (Blazor adapter)
**Skin under audit:** `packages/ui-adapters-blazor/Providers/Bootstrap/wwwroot/css/sunfish-bootstrap.css`
**Reference:** Bootstrap 5.3 (via Context7 `/websites/getbootstrap_5_3`). BS5 ships no first-party calendar, so the expected idiomatic treatment is derived from the table, button, form-control, input-group, dropdown, focus-ring, and `data-bs-theme` primitives that a calendar composes.
**Audit date:** 2026-04-22

---

## 1. Scope and method

Sunfish renders a `<table class="sf-calendar__grid" role="grid">` for month view plus button-driven `sf-calendar__year-grid`, `sf-calendar__decade-grid`, `sf-calendar__century-grid` fragments. Cells are `<td>` elements with `role="gridcell"` (not `<button>`), and the whole component declares `role="application"` with `tabindex="0"` on the outer shell.

The CSS under `packages/.../sunfish-bootstrap.css` lines **14556–14656** paints the calendar chrome. It is **not** composed from `.table`, `.btn`, `.focus-ring`, or `data-bs-theme` subtle-token utilities; it hand-rolls equivalents with hard-coded hex colors (`#0d6efd`, `#e9ecef`, `#212529`), which means:

- Dark-mode `[data-bs-theme=dark]` does **not** repaint the calendar.
- Theming via `--bs-primary` / `--bs-primary-bg-subtle` has **no effect**.
- Focus states are missing entirely (no outline, no `.focus-ring`, no `:focus-visible` box-shadow).
- The template's `sf-calendar__day` tokens targeted by CSS (`--today`, `--selected`, `--outside`, `--in-range`, `--range-start/end`) do **not match** the class names the Razor actually emits (`sf-calendar__cell--today`, `--selected`, `--other-month`, `--range-edge`, no `--in-range` class). **Every cell-state style is dead CSS.**

The last item is the single highest-impact finding and is called out as **P0-A** below.

---

## 2. Prioritized gap list

### P0 — Blocking correctness / dark mode / a11y

#### P0-A. Cell-state CSS does not match template class names (dead styles)

**Template emits** (lines 76–82 of `SunfishCalendar.razor`):

```csharp
var classes = "sf-calendar__cell";
if (!isCurrentMonth) classes += " sf-calendar__cell--other-month";
if (isSelected)     classes += " sf-calendar__cell--selected";
if (isRangeEdge)    classes += " sf-calendar__cell--range-edge";
if (isToday)        classes += " sf-calendar__cell--today";
if (isDisabled)     classes += " sf-calendar__cell--disabled";
if (isFocused)      classes += " sf-calendar__cell--focused";
```

**CSS targets** (lines 14610–14655):

```css
.sf-calendar__day { ... }
.sf-calendar__day--today { ... }
.sf-calendar__day--selected { ... }
.sf-calendar__day--disabled { ... }
.sf-calendar__day--outside { ... }
.sf-calendar__day--in-range { ... }
.sf-calendar__day--range-start { ... }
.sf-calendar__day--range-end { ... }
```

The CSS is addressing a hypothetical `.sf-calendar__day` button element, but the Razor emits `.sf-calendar__cell` on a `<td>`. Inside the cell is an inert `<span class="sf-calendar__day">@captured.Day</span>` with no state modifiers.

**Fix:** either rename the template classes to `sf-calendar__day--*` and move them onto a `<button>` wrapper inside each `<td>` (preferred, matches BS5 button-semantics idiom and fixes P0-B and P0-C), or rename the CSS selectors to `sf-calendar__cell--*`. Also add a missing `sf-calendar__cell--in-range` class for mid-range days (currently the template only flags `--range-edge` but both BS5 range-pickers and the existing CSS expect an `--in-range` fill between them).

**Recommended BS5-idiomatic structure:**

```razor
<td role="gridcell" aria-selected="@(isSelected ? "true" : "false")">
  <button type="button"
          class="@CellClass(date)"
          disabled="@isDisabled"
          @onclick="...">@date.Day</button>
</td>
```

```css
.sf-calendar__day {
  --bs-btn-padding-y: .25rem;
  --bs-btn-padding-x: .25rem;
  --bs-btn-border-radius: var(--bs-border-radius);
  width: 2rem; height: 2rem;
  /* inherits .btn reset */
}
```

#### P0-B. No focus ring anywhere (WCAG 2.4.7 fail)

Neither the outer `[role=application]` shell nor any cell has a `:focus`, `:focus-visible`, or `.focus-ring` style. BS5's idiom is a 4-px `rgba(var(--bs-primary-rgb), .25)` box-shadow applied on `:focus-visible`.

**BS5 snippet** (`/docs/5.3/helpers/focus-ring`):

```scss
$focus-ring-width: .25rem;
$focus-ring-opacity: .25;
$focus-ring-color: rgba($primary, $focus-ring-opacity);
--bs-focus-ring-color: rgba(var(--bs-primary-rgb), var(--bs-focus-ring-opacity));
```

**Proposed fix:**

```css
.sf-calendar__day:focus-visible,
.sf-calendar__nav-btn:focus-visible,
.sf-calendar__title:focus-visible,
.sf-calendar__month-cell:focus-visible,
.sf-calendar__year-cell:focus-visible,
.sf-calendar__decade-cell:focus-visible {
  outline: 0;
  box-shadow: 0 0 0 .25rem rgba(var(--bs-primary-rgb), .25);
  z-index: 1;
}
.sf-calendar__cell--focused .sf-calendar__day {
  box-shadow: 0 0 0 .25rem rgba(var(--bs-primary-rgb), .25);
}
```

#### P0-C. Hard-coded colors defeat `data-bs-theme=dark`

Lines 14569, 14622, 14625, 14629, 14632, 14652 use `#212529`, `#e9ecef`, `#0d6efd`, and `rgba(13,110,253,.12)` directly. Under `[data-bs-theme=dark]`, `--bs-body-color` flips to `#dee2e6`, `--bs-tertiary-bg` becomes `#2b3035`, and `--bs-primary-bg-subtle` becomes `#031633` — none of which the calendar picks up, so dark mode renders a white-text-on-white-background calendar for selected/hover states.

**Token map** (applies per cell):

| Hard-coded today | Should be |
|---|---|
| `#212529` | `var(--bs-body-color)` |
| `#e9ecef` (hover) | `var(--bs-tertiary-bg)` or `var(--bs-secondary-bg)` |
| `#0d6efd` (selected bg) | `var(--bs-primary)` |
| `#fff` (selected text) | `var(--bs-primary-text-emphasis-inverse)` — or, more robust, pair with `text-bg-primary` pattern: `color: #fff; background: var(--bs-primary)` is fine because `--bs-primary` stays saturated in dark mode |
| `rgba(13,110,253,.12)` (in-range) | `var(--bs-primary-bg-subtle)` |
| `1px solid #0d6efd` (today) | `1px solid var(--bs-primary)` or `1px solid var(--bs-primary-border-subtle)` |
| `1px solid #dee2e6` (popup) | `1px solid var(--bs-border-color)` |
| `#fff` (popup bg) | `var(--bs-body-bg)` |
| `var(--bs-secondary-color)` (weekday, outside) | **already correct** |

#### P0-D. `role="application"` on the outer wrapper is wrong

Per WAI-ARIA APG, a calendar is a `grid` composite widget. `role="application"` tells screen readers to suppress virtual-cursor navigation entirely, which actively harms accessibility for users who would otherwise arrow-navigate the grid. BS5 never recommends `role="application"` for any composite. The `<table role="grid">` inside is correct; the outer should be a `<div>` with no role, or `role="group"`, and the roving tabindex should live on the focused cell's button.

**Fix:** drop `role="application"` and `tabindex="0"` from the shell; make `_focusedDate`'s cell's inner `<button>` receive `tabindex="0"` and all others `tabindex="-1"` (roving tabindex — the canonical grid pattern).

---

### P1 — Theming / tokens / idiom

#### P1-A. Nav buttons do not use `.btn` + `.btn-sm` + `.btn-link` / `.btn-outline-secondary`

Current `.sf-calendar__nav-btn` re-implements `btn-sm` padding and a one-off hover. BS5's idiom for chevron-only navigation is `btn btn-sm btn-outline-secondary` or `btn btn-sm btn-link text-body`. This matters because the focus ring, disabled opacity, and dark-mode colors then come for free.

**BS5 snippet** (`/docs/5.3/components/buttons`):

```html
<button type="button" class="btn btn-sm btn-outline-secondary">&lsaquo;</button>
```

**Proposed fix:** either (a) add `class="btn btn-sm btn-outline-secondary"` to the three `<button>`s in the header template, or (b) mirror BS5 button tokens in `.sf-calendar__nav-btn`:

```css
.sf-calendar__nav-btn {
  --bs-btn-padding-y: .25rem;
  --bs-btn-padding-x: .5rem;
  --bs-btn-font-size: .75rem;
  --bs-btn-border-radius: var(--bs-border-radius-sm);
  --bs-btn-color: var(--bs-body-color);
  --bs-btn-hover-bg: var(--bs-tertiary-bg);
  --bs-btn-focus-shadow-rgb: var(--bs-primary-rgb);
  /* then: composes .btn */
}
```

#### P1-B. Title button has no `.btn-link` treatment

`.sf-calendar__title` is a bare `<button>` that triggers drill-up to year/decade/century view. Visually it reads as "April 2026" with no hover/focus affordance. BS5's idiom for inline text-triggers is `.btn-link` (decoration, underline-on-hover) or `.btn-sm` with `text-bg-light`.

```css
.sf-calendar__title {
  background: none;
  border: 0;
  padding: .25rem .5rem;
  border-radius: var(--bs-border-radius-sm);
  font-weight: 600;
  color: var(--bs-body-color);
}
.sf-calendar__title:hover { background: var(--bs-tertiary-bg); }
.sf-calendar__title:focus-visible {
  outline: 0;
  box-shadow: 0 0 0 .25rem rgba(var(--bs-primary-rgb), .25);
}
```

#### P1-C. `.sf-calendar__grid` drops table semantics with `display: flex`

Lines 14585–14588 set `display: flex; flex-direction: column` on the `<table>`, and the Razor still renders `<thead>`/`<tbody>`/`<tr>`/`<td>`. The CSS never styles `thead/tr/td` at all, so day cells inherit default `<td>` layout (no grid, no even columns, no gaps). BS5 tables keep native `display: table` and compose `.table` for borders/hover. There are also **two parallel naming schemes**: `sf-calendar__grid` + `sf-calendar__weekdays/weekday/days` in CSS, but the Razor uses `sf-calendar__grid` with `<th class="sf-calendar__day-header">` and `<th class="sf-calendar__weeknum-header">` — none of which the CSS addresses.

**Fix:** align to one model. Recommended: stay with `<table>` and compose `.table .table-sm .table-borderless text-center align-middle`, then scope cell styles:

```css
.sf-calendar__grid {
  --bs-table-bg: transparent;
  --bs-table-hover-bg: var(--bs-tertiary-bg);
  width: 100%;
  table-layout: fixed;
}
.sf-calendar__grid th.sf-calendar__day-header {
  font-size: .7rem;
  font-weight: 600;
  color: var(--bs-secondary-color);
  text-align: center;
  padding: .25rem 0;
  border: 0;
}
.sf-calendar__grid td.sf-calendar__cell {
  padding: 2px;
  text-align: center;
  border: 0;
}
```

#### P1-D. No `.focus-ring` utility class usage for the outer shell

If the roving-tabindex fix (P0-D) lands, the inner focusable button should get `.focus-ring` idiom rather than a hand-rolled box-shadow. The advantage: `--bs-focus-ring-color`, `--bs-focus-ring-width`, and `--bs-focus-ring-blur` become per-instance tunable.

#### P1-E. `--range-edge` but no `--in-range` mid-range fill

`IsRangeEdge` only flags Start and End. Mid-range cells get `IsSelected` true (because the template's `IsSelected` treats any date between Start and End as selected), so they pick up `.sf-calendar__day--selected` full primary fill — that is almost certainly wrong visually; BS5 range-pickers (e.g., via Flatpickr/BS tempus-dominus conventions) render mid-range as `--bs-primary-bg-subtle` + `--bs-primary-text-emphasis` and only Start/End as solid primary.

**Fix:** split `IsSelected` for Range mode into `IsSelected` (edges only) + `IsInRange` (interior), then:

```css
.sf-calendar__cell--in-range .sf-calendar__day {
  background: var(--bs-primary-bg-subtle);
  color: var(--bs-primary-text-emphasis);
  border-radius: 0;
}
.sf-calendar__cell--range-edge.sf-calendar__cell--range-start .sf-calendar__day {
  border-top-right-radius: 0; border-bottom-right-radius: 0;
}
.sf-calendar__cell--range-edge.sf-calendar__cell--range-end .sf-calendar__day {
  border-top-left-radius: 0; border-bottom-left-radius: 0;
}
```

---

### P2 — Polish / consistency

#### P2-A. Today marker uses a solid 1-px primary border

```css
.sf-calendar__day--today {
  font-weight: 700;
  border: 1px solid #0d6efd;
}
```

This collides visually with the focus ring (both end up as 1-px primary). BS5 idiom for an ambient "current" indicator is a subtle ring: `outline: 1px solid var(--bs-primary-border-subtle)` or an underline/dot. Preferred:

```css
.sf-calendar__cell--today .sf-calendar__day {
  font-weight: 700;
  box-shadow: inset 0 0 0 1px var(--bs-primary);
}
.sf-calendar__cell--today.sf-calendar__cell--selected .sf-calendar__day {
  box-shadow: none; /* selected wins */
}
```

#### P2-B. Out-of-month cells: use `text-muted` / `--bs-secondary-color`, not opacity

Current: `color: var(--bs-secondary-color); opacity: 0.5;`. Applying both compounds; the BS5 idiom is single-source-of-truth via `color: var(--bs-secondary-color)` alone (or `.text-body-tertiary` with `--bs-tertiary-color`). Drop the opacity.

#### P2-C. Disabled state uses opacity, not `--bs-btn-disabled-*` tokens

```css
.sf-calendar__day--disabled { opacity: 0.35; cursor: not-allowed; pointer-events: none; }
```

BS5 buttons use `--bs-btn-disabled-color`, `--bs-btn-disabled-bg`, `--bs-btn-disabled-border-color`, and `--bs-btn-disabled-opacity: .65` (not .35). Match to .65 and attach `disabled` attribute on the inner `<button>` so native behavior (ignored clicks, screen-reader announcement, no focus) comes free — removing the need for `pointer-events: none` and the JS guard in `OnCellClick`.

#### P2-D. Weekday header spacing is uneven because `sf-calendar__weekdays` / `sf-calendar__weekday` CSS is dead

Lines 14590–14602 style a grid-based weekday row that the Razor never emits. The Razor uses `<th class="sf-calendar__day-header">`. Either delete the dead CSS or retarget it.

#### P2-E. Month / year / decade grids have no CSS at all

`sf-calendar__year-grid`, `sf-calendar__decade-grid`, `sf-calendar__century-grid`, and the `-cell` variants (`__month-cell`, `__year-cell`, `__decade-cell`) are emitted by the Razor but have **zero** rules in the Bootstrap skin — they render as a bare column of unstyled `<button>`s. BS5 idiom for a 4×3 picker grid:

```css
.sf-calendar__year-grid,
.sf-calendar__decade-grid,
.sf-calendar__century-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: .25rem;
  padding: .25rem 0;
}
.sf-calendar__month-cell,
.sf-calendar__year-cell,
.sf-calendar__decade-cell {
  --bs-btn-padding-y: .5rem;
  --bs-btn-padding-x: .25rem;
  --bs-btn-font-size: .875rem;
  --bs-btn-color: var(--bs-body-color);
  --bs-btn-hover-bg: var(--bs-tertiary-bg);
  --bs-btn-border-radius: var(--bs-border-radius);
  background: none;
  border: 0;
  padding: var(--bs-btn-padding-y) var(--bs-btn-padding-x);
  color: var(--bs-btn-color);
  border-radius: var(--bs-btn-border-radius);
}
.sf-calendar__month-cell--selected,
.sf-calendar__year-cell--selected {
  background: var(--bs-primary);
  color: #fff;
}
.sf-calendar__month-cell--current,
.sf-calendar__year-cell--current,
.sf-calendar__decade-cell--current {
  box-shadow: inset 0 0 0 1px var(--bs-primary);
}
```

#### P2-F. Week-number column has dead `.sf-calendar__weeknum` / `__weeknum-header` rules absent

The Razor emits `<th class="sf-calendar__weeknum-header">` and `<td class="sf-calendar__weeknum">` when `DisplayWeekNumbers` is true, but the Bootstrap skin has no rules for either class. Needs a subtle column treatment:

```css
.sf-calendar__weeknum-header,
.sf-calendar__weeknum {
  color: var(--bs-secondary-color);
  font-size: .7rem;
  font-weight: 500;
  text-align: center;
  border-right: 1px solid var(--bs-border-color);
  padding-right: .25rem;
}
```

#### P2-G. Datepicker popup is a hand-rolled dropdown; should use `.dropdown-menu`

`.sf-date-range-picker__popup` and `.sf-datetime-picker__popup` (lines 14658, 14697) re-implement `.dropdown-menu` with `border: 1px solid #dee2e6; border-radius: 0.5rem; box-shadow: 0 1rem 3rem rgba(0,0,0,.175)`. BS5 exposes the same via CSS vars:

```css
/* from BS5 dropdown.css */
--bs-dropdown-bg: var(--bs-body-bg);
--bs-dropdown-border-color: var(--bs-border-color-translucent);
--bs-dropdown-border-radius: var(--bs-border-radius);
--bs-dropdown-box-shadow: var(--bs-box-shadow);
```

Composing `.dropdown-menu.show` (or stealing its vars) would inherit dark mode and user overrides automatically.

---

### P3 — Nice-to-have

#### P3-A. `.sf-calendar` shell could expose its own custom-properties

Following the BS5 pattern of per-component tokens (`--bs-btn-*`, `--bs-nav-*`):

```css
.sf-calendar {
  --sf-calendar-cell-size: 2rem;
  --sf-calendar-cell-gap: 2px;
  --sf-calendar-today-color: var(--bs-primary);
  --sf-calendar-selected-bg: var(--bs-primary);
  --sf-calendar-selected-color: #fff;
  --sf-calendar-range-bg: var(--bs-primary-bg-subtle);
  --sf-calendar-range-color: var(--bs-primary-text-emphasis);
  --sf-calendar-hover-bg: var(--bs-tertiary-bg);
}
```

Consumers can then `<SunfishCalendar Class="my-compact" />` with `.my-compact { --sf-calendar-cell-size: 1.75rem; }`.

#### P3-B. Compact modifier `sf-calendar--sm`

BS5 has `.table-sm`, `.btn-sm`, `.form-control-sm`. Add `.sf-calendar--sm` (1.75-rem cells, 0.75-rem font) for embedding in popovers.

#### P3-C. Vertical orientation has no CSS either

`OrientationClass` emits `sf-calendar--vertical` / `sf-calendar--horizontal`; neither has rules. For a multi-view flyout, vertical should stack `.sf-calendar__year-grid` + `.sf-calendar__decade-grid` side-by-side rather than 4×3.

#### P3-D. RTL not addressed

BS5 supports RTL via `dir="rtl"`. Chevrons in the header (`&lsaquo;` / `&rsaquo;`) should swap; `--range-start` / `--range-end` border-radius should swap. None of this is in the CSS.

---

## 3. Focus-area matrix

| Area | BS5 idiom | Sunfish today | Gap | Priority |
|---|---|---|---|---|
| Header chrome | `.btn-sm .btn-outline-secondary` + `.btn-link` title | Bespoke `sf-calendar__nav-btn` w/ hard-coded hex | No `.btn` composition; no focus ring; hard-coded colors | P1-A, P1-B |
| Day-of-week row | `<th>` in `.table` w/ `--bs-secondary-color` | `<th class="sf-calendar__day-header">`, **no CSS rule** | Class has no styling; spacing undefined | P2-D |
| Day cells | `<button>` w/ `.btn` tokens; roving tabindex on `<td>` parent | Inert `<span>` inside `<td>`; state classes named `__cell--*` but CSS targets `__day--*` | **Dead CSS** (P0-A), no button semantics, no disabled native attr | P0-A, P2-C |
| Selected | `background: var(--bs-primary); color: #fff` | `#0d6efd !important` + `#fff !important` | Hard-coded; `!important` blocks theming | P0-C |
| Today | Subtle primary indicator (`--bs-primary-border-subtle` inset) | `border: 1px solid #0d6efd` | Collides with focus ring; hard-coded | P0-C, P2-A |
| In-range | `--bs-primary-bg-subtle` fill + `--bs-primary-text-emphasis` text | CSS exists on wrong class name; template lacks `--in-range` flag | Renders as full `.--selected` for interior days | P0-A, P1-E |
| Range edges | Solid primary + squared inner-edge radius | `--range-edge` class set but styled as plain `--selected` | Interior and edge look identical | P1-E |
| Disabled | `--bs-btn-disabled-opacity: .65` + native `disabled` attr | `opacity: .35; pointer-events: none` | Wrong opacity; no native attr; a11y-weak | P2-C |
| Out-of-month | `color: var(--bs-secondary-color)` | `color: var(--bs-secondary-color); opacity: .5` | Double-dimming | P2-B |
| Month/Year/Decade grid | `display: grid; grid-template-columns: repeat(4,1fr)` + `.btn` tokens | `<button>` elements emitted; **no CSS at all** | Unstyled, single column | P2-E |
| Focus (shell) | Roving tabindex on gridcell button + `.focus-ring` box-shadow | `tabindex="0"` on `role="application"` shell | a11y anti-pattern; zero focus style | P0-B, P0-D |
| Dark mode | `[data-bs-theme=dark]` flips `--bs-*` tokens | Hard-coded `#212529`, `#0d6efd`, `#fff` | No repaint in dark mode | P0-C |
| Form-control integration | `.input-group` + `.dropdown-menu` | Bespoke `sf-datepicker__input-wrapper` + hand-rolled popup | Works but not idiomatic; misses theming | P2-G |

---

## 4. Suggested remediation order

1. **P0-A** rename (or re-emit) cell state classes so CSS and template agree — **1 hr**; immediately un-breaks all state visuals.
2. **P0-D** drop `role="application"`, move tabindex to inner `<button>` (roving) — **1 hr**; unblocks P0-B.
3. **P0-B** add `:focus-visible` box-shadow using `--bs-focus-ring-*` — **30 min**.
4. **P0-C** swap hard-coded hex for `--bs-*` tokens — **1 hr**; dark mode works.
5. **P1-E** split mid-range from edges — **1 hr** template + CSS.
6. **P1-A/B, P2-A/B/C** cosmetic polish pass — **1 hr**.
7. **P2-E/F** add CSS for year/decade/century grids + week-number column — **1 hr**.
8. **P2-G** retarget popups at `.dropdown-menu` tokens — **30 min**.
9. **P3** token surface + size modifier + RTL — **as demand appears**.

Total: **~7 hours** for P0+P1+P2 to bring SunfishCalendar to full BS5 idiomatic parity with first-class dark mode and accessibility.

---

## 5. Verification checklist

- [ ] Visual diff kitchen-sink calendar in light and `[data-bs-theme=dark]` modes
- [ ] Tab-ring is visible on nav buttons, title button, and focused day cell
- [ ] Keyboard: roving tabindex — only one cell is in the tab order at a time
- [ ] Range selection shows subtle fill for interior days, solid for edges
- [ ] Disabled days cannot be activated via Enter/Space (native `disabled`)
- [ ] Setting `--bs-primary: #7c3aed` on a wrapper re-themes selected/today/in-range
- [ ] axe-core passes: no `role="application"` finding, WCAG 2.4.7 passes
- [ ] Year/Decade/Century grids render as 4×3 with consistent cell sizing
- [ ] Week-number column (when enabled) has a visible separator and muted text

---

## 6. Citations

- Tables — getbootstrap.com/docs/5.3/content/tables (`.table`, `.table-hover`, `.table-sm`, `.table-borderless`)
- Buttons — getbootstrap.com/docs/5.3/components/buttons (`.btn-sm`, `.btn-outline-secondary`, `--bs-btn-*` tokens)
- Focus ring helper — getbootstrap.com/docs/5.3/helpers/focus-ring (`.focus-ring`, `--bs-focus-ring-color/width/blur`)
- CSS variables / dark mode — getbootstrap.com/docs/5.3/customize/css-variables (`[data-bs-theme=dark]` token flips)
- Borders — getbootstrap.com/docs/5.3/utilities/borders (`.border-primary-subtle`, `--bs-primary-border-subtle`)
- Input group — getbootstrap.com/docs/5.3/forms/input-group (datepicker host pattern)
- Dropdowns — getbootstrap.com/docs/5.3/components/dropdowns (`.dropdown-menu` tokens for popover surface)
- Forms overview — getbootstrap.com/docs/5.3/forms/overview (`$input-btn-*` shared tokens)
