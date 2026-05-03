# `compat-octicons` Package Policy

## Purpose

`Sunfish.Compat.Octicons` is a **migration off-ramp** for consumers moving from Octicons
Blazor wrappers (e.g. `BlazorOcticons`, `BlazorOcticonsGenerator`, `NgIcons.Octicons`).
It exposes an Octicons-API-shaped Razor component (`Octicon`) that renders the same
`<i class="octicon octicon-*">` markup consumers already style with their upstream
Octicons SVG sprite / CSS setup. It does NOT provide visual or behavioral parity with
any specific wrapper; it provides **source-code shape parity** so consumers can flip
their Octicons `using` directives to `using Sunfish.Compat.Octicons` and keep most
markup intact.

`compat-octicons` is **not** the source of truth for any Sunfish component. ui-core and
the adapter packages own the canonical contracts. This package is a thin, disposable
shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Octicons-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `OcticonName.cs` / `OcticonNameExtensions.cs`
- Any change to `docs/compat-octicons-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Octicons parity value vs. maintenance cost.
3. Update `docs/compat-octicons-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants (preserve across every vendor)

1. **No Octicons NuGet dependency.** This package MUST NOT `<PackageReference>` any
   `Octicons.*` / `BlazorOcticons` / `BlazorOcticonsGenerator` / `NgIcons.Octicons` /
   `@primer/octicons` package. Consumers must not be forced to carry a vendor package
   dependency.
2. **All wrappers live in the root namespace** `Sunfish.Compat.Octicons` (not nested).
   This mirrors the flat-namespace shape of the dominant Octicons Blazor wrappers.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` —
   never silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from
   the Octicons / vendor-wrapper original must have an explicit section in
   `docs/compat-octicons-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork
   `CompatChildComponent`, `UnsupportedParam`, or `CompatIconAdapter` into this package —
   reference the shared package.

## Octicons-specific clarifications

### Asset shipping is the consumer's responsibility

Octicons (the [Primer Octicons](https://github.com/primer/octicons) library) is licensed
under **MIT**. Hard Invariant #1 (no vendor NuGet dependency) already precludes this
package from redistributing any Octicons assets. This means:

- **Consumers remain responsible** for shipping Octicons CSS / SVG sprite via their
  existing setup (`@primer/octicons` on NPM, a CDN `<link>` in `index.html` /
  `App.razor`, or self-hosted). `compat-octicons` does NOT emit any `<link>` or
  `<script>` tag.
- The wrapper renders `<i class="octicon octicon-@slug sf-octicon">` markup. The
  `octicon octicon-*` classes are what upstream Octicons CSS styles; the `sf-octicon`
  class is preserved alongside per the Phase-3 split-class convention so Sunfish-aware
  styles can target the wrapper independently.
- If a consumer has not loaded any Octicons CSS/assets, the `<i>` element will render
  empty. The compat wrapper does not try to substitute via a Sunfish `IconProvider` —
  preserving the Octicons visual is the explicit goal when a consumer picks this
  package over `SunfishIcon`.

### Why not delegate to `SunfishIcon` for this SVG-based library?

Octicons ships as individual SVG files — so in principle a compat shim *could*
delegate to `SunfishIcon` and let the active `ISunfishIconProvider` resolve glyphs.
We instead mirror the `compat-lucide` / `compat-bootstrap-icons` direct-emit pattern
because:

1. Consumers migrating off an Octicons Blazor wrapper already have an Octicons SVG
   sprite / CSS pipeline set up. The compat shim preserves that investment rather
   than forcing it to be torn down.
2. Direct emission keeps the Octicons visual exact; provider-delegation could
   visually substitute based on the active Sunfish adapter (which ships FluentUI /
   Bootstrap / Material icons — not the GitHub-branded Octicons set that makes this
   library distinctive).
3. Consistency with `compat-lucide` / `compat-bootstrap-icons` reduces cognitive
   load for migrators and for Sunfish maintainers reviewing parallel compat packages.

Consumers who want Sunfish-native rendering should use `SunfishIcon` directly; they
choose `compat-octicons` specifically when they want their GitHub-branded Octicons
markup to keep working.

### Component-name divergence from some wrappers

Some wrappers (e.g., `BlazorOcticonsGenerator`) generate per-icon components
(`<MarkGithub />`, `<Repo />`, etc.) rather than a single parameterized wrapper.
This package ships a single `Octicon` type with a `Name` parameter —
matching the `BlazorOcticons` shape (`<Octicon Icon="..." />` / `<Octicon Name="..." />`)
and aligning with the Sunfish compat-icon family convention (`LucideIcon`,
`HeroiconsIcon`, etc. all use a single wrapper with `Name=`). Consumers migrating
off a per-icon-component wrapper adapt with a single find-and-replace per icon. The
divergence is documented in `docs/compat-octicons-mapping.md`.

## Coverage Expansion

Phase 3D ships `Octicon` plus a 50-icon starter-set `OcticonName` enum and its
`ToSlug()` extension. Additional icons and wrapper surfaces are added one-per-PR
under this policy gate. Octicons' upstream catalog contains ~270 icons — the starter
set covers the GitHub-branded core (MarkGithub, Repo, Git*, Issue*, PullRequest) plus
the common UI glyphs migrators reach for first. Consumers outside the starter set can
pass a raw string via `NameString`.

## See Also

- `docs/compat-octicons-mapping.md` — authoritative divergence log
- `packages/compat-shared/POLICY-TEMPLATE.md` — shared-invariant source
- `packages/compat-lucide/POLICY.md` — sibling icon-compat package (Phase 3A; closest pattern)
- `packages/compat-bootstrap-icons/POLICY.md` — sibling icon-compat package (Phase 2 reference)
- `packages/compat-font-awesome/POLICY.md` — sibling icon-compat package (Phase 1 reference)
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
