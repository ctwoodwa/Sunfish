# Phase 2: UI Core Contracts — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create `packages/ui-core` with three framework-agnostic contract interfaces — `ISunfishCssProvider`, `ISunfishIconProvider`, and `ISunfishJsInterop` — migrated from Marilo.Core and stripped of all Blazor-specific types.

**Architecture:** `ui-core` sits between `foundation` and `ui-adapters-blazor` in the dependency chain. It must have no Blazor references (`MarkupString`, `ElementReference`, `DotNetObjectReference`, `IJSRuntime` are all forbidden per CONTRIBUTING.md). It depends only on `Sunfish.Foundation` (for enums used in CSS method signatures). The three interfaces are the entire public surface of this package — no implementations live here.

**Tech Stack:** .NET 10, C# 13, xUnit 2.9.x, `Nullable enable`, `TreatWarningsAsErrors` (inherited from `Directory.Build.props`; not re-declared in per-project csproj).

---

## Scope

**In scope (this plan):** the three UI Core interfaces, their tests, and `Sunfish.slnx` registration.

**Out of scope — tracked separately in the broader Marilo → Sunfish rebrand:**

- `packages/foundation` (Phase 1; presumed complete — this plan treats it as a precondition)
- `packages/ui-adapters-blazor` (Phase 3; where `MarkupString`/`ElementReference`/`DotNetObjectReference` re-enter as Blazor-specific overloads, and where `ObserveScrollAsync` lands)
- `apps/docs` content rebrand (`Marilo` → `Sunfish` in prose, code snippets, and screenshots)
- `apps/kitchen-sink` demo rebrand (component names, using directives, CSS classes)
- NuGet `PackageId`, `Authors`, `RepositoryUrl` sweep across all csproj files
- License headers and copyright strings
- CI/CD workflow references (workflow names, badge URLs, publish targets)
- Namespace sweep in consumer code beyond this package

A running checklist of those items should live at `docs/superpowers/plans/marilo-to-sunfish-rebrand-tracker.md` (create separately). **This plan must not modify files outside `packages/ui-core/` and `Sunfish.slnx`.**

---

## Prerequisites & Environment

**Paths.** Commands below use two environment variables so the plan is portable across machines:

```bash
# Set once per shell session. Adjust to your layout.
export SUNFISH="$HOME/Sunfish"                    # or C:/Projects/Sunfish on Windows
export MARILO="$HOME/Marilo"                      # or C:/Projects/Marilo on Windows
```

All `cd "$SUNFISH"`, `cp "$MARILO/..."`, and `sed -i` commands assume GNU sed. On macOS, replace `sed -i` with `sed -i ''`. On Windows, use WSL or Git Bash with MSYS GNU sed.

**GitButler virtual branch.** This plan commits through the GitButler CLI (`but`). The virtual branch name `feat/migration-phase2-ui-core` is a **GitButler vbranch** layered on top of the real git branch `claude/review-sunfish-phase2-ui-AGtOf`. Before starting:

```bash
cd "$SUNFISH"
git checkout claude/review-sunfish-phase2-ui-AGtOf
but status                                        # confirm the vbranch is applied
# If the vbranch doesn't exist yet:
#   but branch new feat/migration-phase2-ui-core
```

Each `but stage`, `but commit`, and `but push` below takes `feat/migration-phase2-ui-core` as the trailing vbranch argument — this is GitButler syntax, not a typo for `git`.

**Phase 1 preconditions (fail fast if missing).** This plan depends on Phase 1 having migrated these types into `Sunfish.Foundation.Enums`:

- `ButtonVariant`, `ButtonSize` (used in `CssProviderContractTests`)
- `ResizeEdges` (renamed from `MariloResizeEdges`; used in `ResizableContainerHandleClass` signature)
- `IconSize`, `IconRenderMode` (used in `ISunfishIconProvider`)

Run this check before Task 1:

```bash
cd "$SUNFISH"
dotnet build packages/foundation/Sunfish.Foundation.csproj
grep -rE "enum (ButtonVariant|ButtonSize|ResizeEdges|IconSize|IconRenderMode)" \
    packages/foundation/ || { echo "FAIL: missing Phase 1 enums"; exit 1; }
```

If any are missing, stop and escalate — do not add them here (they belong in `foundation`).

---

## Key Decisions

**D-CSS:** `IMariloCssProvider` → `ISunfishCssProvider`. All method signatures are pure strings/enums — no Blazor types. One rename fix: `MariloResizeEdges` → `ResizeEdges` (that's how the enum landed in `Sunfish.Foundation.Enums` after Phase 1). Add `// TODO(phase-2-followup): split by category` at the top per the master plan decision D3.

**D-ICON:** `IMariloIconProvider` returns `MarkupString` (a Blazor type). Change return type to `string`. The contract doc explains that the string is pre-rendered HTML intended to be injected as raw markup — callers must not HTML-encode it. The Blazor adapter layer wraps it as `new MarkupString(provider.GetIcon(name, size))`.

**D-ICON-DIM:** Two default interface members (`RenderMode`, `LibraryName`) are added that were not in the Marilo original. Rationale: both are diagnostic/tooling concerns that every provider should be able to answer, but that most providers can answer with a sensible default. Default-interface-implementation keeps existing Marilo provider classes source-compatible after the rename without forcing every implementer to add boilerplate. These defaults live on the contract (not an extension class) because they describe the provider itself.

**D-JS:** `IMariloJsInterop` uses `ElementReference` (Blazor) and `DotNetObjectReference<object>` (Blazor JSInterop). For the framework-agnostic contract: replace `ElementReference element` with `string elementId`, and drop `ObserveScrollAsync` entirely (no framework-agnostic substitute — will be added to the Blazor adapter in Phase 3). `BoundingBox` record moves into the same file.

**D-JS-DISP:** `ISunfishJsInterop` extends `IAsyncDisposable`. Rationale: Marilo's version did, and removing it would be a silent behavioral regression — adapter implementations load a JS module that needs teardown on host shutdown. Keeping the constraint on the contract forces every adapter to implement disposal; moving it to the adapter base class would permit a leaky concrete implementation to slip through.

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
    CssProviderContractTests.cs      ← method-count + signature spot-checks (6 tests)
    IconProviderContractTests.cs     ← interface shape (2 tests)
    JsInteropContractTests.cs       ← interface shape, ObserveScrollAsync absence, BoundingBox (6 tests)
    AssemblyContractTests.cs         ← shared assembly-level Blazor-dependency check (2 tests)
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

- [ ] **Step 2: Verify the project file is valid**

```bash
cd "$SUNFISH"
dotnet restore packages/ui-core/Sunfish.UICore.csproj
```

Expected: restore completes with 0 errors. The project has no source files yet, which is fine — `dotnet build` would warn CS2008 (no source files), but `restore` does not. The `Contracts/` directory is created implicitly by the first file write in Task 2.

---

## Task 2: Migrate ISunfishCssProvider

**Files:**
- Create: `packages/ui-core/Contracts/ISunfishCssProvider.cs`
- Source: `$MARILO/src/Marilo.Core/Contracts/IMariloCssProvider.cs`

- [ ] **Step 1: Write the test first**

Create `packages/ui-core/tests/CssProviderContractTests.cs`:

```csharp
using System.Linq;
using System.Reflection;
using Sunfish.Foundation.Enums;
using Sunfish.UICore.Contracts;

namespace Sunfish.UICore.Tests;

/// <summary>
/// Verifies the ISunfishCssProvider interface shape.
/// These tests protect against accidental method deletions during the migration
/// and against accidental re-introduction of Blazor types.
/// </summary>
public class CssProviderContractTests
{
    private static readonly Type ContractType = typeof(ISunfishCssProvider);

    // Exact expected count. IMariloCssProvider had 88 methods at the time of migration.
    // If a future task intentionally splits or extends the interface, update this literal
    // in the same commit so an accidental deletion cannot hide under a loose bound.
    private const int ExpectedMethodCount = 88;

    [Fact]
    public void ISunfishCssProvider_HasExpectedMethodCount()
    {
        var methods = ContractType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(ExpectedMethodCount, methods.Length);
    }

    [Fact]
    public void ISunfishCssProvider_HasButtonClass()
    {
        var method = ContractType.GetMethod(
            "ButtonClass",
            [typeof(ButtonVariant), typeof(ButtonSize), typeof(bool), typeof(bool)]);
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method!.ReturnType);
    }

    [Fact]
    public void ISunfishCssProvider_HasDataGridClass()
    {
        var method = ContractType.GetMethod("DataGridClass");
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method!.ReturnType);
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
        // Verifies that MariloResizeEdges was correctly renamed to ResizeEdges.
        var method = ContractType.GetMethod("ResizableContainerHandleClass");
        Assert.NotNull(method);
        var param = method!.GetParameters().FirstOrDefault(p => p.Name == "edge");
        Assert.NotNull(param);
        Assert.Equal(typeof(ResizeEdges), param!.ParameterType);
    }

    [Fact]
    public void ISunfishCssProvider_AllMethodsReturnStrings()
    {
        // The CSS provider contract is pure string output. Any non-string return is a smell
        // that a Blazor type (e.g., MarkupString) or a complex DTO leaked in.
        var bad = ContractType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.ReturnType != typeof(string))
            .Select(m => m.Name)
            .ToArray();
        Assert.Empty(bad);
    }
}
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
cd "$SUNFISH"
dotnet build packages/ui-core/tests/tests.csproj 2>&1 | tail -5
```

Expected: build error referencing `ISunfishCssProvider` not found.

- [ ] **Step 4: Create ISunfishCssProvider.cs**

Copy the Marilo source, then apply transformations. If `$MARILO` points to a repo that has already been rebranded or archived, these paths may need adjustment.

```bash
set -euo pipefail

SRC="$MARILO/src/Marilo.Core/Contracts/IMariloCssProvider.cs"
DST="$SUNFISH/packages/ui-core/Contracts/ISunfishCssProvider.cs"

[ -f "$SRC" ] || { echo "FAIL: source not found: $SRC"; exit 1; }

cp "$SRC" "$DST"

# 1. Update using directive
sed -i 's/using Marilo\.Core\.Enums;/using Sunfish.Foundation.Enums;/' "$DST"

# 2. Update namespace
sed -i 's/namespace Marilo\.Core\.Contracts;/namespace Sunfish.UICore.Contracts;/' "$DST"

# 3. Rename interface
sed -i 's/IMariloCssProvider/ISunfishCssProvider/g' "$DST"

# 4. Rename the Marilo-prefixed enum type (only one: MariloResizeEdges → ResizeEdges)
sed -i 's/MariloResizeEdges/ResizeEdges/g' "$DST"

# 5. Update doc comment to say Sunfish instead of Marilo
sed -i 's/Marilo component states/Sunfish component states/g' "$DST"
sed -i 's/keeping component logic design-system-agnostic/keeping Sunfish component logic design-system-agnostic/g' "$DST"
```

Then prepend the TODO comment at the top of the interface body (after the `{`):

Edit `packages/ui-core/Contracts/ISunfishCssProvider.cs` — add this line after the opening `{` of the interface:

```csharp
    // TODO(phase-2-followup): split by category into ISunfishButtonCssProvider, ISunfishFormCssProvider, etc.
```

- [ ] **Step 5: Blazor-contamination grep gate**

The sed surgery above only rewrites what we anticipated. This gate catches anything we missed.

```bash
cd "$SUNFISH"

# These tokens must not appear anywhere in the migrated file.
FORBIDDEN='Marilo|MarkupString|ElementReference|IJSRuntime|DotNetObjectReference|Microsoft\.AspNetCore\.Components|Microsoft\.JSInterop|RenderFragment|ComponentBase'

if grep -E "$FORBIDDEN" packages/ui-core/Contracts/ISunfishCssProvider.cs; then
  echo "FAIL: forbidden token found in ISunfishCssProvider.cs"
  exit 1
fi
echo "OK: no forbidden tokens"
```

- [ ] **Step 6: Build ui-core and run tests**

```bash
cd "$SUNFISH"
dotnet build packages/ui-core/Sunfish.UICore.csproj
dotnet test packages/ui-core/tests/tests.csproj
```

Expected: build clean, all `CssProviderContractTests` pass (6 tests).

- [ ] **Step 7: Commit via GitButler**

Reminder: the trailing argument is the **GitButler virtual branch name**, not a git branch. See "Prerequisites & Environment."

```bash
cd "$SUNFISH"
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
- Source: `$MARILO/src/Marilo.Core/Contracts/IMariloIconProvider.cs`

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
        Assert.Equal(typeof(string), method!.ReturnType);
    }

    [Fact]
    public void ISunfishIconProvider_HasGetIconSpriteUrl()
    {
        var method = ContractType.GetMethod("GetIconSpriteUrl");
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method!.ReturnType);
    }
}
```

The "no Blazor dependency" test is intentionally deferred to a single shared test class
(`AssemblyContractTests.cs`, added in Task 4) to avoid running the same assertion three times
across files.

- [ ] **Step 2: Run the test — verify it fails (ISunfishIconProvider doesn't exist yet)**

```bash
cd "$SUNFISH"
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

- [ ] **Step 4: Blazor-contamination grep gate**

```bash
cd "$SUNFISH"
FORBIDDEN='Marilo|MarkupString|ElementReference|IJSRuntime|DotNetObjectReference|Microsoft\.AspNetCore\.Components|Microsoft\.JSInterop|RenderFragment|ComponentBase'
if grep -E "$FORBIDDEN" packages/ui-core/Contracts/ISunfishIconProvider.cs; then
  echo "FAIL: forbidden token found in ISunfishIconProvider.cs"
  exit 1
fi
echo "OK: no forbidden tokens"
```

- [ ] **Step 5: Build and run tests**

```bash
cd "$SUNFISH"
dotnet build packages/ui-core/Sunfish.UICore.csproj
dotnet test packages/ui-core/tests/tests.csproj
```

Expected: build clean. 2 icon tests + 6 CSS tests = 8 tests passing.

- [ ] **Step 6: Commit via GitButler**

```bash
cd "$SUNFISH"
but stage "packages/ui-core/Contracts/ISunfishIconProvider.cs" "feat/migration-phase2-ui-core"
but stage "packages/ui-core/tests/IconProviderContractTests.cs" "feat/migration-phase2-ui-core"
but commit -m "feat(ui-core): add ISunfishIconProvider contract; returns string not MarkupString" "feat/migration-phase2-ui-core"
```

---

## Task 4: Migrate ISunfishJsInterop

**Files:**
- Create: `packages/ui-core/Contracts/ISunfishJsInterop.cs`
- Source: `$MARILO/src/Marilo.Core/Contracts/IMariloJsInterop.cs`

The Marilo version has two Blazor-specific members:
- `GetElementBoundsAsync(ElementReference element)` — replace `ElementReference` with `string elementId`
- `ObserveScrollAsync(ElementReference element, DotNetObjectReference<object> callback)` — drop entirely; will be in Blazor adapter (Phase 3)

`BoundingBox` record migrates into this file unchanged (it has no Blazor types).

- [ ] **Step 1: Write the test first**

Create `packages/ui-core/tests/JsInteropContractTests.cs`:

```csharp
using System.Linq;
using System.Reflection;
using Sunfish.UICore.Contracts;

namespace Sunfish.UICore.Tests;

public class JsInteropContractTests
{
    private static readonly Type ContractType = typeof(ISunfishJsInterop);

    [Fact]
    public void ISunfishJsInterop_HasInitializeAsync()
    {
        Assert.NotNull(ContractType.GetMethod("InitializeAsync"));
    }

    [Fact]
    public void ISunfishJsInterop_HasShowModalAsync()
    {
        Assert.NotNull(ContractType.GetMethod("ShowModalAsync", [typeof(string)]));
    }

    [Fact]
    public void ISunfishJsInterop_GetElementBoundsAsync_TakesStringNotElementReference()
    {
        // Positive: a string overload exists on the framework-agnostic contract.
        Assert.NotNull(ContractType.GetMethod("GetElementBoundsAsync", [typeof(string)]));
    }

    [Fact]
    public void ISunfishJsInterop_DoesNotDeclare_ObserveScrollAsync()
    {
        // ObserveScrollAsync is Blazor-specific (DotNetObjectReference) and was deliberately
        // dropped from the framework-agnostic contract. It will be re-added in the Blazor
        // adapter in Phase 3.
        Assert.Null(ContractType.GetMethod("ObserveScrollAsync"));
    }

    [Fact]
    public void ISunfishJsInterop_ExtendsIAsyncDisposable()
    {
        // See Key Decision D-JS-DISP. Disposal must remain on the contract so adapter
        // implementers cannot silently regress JS-module teardown.
        Assert.Contains(typeof(IAsyncDisposable), ContractType.GetInterfaces());
    }

    [Fact]
    public void BoundingBox_HasNoBlazorPropertyTypes()
    {
        // Stronger than checking property existence: assert no property's type is
        // sourced from a Blazor assembly.
        var props = typeof(BoundingBox).GetProperties();
        Assert.NotEmpty(props);
        var bad = props
            .Where(p => p.PropertyType.Assembly.GetName().Name is string n
                        && (n.Contains("AspNetCore.Components") || n.Contains("JSInterop")))
            .Select(p => $"{p.Name}: {p.PropertyType.FullName}")
            .ToArray();
        Assert.Empty(bad);

        // Also: expected shape is (double, double, double, double).
        Assert.Equal(4, props.Length);
        Assert.All(props, p => Assert.Equal(typeof(double), p.PropertyType));
    }
}
```

Also create a single shared assembly-level Blazor-dependency check at
`packages/ui-core/tests/AssemblyContractTests.cs` (replaces the per-interface duplicates):

```csharp
using System.Linq;
using Sunfish.UICore.Contracts;

namespace Sunfish.UICore.Tests;

public class AssemblyContractTests
{
    [Fact]
    public void UICoreAssembly_HasNoBlazorOrJsInteropReferences()
    {
        var assembly = typeof(ISunfishCssProvider).Assembly;
        var refs = assembly.GetReferencedAssemblies()
            .Select(r => r.Name)
            .Where(n => n is not null)
            .ToArray();
        Assert.DoesNotContain(refs, n => n!.Contains("AspNetCore.Components"));
        Assert.DoesNotContain(refs, n => n!.Contains("Microsoft.JSInterop"));
    }

    [Fact]
    public void NoContractMethod_UsesBlazorTypes()
    {
        // Sweep all method signatures on all three contracts. Any parameter or return
        // type whose assembly name implicates Blazor or JSInterop is a regression.
        var contracts = new[]
        {
            typeof(ISunfishCssProvider),
            typeof(ISunfishIconProvider),
            typeof(ISunfishJsInterop),
        };

        bool IsForbidden(Type t)
        {
            var name = t.Assembly.GetName().Name ?? string.Empty;
            return name.Contains("AspNetCore.Components") || name.Contains("Microsoft.JSInterop");
        }

        var bad = contracts
            .SelectMany(c => c.GetMethods())
            .Where(m => IsForbidden(m.ReturnType)
                        || m.GetParameters().Any(p => IsForbidden(p.ParameterType)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToArray();
        Assert.Empty(bad);
    }
}
```

- [ ] **Step 2: Run the test — verify it fails**

```bash
cd "$SUNFISH"
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

- [ ] **Step 4: Blazor-contamination grep gate**

```bash
cd "$SUNFISH"
FORBIDDEN='Marilo|MarkupString|ElementReference|IJSRuntime|DotNetObjectReference|Microsoft\.AspNetCore\.Components|Microsoft\.JSInterop|RenderFragment|ComponentBase'
if grep -E "$FORBIDDEN" packages/ui-core/Contracts/ISunfishJsInterop.cs; then
  echo "FAIL: forbidden token found in ISunfishJsInterop.cs"
  exit 1
fi
echo "OK: no forbidden tokens"
```

- [ ] **Step 5: Build and run tests**

```bash
cd "$SUNFISH"
dotnet build packages/ui-core/Sunfish.UICore.csproj
dotnet test packages/ui-core/tests/tests.csproj
```

Expected counts: 6 CSS + 2 icon + 6 JsInterop + 2 assembly-level = **16 tests passing**.

- [ ] **Step 6: Commit via GitButler**

```bash
cd "$SUNFISH"
but stage "packages/ui-core/Contracts/ISunfishJsInterop.cs" "feat/migration-phase2-ui-core"
but stage "packages/ui-core/tests/JsInteropContractTests.cs" "feat/migration-phase2-ui-core"
but stage "packages/ui-core/tests/AssemblyContractTests.cs" "feat/migration-phase2-ui-core"
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
cd "$SUNFISH"
dotnet build Sunfish.slnx
dotnet test Sunfish.slnx --no-build
```

Expected:
- Build: 0 errors, 0 warnings
- Tests: foundation 3 passing + ui-core 16 passing = **19 tests total**

If the foundation test count differs from 3, update both this expected total and the
matching line in the Self-Review Checklist below in the same commit.

- [ ] **Step 3: Repo-wide Blazor-contamination sweep (final gate)**

A belt-and-braces check that nothing snuck in across the package as a whole.

```bash
cd "$SUNFISH"
FORBIDDEN='Marilo|MarkupString|ElementReference|IJSRuntime|DotNetObjectReference|Microsoft\.AspNetCore\.Components|Microsoft\.JSInterop|RenderFragment|ComponentBase'
matches=$(grep -rE "$FORBIDDEN" packages/ui-core/Contracts/ || true)
if [ -n "$matches" ]; then
  echo "FAIL: forbidden tokens in ui-core/Contracts:"
  echo "$matches"
  exit 1
fi
echo "OK: ui-core/Contracts is Blazor-free"
```

- [ ] **Step 4: Commit via GitButler**

```bash
cd "$SUNFISH"
but stage "Sunfish.slnx" "feat/migration-phase2-ui-core"
but commit -m "chore: add ui-core projects to Sunfish.slnx; 19 tests passing" "feat/migration-phase2-ui-core"
```

- [ ] **Step 5: Push the Phase 2 vbranch**

`but push <vbranch>` pushes the underlying refs through GitButler. The first push for a new
vbranch is what creates the remote branch on GitHub.

```bash
cd "$SUNFISH"
but push "feat/migration-phase2-ui-core"
```

Note on transient network errors: if push fails for network reasons, retry up to 4 times with
exponential backoff (2s, 4s, 8s, 16s). Do not retry on auth or non-fast-forward failures —
investigate the underlying cause first.

---

## Self-Review Checklist

**Contract shape**

- [ ] `ISunfishCssProvider` has no Blazor types (`RenderFragment`, `ComponentBase`, `ElementReference`, `IJSRuntime`, `MarkupString`, `DotNetObjectReference`)
- [ ] `ISunfishCssProvider` retains the same method count as `IMariloCssProvider` (88 — exact match enforced by `HasExpectedMethodCount`)
- [ ] `ISunfishIconProvider.GetIcon()` returns `string`, not `MarkupString`
- [ ] `ISunfishIconProvider` declares `RenderMode` and `LibraryName` default interface members (Decision D-ICON-DIM)
- [ ] `ISunfishJsInterop.GetElementBoundsAsync()` takes `string elementId`, not `ElementReference`
- [ ] `ISunfishJsInterop` extends `IAsyncDisposable` (Decision D-JS-DISP)
- [ ] `ObserveScrollAsync` is NOT in `ISunfishJsInterop` (Blazor-specific; deferred to Phase 3)
- [ ] `BoundingBox` is a `(double, double, double, double)` record with no Blazor-typed property

**Rebrand hygiene**

- [ ] `MariloResizeEdges` does not appear anywhere in `packages/ui-core/`
- [ ] `Marilo` (case-sensitive) does not appear anywhere in `packages/ui-core/Contracts/` (run the final sweep in Task 5 Step 3)
- [ ] All `using` directives import from `Sunfish.*`, none from `Marilo.*`
- [ ] All XML doc comments reference Sunfish, not Marilo

**Build & test gates**

- [ ] `dotnet build Sunfish.slnx` = 0 warnings, 0 errors
- [ ] `dotnet test Sunfish.slnx` total tests pass — expected 19 (3 foundation + 16 ui-core); verify foundation count and update if it has drifted
- [ ] All test files have `GenerateDocumentationFile=false` to avoid CS1591 warnings
- [ ] The shared `AssemblyContractTests.NoContractMethod_UsesBlazorTypes` test exists and passes

**Process**

- [ ] All commits authored on the GitButler vbranch `feat/migration-phase2-ui-core` (not directly on `claude/review-sunfish-phase2-ui-AGtOf`)
- [ ] `but push feat/migration-phase2-ui-core` succeeded
- [ ] No files outside `packages/ui-core/` and `Sunfish.slnx` were modified by this plan (run `git diff --name-only origin/main...HEAD` to confirm)
- [ ] Marilo source repo at `$MARILO` was read-only — no edits made there

---

## Open questions / parking lot

These are not blockers for Phase 2 but should be raised in the broader rebrand tracker:

- **NuGet authority**: who owns the `Sunfish.UICore` PackageId on nuget.org? Confirm before first publish.
- **Telerik compat shim** (`packages/compat-telerik`): does any Marilo CSS class name leak into the contract method names (e.g., `KendoGrid*`)? If yes, decide rename policy in Phase 3.
- **Docs site cross-references**: `apps/docs` will need URL rewrites from `marilo/...` to `sunfish/...`. Track redirects.
- **Consumer migration guide**: a "Marilo → Sunfish 1.0 upgrade" doc is needed before the first public release; collect breaking changes per package as they happen.
