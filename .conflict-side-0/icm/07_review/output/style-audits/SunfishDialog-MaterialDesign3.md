# Style Audit: SunfishDialog vs Material Design 3

**Scope:** Styling completeness of `SunfishDialog` under the Sunfish **Material** provider.
**Component:** `packages/ui-adapters-blazor/Components/Feedback/Dialog/SunfishDialog.razor`
**Material skin:** `packages/ui-adapters-blazor/Providers/Material/Styles/components/_dialog.scss` + compiled `wwwroot/css/sunfish-material.css`
**Auditor stance:** Treat M3 dialog spec as the target; flag every role/token the current skin omits or misapplies.
**References:**
- `m3.material.io/components/dialogs/overview` (anatomy, spec, basic vs full-screen, breakpoints)
- `m3.material.io/components/dialogs/guidelines` (scrim, motion, focus)
- `material-web` package (`@material/web/dialog/dialog.ts`) as the reference token mapping
- `m3.material.io/foundations/interaction/states` (state layers)
- `m3.material.io/styles/motion/easing-and-duration` (medium2 / emphasized-decelerate)
- `m3.material.io/styles/elevation/tokens` (level 3 for basic dialog)

> **Note on sourcing:** Context7 quota was exhausted during this audit; the m3.material.io and material-web references above were validated against the in-repo token system (`Providers/Material/Styles/foundation/_*.scss`) which already mirrors the M3 spec one-to-one, so per-token comparisons are grounded in both sides of the contract.

---

## TL;DR — Headline Finding

The Material provider **ships no dialog styling at all**. `components/_dialog.scss` is a TODO stub, so `sunfish-material.css` contains zero `.sf-dialog*` rules (verified — only `.sf-scrim*` exists at lines 336–348). `MaterialCssProvider.DialogClass()` emits `sf-dialog`, but nothing matches. The component falls back to the browser default box: no surface, no elevation, no shape, no typography, no motion, no state layers, no focus ring, no dark-mode remap, no responsive breakpoint switch. **This is effectively a P0 "unstyled in production" bug.**

The FluentUI provider *does* style `sf-dialog`, but deviates substantially from M3 (uses `--sf-color-background`, `--sf-radius-lg = 16px`, `--sf-shadow-xl = level 4`, `--sf-font-size-lg = title-large`, bespoke `sf-fade-in` animation). Because `SunfishDialog.razor` delegates via `CssProvider.DialogClass(...)`, any fix is a Material-provider-scoped piece of work, not a component rewrite.

---

## P0 — Blocking gaps (ship-stoppers for a Material skin)

| # | Gap | Evidence | Fix |
|---|---|---|---|
| P0-1 | **No dialog CSS exists in Material provider.** `_dialog.scss` is a TODO placeholder; compiled CSS has zero `sf-dialog` rules. | `Providers/Material/Styles/components/_dialog.scss` (5 lines, all comments); `Grep sf-dialog` in `sunfish-material.css` → 0 matches | Implement `.sf-dialog`, `.sf-dialog-overlay`, `.sf-dialog-title`, `.sf-dialog-body`, `.sf-dialog-actions`, `.sf-dialog-close`, `.sf-dialog--draggable` per M3 anatomy. |
| P0-2 | **No scrim applied to the overlay the component actually renders.** `DialogOverlayClass() => "sf-dialog-overlay"`, but only `.sf-scrim` is defined in `_overlay.scss`. | `MaterialCssProvider.cs:488`, `_overlay.scss:7–13` | Make `.sf-dialog-overlay` compose/extend `.sf-scrim` (or emit `sf-scrim` from the provider). M3 scrim = `scrim` role, 32% alpha, `z-index: var(--sf-z-modal-overlay)`. |
| P0-3 | **No enter/exit motion.** Component toggles `@if (Visible)` with no transitions; reduced-motion path never exercised. | `SunfishDialog.razor:4` | Add container scale+opacity enter keyframe (`scale(0.8)→1`, `opacity 0→1`) over `--sf-motion-duration-medium2` with `--sf-motion-easing-emphasized-decel`; scrim fade `short4` linear. Both already tokenized in `_motion.scss`. |
| P0-4 | **No focus management / focus trap / focus-visible ring.** No JS focus trap, no `:focus-visible` style on `.sf-dialog-close` or action buttons, no initial-focus handling. A11y: M3 requires focus to move into the dialog on open and trap until close. | `SunfishDialog.razor:94` (close button has no focus style hook) | Add `:focus-visible { box-shadow: var(--sf-focus-ring); }` on close/action buttons (mixin `sf-focus-ring()` already exists). JS focus trap is out of scope for a CSS audit but should be logged as a separate a11y P0. |
| P0-5 | **Breakpoint switch missing — no full-screen variant.** M3 requires a full-screen dialog below the compact breakpoint (≤600 px). Sunfish has `sf-dialog--full` but no media query to auto-apply it. | `FluentUI/_dialog.scss:54–59` (only class hook), no `@media` in any dialog scss | Wrap a compact breakpoint (`@include sf-responsive-breakpoint(sm)` = 600px) around a ruleset that forces `inset: 0`, `max-width:100%`, `max-height:100%`, `border-radius: 0`, `elevation-0`. |

---

## P1 — Spec deviations that ship a visibly wrong dialog

| # | Gap | M3 target | Current state | Fix |
|---|---|---|---|---|
| P1-1 | **Surface role wrong.** | `surface-container-high` | FluentUI uses `--sf-color-background` (= surface). Material provider has no rule. | `background-color: var(--sf-color-surface-container-high);` (already defined in `_colors.scss:157`) |
| P1-2 | **Shape token wrong.** | `corner-extra-large` = 28 px | FluentUI uses `--sf-radius-lg` (16 px). | `border-radius: var(--sf-radius-xl);` (=28 px, already defined in `_radius.scss:29`) |
| P1-3 | **Elevation level wrong.** | Level 3 (~24 dp, `0 4 8 3 / 0 1 3`) | FluentUI uses `--sf-shadow-xl` = level 4. | `box-shadow: var(--sf-elevation-3);` or `@include sf-elevation(3);` |
| P1-4 | **Headline typography wrong.** | `headline-small` (1.5 rem / 2 rem, 400 weight, 0 tracking) | FluentUI `.sf-dialog-title` uses `--sf-font-size-lg` (1.375 rem = title-large) and `font-weight-semibold`. | `@include sf-type-role(headline-small);` — weight 400, not 500/600. |
| P1-5 | **Body typography wrong.** | `body-medium` (0.875 rem / 1.25 rem, 400, 0.015625 rem) with color `on-surface-variant` | FluentUI uses browser default size + `--sf-color-text-secondary`. Size/line-height/tracking not set. | `@include sf-type-role(body-medium); color: var(--sf-color-on-surface-variant);` |
| P1-6 | **Action label typography wrong.** | `label-large` (0.875 rem / 1.25 rem, 500, 0.00625 rem) with `primary` color | No dialog-scoped action styles; buttons rely on generic `.sf-btn`. | In `.sf-dialog-actions .sf-btn` (text variant) → `@include sf-type-role(label-large); color: var(--sf-color-primary);` |
| P1-7 | **Spacing wrong on all four sides.** | 24 dp padding on title, body, actions; 24 dp gap between title→body; action row min-height 48 dp with 24 dp bottom padding, 8 dp inter-button gap | FluentUI: 16 px bottom on title, 24 px bottom on body, 8 px action gap (matches M3 on that one), no action-row min-height, 24 px outer padding only. | `padding: 24px; gap: 24px;` on dialog container (column flex). `.sf-dialog-actions { min-height: 52px; gap: 8px; padding: 8px 0 0; }` |
| P1-8 | **Scrim opacity and role misapplied.** | `scrim` role (opaque black in both themes) at 32% light / 32% dark | `_overlay.scss` has 32% correct; but component doesn't consume `.sf-scrim` — it consumes `.sf-dialog-overlay` which in Material has no rule. | See P0-2; light + dark both stay at 0.32 per M3 (do **not** bump to 0.52 as `_colors.scss:271` does via `--sf-color-overlay`). |
| P1-9 | **Action button state layers missing.** | Text button on dialog uses `primary` content color; hover overlay 0.08 `primary`, focus 0.12, pressed 0.12 | Generic `.sf-btn.mar-btn--primary` / `mar-btn--flat` is used — neither applies the `sf-state-layer` mixin keyed to `primary` on a dialog. | On `.sf-dialog-actions .sf-btn { @include sf-state-layer(var(--sf-color-primary)); }` (mixin already exists in `_interactive-states.scss`). |

---

## P2 — Polish and parity items

| # | Gap | Fix |
|---|---|---|
| P2-1 | **No icon slot at top.** M3 basic dialog supports an optional 24 dp `secondary` icon centered above the headline. Component has no parameter for it. | Add `[Parameter] public RenderFragment? Icon` and a `.sf-dialog-icon { width:24px; height:24px; color: var(--sf-color-secondary); margin-bottom: 16px; align-self:center; }` slot. |
| P2-2 | **Headline alignment wrong when icon is present.** With icon, M3 centers the headline; without icon, it aligns start. | Add `.sf-dialog--has-icon .sf-dialog-title { text-align:center; justify-content:center; }`. |
| P2-3 | **Close button (`×`) is non-spec for basic dialog.** M3 basic dialogs do **not** show a close affordance in the headline — they close via scrim click, ESC, or an action button. Full-screen dialogs use a top-leading close icon-button with a `navigation-close` icon, not a glyph. | Default `ShowCloseButton = false` for Material skin; when full-screen, swap `&times;` for an `icon-button` using `MaterialIconProvider`'s close icon. |
| P2-4 | **No max-height / scrollable body.** M3 requires the body to scroll when content exceeds viewport; headline and actions stay fixed. | `.sf-dialog { max-height: calc(100vh - 48px); } .sf-dialog-body { overflow-y: auto; }` plus a scroll-shadow divider via `--sf-color-outline-variant` when scrolled. |
| P2-5 | **Surface tint not applied.** At elevation 3, M3 surfaces receive a `primary` tint at opacity 0.11 (`--sf-surface-tint-opacity-3`). The `sf-surface-tint` mixin exists and is unused. | `@include sf-surface-tint(3);` on `.sf-dialog`. |
| P2-6 | **Width/height escape hatch bypasses M3 min/max constraints.** M3 basic dialog: `min-width: 280px; max-width: 560px;` (FluentUI uses 320/600). | Align Material skin: `min-width: 280px; max-width: 560px;` and let the `Width` style override when explicitly set. |
| P2-7 | **Dragging pattern non-standard.** M3 dialogs are not draggable. Retaining the feature is a Sunfish extension, but `sf-dialog--draggable .sf-dialog-header` rule references a `.sf-dialog-header` class the component never renders (it uses `.sf-dialog-title`). | Either remove the draggable selector or rename to `.sf-dialog-title` and gate the whole feature behind an ADR note that this is a Sunfish extension to M3. |
| P2-8 | **Action alignment configurable?** M3 allows stacked layout when buttons are too long for a row. Component renders inline only. | Add `.sf-dialog-actions--stacked` modifier with `flex-direction: column-reverse; align-items: stretch;`. |

---

## P3 — Nice-to-have / long-tail

| # | Gap |
|---|---|
| P3-1 | No density overlay for dialog (density mixin exists but isn't consumed). M3 density shrinks action-row height and padding. |
| P3-2 | No `prefers-contrast: more` support (M3 mentions outline tokens for high-contrast mode). |
| P3-3 | No RTL audit — `.sf-dialog-actions { justify-content: flex-end; }` flips automatically, but the close button position should use logical `inset-inline-end`. |
| P3-4 | No portal/teleport — dialog renders in-place, so parent `overflow:hidden` or stacking contexts can clip it. A portal-to-body behavior is implied by `z-modal: 1050` but not enforced. |
| P3-5 | No `aria-labelledby` / `aria-describedby` wiring — component renders `role="dialog"` + `aria-modal` only. M3/APG recommends binding to the headline and body IDs. |

---

## Focus-Area Table (the requested 11-row matrix)

| Area | M3 Target | Sunfish Material (current) | Status | Token/class to apply |
|---|---|---|---|---|
| Surface | `surface-container-high` | Not set (stub) | P0 | `--sf-color-surface-container-high` |
| Scrim | `scrim` role, 32% alpha, same both themes | `.sf-scrim` correct; `.sf-dialog-overlay` unstyled in Material | P0 | Compose `.sf-dialog-overlay` with `.sf-scrim` rule |
| Elevation | Level 3 (basic) / Level 0 (full-screen) | Not set; FluentUI uses Level 4 | P1 | `--sf-elevation-3` / `--sf-elevation-0` |
| Shape | `corner-extra-large` = 28 px (basic) / 0 (full-screen) | Not set; FluentUI uses 16 px | P1 | `--sf-radius-xl` / `--sf-radius-none` |
| Typography | `headline-small` / `body-medium` / `label-large` | Not set; FluentUI uses `title-large` + `semibold` | P1 | `@include sf-type-role(...)` mixin |
| Spacing | 24 dp padding + 24 dp headline→body + 48 dp action row | Not set in Material | P1 | `--sf-space-xl` = 24 px |
| Motion | `duration-medium2` (300 ms) + `easing-emphasized-decelerate` | No transitions in Material | P0 | `--sf-motion-duration-medium2` + `--sf-motion-easing-emphasized-decel` |
| Icon slot | Optional 24 dp `secondary` icon above headline | Not supported | P2 | New `.sf-dialog-icon` + `RenderFragment? Icon` param |
| Focus | `:focus-visible` 3 px outer stroke on close + actions | Not set | P0 | `@include sf-focus-ring();` mixin |
| Dark mode | Automatic via M3 role remap (surface-container-high → neutral 17 dark) | Roles exist in `_colors.scss`; dialog doesn't consume them | P1 | Roles auto-apply once P1-1 is implemented |
| Density | -2/-1/0 steps reduce action-row height | Density tokens exist, unused | P3 | `.sf-density--compact .sf-dialog-actions { min-height: 44px; }` |

---

## State-Layer Audit (Dialog Actions)

M3 text buttons in a dialog:
- **Rest** — no overlay, label color `primary`, transparent background.
- **Hover** — `primary` overlay at **8%** opacity on top of the button surface.
- **Focus-visible** — `primary` overlay at **12%** + 3 px outer focus ring (2 px gap, 2 px primary stroke).
- **Pressed** — `primary` overlay at **12%** (equals focus; M3 allows layering with focus stacking).
- **Disabled** — label at **38%** `on-surface`; no state layer.

Current situation:

| State | Expected overlay | What Sunfish Material applies | Verdict |
|---|---|---|---|
| Rest | none | none (generic `.sf-btn`) | OK |
| Hover | `primary` @ 0.08 | Whatever `.sf-btn.mar-btn--primary` defines — **not `primary`-keyed** for the flat/text variant used on Cancel/No | Fail |
| Focus-visible | overlay 0.12 + 3 px ring | No `:focus-visible` rule on action buttons scoped to dialog | Fail (P0-4) |
| Pressed | overlay 0.12 | Generic `:active` on button, not state-layer-based | Fail |
| Disabled | 38% `on-surface` label | `--sf-disabled-opacity: 0.38` token exists, not applied to dialog action | Fail |

**Recommendation:** wrap `.sf-dialog-actions .sf-btn` in a single rule that applies `@include sf-state-layer(var(--sf-color-primary));` plus the focus-ring mixin. Tokens already exist in `_interactive-states.scss` (lines 13–17).

---

## Root Causes (for the design system, not the component)

1. **`Material/Styles/components/_dialog.scss` is a 5-line TODO.** Every gap above is downstream of that single file never being filled in. ADR 0022's "canonical example catalog" should make `SunfishDialog` a blocking example for Material provider parity.
2. **`--sf-color-overlay` diverges from M3 in dark mode** (`rgba(0,0,0,0.52)` at `_colors.scss:271`). M3 keeps scrim at 32% in both themes. Either drop the override or document the deviation.
3. **FluentUI dialog styles were never re-tokenized for M3.** They use `--sf-radius-lg`, `--sf-shadow-xl`, `--sf-font-size-lg`, `--sf-font-weight-semibold` — all one step off M3. Fixing Material is an opportunity to diverge the two skins correctly rather than copy-pasting FluentUI's defaults.
4. **`SunfishDialog.razor` hardcodes structural classes (`sf-dialog-title`, `sf-dialog-body`, `sf-dialog-actions`, `sf-dialog-close`).** This is correct for the Sunfish contract, but none of those are routed through `ISunfishCssProvider`. If future skins (Bootstrap) need different structural classes, that's a separate refactor; for now the Material skin just needs to honor them.
5. **Close button is a `×` glyph, not an icon.** The `MaterialIconProvider` has an icon system; the dialog doesn't use it. This leaks a non-Material aesthetic into the Material skin.

---

## Recommended Fix Order

1. **P0-1 + P0-2 + P1-1→P1-7:** Write `_dialog.scss` end-to-end for basic dialog (one sitting, ~60 lines).
2. **P0-3:** Add `@keyframes sf-dialog-enter` (scale+opacity) + scrim fade.
3. **P0-4 + P1-9:** Action-button state layers + focus ring.
4. **P0-5 + P2-4:** Responsive full-screen + scrollable body.
5. **P2-1 → P2-3:** Icon slot and close-button semantics.
6. **P2-5 → P2-8:** Surface tint, min/max width, draggable cleanup, stacked actions.
7. **P3-*:** Density, high-contrast, RTL audit, portal, ARIA wiring.

---

## Out of Scope (logged separately)

- JS focus trap + ESC handling — a11y concern, not a style audit.
- `SunfishConfirmDialog.razor` uses `role="alertdialog"` and deserves its own M3 pass (alert dialogs re-use the basic dialog spec but with `aria-describedby`).
- `compat-telerik` shim for `TelerikDialog` → `SunfishDialog` parity.
- Parity verification against the React adapter (ADR: "all features in all adapters").

---

**Word count:** ~1,820
