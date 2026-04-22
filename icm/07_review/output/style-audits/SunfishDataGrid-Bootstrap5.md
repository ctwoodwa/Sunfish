# Style Audit: SunfishDataGrid vs Bootstrap 5.3

Generated: 2026-04-22
Scope: visual/stylistic parity of the Bootstrap-provider skin for `SunfishDataGrid` against upstream Bootstrap 5.3 conventions (getbootstrap.com/docs/5.3).
Sources:
- Sunfish component: `packages/ui-adapters-blazor/Components/DataDisplay/DataGrid/*`
- Sunfish Bootstrap skin: `packages/ui-adapters-blazor/Providers/Bootstrap/wwwroot/css/sunfish-bootstrap.css` (lines 13869–14185)
- Bootstrap CSS provider class wiring: `Providers/Bootstrap/BootstrapCssProvider.cs` (lines 690–718)
- Prior tracker: `Components/DataDisplay/DataGrid/GAP_ANALYSIS.md` (tracks API gaps, not styling — complementary)

---

## 1. Summary

**Where the Bootstrap skin matches BS5 idiomatically.** The grid root class is `table-responsive sf-bs-datagrid` which correctly composes Bootstrap's `.table-responsive` overflow container. Striped rows correctly consume `rgba(var(--bs-emphasis-color-rgb), 0.05)`, which matches BS5's `--bs-table-striped-bg` recipe. Row selection routes through `.table-active`, which is the correct contextual class per the Tables docs. Loading overlay tokens (`--bs-body-bg-rgb`), the footer row token (`--bs-tertiary-bg`), detail row (`--bs-secondary-bg`), empty-state color (`--bs-secondary-color`), pager active-button border (`--bs-primary`), and the filter-menu-active pill (`rgba(var(--bs-primary-rgb), 0.1)`) all resolve to the correct BS5 CSS variables so they inherit color-mode values automatically. Size tiers (`small/medium/large`) cleanly map to CSS custom properties that cascade to every cell.

**Where it diverges.** Sunfish uses a custom `<table>` with hand-rolled header/row/cell classes instead of inheriting any of Bootstrap's `.table` base styles (no `.table` on the `<table>` element, only on the wrapper `<div>` where `.table-responsive` is actually intended for a `<div>` around a `.table`). This means the grid re-implements borders, padding, striping, row dividers, and hover states in `sf-bs-datagrid-*` rules rather than participating in BS5's `--bs-table-*` variable contract. Consequently: no hover state (rows only highlight via `sf-bs-datagrid-row:hover`, but cells don't inherit `--bs-table-hover-bg`); checkboxes inside cells use raw `<input type="checkbox">` with zero `.form-check-input` styling; the sort indicator is a Unicode arrow (U+25B2/U+25BC) rather than an icon with `.visually-hidden` text; the loading overlay uses a custom span instead of `.spinner-border`; the empty-state is raw text rather than an `.alert` or `.placeholder-glow`; pagination lives in a sibling component (`SunfishPagination`) and does not render as Bootstrap's `.pagination` > `.page-item` > `.page-link` list; focus rings do not use `--bs-focus-ring-*` box-shadow — they use hard `outline`; **there are zero `[data-bs-theme=dark]` overrides for any `sf-bs-datagrid-*` class**, so several hardcoded surface colors (e.g., `background-color: var(--bs-body-bg)` on sticky locked cells) will look correct, but the sort indicator's `color: var(--bs-primary)` and the highlighted-row `rgba(var(--bs-warning-rgb), 0.18)` both stay the same brightness in dark mode, reducing legibility. Buttons rendered in command cells (`mar-datagrid-cmd-btn`) are not mapped to `.btn .btn-sm .btn-outline-secondary` — they are bare buttons with no BS visual language.

---

## 2. Prioritized Gap List

Priorities: **P0** (must-fix for parity, blocks a credible "Bootstrap skin" claim), **P1** (should-fix, degrades a11y or dark-mode usability), **P2** (nice-to-have, brings idiomatic alignment), **P3** (cosmetic polish).

---

### P0-1 — Root `<table>` does not carry the `.table` class

**Gap:** The grid emits `<table style="width:100%;table-layout:fixed;">` with no `.table` class. BS5 Tables documentation states the `.table` class is the foundation — it opts into `--bs-table-color`, `--bs-table-bg`, `--bs-table-border-color`, `--bs-table-striped-bg`, `--bs-table-hover-bg`, and `--bs-table-active-bg` tokens. Without it, `.table-active` (used by `DataGridRowClass`) is a no-op.

**Framework baseline** (https://getbootstrap.com/docs/5.3/content/tables/):
```html
<div class="table-responsive">
  <table class="table table-hover">
    ...
  </table>
</div>
```

**Sunfish today** (`SunfishDataGrid.razor` line 54):
```razor
<div class="sf-datagrid-content" style="@ContentContainerStyle">
    <table style="width:100%;table-layout:fixed;">
```
The root `<div>` uses `DataGridClass()` → `"table-responsive sf-bs-datagrid"`, but the inner `<table>` has no class. `DataGridRowClass(isSelected, isStriped)` returns `"sf-bs-datagrid-row table-active"` which assumes the parent table carries `.table`, but it does not.

**Recommendation:**
1. Add an opt-in Razor parameter (or harden at the provider level) to emit `class="table"` on the inner `<table>`. The simplest fix is in the Razor markup:
```razor
<table class="@CssProvider.DataGridTableClass()" style="width:100%;table-layout:fixed;">
```
Add on the provider:
```csharp
public string DataGridTableClass() => "table table-hover align-middle mb-0";
```
2. Gate `.table-striped` via `Striped`: render `"table table-hover table-striped align-middle mb-0"` when `Striped == true` — and then delete the bespoke `.sf-bs-datagrid-row--striped` rule (lines 13928–13930) because BS5's native `.table-striped` handles odd/even via `--bs-table-striped-bg`.

---

### P0-2 — No `data-bs-theme=dark` overrides for any datagrid class

**Gap:** The Sunfish Bootstrap CSS contains dark-mode overrides for datasheet, chart, scheduler, etc., but zero `[data-bs-theme=dark] .sf-bs-datagrid*` rules. Several datagrid tokens are hardcoded and will not flip for dark mode:
- `--mar-datagrid-resize-handle-color, rgba(0, 0, 0, 0.2)` (line 14184) — darkens on light bg, invisible on dark bg.
- `mar-datagrid-row--highlighted`: `background-color: rgba(var(--bs-warning-rgb), 0.18) !important; outline: 1px solid rgba(var(--bs-warning-rgb), 0.5);` — BS warning-rgb is identical in both modes, but the 0.18 alpha on dark background is too low to see.
- Popup overlay (`sf-bs-datagrid-popup-overlay`, line 14077): `background: rgba(0, 0, 0, 0.3);` — identical in both modes but the contrast drops against the dark body.
- Sort indicator at `color: var(--bs-primary)` — BS primary `#0d6efd` should stay, but verify contrast against `#212529` body-bg.

**Framework baseline** (https://getbootstrap.com/docs/5.3/customize/color-modes/):
```css
[data-bs-theme=dark] {
  color-scheme: dark;
  --bs-body-bg: #212529;
  --bs-border-color: #495057;
  --bs-emphasis-color: #fff;
  --bs-secondary-bg: #343a40;
  --bs-tertiary-bg: #2b3035;
}
```
Components should compose these tokens rather than hardcode RGB.

**Sunfish today** (`sunfish-bootstrap.css` line 14170):
```css
.mar-datagrid-col-resize-handle:hover,
.mar-datagrid-col-resize-handle.is-dragging {
  background-color: var(--mar-datagrid-resize-handle-color, rgba(0, 0, 0, 0.2));
}
```

**Recommendation:** add a dedicated dark-mode section near the bottom of the datagrid block:
```css
[data-bs-theme=dark] .mar-datagrid-col-resize-handle:hover,
[data-bs-theme=dark] .mar-datagrid-col-resize-handle.is-dragging {
  background-color: rgba(255, 255, 255, 0.2);
}
[data-bs-theme=dark] .mar-datagrid-row--highlighted {
  background-color: rgba(var(--bs-warning-rgb), 0.28) !important;
  outline-color: rgba(var(--bs-warning-rgb), 0.65);
}
[data-bs-theme=dark] .sf-bs-datagrid-popup-overlay {
  background: rgba(0, 0, 0, 0.55);
}
```
Also remove the hardcoded `background-color: var(--bs-body-bg)` fallback on `.sf-bs-datagrid-col--locked` (line 14114) and verify it's already using a var — it is, but confirm rendering against actual BS5 dark palette.

---

### P0-3 — Checkboxes do not use `.form-check-input`

**Gap:** Select-all and per-row checkboxes are emitted as raw `<input type="checkbox">`. BS5 documents the contract for checkbox styling is `.form-check-input` — without it they render as user-agent defaults (tiny white squares with native accents) inside data rows, which looks broken against the rest of the BS form palette.

**Framework baseline** (https://getbootstrap.com/docs/5.3/forms/checks/):
```html
<input class="form-check-input" type="checkbox" id="checkbox1">
<label class="form-check-label" for="checkbox1">Label</label>
```

**Sunfish today** (`SunfishDataGrid.razor` lines 96–100, 83–90 of `.Rendering.cs`):
```razor
<input type="checkbox"
       checked="@(_selectedItems.Count > 0 && _selectedItems.Count == _displayedItems.Count)"
       aria-label="Select all rows"
       @onchange="OnSelectAllChanged" />
```
And in Rendering.cs:
```csharp
builder.OpenElement(33, "input");
builder.AddAttribute(34, "type", "checkbox");
```

**Recommendation:** add `form-check-input` to both the select-all (razor line 97) and per-row (Rendering.cs line 83–86) checkboxes. Because those are Bootstrap-provider-specific, route the class through the CSS provider:
```csharp
public string DataGridCheckboxInputClass() => "form-check-input m-0";
```
Then consume:
```csharp
builder.AddAttribute(34, "type", "checkbox");
builder.AddAttribute(35, "class", CssProvider.DataGridCheckboxInputClass());
```

---

### P0-4 — Command buttons bypass `.btn` entirely

**Gap:** Edit/Save/Cancel/Delete buttons are emitted with `class="mar-datagrid-cmd-btn"` (and a `--sm` variant), which is defined nowhere in the Bootstrap CSS — leaving them as bare `<button>` elements with no fill, border, padding, or hover. `SunfishGridCommandButton.razor` similarly does not surface a `.btn` by default.

**Framework baseline** (https://getbootstrap.com/docs/5.3/components/buttons/):
```html
<button type="button" class="btn btn-sm btn-outline-secondary">Edit</button>
<button type="button" class="btn btn-sm btn-outline-danger">Delete</button>
```

**Sunfish today** (`SunfishDataGrid.Rendering.cs` line 213):
```csharp
builder.OpenElement(82, "button");
builder.AddAttribute(83, "type", "button");
builder.AddAttribute(84, "class", "mar-datagrid-cmd-btn");
```

**Recommendation:** either (a) route the command button class through the CSS provider and emit BS classes, or (b) add the missing style block for `mar-datagrid-cmd-btn`. Option (a) is the idiomatic fix:
```csharp
// BootstrapCssProvider.cs
public string DataGridCommandButtonClass(string kind) => kind switch
{
    "save"    => "btn btn-sm btn-primary",
    "cancel"  => "btn btn-sm btn-outline-secondary",
    "delete"  => "btn btn-sm btn-outline-danger",
    "inline"  => "btn btn-sm btn-outline-secondary", // icon-only for InCell
    _         => "btn btn-sm btn-outline-secondary",
};
```
Then in Rendering.cs replace `"mar-datagrid-cmd-btn"` with a call to `CssProvider.DataGridCommandButtonClass("edit")`, etc. Note: the Edit and Delete buttons should be grouped with `.btn-group .btn-group-sm` for BS visual grouping.

---

### P0-5 — Sort indicator uses Unicode glyphs instead of icons + `.visually-hidden`

**Gap:** The sort arrow is a hardcoded `"▲"` / `"▼"` character inside a `<span class="sf-datagrid-sort-indicator" aria-hidden="true">`. BS5 does not ship sort icons, but it does ship Bootstrap Icons and has a strong convention for `aria-hidden` icon + `.visually-hidden` label. Screen readers lose directional information because the arrows are `aria-hidden`. Sighted users see a glyph that doesn't match the weight/antialiasing of the rest of the Bootstrap chrome.

**Framework baseline** (Bootstrap Icons + a11y pattern — https://icons.getbootstrap.com/):
```html
<span class="ms-1 text-primary" aria-hidden="true">
  <i class="bi bi-caret-up-fill"></i>
</span>
<span class="visually-hidden">(sorted ascending)</span>
```

**Sunfish today** (`SunfishDataGrid.razor` line 139):
```razor
<span class="sf-datagrid-sort-indicator" aria-hidden="true">
    @(sortDir.Direction == SortDirection.Ascending ? "▲" : "▼")
    @if (_state.SortDescriptors.Count > 1)
    {
        <sub class="sf-datagrid-sort-order">@sortOrder</sub>
    }
</span>
```

**Recommendation:** keep the Unicode fallback (to avoid a Bootstrap Icons hard dependency), but add a `.visually-hidden` sibling with textual direction for screen-readers, and align spacing with `.ms-1`:
```razor
<span class="sf-bs-datagrid-sort-indicator ms-1" aria-hidden="true">
    @(sortDir.Direction == SortDirection.Ascending ? "▲" : "▼")
    @if (_state.SortDescriptors.Count > 1)
    {
        <sub class="sf-bs-datagrid-sort-order">@sortOrder</sub>
    }
</span>
<span class="visually-hidden">
    (sorted @(sortDir.Direction == SortDirection.Ascending ? "ascending" : "descending"))
</span>
```
Note: the `aria-sort` attribute already carries the directional info (line 123), so screen-reader coverage is not fully broken today, but the `.visually-hidden` pattern is the BS5 idiom for row-level updates and pairs with any consumer CSS they might layer.

---

### P1-1 — Loading overlay does not use `.spinner-border`

**Gap:** The overlay renders a `<div class="sf-datagrid-loading-spinner" role="status"><span class="sf-datagrid-loading-text">Loading…</span></div>`. There is no actual CSS rule for `sf-datagrid-loading-spinner` in the Bootstrap skin, so the user sees only the text "Loading…". BS5 ships `.spinner-border` for exactly this.

**Framework baseline** (https://getbootstrap.com/docs/5.3/components/spinners/):
```html
<div class="spinner-border text-primary" role="status">
  <span class="visually-hidden">Loading...</span>
</div>
```

**Sunfish today** (`SunfishDataGrid.razor` lines 44–51):
```razor
@if (IsLoading)
{
    <div class="sf-datagrid-loading-overlay" aria-live="polite">
        <div class="sf-datagrid-loading-spinner" role="status">
            <span class="sf-datagrid-loading-text">Loading…</span>
        </div>
    </div>
}
```

**Recommendation:** route via provider (keeps MudBlazor/Tailwind adapters free to differ):
```csharp
public string DataGridLoadingOverlayClass() => "sf-bs-datagrid-loading-overlay";
public string DataGridLoadingSpinnerClass() => "spinner-border text-primary";
public string DataGridLoadingTextClass() => "visually-hidden";
```
Markup becomes:
```razor
<div class="@CssProvider.DataGridLoadingOverlayClass()" aria-live="polite">
    <div class="@CssProvider.DataGridLoadingSpinnerClass()" role="status">
        <span class="@CssProvider.DataGridLoadingTextClass()">Loading…</span>
    </div>
</div>
```

---

### P1-2 — Empty state renders as raw text, not a Bootstrap alert or placeholder

**Gap:** The default no-data cell is a plain `<td>No data available.</td>`. BS5 recommends `.alert` for informational messaging and `.placeholder-glow` for empty-during-load. The current behavior provides no visual weight and mixes with body rows.

**Framework baseline** (https://getbootstrap.com/docs/5.3/components/alerts/):
```html
<div class="alert alert-secondary mb-0 text-center" role="status">
  No records to display.
</div>
```

**Sunfish today** (`SunfishDataGrid.razor` lines 291–300):
```razor
<tr role="row">
    <td colspan="@TotalColumnCount" role="gridcell" class="sf-datagrid-empty">
        @if (NoDataTemplate != null) { @NoDataTemplate }
        else { <text>No data available.</text> }
    </td>
</tr>
```
CSS (line 14058):
```css
.sf-bs-datagrid-empty {
  text-align: center;
  padding: var(--sf-space-xl) var(--sf-space-md);
  color: var(--bs-secondary-color);
}
```

**Recommendation:** the current class is fine; bump visual weight by wrapping the fallback in an inline `.alert`:
```razor
<td colspan="@TotalColumnCount" role="gridcell" class="sf-bs-datagrid-empty">
    @if (NoDataTemplate != null)
    {
        @NoDataTemplate
    }
    else
    {
        <div class="alert alert-secondary mb-0 d-inline-block" role="status">
            No data available.
        </div>
    }
</td>
```

---

### P1-3 — Focus ring uses `outline` instead of `--bs-focus-ring` box-shadow

**Gap:** `.sf-bs-datagrid-cell:focus` / `.sf-bs-datagrid-header-cell:focus` use `outline: 2px solid var(--bs-focus-ring-color, var(--sf-color-primary)); outline-offset: -2px;`. BS5's idiomatic focus-ring uses a `box-shadow` pattern built from `--bs-focus-ring-width`, `--bs-focus-ring-color`, `--bs-focus-ring-blur`, and is keyed to `:focus-visible` — not `:focus`. Using `:focus` makes the ring appear on mouse-click (which BS5 explicitly avoids); using `:focus-visible` shows it only for keyboard focus.

**Framework baseline** (https://getbootstrap.com/docs/5.3/helpers/focus-ring/):
```css
.focus-ring:focus {
  outline: 0;
  box-shadow: var(--bs-focus-ring-x, 0) var(--bs-focus-ring-y, 0) var(--bs-focus-ring-blur, 0) var(--bs-focus-ring-width) var(--bs-focus-ring-color);
}
```
Plus `:focus-visible` is the preferred selector for keyboard-only visuals.

**Sunfish today** (lines 14125–14131, 14133–14136):
```css
.sf-bs-datagrid-cell:focus,
.sf-bs-datagrid-header-cell:focus {
  outline: 2px solid var(--bs-focus-ring-color, var(--sf-color-primary));
  outline-offset: -2px;
  position: relative;
  z-index: 1;
}
.sf-bs-datagrid-row--focused {
  outline: 2px solid var(--bs-focus-ring-color, var(--sf-color-primary));
  outline-offset: -1px;
}
```

**Recommendation:** switch to `:focus-visible` + box-shadow:
```css
.sf-bs-datagrid-cell:focus-visible,
.sf-bs-datagrid-header-cell:focus-visible {
  outline: 0;
  box-shadow: inset 0 0 0 var(--bs-focus-ring-width, 0.25rem) var(--bs-focus-ring-color);
  position: relative;
  z-index: 1;
}
.sf-bs-datagrid-row--focused {
  outline: 0;
  box-shadow: inset 0 0 0 2px var(--bs-focus-ring-color);
}
```

---

### P1-4 — No `table-hover` behavior; hover is bespoke and uses a surface token, not `--bs-table-hover-bg`

**Gap:** The hover rule `.sf-bs-datagrid-row:hover { background: var(--sf-color-surface); }` overrides a Sunfish surface variable instead of BS5's `--bs-table-hover-bg: rgba(var(--bs-emphasis-color-rgb), 0.075)`. As a result hover does not flip in dark mode and doesn't match BS5's native `.table-hover > tbody > tr:hover > *` pattern, which uses `--bs-table-color-state` / `--bs-table-bg-state` so it composes with `.table-active` and striping.

**Framework baseline** (lines 1926–1929 of the generated bundle):
```css
.table-hover > tbody > tr:hover > * {
  --bs-table-color-state: var(--bs-table-hover-color);
  --bs-table-bg-state: var(--bs-table-hover-bg);
}
```

**Sunfish today** (lines 13910–13912):
```css
.sf-bs-datagrid-row:hover {
  background: var(--sf-color-surface);
}
```

**Recommendation:** once P0-1 is landed (adding `.table` and `.table-hover` to the inner `<table>`), **delete** the `.sf-bs-datagrid-row:hover` rule — BS5 will handle hover natively. If Sunfish still needs to disable hover on group/detail rows, add `.sf-bs-datagrid-group-header > *, .sf-bs-datagrid-detail-row > *` exemptions that reset `--bs-table-bg-state` to `transparent`.

---

### P1-5 — Pager buttons redefine `.btn-outline-secondary` via extend rather than using it

**Gap:** `.sf-bs-datagrid-pager-btn` at CSS line 14049 is pulled into a mass of `@extend`-like selector chains (the `.btn, .sf-bs-datagrid-pager-btn` pattern shows throughout the bundle). This works, but it means the pager class is coupled to the entire BS button stylesheet. Worse, the active state (`sf-bs-datagrid-pager-btn.active, sf-bs-datagrid-pager-btn--active` at 14054) only sets `border-color: var(--bs-primary)` — BS5's `.pagination .page-item.active .page-link` uses `background-color: var(--bs-pagination-active-bg)` and `color: var(--bs-pagination-active-color)`. This is also not the documented pagination structure (`<nav><ul class="pagination"><li class="page-item"><a class="page-link">`).

**Framework baseline** (https://getbootstrap.com/docs/5.3/components/pagination/):
```html
<nav aria-label="Page navigation">
  <ul class="pagination">
    <li class="page-item disabled"><a class="page-link">Previous</a></li>
    <li class="page-item active" aria-current="page"><a class="page-link" href="#">2</a></li>
    <li class="page-item"><a class="page-link" href="#">3</a></li>
    <li class="page-item"><a class="page-link">Next</a></li>
  </ul>
</nav>
```

**Sunfish today**: `SunfishPagination` is a separate component referenced at `SunfishDataGrid.razor` line 414 — its implementation lives outside this audit file, but the pager-btn CSS (14049–14056) suggests it currently emits plain buttons, not `.pagination > .page-item > .page-link` structure.

**Recommendation:** this is worth a P1-level note here even though the actual markup change is scoped to `SunfishPagination.razor`. Route the pager through `CssProvider.PaginationListClass() => "pagination"`, `PaginationItemClass(bool active, bool disabled)`, `PaginationLinkClass()` and in Bootstrap return `page-item`, `page-item active`, and `page-link` respectively. Then drop the `.sf-bs-datagrid-pager-btn` class entirely.

---

### P1-6 — Filter-row input does not carry `.form-control-sm`

**Gap:** The filter-row `<input type="text">` (line 265 in `SunfishDataGrid.razor`) is un-classed; the CSS nests under `.sf-bs-datagrid-filter-cell .form-control` (line 13949) expecting the consumer to have applied `.form-control`. But the component never emits that class.

**Framework baseline** (https://getbootstrap.com/docs/5.3/forms/form-control/):
```html
<input type="text" class="form-control form-control-sm" placeholder="Filter...">
```

**Sunfish today** (`SunfishDataGrid.razor` line 265):
```razor
<input type="text"
       value="@(currentFilter?.Value?.ToString() ?? "")"
       placeholder="Filter..."
       aria-label="@($"Filter {column.DisplayTitle}")"
       @onchange="(e) => OnFilterChanged(column.Field, e)" />
```

**Recommendation:** route via provider and emit:
```csharp
public string DataGridFilterInputClass() => "form-control form-control-sm";
```
Consume in markup:
```razor
<input type="text"
       class="@CssProvider.DataGridFilterInputClass()"
       ...
```

---

### P1-7 — Search box input skips `.form-control`

**Gap:** Same pattern as P1-6 for the above-grid search box (line 30):
```razor
<input type="text"
       class="sf-datagrid-searchbox-input"
       placeholder="@SearchBoxPlaceholder"
       ...
```
There is a `.sf-bs-datagrid-searchbox input` selector block (CSS via the `.form-control, .sf-bs-datagrid-searchbox input` chain), so it inherits form-control styling transitively, but the explicit `.form-control` class would be cleaner and guarantee correctness if providers are swapped.

**Recommendation:** add `class="form-control"` (or a provider-issued class) to the search input. Bonus: wrap with `.input-group` and a search icon prepend for BS idiomatic search look.

---

### P1-8 — Popup/edit dialog bypasses BS5 `.modal`

**Gap:** The popup-edit dialog (lines 372–406) uses bespoke classes `sf-datagrid-popup-overlay`, `sf-datagrid-popup-dialog`, `sf-datagrid-popup-header`, `sf-datagrid-popup-body`, `sf-datagrid-popup-actions`. BS5's `.modal` stack has full keyboard-trap, ARIA, and focus-management behavior — re-implementing it is an a11y regression.

**Framework baseline** (https://getbootstrap.com/docs/5.3/components/modal/):
```html
<div class="modal fade show" tabindex="-1" role="dialog" style="display:block">
  <div class="modal-dialog">
    <div class="modal-content">
      <div class="modal-header">
        <h5 class="modal-title">Edit item</h5>
        <button type="button" class="btn-close" aria-label="Close"></button>
      </div>
      <div class="modal-body">…</div>
      <div class="modal-footer">
        <button class="btn btn-secondary">Cancel</button>
        <button class="btn btn-primary">Save</button>
      </div>
    </div>
  </div>
</div>
```

**Sunfish today** (line 375):
```razor
<div class="sf-datagrid-popup-overlay" @onclick="CancelEdit">
    <div class="sf-datagrid-popup-dialog" role="dialog" ...>
        <div class="sf-datagrid-popup-header"><h3>...</h3></div>
        ...
```

**Recommendation:** Route popup classes through the provider and have Bootstrap return `modal`, `modal-dialog modal-lg`, `modal-content`, `modal-header`, `modal-body`, `modal-footer`. Add a `.btn-close` button for the header and bind it to `CancelEdit`. Because the popup is custom-rendered (not BS JS-driven), you'll need to manually add `modal-open` to `<body>` and a `modal-backdrop fade show` sibling — this is a larger change and ranks P1 not P0 only because `GridEditMode.Popup` is opt-in.

---

### P2-1 — Bespoke `.sf-bs-datagrid-row--striped` duplicates BS5's `.table-striped`

**Gap:** CSS line 13928 defines `.sf-bs-datagrid-row--striped:nth-child(even)` targeting `rgba(var(--bs-emphasis-color-rgb), 0.05)` — which is literally the value of `--bs-table-striped-bg` from BS5 (see line 1861). Duplicating.

**Sunfish today**:
```css
.sf-bs-datagrid-row--striped:nth-child(even) {
  background-color: rgba(var(--bs-emphasis-color-rgb), 0.05);
}
```

**Recommendation:** delete once P0-1 adds `.table-striped` to the table when `Striped == true`. Also delete the `isStriped` parameter on `DataGridRowClass` and stop passing striping per-row.

---

### P2-2 — `table-layout: fixed` inline style may fight column auto-sizing in dark mode flex layouts

**Gap:** The inline `style="width:100%;table-layout:fixed;"` is reasonable when resize/reorder are active, but removes BS's normal `table-layout: auto` heuristics for grids without resize. This doesn't affect parity but it's worth noting that BS5 `.table` uses `table-layout: auto`.

**Recommendation:** make `table-layout: fixed` conditional on `AllowColumnResize || AllowColumnReorder || _visibleColumns.Any(c => c.Locked)` — otherwise default to `auto` to match BS5 behavior.

---

### P2-3 — Group header row uses `.table-secondary` without `.table` context

**Gap:** `DataGridGroupHeaderClass()` returns `"table-secondary fw-bold sf-bs-datagrid-group-header"`. `.table-secondary` (lines 1945–1957) relies on `--bs-table-color`, `--bs-table-bg`, `--bs-table-border-color`, `--bs-table-striped-bg`, etc. being scoped to a `.table` parent. Without P0-1 the cell-state cascade is partly broken.

**Recommendation:** covered by P0-1. Once `.table` is on the table, the `.table-secondary` variant works as documented.

---

### P2-4 — Footer row uses `--bs-tertiary-bg` directly; consider `.table-group-divider`

**Gap:** Footer row (line 14097) uses a double-thick top border via `border-top: 1px solid var(--sf-color-border)` + `background: var(--bs-tertiary-bg)`. BS5 has `.table-group-divider` that renders a doubled `border-top: calc(var(--bs-border-width) * 2) solid currentcolor` — idiomatic for thead/tfoot separation.

**Framework baseline**:
```html
<tfoot class="table-group-divider">…</tfoot>
```

**Sunfish today** (line 14097):
```css
.sf-bs-datagrid-footer-row {
  background-color: var(--bs-tertiary-bg);
  border-top: 1px solid var(--sf-color-border);
  font-weight: 600;
}
```

**Recommendation:** emit `class="table-group-divider"` on `<tfoot>` and remove the top-border style from `.sf-bs-datagrid-footer-row`.

---

### P2-5 — Detail row background uses `--bs-secondary-bg`; consider `.table-light` / subtle accent

**Gap:** Detail row bg is `var(--bs-secondary-bg)` which is `#e9ecef` in light and `#343a40` in dark — both are only slightly different from body. BS5 has `--bs-tertiary-bg` (lighter) and contextual `--bs-primary-bg-subtle` tokens more commonly used for "expanded" visual containers.

**Recommendation:** use `--bs-tertiary-bg` for detail rows (lighter than secondary, closer to BS5's own "nested panel" convention), and add a left accent: `box-shadow: inset 4px 0 0 0 var(--bs-primary);`.

---

### P2-6 — Resize handle hover color hardcodes `rgba(0, 0, 0, 0.2)`

**Gap:** `--mar-datagrid-resize-handle-color, rgba(0, 0, 0, 0.2)` — the fallback is dark regardless of theme. The variable is not set by the size tiers or by dark-mode overrides.

**Recommendation:** set the variable at the grid root and override per-theme:
```css
.sf-bs-datagrid { --mar-datagrid-resize-handle-color: rgba(var(--bs-emphasis-color-rgb), 0.15); }
[data-bs-theme=dark] .sf-bs-datagrid { --mar-datagrid-resize-handle-color: rgba(var(--bs-emphasis-color-rgb), 0.25); }
```
(the emphasis-color-rgb flips with the theme).

---

### P2-7 — Column menu trigger (&#x22EE;) has no CSS rule in the Bootstrap skin

**Gap:** `SunfishDataGrid.razor` line 197 emits `<button class="mar-datagrid-column-menu-trigger">` — there is no matching `.mar-datagrid-column-menu-trigger` rule in `sunfish-bootstrap.css` (confirmed by grep). The button renders as a raw user-agent button.

**Recommendation:** add a minimal style that matches the filter-menu-btn style (lines 13958–13979):
```css
.sf-bs-datagrid .mar-datagrid-column-menu-trigger {
  display: inline-flex; align-items: center; justify-content: center;
  margin-left: 0.25rem;
  border: 1px solid transparent;
  background: transparent;
  color: var(--bs-secondary-color);
  min-width: 1.5rem; height: 1.5rem;
  border-radius: 0.25rem; cursor: pointer;
}
.sf-bs-datagrid .mar-datagrid-column-menu-trigger:hover {
  border-color: var(--bs-border-color);
  background: var(--bs-tertiary-bg);
  color: var(--bs-body-color);
}
```

---

### P3-1 — Sticky locked column `z-index` values are magic numbers

**Gap:** `GetFrozenCellStyle` inline-styles `z-index: 3` for header, `2` for body. These are fine but BS5 documents `$zindex-sticky: 1020` — the current values are scoped to the grid, which is OK, but a comment explaining the scope would help maintainers.

**Recommendation:** add inline comment (no code change needed) or promote to CSS variables: `--sf-datagrid-z-sticky-header: 3; --sf-datagrid-z-sticky-body: 2;`.

---

### P3-2 — Missing vertical alignment on table

**Gap:** BS5 `.table` defaults to `vertical-align: top;`. Sunfish cells don't specify, so inherit user-agent baseline, which causes badge+text cells to misalign.

**Recommendation:** add `.align-middle` to the `<table>` class list (or rely on P0-1 + `.align-middle` modifier).

---

### P3-3 — Drag handles and detail-toggle icons lack `bi` icon affordance

**Gap:** The row drag handle uses `"⠇"` (Braille pattern dots-1-2-3), the detail toggle uses `"▼" / "▶"` (play triangles). These Unicode glyphs look aliased/crude against Bootstrap's Segoe/-apple-system font stack.

**Recommendation:** P3 polish — if/when Bootstrap Icons are a declared peer dep, swap to `<i class="bi bi-grip-vertical"></i>` and `<i class="bi bi-caret-right-fill"></i>` / `bi-caret-down-fill`.

---

### P3-4 — Border-radius on the grid root clips sticky columns

**Gap:** `.sf-bs-datagrid { border-radius: var(--sf-radius-md); }` (line 13871) is fine when the grid is not scrolling, but because `.table-responsive` applies `overflow-x: auto` to the parent, on horizontal scroll the sticky-locked column's background-color will render outside the rounded corner, creating a square-cornered sliver at the edges.

**Recommendation:** add `overflow: hidden` alongside `border-radius` on `.sf-bs-datagrid`, or lift the radius to a wrapper `<div>` that isn't the scroll container. Low-impact polish.

---

### P3-5 — Validation summary consolidates `.alert` into one selector

**Gap:** `.alert, .sf-bs-datagrid-validation-summary` chain means any use of the validation-summary gets the full `.alert` role-styling — but the existing selector also chains in `.alert-danger, .sf-bs-datagrid-validation-summary` (line 4912) which hardcodes the summary to "danger" color. If a consumer wants a "warning-flavored" validation summary they cannot opt out.

**Recommendation:** split — emit `class="alert alert-danger"` directly in markup for validation summary, and remove the `.sf-bs-datagrid-validation-summary` shortcut.

---

## 3. Focus-Area Coverage Table

| Area | BS5 idiomatic? | Sunfish state | Priority |
|---|---|---|---|
| **Header (&lt;thead&gt;)** | Yes — `.table > thead { vertical-align: bottom }`, `.table-group-divider` for separation | Custom `.sf-bs-datagrid-header` with `background: var(--sf-color-surface)` and 2px border-bottom; no `.table` parent | P0-1 |
| **Row (&lt;tr&gt;)** | Yes — relies on `--bs-table-bg`, `--bs-table-color`, hover via `.table-hover` | `.sf-bs-datagrid-row` with bespoke hover and striping; `.table-active` used for selection but table lacks `.table` class | P0-1, P1-4 |
| **Cell (&lt;td&gt;)** | Yes — `--bs-table-bg-state` cascades for hover/active/striped | `.sf-bs-datagrid-cell` with hardcoded padding; size tier vars correctly consumed | P0-1 |
| **Sort indicator** | Not shipped natively; pattern is icon + `.visually-hidden` + `aria-sort` | Unicode arrow + `aria-sort` on `<th>`; no `.visually-hidden` label | P0-5 |
| **Filter chrome (row + menu + button)** | Uses `.form-control-sm`, `.dropdown-menu`, `.btn`, `.input-group` | Custom `.sf-datagrid-filter-menu-btn` with inline SVG, custom popup div; input lacks `.form-control` | P1-6, P1-1 |
| **Pager** | `<nav><ul.pagination><li.page-item><a.page-link>` structure | Custom `SunfishPagination` component emitting plain buttons with `.sf-bs-datagrid-pager-btn` | P1-5 |
| **Hover state** | `.table-hover > tbody > tr:hover > * { --bs-table-bg-state: var(--bs-table-hover-bg) }` | Bespoke `.sf-bs-datagrid-row:hover` using `--sf-color-surface` | P1-4 |
| **Focus state** | `.focus-ring` + `:focus-visible` + box-shadow from `--bs-focus-ring-*` | `:focus` + `outline` using `--bs-focus-ring-color` fallback | P1-3 |
| **Active / selected** | `.table-active` with `--bs-table-active-bg: rgba(var(--bs-emphasis-color-rgb), 0.1)` | `.table-active` applied via `DataGridRowClass` + bespoke `--selected` rule; works transitively but table lacks `.table` class | P0-1 |
| **Disabled state** | `.btn.disabled`, `.form-control:disabled` with `--bs-border-color` and `opacity: 0.65` | Not surfaced explicitly on command buttons or checkbox states | P2 |
| **Selected cell (cell selection)** | N/A — BS5 does not define cell-selection; uses `.table-active` for cells | `.mar-datagrid-cell--selected` exists as a class but no corresponding CSS rule in Bootstrap skin | P2 (unstyled) |
| **Loading** | `.spinner-border` or `.spinner-grow` + `.visually-hidden` text + `role="status"` | Custom div with no spinner CSS in Bootstrap skin | P1-1 |
| **Empty state** | `.alert .alert-secondary` or `.placeholder-glow` | Raw text in `<td>` with custom `.sf-bs-datagrid-empty` | P1-2 |
| **Error state** | `.alert .alert-danger` or `.is-invalid` on form-controls | `.sf-bs-datagrid-validation-summary` chained with `.alert-danger` — no component-level error surfacing | P3-5 |
| **Density: compact (small)** | N/A — BS5 has `.table-sm { padding: 0.25rem 0.25rem }` | `.mar-datagrid--size-small` overrides `--mar-datagrid-cell-padding: 0.25rem 0.5rem` — matches BS5 `.table-sm` closely | Covered |
| **Density: comfortable (medium)** | BS5 default = `0.5rem 0.5rem` | `--mar-datagrid-cell-padding: 0.5rem 0.75rem` — slightly roomier than BS5 default, acceptable | Covered |
| **Density: spacious (large)** | Not a BS5 variant | `--mar-datagrid-cell-padding: 0.75rem 1rem` — Sunfish extension | Covered |
| **Theme tokens (--bs-*)** | 32+ tokens exposed including `--bs-table-*`, `--bs-border-color`, `--bs-body-bg` | Partially consumed — `--bs-body-bg`, `--bs-secondary-bg`, `--bs-tertiary-bg`, `--bs-primary`, `--bs-secondary-color`, `--bs-warning-rgb` used; `--bs-table-*` not consumed because `.table` is absent | P0-1, P0-2 |
| **Dark mode (data-bs-theme)** | First-class via `[data-bs-theme=dark]` scope | Zero `[data-bs-theme=dark] .sf-bs-datagrid*` rules; several hardcoded `rgba(0,0,0,0.x)` colors | P0-2 |
| **Focus ring a11y** | `--bs-focus-ring-width: 0.25rem`, `.focus-ring` util, `:focus-visible` | Uses `:focus` not `:focus-visible`; `outline` not box-shadow | P1-3 |
| **Row selection indicator (visual)** | `.table-active` fills via CSS vars | Works but dependent on P0-1 | P0-1 |
| **ARIA-reflecting classes** | `.visually-hidden` for SR-only text | Not used for sort direction; checkbox aria-label is set | P0-5 |
| **Checkbox styling** | `.form-check-input` | Raw `<input type="checkbox">`, unstyled in Bootstrap theme | P0-3 |
| **Command buttons** | `.btn .btn-sm .btn-outline-*` + `.btn-group` | `.mar-datagrid-cmd-btn` class with no CSS rule in Bootstrap skin | P0-4 |
| **Edit popup** | `.modal .modal-dialog .modal-content` + `.btn-close` | Bespoke overlay/dialog/header/body/actions classes | P1-8 |
| **Column resize handle** | N/A — not a BS5 primitive | `.mar-datagrid-col-resize-handle` with hardcoded dark hover color | P2-6, P0-2 |
| **Frozen/locked columns** | N/A — not a BS5 primitive; `position:sticky` via custom CSS | `.sf-bs-datagrid-col--locked` uses `--bs-body-bg`, z-index layering OK; shadow class `--locked-end` present | Covered |
| **Row drag handle** | N/A — not a BS5 primitive | Unicode Braille glyph; no Bootstrap-specific CSS | P3-3 |
| **Group header** | Use `.table-secondary` variant | `.table-secondary fw-bold sf-bs-datagrid-group-header` | Covered (but depends on P0-1) |
| **Footer row** | `.table-group-divider` on `<tfoot>` | Bespoke `.sf-bs-datagrid-footer-row` with custom border-top | P2-4 |
| **Detail row** | Not a primitive; convention is `.bg-body-tertiary` | `.sf-bs-datagrid-detail-row` uses `--bs-secondary-bg` | P2-5 |
| **Responsive / overflow** | `.table-responsive` wrapper on `<div>` around `<table>` | `.table-responsive` applied to the grid root div; inner `.sf-datagrid-content` adds its own `overflow-x: auto` — potentially duplicates scrollbar | P2 (scrollbar overlap) |
| **Text-alignment per column** | `.text-start`, `.text-center`, `.text-end` utility classes | Inline `text-align` style from `column.TextAlign` | Covered (functional) |

---

## Implementation Order Suggestion

For a follow-up agent, the most efficient sequence is:

1. **P0-1** (add `.table` class) — unlocks P1-4 (hover), P2-1 (striping dedup), P2-3 (group header variant), and correct `.table-active` behavior in one change.
2. **P0-2** (dark-mode overrides) — add `[data-bs-theme=dark]` block for the three affected rules.
3. **P0-3** (`.form-check-input` on checkboxes) — two-line change via provider method.
4. **P0-4** (`.btn .btn-sm .btn-outline-*` on command buttons) — provider method + Rendering.cs swap.
5. **P0-5** (sort indicator `.visually-hidden` sibling) — markup-only change in Razor.
6. P1 cluster (loading spinner, empty alert, focus-visible, filter-input form-control, search form-control).
7. P2 + P3 cleanup.

After P0 + P1 the grid will read as a genuine Bootstrap 5.3 component to the eye and to screen-readers.

---

## Cross-reference to `GAP_ANALYSIS.md`

That file tracks API-level phase work; this audit is purely stylistic. No overlap except:
- **C4 Column Chooser** (unimplemented per GAP_ANALYSIS) — when landed, should emit `.modal` markup per P1-8.
- **D3 Pager Template** — if implemented, should render the BS5 `.pagination > .page-item > .page-link` structure per P1-5.

No gaps identified here duplicate that document; they are purely complementary.
