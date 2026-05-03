# Sunfish.Analyzers.CompatVendorUsings

Roslyn analyzer + code fix that detects vendor Blazor `using` directives and suggests
the corresponding `Sunfish.Compat.*` replacement.

## What it does

When your code contains a `using` directive for a known commercial Blazor vendor
library (or certain icon libraries), this analyzer raises an informational
diagnostic pointing at the Sunfish compat package that provides a migration
off-ramp. For vendors with a shim, it also offers a **code fix** that flips the
using directive in place.

The analyzer ships in the NuGet `analyzers/dotnet/cs/` folder and activates
automatically for any project that references the package. No configuration is
required.

## Diagnostic IDs

| ID       | Severity | Code fix | Meaning |
|----------|----------|----------|---------|
| `SF0001` | Info     | Yes      | Vendor namespace detected; a Sunfish.Compat.* shim is available. |
| `SF0002` | Info     | No       | Vendor namespace detected; Sunfish has no shim (manual migration). |

### SF0001 — Compat shim available

Example:

```csharp
using Telerik.Blazor.Components;   // SF0001: flip to 'using Sunfish.Compat.Telerik;'
```

Applying the code fix produces:

```csharp
using Sunfish.Compat.Telerik;
```

For vendors whose surface spans many child namespaces (Syncfusion, Infragistics),
the code fix collapses all matching usings to the single Sunfish compat namespace.
If you apply the fix to both `using Syncfusion.Blazor.Buttons;` and
`using Syncfusion.Blazor.Grids;`, the second fix will remove its using directive
because `using Sunfish.Compat.Syncfusion;` is already present.

### SF0002 — No shim available (DevExpress)

DevExpress was dropped from the compat-package roadmap on 2026-04-22 (see the
[compat-expansion intake note](../../../icm/00_intake/output/compat-expansion-intake.md)
§9 for the licensing rationale). The analyzer still flags `using DevExpress.Blazor;`
so that DevExpress migrators are nudged toward the manual-migration guide:

```csharp
using DevExpress.Blazor;   // SF0002: no shim; see docs/devexpress-migration.md
```

No code fix is offered for SF0002.

## Detected vendor namespaces

### Commercial-vendor compats (SF0001)

| Source namespace(s)                                                | Replacement                       |
|--------------------------------------------------------------------|-----------------------------------|
| `Telerik.Blazor.Components`                                        | `Sunfish.Compat.Telerik`          |
| `Syncfusion.Blazor` and any child namespace (`Syncfusion.Blazor.*`) | `Sunfish.Compat.Syncfusion`       |
| `IgniteUI.Blazor` and any child namespace (`IgniteUI.Blazor.*`)    | `Sunfish.Compat.Infragistics`     |

### Icon-library compats (SF0001)

| Source namespace(s)                                     | Replacement                       |
|---------------------------------------------------------|-----------------------------------|
| `Blazored.FontAwesome`                                  | `Sunfish.Compat.FontAwesome`      |
| `FontAwesome.Sharp`                                     | `Sunfish.Compat.FontAwesome`      |
| `Microsoft.FluentUI.AspNetCore.Components.Icons`        | `Sunfish.Compat.FluentIcons`      |
| `MatBlazor` / `MatBlazor.*`                             | `Sunfish.Compat.MaterialIcons`    |
| `Blazicons.Lucide`                                      | `Sunfish.Compat.Lucide`           |
| `Heroicons.Blazor` / `Heroicons.Blazor.*`               | `Sunfish.Compat.Heroicons`        |
| `Octicons.Blazor` / `Octicons.Blazor.*`                 | `Sunfish.Compat.Octicons`         |
| `BlazorBootstrap.Icons` / `BlazorBootstrap.Icons.*`     | `Sunfish.Compat.BootstrapIcons`   |

`BlazorBootstrap` is deliberately scoped to its `Icons` sub-namespace only;
Sunfish does not provide a general BlazorBootstrap compat surface, and flagging
the whole namespace would produce noise on non-icon usages.

### No-compat (SF0002)

| Source namespace(s)                         | Replacement                            |
|---------------------------------------------|----------------------------------------|
| `DevExpress.Blazor` / `DevExpress.Blazor.*` | _(none — see `docs/devexpress-migration.md`)_ |

## Enabling

Reference the analyzer package from any project that should receive the
diagnostics:

```xml
<ItemGroup>
  <PackageReference Include="Sunfish.Analyzers.CompatVendorUsings" PrivateAssets="all" />
</ItemGroup>
```

The analyzer runs at design-time in Visual Studio / Rider and during `dotnet build`.

## Suppressing

Per-line:

```csharp
#pragma warning disable SF0001
using Telerik.Blazor.Components;
#pragma warning restore SF0001
```

Project-wide via `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.SF0001.severity = none
dotnet_diagnostic.SF0002.severity = none
```

Project-wide via csproj:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);SF0001;SF0002</NoWarn>
</PropertyGroup>
```

## Rule design notes

- **Prefix matching.** Vendor namespaces like `Syncfusion.Blazor.Buttons` and
  `Syncfusion.Blazor.Grids` are matched against a single prefix entry
  (`Syncfusion.Blazor`). The prefix match requires the next character after
  the prefix to be either end-of-string or `.` so that `SyncfusionFake.Blazor`
  does not accidentally match.
- **Exact vs prefix precedence.** Exact matches are checked first; a
  fully-qualified vendor namespace (`Telerik.Blazor.Components`) will never
  be shadowed by a less-specific prefix.
- **Generated code is excluded.** Blazor's Razor toolchain emits generated
  `.g.cs` files with `@using`-derived usings; those should not be flagged.
- **Silent on unknown namespaces.** The analyzer never warns on a namespace
  that is not in its registry; unknown namespaces are always fine.

## Updating the registry

Add entries to `VendorNamespaceRegistry.Entries`. When adding a vendor whose
component API spans multiple child namespaces, set `prefixMatch: true`.
Add a test in `tests/CompatVendorUsingsAnalyzerTests.cs` covering at least one
representative child namespace.
