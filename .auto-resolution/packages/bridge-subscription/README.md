# Sunfish.Bridge.Subscription

Bridge → Anchor subscription-event-emitter substrate per [ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) amendments A1 + A1.12 (post-council surface).

## Phase 1 scope (this slice)

- `BridgeSubscriptionEvent` (record per A1.2) — canonical-JSON wire shape.
- `BridgeSubscriptionEventType` (enum, 7 values per A1.1).
- `DeliveryMode` enum (`Webhook` / `Sse` per A1.3).
- `SignatureAlgorithm` enum (`HmacSha256` default; `Ed25519` reserved per A1.12.2).
- `WebhookRegistration` + `SubscribedEventFilter` records per A1.4.
- 10 new `AuditEventType` constants in `Sunfish.Kernel.Audit` per A1.7 + A1.12.

## Subsequent phases

- **P2** — `IEventSigner` + `HmacSha256EventSigner` + `BridgeSubscriptionAuditPayloads` factory + per-tenant LRU dedup (24h) + ±5min clock-skew window.
- **P3** — `IWebhookDeliveryService` + retry policy (1s/5s/30s/5min/30min/2h/12h × 7) + dead-letter queue + per-Anchor cert pinning + self-signed override.
- **P4** — `ISseDeliveryService` (unbounded reconnect per A1.12.4) + per-tenant queue (1h/10000-event fallback) + `POST /api/v1/tenant/webhook` registration + 90-day secret rotation with 24h grace.
- **P5** — `IBridgeSubscriptionEventHandler` Anchor-side default impl + `AddInMemoryBridgeSubscription()` DI + `apps/docs/bridge/subscription-events/overview.md` + ledger flip. Closes ADR 0062-A1.6 halt-condition.

## Cohort discipline

W#34 (Foundation.Versioning) + W#35 (Foundation.Migration) + W#30 (Foundation.Transport) established the substrate-package conventions this package reuses: `JsonStringEnumConverter` for canonical-JSON-stable enums, camelCase property names, two-overload `AddInMemoryX()` DI extension, `apps/docs/X/overview.md` page convention, W#32 both-or-neither audit-enabled constructor pattern.
