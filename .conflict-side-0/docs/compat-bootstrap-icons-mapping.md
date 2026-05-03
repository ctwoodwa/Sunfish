# compat-bootstrap-icons Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Bootstrap Icons code maps to after migration. Any change to an entry
> (promoting a parameter from "mapped" to "throws", changing a default, adding a
> divergence, expanding the `IconName` starter set) is a **breaking change** for
> consumers and must land under the policy gate in the same PR as the code change. See
> `packages/compat-bootstrap-icons/POLICY.md`.

## Conventions

- **Mapped** — Parameter value translates 1:1 to a Sunfish parameter / CSS class on
  the emitted `<i>`.
- **Forwarded** — Attribute is passed through via `AdditionalAttributes` (e.g. `class`,
  `style`, `tabindex`). No semantic transform.
- **LogAndFallback** — Value is accepted but not implemented; a warning is logged via
  `ILogger` and rendering falls back to the default behavior. Reserved for cosmetic
  parameters with no functional impact.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values
  that would silently change behavior if dropped, or that are out of scope for Phase 1.

---

## BootstrapIcon

- **Upstream target (framework-agnostic):** `<i class="bi bi-star"></i>`
- **BlazorBootstrap target:** `<Icon Name="IconName.Star" />` — note the unqualified
  type name `Icon`.
- **Sunfish compat target:** `<BootstrapIcon Name="IconName.Star" />` (rename note
  below).
- **Rendered markup:** `<i class="bi bi-star sf-bi-icon [fs-*]" role="..."
  aria-label="..." ...AdditionalAttributes></i>`.

| Parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Name` | `IconName?` | Translated via `IconNameExtensions.ToSlug(IconName)` → emitted as `bi-<slug>` on the `<i>` element. | Supported |
| `NameString` | `string?` | Emitted verbatim as `bi-<NameString>`. Bypasses the typed-enum. Useful for icons outside the 50-icon starter-set. When both `Name` and `NameString` are set, `NameString` wins and a warning is logged. | Supported |
| `Size` | `Sunfish.Foundation.Enums.IconSize?` | Small → `fs-6`, Medium → `fs-5`, Large → `fs-3`, ExtraLarge → `fs-1`. Null → no size class (inherits ambient font-size, matching upstream Bootstrap Icons behavior). | Supported |
| `AriaLabel` | `string?` | When set, emits `role="img" aria-label="..."`; otherwise `role="presentation" aria-hidden="true"`. | Supported |
| `Class` / other attributes | — | Forwarded via `AdditionalAttributes` — merged with the compat-emitted class string. | Forwarded |

### Divergences

- **Name divergence from BlazorBootstrap.** BlazorBootstrap ships its wrapper as
  the unqualified type `Icon`. This package renames to `BootstrapIcon` to
  disambiguate against parallel compat packages (`FontAwesomeIcon`, `MaterialIcon`,
  `FluentIcon`) that share the `Sunfish.Compat.*` root namespace family. Consumers
  who want to preserve the `using BlazorBootstrap;` call-site shape can alias:
  ```csharp
  using Icon = Sunfish.Compat.BootstrapIcons.BootstrapIcon;
  ```
  Behavior is otherwise identical — the same `Name="IconName.*"` markup compiles
  unchanged after the alias.
- **Rendering path differs from `FontAwesomeIcon`.** `FontAwesomeIcon` delegates to
  `SunfishIcon` and lets the active `ISunfishIconProvider` resolve the glyph. This
  package instead emits the native `<i class="bi bi-*">` markup directly, because
  Bootstrap Icons ship as a CSS pseudo-element font — the upstream visual requires
  the `bi-*` class on the element. The trade-off is: this package preserves the
  Bootstrap Icons glyph exactly (when `bootstrap-icons.css` is loaded), while
  `FontAwesomeIcon`'s provider-delegation path may visually substitute based on
  the active Sunfish `IconProvider`. Choose this package when you want the
  Bootstrap Icons visual; choose `SunfishIcon` when you want the active adapter's
  icon set.
- **`bootstrap-icons.css` must be loaded by the consumer.** The compat package
  does NOT emit a `<link>` or bundle the stylesheet. Add the upstream CSS to your
  app's host page (typically `App.razor` / `index.html`) — usually via CDN:
  ```html
  <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.css">
  ```
  (Or via npm / self-hosted.) Without it, the `<i>` element renders empty — the
  glyphs are CSS pseudo-element content keyed to `bi-*` classes.
- **`Size` semantics differ from upstream.** Bootstrap Icons has no native `Size`
  parameter (sizing is done via `font-size` on the surrounding element or
  Bootstrap's `fs-*` utilities). This package accepts the Sunfish
  `IconSize` enum and maps to Bootstrap's `fs-*` utility classes. Consumers who
  need finer control can omit `Size` (inheriting ambient font-size) and apply
  their own `style="font-size:..."` via attribute splat.
- **`Name` vs `NameString` precedence.** When both are set, `NameString` wins and a
  warning is logged. This is an intentional source-shape-parity trade-off —
  migrators may have a mix of typed and string-literal icon call sites; the raw
  string is preserved verbatim (which they can see and debug) rather than
  silently dropping the string in favor of the enum.

---

## IconName starter set

Phase 1 ships a **50-icon starter set** covering common UI needs. Bootstrap Icons'
upstream catalog exceeds 2,000 icons.

| Category | Icons |
|---|---|
| Core UI / navigation | `House`, `Search`, `Gear`, `Person`, `List`, `X` |
| Direction / checkmarks | `Check`, `ArrowLeft`, `ArrowRight`, `ArrowUp`, `ArrowDown`, `ChevronUp`, `ChevronDown`, `ChevronLeft`, `ChevronRight` |
| Communication | `Envelope`, `Telephone`, `Calendar`, `Clock`, `Chat` |
| Files / media | `Folder`, `FileText`, `Image`, `CameraVideo`, `MusicNote` |
| Editing actions | `Save`, `Pencil`, `Trash`, `Plus`, `Dash` |
| Social / sharing | `Heart`, `Bookmark`, `Share`, `Clipboard`, `Printer` |
| Transfer | `Download`, `Upload` |
| Status / alerts | `InfoCircle`, `ExclamationTriangle`, `XCircle`, `CheckCircle` |
| Data / layout | `Grid`, `BarChart`, `Filter`, `SortAlphaDown` |
| Media control | `PlayFill`, `PauseFill`, `StopFill`, `VolumeUp`, `Eye` |

Consumers whose icons are not in the starter set can:

1. Use the `NameString` parameter to pass the raw Bootstrap Icons slug:
   `<BootstrapIcon NameString="rocket-takeoff" />`.
2. Submit a policy-gated PR to extend the `IconName` enum and `ToSlug()` map.

### Divergences

- **Not the full catalog.** Shipping 2,000+ enum members would inflate the package and
  lock us into upstream Bootstrap Icons naming evolutions. The starter set is
  intentionally scoped to the most common icons; expanding it is cheap but deliberate.
- **Naming is hand-authored.** `ToSlug()` is a `switch` expression rather than a
  derived `ToString().ToLowerKebab()`, because some canonical Bootstrap Icons names
  (e.g. `play-fill`, `exclamation-triangle`, `bar-chart`) would not survive a naive
  PascalCase→kebab transform cleanly. Future additions should add an explicit arm.

---

## Future coverage

Deferred to follow-up PRs (track as separate ICM intake items if prioritized):

- **Enum starter-set expansion** — per-request additions under the policy gate.
- **Additional wrapper types** — BlazorBootstrap ships richer icon composition
  primitives (`IconStack`, size/color presets). Not covered in Phase 2C; evaluate
  if migration traffic requests them.
- **Direct SVG inlining** — a future mode where the compat package inlines
  Bootstrap Icons SVGs (licensed MIT, small, ~300KB total) rather than relying on
  the consumer's CSS. Would eliminate the `bootstrap-icons.css` setup step but
  violates the no-vendor-NuGet invariant unless we vendor the SVGs into the
  Sunfish repo — a separate policy decision.
- **Sunfish-native size contract** — `Size` currently maps to Bootstrap `fs-*`
  utilities, which requires Bootstrap's CSS loaded. A future mode could emit
  inline `style="font-size:..."` for consumers who aren't carrying Bootstrap's
  base CSS.

---

## Notes for compat-icon-expansion Phase 3

These notes seed the architecture decisions for the icon-compat packages shipping
after Phase 2:

1. **CSS-only icon libraries render directly, not via `SunfishIcon`.**
   `compat-bootstrap-icons` emits native `<i class="bi bi-*">` markup rather than
   delegating to `SunfishIcon`, because Bootstrap Icons requires the `bi-*` class
   to resolve the glyph. Other CSS-only or font-based icon libraries
   (`compat-octicons` via font, `compat-material-icons` via Material Symbols font)
   should adopt the same direct-emit pattern. SVG-based libraries
   (`compat-lucide`, `compat-heroicons`) may instead delegate to `SunfishIcon`
   since the provider can own the inlined SVG content.
2. **Typed enum vs raw string escape hatch.** Shipping `IconName` alongside
   `NameString` lets consumers use the typed call-site for common icons and the
   raw-string call-site for everything else. Phase-3 packages should replicate
   this dual-shape to avoid forcing PRs for every missing icon.
3. **Class-name preservation.** The wrapper emits both upstream (`bi bi-*`) and
   Sunfish-prefixed (`sf-bi-icon`) classes — consistent with the
   `compat-font-awesome` `sf-fa-*` / `fa-*` split. Phase-3 packages should
   preserve the same split.
4. **Size-keyword translation.** `compat-bootstrap-icons` maps `Sunfish.IconSize`
   to Bootstrap's `fs-*` utilities; `compat-font-awesome` maps FA's
   `xs / sm / 1x-6x` to the same `IconSize` enum. The pattern converges: every
   icon-compat package accepts (or maps to) `Sunfish.Foundation.Enums.IconSize`,
   with per-package translation to whatever CSS class the vendor uses.
