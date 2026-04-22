# Style Audit: SunfishDialog vs Fluent UI v9 Dialog

**Component:** `SunfishDialog` (Blazor) with FluentUI skin
**Reference:** Fluent UI React v9 `Dialog` — https://react.fluentui.dev/?path=/docs/components-dialog--docs
**Audit date:** 2026-04-22
**Scope:** Styling completeness only — surface, backdrop, elevation, typography, spacing, motion, focus, dark mode. Behavioral parity (focus trap, inert, escape handling) is flagged where it couples with visual state but is not the primary subject.

---

## 1. Source references

- Razor component: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Feedback\Dialog\SunfishDialog.razor`
- Fluent skin CSS block: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Providers\FluentUI\wwwroot\css\sunfish-fluentui.css` lines 3608–3681, plus the shared overlay block at lines 308–315
- CSS provider mapping: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Providers\FluentUI\FluentUICssProvider.cs` lines 481–489 (`DialogClass`, `DialogOverlayClass`)
- Token roots: same CSS file, `:root` lines 1–117 (light) and `[data-sf-theme=dark]` lines 119–147 / 231–236 (dark shadows) / 1050–1053 (dark overrides)

Fluent v9 token definitions cited from `react.fluentui.dev` (Dialog docs, Spacing docs, Border Radii docs, Typography docs).

---

## 2. Structural parity

Fluent v9 exposes these slots on the Dialog tree: `DialogTrigger`, `DialogSurface`, `DialogBody`, `DialogTitle`, `DialogContent`, `DialogActions`. `DialogSurface` owns the backdrop via its `backdrop` slot and accepts `modalType = "modal" | "non-modal" | "alert"`, plus a `surfaceMotion` slot for enter/exit animation.

SunfishDialog renders:

```
.sf-dialog-overlay  (only when Modal == true)
  .sf-dialog
    .sf-dialog-title   (flex row with title + close button)
    .sf-dialog-body    (DialogContent or ChildContent)
    .sf-dialog-actions (DialogActions RF or enum-driven buttons)
```

Structural gaps vs Fluent v9:

- No distinct `DialogBody` wrapper between title and content — Sunfish collapses body+content into `.sf-dialog-body`. Fluent v9 uses `DialogBody` as a CSS grid container whose rows are title / content / actions; this is what gives Fluent its consistent vertical rhythm across dialogs with and without titles.
- No `modalType="alert"` distinction. Sunfish uses `role="dialog"` (main) and `role="alertdialog"` (confirm variant lives in `SunfishConfirmDialog.razor`), but there is no visual differentiation (icon lane, stronger focus stroke) beyond role.
- No `DialogTrigger` abstraction. That is fine — this is a shell-level concern, not a styling gap.
- No surface-scoped motion slot; motion is baked into the `.sf-dialog` element via `animation: sf-fade-in`.

---

## 3. Focus-area comparison

| Focus area | Fluent v9 target | Sunfish current | Verdict |
|---|---|---|---|
| Surface background | `colorNeutralBackground1` (#ffffff light / #292929 dark) | `--sf-color-background` (#ffffff / #1b1a19 dark) | Close; dark is slightly blacker than Fluent |
| Surface radius | `borderRadiusXLarge` (8px) | `--sf-radius-lg` (8px) | Match |
| Surface elevation | `shadow64` — two-layer, 64px offset (`0 0 2px rgba(0,0,0,.2), 0 64px 128px rgba(0,0,0,.24)`) exposed as `--elevation-shadow-dialog` | `--sf-shadow-xl` (`0 25.6px 57.6px /.22, 0 4.8px 14.4px /.18`) | Mismatch — Sunfish uses a generic xl shadow, not the dialog-tier shadow64. Token `--elevation-shadow-dialog` is defined (line 224) but not consumed |
| Surface padding | `spacingVerticalXXL` (24px) top/bottom, `spacingHorizontalXXL` (24px) sides on `DialogBody`; content grid rows use `spacingVerticalS`/`M` gaps | `var(--sf-space-xl)` = 24px, applied as uniform padding on `.sf-dialog` | Match on total, but Sunfish padding is on the surface root and body margins are ad-hoc; Fluent applies it via the body grid |
| Title typography | `fontSize: fontSizeBase500` (20px) / `lineHeight: lineHeightBase500` (28px) / `fontWeight: fontWeightSemibold` (600) | `font-size: var(--sf-font-size-lg)` = 1.25rem = 20px, `font-weight: var(--sf-font-weight-semibold)` = 600; no explicit line-height | Size + weight match; line-height gap (inherits body line-height 1.5 → ~30px, close but not 28px exact) |
| Title color | `colorNeutralForeground1` | Inherits `--sf-color-on-background` via `.sf-dialog` | Acceptable; no explicit rule means it drifts when parent color changes |
| Body text color | `colorNeutralForeground2` | `var(--sf-color-text-secondary)` (#605e5c light) | Match semantically |
| Actions layout | Flex row, gap `spacingHorizontalS` (8px), justify end, `spacingVerticalL` (16px) top margin | `display: flex; justify-content: flex-end; gap: var(--sf-space-sm)` (8px); top spacing comes from `.sf-dialog-body` margin-bottom of 24px (`--sf-space-xl`) | Gap + alignment match. Top spacing is 24px vs Fluent 16px → minor inconsistency |
| Backdrop color | `colorBackgroundOverlay` = `rgba(0,0,0,0.4)` light, `rgba(0,0,0,0.4)` dark (Fluent keeps dim same, relies on surface contrast) | `--sf-color-overlay` = `rgba(0,0,0,0.4)` light, `rgba(0,0,0,0.6)` dark | Light matches; Sunfish dark is 50 % darker than Fluent (Fluent v9 does not darken the overlay in dark theme) |
| Backdrop animation | Fade via `surfaceMotion` enter/exit atoms on opacity | `animation: sf-fade-in var(--sf-transition-fast)` (120 ms) | Direction matches; no exit animation because component is removed via `@if (Visible)` with no presence component |
| Surface motion | Enter: opacity 0 → 1, translateY small-to-zero, `motion-duration-normal` (200 ms) with decelerate easing; exit: reverse with accelerate easing | `animation: sf-fade-in var(--sf-transition-normal)` (200 ms ease) — only opacity, no translate, no exit | Partial — duration matches, easing curve is default `ease` (not decelerate), no enter translate, no exit animation |
| Focus management (visual) | Focus ring via `createCustomFocusIndicatorStyle` on DialogSurface and actions; 2 px outer ring with `focusStrokeOuter`/`focusStrokeInner` | `--sf-focus-ring: 0 0 0 2px var(--sf-color-background), 0 0 0 4px var(--sf-color-primary)` defined but never applied to `.sf-dialog`, `.sf-dialog-close`, or action buttons | Gap — token exists, zero consumers in the dialog tree |
| Close button (title bar) | `Button appearance="subtle" icon={<Dismiss24Regular />}`, 32×32 hit target, hover = `colorSubtleBackgroundHover` | `.sf-dialog-close` — no CSS rule at all; relies on UA default button styling for `&times;` glyph | Major gap — unstyled close button |
| Dark theme surface | `colorNeutralBackground1` swaps to #292929; `shadow64` retains same `rgba(0,0,0,.24)` alpha | `--sf-color-background` → #1b1a19; `--sf-shadow-xl` raised to 0.55/0.45 alpha | Reasonable but not Fluent-accurate. Fluent does not re-alpha the shadow in dark; it relies on layered opaque tones |
| Sizing | Fluent surface: `max-width: 600px`, responsive down to `calc(100vw - spacingHorizontalXL*2)`, single size; width controlled by caller | Size modifier classes `.sf-dialog--small|medium|large|full` targeting `.sf-dialog__panel` selector | Gap — modifier classes target `.sf-dialog__panel` but the component emits `.sf-dialog` (no `__panel` child). Size variants are dead selectors today |
| Scroll behaviour | Body is `overflow-y: auto` with `max-height: calc(100vh - spacingVerticalXXXL*2)` so long content scrolls inside the surface | No max-height, no overflow — long content overflows the viewport | Gap — accessibility + UX regression on small viewports |
| Stacking | Fluent renders DialogSurface into a Portal; backdrop z-index is managed relative to other portals | `--sf-z-modal-overlay: 1040`, `--sf-z-modal: 1050` as static z-index tokens; no portaling | Acceptable for Blazor; static z layering is fine |

---

## 4. Prioritized gap list

### P0 — Fix before claiming Fluent parity

1. **Surface elevation uses wrong shadow.** `.sf-dialog` (line 3615) applies `var(--sf-shadow-xl)` but the file already defines `--elevation-shadow-dialog` (line 224) that matches Fluent's `shadow64`. Swap the var to consume the dialog-specific token.
2. **Size modifier classes are dead selectors.** `.sf-dialog--small .sf-dialog__panel` (lines 3664–3681) will never match because the Razor component emits `.sf-dialog` with no `__panel` child element. Either (a) wrap the inner surface in `<div class="sf-dialog__panel">` and put `max-width`, `padding`, `box-shadow`, `background`, `animation` on the panel while leaving `.sf-dialog` as a centering wrapper, or (b) rewrite the size modifiers to target `.sf-dialog--small` directly. Option (a) is closer to Fluent's surface-as-inner-element model and lets the outer `.sf-dialog` act like a flex container for centering.
3. **Overflow handling is missing.** Add `max-height: calc(100vh - var(--sf-space-3xl) * 2)` and `overflow: auto` (or split into `.sf-dialog-body { overflow: auto; }` with flex column on the surface). Without this, content longer than the viewport silently clips or forces page scroll on the backdrop layer.
4. **Close button is unstyled.** `.sf-dialog-close` has no CSS rule. Fluent renders this as a 32×32 subtle icon button with hover + focus states. Add a rule block with explicit size, padding, hover (`colorSubtleBackgroundHover` → `var(--sf-color-surface-hover)`), focus ring, and absolute positioning inside `.sf-dialog-title` rather than title flex.

### P1 — Visible parity gaps

5. **No exit animation.** The Razor uses `@if (Visible) { ... }` which unmounts immediately. Fluent v9 uses `surfaceMotion` with distinct enter/exit atoms and accelerate easing on exit. Add a `data-sf-state="open|closing"` attribute, keep the node rendered for one animation frame on close, and define `@keyframes sf-dialog-exit` (opacity + translateY, 150 ms accelerate). Also factor the enter keyframe into a dialog-specific `sf-dialog-enter` that adds a 10-px translateY so the surface rises into place rather than only fading.
6. **Backdrop dark theme is too dark.** `--sf-color-overlay` jumps from 0.4 to 0.6 alpha in dark theme (line 146). Fluent v9 keeps `colorBackgroundOverlay` at 0.4 in both themes and lets surface contrast carry the work. Drop the dark override (or reduce to 0.5 max) to match.
7. **Focus ring not wired.** `--sf-focus-ring` exists but no dialog selector uses it. Add `:focus-visible` rules to `.sf-dialog-close` and to `.sf-dialog-actions button`. Consider a surface-level `:focus-visible` ring on the dialog itself when `role=dialog` is the initial focus target and contains no focusable children (Fluent's NoFocusableElement story).
8. **Title line-height not set.** Fluent pairs `fontSizeBase500` with `lineHeightBase500` (20/28). Sunfish sets `font-size: var(--sf-font-size-lg)` without an explicit line-height, so the title inherits `--sf-line-height-base` (1.5 → 30 px). Add `line-height: 1.4` (or compute `28 / 20 = 1.4`) to the `.sf-dialog-title` rule, or introduce `--sf-line-height-lg: 1.4` and use it.
9. **No body/grid separation.** Fluent's `DialogBody` is a CSS grid with `grid-template-rows: auto 1fr auto` so long content pushes actions to the bottom of a fixed-height surface. Sunfish stacks three block elements with margins. Add `display: flex; flex-direction: column` to `.sf-dialog` with `flex: 1 1 auto` on `.sf-dialog-body` (prereq: P0 #3).

### P2 — Secondary polish

10. **Actions top spacing off by 8 px.** Fluent puts 16 px between content and actions; Sunfish puts 24 px via `.sf-dialog-body { margin-bottom: var(--sf-space-xl) }`. Change to `--sf-space-lg` (16 px) or move spacing onto `.sf-dialog-actions { margin-top: var(--sf-space-lg) }` and drop the body margin-bottom.
11. **Missing alert variant styling.** `SunfishConfirmDialog` uses `role="alertdialog"` but shares `.sf-dialog` skin. Fluent's alert variant is visually indistinguishable from modal by default but gets a stronger focus management contract. If Sunfish is going to ship an alert-style visual (e.g., severity icon lane), add `.sf-dialog--alert` modifier hooks now.
12. **No reduced-motion handling.** Fluent wraps its motion atoms in `prefers-reduced-motion` short-circuits. Add `@media (prefers-reduced-motion: reduce) { .sf-dialog, .sf-dialog-overlay { animation: none; } }` near the keyframes block (line 1055).
13. **Draggable header selector is stale.** `.sf-dialog--draggable .sf-dialog-header` (line 3655) targets `.sf-dialog-header`, but the component emits `.sf-dialog-title` — draggable will never activate. Either rename the CSS selector to `.sf-dialog-title` under the draggable modifier, or emit a `.sf-dialog-header` wrapper when `Draggable == true`.
14. **Title does not space the close button.** `.sf-dialog-title` sets `gap: var(--sf-space-md)` between the `<span>` title and the close button, but no `justify-content: space-between`. Works today because the span takes intrinsic width and the button is sibling 2, so they end up side-by-side with a 12-px gap rather than title-left / close-right. Fluent positions the close button flush-right via grid placement.

### P3 — Opportunistic improvements

15. **Z-index consolidation.** `--sf-z-modal-overlay` (1040) and `--sf-z-modal` (1050) are used, but `--sf-z-modal` is not referenced in any `.sf-dialog` rule — it's set via the inline `z-index: var(--sf-z-modal)` on line 3619. Keep as-is, but note that drawers and toasts (`--sf-z-toast: 1080`) can render above an open dialog, which Fluent avoids via portal layering.
16. **Document the overlay-click behaviour in CSS.** `CloseOnOverlayClick` is true by default — Fluent v9 requires an explicit opt-in and differentiates modal from alert. Consider a data attribute like `data-sf-dismiss-on-click="false"` that CSS can style (e.g., subtly different cursor) so the behaviour is discoverable visually.

---

## 5. Token mapping table

| Fluent v9 token | Fluent value | Sunfish token (current) | Sunfish value | CSS location | Action |
|---|---|---|---|---|---|
| `colorNeutralBackground1` | `#ffffff` / `#292929` | `--sf-color-background` | `#ffffff` / `#1b1a19` | `sunfish-fluentui.css:22, 137` | Consider nudging dark to `#292929` for closer Fluent parity |
| `colorBackgroundOverlay` | `rgba(0,0,0,0.4)` (both themes) | `--sf-color-overlay` | `rgba(0,0,0,0.4)` / `rgba(0,0,0,0.6)` | `:31, :146` | P1 #6 — drop dark override or reduce to 0.5 |
| `borderRadiusXLarge` | 8px | `--sf-radius-lg` | 8px | `:205` | Match |
| `shadow64` (`--elevation-shadow-dialog`) | `0 0 2px rgba(0,0,0,.2), 0 64px 128px rgba(0,0,0,.24)` | `--elevation-shadow-dialog` defined at `:224`, unused | Same value already defined | `:224` | P0 #1 — switch `.sf-dialog` box-shadow to `var(--elevation-shadow-dialog)` |
| `fontSizeBase500` | 20px | `--sf-font-size-lg` | 1.25rem (20px) | `:167` | Match |
| `lineHeightBase500` | 28px (1.4) | none | inherits 1.5 | — | P1 #8 — add explicit line-height |
| `fontWeightSemibold` | 600 | `--sf-font-weight-semibold` | 600 | `:173` | Match |
| `spacingVerticalXXL` / `spacingHorizontalXXL` (body padding) | 24px | `--sf-space-xl` | 24px | `:155` | Match |
| `spacingVerticalL` (actions top gap) | 16px | (not applied) | body bottom margin 24px | `:3641` | P2 #10 — swap to `var(--sf-space-lg)` |
| `spacingHorizontalS` (actions gap) | 8px | `--sf-space-sm` | 8px | `:152` | Match |
| `durationNormal` | 200ms | `--sf-transition-normal` | 200ms ease | `:241` | Duration match; easing is default `ease`, Fluent prefers `decelerate` on enter |
| `curveDecelerateMid` | `cubic-bezier(0.1, 0.9, 0.2, 1)` | (none) | — | — | P1 #5 — add `--sf-ease-decelerate`/`--sf-ease-accelerate` tokens and use them in the dialog keyframe |
| `focusStrokeOuter` / `focusStrokeWidth` | `#000`, 2px | `--sf-focus-ring` | `0 0 0 2px bg, 0 0 0 4px primary` | `:255` | P1 #7 — wire into close + action `:focus-visible` rules |
| `colorSubtleBackgroundHover` | `#f5f5f5` | `--sf-color-surface-hover` | `#edebe9` / `#323130` | `:24, :139` | Usable for close button hover (P0 #4) |

---

## 6. Suggested follow-on work

1. Raise a build ticket under pipeline variant **sunfish-feature-change** scoped to "SunfishDialog Fluent parity — P0 items 1–4". This is a visible regression on kitchen-sink and docs, so stage 06 deliverables apply (kitchen-sink demo, apps/docs update).
2. Add a `.sf-dialog__panel` refactor to `SunfishDialog.razor` so the existing size modifiers activate without rewriting the modifier selectors — cleaner than deleting the variant classes.
3. Capture a visual baseline with `openwolf designqc` against a Fluent v9 Storybook dialog at the same viewport and diff before/after. Target deltas: shadow edge, dark backdrop alpha, close button hit area, reflowed title line-height.
4. Mirror any token additions (`--sf-ease-decelerate`, `--sf-line-height-lg`) across `sunfish-material.css` and `sunfish-bootstrap.css` to keep skin parity.

---

## 7. Citations

- Fluent UI React Dialog: https://react.fluentui.dev/?path=/docs/components-dialog--docs
- Fluent UI spacing tokens: https://react.fluentui.dev/?path=/docs/theme-spacing--docs
- Fluent UI border radii tokens: https://react.fluentui.dev/?path=/docs/theme-border-radii--docs
- Fluent UI typography tokens: https://react.fluentui.dev/?path=/docs/theme-typography--docs
- Fluent UI motion / createPresenceComponent: https://react.fluentui.dev/?path=/docs/motion-apis-createpresencecomponent--docs
- Fluent UI focus indicators: https://react.fluentui.dev/?path=/docs/concepts-developer-accessibility-focus-indicator--docs

---
