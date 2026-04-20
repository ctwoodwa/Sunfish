# Package Conventions

**Status:** Accepted
**Last reviewed:** 2026-04-19
**Governs:** Every `.csproj` in `packages/`, `accelerators/`, and `apps/`.
**Companion docs:** [naming.md](../product/naming.md), [testing-strategy.md](testing-strategy.md), [coding-standards.md](coding-standards.md).

This is the practical guide for structuring a new Sunfish package. It codifies conventions already in use in `packages/foundation-catalog/`, `packages/foundation-multitenancy/`, `packages/foundation-featuremanagement/`, `packages/foundation-localfirst/`, `packages/foundation-integrations/`, and most `blocks-*` modules. For a new package, start by copying one of those — then adjust per this doc.

## Repo-level infrastructure

### Central properties — `Directory.Build.props`

At repo root. Applies to every project. Current values:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Authors>Sunfish Contributors</Authors>
    <Company>Sunfish</Company>
    <RepositoryUrl>https://github.com/your-org/sunfish</RepositoryUrl>
    <Version>0.1.0</Version>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

Consequences for every package:

- **`TargetFramework`** is `net10.0` — don't set it per project unless an exception is required.
- **`Nullable` + `ImplicitUsings`** are on everywhere.
- **`GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`** means every public type and member must have XML documentation — undocumented public members raise CS1591, which becomes an error. Test projects opt out with `<GenerateDocumentationFile>false</GenerateDocumentationFile>` (see below).
- **`IsPackable=false` by default.** Library packages set `<IsPackable>true</IsPackable>` in their own csproj.

### Central package versions — `Directory.Packages.props`

At repo root. `ManagePackageVersionsCentrally` is on — individual csprojs reference packages with `<PackageReference Include="..." />` (no `Version=`). Versions live in `Directory.Packages.props`. If you need a new NuGet package, add it there first.

### Solution file — `Sunfish.slnx`

The modern `.slnx` XML format (not classic `.sln`). When you add a package, add two entries (the project and its tests) under a tier-appropriate `<Folder>`. See [naming.md §slnx folder names](../product/naming.md#slnx-folder-names).

## Package folder layout

Every code package follows this shape:

```
packages/<package-folder>/
├── <Namespace>.csproj            ← library project
├── <Source>.cs …                 ← source files, one public type per file
├── <Subfolder>/                  ← optional logical grouping (e.g. Bundles/, Templates/)
│   └── …
├── Manifests/                    ← optional; embedded JSON seeds
│   └── <Kind>/*.json
├── Schemas/                      ← optional; embedded JSON Schema meta-schemas
│   └── *.schema.json
└── tests/                        ← sibling test project
    ├── tests.csproj
    ├── GlobalUsings.cs
    ├── Fixtures/                 ← optional test fixtures
    │   └── …
    └── <TestFile>.cs …
```

Razor packages (UI adapters, `blocks-*` with components) follow the same layout plus `.razor`, `.razor.css`, and `wwwroot/` folders.

## Library csproj template

Use this shape for a new Foundation, Blocks, or Ingestion package (non-Razor). Replace angle-bracket placeholders with real values.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId><Sunfish.Tier.Name></PackageId>
    <Description><One-paragraph description of what the package is for. Reference the ADR that introduced it.></Description>
    <PackageTags>sunfish;<tier>;<key-concepts></PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <!-- Only add what this package actually consumes. No transitive pulls. -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
  </ItemGroup>

  <ItemGroup>
    <!-- ProjectReferences to the packages this one depends on, per the dependency-direction rules. -->
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Exclude the sibling tests folder from the library build. -->
    <Compile Remove="tests/**/*.cs" />
  </ItemGroup>

  <ItemGroup>
    <!-- Only if the test project needs access to internals. -->
    <InternalsVisibleTo Include="<Sunfish.Tier.Name>.Tests" />
  </ItemGroup>
</Project>
```

### Key rules

- **`IsPackable=true`** for libraries that are intended as NuGet output. (Current default in `Directory.Build.props` is false; libraries opt in.)
- **`PackageId`** matches the namespace and csproj filename.
- **`Description`** is a real sentence that references the ADR or platform spec section where the package was introduced. Reviewers will ask for this.
- **`PackageTags`** — semicolon-separated lowercase keywords that search-match the package.
- **`<Compile Remove="tests/**/*.cs" />`** — essential. Without this, MSBuild compiles the test files into the library assembly.

### Razor packages

For Blazor component libraries or `blocks-*` modules that ship components, swap the SDK:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Blocks.Tasks</PackageId>
    <Description>Task-board state-machine block for Sunfish — opinionated composition over SunfishDataGrid and SunfishCard.</Description>
    <PackageTags>blazor;tasks;kanban;blocks;domain;sunfish</PackageTags>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <NoWarn>CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="tests/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Sunfish.Blocks.Tasks.Tests" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\ui-core\Sunfish.UICore.csproj" />
    <ProjectReference Include="..\ui-adapters-blazor\Sunfish.UIAdapters.Blazor.csproj" />
  </ItemGroup>
</Project>
```

Razor packages frequently use `<NoWarn>CS1591</NoWarn>` because `.razor` files produce un-XML-doc-able generated types. New component files still get XML docs where practical.

## Test csproj template

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <AssemblyName><Sunfish.Tier.Name>.Tests</AssemblyName>
    <RootNamespace><Sunfish.Tier.Name>.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\<Sunfish.Tier.Name>.csproj" />
  </ItemGroup>

</Project>
```

### Key rules

- **`TargetFramework` restated.** While `Directory.Build.props` sets it, the test csproj typically repeats it for clarity.
- **`GenerateDocumentationFile=false`** — tests don't need XML docs.
- **`AssemblyName` and `RootNamespace` are explicit.** Without them, every test project builds to `tests.dll` — confusing in tooling and in `InternalsVisibleTo`.
- **Test file name: `tests.csproj`.** Conventional across the repo.
- **`GlobalUsings.cs`** typically contains at minimum `global using Xunit;` plus any types referenced in nearly every test file (e.g. `global using System.Text.Json;`, `global using System.Text.Json.Nodes;`).

## Embedded resources (manifests, schemas, fixtures)

JSON files shipped as embedded resources use explicit `LogicalName` entries — MSBuild's default `%(RecursiveDir)` produces backslashes on Windows that break cross-platform resource lookups.

```xml
<ItemGroup>
  <None Remove="Manifests/**/*.json" />
  <None Remove="Schemas/**/*.json" />
  <EmbeddedResource Include="Manifests/Bundles/property-management.bundle.json">
    <LogicalName>Bundles/property-management.bundle.json</LogicalName>
  </EmbeddedResource>
  <EmbeddedResource Include="Schemas/bundle-manifest.schema.json">
    <LogicalName>Schemas/bundle-manifest.schema.json</LogicalName>
  </EmbeddedResource>
</ItemGroup>
```

### Rules

- Always list each resource explicitly with a `LogicalName`.
- Always use forward slashes in `LogicalName`, even on Windows.
- Remove the default `None` glob to avoid MSBuild double-including the JSON.
- Test fixtures copied to the test output directory (not embedded) use a separate pattern — see [testing-strategy.md](testing-strategy.md).

## `InternalsVisibleTo` patterns

Two valid patterns exist in the repo:

1. **Explicit assembly name** (preferred for new packages):
   ```xml
   <InternalsVisibleTo Include="Sunfish.Foundation.Catalog.Tests" />
   ```
   Requires the test project to set `<AssemblyName>Sunfish.Foundation.Catalog.Tests</AssemblyName>`.

2. **Legacy bare `tests`**:
   ```xml
   <InternalsVisibleTo Include="tests" />
   ```
   Matches any test project named `tests.dll`. Used by older packages (`Sunfish.Foundation.csproj`). New packages don't use this.

## ProjectReference vs. PackageReference

- **`ProjectReference`** — for every sibling Sunfish package in the same repo. Path-relative.
- **`PackageReference`** — for external NuGet dependencies. Version omitted (central via `Directory.Packages.props`).

Never mix: don't `PackageReference` a Sunfish package from within the Sunfish repo.

## Checklist — adding a new package

1. **Decide the tier** and pick the right folder prefix per [naming.md](../product/naming.md). If it's cross-cutting and doesn't fit a tier, open an ADR first.
2. **Create the folder** `packages/<kebab-name>/`.
3. **Create the csproj** using the template above. Set `PackageId`, `Description`, `PackageTags`.
4. **Add sibling `tests/`** folder with `tests.csproj`, `GlobalUsings.cs`, and at least one test covering the new types.
5. **Register in `Sunfish.slnx`** — add a `<Folder>` block for the tier if new, then `<Project Path="...">` entries for both csprojs.
6. **Add dependencies** via `ProjectReference` (sibling packages) and `PackageReference` (NuGet, central-managed versions only).
7. **Exclude tests from library compile** — `<Compile Remove="tests/**/*.cs" />`.
8. **Grant internals if needed** — `<InternalsVisibleTo Include="Sunfish.<Tier>.<Name>.Tests" />`.
9. **Run `dotnet build`** — green with 0 warnings. `TreatWarningsAsErrors` surfaces XML-doc gaps immediately.
10. **Run `dotnet test`** — every test passes.
11. **Cross-link** from the ADR that introduced the package, the roadmap tracker, and any consuming modules.

## Gotchas from real experience

- **LogicalName forward slashes.** Solved above. Without `LogicalName`, `Manifests/Bundles/x.json` becomes `Bundles\x.json` (backslash) which hand-written loaders won't find.
- **`TreatWarningsAsErrors` + XML docs.** Every public type needs at least a `<summary>`. Use `/// <inheritdoc />` when implementing an interface method and you don't need to restate the contract.
- **Default-interface-method accessibility.** `bool IsResolved => Tenant is not null;` on an interface is only callable when the caller holds the interface type, not the concrete type. Cast to the interface or type variables as the interface.
- **JsonSchema.Net global registry.** When a test parses a JSON Schema with a `$id`, the schema is registered globally. Two tests parsing different content with the same `$id` throws `Overwriting registered schemas is not permitted.` Strip `$id` before `JsonSchema.FromText` in tests, or give each schema a unique `$id`.
- **`Microsoft.NET.Sdk.Razor` + `Nullable` / `ImplicitUsings`.** Razor csprojs that also set `Nullable` and `ImplicitUsings` explicitly are fine — inheritance from `Directory.Build.props` works but restating doesn't hurt.

## Cross-references

- [architecture-principles.md](../product/architecture-principles.md) — why packages are split the way they are.
- [naming.md](../product/naming.md) — what to name them.
- [testing-strategy.md](testing-strategy.md) — how to write the test project.
- [`docs/adrs/`](../../docs/adrs/) — decisions that triggered each Foundation-tier package.
- `packages/foundation-multitenancy/` and its tests — a recent minimal example to copy.
- `packages/foundation-catalog/` and its tests — a recent larger example with embedded resources.
