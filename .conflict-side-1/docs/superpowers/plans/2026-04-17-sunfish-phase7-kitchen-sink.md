# Phase 7: Kitchen-Sink Demo Application — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate `C:/Projects/Marilo/samples/Marilo.Demo/` → `C:/Projects/Sunfish/apps/kitchen-sink/` as a runnable Blazor Server application that visually exercises every Sunfish component end-to-end. This is the primary human-review surface for the migration — if the gallery renders correctly in a browser, the adapter is working.

**Architecture:** `apps/kitchen-sink/` is a web-SDK application, NOT a package. It sits outside `packages/` and references the dependency chain via `Sunfish.Components.Blazor` (transitively pulls `Sunfish.UICore` and `Sunfish.Foundation`). Also references the FluentUI provider (default) and — if available — Bootstrap and Material providers for the runtime provider switcher. The app is a consumer of Sunfish, so Sunfish treats it like any downstream project: through public csproj references only.

**Tech Stack:** .NET 10, C# 13, Blazor Server (InteractiveServer render mode), Kestrel, MapLibre GL / Prism.js CDN assets.

---

## Scope

### In Scope

- Copy every demo page, layout, service, and wwwroot asset from `Marilo.Demo` to `apps/kitchen-sink/`.
- Rename `Marilo` → `Sunfish` across all `.razor`, `.cs`, `.json`, `.css`, `.html` files in the new tree.
- Rewrite `Program.cs` to register Sunfish services (`AddSunfish()`, provider, icon sprites).
- Update `App.razor`, `_Imports.razor`, and `MainLayout.razor` with Sunfish namespaces and tokens.
- Register `apps/kitchen-sink/` in `Sunfish.slnx`.
- Add a top-level `apps/README.md` (brief) and update root `README.md` to point at kitchen-sink as the demo entry.
- Provide a human-review checklist covering representative pages from every category.

### Out of Scope

- **Playwright / bUnit tests for the demo app.** Phase 7 is visual/human review only. Automated parity tests are a follow-up phase.
- **WASM hosting.** The demo is Blazor Server only; a WASM hosting variant is explicitly deferred.
- **Alternate providers beyond FluentUI.** If `Sunfish.Providers.Bootstrap` and `Sunfish.Providers.Material` do not exist at execution time (Phase 3c incomplete), the kitchen-sink ships with FluentUI only and the `ProviderSwitcher` is gated behind `#if BOOTSTRAP_PROVIDER` / `#if MATERIAL_PROVIDER` conditional compilation. See Task 6 for the contingency.
- **New demo pages.** Keep parity with Marilo.Demo — no net-new demos in this phase.
- **Migration of `apps/docs`.** That's Phase 8.

---

## Key Decisions

**D-LOCATION:** `apps/kitchen-sink/` at repo root, parallel to `packages/`. The master migration plan and `CLAUDE.md` both describe `apps/` as a deliberate tier (demo + docs apps). The csproj file is `Sunfish.KitchenSink.csproj` to match the `Sunfish.<Area>` naming established in Phases 1–3.

**D-HOSTING-MODEL:** Blazor Server with `InteractiveServer` render mode. Matches Marilo.Demo's model (`builder.Services.AddRazorComponents().AddInteractiveServerComponents()` then `app.MapRazorComponents<App>().AddInteractiveServerRenderMode()`). WASM is out of scope — a WASM variant would require per-component re-evaluation of JS interop (the adapter currently assumes server-side disposal semantics).

**D-PROJECT-REFERENCES:** kitchen-sink references:
- `packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj` (mandatory — pulls Foundation + UICore transitively)
- `packages/providers-fluentui/Sunfish.Providers.FluentUI.csproj` (mandatory — default provider)
- `packages/providers-bootstrap/Sunfish.Providers.Bootstrap.csproj` (optional — only if Phase 3c has landed)
- `packages/providers-material/Sunfish.Providers.Material.csproj` (optional — only if Phase 3c has landed)
- `packages/icons-tabler/Sunfish.Icons.Tabler.csproj` (if Phase 3d has landed; else fall back to provider-supplied icons)

The app does NOT reference `Sunfish.Foundation.csproj` or `Sunfish.UICore.csproj` directly — they come through the adapter as transitive references. Direct references would be redundant and create maintenance drift.

**D-MIGRATION-APPROACH:** Reuse the sed pattern from `scripts/migrate-marilo-category.sh` (Phase 3b), adapted for an app tree. The Marilo.Demo tree contains razor pages, razor layouts, razor components (Components/), plain C# services, and static assets — structurally very similar to a component category. A new script `scripts/migrate-marilo-demo.sh` drives the copy + sed pass. Key differences from the category script:
- Demo uses `namespace Marilo.Demo.*` not `namespace Marilo.Components.*` → rewrite to `namespace Sunfish.KitchenSink.*`
- Demo references `MariloAppShell`, `MariloNavMenu`, etc. (consumer of components, not definer) — sed must rewrite `<MariloX>` / `</MariloX>` / `MariloX` identifiers throughout.
- `wwwroot/css/app.css` and `Marilo.Demo.styles.css` reference identifiers; update.
- `App.razor` contains link tags with provider stylesheet IDs (`marilo-provider-fluentui`) — renamed to `sunfish-provider-fluentui`.

**D-PROVIDER-DEFAULT:** FluentUI is the default provider (matches Marilo). The first-paint script in `App.razor` defaults to FluentUI stylesheet enabled, Bootstrap/Material disabled. `localStorage` key `marilo:provider` becomes `sunfish:provider`. If Phase 3c has not landed, provider-switching UI is hidden via a compile-time flag; only FluentUI stylesheet link is emitted.

**D-PAGE-RENAME:** Route URLs stay lowercase-matched to Marilo (e.g. `/components/Button/overview`) to keep muscle memory for anyone doing side-by-side comparison. Page titles change: `Marilo Component Gallery` → `Sunfish Component Gallery`. Navbar brand text: `Marilo` → `Sunfish`. Hero copy: rewritten for Sunfish positioning ("Sunfish is a framework-agnostic suite…" borrowed from root README).

**D-KITCHENSINK-TESTS:** No automated tests in this phase. Phase 7 ships a runnable app whose acceptance criterion is "human opens browser, all pages render, no console errors, provider switch works." Playwright visual regression and accessibility audits are deferred to a separate phase once the demo is stable.

---

## File Structure (after Phase 7)

```
apps/
  README.md                                  ← new
  kitchen-sink/
    Sunfish.KitchenSink.csproj               ← replaces Marilo.Demo.csproj
    Program.cs                                ← rewritten: AddSunfish + provider reg
    App.razor                                 ← rewritten: sunfish-provider-* IDs
    Routes.razor, _Imports.razor, appsettings.json
    Layout/ (MainLayout.razor, ComponentDemoLayout.razor)
    Pages/
      Home.razor                              ← Sunfish hero copy
      IconsPage.razor (+ .css)
      Components/
        ComponentsLandingPage.razor
        Button/Overview.razor
        DataGrid/{Overview,Appearance,Events,Accessibility}.razor
        … (122 subfolders, ~137 razor pages total)
    Components/ (DemoSection, PageSection, FavoriteToggle, …)
    Services/ (FavoritesService.cs, ProviderSwitcher.cs)
    Data/ (ComponentRegistry.cs, SiteLinks.cs, ThemePresets.cs)
    wwwroot/ (css/app.css, js/{favorites,provider-switcher}.js,
              icons/sunfish-icons.json, map-data/, map-styles/, demo.pdf)

scripts/migrate-marilo-demo.sh                ← new (demo-tree variant of category script)
```

Also updated: `Sunfish.slnx`, `README.md` (root), new `apps/README.md`.

---

## Prerequisites

Before starting Phase 7:

- [ ] Phase 3b complete: all 14 component categories migrated in `packages/ui-adapters-blazor/Components/`
- [ ] Phase 3c complete or partially complete: at minimum, `Sunfish.Providers.FluentUI` exists and builds. Bootstrap/Material optional.
- [ ] Phase 3d complete or deferred: if incomplete, the demo uses FluentUI's bundled icons (no standalone Tabler icons package).
- [ ] `dotnet build Sunfish.slnx` passes at HEAD of main.

If any of the above are incomplete, document the gap in Task 6 (provider registration) and degrade gracefully — the kitchen-sink should still run with whatever providers DO exist.

---

## Task 1: Create branch and scaffold `apps/kitchen-sink/`

- [ ] **Step 1: Create the feature branch and apps/ folder**

```bash
cd "C:/Projects/Sunfish"
git checkout -b feat/migration-phase7-kitchen-sink
mkdir -p "apps/kitchen-sink"
```

- [ ] **Step 2: Add `apps/README.md`**

```markdown
# Sunfish Apps

Runnable applications that exercise the Sunfish packages.

| App | Purpose | Tech |
|---|---|---|
| `kitchen-sink/` | Component gallery — every Sunfish component on a page | Blazor Server |
| `docs/` | Documentation site (Phase 8) | DocFX |

Apps are NOT part of the published NuGet graph. They depend on `packages/` through project references.
```

- [ ] **Step 3: Commit**

```bash
git add "apps/README.md"
git commit -m "chore(apps): scaffold apps tier with kitchen-sink placeholder"
```

---

## Task 2: Create the demo-tree migration script

**File:** `scripts/migrate-marilo-demo.sh` (demo-tree variant of `migrate-marilo-category.sh` — uses `Sunfish.KitchenSink` namespace and rewrites `<MariloX>` consumer tags in addition to `@inherits`).

- [ ] **Step 1: Write the script**

```bash
#!/usr/bin/env bash
# Usage: scripts/migrate-marilo-demo.sh
# Prerequisites: SUNFISH and MARILO env vars exported.
# Copies Marilo.Demo -> apps/kitchen-sink and applies the Sunfish rename pass.

set -euo pipefail

SRC="$MARILO/samples/Marilo.Demo"
DST="$SUNFISH/apps/kitchen-sink"

[ -d "$SRC" ] || { echo "FAIL: Marilo.Demo not found: $SRC"; exit 1; }

# Do NOT fail if DST exists — only fail if already populated beyond README
if [ -d "$DST" ] && [ "$(ls -A "$DST" 2>/dev/null | grep -v '^README' | wc -l)" -gt 0 ]; then
  echo "FAIL: $DST already populated (migration re-run?)"
  exit 1
fi

echo "→ Copying Marilo.Demo → apps/kitchen-sink"
mkdir -p "$DST"
# Copy everything except bin, obj, and csproj (we'll write the csproj fresh)
rsync -a \
  --exclude="bin/" --exclude="obj/" --exclude="Marilo.Demo.csproj" \
  "$SRC/" "$DST/"

echo "→ Removing backup/scratch files"
find "$DST" -type f \( -name "*.bak" -o -name "*.orig" -o -name "*~" \) -delete

echo "→ Renaming Marilo-prefixed files (Marilo.Demo.styles.css etc.)"
find "$DST" -type f -name "Marilo*" | while read -r f; do
  new="$(dirname "$f")/$(basename "$f" | sed 's/^Marilo/Sunfish/')"
  mv "$f" "$new"
done

echo "→ Renaming wwwroot/icons/marilo-icons.json"
if [ -f "$DST/wwwroot/icons/marilo-icons.json" ]; then
  mv "$DST/wwwroot/icons/marilo-icons.json" "$DST/wwwroot/icons/sunfish-icons.json"
fi

echo "→ Rewriting content (sed pass — code & razor)"
find "$DST" -type f \( -name "*.razor" -o -name "*.cs" -o -name "*.razor.cs" -o -name "*.json" \) -exec sed -i \
  -e 's/\bIMarilo/ISunfish/g' \
  -e 's/\bMarilo\.Demo\b/Sunfish.KitchenSink/g' \
  -e 's/\bMarilo\.Core\.Contracts/Sunfish.UICore.Contracts/g' \
  -e 's/\bMarilo\.Core\./Sunfish.Foundation./g' \
  -e 's/\bMarilo\.Components\.Internal\b/Sunfish.Components.Blazor.Internal/g' \
  -e 's/\bMarilo\.Components\./Sunfish.Components.Blazor.Components./g' \
  -e 's/\bMarilo\.Components\b/Sunfish.Components.Blazor/g' \
  -e 's/\bMarilo\.Providers\.FluentUI/Sunfish.Providers.FluentUI/g' \
  -e 's/\bMarilo\.Providers\.Bootstrap/Sunfish.Providers.Bootstrap/g' \
  -e 's/\bMarilo\.Providers\.Material/Sunfish.Providers.Material/g' \
  -e 's/@inherits MariloComponentBase/@inherits SunfishComponentBase/g' \
  -e 's/\bMariloResizeEdges\b/ResizeEdges/g' \
  -e 's/\bMarilo/Sunfish/g' \
  -e 's/class="mar-/class="sf-/g' \
  -e 's/class="marilo-/class="sf-/g' \
  -e "s/class='mar-/class='sf-/g" \
  -e "s/class='marilo-/class='sf-/g" \
  -e "s/'marilo:/'sunfish:/g" \
  -e 's/"marilo:/"sunfish:/g' \
  -e 's/marilo-provider-/sunfish-provider-/g' \
  -e 's/_content\/Marilo\.Providers\./_content\/Sunfish.Providers./g' \
  -e 's/css\/marilo-fluentui\.css/css\/sunfish-fluentui.css/g' \
  -e 's/css\/marilo-bootstrap\.css/css\/sunfish-bootstrap.css/g' \
  -e 's/css\/marilo-material\.css/css\/sunfish-material.css/g' \
  {} \;

echo "→ Rewriting CSS and HTML files"
find "$DST" -type f \( -name "*.css" -o -name "*.html" -o -name "*.cshtml" \) -exec sed -i \
  -e 's/\bMarilo Demo\b/Sunfish Kitchen Sink/g' \
  -e 's/\bMarilo\b/Sunfish/g' \
  -e 's/class="mar-/class="sf-/g' \
  -e 's/class="marilo-/class="sf-/g' \
  {} \;

echo "→ Rewriting JS storage keys and identifiers"
find "$DST/wwwroot/js" -type f -name "*.js" -exec sed -i \
  -e "s/'marilo:/'sunfish:/g" \
  -e 's/"marilo:/"sunfish:/g' \
  -e 's/marilo-provider-/sunfish-provider-/g' \
  -e 's/window\.Marilo\b/window.Sunfish/g' \
  -e 's/\bMarilo\b/Sunfish/g' \
  {} \;

echo "→ Grepping for leftover Marilo references (informational)"
if grep -rE '\bMarilo[A-Za-z]|\bMarilo\.' "$DST" 2>/dev/null | grep -v "^Binary" | head -20; then
  echo "WARN: leftover 'Marilo' identifiers remain (review manually)"
fi

echo "OK: Marilo.Demo migration complete → $DST"
```

Note: the script is **informational-warn** on leftover `Marilo` references (app-tree prose may legitimately reference Marilo in comments). Review output manually.

- [ ] **Step 2: Make the script executable and commit**

```bash
chmod +x "C:/Projects/Sunfish/scripts/migrate-marilo-demo.sh"
git add "scripts/migrate-marilo-demo.sh"
git commit -m "chore(scripts): add migrate-marilo-demo.sh for Phase 7 app-tree migration"
```

---

## Task 3: Run the migration script

- [ ] **Step 1:** Run the script:

```bash
export MARILO="C:/Projects/Marilo"
export SUNFISH="C:/Projects/Sunfish"
bash "$SUNFISH/scripts/migrate-marilo-demo.sh"
```

Expected: "OK: Marilo.Demo migration complete → …". WARN lines for prose/comments are acceptable.

- [ ] **Step 2:** Verify counts:

```bash
find apps/kitchen-sink -name "*.razor" | wc -l              # expect ~160
find apps/kitchen-sink -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*" | wc -l   # expect ~7
ls apps/kitchen-sink/Pages/Components | wc -l               # expect 122
```

- [ ] **Step 3:** Content spot-check:

```bash
grep -l "Marilo" apps/kitchen-sink/Services/ProviderSwitcher.cs && echo WARN || echo OK
grep -c "SunfishButton" apps/kitchen-sink/Pages/Components/Button/Overview.razor   # expect 10+
```

- [ ] **Step 4:** Do NOT commit yet — the project won't build until Task 4 writes the csproj.

---

## Task 4: Write `Sunfish.KitchenSink.csproj`

**Files:**
- Create: `C:/Projects/Sunfish/apps/kitchen-sink/Sunfish.KitchenSink.csproj`

- [ ] **Step 1: Write the csproj**

```xml
<!-- apps/kitchen-sink/Sunfish.KitchenSink.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <RootNamespace>Sunfish.KitchenSink</RootNamespace>
    <AssemblyName>Sunfish.KitchenSink</AssemblyName>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\packages\ui-adapters-blazor\Sunfish.Components.Blazor.csproj" />
    <ProjectReference Include="..\..\packages\providers-fluentui\Sunfish.Providers.FluentUI.csproj" />
    <!-- Conditional: only if Phase 3c/Bootstrap provider exists -->
    <ProjectReference Include="..\..\packages\providers-bootstrap\Sunfish.Providers.Bootstrap.csproj"
                      Condition="Exists('..\..\packages\providers-bootstrap\Sunfish.Providers.Bootstrap.csproj')" />
    <ProjectReference Include="..\..\packages\providers-material\Sunfish.Providers.Material.csproj"
                      Condition="Exists('..\..\packages\providers-material\Sunfish.Providers.Material.csproj')" />
  </ItemGroup>

</Project>
```

Note: `Exists(...)` conditions on `ProjectReference` let the kitchen-sink build against whatever provider packages exist. If Bootstrap/Material land later (Phase 3c finishes), re-running `dotnet restore` picks them up automatically. `Nullable`/`ImplicitUsings` are explicit (not only in Directory.Build.props) to match Phase 3a convention.

- [ ] **Step 2: Try a baseline restore**

```bash
cd "C:/Projects/Sunfish"
dotnet restore apps/kitchen-sink/Sunfish.KitchenSink.csproj
```

Expected: restore succeeds. If the FluentUI provider doesn't exist yet, restore fails — in that case pause and ensure Phase 3c (at least the FluentUI provider package) is complete before proceeding.

---

## Task 5: Rewrite `Program.cs`

The sed pass leaves a mostly-correct file, but DI registration needs a full rewrite: Marilo's `AddMarilo().AddMariloCoreServices().AddMariloInteropServices()` triple collapsed into a single `AddSunfish()` in Phase 1, and missing-provider behavior needs explicit handling.

- [ ] **Step 1: Replace `Program.cs` content**

Create `C:/Projects/Sunfish/apps/kitchen-sink/Program.cs` with this structure (full content — all braces required):

```csharp
using Sunfish.UICore.Contracts;
using Sunfish.Foundation.Extensions;
using Sunfish.KitchenSink.Data;
using Sunfish.KitchenSink.Services;
using Sunfish.Providers.FluentUI;
using Sunfish.Providers.FluentUI.Extensions;
#if BOOTSTRAP_PROVIDER
using Sunfish.Providers.Bootstrap;
using Sunfish.Providers.Bootstrap.Extensions;
#endif
#if MATERIAL_PROVIDER
using Sunfish.Providers.Material;
using Sunfish.Providers.Material.Extensions;
#endif

var builder = WebApplication.CreateBuilder(args);

// Preserve Marilo.Demo's container-HTTP-fallback logic verbatim
// (copy the useHttpOnly + builder.WebHost.UseUrls block from the sed-rewritten file).

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Single entry point (replaces AddMarilo + AddMariloCoreServices + AddMariloInteropServices):
builder.Services.AddSunfish();

// FluentUI (mandatory)
builder.Services.AddSingleton<FluentUIOptions>();
builder.Services.AddScoped<FluentUICssProvider>();
builder.Services.AddScoped<FluentUIIconProvider>();
builder.Services.AddScoped<FluentUIJsInterop>();

#if BOOTSTRAP_PROVIDER
builder.Services.AddSingleton<BootstrapOptions>();
builder.Services.AddScoped<BootstrapCssProvider>();
builder.Services.AddScoped<BootstrapIconProvider>();
builder.Services.AddScoped<BootstrapJsInterop>();
#endif
#if MATERIAL_PROVIDER
builder.Services.AddSingleton<MaterialOptions>();
builder.Services.AddScoped<MaterialCssProvider>();
builder.Services.AddScoped<MaterialIconProvider>();
builder.Services.AddScoped<MaterialJsInterop>();
#endif

// Switcher as live CSS/Icon/JS provider
builder.Services.AddScoped<ProviderSwitcher>();
builder.Services.AddScoped<ISunfishCssProvider>(sp => sp.GetRequiredService<ProviderSwitcher>());
builder.Services.AddScoped<ISunfishIconProvider>(sp => sp.GetRequiredService<ProviderSwitcher>());
builder.Services.AddScoped<ISunfishJsInterop>(sp => sp.GetRequiredService<ProviderSwitcher>());

builder.Services.AddScoped<FavoritesService>();

// Preserve the SiteLinks binding block from the sed-rewritten file.

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHsts();
if (!useHttpOnly) app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<Sunfish.KitchenSink.App>().AddInteractiveServerRenderMode();
app.Run();
```

Key changes vs. Marilo.Demo Program.cs:
- `AddMarilo()*3` → single `AddSunfish()`
- Bootstrap/Material gated by `#if` symbols (auto-flipped via `<DefineConstants>` — see Step 2)
- App type: `Marilo.Demo.App` → `Sunfish.KitchenSink.App`
- `using Marilo.Components.Internal.Interop` removed (not needed in Program.cs)

- [ ] **Step 2: Add conditional `<DefineConstants>` to the csproj**

In `Sunfish.KitchenSink.csproj` `<PropertyGroup>`:

```xml
<DefineConstants Condition="Exists('..\..\packages\providers-bootstrap\Sunfish.Providers.Bootstrap.csproj')">$(DefineConstants);BOOTSTRAP_PROVIDER</DefineConstants>
<DefineConstants Condition="Exists('..\..\packages\providers-material\Sunfish.Providers.Material.csproj')">$(DefineConstants);MATERIAL_PROVIDER</DefineConstants>
```

Preprocessor symbols flip automatically when the provider packages land — no code change needed.

---

## Task 6: Fix up `ProviderSwitcher.cs` for conditional providers

**Files:**
- Edit: `C:/Projects/Sunfish/apps/kitchen-sink/Services/ProviderSwitcher.cs`

The sed pass renamed types correctly, but the constructor still takes three providers and will throw if Bootstrap/Material aren't registered. Wrap those in `#if` blocks to match Program.cs.

- [ ] **Step 1: Read the sed-pass output to confirm state**

```bash
head -50 "C:/Projects/Sunfish/apps/kitchen-sink/Services/ProviderSwitcher.cs"
```

Expected: class `ProviderSwitcher : ISunfishCssProvider, ISunfishIconProvider, ISunfishJsInterop`. Constructor references `FluentUICssProvider`, `BootstrapCssProvider`, `MaterialCssProvider`.

- [ ] **Step 2: Wrap Bootstrap/Material fields, constructor params, and switch arms in `#if`**

Use Read + Edit tools to wrap:
- The `_bootstrap*` fields in `#if BOOTSTRAP_PROVIDER` / `#endif`
- The `_material*` fields in `#if MATERIAL_PROVIDER` / `#endif`
- The corresponding constructor parameters (and assignments)
- The `DesignProvider.Bootstrap` and `DesignProvider.Material` switch arms (fall through to FluentUI when the provider is absent)

The `DesignProvider` enum itself keeps all three values — the switch-arm change means picking Bootstrap at runtime when BOOTSTRAP_PROVIDER isn't defined resolves to FluentUI (safe degradation). The MainLayout provider-picker UI will still show Bootstrap/Material options, but clicking them will quietly stay on FluentUI. Acceptable for Phase 7; Task 7 optionally hides the UI.

- [ ] **Step 3: Build the kitchen-sink**

```bash
cd "C:/Projects/Sunfish"
dotnet build apps/kitchen-sink/Sunfish.KitchenSink.csproj
```

Expected: 0 errors, possibly warnings about unreachable code if Bootstrap/Material providers are absent. Fix any compile errors iteratively.

---

## Task 7: Fix up `MainLayout.razor` provider picker

**File:** `apps/kitchen-sink/Layout/MainLayout.razor`

- [ ] **Step 1:** Confirm navbar brand `<span>` reads `Sunfish` (sed should have done this — verify).
- [ ] **Step 2:** Add compile-time flags to `@code`:

```csharp
private const bool BootstrapAvailable =
#if BOOTSTRAP_PROVIDER
    true;
#else
    false;
#endif
private const bool MaterialAvailable =
#if MATERIAL_PROVIDER
    true;
#else
    false;
#endif
```

- [ ] **Step 3:** Wrap Bootstrap and Material provider-picker `<button>` entries with `@if (BootstrapAvailable) { … }` / `@if (MaterialAvailable) { … }`.
- [ ] **Step 4:** Change footer text to `Sunfish Kitchen Sink · @DateTime.UtcNow.Year · see LICENSE`.

---

## Task 8: Update `App.razor` provider stylesheet wiring

**File:** `apps/kitchen-sink/App.razor`

Verify the sed pass produced these four changes; fix manually if not:

- [ ] **Step 1:** Provider link tags use `id="sunfish-provider-fluentui"` / `sunfish-provider-bootstrap` / `sunfish-provider-material` with `href="_content/Sunfish.Providers.{FluentUI|Bootstrap|Material}/css/sunfish-{fluentui|bootstrap|material}.css"`.
- [ ] **Step 2:** First-paint inline script reads `localStorage.getItem('sunfish:provider')` and toggles the `sunfish-provider-*` element IDs.
- [ ] **Step 3:** Page title is `<title>Sunfish Component Gallery</title>`.
- [ ] **Step 4:** Global JS namespace is `window.Sunfish` (not `window.Marilo`).

Leave Bootstrap/Material `<link>` tags in place with `disabled` even when those providers aren't compiled in — a 404 on a `disabled` stylesheet does not break the page.

---

## Task 9: Update `_Imports.razor`

**Files:**
- Edit: `C:/Projects/Sunfish/apps/kitchen-sink/_Imports.razor`

- [ ] **Step 1: Rewrite to use Sunfish namespaces**

```razor
@using System.Net.Http
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.JSInterop
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@using Sunfish.Foundation.Base
@using Sunfish.Foundation.Configuration
@using Sunfish.Foundation.Enums
@using Sunfish.Foundation.Models
@using Sunfish.Foundation.Services
@using Sunfish.UICore.Contracts
@using Sunfish.Components.Blazor
@using Sunfish.Components.Blazor.Base
@using Sunfish.Components.Blazor.Components.Buttons
@using Sunfish.Components.Blazor.Components.Charts
@using Sunfish.Components.Blazor.Components.DataDisplay
@using Sunfish.Components.Blazor.Components.DataDisplay.Scheduler
@using Sunfish.Components.Blazor.Components.DataGrid
@using Sunfish.Components.Blazor.Components.Editors
@using Sunfish.Components.Blazor.Components.Feedback
@using Sunfish.Components.Blazor.Components.Forms.Containers
@using Sunfish.Components.Blazor.Components.Forms.Inputs
@using Sunfish.Components.Blazor.Components.Layout
@using Sunfish.Components.Blazor.Components.Navigation
@using Sunfish.Components.Blazor.Components.Overlays
@using Sunfish.Components.Blazor.Components.Utility
@using Sunfish.KitchenSink
@using Sunfish.KitchenSink.Data
@using Sunfish.KitchenSink.Services
@using Sunfish.KitchenSink.Layout
@using Sunfish.KitchenSink.Pages
@using Sunfish.KitchenSink.Components
```

- [ ] **Step 2: Build the project**

```bash
cd "C:/Projects/Sunfish"
dotnet build apps/kitchen-sink/Sunfish.KitchenSink.csproj 2>&1 | tail -20
```

Expected: 0 errors. Warnings about unused usings are OK. Fix any missing-namespace compile errors by removing the offending `@using` line.

---

## Task 10: Update `Home.razor` hero copy

**File:** `apps/kitchen-sink/Pages/Home.razor`

- [ ] **Step 1:** Replace hero H1 + P with Sunfish text:

```razor
<div class="landing-hero">
    <h1>Sunfish Component Gallery</h1>
    <p>Sunfish is a framework-agnostic suite of open-source and commercial building blocks
       that helps scaffold, prototype, and ship real-world applications with interchangeable
       UI and domain components. This gallery renders every component in the Blazor adapter;
       use the theme picker to switch providers.</p>
</div>
```

- [ ] **Step 2:** Scan the rest of the file for residual `Marilo` identifiers; fix manually.

---

## Task 11: Update `ComponentRegistry.cs` API paths

**File:** `apps/kitchen-sink/Data/ComponentRegistry.cs`

Marilo's `Api()` helper built `/api/Marilo.Components.Buttons.MariloButton.html`. Sunfish equivalents are `/api/Sunfish.Components.Blazor.Buttons.SunfishButton.html`.

- [ ] **Step 1:** Fix the helpers:

```csharp
private static string Api(string ns, string name) => $"/api/Sunfish.Components.Blazor.{ns}.Sunfish{name}.html";
private static string ApiNs(string ns) => $"/api/Sunfish.Components.Blazor.{ns}.html";
```

- [ ] **Step 2:** Spot-check a handful of `ComponentInfo` entries across categories for residual `Marilo` strings.

Note: `apps/docs` is Phase 8, so these URLs will 404 in Phase 7 — that's expected.

---

## Task 12: Register the project in `Sunfish.slnx`

**Files:**
- Edit: `C:/Projects/Sunfish/Sunfish.slnx`

- [ ] **Step 1: Add the kitchen-sink project**

```xml
<Solution>
  <Folder Name="/foundation/">
    <Project Path="packages/foundation/Sunfish.Foundation.csproj" />
    <Project Path="packages/foundation/tests/tests.csproj" />
  </Folder>
  <Folder Name="/ui-core/">
    <Project Path="packages/ui-core/Sunfish.UICore.csproj" />
    <Project Path="packages/ui-core/tests/tests.csproj" />
  </Folder>
  <Folder Name="/ui-adapters-blazor/">
    <Project Path="packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj" />
    <Project Path="packages/ui-adapters-blazor/tests/tests.csproj" />
  </Folder>
  <Folder Name="/apps/">
    <Project Path="apps/kitchen-sink/Sunfish.KitchenSink.csproj" />
  </Folder>
</Solution>
```

If Phase 3c providers exist, also add a `/providers/` folder with their csproj entries.

- [ ] **Step 2: Full-solution build**

```bash
cd "C:/Projects/Sunfish"
dotnet build Sunfish.slnx 2>&1 | tail -15
```

Expected: 0 errors. Warnings may include missing CSS content files from not-yet-migrated providers; investigate per warning.

---

## Task 13: Update root README.md

**File:** `README.md` (root)

- [ ] After the "Repository layout" section, add:

```markdown
## Try the Kitchen Sink

The fastest way to see Sunfish in action:

~~~bash
dotnet run --project apps/kitchen-sink
~~~

Open https://localhost:5301 and browse the sidebar. Every Sunfish Blazor component has a demo page. The theme picker (top-right) switches providers and dark/light mode.
```

---

## Task 14: Run the app locally and smoke-test

- [ ] **Step 1:** Launch:

```bash
cd "C:/Projects/Sunfish"
dotnet run --project apps/kitchen-sink/Sunfish.KitchenSink.csproj
```

Expected: Kestrel binds to `https://localhost:5301`, no startup exceptions.

- [ ] **Step 2:** Walk these URLs in order; each should render without console errors:

1. `/` — landing, "Sunfish Component Gallery" title, Quick Preview buttons/chips/badges
2. `/components` — category cards
3. `/components/Button/overview` — 6 button variants visible
4. `/components/Button/appearance` — fill modes (solid/outline/flat/link/clear)
5. `/components/DataGrid/overview` — grid with sample data
6. `/components/DataGrid/events` — sort/filter/paginate respond
7. `/components/Dialog/overview` — modal opens with backdrop
8. `/components/Calendar/overview` — month view renders
9. `/components/Map/overview` — MapLibre tiles load (needs internet)
10. `/components/Chart/overview` — SVG chart renders
11. `/components/Form/overview` — fields align, validation on blur
12. `/components/Notification/overview` — toasts animate
13. `/icons` — Tabler icon grid (if icons package exists)

- [ ] **Step 3:** DevTools console: no red errors. MapLibre CDN warnings OK if offline; `/api/…` 404s expected (Phase 8).
- [ ] **Step 4:** Theme picker: switch FluentUI → Bootstrap → Material (or whichever are compiled in). Page reflows; reload persists (localStorage `sunfish:provider`).
- [ ] **Step 5:** Dark mode: toggle moon icon; background inverts; reload persists.
- [ ] **Step 6:** Ctrl-C, commit:

```bash
git add apps/ scripts/migrate-marilo-demo.sh Sunfish.slnx README.md
git commit -m "feat(kitchen-sink): migrate Marilo.Demo to apps/kitchen-sink with Sunfish branding"
```

---

## Task 15: Push and verify CI

- [ ] **Step 1: Push the branch**

```bash
git push origin feat/migration-phase7-kitchen-sink
```

- [ ] **Step 2: Verify CI passes**

CI should run `dotnet build Sunfish.slnx` and `dotnet test Sunfish.slnx`. No new tests were added in Phase 7, so test counts match Phase 3b (baseline + whatever accumulated through 3c/3d).

---

## Self-Review Checklist

### Build & Structure

- [ ] `Sunfish.KitchenSink.csproj` uses `Microsoft.NET.Sdk.Web` (not `Microsoft.NET.Sdk` or Razor SDK)
- [ ] Root namespace is `Sunfish.KitchenSink`
- [ ] `apps/kitchen-sink/` contains: `Program.cs`, `App.razor`, `_Imports.razor`, `Routes.razor`, `Layout/`, `Pages/`, `Components/`, `Services/`, `Data/`, `wwwroot/`
- [ ] `ProjectReference` to `Sunfish.Components.Blazor.csproj` (mandatory)
- [ ] `ProjectReference` to `Sunfish.Providers.FluentUI.csproj` (mandatory)
- [ ] NO direct reference to `Sunfish.Foundation.csproj` or `Sunfish.UICore.csproj` (transitive only)
- [ ] `Sunfish.slnx` registers the kitchen-sink under `/apps/` folder
- [ ] `dotnet build Sunfish.slnx` = 0 errors
- [ ] `dotnet build apps/kitchen-sink/Sunfish.KitchenSink.csproj` = 0 errors

### Content Cleanliness

- [ ] `grep -r "Marilo" apps/kitchen-sink/ --include="*.cs" --include="*.razor"` returns zero non-comment hits
- [ ] `grep -r "MariloButton\|MariloChip\|MariloIcon" apps/kitchen-sink/` returns zero hits
- [ ] `grep -r "marilo-" apps/kitchen-sink/wwwroot/` returns zero hits
- [ ] `grep -r "marilo:" apps/kitchen-sink/wwwroot/js/` returns zero hits
- [ ] `Pages/Home.razor` has `<h1>Sunfish Component Gallery</h1>`
- [ ] `App.razor` has `<title>Sunfish Component Gallery</title>`
- [ ] `Layout/MainLayout.razor` brand text is `Sunfish`
- [ ] `App.razor` link tags use `sunfish-provider-fluentui` / `sunfish-provider-bootstrap` / `sunfish-provider-material`
- [ ] First-paint script reads `localStorage.getItem('sunfish:provider')`
- [ ] `window.Sunfish` global (not `window.Marilo`)

### Runtime Verification (human)

- [ ] `dotnet run --project apps/kitchen-sink` starts cleanly on port 5301 (or the launchSettings port)
- [ ] Landing page (`/`) renders; Quick Preview shows 6 button variants, 3 chips, 3 badges
- [ ] Left-sidebar navigation lists all 14 component categories
- [ ] `/components/Button/overview` renders all button appearance demos (variants, sizes, fill modes, rounded, icons)
- [ ] `/components/DataGrid/overview` renders a sortable table
- [ ] `/components/DataGrid/events` event handlers respond (click a sort header, rows re-order)
- [ ] `/components/Dialog/overview` opens a modal dialog with backdrop
- [ ] `/components/Form/overview` shows labels + inputs + validation messages
- [ ] `/components/Chart/overview` renders an SVG chart
- [ ] `/components/Map/overview` loads MapLibre tiles (internet required)
- [ ] Theme picker (top-right) opens dropdown with theme presets, provider options, and light/dark toggle
- [ ] Switching to Bootstrap provider (if available) updates component appearance
- [ ] Switching to Material provider (if available) updates component appearance
- [ ] Dark mode toggle inverts colors across the page
- [ ] Provider / theme / dark-mode selections persist across page reload
- [ ] Browser DevTools console shows no red errors (yellow warnings for CDN 404s are acceptable)
- [ ] Favorites: click the star icon on a component page, confirm it appears in the "Favorites" sidebar section, reload, confirm persistence

### Parity Spot Checks (optional but recommended)

Run Marilo.Demo side-by-side (on a different port, e.g. `dotnet run --project C:/Projects/Marilo/samples/Marilo.Demo --urls https://localhost:5302`) and eyeball the same pages in both. Look for:

- [ ] Button colors match (primary blue, danger red, etc. — may differ slightly if provider CSS drifted, but should be recognizably close)
- [ ] Icon choice is the same (both should show the Tabler icon set when `Sunfish.Icons.Tabler` is wired up)
- [ ] Layout spacing is comparable (demo-row gaps, page margins)
- [ ] Code samples in `DemoSection` have the same Prism syntax highlighting
- [ ] No components render blank in Sunfish that render filled in Marilo

Note any visible differences in `.wolf/memory.md` — they are candidates for Phase 3b or Phase 3c follow-up fixes.

---

## How to Run Locally

```bash
cd C:/Projects/Sunfish
dotnet run --project apps/kitchen-sink/Sunfish.KitchenSink.csproj
```

Open https://localhost:5301 in a browser (port may vary — check the terminal output for the actual URL printed by Kestrel).

### Useful URLs

| URL | Exercises |
|---|---|
| `/` | Landing page with Quick Preview |
| `/components` | Category cards |
| `/components/Button/overview` | Simplest smoke test |
| `/components/DataGrid/overview` | DataGrid with data, sort, filter |
| `/components/Calendar/overview` | Date-picking (complex interaction) |
| `/components/Map/overview` | MapLibre JS interop |
| `/components/Editor/overview` | Rich text editor (JS interop + contenteditable) |
| `/components/AllocationScheduler/overview` | Most visually distinctive Sunfish-specific component |
| `/icons` | Tabler icon gallery |

### HTTPS / container notes

- Default: `https://localhost:5301` using ASP.NET Core dev cert. Trust it once: `dotnet dev-certs https --trust`.
- Container: set `DOTNET_RUNNING_IN_CONTAINER=true` and the app falls back to `http://0.0.0.0:5301`.

### Troubleshooting

| Symptom | Fix |
|---|---|
| `FileNotFoundException: Sunfish.Providers.FluentUI` at startup | Complete Phase 3c FluentUI provider first, or remove that ProjectReference |
| Page renders but unstyled | Check `App.razor` link tags resolve; `curl _content/Sunfish.Providers.FluentUI/css/sunfish-fluentui.css` |
| Icons as boxes | Check `ISunfishIconProvider.GetIconSpriteUrl()`; confirm wwwroot icons path |
| Provider picker crashes on Bootstrap click | Ensure `#if BOOTSTRAP_PROVIDER` wraps ProviderSwitcher AND `<DefineConstants>` has `BOOTSTRAP_PROVIDER` |
| Console: "Failed to load resource: …marilo-*…" | Sed pass missed a reference — `grep -r "marilo" apps/kitchen-sink` and fix |

---

## Rollback Plan

If Phase 7 needs to be rolled back:

```bash
git checkout main
git branch -D feat/migration-phase7-kitchen-sink
rm -rf apps/kitchen-sink/
# Revert Sunfish.slnx and README.md changes manually (or cherry-pick the revert commit)
```

The rollback is clean because Phase 7 only adds files under `apps/` and scripts/, plus two edits to `Sunfish.slnx` and `README.md`. No package changes.

---

## Exit Criteria

Phase 7 is complete when:

1. `apps/kitchen-sink/Sunfish.KitchenSink.csproj` exists and is in `Sunfish.slnx`
2. `dotnet build Sunfish.slnx` returns 0 errors
3. `dotnet run --project apps/kitchen-sink` starts cleanly
4. All 13 representative pages from the runtime checklist render without console errors
5. Provider picker works for at least FluentUI (more if 3c complete)
6. Dark mode toggle works
7. No "Marilo" identifiers remain in `apps/kitchen-sink/` source or wwwroot
8. README.md and apps/README.md reference kitchen-sink
9. Branch `feat/migration-phase7-kitchen-sink` pushed
10. Changelog / migration notes updated (Phase 8 territory, but note here for handoff)
