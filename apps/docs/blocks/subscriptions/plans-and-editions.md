---
uid: block-subscriptions-plans-and-editions
title: Subscriptions — Plans and Editions
description: Catalog concepts — plans, editions, add-ons, and the subscription record that binds a tenant to them.
keywords:
  - plans
  - editions
  - addons
  - catalog
  - tenant-subscription
---

# Subscriptions — Plans and Editions

## Overview

`blocks-subscriptions` draws a hard line between *catalog* concepts (what you sell) and *tenant* concepts (what a specific tenant has subscribed to). Plans, editions, and add-ons live in the catalog and are shared across tenants; subscriptions live in the tenant scope and reference the catalog.

## Plan

A `Plan` is a catalog-level record with a name, an `Edition` tier, a monthly price, and a short description. It is immutable at the record level (C# `sealed record` with `init`-only setters); catalogs change by introducing new plan ids rather than mutating existing ones.

```csharp
public sealed record Plan
{
    public required PlanId Id { get; init; }
    public required string Name { get; init; }
    public required Edition Edition { get; init; }
    public required decimal MonthlyPrice { get; init; }
    public string Description { get; init; } = string.Empty;
}
```

Plans are *not* tenant-scoped — `ListPlansAsync` returns the full catalog regardless of the ambient tenant.

## Edition

`Edition` is a three-value enum that layers a tier onto each plan:

- `Lite` — entry-level.
- `Standard` — mid-tier.
- `Enterprise` — top-tier.

Edition appears on both `Plan` (as a property of the catalog entry) and `Subscription` (as the tier the tenant actually bought). Storing it on both ends lets a tenant's subscription outlive changes to the catalog plan.

## AddOn

An `AddOn` is a catalog-level optional product attached on top of a subscription. Add-ons are not tenant-scoped — the *association* between a tenant's subscription and an add-on lives on `Subscription.AddOns` (`IReadOnlyList<AddOnId>`).

```csharp
public sealed record AddOn
{
    public required AddOnId Id { get; init; }
    public required string Name { get; init; }
    public required decimal MonthlyPrice { get; init; }
    public string Description { get; init; } = string.Empty;
}
```

Add-ons are attached via `ISubscriptionService.AddAddOnAsync(subscriptionId, addOnId, ct)`. Duplicate adds are idempotent — attempting to add the same add-on twice leaves the subscription in the same logical state.

## Subscription

`Subscription` is the tenant-scoped binding of a tenant to a plan. It implements `IMustHaveTenant`.

```csharp
public sealed record Subscription : IMustHaveTenant
{
    public required SubscriptionId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required PlanId PlanId { get; init; }
    public required Edition Edition { get; init; }
    public required DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public IReadOnlyList<AddOnId> AddOns { get; init; } = [];
}
```

`EndDate` is optional — `null` means open-ended. `Edition` is copied onto the subscription at creation time so the tenant is insulated from later catalog changes.

## Typical workflow

```csharp
// Browse the catalog
await foreach (var plan in svc.ListPlansAsync(ct))
{
    Console.WriteLine($"{plan.Name} / {plan.Edition} — ${plan.MonthlyPrice}/mo");
}

// Create a subscription for the current tenant
var sub = await svc.CreateSubscriptionAsync(new CreateSubscriptionRequest
{
    PlanId = plan.Id,
    Edition = Edition.Standard,
    StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
});

// Attach an add-on
sub = await svc.AddAddOnAsync(sub.Id, addOnId, ct);
```

## Listing subscriptions

`ISubscriptionService.ListSubscriptionsAsync(ListSubscriptionsQuery, ct)` streams subscriptions for the ambient tenant. The query is optional and additive:

```csharp
public sealed record ListSubscriptionsQuery
{
    public PlanId? PlanId { get; init; }
    public Edition? Edition { get; init; }

    public static ListSubscriptionsQuery Empty { get; } = new();
}
```

Pass `ListSubscriptionsQuery.Empty` to list every subscription for the current tenant.

## Catalog vs. tenant boundary

The split between catalog (`Plan`, `AddOn`) and tenant-scoped (`Subscription`, `UsageMeter`, `MeteredUsage`) is the single most important design choice in this block. Practical implications:

- **`Plan` rows live once.** A plan defined by the operator is visible to every tenant.
- **`Subscription` rows are per-tenant.** The same `PlanId` referenced by five subscriptions means five tenants subscribed to that plan.
- **Price at subscribe-time is not captured automatically.** `Subscription.Edition` is captured, but `MonthlyPrice` is not. If the catalog changes its price later, historical subscriptions continue to reference the catalog's latest price when you join. If price-at-subscribe-time matters for billing, capture it in your billing pipeline at the moment `CreateSubscriptionAsync` returns.
- **Add-on association is a list of ids.** The `AddOns` list on `Subscription` is an `IReadOnlyList<AddOnId>`. EF Core materialises it via a list-of-scalar conversion configured in `SubscriptionEntityConfiguration`. Consumers that want richer per-subscription add-on metadata (attached-at timestamps, custom quantity) should add a join entity in their own block.

## Subscription lifecycle

`blocks-subscriptions` does not enforce a state machine (no `Active` / `Cancelled` enum). The record's lifecycle is expressed through `StartDate` / `EndDate` — a subscription is "active" when today is within `[StartDate, EndDate ?? MaxValue]`. This keeps the data model simple; higher-level blocks can layer a status machine if they need one.

To cancel a subscription, set `EndDate` to today. The block does not expose `CancelSubscriptionAsync` — use `UpdateSubscriptionAsync(... with { EndDate = today })` (or the equivalent on the in-memory service). A terminal cancellation that preserves history is a follow-up.

## Add-on idempotency

`AddAddOnAsync(subscriptionId, addOnId)` is idempotent — calling it twice with the same pair leaves the `Subscription.AddOns` list unchanged on the second call. The in-memory implementation checks `List.Contains` before adding. A persistence-backed implementation should use a deterministic insert-if-not-exists query.

Removing an add-on is not currently exposed on the service surface — see usage-meters documentation for the general pattern of "call the underlying store directly" if you need detach semantics today.

## Edition naming guidance

The three edition values are deliberately generic:

- **Lite** — entry-level; often free or low-priced, limited features.
- **Standard** — mid-tier; the catalog's "most common" choice.
- **Enterprise** — top-tier; SLA, white-glove support, advanced features.

If your commercial narrative needs different names (e.g. "Starter / Pro / Business"), display them via a presentation-layer mapping rather than by extending the enum. An enum change is a breaking API change; a display mapping is not.

## Related

- [Overview](overview.md)
- [Usage Meters](usage-meters.md)
- [Entity Model](entity-model.md)
