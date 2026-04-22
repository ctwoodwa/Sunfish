# compat-heroicons Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Heroicons code maps to after migration. Any change to an entry (promoting
> a parameter from "mapped" to "throws", changing a default, adding a divergence,
> expanding the `HeroiconName` starter set or `HeroiconVariant` surface) is a **breaking
> change** for consumers and must land under the policy gate in the same PR as the
> code change. See `packages/compat-heroicons/POLICY.md`.

## Conventions

- **Mapped** — Parameter value translates 1:1 to a Sunfish parameter / CSS class on
  the emitted `<i>`.
- **Forwarded** — Attribute is passed through via `AdditionalAttributes` (e.g. `class`,
  `style`, `tabindex`). No semantic transform.
- **LogAndFallback** — Value is accepted but not implemented; a warning is logged via
  `ILogger` and rendering falls back to the default behavior. Reserved for cosmetic
  parameters with no functional impact.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values
  that would silently change behavior if dropped, or that are out of scope for this
  phase.
- **Not-in-scope** — Intentionally not mirrored; consumers who need the feature should
  pass through `AdditionalAttributes` / `style` or maintain a parallel code path.

---

## Heroicon

- **Upstream target (framework-agnostic):** `<svg class="w-6 h-6" ...>…</svg>` rendered
  from one of the three Heroicons SVG sets (outline / solid / mini).
- **Blazor.Heroicons target:** `<Heroicon Name="@HeroiconName.Home" Type="HeroiconType.Outline" />`
  (the upstream Blazor wrapper uses `Type`; this compat package renames to `Variant`
  for consistency with `MaterialSymbolVariant`).
- **Sunfish compat target:** `<Heroicon Name="HeroiconName.Home" Variant="HeroiconVariant.Outline" />`.
- **Rendered markup:** `<i class="heroicon heroicon-<slug> heroicon-<variant>
  sf-heroicon [sf-heroicon-size-*]" role="..." aria-label="..."
  ...AdditionalAttributes></i>`.

| Parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Name` | `HeroiconName?` | Translated via `HeroiconNameExtensions.ToSlug(HeroiconName)` → emitted as `heroicon-<slug>` on the `<i>` element. | Supported |
| `NameString` | `string?` | Emitted verbatim as `heroicon-<NameString>`. Bypasses the typed-enum. Useful for icons outside the 50-icon starter-set. When both `Name` and `NameString` are set, `NameString` wins and a warning is logged. | Supported |
| `Variant` | `HeroiconVariant` | `Outline`→`heroicon-outline` (default), `Solid`→`heroicon-solid`, `Mini`→`heroicon-mini`. Matches Heroicons' own default of Outline. | Supported |
| `Size` | `Sunfish.Foundation.Enums.IconSize?` | Small → `sf-heroicon-size-sm`, Medium → `sf-heroicon-size-md`, Large → `sf-heroicon-size-lg`, ExtraLarge → `sf-heroicon-size-xl`. Null → no size class (inherits consumer-side sizing, typically Tailwind `w-6 h-6` utilities). | Supported |
| `AriaLabel` | `string?` | When set, emits `role="img" aria-label="..."`; otherwise `role="presentation" aria-hidden="true"`. | Supported |
| `Class` / other attributes | — | Forwarded via `AdditionalAttributes` — merged with the compat-emitted class string. | Forwarded |
| Blazor.Heroicons `Type` | — | Renamed to `Variant` for consistency with `MaterialSymbolVariant`. Existing `Type="HeroiconType.Outline"` call sites migrate to `Variant="HeroiconVariant.Outline"`. | Renamed |
| Per-icon `StrokeWidth` | — | Not mirrored in this phase — Heroicons 2.0's per-icon stroke tokens have no Sunfish contract. Route via `AdditionalAttributes` / `style="stroke-width:..."` or Tailwind utility classes. | Not-in-scope |
| Per-icon `Color` | — | Not mirrored — Sunfish has no per-icon color primitive in this phase; style via `class` / `style` on the host `<i>` or a parent container. | Not-in-scope |
| Inline-SVG rendering (`<svg>` emission) | — | Not mirrored — `compat-heroicons` emits the `<i class="heroicon-*">` shell only. Consumers inline SVGs via a build-time or runtime pipeline of their choice (NPM, CDN, self-hosted). | Not-in-scope |

### Variant vocabulary

Tailwind Labs publishes Heroicons in three distinct variants that have different
geometry and intended usage:

| Variant | Geometry | Typical sizing | Emitted class |
|---|---|---|---|
| `Outline` | 24×24 stroke-based (1.5px stroke) | `w-6 h-6` | `heroicon-outline` |
| `Solid` | 24×24 filled | `w-6 h-6` | `heroicon-solid` |
| `Mini` | 20×20 filled (tighter forms for small UIs) | `w-5 h-5` | `heroicon-mini` |

`HeroiconVariant.Outline` is the default so unparameterized
`<Heroicon Name="@HeroiconName.Home" />` markup matches Heroicons' own documented
default variant.

### Divergences

- **Parameter rename — `Type` → `Variant`.** Blazor.Heroicons uses `Type` as the
  parameter name for the variant selector. This compat package uses `Variant` for
  consistency with `MaterialSymbolVariant` (compat-material-icons, Phase 2 precedent).
  Consumers migrating from Blazor.Heroicons rename their `Type="HeroiconType.Outline"`
  to `Variant="HeroiconVariant.Outline"`. The underlying enum values map 1:1.
- **Rendering path differs from `FontAwesomeIcon`.** `FontAwesomeIcon` (Phase 1)
  delegates to `SunfishIcon` and lets the active `ISunfishIconProvider` resolve the
  glyph. `Heroicon` instead emits the native `<i class="heroicon-*">` markup
  directly — Heroicons ship as hand-authored SVGs whose outline/solid/mini variants
  carry visually distinct geometry that a generic provider substitution would erase.
  This follows the `compat-bootstrap-icons` precedent.
- **No inline SVG body.** Blazor.Heroicons inlines `<svg>…</svg>` via RCL static
  assets. `compat-heroicons` emits only the class-based hook; the consumer-side
  pipeline is responsible for rendering the glyph (see Asset shipping below).
- **50-icon starter set, not the full ~300-icon catalog.** The full Heroicons
  catalog covers ~300 icons × 3 variants. The starter set covers the most common
  migrators-reach-for-first icons; consumers outside the starter set use
  `NameString`.

---

## Typed Identifier Enum (`HeroiconName`)

- **Heroicons source:** Blazor.Heroicons exposes `HeroiconName.Academic_Cap`,
  `HeroiconName.Home`, etc. as a flat enum — ~300 members covering the full catalog.
- **Sunfish surface:** A 50-member flat `HeroiconName` enum of typed identifiers.
  Each member maps to its canonical kebab-case Heroicons slug via
  `HeroiconNameExtensions.ToSlug(HeroiconName)`. The variant is selected separately
  via the `Variant` parameter (same upstream slug across all three variants).

### Starter-set coverage

| Category | Members |
|---|---|
| Navigation & chrome | `Home`, `MagnifyingGlass`, `Cog6Tooth`, `User`, `Bars3`, `XMark`, `Check` |
| Arrows | `ArrowLeft`, `ArrowRight`, `ArrowUp`, `ArrowDown`, `ChevronUp`, `ChevronDown`, `ChevronLeft`, `ChevronRight` |
| Communication | `Envelope`, `Phone`, `Calendar`, `Clock`, `ChatBubbleLeft` |
| Content types | `Folder`, `Document`, `Photo`, `Film`, `MusicalNote`, `BookmarkSquare` |
| Editing actions | `Pencil`, `Trash`, `Plus`, `Minus` |
| Social / sharing | `Heart`, `Bookmark`, `Share`, `DocumentDuplicate`, `Printer` |
| Transfer | `ArrowDownTray` (download), `ArrowUpTray` (upload) |
| Status / alerts | `InformationCircle`, `ExclamationTriangle`, `XCircle`, `CheckCircle` |
| Data / layout | `Squares2x2` (grid), `ListBullet`, `Funnel` (filter), `ArrowsUpDown` (sort) |
| Media control | `Play`, `Pause`, `Stop` |
| Visibility & security | `Eye`, `LockClosed` |

### Divergences

- **PascalCase member names, kebab-case slugs.** Heroicons slugs are kebab-case
  (`magnifying-glass`, `cog-6-tooth`, `arrow-down-tray`). The enum members use
  `PascalCase` per .NET conventions; `HeroiconNameExtensions.ToSlug` maps each
  member to its canonical slug. The map is hand-authored rather than derived from
  `ToString()` because several names embed numeric tokens that a naive transform
  would mis-split:

  | Enum member | Canonical slug |
  |---|---|
  | `MagnifyingGlass` | `magnifying-glass` |
  | `Cog6Tooth` | `cog-6-tooth` |
  | `Bars3` | `bars-3` |
  | `Squares2x2` | `squares-2x2` |
  | `ArrowDownTray` | `arrow-down-tray` |

- **Flat, not nested by variant.** Blazor.Heroicons uses a single flat enum;
  Heroicons' three variants share the same slug surface (e.g. `home` is a valid
  slug for outline, solid, and mini). This compat surface preserves that shape —
  there is no `HeroiconName.Outline.Home` form.

---

## Asset Shipping — Consumer Responsibility

`compat-heroicons` **does not ship any SVG files, CSS, or `<link>`/`<script>` tags**.
The consumer remains responsible for loading Heroicons' SVGs via one of:

- NPM install (`@heroicons/react` / `@heroicons/vue`) and wiring through an
  existing JS bundler / front-end build pipeline.
- Self-hosting the SVG files from the Heroicons GitHub release.
- CDN-referencing the SVGs from a public mirror.
- An existing `Blazor.Heroicons` / `TailBlazor.HeroIcons` asset layout (no change
  needed during migration — the wrappers bundle their own RCL static assets; this
  compat package does not, per Hard Invariant #1).

If the SVG pipeline is not loaded, the `<i>` element renders empty. This is the
same failure mode as the upstream Heroicons rendering path and is intentional:
Hard Invariant #1 (no vendor NuGet dependency) means no asset shipping either.

The wrapper renders `<i class="heroicon heroicon-<slug> heroicon-<variant>
sf-heroicon">` markup. Consumers typically style this via a CSS `::before`
pseudo-element with the corresponding SVG, via a JS `document.querySelectorAll`
swap, or via a build-time transform — all of which are consumer-side.

---

## Consumer CSS Loading Notes

Because this package ships no CSS, consumers choose how to resolve the
`heroicon-*` classes. Common patterns:

1. **Tailwind + inline SVG via shadcn/ui-style pipeline.** Use the upstream
   `@heroicons/react` or `@heroicons/vue` package through an existing JS build,
   and treat the `heroicon-*` classes emitted by this compat wrapper as semantic
   hooks for `@apply`-style Tailwind utilities.
2. **Pure-CSS background-image swap.** Self-host the SVGs and style each
   `.heroicon-<slug>.heroicon-<variant>` selector with a
   `background-image: url(...)` rule. Works without a JS bundler.
3. **Blazor.Heroicons bundled assets.** Keep the existing Blazor.Heroicons `using`
   alongside `using Sunfish.Compat.Heroicons` during incremental migration; the
   compat wrapper and the upstream wrapper can coexist because neither bundles
   CSS that would collide.

The `sf-heroicon` class is preserved alongside the `heroicon-*` classes on every
rendered element so Sunfish-aware CSS can style the wrapper independently of
upstream Heroicons visuals.

---

## Future Coverage

Candidates for future policy-gated PRs:

- **Source-generated typed catalog** covering all ~300 Heroicons. Hand-maintenance
  does not scale past the current 50-entry starter set.
- **`StrokeWidth` parameter** on `Outline`-variant renders once Sunfish exposes a
  stroke-width contract (Heroicons 2.0 publishes per-icon stroke tokens).
- **Inline-SVG rendering mode** as an alternative to the class-hook approach. Would
  require a ui-core contract extension to carry SVG bodies without violating Hard
  Invariant #1.
- **`Color` parameter** once Sunfish adds a first-class icon-color primitive.
- **Provider-delegation mode** for consumers willing to accept cross-library glyph
  substitution in exchange for provider-consistent visuals (following the
  `FontAwesomeIcon` Phase 1 pattern).
