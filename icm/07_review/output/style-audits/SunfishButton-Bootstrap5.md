# Style Audit: SunfishButton vs Bootstrap 5.3 Button

**Date:** 2026-04-22
**Component:** `SunfishButton` (Blazor adapter) / Bootstrap skin
**BS5 reference:** https://getbootstrap.com/docs/5.3/components/buttons/ (and button-group, forms/checks, helpers/focus-ring, components/spinners)
**Status:** Draft — stage 07_review

---

## 1. Scope & method

Audited files:

- `packages/ui-adapters-blazor/Components/Buttons/SunfishButton.razor` — renders a `<button>` and delegates all class production to `CssProvider.ButtonClass(...)`.
- `packages/ui-adapters-blazor/Providers/Bootstrap/BootstrapCssProvider.cs` — `ButtonClass(...)` (lines 236-258), `IconButtonClass`, `ToggleButtonClass`, `SplitButtonClass`.
- `packages/ui-adapters-blazor/Providers/Bootstrap/wwwroot/css/sunfish-bootstrap.css` — the compiled BS5 skin (`.btn`, `.btn-*`, `.btn-outline-*`, `.btn-sm/lg`, `.btn-group*`, `.btn-check`, `.focus-ring*`, `[data-bs-theme=*]`).
- `packages/foundation/Enums/ButtonVariant.cs` — enum surface.

Method: (1) queried Context7 `/websites/getbootstrap_5_3` for current BS5 button docs; (2) grepped the compiled skin for every `btn*` selector; (3) compared emitted classes from `BootstrapCssProvider.ButtonClass` against the BS5 feature matrix.

**Key finding up front:** there are **zero `sf-btn` / `sf-button` selectors** in the Bootstrap skin. SunfishButton inherits its look entirely from vanilla `.btn` / `.btn-*-*` rules in the bundled Bootstrap output. That is a deliberate "thin-skin" strategy and is the right baseline — but it also means every BS5 feature that requires extra markup (spinner, icon-label gap, `btn-check` label pattern, split dropdown toggle, `data-bs-theme` scoping) is *not* surfaced through the Sunfish API. See gaps below.

---

## 2. Focus-area coverage table

| BS5 feature | Sunfish API surface | Emits correct BS5 class? | Priority |
|---|---|---|---|
| `.btn` base | Always emitted by `ButtonClass` | Yes | — |
| `.btn-primary/secondary/success/danger/warning/info` | `ButtonVariant` enum | Yes | — |
| `.btn-light` / `.btn-dark` | **No enum value** | No | **P1** |
| `.btn-link` | `FillMode.Link` | Yes | — |
| `.btn-outline-*` (8 colors) | `FillMode.Outline` (6 colors only) | Partial — no light/dark | **P1** |
| `.btn-lg` / `.btn-sm` | `ButtonSize.Large` / `Small` | Yes | — |
| `--bs-btn-padding-*` CSS-var sizing | Not exposed | No | P3 |
| Block / full-width (`d-grid`, `w-100`) | `AdditionalAttributes` only | No first-class support | **P2** |
| `:hover`, `:focus-visible`, `:active` states | Inherited from `.btn` | Yes | — |
| `.focus-ring` helper class | Not emitted | No | P2 |
| `disabled` attribute + `.disabled` class | Both emitted (redundantly, see below) | Partial | **P1** (redundancy) |
| `aria-disabled` for anchor-rendered buttons | Hard-coded `<button>` | N/A | P3 |
| Toggle button (`aria-pressed`, `.active`) | `SunfishToggleButton` exists separately | Partial — no `aria-pressed` on SunfishButton | **P1** |
| `.btn-check` (input+label toggle pattern) | Not supported | No | **P2** |
| `.btn-group` / `.btn-group-vertical` | `ButtonGroupClass()`; no vertical | Partial | **P2** |
| `.btn-toolbar` | Not emitted | No | P3 |
| Split-button / dropdown toggle | `SunfishSplitButton` exists | Partial — no `dropdown-toggle-split` class confirmed | **P2** |
| Icon-only button (square, correct aspect) | `SunfishIconButton` emits `btn-icon sf-bs-icon-button` | No CSS for `.btn-icon` or `.sf-bs-icon-button` in skin | **P0** |
| Icon + label spacing | `.sf-button__icon` span with no CSS | No | **P1** |
| Spinner / loading state | Not supported | No | **P1** |
| `RoundedMode` (`rounded-pill`, `rounded-1/3`) | `FillMode.Solid` + `RoundedMode` | Yes (uses BS5 utilities) | — |
| `FillMode.Flat` / `FillMode.Clear` | Custom composition | Non-standard (see gap G4) | P2 |
| Dark mode (`data-bs-theme="dark"`) | Inherited via CSS vars | Yes (ambient) | — |
| Per-button dark scoping | Not exposed | No | P3 |

---

## 3. Prioritised gap list

### P0 — correctness bugs

#### G1. `SunfishIconButton` emits `.btn-icon` and `.sf-bs-icon-button`, but neither class is defined

- **Sunfish current** (`BootstrapCssProvider.cs:260-264`):

  ```csharp
  public string IconButtonClass(ButtonSize size) =>
      new CssClassBuilder()
          .AddClass("btn btn-icon sf-bs-icon-button")
          .AddClass(BootstrapSize(size), size != ButtonSize.Medium)
          .Build();
  ```

  Grep of `sunfish-bootstrap.css` for `sf-bs-icon-button` and `btn-icon` → **no rules found**. The button therefore renders as a plain `.btn` with rectangular padding, no square aspect, no icon centring.

- **BS5 reference:** BS5 does not ship an icon-only variant; the documented pattern is explicit CSS-var overrides or a custom class. See https://getbootstrap.com/docs/5.3/components/buttons/#sizes (CSS-var approach).

- **Proposed fix:** add to `sunfish-bootstrap.css`:

  ```css
  .btn.sf-bs-icon-button {
    --bs-btn-padding-x: 0.5rem;
    --bs-btn-padding-y: 0.5rem;
    aspect-ratio: 1 / 1;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    line-height: 1;
  }
  .btn-sm.sf-bs-icon-button { --bs-btn-padding-x: .375rem; --bs-btn-padding-y: .375rem; }
  .btn-lg.sf-bs-icon-button { --bs-btn-padding-x: .625rem; --bs-btn-padding-y: .625rem; }
  ```

  Drop the orphan `btn-icon` token from the provider.

---

### P1 — missing BS5 surface / semantic/a11y gaps

#### G2. `ButtonVariant` enum is missing `Light` and `Dark`

- **BS5 reference** (https://getbootstrap.com/docs/5.3/components/buttons/#variants):

  ```html
  <button class="btn btn-light">Light</button>
  <button class="btn btn-dark">Dark</button>
  <button class="btn btn-outline-light">Light</button>
  <button class="btn btn-outline-dark">Dark</button>
  ```

- **Sunfish current** (`packages/foundation/Enums/ButtonVariant.cs`): only Primary, Secondary, Danger, Warning, Info, Success. `BootstrapVariant()` therefore has no way to emit `btn-light` / `btn-dark`. Consumers wanting a neutral toolbar/chrome button have to drop to `AdditionalAttributes="class=btn-light"` and lose the adapter's styling contract.

- **Proposed fix:** add `Light` and `Dark` to `ButtonVariant` (foundation package — breaking only if exhaustive switches exist; compile will catch them). Map them in every adapter's `BootstrapVariant` / `MaterialVariant` / `FluentVariant`. Requires adapter parity review (ADR-style note) per CLAUDE.md "Framework-Agnostic Design Principle".

#### G3. `FillMode.Outline` does not cover all 8 BS5 outline colors

Same root cause as G2 — fixed by fixing `ButtonVariant`. BS5 ships `.btn-outline-light` and `.btn-outline-dark` (confirmed in `sunfish-bootstrap.css:3221` and `:3238`), but Sunfish can't target them.

#### G4. `FillMode.Flat` and `FillMode.Clear` are non-standard compositions

- **Sunfish current** (`BootstrapCssProvider.cs:251-252`):

  ```csharp
  .AddClass($"btn-light border-0 text-{BootstrapVariant(variant)}", fillMode == FillMode.Flat)
  .AddClass("btn-link text-decoration-none", fillMode == FillMode.Clear)
  ```

  `FillMode.Flat` composes three classes with a `text-{variant}` that is a BS5 *utility*, not a button-colour token — hover state reverts to `btn-light` grey and does **not** pick up the variant hue. `FillMode.Clear` overrides `btn-link`'s underline, losing the hover underline affordance documented at https://getbootstrap.com/docs/5.3/components/buttons/#link.

- **Proposed fix:** add a dedicated `.sf-btn-flat` / `.sf-btn-clear` class in the skin that overrides the BS5 button CSS variables (`--bs-btn-color`, `--bs-btn-hover-bg`) per variant, rather than stacking utilities. Pattern from BS5 docs — custom `button-variant` mixin at https://getbootstrap.com/docs/5.3/components/buttons/#sass-mixins.

#### G5. `disabled` attribute and `.disabled` class are both emitted

- **Sunfish current:** razor sets `disabled="@(!Enabled)"` *and* the class builder adds `.AddClass("disabled", isDisabled)`.

- **BS5 reference** (https://getbootstrap.com/docs/5.3/components/buttons/#disabled-state): on `<button>` use the `disabled` attribute. The `.disabled` class is intended for `<a role="button">` where the attribute does not exist. Using both is benign visually but duplicates intent and causes a double-specificity match against `.btn:disabled, .btn.disabled` in the CSS.

- **Proposed fix:** in `ButtonClass(...)` drop `.AddClass("disabled", isDisabled)` when the rendered element is `<button>`; keep it only if/when we support anchor-rendered buttons.

#### G6. No `aria-pressed` on toggle state

- **Sunfish current:** `ToggleButtonClass(bool selected)` swaps `btn-primary` / `btn-outline-primary` but the razor does not add `aria-pressed="@selected"`. BS5 explicitly requires `aria-pressed` for toggle buttons (https://getbootstrap.com/docs/5.3/components/buttons/#toggle-states).

- **Proposed fix:** in `SunfishToggleButton.razor`, emit `aria-pressed="@(Selected.ToString().ToLowerInvariant())"` and add `.active` in addition to swapping variants (BS5 convention keeps the same colour + `.active` + `aria-pressed`).

#### G7. Icon span has no CSS

- **Sunfish current:** `<span class="sf-button__icon">@Icon</span>` — no rule for `sf-button__icon` in the skin, so there is no gap between icon and label. Relies on the caller's icon markup to include trailing whitespace.

- **BS5 reference:** BS5 does not ship icon-with-label button styling, but the typical pattern is a small gap utility. Recommended: use `gap-*` on `.btn`.

- **Proposed fix:**

  ```css
  .btn { /* existing */ }
  .btn:has(.sf-button__icon) { display: inline-flex; align-items: center; gap: 0.5rem; }
  .btn-sm:has(.sf-button__icon) { gap: 0.375rem; }
  ```

  Or wrap the whole template in `d-inline-flex align-items-center gap-2`. Prefer the `:has()` approach so existing button markup isn't forced to flex.

#### G8. No loading / spinner slot

- **BS5 reference** (https://getbootstrap.com/docs/5.3/components/spinners/#buttons):

  ```html
  <button class="btn btn-primary" type="button" disabled>
    <span class="spinner-border spinner-border-sm" aria-hidden="true"></span>
    <span role="status">Loading...</span>
  </button>
  ```

- **Sunfish current:** no `Loading` / `IsBusy` parameter on `SunfishButton`. The Enabled flag is the only disable lever.

- **Proposed fix:** add `[Parameter] public bool Loading { get; set; }`. When true: force disabled, render `<span class="spinner-border spinner-border-sm me-2" aria-hidden="true" role="status"></span>` before `ChildContent`, and preserve focusability semantics (`aria-busy="true"`). Contract belongs in `ui-core`, implementation in each adapter (parity).

---

### P2 — nice-to-have surface

#### G9. No block / full-width API

- **BS5 reference** (https://getbootstrap.com/docs/5.3/components/buttons/#block-buttons): grid-based `d-grid` on a wrapper, or `w-100` on the button for full-width rows.

- **Sunfish current:** must be supplied via `AdditionalAttributes` with `class="w-100"`. No first-class parameter.

- **Proposed fix:** add `[Parameter] public bool FullWidth { get; set; }`; when true emit `w-100`.

#### G10. No `btn-check` pattern

- **BS5 reference** (https://getbootstrap.com/docs/5.3/forms/checks/#toggle-buttons + https://getbootstrap.com/docs/5.3/components/button-group/#checkbox-and-radio-button-groups):

  ```html
  <input type="checkbox" class="btn-check" id="x" autocomplete="off">
  <label class="btn btn-outline-primary" for="x">Toggle</label>
  ```

- **Sunfish current:** `ButtonGroupToggleButton` / `SunfishSegmentedControl` exist but render `<button>` with click handlers, not the accessible `input + label` pair. Loses keyboard semantics (space toggles checkbox, arrow keys navigate radio group).

- **Proposed fix:** refactor `ButtonGroupToggleButton` to the `btn-check` pattern when a parent `SunfishSegmentedControl` requests radio-group semantics. This is a contract change in `ui-core` (needs a `ToggleKind` (Checkbox|Radio) parameter) — warrants its own ICM cycle.

#### G11. No `focus-ring` utility hook

- **BS5 reference** (https://getbootstrap.com/docs/5.3/helpers/focus-ring/): `.focus-ring` helper + `.focus-ring-{variant}` utilities override the box-shadow on `:focus-visible` using `--bs-focus-ring-*` variables. The skin already ships these at lines 6955, 7349-7378 (`.focus-ring-primary` through `.focus-ring-dark`) but nothing in Sunfish emits them.

- **Sunfish current** (`sunfish-bootstrap.css:2950`):

  ```css
  .btn:focus-visible {
    outline: 0;
    box-shadow: var(--bs-btn-box-shadow), var(--bs-btn-focus-box-shadow);
  }
  ```

  Works, but users cannot opt into a thicker / variant-tinted focus ring per button.

- **Proposed fix:** add `[Parameter] public bool FocusRing { get; set; } = true`; emit `focus-ring focus-ring-{variant}` when true (opt-out preserves current behaviour).

#### G12. `ButtonGroupClass` does not cover vertical / toolbar

- **BS5 reference:** `.btn-group-vertical` (https://getbootstrap.com/docs/5.3/components/button-group/#vertical-variation) and `.btn-toolbar` (https://getbootstrap.com/docs/5.3/components/button-group/#button-toolbar) are documented.

- **Sunfish current** (`BootstrapCssProvider.cs:266`): `ButtonGroupClass() => "btn-group"`. No overload for orientation. Grep shows `.btn-group-vertical` rules are compiled in the skin (line 3647) but unreachable via the provider.

- **Proposed fix:** change signature to `ButtonGroupClass(Orientation o)` and add a `SunfishButtonToolbar` component that wraps groups with `.btn-toolbar`.

#### G13. `SunfishSplitButton` missing `dropdown-toggle-split`

- **BS5 reference** (https://getbootstrap.com/docs/5.3/components/button-group/#button-toolbar — split-button section): the *caret-only* button in a split pair must carry `dropdown-toggle dropdown-toggle-split` and a `visually-hidden` label.

- **Sunfish current:** `SplitButtonClass() => "btn-group sf-bs-split-button"`; skin lines 13509-13513 style `:first-child` and `:last-child` rounding but do not add the split-toggle caret spacing. `.btn.sf-bs-split-button` rules not found as a standalone toggle class.

- **Proposed fix:** in `SunfishSplitButton.razor` emit `dropdown-toggle dropdown-toggle-split` on the secondary button and include a `<span class="visually-hidden">Toggle Dropdown</span>` child for screen readers.

---

### P3 — polish / future

- **G14.** Expose CSS-var escape hatch (`--bs-btn-padding-x` etc.) via a strongly-typed `ButtonStyleOverrides` record so consumers can ship custom button variants without raw style strings.
- **G15.** Per-button `data-bs-theme` scoping parameter (`Theme = Dark | Light | Auto`) — BS5 documents scoping at the element level (https://getbootstrap.com/docs/5.3/customize/color-modes/#building-with-sass).
- **G16.** Support anchor-rendered buttons (`As="a"` + `Href`) with automatic `role="button"`, `aria-disabled`, and `tabindex="-1"` on disabled — BS5 pattern at https://getbootstrap.com/docs/5.3/components/buttons/#disabled-state.
- **G17.** Add `Form`-binding richness (`FormAction`, `FormMethod`, `FormEnctype`) to match `<button>` spec and BS5's form integration examples.

---

## 4. Recommended sequencing

1. **P0** (G1) — pure CSS fix in the skin, no API surface changes. Ship in the current minor.
2. **P1 batch**:
   - G2/G3 together (foundation enum + adapter mappings) — requires ADR + parity tests (ui-core contract change).
   - G5 + G6 + G7 + G8 — Blazor-adapter changes, parallel React work.
   - G4 — skin CSS + provider refactor.
3. **P2 batch** gated by a `sunfish-feature-change` pipeline: G9, G10, G11, G12, G13 — each adds public surface.
4. **P3** — defer to v1 roadmap.

## 5. Adapter parity notes

Per CLAUDE.md "Adapter Parity" rule, any change in `BootstrapCssProvider` that alters the emitted class set must have matching work in `MaterialCssProvider` and `FluentUICssProvider`. Specifically:

- G2/G3 (enum growth): Material must map `Light`/`Dark` to the correct MUI palette tones; Fluent must map to its neutral foreground tokens.
- G8 (loading slot): contract added in `ui-core`, implemented in all three adapters.
- G9 (`FullWidth`), G11 (`FocusRing`): pure class/attribute work, parity straightforward.

## 6. References

All links are to Bootstrap 5.3 docs (verified via Context7 `/websites/getbootstrap_5_3`):

- Buttons overview, variants, sizes, outline, disabled, block, toggle: https://getbootstrap.com/docs/5.3/components/buttons/
- Button group, vertical, toolbar, split dropdown, checkbox/radio groups: https://getbootstrap.com/docs/5.3/components/button-group/
- `btn-check` toggle pattern: https://getbootstrap.com/docs/5.3/forms/checks/#toggle-buttons
- Focus-ring helper: https://getbootstrap.com/docs/5.3/helpers/focus-ring/
- Spinners in buttons: https://getbootstrap.com/docs/5.3/components/spinners/#buttons
- Color modes / `data-bs-theme`: https://getbootstrap.com/docs/5.3/customize/color-modes/

---

*End of audit. Word count ≈ 1,650.*
