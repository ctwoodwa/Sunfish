# Sunfish.Compat.Shared

Vendor-agnostic primitives shared by every `Sunfish.Compat.*` package.

## What lives here

| Type | Purpose |
|---|---|
| `CompatChildComponent<TParent>` | Base class for child-component shims (grid columns, tab panels, validation-message children, etc.) that must resolve a cascading parent. Handles the `[CascadingParameter]` lookup and the "rendered outside parent" exception. |
| `UnsupportedParam.Throw` | Builds a uniformly-shaped `NotSupportedException` for vendor parameter values that have no Sunfish equivalent. Vendor packages include a pointer to their own mapping doc in the `migrationHint` string. |
| `CompatIconAdapter.ToRenderFragment` | Converts vendor-shaped `object?` icon values (RenderFragment / vendor SVG type / string) into a normalized `RenderFragment`. |

## What does NOT live here

Anything with vendor branding: enum shims (`ButtonType`, `FilterMode`, etc.), theme-color
constants, event-args type shims, or per-vendor wrapper components. Those belong in their
respective `compat-telerik`, `compat-syncfusion`, `compat-devexpress`, `compat-infragistics`
packages.

## Invariants

1. **No vendor NuGet dependency.** This package MUST NOT reference any vendor package.
2. **No vendor-branded identifiers.** Type names, member names, and exception messages stay
   vendor-neutral. Vendors layer branding via `ShimName`/`ParentName` overrides and
   `migrationHint` strings they pass in.
3. **Compatible across every compat package.** A change here affects all four vendor packages
   at once. Coordinate breaking changes via CODEOWNER review.

See [`POLICY-TEMPLATE.md`](./POLICY-TEMPLATE.md) for the per-vendor POLICY boilerplate.
