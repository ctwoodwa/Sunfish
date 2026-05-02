---
id: 52
title: Bidirectional Messaging Substrate
status: Accepted
date: 2026-04-28
tier: foundation
concern:
  - distribution
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0052 — Bidirectional Messaging Substrate

**Status:** Accepted (2026-04-29 by CO; council-reviewed B+-grade; amendments A1–A5 + Minor **landed 2026-04-29** — see §"Amendments (post-acceptance, 2026-04-29)")
**Date:** 2026-04-28 (Proposed) / 2026-04-29 (Accepted) / 2026-04-29 (A1–A5 + Minor landed)
**Council review:** [`0052-council-review-2026-04-29.md`](../../icm/07_review/output/adr-audits/0052-council-review-2026-04-29.md) — Accept with amendments. All addressed below:

- **A1** (Critical) — Public-webhook ingress threat tier delegated to ADR 0043's T2-MSG-INGRESS catalog entry; substrate provides 5-layer defense-in-depth (provider sig verify + sender allow-list + per-tenant rate limit + content-scoring hook + manual unrouted-triage). Public-listings surface consumes via `IInboundMessageScorer`, not via a parallel boundary abstraction.
- **A2** (Critical) — `ThreadToken` cryptographic mechanism specified: HMAC-SHA256 over `{tenant, thread, notBefore}` with per-tenant key from `Sunfish.Foundation.Recovery.ITenantKeyProvider`, 90-day TTL, base32 encoding (34-char total), append-only revocation log, key-rotation cascade with 7-day grace window.
- **A3** (Major) — Shared-sender-domain (`messages.bridge.sunfish.dev`) reputation-contagion kill-trigger named (Phase 2.3 slip past 2026-Q3); per-tenant escape hatch via Postmark Message Streams or AWS SES Configuration Sets; new `MessagingProviderConfig.SenderIsolationMode` enum.
- **A4** (Major) — SMS thread-resolution reframed: fuzzy sender-recency matching (14-day window) is primary; thread token is best-effort tiebreaker; default `SmsThreadTokenStrategy = OmitToken` for Phase 2.1.
- **A5** (Major) — Measurable success criteria added: unrouted-triage rate ≤ 5%, outbound delivery ≥ 99%, parity-test byte-diff exclusion list (provider-tracking + provider-`X-` headers), token verify < 5ms p95 at 100 RPS, signature-verify failure < 0.1%, stuck-state alerting > 24h.
- **Minor** — `MessageVisibility.PartyPrivate` removed (was 4 values, now 3: `Public | PartyPair | OperatorOnly`); thread visibility enforced via participant-set membership; new `IThreadStore.SplitAsync` for legitimate "private aside" use case.
**Resolves:** Phase 2 commercial intake placeholder for ADR 0052 (originally scoped as "outbound messaging contracts"); messaging-substrate intake [`property-messaging-substrate-intake-2026-04-28.md`](../../icm/00_intake/output/property-messaging-substrate-intake-2026-04-28.md); cluster workstream #20.

---

## Context

The Phase 2 commercial intake (`phase-2-commercial-mvp-intake-2026-04-27.md`) reserves ADR 0052 for *"outbound-messaging contracts (extension to `Foundation.Integrations`; today's contracts are inbound-webhook-only)"* — templated email + (later) SMS for invoices, statements, and reminders. The placeholder anticipated egress as the dominant concern.

The property-operations cluster surfaced in the multi-turn architectural conversation 2026-04-28 (turns 6 + 7) made that scope wrong. Three independent forcing functions emerged within the same week:

1. **Vendor coordination requires multi-party threads.** A single work order generates 20+ messages spanning owner ↔ vendor ↔ tenant over two weeks. Messages must land on the right thread, not in a generic inbox; some messages are owner-private, some shared with all participants. Without ingress + threading, vendor coordination is a one-way megaphone, not a workflow.
2. **Leasing pipeline requires inbound applications.** Public listings emit pre-screening criteria documents; prospects reply with applications (often via email, increasingly via SMS for the application-status follow-up); replies must route to the matching `LeasingApplication` thread or the Fair Housing audit trail can't reconstruct who saw which criteria when.
3. **Right-of-entry compliance requires audit-logged outbound notices with signed acknowledgements.** Tenants confirm receipt; confirmation flows back as ingress; without symmetric egress + ingress with shared audit substrate, the compliance trail is incomplete.

Concurrent with this scope expansion, ADR 0013's provider-neutrality enforcement gate landed (PR #196, workstream #14, merged 2026-04-28 14:35Z). The Roslyn analyzer + `BannedSymbols.txt` are now active; vendor SDK references in `blocks-*` and `foundation-*` packages fail the build. Messaging is the **first major exercise of the providers-* pattern** post-enforcement-gate — the substrate this ADR specifies will validate the architecture in flight.

The decision Phase 2 commercial intake deferred is now load-bearing for the property-operations cluster (5 sibling intakes consume this substrate). Resolving it as bidirectional now is cheaper than splitting into outbound-now + inbound-later: egress and ingress share the same provider adapters, the same audit substrate, and the same per-tenant configuration — splitting them obscures that.

---

## Decision drivers

- **Property-ops cluster has 5+ direct consumers** of bidirectional messaging: Vendors (#18), Work Orders (#19), Leasing Pipeline (#22), Public Listings (#28), Phase 2 commercial outbound statements (#5). All five depend on this ADR landing before their own Stage 02 design.
- **ADR 0013 enforcement gate now active.** Vendor SDK isolation is mechanically enforced (`SUNFISH_PROVNEUT_001` + `RS0030`). Any messaging adapter must comply; there is no provider-coupled escape hatch.
- **Phase 2 commercial intake's monthly statement job already needs egress.** That deliverable cannot ship without ADR 0052 in some form.
- **Audit-trail integration is non-negotiable.** Right-of-entry notices, criteria-sent events, vendor entry confirmations, application receipts — all are first-class audit records per ADR 0049 (substrate accepted; PR #190 merged). The messaging substrate must emit to that substrate, not invent its own.
- **Multi-tenancy isolation is non-negotiable.** Each Sunfish tenant (each property LLC, in BDFL Phase 2 scope) configures its own provider adapters and credentials. ADR 0008 multi-tenancy applies.
- **No cross-channel real-time chat.** This ADR is about durable, audit-logged email + SMS — not in-app chat or WebRTC. That keeps scope tractable; chat-class surfaces are Phase 3+.

---

## Considered options

### Option A — Stay outbound-only; build ingress later as ADR 0053

Ship ADR 0052 as the original Phase 2 commercial intake scope (egress only). Defer ingress to a follow-up ADR drafted when the first cluster intake hits Stage 02 and surfaces the gap.

- **Pro:** Smaller initial scope; lower draft cost.
- **Pro:** Doesn't force ingress design before consumers are concrete.
- **Con:** Five cluster intakes block on it; the ingress design has to land before any of them advance to Stage 02 anyway.
- **Con:** Splits provider adapter design — Postmark egress and Postmark Inbound are the same vendor; they share credentials, signing, and rate-limiting concerns. Two ADRs encode them as if they're independent.
- **Con:** Audit-substrate integration ends up in two ADRs that have to stay in sync.

**Verdict:** Rejected. Defers a known requirement and creates two ADRs that must stay in sync.

### Option B — Reframe ADR 0052 as bidirectional substrate now [RECOMMENDED]

Ship ADR 0052 as a single bidirectional substrate: egress contracts + ingress contracts + thread/message/visibility entity model + audit-substrate integration + per-tenant provider config. Single ADR, single set of provider adapters per vendor, single migration path for any code already written against the original placeholder.

- **Pro:** One coherent contract surface for all 5+ cluster consumers + Phase 2 commercial intake.
- **Pro:** Provider adapter packages encapsulate the vendor relationship symmetrically (same `providers-email-postmark` does outbound + inbound).
- **Pro:** Audit-substrate integration in one place.
- **Pro:** Pre-acceptance content for the next 5 cluster ADRs (signatures, work-order, leasing pipeline, public listings, vendor onboarding) all reference the same substrate.
- **Con:** Larger initial draft; more design surface.
- **Con:** A consumer that only needs egress (Phase 2 statements) gets ingress contracts they don't immediately use.

**Verdict:** Recommended. Larger up-front cost, but the alternative imposes that cost twice and creates a sync hazard.

### Option C — Split into ADR 0052 (egress) + ADR 0053 (ingress) + ADR 0054 (thread model)

Three smaller ADRs covering the same total surface, drafted concurrently or sequentially.

- **Pro:** Each ADR is small and easy to review.
- **Pro:** Egress could ship before ingress without blocking statement-email work.
- **Con:** Three ADRs that must reference each other's contracts, evolve together, and stay consistent.
- **Con:** Vendor adapter symmetry (Postmark egress + Postmark Inbound) gets split across three ADRs' implementation checklists.
- **Con:** Council review burden multiplies by three.

**Verdict:** Rejected. Symmetry that's real in the code is fragmented in the ADR record.

---

## Decision

**Adopt Option B.** Reframe ADR 0052 from outbound-only to **bidirectional messaging substrate**: egress + ingress + thread model + audit-substrate integration, in a single ADR with a single contract surface, multiple provider adapter packages, and per-tenant configuration.

### Initial contract surface

```csharp
// Egress
namespace Sunfish.Foundation.Integrations.Messaging;

public interface IOutboundMessageGateway
{
    Task<OutboundDispatchHandle> DispatchAsync(
        OutboundMessage message,
        CancellationToken ct);

    Task<OutboundDispatchStatus> GetStatusAsync(
        OutboundDispatchHandle handle,
        CancellationToken ct);
}

public sealed record OutboundMessage
{
    public required TenantId Tenant { get; init; }
    public required ThreadId Thread { get; init; }      // routes inbound replies
    public required MessageChannel Channel { get; init; } // Email | Sms
    public required IReadOnlyList<MessageRecipient> Recipients { get; init; }
    public required MessageContent Content { get; init; } // template-rendered + content-hash
    public required IReadOnlyList<MessageAttachment> Attachments { get; init; }
    public required AuditCorrelation Audit { get; init; } // ADR 0049 attribution
}

public enum MessageChannel { Email, Sms }

public sealed record OutboundDispatchHandle(string ProviderKey, string ProviderMessageId);

public enum OutboundDispatchStatus
{
    Queued, Sent, Delivered, Bounced, Complained, Opened, Clicked, Failed
}

// Ingress
public interface IInboundMessageReceiver
{
    Task<InboundReceiveOutcome> ReceiveAsync(
        InboundEnvelope envelope,
        CancellationToken ct);
}

public sealed record InboundEnvelope
{
    public required string ProviderKey { get; init; }
    public required MessageChannel Channel { get; init; }
    public required IReadOnlyDictionary<string,string> ProviderHeaders { get; init; }
    public required ReadOnlyMemory<byte> RawBody { get; init; }       // for audit + signature verify
    public required string? ParsedThreadIdToken { get; init; }         // extracted; null = unrouted
    public required InboundParty Sender { get; init; }
    public required IReadOnlyList<MessageAttachment> Attachments { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
}

public enum InboundReceiveOutcome
{
    AppendedToThread,         // matched + appended
    QueuedForUnroutedTriage,  // thread token missing or unparseable
    RejectedAsAbuse,          // rate limit / sender block / signature fail
    Duplicate                 // idempotency dedup
}

// Thread model
public readonly record struct ThreadId(Guid Value);

public sealed record Thread
{
    public required ThreadId Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required ThreadScope Scope { get; init; } // WorkOrder | LeasingApplication | LeaseRelation | TenantRelation | VendorRelation | …
    public required IReadOnlyList<ThreadParticipant> Participants { get; init; }
    public required ThreadVisibility Visibility { get; init; }
    public required DateTimeOffset OpenedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
}

public sealed record ThreadParticipant
{
    public required IdentityRef Identity { get; init; }      // kernel identity OR external party (vendor, leaseholder, prospect)
    public required ThreadRole Role { get; init; }            // Owner | Vendor | Tenant | Bookkeeper | TaxAdvisor | Prospect | Applicant | …
    public required ThreadVisibility EffectiveVisibility { get; init; } // computed from Role + capability graph
}

public sealed record Message
{
    public required Guid Id { get; init; }
    public required ThreadId Thread { get; init; }
    public required MessageDirection Direction { get; init; } // Egress | Ingress
    public required MessageChannel Channel { get; init; }
    public required IdentityRef Sender { get; init; }
    public required IReadOnlyList<IdentityRef> Recipients { get; init; }
    public required MessageContent Content { get; init; }
    public required IReadOnlyList<MessageAttachment> Attachments { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DeliveredAt { get; init; }
    public required MessageVisibility Visibility { get; init; } // shared | partyPrivate
}
```

(Schema sketch only; XML doc + nullability annotations + `required` enforcement are mandatory at Stage 06.)

### Provider adapter pattern (mandatory per ADR 0013 enforcement gate)

```
blocks-messaging
  ↓ depends on (contracts only)
Foundation.Integrations.Messaging
  ↑ implemented by
providers-email-postmark
providers-email-sendgrid          (parity-test pair with Postmark)
providers-email-ses               (cost-optimized alternative)
providers-sms-twilio
providers-email-inbound-postmark  (separate package; same vendor relationship as outbound but different webhook surface)
providers-sms-inbound-twilio
  ↓ uses
Vendor SDK (Postmark.NET, SendGrid.Helpers, AWSSDK.SimpleEmailV2, Twilio.NET)
```

`blocks-messaging` references no vendor SDK directly. The Roslyn analyzer (`SUNFISH_PROVNEUT_001`) enforces this mechanically. The exclusion list (`Sunfish.Foundation.Integrations` + test projects) does NOT include `blocks-messaging` — vendor leakage there is a build error.

### Threading semantics

Inbound parsing extracts a `ParsedThreadIdToken` from one of:

- **Email** — `Reply-To` header in outbound message contains opaque token (`thread+<base32>@messages.{tenant-domain}`); same token round-trips on reply
- **SMS** — first line of outbound contains a reserved token (`[Ref: <base32>]`); replies preserve it ~80% of the time, fall through to fuzzy sender-recency matching otherwise

Token format is opaque to consumers; `Foundation.Integrations.Messaging` owns generation + parsing. Tokens are tenant-scoped (a leaked token from tenant A can't address tenant B's threads).

### Visibility model

Three visibility primitives compose to handle every cluster requirement:

- **Thread-level visibility** — every thread has a default visibility set (the participant list). Drives "who can read this thread."
- **Message-level visibility override** — a message can narrow visibility below thread default ("owner-vendor private aside on a shared work-order thread"). Implementation recommendation: model owner-vendor private threads as their *own* dedicated `VendorRelation`-scoped thread rather than a per-message override; cleaner audit trail.
- **Capability-driven projection** — the actual visible message set per principal per moment is computed via macaroon capabilities (ADR 0032 / `Foundation.Macaroons`), not by static ACLs. A bookkeeper whose capability is revoked stops seeing past messages without rewriting history.

### Per-tenant gateway configuration

Each tenant declares its own provider adapter set:

```yaml
# Per-tenant config (encrypted at rest under tenant key)
messaging:
  egress:
    email: providers-email-postmark
    sms:   providers-sms-twilio
  ingress:
    email: providers-email-inbound-postmark
    sms:   providers-sms-inbound-twilio
  fallback_unrouted_inbox: true
  abuse_posture:
    email_inbound_rate_limit_per_sender_per_hour: 30
    sms_inbound_rate_limit_per_sender_per_hour: 60
    captcha_required_for_anonymous_inquiry: true
```

Per-tenant credential resolution uses the same `CredentialsReference` shape from ADR 0013; concrete secret resolution is the secrets-management adapter's concern (separate ADR; not blocking this one).

### Audit-substrate integration (ADR 0049)

Every egress + ingress event emits a typed audit record:

| Audit record type | Emitted on |
|---|---|
| `MessageDispatched` | Successful egress dispatch |
| `MessageDeliveryStatusChanged` | Provider webhook reports delivery state change |
| `MessageReceived` | Successful ingress envelope persisted |
| `MessageRoutedToThread` | Ingress matched + appended |
| `MessageQueuedForUnroutedTriage` | Ingress could not match a thread |
| `MessageRejectedAsAbuse` | Rate limit / signature fail / sender block |
| `ThreadOpened` / `ThreadClosed` | Thread lifecycle |
| `ThreadParticipantAdded` / `Removed` | Participant change |

Audit records carry redacted projections of sensitive content (PII): subject + sender + recipient + dispatch metadata, **not** message body. Body is stored in `Foundation.Integrations.Messaging`'s persistence under tenant-key encryption; audit substrate stores only the reference.

### What this ADR does NOT do

- Does **not** define the secrets-management adapter that resolves `CredentialsReference` to plaintext API keys (per ADR 0013; deferred to its follow-up ADR).
- Does **not** define inbound abuse heuristics beyond the substrate hooks (rate-limit, signature, sender-block). Per-vendor abuse posture lives in adapter config.
- Does **not** define WhatsApp / iMessage Business / Signal channels (Phase 4+).
- Does **not** define real-time / chat-class surfaces (in-app push, WebSocket, presence). Messaging here is durable, audit-logged, eventual-delivery.
- Does **not** define DKIM / SPF / DMARC posture (per-tenant custom domain config; Phase 2.3 enhancement; shared sender domain `messages.bridge.sunfish.dev` ships in Phase 2.1).
- Does **not** define rate-limiting / retry / circuit-breaker policy beyond hooks (per ADR 0013 — adapters own this via `Microsoft.Extensions.Http.Resilience`).

---

## Consequences

### Positive

- Single substrate replaces N ad-hoc messaging integrations; provider adapters are swappable by redeploy.
- Egress + ingress symmetry per vendor is preserved (Postmark + Postmark Inbound live in one mental model).
- Thread model + visibility model is consistent across vendor coordination, leasing pipeline, work orders, and Phase 2 commercial outbound.
- ADR 0049 audit-substrate integration is one place, one set of audit record types, consistent vocabulary.
- Five cluster intakes (Vendors, Work Orders, Leasing Pipeline, Public Listings, Owner Cockpit) and the Phase 2 commercial outbound statement all unblock simultaneously on this ADR's acceptance.
- Provider-neutrality enforcement gate (workstream #14) gets its first major real-world test; the design pressure validates the architecture before more cluster ADRs lean on it.
- Multi-tenant isolation is structural (per-tenant gateway config + per-tenant token namespace) not bolted on.

### Negative

- Larger initial draft and review surface than the original Phase 2 placeholder anticipated.
- Consumers that only need egress (Phase 2 monthly statements) inherit ingress contracts they don't immediately use. (Mitigation: ingress contracts are in the same package; unused interfaces are zero runtime cost.)
- Inbound parsing has a long tail of vendor-specific webhook quirks (Postmark vs SendGrid Inbound vs SES Inbound have different envelope formats); abstracting them is real adapter work.
- Per-tenant DKIM / custom-domain config is explicitly deferred; tenants share `messages.bridge.sunfish.dev` deliverability in Phase 2.1, which has reputation implications across tenants.
- Three visibility primitives composing is one more concept than "an ACL on the thread." If consumers misuse the message-level override pattern, audit visibility becomes hard to reason about (recommendation: prefer dedicated party-pair threads).

### Trust impact / Security & privacy

- **Inbound is a public-internet boundary.** Anonymous senders can submit envelopes via webhook endpoints. ADR 0043 addendum (covered in [`property-leasing-pipeline-intake-2026-04-28.md`](../../icm/00_intake/output/property-leasing-pipeline-intake-2026-04-28.md)) and the public-listings ADR formalize the trust model. Substrate hooks (rate limit, signature verification, sender block) are mandatory; vendor abuse posture is opaque-on-purpose (Postmark and Twilio have provider-specific signing schemes).
- **Message body is sensitive PII.** Body is encrypted at rest under tenant key; audit substrate stores only the redacted projection (sender, recipient, metadata).
- **Token leakage.** Thread tokens are tenant-scoped and rotate per thread; an exfiltrated token from a closed thread is a closed-thread risk (read of historical messages by holder of token, gated by capability). Recommendation: token TTL + binding to participant identity at parse time.
- **TCPA + CAN-SPAM compliance.** Per-recipient consent flag with audit-trail (when consent granted, when revoked, by what surface). Substrate provides hooks; consent UX is downstream (cluster intakes).

---

## Compatibility plan

### Existing callers / consumers

The original ADR 0052 placeholder in Phase 2 commercial intake is referenced but not yet implemented in code. No production code targets ADR 0052 today; this reframe is non-breaking to the codebase.

The following intake-level references update from "outbound messaging" to "bidirectional messaging substrate":

- `phase-2-commercial-mvp-intake-2026-04-27.md` — line referencing ADR 0052 as outbound-only updates to point at this reframed ADR (one-line edit; chore-class follow-up commit)
- All cluster intakes already reference the bidirectional substrate by intent

### Affected packages (new + modified)

| Package | Change |
|---|---|
| `packages/foundation-integrations` (existing) | **Modified** — adds `Sunfish.Foundation.Integrations.Messaging` namespace with the contracts above; references `Sunfish.Kernel.Audit` for audit emission |
| `packages/blocks-messaging` (new) | **Created** — entity model (Thread, Message, ThreadParticipant, ThreadVisibilityRule), persistence, in-process default `IOutboundMessageGateway` + `IInboundMessageReceiver` |
| `packages/providers-email-postmark` (new) | **Created** — outbound adapter |
| `packages/providers-email-sendgrid` (new) | **Created** — outbound adapter (parity-test pair with Postmark) |
| `packages/providers-email-inbound-postmark` (new) | **Created** — inbound webhook handler + envelope normalizer |
| `packages/providers-sms-twilio` (new) | **Created** — outbound + inbound (Twilio's reply webhook is part of the same vendor surface) |
| `accelerators/bridge` (existing) | **Modified** — webhook receiver endpoints + per-tenant gateway config UI + unrouted inbox triage view |

### Migration

No migration needed for production code (no consumers exist). Migration *guide* required for Phase 2 commercial intake's outbound-statement deliverable to point at `IOutboundMessageGateway` rather than the original placeholder.

---

## Implementation checklist

- [ ] `Sunfish.Foundation.Integrations.Messaging` namespace added with contracts above; full XML doc + nullability + `required` annotations
- [ ] `OutboundMessage`, `InboundEnvelope`, `Thread`, `Message`, `ThreadParticipant`, `ThreadVisibility` records defined (sealed; init-only; required-property enforced)
- [ ] `IOutboundMessageGateway` + `IInboundMessageReceiver` interfaces with full XML doc
- [ ] Audit record types added to `Sunfish.Kernel.Audit` per ADR 0049 (`MessageDispatched`, `MessageReceived`, etc.)
- [ ] In-memory reference `IOutboundMessageGateway` + `IInboundMessageReceiver` shipped in `foundation-integrations` for tests/demos
- [ ] `packages/blocks-messaging` scaffolded with entity registration per ADR 0015 (`ISunfishEntityModule`)
- [ ] `packages/providers-email-postmark` scaffolded; outbound dispatch + delivery webhook handling working against Postmark sandbox
- [ ] `packages/providers-email-sendgrid` scaffolded; adapter parity test pair vs Postmark (same outbound message → byte-equivalent rendered output excluding provider-specific headers)
- [ ] `packages/providers-email-inbound-postmark` scaffolded; webhook signature verify + envelope normalization + thread-token extraction
- [ ] `packages/providers-sms-twilio` scaffolded; outbound + inbound MMS attachment routing
- [ ] Provider-neutrality analyzer (`SUNFISH_PROVNEUT_001`) passes on `blocks-messaging` (no vendor SDK references; build fails if violated — gate already active per workstream #14)
- [ ] Bridge webhook receiver endpoints with per-provider signature verification + rate-limit + abuse posture
- [ ] Bridge unrouted-inbox triage view (Blazor + React adapter parity per ADR 0014)
- [ ] Per-tenant gateway config storage with encryption-at-rest under tenant key (uses Foundation.Recovery primitives once workstream #15 lands)
- [ ] Token format finalized: opaque base32, tenant-scoped, TTL'd, revocable
- [ ] Visibility model: thread-level + capability-driven projection composed via `Foundation.Macaroons`; message-level override discouraged (use party-pair threads)
- [ ] kitchen-sink demo: send vendor work-order assignment → vendor replies via email → reply lands on the work-order thread → tenant receives notification → tenant confirms → all three audit-logged
- [ ] apps/docs entry covering substrate + provider selection guidance + visibility model
- [ ] Phase 2 commercial intake updated to reference reframed ADR 0052 (one-line edit; chore-class follow-up commit)

---

## Open questions

These are the OQ-M1 through OQ-M8 from the messaging-substrate intake, surfaced here as ADR open questions so they can become follow-up amendments or separate intakes if they harden into real constraints.

| ID | Question | Resolution path |
|---|---|---|
| OQ-M1 | Inbound parsing service: Mailgun vs Postmark Inbound vs SES Inbound per email vendor. | Stage 02 design — recommend Postmark Inbound as Phase 2.1 default (cleanest API); SES Inbound as cost-optimized alternative; Mailgun deferred. |
| OQ-M2 | SMS inbound thread-token abandonment: when reply doesn't carry the token. | Stage 02 — fall-through to fuzzy sender-recency matching; if no match, queue for unrouted triage. |
| OQ-M3 | Per-tenant secret store: where do per-tenant Postmark / Twilio API keys live? | Stage 03 — recommend per-tenant secret store reusing Foundation.Recovery primitives (workstream #15) for encryption-at-rest. |
| OQ-M4 | Visibility rule expression: declarative ACL vs predicate vs capability-based. | Stage 02 — capability-based via `Foundation.Macaroons` (aligned with ADR 0032). |
| OQ-M5 | Message-level visibility override implementation. | Stage 02 — recommend dedicated party-pair `VendorRelation` thread instead of per-message override. |
| OQ-M6 | Inbound attachment scanning: where in the pipeline? | Stage 02 — Bridge edge (before persistence); reject early; size cap + content-type allowlist. |
| OQ-M7 | TCPA / CAN-SPAM consent surface. | Stage 02 — per-recipient consent flag with audit trail; consent UX is downstream (Owner Cockpit + cluster intakes). |
| OQ-M8 | DKIM / SPF / DMARC: per-tenant custom domain. | Stage 02 — Phase 2.1 ships shared `messages.bridge.sunfish.dev`; per-tenant custom domain Phase 2.3. |

---

## Revisit triggers

This ADR should be re-evaluated when any of the following fire:

- **A regulated vertical onboards** (HIPAA-class healthcare, FINRA-class financial, COPPA-class child data) and the substrate's PII redaction posture is insufficient for the new compliance regime.
- **A real-time chat-class surface is required** (in-app messaging with presence, typing indicators, sub-second delivery). This ADR is for durable async messaging; chat needs its own substrate.
- **Cross-region data residency** becomes a customer requirement (EU-only data, sovereign cloud). Per-region gateway config and per-region audit substrate may force a rethink.
- **A non-email/SMS channel** crosses the in-scope threshold (WhatsApp Business, iMessage Business, Signal). The substrate is designed channel-agnostic but real channels will pressure-test the abstraction.
- **The provider-neutrality enforcement gate is amended** — if ADR 0013 changes its banned-namespace policy or its exclusion list, this ADR's adapter pattern may need to change in step.
- **The first three messaging adapters reveal a structural commonality** the contracts don't capture (e.g., a uniform retry-budget concept; a uniform tracing-correlation concept). Promote that to Foundation.Integrations.Messaging.
- **A messaging-substrate-driven incident** (deliverability collapse, vendor outage, abuse incident, audit-trail gap) reveals a missing primitive.

---

## References

### Predecessor and sister ADRs

- [ADR 0008](./0008-foundation-multitenancy.md) — Multi-tenancy. Per-tenant gateway config + per-tenant token scope reference this.
- [ADR 0013](./0013-foundation-integrations.md) — Provider-neutrality. This ADR is the first major exercise of the providers-* pattern; the enforcement gate (workstream #14, PR #196) is mechanically active for `blocks-messaging`.
- [ADR 0015](./0015-module-entity-registration.md) — `ISunfishEntityModule`. `blocks-messaging` registers per this pattern.
- [ADR 0028](./0028-crdt-engine-selection.md) — CRDT engine. Threads are append-only event-sourced (AP-class); message order is causal-via-timestamp.
- [ADR 0032](./0032-multi-team-anchor-workspace-switching.md) — Per-tenant capability + macaroon model that drives visibility projection.
- [ADR 0043](./0043-unified-threat-model-public-oss-chain-of-permissiveness.md) — Threat model. The public-input-boundary addendum (driven by Public Listings + Inquiry intake) intersects this ADR's ingress posture.
- [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — Key-loss recovery. Per-tenant credential encryption uses Foundation.Recovery primitives once workstream #15 lands.
- [ADR 0049](./0049-audit-trail-substrate.md) — Audit substrate. Every egress + ingress event emits typed audit records per the table above.

### Roadmap and specifications

- Paper §6.2 — Sync daemon protocol. (Messaging is *not* sync; it's adjacent. Worth noting that messaging substrate runs over the same Bridge HTTP transport, not over the sync daemon.)
- Paper §17.2 — Hosted relay as managed-SaaS deployment. Bridge's webhook receiver endpoints are part of the relay surface.
- [Phase 2 commercial intake](../../icm/00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md) — original ADR 0052 placeholder.
- [Messaging substrate intake](../../icm/00_intake/output/property-messaging-substrate-intake-2026-04-28.md) — Stage 00 spec source for this ADR.
- [Property-ops cluster INDEX](../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md) — pins ADR drafting order.

### Existing code / substrates

- `packages/foundation-integrations/` — existing package this ADR extends with the `Messaging` namespace.
- `packages/kernel-audit/` — existing audit substrate this ADR emits to (PR #190 + #198 merged).
- `packages/analyzers/provider-neutrality/` — enforcement gate this ADR's adapter pattern must satisfy (PR #196 merged).
- `packages/foundation-multitenancy/` — `TenantId` source.
- `packages/foundation-macaroons/` — capability-driven visibility projection source.

### External

- [Postmark Inbound webhook reference](https://postmarkapp.com/developer/user-guide/inbound) — first inbound provider target.
- [Twilio Programmable Messaging webhook reference](https://www.twilio.com/docs/messaging/guides/webhook-request) — first SMS provider target.
- TCPA (Telephone Consumer Protection Act) — outbound SMS consent compliance.
- CAN-SPAM Act — outbound email consent + identification requirements.
- E-SIGN / UETA — adjacent (signed message acknowledgements use ADR 005X signature ADR; not this ADR's concern).

---

## Amendments (post-acceptance, 2026-04-29)

The council review ([`0052-council-review-2026-04-29.md`](../../icm/07_review/output/adr-audits/0052-council-review-2026-04-29.md)) graded the ADR B+ on the UPF rubric and identified 5 amendments — 2 Critical (block cluster Stage 02), 3 Major (parallel with cluster work), plus a Minor `MessageVisibility.PartyPrivate` removal. The CO accepted with amendments; this section authors them. After A1–A5 + the Minor land, the rubric grade lifts to **A** on re-review.

### A1 — Public-webhook ingress threat tier delegated to ADR 0043 (Critical; resolves Risk 2)

ADR 0043's threat-delegation contract requires this ADR to either handle the public-input-boundary threat or cite the ADR that does. The original ADR cites 0043 for "trust model" but never names which T-tier covers ingress abuse. **This amendment names it:**

> Inbound provider webhooks (Postmark Inbound, SendGrid Inbound Parse, Twilio status callback) cross the **T2 Public-Boundary tier** of ADR 0043's catalog: untrusted input, attacker-controlled content, signature verification only authenticates the *transit channel* (the provider relayed it), NOT the *original sender* (a malicious sender can route through Postmark just like a legitimate one). Defense-in-depth at the substrate layer:
>
> 1. **Provider signature verify** — required, but proves only "Postmark sent this," not "Postmark accepted this from a benign sender."
> 2. **Sender allow-list per tenant** — `MessagingProviderConfig.AllowedSenderDomains` + `AllowedFromAddresses` (Phase 2.1 default: empty allow-list = accept all but score; Phase 2.2: enforced).
> 3. **Rate limit per sender + per tenant** — substrate-tier sliding window (default 30/hr per sender, 300/hr per tenant). Exceeding triggers a `MessageRateLimitExceeded` audit record + soft-reject (200 OK to provider, message held in unrouted-triage).
> 4. **Content scoring hook** — substrate exposes `IInboundMessageScorer` interface with default `NullScorer` (always 0); blocks-messaging consumers can plug in spam classifiers, reputation services, or LLM scoring without the substrate caring.
> 5. **Manual unrouted-triage** — the `IUnroutedTriageQueue` interface (already in spec) is the catch-all when 1–4 reject ambiguously.

Public-listings inquiry surface (ADR 0028 cluster intake) consumes this substrate via `IInboundMessageScorer` + per-listing rate limits — **not** by adding a parallel "public-input boundary" abstraction. ADR 0043 §"Threat catalog" gets a new entry: `T2-MSG-INGRESS` with this ADR as the authoritative handler.

### A2 — Thread-token cryptographic mechanism specified (Critical; resolves Risk 3)

The original ADR asserts "tokens are tenant-scoped (a leaked token from tenant A can't address tenant B's threads)" but doesn't specify the mechanism. **This amendment specifies it:**

```csharp
// Foundation.Integrations.Messaging.ThreadToken
public readonly record struct ThreadToken(string Value)
{
    // Format: base32(HMAC-SHA256(tenantKey, $"{tenantId}:{threadId}:{notBeforeUtc:O}")) + "." + base32(notBeforeUtcEpoch)
    // Length: 26 chars (HMAC) + "." + 7 chars (epoch) = 34 chars total — fits SMS reply-by-token windows
}

public interface IThreadTokenIssuer
{
    /// <summary>Mint a token bound to (tenant, thread, NotBefore). 90-day TTL.</summary>
    ThreadToken Mint(TenantId tenant, ThreadId thread, DateTimeOffset notBeforeUtc);

    /// <summary>Verify a token; returns the bound (tenant, thread) if valid + within TTL, null otherwise.</summary>
    (TenantId tenant, ThreadId thread)? Verify(ThreadToken token);

    /// <summary>Revoke a token by adding (token, revokedAtUtc) to the revocation log. Used on thread close + on suspected leakage.</summary>
    Task RevokeAsync(ThreadToken token, CancellationToken ct);
}
```

**Cryptographic properties:**

- **Per-tenant key** sourced from `Sunfish.Foundation.Recovery.ITenantKeyProvider` (workstream #15, shipped). Each tenant has its own HMAC key; cross-tenant verification fails by construction.
- **HMAC-SHA256** — symmetric MAC, fast verify, replay-safe (`notBeforeUtc` is part of the MAC input + checked in `Verify`).
- **TTL: 90 days** by default (overridable in `MessagingProviderConfig.ThreadTokenTtl`). Rationale: matches the longest reasonable conversational thread; longer than that, threads should be rebound via a new token issued at reply time.
- **Revocation log** — append-only per-tenant `IRevokedTokenStore` (the kernel-audit substrate is reused; revocations emit `ThreadTokenRevoked` audit records). Verify checks revocation status before accepting.
- **Rotation policy** — tenant key rotation cascades: on tenant-key rotation, all extant tokens get marked `expires_at = now + grace_period` (default 7 days) so legitimate in-flight replies still route while new tokens use the rotated key.

### A3 — Shared-sender-domain reputation contagion + Phase 2.3 fallback (Major; resolves Risk 1)

`messages.bridge.sunfish.dev` shared across all tenants in Phase 2.1 means one tenant's spam complaint poisons every tenant's deliverability. This amendment names the kill-trigger explicitly + the per-tenant fallback path:

**New revisit trigger** (append to §"Revisit triggers"):

> **Phase 2.3 deliverability isolation slips past 2026-Q3.** If per-tenant DKIM/SPF/DMARC + per-tenant Postmark Message Streams (or AWS SES Configuration Sets) is not in place by end of Q3 2026, ANY tenant deliverability incident on the shared sender domain triggers an immediate Phase 2.1 → Phase 2.2 jump: **abort the shared-domain pattern; require per-tenant subdomain (`messages.<tenantslug>.bridge.sunfish.dev`) before any new tenant onboards**.

**Per-tenant escape hatch (Phase 2.2 fallback if Phase 2.3 slips):**

- **Postmark:** assign each tenant a Message Stream (Postmark feature, no infrastructure churn). Stream = sender-reputation isolation unit. Cost: one Postmark account-level config change per tenant onboard.
- **AWS SES:** assign each tenant a Configuration Set + dedicated IP pool (or sandbox-shared IP for low-volume tenants). Cost: AWS resource-per-tenant management.
- Both options are **substrate-transparent** — `IMessagingGateway` adapter selects the per-tenant stream/config-set based on `TenantId` at send time. No contract changes.

**Implementation checklist addition:** add a `MessagingProviderConfig.SenderIsolationMode ∈ { SharedDomain, PerTenantStream, PerTenantSubdomain }` enum + plumbing. Default `SharedDomain` for Phase 2.1; flippable to `PerTenantStream` without redeployment.

### A4 — SMS thread-token "~80% preservation" replaced with fuzzy-matching primary (Major; resolves AP-1 + AP-21)

The original §"Threading semantics" asserts SMS thread tokens preserve "~80% of the time" with no citation. The reality varies by carrier (T-Mobile rewrites RCS, AT&T strips long URLs, MMS gateways occasionally drop the body) and is uncalibrated. **This amendment reframes:**

- **Token is best-effort, not primary.** The substrate writes `ThreadToken` into outbound SMS body when `MessagingProviderConfig.SmsThreadTokenStrategy == InlineToken`, but does NOT depend on the token surviving.
- **Primary thread-resolution is sender-recency matching.** When inbound SMS arrives at `+1NNNNNNNNNN` from sender `+1MMMMMMMMMM`, the substrate searches the `IThreadStore` for the **most recent** outbound thread to `+1MMMMMMMMMM` from `+1NNNNNNNNNN` within the past 14 days. If found, route the inbound to that thread. If multiple candidates within 14 days, route to the most-recent and log a `SmsAmbiguousThreadResolution` audit record for triage.
- **Token, when preserved, is a tiebreaker.** If the inbound body parses a valid `ThreadToken` AND fuzzy matching would route to a different thread, the token wins (it's cryptographically authenticated).
- **Default `SmsThreadTokenStrategy` = `OmitToken` for Phase 2.1.** Inline tokens look like spam in SMS bodies; rely on fuzzy matching. Tenants with high cross-thread-collision risk can opt into `InlineToken`.

This change makes substrate behavior calibratable (the unrouted-triage rate becomes the deliverable metric) rather than predicating success on an uncited number.

### A5 — Measurable success criteria + parity-test exclusion list (Major; resolves AP-3 + AP-18)

Append a new "## Success criteria (acceptance gates)" subsection before §"Pre-acceptance audit":

```
## Success criteria (acceptance gates)

Phase 2.1 is acceptance-complete when ALL of the following hold:

1. **Unrouted-triage rate ≤ 5%** of inbound messages, measured over a rolling
   30-day window per tenant. Anything higher → manual triage queue saturates;
   substrate failure mode is "human-in-the-loop bottleneck."
2. **Outbound delivery success ≥ 99%** (provider-confirmed delivery, NOT just
   "accepted by provider"). Below 99% → either provider quality issue or
   substrate misuse; halt acceptance.
3. **Postmark ↔ SendGrid parity test passes** with a defined byte-equivalence
   exclusion list. Excluded headers (allowed to differ): `Message-ID`,
   `Date`, `Received`, `X-Postmark-*`, `X-SG-*`, `X-Mailer`, any header
   prefixed `X-Provider-`. Excluded body content: provider-injected
   tracking pixels (HTML `<img src="https://*.postmarkapp.com/...">` or
   `https://*.sendgrid.net/...`). Everything else MUST be byte-equivalent.
4. **Token verification round-trip < 5ms p95** at 100 RPS sustained. Above
   that → IThreadTokenIssuer impl needs caching layer.
5. **Inbound webhook signature verify failure rate < 0.1%** of inbound
   webhooks. Higher → either provider config drift or attack pattern;
   alert on spike.
6. **Stuck-`AwaitingDeliveryConfirmation` observability** — alert if any
   message stays in this state > 24 hours; substrate emits gauge metric.
```

### Minor amendment — `MessageVisibility.PartyPrivate` removed from public surface; party-pair threads enforced mechanically

`MessageVisibility` was a 4-value enum: `{ Public, PartyPair, PartyPrivate, OperatorOnly }`. The Minor finding noted that `PartyPrivate` (a single-recipient private message inside a multi-party thread) shifts complexity from the substrate to consumers — every consumer has to implement the "show this message only to recipient X" filter at the projection tier. **This amendment removes it:**

- **New enum:** `{ Public, PartyPair, OperatorOnly }` (3 values).
- **`PartyPair` enforced mechanically:** thread visibility is determined by participant-set membership, NOT by per-message visibility. A 2-party thread (e.g., tenant ↔ vendor) is `PartyPair` by construction; messages in it are visible to both participants. There's no "private aside" inside a `PartyPair` thread.
- **For the genuine "private aside" use case:** consumers split into a NEW thread with a different participant set. The substrate provides `IThreadStore.SplitAsync(ThreadId source, ParticipantSet newParticipants)` to make this cheap.
- **`OperatorOnly` retained** for system-generated audit messages (e.g., "tenant changed billing email" — operators see, parties don't).

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options considered; Option B (reframe) chosen with explicit rejection rationale for Option A (defer ingress) and Option C (split into three ADRs). Option C's argument was real (smaller reviewable units) but rejected on symmetry grounds.
- [x] **FAILED conditions / kill triggers.** Listed explicitly: regulated vertical incompatibility, real-time chat surface required, data residency, abuse incident, deliverability collapse. Any one fires → ADR re-evaluated.
- [x] **Rollback strategy.** No production code consumes ADR 0052 today (placeholder only). Rollback = revert this ADR + revert the `Sunfish.Foundation.Integrations.Messaging` namespace addition. Cluster intakes that reference bidirectional substrate would revert to "outbound only + ingress TBD" status.
- [x] **Confidence level.** **HIGH.** The substrate is a generalization of patterns already validated (ADR 0013 provider seam, ADR 0049 audit emission, ADR 0032 capability model). No novel primitives. Risk is in the long-tail webhook-vendor differences, not in the core design.
- [x] **Anti-pattern scan.** Glanced at `.claude/rules/universal-planning.md` 21-AP list. None of AP-1 (unvalidated assumptions), AP-3 (vague phases), AP-9 (skipping Stage 0), AP-12 (timeline fantasy), AP-21 (assumed facts without sources) apply. Confidence calibrated; sources cited; phases are observable.
- [x] **Revisit triggers.** Seven explicit conditions named; each tied to an externally-observable signal.
- [x] **Cold Start Test.** Implementation checklist is 16 specific tasks, each verifiable. A fresh contributor reading this ADR plus the messaging substrate intake plus ADR 0013 should be able to scaffold `packages/blocks-messaging` and the first two `providers-*` adapters without asking for clarification.
- [x] **Sources cited.** ADR 0013 + ADR 0049 + ADR 0032 + ADR 0008 + ADR 0028 + ADR 0046 referenced for substrate alignment. Postmark + Twilio webhook docs cited. TCPA + CAN-SPAM referenced for compliance posture. Cluster intake INDEX cited for ADR drafting order.
