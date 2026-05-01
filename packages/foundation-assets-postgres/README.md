# Sunfish.Foundation.Assets.Postgres

Postgres-backed implementation of the Sunfish asset-modeling kernel primitives — `IEntityStore`, `IVersionStore`, `IAuditLog`, `IHierarchyService` — via EF Core + Npgsql.

## What this ships

Production-grade Postgres storage for the foundation asset-modeling contracts (defined in `Sunfish.Foundation.Assets.Common`). Pairs with `Sunfish.Foundation.Persistence`'s `ISunfishEntityModule` pattern.

### Storage seams

- **`PostgresEntityStore`** — `IEntityStore<T>` impl over EF Core; relies on the host's `DbContext` aggregating contributions from all `ISunfishEntityModule` participants.
- **`PostgresVersionStore`** — `IVersionStore<T>` impl with optimistic-concurrency version tracking.
- **`PostgresAuditLog`** — `IAuditLog` impl writing append-only audit records to a Postgres table.
- **`PostgresHierarchyService`** — `IHierarchyService` impl using ltree-style materialized paths (Postgres-specific) for fast hierarchy queries.

### Migrations

- EF Core migration set scoped to the asset-modeling tables.
- Pairs with the host's migration orchestration (Bridge ships migrations through `Sunfish.Bridge.MigrationService`).

## DI

```csharp
services.AddSunfishAssetsPostgres(connectionString);
```

## Boundary

Production hosts (Bridge SaaS posture, hosted-node) wire this implementation. Tests + non-production hosts stay on the in-memory contracts from `Sunfish.Foundation.Assets.Common`.

## See also

- `Sunfish.Foundation.Assets.Common` — contracts (kernel-tier)
- [Sunfish.Foundation.Persistence](../foundation-persistence/README.md) — `ISunfishEntityModule` aggregation pattern
- [Sunfish.Foundation.MultiTenancy](../foundation-multitenancy/README.md) — tenant query filters consumed by every store
