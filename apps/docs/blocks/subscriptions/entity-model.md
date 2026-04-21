---
uid: block-subscriptions-entity-model
title: Subscriptions — Entity Model
description: EF Core entity configurations contributed by blocks-subscriptions via ADR-0015 module registration.
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

## Related

- [Overview](overview.md)
- [DI Wiring](di-wiring.md)
- ADR 0015 — `docs/adrs/0015-module-entity-registration.md`
