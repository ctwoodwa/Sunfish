# Messaging

`Sunfish.Blocks.Messaging` is the bidirectional-messaging substrate for the Sunfish property cluster — durable, audit-logged, multi-party threads spanning email + SMS + provider-internal channels.

It implements [ADR 0052 — Bidirectional Messaging Substrate](../../../docs/adrs/0052-bidirectional-messaging-substrate.md).

## What it gives you

| Type | Role |
|---|---|
| `MessageThread` | Durable conversation between a fixed participant set under a single per-thread visibility default. |
| `Message` | One inbound or outbound payload on a thread (Email / Sms / ProviderInternal). |
| `IThreadStore` | Thread CRUD: `CreateAsync` / `GetAsync` / `SplitAsync` / `AppendMessageAsync`. |
| `IMessagingGateway` | Egress: `SendAsync` + `GetStatusAsync`. Provider implementations live in `providers-*` per ADR 0013. |
| `IThreadTokenIssuer` | Mint / Verify / Revoke opaque thread tokens (HMAC-SHA256 per ADR 0052 amendment A2). |
| `IInboundMessageScorer` | Layer 4 of the 5-layer inbound defense pipeline (per A1). |
| `IUnroutedTriageQueue` | Last-resort holding queue for inbound messages that don't route cleanly. |

The contracts ship in `Sunfish.Foundation.Integrations.Messaging` (kernel-tier-adjacent, framework-neutral); the `MessageThread` + `Message` entities, `InMemoryThreadStore`, `InMemoryMessagingGateway`, and `MessagingEntityModule` live in `Sunfish.Blocks.Messaging`.

## 3-tier visibility model (Minor amendment)

Three primitives compose to handle every cluster requirement:

| Tier | Semantics |
|---|---|
| `Public` | Visible to every participant on the thread. |
| `PartyPair` | Visible only to a specific party pair (e.g., owner + vendor private aside on a shared work-order thread). |
| `OperatorOnly` | Visible only to the operator role (BDFL/property owner); not exposed to vendors, tenants, or applicants. |

The `MessageVisibility` enum lives in the contracts package; each `Message` carries its own override that may narrow the thread default.

## ThreadToken (HMAC-SHA256, per ADR 0052 A2)

Outbound messages embed an opaque token that round-trips on reply (`Reply-To` header for Email; first-line label for SMS). The `HmacThreadTokenIssuer` mints + verifies tokens with per-tenant key material from `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider`:

- **Mint** — HMAC-SHA256 over UTF-8(`tenant.Value || ":" || thread.Value || ":" || ticks`); base32-encode HMAC + epoch with a `.` separator.
- **Verify** — recompute HMAC, check 90-day TTL, reject revoked tokens.
- **Revoke** — append the HMAC fragment to `IRevokedTokenStore` (idempotent + tenant-scoped).

The Phase 1 stub (`InMemoryTenantKeyProvider`) uses HKDF-SHA256 over the tenant id with a fixed development salt — **not secure for production**. ADR 0046 Stage 06 will replace with the operator-key-backed KEK hierarchy.

## SMS thread-token strategy (per A4)

SMS providers strip arbitrary headers; tokens have to live in the message body when used. Per-tenant config picks one of:

- `OmitToken` — Phase 2.1 default. Outbound carries no token; inbound replies route via fuzzy sender-recency matching (14-day window).
- `InlineToken` — first line of outbound contains `[Ref: 7A2K…]`; inbound uses the token when preserved by the carrier and falls through to fuzzy matching otherwise.

## Inbound 5-layer defense pipeline (per A1)

Inbound messages pass through five layers in order before being persisted as a `Message`:

1. **Provider signature verify** — `PostmarkSignatureVerifier` (or vendor equivalent); 401 on failure.
2. **Sender allow-list** — `MessagingProviderConfig.AllowedSenderDomains` + `AllowedFromAddresses`.
3. **Rate limit** — sliding window per sender + per tenant; soft-reject queues to triage.
4. **`IInboundMessageScorer`** — pluggable abuse classifier (Phase 5 ships a `NullScorer` default).
5. **`IUnroutedTriageQueue`** — manual operator triage for ambiguous routing.

After all 5 pass: route to thread via `ThreadToken` lookup OR fuzzy sender-recency matching.

## Per-tenant gateway configuration

`MessagingProviderConfig` declares the egress + ingress provider per tenant per channel + abuse-defense thresholds + sender-isolation strategy. Credentials resolve via `CredentialsReference` (per ADR 0013); the substrate never holds plaintext.

## DI bootstrap

```csharp
services.AddInMemoryMessaging();   // blocks-messaging in-memory substrate
services.AddInMemoryTenantKeyProvider();  // foundation-recovery stub for Phase 1 dev
// Production: register a real provider via providers-postmark / providers-twilio / etc.
```

## Phase 2.1 scope (shipped)

- Contracts (`IMessagingGateway` / `IThreadStore` / `IThreadTokenIssuer` / `IInboundMessageScorer` / `IUnroutedTriageQueue`)
- `MessageThread` + `Message` entities, in-memory implementations
- `HmacThreadTokenIssuer` + `IRevokedTokenStore`
- `MessagingEntityModule` per ADR 0015

## Out of scope (future phases)

- `providers-postmark` first email adapter (Phase 4)
- 5-layer defense host integration + bridge route (Phase 5)
- Audit emission (12 new `AuditEventType` constants — Phase 6)
- SendGrid / Twilio / SES adapters (follow-up hand-offs)
- Phase 2.3 deliverability isolation (per-tenant subdomain DKIM/SPF/DMARC)

## See also

- [ADR 0052](../../../docs/adrs/0052-bidirectional-messaging-substrate.md)
- [ADR 0013](../../../docs/adrs/0013-provider-neutrality-enforcement.md) — provider-neutrality
- [ADR 0046](../../../docs/adrs/0046-recovery-multisig-key-recovery.md) — Foundation.Recovery (provides `ITenantKeyProvider`)
- [ADR 0049](../../../docs/adrs/0049-audit-trail-substrate.md) — audit emission target
- [W#20 hand-off](../../../icm/_state/handoffs/property-messaging-substrate-stage06-handoff.md)
