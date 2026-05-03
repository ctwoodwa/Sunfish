# Sunfish.Foundation.Persistence

EF-Core-adjacent persistence abstractions — `ISunfishEntityModule` (module-entity registration pattern per ADR 0015) and shared tenant-query-filter extensions per ADR 0008.

## What this ships

### Module-entity registration (ADR 0015)

- **`ISunfishEntityModule`** — interface every block implements to contribute its EF Core entity configurations + DbSet exposures. Avoids the per-block `DbContext` proliferation antipattern; one host `DbContext` aggregates contributions from all registered modules.
- **`SunfishModelBuilder`** — extension over `ModelBuilder` that hosts call to apply every registered module's contributions in one pass.

### Tenant-query-filter extensions (ADR 0008)

- **`ApplyTenantQueryFilter<TEntity>(...)`** — extension that applies a global query filter on any `IMustHaveTenant` entity. Filters by the active `ITenantContext.TenantId` so cross-tenant reads are statically impossible from any service consuming the DbContext.
- Composable with the host's domain-specific filters (soft-delete, tombstone, etc.) via `EF.Property<TenantId>(e, "TenantId")`-style chains.

## Pattern

```csharp
// In a block (e.g., blocks-properties):
public sealed class PropertiesEntityModule : ISunfishEntityModule
{
    public void ConfigureModel(ModelBuilder builder)
    {
        builder.Entity<Property>(b => {
            b.HasKey(p => p.Id);
            b.OwnsOne(p => p.Address);
            b.ApplyTenantQueryFilter();
        });
    }
}

// In the host's DbContext.OnModelCreating:
modelBuilder.ApplySunfishEntityModules(_serviceProvider);
```

## ADR map

- [ADR 0008](../../docs/adrs/0008-multi-tenancy.md) — global tenant query filter
- [ADR 0015](../../docs/adrs/0015-module-entity-registration.md) — module-entity registration pattern

## See also

- [apps/docs Overview](../../apps/docs/foundation/persistence/overview.md)
- [Sunfish.Foundation.MultiTenancy](../foundation-multitenancy/README.md) — `IMustHaveTenant` marker + `ITenantContext`
