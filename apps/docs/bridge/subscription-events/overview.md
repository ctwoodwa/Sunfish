# Bridge → Anchor subscription-event-emitter

`Sunfish.Bridge.Subscription` is the substrate behind sub-second propagation of subscription / billing changes from Bridge (Zone C SaaS) to Anchor (Zone A local-first). When a tenant's edition flips — start, renew, cancel, upgrade, downgrade, dunning, expired — Bridge emits a signed event; the Anchor's local `EditionCapabilities` cache updates immediately rather than waiting on the 30-second polling TTL.

It implements [ADR 0031](../../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) amendments A1 + A1.12 (post-A1.12 council-fixed surface). **Closes the ADR 0062-A1.6 halt-condition** — sub-second `EditionCapabilities` responsiveness path is end-to-end functional once Anchor wires the handler.

## What it gives you

| Type | Role |
|---|---|
| `BridgeSubscriptionEvent` | Wire-format envelope per A1.2 — tenantId, eventType, editionBefore/After, effectiveAt, eventId, deliveryAttempt, signature. camelCase canonical-JSON shape. |
| `BridgeSubscriptionEventType` | The 7 canonical event types per A1.1 (Started / Renewed / Cancelled / TierUpgraded / TierDowngraded / Dunning / Expired). |
| `IEventSigner` / `HmacSha256EventSigner` | HMAC-SHA256 over canonical-JSON bytes (signature field zeroed). Wire prefix `"hmac-sha256:"`. Ed25519 reserved for Phase 2+ per A1.12.2. |
| `ISharedSecretStore` / `InMemorySharedSecretStore` | 90-day rotation cadence + 24h grace window per A1.12.1 — both old + new secrets verify during grace. |
| `IIdempotencyCache` / `InMemoryIdempotencyCache` | Per-tenant LRU dedup; 24h retention; `eventId` is the dedup key. |
| `ReplayWindow` | ±5min clock-skew tolerance per A1.2 (matches AWS Signature V4 + GitHub webhook conventions). |
| `IWebhookDeliveryService` / `DefaultWebhookDeliveryService` | HTTPS POST + 30s timeout + 8-attempt retry policy (1s/5s/30s/5min/30min/2h/12h) + dead-letter on terminal failure per A1.3 + A1.5. |
| `WebhookRetryPolicy` | The 7-step exponential backoff schedule. |
| `IDeadLetterQueue` / `InMemoryDeadLetterQueue` | Persistence for events that exhausted the retry budget. |
| `SseReconnectPolicy` | UNBOUNDED 1s/5s/30s/60s-capped reconnect schedule per A1.12.4. **Distinct from webhook retry** — SSE never dead-letters the connection. |
| `SseQueueOverflowPolicy` | 1h max-age + 10000-event max-depth thresholds; either triggers fallback to webhook delivery. |
| `IWebhookRegistrationService` / `DefaultWebhookRegistrationService` | `POST /api/v1/tenant/webhook` per A1.4 — HTTPS-only + non-loopback validation + 32-byte auto-generated shared secret. |
| `ITrustChainResolver` / `DefaultTrustChainResolver` | Per-Anchor cert pinning (PEM upload at registration); self-signed allowance is admin opt-in only with audit emission per A1.12.3. |
| `IBridgeSubscriptionEventHandler` / `InMemoryBridgeSubscriptionEventHandler` | Anchor-side handler per A1.6 — verify HMAC, replay-window check, idempotency, edition-cache update, audit emission. |
| `BridgeSubscriptionAuditPayloads` | Factory for the 10 W#36 audit-event bodies (alphabetized; per ADR 0049 convention). |

## End-to-end flow

```
1. Bridge: subscription state changes (e.g., tenant upgrades to bridge-pro).
2. Bridge: builds an unsigned BridgeSubscriptionEvent.
3. Bridge: HmacSha256EventSigner.SignAsync(unsigned, sharedSecret) → signed event.
4. Bridge: BridgeSubscriptionEventEmitted audit emits.
5. Bridge: DefaultWebhookDeliveryService.DeliverAsync(signed, callbackUrl):
   a. POST canonical-JSON to anchor's URL with 30s timeout.
   b. On 2xx: BridgeSubscriptionEventDelivered audit emits.
   c. On non-2xx / timeout / network error: backoff per WebhookRetryPolicy + retry.
   d. On 8th attempt failure: DLQ + BridgeSubscriptionEventDeliveryFailedTerminal audit.
6. Anchor: receives POST.
7. Anchor: InMemoryBridgeSubscriptionEventHandler.HandleAsync:
   a. Verify HMAC (current OR previous-in-grace per A1.12.1) — fail → 401 + signature-failed audit.
   b. Replay-window check (±5min) — fail → 410 Gone + stale audit.
   c. Idempotency check (eventId LRU) — duplicate → 200 (no-op; Bridge stops retrying).
   d. Update local EditionCapabilities cache via IEditionCacheUpdater (host-supplied).
   e. BridgeSubscriptionEventReceived audit emits.
   f. Return 200.
```

## Wiring

```csharp
// Bootstrap (audit-disabled — test/bootstrap)
services.AddHttpClient<IWebhookDeliveryService>();
services.AddInMemoryBridgeSubscription();

// Bootstrap (audit-enabled — production; both IAuditTrail + IOperationSigner
// must already be registered)
services.AddInMemoryBridgeSubscription(currentTenantId);

// Anchor wires the host-specific edition-cache update path:
services.AddSingleton<IEditionCacheUpdater, MyAnchorEditionUpdater>();
```

The Anchor's `IEditionCacheUpdater` implementation is where ADR 0062's `IMissionEnvelopeProvider` integration lives — typically: parse the `editionAfter` field, update the local `IEditionResolver` cache, fire an envelope-change event on `IMissionEnvelopeProvider`. This handler substrate is intentionally agnostic about *how* the edition cache is structured; it just calls the host-supplied callback.

## Audit emission

10 `AuditEventType` discriminators ship with this substrate (per A1.7 + A1.12):

| Event | Side | Trigger | Dedup |
|---|---|---|---|
| `BridgeSubscriptionEventEmitted` | Bridge | Each emit attempt | None |
| `BridgeSubscriptionEventDelivered` | Bridge | HTTP 2xx | None |
| `BridgeSubscriptionEventDeliveryFailed` | Bridge | Retryable failure | 1h per (tenant, eventId) |
| `BridgeSubscriptionEventDeliveryFailedTerminal` | Bridge | All 7 retries exhausted (DLQ) | None (security-relevant) |
| `BridgeSubscriptionWebhookRegistered` | Bridge | Each `POST /api/v1/tenant/webhook` | None |
| `BridgeSubscriptionWebhookRotationStaged` | Bridge | 90-day rotation cadence per A1.12.1 | None |
| `BridgeWebhookSelfSignedCertsConfigured` | Bridge | Admin opt-in per A1.12.3 | None |
| `BridgeSubscriptionEventReceived` | Anchor | Verified + processed | 24h per (tenant, eventId) |
| `BridgeSubscriptionEventSignatureFailed` | Anchor | HMAC verification failed | 1h per (tenant, sourceIp) |
| `BridgeSubscriptionEventStale` | Anchor | `effectiveAt` outside ±5min | 1h per (tenant, eventType) |

## Cohort context

W#36 closes the cohort of substrate workstreams that establish the Bridge/Anchor coordination plane (W#34 Foundation.Versioning + W#35 Foundation.Migration + W#36 this). Audit-event-payload conventions, `JsonStringEnumConverter` shape, two-overload `AddInMemoryX()` DI, W#32 both-or-neither audit pattern — all match.

Halt-conditions (all stayed off the trip-wire during build):

1. **HMAC vs Ed25519 confusion.** Phase 1 ships HMAC-SHA256 only; Ed25519 enum value reserved for Phase 2+ per A1.12.2.
2. **SSE reconnect ≠ webhook retry.** Distinct types (`SseReconnectPolicy` vs `WebhookRetryPolicy`); SSE is unbounded with 60s cap, webhook is 7-attempt with dead-letter.
3. **Per-Anchor secret persistence.** `ISharedSecretStore` declares the contract; production hosts wrap with foundation-recovery `IFieldEncryptor` for at-rest encryption.
4. **Trust default `false`.** `DefaultTrustChainResolver` defaults every tenant to `PubliclyRootedCa`; self-signed is admin opt-in only with audit emission at configuration time per A1.12.3.

## Phase 1 scope

Substrate-only. Consumer wiring (ADR 0062 Phase 1's `EditionCapabilities` integration via `IEditionCacheUpdater`) is a separate workstream when COB capacity opens.
