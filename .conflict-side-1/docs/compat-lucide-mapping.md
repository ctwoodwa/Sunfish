# compat-lucide Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Lucide code maps to after migration. Any change to an entry (promoting a
> parameter from "mapped" to "throws", changing a default, adding a divergence,
> expanding the `LucideIconName` starter set) is a **breaking change** for consumers
> and must land under the policy gate in the same PR as the code change. See
> `packages/compat-lucide/POLICY.md`.

## Conventions

- **Mapped** — Parameter value translates 1:1 to a Sunfish parameter / CSS class on
  the emitted `<i>`.
- **Forwarded** — Attribute is passed through via `AdditionalAttributes` (e.g. `class`,
  `style`, `tabindex`). No semantic transform.
- **LogAndFallback** — Value is accepted but not implemented; a warning is logged via
  `ILogger` and rendering falls back to the default behavior. Reserved for cosmetic
  parameters with no functional impact.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values
  that would silently change behavior if dropped, or that are out of scope for Phase 3A.

---

## LucideIcon

- **Upstream target (vendor-agnostic):** `<i class="lucide lucide-home"></i>` when
  paired with a Lucide font-face / CSS integration.
- **InfiniLore.Lucide / BlazorBlueprint.Icons.Lucide target:**
  `<LucideIcon Icon="@LucideIcons.Home" />`.
- **Blazicons.Lucide target:** `<Blazicon Svg="LucideIcon.Home" />` — note the generic
  `<Blazicon>` wrapper; consumers migrating off Blazicons will find-and-replace to
  `<LucideIcon Name="LucideIconName.Home" />`.
- **Sunfish compat target:** `<LucideIcon Name="LucideIconName.Home" />`.
- **Rendered markup:** `<i class="lucide lucide-home sf-lucide-icon [sf-lucide-icon--*]"
  role="..." aria-label="..." ...AdditionalAttributes></i>`.

| Parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Name` | `LucideIconName?` | Translated via `LucideIconNameExtensions.ToSlug(LucideIconName)` → emitted as `lucide-<slug>` on the `<i>` element. | Supported |
| `NameString` | `string?` | Emitted verbatim as `lucide-<NameString>`. Bypasses the typed-enum. Useful for icons outside the 50-icon starter-set. When both `Name` and `NameString` are set, `NameString` wins and a warning is logged. | Supported |
| `Size` | `Sunfish.Foundation.Enums.IconSize?` | Small → `sf-lucide-icon--sm`, Medium → `sf-lucide-icon--md`, Large → `sf-lucide-icon--lg`, ExtraLarge → `sf-lucide-icon--xl`. Null → no size class (inherits ambient font-size). | Supported |
| `AriaLabel` | `string?` | When set, emits `role="img" aria-label="..."`; otherwise `role="presentation" aria-hidden="true"`. | Supported |
| `Class` / other attributes | — | Forwarded via `AdditionalAttributes` — merged with the compat-emitted class string. | Forwarded |

### Divergences

- **Parameter name divergence from some vendor wrappers.** InfiniLore.Lucide /
  BlazorBlueprint.Icons.Lucide use `Icon=` for the icon identifier. This package uses
  `Name=` for consistency across the Sunfish compat-icon family (`BootstrapIcon`,
  `MaterialIcon`, etc. all use `Name=`). Consumers can bulk-rename with a single
  sed: `s/Icon="@LucideIcons\./Name="LucideIconName./g`. Consumers migrating off
  `Blazicons.Lucide` should adapt from `<Blazicon Svg="LucideIcon.Home" />` to
  `<LucideIcon Name="LucideIconName.Home" />`.
- **Rendering path matches `compat-bootstrap-icons`, not `compat-font-awesome`.**
  `FontAwesomeIcon` delegates to `SunfishIcon` and lets the active
  `ISunfishIconProvider` resolve the glyph. This package instead emits the native
  `<i class="lucide lucide-*">` markup directly — even though Lucide is SVG-based
  and provider-delegation is technically available. The trade-off is: this package
  preserves the Lucide visual exactly (when the consumer's Lucide CSS / font-face /
  sprite pipeline is loaded), at the cost of not picking up the active Sunfish
  adapter's icon set. Choose this package when you want the Lucide visual; choose
  `SunfishIcon` when you want the active adapter's icon set.
- **Lucide CSS / assets must be loaded by the consumer.** The compat package does
  NOT emit a `<link>` or bundle any SVG/font assets. Add the upstream Lucide CSS
  (font-face or SVG sprite) to your app's host page (typically `App.razor` /
  `index.html`). For font-face consumers, the canonical pattern is:
  ```html
  <link rel="stylesheet" href="https://unpkg.com/lucide-static@latest/font/lucide.css">
  ```
  (Or via npm / self-hosted.) Without it, the `<i>` element renders empty.
- **`Size` semantics use a Sunfish-prefixed class.** Lucide has no native `Size`
  parameter (sizing is done via `width`/`height` on the SVG or CSS font-size on the
  `<i>`). This package accepts the Sunfish `IconSize` enum and maps to
  Sunfish-prefixed modifier classes (`sf-lucide-icon--sm/md/lg/xl`). Consumers who
  need finer control can omit `Size` (inheriting ambient font-size) and apply
  their own `style="font-size:..."` via attribute splat, or ship CSS rules for the
  `sf-lucide-icon--*` modifiers in their own stylesheet.
- **`Name` vs `NameString` precedence.** When both are set, `NameString` wins and a
  warning is logged. Intentional source-shape-parity trade-off — migrators may have
  a mix of typed and string-literal icon call sites; the raw string is preserved
  verbatim (debuggable) rather than silently dropping it in favor of the enum.

---

## LucideIconName starter set

Phase 3A ships a **50-icon starter set** covering common UI needs. Lucide's upstream
catalog exceeds 1,400 icons.

| Category | Icons |
|---|---|
| Core UI / navigation | `Home`, `Search`, `Settings`, `User`, `Menu`, `X` |
| Direction / checkmarks | `Check`, `ArrowLeft`, `ArrowRight`, `ArrowUp`, `ArrowDown`, `ChevronUp`, `ChevronDown`, `ChevronLeft`, `ChevronRight` |
| Communication | `Mail`, `Phone`, `Calendar`, `Clock`, `MessageCircle` |
| Files / media | `Folder`, `FileText`, `Image`, `Video`, `Music` |
| Editing actions | `Save`, `Edit`, `Trash2`, `Plus`, `Minus` |
| Social / sharing | `Heart`, `Bookmark`, `Share2`, `Copy`, `Printer` |
| Transfer | `Download`, `Upload` |
| Status / alerts | `Info`, `AlertTriangle`, `AlertCircle`, `CheckCircle` |
| Data / layout | `LayoutGrid`, `List`, `Filter`, `ArrowDownUp` |
| Media control | `Play`, `Pause`, `Square`, `Eye`, `Lock` |

Consumers whose icons are not in the starter set can:

1. Use the `NameString` parameter to pass the raw Lucide slug:
   `<LucideIcon NameString="rocket" />`.
2. Submit a policy-gated PR to extend the `LucideIconName` enum and `ToSlug()` map.

### Divergences

- **Not the full catalog.** Shipping 1,400+ enum members would inflate the package
  and lock us into upstream Lucide naming evolutions. The starter set is intentionally
  scoped to the most common icons; expanding it is cheap but deliberate.
- **Naming is hand-authored.** `ToSlug()` is a `switch` expression rather than a
  derived `ToString().ToLowerKebab()`, because Lucide preserves numeric-suffix
  variants (`trash-2`, `share-2`) that would not survive a naive PascalCase→kebab
  transform cleanly. Future additions should add an explicit arm.
- **Lucide version drift.** Lucide occasionally renames icons between major
  versions (the Feather-origin library has evolved its naming). The starter set
  reflects the stable names as of the Phase 3A ship date; if upstream renames a
  starter-set icon, we route the change through the policy gate (new enum member
  alongside the old, deprecation window).

---

## Future coverage

Deferred to follow-up PRs (track as separate ICM intake items if prioritized):

- **Enum starter-set expansion** — per-request additions under the policy gate.
- **Variant/stroke-width controls** — Lucide SVGs accept a `stroke-width` attribute
  upstream. Not covered in Phase 3A; evaluate if migration traffic requests them.
  Would be a new parameter, not a breaking change.
- **Inline SVG rendering mode** — a future mode where the compat package inlines
  Lucide SVGs (licensed ISC, small, well under a megabyte total) rather than
  relying on the consumer's font-face / CSS. Would eliminate the Lucide-CSS
  setup step but violates the no-vendor-NuGet invariant unless we vendor the SVGs
  into the Sunfish repo — a separate policy decision (and the same decision seeded
  in `compat-bootstrap-icons-mapping.md`'s future-coverage section).
- **Provider-delegation mode** — optional "render via `SunfishIcon` instead of
  direct emit" parameter, for consumers who want the active adapter's icon set.
  Would be additive; the default stays direct-emit to preserve Phase 3A behavior.

---

## Notes for compat-icon-expansion Phase 3 / SVG-based icon libraries

These notes seed the architecture decisions for the remaining SVG-based icon-compat
packages shipping after Phase 3A:

1. **SVG-based icon libraries can still use direct-emit.** The intake doc noted
   that SVG-based libraries (Lucide, Heroicons) *may* instead delegate to
   `SunfishIcon`. `compat-lucide` chose direct-emit anyway — the consumer has
   already paid the asset-loading cost (they came to this package because they
   have a Lucide pipeline set up), and direct-emit preserves the exact upstream
   visual. Phase 3B (Heroicons) should evaluate the same trade-off with the same
   criteria: *does the consumer want the vendor's visual preserved exactly, or
   are they willing to let the active Sunfish adapter substitute?*
2. **Typed enum vs raw string escape hatch.** Shipping `LucideIconName` alongside
   `NameString` lets consumers use the typed call-site for common icons and the
   raw-string call-site for everything else. Phase 3B+ packages should replicate
   this dual-shape to avoid forcing PRs for every missing icon.
3. **Class-name preservation.** The wrapper emits both upstream
   (`lucide lucide-*`) and Sunfish-prefixed (`sf-lucide-icon`) classes —
   consistent with the `compat-bootstrap-icons` `bi / sf-bi-icon` split and the
   `compat-font-awesome` `fa-* / sf-fa-*` split. Phase 3B+ packages should
   preserve the same split.
4. **Size-keyword translation uses Sunfish-prefixed classes when the vendor has
   no native utility.** Bootstrap Icons has `fs-*` — compat-bootstrap-icons maps
   to those. Lucide has no native size utility — compat-lucide maps to
   Sunfish-prefixed modifiers (`sf-lucide-icon--sm`). Per-package judgement; the
   constant is: every icon-compat package accepts `Sunfish.Foundation.Enums.IconSize`.
5. **Parameter-name choice (`Name` vs vendor's `Icon`).** The Sunfish compat-icon
   family converges on `Name=` across every wrapper. When a vendor wrapper
   originally uses `Icon=`, we document the rename divergence and provide a
   single-line find-and-replace migration hint.

---

## License & attribution

- **Lucide license:** ISC (a Feather fork; the repo's LICENSE is the standard ISC text).
- **ISC is permissive** (≈ MIT-equivalent); no attribution clause beyond the standard
  copyright notice preservation.
- **This package ships no Lucide assets** — consumers retain responsibility for their
  own Lucide asset pipeline and for any attribution their chosen distribution channel
  requires.
