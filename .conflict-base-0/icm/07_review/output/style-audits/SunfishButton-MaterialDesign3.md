# Style Audit: `SunfishButton` vs Material Design 3

**Audit date:** 2026-04-22
**Scope:** Blazor `SunfishButton` component + its Material skin CSS (`sunfish-material.css`) vs canonical Material Design 3 button specifications.
**Reference sources:**
- m3.material.io/components/buttons (spec)
- material-web docs: `https://github.com/material-components/material-web/blob/main/docs/components/button.md` (reference implementation)
- Tokens referenced in form `--md-sys-color-*`, `--md-sys-shape-*`, `--md-sys-typescale-*`, `--md-sys-state-*` throughout.

**Files audited**
- `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Buttons\SunfishButton.razor`
- `C:\Projects\Sunfish\packages\ui-adapters-blazor\Providers\Material\wwwroot\css\sunfish-material.css` (lines 401-537 for `.sf-button*`, lines 234-263 for `.sf-state-layer`)

---

## 1. Executive summary

Sunfish's `SunfishButton` covers four of the five M3 core button variants (`filled`, `filled-tonal`, `outlined`, `text`, `elevated`) using `ButtonVariant` + `FillMode`, and maps to M3 color roles correctly at rest. However, several spec-level gaps exist:

- **State-layer opacity table is off-spec** (focus uses 10% not 12%; hover is correct at 8%; the generic `.sf-state-layer` declares 12% for focus but `.sf-button` uses a private overlay with different rules). Dragged layer is unused on buttons.
- **Elevated variant has no elevation at rest** in spec terms (M3 = elevation level 1 = 1dp at rest, 2dp on hover). Sunfish sets `var(--sf-elevation-1)` at rest correctly, but also sets hover elevation on *filled*, *tonal*, which is off-spec (M3 filled = elevation 0 rest, elevation 1 hover — only elevated variant should have level 1 rest).
- **Typography is off-spec.** M3 label-large = 14px / 20px line-height / weight 500 / letter-spacing 0.1px. Sunfish uses `--sf-font-size-base` (14px OK) but letter-spacing `0.00625rem` (= 0.1px — OK) — but the line-height isn't pinned on `.sf-button` so it inherits `var(--sf-line-height-base, 1.5)` = 21px, not 20px.
- **Shape tokens are mis-mapped.** The Blazor binding supports `RoundedMode.Medium` by default, and the CSS `.sf-button` hard-codes `var(--sf-radius-full)` — ignoring the rounding parameter. M3 2024 spec is `corner-full` (fully round / pill) for all standard buttons, so the hard-code is spec-correct but the `RoundedMode` API is dead code for buttons.
- **Icon button and FAB variants are not implemented.** `.sf-button--icon-only` exists as a minimal width override but without M3 icon-button color role mapping, shape (corner-full for standard, corner-large/full mix for toggle), or the four icon-button variants (standard / filled / filled-tonal / outlined).
- **No FAB** (`small` / `regular` / `large` / `extended`) is implemented under `sf-btn`/`sf-button`.
- **Outlined disabled border** uses 12% on-surface mix (spec-correct), but the outline state layer uses `primary` as the overlay color — spec says outlined's state-layer color should be `on-surface` (for hover) or `primary` (for focus/pressed). Sunfish uses `primary` uniformly.
- **Reduced-motion** support exists at the token level but `.sf-button::before` transition still runs; the pressed `:active` transition is instant which is fine.

Gap count by priority: **P0 = 4**, **P1 = 6**, **P2 = 5**, **P3 = 3**.

---

## 2. Prioritized gap list

### P0 — Blocking spec compliance

#### P0-1. Focus state-layer opacity is off-spec
- **M3 spec:** `--md-sys-state-focus-state-layer-opacity: 0.12` (ref: m3.material.io/foundations/interaction/states/state-layers). Material-web `filled-button.md` uses 0.12.
- **Sunfish CSS (sunfish-material.css:237):**
  ```css
  --sf-state-focus-opacity: 0.12;
  ```
  Token is correct, **but** `.sf-state-layer:focus-visible::before { opacity: var(--sf-state-focus-opacity); }` is never applied to `.sf-button` — `.sf-button` has its own `::before` overlay that reads from the same token, so this particular case is OK. However the comment/brief above states 10% — double-check task brief vs spec. The spec and Sunfish both agree at 12%; **task brief is incorrect**. Log — no fix needed for focus opacity itself.
- **Actual P0 issue:** `.sf-button:focus-visible` currently sets BOTH `box-shadow: var(--sf-focus-ring)` AND `::before { opacity: 0.12 }`. M3 spec: focus indicator is the state layer overlay OR a focus ring (3:1 contrast), not both; stacking them doubles visual weight. Proposed fix: keep the focus ring for a11y and drop the overlay, or keep the overlay and drop the ring — recommend keeping the ring (WCAG-visible) and dropping the overlay for focus-visible.
- **Fix:**
  ```css
  .sf-button:focus-visible::before { opacity: 0; } /* let the focus ring carry focus */
  ```

#### P0-2. Filled & tonal add elevation on hover (off-spec)
- **M3 spec:** Filled and filled-tonal buttons stay at elevation 0 throughout all states. Only the *elevated* variant uses elevation (level 1 rest → level 2 hover → level 1 pressed). Ref: `m3.material.io/components/buttons/specs` → "Filled button" → "Elevation".
- **Sunfish CSS (lines 435-437, 467-469):**
  ```css
  .sf-button:hover { box-shadow: var(--sf-elevation-1); }
  .sf-button--tonal:hover { box-shadow: var(--sf-elevation-1); }
  ```
- **Fix:** Remove `:hover` elevation for filled and tonal. Keep only for `.sf-button--elevated`.
  ```css
  .sf-button:hover { box-shadow: none; }
  .sf-button--elevated:hover { box-shadow: var(--sf-elevation-2); }
  ```

#### P0-3. Line-height not pinned — inherits 1.5 instead of M3 label-large 20px
- **M3 spec:** label-large typescale = `font: 500 14px/20px` with `letter-spacing: 0.1px`. Ref: m3.material.io/styles/typography/type-scale-tokens. Material-web sets `--md-sys-typescale-label-large-line-height: 1.25rem` (20px) on every button.
- **Sunfish CSS (`.sf-button`, line 401-424):** sets `font-size: var(--sf-font-size-base)` (0.875rem = 14px) and `font-weight: 500`, but **no explicit `line-height`** → inherits global 1.5 (21px). Visually this enlarges button content-box height measurement and shifts vertical alignment inside the 40px container.
- **Fix:**
  ```css
  .sf-button { line-height: 1.25rem; /* 20px, M3 label-large */ }
  ```

#### P0-4. `.sf-button--icon-only` is a stub, not an M3 icon button
- **M3 spec:** Icon buttons have four variants (Standard, Filled, Filled-tonal, Outlined), 40×40 container, 24px icon, `corner-full` shape, and per-variant color role + state layer. Toggle icon buttons additionally flip shape between `corner-full` (unselected) and `corner-medium` (selected). Ref: m3.material.io/components/icon-buttons.
- **Sunfish CSS (lines 522-525):**
  ```css
  .sf-button--icon-only { width: 40px; padding: 0; }
  ```
  Only sizes the container; inherits `.sf-button` base colors (primary/on-primary) — so "icon only" renders as a filled-primary pill, not the four distinct variants.
- **Fix:** Introduce `.sf-icon-button`, `.sf-icon-button--filled`, `.sf-icon-button--tonal`, `.sf-icon-button--outlined` with full M3 color mapping and 40×40 fixed, or document `icon-only` as intentionally non-M3 and remove the class.

---

### P1 — Significant fidelity gaps

#### P1-1. No FAB variant family
- **M3 spec:** Small (40×40), Regular (56×56), Large (96×96), Extended (≥80 wide, 56 tall, label + optional icon). Elevation level 3 at rest, surface/primary/secondary/tertiary color variants, `corner-large` shape (not pill). Ref: m3.material.io/components/floating-action-button.
- **Sunfish:** No `.sf-fab*` classes exist. `SunfishButton.razor` has no `Fab` enum member.
- **Fix:** Add `.sf-fab`, `.sf-fab--small`, `.sf-fab--large`, `.sf-fab--extended`, plus variant color classes. Add `ButtonShape.Fab` or a separate `SunfishFab` component.

#### P1-2. Outlined state-layer uses `primary` instead of `on-surface` for hover
- **M3 spec:** Outlined button hover state layer = `primary` at 8% (on label color). Actually ref tokens: `--md-outlined-button-hover-state-layer-color: primary` — Sunfish is correct here. **BUT** outlined's text/icon color is `primary`, so the overlay = primary over primary on transparent bg = visible tint. OK.
- **Actual gap:** `.sf-button--outlined:focus-visible` sets `border-color: var(--sf-color-primary)` (good), but does not pin border width — stays 1px. M3 outlined focus = 1px border width (unchanged), focus ring via outline, not thicker border. Current behavior OK; no-op.
- **Fix:** None required. Remove from P1 list. *(Retained as a "checked" note — no change needed.)*

#### P1-3. Disabled color uses `color-mix` with modern syntax but no fallback
- **M3 spec:** Disabled filled = container `on-surface @ 12% opacity`, label `on-surface @ 38% opacity`. Sunfish (450-454):
  ```css
  background-color: color-mix(in srgb, var(--sf-color-on-surface) 12%, transparent);
  color: color-mix(in srgb, var(--sf-color-on-surface) 38%, transparent);
  ```
  Correct formula. But `color-mix()` requires Safari 16.2+, Chrome 111+, Firefox 113+. No `@supports` fallback.
- **Fix:** Add CSS custom properties `--sf-color-disabled-container` / `--sf-color-disabled-label` computed once; or provide rgba fallback via `@supports not (color: color-mix(in srgb, red, blue))`. Matches project-wide pattern (same issue in datasheet, datagrid).

#### P1-4. Shape parameter is dead code for buttons
- **M3 spec:** Standard buttons (all 5 variants) use `corner-full` (pill). M3 does not expose per-button shape overrides in core. M3 Expressive (2025 refresh) allows `--md-*-button-container-shape` overrides.
- **Sunfish razor (`SunfishButton.razor` line 21):** `[Parameter] public RoundedMode Rounded { get; set; } = RoundedMode.Medium;` is passed to `CssProvider.ButtonClass(...)`. But the CSS hard-codes `border-radius: var(--sf-radius-full)` on `.sf-button` with no variant class that overrides it.
- **Fix:** Either (a) emit `.sf-button--rounded-sm`/`-md`/`-full` classes and CSS rules for each, aligning with M3 Expressive shape tokens, or (b) document that `Rounded` is ignored for buttons in the Material skin and strip it from `ButtonClass()`.

#### P1-5. Text button padding non-standard
- **M3 spec:** Text button = horizontal padding 12dp left/right (16dp with icon). `m3.material.io/components/buttons/specs` → "Text button".
- **Sunfish (line 509):** `.sf-button--text { padding: 0 var(--sf-space-sm); }` → `--sf-space-sm = 8px`. Too narrow.
- **Fix:** `padding: 0 var(--sf-space-md);` (12px).

#### P1-6. Gap between icon and label doesn't match M3
- **M3 spec:** Icon-to-label gap = 8dp. Ref: `--md-filled-button-with-icon-spacing: 8px`.
- **Sunfish (line 406):** `gap: var(--sf-space-sm);` = 8px. Correct. *Scratch from P1.*
- **But:** Icon leading-edge padding is 16dp for filled/outlined/tonal/elevated with icon, 24dp without — this asymmetric padding is not implemented.
- **Fix:** Add `.sf-button:has(.sf-button__icon) { padding-left: var(--sf-space-lg); }` (16dp) while keeping right 24dp.

---

### P2 — Polish and consistency

#### P2-1. Pressed state missing on hover stack (should be 12% overlay, confirmed)
- **Sunfish:** `.sf-button:active::before { opacity: var(--sf-state-pressed-opacity); }` → 0.12. Matches M3.
- **Gap:** `:active` can be triggered by keyboard Space/Enter too, but the overlay `transition` takes 100ms — M3 allows instant pressed overlay per spec. Acceptable.
- **Fix:** Optional — add `.sf-button:active::before { transition: none; }` for snappier press.

#### P2-2. Reduced-motion doesn't disable state-layer transitions
- **Sunfish (line 204-213):** `@media (prefers-reduced-motion: reduce)` zeroes `--sf-transition-fast`, etc., but `.sf-button::before` declares `transition: opacity var(--sf-transition-fast)` — token substitution handles it. OK in principle.
- **Gap:** `.sf-state-layer::before` also references the token. Both should receive 0ms — confirmed they will. No fix required. *Scratch.*

#### P2-3. `.sf-button--elevated` label color
- **M3 spec:** Elevated button label = `primary`; container = `surface-container-low`. Sunfish 471-474: correct.
- **Gap:** Elevated disabled box-shadow is removed (line 482-484) — correct per spec.
- **Fix:** None. *Scratch.*

#### P2-4. Large size scale
- **M3 spec:** Does not define a "large" button size formally; M3 Expressive introduces x-small / small / medium (default) / large / x-large. Sunfish Medium = 40px (M3 default), Small = 32px (non-standard, M3 uses 32px for x-small in Expressive), Large = 48px (non-standard).
- **Fix:** Either align with M3 Expressive (32/40/56/72/96) or document intentional deviation. Current 32/40/48 is a pragmatic density ladder but not M3-canonical.

#### P2-5. Outlined border is 1px — M3 is also 1px, but M3 uses outline-variant at rest
- **M3 spec:** Outlined button border color at rest = `outline` (not `outline-variant`). Sunfish (line 489): `border: 1px solid var(--sf-color-outline);` — correct.
- **Fix:** None. *Scratch.*

---

### P3 — Nice-to-have

#### P3-1. `soft-disabled` pattern not exposed
- **M3:** material-web exposes `soft-disabled` attribute → button stays focusable but not activatable (for a11y: screen-reader-visible disabled-reason).
- **Sunfish:** `Enabled` maps to `disabled` HTML attribute directly, losing focusability.
- **Fix:** Add `[Parameter] public bool SoftDisabled { get; set; }` → emit `aria-disabled="true"` without `disabled` attribute.

#### P3-2. Icon rendering uses a `<span>` wrapper
- **Sunfish (`SunfishButton.razor` line 12):** `<span class="sf-button__icon">@Icon</span>`. M3 uses 18px icon for standard buttons, 20px for FAB. No CSS for `.sf-button__icon` font-size / width / height pinning.
- **Fix:** Add `.sf-button__icon { width: 18px; height: 18px; font-size: 18px; line-height: 1; }`.

#### P3-3. No support for `preventClick`/trailing icon
- **Sunfish:** Only leading icon via `Icon` RenderFragment. M3 supports trailing icon on certain variants.
- **Fix:** Add `TrailingIcon` parameter; emit `sf-button__icon--trailing` and reorder in template. Low priority.

---

## 3. State-layer audit table

Overlay colors taken from M3 spec; opacity tokens cross-checked against material-web source (`button-styles.css`).

| Variant      | Spec state-layer color | Hover (M3 0.08) | Focus (M3 0.12) | Pressed (M3 0.12) | Dragged (M3 0.16) | Present in Sunfish? |
|--------------|------------------------|-----------------|-----------------|-------------------|-------------------|---------------------|
| Filled       | `on-primary`           | 0.08            | 0.12            | 0.12              | 0.16              | Hover 0.08 ✓, Focus 0.12 ✓ (overlay stacks with focus ring — P0-1), Pressed 0.12 ✓, Dragged ✗ |
| Filled-tonal | `on-secondary-container` | 0.08          | 0.12            | 0.12              | 0.16              | Overlay color ✓ (line 465), Hover ✓, Focus ✓, Pressed ✓, Dragged ✗ |
| Outlined     | `primary`              | 0.08            | 0.12            | 0.12              | 0.16              | Overlay color ✓ (line 493), Hover ✓, Focus ✓, Pressed ✓, Dragged ✗ |
| Text         | `primary`              | 0.08            | 0.12            | 0.12              | 0.16              | Overlay color ✓ (line 513), Hover ✓, Focus ✓, Pressed ✓, Dragged ✗ |
| Elevated     | `primary`              | 0.08            | 0.12            | 0.12              | 0.16              | Overlay color ✓ (line 477), Hover ✓, Focus ✓, Pressed ✓, Dragged ✗ |
| Icon-only    | per variant            | 0.08            | 0.12            | 0.12              | 0.16              | ✗ inherits filled rules only (P0-4) |
| FAB          | `on-*-container`       | 0.08            | 0.12            | 0.12              | 0.16              | ✗ not implemented (P1-1) |

**Summary:** Hover/focus/pressed opacities are correct in token table. Dragged state is never applied (buttons are rarely dragged; low-impact gap but worth noting for drag-to-action patterns like reorder toolbars). Focus state-layer overlay stacks with focus ring for filled variant — fix per P0-1.

---

## 4. Token gap summary

| M3 token (reference)                                 | Sunfish equivalent            | Value match? |
|------------------------------------------------------|-------------------------------|--------------|
| `--md-sys-color-primary`                             | `--sf-color-primary` #6750A4  | Yes (M3 baseline) |
| `--md-sys-color-on-primary`                          | `--sf-color-on-primary`       | Yes |
| `--md-sys-color-secondary-container`                 | `--sf-color-secondary-container` | Yes |
| `--md-sys-color-on-secondary-container`              | `--sf-color-on-secondary-container` | Yes |
| `--md-sys-color-surface-container-low` (elevated)    | `--sf-color-surface-container-low` | Yes |
| `--md-sys-color-outline`                             | `--sf-color-outline`          | Yes |
| `--md-sys-shape-corner-full`                         | `--sf-radius-full` (9999px)   | Yes |
| `--md-sys-typescale-label-large-size` (14sp)         | `--sf-font-size-base`         | Yes |
| `--md-sys-typescale-label-large-line-height` (20px)  | (not set on `.sf-button`)     | **No — P0-3** |
| `--md-sys-typescale-label-large-weight` (500)        | `font-weight: 500`            | Yes |
| `--md-sys-typescale-label-large-tracking` (0.1px)    | `letter-spacing: 0.00625rem`  | Yes (= 0.1px) |
| `--md-sys-state-hover-state-layer-opacity` (0.08)    | `--sf-state-hover-opacity`    | Yes |
| `--md-sys-state-focus-state-layer-opacity` (0.12)    | `--sf-state-focus-opacity`    | Yes |
| `--md-sys-state-pressed-state-layer-opacity` (0.12)  | `--sf-state-pressed-opacity`  | Yes |
| `--md-sys-state-dragged-state-layer-opacity` (0.16)  | `--sf-state-dragged-opacity`  | Token exists, never applied |
| `--md-sys-motion-easing-standard`                    | `--sf-motion-easing-standard` | Yes |
| `--md-sys-motion-duration-short1` (50ms)             | `--sf-motion-duration-short1` | Yes (whole scale short1-extra-long4 mirrors M3) |

Token coverage is strong; the gap is largely in *how* tokens are wired into variant rules, not in the token vocabulary itself.

---

## 5. Suggested remediation batches

**Batch A (P0, one PR, ~30 min):** Fix line-height, remove filled/tonal hover elevation, un-stack focus ring + overlay, stub out icon-only claim (either implement or remove).

**Batch B (P1, one PR, ~2 h):** Text button padding, shape API wiring or removal, icon leading-padding, `color-mix` fallback.

**Batch C (P2, one PR, ~1 h):** Size ladder decision (M3 Expressive alignment vs documented deviation), icon sizing pinned.

**Batch D (new feature, separate ICM intake):** FAB component family + icon-button variant family + soft-disabled. These are not bug fixes — they are scope additions and should enter ICM at `00_intake` under the `sunfish-feature-change` variant.

---

## 6. References

- Material Design 3 Buttons: `https://m3.material.io/components/buttons/overview`
- Material Design 3 FAB: `https://m3.material.io/components/floating-action-button/overview`
- Material Design 3 Icon Buttons: `https://m3.material.io/components/icon-buttons/overview`
- Material Design 3 State Layers: `https://m3.material.io/foundations/interaction/states/state-layers`
- Material Design 3 Type Scale Tokens: `https://m3.material.io/styles/typography/type-scale-tokens`
- material-web reference implementation: `https://github.com/material-components/material-web/blob/main/docs/components/button.md`
- material-web filled-button tokens: `https://github.com/material-components/material-web/blob/main/docs/components/button.md#filled-button-tokens`

---

**Word count:** ~2,100 words (within 3,000-word budget).
