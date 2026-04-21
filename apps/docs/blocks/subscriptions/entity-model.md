---
uid: block-subscriptions-entity-model
title: Subscriptions — Entity Model
description: EF Core entity configurations contributed by blocks-subscriptions via ADR-0015 module registration.
keywords:
  - subscriptions
  - entity-model
  - ef-core
  - adr-0015
  - multi-tenancy
---

# Subscriptions — Entity Model

## Overview

`blocks-subscriptions` contributes its EF Core entity configurations to the shared Bridge `DbContext` via the ADR-0015 module-entity registration pattern. It does **not** declare its own `DbContext`; instead, `SubscriptionsEntityModule` implements `ISunfishEntityModule` and the Bridge container picks up its configurations alongside every other block that does the same.

## Entity module

```csharp
public sealed class SubscriptionsEntityModule : ISunfishEntityModule
{
    public string ModuleKey => "sunfish.blocks.subscriptions";

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubscriptionsEntityModule).Assembly);
    }
}
```

The `ModuleKey` is a stable identifier that Bridge uses for logging and migration tracking. `ApplyConfigurationsFromAssembly` picks up every `IEntityTypeConfiguration<T>` defined in the block's assembly.

## Configured entities

The block ships five `IEntityTypeConfiguration<T>` implementations in the `Data/` folder:

| File | Entity |
|---|---|
| `PlanEntityConfiguration.cs` | `Plan` (catalog) |
| `AddOnEntityConfiguration.cs` | `AddOn` (catalog) |
| `SubscriptionEntityConfiguration.cs` | `Subscription` (tenant-scoped) |
| `UsageMeterEntityConfiguration.cs` | `UsageMeter` (tenant-scoped) |
| `MeteredUsageEntityConfiguration.cs` | `MeteredUsage` (tenant-scoped) |

## Entities and their fields

### Plan (catalog)

| Field | Type | Notes |
|---|---|---|
| `Id` | `PlanId` | Strong-typed id (primary key). |
| `Name` | `string` | Required. |
| `Edition` | `Edition` | Tier enum (`Lite`, `Standard`, `Enterprise`). |
| `MonthlyPrice` | `decimal` | Catalog currency. |
| `Description` | `string` | Defaults to empty. |

### AddOn (catalog)

| Field | Type | Notes |
|---|---|---|
| `Id` | `AddOnId` | Primary key. |
| `Name` | `string` | Required. |
| `MonthlyPrice` | `decimal` | Catalog currency. |
| `Description` | `string` | Defaults to empty. |

### Subscription (tenant-scoped)

| Field | Type | Notes |
|---|---|---|
| `Id` | `SubscriptionId` | Primary key. |
| `TenantId` | `TenantId` | `IMustHaveTenant`; filtered by the ambient tenant. |
| `PlanId` | `PlanId` | Foreign reference to the catalog plan. |
| `Edition` | `Edition` | Edition purchased — copied from the plan at create time. |
| `StartDate` | `DateOnly` | Inclusive. |
| `EndDate` | `DateOnly?` | Optional; `null` = open-ended. |
| `AddOns` | `IReadOnlyList<AddOnId>` | Attached add-on ids. |

### UsageMeter (tenant-scoped)

| Field | Type | Notes |
|---|---|---|
| `Id` | `UsageMeterId` | Primary key. |
| `TenantId` | `TenantId` | `IMustHaveTenant`. |
| `SubscriptionId` | `SubscriptionId` | Owning subscription. |
| `Code` | `string` | Stable meter code. |
| `Unit` | `string` | Unit label. |

### MeteredUsage (tenant-scoped)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key. |
| `TenantId` | `TenantId` | `IMustHaveTenant`. |
| `MeterId` | `UsageMeterId` | Owning meter. |
| `Quantity` | `decimal` | Sample quantity. |
| `RecordedAtUtc` | `DateTime` | Observation timestamp. |

## Relationships

- `Subscription` → `Plan` (many-to-one, catalog reference by id only).
- `Subscription` → `AddOn*` (many-to-many by id list — the collection is stored on the subscription row).
- `UsageMeter` → `Subscription` (many-to-one).
- `MeteredUsage` → `UsageMeter` (many-to-one).

## Multi-tenancy filter

Every tenant-scoped entity implements `IMustHaveTenant`. The shared Bridge `DbContext` applies a global query filter that restricts reads to the ambient tenant, so ordinary LINQ queries on `Subscription`, `UsageMeter`, and `MeteredUsage` are tenant-safe by default.

## Contributing additional configuration

If you need to extend the entity configuration (add indexes, alter column widths, register value converters), the cleanest pattern is to add a second `IEntityTypeConfiguration<T>` implementation *in your own assembly* and register it via your own `ISunfishEntityModule`. The shared `DbContext` applies every registered module, so additive configurations compose without forcing you to fork the block.

A more invasive change (renaming a property, changing a key) belongs inside the block's own `*EntityConfiguration` files and should go through the `sunfish-api-change` pipeline variant because it impacts every downstream consumer.

## Strong-typed ids

All ids (`PlanId`, `AddOnId`, `SubscriptionId`, `UsageMeterId`) are Sunfish strong-typed id structs produced by Foundation's id generator. They carry an underlying `Guid`, expose a `NewId()` static factory, and have custom converters registered by Foundation so they round-trip through JSON and EF Core without per-id boilerplate.

`MeteredUsage.Id` is an exception — it is a plain `Guid` because the record is typically produced in bulk from external observation streams and the `Guid` form is cheaper to synthesise.

## Migrations

The block does not ship migrations. Because the entities contribute to the *shared* Bridge `DbContext`, migrations live with the host app and cover every block's entities in one unified migration stream. This keeps version control simple at the cost of making it the host's job to re-scaffold after a block upgrade that changes entities.

Recommended workflow on a block version bump that changes entities:

```bash
dotnet ef migrations add UpgradeSubscriptionsBlock --context BridgeDbContext
dotnet ef database update --context BridgeDbContext
```

The migration name convention is "UpgradeXxxBlock" (or a more specific name if the change is narrower).

## Test coverage

`tests/SubscriptionsEntityModuleTests.cs` asserts:

- `ModuleKey` returns the canonical `sunfish.blocks.subscriptions`.
- `Configure(ModelBuilder)` runs without throwing on a fresh model builder.
- Every expected entity type has a key configured after `Configure(...)` is called.

These are useful regression tests if you are extending the block's entity surface — copy the fixture into your downstream test project and add assertions for your custom entity configurations.

## Related

- [Overview](overview.md)
- [DI Wiring](di-wiring.md)
- ADR 0015 — `docs/adrs/0015-module-entity-registration.md`
- ADR 0008 — `docs/adrs/0008-foundation-multitenancy.md`
