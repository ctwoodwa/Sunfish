# Phase 9: Bridge Solution Accelerator — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

---

## Platform Context

> **⚠ Read this before executing.** Bridge is not a stand-alone demo; it is the **property-management vertical reference implementation** of the Sunfish platform. Sunfish itself is a framework-agnostic suite of open-source and commercial building blocks for decentralized, multi-jurisdictional asset lifecycle and workflow systems — property management is one vertical of several (military bases, rail/subway, school districts, healthcare facilities are peer verticals).
>
> The authoritative platform specification is `docs/specifications/sunfish-platform-specification.md`. That document covers:
>
> 1. Executive Summary & market positioning (vs Aconex, Maximo, Appian/Pega, ServiceNow)
> 2. Reference Architecture (decentralized governance, cryptographic ownership, delegation)
> 3. Core Kernel primitives (entity storage, versioning, audit trails, schema registry, permissions, events)
> 4. Phased Implementation Roadmap (5 spec phases covering current migration work + future verticals)
> 5. Technical Specifications (multi-versioned entity schema, API contracts, authorization evaluation, asset hierarchy)
> 6. Property Management MVP feature set
> 7. Input Modalities (forms, spreadsheets, voice, sensors, drones, satellite imagery)
> 8. Asset Evolution & Versioning Strategy
> 9. BIM Integration approach
> 10. Multi-Jurisdictional & Multi-Tenant design (delegated authority, federation)
> 11. Container & Deployment Guide
> 12. Risk Assessment
> 13. Go-to-Market & Competitive Positioning
>
> **Where this Phase 9 plan fits:** The spec's **Roadmap Phase 4 (Property Management Bridge)** is what this document operationalizes. Spec Phases 1–3 (kernel + forms foundation, asset modeling, workflow orchestration) are a mix of already-shipped migration phases (Phase 1/2/3 foundation+ui-core+adapters) and still-to-come work (asset modeling is NEW — not in the current migration). Spec Phase 5 (secondary verticals) is future work beyond the current migration scope.
>
> **What makes Bridge the vertical reference (not just a PmDemo rebrand):**
>
> 1. Bridge exercises **every** Sunfish kernel primitive end-to-end — entity versioning on leases, audit trails on inspections, time-bound delegation for contractor access, event streams for maintenance workflows
> 2. Bridge's data model demonstrates the **asset hierarchy** kernel primitive (property → unit → fixture → deficiency) as a concrete evolvable tree with temporal queries
> 3. Bridge's auth model demonstrates the **multi-tenant + multi-jurisdictional** design (landlord, property manager, contractor, inspector, code-enforcement agency — each with different scopes and time-bound delegations)
> 4. Bridge is the **first opinionated composition** of Sunfish primitives that a new vertical (e.g., school district facility management) can fork as a starting point
>
> The tasks below still perform the mechanical PmDemo → Bridge rename, but they are now executed **in service of** landing the property-management reference. The ROADMAP.md (task 9-6) should be expanded post-migration to capture how Bridge graduates from "renamed demo" to "canonical vertical reference" — see the spec's Section 6.

---

**Goal:** Migrate `C:/Projects/Marilo/samples/Marilo.PmDemo/` → `C:/Projects/Sunfish/accelerators/bridge/` as the first Sunfish **Solution Accelerator** — a full-stack Blazor Server + .NET Aspire application (Bridge) that showcases how Sunfish packages compose end-to-end: `foundation → ui-core → ui-adapters-blazor → blocks → real app`, AND serves as the **property-management vertical reference implementation** per the platform specification. The accelerator must build green, boot under Aspire, and preserve all 14 existing screens (dashboard, board, tasks, timeline, risk, budget, team, 7 account pages) as the starting baseline for property-management MVP feature expansion (leases, rent collection, inspections, maintenance workflows, vendor quotes, accounting, tax reporting — per spec Section 6).

**Architecture context:** This is the *only* top-level tier with `accelerators/` — not `apps/`, not `samples/`. Master plan D6 locks this tier as a first-class Sunfish concept equivalent to `packages/`. The accelerator takes **project references** (not NuGet) to the Sunfish packages in the same repo — see D-PROJECT-REFERENCES below. Per the platform spec, additional accelerators will follow for each secondary vertical (military base, transit, school district, healthcare) — all siblings of `accelerators/bridge/`.

**Source reference:** The master migration plan (`docs/superpowers/plans/2026-04-16-marilo-sunfish-migration.md`, `## Phase 9: Bridge Solution Accelerator`, lines ~711–1198) sketches Tasks 9-1 through 9-6. **This document is the authoritative Phase 9 plan**; the master plan's Phase 9 section should be reduced to a pointer to this file after merge. Platform-level framing lives in `docs/specifications/sunfish-platform-specification.md` (see Platform Context above).

**Tech Stack:** .NET 10, C# 13, Blazor Server (interactive server components), .NET Aspire 13.2.1, EF Core 10 (Npgsql provider), PostgreSQL, Redis (output cache + SignalR backplane), RabbitMQ (via WolverineFx), Data API Builder (DAB) GraphQL, SignalR, xUnit 2.9.x.

**Known gap vs. platform spec — decentralization primitives:** The current Sunfish repo does NOT yet have kernel-level cryptographic ownership, capability-based delegation, or federation primitives (spec Sections 2, 10). Bridge as shipped by this phase plan uses conventional single-tenant auth (DemoTenantContext + MockOktaService). Spec-aligned decentralization work is a future migration phase — flagged explicitly in the updated ROADMAP.md and in Task 9-10 below.

**Candidate implementation path (see `docs/specifications/research-notes/automerge-evaluation.md`):** The decentralization primitives should adopt Automerge's **semantic model** (Merkle-DAG change log, CRDT merge rules, sync protocol shape) and Keyhive's **capability model** (group membership over Ed25519 keys) as design references. The Automerge library itself is **not** a drop-in: (a) no .NET binding exists, (b) Bridge is server-hosted not local-first, (c) Keyhive's group-based model replaces the spec's original Macaroon-style delegation. Implementation is expected to be a .NET-native version store + crypto + sync built in the style of these references rather than a direct integration. See the evaluation doc for integration paths, mismatches, and open questions.

---

## Key Decisions

**D6 (locked by master plan):** Bridge lives under `accelerators/bridge/`, NOT `apps/bridge/`. Apps are small demo/doc hosts; accelerators are opinionated full-stack starter solutions. This is a new top-level tier in the repo.

**D7 (locked by master plan):** Extract reusable patterns — `IUserNotificationService` + `UserNotification` + the canonical notification pipeline, plus `ITenantContext` — into `packages/foundation/`. Rationale: future accelerators need these without taking a dependency on Bridge.

**D8 (locked by master plan):** Preserve `SETTINGS_STATUS.md` → `ROADMAP.md` with Sunfish branding. **Do NOT complete** the pending roadmap items (Steps 3–12 in the original doc) as part of this migration — those are separate feature work.

**D-PROJECT-REFERENCES (new):** The accelerator references Sunfish packages via `<ProjectReference>` with relative paths (`..\..\packages\ui-adapters-blazor\Sunfish.Components.Blazor.csproj`), NOT `<PackageReference>` to NuGet. Rationale:

1. Same git repo — there is no publish boundary to cross.
2. Iteration speed: a breaking change in `ui-adapters-blazor` surfaces as a build error in the accelerator immediately.
3. Avoids a publish-consume-publish dance during active development.
4. When the repo ships a release, the accelerator's `.csproj` files will be rewritten to `PackageReference` via a release-prep script (out of scope for Phase 9).

**D-CSPROJ-RENAME-MECHANICS (new):** The master plan's Task 9-2 Step 2 uses a single sed pass. In practice, Phase 3b's component migrations (`scripts/migrate-marilo-category.sh`) surfaced four edge cases the naive sed misses:

1. **`.bak` / `.orig` / `~` scratch files** from editors — must be deleted BEFORE the sed pass, otherwise the contamination gate (see step 6 of this task) false-positives on stale Marilo content in backup files.
2. **Plain `.cs` files that inherit `SunfishComponentBase`** — need an explicit `using Sunfish.Components.Blazor.Base;` because `_Imports.razor` doesn't cover non-Razor files. (Not expected in Bridge, but harmless to check.)
3. **Namespace path segments with a non-boundary `Marilo`** — the sed `\bMarilo\b` word-boundary rules from the migration script are required to avoid mangling `MariloXxx` inside larger identifiers.
4. **JSON files with connection string names** — these need domain-aware rewrites, not a brand-name sweep (see Task 9-4).

We reuse the exact patterns from `scripts/migrate-marilo-category.sh` (drop-backups-first, word-boundary sed, contamination gate grep) and add Bridge-specific identifier mappings (`PmDemo → Bridge`, `PmDemoHub → BridgeHub`, `PmDemoDbContext → SunfishBridgeDbContext`, `PmDemoSeeder → BridgeSeeder`). A self-contained script is delivered in Task 9-2 Step 2 so the transformation is reproducible.

**D-EF-MIGRATION-CAPTURE (new):** Renaming `PmDemoDbContext` → `SunfishBridgeDbContext` changes the `ModelSnapshot`'s contextual namespace/class reference. The existing `Migrations/PmDemoDbContextModelSnapshot.cs` is stale the moment we rename — EF Core will either (a) fail migration-add because it can't find the old context, or (b) produce noisy "no changes detected" diffs because the snapshot still thinks the model is rooted in `Marilo.PmDemo.Data`. The clean path is:

1. Rename file `PmDemoDbContextModelSnapshot.cs` → `SunfishBridgeDbContextModelSnapshot.cs`.
2. Rename the class inside it + the `DbContext(typeof(PmDemoDbContext))` attribute argument.
3. Run `dotnet ef migrations add PostRenameSnapshot` to emit a no-op migration that captures the new class name and namespace in the snapshot — this *rebaselines* the snapshot atomically instead of forcing manual edits to the generated snapshot file.
4. Verify the generated `*_PostRenameSnapshot.cs` `Up`/`Down` methods are empty (no column/table changes).
5. If the diff shows actual schema changes, the rename was incomplete — fix and re-run.

Exact command sequence in Task 9-7.

**D-AUTH-SEAM-LOGGING (new):** Demo-only auth seams (`DemoTenantContext`, `MockOktaService`) must log a **startup warning** so developers boot-time learn they're running in demo mode. Without this, it's too easy for a consumer to fork Bridge and ship it with the demo tenant context still wired. Log format:

```
warn: Sunfish.Bridge.Authorization.DemoTenantContext[0]
      DEMO AUTH SEAM ACTIVE: DemoTenantContext is registered. TenantId='demo-tenant', UserId='demo-user'. 
      This is for local development only. Replace with a real ITenantContext implementation before production deployment. 
      See accelerators/bridge/ROADMAP.md §Auth.
```

```
warn: Sunfish.Bridge.AppHost[0]
      DEMO AUTH SEAM ACTIVE: MockOktaService is registered as the OIDC provider. 
      This is for local development only. Replace with a real Okta/Entra ID/Auth0 configuration before production deployment.
```

Both log at `LogLevel.Warning` so they surface even if the default log level is `Information`. They are emitted from constructor (`DemoTenantContext`) and from `AddProject<Projects.MockOktaService>` in `AppHost/Program.cs` via an accompanying `// DEMO ONLY` comment and a `builder.Services.AddSingleton<IStartupFilter, DemoAuthWarningFilter>()` — see Task 9-8.

**D-ASPIRE-VERSION (new):** Aspire `13.2.1` per master plan's tech stack. Verified: `Marilo.PmDemo.AppHost.csproj` uses `<Sdk Name="Aspire.AppHost.Sdk" Version="13.2.1" />`. Aspire package references (`Aspire.Hosting.AppHost`, `Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.Redis`, `Aspire.Hosting.RabbitMQ`, `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, `Aspire.StackExchange.Redis.OutputCaching`) are version-pinned centrally in `Directory.Packages.props`. Task 9-1 Step 5 verifies all Aspire packages resolve against the central version file; if any Aspire dependency is missing from `Directory.Packages.props`, it must be added before the accelerator builds.

---

## File Structure

```
accelerators/
  bridge/                                                  ← new top-level directory
    Sunfish.Bridge.slnx                                    ← solution (slnx format, matches root Sunfish.slnx)
    dab-config.json                                        ← DAB GraphQL config; conn-string renamed
    ROADMAP.md                                             ← renamed from SETTINGS_STATUS.md; brand updated
    README.md                                              ← new; explains what Bridge demonstrates

    Sunfish.Bridge/                                        ← web server project (Blazor Server host)
      Sunfish.Bridge.csproj
      Program.cs
      appsettings.json
      appsettings.Development.json
      Components/
        App.razor
        _Imports.razor
        Pages/Error.razor
      Authorization/
        DemoTenantContext.cs                               ← annotated DEMO ONLY + startup warning
        DemoAuthWarningFilter.cs                           ← NEW; emits startup warning log
      Features/FeatureFlags.cs
      Handlers/TaskStatusChangedHandler.cs
      Hubs/
        BridgeHub.cs                                       ← renamed from PmDemoHub
        IBridgeHubClient.cs                                ← renamed from IPmDemoHubClient
      Messages/Messages.cs
      Properties/launchSettings.json

    Sunfish.Bridge.Client/                                 ← client Razor class library (interactive components)
      Sunfish.Bridge.Client.csproj
      Program.cs
      _Imports.razor                                       ← replaces Marilo.* with Sunfish.* + Sunfish.Foundation.Notifications
      Routes.razor
      Layout/
        MainLayout.razor
        MainLayout.razor.css
        AccountLayout.razor
        AccountLayout.razor.css
      Pages/
        Home.razor
        Tasks.razor
        Board.razor
        Timeline.razor
        Risk.razor
        Budget.razor
        Team.razor
        NotFound.razor
        Account/
          Details.razor + Details.razor.css
          Preferences.razor + Preferences.razor.css
          Notifications.razor + Notifications.razor.css
          Shortcuts.razor + Shortcuts.razor.css
          Personalization.razor
          Integrations.razor                               ← placeholder page
          Billing.razor                                    ← placeholder page
          Team.razor                                       ← placeholder page
      Components/Settings/                                 ← 10 settings wrapper components (unchanged)
        SettingsActionRow.razor + .razor.css
        SettingsCard.razor + .razor.css
        SettingsDangerZone.razor + .razor.css
        SettingsFieldRow.razor + .razor.css
        SettingsHomeLink.razor + .razor.css
        SettingsPageHeader.razor + .razor.css
        SettingsSection.razor + .razor.css
        SettingsSelectRow.razor + .razor.css
        SettingsSidebar.razor + .razor.css
        SettingsToggleRow.razor + .razor.css
      Notifications/                                       ← canonical pipeline; IUserNotificationService + UserNotification moved to foundation
        InMemoryUserNotificationService.cs                 ← implements Sunfish.Foundation.Notifications.IUserNotificationService
        NotificationProjections.cs                         ← SunfishToastUserNotificationForwarder (renamed)
      Services/ProviderSwitcher.cs                         ← runtime provider swap for FluentUI/Bootstrap/Material
      Models/Settings/
        SettingsNavGroup.cs
        SettingsNavItem.cs
      Data/ThemePresets.cs

    Sunfish.Bridge.Data/                                   ← EF Core data layer
      Sunfish.Bridge.Data.csproj
      SunfishBridgeDbContext.cs                            ← renamed from PmDemoDbContext.cs
      DesignTimeDbContextFactory.cs
      Entities/Entities.cs                                 ← Project, ProjectMember, TaskItem, Subtask, Comment, Milestone, Risk, BudgetLine, AuditRecord
      Authorization/
        ITenantContext.cs                                  ← re-exports Sunfish.Foundation.Authorization.ITenantContext
        Permissions.cs
        Roles.cs
      Seeding/BridgeSeeder.cs                              ← renamed from PmDemoSeeder.cs
      Migrations/
        20260407195529_Initial.cs                          ← renamed namespace; column schema unchanged
        20260407195529_Initial.Designer.cs
        <new-timestamp>_PostRenameSnapshot.cs              ← empty migration from Task 9-7
        <new-timestamp>_PostRenameSnapshot.Designer.cs
        SunfishBridgeDbContextModelSnapshot.cs             ← renamed from PmDemoDbContextModelSnapshot.cs

    Sunfish.Bridge.AppHost/                                ← Aspire orchestration
      Sunfish.Bridge.AppHost.csproj
      Program.cs                                           ← conn string "pmdemodb" → "sunfishbridgedb"
      appsettings.json
      Properties/launchSettings.json

    Sunfish.Bridge.ServiceDefaults/
      Sunfish.Bridge.ServiceDefaults.csproj
      Extensions.cs

    Sunfish.Bridge.MigrationService/                       ← one-shot EF migration runner
      Sunfish.Bridge.MigrationService.csproj
      Program.cs
      MigrationWorker.cs
      MigrationTenantContext.cs
      appsettings.json

    MockOktaService/                                       ← annotated DEMO ONLY; minimal OIDC mock
      MockOktaService.csproj
      Program.cs
      appsettings.json
      Services/                                            ← (kept as-is; small tree)

    tests/
      Sunfish.Bridge.Tests.Unit/
        Sunfish.Bridge.Tests.Unit.csproj
        SeederSmokeTests.cs
      Sunfish.Bridge.Tests.Integration/
        Sunfish.Bridge.Tests.Integration.csproj
        HealthCheckTests.cs
      Sunfish.Bridge.Tests.Performance/
        Sunfish.Bridge.Tests.Performance.csproj
        Program.cs
```

**Files to update outside `accelerators/`:**

- `Sunfish.slnx` — register the Bridge solution **OR** keep Bridge as a separate solution (Task 9-1 Step 6).
- `packages/foundation/Sunfish.Foundation.csproj` — no changes (source-only additions).
- `packages/foundation/Notifications/` — new folder with 3 files (Task 9-5).
- `packages/foundation/Authorization/` — new folder with 1 file (Task 9-5).
- `packages/foundation/tests/tests.csproj` — add tests for the new foundation types if not already covered.
- `README.md` — add Bridge accelerator callout (Task 9-9).

---

## Task 9-1: Scaffold Accelerator Directory and Solution

**Files:**
- Create: `accelerators/bridge/` (new top-level directory)
- Create: `accelerators/bridge/Sunfish.Bridge.slnx`
- Rename: project directories under `accelerators/bridge/`

**Source:** `C:/Projects/Marilo/samples/Marilo.PmDemo/` (full tree, including `.slnx`, `dab-config.json`, `MockOktaService/`, 6 production project dirs, 3 test project dirs, `SETTINGS_STATUS.md`).

- [ ] **Step 1: Create the accelerator parent directory**

```bash
mkdir -p "C:/Projects/Sunfish/accelerators"
ls "C:/Projects/Sunfish/accelerators"
```

Expected: directory exists, empty.

- [ ] **Step 2: Copy the full PmDemo tree with `bin/`/`obj/` excluded**

```bash
cd "C:/Projects/Sunfish/accelerators"
mkdir -p bridge
# rsync avoids copying bin/obj noise; fall back to cp if rsync unavailable
rsync -a --exclude='bin/' --exclude='obj/' \
      "C:/Projects/Marilo/samples/Marilo.PmDemo/" \
      "C:/Projects/Sunfish/accelerators/bridge/"
ls bridge/
```

Expected output includes: `dab-config.json`, `Marilo.PmDemo/`, `Marilo.PmDemo.Client/`, `Marilo.PmDemo.Data/`, `Marilo.PmDemo.AppHost/`, `Marilo.PmDemo.ServiceDefaults/`, `Marilo.PmDemo.MigrationService/`, `Marilo.PmDemo.Tests.Unit/`, `Marilo.PmDemo.Tests.Integration/`, `Marilo.PmDemo.Tests.Performance/`, `MockOktaService/`, `Marilo.PmDemo.slnx`, `SETTINGS_STATUS.md`.

- [ ] **Step 3: Delete the original Marilo solution file (regenerated in Step 5)**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"
rm Marilo.PmDemo.slnx
```

- [ ] **Step 4: Rename project directories and the roadmap doc**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"

mv Marilo.PmDemo                      Sunfish.Bridge
mv Marilo.PmDemo.Client               Sunfish.Bridge.Client
mv Marilo.PmDemo.Data                 Sunfish.Bridge.Data
mv Marilo.PmDemo.AppHost              Sunfish.Bridge.AppHost
mv Marilo.PmDemo.ServiceDefaults      Sunfish.Bridge.ServiceDefaults
mv Marilo.PmDemo.MigrationService     Sunfish.Bridge.MigrationService

mkdir -p tests
mv Marilo.PmDemo.Tests.Unit           tests/Sunfish.Bridge.Tests.Unit
mv Marilo.PmDemo.Tests.Integration    tests/Sunfish.Bridge.Tests.Integration
mv Marilo.PmDemo.Tests.Performance    tests/Sunfish.Bridge.Tests.Performance

# MockOktaService stays at the root (matches PmDemo layout)

mv SETTINGS_STATUS.md ROADMAP.md
ls
```

Expected output includes the renamed directories. No `Marilo.*` directories remain.

- [ ] **Step 5: Create the Sunfish.Bridge.slnx file**

Create `accelerators/bridge/Sunfish.Bridge.slnx`:

```xml
<Solution>
  <Configurations>
    <Platform Name="Any CPU" />
    <Platform Name="x64" />
    <Platform Name="x86" />
  </Configurations>
  <Project Path="Sunfish.Bridge.Client/Sunfish.Bridge.Client.csproj" />
  <Project Path="Sunfish.Bridge/Sunfish.Bridge.csproj" />
  <Project Path="Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj" />
  <Project Path="Sunfish.Bridge.ServiceDefaults/Sunfish.Bridge.ServiceDefaults.csproj" />
  <Project Path="MockOktaService/MockOktaService.csproj" />
  <Project Path="Sunfish.Bridge.Data/Sunfish.Bridge.Data.csproj" />
  <Project Path="Sunfish.Bridge.MigrationService/Sunfish.Bridge.MigrationService.csproj" />
  <Project Path="tests/Sunfish.Bridge.Tests.Unit/Sunfish.Bridge.Tests.Unit.csproj" />
  <Project Path="tests/Sunfish.Bridge.Tests.Integration/Sunfish.Bridge.Tests.Integration.csproj" />
  <Project Path="tests/Sunfish.Bridge.Tests.Performance/Sunfish.Bridge.Tests.Performance.csproj" />
</Solution>
```

Note: the csproj paths point at names that **don't exist yet** (the csproj files are still named `Marilo.PmDemo.*.csproj` at this point). Task 9-2 Step 1 renames them. Build verification happens after Task 9-2.

- [ ] **Step 6: Root `Sunfish.slnx` decision**

The root `Sunfish.slnx` registers `foundation`, `ui-core`, `ui-adapters-blazor`. **Do not add Bridge projects to the root slnx.** The accelerator is a separate solution for two reasons:

1. Aspire AppHost requires running `aspire run` from the AppHost directory; having it in the root solution creates confusion about the Aspire entrypoint.
2. Phase 9 builds the accelerator independently; root `dotnet build Sunfish.slnx` should not drag in Postgres/RabbitMQ/Aspire dependencies.

Leave `Sunfish.slnx` unchanged. Document the dual-solution model in README (Task 9-9).

- [ ] **Step 7: Stage and commit**

```bash
cd "C:/Projects/Sunfish"
git add accelerators/bridge/
git commit -m "feat(accelerators): scaffold bridge accelerator directory from PmDemo"
```

---

## Task 9-2: Rename All Marilo/PmDemo Identifiers

**Files:** Every `.cs`, `.razor`, `.razor.cs`, `.razor.css`, `.csproj`, `.json`, `.md`, `.slnx` file under `accelerators/bridge/`.

**Mapping table (source of truth for all transformations):**

| From | To | Scope |
|---|---|---|
| `Marilo.PmDemo.AppHost` | `Sunfish.Bridge.AppHost` | namespace, csproj, project refs |
| `Marilo.PmDemo.Client` | `Sunfish.Bridge.Client` | namespace, csproj, project refs |
| `Marilo.PmDemo.Data` | `Sunfish.Bridge.Data` | namespace, csproj, project refs |
| `Marilo.PmDemo.MigrationService` | `Sunfish.Bridge.MigrationService` | namespace, csproj, project refs |
| `Marilo.PmDemo.ServiceDefaults` | `Sunfish.Bridge.ServiceDefaults` | namespace, csproj, project refs |
| `Marilo.PmDemo.Tests.Unit` | `Sunfish.Bridge.Tests.Unit` | namespace, csproj |
| `Marilo.PmDemo.Tests.Integration` | `Sunfish.Bridge.Tests.Integration` | namespace, csproj |
| `Marilo.PmDemo.Tests.Performance` | `Sunfish.Bridge.Tests.Performance` | namespace, csproj |
| `Marilo.PmDemo` | `Sunfish.Bridge` | namespace, csproj, project refs |
| `PmDemoDbContext` | `SunfishBridgeDbContext` | class name, ctor, file name |
| `PmDemoHub` | `BridgeHub` | class name, file name |
| `IPmDemoHubClient` | `IBridgeHubClient` | interface name, file name |
| `PmDemoSeeder` | `BridgeSeeder` | class name, file name |
| `Marilo_PmDemo` | `Sunfish_Bridge` | Aspire `Projects.*` generated names |
| `Marilo.Core.*` | `Sunfish.Foundation.*` / `Sunfish.UICore.*` | usings (see mapping in Phase 3a D-BASE) |
| `Marilo.Components.*` | `Sunfish.Components.Blazor.Components.*` | usings; same mapping as Phase 3b |
| `Marilo.Components.Shell` | `Sunfish.Components.Blazor.Shell` | using |
| `Marilo.Providers.FluentUI` | `Sunfish.Components.Blazor.Providers.FluentUI` | using (assumes Phase 3c lands these) |
| `Marilo.Providers.Bootstrap` | `Sunfish.Components.Blazor.Providers.Bootstrap` | using |
| `Marilo.Providers.Material` | `Sunfish.Components.Blazor.Providers.Material` | using |
| `MariloXxx` components | `SunfishXxx` | tag names, class refs, code-behind types |
| `MariloToastUserNotificationForwarder` | `SunfishToastUserNotificationForwarder` | class name |
| `IMariloCssProvider` / `IMariloIconProvider` / `IMariloJsInterop` / `IMariloNotificationService` / `IMariloThemeService` | `ISunfish...` | interfaces |
| `AddMarilo` / `AddMariloCoreServices` | `AddSunfish` / `AddSunfishCoreServices` | extension calls (assumes Phase 3 shipped these) |
| `UseFluentUI` / `UseBootstrap` / `UseMaterial` | unchanged | these are provider-local, same in Sunfish |
| `pmdemodb` (conn string name) | `sunfishbridgedb` | Task 9-4 — domain-aware, NOT brand-sweep |
| `pmdemo-redis`, `pmdemo-rabbit`, `pmdemo-dab`, `pmdemo-web`, `pmdemo-migrations` | `bridge-redis`, `bridge-rabbit`, `bridge-dab`, `bridge-web`, `bridge-migrations` | Aspire resource names, Task 9-4 |
| `marilo-pmdemo-apphost` (UserSecretsId) | `sunfish-bridge-apphost` | Task 9-4 |
| `marilo-pmdemo-migrations` (UserSecretsId) | `sunfish-bridge-migrations` | Task 9-4 |
| `wolverine` (Postgres schema) | unchanged | Wolverine outbox schema — keep |
| `demo-tenant`, `demo-user` | unchanged | seed values; keep for continuity |
| `mar-*`, `marilo-*` (CSS class prefixes) | `sf-*` | via Phase 3a D-THEME-CSS-VARS rule |

- [ ] **Step 1: Rename `.csproj` files to match directory names**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"

mv Sunfish.Bridge/Marilo.PmDemo.csproj                              Sunfish.Bridge/Sunfish.Bridge.csproj
mv Sunfish.Bridge.Client/Marilo.PmDemo.Client.csproj                Sunfish.Bridge.Client/Sunfish.Bridge.Client.csproj
mv Sunfish.Bridge.Data/Marilo.PmDemo.Data.csproj                    Sunfish.Bridge.Data/Sunfish.Bridge.Data.csproj
mv Sunfish.Bridge.AppHost/Marilo.PmDemo.AppHost.csproj              Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj
mv Sunfish.Bridge.ServiceDefaults/Marilo.PmDemo.ServiceDefaults.csproj \
   Sunfish.Bridge.ServiceDefaults/Sunfish.Bridge.ServiceDefaults.csproj
mv Sunfish.Bridge.MigrationService/Marilo.PmDemo.MigrationService.csproj \
   Sunfish.Bridge.MigrationService/Sunfish.Bridge.MigrationService.csproj

mv tests/Sunfish.Bridge.Tests.Unit/Marilo.PmDemo.Tests.Unit.csproj \
   tests/Sunfish.Bridge.Tests.Unit/Sunfish.Bridge.Tests.Unit.csproj
mv tests/Sunfish.Bridge.Tests.Integration/Marilo.PmDemo.Tests.Integration.csproj \
   tests/Sunfish.Bridge.Tests.Integration/Sunfish.Bridge.Tests.Integration.csproj
mv tests/Sunfish.Bridge.Tests.Performance/Marilo.PmDemo.Tests.Performance.csproj \
   tests/Sunfish.Bridge.Tests.Performance/Sunfish.Bridge.Tests.Performance.csproj

find . -maxdepth 2 -name "*.csproj" -type f
```

Expected: 10 csproj files, all with `Sunfish.Bridge.*.csproj` names; no `Marilo.PmDemo.*.csproj` remain. (`MockOktaService.csproj` is unchanged — the mock OIDC project keeps its name since it is a demo-specific sidecar.)

- [ ] **Step 2: Run the rename script (drop-backups, sed, code-behind-using-patch)**

Create `scripts/migrate-bridge-accelerator.sh` modeled on `scripts/migrate-marilo-category.sh`:

```bash
#!/usr/bin/env bash
# One-shot rename pass for the Bridge accelerator.
# Usage: scripts/migrate-bridge-accelerator.sh
# Idempotent: re-running on an already-migrated tree is a no-op.

set -euo pipefail

DST="${SUNFISH:-C:/Projects/Sunfish}/accelerators/bridge"
[ -d "$DST" ] || { echo "FAIL: $DST not found (run Task 9-1 first)"; exit 1; }

echo "→ Removing backup/scratch files (*.bak, *.orig, *~)"
find "$DST" -type f \( -name "*.bak" -o -name "*.orig" -o -name "*~" \) -delete

echo "→ Rewriting content — code, razor, csproj, json, slnx, md"
find "$DST" -type f \
  \( -name "*.cs" -o -name "*.razor" -o -name "*.razor.cs" -o -name "*.razor.css" \
     -o -name "*.csproj" -o -name "*.json" -o -name "*.slnx" -o -name "*.md" \) \
  -exec sed -i \
    -e 's/\bMarilo\.PmDemo\.AppHost\b/Sunfish.Bridge.AppHost/g' \
    -e 's/\bMarilo\.PmDemo\.Client\b/Sunfish.Bridge.Client/g' \
    -e 's/\bMarilo\.PmDemo\.Data\b/Sunfish.Bridge.Data/g' \
    -e 's/\bMarilo\.PmDemo\.MigrationService\b/Sunfish.Bridge.MigrationService/g' \
    -e 's/\bMarilo\.PmDemo\.ServiceDefaults\b/Sunfish.Bridge.ServiceDefaults/g' \
    -e 's/\bMarilo\.PmDemo\.Tests\.Unit\b/Sunfish.Bridge.Tests.Unit/g' \
    -e 's/\bMarilo\.PmDemo\.Tests\.Integration\b/Sunfish.Bridge.Tests.Integration/g' \
    -e 's/\bMarilo\.PmDemo\.Tests\.Performance\b/Sunfish.Bridge.Tests.Performance/g' \
    -e 's/\bMarilo\.PmDemo\b/Sunfish.Bridge/g' \
    -e 's/\bMarilo_PmDemo\b/Sunfish_Bridge/g' \
    -e 's/\bPmDemoDbContext\b/SunfishBridgeDbContext/g' \
    -e 's/\bPmDemoHub\b/BridgeHub/g' \
    -e 's/\bIPmDemoHubClient\b/IBridgeHubClient/g' \
    -e 's/\bPmDemoSeeder\b/BridgeSeeder/g' \
    -e 's/\bMarilo\.Core\.Contracts\b/Sunfish.UICore.Contracts/g' \
    -e 's/\bMarilo\.Core\.Services\b/Sunfish.Foundation.Services/g' \
    -e 's/\bMarilo\.Core\.Extensions\b/Sunfish.Foundation.Extensions/g' \
    -e 's/\bMarilo\.Core\.Models\b/Sunfish.Foundation.Models/g' \
    -e 's/\bMarilo\.Core\.Enums\b/Sunfish.Foundation.Enums/g' \
    -e 's/\bMarilo\.Components\.Shell\b/Sunfish.Components.Blazor.Shell/g' \
    -e 's/\bMarilo\.Components\b/Sunfish.Components.Blazor/g' \
    -e 's/\bMarilo\.Providers\.FluentUI\b/Sunfish.Components.Blazor.Providers.FluentUI/g' \
    -e 's/\bMarilo\.Providers\.Bootstrap\b/Sunfish.Components.Blazor.Providers.Bootstrap/g' \
    -e 's/\bMarilo\.Providers\.Material\b/Sunfish.Components.Blazor.Providers.Material/g' \
    -e 's/\bIMariloCssProvider\b/ISunfishCssProvider/g' \
    -e 's/\bIMariloIconProvider\b/ISunfishIconProvider/g' \
    -e 's/\bIMariloJsInterop\b/ISunfishJsInterop/g' \
    -e 's/\bIMariloNotificationService\b/ISunfishNotificationService/g' \
    -e 's/\bIMariloThemeService\b/ISunfishThemeService/g' \
    -e 's/\bMariloNotificationService\b/SunfishNotificationService/g' \
    -e 's/\bMariloToastUserNotificationForwarder\b/SunfishToastUserNotificationForwarder/g' \
    -e 's/\bAddMariloCoreServices\b/AddSunfishCoreServices/g' \
    -e 's/\bAddMarilo\b/AddSunfish/g' \
    -e 's/\bMariloComponentBase\b/SunfishComponentBase/g' \
    -e 's/\bMariloTheme\b/SunfishTheme/g' \
    -e 's/\bMarilo\b/Sunfish/g' \
    -e 's/class="mar-/class="sf-/g' \
    -e 's/class="marilo-/class="sf-/g' \
    -e "s/class='mar-/class='sf-/g" \
    -e "s/class='marilo-/class='sf-/g" \
    {} \;

echo "→ Patching plain .cs files that inherit SunfishComponentBase"
# Defensive: Bridge code doesn't currently inherit the base, but pick up any edge cases.
find "$DST" -type f \( -name "*.razor.cs" -o -name "*.cs" \) ! -name "*.AssemblyInfo.cs" | while read -r f; do
  if grep -q "SunfishComponentBase" "$f" && ! grep -q "using Sunfish\.Components\.Blazor\.Base;" "$f"; then
    if grep -q "^using Sunfish\.Foundation\.Base;" "$f"; then
      sed -i '0,/^using Sunfish\.Foundation\.Base;/{s//using Sunfish.Foundation.Base;\nusing Sunfish.Components.Blazor.Base;/}' "$f"
    else
      sed -i '1i using Sunfish.Components.Blazor.Base;' "$f"
    fi
  fi
done

echo "→ Contamination gate — grep for surviving Marilo identifiers"
if grep -rE '\bMarilo[A-Za-z]|Marilo\.(Core|Components|Providers|PmDemo)' "$DST" \
       --include='*.cs' --include='*.razor' --include='*.razor.cs' \
       --include='*.csproj' --include='*.json' --include='*.slnx'; then
  echo "FAIL: 'Marilo' identifiers remain in $DST"
  exit 1
fi

echo "OK: Bridge accelerator rename complete"
```

Run:

```bash
chmod +x "C:/Projects/Sunfish/scripts/migrate-bridge-accelerator.sh"
"C:/Projects/Sunfish/scripts/migrate-bridge-accelerator.sh"
```

Expected: `OK: Bridge accelerator rename complete` on stdout, exit code 0.

Note: The script explicitly does NOT rewrite connection-string names (`pmdemodb`, `pmdemo-redis`, etc.) — those are domain identifiers handled in Task 9-4, not a brand sweep.

- [ ] **Step 3: Rename internal key files**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"

# Hub
mv Sunfish.Bridge/Hubs/PmDemoHub.cs         Sunfish.Bridge/Hubs/BridgeHub.cs
mv Sunfish.Bridge/Hubs/IPmDemoHubClient.cs  Sunfish.Bridge/Hubs/IBridgeHubClient.cs

# DbContext
mv Sunfish.Bridge.Data/PmDemoDbContext.cs   Sunfish.Bridge.Data/SunfishBridgeDbContext.cs

# Seeder
mv Sunfish.Bridge.Data/Seeding/PmDemoSeeder.cs  Sunfish.Bridge.Data/Seeding/BridgeSeeder.cs

# EF ModelSnapshot — rename now; rebaseline in Task 9-7
mv Sunfish.Bridge.Data/Migrations/PmDemoDbContextModelSnapshot.cs \
   Sunfish.Bridge.Data/Migrations/SunfishBridgeDbContextModelSnapshot.cs

ls Sunfish.Bridge/Hubs/ Sunfish.Bridge.Data/ Sunfish.Bridge.Data/Seeding/ Sunfish.Bridge.Data/Migrations/
```

Expected: file names match the new identifiers. Content was already rewritten by the sed pass in Step 2.

- [ ] **Step 4: Annotate demo-only auth seams with inline comments**

Edit `Sunfish.Bridge/Authorization/DemoTenantContext.cs` — add at the top of the class (above the `public sealed class` declaration):

```csharp
/// <summary>
/// DEMO ONLY. Provides a hardcoded tenant/user identity for local development.
/// Replace with a real <see cref="ITenantContext"/> implementation that reads
/// from authenticated claims (OIDC, Entra, Okta) before production deployment.
/// See accelerators/bridge/ROADMAP.md §Auth for replacement guidance.
/// </summary>
```

Edit `Sunfish.Bridge.AppHost/Program.cs` — add above the MockOktaService line:

```csharp
// DEMO ONLY. MockOktaService is a minimal OIDC mock for local development.
// Replace with real Okta / Entra ID / Auth0 configuration before production.
// See accelerators/bridge/ROADMAP.md §Auth.
```

(Startup log warnings for these two seams are added in Task 9-8.)

- [ ] **Step 5: Build all projects**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"
dotnet restore Sunfish.Bridge.slnx
dotnet build Sunfish.Bridge.slnx 2>&1 | tail -40
```

Expected at this point: build FAILS. The accelerator still has package references to `Marilo.Components`, `Marilo.Providers.*` (now `Sunfish.Components.Blazor.Providers.*` per the rename) — NuGet can't resolve those package IDs because the Sunfish packages referenced via project paths haven't been wired in yet. The contamination gate passed, so identifier cleanup is done; package wiring is Task 9-3.

Typical expected failure:

```
error NU1101: Unable to find package Sunfish.Components.Blazor. No packages exist with this id in source(s)
```

This is the expected state before Task 9-3.

- [ ] **Step 6: Commit**

```bash
cd "C:/Projects/Sunfish"
git add accelerators/bridge/ scripts/migrate-bridge-accelerator.sh
git commit -m "feat(bridge-accelerator): rename all Marilo/PmDemo identifiers to Sunfish/Bridge"
```

---

## Task 9-3: Wire Sunfish Package References (ProjectReference, not NuGet)

Per decision **D-PROJECT-REFERENCES**, the accelerator references Sunfish packages via `<ProjectReference>` with relative paths, not NuGet. This task rewrites the csproj `PackageReference` entries (left over from Marilo's NuGet consumption) to `ProjectReference`.

**Files:**
- Modify: `accelerators/bridge/Sunfish.Bridge.Client/Sunfish.Bridge.Client.csproj`
- Modify: `accelerators/bridge/Sunfish.Bridge.Client/_Imports.razor`

- [ ] **Step 1: Rewrite Sunfish.Bridge.Client.csproj PackageReferences**

Replace the six Marilo-era `PackageReference` entries. The final csproj should look like:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Sunfish.Bridge.Client</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\packages\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\..\..\packages\ui-core\Sunfish.UICore.csproj" />
    <ProjectReference Include="..\..\..\packages\ui-adapters-blazor\Sunfish.Components.Blazor.csproj" />
  </ItemGroup>
</Project>
```

**Why three ProjectReferences, not six:** Phase 3c consolidates the Marilo `Providers.FluentUI`/`Providers.Bootstrap`/`Providers.Material` packages into `Sunfish.Components.Blazor.Providers.*` sub-namespaces of the unified Blazor adapter. The accelerator takes one reference to `Sunfish.Components.Blazor` and receives all three providers transitively. If Phase 3c hasn't shipped yet, add ProjectReferences to the individual provider packages when they land.

**Path arithmetic:** `accelerators/bridge/Sunfish.Bridge.Client/` → `..\..\..\packages\foundation\` traverses up three directories: `Sunfish.Bridge.Client` → `bridge` → `accelerators` → repo root, then down into `packages/foundation`.

- [ ] **Step 2: Update `_Imports.razor`**

Replace the Marilo-era usings in `Sunfish.Bridge.Client/_Imports.razor` (the rename sed pass mapped most of these, but verify). The final file should be:

```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.JSInterop
@using Sunfish.Foundation.Models
@using Sunfish.Foundation.Enums
@using Sunfish.Foundation.Notifications
@using Sunfish.Foundation.Authorization
@using Sunfish.Components.Blazor
@using Sunfish.Components.Blazor.Components.Buttons
@using Sunfish.Components.Blazor.Components.DataDisplay
@using Sunfish.Components.Blazor.Components.DataGrid
@using Sunfish.Components.Blazor.Components.Feedback
@using Sunfish.Components.Blazor.Components.Layout
@using Sunfish.Components.Blazor.Components.Navigation
@using Sunfish.Components.Blazor.Components.Forms.Inputs
@using Sunfish.Components.Blazor.Components.Utility
@using Sunfish.Components.Blazor.Shell
@using Sunfish.Bridge.Client
@using Sunfish.Bridge.Client.Layout
@using Sunfish.Bridge.Client.Models.Settings
@using Sunfish.Bridge.Client.Components.Settings
```

Notes:
- `Sunfish.Foundation.Notifications` and `Sunfish.Foundation.Authorization` are added in Task 9-5 but can be referenced now — the using compiles once the types move.
- `Sunfish.Components.Blazor.Shell` replaces `Marilo.Components.Layout.AppShell` — the shell was moved into its own sub-namespace during Phase 3b. If the actual Phase 3b landing used a different namespace, adjust here.

- [ ] **Step 3: Restore and build**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"
dotnet restore Sunfish.Bridge.slnx
dotnet build Sunfish.Bridge.slnx 2>&1 | tail -40
```

Expected: 0 errors. If there are errors, they should be about types from `Sunfish.Foundation.Notifications` that don't exist yet — those are added in Task 9-5.

If the build still fails with `NU1101` on any `Marilo.*` identifier, the rename missed a reference — inspect `dotnet restore` output and patch manually.

- [ ] **Step 4: Commit**

```bash
cd "C:/Projects/Sunfish"
git add accelerators/bridge/
git commit -m "feat(bridge-accelerator): wire Sunfish package references via ProjectReference"
```

---

## Task 9-4: Update DAB Config and Aspire AppHost Connection Strings

This task handles the domain-specific rewrites that the brand-sweep sed deliberately skipped: resource names and connection strings in `dab-config.json`, `AppHost/Program.cs`, `Program.cs` (server), and Aspire service-discovery identifiers. These are not brand identifiers — they're infrastructure handles.

**Files:**
- Modify: `accelerators/bridge/dab-config.json`
- Modify: `accelerators/bridge/Sunfish.Bridge.AppHost/Program.cs`
- Modify: `accelerators/bridge/Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj`
- Modify: `accelerators/bridge/Sunfish.Bridge/Program.cs`
- Modify: `accelerators/bridge/Sunfish.Bridge.MigrationService/Sunfish.Bridge.MigrationService.csproj` (UserSecretsId)
- Modify: `accelerators/bridge/Sunfish.Bridge/appsettings.json` and `appsettings.Development.json`

- [ ] **Step 1: Rewrite `dab-config.json` connection-string env var**

In `accelerators/bridge/dab-config.json`:

```diff
   "data-source": {
     "database-type": "postgresql",
-    "connection-string": "@env('ConnectionStrings__pmdemodb')",
+    "connection-string": "@env('ConnectionStrings__sunfishbridgedb')",
     "options": {
       "set-session-context": false
     }
   },
```

No other changes in `dab-config.json` — the entity list (Project, ProjectMember, Task, Subtask, Comment, Milestone, Risk, BudgetLine, AuditRecord) and role matrix (Owner, Admin, ProjectManager, TeamMember, Viewer) are the domain model and stay as-is.

DAB config schema version stays at `https://github.com/Azure/data-api-builder/releases/latest/download/dab.draft.schema.json`.

- [ ] **Step 2: Rewrite `Sunfish.Bridge.AppHost/Program.cs`**

The file currently has resource names with the `pmdemo*` prefix. Rewrite to `bridge*` / `sunfishbridgedb`. Target content:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure resources
var postgresServer = builder.AddPostgres("sunfishbridgedb-server")
    .WithDataVolume();
var postgres = postgresServer.AddDatabase("sunfishbridgedb");

var redis = builder.AddRedis("bridge-redis");

var rabbit = builder.AddRabbitMQ("bridge-rabbit")
    .WithManagementPlugin();

// DEMO ONLY. MockOktaService is a minimal OIDC mock for local development.
// Replace with real Okta / Entra ID / Auth0 configuration before production.
// See accelerators/bridge/ROADMAP.md §Auth.
var okta = builder.AddProject<Projects.MockOktaService>("mock-okta");

// One-shot migration runner. Applies EF Core migrations and exits. DAB and the
// web project WaitForCompletion on this so the schema exists before either reads it.
var migrations = builder.AddProject<Projects.Sunfish_Bridge_MigrationService>("bridge-migrations")
    .WithReference(postgres)
    .WaitFor(postgres);

// Data API Builder — exposes the Postgres schema as GraphQL.
// dab-config.json lives next to the .slnx and is bind-mounted into the container.
// WithReference(postgres) injects ConnectionStrings__sunfishbridgedb with the correct
// container-to-container hostname (sunfishbridgedb-server, NOT localhost). dab-config.json
// reads it via @env('ConnectionStrings__sunfishbridgedb').
var dabConfigPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "dab-config.json");
var dab = builder.AddContainer("bridge-dab", "mcr.microsoft.com/azure-databases/data-api-builder", "latest")
    .WithBindMount(dabConfigPath, "/App/dab-config.json", isReadOnly: true)
    .WithReference(postgres)
    .WithHttpEndpoint(targetPort: 5000, name: "graphql")
    .WaitForCompletion(migrations);

// Server project
builder.AddProject<Projects.Sunfish_Bridge>("bridge-web")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(rabbit)
    .WithReference(okta)
    .WithEnvironment("DAB_GRAPHQL_URL", dab.GetEndpoint("graphql"))
    .WaitForCompletion(migrations)
    .WaitFor(redis)
    .WaitFor(rabbit)
    .WaitFor(dab);

builder.Build().Run();
```

Notes:
- `"sunfishbridgedb-server"` is the Postgres *container* name; `"sunfishbridgedb"` is the *database* name inside it. Aspire's `WithReference(postgres)` injects `ConnectionStrings__sunfishbridgedb`.
- `Projects.Sunfish_Bridge_MigrationService` and `Projects.Sunfish_Bridge` are the Aspire-generated project type names; dots in project names become underscores. The sed pass rewrote `Marilo_PmDemo` → `Sunfish_Bridge` but verify.

- [ ] **Step 3: Rewrite `Sunfish.Bridge/Program.cs` connection-string reads**

In `Sunfish.Bridge/Program.cs`, the DbContext registration reads:

```diff
 builder.Services.AddDbContext<SunfishBridgeDbContext>(options =>
-    options.UseNpgsql(builder.Configuration.GetConnectionString("pmdemodb")));
+    options.UseNpgsql(builder.Configuration.GetConnectionString("sunfishbridgedb")));
 builder.EnrichNpgsqlDbContext<SunfishBridgeDbContext>();

-builder.AddRedisOutputCache("pmdemo-redis");
+builder.AddRedisOutputCache("bridge-redis");

 builder.Services.AddSignalR()
-    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("pmdemo-redis") ?? "localhost");
+    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("bridge-redis") ?? "localhost");
```

Also update the Wolverine config block:

```diff
-    var rabbitConn = builder.Configuration.GetConnectionString("pmdemo-rabbit");
+    var rabbitConn = builder.Configuration.GetConnectionString("bridge-rabbit");
     ...
-    var pgConn = builder.Configuration.GetConnectionString("pmdemodb");
+    var pgConn = builder.Configuration.GetConnectionString("sunfishbridgedb");
```

And the SignalR hub map line:

```diff
-app.MapHub<PmDemoHub>("/hubs/pmdemo");
+app.MapHub<BridgeHub>("/hubs/bridge");
```

The sed pass renamed `PmDemoHub` → `BridgeHub` but the **route path** `"/hubs/pmdemo"` is a string literal that needs a manual edit to `"/hubs/bridge"` (string literals were not in the sed scope).

- [ ] **Step 4: Rewrite UserSecretsIds**

In `Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj`:

```diff
-    <UserSecretsId>marilo-pmdemo-apphost</UserSecretsId>
+    <UserSecretsId>sunfish-bridge-apphost</UserSecretsId>
```

In `Sunfish.Bridge.MigrationService/Sunfish.Bridge.MigrationService.csproj`:

```diff
-    <UserSecretsId>marilo-pmdemo-migrations</UserSecretsId>
+    <UserSecretsId>sunfish-bridge-migrations</UserSecretsId>
```

- [ ] **Step 5: Verify Aspire package references align with D-ASPIRE-VERSION**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"
grep -rE 'Aspire\.(Hosting|Npgsql|StackExchange)' \
     Sunfish.Bridge.AppHost/*.csproj \
     Sunfish.Bridge/*.csproj \
     Sunfish.Bridge.MigrationService/*.csproj
```

Expected: `Aspire.Hosting.AppHost`, `Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.Redis`, `Aspire.Hosting.RabbitMQ`, `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, `Aspire.StackExchange.Redis.OutputCaching`. All without explicit `Version="..."` attributes (versions come from `Directory.Packages.props`).

Verify root-level `Directory.Packages.props` contains pinned versions for each Aspire package above. If any are missing, add them. Expected Aspire version family: 13.2.x.

```bash
grep -E 'Aspire\.' "C:/Projects/Sunfish/Directory.Packages.props"
```

Also confirm the AppHost SDK version:

```bash
grep 'Aspire.AppHost.Sdk' "C:/Projects/Sunfish/accelerators/bridge/Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj"
```

Expected: `<Sdk Name="Aspire.AppHost.Sdk" Version="13.2.1" />`.

- [ ] **Step 6: Build Aspire AppHost**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"
dotnet build Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj 2>&1 | tail -20
```

Expected: 0 errors, 0 warnings. If it complains about `Projects.Sunfish_Bridge_MigrationService` not found, the csproj rename in Task 9-2 Step 1 or the project reference in the AppHost csproj wasn't updated. Inspect `Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj` and confirm the `<ProjectReference>` elements point at the renamed csprojs.

- [ ] **Step 7: Commit**

```bash
cd "C:/Projects/Sunfish"
git add accelerators/bridge/
git commit -m "feat(bridge-accelerator): update DAB config, Aspire resource names, and connection strings"
```

---

## Task 9-5: Extract Reusable Patterns to Foundation (D7)

Implements master plan D7 — moves the canonical notification pipeline and tenant context from Bridge-owned code to `packages/foundation`. After this task, any future accelerator can take a `PackageReference` on `Sunfish.Foundation` and get `IUserNotificationService` + `UserNotification` + `ITenantContext` without coupling to Bridge.

**What moves vs stays:**

| Type | From | To | Rationale |
|---|---|---|---|
| `UserNotification` (record) | Bridge.Client.Notifications | `Sunfish.Foundation.Notifications` | Transport shape; framework-agnostic |
| Notification enums (Source, Category, Importance, Delivery) | Bridge.Client.Notifications | `Sunfish.Foundation.Notifications` | Same |
| `IUserNotificationService` | Bridge.Client.Notifications | `Sunfish.Foundation.Notifications` | Contract only |
| `IUserNotificationToastForwarder` | Bridge.Client.Notifications | `Sunfish.Foundation.Notifications` | Framework-agnostic seam |
| `InMemoryUserNotificationService` | Bridge.Client.Notifications | **stays** in Bridge | Impl; demo-specific defaults |
| `NotificationFeedProjection` | Bridge.Client.Notifications | **stays** in Bridge | Depends on bell's `NotificationItem` DTO (ui-adapters-blazor) |
| `SunfishToastUserNotificationForwarder` | Bridge.Client.Notifications | **stays** in Bridge | Depends on `NotificationModel` (ui-adapters-blazor) |
| `ITenantContext` | Bridge.Data.Authorization | `Sunfish.Foundation.Authorization` | Contract; used by any tenant-scoped persistence |
| `DemoTenantContext` | Bridge.Authorization | **stays** in Bridge | Demo-only impl |
| `Permissions`, `Roles` | Bridge.Data.Authorization | **stays** in Bridge | Bridge domain-specific values |

**Files:**
- Create: `packages/foundation/Notifications/UserNotification.cs`
- Create: `packages/foundation/Notifications/NotificationEnums.cs`
- Create: `packages/foundation/Notifications/IUserNotificationService.cs`
- Create: `packages/foundation/Notifications/IUserNotificationToastForwarder.cs`
- Create: `packages/foundation/Authorization/ITenantContext.cs`
- Modify: `accelerators/bridge/Sunfish.Bridge.Client/Notifications/UserNotification.cs` → DELETE (replaced by foundation)
- Modify: `accelerators/bridge/Sunfish.Bridge.Client/Notifications/IUserNotificationService.cs` → DELETE
- Modify: `accelerators/bridge/Sunfish.Bridge.Client/Notifications/InMemoryUserNotificationService.cs` → update using
- Modify: `accelerators/bridge/Sunfish.Bridge.Client/Notifications/NotificationProjections.cs` → update using
- Modify: `accelerators/bridge/Sunfish.Bridge.Data/Authorization/ITenantContext.cs` → DELETE (replaced)
- Modify: `accelerators/bridge/Sunfish.Bridge.Data/SunfishBridgeDbContext.cs` → update using
- Modify: `accelerators/bridge/Sunfish.Bridge/Authorization/DemoTenantContext.cs` → update using
- Modify: `accelerators/bridge/Sunfish.Bridge.MigrationService/MigrationTenantContext.cs` → update using

- [ ] **Step 1: Create foundation notification files**

Create `packages/foundation/Notifications/UserNotification.cs` — copy the record and related types from `accelerators/bridge/Sunfish.Bridge.Client/Notifications/UserNotification.cs`, changing the namespace:

```csharp
namespace Sunfish.Foundation.Notifications;

// ... record body unchanged ...
```

Create `packages/foundation/Notifications/NotificationEnums.cs` with the four enums (`NotificationSource`, `NotificationCategory`, `NotificationImportance`, `NotificationDelivery`) that currently live alongside `UserNotification`.

Create `packages/foundation/Notifications/IUserNotificationService.cs`:

```csharp
namespace Sunfish.Foundation.Notifications;

public interface IUserNotificationService
{
    IReadOnlyList<UserNotification> All { get; }
    int UnreadCount { get; }
    event Action? Changed;
    Task CreateAsync(UserNotification notification, CancellationToken ct = default);
    Task MarkReadAsync(Guid id, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
    Task DeleteAllReadAsync(CancellationToken ct = default);
}
```

Create `packages/foundation/Notifications/IUserNotificationToastForwarder.cs`:

```csharp
namespace Sunfish.Foundation.Notifications;

/// <summary>
/// Seam for presenting a <see cref="UserNotification"/> on a toast surface.
/// Implementations live in UI adapters (Blazor, React) because they depend
/// on the adapter's toast host.
/// </summary>
public interface IUserNotificationToastForwarder
{
    Task ForwardAsync(UserNotification notification, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create foundation tenant-context file**

Create `packages/foundation/Authorization/ITenantContext.cs`:

```csharp
namespace Sunfish.Foundation.Authorization;

/// <summary>
/// Resolves the current tenant and caller identity. Scoped per request.
/// Accelerators / apps register an implementation (e.g. claims-based) in DI.
/// </summary>
public interface ITenantContext
{
    string TenantId { get; }
    string UserId { get; }
    IReadOnlyList<string> Roles { get; }
    bool HasPermission(string permission);
}
```

- [ ] **Step 3: Delete the Bridge duplicates, update usings on impls**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"
rm Sunfish.Bridge.Client/Notifications/UserNotification.cs
rm Sunfish.Bridge.Client/Notifications/IUserNotificationService.cs
rm Sunfish.Bridge.Data/Authorization/ITenantContext.cs
```

For `InMemoryUserNotificationService.cs`, `NotificationProjections.cs`, `SunfishBridgeDbContext.cs`, `DemoTenantContext.cs`, and `MigrationTenantContext.cs` — replace the old using:

```diff
-using Sunfish.Bridge.Client.Notifications;         // (removed — types moved)
-using Sunfish.Bridge.Data.Authorization;
+using Sunfish.Foundation.Notifications;
+using Sunfish.Foundation.Authorization;
```

Note: the `Permissions` and `Roles` classes still live in `Sunfish.Bridge.Data.Authorization` — any code referring to them keeps that using.

- [ ] **Step 4: Build foundation and Bridge**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/foundation/Sunfish.Foundation.csproj 2>&1 | tail -5
dotnet build accelerators/bridge/Sunfish.Bridge.slnx 2>&1 | tail -10
```

Expected: both build with 0 errors. Any lingering `Sunfish.Bridge.Client.Notifications.UserNotification` usages indicate a missed file — inspect and patch.

- [ ] **Step 5: Add foundation unit tests (light coverage)**

Add to `packages/foundation/tests/tests.csproj` tests folder (e.g., `NotificationContractsTests.cs`):

```csharp
using Sunfish.Foundation.Notifications;
using Xunit;

namespace Sunfish.Foundation.Tests;

public class NotificationContractsTests
{
    [Fact]
    public void UserNotification_CanBeConstructed_WithAllFields()
    {
        var n = new UserNotification(
            Id: Guid.NewGuid(),
            Title: "Task assigned",
            Body: "A new task was assigned to you.",
            Source: NotificationSource.System,
            Category: NotificationCategory.Task,
            Importance: NotificationImportance.Normal,
            CorrelationKey: "task:123",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            IsRead: false);
        Assert.False(n.IsRead);
        Assert.Equal("Task assigned", n.Title);
    }
}
```

Run:

```bash
dotnet test packages/foundation/tests/tests.csproj 2>&1 | tail -10
```

Expected: all foundation tests pass. Count bumps up from the existing baseline (Phase 1 ships with 3 passing).

- [ ] **Step 6: Commit**

```bash
cd "C:/Projects/Sunfish"
git add packages/foundation/Notifications/ packages/foundation/Authorization/ \
        packages/foundation/tests/ \
        accelerators/bridge/
git commit -m "feat(foundation): extract notification pipeline and ITenantContext from Bridge accelerator (D7)"
```

---

## Task 9-6: Update ROADMAP.md and Run Smoke Tests

**Files:**
- Modify: `accelerators/bridge/ROADMAP.md`
- Run: `accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/`

The migration sed pass already rewrote most of `ROADMAP.md`, but the doc needs (a) a migration-header note, (b) a rewritten table-of-status so future readers understand this document was preserved during migration, and (c) verification that all `Marilo`/`PmDemo` text is gone.

- [ ] **Step 1: Prepend migration header to ROADMAP.md**

At the very top of `accelerators/bridge/ROADMAP.md`, insert:

```markdown
# Bridge Accelerator — Roadmap

> **Migration note:** This document was preserved from `Marilo.PmDemo/SETTINGS_STATUS.md`
> during the Phase 9 Sunfish migration. Sections 1–2 (canonical notification pipeline,
> shell footer) and Section 3 (MainLayout) are **DONE**. Sections 4–12 (Account pages,
> shared settings components, services, data model, build order) document the original
> roadmap and are **NOT completed** as part of the migration. These remain as the
> forward work plan for the Bridge accelerator.
>
> The canonical notification pipeline has been promoted to `packages/foundation/Notifications/`
> (see Phase 9, Task 9-5). References to `IUserNotificationService` in this doc now
> resolve to `Sunfish.Foundation.Notifications.IUserNotificationService`.
>
> Demo-only auth seams (`DemoTenantContext`, `MockOktaService`) remain in place with
> explicit annotations and startup warnings; replace before production deployment.

---

```

- [ ] **Step 2: Verify no Marilo/PmDemo residue in ROADMAP.md**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"
grep -nE 'Marilo|PmDemo|Mar(ilo)?[A-Z]|@marilo|pmdemodb' ROADMAP.md || echo "CLEAN"
```

Expected: `CLEAN`. If matches, inspect — some may be historical context that should be preserved as "was called X, now called Y". Prefer to rewrite to Sunfish naming in all cases except a one-line "migrated from Marilo.PmDemo" attribution.

- [ ] **Step 3: Run the smoke tests**

```bash
cd "C:/Projects/Sunfish"
dotnet test accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/Sunfish.Bridge.Tests.Unit.csproj -v normal 2>&1 | tail -20
```

Expected: `SeederSmokeTests` passes. These tests exercise `BridgeSeeder` (renamed from `PmDemoSeeder`) against an in-memory/`Testcontainers` Postgres instance. If the tests reference the old class name, the sed pass missed them — inspect `SeederSmokeTests.cs` and fix.

Integration tests (`Sunfish.Bridge.Tests.Integration/HealthCheckTests.cs`) require a live Aspire orchestration, so they are NOT run in this step. They are exercised in Task 9-7 / 9-8 / 9-9 local validation.

- [ ] **Step 4: Commit**

```bash
cd "C:/Projects/Sunfish"
git add accelerators/bridge/ROADMAP.md
git commit -m "docs(bridge-accelerator): update ROADMAP.md with migration header and Sunfish branding"
```

---

## Task 9-7: Rebaseline EF Migrations After DbContext Rename (D-EF-MIGRATION-CAPTURE)

Per decision D-EF-MIGRATION-CAPTURE, renaming `PmDemoDbContext` → `SunfishBridgeDbContext` invalidates the existing model snapshot. This task rebaselines EF Core by running a no-op `migrations add` that captures the new context name + namespace in the snapshot.

**Files:**
- Modify: `accelerators/bridge/Sunfish.Bridge.Data/Migrations/20260407195529_Initial.cs` (namespace check)
- Modify: `accelerators/bridge/Sunfish.Bridge.Data/Migrations/20260407195529_Initial.Designer.cs` (namespace + ContextTypeAttribute)
- Modify: `accelerators/bridge/Sunfish.Bridge.Data/Migrations/SunfishBridgeDbContextModelSnapshot.cs` (namespace, class name, attribute)
- Create: `accelerators/bridge/Sunfish.Bridge.Data/Migrations/<timestamp>_PostRenameSnapshot.cs`
- Create: `accelerators/bridge/Sunfish.Bridge.Data/Migrations/<timestamp>_PostRenameSnapshot.Designer.cs`

- [ ] **Step 1: Verify existing snapshot files were renamed/rewritten by the sed pass**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge/Sunfish.Bridge.Data/Migrations"
grep -l 'PmDemoDbContext\|namespace Marilo' *.cs || echo "CLEAN"
head -3 SunfishBridgeDbContextModelSnapshot.cs
head -3 20260407195529_Initial.Designer.cs
```

Expected: `CLEAN`. The first 3 lines of each should reference `Sunfish.Bridge.Data` (not `Marilo.PmDemo.Data`) and `SunfishBridgeDbContext` (not `PmDemoDbContext`). The `[DbContext(typeof(SunfishBridgeDbContext))]` attribute on the snapshot class should be in place from the sed pass.

If any residue remains, edit the snapshot files manually:
- `SunfishBridgeDbContextModelSnapshot.cs` — class name should be `SunfishBridgeDbContextModelSnapshot`, attribute `[DbContext(typeof(SunfishBridgeDbContext))]`, namespace `Sunfish.Bridge.Data.Migrations`.
- `20260407195529_Initial.Designer.cs` — `[DbContext(typeof(SunfishBridgeDbContext))]`, namespace `Sunfish.Bridge.Data.Migrations`.
- `20260407195529_Initial.cs` — namespace `Sunfish.Bridge.Data.Migrations`.

The `Up`/`Down` SQL inside `20260407195529_Initial.cs` references table names only (`projects`, `tasks`, `subtasks`, ...), not C# identifiers, so it's unaffected by the rename.

- [ ] **Step 2: Verify DesignTimeDbContextFactory targets the renamed context**

Open `accelerators/bridge/Sunfish.Bridge.Data/DesignTimeDbContextFactory.cs`:

```bash
grep -n 'DbContext\|namespace' "C:/Projects/Sunfish/accelerators/bridge/Sunfish.Bridge.Data/DesignTimeDbContextFactory.cs"
```

Expected: implements `IDesignTimeDbContextFactory<SunfishBridgeDbContext>` in namespace `Sunfish.Bridge.Data`. If still references `PmDemoDbContext`, edit manually.

- [ ] **Step 3: Build the data project to confirm everything compiles before running EF CLI**

```bash
cd "C:/Projects/Sunfish"
dotnet build accelerators/bridge/Sunfish.Bridge.Data/Sunfish.Bridge.Data.csproj 2>&1 | tail -10
```

Expected: 0 errors, 0 warnings. `dotnet ef` requires a compiling project to reflect on.

- [ ] **Step 4: Run the rebaseline migration**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"

# PostRenameSnapshot is a no-op migration whose sole purpose is to force EF
# to regenerate the model snapshot with the new DbContext name in the header.
dotnet ef migrations add PostRenameSnapshot \
    --project Sunfish.Bridge.Data/Sunfish.Bridge.Data.csproj \
    --startup-project Sunfish.Bridge.MigrationService/Sunfish.Bridge.MigrationService.csproj \
    --context SunfishBridgeDbContext \
    --output-dir Migrations
```

Expected console output:

```
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
```

Two new files appear:
- `Migrations/<yyyyMMddHHmmss>_PostRenameSnapshot.cs`
- `Migrations/<yyyyMMddHHmmss>_PostRenameSnapshot.Designer.cs`

- [ ] **Step 5: Verify the new migration is empty (no schema changes)**

```bash
cat "C:/Projects/Sunfish/accelerators/bridge/Sunfish.Bridge.Data/Migrations/"*_PostRenameSnapshot.cs
```

Expected: both `protected override void Up(MigrationBuilder migrationBuilder)` and `Down(...)` bodies are empty (or contain only whitespace and comments). If they contain `AddColumn`, `AlterColumn`, `CreateIndex`, or similar — the rename left a stale column/index mapping somewhere. Diagnose by:

```bash
grep -n 'AddColumn\|AlterColumn\|CreateIndex\|RenameTable\|RenameColumn\|DropTable' \
    "C:/Projects/Sunfish/accelerators/bridge/Sunfish.Bridge.Data/Migrations/"*_PostRenameSnapshot.cs
```

If matches are found, roll back (`dotnet ef migrations remove`) and investigate — most likely a `[Table("xxx")]` or `modelBuilder.Entity<X>().ToTable(...)` call in `SunfishBridgeDbContext.OnModelCreating` wasn't preserved.

- [ ] **Step 6: Run the MigrationService locally to apply the new migration**

(Optional validation — skip if no local Postgres; covered in Task 9-9 local-Aspire run.)

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"
# Launches Aspire, applies migrations, stops.
dotnet run --project Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj &
APPHOST_PID=$!
sleep 45   # wait for migration worker to complete
kill $APPHOST_PID 2>/dev/null || true
```

Expected: in the Aspire dashboard / logs, `bridge-migrations` reaches "Completed" state, indicating both `Initial` and `PostRenameSnapshot` applied cleanly against a fresh Postgres volume.

- [ ] **Step 7: Commit**

```bash
cd "C:/Projects/Sunfish"
git add accelerators/bridge/Sunfish.Bridge.Data/Migrations/
git commit -m "chore(bridge-accelerator): rebaseline EF snapshot after DbContext rename (D-EF-MIGRATION-CAPTURE)"
```

---

## Task 9-8: Add Startup Warning Logs for Demo-Only Auth Seams (D-AUTH-SEAM-LOGGING)

Per decision D-AUTH-SEAM-LOGGING, the two demo-only auth seams emit `LogLevel.Warning` messages at startup so consumers can't miss them in console output.

**Files:**
- Create: `accelerators/bridge/Sunfish.Bridge/Authorization/DemoAuthWarningFilter.cs`
- Modify: `accelerators/bridge/Sunfish.Bridge/Authorization/DemoTenantContext.cs` (constructor log)
- Modify: `accelerators/bridge/Sunfish.Bridge/Program.cs` (register filter + add log line near MockOkta)
- Modify: `accelerators/bridge/Sunfish.Bridge.AppHost/Program.cs` (emit warning before `Run()`)

- [ ] **Step 1: Add log emission to `DemoTenantContext` constructor**

Modify `accelerators/bridge/Sunfish.Bridge/Authorization/DemoTenantContext.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Sunfish.Foundation.Authorization;

namespace Sunfish.Bridge.Authorization;

/// <summary>
/// DEMO ONLY. Provides a hardcoded tenant/user identity for local development.
/// Replace with a real <see cref="ITenantContext"/> implementation that reads
/// from authenticated claims (OIDC, Entra, Okta) before production deployment.
/// See accelerators/bridge/ROADMAP.md §Auth for replacement guidance.
/// </summary>
public sealed class DemoTenantContext : ITenantContext
{
    private static int _warningLogged;   // emit once per process

    public DemoTenantContext(ILogger<DemoTenantContext> logger)
    {
        if (System.Threading.Interlocked.CompareExchange(ref _warningLogged, 1, 0) == 0)
        {
            logger.LogWarning(
                "DEMO AUTH SEAM ACTIVE: DemoTenantContext is registered. " +
                "TenantId='{TenantId}', UserId='{UserId}'. " +
                "This is for local development only. Replace with a real ITenantContext implementation " +
                "before production deployment. See accelerators/bridge/ROADMAP.md §Auth.",
                TenantId, UserId);
        }
    }

    public string TenantId => "demo-tenant";
    public string UserId => "demo-user";
    public IReadOnlyList<string> Roles { get; } = [Sunfish.Bridge.Data.Authorization.Roles.ProjectManager];
    public bool HasPermission(string permission) => true;
}

internal static class Roles
{
    public const string ProjectManager = Sunfish.Bridge.Data.Authorization.Roles.ProjectManager;
}
```

Because the context is registered `Scoped`, the constructor runs per-request. The `Interlocked` guard ensures the warning emits once per process lifetime — just on the first request — instead of spamming every request.

- [ ] **Step 2: Add an `IStartupFilter` for MockOkta warning (server side)**

Create `accelerators/bridge/Sunfish.Bridge/Authorization/DemoAuthWarningFilter.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Sunfish.Bridge.Authorization;

/// <summary>
/// Emits a startup warning when the web app boots in demo-auth mode.
/// Registered only when <c>IWebHostEnvironment.IsDevelopment()</c> is true
/// (same gate that registers DemoTenantContext).
/// </summary>
public sealed class DemoAuthWarningFilter : IStartupFilter
{
    private readonly ILogger<DemoAuthWarningFilter> _logger;

    public DemoAuthWarningFilter(ILogger<DemoAuthWarningFilter> logger) => _logger = logger;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        _logger.LogWarning(
            "DEMO AUTH SEAM ACTIVE: Bridge is running with demo authentication wiring " +
            "(DemoTenantContext + MockOktaService). This is for local development only. " +
            "Replace with real ITenantContext + Okta/Entra/Auth0 configuration before production.");
        next(app);
    };
}
```

Register in `Sunfish.Bridge/Program.cs` inside the `if (builder.Environment.IsDevelopment())` block (next to the DemoTenantContext registration):

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<ITenantContext, DemoTenantContext>();
    builder.Services.AddSingleton<IStartupFilter, DemoAuthWarningFilter>();
}
```

- [ ] **Step 3: Emit an AppHost-level warning**

In `Sunfish.Bridge.AppHost/Program.cs`, immediately before `builder.Build().Run();`:

```csharp
// DEMO AUTH SEAM WARNING — surfaces in Aspire dashboard console on boot.
Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  DEMO AUTH SEAM ACTIVE                                                     ║");
Console.WriteLine("║                                                                            ║");
Console.WriteLine("║  MockOktaService is registered as the OIDC provider. This is for local     ║");
Console.WriteLine("║  development only. Replace with real Okta / Entra ID / Auth0 configuration ║");
Console.WriteLine("║  before production deployment. See accelerators/bridge/ROADMAP.md §Auth.   ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

builder.Build().Run();
```

(`Console.WriteLine` is used here instead of `ILogger` because the AppHost `Program.cs` runs before the app host's logger is established — the warning needs to surface in the plain console output so it's visible in the Aspire dashboard before any structured log emerges.)

- [ ] **Step 4: Build and run the solution**

```bash
cd "C:/Projects/Sunfish"
dotnet build accelerators/bridge/Sunfish.Bridge.slnx 2>&1 | tail -10
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 5: Local smoke run — confirm warnings appear**

```bash
cd "C:/Projects/Sunfish/accelerators/bridge"
dotnet run --project Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj &
APPHOST_PID=$!
sleep 30
# In another terminal / browser: navigate to https://localhost:XXXX (from launchSettings.json)
# to trigger DemoTenantContext ctor. Check the bridge-web resource logs.
kill $APPHOST_PID 2>/dev/null || true
```

Expected (in AppHost console, before Run):

```
╔════════════════════════════════════════════════════════════════════════════╗
║  DEMO AUTH SEAM ACTIVE                                                     ║
...
```

Expected (in `bridge-web` Aspire log stream, after first request):

```
warn: Sunfish.Bridge.Authorization.DemoAuthWarningFilter[0]
      DEMO AUTH SEAM ACTIVE: Bridge is running with demo authentication wiring (DemoTenantContext + MockOktaService)...
warn: Sunfish.Bridge.Authorization.DemoTenantContext[0]
      DEMO AUTH SEAM ACTIVE: DemoTenantContext is registered. TenantId='demo-tenant', UserId='demo-user'...
```

- [ ] **Step 6: Commit**

```bash
cd "C:/Projects/Sunfish"
git add accelerators/bridge/
git commit -m "feat(bridge-accelerator): emit startup warnings for demo-only auth seams (D-AUTH-SEAM-LOGGING)"
```

---

## Task 9-9: Update Root README to Showcase Bridge as Reference Implementation

**Files:**
- Modify: `README.md`
- Create: `accelerators/bridge/README.md` (per-accelerator README)

- [ ] **Step 1: Create `accelerators/bridge/README.md`**

```markdown
# Bridge — Solution Accelerator

Bridge is the reference Sunfish solution accelerator: a full-stack project-management
app that composes every tier of the Sunfish stack into a single working solution.

## What Bridge demonstrates

| Tier | How Bridge uses it |
|---|---|
| `packages/foundation` | `IUserNotificationService`, `ITenantContext`, CSS class/style builders |
| `packages/ui-core` | Framework-agnostic contracts (CSS/icon providers, theme service) |
| `packages/ui-adapters-blazor` | All 181 Sunfish Blazor components; shell; AppShell; bell; grid |
| `packages/blocks-*` | Forms, tasks, scheduling composites (as they land) |
| EF Core 10 / Postgres | `SunfishBridgeDbContext` with per-tenant query filters |
| .NET Aspire 13 | `Sunfish.Bridge.AppHost` orchestrates Postgres, Redis, RabbitMQ, DAB, MockOkta |
| Data API Builder | Postgres schema → GraphQL via `dab-config.json` |
| SignalR + Wolverine | `BridgeHub` for real-time updates; Wolverine RabbitMQ transport with Postgres outbox |

## Running Bridge locally

```bash
cd accelerators/bridge
dotnet run --project Sunfish.Bridge.AppHost
```

Aspire dashboard → https://localhost:17123 (port from `launchSettings.json`).

## Demo auth — IMPORTANT

Bridge boots with **demo-only** auth wiring:

- `DemoTenantContext` returns a hardcoded `demo-tenant` / `demo-user`.
- `MockOktaService` provides a minimal OIDC mock.

Both emit a **startup warning** log. See [ROADMAP.md](ROADMAP.md) §Auth for
replacement guidance before production deployment.

## Roadmap

See [ROADMAP.md](ROADMAP.md) for in-progress and planned work.

## Structure

```
accelerators/bridge/
  Sunfish.Bridge/                      Server (Blazor Server host)
  Sunfish.Bridge.Client/               Client RCL (interactive components)
  Sunfish.Bridge.Data/                 EF Core data layer
  Sunfish.Bridge.AppHost/              Aspire orchestration
  Sunfish.Bridge.ServiceDefaults/      Aspire shared defaults (OTEL, health, resilience)
  Sunfish.Bridge.MigrationService/     One-shot EF migration worker
  MockOktaService/                     DEMO ONLY — mock OIDC provider
  dab-config.json                      DAB GraphQL schema config
  tests/                               Unit, integration, performance
```

## Standalone solution

Bridge has its own `Sunfish.Bridge.slnx` — it is **not** part of the root
`Sunfish.slnx`. Run `dotnet build Sunfish.Bridge.slnx` from `accelerators/bridge/`
to build only the accelerator and its Sunfish package dependencies.
```

- [ ] **Step 2: Add Bridge callout to root README.md**

Find the existing "Solution Accelerators" bullet in `C:/Projects/Sunfish/README.md` (around line 28) and the repository-layout block (around line 48). Replace the `bridge/` line with a more descriptive entry and add a new short section:

Before the "Solution Accelerators" section, after the "Blocks & Modules" section, insert:

```markdown
- **Solution Accelerators**
  Opinionated, ready-to-extend starter solutions composed from Sunfish building blocks.
  [Bridge](accelerators/bridge/README.md) is the reference implementation — a full-stack
  project-management app that demonstrates the whole Sunfish stack end-to-end (Blazor
  Server, .NET Aspire, EF Core + Postgres, DAB, SignalR, Wolverine messaging).
```

In the repository-layout tree, update:

```diff
   accelerators/
-    bridge/              # first solution accelerator — Bridge (in progress)
+    bridge/              # Bridge — reference solution accelerator (full-stack PM app)
```

- [ ] **Step 3: Commit**

```bash
cd "C:/Projects/Sunfish"
git add README.md accelerators/bridge/README.md
git commit -m "docs: showcase Bridge accelerator as reference implementation in root README"
```

---

## Task 9-10: Platform-Spec Alignment Roadmap (NEW)

Bridge ships from Phase 9 as a **functional** property-management accelerator, but the platform specification (`docs/specifications/sunfish-platform-specification.md`) describes capabilities Bridge does not yet exercise — specifically the decentralization and federation primitives. This task produces the alignment roadmap document that tracks what exists post-Phase-9 vs. what Bridge needs to reach spec-aligned status.

**Files:**
- Create: `accelerators/bridge/PLATFORM_ALIGNMENT.md`

The alignment doc is the companion to ROADMAP.md — ROADMAP captures **feature** work (new pages, new workflows), PLATFORM_ALIGNMENT captures **platform-primitive adoption** work (switching from DemoTenantContext to Macaroon-based delegation, adopting the cryptographic ownership kernel primitive when it exists, wiring federation).

- [ ] **Step 1: Inventory current state vs spec**

Create `accelerators/bridge/PLATFORM_ALIGNMENT.md` with this template:

```markdown
# Bridge Platform Alignment

This document tracks Bridge's adoption of Sunfish platform primitives as defined in
`docs/specifications/sunfish-platform-specification.md`. Bridge is the property-management
vertical reference implementation — it should exercise every kernel primitive the platform
defines. Gaps are tracked here with target phases.

Legend: ✅ adopted · 🟡 partially adopted · 🔴 not adopted · ⚪ N/A

## Spec Section 3 — Core Kernel Primitives

| Kernel Primitive | Bridge Status | Notes |
|---|---|---|
| Entity storage (multi-versioned) | 🟡 | EF Core entities are versioned via audit columns (CreatedAt/UpdatedAt) but not temporal tables; spec calls for as-of queries |
| Schema registry | 🔴 | Entities use compile-time types only; no runtime schema registry |
| Permissions (ABAC/RBAC evaluator) | 🟡 | Basic RBAC via Permissions.cs + Roles.cs; no policy language or decision engine |
| Audit trail | 🟡 | AuditRecord entity exists; not all mutations emit audit events |
| Event stream | 🔴 | Wolverine handles workflow events but no canonical domain event stream for external consumers |

## Spec Section 2 — Decentralized Primitives

| Primitive | Bridge Status | Notes |
|---|---|---|
| Cryptographic ownership proofs | 🔴 | No crypto primitives in Foundation yet; DemoTenantContext uses tenant IDs as strings. Candidate implementation: Keyhive-style Ed25519 + BeeKEM (see `docs/specifications/research-notes/automerge-evaluation.md`) |
| Delegation / time-bound access | 🔴 | Not implemented. Candidate: Keyhive group-membership graphs (primary) + Macaroon-style ephemeral tokens (supplement for short-lived scenarios) |
| Federation (peer-to-peer sync) | 🔴 | Single-server deployment; no federation endpoints. Candidate: Automerge-style sync protocol shape adapted for .NET; see evaluation doc for integration paths (sidecar vs native .NET rewrite) |

## Spec Section 6 — Property Management MVP Coverage

| MVP Feature | Bridge Status | Notes |
|---|---|---|
| Tenant leases | 🔴 | Not modeled; Bridge currently uses generic TaskItem entities |
| Rent collection | 🔴 | Not implemented |
| Inspection scheduling & documentation | 🔴 | Bridge has generic tasks but no inspection-specific workflow |
| Maintenance requests + vendor quotes | 🟡 | Generic task board covers request intake; quote flow not implemented |
| Repair tracking + depreciation | 🔴 | Not implemented |
| Tax reporting | 🔴 | Not implemented |
| Contractor delegation | 🟡 | RBAC supports role-based access; time-bound delegation not implemented |
| Compliance audit trails | 🟡 | Generic AuditRecord exists; not scoped per jurisdictional requirement |

## Spec Section 7 — Input Modalities

| Modality | Bridge Status | Notes |
|---|---|---|
| Forms | ✅ | SunfishForm + Inputs cover standard data entry |
| Spreadsheet import/export | 🟡 | SunfishDataSheet renders spreadsheet UX; no CSV/XLSX import pipeline |
| Voice transcription | 🔴 | Not implemented |
| Sensor data ingestion | 🔴 | Not implemented |
| Drone/robot imagery | 🔴 | Not implemented |
| Satellite imagery | 🔴 | Not implemented |

## Spec Section 8 — Asset Evolution & Versioning

| Capability | Bridge Status | Notes |
|---|---|---|
| Hierarchy mutations (split/merge/re-parent) | 🔴 | Entity parent-child is static |
| Temporal as-of queries | 🔴 | No point-in-time view support |
| Metadata resolution improvements | 🔴 | No schema evolution story |

## Spec Section 9 — BIM Integration

| Capability | Bridge Status | Notes |
|---|---|---|
| IFC/Revit import | ⚪ | Not in scope for property management MVP; may apply in later verticals |

## Spec Section 10 — Multi-Jurisdictional & Multi-Tenant

| Capability | Bridge Status | Notes |
|---|---|---|
| Multi-tenant isolation | 🟡 | ITenantContext in Foundation; DemoTenantContext is single-tenant |
| Time-bound access (Macaroons) | 🔴 | Not implemented |
| Federation patterns | 🔴 | Not implemented |
| Jurisdictional routing | 🔴 | Not implemented |

## Next Steps (by target migration phase or future)

- **Post-Phase 9 (immediate):** None — Bridge is a functional demo as shipped.
- **Platform Phase A (asset modeling — new migration phase):** Expand kernel primitives for temporal entities + asset hierarchies; swap Bridge's generic TaskItem entity for a Property/Unit/Fixture hierarchy.
- **Platform Phase B (decentralization — new migration phase):** Introduce crypto primitives in Foundation; adopt Keyhive-inspired group-membership capability model (see `docs/specifications/research-notes/automerge-evaluation.md` for the research and the Keyhive-vs-Macaroons reconciliation); rewire DemoTenantContext to use real Ed25519 signed claims. Adopt Automerge's Merkle-DAG change-log semantics and sync-protocol shape without integrating the Automerge library directly (no .NET binding exists as of April 2026; integration via sidecar is an option for a later phase). Initial implementation is a .NET-native version store + crypto + sync inspired by Automerge + Keyhive.
- **Platform Phase C (input modalities — new migration phase):** Build the ingestion pipeline per spec Section 7; wire voice/sensor/drone ingestion into Bridge as optional inputs.
- **Platform Phase D (federation — new migration phase):** Define federation protocol; implement peer-to-peer sync; demonstrate a cross-jurisdictional scenario (landlord + code-enforcement agency share inspection data).

These phases are future work beyond the current migration scope (Phases 1–9). They become concrete plan documents when prioritized.
```

- [ ] **Step 2: Commit**

```bash
git add accelerators/bridge/PLATFORM_ALIGNMENT.md
git commit -m "docs(bridge): add PLATFORM_ALIGNMENT.md — current state vs spec kernel primitives"
```

---

## Phase 9 Summary — What This Produces

| Deliverable | Location |
|---|---|
| Full Bridge full-stack application | `accelerators/bridge/` |
| 14 screens (dashboard, board, tasks, timeline, risk, budget, team, 7 account pages) | `Sunfish.Bridge.Client/Pages/` |
| EF Core data layer with PostgreSQL + demo seed data | `Sunfish.Bridge.Data/` |
| Rebaselined EF model snapshot (post-rename) | `Sunfish.Bridge.Data/Migrations/*_PostRenameSnapshot.cs` |
| Aspire orchestration (Postgres, Redis, RabbitMQ, DAB, MockOkta) | `Sunfish.Bridge.AppHost/` |
| Real-time updates via SignalR | `Sunfish.Bridge/Hubs/BridgeHub.cs` |
| Canonical notification pipeline | `packages/foundation/Notifications/` |
| Multi-tenant seam | `packages/foundation/Authorization/` |
| Annotated demo-only auth seams | `DemoTenantContext.cs`, `MockOktaService/` |
| Startup warning logs for demo auth | `DemoAuthWarningFilter.cs`, `DemoTenantContext` ctor, AppHost banner |
| Pending roadmap preserved | `accelerators/bridge/ROADMAP.md` |
| Per-accelerator + root README | `accelerators/bridge/README.md`, `README.md` |
| Repeatable rename script | `scripts/migrate-bridge-accelerator.sh` |
| Platform-spec alignment inventory | `accelerators/bridge/PLATFORM_ALIGNMENT.md` (NEW — Task 9-10) |

---

## Self-Review Checklist

- [ ] `accelerators/bridge/Sunfish.Bridge.slnx` exists; root `Sunfish.slnx` unchanged
- [ ] All 10 project directories renamed (`Sunfish.Bridge*`, `MockOktaService`)
- [ ] All 10 `.csproj` files renamed to match directory names
- [ ] `scripts/migrate-bridge-accelerator.sh` runs clean (contamination gate passes)
- [ ] No `Marilo` or `PmDemo` identifiers remain in code, razor, csproj, or JSON
      (`grep -rE '\bMarilo|PmDemo' accelerators/bridge/ --include='*.cs' --include='*.razor' --include='*.razor.cs' --include='*.csproj' --include='*.json' --include='*.slnx'` returns nothing)
- [ ] `dab-config.json` uses `@env('ConnectionStrings__sunfishbridgedb')`
- [ ] `AppHost/Program.cs` resource names: `sunfishbridgedb-server`, `sunfishbridgedb`, `bridge-redis`, `bridge-rabbit`, `bridge-dab`, `bridge-migrations`, `bridge-web`, `mock-okta`
- [ ] `Sunfish.Bridge/Program.cs` maps `BridgeHub` at `/hubs/bridge` (not `/hubs/pmdemo`)
- [ ] UserSecretsIds: `sunfish-bridge-apphost`, `sunfish-bridge-migrations`
- [ ] Aspire SDK version: `13.2.1`
- [ ] `Sunfish.Bridge.Client.csproj` uses `<ProjectReference>` (not `<PackageReference>`) to Sunfish packages (D-PROJECT-REFERENCES)
- [ ] Relative path `..\..\..\packages\...` resolves from `accelerators/bridge/Sunfish.Bridge.Client/`
- [ ] `Sunfish.Foundation.Notifications` has `UserNotification`, 4 enums, `IUserNotificationService`, `IUserNotificationToastForwarder` (D7)
- [ ] `Sunfish.Foundation.Authorization` has `ITenantContext` (D7)
- [ ] `InMemoryUserNotificationService` and `SunfishToastUserNotificationForwarder` stay in Bridge (impls, not contracts)
- [ ] `SunfishBridgeDbContext.cs` in place; `PmDemoDbContext.cs` absent
- [ ] `BridgeHub.cs` / `IBridgeHubClient.cs` in place; `PmDemoHub.cs` / `IPmDemoHubClient.cs` absent
- [ ] `BridgeSeeder.cs` in place; `PmDemoSeeder.cs` absent
- [ ] EF `PostRenameSnapshot` migration exists, `Up`/`Down` are empty (D-EF-MIGRATION-CAPTURE)
- [ ] `SunfishBridgeDbContextModelSnapshot.cs` replaces `PmDemoDbContextModelSnapshot.cs`
- [ ] `DemoTenantContext` constructor emits `LogLevel.Warning` once per process (D-AUTH-SEAM-LOGGING)
- [ ] `DemoAuthWarningFilter` registered when `IsDevelopment()`; logs on app start
- [ ] `AppHost/Program.cs` prints demo-auth banner before `Build().Run()`
- [ ] `ROADMAP.md` has migration header; no Marilo/PmDemo residue (except attributional line)
- [ ] `accelerators/bridge/README.md` created; root `README.md` updated with Bridge callout
- [ ] `dotnet build accelerators/bridge/Sunfish.Bridge.slnx` = 0 warnings, 0 errors
- [ ] `dotnet test accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/Sunfish.Bridge.Tests.Unit.csproj` = all green
- [ ] `dotnet build packages/foundation/Sunfish.Foundation.csproj` = 0 warnings, 0 errors
- [ ] `dotnet test packages/foundation/tests/tests.csproj` = all green (baseline 3 + new foundation notification tests)
- [ ] `accelerators/bridge/PLATFORM_ALIGNMENT.md` exists and accurately captures current Bridge adoption of spec kernel primitives (Task 9-10)
- [ ] Top-of-plan Platform Context section links to `docs/specifications/sunfish-platform-specification.md`

---

## Known risks and mitigations

| Risk | Mitigation |
|---|---|
| Phase 3b/3c hasn't shipped all provider packages when Phase 9 starts | Task 9-3 falls back to referencing whatever provider csprojs exist; add more `<ProjectReference>` lines as each provider lands |
| Central package pinning (`Directory.Packages.props`) missing an Aspire dependency | Task 9-4 Step 5 explicitly verifies; add pinned versions before running builds |
| `dotnet ef migrations add PostRenameSnapshot` fails because startup project lacks DI wiring | Use the `--startup-project Sunfish.Bridge.MigrationService` override; MigrationService has the minimal DI graph EF needs |
| Aspire dashboard port conflicts on CI | Task 9-7 Step 6 and 9-8 Step 5 are optional local validations; the plan does not gate on Aspire runtime success |
| `MockOktaService.csproj` still references `Marilo.PmDemo.ServiceDefaults.csproj` after rename | The sed pass handles this; verify with `grep -r 'Marilo' accelerators/bridge/MockOktaService/` after Task 9-2 |
| Wolverine schema `wolverine` collision across accelerators | Intentional — it's a Postgres schema, not a brand identifier; keep as-is |
| Integration tests reference internal Bridge types with the old name | The sed pass covers `.cs` files in `tests/`; verify by building the test projects in Task 9-6 |
