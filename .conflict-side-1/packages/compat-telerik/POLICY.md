# compat-telerik Package Policy

## Purpose

`Sunfish.Compat.Telerik` is a **migration off-ramp** for consumers moving from Telerik UI for
Blazor. It exposes Telerik-API-shaped Razor components (`TelerikButton`, `TelerikGrid<T>`, etc.)
that delegate to canonical Sunfish components under the hood. It does NOT provide visual or
behavioral parity with Telerik; it provides **source-code shape parity** so consumers can flip
`using Telerik.Blazor.Components` → `using Sunfish.Compat.Telerik` and keep most markup intact.

compat-telerik is **not** the source of truth for any Sunfish component. ui-core and the
adapter packages (`ui-adapters-blazor`) own the canonical contracts. compat-telerik is a thin,
disposable shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new Telerik-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `Enums/` or `ThemeConstants/`
- Any change to `docs/compat-telerik-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against Telerik parity value vs. maintenance cost.
3. Update `docs/compat-telerik-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants

These MUST NOT change:

1. **No Telerik NuGet dependency.** This package MUST NOT `<PackageReference>` any
   `Telerik.*` package. Consumers must not be forced to carry a Telerik license.
2. **All wrappers live in the root namespace** `Sunfish.Compat.Telerik` (not nested). This
   mirrors Telerik's flat `Telerik.Blazor.Components.*` shape.
3. **Unsupported parameters throw** `NotSupportedException` via the
   `UnsupportedParam.Throw(paramName, value, migrationHint)` helper — never silently drop
   values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from
   Telerik must have an explicit section in `docs/compat-telerik-mapping.md`.

## Coverage Expansion

Phase 6 ships 12 wrappers. Additional wrappers are added one-per-PR under this policy gate.
Candidates for future coverage: `TelerikTreeView`, `TelerikTreeList`, `TelerikScheduler`,
`TelerikEditor`, `TelerikWizard`, `TelerikTabStrip`, `TelerikChart`.

## See Also

- `docs/compat-telerik-mapping.md` — authoritative divergence log
- `icm/pipelines/sunfish-api-change/routing.md` — ICM ticket workflow
- `CLAUDE.md` — overall project policy
