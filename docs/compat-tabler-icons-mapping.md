# compat-tabler-icons Parameter Mapping & Divergence Log

> **Disambiguation — migration shim vs. native provider.** This document covers
> `Sunfish.Compat.TablerIcons`, the **migration-compat SHIM** that lets apps already using
> a Tabler Icons Blazor wrapper (e.g. `Vizor.Icons.Tabler`, `Kebechet.Blazor.Tabler.Icons`,
> `NgIcons.TablerIcons`) flip their `using` directives and keep existing markup working.
>
> For Sunfish's **native Tabler-backed icon PROVIDER** — the package that registers Tabler
> as your app's default Sunfish icon set — see `Sunfish.Icons.Tabler` in
> `packages/ui-adapters-blazor/Icons/Tabler/`. That package is a completely separate piece
> of work with different consumers, different APIs, and a different purpose. This document
> does not apply to it.

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Tabler Icons code maps to after migration. Any change to an entry (promoting
> a parameter from "mapped" to "throws", changing a default, adding a divergence,
> expanding the `TablerIconName` starter set) is a **breaking change** for consumers and
> must land under the policy gate in the same PR as the code change. See
> `packages/compat-tabler-icons/POLICY.md`.

## Conventions

- **Mapped** — Parameter value translates 1:1 to a Sunfish parameter / CSS class / inline
  style on the emitted `<i>`.
- **Forwarded** — Attribute is passed through via `AdditionalAttributes` (e.g. `class`,
  `style`, `tabindex`). No semantic transform.
- **LogAndFallback** — Value is accepted but not implemented; a warning is logged via
  `ILogger` and rendering falls back to the default behavior. Reserved for cosmetic
  parameters with no functional impact.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values
  that would silently change behavior if dropped, or that are out of scope for Phase 3E.

---

## TablerIcon

- **Upstream target (vendor-agnostic):** `<i class="tabler tabler-home"></i>` when
  paired with a Tabler Icons font-face / CSS integration (the upstream
  [`@tabler/icons-webfont`](https://tabler.io/icons) convention).
- **Vizor.Icons.Tabler / Kebechet.Blazor.Tabler.Icons target:**
  `<TablerIcon Icon="@TablerIconName.Home" />`.
- **Blazicons.Tabler / similar generic-wrapper target:** `<Blazicon Svg="TablerIcon.Home" />`
  — note the generic `<Blazicon>` wrapper; consumers migrating off Blazicons will
  find-and-replace to `<TablerIcon Name="TablerIconName.Home" />`.
- **Sunfish compat target:** `<TablerIcon Name="TablerIconName.Home" Stroke="2.0" />`.
- **Rendered markup:**
  `<i class="tabler tabler-home sf-tabler-icon [sf-tabler-icon--*]" style="stroke-width: 2.0" role="..." aria-label="..." ...AdditionalAttributes></i>`.

| Parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Name` | `TablerIconName?` | Translated via `TablerIconNameExtensions.ToSlug(TablerIconName)` → emitted as `tabler-<slug>` on the `<i>` element. | Supported |
| `NameString` | `string?` | Emitted verbatim as `tabler-<NameString>`. Bypasses the typed-enum. Useful for icons outside the 50-icon starter-set. When both `Name` and `NameString` are set, `NameString` wins and a warning is logged. | Supported |
| `Size` | `Sunfish.Foundation.Enums.IconSize?` | Small → `sf-tabler-icon--sm`, Medium → `sf-tabler-icon--md`, Large → `sf-tabler-icon--lg`, ExtraLarge → `sf-tabler-icon--xl`. Null → no size class (inherits ambient font-size). | Supported |
| `AriaLabel` | `string?` | When set, emits `role="img" aria-label="..."`; otherwise `role="presentation" aria-hidden="true"`. | Supported |
| **`Stroke`** | **`double?`** | **Tabler's distinguishing parameter — stroke-width. When set, emits `style="stroke-width: <value>"` (invariant-culture formatted). Null → no inline style; inherits upstream / consumer stylesheet defaults (Tabler's upstream default is `2.0`). Pass-through to the element so it flows to the CSS `stroke-width` property consumed by Tabler's SVG / font-face stylesheet.** | **Supported (pass-through)** |
| `Class` / other attributes | — | Forwarded via `AdditionalAttributes` — merged with the compat-emitted class string. | Forwarded |

### Divergences

- **Parameter name divergence from some vendor wrappers.** `Vizor.Icons.Tabler` /
  `Kebechet.Blazor.Tabler.Icons` use `Icon=` for the icon identifier. This package
  uses `Name=` for consistency across the Sunfish compat-icon family
  (`BootstrapIcon`, `LucideIcon`, `MaterialIcon`, etc. all use `Name=`). Consumers
  can bulk-rename with a single sed:
  `s/Icon="@TablerIconName\./Name="TablerIconName./g`. Consumers migrating off
  `Blazicons.Tabler` should adapt from `<Blazicon Svg="TablerIcon.Home" />` to
  `<TablerIcon Name="TablerIconName.Home" />`.
- **`Stroke` is Tabler's distinguishing parameter.** Most icon-compat wrappers (Lucide,
  Bootstrap Icons, Heroicons) do not expose a stroke-width parameter at all —
  consumers control stroke via their own CSS. Tabler's upstream Blazor wrappers
  conventionally expose `Stroke` as a first-class parameter because Tabler's default
  icon style is outlined and the stroke-width is the most commonly-tuned visual knob.
  Sunfish `compat-tabler-icons` honors that convention by emitting the value as an
  inline `style="stroke-width: ..."` on the element — consumers get single-site
  control without having to change their CSS.
- **Rendering path matches `compat-lucide` / `compat-bootstrap-icons`, not `compat-font-awesome`.**
  `FontAwesomeIcon` delegates to `SunfishIcon` and lets the active
  `ISunfishIconProvider` resolve the glyph. This package instead emits the native
  `<i class="tabler tabler-*">` markup directly — even though Tabler is SVG-based
  and provider-delegation is technically available. The trade-off is: this package
  preserves the Tabler visual exactly (when the consumer's Tabler CSS / font-face /
  sprite pipeline is loaded), at the cost of not picking up the active Sunfish
  adapter's icon set. Choose this package when you want the Tabler visual; choose
  `SunfishIcon` when you want the active adapter's icon set; register
  `Sunfish.Icons.Tabler` (the native provider) when you want Tabler as the Sunfish
  default.
- **Tabler CSS / assets must be loaded by the consumer.** The compat package does
  NOT emit a `<link>` or bundle any SVG/font assets. Add the upstream Tabler CSS
  (font-face or SVG sprite) to your app's host page (typically `App.razor` /
  `index.html`). For font-face consumers, the canonical pattern is:
  ```html
  <link rel="stylesheet" href="https://unpkg.com/@tabler/icons-webfont/dist/tabler-icons.min.css">
  ```
  (Or via npm / self-hosted.) Without it, the `<i>` element renders empty.
- **`Size` semantics use a Sunfish-prefixed class.** Tabler has no native `Size`
  parameter (sizing is done via `width`/`height` on the SVG or CSS font-size on the
  `<i>`). This package accepts the Sunfish `IconSize` enum and maps to
  Sunfish-prefixed modifier classes (`sf-tabler-icon--sm/md/lg/xl`). Consumers who
  need finer control can omit `Size` (inheriting ambient font-size) and apply
  their own `style="font-size:..."` via attribute splat, or ship CSS rules for the
  `sf-tabler-icon--*` modifiers in their own stylesheet.
- **`Name` vs `NameString` precedence.** When both are set, `NameString` wins and a
  warning is logged. Intentional source-shape-parity trade-off — migrators may have
  a mix of typed and string-literal icon call sites; the raw string is preserved
  verbatim (debuggable) rather than silently dropping it in favor of the enum.

---

## TablerIconName starter set

Phase 3E ships a **50-icon starter set** covering common UI needs. Tabler's upstream
catalog exceeds 5,000 icons.

| Category | Icons |
|---|---|
| Core UI / navigation | `Home`, `Search`, `Settings`, `User`, `Menu2`, `X` |
| Direction / checkmarks | `Check`, `ChevronUp`, `ChevronDown`, `ChevronLeft`, `ChevronRight`, `ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight` |
| Communication | `Mail`, `Phone`, `Calendar`, `Clock`, `MessageCircle` |
| Files / media | `Folder`, `FileText`, `Photo`, `Video`, `Music` |
| Editing actions | `DeviceFloppy` (save), `Pencil`, `Trash`, `Plus`, `Minus` |
| Social / sharing | `Heart`, `Bookmark`, `Share`, `Copy`, `Printer` |
| Transfer | `Download`, `Upload` |
| Status / alerts | `InfoCircle`, `AlertTriangle`, `AlertCircle`, `CircleCheck` |
| Data / layout | `LayoutGrid`, `List`, `Filter`, `ArrowsSort` |
| Media control | `PlayerPlay`, `PlayerPause`, `PlayerStop`, `Eye`, `EyeOff`, `Lock` |

Consumers whose icons are not in the starter set can:

1. Use the `NameString` parameter to pass the raw Tabler slug:
   `<TablerIcon NameString="rocket" />`.
2. Submit a policy-gated PR to extend the `TablerIconName` enum and `ToSlug()` map.

### Divergences

- **Not the full catalog.** Shipping 5,000+ enum members would inflate the package
  and lock us into upstream Tabler naming evolutions. The starter set is intentionally
  scoped to the most common icons; expanding it is cheap but deliberate.
- **Naming is hand-authored.** `ToSlug()` is a `switch` expression rather than a
  derived `ToString().ToLowerKebab()`, because Tabler preserves numeric-suffix
  variants (`menu-2`) and uses prefix-first ordering for compound names (`circle-check`,
  not `check-circle`) that need to match upstream exactly. Future additions should
  add an explicit arm.
- **Vocabulary mismatches vs. Lucide / Heroicons.** Migrators coming from Lucide may
  be surprised by a few upstream name differences — the starter set uses Tabler's
  vocabulary, not the cross-library common name. A quick cheat sheet:

  | Concept | Tabler slug | Lucide slug |
  |---|---|---|
  | Save | `device-floppy` | `save` |
  | Edit | `pencil` | `edit` |
  | Image | `photo` | `image` |
  | Hamburger menu | `menu-2` | `menu` |
  | Status-success circle | `circle-check` | `check-circle` |
  | Status-info circle | `info-circle` | `info` |
  | Play (media) | `player-play` | `play` |
  | Sort | `arrows-sort` | `arrow-down-up` |
- **Tabler version drift.** Tabler renames and adds icons between major releases.
  The starter set reflects stable names as of the Phase 3E ship date; if upstream
  renames a starter-set icon, we route the change through the policy gate (new enum
  member alongside the old, deprecation window).

---

## Future coverage

Deferred to follow-up PRs (track as separate ICM intake items if prioritized):

- **Enum starter-set expansion** — per-request additions under the policy gate.
- **Filled-variant identifier split** — Tabler now ships both outline (default) and
  filled glyphs. Phase 3E treats them as a single namespace; a future PR may add a
  `Variant` parameter (Outline / Filled) or a separate `TablerIconFilled` enum.
  Additive; does not break the current surface.
- **Inline SVG rendering mode** — a future mode where the compat package inlines
  Tabler SVGs (MIT, small, under 5MB total at standard sizes) rather than relying on
  the consumer's font-face / CSS. Would eliminate the Tabler-CSS setup step but
  violates the no-vendor-NuGet invariant unless we vendor the SVGs into the Sunfish
  repo — a separate policy decision (and the same decision seeded in
  `compat-lucide-mapping.md` / `compat-bootstrap-icons-mapping.md`).
- **Provider-delegation mode** — optional "render via `SunfishIcon` instead of
  direct emit" parameter, for consumers who want the active adapter's icon set.
  Would be additive; the default stays direct-emit to preserve Phase 3E behavior.
- **Bridge to native `Sunfish.Icons.Tabler` provider** — a consumer-opt-in switch
  to delegate rendering to the native provider when it is registered, reusing its
  SVG pipeline. Would remove the consumer-side asset-loading step for the
  double-adopter case. Deferred pending migrator demand.

---

## Notes for compat-icon-expansion Phase 3 / SVG-based icon libraries

Shared-pattern cross-reference for reviewers — see
[`docs/compat-lucide-mapping.md`](./compat-lucide-mapping.md#notes-for-compat-icon-expansion-phase-3--svg-based-icon-libraries)
for the Phase 3 pattern this package inherits verbatim (direct-emit rendering, typed
enum + `NameString` escape hatch, `tabler / sf-tabler-icon` split-class preservation,
Sunfish-prefixed size classes, `Name=` convergence across the compat-icon family).

Phase 3E's one addition on top of that pattern: **first-class vendor-distinguishing
parameter (`Stroke`)**. Future SVG-based compat packages should evaluate whether
their vendor has an analogous single knob worth surfacing (e.g. Heroicons' size /
variant discriminator).

---

## License & attribution

- **Tabler Icons license:** [MIT](https://github.com/tabler/tabler-icons/blob/main/LICENSE).
- **MIT is permissive**; no attribution clause beyond the standard copyright notice
  preservation.
- **This package ships no Tabler assets** — consumers retain responsibility for their
  own Tabler asset pipeline and for any attribution their chosen distribution channel
  requires.
