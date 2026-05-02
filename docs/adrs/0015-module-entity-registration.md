---
id: 15
title: Module-Entity Registration Pattern (Shared Bridge DbContext)
status: Accepted
date: 2026-04-20
tier: foundation
concern:
  - persistence
composes:
  - 6
  - 7
  - 8
  - 9
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0015 — Module-Entity Registration Pattern (Shared Bridge DbContext)

**Status:** Accepted
**Date:** 2026-04-20
**Resolves:** The open architectural question flagged by [bridge-data-audit.md](../../_shared/engineering/bridge-data-audit.md) §Recommendation 1 — how do domain `blocks-*` modules integrate with the persistence layer? Either each block owns a `DbContext` of its own, or blocks register their entity configurations into Bridge's single `DbContext`. Every P1 block (`blocks-subscriptions`, `blocks-tenant-admin`, `blocks-businesscases`) and every P2 entity move (`blocks-tasks`, new `blocks-projects`, etc.) is blocked on this decision.

---

## Context

[ADR 0006](0006-bridge-is-saas-shell.md) established that Bridge is a generic SaaS shell — it hosts bundles, but does not own domain models. The follow-up [audit](../../_shared/engineering/bridge-data-audit.md) found that 8 of 9 entities currently in `Sunfish.Bridge.Data` are vertical project-management leakage that must move into `blocks-*` modules. Before any move can happen, the repo needs a decided answer to a prerequisite question: **how does a block register its entities with EF Core?**

Two architectural options frame the decision:

**Option 1 — DbContext-per-block.** Each block ships its own `DbContext<TBlock>` and manages its own migrations. Cross-block work spans multiple `DbContext` instances; distributed transactions use a saga or 2PC. Each block is database-independent by design — a block could theoretically target a different backing store.

**Option 2 — Shared Bridge DbContext.** Bridge hosts a single `SunfishBridgeDbContext`. Blocks expose their entity configurations via EF Core's native `IEntityTypeConfiguration<T>` contract plus a lightweight module-descriptor seam. Bridge composes the configurations at model-build time. One database, one schema, one migration history.

The existing code, the deployment modes we support, and the external tooling we publish all point the same way.

### Constraints pressing on the decision

1. **Lite-mode runs on one database.** Lite-mode (per roadmap) targets single-tenant SQLite or local Postgres installations. Multiple `DbContext`s each pointing at their own database is infeasible for lite-mode. Even pointing multiple `DbContext`s at the same database introduces transaction-boundary awkwardness EF Core does not natively solve.
2. **Data API Builder (DAB) is the public GraphQL surface.** `accelerators/bridge/dab-config.json` reads directly against Bridge's tables. Per-block database targets would require either multiple DAB configs composed at deploy time, or a single config that reaches into multiple databases — neither is supported by DAB today without third-party glue.
3. **No existing block owns a DbContext.** The audit confirmed 11 blocks-* packages — none ships EF persistence. The pattern doesn't exist yet. Whatever ADR 0015 decides becomes the reference for every future block.
4. **Bundle-provisioning service (the P1 milestone enabling per-block seeder orchestration) does not yet exist.** The BridgeSeeder today seeds all domain data in one `DbContext` pass. Option 1 would require standing up distributed seeder choreography before the blocks can move. Option 2 lets seeders compose in the same `DbContext` scope with no new infrastructure.
5. **`IMustHaveTenant` (ADR 0008) is the canonical tenant-scoping marker.** Whichever option wins, the ad-hoc `HasQueryFilter(e => e.TenantId == _currentTenantId)` scattered in `SunfishBridgeDbContext.OnModelCreating` becomes a reusable Foundation.MultiTenancy extension that applies uniformly to every registered entity. Option 2 makes this a one-line invocation in Bridge's composition root; Option 1 requires each block to re-implement the pattern.

### What the audit already recommended

bridge-data-audit.md §38–45 flagged Option 2 as preferred: *"Option 2 for Bridge (single-DbContext simplifies transactions and tooling like DAB). ADR follow-up needed to formalize the module-entity-registration pattern."* This ADR is that follow-up.

---

## Decision

**Blocks register their EF Core entity configurations into Bridge's single `SunfishBridgeDbContext`** via the native `IEntityTypeConfiguration<TEntity>` interface plus a thin **`ISunfishEntityModule`** discovery seam. Bridge composes the registrations at model-build time; blocks do not ship their own `DbContext` or migrations.

### Scope

Applies to every `blocks-*` package that owns EF-persisted entities in Sunfish today or under the roadmap through M5 of the Bridge.Data move plan. Explicitly includes the three P1 blocks (`blocks-subscriptions`, `blocks-tenant-admin`, `blocks-businesscases`) and the P2 moves (`blocks-tasks` domain shape, new `blocks-projects`, `blocks-communications`).

Does **not** apply to:

- Local-first offline stores — `packages/foundation-localfirst` manages its own storage per ADR 0012 (different persistence backend and lifecycle).
- Blob storage — `packages/kernel-blob-store` and federation replication live on a different contract.
- Ingestion pipelines — ingestion packages write into the shell DbContext through kernel entity APIs, not directly through EF.

### The pattern

Three small pieces, all idiomatic EF Core:

**1. A block declares its entities with standard `IEntityTypeConfiguration<TEntity>` classes.**

```csharp
// packages/blocks-subscriptions/Data/SubscriptionEntityConfiguration.cs
namespace Sunfish.Blocks.Subscriptions.Data;

internal sealed class SubscriptionEntityConfiguration
    : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TenantId).IsRequired();
        // Tenant query-filter is applied centrally by Bridge.
    }
}
```

**2. A block exposes its entity configurations via a single `ISunfishEntityModule` descriptor.**

The contract lives in a new small package, `packages/foundation-persistence/`, which bridges Foundation + EF Core without contaminating core Foundation. The pattern mirrors `packages/foundation-assets-postgres/` — a targeted persistence-adapter package that references Foundation and EF Core, so blocks can import EF-adjacent abstractions without pulling EF into Foundation itself.

```csharp
// packages/foundation-persistence/ISunfishEntityModule.cs (NEW contract, lands with this ADR)
namespace Sunfish.Foundation.Persistence;

public interface ISunfishEntityModule
{
    /// <summary>Stable module key, reverse-DNS style (matches bundle-manifest module keys).</summary>
    string ModuleKey { get; }

    /// <summary>Applies the module's EF Core entity configurations to the shared model builder.</summary>
    void Configure(ModelBuilder modelBuilder);
}
```

```csharp
// packages/blocks-subscriptions/Data/SubscriptionsEntityModule.cs
public sealed class SubscriptionsEntityModule : ISunfishEntityModule
{
    public string ModuleKey => "sunfish.blocks.subscriptions";

    public void Configure(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubscriptionsEntityModule).Assembly);
}
```

**3. Bridge's DbContext composes every registered module at model-build time.**

```csharp
// accelerators/bridge/Sunfish.Bridge.Data/SunfishBridgeDbContext.cs
public class SunfishBridgeDbContext : DbContext
{
    private readonly IEnumerable<ISunfishEntityModule> _modules;
    private readonly string _currentTenantId;

    public SunfishBridgeDbContext(
        DbContextOptions<SunfishBridgeDbContext> options,
        IEnumerable<ISunfishEntityModule> modules,
        ITenantContext tenant) : base(options)
    {
        _modules = modules;
        _currentTenantId = tenant.TenantId;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var module in _modules)
        {
            module.Configure(modelBuilder);
        }

        // Apply tenant query filters uniformly across every IMustHaveTenant entity
        // registered by any module. EF treats _currentTenantId as a per-instance
        // parameter, so each DbContext scope sees its own tenant's rows.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
            {
                // Bridge applies a typed filter per entity (small reflection helper).
            }
        }
    }
}
```

Blocks register their module in their DI extension method:

```csharp
// packages/blocks-subscriptions/DependencyInjection/SubscriptionsServiceCollectionExtensions.cs
public static IServiceCollection AddSubscriptions(this IServiceCollection services)
{
    services.AddSingleton<ISunfishEntityModule, SubscriptionsEntityModule>();
    services.AddScoped<ISubscriptionService, InMemorySubscriptionService>();
    return services;
}
```

Bridge's composition root calls `services.AddSubscriptions()` per the bundle manifest's `RequiredModules`/`OptionalModules` list.

### What this pattern buys

- **Single transaction boundary.** Cross-block writes participate in the same `DbContext.SaveChangesAsync()` call. Demo, lite-mode, and self-hosted deployments all work unchanged.
- **Single DAB config.** `dab-config.json` continues to read one database. External GraphQL consumers keep a stable schema surface.
- **Single migration history.** Bridge owns one `Migrations/` folder. Upgrade reasoning is centralized.
- **Idiomatic EF Core.** `IEntityTypeConfiguration<T>` is the standard pattern; nothing invented, nothing framework-specific.
- **Auditable composition.** Every registered module is a named DI singleton. A `/diagnostics/modules` endpoint can enumerate them.
- **Clean module removal.** Drop a block's `Add<Block>()` call and its entity configurations stop being applied. (Migration cleanup is a separate operational concern — see Consequences.)

---

## Consequences

### Positive

- P1 blocks ship on a pattern the whole codebase will use.
- Bridge.Data move plan (M1–M5) unblocks. The audit's phased moves can proceed.
- `ApplyTenantQueryFilters(ITenantContext)` as a Foundation.MultiTenancy extension lands once and covers every `IMustHaveTenant`-marked entity registered by every module.
- New blocks follow a 30-line DI registration — no persistence scaffolding boilerplate.

### Negative

- **Bridge's DbContext becomes a composition point for the whole platform.** This is the deliberate trade: centralize persistence in exchange for demo/lite-mode simplicity. The audit accepted this trade when recommending Option 2.
- **Migration removal is not automatic.** Dropping a block's `AddX()` call stops new entity registration but does not delete tables or drop indexes. Operators who remove a bundle must run a block-owned cleanup migration or accept orphan tables. We will ship a **`dotnet sunfish bundle uninstall`** subcommand in `tooling/scaffolding-cli` that emits the cleanup SQL based on the block's last known entity configuration; that lands with blocks-businesscases (P1).
- **Table-name collisions become possible.** Two blocks could both declare `ToTable("notes")`. Mitigated by the audit recommendation that tables use a module prefix (`subscriptions_plans`, `tenant_admin_users`, `pm_projects`) — captured in `_shared/engineering/package-conventions.md` as the table-naming rule. A collision-check diagnostic runs at model-build time in Debug builds.
- **Tests that use `DbContextFactory` must also supply the `IEnumerable<ISunfishEntityModule>`.** A `TestSunfishEntityModule` helper in `packages/foundation.tests/` avoids per-test boilerplate.

### Rejected alternatives

- **Option 1 (DbContext-per-block).** Rejected for the constraints above: lite-mode can't host it, DAB can't read it cleanly, and no existing block has a `DbContext` to port. If an accelerator someday needs true multi-database isolation (e.g. a compliance-driven per-customer-database deployment), a separate ADR can revisit — per-block DbContext is a valid pattern, just not the default.
- **Pure `ApplyConfigurationsFromAssembly` without `ISunfishEntityModule`.** Rejected because Bridge would need to reference every block assembly directly. The module descriptor keeps registration DI-driven — Bridge knows which modules are loaded via the same path that the bundle-provisioning service uses.
- **Code-first module-specific `DbContext` types that share a connection.** Rejected; EF Core treats them as separate models with separate migrations. It's Option 1 dressed as Option 2 and inherits the downsides of both.

---

## Implementation sequencing

1. **Land `packages/foundation-persistence/`** with `ISunfishEntityModule`. Ship with this ADR. Tests: a no-op module composition test asserting a two-module `DbContext` builds its model correctly.
2. **Extend Bridge's `SunfishBridgeDbContext`** to accept `IEnumerable<ISunfishEntityModule>` and call `module.Configure(modelBuilder)` in `OnModelCreating`. Bridge keeps its existing per-entity `HasQueryFilter` pattern for tenant isolation; new block entities use the `IMustHaveTenant` marker, and Bridge's `OnModelCreating` adds tenant filters to every registered `IMustHaveTenant` entity in the same pass.
3. **P1 blocks implement `ISunfishEntityModule`** — `blocks-subscriptions`, `blocks-tenant-admin`, `blocks-businesscases` each ship their module descriptor + entity configurations as they scaffold.
4. **P2 entity moves follow the pattern** — M1 (`blocks-tasks` domain shape), M2 (`blocks-projects`), M3 (`blocks-communications`).
5. **Cleanup migration command** (`dotnet sunfish bundle uninstall <key>`) lands in scaffolding-cli alongside `blocks-businesscases` (P1, since that block owns the bundle-provisioning service).

---

## Related ADRs

- [ADR 0006](0006-bridge-is-saas-shell.md) — Bridge Is a Generic SaaS Shell. Establishes why domain entities must live outside Bridge.
- [ADR 0007](0007-bundle-manifest-schema.md) — Bundle Manifest Schema. Bundle `RequiredModules`/`OptionalModules` reference the same `ModuleKey` that `ISunfishEntityModule` declares.
- [ADR 0008](0008-foundation-multitenancy.md) — Foundation.MultiTenancy. Defines `IMustHaveTenant` and the `ApplyTenantQueryFilters` extension that this ADR leans on.
- [ADR 0009](0009-foundation-featuremanagement.md) — Feature Management. Bundle-provisioning service (P1, `blocks-businesscases`) consumes both `IBundleCatalog` and `IFeatureEvaluator`; this ADR unblocks the persistence layer for that service.
- [ADR 0013](0013-foundation-integrations.md) — Foundation.Integrations. Provider credentials and webhook envelopes are shell entities and stay in Bridge.Data directly; they are not registered via `ISunfishEntityModule`.

## Related documents

- [bridge-data-audit.md](../../_shared/engineering/bridge-data-audit.md) — The audit that surfaced this ADR's question. Recommendation 1 is resolved by this decision.
- [_shared/engineering/package-conventions.md](../../_shared/engineering/package-conventions.md) — Table-naming rule (module-prefixed) is captured here as part of the persistence-layer conventions.
