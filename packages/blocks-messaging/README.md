# Sunfish.Blocks.Messaging

Bidirectional messaging substrate domain block — `Thread` + `Message` entities, in-memory thread store + messaging gateway, and `MessagingEntityModule` contribution per ADR 0015.

Phase 2.1 of [ADR 0052](../../docs/adrs/0052-bidirectional-messaging-substrate.md). Consumes contracts from `Sunfish.Foundation.Integrations.Messaging` (the kernel-level message envelope + provider-adapter seam).

## What this ships

### Models

- **`MessageThread`** — conversation thread entity (`IMustHaveTenant`); references participating parties + thread-token + creation/update timestamps.
- **`Message`** — single message in a thread; references envelope (`InboundMessageEnvelope` from W#20) + visibility tier + attachments.

### Services

- **`InMemoryThreadStore`** — thread persistence seam.
- **`InMemoryMessagingGateway`** — outbound dispatch + inbound routing seam (in-memory; production wires real `IMessagingGateway`).
- **`MessagingEntityModule`** — `ISunfishEntityModule` contribution per ADR 0015.

## Substrate boundary

This block sits above the foundation-tier messaging substrate (`Sunfish.Foundation.Integrations.Messaging`):

- The substrate ships `IInboundMessageScorer`, `IUnroutedTriageQueue`, `InboundMessageEnvelope`, `MessageChannel` enum, etc.
- This block ships the per-tenant `Thread` + `Message` entities that the substrate's inbound routing populates.

The W#28 public-listings inquiry POST path is a cross-substrate consumer — it builds a synthetic `InboundMessageEnvelope` (per the W#28 P5b unblock addendum) and runs it through the same defense pipeline.

## DI

```csharp
services.AddInMemoryMessaging();
```

(Consumer-side; the foundation substrate's `AddSunfishMessaging()` is the upstream registration.)

## ADR map

- [ADR 0052](../../docs/adrs/0052-bidirectional-messaging-substrate.md) — bidirectional messaging substrate
- [ADR 0015](../../docs/adrs/0015-module-entity-registration.md) — module-entity registration

## See also

- [apps/docs Overview](../../apps/docs/blocks/messaging/overview.md)
- [Sunfish.Foundation.Integrations](../foundation-integrations/) — substrate (`IInboundMessageScorer`, `IUnroutedTriageQueue`, envelopes)
- [Sunfish.Blocks.PublicListings](../blocks-public-listings/README.md) — synthetic-envelope cross-substrate consumer
