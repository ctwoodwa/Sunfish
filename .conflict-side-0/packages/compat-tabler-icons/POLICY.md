# `compat-tabler-icons` Package Policy

> **Disambiguation.** This is a **migration-compat package** that provides a Tabler-Icons-API-shaped
> surface for consumers already using a Tabler Icons Blazor wrapper (e.g. `Vizor.Icons.Tabler`,
> `Kebechet.Blazor.Tabler.Icons`, `NgIcons.TablerIcons`). It lets them flip their `using`
> directives to `using Sunfish.Compat.TablerIcons` and keep most markup working.
>
> For Sunfish's **native Tabler-backed icon provider** — the package Sunfish ships for consumers
> who want Tabler as their default icon set — see `Sunfish.Icons.Tabler` in
> `packages/ui-adapters-blazor/Icons/Tabler/`. That is a completely separate package with a
> different purpose. Do not confuse the two.

## Purpose

`Sunfish.Compat.TablerIcons` is a **migration off-ramp** for consumers moving from Tabler Icons
Blazor wrappers. It exposes a Tabler-Icons-API-shaped Razor component (`TablerIcon`) that renders
`<i class="tabler tabler-*">` markup consumers already style with their upstream Tabler Icons
font-face / CSS setup. It does NOT provide visual or behavioral parity with any specific wrapper;
it provides **source-code shape parity** so consumers can flip their Tabler `using` directives
to `using Sunfish.Compat.TablerIcons` and keep most markup intact.

`compat-tabler-icons` is **not** the source of truth for any Sunfish component. ui-core and the
adapter packages own the canonical contracts. This package is a thin, disposable shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Tabler-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `TablerIconName.cs` / `TablerIconNameExtensions.cs`
- Any change to `docs/compat-tabler-icons-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Tabler-Icons parity value vs. maintenance cost.
3. Update `docs/compat-tabler-icons-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants (preserve across every vendor)

1. **No Tabler Icons NuGet dependency.** This package MUST NOT `<PackageReference>` any
   `Tabler.*` / `Vizor.Icons.Tabler` / `Kebechet.Blazor.Tabler.Icons` / `NgIcons.TablerIcons`
   package. Consumers must not be forced to carry a vendor package dependency.
2. **All wrappers live in the root namespace** `Sunfish.Compat.TablerIcons` (not nested).
   This mirrors the flat-namespace shape of the dominant Tabler-Icons Blazor wrappers.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` —
   never silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from
   the Tabler / vendor-wrapper original must have an explicit section in
   `docs/compat-tabler-icons-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork
   `CompatChildComponent`, `UnsupportedParam`, or `CompatIconAdapter` into this package —
   reference the shared package.

## Tabler-specific clarifications

### Asset shipping is the consumer's responsibility

Tabler Icons are licensed under **MIT**. Hard Invariant #1 (no vendor NuGet dependency) already
precludes this package from redistributing any Tabler Icons assets. This means:

- **Consumers remain responsible** for shipping Tabler Icons CSS / font-face / SVG sprites
  via their existing setup (CDN `<link>` in `index.html` / `App.razor`, NPM install, or
  self-hosted). `compat-tabler-icons` does NOT emit any `<link>` or `<script>` tag.
- The wrapper renders `<i class="tabler tabler-@slug sf-tabler-icon">` markup. The
  `tabler tabler-*` classes are what upstream Tabler CSS styles; the `sf-tabler-icon`
  class is preserved alongside per the Phase-1+2 split-class convention so Sunfish-aware
  styles can target the wrapper independently.
- If a consumer has not loaded any Tabler CSS/assets, the `<i>` element will render
  empty. The compat wrapper does not try to substitute via a Sunfish `IconProvider` —
  preserving the Tabler visual is the explicit goal when a consumer picks this package
  over `SunfishIcon` or the native `Sunfish.Icons.Tabler` provider.

### Distinguishing parameter — `Stroke`

Unlike most icon libraries, Tabler Icons exposes a **stroke-width** control as a
first-class parameter because the default outlined glyphs are drawn from configurable
stroke paths. The wrapper surfaces this as the `Stroke` parameter (nullable `double`,
Tabler's default is `2.0`). When set, the compat wrapper emits `style="stroke-width: <value>"`
on the element. This is the distinguishing parameter vs. `compat-lucide` (which shares the
same SVG-icon lineage but does not ship a `Stroke` parameter in its Phase 3A surface).

### Why not delegate to `SunfishIcon` or `Sunfish.Icons.Tabler`?

Tabler Icons ships as individual SVG files (outline + filled variants). In principle this
compat shim *could* delegate to `SunfishIcon` or to the native `Sunfish.Icons.Tabler`
provider. We instead mirror the `compat-bootstrap-icons` / `compat-lucide` direct-emit
pattern because:

1. Consumers migrating off a Tabler Blazor wrapper already have a Tabler CSS / font-face /
   SVG-sprite pipeline set up. The compat shim preserves that investment rather than forcing
   it to be torn down.
2. Direct emission keeps the Tabler visual exact; provider-delegation could visually
   substitute based on the active Sunfish adapter (which may not ship Tabler glyphs).
3. Consistency with `compat-bootstrap-icons` / `compat-lucide` reduces cognitive load for
   migrators and for Sunfish maintainers reviewing parallel compat packages.

Consumers who want Sunfish-native rendering should use `SunfishIcon` directly; consumers
who want Tabler as their *active* Sunfish icon provider should register
`Sunfish.Icons.Tabler`. They choose `compat-tabler-icons` specifically when they want
their Tabler-wrapper markup to keep working during migration.

### Component-name divergence from some wrappers

Some wrappers (e.g. Blazicons) use the generic name `Blazicon` with a `Svg="TablerIcon.Home"`
shape. This package ships the type as `TablerIcon` — matching the more-direct
`Vizor.Icons.Tabler` / `Kebechet.Blazor.Tabler.Icons` shape
(`<TablerIcon Icon="@TablerIconName.Home" />`). Consumers migrating off `Blazicons` can
adapt by updating from `<Blazicon Svg="TablerIcon.Home" />` to
`<TablerIcon Name="TablerIconName.Home" />` in a single find-and-replace. The divergence is
documented in `docs/compat-tabler-icons-mapping.md`.

## Coverage Expansion

Phase 3E ships `TablerIcon` plus a 50-icon starter-set `TablerIconName` enum and its
`ToSlug()` extension. Additional icons and wrapper surfaces are added one-per-PR under
this policy gate. Tabler's upstream catalog exceeds 5,000 icons — the starter set covers
the icons most migrators reach for first. Consumers outside the starter set can pass a
raw string via `NameString`.

## See Also

- `docs/compat-tabler-icons-mapping.md` — authoritative divergence log
- `packages/compat-shared/POLICY-TEMPLATE.md` — shared-invariant source
- `packages/compat-lucide/POLICY.md` — sibling icon-compat package (Phase 3 reference; closest pattern)
- `packages/compat-bootstrap-icons/POLICY.md` — sibling icon-compat package (Phase 2 reference)
- `packages/ui-adapters-blazor/Icons/Tabler/` — native `Sunfish.Icons.Tabler` provider (NOT this package)
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
