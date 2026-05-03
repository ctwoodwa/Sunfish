# `compat-<vendor>` Package Policy — Template

Copy this file into each new `compat-<vendor>` package as `POLICY.md`, then fill the
`<vendor>` placeholders and any vendor-specific clauses. The shared invariants below MUST
be preserved verbatim across every vendor.

---

## Purpose

`Sunfish.Compat.<Vendor>` is a **migration off-ramp** for consumers moving from `<Vendor>` UI
for Blazor. It exposes `<Vendor>`-API-shaped Razor components that delegate to canonical
Sunfish components under the hood. It does NOT provide visual or behavioral parity with
`<Vendor>`; it provides **source-code shape parity** so consumers can flip
`using <Vendor>.Blazor.Components` → `using Sunfish.Compat.<Vendor>` and keep most markup intact.

`compat-<vendor>` is **not** the source of truth for any Sunfish component. ui-core and the
adapter packages own the canonical contracts. This package is a thin, disposable shim layer.

## Policy Gate

All changes to this package require **explicit sign-off** from a listed CODEOWNER. This
includes:

- Adding a new `<Vendor>`-shaped wrapper component
- Changing the parameter mapping of an existing wrapper
- Promoting a parameter from "mapped" to "unsupported" or vice versa
- Adding new entries to `Enums/` or `ThemeConstants/`
- Any change to `docs/compat-<vendor>-mapping.md`

## Required Workflow

1. Open an ICM ticket under the `sunfish-api-change` or `sunfish-feature-change` pipeline variant.
2. Justify the change against `<Vendor>` parity value vs. maintenance cost.
3. Update `docs/compat-<vendor>-mapping.md` in the **same PR** as the code change.
4. Obtain CODEOWNER approval before merge.

## Hard Invariants (preserve across every vendor)

1. **No `<Vendor>` NuGet dependency.** This package MUST NOT `<PackageReference>` any
   vendor package. Consumers must not be forced to carry a vendor license.
2. **All wrappers live in the root namespace** `Sunfish.Compat.<Vendor>` (not nested). This
   mirrors `<Vendor>`'s flat namespace shape.
3. **Unsupported parameters throw** `NotSupportedException` via
   `Sunfish.Compat.Shared.UnsupportedParam.Throw(paramName, value, migrationHint)` — never
   silently drop values that have functional (non-cosmetic) impact.
4. **Divergences are documented.** Any wrapper whose behavior or surface diverges from the
   `<Vendor>` original must have an explicit section in `docs/compat-<vendor>-mapping.md`.
5. **Shared primitives come from `Sunfish.Compat.Shared`.** Do not fork `CompatChildComponent`,
   `UnsupportedParam`, or `CompatIconAdapter` into the vendor package — reference the shared
   package.

## Coverage Expansion

Each compat package ships an initial 12-wrapper surface (Button, Icon, CheckBox, TextBox,
DropDownList, ComboBox, DatePicker, Form, Grid, Window, Tooltip, Notification). Additional
wrappers are added one-per-PR under this policy gate.
