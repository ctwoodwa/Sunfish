# Marilo → Sunfish Migration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebrand and migrate all Marilo content (181 Blazor components, 3 design system providers, icon packages, app shell, domain block seeds, demo app, documentation, and the PmDemo Bridge application as the first Sunfish Solution Accelerator) into the Sunfish repository under the Sunfish namespace and package architecture.

**Architecture:** Marilo's 8 source packages map cleanly to Sunfish's layered structure. `Marilo.Core` framework-agnostic content (enums, models, data contracts, service interfaces) becomes `packages/foundation`; Blazor-specific base classes move to `packages/ui-adapters-blazor`. `IMariloCssProvider` and icon/JS interop contracts become `packages/ui-core`. The 181 Razor components become `packages/ui-adapters-blazor`. Domain-specific component orchestration seeds `blocks-forms`, `blocks-tasks`, `blocks-scheduling`, and `blocks-assets`. Telerik-shaped API compatibility patterns move to `packages/compat-telerik`. The kitchen-sink demo migrates to `apps/kitchen-sink`, component specs migrate to `apps/docs`, and the PmDemo full-stack application migrates to a new `accelerators/bridge/` tree as the first Solution Accelerator.

**Tech Stack:** .NET 10, C# 13, Blazor Server + WASM, Razor, SCSS, bUnit 2.7.x, xUnit 2.9.x, Playwright, DocFX; PostgreSQL 16, EF Core, SignalR, Wolverine, RabbitMQ, Redis, Data API Builder, .NET Aspire 13

---

## Scope Check — This is a Multi-Phase Migration

This migration spans 9 independent subsystems. This document is the **master strategy plan**. Each phase should be executed as a separate ICM pipeline run with its own implementation plan.

| Phase | Subsystem | Status | Plan doc |
|-------|-----------|--------|----------|
| 1 | Foundation package (117 files, 3 tests) | ✅ Complete (local branch, unpushed) | *(this doc, Tasks 1–8)* |
| 2 | UI Core contracts (8 files, 13 tests) | ✅ Merged to main (PR #7) | `2026-04-16-sunfish-phase2-ui-core.md` |
| 3a | Blazor adapter infrastructure (6 tests) | ✅ Complete, PR open | `2026-04-16-sunfish-phase3a-blazor-infra.md` |
| 3b | Blazor component migration (329 files, 29 adapter tests) | ✅ Complete, pushed | `2026-04-16-sunfish-phase3b-blazor-components.md` (reconstructed as-shipped) |
| 3c | Providers (FluentUI/Bootstrap/Material) + SCSS/JS rename | 📄 Planned | `2026-04-17-sunfish-phase3c-providers-scss.md` |
| 3d | Icon packages (Tabler + legacy `[Obsolete]`) | 📄 Planned | `2026-04-17-sunfish-phase3d-icons.md` |
| 4 | App Shell (15 files) | 📄 Planned | `2026-04-17-sunfish-phase4-app-shell.md` |
| 5 | Domain blocks (forms, tasks, scheduling, assets — all greenfield) | 📄 Planned | `2026-04-17-sunfish-phase5-domain-blocks.md` |
| 6 | compat-telerik (12 wrappers, policy-gated) | 📄 Planned | `2026-04-17-sunfish-phase6-compat-telerik.md` |
| 7 | Kitchen-sink demo (~160 pages) | 📄 Planned | `2026-04-17-sunfish-phase7-kitchen-sink.md` |
| 8 | Documentation migration (107 specs, DocFX) | 📄 Planned | `2026-04-17-sunfish-phase8-docs.md` |
| 9 | Bridge Solution Accelerator (PmDemo rebrand) | 📄 Planned | `2026-04-17-sunfish-phase9-bridge-accelerator.md` |

**Critical path through complete:** Phase 1 → 2 → 3a → 3b done. 3c and 3d unlock visual completeness. Phases 4–9 depend only on 3 (any sub-phase that provides the needed surface).

**Critical path:** Phase 1 → Phase 2 → Phase 3. All other phases depend only on Phase 3.
Phase 9 can run in parallel with Phases 4–8 once Phase 3 is complete.

---

## Key Decisions (Lock These In Before Coding)

These decisions affect every file in the migration. Resolve them in Phase 1.

### D1: CSS Class Prefix
- Option A: `.sunfish-button` — verbose, unambiguous
- Option B: `.sf-button` — concise, production-friendly
- **Recommendation:** `.sf-` prefix. Consistent with the pattern of large design systems (e.g., Fluent's `fui-`, Chakra's `chakra-`). Shorter in shipped HTML.

### D2: C# Namespace
- `Marilo.*` → `Sunfish.*`
- Package IDs: `Marilo.Core` → `Sunfish.Foundation`, `Marilo.Components` → `Sunfish.Components.Blazor`, etc.
- Full mapping in Phase 1 task list below.

### D3: CSS Provider Interface — Split or Keep Monolithic
- `IMariloCssProvider` has 265 methods across all component categories in one interface.
- Option A: Keep monolithic → one `ISunfishCssProvider` in `ui-core` (simpler to migrate, harder to implement in full)
- Option B: Split by category → `ISunfishButtonCssProvider`, `ISunfishFormCssProvider`, etc. (breaks `IMariloCssProvider` migration but better long-term design)
- **Recommendation:** Keep monolithic for migration, track split as a follow-up `sunfish-api-change` task. Annotate with `// TODO: split by category` comments.

### D4: Version Numbering
- Marilo is at 2.0.0. Sunfish is pre-1.0.
- **Recommendation:** Reset to `0.1.0` for all Sunfish packages. Marilo 2.0.0 represents a private pre-release lineage; Sunfish starts fresh from a public versioning perspective.

### D5: React Adapter
- Marilo is Blazor-only. Sunfish architecture requires Blazor + React parity.
- **Recommendation:** React adapter is out of scope for this migration. Log a `sunfish-gap-analysis` item for React adapter parity.

### D6: Solution Accelerator Directory
- PmDemo is a complete application, not a component library package or demo page.
- Option A: `apps/bridge/` — alongside kitchen-sink and docs
- Option B: `accelerators/bridge/` — top-level directory signalling a distinct tier
- **Recommendation:** `accelerators/bridge/`. Creates a clear "Solution Accelerators" tier consistent with the README architecture description. `apps/` should stay shallow (kitchen-sink, docs only).

### D7: Reusable Patterns to Extract from PmDemo
- The PmDemo notification pipeline, multi-tenant seams, and Settings UI components are independently reusable.
- **Notification pipeline** (`IUserNotificationService`, `UserNotification`, projections): Extract to `packages/foundation` during Phase 9. Other accelerators will reuse it.
- **Multi-tenant context** (`ITenantContext`): Extract to `packages/foundation`. Pattern will recur in every accelerator.
- **Settings UI components** (`SettingsCard`, `SettingsToggleRow`, etc.): Evaluate for promotion to `blocks-settings` after Phase 9 stabilizes.
- **Aspire service defaults**: Keep in each accelerator for now; consider `tooling/aspire-defaults/` if 2+ accelerators duplicate it.

### D8: PmDemo Pending Work (from SETTINGS_STATUS.md)
- Steps 1–2 of the roadmap are complete (notification pipeline, settings shell).
- Steps 3–12 are pending (DAB migrations, full 10-page account section, service layer, test suite).
- **Recommendation:** Migrate the current state as-is. Preserve the SETTINGS_STATUS.md roadmap as `accelerators/bridge/ROADMAP.md` after renaming it. Do not complete the pending steps as part of the migration — that is follow-on work tracked in its own ICM pipeline run.

---

## Package Name Mapping

| Marilo Package | Sunfish Package | Notes |
|---|---|---|
| `Marilo.Core` (framework-agnostic parts) | `packages/foundation` | Enums, models, data contracts, service interfaces, CssClassBuilder, StyleBuilder, theme config |
| `Marilo.Core` (Blazor-specific parts) | `packages/ui-adapters-blazor` | `MariloComponentBase` → `SunfishComponentBase`, SignalR integration |
| *(new)* | `packages/ui-core` | `ISunfishCssProvider`, `ISunfishIconProvider`, `ISunfishJsInterop`, `SunfishBuilder` DI pattern |
| `Marilo.Components` | `packages/ui-adapters-blazor` | 181 components, `_Imports.razor`, `SunfishThemeProvider` |
| `Marilo.Components.Shell` | `packages/ui-adapters-blazor` (Shell subfolder) | App shell components merged into adapter package |
| `Marilo.Icons` | `packages/ui-adapters-blazor` (Icons subfolder) | Legacy icon set; mark as `[Obsolete]` in Sunfish |
| `Marilo.Icons.Tabler` | `packages/ui-adapters-blazor` (Icons subfolder) | Primary icon package; 5,000+ Tabler icons |
| `Marilo.Providers.FluentUI` | `packages/ui-adapters-blazor` (Providers/FluentUI) | Rename CSS prefix `.marilo-` → `.sf-` |
| `Marilo.Providers.Bootstrap` | `packages/ui-adapters-blazor` (Providers/Bootstrap) | Rename CSS prefix |
| `Marilo.Providers.Material` | `packages/ui-adapters-blazor` (Providers/Material) | Rename CSS prefix |
| `samples/Marilo.Demo` | `apps/kitchen-sink` | Rename all component references; update routes/titles |
| `docs/component-specs/` | `apps/docs` | Component specifications, content templates |
| `docfx/` | `apps/docs` (toolchain TBD) | Evaluate whether DocFX stays or is replaced |
| `tests/Marilo.Tests.Unit/` | `packages/ui-adapters-blazor/tests/` | Co-locate tests with source |
| `tests/Marilo.Tests.Integration/` | `tests/` (repo root) | Keep integration tests separate |
| `tests/visual-parity/` | `tests/visual-parity/` | Playwright tests; update selectors for `.sf-` prefix |
| **PmDemo — Solution Accelerator** | | |
| `samples/Marilo.PmDemo/Marilo.PmDemo/` | `accelerators/bridge/Sunfish.Bridge/` | Server: Blazor SSR, SignalR hub, Wolverine handlers, feature flags |
| `samples/Marilo.PmDemo/Marilo.PmDemo.Client/` | `accelerators/bridge/Sunfish.Bridge.Client/` | Client: all pages, layouts, components, notification pipeline, services |
| `samples/Marilo.PmDemo/Marilo.PmDemo.Data/` | `accelerators/bridge/Sunfish.Bridge.Data/` | EF Core DbContext, entities, seeder, migrations, RBAC |
| `samples/Marilo.PmDemo/Marilo.PmDemo.AppHost/` | `accelerators/bridge/Sunfish.Bridge.AppHost/` | Aspire orchestration: PostgreSQL, Redis, RabbitMQ, mock Okta, DAB, migrations |
| `samples/Marilo.PmDemo/Marilo.PmDemo.ServiceDefaults/` | `accelerators/bridge/Sunfish.Bridge.ServiceDefaults/` | Aspire service defaults: OTEL, health checks, resilience |
| `samples/Marilo.PmDemo/Marilo.PmDemo.MigrationService/` | `accelerators/bridge/Sunfish.Bridge.MigrationService/` | One-shot EF Core migration runner |
| `samples/Marilo.PmDemo/MockOktaService/` | `accelerators/bridge/MockOktaService/` | Mock OIDC provider (dev/demo only; annotate for replacement) |
| `samples/Marilo.PmDemo/SETTINGS_STATUS.md` | `accelerators/bridge/ROADMAP.md` | Rename; update brand; preserve pending steps as roadmap items |
| `samples/Marilo.PmDemo/dab-config.json` | `accelerators/bridge/dab-config.json` | Update connection string env var name |

---

## File Structure — Phase 1 (Foundation)

Files to create in `packages/foundation/`:

```
packages/foundation/
  Sunfish.Foundation.csproj
  Extensions/
    ServiceCollectionExtensions.cs    # AddSunfish(), SunfishBuilder pattern
  Enums/
    ButtonVariant.cs
    AlertSeverity.cs
    GridEnums.cs
    LayoutEnums.cs
    FormEnums.cs
    ComponentEnums.cs
    [+13 more enum files]
  Models/
    DataChangeInfo.cs
    DialogOptions.cs
    FileUploadInfo.cs
    NotificationModel.cs
    TreeDragDropEventArgs.cs
    [+25 more model files]
  Configuration/
    SunfishOptions.cs
    SunfishTheme.cs
    SunfishColorPalette.cs
    SunfishShape.cs
    SunfishTypographyScale.cs
  Services/
    ISunfishThemeService.cs
    ISunfishNotificationService.cs
    ISunfishDialogService.cs
    ISunfishOverlayService.cs
    ThemeService.cs
    SunfishNotificationService.cs
    SignalRConnectionRegistry.cs
    ISignalRConnectionRegistry.cs
  Data/
    DataRequest.cs
    DataResult.cs
    FilterDescriptor.cs
    SortDescriptor.cs
    PageState.cs
  BusinessLogic/
    BusinessObjectBase.cs
    BusinessRuleEngine.cs
    IBusinessRule.cs
    AuthorizationEngine.cs
  Base/
    CssClassBuilder.cs               # Renamed from Marilo.Core
    StyleBuilder.cs
  Helpers/
    GridReflectionHelper.cs
```

Files to create in `packages/ui-core/`:

```
packages/ui-core/
  Sunfish.UICore.csproj
  Contracts/
    ISunfishCssProvider.cs           # 265-method interface; renamed from IMariloCssProvider
    ISunfishIconProvider.cs
    ISunfishJsInterop.cs
```

---

## Phase 1: Foundation Package

### Task 1: Repository Infrastructure

**Files:**
- Create: `packages/foundation/Sunfish.Foundation.csproj`
- Create: `Directory.Build.props` (repo root)
- Create: `Directory.Packages.props` (repo root)
- Modify: `.gitignore` (add `bin/`, `obj/`, `_site/`)

- [ ] **Step 1: Create Directory.Build.props**

```xml
<!-- C:\Projects\Sunfish\Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <Authors>Sunfish Contributors</Authors>
    <Company>Sunfish</Company>
    <RepositoryUrl>https://github.com/your-org/sunfish</RepositoryUrl>
    <PackageVersion>0.1.0</PackageVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create Directory.Packages.props**

Copy from `C:\Projects\Marilo\Directory.Packages.props`. Replace all `Marilo` references. Keep the same .NET 10.0.6 package versions.

```xml
<!-- C:\Projects\Sunfish\Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- AspNetCore -->
    <PackageVersion Include="Microsoft.AspNetCore.Components.Web" Version="10.0.6" />
    <PackageVersion Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.6" />
    <!-- Testing -->
    <PackageVersion Include="bunit" Version="2.7.2" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.9.3" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.0" />
    <!-- Utilities -->
    <PackageVersion Include="Markdig" Version="0.40.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create Sunfish.Foundation.csproj**

```xml
<!-- C:\Projects\Sunfish\packages\foundation\Sunfish.Foundation.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Foundation</PackageId>
    <Description>Core contracts, enums, models, and services for Sunfish.</Description>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Verify solution builds with empty project**

```bash
cd C:/Projects/Sunfish
dotnet build packages/foundation/Sunfish.Foundation.csproj
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add Directory.Build.props Directory.Packages.props packages/foundation/Sunfish.Foundation.csproj
git commit -m "feat(foundation): scaffold Sunfish.Foundation project and repo build infrastructure"
```

---

### Task 2: Migrate Enums

**Files:**
- Source: `C:\Projects\Marilo\src\Marilo.Core\Enums\` (18 files)
- Create: `C:\Projects\Sunfish\packages\foundation\Enums\` (18 files)

- [ ] **Step 1: Copy all enum files**

```bash
cp -r "C:/Projects/Marilo/src/Marilo.Core/Enums/." "C:/Projects/Sunfish/packages/foundation/Enums/"
```

- [ ] **Step 2: Replace namespace in all enum files**

```bash
find "C:/Projects/Sunfish/packages/foundation/Enums" -name "*.cs" \
  -exec sed -i 's/namespace Marilo\./namespace Sunfish./g' {} \;
find "C:/Projects/Sunfish/packages/foundation/Enums" -name "*.cs" \
  -exec sed -i 's/using Marilo\./using Sunfish./g' {} \;
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
```

Expected: Build succeeded, no errors.

- [ ] **Step 4: Commit**

```bash
git add packages/foundation/Enums/
git commit -m "feat(foundation): migrate enums from Marilo.Core (ButtonVariant, AlertSeverity, GridEnums, etc.)"
```

---

### Task 3: Migrate Models

**Files:**
- Source: `C:\Projects\Marilo\src\Marilo.Core\Models\` (30+ files)
- Create: `C:\Projects\Sunfish\packages\foundation\Models\`

- [ ] **Step 1: Copy all model files**

```bash
cp -r "C:/Projects/Marilo/src/Marilo.Core/Models/." "C:/Projects/Sunfish/packages/foundation/Models/"
```

- [ ] **Step 2: Replace namespace and using references**

```bash
find "C:/Projects/Sunfish/packages/foundation/Models" -name "*.cs" \
  -exec sed -i 's/namespace Marilo\./namespace Sunfish./g' {} \; \
  -exec sed -i 's/using Marilo\./using Sunfish./g' {} \;
```

- [ ] **Step 3: Build**

```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add packages/foundation/Models/
git commit -m "feat(foundation): migrate event and state models from Marilo.Core"
```

---

### Task 4: Migrate Data Contracts

**Files:**
- Source: `C:\Projects\Marilo\src\Marilo.Core\Data\`
- Create: `C:\Projects\Sunfish\packages\foundation\Data\`

- [ ] **Step 1: Copy data files**

```bash
cp -r "C:/Projects/Marilo/src/Marilo.Core/Data/." "C:/Projects/Sunfish/packages/foundation/Data/"
```

- [ ] **Step 2: Replace namespaces**

```bash
find "C:/Projects/Sunfish/packages/foundation/Data" -name "*.cs" \
  -exec sed -i 's/namespace Marilo\./namespace Sunfish./g' {} \; \
  -exec sed -i 's/using Marilo\./using Sunfish./g' {} \;
```

- [ ] **Step 3: Build**

```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
```

- [ ] **Step 4: Commit**

```bash
git add packages/foundation/Data/
git commit -m "feat(foundation): migrate ORM-agnostic data contracts (DataRequest, FilterDescriptor, etc.)"
```

---

### Task 5: Migrate Configuration and Theme Models

**Files:**
- Source: `C:\Projects\Marilo\src\Marilo.Core\Configuration\`
- Create: `C:\Projects\Sunfish\packages\foundation\Configuration\`
- Rename: `MariloOptions` → `SunfishOptions`, `MariloTheme` → `SunfishTheme`, etc.

- [ ] **Step 1: Copy configuration files**

```bash
cp -r "C:/Projects/Marilo/src/Marilo.Core/Configuration/." "C:/Projects/Sunfish/packages/foundation/Configuration/"
```

- [ ] **Step 2: Replace class names and namespaces**

```bash
find "C:/Projects/Sunfish/packages/foundation/Configuration" -name "*.cs" \
  -exec sed -i 's/Marilo\b/Sunfish/g' {} \;
```

This catches both `namespace Marilo.` and class names like `MariloOptions`, `MariloTheme`, `MariloColorPalette`, `MariloShape`, `MariloTypographyScale`.

- [ ] **Step 3: Build and verify all types resolve**

```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
```

- [ ] **Step 4: Commit**

```bash
git add packages/foundation/Configuration/
git commit -m "feat(foundation): migrate theme configuration models (SunfishOptions, SunfishTheme, SunfishColorPalette)"
```

---

### Task 6: Migrate Services

**Files:**
- Source: `C:\Projects\Marilo\src\Marilo.Core\Services\`
- Create: `C:\Projects\Sunfish\packages\foundation\Services\`

- [ ] **Step 1: Copy service files**

```bash
cp -r "C:/Projects/Marilo/src/Marilo.Core/Services/." "C:/Projects/Sunfish/packages/foundation/Services/"
```

- [ ] **Step 2: Replace all Marilo → Sunfish identifiers**

```bash
find "C:/Projects/Sunfish/packages/foundation/Services" -name "*.cs" \
  -exec sed -i 's/Marilo\b/Sunfish/g' {} \;
```

Verify: `IMariloThemeService` → `ISunfishThemeService`, `MariloNotificationService` → `SunfishNotificationService`, etc.

- [ ] **Step 3: Build**

```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
```

- [ ] **Step 4: Commit**

```bash
git add packages/foundation/Services/
git commit -m "feat(foundation): migrate service contracts and implementations (theme, notification, dialog, overlay)"
```

---

### Task 7: Migrate Helpers, BusinessLogic, and CssClassBuilder

**Files:**
- Source: `C:\Projects\Marilo\src\Marilo.Core\Base\`, `BusinessLogic\`, `Helpers\`
- Create: corresponding folders in `packages/foundation/`

- [ ] **Step 1: Copy remaining Core files**

```bash
cp -r "C:/Projects/Marilo/src/Marilo.Core/Base/." "C:/Projects/Sunfish/packages/foundation/Base/"
cp -r "C:/Projects/Marilo/src/Marilo.Core/BusinessLogic/." "C:/Projects/Sunfish/packages/foundation/BusinessLogic/"
cp -r "C:/Projects/Marilo/src/Marilo.Core/Helpers/." "C:/Projects/Sunfish/packages/foundation/Helpers/"
```

- [ ] **Step 2: Replace namespaces and identifiers**

```bash
find "C:/Projects/Sunfish/packages/foundation" -name "*.cs" \
  -exec sed -i 's/Marilo\b/Sunfish/g' {} \;
```

- [ ] **Step 3: Build clean**

```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
```

Expected: Build succeeded, 0 warnings (XML doc warnings are acceptable at this stage).

- [ ] **Step 4: Commit**

```bash
git add packages/foundation/
git commit -m "feat(foundation): complete foundation package migration — CssClassBuilder, BusinessLogic, Helpers"
```

---

### Task 8: DI Registration — SunfishBuilder

**Files:**
- Source: `C:\Projects\Marilo\src\Marilo.Core\Extensions\ServiceCollectionExtensions.cs`
- Create: `C:\Projects\Sunfish\packages\foundation\Extensions\ServiceCollectionExtensions.cs`

- [ ] **Step 1: Copy and rename**

```bash
cp "C:/Projects/Marilo/src/Marilo.Core/Extensions/ServiceCollectionExtensions.cs" \
   "C:/Projects/Sunfish/packages/foundation/Extensions/ServiceCollectionExtensions.cs"
find "C:/Projects/Sunfish/packages/foundation/Extensions" -name "*.cs" \
  -exec sed -i 's/Marilo\b/Sunfish/g' {} \;
```

Result: `AddMarilo()` → `AddSunfish()`, `MariloBuilder` → `SunfishBuilder`.

- [ ] **Step 2: Build and verify DI extension compiles**

```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
```

- [ ] **Step 3: Write unit test confirming DI registration**

Create `C:\Projects\Sunfish\packages\foundation\tests\ServiceCollectionExtensionsTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Extensions;
using Sunfish.Foundation.Services;
using Xunit;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSunfish_RegistersThemeService()
    {
        var services = new ServiceCollection();
        services.AddSunfish();
        var provider = services.BuildServiceProvider();

        var themeService = provider.GetService<ISunfishThemeService>();

        Assert.NotNull(themeService);
    }
}
```

- [ ] **Step 4: Create test project and run**

```bash
cd C:/Projects/Sunfish
dotnet new xunit -o packages/foundation/tests --framework net10.0
# Add reference to Sunfish.Foundation
dotnet add packages/foundation/tests/tests.csproj reference packages/foundation/Sunfish.Foundation.csproj
cp "C:/Projects/Sunfish/packages/foundation/tests/ServiceCollectionExtensionsTests.cs" ... # move to correct location
dotnet test packages/foundation/tests/
```

Expected: 1 test passed.

- [ ] **Step 5: Commit**

```bash
git add packages/foundation/Extensions/ packages/foundation/tests/
git commit -m "feat(foundation): add SunfishBuilder DI registration with passing test"
```

---

## Phase 2: UI Core Contracts

> **Note:** Write a separate implementation plan for this phase before starting. Input: completed Phase 1. Output: `packages/ui-core/Sunfish.UICore.csproj` with `ISunfishCssProvider`, `ISunfishIconProvider`, `ISunfishJsInterop` fully defined.

### Phase 2 Summary (expand into full plan)

**Tasks:**
1. Create `packages/ui-core/Sunfish.UICore.csproj` (depends on `Sunfish.Foundation`)
2. Copy `IMariloCssProvider.cs` → `ISunfishCssProvider.cs`; rename all `Marilo` → `Sunfish`; add `// TODO: split by category (#phase-2-followup)` comment at top
3. Copy `IMariloIconProvider.cs` → `ISunfishIconProvider.cs`; rename
4. Copy `IMariloJsInterop.cs` → `ISunfishJsInterop.cs`; rename
5. Build `ui-core` clean
6. Write interface-contract unit tests (verify method count, verify expected method signatures)
7. Commit

---

## Phase 3: Blazor Adapter (181 Components + Providers + Icons)

> **Note:** This is the largest phase. Break it into 3 sub-plans:
> - **Phase 3a:** Package setup + SunfishComponentBase + infrastructure
> - **Phase 3b:** Component migration by category (14 categories; one commit per category)
> - **Phase 3c:** Provider migration (FluentUI, Bootstrap, Material) + SCSS rename

### Phase 3 Summary (expand into full plans)

**Phase 3a — Infrastructure:**
1. Create `packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj`
2. Add `_Imports.razor` with `@using Sunfish.*` directives
3. Migrate `MariloComponentBase` → `SunfishComponentBase.razor`
4. Migrate `MariloThemeProvider` → `SunfishThemeProvider.razor`
5. Build clean

**Phase 3b — Component Categories (181 components):**

For each of the 14 categories:
```bash
# Pattern for each category (example: Buttons)
cp -r "C:/Projects/Marilo/src/Marilo.Components/Buttons/." \
      "C:/Projects/Sunfish/packages/ui-adapters-blazor/Components/Buttons/"
find ".../Buttons" -name "*.razor" -o -name "*.cs" \
  | xargs sed -i 's/Marilo\b/Sunfish/g'
# Then: build, bUnit tests, commit
```

Categories and component counts:
| Category | Components |
|---|---|
| Buttons | 11 |
| Charts | 3 + child components |
| DataDisplay | 34 |
| DataGrid | 15 |
| Editors | 2 |
| Feedback | 22 |
| Forms | 43 |
| Layout | 27 |
| Navigation | 19 |
| Overlays | 2 |
| Utility | 3 |

**Phase 3c — Providers:**
1. Copy each provider (`Marilo.Providers.FluentUI`, Bootstrap, Material)
2. Rename namespace/class: `FluentUICssProvider` → `SunfishFluentUICssProvider`
3. Replace SCSS CSS prefix: `.marilo-` → `.sf-`
4. Rebuild SCSS → CSS
5. Run visual parity tests (Playwright) to verify rename didn't break styles
6. Commit per provider

**Phase 3d — Icons:**
1. Copy `Marilo.Icons.Tabler` → `packages/ui-adapters-blazor/Icons/Tabler/`
2. Copy `Marilo.Icons` (legacy) → same location; mark with `[Obsolete]`
3. Rename `AddMariloIconsTabler()` → `AddSunfishIconsTabler()`
4. Build and commit

---

## Phase 4: App Shell

> **Note:** Write a separate implementation plan for this phase. Input: completed Phase 3. Output: shell components living in `packages/ui-adapters-blazor/Shell/`.

**Components to migrate:**
- `MariloAppShell` → `SunfishAppShell`
- `MariloAppShellNavLink`, `MariloAppShellNavGroup`
- `MariloAppShellSlideOver`
- `MariloAccountMenu`, `MariloUserMenu`
- `MariloNotificationBell`

---

## Phase 5: Domain Blocks

> **Note:** One implementation plan per block. Each block plan should: migrate relevant Marilo domain components into the block's package, keep UI components in `ui-adapters-blazor`, extract only domain orchestration logic into the block.

**Block mapping:**

| Sunfish Package | Marilo Domain Components |
|---|---|
| `blocks-forms` | `MariloForm`, `MariloField`, `MariloValidation*`, validation orchestration logic |
| `blocks-tasks` | Task board state machines, task status flows (no dedicated Marilo component found; treat as greenfield using Sunfish components) |
| `blocks-scheduling` | `MariloScheduler` orchestration layer, `MariloAllocationScheduler`, `MariloCalendar` |
| `blocks-assets` | Asset catalog domain logic; no direct Marilo analog — greenfield using DataGrid + FileManager components |

---

## Phase 6: compat-telerik

> **Note:** Write a separate implementation plan after Phase 3 is complete. Source: Marilo's Telerik-shaped API patterns (child registration pattern, `MariloMultiSelect` declarative API, etc.).

**Goal:** `packages/compat-telerik` provides a Telerik API-shaped surface over Sunfish components.
- `TelerikButton` → delegates to `SunfishButton` with mapped parameters
- Map parameter names: Telerik → Sunfish equivalents (document divergences)
- Policy gate: all changes require justification per `compat-telerik` policy

---

## Phase 7: Kitchen Sink Demo

> **Note:** Write a separate implementation plan after Phase 3. Input: completed `ui-adapters-blazor`. Migrate `C:\Projects\Marilo\samples\Marilo.Demo\` → `C:\Projects\Sunfish\apps\kitchen-sink\`.

**Tasks summary:**
1. Create Blazor Server app in `apps/kitchen-sink/`
2. Migrate page structure: `Pages/Components/<Category>/<Component>/`
3. Replace all `<Marilo*>` → `<Sunfish*>` in `.razor` pages
4. Replace `using Marilo.*` → `using Sunfish.*`
5. Update page titles, route names, and nav links
6. Verify all 181 components render in the kitchen sink
7. Run Playwright visual parity tests

---

## Phase 8: Documentation

> **Note:** Write a separate implementation plan after Phase 7. Input: working kitchen sink demo.

**Tasks summary:**
1. Migrate `C:\Projects\Marilo\docs\component-specs\` → `C:\Projects\Sunfish\apps\docs\`
2. Replace all "Marilo" brand mentions with "Sunfish" in spec files
3. Replace component names (`MariloButton` → `SunfishButton`, etc.)
4. Migrate `C:\Projects\Marilo\docfx\` toolchain — evaluate replacing DocFX with alternative (document decision in ICM architecture stage)
5. Migrate `docs/_contentTemplates/` (46 template files)
6. Generate icon browser for Sunfish branding
7. Update `apps/docs` README and getting-started guides

---

## Phase 9: Bridge Solution Accelerator

> **Note:** Write a separate implementation plan for this phase. Input: completed Phase 3 (Sunfish components available). Output: working Bridge application at `accelerators/bridge/` under Sunfish branding with all 14 screens functional.

### Phase 9 File Structure

```
accelerators/
  bridge/
    Sunfish.Bridge.sln                                   # Solution file (renamed)
    dab-config.json                                  # DAB GraphQL config (updated env var names)
    ROADMAP.md                                       # Renamed from SETTINGS_STATUS.md; brand updated
    Sunfish.Bridge/                                      # Server project
      Program.cs
      appsettings.json
      Components/App.razor
      Authorization/DemoTenantContext.cs             # Annotate: DEMO ONLY — replace with real auth
      Features/FeatureFlags.cs
      Handlers/TaskStatusChangedHandler.cs
      Hubs/BridgeHub.cs                                  # Renamed from BridgeHub
      Hubs/IBridgeHubClient.cs
      Messages/Messages.cs
    Sunfish.Bridge.Client/                               # Client library
      _Imports.razor
      Routes.razor
      Layout/MainLayout.razor
      Layout/AccountLayout.razor
      Pages/Home.razor
      Pages/Tasks.razor
      Pages/Board.razor
      Pages/Timeline.razor
      Pages/Risk.razor
      Pages/Budget.razor
      Pages/Team.razor
      Pages/Account/Details.razor
      Pages/Account/Preferences.razor
      Pages/Account/Notifications.razor
      Pages/Account/Personalization.razor
      Pages/Account/Shortcuts.razor
      Pages/Account/Integrations.razor               # Placeholder — "Coming soon"
      Pages/Account/Billing.razor                    # Placeholder
      Pages/Account/Team.razor                       # Placeholder
      Components/Settings/                           # 10 settings wrapper components
      Notifications/                                 # Canonical notification pipeline (see D7)
      Services/ProviderSwitcher.cs
      Data/ThemePresets.cs
    Sunfish.Bridge.Data/                                 # EF Core data layer
      SunfishBridgeDbContext.cs                          # Renamed from BridgeDbContext
      DesignTimeDbContextFactory.cs
      Entities/Entities.cs                           # Project, TaskItem, Subtask, Comment, Milestone, Risk, BudgetLine, AuditRecord
      Authorization/ITenantContext.cs                # See D7 — also promote to foundation
      Authorization/Permissions.cs
      Authorization/Roles.cs
      Seeding/BridgeSeeder.cs                            # Renamed from BridgeSeeder
      Migrations/
    Sunfish.Bridge.AppHost/                              # Aspire orchestration
      Program.cs
    Sunfish.Bridge.ServiceDefaults/
    Sunfish.Bridge.MigrationService/
    MockOktaService/                                 # Annotate: DEMO ONLY
    tests/
      Sunfish.Bridge.Tests.Unit/
      Sunfish.Bridge.Tests.Integration/
      Sunfish.Bridge.Tests.Performance/
```

---

### Task 9-1: Scaffold Accelerator Directory and Solution

**Files:**
- Create: `accelerators/bridge/` (new top-level directory)
- Create: `accelerators/bridge/Sunfish.Bridge.sln`

- [ ] **Step 1: Create the accelerator directory and solution**

```bash
mkdir -p "C:/Projects/Sunfish/accelerators/bridge"
dotnet new sln -n Sunfish.Bridge -o "C:/Projects/Sunfish/accelerators/bridge"
```

- [ ] **Step 2: Copy the full PmDemo tree**

```bash
cp -r "C:/Projects/Marilo/samples/Marilo.PmDemo/." \
      "C:/Projects/Sunfish/accelerators/bridge/"
```

- [ ] **Step 3: Delete the original Marilo solution file**

```bash
rm "C:/Projects/Sunfish/accelerators/bridge/Marilo.PmDemo.slnx"
```

- [ ] **Step 4: Rename project directories**

```bash
cd C:/Projects/Sunfish/accelerators/bridge
mv Marilo.PmDemo        Sunfish.Bridge
mv Marilo.PmDemo.Client Sunfish.Bridge.Client
mv Marilo.PmDemo.Data   Sunfish.Bridge.Data
mv Marilo.PmDemo.AppHost Sunfish.Bridge.AppHost
mv Marilo.PmDemo.ServiceDefaults Sunfish.Bridge.ServiceDefaults
mv Marilo.PmDemo.MigrationService Sunfish.Bridge.MigrationService
mv Marilo.PmDemo.Tests.Unit          tests/Sunfish.Bridge.Tests.Unit
mv Marilo.PmDemo.Tests.Integration   tests/Sunfish.Bridge.Tests.Integration
mv Marilo.PmDemo.Tests.Performance   tests/Sunfish.Bridge.Tests.Performance
mv SETTINGS_STATUS.md ROADMAP.md
```

- [ ] **Step 5: Add all projects to solution**

```bash
dotnet sln Sunfish.Bridge.sln add \
  Sunfish.Bridge/Sunfish.Bridge.csproj \
  Sunfish.Bridge.Client/Sunfish.Bridge.Client.csproj \
  Sunfish.Bridge.Data/Sunfish.Bridge.Data.csproj \
  Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj \
  Sunfish.Bridge.ServiceDefaults/Sunfish.Bridge.ServiceDefaults.csproj \
  Sunfish.Bridge.MigrationService/Sunfish.Bridge.MigrationService.csproj \
  tests/Sunfish.Bridge.Tests.Unit/Sunfish.Bridge.Tests.Unit.csproj \
  tests/Sunfish.Bridge.Tests.Integration/Sunfish.Bridge.Tests.Integration.csproj \
  tests/Sunfish.Bridge.Tests.Performance/Sunfish.Bridge.Tests.Performance.csproj
```

- [ ] **Step 6: Commit scaffolding**

```bash
git add accelerators/
git commit -m "feat(accelerators): scaffold bridge accelerator directory from PmDemo"
```

---

### Task 9-2: Rename All Marilo → Sunfish Identifiers

**Files:** Every `.cs`, `.razor`, `.csproj`, `.json` file in `accelerators/bridge/`

- [ ] **Step 1: Rename .csproj files to match directory names**

```bash
cd C:/Projects/Sunfish/accelerators/bridge
mv Sunfish.Bridge/Marilo.PmDemo.csproj                  Sunfish.Bridge/Sunfish.Bridge.csproj
mv Sunfish.Bridge.Client/Marilo.PmDemo.Client.csproj    Sunfish.Bridge.Client/Sunfish.Bridge.Client.csproj
mv Sunfish.Bridge.Data/Marilo.PmDemo.Data.csproj        Sunfish.Bridge.Data/Sunfish.Bridge.Data.csproj
mv Sunfish.Bridge.AppHost/Marilo.PmDemo.AppHost.csproj  Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj
mv Sunfish.Bridge.ServiceDefaults/Marilo.PmDemo.ServiceDefaults.csproj \
   Sunfish.Bridge.ServiceDefaults/Sunfish.Bridge.ServiceDefaults.csproj
mv Sunfish.Bridge.MigrationService/Marilo.PmDemo.MigrationService.csproj \
   Sunfish.Bridge.MigrationService/Sunfish.Bridge.MigrationService.csproj
```

- [ ] **Step 2: Replace all Marilo/PmDemo text patterns across all files**

```bash
cd C:/Projects/Sunfish/accelerators/bridge

# Namespace and class name substitutions (order matters)
find . -type f \( -name "*.cs" -o -name "*.razor" -o -name "*.csproj" -o -name "*.json" -o -name "*.md" \) \
  -exec sed -i \
    -e 's/Marilo\.PmDemo\./Sunfish.Bridge./g' \
    -e 's/Marilo\.PmDemo/Sunfish.Bridge/g' \
    -e 's/namespace Marilo\./namespace Sunfish./g' \
    -e 's/using Marilo\./using Sunfish./g' \
    -e 's/MariloThemeProvider/SunfishThemeProvider/g' \
    -e 's/MariloAppShell/SunfishAppShell/g' \
    -e 's/MariloAppShellNavGroup/SunfishAppShellNavGroup/g' \
    -e 's/MariloAppShellNavLink/SunfishAppShellNavLink/g' \
    -e 's/MariloAppShellSlideOver/SunfishAppShellSlideOver/g' \
    -e 's/MariloAccountMenu/SunfishAccountMenu/g' \
    -e 's/MariloNotificationBell/SunfishNotificationBell/g' \
    -e 's/MariloSnackbarHost/SunfishSnackbarHost/g' \
    -e 's/MariloDataGrid/SunfishDataGrid/g' \
    -e 's/MariloGridColumn/SunfishGridColumn/g' \
    -e 's/MariloGantt/SunfishGantt/g' \
    -e 's/MariloCard/SunfishCard/g' \
    -e 's/MariloChip/SunfishChip/g' \
    -e 's/MariloButton/SunfishButton/g' \
    -e 's/MariloIcon/SunfishIcon/g' \
    -e 's/MariloSelect/SunfishSelect/g' \
    -e 's/Marilo\b/Sunfish/g' \
    -e 's/marilo-/sf-/g' \
    {} \;
```

- [ ] **Step 3: Rename key internal files**

```bash
cd C:/Projects/Sunfish/accelerators/bridge

# Hub rename
mv Sunfish.Bridge/Hubs/BridgeHub.cs         Sunfish.Bridge/Hubs/BridgeHub.cs
mv Sunfish.Bridge/Hubs/IBridgeHubClient.cs  Sunfish.Bridge/Hubs/IBridgeHubClient.cs

# DbContext rename
mv Sunfish.Bridge.Data/BridgeDbContext.cs         Sunfish.Bridge.Data/SunfishBridgeDbContext.cs
mv Sunfish.Bridge.Data/DesignTimeDbContextFactory.cs Sunfish.Bridge.Data/DesignTimeDbContextFactory.cs  # no rename

# Seeder rename
mv Sunfish.Bridge.Data/Seeding/BridgeSeeder.cs  Sunfish.Bridge.Data/Seeding/BridgeSeeder.cs
```

- [ ] **Step 4: Annotate demo-only auth seams**

In `Sunfish.Bridge/Authorization/DemoTenantContext.cs`, add at the top:

```csharp
// DEMO ONLY: Replace with real tenant resolution (e.g., claims-based from authenticated user).
// See ROADMAP.md §Auth for replacement guidance.
```

In `Sunfish.Bridge.AppHost/Program.cs`, add above the MockOktaService registration:

```csharp
// DEMO ONLY: MockOktaService provides a mock OIDC provider for local development.
// Replace with real Okta/Entra configuration before production deployment.
```

- [ ] **Step 5: Build all projects**

```bash
cd C:/Projects/Sunfish/accelerators/bridge
dotnet build Sunfish.Bridge.sln
```

Expected: Build succeeded. Resolve any remaining `Marilo` references that the sed pass missed (check build output for namespace errors).

- [ ] **Step 6: Commit**

```bash
git add accelerators/bridge/
git commit -m "feat(bridge-accelerator): rebrand all Marilo → Sunfish identifiers and file names"
```

---

### Task 9-3: Update Sunfish Package References

The accelerator depends on Sunfish component packages that will be produced by Phase 3. Until Phase 3 is complete, use project references pointing to the migrated packages in this repo.

**Files:**
- Modify: `Sunfish.Bridge.Client/Sunfish.Bridge.Client.csproj`
- Modify: `Sunfish.Bridge/Sunfish.Bridge.csproj`

- [ ] **Step 1: Replace Marilo NuGet references with Sunfish project references**

In `Sunfish.Bridge.Client/Sunfish.Bridge.Client.csproj`, replace:

```xml
<!-- Before -->
<PackageReference Include="Marilo.Components" />
<PackageReference Include="Marilo.Components.Shell" />
<PackageReference Include="Marilo.Icons.Tabler" />
<PackageReference Include="Marilo.Providers.FluentUI" />
<PackageReference Include="Marilo.Providers.Bootstrap" />
<PackageReference Include="Marilo.Providers.Material" />
```

With:

```xml
<!-- After — project references until packages are published -->
<ProjectReference Include="..\..\..\..\packages\ui-adapters-blazor\Sunfish.Components.Blazor.csproj" />
```

- [ ] **Step 2: Update _Imports.razor usings**

In `Sunfish.Bridge.Client/_Imports.razor`, replace:

```razor
@using Marilo.Components
@using Marilo.Components.Shell
@using Marilo.Core.Enums
@using Marilo.Core.Models
```

With:

```razor
@using Sunfish.Components.Blazor
@using Sunfish.Components.Blazor.Shell
@using Sunfish.Foundation.Enums
@using Sunfish.Foundation.Models
```

- [ ] **Step 3: Build and verify no missing component references**

```bash
dotnet build Sunfish.Bridge.sln
```

Expected: Build succeeded, 0 unresolved component references.

- [ ] **Step 4: Commit**

```bash
git add accelerators/bridge/
git commit -m "feat(bridge-accelerator): wire Sunfish package references (project refs until Phase 3 ships)"
```

---

### Task 9-4: Update DAB Config and Infrastructure

**Files:**
- Modify: `accelerators/bridge/dab-config.json`
- Modify: `Sunfish.Bridge.AppHost/Program.cs`

- [ ] **Step 1: Update DAB connection string env var**

In `dab-config.json`, replace:

```json
"connection-string": "@env('ConnectionStrings__sunfishbridgedb')"
```

With:

```json
"connection-string": "@env('ConnectionStrings__sunfishbridgedb')"
```

- [ ] **Step 2: Update AppHost connection string name**

In `Sunfish.Bridge.AppHost/Program.cs`, find the PostgreSQL resource definition and rename the connection string:

```csharp
// Before
var postgres = builder.AddPostgres("sunfishbridgedb")

// After
var postgres = builder.AddPostgres("sunfishbridgedb")
```

- [ ] **Step 3: Verify Aspire AppHost builds**

```bash
dotnet build Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj
```

- [ ] **Step 4: Commit**

```bash
git add accelerators/bridge/dab-config.json \
        accelerators/bridge/Sunfish.Bridge.AppHost/
git commit -m "feat(bridge-accelerator): update DAB config and Aspire connection string names"
```

---

### Task 9-5: Extract Reusable Patterns to Foundation (D7)

This task extracts two patterns from the Bridge accelerator that belong in `packages/foundation` so other accelerators can reuse them without taking a dependency on the Bridge accelerator.

**Files:**
- Create: `packages/foundation/Notifications/IUserNotificationService.cs`
- Create: `packages/foundation/Notifications/UserNotification.cs`
- Create: `packages/foundation/Notifications/NotificationEnums.cs`
- Create: `packages/foundation/Authorization/ITenantContext.cs`
- Modify: `accelerators/bridge/Sunfish.Bridge.Client/Notifications/` (switch to foundation types)

- [ ] **Step 1: Copy notification contracts to foundation**

```bash
cp "C:/Projects/Sunfish/accelerators/bridge/Sunfish.Bridge.Client/Notifications/IUserNotificationService.cs" \
   "C:/Projects/Sunfish/packages/foundation/Notifications/"
cp "C:/Projects/Sunfish/accelerators/bridge/Sunfish.Bridge.Client/Notifications/UserNotification.cs" \
   "C:/Projects/Sunfish/packages/foundation/Notifications/"
```

Update namespace in both files: `Sunfish.Bridge.Client.Notifications` → `Sunfish.Foundation.Notifications`

- [ ] **Step 2: Copy ITenantContext to foundation**

```bash
cp "C:/Projects/Sunfish/accelerators/bridge/Sunfish.Bridge.Data/Authorization/ITenantContext.cs" \
   "C:/Projects/Sunfish/packages/foundation/Authorization/"
```

Update namespace: `Sunfish.Bridge.Data.Authorization` → `Sunfish.Foundation.Authorization`

- [ ] **Step 3: Update Bridge accelerator to reference foundation types**

In `Sunfish.Bridge.Client/_Imports.razor`, add:

```razor
@using Sunfish.Foundation.Notifications
@using Sunfish.Foundation.Authorization
```

In `Sunfish.Bridge.Client/Notifications/IUserNotificationService.cs`, change to extend `Sunfish.Foundation.Notifications.IUserNotificationService` (or delete and use foundation directly). Remove the duplicate definition.

In `Sunfish.Bridge.Data/Authorization/ITenantContext.cs`, replace with:

```csharp
// Re-export from foundation for backward compatibility
using Sunfish.Foundation.Authorization;
```

- [ ] **Step 4: Build both projects**

```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
dotnet build accelerators/bridge/Sunfish.Bridge.sln
```

Expected: Both build successfully.

- [ ] **Step 5: Commit**

```bash
git add packages/foundation/Notifications/ packages/foundation/Authorization/ \
        accelerators/bridge/
git commit -m "feat(foundation): extract notification pipeline and ITenantContext contracts from Bridge accelerator"
```

---

### Task 9-6: Update ROADMAP.md and Run Tests

**Files:**
- Modify: `accelerators/bridge/ROADMAP.md`
- Run: `accelerators/bridge/tests/`

- [ ] **Step 1: Update ROADMAP.md brand and pending items**

At the top of `ROADMAP.md`, add:

```markdown
# Bridge Accelerator — Roadmap

> Migrated from Marilo.PmDemo. Brand updated: Marilo → Sunfish.
> Steps 1–2 complete. Steps 3–12 are pending work tracked in the Sunfish ICM pipeline.
```

Search-replace throughout: `Marilo` → `Sunfish`, `PmDemo` → `Pm`, `MariloXxx` → `SunfishXxx`.

- [ ] **Step 2: Run the smoke tests**

```bash
dotnet test accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/ -v normal
```

Expected: All existing seeder smoke tests pass.

- [ ] **Step 3: Commit**

```bash
git add accelerators/bridge/ROADMAP.md
git commit -m "docs(bridge-accelerator): update ROADMAP.md with Sunfish branding and migration status"
```

---

### Phase 9 Summary — What This Produces

| Deliverable | Location |
|---|---|
| Full Bridge application | `accelerators/bridge/` |
| 14 screens (dashboard, board, tasks, timeline, risk, budget, team, 7 account pages) | `Sunfish.Bridge.Client/Pages/` |
| EF Core data layer with PostgreSQL + demo seed data | `Sunfish.Bridge.Data/` |
| Aspire orchestration (PostgreSQL, Redis, RabbitMQ, DAB, mock OIDC) | `Sunfish.Bridge.AppHost/` |
| Real-time updates via SignalR | `Sunfish.Bridge/Hubs/BridgeHub.cs` |
| Canonical notification pipeline in foundation | `packages/foundation/Notifications/` |
| Multi-tenant seam in foundation | `packages/foundation/Authorization/` |
| Annotated demo-only auth seams | `DemoTenantContext.cs`, `MockOktaService/` |
| Pending roadmap preserved | `ROADMAP.md` |

---

## Self-Review

### Spec coverage check

| Requirement | Covered in plan? |
|---|---|
| Rebrand Marilo → Sunfish | ✓ Tasks 1-8, rename strategy established |
| Migrate component source (181 components) | ✓ Phase 3b |
| Migrate specs/docs | ✓ Phase 8 |
| Migrate kitchen-sink demo | ✓ Phase 7 |
| Telerik compatibility surface | ✓ Phase 6 |
| Map to Sunfish layered architecture | ✓ Package mapping table |
| Naming convention decisions | ✓ Decisions D1-D8 |
| React adapter gap | ✓ Documented as out-of-scope, gap-analysis item logged |
| PmDemo as Solution Accelerator | ✓ Phase 9, Tasks 9-1 through 9-6 |
| PmDemo reusable patterns (notifications, multi-tenant) | ✓ Task 9-5, D7 |
| PmDemo pending roadmap preserved | ✓ Task 9-6, D8 |
| PmDemo demo-only seams annotated | ✓ Task 9-2, D8 |

### Gaps and risks

1. **React adapter parity**: Sunfish architecture requires Blazor + React parity. This migration only covers Blazor. Log `sunfish-gap-analysis` item after Phase 3.
2. **SCSS compilation pipeline**: Sunfish has no npm build setup yet. Phase 1 should add `package.json` with SCSS build scripts (not covered in Task 1 — add to Task 1 or as Task 9).
3. **Solution file**: No `.sln` or `.slnx` created in Phase 1. Add to Task 1.
4. **Legacy icons `[Obsolete]`**: Phase 3d marks `Marilo.Icons` as obsolete in Sunfish, but doesn't include a migration guide for consumers. Add a note in Phase 8 docs.
5. **SignalR dependency in foundation**: `SignalRConnectionRegistry.cs` lives in `Marilo.Core` but is Blazor-adjacent. Verify it belongs in `foundation` rather than `ui-adapters-blazor` before Phase 1 completes.
6. **PmDemo Phase 9 depends on Phase 3**: Tasks 9-3 and beyond cannot be completed until `ui-adapters-blazor` exists. Tasks 9-1 and 9-2 (scaffolding + rename) can run independently as soon as the plan starts.
7. **EF Core migrations after rename**: The `BridgeDbContext` → `SunfishBridgeDbContext` rename will break existing migration history snapshots. After Task 9-2, run `dotnet ef migrations add PostRename` to capture the rename in a new migration, then verify the migration applies cleanly against a fresh PostgreSQL instance.
8. **Settings page completeness**: 7 of the 10 account pages are production-grade; 3 are placeholders ("Coming soon"). The ROADMAP.md preserves the pending work. Do not attempt to complete them during migration.

### Placeholder scan

No TBD/TODO/fill-in-later items in Phase 1 tasks. Phases 2-8 are summarized intentionally (each expands into its own implementation plan). This is a strategy plan, not a phase-level implementation plan.

---

## Next Step

Start with **Phase 1, Task 1** above. Phase 1 can be executed immediately — it has no upstream dependencies.

When Phase 1 is complete, write the Phase 2 implementation plan (UI Core Contracts) as a separate plan document at:
`docs/superpowers/plans/2026-04-16-marilo-sunfish-phase2-ui-core.md`
