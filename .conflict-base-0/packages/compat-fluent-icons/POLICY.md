# `compat-fluent-icons` Package Policy

## Purpose

`Sunfish.Compat.FluentIcons` is a **migration off-ramp** for consumers moving from
Microsoft Fluent UI System Icons (`Microsoft.FluentUI.AspNetCore.Components.Icons`) and
its peer Blazor wrappers (`Microsoft.Fast.Components.FluentUI.Icons`, `Blazicons.FluentUI`).
It exposes Fluent-UI-API-shaped Razor components (`FluentIcon`) plus a partial mirror of
Fluent's typed icon lattice (`Size20.Regular.*`, `Size20.Filled.*`) that delegates to
canonical Sunfish components under the hood. It does NOT provide visual or behavioral
parity with Microsoft Fluent UI System Icons; it provides **source-code shape parity**
so consumers can flip their `using Microsoft.FluentUI.AspNetCore.Components` →
`using Sunfish.Compat.FluentIcons` and keep most markup intact.

`compat-fluent-icons` is **not** the source of truth for any Sunfish component. ui-core
and the adapter packages own the canonical contracts. This package is a thin, disposable
shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Fluent-UI-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `Sizes/Size20/Regular.cs` / `Sizes/Size20/Filled.cs`, or
  adding a new `Size*` bucket
- Any change to `docs/compat-fluent-icons-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Fluent UI System Icons parity value vs. maintenance cost.
3. Update `docs/compat-fluent-icons-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants (preserve across every vendor)

1. **No Fluent UI System Icons NuGet dependency.** This package MUST NOT
   `<PackageReference>` any `Microsoft.FluentUI.AspNetCore.Components.Icons`,
   `Microsoft.Fast.Components.FluentUI.Icons`, `Blazicons.FluentUI` or peer vendor
   package. Consumers must not be forced to carry a Fluent UI Icons package dependency.
2. **All wrappers live in the root namespace** `Sunfish.Compat.FluentIcons` (not nested).
   This mirrors Fluent UI Blazor's flat component namespace. Typed icon lattice classes
   (`Size20.Regular`, `Size20.Filled`) live under `Sunfish.Compat.FluentIcons.Size20`
   to preserve Fluent's `Size × Variant × Name` access path.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` —
   never silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from
   the Fluent UI System Icons original must have an explicit section in
   `docs/compat-fluent-icons-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork
   `CompatChildComponent`, `UnsupportedParam`, or `CompatIconAdapter` into this package —
   reference the shared package.

## Fluent-UI-specific clarifications

### Asset shipping is the consumer's responsibility

Microsoft Fluent UI System Icons are MIT-licensed (both the icon assets and the Blazor
wrapper code). Hard Invariant #1 (no vendor NuGet dependency) already precludes this
package from redistributing any Fluent SVG assets. This means:

- **Consumers remain responsible** for shipping Fluent icon assets via their existing
  setup (the Fluent UI Blazor component library, self-hosted SVGs, or the active Sunfish
  `ISunfishIconProvider`'s own icon set). `compat-fluent-icons` does NOT emit any
  `<link>` or `<script>` tag and does NOT embed SVGs.
- Icons are actually rendered by whichever **Sunfish `ISunfishIconProvider`** is active
  in the consumer app (FluentUI / Bootstrap / Material). The identifier passed through
  the compat layer (e.g. `"home"`) is resolved by that provider — the rendered glyph may
  visually differ from Fluent's own glyph, especially when the active provider is not
  the Fluent provider. This is the documented cost of source-shape parity without
  behavioral parity.

### Partial typed-icon lattice (starter set)

Fluent UI System Icons exposes a three-dimensional lattice — `Size × Variant × Name`
where Size ∈ {10, 12, 16, 20, 24, 28, 32, 48} and Variant ∈ {Regular, Filled}. Mirroring
the full lattice would generate thousands of types and lock `compat-fluent-icons` into
Fluent's catalog evolution cadence. Phase 2A ships a **50-icon starter set per variant**
in the **`Size20` bucket only** (`Size20.Regular.*`, `Size20.Filled.*`). This covers the
most-used default size in the Fluent UI ecosystem (mid-density UI chrome).

Consumers whose icons fall outside the starter set can:

1. Pass a plain string identifier directly: `<FluentIcon Value="@(\"rocket\")" />`.
2. Submit a policy-gated PR to extend the starter class or add a new `Size*` bucket.

### Out of scope for Phase 2A

- **Other Size buckets** (`Size10`, `Size12`, `Size16`, `Size24`, `Size28`, `Size32`,
  `Size48`) — intentionally not mirrored. Consumers relying on a specific pixel size
  should style via CSS on the host element or pass the appropriate identifier directly.
- **`Icon.FromImageUrl`** factory — the Fluent wrapper supports rendering an arbitrary
  image URL as an icon. Out of scope for Phase 2A; consumers should use an `<img>` tag
  or pass a `RenderFragment` to `<FluentIcon Value="...">`.
- **Width/Height as individual parameters** — Fluent allows overriding the rendered
  size in pixels. Compat maps to Sunfish `IconSize` enum only; precise pixel control
  is out of scope.
- **`Slot`, `OnClick`, `Color`, `CustomColor`, `Title`** and other Fluent-specific
  presentational parameters — logged-and-dropped or forwarded via `AdditionalAttributes`
  where possible.

## Coverage Expansion

Phase 2A ships `FluentIcon` plus a 50-icon starter set in `Size20.Regular` and
`Size20.Filled`. Additional icons, size buckets, and wrapper surfaces are added
one-per-PR under this policy gate. Candidates for future coverage:

- Extending the typed-icon starter set beyond the first 50 per variant
- Adding additional Size buckets (`Size16`, `Size24`) when consumer demand is evidenced
- `Icon.FromImageUrl` factory parity (requires a Sunfish contract decision for URL-to-
  fragment resolution)
- Width/Height pixel-precise sizing (requires a `ui-core` `SunfishIcon` contract extension)

## See Also

- `docs/compat-fluent-icons-mapping.md` — authoritative divergence log
- `packages/compat-shared/POLICY-TEMPLATE.md` — shared-invariant source
- `packages/compat-font-awesome/POLICY.md` — sibling compat-icon package (Phase 1 reference)
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
