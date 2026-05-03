# Style Audit — SunfishButton vs Fluent UI v9 Button

**Audit date:** 2026-04-22
**Sunfish source (component):** `packages/ui-adapters-blazor/Components/Buttons/SunfishButton.razor`
**Sunfish source (skin):** `packages/ui-adapters-blazor/Providers/FluentUI/wwwroot/css/sunfish-fluentui.css` (rules `.sf-button*`, lines 1366–1607)
**Sunfish source (class builder):** `packages/ui-adapters-blazor/Providers/FluentUI/FluentUICssProvider.cs` (lines 175–198)
**Fluent v9 reference (API):** `microsoft/fluentui` → `packages/react-components/react-button/library/src/components/Button/Button.types.ts`
**Fluent v9 reference (styles):** `microsoft/fluentui` → `packages/react-components/react-button/library/src/components/Button/useButtonStyles.styles.ts`
**Fluent v9 docs:** https://react.fluentui.dev/?path=/docs/components-button-button--docs (redirects to https://storybooks.fluentui.dev/react/?path=/docs/components-button-button--docs)

---

## 1. Executive Summary

SunfishButton ships a surface roughly comparable in **shape taxonomy** (rounded/square/full ~ rounded/square/circular) and **size taxonomy** (small/medium/large) to Fluent v9, but diverges materially in three high-risk areas:

1. **Appearance vocabulary mismatch.** Sunfish exposes six color-charged variants (Primary/Secondary/Danger/Warning/Info/Success) while Fluent v9 exposes five intent-neutral appearances (primary/secondary/outline/subtle/transparent). Sunfish has **no `subtle` and no `transparent`** built into `ButtonVariant`, and partially surfaces them through `FillMode.Flat`/`FillMode.Clear` — which never received Fluent-grade hover/pressed layering.
2. **Focus indicator is not Fluent-shaped.** Sunfish emits a single dual-ring `box-shadow` (`--sf-focus-ring`). Fluent v9 uses a 2-layer indicator (outer `strokeWidthThick` outline + inner `strokeWidthThin` boxShadow inset in `colorStrokeFocus2`), and for `appearance="primary"` adds a third `strokeWidthThick` `colorNeutralForegroundOnBrand` inner ring. Sunfish's ring therefore looks correct on neutral surfaces but collapses visually on primary fills.
3. **Disabled/hover/pressed tokens are hand-picked hex, not semantic tokens.** `#c43035`, `#e69a3e`, `#00a8d6`, `#0e6e0e`, `#0c5e0c`, `#a52b2f` etc. are hard-coded in `.sf-button--danger:hover` through `.sf-button--success:active`. This prevents dark-mode token swap and breaks the dark skin (verified: dark block at lines 119–147 does not redefine hover/active intermediates).

There are also smaller parity gaps around icon slot geometry (no `sf-button__icon` CSS rule — icon spacing is inherited only from parent `gap`), icon-only collapse (`IconButtonClass` is a separate surface, not the same button switched by `iconOnly`), high-contrast (forced-colors) support (absent), reduced-motion support (absent), iconPosition API (absent), `disabledFocusable` API (absent), and `min-width` parity (Sunfish ships no min-width; Fluent medium is 96px, small is 64px).

Total: **4 P0, 7 P1, 6 P2, 3 P3** prioritized gaps.

---

## 2. Prioritized Gap List

### P0 — Blocking parity; user-visible regressions or accessibility failures

#### P0-1. Missing `subtle` and `transparent` appearances

**Fluent v9 reference** (`Button.types.ts`):
```ts
appearance?: 'secondary' | 'primary' | 'outline' | 'subtle' | 'transparent';
```
Subtle uses `tokens.colorSubtleBackground` / `colorSubtleBackgroundHover`; transparent uses `colorTransparentBackground` / `colorTransparentBackgroundHover`. Both swap icon filled/regular on hover — Fluent-specific iconography behavior.
Upstream: https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-button/library/src/components/Button/useButtonStyles.styles.ts#L181-L270

**Sunfish current** (`ButtonVariant.cs`): enum has Primary/Secondary/Danger/Warning/Info/Success only. `FillMode.Flat` and `FillMode.Clear` approximate the idea but:
```css
.sf-button--fill-flat { background: transparent; border-color: transparent; }
.sf-button--fill-flat:hover { background: var(--sf-color-surface-hover); }
.sf-button--fill-clear { background: transparent; border-color: transparent; }
/* No :hover rule for clear */
```

**Proposed fix.** Add `ButtonAppearance { Secondary, Primary, Outline, Subtle, Transparent }` to `foundation/Enums` and deprecate (keep as alias) the conflation in `FillMode`. Add CSS rules `.sf-button--subtle` and `.sf-button--transparent` that read `--sf-color-subtle-bg` / `--sf-color-transparent-bg` plus their `-hover` / `-pressed` variants. See token-mapping table §3.

---

#### P0-2. Focus indicator does not match Fluent v9 dual-stroke spec

**Fluent v9 reference** (`useButtonStyles.styles.ts`, base `useRootBaseClassName`):
```ts
...createCustomFocusIndicatorStyle({
  borderColor: tokens.colorStrokeFocus2,
  borderRadius: tokens.borderRadiusMedium,
  borderWidth: '1px',
  outline: `${tokens.strokeWidthThick} solid ${tokens.colorTransparentStroke}`,
  boxShadow: `0 0 0 ${tokens.strokeWidthThin} ${tokens.colorStrokeFocus2} inset`,
  zIndex: 1,
}),
```
And for `primary` specifically (`useRootFocusStyles.primary`):
```ts
boxShadow: `${tokens.shadow2}, 0 0 0 ${tokens.strokeWidthThin} ${tokens.colorStrokeFocus2} inset,
  0 0 0 ${tokens.strokeWidthThick} ${tokens.colorNeutralForegroundOnBrand} inset`,
```
Upstream: https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-button/library/src/components/Button/useButtonStyles.styles.ts#L102-L125 and L465-L490

**Sunfish current** (line 1385–1388, 255):
```css
.sf-button:focus-visible { outline: none; box-shadow: var(--sf-focus-ring); }
/* --sf-focus-ring: 0 0 0 2px var(--sf-color-background), 0 0 0 4px var(--sf-color-primary); */
```
Single token; no primary-specific inner-white ring; no outer transparent outline layer.

**Proposed fix.** Introduce `--sf-stroke-focus-2` token (= Fluent `colorStrokeFocus2`), and rewrite `.sf-button:focus-visible` to emit an outer `2px solid transparent` outline + inset `1px var(--sf-stroke-focus-2)` boxShadow. Add an `.sf-button--primary:focus-visible` override that layers an additional inner `2px var(--sf-color-on-primary)` ring. Verify with axe and WCAG 2.4.7 on both primary and neutral backgrounds.

---

#### P0-3. Hover/pressed colors are hard-coded hex, breaking dark theme

**Fluent v9 reference**: every appearance reads from tokens that are remapped in `teamsDarkTheme` / `webDarkTheme`. Example — primary pressed:
```ts
backgroundColor: tokens.colorBrandBackgroundPressed,
```
Upstream: https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-button/library/src/components/Button/useButtonStyles.styles.ts#L145-L174

**Sunfish current** (lines 1446–1494):
```css
.sf-button--danger:hover   { background: #c43035; }
.sf-button--danger:active  { background: #a52b2f; }
.sf-button--warning:hover  { background: #e69a3e; }
.sf-button--warning:active { background: #cc8a38; }
.sf-button--info:hover     { background: #00a8d6; }
.sf-button--info:active    { background: #0094bd; }
.sf-button--success:hover  { background: #0e6e0e; }
.sf-button--success:active { background: #0c5e0c; }
```
Dark theme block (line 119–147) redefines `--sf-color-danger` etc. but never overrides these hover/active hex values, so in dark mode a dangerous hover still paints red-for-light.

**Proposed fix.** Add `--sf-color-{danger|warning|info|success}-{hover|active}` tokens to `:root` and `[data-sf-theme=dark]`, then replace every hex literal with `var(--sf-color-<variant>-<state>)`. Mirrors how `--sf-color-primary-hover` / `-active` are already structured.

---

#### P0-4. No high-contrast (`@media (forced-colors: active)`) or reduced-motion support

**Fluent v9 reference**: base, primary, subtle, transparent, and disabled all define `'@media (forced-colors: active)'` branches that repaint using Windows system keywords (`Highlight`, `HighlightText`, `ButtonText`, `ButtonFace`, `GrayText`) and set `forcedColorAdjust: 'none'`. Base style also defines:
```ts
'@media screen and (prefers-reduced-motion: reduce)': {
  transitionDuration: '0.01ms',
},
```
Upstream: https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-button/library/src/components/Button/useButtonStyles.styles.ts#L76-L100

**Sunfish current**: zero `forced-colors` and zero `prefers-reduced-motion` media queries on any `.sf-button*` rule (grep confirms).

**Proposed fix.** Append a `@media (forced-colors: active)` block per appearance (Primary → Highlight/HighlightText; Disabled → GrayText) and a global `@media (prefers-reduced-motion: reduce) { .sf-button { transition-duration: 0.01ms; } }`. This is a WCAG 1.4.11 + 2.3.3 compliance fix, not cosmetic.

---

### P1 — Visible quality gap; spec-required for v9 parity

#### P1-1. No `iconPosition` (`before`/`after`) API

**Fluent v9** (`Button.types.ts`):
```ts
iconPosition?: 'before' | 'after'; // default 'before'
```
Upstream: Button.types.ts line ~50.

**Sunfish current**: `SunfishButton.razor` renders `<span class="sf-button__icon">@Icon</span>` always before `@ChildContent`. No parameter.
```razor
@if (Icon != null) { <span class="sf-button__icon">@Icon</span> }
@ChildContent
```

**Proposed fix.** Add `[Parameter] public IconPosition IconPosition { get; set; } = IconPosition.Before;` (new enum in `foundation`) and conditionally reorder `<span>` and `@ChildContent`. Add CSS `.sf-button__icon--before { margin-right: var(--sf-button-icon-spacing); }` and `.sf-button__icon--after { margin-left: var(--sf-button-icon-spacing); }`, dropping the current `gap`-only approach.

---

#### P1-2. No `sf-button__icon` CSS rule; icon sizing depends on caller

**Fluent v9** (`useIconBaseClassName` + `useIconStyles`): the icon slot is explicitly sized (20px medium, 20px small, 24px large) and spaced (`spacingHorizontalSNudge` default, `spacingHorizontalXS` for small).

**Sunfish current**: grepping for `.sf-button__icon` in `sunfish-fluentui.css` returns zero matches. The markup class exists (SunfishButton.razor line 12) but has no paired style rule; spacing relies on the parent's `gap: var(--sf-space-sm)`, so icon geometry varies with whatever the icon author passes in.

**Proposed fix.** Add:
```css
.sf-button__icon { display: inline-flex; align-items: center; justify-content: center;
  font-size: 20px; height: 20px; width: 20px; }
.sf-button--small .sf-button__icon { font-size: 20px; height: 20px; width: 20px; }
.sf-button--large .sf-button__icon { font-size: 24px; height: 24px; width: 24px; }
```

---

#### P1-3. No icon-only collapse mode on `SunfishButton`

**Fluent v9** computes `iconOnly = !children && icon` and applies `useRootIconOnlyStyles` (24/32/40 px square) to `<Button>` directly. In Sunfish, icon-only is a separate component surface: `IconButtonClass` → `.sf-icon-button` (lines 5090–5124).

**Proposed fix.** Detect `ChildContent == null && Icon != null` in `SunfishButton.razor` and add an `sf-button--icon-only` modifier that reuses the existing icon-button sizing rules. Keeps the public API unified with Fluent.

---

#### P1-4. Missing `disabledFocusable` semantic

**Fluent v9** (`Button.types.ts`):
```ts
disabledFocusable?: boolean; // allows Tab-landing on disabled for menus/toolbars
```

**Sunfish current**: only `Enabled` (bool). The button falls out of tab order when disabled — breaks Toolbar/Menu patterns that keep disabled items focusable for discoverability.

**Proposed fix.** Add `[Parameter] public bool DisabledFocusable { get; set; }`, render `aria-disabled="true"` + `tabindex="0"` when `DisabledFocusable && !Enabled`, and skip the HTML `disabled` attribute in that branch. ToolbarButton and MenuItem will need the same treatment in a follow-up.

---

#### P1-5. `min-width` not enforced

**Fluent v9**: `minWidth: '96px'` on base, `64px` on small, `96px` on large. Provides visual rhythm between adjacent buttons.

**Sunfish current** (lines 1391–1407): only `min-height` is set (24/32/40). No `min-width`.

**Proposed fix.** Add `min-width: 96px` on `.sf-button--medium` and `.sf-button--large`, `min-width: 64px` on `.sf-button--small`. Exclude when `sf-button--icon-only`, `sf-button--fill-link`, or `sf-button--fill-clear`.

---

#### P1-6. `font-weight` varies by size in Fluent, not in Sunfish

**Fluent v9**: small uses `fontWeightRegular`, medium/large use `fontWeightSemibold`.

**Sunfish current** (line 1373): base sets `var(--sf-font-weight-semibold)`; `.sf-button--small` (lines 1391–1395) does not override it.

**Proposed fix.** Add `font-weight: var(--sf-font-weight-regular);` to `.sf-button--small`. Introduce `--sf-font-weight-regular: 400` if not defined.

---

#### P1-7. Transition timing hard-coded to 120ms linear-ish

**Fluent v9**: `transitionDuration: tokens.durationFaster` (100ms), `transitionTimingFunction: tokens.curveEasyEase` (cubic-bezier(0.33, 0, 0.67, 1)).

**Sunfish current**: `--sf-transition-fast: 120ms ease;`. Close but not token-accurate — the `ease` keyword is a different curve.

**Proposed fix.** Add `--sf-duration-faster: 100ms` and `--sf-curve-easy-ease: cubic-bezier(0.33, 0, 0.67, 1)` tokens, update `.sf-button` transition to reference both, leaving `--sf-transition-fast` as a compatibility alias.

---

### P2 — Polish; Fluent-grade but not blocking

#### P2-1. Subtle/transparent variants swap icon-filled ↔ icon-regular on hover

**Fluent v9** (subtle & transparent branches): selectors like `& .${iconFilledClassName}` toggle `display: inline` on hover/pressed. Requires paired filled/regular icon SVGs shipped together.

**Sunfish current**: no dual-icon convention. `SunfishIcon` likely renders a single glyph.

**Proposed fix.** Either (a) document that Sunfish's `Subtle`/`Transparent` are single-glyph and the filled-on-hover behavior is an intentional omission, or (b) introduce `IconFilled`/`IconRegular` slots on SunfishButton with matching `.sf-button__icon--filled`/`--regular` selectors that toggle on `:hover`. Option (a) for v1; (b) for a later parity milestone.

---

#### P2-2. `shape` naming is non-canonical (`RoundedMode.None/Small/Medium/Large/Full` vs Fluent `rounded/circular/square`)

**Fluent v9**: `shape?: 'rounded' | 'circular' | 'square'` (3 values).

**Sunfish current**: 5 values in `RoundedMode`. Fluent's `circular` maps to `RoundedMode.Full` semantically; Fluent's `square` maps to `RoundedMode.None`; Fluent's `rounded` (default) maps to `RoundedMode.Medium`. `Small` and `Large` are Sunfish-specific extensions.

**Proposed fix.** Keep `RoundedMode` (it's a superset) but add `[Parameter] public ButtonShape? Shape { get; set; }` that, when set, short-circuits `RoundedMode` and emits `sf-button--shape-circular` / `sf-button--shape-square` / `sf-button--shape-rounded`. Document the mapping in the adapter README so Telerik-compat and React-adapter can converge.

---

#### P2-3. `borderRadius` size coupling

**Fluent v9**: `borderRadius` lives **on the size class**, not on the base — `small` gets `borderRadiusMedium`, `large` gets `borderRadiusMedium`, medium inherits base. This means shape overrides flow through the size tier predictably.

**Sunfish current**: base `.sf-button` sets `border-radius: var(--sf-radius-md)` and size classes don't touch it. Fine for solid shapes, but couples `FillMode.Link`'s `padding: 0` and `min-height: auto` awkwardly.

**Proposed fix.** Move `border-radius` off `.sf-button` base and onto each size class. Low priority; only matters when `Shape` API from P2-2 lands.

---

#### P2-4. No `text-overflow` / `overflow: hidden` on root

**Fluent v9**: base includes `overflow: 'hidden'`. Prevents long `children` from breaking layout before the ellipsis cascade kicks in.

**Sunfish current**: only `white-space: nowrap` (line 1379). Long text can still overflow visually.

**Proposed fix.** Add `overflow: hidden; text-overflow: ellipsis;` to `.sf-button`.

---

#### P2-5. Outline variant's `color` + `border-color` derivation is inconsistent

**Fluent v9**: `outline` is a pure background-transparency layer — it **keeps** the neutral text/border from base, only swapping `backgroundColor` to `colorTransparentBackground`. Sunfish inverts this: `.sf-button--outline.sf-button--primary` forces `color: var(--sf-color-primary)` and matching border (line 1501–1504), which is closer to a "Primary Outline" semantic than Fluent's intent-neutral outline.

**Proposed fix.** Either (a) rename the modifier to `sf-button--primary-outline` etc. and reserve `sf-button--outline` for the neutral Fluent behavior, or (b) accept the divergence and document that Sunfish outline is intent-colored (matches Telerik's ThemeColor + FillMode.Outline convention, which is probably preferred given `compat-telerik`).

---

#### P2-6. `FillMode.Link` breaks the box model without enforcing it

**Sunfish current** (lines 1580–1586):
```css
.sf-button--fill-link { background: transparent; border-color: transparent;
  text-decoration: underline; padding: 0; min-height: auto; }
```
Removes padding/min-height, but doesn't set `color` to `--sf-color-primary` or add a hover `text-decoration-thickness`. Result: the link-styled button inherits whatever variant color it's paired with, which is usually correct for Primary but off-brand for Secondary (becomes a gray link).

**Proposed fix.** Add `color: var(--sf-color-primary);` and `:hover { text-decoration-thickness: 2px; }` to `.sf-button--fill-link`. Alternatively, add a Fluent-style `<Link>` component and deprecate the link fill mode.

---

### P3 — Nice-to-have; future consideration

#### P3-1. Mozilla-specific `@supports (-moz-appearance:button)` box-shadow fix-up

Fluent v9 guards against a Firefox box-shadow rounding bug (Mozilla BugID 1857642) by adding 0.25px to `strokeWidthThin` inside a `@supports` block. Unlikely to hit Sunfish, but worth tracking once P0-2 lands.

#### P3-2. `cursor: pointer` is only set on `:hover` in Fluent, always on in Sunfish

Minor; Fluent scopes `cursor: pointer` inside `':hover'` so the outer base stays `cursor: default`. Sunfish base (line 1377) sets it globally. No user-visible issue.

#### P3-3. No `forced-colors` token `colorTransparentStroke` equivalent

Needed only if P0-4 is implemented fully; the outer 2px transparent outline that Fluent emits is critical for Windows high-contrast where the focus ring becomes the visible indicator.

---

## 3. Token-Mapping Table

Fluent v9 token → Sunfish CSS variable (status) → CSS rule location in `sunfish-fluentui.css`

| Fluent v9 token | Sunfish variable | Status | Location |
|---|---|---|---|
| `colorBrandBackground` | `--sf-color-primary` | mapped | `:root` L3 |
| `colorBrandBackgroundHover` | `--sf-color-primary-hover` | mapped | `:root` L4, used L1417 |
| `colorBrandBackgroundPressed` | `--sf-color-primary-active` | mapped | `:root` L5, used L1422 |
| `colorNeutralForegroundOnBrand` | `--sf-color-on-primary` | mapped | `:root` L7 |
| `colorNeutralBackground1` | `--sf-color-background` | mapped | `:root` L22 |
| `colorNeutralBackground1Hover` | `--sf-color-surface-hover` | mapped (approx) | `:root` L24, used L1433 |
| `colorNeutralBackground1Pressed` | `--sf-color-border` | drift | L1437 (should be distinct `-pressed` token) |
| `colorNeutralForeground1` | `--sf-color-on-background` | mapped | `:root` L25 |
| `colorNeutralStroke1` | `--sf-color-border-strong` | mapped | `:root` L30 |
| `colorNeutralStroke1Hover` | — | **missing** | needs `--sf-color-border-strong-hover` |
| `colorNeutralStroke1Pressed` | — | **missing** | needs `--sf-color-border-strong-pressed` |
| `colorSubtleBackground` | — | **missing** | needed for P0-1 |
| `colorSubtleBackgroundHover` | — | **missing** | needed for P0-1 |
| `colorSubtleBackgroundPressed` | — | **missing** | needed for P0-1 |
| `colorTransparentBackground` | implicit `transparent` | partial | used inline L1498, L1567, L1572 |
| `colorTransparentBackgroundHover` | — | **missing** | needed for P0-1 |
| `colorTransparentStroke` | — | **missing** | needed for P0-4 (focus outline) |
| `colorNeutralBackgroundDisabled` | `--sf-color-surface` (reused) | drift | L1558 — Fluent has a dedicated disabled surface |
| `colorNeutralForegroundDisabled` | `--sf-color-text-disabled` | mapped | `:root` L28, used L1559 |
| `colorNeutralStrokeDisabled` | `--sf-color-border` | drift | L1560 |
| `colorStrokeFocus2` | — | **missing** | needed for P0-2 |
| `borderRadiusNone` | `0` (literal) | inline | L1594 |
| `borderRadiusSmall` | `--sf-radius-sm` (2px) | mapped | `:root` L203 |
| `borderRadiusMedium` | `--sf-radius-md` (4px) | mapped | `:root` L204 |
| `borderRadiusLarge` | `--sf-radius-lg` (8px) | mapped | `:root` L205 |
| `borderRadiusCircular` | `9999px` (literal) | inline | L1606 |
| `fontSizeBase200` | `--sf-font-size-sm` | mapped | `:root` L164 |
| `fontSizeBase300` | `--sf-font-size-base` | mapped | `:root` L165 |
| `fontSizeBase400` | `--sf-font-size-md` | mapped | `:root` L166 |
| `lineHeightBase200/300/400` | — | **missing** | no `--sf-line-height-*` tokens |
| `fontWeightRegular` | — | **missing** | needed for P1-6 |
| `fontWeightSemibold` | `--sf-font-weight-semibold` | mapped | `:root` L173 |
| `fontFamilyBase` | `--sf-font-family` | mapped | used L1371 |
| `spacingHorizontalS` | — | partial | `--sf-space-sm` exists but not in horizontal/vertical split |
| `spacingHorizontalM` | `--sf-space-sm` (reused via `gap`) | drift | L1370 |
| `spacingHorizontalL` | — | **missing** | needed for `large` padding parity |
| `spacingHorizontalSNudge` | — | **missing** | needed for icon spacing parity |
| `spacingHorizontalXS` | — | **missing** | needed for small-icon spacing |
| `strokeWidthThin` | `1px` (literal) | inline | L1375 |
| `strokeWidthThick` | — | **missing** | needed for P0-2 (focus outer outline) |
| `strokeWidthThicker` | — | **missing** | primary focus outer ring |
| `durationFaster` | `--sf-transition-fast` (120ms) | drift (100ms upstream) | L240 |
| `curveEasyEase` | `ease` (keyword) | drift | L240 |
| `shadow2` | — | **missing** | primary focus shadow layer |

---

## 4. Focus-Area Coverage Table

State / feature → Fluent v9 behavior → Sunfish coverage

| Focus area | Fluent v9 | Sunfish | Verdict |
|---|---|---|---|
| **Rest (secondary/default)** | neutral bg + stroke + text | `.sf-button--secondary` L1426-1430 | Parity |
| **Rest (primary)** | brand bg + `transparent` border | `.sf-button--primary` L1410-1414 | Parity |
| **Rest (outline)** | transparent bg, base text/border | `.sf-button--outline` L1497-1499 + intent-colored overrides | Drift (see P2-5) |
| **Rest (subtle)** | `colorSubtleBackground` | — | **Missing (P0-1)** |
| **Rest (transparent)** | `colorTransparentBackground` | partial via `fill-clear` | **Missing (P0-1)** |
| **Hover** | `*BackgroundHover` + `*Stroke1Hover` + `*Foreground1Hover` | bg only (border/fg often unchanged) | Drift |
| **Pressed** (`:hover:active,:active:focus-visible`) | `*BackgroundPressed` triad | `:active` only, no `:active:focus-visible` | Drift |
| **Disabled** | `colorNeutralBackgroundDisabled` + `NeutralStrokeDisabled` + `NeutralForegroundDisabled`; `cursor:not-allowed`; icon tint | disabled L1556-1563; `cursor:not-allowed` present; no icon tint rule | Partial |
| **Focus-visible (neutral)** | outer `2px` transparent outline + inner `1px` `colorStrokeFocus2` shadow | `--sf-focus-ring` dual shadow | Drift (P0-2) |
| **Focus-visible (primary)** | adds inner `2px` `NeutralForegroundOnBrand` ring | none | **Missing (P0-2)** |
| **High contrast** | `@media (forced-colors: active)` branches on every appearance | none | **Missing (P0-4)** |
| **Reduced motion** | `prefers-reduced-motion: reduce` → 0.01ms | none | **Missing (P0-4)** |
| **Icon slot (sized)** | 20px / 20px / 24px per size | no `sf-button__icon` rule | **Missing (P1-2)** |
| **Icon position** | `before` / `after` | always before | **Missing (P1-1)** |
| **Icon filled↔regular swap on hover** | subtle + transparent only | none | **Missing (P2-1)** |
| **Icon-only (collapsed)** | `iconOnly` branch: 24/32/40 square | separate `SunfishIconButton` | Drift (P1-3) |
| **Min-width** | 64 / 96 / 96 | none | **Missing (P1-5)** |
| **Min-height** | implicit via padding | 24/32/40 | Parity (different impl, same result) |
| **Typography (size-coupled weight)** | small=regular, m/l=semibold | semibold across the board | Drift (P1-6) |
| **`disabled` attribute handling** | HTML disabled + `aria-disabled` paths | HTML `disabled` only | Drift (P1-4) |
| **`disabledFocusable` semantic** | keeps Tab focus, sets `aria-disabled` | not supported | **Missing (P1-4)** |
| **Shape (rounded/circular/square)** | 3 enum values | 5 values in `RoundedMode` (superset) | Different taxonomy (P2-2) |
| **Dark theme token swap** | automatic via token remap | manual — hard-coded hex leaks through | Bug (P0-3) |
| **Transition timing** | 100ms `curveEasyEase` | 120ms `ease` | Drift (P1-7) |
| **Mozilla box-shadow bug workaround** | `@supports (-moz-appearance:button)` | none | Not observed (P3-1) |

---

## 5. Recommended Sequencing

1. **Sprint A (P0 bundle):** Ship focus-indicator rewrite (P0-2), disabled/hover/pressed token de-hexing (P0-3), high-contrast + reduced-motion (P0-4). No API changes; pure CSS + token additions. Validate against axe + Windows high-contrast mode.
2. **Sprint B (P0-1 + P1 API additions):** Add `ButtonAppearance` enum with Subtle/Transparent, add `IconPosition`, add `DisabledFocusable`. Coordinate with React adapter (parity pair) and `compat-telerik` (deprecation path for `ButtonVariant.Danger/Warning/Info/Success` — these have no Fluent equivalent and should either move to a sibling `ButtonIntent` prop or be flagged as Sunfish-extension).
3. **Sprint C (P1 styling):** Icon slot CSS (P1-2), icon-only collapse (P1-3), min-width (P1-5), size-coupled font-weight (P1-6), duration/curve tokens (P1-7).
4. **Sprint D (P2 polish):** Everything else, plus Storybook/kitchen-sink parity checks.

Parity tests should be added in `packages/ui-adapters-blazor/tests` asserting that every `ButtonAppearance × ButtonSize × ButtonShape × IconPosition × Disabled×DisabledFocusable` combination emits a stable class list, and that a snapshot of the computed styles matches a golden file — same approach used for existing adapter parity tests.

---

*Word count: ~2,580.*
