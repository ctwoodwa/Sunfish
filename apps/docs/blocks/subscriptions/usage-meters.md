---
uid: block-subscriptions-usage-meters
title: Subscriptions — Usage Meters
description: UsageMeter and MeteredUsage — recording per-tenant metered consumption against subscriptions.
---

# Subscriptions — Usage Meters

## Overview

`blocks-subscriptions` includes a lightweight metering model for recording per-tenant consumption against a subscription. Meters are named dimensions (`api-calls`, `seats-used`, `storage-gb`); usage records are point-in-time samples written against a meter. Both types are tenant-scoped (`IMustHaveTenant`) so every sample and every meter carry the tenant identity at rest.

## UsageMeter

A `UsageMeter` attaches a named dimension to a specific subscription:

```csharp
public sealed record UsageMeter : IMustHaveTenant
{
    public required UsageMeterId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required SubscriptionId SubscriptionId { get; init; }
    public required string Code { get; init; }   // e.g. "api-calls"
    public required string Unit { get; init; }   // e.g. "calls"
}
```

The `Code` is a stable identifier (typical kebab-case). `Unit` is a free-form label shown in UI. Both are `string` — there is no built-in enum of supported units.

## MeteredUsage

A `MeteredUsage` is a single recorded sample:

```csharp
public sealed record MeteredUsage : IMustHaveTenant
{
    public required Guid Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required UsageMeterId MeterId { get; init; }
    public required decimal Quantity { get; init; }
    public required DateTime RecordedAtUtc { get; init; }
}
```

Samples are append-only — the service does not expose an update or delete method. To correct a sample, record a compensating negative sample or (in a persistence-backed world) issue a backend correction.

## Recording usage

`ISubscriptionService.RecordUsageAsync(UsageMeterId meterId, decimal quantity, CancellationToken ct)` appends a sample to the specified meter and returns the persisted record. `quantity` is expected to be non-negative; the current in-memory implementation does not enforce this, so callers should pre-validate.

```csharp
await svc.RecordUsageAsync(meter.Id, quantity: 42m, ct);
```

## Aggregation

The service does not expose a built-in aggregation (sum over a period) today. Aggregation is left to downstream code — a reporting layer, a SQL query on the persistence model, or an external time-series store. This is intentional: the block's scope is recording and storing, not billing.

**Follow-up:** introduce `GetUsageTotalAsync(UsageMeterId, DateTime from, DateTime to, ct)` or a streaming equivalent once the billing story firms up.

## Typical workflow

```csharp
// Typically done at subscription creation time, outside this block's service surface.
var meter = new UsageMeter
{
    Id = UsageMeterId.NewId(),
    TenantId = tenantId,
    SubscriptionId = subscription.Id,
    Code = "api-calls",
    Unit = "calls",
};

// Record usage on each API call (or batch per minute).
await svc.RecordUsageAsync(meter.Id, quantity: 1m, ct);
```

Note that *creating* a meter is not currently exposed on `ISubscriptionService`. The in-memory service stores meters internally; a persistence-backed service is expected to expose a create or seed path separately, or accept pre-seeded meter data. This is a known gap.

## Multi-tenancy

All meter and usage records implement `IMustHaveTenant`. The service implementations are expected to filter by the ambient tenant; passing a meter id that belongs to a different tenant should behave as "not found" rather than silently recording a cross-tenant sample.

## Related

- [Overview](overview.md)
- [Plans and Editions](plans-and-editions.md)
- [Entity Model](entity-model.md)
