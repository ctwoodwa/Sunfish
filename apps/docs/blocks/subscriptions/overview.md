---
uid: block-subscriptions-overview
title: Subscriptions — Overview
description: Introduction to the blocks-subscriptions package — plans, editions, subscriptions, add-ons, and usage meters.
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

## Related

- [Plans and Editions](plans-and-editions.md)
- [Usage Meters](usage-meters.md)
- [Entity Model](entity-model.md)
- [DI Wiring](di-wiring.md)
