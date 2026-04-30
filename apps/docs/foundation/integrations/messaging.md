# Messaging Contracts

`Sunfish.Foundation.Integrations.Messaging` is the contract surface for the bidirectional messaging substrate (per [ADR 0052](../../../docs/adrs/0052-bidirectional-messaging-substrate.md)). It pairs with [`Sunfish.Blocks.Messaging`](../../blocks/messaging/overview.md) (the InMemory implementations + `MessageThread` / `Message` entities) and `providers-postmark` / `providers-twilio` / etc. (the vendor-specific gateways per [ADR 0013](../../../docs/adrs/0013-provider-neutrality-enforcement.md)).

## Contract surface

### Identifiers

| Type | Wraps | Purpose |
|---|---|---|
| `ThreadId` | `Guid` | Thread identifier; round-trips across egress + ingress via `ThreadToken`. |
| `MessageId` | `Guid` | Single message id (substrate-side; distinct from any provider id). |
| `ParticipantId` | `Guid` | Participant on a thread. |
| `ThreadToken` | `string` | Opaque token round-tripped via `Reply-To` / SMS first-line label. HMAC-SHA256 + epoch per ADR 0052 amendment A2. |

### Enums

| Enum | Values |
|---|---|
| `MessageDirection` | `Inbound` / `Outbound` |
| `MessageChannel` | `Email` / `Sms` / `ProviderInternal` |
| `MessageVisibility` | `Public` / `PartyPair` / `OperatorOnly` (3-tier per Minor amendment) |
| `SenderIsolationMode` | `SharedDomain` / `PerTenantStream` / `PerTenantSubdomain` (per A3) |
| `SmsThreadTokenStrategy` | `OmitToken` (Phase 2.1 default) / `InlineToken` (per A4) |

### Records

- `Participant` — identity wrapper (`ActorId`-based per W#31 substitution) + display name + email + phone.
- `OutboundMessageRequest` / `OutboundMessageResult` + `OutboundMessageStatus` enum — egress payload + result.
- `InboundMessageEnvelope` — normalized inbound payload (from any provider adapter) before the 5-layer defense pipeline.
- `MessagingProviderConfig` — per-tenant per-channel config (sender isolation + abuse-defense thresholds + thread-token policy).

### Interfaces

| Interface | Methods |
|---|---|
| `IMessagingGateway` | `SendAsync` + `GetStatusAsync` (egress; provider-implemented) |
| `IThreadStore` | `CreateAsync` + `GetAsync` + `SplitAsync` + `AppendMessageAsync` (CRUD) |
| `IThreadTokenIssuer` | `MintAsync` + `VerifyAsync` + `RevokeAsync` (HMAC-SHA256 implementation in `HmacThreadTokenIssuer`) |
| `IInboundMessageScorer` | `ScoreAsync` (Layer 4 of the 5-layer defense pipeline) |
| `IUnroutedTriageQueue` | `EnqueueAsync` + `ListPendingAsync` + `ResolveAsync` (catch-all manual triage) |
| `IRevokedTokenStore` | `AppendAsync` + `IsRevokedAsync` (consulted by `HmacThreadTokenIssuer.VerifyAsync`) |

### Implementations shipped in `foundation-integrations` (vs `blocks-messaging`)

Phase 1+3 of W#20 ship the canonical implementations for **token issuance + revocation** in `foundation-integrations` (cross-substrate consumers — W#19 Work Orders Phase 6, W#21 Signatures, etc. — depend on these):

- `HmacThreadTokenIssuer` — HMAC-SHA256 with per-tenant key from `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider`.
- `InMemoryRevokedTokenStore` — `ConcurrentDictionary` revocation log.

The thread/message **CRUD** + entity types live in `Sunfish.Blocks.Messaging`. See [the blocks-messaging overview](../../blocks/messaging/overview.md) for the full picture.

## DI bootstrap

```csharp
services.AddInMemoryTenantKeyProvider();   // foundation-recovery stub for Phase 1 dev
// HmacThreadTokenIssuer + InMemoryRevokedTokenStore registered when needed
// (typically by the host or a provider package).
services.AddInMemoryMessaging();           // blocks-messaging — InMemoryThreadStore +
                                           // InMemoryMessagingGateway + entity-module
```

## Cross-substrate consumers

- **W#19 Work Orders** — `WorkOrder.PrimaryThread: ThreadId?` (Phase 6 wires `IThreadStore.SplitAsync` for owner+vendor coordination threads).
- **W#21 Signatures** (`kernel-signatures`) — uses `IThreadTokenIssuer` for signature-event correlation.
- **W#28 Public Listings** — Bridge route for inquiry-form posts will consume `IInboundMessageScorer`.

## See also

- [ADR 0052](../../../docs/adrs/0052-bidirectional-messaging-substrate.md)
- [blocks-messaging overview](../../blocks/messaging/overview.md)
- [W#20 hand-off](../../../icm/_state/handoffs/property-messaging-substrate-stage06-handoff.md)
