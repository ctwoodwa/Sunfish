# Phase 3d: Icon packages (Tabler + legacy) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate Marilo's two icon packages into Sunfish as independent, packable RCLs: `Sunfish.Icons.Tabler` (primary, 5,039 Tabler icons via SVG sprite) and `Sunfish.Icons.Legacy` (362-icon curated set, obsolete on day one).

**Architecture:** Icon packages implement `ISunfishIconProvider` (from ui-core) and ship sprite assets via the RCL static-asset pipeline. Dependency order: `foundation → ui-core → { Icons/Tabler, Icons/Legacy }`. Neither icon package references `Sunfish.Components.Blazor` — consumers with a custom adapter can still use them.

**Tech Stack:** .NET 10, C# 13, Razor Class Library (`Microsoft.NET.Sdk.Razor`), xUnit 2.9.x (no bUnit — providers are POCOs returning strings).

---

## Scope

**In scope**
- `Marilo.Icons.Tabler` → `packages/ui-adapters-blazor/Icons/Tabler/` (PackageId `Sunfish.Icons.Tabler`)
- `Marilo.Icons` (sprite provider only) → `packages/ui-adapters-blazor/Icons/Legacy/` (PackageId `Sunfish.Icons.Legacy`)
- Copy sprite SVG assets to each package's `wwwroot/icons/` (verbatim for Tabler; rename `id="marilo-*"` → `id="sf-*"` in legacy)
- `SunfishTablerIconProvider` and `SunfishLegacyIconProvider` against `ISunfishIconProvider` (returns `string`, **not** `MarkupString`)
- DI extensions: `AddSunfishIconsTabler()` and `AddSunfishIconsLegacy()` (latter `[Obsolete]`)
- Mark every public type in `Sunfish.Icons.Legacy` with `[Obsolete]`
- 2 xUnit test projects (11 Tabler tests + 8 Legacy tests = +19 total)
- Update `Sunfish.slnx` (new `/icons/` solution folder)
- Branch: `feat/migration-phase3d-icons`

**Out of scope**
- Phase 3c: CSS provider implementations and component SCSS (separate plan)
- Generic bring-your-own-sprite helpers (`CssIconProvider`, `CustomSpriteIconProvider`, `AddMariloIconsCustom`) — defer to Phase 3e
- The 364 individual `*.svg` source files alongside Marilo's legacy sprite (they're build inputs, not runtime assets). Sprite-only migration.
- Building a fresh Tabler sprite from upstream source (pre-built sprite is checked in; no generation script found in Marilo repo)
- Kitchen-sink demos / docs updates

---

## Prerequisites

- [x] Phase 1: `IconSize`, `IconRenderMode`, `IconFlip`, `IconThemeColor` in `Sunfish.Foundation.Enums`
- [x] Phase 2: `ISunfishIconProvider` in `Sunfish.UICore.Contracts`
- [x] Phase 3a: `packages/ui-adapters-blazor/` exists with `Sunfish.Components.Blazor.csproj`
- [x] Phase 3b: `SunfishIcon.razor` consuming `ISunfishIconProvider`
- [ ] **Phase 3c (providers + SCSS):** ships before this phase. Establishes the `Providers/` sibling tree that this phase mirrors at `Icons/`. If 3c slips, this plan is still runnable — it doesn't depend on any 3c output.

---

## Key Decisions

**D-LOCATION:** Icons live under `packages/ui-adapters-blazor/Icons/<Name>/` as sibling trees to `Components/` and `Providers/`. Each is a separate packable csproj. Rationale: icons are optional per consumer; keeping them separate lets consumers swap/skip without taking the full adapter dependency. Sibling-under-blazor (rather than a top-level `packages/icons-*/`) keeps RCL static-asset machinery co-located with the adapter that needs it.

**D-LEGACY-OBSOLETE:** The legacy package is obsolete on day one. `SunfishLegacyIconProvider`, `SunfishIconsLegacyServiceExtensions`, and the `AddSunfishIconsLegacy` method all carry `[Obsolete("Use Sunfish.Icons.Tabler. Legacy icon set retained for backward compatibility only.")]`. Tests suppress CS0618 via `<NoWarn>` in the **test** csproj only (not the library).

**D-SPRITE-ASSET-PATH:** Sprite SVGs ship as RCL static web assets at `wwwroot/icons/…` and are served at `_content/<PackageId>/icons/<file>.svg`:
- Tabler: `_content/Sunfish.Icons.Tabler/icons/tabler-sprite.svg`
- Legacy: `_content/Sunfish.Icons.Legacy/icons/sprite.svg`

Matches Marilo's convention (option **b** in the brief). Providers hard-code the `_content/...` URL. RCL SDK handles the static-asset mapping via PackageId — no build-time rewriting.

**D-DI-EXTENSION:** `AddSunfishIconsTabler()` and `AddSunfishIconsLegacy()` both `AddSingleton<ISunfishIconProvider, …Provider>()`. If a consumer calls both, **last-wins** (default DI behaviour — we do **not** throw; throwing at registration time would break builder-fluent style and matches Marilo's behaviour). Documented in XML docs on both extensions.

**D-RENDER-MODE:** Both providers use `IconRenderMode.SvgSprite`. Markup: `<svg …><use href="{spriteUrl}#{symbolId}"></use></svg>`. No inline SVG or CSS-class mode in this phase.

**D-LIBRARYNAME:**
- `SunfishTablerIconProvider.LibraryName => "Tabler"`
- `SunfishLegacyIconProvider.LibraryName => "Legacy"` (was `"MariloCustom"` — renamed to match package name)

**D-CSS-CLASS-PREFIX:** Marilo hard-codes `mar-icon` / `mar-icon--{size}` on the rendered `<svg>`. Rename to `sf-icon` / `sf-icon--{size}` to match the `--sf-*` prefix established in Phase 3a.

**D-SYMBOL-ID-PREFIX (legacy):** Marilo's legacy sprite uses `id="marilo-*"` and the provider passes names verbatim — so callers had to write `GetIcon("marilo-search")`. This is inconsistent with Tabler (which auto-prefixes). **Decision:** rename every `<symbol id="marilo-*">` to `<symbol id="sf-*">` in the migrated sprite, and make `SunfishLegacyIconProvider.GetIcon` auto-prefix with `sf-`. Callers use `GetIcon("search")` consistently with Tabler. Document in legacy README as a breaking change (acceptable because the package is `[Obsolete]`).

**D-TEST-FRAMEWORK:** Pure xUnit (no bUnit). Providers are POCOs — no Blazor render tree involved. Faster, simpler, matches Phase 2 style.

---

## File Structure (after Phase 3d)

```
packages/ui-adapters-blazor/
  Sunfish.Components.Blazor.csproj       (existing)
  Base/ Components/ Internal/ wwwroot/   (existing)
  Providers/                             (from Phase 3c)
  Icons/                                 ← NEW
    Tabler/
      Sunfish.Icons.Tabler.csproj
      SunfishTablerIconProvider.cs
      SunfishIconsTablerServiceExtensions.cs
      README.md
      wwwroot/icons/tabler-sprite.svg    (2.1 MB, 5,039 symbols, verbatim copy)
      tests/
        tests.csproj                     (xunit)
        SunfishTablerIconProviderTests.cs
    Legacy/
      Sunfish.Icons.Legacy.csproj
      SunfishLegacyIconProvider.cs       [Obsolete]
      SunfishIconsLegacyServiceExtensions.cs  [Obsolete]
      README.md                          (migration guide)
      wwwroot/icons/sprite.svg           (112 KB, 362 symbols, marilo-→sf- renamed)
      tests/
        tests.csproj                     (xunit, NoWarn CS0618)
        SunfishLegacyIconProviderTests.cs
```

Files to update:
- `Sunfish.slnx` — add new `/icons/` folder with 4 projects

---

## Task 1: Scaffold Tabler package (csproj + sprite)

```bash
SUNFISH="C:/Projects/Sunfish"
MARILO="C:/Projects/Marilo"
```

- [ ] **Step 1: Create directories**

```bash
mkdir -p "$SUNFISH/packages/ui-adapters-blazor/Icons/Tabler/wwwroot/icons"
mkdir -p "$SUNFISH/packages/ui-adapters-blazor/Icons/Tabler/tests"
```

- [ ] **Step 2: Create `Sunfish.Icons.Tabler.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Icons.Tabler</PackageId>
    <Description>Tabler Icons SVG sprite provider for Sunfish (5,000+ MIT icons).</Description>
    <PackageTags>blazor;icons;tabler;svg;ui-framework;sunfish</PackageTags>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InternalsVisibleTo Include="Sunfish.Icons.Tabler.Tests" />
  </PropertyGroup>
  <ItemGroup><Compile Remove="tests/**/*.cs" /></ItemGroup>
  <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\..\..\ui-core\Sunfish.UICore.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Copy sprite + verify**

```bash
cp "$MARILO/src/Marilo.Icons.Tabler/wwwroot/icons/tabler-sprite.svg" \
   "$SUNFISH/packages/ui-adapters-blazor/Icons/Tabler/wwwroot/icons/"

grep -o '<symbol ' "$SUNFISH/packages/ui-adapters-blazor/Icons/Tabler/wwwroot/icons/tabler-sprite.svg" | wc -l
# Expected: 5039
```

- [ ] **Step 4: Write minimal README** (usage snippet + MIT license note)

- [ ] **Step 5: Baseline build**

```bash
dotnet build "$SUNFISH/packages/ui-adapters-blazor/Icons/Tabler/Sunfish.Icons.Tabler.csproj"
```
Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Commit**

```bash
but stage "packages/ui-adapters-blazor/Icons/Tabler/Sunfish.Icons.Tabler.csproj" "feat/migration-phase3d-icons"
but stage "packages/ui-adapters-blazor/Icons/Tabler/wwwroot/icons/tabler-sprite.svg" "feat/migration-phase3d-icons"
but stage "packages/ui-adapters-blazor/Icons/Tabler/README.md" "feat/migration-phase3d-icons"
but commit -m "feat(icons-tabler): scaffold Sunfish.Icons.Tabler with sprite asset" "feat/migration-phase3d-icons"
```

---

## Task 2: Tabler provider + DI extension (TDD)

- [ ] **Step 1: Test csproj** — plain `Microsoft.NET.Sdk`, refs xunit + Microsoft.NET.Test.Sdk + xunit.runner.visualstudio, ProjectReference to `Sunfish.Icons.Tabler.csproj`, `AssemblyName = Sunfish.Icons.Tabler.Tests`.

- [ ] **Step 2: Write failing tests** (`SunfishTablerIconProviderTests.cs`):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Enums;
using Sunfish.Icons.Tabler;
using Sunfish.UICore.Contracts;

namespace Sunfish.Icons.Tabler.Tests;

public class SunfishTablerIconProviderTests
{
    private static ISunfishIconProvider Resolve()
    {
        var services = new ServiceCollection();
        services.AddSunfishIconsTabler();
        return services.BuildServiceProvider().GetRequiredService<ISunfishIconProvider>();
    }

    [Fact] public void Registers() => Assert.IsType<SunfishTablerIconProvider>(Resolve());
    [Fact] public void LibraryName_IsTabler() => Assert.Equal("Tabler", Resolve().LibraryName);
    [Fact] public void RenderMode_IsSvgSprite() => Assert.Equal(IconRenderMode.SvgSprite, Resolve().RenderMode);

    [Fact]
    public void GetIcon_ReturnsNonEmptyMarkup()
    {
        var m = Resolve().GetIcon("home");
        Assert.False(string.IsNullOrWhiteSpace(m));
        Assert.Contains("<svg", m);
        Assert.Contains("tabler-home", m);
    }

    [Fact]
    public void GetIcon_RespectsExistingPrefix()
    {
        var m = Resolve().GetIcon("tabler-home");
        Assert.Contains("#tabler-home", m);
        Assert.DoesNotContain("tabler-tabler-", m);
    }

    [Theory]
    [InlineData(IconSize.Small, "16"), InlineData(IconSize.Medium, "20")]
    [InlineData(IconSize.Large, "24"), InlineData(IconSize.ExtraLarge, "32")]
    public void GetIcon_MapsSizeToPixels(IconSize size, string px)
    {
        var m = Resolve().GetIcon("home", size);
        Assert.Contains($"width=\"{px}\"", m);
        Assert.Contains($"height=\"{px}\"", m);
    }

    [Fact]
    public void SpriteUrl_IsRclContentPath() =>
        Assert.Equal("_content/Sunfish.Icons.Tabler/icons/tabler-sprite.svg", Resolve().GetIconSpriteUrl());

    [Fact]
    public void GetIcon_UsesSfIconCssClass()
    {
        var m = Resolve().GetIcon("home");
        Assert.Contains("sf-icon", m);
        Assert.DoesNotContain("mar-icon", m);
    }
}
```

- [ ] **Step 3: Run failing build** (expect errors referencing missing types).

- [ ] **Step 4: Create `SunfishTablerIconProvider.cs`**

Transform `$MARILO/src/Marilo.Icons.Tabler/TablerIconProvider.cs`:
- `using Marilo.Core.Contracts;` → `using Sunfish.UICore.Contracts;`
- `using Marilo.Core.Enums;` → `using Sunfish.Foundation.Enums;`
- Remove `using Microsoft.AspNetCore.Components;` (no `MarkupString`)
- `namespace Marilo.Icons.Tabler;` → `namespace Sunfish.Icons.Tabler;`
- `TablerIconProvider` → `SunfishTablerIconProvider`
- `IMariloIconProvider` → `ISunfishIconProvider`
- `_content/Marilo.Icons.Tabler/…` → `_content/Sunfish.Icons.Tabler/…`
- `public MarkupString GetIcon(…)` → `public string GetIcon(…)` and drop `new MarkupString(…)` wrapper
- `mar-icon mar-icon--{size…}` → `sf-icon sf-icon--{size…}`

Final shape:

```csharp
using Sunfish.Foundation.Enums;
using Sunfish.UICore.Contracts;

namespace Sunfish.Icons.Tabler;

/// <summary>Icon provider rendering Tabler Icons (MIT — https://tabler.io/icons) from an SVG sprite.</summary>
public sealed class SunfishTablerIconProvider : ISunfishIconProvider
{
    private const string SpriteUrl = "_content/Sunfish.Icons.Tabler/icons/tabler-sprite.svg";

    public IconRenderMode RenderMode => IconRenderMode.SvgSprite;
    public string LibraryName => "Tabler";

    public string GetIcon(string name, IconSize size = IconSize.Medium)
    {
        var px = size switch
        {
            IconSize.Small      => "16",
            IconSize.Medium     => "20",
            IconSize.Large      => "24",
            IconSize.ExtraLarge => "32",
            _                   => "20"
        };
        var iconId = name.StartsWith("tabler-", StringComparison.Ordinal) ? name : $"tabler-{name}";
        return $"""<svg class="sf-icon sf-icon--{size.ToString().ToLower()}" width="{px}" height="{px}" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><use href="{SpriteUrl}#{iconId}"></use></svg>""";
    }

    public string GetIconSpriteUrl() => SpriteUrl;
}
```

- [ ] **Step 5: Create `SunfishIconsTablerServiceExtensions.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.UICore.Contracts;

namespace Sunfish.Icons.Tabler;

public static class SunfishIconsTablerServiceExtensions
{
    /// <summary>
    /// Registers the Tabler SVG sprite icon provider as <see cref="ISunfishIconProvider"/>.
    /// Only one icon provider should be registered; calling both this and
    /// <c>AddSunfishIconsLegacy</c> results in last-registration-wins.
    /// </summary>
    public static IServiceCollection AddSunfishIconsTabler(this IServiceCollection services)
    {
        services.AddSingleton<ISunfishIconProvider, SunfishTablerIconProvider>();
        return services;
    }
}
```

- [ ] **Step 6: Build + test**

```bash
dotnet build "$SUNFISH/packages/ui-adapters-blazor/Icons/Tabler/Sunfish.Icons.Tabler.csproj"
dotnet test  "$SUNFISH/packages/ui-adapters-blazor/Icons/Tabler/tests/tests.csproj"
```
Expected: 0 errors, 0 warnings, **11 tests passing** (3 facts + 1 non-empty + 1 no-double-prefix + 4 theory cases + 1 sprite URL + 1 CSS class).

- [ ] **Step 7: Commit** the 4 new files (provider, extension, test csproj, tests).

---

## Task 3: Scaffold Legacy package (csproj + sprite with ID rename)

- [ ] **Step 1: Create directories** (mirror Task 1 under `Icons/Legacy/`).

- [ ] **Step 2: Create `Sunfish.Icons.Legacy.csproj`** — identical to Tabler's but `PackageId = Sunfish.Icons.Legacy`, description "Legacy Sunfish icon set (obsolete). Prefer Sunfish.Icons.Tabler for new projects.", tags include `legacy;obsolete`.

- [ ] **Step 3: Copy + transform sprite**

```bash
# stream-transform marilo- symbol ids to sf- during copy (avoids sed -i portability issues)
sed 's/id="marilo-/id="sf-/g' \
  "$MARILO/src/Marilo.Icons/wwwroot/icons/sprite.svg" \
  > "$SUNFISH/packages/ui-adapters-blazor/Icons/Legacy/wwwroot/icons/sprite.svg"

# Verify
grep -c 'id="sf-'     "$SUNFISH/packages/ui-adapters-blazor/Icons/Legacy/wwwroot/icons/sprite.svg"  # 362
grep -c 'id="marilo-' "$SUNFISH/packages/ui-adapters-blazor/Icons/Legacy/wwwroot/icons/sprite.svg"  # 0
```

- [ ] **Step 4: Write README** — mark as obsolete, note the breaking change (`GetIcon("search")` not `GetIcon("marilo-search")`; symbol IDs now `sf-*`), show migration to `AddSunfishIconsTabler()`.

- [ ] **Step 5: Baseline build**

```bash
dotnet build "$SUNFISH/packages/ui-adapters-blazor/Icons/Legacy/Sunfish.Icons.Legacy.csproj"
```
Expected: 0 errors, 0 warnings.

- [ ] **Step 6: Commit** (csproj, sprite, README).

---

## Task 4: Legacy provider + DI extension (TDD, with `[Obsolete]`)

- [ ] **Step 1: Test csproj** — same as Tabler's but `AssemblyName = Sunfish.Icons.Legacy.Tests` and add `<NoWarn>$(NoWarn);CS0618</NoWarn>` in the PropertyGroup (tests legitimately consume obsolete API under test; library csproj does **not** suppress).

- [ ] **Step 2: Write failing tests** (`SunfishLegacyIconProviderTests.cs`):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Enums;
using Sunfish.Icons.Legacy;
using Sunfish.UICore.Contracts;

namespace Sunfish.Icons.Legacy.Tests;

public class SunfishLegacyIconProviderTests
{
    private static ISunfishIconProvider Resolve()
    {
        var services = new ServiceCollection();
        services.AddSunfishIconsLegacy();
        return services.BuildServiceProvider().GetRequiredService<ISunfishIconProvider>();
    }

    [Fact] public void Registers() => Assert.IsType<SunfishLegacyIconProvider>(Resolve());
    [Fact] public void LibraryName_IsLegacy() => Assert.Equal("Legacy", Resolve().LibraryName);
    [Fact] public void RenderMode_IsSvgSprite() => Assert.Equal(IconRenderMode.SvgSprite, Resolve().RenderMode);

    [Fact]
    public void GetIcon_AutoPrefixesWithSf()
    {
        var m = Resolve().GetIcon("search");
        Assert.Contains("#sf-search", m);
        Assert.DoesNotContain("marilo-search", m);
    }

    [Fact]
    public void GetIcon_RespectsExistingSfPrefix()
    {
        var m = Resolve().GetIcon("sf-search");
        Assert.Contains("#sf-search", m);
        Assert.DoesNotContain("sf-sf-", m);
    }

    [Fact]
    public void SpriteUrl_IsRclContentPath() =>
        Assert.Equal("_content/Sunfish.Icons.Legacy/icons/sprite.svg", Resolve().GetIconSpriteUrl());

    [Fact]
    public void ProviderType_IsMarkedObsolete() =>
        Assert.NotEmpty(typeof(SunfishLegacyIconProvider)
            .GetCustomAttributes(typeof(ObsoleteAttribute), false));

    [Fact]
    public void ExtensionMethod_IsMarkedObsolete()
    {
        var method = typeof(SunfishIconsLegacyServiceExtensions)
            .GetMethod(nameof(SunfishIconsLegacyServiceExtensions.AddSunfishIconsLegacy))!;
        Assert.NotEmpty(method.GetCustomAttributes(typeof(ObsoleteAttribute), false));
    }
}
```

- [ ] **Step 3: Run failing build** (expect errors).

- [ ] **Step 4: Create `SunfishLegacyIconProvider.cs`**

Transform `$MARILO/src/Marilo.Icons/MariloIconProvider.cs` applying the same namespace/type/URL/CSS-class renames as Tabler, **plus**:
- `LibraryName => "MariloCustom"` → `LibraryName => "Legacy"`
- **Add auto-prefix** (new behaviour): `var iconId = name.StartsWith("sf-", StringComparison.Ordinal) ? name : $"sf-{name}";`
- Decorate class with `[Obsolete("Use Sunfish.Icons.Tabler. Legacy icon set retained for backward compatibility only.")]`

```csharp
using Sunfish.Foundation.Enums;
using Sunfish.UICore.Contracts;

namespace Sunfish.Icons.Legacy;

/// <summary>Legacy sprite-based icon provider. Prefer SunfishTablerIconProvider.</summary>
[Obsolete("Use Sunfish.Icons.Tabler. Legacy icon set retained for backward compatibility only.")]
public sealed class SunfishLegacyIconProvider : ISunfishIconProvider
{
    private const string SpriteUrl = "_content/Sunfish.Icons.Legacy/icons/sprite.svg";

    public IconRenderMode RenderMode => IconRenderMode.SvgSprite;
    public string LibraryName => "Legacy";

    public string GetIcon(string name, IconSize size = IconSize.Medium)
    {
        var px = size switch
        {
            IconSize.Small      => "16",
            IconSize.Medium     => "20",
            IconSize.Large      => "24",
            IconSize.ExtraLarge => "32",
            _                   => "20"
        };
        var iconId = name.StartsWith("sf-", StringComparison.Ordinal) ? name : $"sf-{name}";
        return $"""<svg class="sf-icon sf-icon--{size.ToString().ToLower()}" width="{px}" height="{px}" aria-hidden="true" focusable="false"><use href="{SpriteUrl}#{iconId}"></use></svg>""";
    }

    public string GetIconSpriteUrl() => SpriteUrl;
}
```

- [ ] **Step 5: Create `SunfishIconsLegacyServiceExtensions.cs`** (class and method both `[Obsolete]`):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.UICore.Contracts;

namespace Sunfish.Icons.Legacy;

[Obsolete("Use Sunfish.Icons.Tabler. Legacy icon set retained for backward compatibility only.")]
public static class SunfishIconsLegacyServiceExtensions
{
    /// <summary>
    /// Registers the legacy Sunfish icon provider as <see cref="ISunfishIconProvider"/>.
    /// Only one icon provider should be registered; last-registration-wins.
    /// </summary>
    [Obsolete("Use AddSunfishIconsTabler() from Sunfish.Icons.Tabler instead.")]
    public static IServiceCollection AddSunfishIconsLegacy(this IServiceCollection services)
    {
        services.AddSingleton<ISunfishIconProvider, SunfishLegacyIconProvider>();
        return services;
    }
}
```

Note: Marilo had both `UseMariloIcons(MariloBuilder)` and `AddMariloIcons(IServiceCollection)`. For Sunfish we ship only the `IServiceCollection` variant — a `SunfishBuilder.UseIcons` can be added in Phase 3c follow-up if needed.

- [ ] **Step 6: Build + test**

```bash
dotnet build "$SUNFISH/packages/ui-adapters-blazor/Icons/Legacy/Sunfish.Icons.Legacy.csproj"
dotnet test  "$SUNFISH/packages/ui-adapters-blazor/Icons/Legacy/tests/tests.csproj"
```
Expected: 0 errors, 0 warnings (CS0618 suppressed in test csproj), **8 tests passing**.

- [ ] **Step 7: Commit** (provider, extension, test csproj, tests).

---

## Task 5: Register in `Sunfish.slnx` + full-solution verification

- [ ] **Step 1: Update `Sunfish.slnx`** — add a new `/icons/` solution folder with the 4 new projects (preserve `/foundation/`, `/ui-core/`, `/ui-adapters-blazor/`, and any `/providers/` folder added by Phase 3c):

```xml
<Folder Name="/icons/">
  <Project Path="packages/ui-adapters-blazor/Icons/Tabler/Sunfish.Icons.Tabler.csproj" />
  <Project Path="packages/ui-adapters-blazor/Icons/Tabler/tests/tests.csproj" />
  <Project Path="packages/ui-adapters-blazor/Icons/Legacy/Sunfish.Icons.Legacy.csproj" />
  <Project Path="packages/ui-adapters-blazor/Icons/Legacy/tests/tests.csproj" />
</Folder>
```

- [ ] **Step 2: Full solution build + test**

```bash
cd "$SUNFISH"
dotnet build Sunfish.slnx
dotnet test Sunfish.slnx --no-build
```

Expected: 0 errors, 0 warnings. Test count: **pre-3d baseline + 19** (11 Tabler + 8 Legacy). Capture the pre-3d total before starting and verify the exact delta of +19.

- [ ] **Step 3: Commit + push**

```bash
but stage "Sunfish.slnx" "feat/migration-phase3d-icons"
but commit -m "feat(slnx): register icon packages; +19 tests green (11 Tabler + 8 Legacy)" "feat/migration-phase3d-icons"
git push origin feat/migration-phase3d-icons
```

---

## Self-Review Checklist

- [ ] Both csprojs use `Microsoft.NET.Sdk.Razor`, have correct `PackageId`, `FrameworkReference Microsoft.AspNetCore.App`, project references to foundation + ui-core (no reference to `Sunfish.Components.Blazor`)
- [ ] Tabler sprite copied verbatim: ~2.1 MB, 5,039 `<symbol>` elements, symbol IDs still `tabler-*`
- [ ] Legacy sprite: ~112 KB, 362 `<symbol>` elements, **every** id renamed `marilo-*` → `sf-*` (zero `marilo-` matches remaining)
- [ ] Both providers return `string` (not `MarkupString`), emit `sf-icon` CSS class, correct `_content/…` sprite URLs, correct `LibraryName`
- [ ] `SunfishLegacyIconProvider` auto-prefixes with `sf-`
- [ ] `SunfishLegacyIconProvider` **and** `SunfishIconsLegacyServiceExtensions` **and** `AddSunfishIconsLegacy` method all carry `[Obsolete]`
- [ ] Reflection tests confirm `[Obsolete]` attributes ship in the published assembly
- [ ] Neither provider file contains `Marilo`, `IMariloIconProvider`, `MarkupString`, `mar-icon`, or `MariloCustom`
- [ ] Both DI extensions register `AddSingleton<ISunfishIconProvider, …>` (last-wins)
- [ ] Legacy **test** csproj includes `<NoWarn>$(NoWarn);CS0618</NoWarn>`; Legacy **library** csproj does **not**
- [ ] Tabler test csproj does **not** suppress CS0618
- [ ] `dotnet build Sunfish.slnx` = 0 errors, 0 warnings
- [ ] `dotnet test Sunfish.slnx` shows +19 new tests, all passing
- [ ] `Sunfish.slnx` has `/icons/` folder with 4 projects; pre-existing folders preserved
- [ ] READMEs present for both packages; legacy README documents the `marilo-*` → `sf-*` breaking change
