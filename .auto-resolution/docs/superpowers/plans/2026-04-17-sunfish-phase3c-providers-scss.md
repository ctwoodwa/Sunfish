# Phase 3c: Provider Migration + SCSS Rename + JS Content Rename — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the three Marilo design-system provider packages (FluentUI, Bootstrap, Material) into the Sunfish adapter tree; rename all `--marilo-*`/`.marilo-*`/`.mar-*` tokens and selectors in their SCSS trees to `--sf-*`/`.sf-*`; recompile SCSS→CSS; rename `marilo-*`/`mar-*` selectors inside adapter JS file contents; and drop any remaining dual-emit `--marilo-*` aliases from `SunfishThemeProvider.razor` once the rename is complete.

**Architecture:** Providers live **inside** `packages/ui-adapters-blazor/Providers/<Name>/` (not as separate packages in a separate repo directory). Each provider is still a packable Razor class library (`Sunfish.Providers.FluentUI`, `Sunfish.Providers.Bootstrap`, `Sunfish.Providers.Material`) via its own csproj, with its own `PackageId` and `Version`. They share the same dependency stack: `foundation` → `ui-core` → `ui-adapters-blazor` → `Providers/<Name>`.

SCSS compilation uses **dart-sass CLI via npm** (same tooling as Marilo): `sass <src>:<dst> --style=expanded --no-source-map`. The root `package.json` gains `scss:build:<provider>` scripts. No webpack, no gulp.

**Tech Stack:** .NET 10, C# 13, Blazor (Razor Class Library), dart-sass ^1.98, Node/npm for the sass CLI, bUnit 2.7.x, xUnit 2.9.x.

---

## Prerequisite

Phase 3b (`feat/migration-phase3b-blazor-components`) **must be merged into `main`** before Phase 3c begins. Branch Phase 3c off the post-3b `main`:

```bash
cd "C:/Projects/Sunfish"
git fetch origin
git switch main
git pull --ff-only
git switch -c feat/migration-phase3c-providers-scss
```

Phase 3b owns the per-component `.razor.scss` files that reference `--marilo-*` tokens (via D-SCSS and D-CSS-LITERALS). Those component SCSS files are **not** in scope for Phase 3c — they continue to consume `--sf-*` tokens defined by the providers after this phase.

---

## Scope

### In scope
1. Copy three provider csproj trees (FluentUI, Bootstrap, Material) from Marilo into `packages/ui-adapters-blazor/Providers/<Name>/`.
2. Rename C# type names, namespaces, and XML doc comments: `Marilo*` → `Sunfish*`, `IMarilo*` → `ISunfish*`.
3. Rewrite DI extensions: `UseFluentUI(this MariloBuilder)` → `AddSunfishFluentUI(this SunfishBuilder)` (same for Bootstrap, Material).
4. Bulk-rename tokens and selectors across all provider SCSS trees (~245 files total):
   - `--marilo-*` → `--sf-*` (CSS custom properties)
   - `.marilo-*` → `.sf-*` (selectors)
   - `.mar-*` → `.sf-*` (BEM-block selectors)
   - `marilo-*` mixin/function names → `sf-*`
5. Install dart-sass via npm and add `scss:build:<provider>` scripts to a new root `package.json`.
6. Rebuild each provider's compiled CSS: `wwwroot/css/sunfish-<provider>.css`.
7. Rename JS **contents** inside `packages/ui-adapters-blazor/wwwroot/js/*.js`:
   - `.marilo-*`, `.mar-*` selectors → `.sf-*`
   - `MariloX` identifiers → `SunfishX`
   - File names are already `sunfish-*.js` (Phase 3a) — only content is rewritten.
8. Copy icon SVG sprites (FluentUI `fluent-icons.svg`; Bootstrap uses `Marilo.Icons` sprite — rebase path to `Sunfish.Icons`).
9. Drop any remaining `--marilo-*` dual-emit aliases in `SunfishThemeProvider.razor` (verification step — current 3b file appears to already emit only `--sf-*`).
10. Register the three new provider csprojs in `Sunfish.slnx`.

### Out of scope
- Visual-parity regression testing (deferred to Phase 7 / kitchen-sink demo per D-VERIFY-PARITY).
- React adapter providers (Phase 4+).
- Creating a Sunfish.Icons package — Bootstrap's sprite URL will be retargeted to `_content/Sunfish.Icons/...` as a placeholder; creation of that package is Phase 3d/later.
- Docs site updates (separate `sunfish-docs-change` pipeline).
- Blocks / kitchen-sink updates.
- Legacy `_generated-base.scss` monoliths — they are retained-but-not-imported in Marilo; do not copy them.

---

## Key Decisions

**D-PROVIDER-STRUCTURE:** Providers live under `packages/ui-adapters-blazor/Providers/<Name>/`, not in a separate top-level `packages/ui-adapters-blazor-providers-*`. Each has its own csproj with a distinct `PackageId` (`Sunfish.Providers.FluentUI`, etc.). Rationale: keeps adapters and their design-system skins co-located, avoids an explosion of sibling folders at the packages root, and matches the atomic migration pattern used in Phase 3a/3b. They remain independently packable.

**D-SCSS-TOOLING:** Use dart-sass CLI via npm — identical to Marilo. A root `package.json` at the Sunfish repo root hosts `scss:build:<provider>`, `scss:watch:<provider>`, `scss:build` (run-p). No SCSS compilation is wired into MSBuild — CSS is committed as a pre-built artifact in `wwwroot/css/sunfish-<provider>.css`. Developers run `npm run scss:build` after editing SCSS, same as Marilo workflow. Rationale: Marilo's tooling already works, is well-understood, and keeping it avoids re-architecting during a mechanical rename phase.

**D-DI-EXTENSION:** Each provider ships **one** DI extension method on `SunfishBuilder` (defined in `Sunfish.Foundation.Extensions`):
- `AddSunfishFluentUI(this SunfishBuilder, Action<FluentUIOptions>? configure = null)`
- `AddSunfishBootstrap(this SunfishBuilder, Action<BootstrapOptions>? configure = null)`
- `AddSunfishMaterial(this SunfishBuilder, Action<MaterialOptions>? configure = null)`

Each registers `ISunfishCssProvider`, `ISunfishIconProvider`, `ISunfishJsInterop` as scoped, plus an options singleton. Each returns the `SunfishBuilder` so `AddSunfish(...).AddSunfishFluentUI().AddMariloInteropServices()` chains cleanly. (Note: `AddMariloInteropServices` is the name still used by Phase 3b in `ui-adapters-blazor/Internal/Interop/InteropServiceExtensions.cs`; its rename is not in scope here but a TODO is noted.)

**D-CSS-PREFIX-FINAL:** `.sf-` is authoritative. `.marilo-*` and `.mar-*` aliases are **fully removed** — not retained as backward-compat. Any consumer mid-migration must update imports to use `Sunfish.Providers.*` packages and `.sf-*` selectors. Rationale: keeping dual classes doubles SCSS output size and creates ambiguous styling surface area.

**D-VERIFY-PARITY:** Defer visual parity to Phase 7 (kitchen-sink demo). Phase 3c's verification stops at:
- All three providers build with 0 errors/warnings
- `dotnet build Sunfish.slnx` and `dotnet test Sunfish.slnx` both green (29 tests from Phase 3b unchanged)
- Post-rename SCSS compiles with 0 deprecation warnings beyond the ones Marilo already silences
- A scripted post-rename audit finds zero remaining `--marilo-`, `.marilo-`, or `.mar-` tokens/selectors in provider SCSS and in adapter JS contents

No pixel-diffs, no Playwright, no manual comparison against Marilo samples in this phase. Rationale: Phase 3c is mechanical — there is no behavioral or visual change intended. Phase 7's kitchen-sink build will exercise all three providers against real components and is the natural validation gate.

---

## File Structure (after Phase 3c)

```
packages/ui-adapters-blazor/
  Sunfish.Components.Blazor.csproj         ← unchanged
  SunfishThemeProvider.razor                ← verify: emits only --sf-*
  Base/SunfishComponentBase.cs              ← unchanged
  Internal/                                 ← unchanged
  wwwroot/js/*.js                           ← content rewritten (.sf-*, SunfishX)
  Components/**                             ← unchanged (from 3b)
  Providers/
    FluentUI/
      Sunfish.Providers.FluentUI.csproj
      FluentUICssProvider.cs                ← ~822 lines; renamed Marilo→Sunfish, mar-→sf-
      FluentUIIconProvider.cs               ← ~31 lines
      FluentUIJsInterop.cs                  ← ~54 lines; JS path updated
      Extensions/
        ServiceCollectionExtensions.cs      ← AddSunfishFluentUI(this SunfishBuilder)
      Styles/                               ← 95 SCSS files, all --sf-* / .sf-*
        sunfish-fluentui.scss               ← renamed entrypoint
        _index.scss
        foundation/ (9 files)
        patterns/ (5 files)
        components/ (81 files)
      wwwroot/
        css/sunfish-fluentui.css            ← recompiled
        icons/fluent-icons.svg              ← copied as-is
        js/sunfish-fluentui.js              ← content rewritten
      STYLES_README.md                      ← optional; update Marilo→Sunfish

    Bootstrap/
      Sunfish.Providers.Bootstrap.csproj
      BootstrapCssProvider.cs               ← ~1069 lines
      BootstrapIconProvider.cs              ← ~33 lines; sprite path updated
      BootstrapJsInterop.cs                 ← ~54 lines
      Extensions/ServiceCollectionExtensions.cs  ← AddSunfishBootstrap
      Styles/                               ← 97 SCSS files (includes Bootstrap bridge partials)
        sunfish-bootstrap.scss              ← renamed entrypoint
        _index.scss
        _variables.scss, _tokens.scss, _tokens-dark.scss
        _bridge-*.scss (10 files)
        foundation/ (9)
        patterns/ (5)
        components/ (84)
      wwwroot/
        css/sunfish-bootstrap.css
        icons/bootstrap-icons.svg
        js/sunfish-bootstrap.js
      STYLES_README.md

    Material/
      Sunfish.Providers.Material.csproj
      MaterialCssProvider.cs                ← ~821 lines
      MaterialIconProvider.cs               ← ~30 lines; sprite path retargeted to FluentUI provider
      MaterialJsInterop.cs                  ← ~55 lines
      _Imports.razor                        ← single @using line
      Extensions/ServiceCollectionExtensions.cs  ← AddSunfishMaterial
      Styles/                               ← 92 SCSS files
        sunfish-material.scss               ← renamed entrypoint
        _index.scss
        foundation/ (9)
        patterns/ (5)
        components/ (78)
      wwwroot/
        css/sunfish-material.css
        (no icons — reuses FluentUI sprite)
        (no js — Material uses _content/Sunfish.Providers.Material/js if needed)

Root-level new/modified files:
  package.json                              ← new; dart-sass + run-p, scss:build scripts
  package-lock.json                         ← generated by npm install
  .gitignore                                ← add node_modules/ if not present
  Sunfish.slnx                              ← + 3 provider csprojs
```

Counts to expect after Phase 3c:
- FluentUI SCSS files: 95 (9 foundation + 5 patterns + 81 components + `_index.scss`, entrypoint, `_gantt.scss`, `_resizable-container.scss`, `_signalr-status.scss`, `STYLES_README.md` excluded from count)
- Bootstrap SCSS files: 97 (adds `_variables.scss`, `_tokens.scss`, `_tokens-dark.scss`, 10 `_bridge-*.scss`)
- Material SCSS files: 92 (same layout as FluentUI but fewer components — 78 component partials)

---

## Task 1: Add root package.json with dart-sass tooling

**Files:**
- Create: `C:/Projects/Sunfish/package.json`
- Modify: `C:/Projects/Sunfish/.gitignore` (ensure `node_modules/` and `package-lock.json` handled appropriately — keep `package-lock.json`)

- [ ] **Step 1: Create package.json**

```json
{
  "name": "sunfish",
  "version": "0.1.0",
  "private": true,
  "license": "MIT",
  "description": "Sunfish — framework-agnostic UI building blocks",
  "scripts": {
    "scss:build:fluentui": "sass packages/ui-adapters-blazor/Providers/FluentUI/Styles/sunfish-fluentui.scss:packages/ui-adapters-blazor/Providers/FluentUI/wwwroot/css/sunfish-fluentui.css --style=expanded --no-source-map",
    "scss:build:bootstrap": "sass packages/ui-adapters-blazor/Providers/Bootstrap/Styles/sunfish-bootstrap.scss:packages/ui-adapters-blazor/Providers/Bootstrap/wwwroot/css/sunfish-bootstrap.css --style=expanded --no-source-map --load-path=node_modules --silence-deprecation=color-functions --silence-deprecation=import --silence-deprecation=global-builtin --silence-deprecation=if-function",
    "scss:build:material": "sass packages/ui-adapters-blazor/Providers/Material/Styles/sunfish-material.scss:packages/ui-adapters-blazor/Providers/Material/wwwroot/css/sunfish-material.css --style=expanded --no-source-map",
    "scss:build": "run-p scss:build:fluentui scss:build:bootstrap scss:build:material",
    "scss:watch:fluentui": "npm run scss:build:fluentui -- --watch",
    "scss:watch:bootstrap": "npm run scss:build:bootstrap -- --watch",
    "scss:watch:material": "npm run scss:build:material -- --watch",
    "scss:watch": "run-p scss:watch:fluentui scss:watch:bootstrap scss:watch:material"
  },
  "devDependencies": {
    "bootstrap": "^5.3.3",
    "npm-run-all": "^4.1.5",
    "sass": "^1.98.0"
  }
}
```

- [ ] **Step 2: Install dependencies**

```bash
cd "C:/Projects/Sunfish"
npm install
```

Expected: `node_modules/` populated; `package-lock.json` created; no scss:build runs yet (providers don't exist).

- [ ] **Step 3: Confirm .gitignore**

Ensure `C:/Projects/Sunfish/.gitignore` contains `node_modules/`. If missing, add it. Do not ignore `package-lock.json` (commit it for reproducibility).

- [ ] **Step 4: Stage and commit**

```bash
git add package.json package-lock.json .gitignore
git commit -m "build: add root package.json with dart-sass tooling for providers"
```

---

## Task 2: Migrate FluentUI provider (reference implementation)

This is the most detailed task. Bootstrap and Material follow the same shape with provider-specific adjustments.

**Files:**
- Create: `packages/ui-adapters-blazor/Providers/FluentUI/Sunfish.Providers.FluentUI.csproj`
- Copy+transform: `FluentUICssProvider.cs`, `FluentUIIconProvider.cs`, `FluentUIJsInterop.cs`, `Extensions/ServiceCollectionExtensions.cs`, `Styles/**`, `wwwroot/**`
- Source: `C:/Projects/Marilo/src/Marilo.Providers.FluentUI/`

- [ ] **Step 1: Create the provider directory and copy the source tree**

```bash
SRC="C:/Projects/Marilo/src/Marilo.Providers.FluentUI"
DST="C:/Projects/Sunfish/packages/ui-adapters-blazor/Providers/FluentUI"
mkdir -p "$DST/Extensions" "$DST/Styles" "$DST/wwwroot/css" "$DST/wwwroot/icons" "$DST/wwwroot/js"

# Copy C# files (we'll transform them next)
cp "$SRC/FluentUICssProvider.cs" "$DST/"
cp "$SRC/FluentUIIconProvider.cs" "$DST/"
cp "$SRC/FluentUIJsInterop.cs" "$DST/"
cp "$SRC/Extensions/MariloBuilderExtensions.cs" "$DST/Extensions/ServiceCollectionExtensions.cs"
cp "$SRC/STYLES_README.md" "$DST/"

# Copy SCSS tree (skip _generated-base.scss — legacy, not imported)
cp -r "$SRC/Styles/foundation" "$DST/Styles/"
cp -r "$SRC/Styles/patterns" "$DST/Styles/"
cp -r "$SRC/Styles/components" "$DST/Styles/"
cp "$SRC/Styles/_index.scss" "$DST/Styles/"
cp "$SRC/Styles/marilo-fluentui.scss" "$DST/Styles/sunfish-fluentui.scss"
# Top-level loose partials in Marilo's Styles root (non-generated ones):
cp "$SRC/Styles/_gantt.scss" "$DST/Styles/" 2>/dev/null || true
cp "$SRC/Styles/_resizable-container.scss" "$DST/Styles/" 2>/dev/null || true
cp "$SRC/Styles/_signalr-status.scss" "$DST/Styles/" 2>/dev/null || true

# Icons
cp "$SRC/wwwroot/icons/fluent-icons.svg" "$DST/wwwroot/icons/"

# JS
cp "$SRC/wwwroot/js/marilo-fluentui.js" "$DST/wwwroot/js/sunfish-fluentui.js"
```

Note: we do **not** copy `_generated-base.scss` — Marilo's own entrypoint comment says it is retained-but-not-imported legacy. Skipping reduces file count by ~3172 lines of dead SCSS.

- [ ] **Step 2: Create the csproj**

Create `packages/ui-adapters-blazor/Providers/FluentUI/Sunfish.Providers.FluentUI.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Providers.FluentUI</PackageId>
    <Description>Fluent UI design-system provider for Sunfish.</Description>
    <PackageTags>blazor;sunfish;fluent-ui;ui-provider;razor-class-library</PackageTags>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\..\..\ui-core\Sunfish.UICore.csproj" />
    <ProjectReference Include="..\..\Sunfish.Components.Blazor.csproj" />
  </ItemGroup>
  <!-- SCSS files are authored but not compiled by MSBuild; CSS is pre-compiled via npm -->
  <ItemGroup>
    <None Remove="Styles\**\*.scss" />
    <Content Include="Styles\**\*.scss" Pack="false" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Transform C# files (namespaces, types, doc comments)**

Apply these substitutions to every `.cs` file in `Providers/FluentUI/`:

| From | To |
|---|---|
| `using Marilo.Core.Base;` | `using Sunfish.Foundation.Base;` |
| `using Marilo.Core.Contracts;` | `using Sunfish.UICore.Contracts;` |
| `using Marilo.Core.Enums;` | `using Sunfish.Foundation.Enums;` |
| `using Marilo.Core.Extensions;` | `using Sunfish.Foundation.Extensions;` |
| `using Marilo.Core.Configuration;` | `using Sunfish.Foundation.Configuration;` |
| `namespace Marilo.Providers.FluentUI` | `namespace Sunfish.Providers.FluentUI` |
| `namespace Marilo.Providers.FluentUI.Extensions` | `namespace Sunfish.Providers.FluentUI.Extensions` |
| `IMariloCssProvider` | `ISunfishCssProvider` |
| `IMariloIconProvider` | `ISunfishIconProvider` |
| `IMariloJsInterop` | `ISunfishJsInterop` |
| `MariloBuilder` | `SunfishBuilder` |
| `MariloTheme` | `SunfishTheme` |
| `"Marilo"` (doc text) | `"Sunfish"` |
| `"Marilo components"` | `"Sunfish components"` |
| `"Marilo Icons sprite"` | `"Sunfish Icons sprite"` |

Also: every `.mar-*` CSS class literal embedded inside C# strings must change to `.sf-*`. In `FluentUICssProvider.cs` there are ~800 such literals across ~160 methods (e.g., `AddClass("mar-container")` → `AddClass("sf-container")`, `$"mar-button--{variant}"` → `$"sf-button--{variant}"`).

**Use Read + Edit with `replace_all`** on the CssProvider file — do **not** try to hand-edit each literal. Recommended order for mechanical substitutions inside C# strings:

1. `"mar-` → `"sf-` (replace_all)
2. `$"mar-` → `$"sf-` (replace_all)
3. `"marilo-icon"` → `"sunfish-icon"` etc. for any `marilo-` classes in string literals (rare — only in IconProvider) (replace_all)
4. Path literal: `_content/Marilo.Providers.FluentUI/` → `_content/Sunfish.Providers.FluentUI/` (replace_all)

- [ ] **Step 4: Rewrite the DI extension (ServiceCollectionExtensions.cs)**

Replace the copied file contents with:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Extensions;
using Sunfish.UICore.Contracts;

namespace Sunfish.Providers.FluentUI.Extensions;

public class FluentUIOptions
{
    public SunfishTheme? Theme { get; set; }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Sunfish Fluent UI design-system provider on the given <see cref="SunfishBuilder"/>.
    /// Registers <see cref="ISunfishCssProvider"/>, <see cref="ISunfishIconProvider"/>, and
    /// <see cref="ISunfishJsInterop"/> as scoped services.
    /// </summary>
    public static SunfishBuilder AddSunfishFluentUI(this SunfishBuilder builder, Action<FluentUIOptions>? configure = null)
    {
        var options = new FluentUIOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<ISunfishCssProvider, FluentUICssProvider>();
        builder.Services.AddScoped<ISunfishIconProvider, FluentUIIconProvider>();
        builder.Services.AddScoped<ISunfishJsInterop, FluentUIJsInterop>();
        return builder;
    }
}
```

Note: the `AddMariloCoreServices()` call at the end of Marilo's version is dropped — Sunfish's `AddSunfish()` already registers core services upstream of the `SunfishBuilder`. The extension just returns the builder for further chaining.

- [ ] **Step 5: Run the SCSS rename sweep**

Create a reusable rename helper at `C:/Projects/Sunfish/scripts/rename-marilo-scss.sh`:

```bash
#!/usr/bin/env bash
# Rename Marilo → Sunfish tokens/selectors in an SCSS tree (in place).
# Usage: rename-marilo-scss.sh <path-to-styles-dir>
set -euo pipefail
DIR="${1:?usage: rename-marilo-scss.sh <styles-dir>}"

# Find all .scss files (safe: recurse, preserve symlinks skipped)
mapfile -t FILES < <(find "$DIR" -type f -name '*.scss')

# Order matters: --marilo- first (longest), then .marilo-, then marilo- (bare mixin/fn),
# then .mar- (shortest — catches BEM blocks).
for f in "${FILES[@]}"; do
  sed -i \
    -e 's/--marilo-/--sf-/g' \
    -e 's/\.marilo-/.sf-/g' \
    -e 's/\bmarilo-/sf-/g' \
    -e 's/\.mar-/.sf-/g' \
    -e 's/\bmar-/sf-/g' \
    "$f"
done

echo "Renamed tokens/selectors in ${#FILES[@]} SCSS files under $DIR"
```

Notes on the sed rules:
- `--marilo-` must come first to avoid being swallowed by `\bmarilo-`.
- `\.marilo-` and `\.mar-` are anchored to a literal `.` so they only match selectors, not incidental prose.
- `\bmarilo-` and `\bmar-` catch mixin names (`@mixin marilo-focus-ring`, `@include mar-focus`). The `\b` word boundary prevents matching inside tokens like `--sf-...` already rewritten on prior lines.
- Run under `bash` on Windows via Git Bash / WSL; do not invoke via cmd.exe.

Execute against FluentUI:

```bash
bash C:/Projects/Sunfish/scripts/rename-marilo-scss.sh \
  C:/Projects/Sunfish/packages/ui-adapters-blazor/Providers/FluentUI/Styles
```

Expected: output "Renamed tokens/selectors in 95 SCSS files under ...".

- [ ] **Step 6: Post-rename audit for FluentUI SCSS**

```bash
cd "C:/Projects/Sunfish/packages/ui-adapters-blazor/Providers/FluentUI/Styles"
echo "Residual --marilo-:" && grep -rc '\-\-marilo-' . | grep -v ':0$' || echo "  (none)"
echo "Residual .marilo-:" && grep -rc '\.marilo-' . | grep -v ':0$' || echo "  (none)"
echo "Residual .mar-:" && grep -rc '\.mar-' . | grep -v ':0$' || echo "  (none)"
echo "Residual bare marilo-:" && grep -rEc '\bmarilo-' . | grep -v ':0$' || echo "  (none)"
```

Expected: all four report "(none)". If any residuals appear, investigate and re-run sed or hand-edit.

- [ ] **Step 7: Compile FluentUI SCSS**

```bash
cd "C:/Projects/Sunfish"
npm run scss:build:fluentui
```

Expected: `packages/ui-adapters-blazor/Providers/FluentUI/wwwroot/css/sunfish-fluentui.css` created; 0 errors; any deprecation warnings are OK for now.

- [ ] **Step 8: Build the FluentUI provider project**

```bash
cd "C:/Projects/Sunfish"
dotnet restore packages/ui-adapters-blazor/Providers/FluentUI/Sunfish.Providers.FluentUI.csproj
dotnet build packages/ui-adapters-blazor/Providers/FluentUI/Sunfish.Providers.FluentUI.csproj
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 9: Register in Sunfish.slnx**

Edit `C:/Projects/Sunfish/Sunfish.slnx` to add:

```xml
  <Folder Name="/providers/">
    <Project Path="packages/ui-adapters-blazor/Providers/FluentUI/Sunfish.Providers.FluentUI.csproj" />
  </Folder>
```

(Add Bootstrap and Material to the same folder in subsequent tasks.)

- [ ] **Step 10: Full solution build smoke test**

```bash
cd "C:/Projects/Sunfish"
dotnet build Sunfish.slnx
dotnet test Sunfish.slnx --no-build
```

Expected: 0 errors, 0 warnings. Test count unchanged: **29 tests** (3 foundation + 13 ui-core + 13 blazor adapter — i.e. 6 from Phase 3a + 7 added in 3b; replace with the actual Phase 3b-final count).

- [ ] **Step 11: Stage and commit**

```bash
git add packages/ui-adapters-blazor/Providers/FluentUI
git add Sunfish.slnx scripts/rename-marilo-scss.sh
git commit -m "feat(providers): migrate FluentUI provider with SCSS rename to --sf-*"
```

---

## Task 3: Migrate Bootstrap provider

Same shape as Task 2 with these deltas:

- Copy from `C:/Projects/Marilo/src/Marilo.Providers.Bootstrap/`.
- Destination: `packages/ui-adapters-blazor/Providers/Bootstrap/`.
- Additional top-level SCSS partials to copy: `_variables.scss`, `_tokens.scss`, `_tokens-dark.scss`, and the 10 `_bridge-*.scss` files.
- Bootstrap's entrypoint imports from `bootstrap/scss/...`; the `--load-path=node_modules` flag in the npm script resolves these. No SCSS edits required for Bootstrap imports — only the `@import "index"` at the end references Marilo's index, and `_index.scss` + `_variables.scss` + `_tokens.scss` + bridge files all get swept by the rename script.
- `BootstrapIconProvider.cs` references `_content/Marilo.Icons/icons/sprite.svg` — retarget to `_content/Sunfish.Icons/icons/sprite.svg` (placeholder; `Sunfish.Icons` package does not yet exist but the path keeps Phase 3d's work clear).
- `BootstrapJsInterop.cs` references `./_content/Marilo.Providers.Bootstrap/js/marilo-bootstrap.js` — retarget to `./_content/Sunfish.Providers.Bootstrap/js/sunfish-bootstrap.js`.

- [ ] **Step 1: Copy tree** (mirror Task 2 Step 1 with Bootstrap paths; include `_variables.scss`, `_tokens.scss`, `_tokens-dark.scss`, and all `_bridge-*.scss` partials)

- [ ] **Step 2: Create `Sunfish.Providers.Bootstrap.csproj`** (same shape as FluentUI; `PackageId="Sunfish.Providers.Bootstrap"`)

- [ ] **Step 3: Transform C# files** (same substitutions; note the `"Marilo Icons sprite"` comment and sprite path retarget in `BootstrapIconProvider.cs`)

- [ ] **Step 4: Rewrite DI extension** as `AddSunfishBootstrap(this SunfishBuilder, Action<BootstrapOptions>? configure = null)`

- [ ] **Step 5: Run SCSS rename sweep**

```bash
bash C:/Projects/Sunfish/scripts/rename-marilo-scss.sh \
  C:/Projects/Sunfish/packages/ui-adapters-blazor/Providers/Bootstrap/Styles
```

Expected: "Renamed tokens/selectors in 97 SCSS files".

Note: the Bootstrap provider imports `bootstrap/scss/*` partials which contain `.btn`, `.col`, etc. — those are Bootstrap's own classes and must NOT be renamed. Our sed rules only target `marilo-`, `mar-`, `.marilo-`, `.mar-`, so Bootstrap classes (`.container`, `.btn-primary`, etc.) remain untouched. Verify with a spot-check:

```bash
grep -c '\.btn' packages/ui-adapters-blazor/Providers/Bootstrap/Styles/_bridge-buttons.scss
```

Expected: nonzero (Bootstrap bridge intentionally references `.btn`).

- [ ] **Step 6: Post-rename audit** (same 4 grep checks as FluentUI)

- [ ] **Step 7: Compile Bootstrap SCSS**

```bash
npm run scss:build:bootstrap
```

Expected: `sunfish-bootstrap.css` produced; deprecation warnings from Bootstrap internals are silenced by the flags in `package.json`.

- [ ] **Step 8: Build + register in slnx + smoke test** (as Task 2 Steps 8–10)

- [ ] **Step 9: Commit**

```bash
git add packages/ui-adapters-blazor/Providers/Bootstrap Sunfish.slnx
git commit -m "feat(providers): migrate Bootstrap provider with SCSS rename to --sf-*"
```

---

## Task 4: Migrate Material provider

Same shape as Task 2 with these deltas:

- Copy from `C:/Projects/Marilo/src/Marilo.Providers.Material/`.
- Destination: `packages/ui-adapters-blazor/Providers/Material/`.
- **No wwwroot/icons** — Material reuses the FluentUI sprite via a path reference. After transformation, `MaterialIconProvider.cs` constant becomes:
  ```csharp
  private const string SpriteUrl = "_content/Sunfish.Providers.FluentUI/icons/fluent-icons.svg";
  ```
  This is intentional: a future Phase 3d (or a Material-specific icon task) can introduce a dedicated Material sprite. Document this cross-package path in a code comment.
- **No wwwroot/js directory exists in Marilo's Material** — `MaterialJsInterop.cs` references `./_content/Marilo.Providers.Material/js/marilo-material.js` but that file is not present in the source tree. Create an empty `wwwroot/js/sunfish-material.js` placeholder (export an empty module) OR have the JsInterop class point to a shared adapter script. Simplest: create the empty JS file with a comment explaining it's a placeholder.
- `_Imports.razor` (1 line: `@using Microsoft.AspNetCore.Components.Web`) — copy as-is.

- [ ] **Step 1: Copy tree** (mirror Task 2 Step 1 with Material paths; include `_Imports.razor`; skip icons dir)

- [ ] **Step 2: Create `Sunfish.Providers.Material.csproj`**

- [ ] **Step 3: Transform C# files** — including the sprite URL path in `MaterialIconProvider.cs`

- [ ] **Step 4: Rewrite DI extension** as `AddSunfishMaterial(this SunfishBuilder, Action<MaterialOptions>? configure = null)`

- [ ] **Step 5: Run SCSS rename sweep**

```bash
bash C:/Projects/Sunfish/scripts/rename-marilo-scss.sh \
  C:/Projects/Sunfish/packages/ui-adapters-blazor/Providers/Material/Styles
```

Expected: "Renamed tokens/selectors in 92 SCSS files".

- [ ] **Step 6: Post-rename audit** (4 grep checks)

- [ ] **Step 7: Create placeholder JS file**

Create `packages/ui-adapters-blazor/Providers/Material/wwwroot/js/sunfish-material.js`:

```javascript
// Sunfish Material provider — JS interop placeholder.
// Most interop is provided by the adapter itself; this file exists so
// MaterialJsInterop.InitializeAsync() has a module to import.
export {};
```

- [ ] **Step 8: Compile Material SCSS**

```bash
npm run scss:build:material
```

- [ ] **Step 9: Build + register in slnx + smoke test** (as Task 2 Steps 8–10)

- [ ] **Step 10: Commit**

```bash
git add packages/ui-adapters-blazor/Providers/Material Sunfish.slnx
git commit -m "feat(providers): migrate Material provider with SCSS rename to --sf-*"
```

---

## Task 5: Rewrite adapter JS file contents

The 13 JS files under `packages/ui-adapters-blazor/wwwroot/js/` still contain `.marilo-*` / `.mar-*` selectors and `MariloX` identifiers in comments/variable names. File names were already switched to `sunfish-*.js` in Phase 3a.

Known residuals from a prior grep (21 occurrences across 13 files):

```
sunfish-resize.js             1
sunfish-positioning.js        1
sunfish-clipboard-download.js 1
sunfish-dropzone.js           1
sunfish-measurement.js        2
allocation-scheduler.js       1
sunfish-observers.js          2
sunfish-datasheet.js          2
sunfish-graphics.js           1
resizable-container.js        4
sunfish-dragdrop.js           1
sunfish-gantt.js              1
sunfish-map.js                3
```

**Files:**
- Modify in place: `packages/ui-adapters-blazor/wwwroot/js/*.js` (13 files)

- [ ] **Step 1: Scripted JS content rename**

Create `C:/Projects/Sunfish/scripts/rename-marilo-js.sh`:

```bash
#!/usr/bin/env bash
# Rename Marilo → Sunfish identifiers and selectors in adapter JS files (in place).
# Usage: rename-marilo-js.sh <path-to-js-dir>
set -euo pipefail
DIR="${1:?usage: rename-marilo-js.sh <js-dir>}"

mapfile -t FILES < <(find "$DIR" -maxdepth 1 -type f -name '*.js')

for f in "${FILES[@]}"; do
  sed -i \
    -e 's/\.marilo-/.sf-/g' \
    -e 's/\.mar-/.sf-/g' \
    -e "s/'marilo-/'sf-/g" \
    -e 's/"marilo-/"sf-/g' \
    -e "s/'mar-/'sf-/g" \
    -e 's/"mar-/"sf-/g' \
    -e 's/\bMarilo\([A-Z][A-Za-z0-9_]*\)/Sunfish\1/g' \
    -e 's/\bMarilo\b/Sunfish/g' \
    "$f"
done

echo "Renamed JS content in ${#FILES[@]} files under $DIR"
```

Notes:
- Selector rules cover both CSS-selector literals inside `querySelector()` calls and string-quoted class names for `classList.add()`.
- `\bMarilo\([A-Z][A-Za-z0-9_]*\)` handles PascalCase identifiers like `MariloDataSheetBridge` → `SunfishDataSheetBridge`.
- `\bMarilo\b` catches bare `Marilo` in comments.

Execute:

```bash
bash C:/Projects/Sunfish/scripts/rename-marilo-js.sh \
  C:/Projects/Sunfish/packages/ui-adapters-blazor/wwwroot/js
```

- [ ] **Step 2: Post-rename audit**

```bash
cd "C:/Projects/Sunfish/packages/ui-adapters-blazor/wwwroot/js"
echo "Residual marilo/mar:"
grep -Ec '\.marilo-|\.mar-|\bMarilo\b|'"'"'marilo-|'"'"'mar-|"marilo-|"mar-' *.js | grep -v ':0$' || echo "  (none)"
```

Expected: "(none)".

- [ ] **Step 3: Adapter build smoke test**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj
dotnet test packages/ui-adapters-blazor/tests/tests.csproj
```

Expected: 0 errors, 0 warnings. All adapter tests pass (the JS content isn't exercised at unit-test time but we confirm nothing else broke).

- [ ] **Step 4: Commit**

```bash
git add packages/ui-adapters-blazor/wwwroot/js scripts/rename-marilo-js.sh
git commit -m "refactor(ui-adapters-blazor): rename .mar-/.marilo- selectors and MariloX ids in JS"
```

---

## Task 6: Verify SunfishThemeProvider emits only --sf-*

Phase 3a's plan called for dual-emit (`--sf-*` authoritative + `--marilo-*` aliases) per D-THEME-CSS-ATOMICITY. As of the start of Phase 3c, the actual `SunfishThemeProvider.razor` in Phase 3b appears to emit only `--sf-*` (no alias block present). This task is therefore primarily **verification**.

**Files:**
- Verify / modify: `packages/ui-adapters-blazor/SunfishThemeProvider.razor`

- [ ] **Step 1: Audit the file for --marilo- references**

```bash
grep -n 'marilo-' packages/ui-adapters-blazor/SunfishThemeProvider.razor || echo "no marilo- references found"
```

Expected: "no marilo- references found". If any appear (e.g., a `--marilo-color-primary` alias block was introduced mid-3b and not cleaned up), remove them — delete any `.AddStyle("--marilo-...", ...)` lines from `GenerateThemeStyles()`.

- [ ] **Step 2: Check the tests still pass**

```bash
cd "C:/Projects/Sunfish"
dotnet test packages/ui-adapters-blazor/tests/tests.csproj --filter SunfishThemeProvider
```

Expected: 3 tests pass (existing theme provider tests).

- [ ] **Step 3: Commit if any changes were made**

Only commit if Step 1 revealed and removed aliases. If the file was already clean, skip this commit — note "no-op verification" in the task notes.

```bash
git add packages/ui-adapters-blazor/SunfishThemeProvider.razor
git commit -m "refactor(ui-adapters-blazor): drop --marilo-* CSS-var aliases from SunfishThemeProvider"
```

---

## Task 7: End-to-end verification

**Files:** none (audit-only)

- [ ] **Step 1: Repo-wide residual audit**

```bash
cd "C:/Projects/Sunfish"
echo "=== Provider SCSS residuals ==="
for p in FluentUI Bootstrap Material; do
  echo "-- $p --"
  grep -rEl '\-\-marilo-|\.marilo-|\.mar-|\bmarilo-|\bmar-' \
    "packages/ui-adapters-blazor/Providers/$p/Styles" || echo "  (clean)"
done

echo "=== Adapter JS residuals ==="
grep -rEl '\.marilo-|\.mar-|\bMarilo\b' \
  packages/ui-adapters-blazor/wwwroot/js || echo "  (clean)"

echo "=== Provider C# residuals ==="
grep -rEnl '\bMarilo\b|\bIMarilo|\bmar-|\bmarilo-' \
  packages/ui-adapters-blazor/Providers --include='*.cs' || echo "  (clean)"

echo "=== SunfishThemeProvider CSS-var residual ==="
grep -n 'marilo-' packages/ui-adapters-blazor/SunfishThemeProvider.razor || echo "  (clean)"
```

Expected: every section reports "(clean)".

- [ ] **Step 2: Full SCSS rebuild**

```bash
cd "C:/Projects/Sunfish"
npm run scss:build
```

Expected: three CSS files rebuilt, no errors. Bootstrap deprecation warnings (from Bootstrap's own SCSS) are silenced; FluentUI and Material should emit zero warnings.

- [ ] **Step 3: Full solution build and test**

```bash
cd "C:/Projects/Sunfish"
dotnet restore Sunfish.slnx
dotnet build Sunfish.slnx
dotnet test Sunfish.slnx --no-build
```

Expected: 0 errors, 0 warnings. **29 tests passing** (3 foundation + 13 ui-core + 13 blazor adapter — unchanged from end of Phase 3b). Provider projects add no new tests in this phase.

- [ ] **Step 4: Commit any rebuilt CSS artifacts**

If `npm run scss:build` produced CSS diffs that weren't already committed during Tasks 2–4:

```bash
git add packages/ui-adapters-blazor/Providers/*/wwwroot/css
git commit -m "build: recompile provider CSS from --sf-* SCSS trees"
```

- [ ] **Step 5: Push branch**

```bash
git push -u origin feat/migration-phase3c-providers-scss
```

---

## Self-Review Checklist

Source tree:
- [ ] `packages/ui-adapters-blazor/Providers/FluentUI/Sunfish.Providers.FluentUI.csproj` exists, builds, PackageId is `Sunfish.Providers.FluentUI`
- [ ] `packages/ui-adapters-blazor/Providers/Bootstrap/Sunfish.Providers.Bootstrap.csproj` exists, builds
- [ ] `packages/ui-adapters-blazor/Providers/Material/Sunfish.Providers.Material.csproj` exists, builds
- [ ] All three csprojs reference `foundation`, `ui-core`, and `Sunfish.Components.Blazor`

C# rename:
- [ ] No `using Marilo.*;` lines in any provider `.cs` file
- [ ] No `namespace Marilo.*` declarations in any provider `.cs` file
- [ ] No `IMariloCssProvider`, `IMariloIconProvider`, `IMariloJsInterop`, `MariloBuilder`, or `MariloTheme` references
- [ ] All `"mar-*"` CSS class literals inside CssProvider C# strings are now `"sf-*"`
- [ ] Icon sprite paths use `_content/Sunfish.Providers.*/` (FluentUI) or `_content/Sunfish.Icons/` (Bootstrap placeholder) or `_content/Sunfish.Providers.FluentUI/` (Material cross-ref)
- [ ] JS module paths use `_content/Sunfish.Providers.<Name>/js/sunfish-<name>.js`

DI extensions:
- [ ] `AddSunfishFluentUI(this SunfishBuilder, …)` exists in `Sunfish.Providers.FluentUI.Extensions`
- [ ] `AddSunfishBootstrap(this SunfishBuilder, …)` exists in `Sunfish.Providers.Bootstrap.Extensions`
- [ ] `AddSunfishMaterial(this SunfishBuilder, …)` exists in `Sunfish.Providers.Material.Extensions`
- [ ] Each returns `SunfishBuilder` for chaining
- [ ] Each registers `ISunfishCssProvider`, `ISunfishIconProvider`, `ISunfishJsInterop` as scoped

SCSS rename:
- [ ] 0 residual `--marilo-*` tokens in any provider SCSS tree
- [ ] 0 residual `.marilo-*` selectors in any provider SCSS tree
- [ ] 0 residual `.mar-*` selectors in any provider SCSS tree
- [ ] 0 residual bare `marilo-*` mixin/function names in any provider SCSS tree
- [ ] Entrypoint files renamed: `sunfish-fluentui.scss`, `sunfish-bootstrap.scss`, `sunfish-material.scss`
- [ ] `_generated-base.scss` legacy monoliths were **not** copied

SCSS build:
- [ ] Root `package.json` exists with `scss:build:fluentui`, `scss:build:bootstrap`, `scss:build:material`, `scss:build`
- [ ] `node_modules/` is gitignored; `package-lock.json` is committed
- [ ] `npm run scss:build` produces three CSS files in `packages/ui-adapters-blazor/Providers/*/wwwroot/css/`
- [ ] Bootstrap deprecation-silence flags are present (`color-functions`, `import`, `global-builtin`, `if-function`)

JS content rename:
- [ ] 0 residual `.marilo-` / `.mar-` selectors in `packages/ui-adapters-blazor/wwwroot/js/*.js`
- [ ] 0 residual bare `Marilo` identifiers in adapter JS files
- [ ] All 13 adapter JS files touched (selectors and/or identifiers)

Theme provider:
- [ ] `SunfishThemeProvider.razor` contains no `--marilo-*` alias emissions
- [ ] 3 `SunfishThemeProvider` bUnit tests still pass

Solution integration:
- [ ] `Sunfish.slnx` includes all three provider csprojs under a `/providers/` folder
- [ ] `dotnet build Sunfish.slnx` = 0 errors, 0 warnings
- [ ] `dotnet test Sunfish.slnx` = 29 tests passing (baseline from Phase 3b)

Scripts:
- [ ] `scripts/rename-marilo-scss.sh` committed and re-runnable
- [ ] `scripts/rename-marilo-js.sh` committed and re-runnable

Branching:
- [ ] Branch `feat/migration-phase3c-providers-scss` was cut from the post-3b `main`
- [ ] Branch pushed to `origin`

---

## Commit Message Conventions

Follow the Phase 3b pattern — Conventional Commits with scope:

- `feat(providers): migrate FluentUI provider with SCSS rename to --sf-*`
- `feat(providers): migrate Bootstrap provider with SCSS rename to --sf-*`
- `feat(providers): migrate Material provider with SCSS rename to --sf-*`
- `build: add root package.json with dart-sass tooling for providers`
- `refactor(ui-adapters-blazor): rename .mar-/.marilo- selectors and MariloX ids in JS`
- `refactor(ui-adapters-blazor): drop --marilo-* CSS-var aliases from SunfishThemeProvider`
- `build: recompile provider CSS from --sf-* SCSS trees`

---

## Known Follow-ups (out of scope but worth flagging)

1. `packages/ui-adapters-blazor/Internal/Interop/InteropServiceExtensions.cs` still defines `AddMariloInteropServices(...)` — this was migrated as-named in Phase 3b. Rename to `AddSunfishInteropServices` in a follow-up (API-change pipeline).
2. Bootstrap provider's `_content/Sunfish.Icons/icons/sprite.svg` path points to a package that does not yet exist; Phase 3d should create `Sunfish.Icons` (from `Marilo.Icons`).
3. Material provider currently reuses the FluentUI icon sprite via a cross-package `_content` path — consider a dedicated Material sprite in a later phase.
4. Consider adding a smoke-test fixture in each provider's folder (e.g., `tests/FluentUIProviderTests.cs`) that verifies the DI extension registers the correct types. Out of scope for mechanical rename but low-effort to add in Phase 3d.
