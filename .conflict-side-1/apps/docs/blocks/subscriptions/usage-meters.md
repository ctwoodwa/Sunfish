---
uid: block-subscriptions-usage-meters
title: Subscriptions — Usage Meters
description: UsageMeter and MeteredUsage — recording per-tenant metered consumption against subscriptions.
keywords:
  - usage-meter
  - metered-usage
  - billing-events
  - metering
  - saas-metering
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

## Typical aggregation patterns

Because the service does not provide aggregation, consumers generally pick one of three strategies:

1. **EF Core LINQ** — for persistence-backed implementations, project a sum over `MeteredUsage` for the billing period:
   ```csharp
   var total = await dbContext.Set<MeteredUsage>()
       .Where(u => u.MeterId == meterId)
       .Where(u => u.RecordedAtUtc >= periodStart && u.RecordedAtUtc < periodEnd)
       .SumAsync(u => u.Quantity, ct);
   ```
2. **Pre-aggregated rollup table** — have a nightly job collapse same-day samples into a summary row in a custom table. Cheaper for dashboards but adds write complexity.
3. **External time-series store** — stream usage events to a TSDB (Timescale, InfluxDB) in parallel with writing to `MeteredUsage`. Use the TSDB for dashboards and the record for billing.

None of these are in the block today — each is an explicit build-on-top decision.

## Sample volume considerations

`MeteredUsage` rows accumulate quickly. A single `api-calls` meter with 10 RPS produces ~860k rows per day. The in-memory service is fine for demos and tests; production hosts on any non-trivial metering workload should:

- Use a persistence-backed implementation from day one.
- Partition the `MeteredUsage` table by month (PostgreSQL native partitioning works well here).
- Batch writes — record many samples in one `RecordUsageAsync` call if your API traffic allows.

The block does not provide a batch API (`RecordUsageBatchAsync`) today; adding one is a low-friction follow-up and the service surface is designed to accept one without breaking existing callers.

## Correcting a mistaken sample

Samples are append-only. To correct an overcount, insert a compensating *negative* sample:

```csharp
await svc.RecordUsageAsync(meter.Id, quantity: -5m, ct);
```

The sum-based aggregation will net out correctly. An operator-level "void the last sample" that removes rows is not exposed; that kind of corrective action belongs at a higher privilege level than the block's service surface.

## Unit conventions

`Unit` is free-form text but convention matters for downstream consumers. Recommended values:

- `"calls"` for request-count meters.
- `"mb"`, `"gb"`, `"tb"` for storage (lower case, no unit prefix).
- `"minutes"`, `"hours"` for time-based meters.
- `"seats"` for licence-count meters (`RecordUsageAsync` with `quantity = 1` per seat per day is a common pattern).

A future pass may introduce an `enum UsageUnit` to formalise this; until then, consistency within your own catalog is the pragmatic answer.

## Related

- [Overview](overview.md)
- [Plans and Editions](plans-and-editions.md)
- [Entity Model](entity-model.md)
