# `compat-heroicons` Package Policy

## Purpose

`Sunfish.Compat.Heroicons` is a **migration off-ramp** for consumers moving from
Heroicons Blazor wrappers (`Blazor.Heroicons`, `TailBlazor.HeroIcons`,
`HeroIcons.Blazor`). It exposes a Heroicons-API-shaped Razor component
(`Heroicon`) with a `Variant` discriminator covering all three Heroicons forms
(Outline / Solid / Mini) that Tailwind Labs ships upstream. It does NOT provide
visual or behavioral parity with any specific wrapper; it provides **source-code
shape parity** so consumers can flip their Heroicons `using` directives to
`using Sunfish.Compat.Heroicons` and keep most markup intact.

`compat-heroicons` is **not** the source of truth for any Sunfish component.
ui-core and the adapter packages own the canonical contracts. This package is a
thin, disposable shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Heroicons-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `HeroiconName.cs` / `HeroiconNameExtensions.cs`
- Adding variants to `HeroiconVariant`
- Any change to `docs/compat-heroicons-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Heroicons parity value vs. maintenance cost.
3. Update `docs/compat-heroicons-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants (preserve across every vendor)

1. **No Heroicons NuGet dependency.** This package MUST NOT `<PackageReference>` any
   `Blazor.Heroicons` / `TailBlazor.HeroIcons` / `HeroIcons.Blazor` package. Consumers
   must not be forced to carry a downstream Heroicons wrapper dependency.
2. **All wrappers live in the root namespace** `Sunfish.Compat.Heroicons` (not
   nested). This mirrors Blazor.Heroicons' flat namespace shape.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` —
   never silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from
   the Heroicons original must have an explicit section in
   `docs/compat-heroicons-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork
   `CompatChildComponent`, `UnsupportedParam`, or `CompatIconAdapter` into this package —
   reference the shared package.

## Heroicons-specific clarifications

### License — MIT

Heroicons are licensed under the **MIT License** by Tailwind Labs. MIT does not
require runtime attribution inside the consumer app, but consumers who ship
Heroicons SVGs as part of their distribution SHOULD preserve the MIT copyright
notice per the license's terms. This is a consumer-side obligation —
`compat-heroicons` neither ships nor redistributes any Heroicons assets, so no
attribution is carried at the Sunfish-package level.

### Asset shipping is the consumer's responsibility

Hard Invariant #1 (no vendor NuGet dependency) combined with the compat pattern
(no asset shipping) means this package ships **no SVG files and no CSS**.
Specifically:

- **Consumers remain responsible** for loading Heroicons SVGs in their app.
  Typical options:
  - NPM install `@heroicons/react` / `@heroicons/vue` and use the existing bundler
    pipeline (for Blazor apps that already front-end a JS build).
  - Self-host the SVG files from the Heroicons GitHub release.
  - CDN-reference the SVGs from a public mirror.
  - An existing Blazor.Heroicons / TailBlazor.HeroIcons layout continues to work
    unchanged (the wrappers bundle their own RCL static assets — compat-heroicons
    does not, per Hard Invariant #1).
- `compat-heroicons` does NOT emit any `<link>` or `<script>` tag, nor does it
  inline SVG contents.
- The wrapper renders `<i class="heroicon heroicon-<slug> heroicon-<variant>
  sf-heroicon">` markup. The `heroicon-*` classes are the hook a consumer-side
  CSS or JS pipeline uses to substitute the corresponding SVG; the `sf-heroicon`
  class is preserved alongside per the shared-CSS-hook convention so
  Sunfish-aware styles can target the wrapper independently.

### Heroicons' three-variant shape

Tailwind Labs ships Heroicons in three coexisting variants from the same upstream
library:

- **Outline** (24×24, stroke-based) — default. Emits `heroicon-outline`.
- **Solid** (24×24, filled). Emits `heroicon-solid`.
- **Mini** (20×20, filled, tuned for small UIs). Emits `heroicon-mini`.

All three are shipped in one compat package (mirroring Phase 2's
`compat-material-icons` precedent where Material Icons + Material Symbols were
merged under a `Variant` discriminator — same upstream library, multiple forms).
The `HeroiconVariant` enum selects between them. Defaulting to `Outline` matches
Heroicons' own documented default.

### What is NOT mirrored

- **Provider-delegation to `SunfishIcon`.** Unlike `FontAwesomeIcon` (Phase 1),
  this package does not delegate to `SunfishIcon` — Heroicons ship as
  hand-authored SVGs with variant-specific geometry, so a Sunfish
  `IconProvider`'s generic glyph substitution would not visually preserve the
  Heroicons look. This matches the `compat-bootstrap-icons` approach of emitting
  native vendor markup directly.
- **Inline SVG rendering.** Blazor.Heroicons inlines `<svg>...</svg>` markup via
  RCL static assets. `compat-heroicons` emits only the `<i class="heroicon-*">`
  shell; the consumer-side pipeline is responsible for SVG substitution (see
  Asset shipping above).
- **Per-icon color / stroke-width parameters.** Not mirrored in this phase —
  consumers route these via `AdditionalAttributes` / `style` / Tailwind utility
  classes.

## Coverage Expansion

Phase 3 ships `Heroicon`, the `HeroiconVariant` enum (Outline/Solid/Mini), a
50-entry `HeroiconName` starter-set enum, and its `ToSlug()` extension.
Additional icons and wrapper surfaces are added one-per-PR under this policy
gate. The full Heroicons catalog exceeds 300 icons across all three variants —
consumers outside the starter set can pass a raw slug via `NameString`.

Candidates for future coverage:

- Typed identifier enum for the full Heroicons catalog (~300 icons) — likely
  source-generated rather than hand-maintained
- A `StrokeWidth` parameter on Outline-variant renders (Heroicons 2.0 exposes
  per-icon stroke-width tokens)
- Inline-SVG rendering as an optional mode (requires a ui-core contract
  extension to carry SVG bodies without forcing an NPM-style asset pipeline)
- A SunfishIcon-delegating rendering mode for consumers willing to accept
  cross-library glyph substitution in exchange for provider-consistent visuals

## See Also

- `docs/compat-heroicons-mapping.md` — authoritative divergence log
- `packages/compat-shared/POLICY-TEMPLATE.md` — shared-invariant source
- `packages/compat-material-icons/POLICY.md` — closest Phase 2 precedent (same
  Variant-discriminator pattern)
- `packages/compat-bootstrap-icons/POLICY.md` — precedent for typed enum + ToSlug
- `packages/compat-font-awesome/POLICY.md` — Phase 1 reference policy
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
