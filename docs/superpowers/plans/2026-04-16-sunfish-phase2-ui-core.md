# Phase 2: UI Core Contracts — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create `packages/ui-core` with three framework-agnostic contract interfaces — `ISunfishCssProvider`, `ISunfishIconProvider`, and `ISunfishJsInterop` — migrated from Marilo.Core and stripped of all Blazor-specific types.

**Architecture:** `ui-core` sits between `foundation` and `ui-adapters-blazor` in the dependency chain. It must have no Blazor references (`MarkupString`, `ElementReference`, `DotNetObjectReference`, `IJSRuntime` are all forbidden per CONTRIBUTING.md). It depends only on `Sunfish.Foundation` (for enums used in CSS method signatures). The three interfaces are the entire public surface of this package — no implementations live here.

**Tech Stack:** .NET 10, C# 13, xUnit 2.9.x, `Nullable enable`, `TreatWarningsAsErrors`

---

## Key Decisions

**D-CSS:** `IMariloCssProvider` → `ISunfishCssProvider`. All method signatures are pure strings/enums — no Blazor types. One rename fix: `MariloResizeEdges` → `ResizeEdges` (that's how the enum landed in `Sunfish.Foundation.Enums` after Phase 1). Add `// TODO(phase-2-followup): split by category` at the top per the master plan decision D3.

**D-ICON:** `IMariloIconProvider` returns `MarkupString` (a Blazor type). Change return type to `string`. The contract doc explains that the string is pre-rendered HTML intended to be injected as raw markup — callers must not HTML-encode it. The Blazor adapter layer wraps it as `new MarkupString(provider.GetIcon(name, size))`.

**D-JS:** `IMariloJsInterop` uses `ElementReference` (Blazor) and `DotNetObjectReference<object>` (Blazor JSInterop). For the framework-agnostic contract: replace `ElementReference element` with `string elementId`, and drop `ObserveScrollAsync` entirely (no framework-agnostic substitute — will be added to the Blazor adapter in Phase 3). `BoundingBox` record moves into the same file.

---

## File Structure

```
packages/ui-core/
  Sunfish.UICore.csproj              ← new; depends on Sunfish.Foundation
  Contracts/
    ISunfishCssProvider.cs           ← migrated from IMariloCssProvider; ~265 lines
    ISunfishIconProvider.cs          ← migrated from IMariloIconProvider; no MarkupString
    ISunfishJsInterop.cs             ← migrated from IMariloJsInterop; no Blazor types
  tests/
    tests.csproj                     ← new; xUnit; depends on Sunfish.UICore
    CssProviderContractTests.cs      ← method-count + signature spot-checks
    IconProviderContractTests.cs     ← interface shape test
    JsInteropContractTests.cs        ← interface shape test
```

Files to update:
- `Sunfish.slnx` — add ui-core project and its tests

---

## Task 1: Create ui-core project

**Files:**
- Create: `packages/ui-core/Sunfish.UICore.csproj`

- [ ] **Step 1: Create Sunfish.UICore.csproj**

```xml
<!-- packages/ui-core/Sunfish.UICore.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.UICore</PackageId>
    <Description>Framework-agnostic CSS, icon, and JS interop contracts for Sunfish.</Description>
    <NoWarn>CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="tests/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
  </ItemGroup>
</Project>
```

`CS1591` is suppressed at the project level so individual interface members don't require XML doc comments beyond what we migrate from Marilo. The `Compile Remove` prevents the test subfolder from being compiled into the library (same pattern as foundation).

- [ ] **Step 2: Create Contracts directory placeholder**

```bash
mkdir -p "C:/Projects/Sunfish/packages/ui-core/Contracts"
```

- [ ] **Step 3: Verify the project file is valid**

```bash
cd "C:/Projects/Sunfish"
dotnet restore packages/ui-core/Sunfish.UICore.csproj
```

Expected: restore completes with 0 errors. The project has no source files yet, which is fine.

---

## Task 2: Migrate ISunfishCssProvider

**Files:**
- Create: `packages/ui-core/Contracts/ISunfishCssProvider.cs`
- Source: `C:/Projects/Marilo/src/Marilo.Core/Contracts/IMariloCssProvider.cs`

- [ ] **Step 1: Write the test first**

Create `packages/ui-core/tests/CssProviderContractTests.cs`:

```csharp
using System.Reflection;
using Sunfish.UICore.Contracts;

namespace Sunfish.UICore.Tests;

/// <summary>
/// Verifies the ISunfishCssProvider interface shape.
/// These tests protect against accidental method deletions during the migration.
/// </summary>
public class CssProviderContractTests
{
    private static readonly Type ContractType = typeof(ISunfishCssProvider);

    [Fact]
    public void ISunfishCssProvider_HasExpectedMethodCount()
    {
        // IMariloCssProvider had 88 methods. Count the Sunfish equivalent.
        var methods = ContractType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.True(methods.Length >= 80, $"Expected at least 80 methods, got {methods.Length}");
    }

    [Fact]
    public void ISunfishCssProvider_HasButtonClass()
    {
        var method = ContractType.GetMethod("ButtonClass", [typeof(ButtonVariant), typeof(ButtonSize), typeof(bool), typeof(bool)]);
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void ISunfishCssProvider_HasDataGridClass()
    {
        var method = ContractType.GetMethod("DataGridClass");
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void ISunfishCssProvider_HasAllocationSchedulerMethods()
    {
        var method = ContractType.GetMethod("AllocationSchedulerClass");
        Assert.NotNull(method);
    }

    [Fact]
    public void ISunfishCssProvider_HasResizableContainerHandleClass()
    {
        // Verifies that MariloResizeEdges was correctly renamed to ResizeEdges
        var method = ContractType.GetMethod("ResizableContainerHandleClass");
        Assert.NotNull(method);
        var param = method.GetParameters().FirstOrDefault(p => p.Name == "edge");
        Assert.NotNull(param);
        Assert.Equal(typeof(ResizeEdges), param.ParameterType);
    }
}
```

Note: you need the following using directives. Add them to the top of the file (they come from `Sunfish.Foundation.Enums`):
```csharp
using Sunfish.Foundation.Enums;
```

- [ ] **Step 2: Create the tests project**

Create `packages/ui-core/tests/tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Sunfish.UICore.csproj" />
  </ItemGroup>
</Project>
```

`GenerateDocumentationFile` is `false` to suppress CS1591 warnings inherited from `Directory.Build.props`. This is the same fix used in the foundation tests project.

- [ ] **Step 3: Run the test — verify it fails to compile (ISunfishCssProvider doesn't exist yet)**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-core/tests/tests.csproj 2>&1 | tail -5
```

Expected: build error referencing `ISunfishCssProvider` not found.

- [ ] **Step 4: Create ISunfishCssProvider.cs**

Copy `C:/Projects/Marilo/src/Marilo.Core/Contracts/IMariloCssProvider.cs` to `packages/ui-core/Contracts/ISunfishCssProvider.cs`, then apply these transformations:

```bash
cp "C:/Projects/Marilo/src/Marilo.Core/Contracts/IMariloCssProvider.cs" \
   "C:/Projects/Sunfish/packages/ui-core/Contracts/ISunfishCssProvider.cs"

# 1. Update using directive
sed -i 's/using Marilo\.Core\.Enums;/using Sunfish.Foundation.Enums;/' \
    "C:/Projects/Sunfish/packages/ui-core/Contracts/ISunfishCssProvider.cs"

# 2. Update namespace
sed -i 's/namespace Marilo\.Core\.Contracts;/namespace Sunfish.UICore.Contracts;/' \
    "C:/Projects/Sunfish/packages/ui-core/Contracts/ISunfishCssProvider.cs"

# 3. Rename interface
sed -i 's/IMariloCssProvider/ISunfishCssProvider/g' \
    "C:/Projects/Sunfish/packages/ui-core/Contracts/ISunfishCssProvider.cs"

# 4. Rename the Marilo-prefixed enum type (only one: MariloResizeEdges → ResizeEdges)
sed -i 's/MariloResizeEdges/ResizeEdges/g' \
    "C:/Projects/Sunfish/packages/ui-core/Contracts/ISunfishCssProvider.cs"

# 5. Update doc comment to say Sunfish instead of Marilo
sed -i 's/Marilo component states/Sunfish component states/g' \
    "C:/Projects/Sunfish/packages/ui-core/Contracts/ISunfishCssProvider.cs"
sed -i 's/keeping component logic design-system-agnostic/keeping Sunfish component logic design-system-agnostic/g' \
    "C:/Projects/Sunfish/packages/ui-core/Contracts/ISunfishCssProvider.cs"
```

Then prepend the TODO comment at the top of the interface body (after the `{`):

Edit `packages/ui-core/Contracts/ISunfishCssProvider.cs` — add this line after the opening `{` of the interface:

```csharp
    // TODO(phase-2-followup): split by category into ISunfishButtonCssProvider, ISunfishFormCssProvider, etc.
```

- [ ] **Step 5: Build ui-core and run tests**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-core/Sunfish.UICore.csproj
dotnet test packages/ui-core/tests/tests.csproj
```

Expected: build clean, all `CssProviderContractTests` pass (4 tests).

- [ ] **Step 6: Commit**

```bash
cd "C:/Projects/Sunfish"
but stage "packages/ui-core/Sunfish.UICore.csproj" "feat/migration-phase2-ui-core"
but stage "packages/ui-core/Contracts/ISunfishCssProvider.cs" "feat/migration-phase2-ui-core"
but stage "packages/ui-core/tests/tests.csproj" "feat/migration-phase2-ui-core"
but stage "packages/ui-core/tests/CssProviderContractTests.cs" "feat/migration-phase2-ui-core"
but commit -m "feat(ui-core): add ISunfishCssProvider contract with passing tests" "feat/migration-phase2-ui-core"
```

---

## Task 3: Migrate ISunfishIconProvider

**Files:**
- Create: `packages/ui-core/Contracts/ISunfishIconProvider.cs`
- Source: `C:/Projects/Marilo/src/Marilo.Core/Contracts/IMariloIconProvider.cs`

The Marilo version returns `MarkupString` from `Microsoft.AspNetCore.Components`. Since `ui-core` must be framework-agnostic, we change the return type to `string`. The Blazor adapter casts: `new MarkupString(provider.GetIcon(name, size))`.

- [ ] **Step 1: Write the test first**

Create `packages/ui-core/tests/IconProviderContractTests.cs`:

```csharp
using System.Reflection;
using Sunfish.UICore.Contracts;

namespace Sunfish.UICore.Tests;

public class IconProviderContractTests
{
    private static readonly Type ContractType = typeof(ISunfishIconProvider);

    [Fact]
    public void ISunfishIconProvider_GetIcon_ReturnsString_NotMarkupString()
    {
        var method = ContractType.GetMethod("GetIcon");
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void ISunfishIconProvider_HasGetIconSpriteUrl()
    {
        var method = ContractType.GetMethod("GetIconSpriteUrl");
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void ISunfishIconProvider_HasNoBlazorDependency()
    {
        // Verify the assembly that defines ISunfishIconProvider does not reference
        // Microsoft.AspNetCore.Components
        var assembly = ContractType.Assembly;
        var refs = assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name?.Contains("AspNetCore.Components") == true);
    }
}
```

- [ ] **Step 2: Run the test — verify it fails (ISunfishIconProvider doesn't exist yet)**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-core/tests/tests.csproj 2>&1 | grep -E "error|Error" | head -5
```

Expected: compile error.

- [ ] **Step 3: Create ISunfishIconProvider.cs**

Create `packages/ui-core/Contracts/ISunfishIconProvider.cs` with this content (do NOT copy from Marilo — write it fresh to avoid `MarkupString` contamination):

```csharp
using Sunfish.Foundation.Enums;

namespace Sunfish.UICore.Contracts;

/// <summary>
/// Provides design-system-specific icon rendering. Implementations resolve icon
/// names to the appropriate markup (inline SVG, sprite reference, icon font, etc.).
/// </summary>
/// <remarks>
/// Returns pre-rendered HTML strings. Callers must treat the returned value as raw
/// markup and must not HTML-encode it. In Blazor, wrap as
/// <c>new MarkupString(provider.GetIcon(name, size))</c>.
/// </remarks>
public interface ISunfishIconProvider
{
    /// <summary>
    /// Returns the pre-rendered HTML markup for the requested icon at the given size.
    /// </summary>
    /// <param name="name">The logical icon name (e.g., "home", "settings").</param>
    /// <param name="size">The desired icon size.</param>
    /// <returns>A pre-rendered HTML string containing the icon markup. Do not HTML-encode.</returns>
    string GetIcon(string name, IconSize size = IconSize.Medium);

    /// <summary>
    /// Gets the URL of the SVG sprite sheet used by this icon provider.
    /// </summary>
    /// <returns>A relative or absolute URL pointing to the sprite SVG file.
    /// Returns <see cref="string.Empty"/> for <see cref="IconRenderMode.CssClass"/> providers.</returns>
    string GetIconSpriteUrl();

    /// <summary>
    /// Gets the render mode used by this icon provider (sprite, inline SVG, or CSS class).
    /// </summary>
    IconRenderMode RenderMode => IconRenderMode.SvgSprite;

    /// <summary>
    /// Gets the display name of the icon library (e.g., "Tabler", "Lucide", "Custom").
    /// Used for diagnostics and tooling.
    /// </summary>
    string LibraryName => "Unknown";
}
```

- [ ] **Step 4: Build and run tests**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-core/Sunfish.UICore.csproj
dotnet test packages/ui-core/tests/tests.csproj
```

Expected: build clean. 3 icon tests + 4 CSS tests = 7 tests passing.

- [ ] **Step 5: Commit**

```bash
cd "C:/Projects/Sunfish"
but stage "packages/ui-core/Contracts/ISunfishIconProvider.cs" "feat/migration-phase2-ui-core"
but stage "packages/ui-core/tests/IconProviderContractTests.cs" "feat/migration-phase2-ui-core"
but commit -m "feat(ui-core): add ISunfishIconProvider contract; returns string not MarkupString" "feat/migration-phase2-ui-core"
```

---

## Task 4: Migrate ISunfishJsInterop

**Files:**
- Create: `packages/ui-core/Contracts/ISunfishJsInterop.cs`
- Source: `C:/Projects/Marilo/src/Marilo.Core/Contracts/IMariloJsInterop.cs`

The Marilo version has two Blazor-specific members:
- `GetElementBoundsAsync(ElementReference element)` — replace `ElementReference` with `string elementId`
- `ObserveScrollAsync(ElementReference element, DotNetObjectReference<object> callback)` — drop entirely; will be in Blazor adapter (Phase 3)

`BoundingBox` record migrates into this file unchanged (it has no Blazor types).

- [ ] **Step 1: Write the test first**

Create `packages/ui-core/tests/JsInteropContractTests.cs`:

```csharp
using System.Reflection;
using Sunfish.UICore.Contracts;

namespace Sunfish.UICore.Tests;

public class JsInteropContractTests
{
    private static readonly Type ContractType = typeof(ISunfishJsInterop);

    [Fact]
    public void ISunfishJsInterop_HasInitializeAsync()
    {
        var method = ContractType.GetMethod("InitializeAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void ISunfishJsInterop_HasShowModalAsync()
    {
        var method = ContractType.GetMethod("ShowModalAsync", [typeof(string)]);
        Assert.NotNull(method);
    }

    [Fact]
    public void ISunfishJsInterop_GetElementBoundsAsync_TakesStringNotElementReference()
    {
        var method = ContractType.GetMethod("GetElementBoundsAsync", [typeof(string)]);
        Assert.NotNull(method);
    }

    [Fact]
    public void ISunfishJsInterop_HasNoBlazorDependency()
    {
        var assembly = ContractType.Assembly;
        var refs = assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name?.Contains("AspNetCore.Components") == true);
        Assert.DoesNotContain(refs, r => r.Name?.Contains("JSInterop") == true);
    }

    [Fact]
    public void BoundingBox_IsFrameworkAgnosticRecord()
    {
        var type = typeof(BoundingBox);
        Assert.True(type.IsValueType || (type.IsClass && type.GetProperties().Length > 0));
        Assert.Null(type.GetProperty("ElementReference")); // No Blazor contamination
    }
}
```

- [ ] **Step 2: Run the test — verify it fails**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-core/tests/tests.csproj 2>&1 | grep -E "error|Error" | head -5
```

Expected: compile error.

- [ ] **Step 3: Create ISunfishJsInterop.cs**

Create `packages/ui-core/Contracts/ISunfishJsInterop.cs`:

```csharp
namespace Sunfish.UICore.Contracts;

/// <summary>
/// Framework-agnostic contract for Sunfish JavaScript interop operations.
/// </summary>
/// <remarks>
/// The Blazor adapter implementation extends this with Blazor-specific overloads
/// (e.g., <c>GetElementBoundsAsync(ElementReference)</c> and
/// <c>ObserveScrollAsync(ElementReference, DotNetObjectReference)</c>)
/// that are not part of this contract because they reference Blazor types.
/// </remarks>
public interface ISunfishJsInterop : IAsyncDisposable
{
    /// <summary>Initializes the JS module. Must be called before other methods.</summary>
    ValueTask InitializeAsync();

    /// <summary>Shows the modal with the given HTML element ID.</summary>
    /// <returns><c>true</c> if the modal was shown; <c>false</c> if not found.</returns>
    ValueTask<bool> ShowModalAsync(string modalId);

    /// <summary>Hides the modal with the given HTML element ID.</summary>
    ValueTask HideModalAsync(string modalId);

    /// <summary>
    /// Returns the bounding box of the element with the given HTML element ID.
    /// </summary>
    /// <param name="elementId">The HTML id attribute value of the target element.</param>
    ValueTask<BoundingBox> GetElementBoundsAsync(string elementId);
}

/// <summary>
/// The bounding box of a DOM element as reported by <c>getBoundingClientRect()</c>.
/// </summary>
public record BoundingBox(double X, double Y, double Width, double Height);
```

- [ ] **Step 4: Build and run tests**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-core/Sunfish.UICore.csproj
dotnet test packages/ui-core/tests/tests.csproj
```

Expected: build clean. 4 CSS + 3 icon + 5 JsInterop = 12 tests passing.

- [ ] **Step 5: Commit**

```bash
cd "C:/Projects/Sunfish"
but stage "packages/ui-core/Contracts/ISunfishJsInterop.cs" "feat/migration-phase2-ui-core"
but stage "packages/ui-core/tests/JsInteropContractTests.cs" "feat/migration-phase2-ui-core"
but commit -m "feat(ui-core): add ISunfishJsInterop contract; string elementId, no Blazor types" "feat/migration-phase2-ui-core"
```

---

## Task 5: Register ui-core in solution file and final build check

**Files:**
- Modify: `Sunfish.slnx`

- [ ] **Step 1: Add ui-core projects to Sunfish.slnx**

Edit `Sunfish.slnx`:

```xml
<Solution>
  <Project Path="packages/foundation/Sunfish.Foundation.csproj" />
  <Project Path="packages/foundation/tests/tests.csproj" />
  <Project Path="packages/ui-core/Sunfish.UICore.csproj" />
  <Project Path="packages/ui-core/tests/tests.csproj" />
</Solution>
```

- [ ] **Step 2: Full solution build and test**

```bash
cd "C:/Projects/Sunfish"
dotnet build Sunfish.slnx
dotnet test Sunfish.slnx --no-build
```

Expected:
- Build: 0 errors, 0 warnings
- Tests: foundation 3 passing + ui-core 12 passing = 15 tests total

- [ ] **Step 3: Commit**

```bash
cd "C:/Projects/Sunfish"
but stage "Sunfish.slnx" "feat/migration-phase2-ui-core"
but commit -m "chore: add ui-core projects to Sunfish.slnx; 15 tests passing" "feat/migration-phase2-ui-core"
```

- [ ] **Step 4: Push Phase 2 branch**

```bash
cd "C:/Projects/Sunfish"
but push "feat/migration-phase2-ui-core"
```

---

## Self-Review Checklist

- [ ] `ISunfishCssProvider` has no Blazor types (`RenderFragment`, `ComponentBase`, `ElementReference`, `IJSRuntime`)
- [ ] `ISunfishIconProvider.GetIcon()` returns `string`, not `MarkupString`  
- [ ] `ISunfishJsInterop.GetElementBoundsAsync()` takes `string elementId`, not `ElementReference`
- [ ] `ObserveScrollAsync` is NOT in `ISunfishJsInterop` (it's Blazor-specific; deferred to Phase 3)
- [ ] `MariloResizeEdges` does not appear anywhere in `packages/ui-core/`
- [ ] `dotnet build Sunfish.slnx` = 0 warnings, 0 errors
- [ ] `dotnet test Sunfish.slnx` = 15 tests passing (3 foundation + 12 ui-core)
- [ ] All test files have `GenerateDocumentationFile=false` to avoid CS1591 warnings
