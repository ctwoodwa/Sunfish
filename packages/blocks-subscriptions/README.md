# Sunfish.Blocks.Subscriptions

Subscription-management block — plans, editions, subscriptions, add-ons, and usage meters with EF Core entity-module contribution per ADR 0015.

## What this ships

### Models

- **`Plan`** — top-level subscription product (e.g., "Pro", "Enterprise"); has `PlanId`, name, base monthly price, billing cadence.
- **`Edition`** — variant of a Plan (e.g., "Pro Annual", "Pro Monthly"); price + term + included-units overrides.
- **`Subscription`** — tenant's active subscription record; references `Plan` + `Edition`; tracks lifecycle (Trial / Active / Suspended / Cancelled / Expired).
- **`AddOn`** — orderable supplement to a base subscription (e.g., extra seats, premium support); `AddOnId`.
- **`UsageMeter`** — metered-billing seam (e.g., API calls, storage GB, transaction count); `UsageMeterId`.
- **`MeteredUsage`** — append-only usage record per subscription + meter + period.

### Services

- **`ISubscriptionService`** + `InMemorySubscriptionService` — CRUD + plan switching + add-on attachment.
- **`SubscriptionEntityModule`** — `ISunfishEntityModule` contribution per ADR 0015.

### UI

- Razor components and localization resources for the subscription-management surface.

## DI

```csharp
services.AddInMemorySubscriptions();
```

## Cluster role

Horizontal block consumed across the cluster. Pairs with `blocks-businesscases` (entitlement resolution against active subscription) and `blocks-tenant-admin` (subscription-management UI surface).

## ADR map

- [ADR 0015](../../docs/adrs/0015-module-entity-registration.md) — module-entity registration

## See also

- [apps/docs Overview](../../apps/docs/blocks/subscriptions/overview.md)
- [Sunfish.Blocks.BusinessCases](../blocks-businesscases/README.md) — entitlement resolver consuming `Subscription`
- [Sunfish.Blocks.TenantAdmin](../blocks-tenant-admin/README.md) — UI surface
