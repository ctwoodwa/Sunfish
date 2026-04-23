# Migration Guides — Commercial Blazor Vendors to Sunfish

This index points to per-vendor walkthroughs for flipping your `using` directives from a
commercial Blazor vendor's namespace to the `Sunfish.Compat.*` equivalent (or, for
DevExpress, the manual-migration path).

## Universal first step

Before you touch any `using` directive, install
[`Sunfish.Analyzers.CompatVendorUsings`](../../packages/analyzers/compat-vendor-usings/README.md).
The analyzer surfaces migration opportunities in your existing codebase — it flags every
vendor namespace it recognizes with either **SF0001** (compat shim available; code fix
offered) or **SF0002** (no shim; manual migration). Once the analyzer is referenced, the
migration becomes a review of SF0001 diagnostics plus any SF0002 informationals.

## Vendor table

| Vendor | Compat package | Analyzer rule | Migration guide |
|---|---|---|---|
| Telerik UI for Blazor | `Sunfish.Compat.Telerik` | SF0001 | [from-telerik.md](from-telerik.md) |
| Syncfusion Blazor | `Sunfish.Compat.Syncfusion` | SF0001 | [from-syncfusion.md](from-syncfusion.md) |
| Infragistics Ignite UI for Blazor | `Sunfish.Compat.Infragistics` | SF0001 | [from-infragistics.md](from-infragistics.md) |
| DevExpress Blazor | _(no compat shim)_ | SF0002 | [from-devexpress.md](from-devexpress.md) |

## What compat provides — and what it doesn't

Every `Sunfish.Compat.*` package ships **source-shape parity only**. Your existing markup,
parameter names, and handler signatures continue to compile. The packages do **not**
guarantee pixel-perfect visual parity or behavioral parity with the original vendor
components — they wrap canonical Sunfish adapter components underneath. Each vendor's
`docs/compat-<vendor>-mapping.md` file is an audit trail of every divergence (dropped
parameters, thrown values, fallback defaults).

## Background reading

- [`packages/compat-shared/POLICY-TEMPLATE.md`](../../packages/compat-shared/POLICY-TEMPLATE.md) — the policy gate every compat package inherits
- [`docs/adrs/0014-adapter-parity-policy.md`](../adrs/0014-adapter-parity-policy.md) — why Sunfish treats adapter parity as a first-class promise
- [`docs/adrs/0026-bridge-posture.md`](../adrs/0026-bridge-posture.md) — how compat packages fit the overall bridge posture
