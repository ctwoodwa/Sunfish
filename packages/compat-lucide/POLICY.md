# `compat-lucide` Package Policy

## Purpose

`Sunfish.Compat.Lucide` is a **migration off-ramp** for consumers moving from Lucide Blazor
wrappers (e.g. `Blazicons.Lucide`, `InfiniLore.Lucide`, `BlazorBlueprint.Icons.Lucide`).
It exposes a Lucide-API-shaped Razor component (`LucideIcon`) that renders the same
`<i class="lucide lucide-*">` markup consumers already style with their upstream Lucide
font-face / CSS setup. It does NOT provide visual or behavioral parity with any specific
wrapper; it provides **source-code shape parity** so consumers can flip their Lucide
`using` directives to `using Sunfish.Compat.Lucide` and keep most markup intact.

`compat-lucide` is **not** the source of truth for any Sunfish component. ui-core and
the adapter packages own the canonical contracts. This package is a thin, disposable
shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Lucide-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `LucideIconName.cs` / `LucideIconNameExtensions.cs`
- Any change to `docs/compat-lucide-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Lucide parity value vs. maintenance cost.
3. Update `docs/compat-lucide-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants (preserve across every vendor)

1. **No Lucide NuGet dependency.** This package MUST NOT `<PackageReference>` any
   `Lucide.*` / `Blazicons.Lucide` / `InfiniLore.Lucide` / `BlazorBlueprint.Icons.Lucide`
   package. Consumers must not be forced to carry a vendor package dependency.
2. **All wrappers live in the root namespace** `Sunfish.Compat.Lucide` (not nested).
   This mirrors the flat-namespace shape of the dominant Lucide Blazor wrappers.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` —
   never silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from
   the Lucide / vendor-wrapper original must have an explicit section in
   `docs/compat-lucide-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork
   `CompatChildComponent`, `UnsupportedParam`, or `CompatIconAdapter` into this package —
   reference the shared package.

## Lucide-specific clarifications

### Asset shipping is the consumer's responsibility

Lucide icons are licensed under **ISC** (a Feather fork; LICENSE file is the standard
ISC text). Hard Invariant #1 (no vendor NuGet dependency) already precludes this
package from redistributing any Lucide assets. This means:

- **Consumers remain responsible** for shipping Lucide CSS / font-face / SVG sprites
  via their existing setup (CDN `<link>` in `index.html` / `App.razor`, NPM install,
  or self-hosted). `compat-lucide` does NOT emit any `<link>` or `<script>` tag.
- The wrapper renders `<i class="lucide lucide-@slug sf-lucide-icon">` markup. The
  `lucide lucide-*` classes are what upstream Lucide CSS styles; the `sf-lucide-icon`
  class is preserved alongside per the Phase-1+2 split-class convention so
  Sunfish-aware styles can target the wrapper independently.
- If a consumer has not loaded any Lucide CSS/assets, the `<i>` element will render
  empty. The compat wrapper does not try to substitute via a Sunfish `IconProvider` —
  preserving the Lucide visual is the explicit goal when a consumer picks this
  package over `SunfishIcon`.

### Why not delegate to `SunfishIcon` for this SVG-based library?

Lucide ships as individual SVG files — so in principle a compat shim *could* delegate
to `SunfishIcon` and let the active `ISunfishIconProvider` resolve glyphs. We instead
mirror the `compat-bootstrap-icons` direct-emit pattern because:

1. Consumers migrating off a Lucide Blazor wrapper already have a Lucide CSS /
   font-face / SVG-sprite pipeline set up. The compat shim preserves that investment
   rather than forcing it to be torn down.
2. Direct emission keeps the Lucide visual exact; provider-delegation could
   visually substitute based on the active Sunfish adapter (which may not ship Lucide
   glyphs).
3. Consistency with `compat-bootstrap-icons` (Phase 2) reduces cognitive load for
   migrators and for Sunfish maintainers reviewing parallel compat packages.

Consumers who want Sunfish-native rendering should use `SunfishIcon` directly; they
choose `compat-lucide` specifically when they want their Lucide markup to keep working.

### Component-name divergence from some wrappers

Some wrappers (e.g., `Blazicons.Lucide`) use the generic name `Blazicon` with a
`Svg="LucideIcon.Star"` shape. This package ships the type as `LucideIcon` —
matching the more-direct `InfiniLore.Lucide` / `BlazorBlueprint.Icons.Lucide` shape
(`<LucideIcon Icon="@LucideIcons.Star" />`). Consumers migrating off `Blazicons`
can adapt by updating from `<Blazicon Svg="LucideIcon.Star" />` to
`<LucideIcon Name="LucideIconName.Star" />` in a single find-and-replace. The
divergence is documented in `docs/compat-lucide-mapping.md`.

## Coverage Expansion

Phase 3A ships `LucideIcon` plus a 50-icon starter-set `LucideIconName` enum and its
`ToSlug()` extension. Additional icons and wrapper surfaces are added one-per-PR
under this policy gate. Lucide's upstream catalog exceeds 1,400 icons — the starter
set covers the icons most migrators reach for first. Consumers outside the starter
set can pass a raw string via `NameString`.

## See Also

- `docs/compat-lucide-mapping.md` — authoritative divergence log
- `packages/compat-shared/POLICY-TEMPLATE.md` — shared-invariant source
- `packages/compat-bootstrap-icons/POLICY.md` — sibling icon-compat package (Phase 2 reference; closest pattern)
- `packages/compat-font-awesome/POLICY.md` — sibling icon-compat package (Phase 1 reference)
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
