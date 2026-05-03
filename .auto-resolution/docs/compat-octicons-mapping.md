# compat-octicons Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Octicons code maps to after migration. Any change to an entry (promoting a
> parameter from "mapped" to "throws", changing a default, adding a divergence,
> expanding the `OcticonName` starter set) is a **breaking change** for consumers
> and must land under the policy gate in the same PR as the code change. See
> `packages/compat-octicons/POLICY.md`.

## Conventions

- **Mapped** — Parameter value translates 1:1 to a Sunfish parameter / CSS class on
  the emitted `<i>`.
- **Forwarded** — Attribute is passed through via `AdditionalAttributes` (e.g. `class`,
  `style`, `tabindex`). No semantic transform.
- **LogAndFallback** — Value is accepted but not implemented; a warning is logged via
  `ILogger` and rendering falls back to the default behavior. Reserved for cosmetic
  parameters with no functional impact.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values
  that would silently change behavior if dropped, or that are out of scope for Phase 3D.

---

## Octicon

- **Upstream target (vendor-agnostic):** `<svg class="octicon octicon-mark-github">…</svg>`
  (inline SVG from the `@primer/octicons` NPM package) or
  `<i class="octicon octicon-mark-github"></i>` (CSS-font / sprite variants).
- **BlazorOcticons target:** `<Octicon Icon="..." />` — per-icon Blazor components or a
  parameterized wrapper, depending on the package generation.
- **BlazorOcticonsGenerator target:** per-icon components (`<MarkGithub />`, `<Repo />`,
  etc.) via source generation.
- **NgIcons.Octicons target:** `<NgIcon Name="@NgIconName.OctMarkGithub" />` prefixed
  naming inside a multi-library aggregator.
- **Sunfish compat target:** `<Octicon Name="OcticonName.MarkGithub" />`.
- **Rendered markup:** `<i class="octicon octicon-mark-github sf-octicon [sf-octicon--*]"
  role="..." aria-label="..." ...AdditionalAttributes></i>`.

| Parameter | Type | Sunfish mapping | Status |
|---|---|---|---|
| `Name` | `OcticonName?` | Translated via `OcticonNameExtensions.ToSlug(OcticonName)` → emitted as `octicon-<slug>` on the `<i>` element. | Supported |
| `NameString` | `string?` | Emitted verbatim as `octicon-<NameString>`. Bypasses the typed-enum. Useful for icons outside the 50-icon starter-set. When both `Name` and `NameString` are set, `NameString` wins and a warning is logged. | Supported |
| `Size` | `Sunfish.Foundation.Enums.IconSize?` | Small → `sf-octicon--sm`, Medium → `sf-octicon--md`, Large → `sf-octicon--lg`, ExtraLarge → `sf-octicon--xl`. Null → no size class (inherits ambient font-size). | Supported |
| `AriaLabel` | `string?` | When set, emits `role="img" aria-label="..."`; otherwise `role="presentation" aria-hidden="true"`. | Supported |
| `Class` / other attributes | — | Forwarded via `AdditionalAttributes` — merged with the compat-emitted class string. | Forwarded |

### Divergences

- **Parameter name divergence from some vendor wrappers.** BlazorOcticons wrappers
  sometimes use `Icon=` for the icon identifier. This package uses `Name=` for
  consistency across the Sunfish compat-icon family (`BootstrapIcon`, `LucideIcon`,
  `MaterialIcon`, etc. all use `Name=`). Consumers can bulk-rename with a single
  sed: `s/Icon="@Octicons\./Name="OcticonName./g`.
- **Single-wrapper shape vs per-icon components.** Some generators
  (`BlazorOcticonsGenerator`) emit one component per icon (`<MarkGithub />`,
  `<Repo />`, `<GitPullRequest />`, …). This package ships a single parameterized
  `Octicon` wrapper instead, matching Sunfish's one-wrapper-per-library convention.
  Consumers migrating off per-icon components adapt with a single find-and-replace
  per call site — e.g. `<MarkGithub />` → `<Octicon Name="OcticonName.MarkGithub" />`.
- **Rendering path matches `compat-lucide`, not `compat-font-awesome`.**
  `FontAwesomeIcon` delegates to `SunfishIcon` and lets the active
  `ISunfishIconProvider` resolve the glyph. This package instead emits the native
  `<i class="octicon octicon-*">` markup directly — even though Octicons is SVG-based
  and provider-delegation is technically available. The trade-off is: this package
  preserves the GitHub-branded Octicons visual exactly (when the consumer's Octicons
  SVG sprite / CSS pipeline is loaded), at the cost of not picking up the active
  Sunfish adapter's icon set. Choose this package when you want the Octicons visual;
  choose `SunfishIcon` when you want the active adapter's icon set.
- **Octicons CSS / assets must be loaded by the consumer.** The compat package does
  NOT emit a `<link>` or bundle any SVG/font assets. Add the upstream Octicons CSS
  or SVG sprite to your app's host page (typically `App.razor` / `index.html`). The
  canonical pattern is to install `@primer/octicons` from NPM and inline the sprite,
  or use a CDN `<link>`. Without it, the `<i>` element renders empty.
- **`Size` semantics use a Sunfish-prefixed class.** Octicons has no native `Size`
  parameter on the `<i>` wrapper (GitHub Primer sizes icons via inline `width`/
  `height` attributes on the `<svg>` element, typically 16px or 24px). This package
  accepts the Sunfish `IconSize` enum and maps to Sunfish-prefixed modifier classes
  (`sf-octicon--sm/md/lg/xl`). Consumers who need finer control can omit `Size`
  (inheriting ambient font-size) and apply their own `style="font-size:..."` via
  attribute splat, or ship CSS rules for the `sf-octicon--*` modifiers in their
  own stylesheet.
- **`Name` vs `NameString` precedence.** When both are set, `NameString` wins and a
  warning is logged. Intentional source-shape-parity trade-off — migrators may have
  a mix of typed and string-literal icon call sites; the raw string is preserved
  verbatim (debuggable) rather than silently dropping it in favor of the enum.

---

## OcticonName starter set

Phase 3D ships a **50-icon starter set** covering common UI needs plus the
GitHub-branded core that makes Octicons distinctive. Octicons' upstream catalog
contains ~270 icons.

| Category | Icons |
|---|---|
| GitHub-branded core | `MarkGithub`, `Repo`, `GitBranch`, `GitCommit`, `GitMerge`, `GitPullRequest`, `IssueOpened`, `IssueClosed` |
| Checkmarks / direction | `Check`, `X`, `ChevronUp`, `ChevronDown`, `ChevronLeft`, `ChevronRight`, `ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight` |
| Core UI / navigation | `Home`, `Gear`, `Person`, `People`, `Organization` |
| Security / access | `Key`, `Lock`, `Unlock`, `Eye`, `EyeClosed` |
| Social / bookmarking | `Heart`, `Star`, `StarFill`, `Bookmark`, `BookmarkFill` |
| Data / layout | `Search`, `Filter`, `Sort` |
| Transfer | `Download`, `Upload` |
| Editing actions | `Pencil`, `Trash`, `Plus`, `PlusCircle`, `Dash` |
| Communication / notification | `Comment`, `Mail`, `Bell` |
| Status / alerts | `Info`, `Alert`, `Stop`, `CheckCircle` |

Consumers whose icons are not in the starter set can:

1. Use the `NameString` parameter to pass the raw Octicons slug:
   `<Octicon NameString="rocket" />`.
2. Submit a policy-gated PR to extend the `OcticonName` enum and `ToSlug()` map.

### Divergences

- **Not the full catalog.** Shipping ~270 enum members would inflate the package
  and lock us into upstream Octicons naming evolutions (GitHub periodically
  renames/deprecates icons across major Primer versions). The starter set is
  intentionally scoped to the most common icons plus the GitHub-branded core;
  expanding it is cheap but deliberate.
- **Naming is hand-authored.** `ToSlug()` is a `switch` expression rather than a
  derived `ToString().ToLowerKebab()`, so each upstream slug is explicitly
  verified against the Primer Octicons catalog. Future additions should add an
  explicit arm.
- **Octicons version drift.** Octicons has evolved naming across Primer v10 → v14
  (e.g. `organization` / `people` / `person` relationships, `star-fill` vs
  historical `star` behaviour). The starter set reflects the stable names as of
  the Phase 3D ship date; if upstream renames a starter-set icon, we route the
  change through the policy gate (new enum member alongside the old, deprecation
  window).

---

## GitHub Primer context

GitHub publishes Octicons as part of the broader [Primer design system](https://primer.style/octicons).
The canonical rendering pipeline for Octicons is:

1. **SVG (primary)** — inline SVG via `@primer/octicons` NPM package. Each icon
   exposes a `toSVG(options)` method that returns the raw SVG markup. GitHub.com
   uses this path internally; it offers the best accessibility and CSS-theming
   story.
2. **CSS sprite** — a single SVG sprite file loaded via `<svg><use xlink:href>`
   references. Popular with static sites.
3. **CSS font** (discontinued) — the original `octicons.css` web font was retired
   with Primer v10 in favour of SVG. Some older Blazor wrappers still emit
   font-style `<i class="octicon octicon-*">` markup — this compat package
   matches that shape because it's the Blazor-ecosystem convention consumers are
   migrating from.

The `compat-octicons` wrapper emits `<i class="octicon octicon-@slug sf-octicon">`
markup regardless of the upstream rendering path the consumer has configured. The
consumer's CSS / sprite / font-face pipeline is what determines the actual visual
output.

---

## Consumer CSS-loading notes

`compat-octicons` does NOT emit any `<link>` or `<script>` tag. To make the `<i>`
wrappers render visible glyphs, consumers need one of the following:

**Option A — NPM + inline sprite** (recommended, matches GitHub.com's approach):

```bash
npm install @primer/octicons
```

Import the sprite into your app's host page (`App.razor` / `index.html`) and use
CSS rules to target the `octicon` / `octicon-*` classes.

**Option B — CDN `<link>`** (fastest setup, requires a font-face or sprite CSS):

```html
<!-- Add to index.html or App.razor -->
<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@primer/octicons@19/build/build.css">
```

Note: Primer's build does not ship a precompiled CSS font at every CDN — check
the version-specific build before committing to this path. Self-hosted from the
`@primer/octicons` NPM package is the most reliable option.

**Option C — self-hosted sprite** — download `primer/octicons/build/sprite.svg`
from the GitHub release and serve it alongside your app, then reference via
`<svg><use xlink:href="/octicons-sprite.svg#mark-github" />`.

Without one of these options loaded, the `<i>` element will render empty (no
visual glyph) but the compat wrapper will still emit the correct classes and
accessibility attributes — this makes diagnostics straightforward (you'll see
empty `<i>` tags in dev tools rather than silent rendering failures).

---

## Future coverage

Deferred to follow-up PRs (track as separate ICM intake items if prioritized):

- **Enum starter-set expansion** — per-request additions under the policy gate.
- **Inline SVG rendering mode** — a future mode where the compat package inlines
  Octicons SVGs directly rather than relying on the consumer's sprite / CSS.
  Would eliminate the asset-loading step but violates the no-vendor-NuGet
  invariant unless we vendor the SVGs into the Sunfish repo — a separate policy
  decision (shared with `compat-lucide` / `compat-heroicons` future-coverage
  sections).
- **Provider-delegation mode** — optional "render via `SunfishIcon` instead of
  direct emit" parameter, for consumers who want the active adapter's icon set.
  Would be additive; the default stays direct-emit to preserve Phase 3D behavior.
- **Per-icon component aliases** — optional `<MarkGithub />` / `<Repo />` /
  `<GitPullRequest />` components that alias `<Octicon Name="..." />` for
  consumers migrating off BlazorOcticonsGenerator. Currently out of scope
  (source-shape parity via find-and-replace is considered sufficient).

---

## Cross-references

- Sibling icon-compat package (Phase 3A; closest pattern): `docs/compat-lucide-mapping.md`
- Sibling icon-compat package (Phase 2): `docs/compat-bootstrap-icons-mapping.md`
- Sibling icon-compat package (Phase 1): `docs/compat-font-awesome-mapping.md`
- Package policy: `packages/compat-octicons/POLICY.md`
- Shared policy template: `packages/compat-shared/POLICY-TEMPLATE.md`
- Stage 01 Discovery survey: `icm/01_discovery/output/compat-icon-library-survey-2026-04-22.md`

---

## License & attribution

- **Octicons license:** MIT (GitHub's Primer Octicons library).
- **MIT is permissive** — no attribution clause beyond the standard copyright
  notice preservation.
- **This package ships no Octicons assets** — consumers retain responsibility for
  their own Octicons asset pipeline (NPM `@primer/octicons`, CDN, or self-hosted
  sprite). Any attribution their chosen distribution channel requires remains
  their responsibility.
- **GitHub trademarks:** the `MarkGithub` icon (the GitHub octocat silhouette)
  and other GitHub-branded glyphs remain trademarks of GitHub, Inc. The MIT
  license on the Octicons source code does not grant trademark rights. Consumers
  using these icons in UIs that imply affiliation with GitHub should consult
  [GitHub's logo and brand guidelines](https://github.com/logos).
