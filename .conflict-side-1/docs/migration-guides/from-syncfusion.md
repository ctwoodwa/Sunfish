# Migrating from Syncfusion Blazor to Sunfish

Sunfish ships `Sunfish.Compat.Syncfusion`, a local-first, fully-open-source shim that
mirrors the Syncfusion Blazor `Sf*` component surface so you can migrate off Syncfusion
without rewriting markup. The compat package wraps canonical Sunfish components, carries
**zero runtime dependency on the Syncfusion NuGets**, and is designed as a drop-in swap
for most common `Sf*` component usages.

## Prerequisites

- A .NET 9 Blazor project that currently references any `Syncfusion.Blazor.*` package.
- Install `Sunfish.Compat.Syncfusion` from NuGet (version pin TBD — pin against a specific
  prerelease until v1).
- Install `Sunfish.Analyzers.CompatVendorUsings` — the Roslyn analyzer that detects
  Syncfusion usings and offers a code fix. Reference it with `PrivateAssets="all"`.

## Step-by-step

1. **Add the package references:**

   ```xml
   <ItemGroup>
     <PackageReference Include="Sunfish.Compat.Syncfusion" />
     <PackageReference Include="Sunfish.Analyzers.CompatVendorUsings" PrivateAssets="all" />
   </ItemGroup>
   ```

2. **Run the analyzer.** Every `using Syncfusion.Blazor;` or child namespace
   (`Syncfusion.Blazor.Buttons`, `Syncfusion.Blazor.Inputs`, `Syncfusion.Blazor.Grids`,
   `Syncfusion.Blazor.Popups`, `Syncfusion.Blazor.DropDowns`, `Syncfusion.Blazor.DataForm`,
   `Syncfusion.Blazor.Calendars`, `Syncfusion.Blazor.Notifications`) raises **SF0001**.

3. **Apply the code fix.** The fix collapses every flagged child namespace to the single
   `using Sunfish.Compat.Syncfusion;` — applying the fix across a file that imports
   multiple Syncfusion child namespaces will leave exactly one compat using at the top.

4. **Build and run tests.** Component names (`SfButton`, `SfGrid<TValue>`, `SfDataForm`,
   etc.) and most parameter shapes are preserved.

5. **Review the divergence log:** [`docs/compat-syncfusion-mapping.md`](../compat-syncfusion-mapping.md).

6. **Address manual follow-ups.** The visible divergences you'll hit first:

   - **`SfDataForm` is opinionated; `SunfishForm` is thin.** Layout-orchestration
     parameters — `ColumnCount`, `ColumnSpacing`, `ButtonsAlignment`, `EnableFloatingLabel`,
     `LabelPosition` — are log-and-dropped. Wrap form children in your own
     grid/flex layout to achieve the multi-column effect. `ValidationDisplayMode = ToolTip`
     throws — there is no Sunfish tooltip-validation surface.
   - **`SfIcon.Name` is a ~50-icon curated subset of Syncfusion's ~1,500-value enum.**
     Unmapped values log a warning and fall through to your icon provider (LogAndFallback).
     For icons outside the subset, use the raw `<span class="e-icons e-<name>" />` pattern or
     migrate your icon provider to a Sunfish icon package.
   - **No `SfWindow` — use `SfDialog`.** Syncfusion itself ships no `SfWindow`; `SfDialog`
     is the analog of Telerik's `TelerikWindow`. The compat package intentionally follows
     Syncfusion's naming.
   - **Multi-line `SfTextBox`.** `Multiline=true` with `Type=Password` throws; other
     combinations fall back to single-line with a warning.

## Licensing sidebar

Syncfusion's Community License is silent on third-party API-shape replication. Because
`Sunfish.Compat.Syncfusion` ships **zero dependency on any `Syncfusion.Blazor.*` NuGet
package** — it only mirrors the type names and parameter shapes as a migration off-ramp —
it does not subclass, derive from, or redistribute Syncfusion code. This is the same
posture used by `Sunfish.Compat.Telerik`.

## When NOT to use this package

If your product depends on Syncfusion's exact visual rendering, its themes, its specific
grid filtering/grouping UX, or features outside the mapped surface (e.g.
`GridEvents` wiring in Phase 0, `PopupHeight`/`PopupWidth` on dropdowns, toolbar models,
`EnablePersistence`, virtual scrolling), stay on Syncfusion. This compat gives you
**source-shape parity**, not visual or behavioral parity.
