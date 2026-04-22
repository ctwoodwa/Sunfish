# `compat-material-icons` Package Policy

## Purpose

`Sunfish.Compat.MaterialIcons` is a **migration off-ramp** for consumers moving from
Google Material Icons / Material Symbols Blazor wrappers (e.g.
`MudBlazor.FontIcons.MaterialIcons`, `MudBlazor.FontIcons.MaterialSymbols`,
`Blazorise.Icons.Material`, `Blazicons.MaterialDesignIcons`). It exposes
MaterialIcons/MaterialSymbols-API-shaped Razor components (`MaterialIcon`,
`MaterialSymbol`) that emit the same CSS-ligature markup Google's official
rendering path uses (`<span class="material-icons">home</span>` /
`<span class="material-symbols-outlined">home</span>`). It does NOT provide visual or
behavioral parity on its own — it provides **source-code shape parity** so consumers
can flip their `using` directives and keep markup intact while their existing Google
Fonts `<link>` (or self-hosted webfont) continues to render glyphs.

`compat-material-icons` is **not** the source of truth for any Sunfish component.
ui-core and the adapter packages own the canonical contracts. This package is a thin,
disposable shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Material-Icons- or Material-Symbols-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `MaterialIconName`
- Adding variants to `MaterialSymbolVariant`
- Any change to `docs/compat-material-icons-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Material parity value vs. maintenance cost.
3. Update `docs/compat-material-icons-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants (preserve across every vendor)

1. **No Material NuGet dependency.** This package MUST NOT `<PackageReference>` any
   `MudBlazor.FontIcons.*` / `Blazorise.Icons.Material` / `Blazicons.MaterialDesignIcons`
   package. Consumers must not be forced to carry a downstream Material wrapper
   dependency.
2. **All wrappers live in the root namespace** `Sunfish.Compat.MaterialIcons` (not
   nested). This mirrors MudBlazor's flat `MudBlazor.FontIcons.MaterialIcons` namespace
   shape.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` —
   never silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from
   the Material Icons / Material Symbols original must have an explicit section in
   `docs/compat-material-icons-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork
   `CompatChildComponent`, `UnsupportedParam`, or `CompatIconAdapter` into this package
   — reference the shared package.

## Material-specific clarifications

### License — Apache 2.0

Google Material Icons and Material Symbols are licensed under **Apache-2.0**. Apache-2.0
does **not** require attribution at runtime inside the consumer app, but consumers who
ship Google's font files as part of their distribution SHOULD preserve Apache's
copyright and `NOTICE` files per §4 of the license. This is a consumer-side obligation
— `compat-material-icons` neither ships nor redistributes any Google assets, so no
attribution is carried at the Sunfish-package level.

### Asset shipping is the consumer's responsibility

Hard Invariant #1 (no vendor NuGet dependency) combined with §3 of this policy
(consumer remains responsible for font loading via CSS) means this package ships
**no font files and no CSS**. Specifically:

- **Consumers remain responsible** for loading the Material Icons or Material Symbols
  webfont in their app layout. Typical options:
  - Google Fonts `<link>` tag:
    `<link href="https://fonts.googleapis.com/icon?family=Material+Icons" rel="stylesheet">`
    (and/or `...?family=Material+Symbols+Outlined` etc. for Symbols variants)
  - Self-hosted variable-font file plus CSS `@font-face` declaration
  - An existing MudBlazor / Blazorise layout continues to work unchanged
- `compat-material-icons` does NOT emit any `<link>` or `<script>` tag.
- When the Google font is loaded, the browser renders the CSS-ligature markup emitted
  by `MaterialIcon` / `MaterialSymbol` as the Material glyph. When it is not loaded,
  the raw ligature text (e.g. `home`) is shown — same failure mode as Google's own
  rendering path.

### Material Icons vs Material Symbols

Google ships two coexisting icon systems from the same upstream library:

- **Material Icons** (legacy): a filled-style SVG/font set, rendered via
  `<span class="material-icons">icon_name</span>`. `MaterialIcon` targets this shape.
- **Material Symbols** (newer): a variable-font successor with three stylistic variants
  (Outlined / Rounded / Sharp) plus axes for fill, weight, grade, and optical size.
  Rendered via `<span class="material-symbols-outlined">icon_name</span>` etc.
  `MaterialSymbol` targets this shape, with a `Variant` parameter that picks between
  the three class-level variants. Per-axis (fill/weight/grade/optical-size) control is
  out of scope for Phase 2 — consumers needing those can use `AdditionalAttributes`
  to pass a `style="font-variation-settings:..."` override.

Both components are intentionally shipped in one package (per Stage 01 discovery
recommendation) because upstream treats Material Icons and Material Symbols as one
library in two evolving forms.

### What is NOT mirrored

- Per-variable-axis parameters (`Fill`, `Weight`, `Grade`, `OpticalSize`) — Phase 2
  scope limit; route through `AdditionalAttributes` / `style`.
- Ligature-rendering toggles (Material Symbols supports both ligatures and code points;
  only ligature rendering is mirrored because that's what MudBlazor and Blazorise both
  emit).
- MudBlazor's `Color` parameter — Sunfish has no per-icon color primitive in Phase 2;
  consumers style via the active `ISunfishIconProvider` or CSS on the host element.

## Coverage Expansion

Phase 2 ships `MaterialIcon`, `MaterialSymbol`, the `MaterialSymbolVariant` enum, and a
50-entry `MaterialIconName` starter-set of typed identifier constants. Additional
icons and wrapper surfaces are added one-per-PR under this policy gate. Candidates for
future coverage:

- Typed identifier classes for the full Material catalog (~2,500 icons) — likely
  source-generated rather than hand-maintained
- Per-variable-axis parameters on `MaterialSymbol` for FILL/wght/GRAD/opsz
- A `MaterialIconColor` parameter if Sunfish adds an icon-color contract
- Code-point-based rendering as an alternative to ligatures (for i18n-sensitive apps
  where ligatures misfire)

## See Also

- `docs/compat-material-icons-mapping.md` — authoritative divergence log
- `packages/compat-shared/POLICY-TEMPLATE.md` — shared-invariant source
- `packages/compat-font-awesome/POLICY.md` — Phase 1 reference policy
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
