---
uid: block-subscriptions-overview
title: Subscriptions — Overview
description: Introduction to the blocks-subscriptions package — plans, editions, subscriptions, add-ons, and usage meters.
keywords:
  - subscriptions
  - plans
  - editions
  - add-ons
  - usage-metering
  - saas
  - multi-tenant
---

# Subscriptions — Overview

## Overview

The `blocks-subscriptions` package provides a self-contained building block for modelling a multi-tier subscription catalog (plans × editions), attaching tenants to those plans with optional add-ons, and recording metered usage against per-subscription meters. It ships with a framework-agnostic service contract (`ISubscriptionService`), an in-memory implementation, an ADR-0015 entity module for persistence contribution, and a ready-to-use Blazor list block.

## Package path

`packages/blocks-subscriptions` — assembly `Sunfish.Blocks.Subscriptions`.

## When to use it

- You need an opinionated subscription model with catalog plans, multiple pricing tiers (editions), add-ons, and usage meters — without writing any of it yourself.
- You want tenant-scoped subscription records that participate in Sunfish's `IMustHaveTenant` multi-tenancy model out of the box.
- You want your block's EF Core entity configurations automatically applied to the shared Bridge `DbContext` via the ADR-0015 module-entity registration pattern.

## Key entities

- **`Plan`** — catalog-level subscription tier (`Id`, `Name`, `Edition`, `MonthlyPrice`, `Description`). Not tenant-scoped.
- **`Edition`** — pricing/feature tier enum: `Lite`, `Standard`, `Enterprise`.
- **`AddOn`** — catalog-level add-on product (`Id`, `Name`, `MonthlyPrice`, `Description`). Not tenant-scoped.
- **`Subscription`** — tenant-scoped record linking a tenant to a plan, with a start/end date, edition, and a list of attached add-ons.
- **`UsageMeter`** — tenant-scoped meter attached to a subscription (`Code`, `Unit`) — e.g. `"api-calls"` / `"calls"`.
- **`MeteredUsage`** — tenant-scoped usage sample recorded against a meter.

## Key services

- **`ISubscriptionService`** — core contract covering catalog listing, subscription CRUD, add-on attachment, and usage recording.
- **`InMemorySubscriptionService`** — thread-safe in-memory implementation suitable for tests and demos.
- **`SubscriptionsEntityModule`** — `ISunfishEntityModule` implementation that contributes the block's EF Core configurations to the shared Bridge `DbContext` per ADR 0015.

## Key UI components

- **`SubscriptionListBlock`** — Blazor block backed by `SubscriptionListState` that lists the current tenant's subscriptions.

## DI wiring

```csharp
services.AddInMemorySubscriptions();
```

Registers `ISubscriptionService` (singleton → `InMemorySubscriptionService`) and contributes `SubscriptionsEntityModule` as an `ISunfishEntityModule`. See [DI Wiring](di-wiring.md) for details.

## Multi-tenancy

`Subscription`, `UsageMeter`, and `MeteredUsage` all implement `IMustHaveTenant`. Every record carries a `TenantId` and the service implementations (including persistence-backed ones) are expected to filter by the ambient tenant. `Plan` and `AddOn` are *not* tenant-scoped — they are catalog-level and shared across tenants.

## Status and deferred items

- Proration on mid-period plan changes is not modelled.
- Billing integration is out of scope for this block — `blocks-subscriptions` records the subscription; invoice generation (rent-collection or a separate billing block) is a downstream concern.
- The in-memory service does not persist across process restarts; use the EF Core-backed wiring for real apps.
- Usage aggregation (sum per meter per window) is not in the service surface yet; callers aggregate their own samples.

## Where things live in the package

| Path (under `packages/blocks-subscriptions/`) | Purpose |
|---|---|
| `Models/Plan.cs` | Catalog plan record. |
| `Models/Edition.cs` | `Lite` / `Standard` / `Enterprise` enum. |
| `Models/AddOn.cs` | Catalog add-on record. |
| `Models/Subscription.cs` | Tenant-scoped subscription. |
| `Models/UsageMeter.cs`, `MeteredUsage.cs` | Tenant-scoped meters and samples. |
| `Services/ISubscriptionService.cs` | Framework-agnostic contract. |
| `Services/InMemorySubscriptionService.cs` | Thread-safe in-memory implementation. |
| `Data/*EntityConfiguration.cs` | Per-entity `IEntityTypeConfiguration<T>` classes. |
| `Data/SubscriptionsEntityModule.cs` | ADR-0015 module; applies configurations to the shared `DbContext`. |
| `DependencyInjection/SubscriptionsServiceCollectionExtensions.cs` | `AddInMemorySubscriptions` extension. |
| `State/SubscriptionListState.cs` | Backing state object for the list block. |
| `SubscriptionListBlock.razor` | List view UI. |
| `tests/InMemorySubscriptionServiceTests.cs` | Service behaviour. |
| `tests/SubscriptionsEntityModuleTests.cs` | Entity module contract. |

## Example: wire + browse + subscribe + record usage

```csharp
// Host wiring
builder.Services
    .AddSunfishFoundation()
    .AddSunfishBridge()
    .AddInMemorySubscriptions();

// Later, in a request handler
var svc = sp.GetRequiredService<ISubscriptionService>();

// Browse catalog (not tenant-scoped)
var plans = new List<Plan>();
await foreach (var plan in svc.ListPlansAsync(ct))
    plans.Add(plan);

// Create a subscription for the current tenant
var sub = await svc.CreateSubscriptionAsync(new CreateSubscriptionRequest
{
    PlanId    = plans[0].Id,
    Edition   = Edition.Standard,
    StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
});

// Record metered usage (meters are seeded or pre-created elsewhere)
await svc.RecordUsageAsync(meter.Id, quantity: 42m, ct);
```

## ADRs in effect

- **ADR 0015 — Module entity registration pattern.** `blocks-subscriptions` is one of the canonical implementers: it ships `SubscriptionsEntityModule : ISunfishEntityModule` so its EF Core entity configurations are applied to the shared Bridge `DbContext` rather than a per-block context.
- **ADR 0008 — Foundation multi-tenancy.** Tenant-scoped entities implement `IMustHaveTenant` and are filtered by the ambient tenant via the Bridge global query filter.
- **ADR 0022 — Example catalog + docs taxonomy.** Governs the structure of this docs page set.

## Interaction model

A typical SaaS host uses the block in three places:

1. **Catalog page** — call `ListPlansAsync` / `ListAddOnsAsync` (catalog scope) to render pricing tiers and attach options.
2. **Subscription page for the current tenant** — call `ListSubscriptionsAsync(query)` (tenant-scoped) to render what they have and render the list block (`SubscriptionListBlock`) inside the layout.
3. **Metering touchpoint** — call `RecordUsageAsync` from whichever handler observes the metered event (API middleware, scheduled rollup, stripe-webhook-style ingester). The block does not care about the call site; any code path with the meter id can emit samples.

## Comparison with other billing blocks

- **vs. `blocks-rent-collection`** — rent-collection is about periodic invoices against a schedule; subscriptions is about tenant binding and metered usage. They co-exist happily if your app bills some tenants by subscription and others by rent schedule.
- **vs. full billing platform (Stripe Billing, Recurly)** — these platforms handle dunning, invoicing, tax, and payment processing. `blocks-subscriptions` is the *record keeper* for plan/tier/metering; the billing platform consumes the records.

## Related

- [Plans and Editions](plans-and-editions.md)
- [Usage Meters](usage-meters.md)
- [Entity Model](entity-model.md)
- [DI Wiring](di-wiring.md)
