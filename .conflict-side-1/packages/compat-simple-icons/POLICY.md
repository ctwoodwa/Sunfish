# `compat-simple-icons` Package Policy

## Purpose

`Sunfish.Compat.SimpleIcons` is a **migration off-ramp** for consumers moving from Simple
Icons Blazor wrappers (e.g. `timewarp-simple-icons`, `Json_exe.MudBlazor.SimpleIcons`,
`NgIcons.SimpleIcons`). It exposes a Simple-Icons-API-shaped Razor component
(`SimpleIcon`) that renders `<i class="si si-*">` markup matching the upstream Simple
Icons stylesheet / sprite conventions. It does NOT provide visual or behavioral parity
with any specific wrapper; it provides **source-code shape parity** so consumers can
flip their Simple Icons `using` directives to `using Sunfish.Compat.SimpleIcons` and
keep most markup intact.

`compat-simple-icons` is **not** the source of truth for any Sunfish component. ui-core
and the adapter packages own the canonical contracts. This package is a thin, disposable
shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Simple-Icons-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `SimpleIconSlug.cs` / `SimpleIconSlugExtensions.cs`
- Any change to `docs/compat-simple-icons-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Simple Icons parity value vs. maintenance cost.
3. Update `docs/compat-simple-icons-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants (preserve across every vendor)

1. **No Simple Icons NuGet dependency.** This package MUST NOT `<PackageReference>` any
   `SimpleIcons.*` / `timewarp-simple-icons` / `NgIcons.SimpleIcons` /
   `Json_exe.MudBlazor.SimpleIcons` / other Simple-Icons-wrapper package. Consumers must
   not be forced to carry a vendor package dependency.
2. **All wrappers live in the root namespace** `Sunfish.Compat.SimpleIcons` (not
   nested). This mirrors the flat-namespace shape used by the common Simple Icons
   Blazor wrappers.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` —
   never silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from
   the Simple Icons originals must have an explicit section in
   `docs/compat-simple-icons-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork
   `CompatChildComponent`, `UnsupportedParam`, or `CompatIconAdapter` into this package —
   reference the shared package.

## Simple-Icons-specific clarifications

### License — CC0-1.0 (public domain)

Simple Icons is licensed **CC0-1.0** (Creative Commons Zero — public domain dedication).
This is the **most permissive license in the compat-icon-expansion workstream** — more
permissive than MIT (no notice requirement), more permissive than Apache-2.0 (no
attribution or patent-grant preservation), more permissive than Font Awesome's
CC-BY-4.0 (no attribution requirement). At the Sunfish-package level:

- **No runtime attribution is required.** Consumers may ship Simple Icons assets in a
  commercial product with no obligation to credit Simple Icons.
- **No license notice must travel with the rendered output.** Unlike CC-BY-4.0 (Font
  Awesome Free SVGs), there is no "attribution chain" requirement.
- **No derivative-work restrictions.** Consumers may modify, remix, or redistribute
  the SVGs without constraint.

**This is NOT the norm for icon libraries.** Every other library in the
compat-icon-expansion workstream ships under MIT, Apache-2.0, ISC, or CC-BY-4.0 —
all of which require at least a notice / attribution. Simple Icons' CC0-1.0 release
is an explicit, deliberate choice by the project maintainers to remove friction for
downstream use.

That said: the CC0-1.0 dedication covers the **icon artwork and slug catalog**. It
does NOT cover the depicted brands' trademarks, which remain the property of their
respective owners. Consumers rendering brand icons in a product context — especially
one that could imply endorsement, partnership, or official affiliation — should:

1. Review each depicted brand's trademark / usage policy. Most brands publish
   "acceptable uses" that cover the common integration-indicator case
   (e.g. "Sign in with GitHub", "Share to Twitter", "Pay with Stripe").
2. Consider a courtesy acknowledgement — e.g. a footer note citing Simple Icons'
   public-domain release — even though none is legally required. The Simple Icons
   project does substantial ongoing curation work; a voluntary credit sustains the
   community that maintains the catalog.

### Asset shipping is the consumer's responsibility

Hard Invariant #1 (no vendor NuGet dependency) precludes this package from
redistributing any Simple Icons assets. This means:

- **Consumers remain responsible** for shipping a Simple Icons stylesheet / sprite /
  SVG bundle via their existing setup (CDN `<link>` in `index.html` / `App.razor`,
  NPM install, or self-hosted). `compat-simple-icons` does NOT emit any `<link>`
  or `<script>` tag.
- The wrapper renders `<i class="si si-@slug sf-simple-icon" style="color: @Color">`
  markup. The `si si-*` classes are what an upstream Simple Icons stylesheet /
  webfont targets; the `sf-simple-icon` class is preserved alongside per the
  compat-icon split-class convention so Sunfish-aware styles can target the wrapper
  independently.
- If no Simple Icons stylesheet is loaded, the `<i>` element renders empty — Simple
  Icons' distribution model is typically SVG sprites or a webfont, both keyed to
  `si-*` classes.

### Color pass-through is intentional

Unlike `FontAwesomeIcon` or `BootstrapIcon`, `SimpleIcon` exposes a dedicated `Color`
parameter. This is a deliberate nod to Simple Icons' distinguishing feature: the
project curates each brand's **official color** (e.g. `#181717` for GitHub, `#1DA1F2`
for Twitter, `#4285F4` for Google). Consumers frequently render brand icons in their
official colors to improve recognition; threading that through as a first-class
parameter rather than making consumers write `style="color:..."` via splat is worth
the API surface cost.

The value is emitted as inline `color: @Color` — no validation against a known
palette. `"currentColor"` is a valid value and is a common choice when a consumer
wants the icon to inherit ambient text color (monochrome usage).

### Component / enum-naming divergence

The typed identifier is exposed as `SimpleIconSlug` (enum) with a matching `Slug`
parameter on `SimpleIcon`, rather than the more generic `IconName` / `Name` used by
`compat-bootstrap-icons`. The rename reflects Simple Icons' own terminology — the
project calls brand identifiers "slugs" uniformly, and consumer code reads more
naturally as `<SimpleIcon Slug="SimpleIconSlug.Github" />` than `<SimpleIcon
Name="..." />`.

## Coverage Expansion

Phase 3C ships `SimpleIcon` plus a 50-slug starter-set `SimpleIconSlug` enum and its
`ToSlug()` extension. Additional slugs and wrapper surfaces are added one-per-PR under
this policy gate. Simple Icons' upstream catalog exceeds **2,800 brand icons** — the
starter set covers well under 2% of the catalog by design. Consumers outside the
starter set should pass a raw string via `SlugString`; the escape hatch is the
expected primary call-site for Simple Icons since a full typed enum would be
impractical to maintain against a catalog that churns with every brand-identity
refresh upstream.

## See Also

- `docs/compat-simple-icons-mapping.md` — authoritative divergence log
- `packages/compat-shared/POLICY-TEMPLATE.md` — shared-invariant source
- `packages/compat-font-awesome/POLICY.md` — Phase 1 sibling compat-icon package
- `packages/compat-bootstrap-icons/POLICY.md` — Phase 2 sibling; closest typed-enum +
  raw-string escape-hatch precedent
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
