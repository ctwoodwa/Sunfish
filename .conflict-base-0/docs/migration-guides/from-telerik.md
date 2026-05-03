# Migrating from Telerik UI for Blazor to Sunfish

Sunfish ships `Sunfish.Compat.Telerik`, a local-first, fully-open-source shim that mirrors
the Telerik UI for Blazor component surface so you can migrate off Telerik without
rewriting markup. The compat package wraps canonical Sunfish components, carries zero
runtime dependency on the Telerik NuGets, and is designed as a drop-in swap for most common
`Telerik*` component usages.

## Prerequisites

- A .NET 9 Blazor project that currently references `Telerik.UI.for.Blazor`.
- Install `Sunfish.Compat.Telerik` from NuGet (version pin TBD — pin against a specific
  prerelease until v1).
- Install `Sunfish.Analyzers.CompatVendorUsings` — the Roslyn analyzer that detects Telerik
  usings and offers a code fix. Reference it with `PrivateAssets="all"`.

## Step-by-step

1. **Add the package references** to the project that consumes Telerik:

   ```xml
   <ItemGroup>
     <PackageReference Include="Sunfish.Compat.Telerik" />
     <PackageReference Include="Sunfish.Analyzers.CompatVendorUsings" PrivateAssets="all" />
   </ItemGroup>
   ```

2. **Run the analyzer.** Either via `dotnet build` or by opening the solution in Visual
   Studio / Rider. Every `using Telerik.Blazor.Components;` (in `.cs` or `.razor` / `_Imports.razor`)
   is flagged as **SF0001 — Vendor namespace detected; a Sunfish.Compat.* shim is available.**

3. **Apply the code fix.** Visual Studio / Rider offer the fix inline via the lightbulb;
   `dotnet format analyzers` will apply all SF0001 fixes in one pass. The fix rewrites the
   using to `using Sunfish.Compat.Telerik;`.

4. **Build and run tests.** The compat package preserves Telerik type names (`TelerikButton`,
   `TelerikGrid<TItem>`, etc.), so existing markup continues to compile.

5. **Review the divergence log.** Open [`docs/compat-telerik-mapping.md`](../compat-telerik-mapping.md).
   Every Telerik parameter the shim maps, drops, or throws on is recorded there. The doc is
   treated as public API: any behavior change is a breaking change.

6. **Address manual follow-ups (known gaps).** A few surfaces require attention that the
   analyzer cannot automate:

   - `TelerikGrid<T>.OnRead` still throws `NotSupportedException` — you must switch to an
     in-memory or server-bound `Data` collection.
   - Parameter-name differences for specific components — e.g. Telerik `CheckBox` (two words)
     uses the component name `TelerikCheckBox` but wraps Sunfish's `Checkbox` (one word).
   - `TelerikButton.ButtonType = Reset` throws; migrate to an `OnClick` handler that resets
     form state explicitly.
   - `TelerikWindow.State = Maximized | Minimized` throws in Phase 6.

## When NOT to use this package

If your product depends on Telerik's exact visual rendering or on behavioral features not
reproduced here (Excel-like keyboard navigation on the grid, its specific filtering UX,
animations, icon-font baseline), stay on Telerik. `Sunfish.Compat.Telerik` is a
**source-shape compat**, not a visual / behavioral parity layer. The goal is to let you
delete the Telerik license, not to reproduce its look and feel.
