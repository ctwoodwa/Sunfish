# Intake — ADR 0031 Amendment A1: Bridge → Anchor Subscription-Event-Emitter Contract

**Date:** 2026-04-30
**Requestor:** XO (declared by ADR 0062-A1.6 / A1.16 as the sibling amendment unblocking sub-minute `EditionCapabilities` responsiveness in Mission Space Negotiation Protocol)
**Request:** Amend ADR 0031 (Bridge hybrid multi-tenant SaaS) with a new amendment A1 specifying a Bridge → Anchor subscription-event-emitter contract — the substrate by which Bridge pushes subscription state changes (start / renewal / cancellation / tier upgrade / tier downgrade / dunning state) to subscribed Anchor instances in real time, rather than Anchor having to poll Bridge.
**Pipeline variant:** `sunfish-api-change` (introduces new Bridge → Anchor push-event contract)
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

ADR 0062 (Mission Space Negotiation Protocol; council-fixed via A1) introduces an `EditionCapabilities` dimension consumed by feature gates to determine commercial-tier-gated availability. The probe-cost classification (per A1.6) places `EditionCapabilities` in the `High` cost class with a **30-second cache TTL** — chosen to bound user-facing UX surprise from billing-cycle transitions ("user pays, sees pay-to-upgrade upsell for up to 30 seconds while cache propagates").

A 30-second TTL is the **operational ceiling** when the substrate has no push-event signal. In a real billing system (per ADR 0009 Edition / IEditionResolver semantics), the user expects sub-second reflection of an upgrade — clicking "Upgrade to Bridge tier" and immediately seeing the gated feature available, NOT a 30-second wait for the next probe.

A1.6 acknowledges this gap explicitly via a halt-condition: ADR 0062 Phase 1 may NOT begin until either (a) the 30-second TTL is operationally accepted, or (b) ADR 0031 is amended to specify a Bridge → Anchor subscription-event-emitter contract. This intake files (b) as a candidate amendment.

## Predecessor

ADR 0031 (Bridge hybrid multi-tenant SaaS) — Accepted on `origin/main`. The ADR specifies the Bridge accelerator's hosted-node-as-SaaS pattern but does NOT specify a push-event contract for Bridge → Anchor. Bridge currently exposes:

- `GET /api/v1/tenant/edition` — Anchor polls for current Edition state
- `POST /api/v1/tenant/upgrade` — operator-initiated upgrade
- `POST /api/v1/tenant/cancel` — operator-initiated cancellation

There is no mechanism for Bridge to push state changes; ADR 0062's `EditionCapabilities` probe is restricted to the `GET` endpoint with the 30-second cache TTL.

**Why amendment, not new ADR:** subscription-event emission is intrinsic to the Bridge's hosted-SaaS posture; ADR 0031 already governs the Bridge surface. Adding a push contract is a natural extension, not a new architectural decision.

## Industry prior-art

- **Stripe webhooks** — canonical billing-event-emitter pattern; HTTPS POST to subscriber-provided URL with HMAC-signed payload + retry policy + idempotency keys
- **Square Webhooks** — similar shape; per-event-type subscription
- **WebPubSub / SSE** — push channel without per-event ACK
- **NATS / Redis Streams** — internal event-bus patterns; less applicable for cross-organization push

Stripe/Square webhooks are the closest engineering analog: cross-organization (Bridge tenant → Anchor instance, possibly behind NAT); HTTPS-based; well-trodden.

## Scope

- **Subscription-event types** — at minimum: `SubscriptionStarted`, `SubscriptionRenewed`, `SubscriptionCancelled`, `SubscriptionTierUpgraded`, `SubscriptionTierDowngraded`, `SubscriptionDunning`, `SubscriptionExpired`
- **Event payload shape** — `(tenant_id, event_type, edition_before, edition_after, effective_at, signature)` canonical JSON encoded; HMAC-signed per ADR 0009 capability semantics
- **Delivery mechanism** — webhook (HTTPS POST to Anchor-provided URL) vs SSE (Server-Sent Events; Anchor maintains long-lived connection) vs hybrid (SSE primary; webhook fallback)
- **Retry + idempotency** — failed deliveries retried with exponential backoff; idempotency keys prevent duplicate-event processing
- **Anchor-side handler** — `IBridgeSubscriptionEventHandler` substrate that consumes events; ADR 0062's `IDimensionProbe<EditionCapabilities>` consumes the handler's most-recent state
- **Authentication** — Anchor authenticates webhook deliveries via HMAC signature; Bridge authenticates Anchor's webhook URL registration via tenant API key
- **Webhook URL registration** — Anchor registers its webhook endpoint at install time; URL is per-Anchor (not per-feature)
- **Audit emission** — `BridgeSubscriptionEventDelivered` / `BridgeSubscriptionEventDeliveryFailed` per `Sunfish.Kernel.Audit.AuditEventType` patterns

## Dependencies and Constraints

- **Hard predecessor:** ADR 0031 itself (Bridge accelerator). Already on `origin/main`.
- **Soft predecessor:** ADR 0009 (Edition / IEditionResolver). Already on `origin/main`.
- **Hard consumer:** ADR 0062-A1.6 / A1.16 (Mission Space Negotiation Protocol; the halt-condition for Phase 1 substrate scaffold)
- **Effort estimate:** medium (~6–10h authoring + council review). The contract surface is well-defined; primary authoring work is choosing webhook vs SSE vs hybrid.
- **Council review posture:** pre-merge canonical per cohort discipline (13-of-13 substrate amendments needed council fixes; 6-of-13 had structural-citation failures caught pre-merge).

## Affected Areas

- accelerators/bridge: subscription-event-emitter contract + delivery mechanism
- foundation-integrations / providers-stripe (eventual): translate Stripe webhook events into Bridge subscription events
- accelerators/anchor: `IBridgeSubscriptionEventHandler` substrate + webhook endpoint registration
- packages/foundation-mission-space (post-ADR-0062 build): `IDimensionProbe<EditionCapabilities>` consumes the handler's state

## Downstream Consumers

- **ADR 0062 Phase 1 build** — unblocks sub-second `EditionCapabilities` responsiveness; eliminates the 30-second TTL operational ceiling
- **All commercial-tier-gated features** in Anchor — billing-cycle UX expectations met (instant upgrade reflection)
- **Phase 2 commercial MVP** — multi-tenant Bridge subscription transitions for BDFL's property business

## Next Steps

Promote to active workstream when CO confirms; XO authors the A1 amendment; pre-merge council; merge under the same auto-merge-disabled-then-re-enabled pattern as ADR 0028-A6+A7 + A5+A8 + ADR 0062+A1. Recommend authoring **after** ADR 0062 lands (currently auto-merge-enabled on PR #406); ADR 0031-A1 does not block ADR 0062 Phase 0 / planning but unblocks Phase 1 substrate scaffold's `EditionCapabilities` responsiveness story.

## Cross-references

- Parent: ADR 0062-A1.6 / A1.16 (declaration; halt-condition for ADR 0062 Phase 1)
- Parent ADR: ADR 0031 (Bridge hybrid multi-tenant SaaS)
- Council review: `icm/07_review/output/adr-audits/0062-council-review-2026-04-30.md` (F6 / F8 council recommendations A6 / A8 drove A1.6 / A1.8 which in turn declared this intake)
- Sibling: ADR 0009 (Edition / IEditionResolver) — the upstream contract `EditionCapabilities` consumes
