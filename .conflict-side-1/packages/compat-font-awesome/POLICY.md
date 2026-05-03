# `compat-font-awesome` Package Policy

## Purpose

`Sunfish.Compat.FontAwesome` is a **migration off-ramp** for consumers moving from Font
Awesome Blazor wrappers (e.g. historical `Blazored.FontAwesome`, `Blazicons.FontAwesome`,
`Blazorise.Icons.FontAwesome`). It exposes Font-Awesome-API-shaped Razor components
(`FontAwesomeIcon`, `FaList`, `FaLayers`, etc.) that delegate to canonical Sunfish
components under the hood. It does NOT provide visual or behavioral parity with Font
Awesome; it provides **source-code shape parity** so consumers can flip their FA `using`
directives to `using Sunfish.Compat.FontAwesome` and keep most markup intact.

`compat-font-awesome` is **not** the source of truth for any Sunfish component. ui-core
and the adapter packages own the canonical contracts. This package is a thin, disposable
shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Font-Awesome-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `FasIcons.cs` / `FarIcons.cs` / `FabIcons.cs`
- Any change to `docs/compat-font-awesome-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Font Awesome parity value vs. maintenance cost.
3. Update `docs/compat-font-awesome-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants (preserve across every vendor)

1. **No Font Awesome NuGet dependency.** This package MUST NOT `<PackageReference>` any
   `FontAwesome.*` / `Blazored.FontAwesome` / `Blazicons.*` / `Blazorise.Icons.FontAwesome`
   package. Consumers must not be forced to carry a Font Awesome package dependency.
2. **All wrappers live in the root namespace** `Sunfish.Compat.FontAwesome` (not nested).
   This mirrors the historical flat FA-Blazor-wrapper namespace shape.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` —
   never silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from
   the Font Awesome original must have an explicit section in
   `docs/compat-font-awesome-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork
   `CompatChildComponent`, `UnsupportedParam`, or `CompatIconAdapter` into this package —
   reference the shared package.

## Font-Awesome-specific clarifications

### Asset shipping is the consumer's responsibility

Font Awesome Free icons are licensed under **CC-BY-4.0** (SVGs/JS), **SIL-OFL-1.1**
(webfonts), and **MIT** (code). Hard Invariant #1 (no vendor NuGet dependency) already
precludes this package from redistributing any FA assets, which also sidesteps the
attribution requirement at the Sunfish-package level. However, this means:

- **Consumers remain responsible** for shipping Font Awesome assets via their existing
  setup (CDN `<link>` in `index.html`, NPM install, self-hosted font files, or
  Kit-script embed). `compat-font-awesome` does NOT emit any `<link>` or `<script>` tag.
- Icons are actually rendered by whichever **Sunfish `ISunfishIconProvider`** is active
  in the consumer app (FluentUI / Bootstrap / Material). The identifier passed through
  the compat layer (e.g. `"star"`) is resolved by that provider — the rendered glyph may
  visually differ from Font Awesome's own glyph. This is the documented cost of
  source-shape parity without behavioral parity.
- If a consumer wants pixel-for-pixel FA glyphs, they continue to reference FA's
  CDN/NPM in their app layout as before; the compat wrappers remain compatible with
  that setup since they emit markup the active Sunfish provider shapes.

### Pro-only features are out of scope

The following Font Awesome Pro-tier features are **not** mirrored in this package — they
are documented divergences:

- **Duotone** icons and the `FaDuotoneIcon` component
- **Sharp** family (`FaSharpIcon`, `fa-sharp` classes)
- **Chisel**, **Thin**, and other Pro-only families
- Pro-only icon identifiers (typically documented on FA's Pro-tier icon search)

Consumers relying on these features should evaluate whether the Sunfish icon-provider
ecosystem meets their needs, or maintain a parallel FA-Pro code path outside the compat
package.

## Coverage Expansion

Phase 1 ships `FontAwesomeIcon`, `FaList` / `FaListItem`, `FaLayers` / `FaLayersText` /
`FaLayersCounter`, plus starter-set typed identifier classes (`FasIcons`, `FarIcons`,
`FabIcons`). Additional icons and wrapper surfaces are added one-per-PR under this
policy gate. Candidates for future coverage:

- Deeper `Transform` support (rotate / flip / grow / shrink)
- `Spin` / `Pulse` animations (currently LogAndFallback)
- Expanding the typed-icon starter set beyond the first 50 per style family

## See Also

- `docs/compat-font-awesome-mapping.md` — authoritative divergence log
- `packages/compat-shared/POLICY-TEMPLATE.md` — shared-invariant source
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
