---
uid: foundation-persistence-entity-module-pattern
title: Persistence — Entity-Module Pattern
description: The ADR-0015 pattern — each block registers entity configurations via ISunfishEntityModule, one shared DbContext, no per-block DbContext.
---

# Persistence — Entity-Module Pattern

## The pattern in one picture

Three small pieces, all idiomatic EF Core:

1. A block declares entities using standard `IEntityTypeConfiguration<TEntity>` classes.
2. The block exposes those configurations through a single `ISunfishEntityModule` descriptor.
3. Bridge's shared `SunfishBridgeDbContext` composes every registered module at model-build time and adds tenant query filters uniformly.

## Step 1 — entity configuration

Every entity in a block gets a standard EF Core configuration class. No framework types beyond `IEntityTypeConfiguration<T>` are needed.

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

        // Do NOT add a tenant query filter here — Bridge applies it centrally
        // for every entity that implements IMustHaveTenant.
    }
}
```

The entity itself implements the appropriate multitenancy marker:

```csharp
public sealed class Subscription : IMustHaveTenant
{
    public Guid Id { get; init; }
    public TenantId TenantId { get; init; }
    // ...
}
```

## Step 2 — the module descriptor

Each block ships one `ISunfishEntityModule` per module. The typical body is one line — delegate to `ApplyConfigurationsFromAssembly` against the block's own assembly.

```csharp
// packages/blocks-subscriptions/Data/SubscriptionsEntityModule.cs
public sealed class SubscriptionsEntityModule : ISunfishEntityModule
{
    public string ModuleKey => "sunfish.blocks.subscriptions";

    public void Configure(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubscriptionsEntityModule).Assembly);
}
```

`ModuleKey` is reverse-DNS style and matches the module keys a bundle manifest references in `RequiredModules` / `OptionalModules` (see [Catalog — Bundle Manifests](../catalog/bundle-manifests.md)). Keeping the same identifier on both ends — bundle composition and entity registration — means a host can answer "which modules are active?" from either surface and get consistent answers.

## Step 3 — Bridge composes

The shared `DbContext` in Bridge accepts every registered module and composes them in `OnModelCreating`.

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

Bridge's composition root calls `services.AddSubscriptions()` per the bundle manifest's `RequiredModules` / `OptionalModules` list.

## What this pattern buys

- **Single transaction boundary.** Cross-block writes participate in one `SaveChangesAsync` call. Demo, lite-mode, and self-hosted deployments all work unchanged.
- **Single DAB config.** `dab-config.json` continues to read one database. External GraphQL consumers see a stable schema surface.
- **Single migration history.** Bridge owns one `Migrations/` folder.
- **Idiomatic EF Core.** `IEntityTypeConfiguration<T>` is the standard pattern — nothing invented.
- **Auditable composition.** Every registered module is a named DI singleton. A `/diagnostics/modules` endpoint can enumerate them.
- **Clean module removal.** Drop a block's `Add<Block>()` call and its entity configurations stop being applied on model build.

## Caveats

- **Bridge's DbContext becomes a platform composition point.** That is the deliberate trade the ADR accepts in exchange for demo and lite-mode simplicity.
- **Migration removal is not automatic.** Dropping a block stops registering entities but does not delete tables. Operators who remove a bundle run a block-owned cleanup migration or accept orphan tables. The `dotnet sunfish bundle uninstall` subcommand in the scaffolding CLI emits the cleanup SQL based on the block's last known entity configuration.
- **Table-name collisions are possible.** Two blocks could both declare `ToTable("notes")`. The mitigation — agreed in ADR 0015 and captured in `_shared/engineering/package-conventions.md` — is a module-prefixed table name (`subscriptions_plans`, `tenant_admin_users`, `pm_projects`). A Debug-build collision check runs at model-build time.
- **Tests must supply modules.** `DbContextFactory` tests also provide the `IEnumerable<ISunfishEntityModule>` — a `TestSunfishEntityModule` helper in `packages/foundation.tests/` avoids per-test boilerplate.

## Related

- [Overview](overview.md)
- [Multitenancy — Tenant-Scoped Markers](../multitenancy/tenant-scoped-markers.md)
- [Catalog — Bundle Manifests](../catalog/bundle-manifests.md)
- [ADR 0015 — Module-Entity Registration Pattern](xref:adr-0015-module-entity-registration)
