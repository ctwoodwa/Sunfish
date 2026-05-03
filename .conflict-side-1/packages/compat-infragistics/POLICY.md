# compat-infragistics Package Policy

## Purpose

`Sunfish.Compat.Infragistics` is a **migration off-ramp** for consumers moving from
Infragistics Ignite UI for Blazor. It exposes Ignite-UI-API-shaped Razor components
(`IgbButton`, `IgbGrid`, `IgbDialog`, etc.) that delegate to canonical Sunfish components
under the hood. It does NOT provide visual or behavioral parity with Ignite UI; it provides
**source-code shape parity** so consumers can flip `using IgniteUI.Blazor.Controls` →
`using Sunfish.Compat.Infragistics` and keep most markup intact.

compat-infragistics is **not** the source of truth for any Sunfish component. ui-core and
the adapter packages (`ui-adapters-blazor`) own the canonical contracts. This package is a
thin, disposable shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Ignite-UI-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `Enums/` or `EventArgs/`
- Any change to `docs/compat-infragistics-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Ignite UI parity value vs. maintenance cost.
3. Update `docs/compat-infragistics-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants

These MUST NOT change:

1. **No `IgniteUI.Blazor` NuGet dependency.** This package MUST NOT `<PackageReference>` any
   `IgniteUI.*` or `Infragistics.*` package. Consumers must not be forced to carry an
   Infragistics license to consume compat-infragistics. This invariant is the backbone of
   the licensing analysis in §Licensing below.
2. **All wrappers live in the root namespace** `Sunfish.Compat.Infragistics` (not nested).
   This mirrors Ignite UI's flat `IgniteUI.Blazor.Controls.*` shape.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` — never
   silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from the
   Ignite UI original must have an explicit section in `docs/compat-infragistics-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork
   `CompatChildComponent`, `UnsupportedParam`, or `CompatIconAdapter` into this package —
   reference the shared package.

## Licensing — Hybrid MIT + Commercial

Ignite UI Blazor has a materially more nuanced licensing posture than the other compat
targets, and this section is load-bearing for the invariants above:

- **`igniteui-blazor`** (the Blazor wrapper repo) is **MIT-licensed**. Declaring types with
  identical names in a different namespace is unambiguously permitted.
- **`igniteui-webcomponents`** (the underlying WC library) is **dual-licensed**:
  - **MIT** for 35+ standard components — covers all 10 non-Grid compat targets in this
    package (Button, Icon, Checkbox, Input, Select, Combo, DatePicker, Dialog, Tooltip,
    Toast).
  - **Commercial** for **Grids** (Data, Tree, Hierarchical, Pivot) and Dock Manager.
- Compat declares API-shape types without shipping vendor runtime implementation, so Hard
  Invariant #1 (no `IgniteUI.Blazor` NuGet) carries us: no commercial runtime code is ever
  pulled in or redistributed. The public API surface (parameter names, method signatures)
  is not itself commercial.

### IgbGrid BDFL Sign-Off Clause

Adding *real* behavior to `IgbGrid` beyond shape-parity delegation (i.e. any change that
re-implements, ports, or closely mirrors runtime grid behavior from
`igniteui-webcomponents`' commercial grid implementation) requires **explicit BDFL policy
review in addition to the standard CODEOWNER sign-off**. Shape-parity parameter additions
and delegation-only changes remain under the normal policy gate.

## Web-Components Backend Note

Per [`icm/01_discovery/output/infragistics-wc-architecture-spike-2026-04-22.md`](../../icm/01_discovery/output/infragistics-wc-architecture-spike-2026-04-22.md):

> compat-infragistics renders canonical Sunfish components. Shadow DOM, Lit, and JS interop
> from Ignite UI are absent from the consumer's build because this package does not
> reference `IgniteUI.Blazor`. The WC backend is upstream of the boundary and does not leak
> into rendered output.

No `<igc-*>` custom-element tag is ever emitted by a compat-infragistics wrapper. The
compat shim substitutes the type that `using` resolves to and delegates to plain Blazor
Sunfish components; the WC machinery exists only in consumers who still import the real
Ignite UI package.

## Coverage

Initial surface ships **11 wrappers** (Form dropped — Ignite UI has no `IgbForm`; consumers
use Blazor `EditForm` natively). Additional wrappers are added one-per-PR under this policy
gate. Candidates for future coverage: `IgbSnackbar`, `IgbIconButton`, `IgbSwitch`,
`IgbRadio`, `IgbTabs`, `IgbAccordion`, `IgbTreeGrid`.

## See Also

- `docs/compat-infragistics-mapping.md` — authoritative divergence log
- `packages/compat-telerik/POLICY.md` — pattern-reference policy
- `icm/01_discovery/output/compat-infragistics-surface-inventory-2026-04-22.md` — surface spec
- `icm/01_discovery/output/infragistics-wc-architecture-spike-2026-04-22.md` — WC spike
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
