# compat-syncfusion Package Policy

## Purpose

`Sunfish.Compat.Syncfusion` is a **migration off-ramp** for consumers moving from Syncfusion
Blazor. It exposes Syncfusion-API-shaped Razor components (`SfButton`, `SfGrid<T>`, `SfDialog`,
etc.) that delegate to canonical Sunfish components under the hood. It does NOT provide visual
or behavioral parity with Syncfusion; it provides **source-code shape parity** so consumers can
flip `using Syncfusion.Blazor.*` → `using Sunfish.Compat.Syncfusion` and keep most markup
intact.

compat-syncfusion is **not** the source of truth for any Sunfish component. ui-core and the
adapter packages (`ui-adapters-blazor`) own the canonical contracts. compat-syncfusion is a
thin, disposable shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Syncfusion-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `Enums/` or the `IconName` subset
- Any change to `docs/compat-syncfusion-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Syncfusion parity value vs. maintenance cost.
3. Update `docs/compat-syncfusion-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants

These MUST NOT change:

1. **No Syncfusion NuGet dependency.** This package MUST NOT `<PackageReference>` any
   `Syncfusion.*` package. Consumers must not be forced to carry a Syncfusion license.
2. **All wrappers live in the root namespace** `Sunfish.Compat.Syncfusion` (not nested). This
   mirrors Syncfusion's flat `Syncfusion.Blazor.*` shape where consumer markup sees `<SfButton />`
   without namespace prefix.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` — never
   silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from the
   Syncfusion original must have an explicit section in `docs/compat-syncfusion-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork `CompatChildComponent`,
   `UnsupportedParam`, or `CompatIconAdapter` — reference the shared package.

## Syncfusion-Specific Clauses

### IconName enum subset

Syncfusion's `IconName` enum exposes ~1,500 font-icon values (the full Syncfusion icon
catalog). compat-syncfusion **does not ship a 1:1 enum clone**; instead `SfIcon.Name` accepts a
string (Syncfusion's `IconName` values `ToString()` cleanly) and maps a curated subset of the
most common icons through to Sunfish equivalents. Unmapped names `LogAndFallback` to a
placeholder glyph with a warning log pointing at `docs/compat-syncfusion-mapping.md`.

Consumers who need a specific Syncfusion icon not in the subset should:
- Use the raw CSS-class pattern (`<span class="e-icons e-<icon-name>"></span>`) which
  bypasses the component shim entirely.
- File a PR under this POLICY gate adding the icon name to the mapping table.

### Licensing

Syncfusion Essential Studio for Blazor is available under a **Community License** (free for
individuals / small teams) and a **Commercial License**. compat-syncfusion ships source-code
shape parity only — the package carries **zero runtime or build-time dependency on Syncfusion
NuGets or DLLs**. Consumers migrating off Syncfusion drop the Syncfusion NuGet reference and
pick up `Sunfish.Compat.Syncfusion`; no Syncfusion license is implicated.

The full Syncfusion EULA should be reviewed before first public release (pending Stage 02
legal-review checkbox) for trademark clauses on the `Sf*` prefix and API-naming restrictions;
provisionally the Community License is silent on API-surface replication. See
`icm/01_discovery/output/compat-syncfusion-surface-inventory-2026-04-22.md` §3 for the
licensing posture.

## Coverage Expansion

The initial package ships 12 main wrappers (SfButton, SfIcon, SfCheckBox, SfTextBox,
SfDropDownList, SfComboBox, SfDatePicker, SfDataForm, SfGrid, SfDialog, SfTooltip, SfToast)
plus ~20 child-component shims (Grid settings, Dialog templates, etc.) and ~18 EventArgs
shims. Additional wrappers are added one-per-PR under this policy gate.

## See Also

- `docs/compat-syncfusion-mapping.md` — authoritative divergence log
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
