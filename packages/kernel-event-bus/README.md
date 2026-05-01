# Sunfish.Kernel.EventBus

Sunfish kernel §3.6 Event Bus — in-process reference implementation using `System.Threading.Channels` with per-entity ordering, Ed25519-signed events, and idempotent delivery.

## What this ships

### Contracts

- **`IEventBus`** — publish/subscribe surface. Topics are typed by the event payload class.
- **`IEventEnvelope<T>`** — kernel-signed envelope: payload + tenant + entity-id + sequence number + Ed25519 signature.
- **`IEventHandler<T>`** — subscriber interface; handlers register declaratively via DI.

### Reference impl

- **`InMemoryEventBus`** — Channels-backed in-process bus.
- **Per-entity ordering** — events for the same `(tenant, entity-id)` are dispatched in submit order (FIFO per entity); concurrency across distinct entities.
- **Ed25519-signed events** — every envelope carries a signature over the canonical-JSON form; `IAuditTrail` consumers can verify.
- **Idempotent delivery** — handlers see each `(tenant, sequence)` exactly once; redeliveries are deduped by sequence number.

### Determinism + testability

- `InMemoryEventBus` exposes `WaitForDeliveryAsync` so tests can deterministically wait for handler completion before asserting (resolves the W#4 bug-268 readiness-wait flake).

## DI

```csharp
services.AddSunfishEventBus();
services.AddSingleton<IEventHandler<MyEvent>, MyHandler>();
```

## ADR map

- Sunfish kernel §3.6 (Event Bus)
- [ADR 0049](../../docs/adrs/0049-foundation-audit.md) — kernel-audit consumes signed event envelopes for the audit trail

## Out of scope

- Cross-process / cross-host event distribution — Wolverine + RabbitMQ shipped in the Bridge SaaS posture handles that; this kernel package is in-process only.

## See also

- `Sunfish.Foundation.RuleEngine.EventBridge` — wires this bus to the rule engine for reactive evaluation
