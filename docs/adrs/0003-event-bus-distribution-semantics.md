---
id: 3
title: Event-Bus Distribution Semantics
status: Accepted
date: 2026-04-19
tier: kernel
concern:
  - audit
  - distribution
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0003 — Event-Bus Distribution Semantics

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** G33 (Appendix C #3)

---

## Context

The Sunfish platform specification asks (Appendix C #3): *"Event bus distribution semantics —
Sunfish default (at-least-once) or opt-in stronger (exactly-once via Kafka transactions)?"*

The spec §3.6 already declares at-least-once delivery with idempotent subscribers as the baseline.
The open question is whether exactly-once semantics (via Kafka transactions or equivalent) should
be an opt-in path, and if so, how it is exposed.

**Shipped state (G3, PR #23):**
`IEventBus` is the kernel contract. `InMemoryEventBus` is the reference implementation:
- Publishes `SignedOperation<KernelEvent>` envelopes after Ed25519 signature verification.
- Deduplicates by `Nonce` — duplicate envelopes are silently discarded, making publish retries
  safe.
- Per-`EntityId` ordering is maintained via `System.Threading.Channels` — events for the same
  entity are yielded in publish order; cross-entity ordering is not guaranteed.
- `ResumeFrom` checkpoint is a no-op in the in-memory backend (no replay buffer).

The `IEventBus` interface is transport-neutral by design — the XML doc comments on the interface
explicitly state that distributed backends (MassTransit, Wolverine, Kafka, libp2p) are a
deliberate follow-up.

---

## Decision

**At-least-once is the normative delivery contract. Exactly-once is a pluggable backend option,
not an API-level concern.**

### 1. `IEventBus` consumers MUST be idempotent — this is a contract requirement

Idempotency is not a guideline; it is a **hard requirement** for any `IEventBus` subscriber. The
nonce-level deduplication in `InMemoryEventBus` provides a best-effort dedup window, but it is
not a substitute for subscriber-side idempotency (the in-memory dedup window is not durable and
does not survive process restarts).

The recommended idempotency pattern for Sunfish subscribers:

```csharp
// Canonical idempotency shape — no new types required.
// Subscribers track processed nonces and use conditional writes.

async ValueTask HandleAsync(SignedOperation<KernelEvent> envelope)
{
    // 1. Check dedup store keyed on envelope.Nonce
    if (await _dedupStore.ExistsAsync(envelope.Nonce, ct)) return;

    // 2. Apply domain change with conditional write (optimistic concurrency)
    await _repository.ApplyAsync(envelope.Payload, expectedVersion: ..., ct);

    // 3. Record nonce with TTL (dedup window; 24 h is a reasonable default)
    await _dedupStore.RecordAsync(envelope.Nonce, ttl: TimeSpan.FromHours(24), ct);
}
```

This pattern — (a) nonce check, (b) conditional write, (c) nonce record — is the standard shape.
No new Sunfish types are needed; the `Nonce` field on `SignedOperation<T>` is already the natural
deduplication key.

### 2. `InMemoryEventBus` — single-process at-least-once

The in-process reference backend provides:
- At-least-once delivery (publish retries are safe via nonce dedup).
- Per-entity ordering via `Channel<T>`.
- Non-durable: events published before a subscription started are not visible.

This is the correct backend for integration tests and single-process applications. It is not
appropriate for multi-process or distributed deployments.

### 3. Exactly-once via pluggable backend — v1+ option

A future `MassTransit.EventBus` (or `Wolverine.EventBus`, `Kafka.EventBus`) backend can expose
Kafka-transaction semantics as a backend-specific binding configuration. The `IEventBus` interface
does not need to change — only the backend registration differs:

```csharp
// Hypothetical future registration — exact API TBD in that PR:
services.AddSunfishEventBus(bus => bus
    .UseMassTransit(mt => mt
        .UseKafka(kafka => kafka
            .EnableExactlyOnce()               // Kafka transaction semantics
            .WithTransactionalIdPrefix("sf-")
        )
    ));
```

Exactly-once via Kafka transactions requires Kafka 2.5+ with idempotent producers and
transactional APIs. This complexity is appropriate for high-stakes financial or audit-log
scenarios; it must not be imposed on all deployments.

### 4. Delivery guarantee matrix

| Backend | Ordering | Delivery | Durable | Replay |
|---------|----------|----------|---------|--------|
| `InMemoryEventBus` | Per-entity | At-least-once | No | No |
| Future `MassTransit` (RabbitMQ) | Per-entity | At-least-once | Yes | No |
| Future `MassTransit` (Kafka) | Per-entity | At-least-once (default) / Exactly-once (opt-in) | Yes | Yes (offset replay) |

---

## Consequences

**Positive**
- Spec §3.6 is now codified as a hard contract, not a soft guideline.
- `IEventBus` is stable — no API changes needed when distributed backends ship.
- The idempotency pattern is documented; subscriber authors have a clear template.
- Exactly-once opt-in is scoped to backend configuration, preventing API sprawl.

**Negative / Trade-offs**
- Subscribers bear the cost of idempotency implementation. There is no framework-provided
  dedup store; each application must supply one (Redis, SQL, in-memory dictionary, etc.).
- The dedup window TTL is application-defined. Choosing too short a window risks duplicate
  processing for slow or retried events; too long a window wastes storage.
- MassTransit / Kafka backend is not shipped; distributed deployments must wait for v1+.

**Revisit triggers**
- MassTransit backend PR — update the delivery guarantee matrix above.
- A Sunfish accelerator requires exactly-once semantics before v1+.

---

## References

- Sunfish platform spec §3.6
- `packages/kernel-event-bus/IEventBus.cs`
- G3 `InMemoryEventBus` implementation (PR #23)
- Gap analysis G33: `icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md`
