---
uid: foundation-persistence-overview
title: Persistence — Overview
description: The EF Core seam that lets blocks contribute entity configurations into Bridge's shared DbContext.
keywords:
  - persistence
  - EF Core
  - DbContext
  - entity module
  - ADR 0015
  - module registration
---

# Persistence — Overview

## What this package gives you

`Sunfish.Foundation.Persistence` is the EF-Core-adjacent package that carries one contract — [`ISunfishEntityModule`](xref:Sunfish.Foundation.Persistence.ISunfishEntityModule) — plus the conventions that let blocks register their entities into Bridge's shared `DbContext` without inventing a new persistence framework.

It is deliberately a **small** package. The whole thing is one interface:

```csharp
public interface ISunfishEntityModule
{
    string ModuleKey { get; }
    void Configure(ModelBuilder modelBuilder);
}
```

- `ModuleKey` is a stable, reverse-DNS identifier (`sunfish.blocks.subscriptions`) that matches the module keys referenced by bundle manifests (`BusinessCaseBundleManifest.RequiredModules` / `OptionalModules`).
- `Configure(ModelBuilder)` is called once per `DbContext` model build, after the base `DbContext.OnModelCreating` has run. Implementations typically delegate to `ModelBuilder.ApplyConfigurationsFromAssembly` against the block's own assembly so each entity's `IEntityTypeConfiguration<T>` class is discovered without per-entity boilerplate.

The package source lives at `packages/foundation-persistence/`.

## Why it exists

[ADR 0015](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0015-module-entity-registration.md) settled a prerequisite architectural question: **how do `blocks-*` modules integrate with the persistence layer?** Two options were on the table — one `DbContext` per block, or a shared Bridge `DbContext` with blocks contributing their entity configurations into it. The ADR chose the shared-context approach based on four concrete constraints:

1. **Lite-mode runs on one database.** Multiple `DbContext`s pointing at their own databases is infeasible.
2. **Data API Builder (DAB)** — the public GraphQL surface — reads directly against Bridge's tables. One config, one database.
3. **No existing block ships EF persistence.** Whatever this ADR decides becomes the pattern every future block uses.
4. **Tenant query filters should land once.** [`IMustHaveTenant`](xref:Sunfish.Foundation.MultiTenancy.IMustHaveTenant) (from `Sunfish.Foundation.MultiTenancy`) can be enforced uniformly across every registered entity only if the entities all live in one model-build pass.

The `foundation-persistence` package bridges Foundation and EF Core without contaminating core Foundation. It mirrors the `foundation-assets-postgres` pattern — a targeted persistence-adapter package that brings in EF Core so blocks can import EF-adjacent abstractions without pulling EF into Foundation itself.

## Why this package, not `Sunfish.Foundation`

`Sunfish.Foundation` is EF-Core-free on purpose. Blocks that will never need EF (capability code, pure domain logic, reporting pipelines) must be able to take a dependency on core Foundation without inheriting `Microsoft.EntityFrameworkCore.*` as a transitive. Splitting the `ISunfishEntityModule` contract into `Sunfish.Foundation.Persistence` lets EF-persisting blocks import one extra package; EF-free modules import nothing.

## Connection lifecycle, migrations, one DbContext

The shared `SunfishBridgeDbContext` lives in Bridge, not in this package. This package only defines the contract modules implement. At composition time, Bridge:

1. Receives every registered `ISunfishEntityModule` as an `IEnumerable<ISunfishEntityModule>` through DI.
2. Calls `module.Configure(modelBuilder)` for each module in `OnModelCreating`, after the base `DbContext.OnModelCreating` runs.
3. Walks the resulting model and applies tenant query filters to every entity that implements `IMustHaveTenant`.

Because there is one `DbContext`, there is one migration history. Bridge owns a single `Migrations/` folder. Upgrade reasoning is centralized. Cross-block writes participate in the same `DbContext.SaveChangesAsync()` call — no saga, no two-phase commit.

## Scope

The ADR's decision applies to every `blocks-*` package that owns EF-persisted entities. It **does not** apply to:

- **Local-first offline stores** — `packages/foundation-localfirst` manages its own storage on a different backend and lifecycle.
- **Blob storage** — `packages/kernel-blob-store` and its federation replication live on a separate contract.
- **Ingestion pipelines** — they write into the shell `DbContext` through kernel entity APIs, not directly through EF.

A block that does not persist through EF Core simply skips this package.

## Related

- [Entity-Module Pattern](entity-module-pattern.md)
- [Multitenancy — Tenant-Scoped Markers](../multitenancy/tenant-scoped-markers.md)
- [ADR 0015 — Module-Entity Registration Pattern](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0015-module-entity-registration.md)
</content>
</invoke>