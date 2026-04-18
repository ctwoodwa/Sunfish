# Phase 6: compat-telerik — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create `packages/compat-telerik/Sunfish.Compat.Telerik.csproj` — a Telerik-API-shaped compatibility surface over Sunfish components. Consumers migrating from Telerik UI for Blazor replace `using Telerik.Blazor.Components` with `using Sunfish.Compat.Telerik` and keep most of their markup intact. Phase 6 ships the package framework plus 12 high-value wrappers; coverage expansion happens in follow-up phases.

**Architecture:** compat-telerik sits **above** `ui-adapters-blazor` in the dependency chain. It has no dependency on Telerik's NuGet package — it only mirrors Telerik's *public type shape* (component names, parameter names, and enum members). Every wrapper is a thin Razor component that forwards to a Sunfish component and performs parameter mapping. compat-telerik is a **migration off-ramp**, not a source of truth; ui-core + ui-adapters-blazor own the canonical contracts.

**Tech Stack:** .NET 10, C# 13, Blazor (Razor Class Library), bUnit 2.7.x, xUnit 2.9.x

---

## Key Decisions

**D-NAMESPACE:** Root namespace `Sunfish.Compat.Telerik`. Components live directly in the root namespace (NOT a nested `Components` namespace) to match Telerik's flat `Telerik.Blazor.Components.TelerikButton` style. Migration is mechanical: swap `using Telerik.Blazor.Components` → `using Sunfish.Compat.Telerik` and `<TelerikButton>` markup resolves unchanged.

**D-PARAMETER-MAPPING:** Telerik uses `string` theme constants (`ThemeConstants.Button.ThemeColor.Primary` = `"primary"`); Sunfish uses typed enums (`ButtonVariant.Primary`). Each wrapper owns private `Map<Param>` helpers that convert string → enum. Unknown strings fall back to the Sunfish default and log a warning (NOT throw — see D-UNSUPPORTED-PARAMS for the distinction). Re-published constants live under `Sunfish.Compat.Telerik.ThemeConstants` with literal values matching Telerik exactly.

**D-UNSUPPORTED-PARAMS:** When Telerik exposes a parameter/enum member Sunfish cannot express (e.g., `ButtonType.Reset`), the wrapper throws `NotSupportedException` **with a migration-hint message** formatted as:

> "Telerik parameter `{Param}={Value}` has no Sunfish equivalent. Migration hint: {hint}. See docs/compat-telerik-mapping.md."

NEVER silently swallow unsupported values — consumers must know their code is not portable. Silent "log + drop" is reserved for cosmetic-only parameters with no functional impact (e.g., `TabIndex` forwarded via `AdditionalAttributes`).

**D-NUGET-DEPENDENCY:** compat-telerik **MUST NOT** reference `Telerik.UI.for.Blazor` or any Telerik package. It re-declares the few Telerik public types it needs (enum shapes, `ThemeConstants` string values) inside `Sunfish.Compat.Telerik.*`. The package must not force a Telerik license on consumers. Source-compatibility means: consumer code that compiled against `Telerik.Blazor.Components.TelerikButton` compiles against our wrapper after swapping the `using` directive.

**D-POLICY-GATE:** compat-telerik is policy-gated. Every PR requires sign-off, enforced via: (1) `packages/compat-telerik/CODEOWNERS` listing reviewer(s); (2) a header comment in the csproj pointing at policy; (3) `packages/compat-telerik/POLICY.md` documenting the rules — new wrappers require an ICM `sunfish-api-change` or `sunfish-feature-change` ticket, and divergences must land in `docs/compat-telerik-mapping.md` in the same PR. Phase 6 ships 12 wrappers; follow-ups go one-per-PR.

**D-DIVERGENCE-LOG:** `docs/compat-telerik-mapping.md` is the audit trail and is treated as public API. Each wrapper has a section listing: Telerik target, Sunfish target, parameter table (mapped/dropped/throws), behavioral differences. Changing an entry (e.g., promoting a param from "mapped" to "throws") is a breaking change for consumers.

---

## Scope

### In Scope (Phase 6 wrappers)

Exactly these 12 wrappers, chosen for coverage of the most common Telerik migration scenarios:

1. **TelerikButton** → `SunfishButton` (Buttons)
2. **TelerikIcon** → `SunfishIcon` (Utility)
3. **TelerikCheckBox** → `SunfishCheckbox` (Forms)
4. **TelerikTextBox** → `SunfishTextBox` (Forms)
5. **TelerikDropDownList\<T\>** → `SunfishDropDownList<T>` (Forms)
6. **TelerikComboBox\<T\>** → `SunfishComboBox<T>` (Forms)
7. **TelerikDatePicker** → `SunfishDatePicker` (Forms)
8. **TelerikForm** → `SunfishForm` (Forms)
9. **TelerikGrid\<T\>** → `SunfishDataGrid<T>` (DataGrid)
10. **TelerikWindow** → `SunfishWindow` (Overlays)
11. **TelerikTooltip** → `SunfishTooltip` (Overlays)
12. **TelerikNotification** → `SunfishSnackbarHost` (Feedback)

### Out of Scope (deferred to follow-up phases)

- TelerikTreeView, TelerikTreeList, TelerikScheduler, TelerikGantt, TelerikChart family, TelerikEditor, TelerikUpload, TelerikWizard, TelerikStepper, TelerikAnimationContainer, TelerikPopup
- Telerik `EventArgs` types (e.g., `GridReadEventArgs`) — consumers using events must migrate signatures; we do not shim event-arg shapes in Phase 6
- Full `ThemeConstants` coverage — only the string constants needed by the 12 wrappers ship in Phase 6
- Roslyn analyzer that detects `using Telerik.Blazor.Components` — tracked as separate ICM ticket

---

## File Structure (after Phase 6)

```
packages/compat-telerik/
  Sunfish.Compat.Telerik.csproj              ← new; Razor SDK; no Telerik NuGet
  POLICY.md                                  ← policy gate documentation
  CODEOWNERS                                 ← reviewer list
  _Imports.razor                             ← using Sunfish.Compat.Telerik, Sunfish.Components.Blazor.Components.*
  TelerikButton.razor                        ← wrapper
  TelerikIcon.razor
  TelerikCheckBox.razor
  TelerikTextBox.razor
  TelerikDropDownList.razor                  ← generic component, T parameter
  TelerikComboBox.razor
  TelerikDatePicker.razor
  TelerikForm.razor
  TelerikGrid.razor
  TelerikWindow.razor
  TelerikTooltip.razor
  TelerikNotification.razor
  Enums/
    ButtonType.cs                            ← re-declares Telerik.Blazor.Enums.ButtonType
    WindowState.cs
    FilterMode.cs
    SortMode.cs
    (etc — only what Phase 6 wrappers need)
  ThemeConstants/
    ThemeConstants.cs                        ← string constants: Button.ThemeColor.*, FillMode.*, Size.*
  Internal/
    ParameterMappers.cs                      ← shared MapThemeColor, MapSize, MapFillMode helpers
    UnsupportedParam.cs                      ← ThrowNotSupported(param, value, hint) helper
  tests/
    tests.csproj                             ← bUnit + xUnit
    TelerikButtonTests.cs
    TelerikIconTests.cs
    TelerikCheckBoxTests.cs
    TelerikTextBoxTests.cs
    TelerikDropDownListTests.cs
    TelerikComboBoxTests.cs
    TelerikDatePickerTests.cs
    TelerikFormTests.cs
    TelerikGridTests.cs
    TelerikWindowTests.cs
    TelerikTooltipTests.cs
    TelerikNotificationTests.cs
    ParameterMappersTests.cs

docs/
  compat-telerik-mapping.md                  ← audit trail; updated per wrapper

Sunfish.slnx                                 ← register compat-telerik + tests
```

---

## Parameter Mapping Reference — TelerikButton

This table is the canonical example. Every wrapper produces one of these tables in `docs/compat-telerik-mapping.md`.

| Telerik parameter | Type (Telerik) | Sunfish parameter | Type (Sunfish) | Mapping |
|---|---|---|---|---|
| `ThemeColor` | `string` | `Variant` | `ButtonVariant` | `"primary"→Primary`, `"secondary"→Secondary`, `"tertiary"→Tertiary`, `"info"→Info`, `"success"→Success`, `"warning"→Warning`, `"error"→Error`, `"dark"→Dark`, `"light"→Light`, `"inverse"→Inverse`, `null/""→Primary` |
| `Size` | `string` | `Size` | `ButtonSize` | `"sm"→Small`, `"md"→Medium`, `"lg"→Large`, `null→Medium` |
| `FillMode` | `string` | `FillMode` | `FillMode` | `"solid"→Solid`, `"flat"→Flat`, `"outline"→Outline`, `"link"→Link`, `"clear"→Clear`, `null→Solid` |
| `Rounded` | `string` | `Rounded` | `RoundedMode` | `"small"→Small`, `"medium"→Medium`, `"large"→Large`, `"full"→Full`, `"none"→None`, `null→Medium` |
| `Enabled` | `bool` | `Enabled` | `bool` | Passthrough |
| `ButtonType` | `ButtonType` (compat enum) | `ButtonType` | `ButtonType` (Sunfish) | `Button→Button`, `Submit→Submit`, `Reset→` **throws** `NotSupportedException` (Sunfish has no Reset; migration hint: "Use an `OnClick` handler that resets form state explicitly.") |
| `Form` | `string?` | `Form` | `string?` | Passthrough |
| `OnClick` | `EventCallback<MouseEventArgs>` | `OnClick` | `EventCallback<MouseEventArgs>` | Passthrough |
| `Icon` | `object?` (Telerik's Icon type) | `Icon` | `RenderFragment?` | If `object` is a `RenderFragment`, passthrough. If it's a Telerik `ISvgIcon`-shaped value, call `SvgIconAdapter.ToRenderFragment(value)`. If null, null. |
| `ChildContent` | `RenderFragment?` | `ChildContent` | `RenderFragment?` | Passthrough |
| `Class` | `string?` | — | — | Forwarded via `AdditionalAttributes["class"]` |
| `TabIndex` | `int?` | — | — | Forwarded via `AdditionalAttributes["tabindex"]` (silent forward — cosmetic-only) |

Every wrapper ships with a table like this in `docs/compat-telerik-mapping.md`.

---

## Fully-Worked Example: TelerikButton.razor

This is the shape every wrapper follows.

```razor
@namespace Sunfish.Compat.Telerik
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.Extensions.Logging
@using Sunfish.Components.Blazor.Components.Buttons
@using Sunfish.UICore.Enums
@using Sunfish.Compat.Telerik.Internal
@using CompatEnums = Sunfish.Compat.Telerik.Enums
@inject ILogger<TelerikButton> Logger

<SunfishButton Variant="@_variant" Size="@_size" FillMode="@_fillMode" Rounded="@_rounded"
               Enabled="@Enabled" ButtonType="@_buttonType" Form="@Form"
               OnClick="@OnClick" Icon="@_iconFragment"
               @attributes="AdditionalAttributes">
    @ChildContent
</SunfishButton>

@code {
    // --- Telerik-shaped public parameters ---
    [Parameter] public string? ThemeColor { get; set; }
    [Parameter] public string? Size { get; set; }
    [Parameter] public string? FillMode { get; set; }
    [Parameter] public string? Rounded { get; set; }
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>ButtonType.Reset throws NotSupportedException — Sunfish has no Reset semantics.</summary>
    [Parameter] public CompatEnums.ButtonType ButtonType { get; set; } = CompatEnums.ButtonType.Button;

    [Parameter] public string? Form { get; set; }
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>Accepts a RenderFragment directly, or a Telerik ISvgIcon-shaped object.</summary>
    [Parameter] public object? Icon { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // --- Mapped Sunfish values (computed in OnParametersSet) ---
    private ButtonVariant _variant;
    private ButtonSize _size;
    private Sunfish.UICore.Enums.FillMode _fillMode;
    private RoundedMode _rounded;
    private Sunfish.UICore.Enums.ButtonType _buttonType;
    private RenderFragment? _iconFragment;

    protected override void OnParametersSet()
    {
        _variant = MapVariant(ThemeColor);
        _size = MapSize(Size);
        _fillMode = MapFillMode(FillMode);
        _rounded = MapRounded(Rounded);
        _buttonType = MapButtonType(ButtonType);
        _iconFragment = SvgIconAdapter.ToRenderFragment(Icon);
    }

    // --- Private mapping helpers ---
    private ButtonVariant MapVariant(string? s) => s switch
    {
        null or "" or "primary" => ButtonVariant.Primary,
        "secondary" => ButtonVariant.Secondary, "tertiary" => ButtonVariant.Tertiary,
        "info" => ButtonVariant.Info, "success" => ButtonVariant.Success,
        "warning" => ButtonVariant.Warning, "error" => ButtonVariant.Error,
        "dark" => ButtonVariant.Dark, "light" => ButtonVariant.Light,
        "inverse" => ButtonVariant.Inverse,
        _ => LogAndFallback(s, ButtonVariant.Primary, nameof(ThemeColor))
    };

    private ButtonSize MapSize(string? s) => s switch
    {
        null or "" or "md" => ButtonSize.Medium,
        "sm" => ButtonSize.Small, "lg" => ButtonSize.Large,
        _ => LogAndFallback(s, ButtonSize.Medium, nameof(Size))
    };

    private Sunfish.UICore.Enums.FillMode MapFillMode(string? s) => s switch
    {
        null or "" or "solid" => Sunfish.UICore.Enums.FillMode.Solid,
        "flat" => Sunfish.UICore.Enums.FillMode.Flat,
        "outline" => Sunfish.UICore.Enums.FillMode.Outline,
        "link" => Sunfish.UICore.Enums.FillMode.Link,
        "clear" => Sunfish.UICore.Enums.FillMode.Clear,
        _ => LogAndFallback(s, Sunfish.UICore.Enums.FillMode.Solid, nameof(FillMode))
    };

    private RoundedMode MapRounded(string? s) => s switch
    {
        null or "" or "medium" => RoundedMode.Medium,
        "small" => RoundedMode.Small, "large" => RoundedMode.Large,
        "full" => RoundedMode.Full, "none" => RoundedMode.None,
        _ => LogAndFallback(s, RoundedMode.Medium, nameof(Rounded))
    };

    private Sunfish.UICore.Enums.ButtonType MapButtonType(CompatEnums.ButtonType t) => t switch
    {
        CompatEnums.ButtonType.Button => Sunfish.UICore.Enums.ButtonType.Button,
        CompatEnums.ButtonType.Submit => Sunfish.UICore.Enums.ButtonType.Submit,
        CompatEnums.ButtonType.Reset => throw UnsupportedParam.Throw(
            nameof(ButtonType), "Reset",
            "Sunfish has no Reset button type. Use an OnClick handler that resets form state explicitly."),
        _ => throw UnsupportedParam.Throw(nameof(ButtonType), t.ToString(), "Unknown ButtonType value.")
    };

    private T LogAndFallback<T>(string? received, T fallback, string paramName)
    {
        Logger.LogWarning(
            "compat-telerik: Unrecognized value '{Value}' for parameter '{Param}' on TelerikButton. " +
            "Falling back to '{Fallback}'. See docs/compat-telerik-mapping.md.",
            received, paramName, fallback);
        return fallback;
    }
}
```

Key points:
- Telerik-shaped parameters are public; Sunfish-shaped values are `private readonly` fields populated in `OnParametersSet`.
- Mapping helpers are `private`, never `public` — the mapping is an implementation detail.
- Unknown values log + fall back; explicitly-unsupported values throw via `UnsupportedParam.Throw`.
- `@attributes="AdditionalAttributes"` preserves arbitrary Telerik attrs (`class`, `style`, `tabindex`, etc.).

---

## Task 1: Create compat-telerik project and policy files

**Files:** `Sunfish.Compat.Telerik.csproj`, `POLICY.md`, `CODEOWNERS`, `_Imports.razor` (all under `packages/compat-telerik/`).

- [ ] **Step 1: Create the branch**

```bash
cd "C:/Projects/Sunfish"
git checkout -b feat/migration-phase6-compat-telerik
```

- [ ] **Step 2: Create Sunfish.Compat.Telerik.csproj**

```xml
<!-- packages/compat-telerik/Sunfish.Compat.Telerik.csproj -->
<!--
    POLICY-GATED PACKAGE.
    All changes require explicit reviewer sign-off per packages/compat-telerik/POLICY.md.
    This package MUST NOT reference Telerik.UI.for.Blazor or any Telerik NuGet.
-->
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Compat.Telerik</PackageId>
    <Description>Telerik-API-shaped compatibility surface over Sunfish components. Migration off-ramp for Telerik UI for Blazor consumers.</Description>
    <PackageTags>blazor;compat;telerik;migration;sunfish</PackageTags>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Sunfish.Compat.Telerik</RootNamespace>
    <InternalsVisibleTo Include="Sunfish.Compat.Telerik.Tests" />
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="tests/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\ui-core\Sunfish.UICore.csproj" />
    <ProjectReference Include="..\ui-adapters-blazor\Sunfish.Components.Blazor.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create POLICY.md** covering: package purpose (migration off-ramp), D-POLICY-GATE rules, mapping-doc requirement, no-Telerik-NuGet invariant, link to ICM `sunfish-api-change` variant.

- [ ] **Step 4: Create CODEOWNERS**

```
# packages/compat-telerik/CODEOWNERS
# Policy-gated package. All changes require sign-off from a listed owner.
* @sunfish-maintainers
```

(Replace placeholder handle with real GitHub handles before merge.)

- [ ] **Step 5: Create _Imports.razor**

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using Sunfish.Compat.Telerik
@using Sunfish.Compat.Telerik.Enums
@using Sunfish.Compat.Telerik.ThemeConstants
@using Sunfish.Compat.Telerik.Internal
@using Sunfish.Components.Blazor
@using Sunfish.Components.Blazor.Components.Buttons
@using Sunfish.Components.Blazor.Components.Forms
@using Sunfish.Components.Blazor.Components.DataGrid
@using Sunfish.Components.Blazor.Components.Overlays
@using Sunfish.Components.Blazor.Components.Feedback
@using Sunfish.Components.Blazor.Components.Utility
@using Sunfish.UICore.Enums
```

- [ ] **Step 6: Restore and verify baseline build**

```bash
cd "C:/Projects/Sunfish"
dotnet restore packages/compat-telerik/Sunfish.Compat.Telerik.csproj
dotnet build packages/compat-telerik/Sunfish.Compat.Telerik.csproj
```

Expected: 0 errors. No source files yet is fine.

- [ ] **Step 7: Stage and commit**

```bash
but stage "packages/compat-telerik/Sunfish.Compat.Telerik.csproj" \
           "packages/compat-telerik/POLICY.md" \
           "packages/compat-telerik/CODEOWNERS" \
           "packages/compat-telerik/_Imports.razor" \
           "feat/migration-phase6-compat-telerik"
but commit -m "feat(compat-telerik): scaffold policy-gated package; no Telerik NuGet dependency" "feat/migration-phase6-compat-telerik"
```

---

## Task 2: Create shared infrastructure (enums, theme constants, mappers)

**Files:** `Enums/ButtonType.cs`, `Enums/WindowState.cs`, `Enums/FilterMode.cs`, `Enums/SortMode.cs`, `ThemeConstants/ThemeConstants.cs`, `Internal/UnsupportedParam.cs`, `Internal/SvgIconAdapter.cs` (all under `packages/compat-telerik/`).

- [ ] **Step 1: Enums — mirror Telerik's public enum shapes**

Each enum file mirrors the Telerik member names exactly (NOT the semantics — a `Reset` member exists on `ButtonType` but will throw in the mapper). Example:

```csharp
// packages/compat-telerik/Enums/ButtonType.cs
namespace Sunfish.Compat.Telerik.Enums;

/// <summary>
/// Mirrors Telerik.Blazor.Enums.ButtonType. NOTE: Reset throws NotSupportedException
/// when used with TelerikButton — Sunfish has no Reset semantics.
/// </summary>
public enum ButtonType
{
    Button = 0,
    Submit = 1,
    Reset  = 2
}
```

Only ship the members actually needed by the 12 Phase 6 wrappers.

- [ ] **Step 2: ThemeConstants — re-publish Telerik string constants**

Create `packages/compat-telerik/ThemeConstants/ThemeConstants.cs` with nested static classes mirroring Telerik's `Telerik.Blazor.ThemeConstants`. Values MUST match Telerik's literal strings exactly (`"primary"`, `"sm"`, `"solid"`, etc.). Phase 6 ships only the sections needed by the 12 wrappers: `Size`, `FillMode`, `Rounded`, `Button.ThemeColor`, `Button.Icon`, `Grid.Selectable.Mode`, `Window.ThemeColor`, `Notification.ThemeColor`. Each mapping helper in a wrapper references these constants for its accepted strings. Additional sections are added by future wrapper PRs.

- [ ] **Step 3: UnsupportedParam helper**

```csharp
// packages/compat-telerik/Internal/UnsupportedParam.cs
namespace Sunfish.Compat.Telerik.Internal;

internal static class UnsupportedParam
{
    public static NotSupportedException Throw(string paramName, string value, string migrationHint)
        => new NotSupportedException(
            $"Telerik parameter `{paramName}={value}` has no Sunfish equivalent. " +
            $"Migration hint: {migrationHint} " +
            $"See docs/compat-telerik-mapping.md.");
}
```

- [ ] **Step 4: SvgIconAdapter**

```csharp
// packages/compat-telerik/Internal/SvgIconAdapter.cs
using Microsoft.AspNetCore.Components;

namespace Sunfish.Compat.Telerik.Internal;

internal static class SvgIconAdapter
{
    /// <summary>
    /// Converts Telerik-shaped icon values (RenderFragment or ISvgIcon-shaped object)
    /// into Sunfish RenderFragment. Returns null for null input.
    /// </summary>
    public static RenderFragment? ToRenderFragment(object? icon) => icon switch
    {
        null => null,
        RenderFragment rf => rf,
        // Future: detect ISvgIcon-shaped types via duck typing
        _ => (RenderFragment)(builder => builder.AddContent(0, icon.ToString()))
    };
}
```

- [ ] **Step 5: Build to verify infrastructure compiles**

```bash
dotnet build packages/compat-telerik/Sunfish.Compat.Telerik.csproj
```

Expected: 0 errors.

- [ ] **Step 6: Stage and commit**

```bash
but stage "packages/compat-telerik/Enums/" \
           "packages/compat-telerik/ThemeConstants/" \
           "packages/compat-telerik/Internal/" \
           "feat/migration-phase6-compat-telerik"
but commit -m "feat(compat-telerik): add enum/theme-constant shims and parameter-mapping helpers" "feat/migration-phase6-compat-telerik"
```

---

## Task 3: Create mapping document

**Files:** `docs/compat-telerik-mapping.md`

- [ ] **Step 1:** Create the file. Seed with header (purpose = audit trail; treated as public API) + the `TelerikButton` row (full table from "Parameter Mapping Reference" section above). Leave `TBD — see Task N` placeholders for the other 11 wrappers; each wrapper task fills in its row. Each wrapper section includes: Telerik target, Sunfish target, parameter table (mapped/dropped/throws), known divergences, migration hints.

- [ ] **Step 2: Stage and commit**

```bash
but stage "docs/compat-telerik-mapping.md" "feat/migration-phase6-compat-telerik"
but commit -m "docs(compat-telerik): add mapping/divergence audit document" "feat/migration-phase6-compat-telerik"
```

---

## Task 4: Implement Buttons category (TelerikButton, TelerikIcon)

**Files:** 2 `.razor` files + 2 test `.cs` files. (Test project scaffolding — see Task 8 — must exist before this task can build.)

- [ ] **Step 1: Create TelerikButton.razor** using the full example from "Fully-Worked Example" above.

- [ ] **Step 2: Create TelerikIcon.razor** — smaller surface; `Icon` parameter (object/string) forwards to `SunfishIcon.Name` or `SunfishIcon.Icon` RenderFragment depending on input type.

- [ ] **Step 3: Write smoke tests (3 minimum per wrapper):**
  1. Renders without throwing using default parameters
  2. Forwards `AdditionalAttributes` (e.g., `class="x"`) to the rendered root element
  3. At least one parameter-mapping test per mapped parameter: input Telerik value → expected Sunfish output

Example skeleton:

```csharp
public class TelerikButtonTests : TestContext
{
    [Theory]
    [InlineData("primary", "sf-button--primary")]
    [InlineData("secondary", "sf-button--secondary")]
    public void ThemeColor_MapsToVariantClass(string themeColor, string expectedClass)
    {
        var cut = RenderComponent<TelerikButton>(p => p
            .Add(x => x.ThemeColor, themeColor).AddChildContent("x"));
        cut.Find("button").ClassList.Should().Contain(expectedClass);
    }

    [Fact]
    public void ButtonType_Reset_Throws_WithMigrationHint()
    {
        var act = () => RenderComponent<TelerikButton>(p => p
            .Add(x => x.ButtonType, Sunfish.Compat.Telerik.Enums.ButtonType.Reset));
        act.Should().Throw<NotSupportedException>()
           .WithMessage("*ButtonType=Reset*Migration hint*");
    }
}
```

- [ ] **Step 4: Update docs/compat-telerik-mapping.md** with TelerikButton and TelerikIcon rows.

- [ ] **Step 5: Build + test, then stage and commit.**

```bash
dotnet build Sunfish.slnx && dotnet test packages/compat-telerik/tests/tests.csproj
but stage "packages/compat-telerik/TelerikButton.razor" \
           "packages/compat-telerik/TelerikIcon.razor" \
           "packages/compat-telerik/tests/TelerikButtonTests.cs" \
           "packages/compat-telerik/tests/TelerikIconTests.cs" \
           "docs/compat-telerik-mapping.md" \
           "feat/migration-phase6-compat-telerik"
but commit -m "feat(compat-telerik): add TelerikButton and TelerikIcon wrappers" "feat/migration-phase6-compat-telerik"
```

---

## Task 5: Implement Forms category (6 wrappers)

**Files:** 6 wrapper `.razor` files + 6 test `.cs` files in `packages/compat-telerik/` and `packages/compat-telerik/tests/`.

Each wrapper follows the TelerikButton pattern: Telerik-shaped parameters public; `OnParametersSet` populates private mapped fields; unsupported values throw via `UnsupportedParam.Throw`; unrecognized strings `LogAndFallback`. Each wrapper ships with 3+ bUnit tests (render, attribute forwarding, parameter mapping) and one row in `docs/compat-telerik-mapping.md`.

- [ ] **Step 1: TelerikCheckBox.razor** → `SunfishCheckbox`. Public params: `Value` (bool/bool?), `ValueChanged`, `Enabled`, `Id`, `TabIndex`. Two-way binding via `@bind-Value`.

- [ ] **Step 2: TelerikTextBox.razor** → `SunfishTextBox`. Public params: `Value`, `ValueChanged`, `Label`, `Placeholder`, `Enabled`, `DebounceDelay` (maps to Sunfish debounce if present, else log + drop), `Width` (via inline `style`).

- [ ] **Step 3: TelerikDropDownList.razor** → generic `SunfishDropDownList<TItem, TValue>`. Public params: `Data`, `TextField`, `ValueField` (Telerik uses `string` property names — convert to `Func<TItem, object>` via a small `Internal.FieldAccessorFactory` helper using expression-tree compilation), `Value`, `ValueChanged`, `DefaultText`.

- [ ] **Step 4: TelerikComboBox.razor** → generic `SunfishComboBox<TItem, TValue>`. Same shape as DropDownList + `Filterable`, `AllowCustom`.

- [ ] **Step 5: TelerikDatePicker.razor** → `SunfishDatePicker`. Public params: `Value` (DateTime?), `ValueChanged`, `Format`, `Min`, `Max`. Calendar-customization params (`View`, `BottomView`) → log + drop (cosmetic).

- [ ] **Step 6: TelerikForm.razor** → `SunfishForm`. Public params: `Model`, `EditContext`, `OnSubmit`, `OnValidSubmit`, `OnInvalidSubmit`, `Orientation`. Validation child components (`TelerikValidationMessage`, `FormValidation`) deferred to future phase — log a warning if encountered in `ChildContent`.

- [ ] **Step 7: Write smoke tests, update mapping doc, build + test, then commit.**

```bash
but stage "packages/compat-telerik/Telerik*.razor" \
           "packages/compat-telerik/tests/" \
           "packages/compat-telerik/Internal/FieldAccessorFactory.cs" \
           "docs/compat-telerik-mapping.md" \
           "feat/migration-phase6-compat-telerik"
but commit -m "feat(compat-telerik): add 6 Forms wrappers (CheckBox, TextBox, DropDownList, ComboBox, DatePicker, Form)" "feat/migration-phase6-compat-telerik"
```

---

## Task 6: Implement DataGrid wrapper (TelerikGrid<T>)

**Files:** `packages/compat-telerik/TelerikGrid.razor`, `packages/compat-telerik/tests/TelerikGridTests.cs`.

TelerikGrid has the largest surface — separate task and commit.

- [ ] **Step 1: Create TelerikGrid.razor** as generic wrapper over `SunfishDataGrid<TItem>`. Params to map:
  - `Data` (IEnumerable<TItem>) — passthrough
  - `Pageable`, `PageSize` — map to paging props
  - `Sortable`, `SortMode` (compat enum) — map to Sunfish sort mode
  - `Filterable`, `FilterMode` (compat enum) — map to Sunfish filter mode
  - `Selectable`, `SelectionMode` (compat enum), `SelectedItems`, `SelectedItemsChanged` — passthrough or map
  - `OnRead` — Telerik's manual-data callback. If Sunfish has equivalent, map; else throw with migration hint "Use `Data` with in-memory or server-bound collection."
  - `ChildContent` — Telerik's `<GridColumn Field="X">` child markup is the **largest documented divergence**. Phase 6 does NOT ship a `<TelerikGridColumn>` shim; the mapping doc tells consumers to migrate column markup manually to `<SunfishDataGridColumn>`. A future PR can add the shim if demand warrants.

- [ ] **Step 2: Write tests (render, attribute forwarding, `OnRead`-only throws with hint, each mapped enum), update mapping doc (GridColumn divergence gets its own section), build + test, commit.**

```bash
but stage "packages/compat-telerik/TelerikGrid.razor" \
           "packages/compat-telerik/tests/TelerikGridTests.cs" \
           "docs/compat-telerik-mapping.md" \
           "feat/migration-phase6-compat-telerik"
but commit -m "feat(compat-telerik): add TelerikGrid<T> wrapper; document GridColumn divergence" "feat/migration-phase6-compat-telerik"
```

---

## Task 7: Implement Overlays + Feedback (3 wrappers)

**Files:** 3 wrapper `.razor` files + 3 test `.cs` files.

- [ ] **Step 1: TelerikWindow.razor** → `SunfishWindow`. Params: `Visible`, `VisibleChanged`, `Title`, `Width`, `Height`, `Modal`, `State` (compat `WindowState` — `Maximized`/`Minimized` throw if Sunfish lacks equivalent), `OnClose`.

- [ ] **Step 2: TelerikTooltip.razor** → `SunfishTooltip`. Params: `TargetSelector`, `Position`, `ShowOn`, `Content`. Map `Position` (Telerik) → `Placement` (Sunfish) if names diverge.

- [ ] **Step 3: TelerikNotification.razor** → `SunfishSnackbarHost`. Telerik exposes imperative `Show`/`Hide` methods on `@ref`; mirror these as method shims that forward to Sunfish's API (service-injected or `@ref`-based depending on Sunfish's shape). If only declarative, document as a breaking migration step.

- [ ] **Step 4: Write tests (3 per wrapper), update mapping doc, build + test, commit.**

```bash
but stage "packages/compat-telerik/TelerikWindow.razor" \
           "packages/compat-telerik/TelerikTooltip.razor" \
           "packages/compat-telerik/TelerikNotification.razor" \
           "packages/compat-telerik/tests/" \
           "docs/compat-telerik-mapping.md" \
           "feat/migration-phase6-compat-telerik"
but commit -m "feat(compat-telerik): add Window, Tooltip, Notification wrappers" "feat/migration-phase6-compat-telerik"
```

---

## Task 8: Create tests project

> **Execution note:** Do this task immediately after Task 2 in practice, since Task 4 onwards writes tests against it.

**Files:** `packages/compat-telerik/tests/tests.csproj`.

- [ ] **Step 1: Create tests.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <RootNamespace>Sunfish.Compat.Telerik.Tests</RootNamespace>
    <AssemblyName>Sunfish.Compat.Telerik.Tests</AssemblyName>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="bunit" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Sunfish.Compat.Telerik.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Verify discovery** — `dotnet test packages/compat-telerik/tests/tests.csproj --list-tests`. Commit folded into Task 4.

---

## Task 9: Register compat-telerik in Sunfish.slnx

- [ ] **Step 1: Edit Sunfish.slnx** — add a new `<Folder Name="/compat-telerik/">` containing `Sunfish.Compat.Telerik.csproj` and its `tests/tests.csproj`, alongside the existing foundation/ui-core/ui-adapters-blazor folders.

- [ ] **Step 2: Full solution build and test**

```bash
cd "C:/Projects/Sunfish"
dotnet build Sunfish.slnx
dotnet test Sunfish.slnx --no-build
```

Expected: 0 errors, 0 warnings. Test count = prior total + ~36 compat-telerik tests (3 per wrapper × 12 wrappers).

- [ ] **Step 3: Verify no Telerik NuGet sneaked in**

```bash
dotnet list packages/compat-telerik/Sunfish.Compat.Telerik.csproj package | grep -i telerik
# Expected: no output
```

- [ ] **Step 4: Stage and commit**

```bash
but stage "Sunfish.slnx" "feat/migration-phase6-compat-telerik"
but commit -m "chore: register compat-telerik in Sunfish.slnx; full solution green" "feat/migration-phase6-compat-telerik"
```

---

## Task 10: Push branch

- [ ] **Step 1: Push**

```bash
git push -u origin feat/migration-phase6-compat-telerik
```

- [ ] **Step 2: Open PR with policy-gate callout**

PR description should explicitly call out:
- "This PR establishes the compat-telerik package — policy-gated per CLAUDE.md D-TELERIK."
- Confirms no Telerik NuGet dependency
- Links to `docs/compat-telerik-mapping.md`
- Requests review from CODEOWNERS

---

## Self-Review Checklist

- [ ] `Sunfish.Compat.Telerik.csproj` uses `Microsoft.NET.Sdk.Razor`
- [ ] `FrameworkReference Include="Microsoft.AspNetCore.App"` present
- [ ] **No `<PackageReference>` mentions `Telerik.*`** (verify via `dotnet list package`)
- [ ] `POLICY.md` exists and documents the reviewer gate
- [ ] `CODEOWNERS` exists with at least one reviewer entry
- [ ] All 12 wrappers present: Button, Icon, CheckBox, TextBox, DropDownList, ComboBox, DatePicker, Form, Grid, Window, Tooltip, Notification
- [ ] Components live at the package root namespace (`Sunfish.Compat.Telerik.TelerikButton`), not nested
- [ ] Every `Map<Param>` helper has a corresponding row in `docs/compat-telerik-mapping.md`
- [ ] Every `NotSupportedException` throw uses `UnsupportedParam.Throw(...)` for consistent message shape
- [ ] Every wrapper has at least 3 bUnit tests (render, attribute forwarding, parameter mapping)
- [ ] Every wrapper forwards `AdditionalAttributes` via `@attributes` on the inner Sunfish component
- [ ] Unsupported-parameter tests assert the migration-hint message contains both the param name and `docs/compat-telerik-mapping.md`
- [ ] `docs/compat-telerik-mapping.md` has one section per wrapper with at minimum: Telerik target, Sunfish target, parameter table, divergences
- [ ] `Sunfish.slnx` registers compat-telerik + its tests
- [ ] `dotnet build Sunfish.slnx` = 0 warnings, 0 errors
- [ ] `dotnet test Sunfish.slnx` = all prior tests pass + ~36 new compat-telerik tests pass
- [ ] Branch is `feat/migration-phase6-compat-telerik`
- [ ] Commits are grouped by category (project scaffold, infrastructure, mapping doc, Buttons, Forms, DataGrid, Overlays+Feedback, slnx)

---

## Notes for Future Phases

- **Coverage expansion:** Follow-up PRs under the policy gate. Candidates: TelerikTreeView, TelerikTreeList, TelerikScheduler, TelerikEditor, TelerikWizard, TelerikTabStrip, TelerikChart family.
- **GridColumn shim:** Demand-driven follow-up. Telerik columns register via `CascadingValue`, requires careful handling.
- **EventArgs shims:** Future `Sunfish.Compat.Telerik.EventArgs.*` types with implicit conversion to Sunfish event args.
- **Analyzer:** Separate ICM `sunfish-scaffolding` ticket — a Roslyn analyzer that flags `using Telerik.Blazor.Components` and suggests the compat replacement.
- **Marilo finding:** Audit of `C:/Projects/Marilo/src` confirmed **no pre-existing Telerik-shaped wrappers**. Telerik references are inspirational doc-comments only (`MariloMultiSelect` mirrors Telerik's settings-child-component pattern; `MariloIcon` references Telerik's `ISvgIcon` parameter shape). compat-telerik is genuinely greenfield.
