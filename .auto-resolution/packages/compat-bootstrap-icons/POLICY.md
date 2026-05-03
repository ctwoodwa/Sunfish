# `compat-bootstrap-icons` Package Policy

## Purpose

`Sunfish.Compat.BootstrapIcons` is a **migration off-ramp** for consumers moving from
Bootstrap Icons Blazor wrappers (e.g. `BlazorBootstrap` — which exposes an
`<Icon Name="IconName.Star" />` shape — `Blazicons.Bootstrap`, `Blazorise.Icons.Bootstrap`).
It exposes a Bootstrap-Icons-API-shaped Razor component (`BootstrapIcon`) that renders the
same `<i class="bi bi-*">` markup consumers already style with the upstream
`bootstrap-icons.css`. It does NOT provide visual or behavioral parity with any specific
wrapper; it provides **source-code shape parity** so consumers can flip their Bootstrap
Icons `using` directives to `using Sunfish.Compat.BootstrapIcons` and keep most markup
intact.

`compat-bootstrap-icons` is **not** the source of truth for any Sunfish component. ui-core
and the adapter packages own the canonical contracts. This package is a thin, disposable
shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Bootstrap-Icons-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `IconName.cs` / `IconNameExtensions.cs`
- Any change to `docs/compat-bootstrap-icons-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Bootstrap Icons parity value vs. maintenance cost.
3. Update `docs/compat-bootstrap-icons-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants (preserve across every vendor)

1. **No Bootstrap Icons NuGet dependency.** This package MUST NOT `<PackageReference>`
   any `BootstrapIcons.*` / `Blazicons.Bootstrap` / `Blazorise.Icons.Bootstrap` /
   `BlazorBootstrap` package. Consumers must not be forced to carry a vendor package
   dependency. Note: BlazorBootstrap ships Bootstrap Icons as a transitive dependency;
   this compat package does NOT — consumers continue loading the vendor's CSS/font
   themselves.
2. **All wrappers live in the root namespace** `Sunfish.Compat.BootstrapIcons` (not
   nested). This mirrors BlazorBootstrap's flat-namespace shape.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` —
   never silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from
   the Bootstrap Icons / BlazorBootstrap original must have an explicit section in
   `docs/compat-bootstrap-icons-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork
   `CompatChildComponent`, `UnsupportedParam`, or `CompatIconAdapter` into this package —
   reference the shared package.

## Bootstrap-Icons-specific clarifications

### Asset shipping is the consumer's responsibility

Bootstrap Icons are licensed under **MIT** (both SVGs and code). Hard Invariant #1
(no vendor NuGet dependency) already precludes this package from redistributing any
Bootstrap Icons assets. This means:

- **Consumers remain responsible** for shipping `bootstrap-icons.css` via their
  existing setup (CDN `<link>` in `index.html` / `App.razor`, NPM install, or
  self-hosted). `compat-bootstrap-icons` does NOT emit any `<link>` or `<script>`
  tag.
- The wrapper renders `<i class="bi bi-@slug sf-bi-icon">` markup. The `bi bi-*`
  classes are what upstream `bootstrap-icons.css` styles; the `sf-bi-icon` class
  is preserved alongside per the Phase-1 split-class convention so Sunfish-aware
  styles can target the wrapper independently.
- If a consumer has not loaded `bootstrap-icons.css`, the `<i>` element will
  render empty (Bootstrap Icons are pseudo-element glyphs keyed to the `bi-*`
  class). The compat wrapper does not try to substitute via a Sunfish
  `IconProvider` — preserving the Bootstrap Icons visual is the explicit goal
  when a consumer picks this package over `SunfishIcon`.

### Component-name divergence from BlazorBootstrap

BlazorBootstrap uses the unqualified name `Icon` for its Bootstrap-Icons wrapper. This
package ships the type as `BootstrapIcon` for disambiguation against parallel compat
packages (`MaterialIcon`, `FluentIcon`, `FontAwesomeIcon`) that will share the same
root-namespace family under `Sunfish.Compat.*`. Consumers who want to preserve a
BlazorBootstrap-shaped `using BlazorBootstrap;` call site can alias:

```csharp
using Icon = Sunfish.Compat.BootstrapIcons.BootstrapIcon;
```

The divergence is documented in `docs/compat-bootstrap-icons-mapping.md`.

## Coverage Expansion

Phase 1 ships `BootstrapIcon` plus a 50-icon starter-set `IconName` enum and its
`ToSlug()` extension. Additional icons and wrapper surfaces are added one-per-PR under
this policy gate. Bootstrap Icons' upstream catalog exceeds 2,000 icons — the starter
set covers the icons most migrators reach for first. Consumers outside the starter
set can pass a raw string via `NameString`.

## See Also

- `docs/compat-bootstrap-icons-mapping.md` — authoritative divergence log
- `packages/compat-shared/POLICY-TEMPLATE.md` — shared-invariant source
- `packages/compat-font-awesome/POLICY.md` — sibling icon-compat package (Phase 1 reference)
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
