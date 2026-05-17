# foundation-events

The canonical cross-cluster domain-event substrate for Sunfish.

## What it provides

- **`DomainEventEnvelope<TPayload>`** — the canonical envelope every
  cross-cluster event carries. 10 required fields:
  `EventId / EventType / SchemaVersion / OccurredAt / TenantId /
  OriginatingReplicaId / IdempotencyKey / Payload` + optional
  `CausationId / CorrelationId`. Per
  `_shared/engineering/cross-cluster-event-bus-design.md` §1.
- **`IDomainEventPublisher`** — producer-side emit contract.
  `DefaultDomainEventPublisher` is the canonical impl;
  `NoopDomainEventPublisher` is the test seam.
- **`IDomainEventStore`** — SQLite-backed append-only persistence
  (`domain_events` table). `SqliteDomainEventStore` is the
  implementation; idempotent on `(tenant_id, idempotency_key)` via
  `INSERT ... ON CONFLICT DO NOTHING` returning the existing event
  id on dedup.
- **`IEventReader`** — consumer-side cursor model (per-handler
  cursors in the `event_handler_cursors` table). `SqliteEventReader`
  is the implementation; cursors stay pinned on handler failure with
  a 7-step retry backoff (30s → 2m → 10m → 1h → 6h → 24h → 72h →
  exhausted).
- **`IEventDispatcher`** — in-process best-effort push delivery.
  `InProcessEventDispatcher` is the implementation; subscriber
  failures are isolated; not the durable path (that's the cursor
  reader).
- **`EventDispatcherHost`** — `Microsoft.Extensions.Hosting`
  background service that ticks `SqliteEventReader.DrainOnceAsync`
  on a poll interval (default 1s).
- **`EventId`** — inline ULID generator (26-char Crockford base-32)
  used for envelope `EventId` + failure-row primary keys.
- **`ReplicaId`** (in `Sunfish.Foundation.Assets.Common`) — opaque
  per-replica identifier carried in every envelope.

## When to use

Every `blocks-*` cluster that emits cross-cluster events per
`_shared/engineering/cross-cluster-event-bus-design.md` §3 catalog.
Examples:

- `blocks-financial-*` emits `Financial.JournalEntryPosted`,
  `Financial.PaymentApplied`, `Financial.PeriodSoftClosed`, etc.
- `blocks-work-*` emits `Work.WorkOrderCreated`,
  `Work.WorkOrderCompleted`, etc.
- `blocks-people-*` emits `People.TenantActivated`,
  `People.LeaseExecuted`, etc.

## Idempotency-key discipline

Per `cross-cluster-event-bus-design.md` §4, every event type defines
a deterministic derivation of its idempotency key from its semantic
identity. The catalog (§3.1+) pins per-event-type formats. Two
emissions with the same `(TenantId, IdempotencyKey)` tuple collapse
into a single row at the store level — producers can retry safely
without double-effect.

## Quickstart

```csharp
// Host composition root:
services.AddSingleton(_ => new SqliteConnection("Data Source=anchor.db"));
services.AddFoundationEvents();
services.AddBlocksFinancialPeriods();
services.AddBlocksFinancialTax();

var app = builder.Build();
await app.Services.ApplyFoundationEventsMigrationsAsync();

// In a cluster handler:
var envelope = new DomainEventEnvelope<PeriodSoftClosed>
{
    EventId        = EventId.New(),
    EventType      = "Financial.PeriodSoftClosed",
    SchemaVersion  = 1,
    OccurredAt     = DateTimeOffset.UtcNow,
    TenantId       = tenantContext.Current,
    OriginatingReplicaId = replicaContext.Current,
    IdempotencyKey = $"period-soft-closed:{periodId.Value}:{occurredAtTicks}",
    Payload        = new PeriodSoftClosed(periodId, chartId, principal.UserId),
};
await publisher.PublishAsync(envelope, ct);
```

## Migration path for new clusters

Each cluster that wants to emit cross-cluster events:

1. Add `<ProjectReference Include="..\foundation-events\Sunfish.Foundation.Events.csproj" />` to the package csproj.
2. Add `using Sunfish.Foundation.Events;` to the service file that
   publishes.
3. Inject `IDomainEventPublisher` via constructor.
4. Construct `DomainEventEnvelope<TPayload>` and call
   `PublishAsync`.

No local `IDomainEventPublisher` declaration; no DI registration in
the cluster — the host's `AddFoundationEvents()` provides the
canonical surface.

## What ships in PR 6 + what's deferred

**PR 6 (this):** DI extension + cluster migration sweep
(`blocks-financial-periods` + `blocks-financial-tax`) + this docs
walkthrough.

**Deferred to follow-on:**

- **PR 5 — Loro op-log bridge** for cross-replica sync. Deferred
  pending `ILoroDocAccessor` substrate ratification in `kernel-crdt`.
  Single-replica Anchor instances don't need cross-replica sync;
  the per-handler cursor model handles in-process delivery already.
- **Real ULID library** — `EventId.New()` is inline and produces
  the canonical 26-char Crockford base-32 string format. A future
  swap to the upstream `Ulid` NuGet is value-shape-only (consumers
  treat the EventId as opaque string).
- **Multi-tenant `EventDispatcherHost` drain** — v1 takes a single
  `TenantId` at construction. Multi-tenant hosts (rare; the
  canonical Anchor is one tenant per replica) walk
  `ITenantCatalog` in a v2 enhancement.
- **`TenantContext` / `ReplicaContext` injection** — producers
  currently pass `TenantId` + `ReplicaId` explicitly. A future
  follow-on adds `ITenantContext` + `IReplicaContext` interfaces
  so handlers can inject + ambient them.
