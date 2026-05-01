# ADR 0031 — Bridge as Hybrid Multi-Tenant SaaS (Zone C default, Option B contractual)

**Status:** Accepted (2026-04-23)
**Date:** 2026-04-23
**Supersedes:** ADR 0026 (Bridge Posture — the dual-posture "SaaS shell + managed relay" framing is replaced by this ADR's Zone-C Hybrid model; both Bridge deployment modes are now paper-aligned).
**Resolves:** Paper v12.0 §17.2 ("Hosted relay as a SaaS node") and §20 (Architecture Selection Framework) give Bridge a clearer conceptual home than ADR 0026's framing allowed. Bridge is not "SaaS shell vs. relay" — it is the **Zone-C Hybrid implementation** of the paper: a hosted node peer (plus a traditional web layer for signup, billing, and the browser-accessible shell) serving multiple commercial tenants. This ADR decides (a) the tenant-isolation posture, (b) the browser-shell key-bootstrap flow, and (c) the migration path for ADR 0026's current code.

Anchor's role is confirmed but trivially: **Anchor is the Zone-A local-first desktop implementation of the paper.** The two accelerators now have distinct, paper-aligned roles:

| Accelerator | Zone | Paper mapping |
|---|---|---|
| Anchor | A — Local-First Node | §5 (kernel), §13 (UX), §20.7 Zone A |
| Bridge | C — Hybrid | §17.2 (hosted-relay-as-SaaS), §20.7 Zone C |

---

## Context

Commercial customers of Sunfish are expected to fall into two buckets:

1. **"Install Anchor on our workstations"** — enterprise IT runs the full paper architecture on their own hardware; no vendor-hosted infrastructure. Zone A.
2. **"Give us a URL we can log into"** — SMB or enterprise customers who want SaaS ergonomics (no install, log in from any browser, vendor handles ops) but can't accept a pre-paper SaaS's data-custody risk. Zone C.

The first bucket is already served: `accelerators/anchor/` is the paper's §5.1 kernel wrapped in a MAUI desktop shell with QR onboarding (Wave 3.3).

The second bucket is what Bridge needs to become. Today's Bridge is a classic multi-tenant SaaS (Aspire + Postgres + DAB + SignalR + Wolverine, with `DemoTenantContext` as the authoritative tenant resolver). That shape was chosen before the paper landed. Paper §17.2's new clause — *"operating the relay as a full replicated node yields a cloud-hosted, browser-accessible deployment indistinguishable from SaaS from the user's perspective ... the relay stores ciphertext only; role keys remain on end-user devices"* — gives us a specific design: Bridge runs hosted-node peers, not an authoritative tenant database.

The question this ADR answers: **when three or four commercial clients each sign up, how is their data and compute isolated?**

---

## Decision drivers

- **Paper v12.0 §17.2 ciphertext-at-rest invariant** — the operator must never hold plaintext of team data. This constrains the multi-tenancy threat model in ways that don't apply to conventional SaaS.
- **Paper §20.7 Zone C** — hybrid is the expected outcome for enterprise software; Bridge is the canonical example.
- **Pre-release + breaking-changes-approved posture** — we can reshape Bridge's internals freely.
- **Commercial viability** — 3–4 clients at launch grows to 30–40 within 18 months. Per-tenant isolated deployments don't scale at that rate without a large ops team.
- **Compliance plausibility** — regulated clients (healthcare, finance) need to point to isolation boundaries that don't rely on "the operator won't read your data." Per-tenant data-plane isolation gives them that boundary even when the control plane is shared.
- **Ciphertext-as-defense-in-depth** — multi-tenant bugs that leak bytes across tenants yield undecryptable ciphertext, not exposed team data. This makes multi-tenant substantially safer than in traditional SaaS.

---

## Considered options

### Option A — Fully multi-tenant Bridge

One Bridge control plane, one shared hosted-node process serving all tenants via team_id-scoped gossip internally, one shared relay.

- **Pro:** lowest infra cost; familiar SaaS economics.
- **Pro:** paper's ciphertext invariant means cross-tenant byte leakage yields undecryptable data — fundamentally stronger multi-tenancy than traditional SaaS.
- **Con:** compliance audits still ask about cross-tenant isolation; "they're all ciphertext" is a strong line but some regulators want a physical boundary.
- **Con:** noisy-neighbor risk; a rogue team's gossip storm affects others.
- **Con:** single process = single failure domain; bug in Bridge data-plane affects all tenants.

### Option B — Per-tenant isolated Bridge deployments

Each tenant gets its own full Bridge stack: its own control plane, its own hosted node, its own relay, its own infra footprint.

- **Pro:** zero cross-tenant blast radius.
- **Pro:** cleanest compliance story; each tenant can live in its own region, cloud, or on-prem.
- **Pro:** per-tenant customization (versions, features, SLAs).
- **Con:** infrastructure cost scales linearly with tenant count.
- **Con:** operator tooling (billing, support, updates) replicates per tenant unless ops team builds an orchestrator.
- **Con:** doesn't fit the "4 clients today, 40 in 18 months" growth path.

### Option C — Hybrid: shared control plane + per-tenant data plane

One shared Bridge control plane for billing/signup/support. Per-tenant data-plane — one `local-node-host` process per tenant, its own SQLCipher DB, its own subdomain (e.g. `acme.sunfish.example.com`). Shared stateless relay tier.

- **Pro:** operator cost close to Option A's efficiency for shared infra (control plane, relay, orchestration).
- **Pro:** per-tenant data-plane isolation gives auditors a physical boundary.
- **Pro:** any tenant can upgrade to Option B (dedicated stack) without changing the protocol — just relocate their hosted-node process.
- **Pro:** paper §17.2 compliant per-tenant; operator still holds only ciphertext.
- **Con:** control-plane bugs can affect all tenants — but control plane holds no team data.
- **Con:** slightly more infra complexity than Option A (N hosted-node processes rather than 1).

### Option D — Status quo + defer

Keep ADR 0026's "SaaS shell + Relay" dual-posture; revisit after first production customer.

- **Pro:** no immediate work.
- **Con:** ships a product that doesn't match the paper's §17.2 framing; first commercial deal will force the refactor anyway.
- **Rejected.**

---

## Decision (recommended)

**Adopt Option C for the default deployment model; Option B is a named contractual upgrade tier.**

### Default: Option C — Zone-C Hybrid multi-tenant

Bridge ships as a single deployable system with two planes:

**Control plane** (shared across all tenants)
- Retains today's Bridge infrastructure: Aspire orchestration, Postgres, DAB, SignalR, Wolverine.
- Serves operator-owned functions only: signup, billing, subscription tier enforcement, admin backoffice, support tickets, system status.
- Does NOT hold any team data — just `{tenant_id, plan, billing, support_contacts, team_public_key}` records.
- `ITenantContext` still resolves tenants, but exclusively for control-plane concerns; it has no authority over team data.

**Data plane** (isolated per tenant)
- Each tenant gets a dedicated `apps/local-node-host` process.
- Each tenant gets a dedicated SQLCipher DB at a per-tenant path.
- Each tenant gets a subdomain: `acme.sunfish.example.com`, `globex.sunfish.example.com`, etc.
- Hosted-node process participates in the tenant's gossip scope as a ciphertext-only peer (paper §17.2); it holds the tenant's event-log ciphertext for catch-up-on-reconnect but cannot decrypt unless the tenant admin explicitly issues it a role attestation (trust-on-opt-in).
- Tenant admin decides at signup: **Relay-only** (operator sees ciphertext only — default), **Attested hosted peer** (operator can decrypt for backup verification / admin-assisted recovery — opt-in), or **No hosted peer** (self-hosted, Bridge provides only control-plane services).

**Relay tier** (shared, stateless)
- One relay process (can be scaled horizontally) accepts sync-daemon transport connections from all tenants.
- Relays team_id-scoped fan-out per Wave 4.2's `RelayServer`.
- Stateless — no persistence. A tenant's catch-up-on-reconnect traffic goes through the relay but gets persisted by that tenant's hosted-node peer, not the relay.

**Browser shell** (new)
- New Blazor Server app at each tenant's subdomain.
- User authenticates, browser fetches wrapped role-key bundle (see key-bootstrap decision below), decrypts role keys into memory, opens WebSocket to the tenant's hosted-node peer, reads/writes via CRDT ops decrypted in-browser.
- Session keys wiped on tab close / logout. No persistent browser local-node in v1.

### Contractual upgrade: Option B — dedicated deployment

Offered as a paid tier (enterprise plan). When a customer contracts for isolation:

- Bridge stack cloned to dedicated infra (own Aspire stack, own Postgres, own relay, own domain).
- Same codebase. The tenant's hosted-node process is the only thing that moves — from shared data-plane orchestration to a dedicated one.
- Same sync protocol; workstations running Anchor see no change.
- Migration between postures is non-disruptive because the paper's wire format is posture-agnostic.

### Key decisions embedded in this ADR

1. **Client-side: Anchor stays single-tenant-per-install for v1.** Multi-team Anchor (Slack-workspaces-style) is a v2 concern, tracked as a future ADR. Rationale: simpler UX, clearer compliance posture per install, matches how SMB customers think about "my company's software."
2. **Browser key bootstrap: passphrase-derived device key as the default flow**, with WebAuthn as an opt-in hardening option and QR-from-phone as the fallback for admin-initiated invites. Passphrase is friction-tolerable for SMB SaaS; WebAuthn is what enterprise buyers expect.
3. **Operator NOT in CP quorum by default** — paper §2.3 applies. Teams of fewer than 3 members either opt into attested-hosted-peer (operator becomes a quorum participant) or accept AP-mode downgrade per paper §2.3.
4. **Admin key recovery: hard mode — N-of-M admin attestations, no operator backdoor.** Defensible for v1; softer recovery paths can ship in a later ADR with explicit security-review sign-off.
5. **Cross-tenant collaboration: deferred.** Intra-tenant only in v1. Cross-tenant (via ADR 0029 federation or cross-team attestations) is a separate ADR.
6. **Single domain per tenant (`{tenant}.sunfish.example.com`) for the browser shell; operator admin at `admin.sunfish.example.com`.** Keeps the user's mental model clean: "my company's URL" vs. "vendor's admin URL."

---

## Consequences

### Positive

- Bridge has a single coherent identity: Zone-C Hybrid. No more "dual-posture" ambiguity from ADR 0026.
- Per-tenant data-plane isolation gives a defensible compliance story beyond "data is ciphertext."
- Shared control plane keeps operator cost flat as tenant count grows.
- Migration path to Option B (dedicated deployment) is a data-plane relocation, not a rewrite — customers can upgrade without protocol change.
- Anchor's role is clarified and unchanged — workstation install continues to be the Zone-A baseline.
- The paper's §17.2 ciphertext-at-rest invariant is preserved across all three deployment variants (Relay-only / Attested hosted peer / No hosted peer), and tenants choose their trust level at signup.
- Browser shell pattern (ephemeral in-memory node per session) is paper-compliant without requiring OPFS/PWA adoption from users.

### Negative

- Bridge's current SaaS-authority code (`DemoTenantContext`, EF query filters, Wolverine outbox driving authoritative state) must be reshaped: those components move to control-plane-only scope, and the data-plane replaces them with kernel-sync + kernel-crdt + kernel-security flows. Several weeks of focused refactor.
- Per-tenant `local-node-host` orchestration is new infrastructure work — Bridge needs to spawn, monitor, and health-check N worker processes instead of one. Kubernetes-shaped problem.
- Browser shell is a new product surface — design, build, test.
- Cross-tenant collaboration deferral may bite us if an early commercial customer has a "we need to share contracts with our client's team" requirement. Risk: named in open questions §5.
- Admin recovery "no operator backdoor" is defensible but will lose deals to competitors who offer easier recovery. Risk: accepted as v1 posture; revisit when named in a support-ticket case.

---

## Compatibility plan

### ADR 0026 is superseded by this ADR

- ADR 0026's "dual-posture" framing is replaced. Both of Bridge's modes (SaaS-facing hosted-node + managed relay) are now **paper-aligned** per §17.2; neither is "non-paper-aligned SaaS shell" anymore.
- Update ADR 0026 status to "Superseded by ADR 0031."
- Update `accelerators/bridge/README.md` + `PLATFORM_ALIGNMENT.md` to reflect Zone-C framing.

### Work sequencing (Wave 5, new)

This ADR opens Wave 5 of the paper-alignment plan:

1. **Wave 5.1 — Control-plane scope narrowing** (~1 week): refactor `ITenantContext`, `DemoTenantContext`, Bridge.Data/* to serve only `{tenant_id, plan, billing, team_public_key}` concerns. Delete authoritative-tenant-data code paths. Introduce a `TenantRegistration` entity + signup flow.
2. **Wave 5.2 — Per-tenant data-plane orchestration** (~2 weeks): Bridge.AppHost spawns one `local-node-host` process per tenant, wired to the tenant's SQLCipher DB. Monitoring + health checks. Graceful tenant lifecycle (create, pause, delete).
3. **Wave 5.3 — Browser shell v1** (~2-3 weeks): new Blazor Server app per tenant subdomain. Passphrase-derived device-key auth. WebSocket connection to per-tenant hosted-node process. Ephemeral in-memory node. First usable browser-accessible paper-aligned experience.
4. **Wave 5.4 — Founder + joiner flows via browser** (~1 week): adapt Anchor's QR onboarding (Wave 3.4) for browser-first signup. Operator-side = tenant record creation; tenant-side = founder bundle generation on first admin device.
5. **Wave 5.5 — Dedicated deployment packaging (Option B)** (~1 week): IaC templates (Bicep/Terraform/k8s manifests) for spinning up a dedicated Bridge per enterprise contract.

Total: ~7-8 weeks of engineering for Option C end-to-end usable.

### Anchor-side minor updates

- `accelerators/anchor/README.md` gains a "Paper §20.7 Zone A — Local-First Node" callout in the "Role in the architecture" section.
- No code changes.

---

## Implementation checklist

- [ ] Flip ADR 0026's status to "Superseded by ADR 0031."
- [ ] Update `accelerators/bridge/README.md` to describe Zone-C Hybrid model + Relay-only / Attested / No-hosted-peer trust levels.
- [ ] Update `accelerators/bridge/PLATFORM_ALIGNMENT.md` — replace "Posture A / Posture B" framing with "Control plane / Data plane / Relay tier."
- [ ] Add `_shared/product/paper-alignment-plan.md` Wave 5 section with 5.1 through 5.5 deliverables.
- [ ] Update `accelerators/anchor/README.md` with Zone-A callout.
- [ ] Update `CLAUDE.md` foundational-paper callout to mention the zone mapping (Anchor = Zone A, Bridge = Zone C).
- [ ] Open BDFL-sign-off tickets for the five deferred decisions:
  - [ ] Browser persistent storage (OPFS opt-in) posture for v2.
  - [ ] Cross-tenant collaboration mechanism (federation vs. extended attestation vs. shared sub-document).
  - [ ] Admin-recovery policy when a tenant loses all admin devices.
  - [ ] Multi-team Anchor (workspace-switcher) scope for v2.
  - [ ] Browser WebAuthn-only path for regulated-industry tier.

---

## References

- [Paper v12.0 §17.2](../../_shared/product/local-node-architecture-paper.md#172-managed-relay-as-sustainable-revenue) — hosted-relay-as-SaaS-node.
- [Paper v12.0 §20.7](../../_shared/product/local-node-architecture-paper.md#207-the-three-outcome-zones) — Zone A/B/C outcome framework.
- [ADR 0026](./0026-bridge-posture.md) — superseded by this ADR.
- [ADR 0029](./0029-federation-reconciliation.md) — federation-* packages for cross-tenant/cross-org sync (relevant when cross-tenant collaboration ships).
- [`accelerators/bridge/`](../../accelerators/bridge/) — the code this ADR reshapes.
- [`accelerators/anchor/`](../../accelerators/anchor/) — the Zone-A sibling (confirmed unchanged).
- [`apps/local-node-host/`](../../apps/local-node-host/) — the hosted-node process Bridge will orchestrate per-tenant.
- [`icm/07_review/output/paper-alignment-audit-2026-04-23-refresh.md`](../../icm/07_review/output/paper-alignment-audit-2026-04-23-refresh.md) — current state audit.

---

## Amendments (post-acceptance, 2026-05-01)

### A1 (PROPOSED) — Bridge → Anchor subscription-event-emitter contract

**Driver:** ADR 0062-A1.6 / A1.16 halt-condition. ADR 0062 (Mission Space Negotiation Protocol; landed post-A1 via PR #406) introduces an `EditionCapabilities` dimension consumed by feature gates to determine commercial-tier-gated availability. Post-A1.6, the cache TTL is **30 seconds** (operationally acceptable ceiling without push events) — but real billing-cycle UX expects sub-second reflection of upgrades. ADR 0062 Phase 1 substrate scaffold halts on either the 30-second TTL being operationally accepted OR ADR 0031 being amended to add a Bridge → Anchor subscription-event-emitter contract. **A1 is that contract.**

**Pipeline variant:** `sunfish-api-change` (introduces new Bridge → Anchor push-event surface).

**Companion intake:** [`icm/00_intake/output/2026-04-30_bridge-subscription-event-emitter-intake.md`](../../icm/00_intake/output/2026-04-30_bridge-subscription-event-emitter-intake.md) (PR #409; merged).

**Council review posture:** **Pre-merge canonical** per cohort discipline (substrate-tier surface introducing new Bridge accelerator contract; not a mechanical schema-extension; council-waiver does NOT apply per Decision Discipline Rule 3 boundaries).

#### A1.1 — Subscription-event types

Bridge emits 7 subscription-event types covering the canonical lifecycle of a Bridge-tenant subscription:

| Event type | Trigger | Payload-specific fields | Notes |
|---|---|---|---|
| `SubscriptionStarted` | Tenant first activates a subscribed Edition | `editionAfter` set; `editionBefore == null` | First-ever subscription |
| `SubscriptionRenewed` | Existing subscription auto-renews | `editionBefore == editionAfter` | Routine; less surface than other events |
| `SubscriptionCancelled` | Tenant cancels (effective at next billing-cycle boundary) | `editionBefore` set; `editionAfter == null` | Operator-initiated; future-dated effect |
| `SubscriptionTierUpgraded` | Tier upgrade (e.g., `anchor-self-host` → `bridge-pro`) | `editionBefore` + `editionAfter` set | Upgrade reflects sub-second per A1's purpose |
| `SubscriptionTierDowngraded` | Tier downgrade | `editionBefore` + `editionAfter` set | Downgrade may sequester features per ADR 0028-A8.3 |
| `SubscriptionDunning` | Payment failed; entering grace period | `editionBefore` set; `editionAfter` reflects grace-tier semantics | Warning posture; user UX should surface |
| `SubscriptionExpired` | Grace period exhausted; subscription lapsed | `editionBefore` set; `editionAfter == null` | Final state; differs from `SubscriptionCancelled` (operator-vs-payment-driven) |

Edition keys are strings per ADR 0009 (`IEditionResolver` returns `TenantId → edition key`); the event payload uses these keys verbatim.

#### A1.2 — Event payload + signature

Each subscription event is canonical-JSON-encoded per `Sunfish.Foundation.Crypto.CanonicalJson.Serialize`. Wire format (camelCase per ADR 0028-A7.8):

```json
{
  "tenantId": "tenant-...",
  "eventType": "SubscriptionTierUpgraded",
  "editionBefore": "anchor-self-host",
  "editionAfter": "bridge-pro",
  "effectiveAt": "2026-05-01T10:00:00Z",
  "eventId": "evt-7f9d2a...",
  "deliveryAttempt": 1,
  "signature": "hmac-sha256:..."
}
```

**Signature scheme:** HMAC-SHA256 over the canonical-JSON bytes (excluding the `signature` field itself). The shared secret is the per-Anchor webhook-registration token (per A1.4). Anchor verifies the signature on every received event; signature failures result in HTTP 401 + audit emission of `BridgeSubscriptionEventSignatureFailed`.

**Idempotency:** the `eventId` field is a globally-unique UUID per delivery + event tuple. Anchor MUST de-duplicate by `eventId` (per-tenant LRU cache; 24-hour retention). Re-delivery is a no-op.

**Replay-attack hardening:** `effectiveAt` is bounded — Anchor rejects events whose `effectiveAt` differs from receive-time by more than ±5 minutes (clock-skew tolerance per AWS Signature V4 + GitHub webhook conventions); rejection emits `BridgeSubscriptionEventStale` audit. Tunable per-deployment.

#### A1.3 — Delivery mechanism

A1 ships **webhook-primary delivery** (HTTPS POST to Anchor-provided URL) with **SSE fallback** for Anchor instances electing long-lived-connection delivery:

- **Webhook (primary):** Bridge POSTs canonical-JSON event to the Anchor's registered URL (per A1.4). 30-second timeout; non-200 → retry per A1.5.
- **SSE (alternate):** Anchor MAY register SSE mode via `X-Sunfish-Delivery-Mode: sse` header. Bridge maintains long-lived connection; events as `data:` frames with `event:` type. Reconnect on disconnect; backoff per A1.5.

Per ADR 0061 transport-tier model: Anchor instances behind symmetric NAT use webhook (Bridge pushes); Anchor instances with stable outbound use SSE. Vendor selection is per-deployment.

A1 ships in-process Bridge-side delivery (Bridge holds the queue + emits directly). Phase 2.3+ may add `providers-bridge-eventbus-aws-sqs` / `providers-bridge-eventbus-redis-streams` adapters per ADR 0013 provider-neutrality if Bridge load demands externalization.

#### A1.4 — Webhook URL registration

Anchor registers its webhook URL at install time:

```http
POST /api/v1/tenant/webhook
Authorization: Bearer <tenant-api-key>
Content-Type: application/json

{
  "callbackUrl": "https://anchor.example.com/sunfish/bridge-events",
  "deliveryMode": "webhook",
  "subscribedEvents": ["SubscriptionStarted", "SubscriptionTierUpgraded", "SubscriptionTierDowngraded"],
  "sharedSecret": "<base64-encoded-per-anchor-registration-token>"
}
```

`sharedSecret` is generated per-Anchor by Bridge at registration time (Bridge holds it server-side; Anchor stores it in its keystore per ADR 0046). Both sides use it for HMAC signing/verification per A1.2.

`subscribedEvents` is an opt-in filter; Anchor declares which events it cares about. Default: all 7. Re-registration replaces the prior URL/filter atomically.

Webhook URLs MUST be HTTPS (Bridge refuses HTTP at registration). The `callbackUrl` MUST resolve to a non-loopback address.

#### A1.5 — Retry + idempotency

**Retry policy (Bridge-side):** failed deliveries (HTTP non-200 OR timeout OR network error) retried with exponential backoff: 1s → 5s → 30s → 5min → 30min → 2h → 12h. After 7 attempts, the event moves to a dead-letter queue + emits `BridgeSubscriptionEventDeliveryFailedTerminal` audit.

**Idempotency (Anchor-side):** per A1.2, `eventId` is the de-duplication key. Anchor's `IBridgeSubscriptionEventHandler` (per A1.6) enforces idempotency via the per-tenant LRU cache.

**Re-delivery semantics:** Bridge increments `deliveryAttempt` on each retry; Anchor MAY use this for diagnostic surfaces. Re-delivery does NOT change the `effectiveAt` field.

#### A1.6 — Anchor-side handler substrate

```csharp
namespace Sunfish.Bridge.Subscription;

public interface IBridgeSubscriptionEventHandler
{
    /// <summary>
    /// Handles a Bridge subscription event. MUST be idempotent (per eventId).
    /// Returns the appropriate HTTP response code for Bridge's delivery acknowledgment.
    /// </summary>
    ValueTask<int> HandleAsync(
        BridgeSubscriptionEvent evt,
        CancellationToken ct = default
    );
}

public sealed record BridgeSubscriptionEvent(
    string         TenantId,
    string         EventType,                  // canonical event-type string per A1.1
    string?        EditionBefore,
    string?        EditionAfter,
    DateTimeOffset EffectiveAt,
    string         EventId,
    int            DeliveryAttempt,
    string         Signature
);
```

Default implementation:

1. Verify HMAC signature (per A1.2)
2. Check `eventId` in per-tenant LRU cache; return 200 immediately if dedup-hit
3. Update local `EditionCapabilities` cache via `IEditionResolver` (consumes `editionAfter`)
4. Emit `BridgeSubscriptionEventReceived` audit (per A1.7)
5. Trigger `EnvelopeChange` event via ADR 0062's `IMissionEnvelopeProvider` (the `EditionCapabilities` dimension transition propagates to UI gates)
6. Return 200

**Per ADR 0062-A1.6:** A1's contract is what closes the halt-condition. Once Anchor consumes `IBridgeSubscriptionEventHandler` events, `EditionCapabilities` flips sub-second instead of waiting for the 30-second cache TTL.

#### A1.7 — Audit emission

8 new `AuditEventType` constants (4 Bridge-side + 4 Anchor-side):

| Constant | Side | Trigger | Dedup window |
|---|---|---|---|
| `BridgeSubscriptionEventEmitted` | Bridge | Each emit attempt | None |
| `BridgeSubscriptionEventDelivered` | Bridge | Delivery succeeded (HTTP 200) | None |
| `BridgeSubscriptionEventDeliveryFailed` | Bridge | Retryable failure | 1-hour per `(tenant_id, event_id)` |
| `BridgeSubscriptionEventDeliveryFailedTerminal` | Bridge | All 7 retries exhausted | None (security-relevant) |
| `BridgeSubscriptionEventReceived` | Anchor | Successfully verified + processed | 24-hour per `(tenant_id, event_id)` |
| `BridgeSubscriptionEventSignatureFailed` | Anchor | HMAC verification failed | 1-hour per `(tenant_id, source_ip)` |
| `BridgeSubscriptionEventStale` | Anchor | `effectiveAt` outside ±5min clock-skew | 1-hour per `(tenant_id, event_type)` |
| `BridgeSubscriptionWebhookRegistered` | Bridge | Anchor registers webhook URL | None (security-relevant) |

#### A1.8 — Acceptance criteria

For A1-conformant implementation:

- [ ] Bridge emits 7 subscription event types per A1.1
- [ ] Each event canonical-JSON-encoded; HMAC-SHA256 signed; UUID `eventId`; ISO 8601 `effectiveAt`
- [ ] Webhook delivery (HTTPS POST; 30s timeout) + SSE fallback per A1.3
- [ ] Webhook URL registration via `POST /api/v1/tenant/webhook` per A1.4 (HTTPS-only; per-Anchor shared secret)
- [ ] Retry policy 1s/5s/30s/5min/30min/2h/12h (7 attempts; dead-letter after) per A1.5
- [ ] Anchor-side `IBridgeSubscriptionEventHandler` substrate per A1.6 (idempotent; HMAC verifies; ±5min clock-skew)
- [ ] 8 new `AuditEventType` constants per A1.7 + per-event dedup
- [ ] Test coverage: 7 event-type round-trips + HMAC verify success/fail + idempotency (same eventId twice) + replay-attack (>5min stale) + retry-policy timing + SSE fallback round-trip + audit dedup tests

#### A1.9 — Cited-symbol verification

**Existing on `origin/main`** (verified 2026-05-01):

- ADR 0062 (Mission Space Negotiation Protocol; post-A1) — provides `MissionEnvelope` + `EditionCapabilities` dimension + `IMissionEnvelopeProvider`
- ADR 0009 (Foundation.FeatureManagement; `IEditionResolver`) — verified Accepted
- ADR 0046 (Foundation.Recovery) — provides keystore for shared-secret storage
- ADR 0049 (audit substrate) — telemetry emission target
- ADR 0061 (three-tier peer transport) — transport-tier model rationale for webhook-vs-SSE
- ADR 0028-A7.8 — camelCase canonical encoding precedent
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` — verified existing per ADR 0028-A4 retraction
- HMAC-SHA256 — RFC 2104 IETF standard

**Introduced by A1** (not on `origin/main`; ship in implementation hand-off):

- 8 new `AuditEventType` constants per A1.7
- `Sunfish.Bridge.Subscription.IBridgeSubscriptionEventHandler` interface
- `Sunfish.Bridge.Subscription.BridgeSubscriptionEvent` record
- Bridge endpoint `POST /api/v1/tenant/webhook` + Bridge-side webhook delivery service (in-process queue + retry policy)
- Per-tenant LRU dedup cache for Anchor-side idempotency

**Cohort lesson reminder (per ADR 0028-A10 + ADR 0063-A1.15):** §A0 self-audit pattern is necessary but NOT sufficient. Council remains canonical defense. The 3-direction spot-check at draft time was applied.

#### A1.10 — Implementation hand-off

Stage 06 hand-off lands as a medium workstream (~6–10h per parent intake at PR #409). Recommend writing the hand-off file at `icm/_state/handoffs/bridge-subscription-event-emitter-stage06-handoff.md` after pre-merge council review completes.

5-phase split:
- **Phase 1:** substrate scaffold + 8 audit constants + `IBridgeSubscriptionEventHandler` substrate + `BridgeSubscriptionEvent` record
- **Phase 2:** Bridge-side event emission queue + HMAC signing + canonical-JSON encoding
- **Phase 3:** Webhook delivery (primary) + retry policy + dead-letter queue
- **Phase 4:** SSE delivery (alternate) + webhook URL registration endpoint
- **Phase 5:** Anchor-side handler default implementation + `IMissionEnvelopeProvider` integration + DI extension + apps/docs + ledger flip

#### A1.11 — Cohort discipline

Per `feedback_decision_discipline.md` cohort batting average (16-of-16 substrate amendments needing council fixes; structural-citation failure rate 11-of-16 ~69% XO-authored — A9's parent-propagation retracted via A10):

- **Pre-merge council CANONICAL** for A1. Auto-merge intentionally DISABLED until a Stage 1.5 council subagent reviews. Council should specifically pressure-test:
  - Webhook-vs-SSE delivery split — is "primary webhook + alternate SSE" right, or should A1 ship one mechanism?
  - 7-event taxonomy — any standard subscription lifecycle states missing? (`SubscriptionTrialStarted` / `SubscriptionTrialExpired` / `SubscriptionPaused`?)
  - HMAC-SHA256 vs Ed25519-signed events (would compose with ADR 0032's keypair)
  - Retry policy 1s/5s/30s/5min/30min/2h/12h × 7 attempts — covers realistic Anchor outages?
  - 30-second webhook timeout reasonable for HTTPS POST behind home NAT?
  - ±5min clock-skew tolerance — too tight for poor-time-sync Anchor instances?
  - 8 new `AuditEventType` constants — collision check
- **Cited-symbol verification** per A1.9 (3-direction spot-check at draft time)
- **Standing rung-6 spot-check** within 24h of A1 merging

**A1 closes ADR 0062-A1.6's halt-condition** (Phase 1 substrate scaffold of ADR 0062 may proceed once A1 lands + Anchor-side handler is wired).
