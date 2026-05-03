# Intake Note — Bidirectional Messaging Substrate (Reframes ADR 0052)

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build` and a hand-off file appears in `icm/_state/handoffs/`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turns 6 + 7 — leasing pipeline outbound criteria + vendor coordination inbound replies forced bidirectional scope).
**Pipeline variant:** `sunfish-api-change` (reframes ADR 0052 from outbound-only to bidirectional; new contracts replace prior placeholder).
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
**Position in cluster:** Spine #4 — communication backbone for vendors, leaseholders, prospects, applicants.

---

## Problem Statement

Phase 2 commercial intake placeholders ADR 0052 as "outbound messaging" — templated email + (later) SMS for invoices, statements, reminders. The leasing pipeline and vendor coordination requirements surfaced in the 2026-04-28 conversation expand that scope decisively:

1. **Outbound now includes** pre-screening criteria documents to prospects, application links, showing invitations, showing reminders, vendor work-order assignments, vendor magic-link access tokens, right-of-entry notices to tenants, completion notifications.
2. **Inbound is now load-bearing.** Prospects reply to criteria emails with applications. Vendors reply to work-order assignments with status updates. Tenants reply to entry notices to confirm or reschedule. Leaseholders reply to maintenance follow-ups. Without inbound parsing and routing, the communication is a one-way megaphone — not a workflow.
3. **Threading is required.** A single work order can have 20+ messages spanning owner ↔ vendor ↔ tenant over two weeks. Messages need to land on the right thread (work-order, leasing-pipeline-application, lease-related-conversation), not in a generic owner inbox.
4. **Multi-party visibility is required.** A vendor message to "the work order" is visible to owner + vendor + tenant. An owner-private note to vendor is not. The visibility model is part of the substrate, not an afterthought.

ADR 0052's scope as "outbound messaging" doesn't fit. The right framing is **"bidirectional messaging substrate"** — egress + ingress + threading + multi-party visibility. Either ADR 0052 reframes, or it splits into 0052 (outbound) + 0053 (inbound) + 0054 (thread model). Recommend reframe; the contracts are symmetric and splitting them obscures that.

## Scope Statement

### In scope (this intake)

1. **Egress contracts** (extends `Foundation.Integrations` per ADR 0013):
   - `IOutboundMessageGateway` — channel-agnostic send (email | sms | future channels)
   - Templated message rendering with versioning + content-hash binding (same mechanic as criteria docs / signed leases)
   - Send-tracking (queued, sent, delivered, bounced, complained, opened, clicked)
   - Provider adapters: `providers-email-postmark`, `providers-email-sendgrid`, `providers-email-ses`, `providers-sms-twilio` (one of each minimum)
   - Per-tenant gateway configuration (different LLCs may use different email providers)

2. **Ingress contracts**:
   - `IInboundMessageReceiver` — channel-agnostic receive
   - Webhook endpoints on Bridge for Mailgun / Postmark / SES inbound email + Twilio SMS reply
   - Inbound parsing: extract thread/work-order/application ID from `Reply-To` (email) and reserved tokens (SMS); extract attachments (photos, PDFs); extract sender identity
   - Inbound abuse posture: rate-limit, sender allow/block list, attachment scanning hook
   - Inbound message routing: matched messages append to thread; unmatched land in "unrouted inbox" for triage

3. **Thread entity model**:
   - `Thread` — opaque ID, scoped to one of: WorkOrder | LeasingApplication | LeaseRelation | TenantRelation | VendorRelation | (other future scopes)
   - `Message` — direction (egress | ingress), channel, sender (kernel identity or external party ref), recipients, content, attachments, sent_at, delivered_at, status
   - `ThreadParticipant` — who's on the thread + visibility role (owner | vendor | tenant | bookkeeper | etc.); drives visibility filtering
   - `ThreadVisibilityRule` — per-thread or per-message override: "private to {owner, vendor}" | "shared with all participants" | "owner-only"

4. **`blocks-messaging` package.** New persistent block; entity registration per ADR 0015; persistence via `foundation-persistence`.

5. **Provider-neutrality compliance.** Per ADR 0013 (and the in-flight enforcement gate, workstream #14), `blocks-messaging` references no vendor SDK directly; only `providers-*` packages do. The Roslyn analyzer must pass for this package.

6. **Bidirectional ADR 0052** — drafted, replaces the placeholder in Phase 2 commercial intake.

### Out of scope (this intake — handled elsewhere)

- Specific message templates (criteria docs, showing invites, entry notices, etc.) — handled in their own domain intakes (Leasing Pipeline, Work Orders, Leases). This intake delivers the substrate; templates are content.
- Real-time chat / in-app notifications — Phase 3+
- Voice / phone-call integration — out of scope indefinitely
- Custom attachment processing (OCR receipts, virus scan) — extension hooks reserved; implementation in receipts/work-orders intakes

### Explicitly NOT in scope (deferred)

- WhatsApp / Signal / iMessage Business — Phase 4+
- SMTP self-hosting (running our own mail server) — out of scope; `providers-email-*` only
- Inbound IMAP polling (legacy mailbox monitoring) — out of scope; webhook-based inbound only

---

## Affected Sunfish Areas

| Layer | Item | Change |
|---|---|---|
| Foundation | `Foundation.Integrations` | Extends with bidirectional contracts; today's contracts are inbound-webhook-only per ADR 0013 |
| Foundation | `foundation-persistence` | Thread, Message, ThreadParticipant entities |
| Blocks | `blocks-messaging` (new) | Primary deliverable |
| Providers | `providers-email-postmark` (new) | First exemplar email provider |
| Providers | `providers-email-sendgrid` (new) | Second email provider for adapter parity testing |
| Providers | `providers-email-ses` (new, optional Phase 2.1) | AWS SES for cost-sensitive tenants |
| Providers | `providers-sms-twilio` (new) | First SMS provider |
| Bridge | Webhook receiver endpoints | Inbound webhook handlers per provider |
| ADRs | ADR 0052 (reframe) | Bidirectional substrate; replaces outbound-only scope |
| ADRs | ADR 0013 (provider-neutrality) | First major exercise of the providers-* pattern post-enforcement-gate |
| ADRs | ADR 0049 (audit substrate) | Inbound + outbound messages are first-class audit events; PII-redacted projections for audit log |
| ADRs | ADR 0043 (threat model) | Inbound message boundary; abuse posture; sender authentication |
| Other | Phase 2 commercial intake | "Outbound messaging contracts" deliverable updates to point at this reframed scope |

---

## Acceptance Criteria

- [ ] ADR 0052 reframed and accepted (bidirectional substrate)
- [ ] `IOutboundMessageGateway`, `IInboundMessageReceiver` contracts in `Foundation.Integrations` with full XML doc
- [ ] `Thread`, `Message`, `ThreadParticipant`, `ThreadVisibilityRule` entities in `blocks-messaging`
- [ ] `providers-email-postmark` + `providers-email-sendgrid` + `providers-sms-twilio` ship with passing tests
- [ ] Adapter parity test: same outbound email rendered identically via Postmark and SendGrid
- [ ] Inbound webhook endpoints on Bridge with rate-limiting, signature verification per provider, abuse posture
- [ ] Inbound parsing: thread ID extraction from email `Reply-To`, SMS reserved-token format, attachment routing
- [ ] Unrouted-inbox triage UX (Bridge owner cockpit)
- [ ] Provider-neutrality analyzer passes on `blocks-messaging` (no vendor SDK references)
- [ ] kitchen-sink demo: send a vendor work-order assignment → vendor replies via email → reply lands on the work-order thread, visible to owner + vendor + tenant per visibility rules
- [ ] apps/docs entry covering the messaging substrate + provider selection guidance
- [ ] Phase 2 commercial intake updated to reference reframed ADR 0052

---

## Open Questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-M1 | Inbound parsing service: Mailgun vs Postmark Inbound vs SES Inbound. Each has different webhook formats, attachment handling, deliverability characteristics. | Stage 02. Recommend Postmark Inbound as Phase 2.1 default (cleanest API); SES Inbound as cost-optimized alternative. Ship both as `providers-email-inbound-*`. |
| OQ-M2 | SMS inbound thread routing: Twilio supports MMS attachments but reply-token format is fragile. What's the abandonment policy when SMS reply doesn't include a recognizable token? | Stage 02. Route to "unrouted inbox" with sender-based fuzzy matching (recent threads with this phone number). |
| OQ-M3 | Per-tenant provider config: bookkeeper for Tenant A uses one Postmark account; BDFL uses another. How is provider-config storage scoped? | Stage 03. Recommend per-tenant secret store; reuse Foundation.Recovery primitives for encryption-at-rest. |
| OQ-M4 | Visibility rule expression language: declarative ACL ("owner + vendor") vs predicate ("any participant whose role is X") vs capability-based (whoever holds capability C)? | Stage 02 design. Recommend capability-based to align with Foundation.Macaroons + ADR 0032. |
| OQ-M5 | Message-level visibility override: thread is "shared," but owner sends a side-message to vendor only. Implementation as a child Thread vs message-level visibility override? | Stage 02. Recommend dedicated owner-vendor private Thread (cleaner; visibility filtering becomes thread-level only). |
| OQ-M6 | Inbound attachment scanning: virus scan + size cap + content-type allowlist. Where in the pipeline? | Stage 02. Recommend Bridge edge (before persistence); reject early. |
| OQ-M7 | Compliance — TCPA (telephone consumer protection) for outbound SMS, CAN-SPAM for outbound email. Consent management surface. | Stage 02 — recommend per-recipient consent flag with audit trail; defer detailed compliance UX to Phase 2.3 |
| OQ-M8 | DKIM / SPF / DMARC for outbound email — per-tenant domain configuration vs shared sender domain? | Stage 02 — Phase 2.1 ships shared sender domain (`messages.bridge.sunfish.dev` or similar); per-tenant custom domain is Phase 2.3. |

---

## Dependencies

**Blocked by:**
- ADR 0013 enforcement gate (workstream #14, ready-to-build) — provider-neutrality analyzer must be in place before this intake's Stage 06 starts
- ADR 0049 audit substrate (already accepted) — for messaging-event audit logs
- Foundation.Recovery split (workstream #15, ready-to-build) — for per-tenant secret encryption (provider configs, API keys)

**Blocks:**
- Vendors (sibling intake) — magic-link onboarding form delivery
- Work Orders (sibling intake) — multi-party threads, entry-notice delivery
- Leasing Pipeline (sibling intake) — outbound criteria, application links, showing invitations
- Public Listings (sibling intake) — inquiry intake → owner notification
- Phase 2 commercial intake's "outbound statement email" deliverable

**Cross-cutting open questions consumed:** OQ6 (inbound channel parsing) from INDEX.

---

## Pipeline Variant Choice

`sunfish-api-change` because:
- Reframes ADR 0052 (existing placeholder in Phase 2 commercial intake) — that's a public-contract change in the architecture decision record
- Introduces new contracts in `Foundation.Integrations` that other Phase 2 deliverables consume
- Migration guide required for any Phase 2 outbound-messaging code already written against the original ADR 0052 placeholder

Stage 02 (architecture) and Stage 03 (package design) mandatory; migration guide as Stage 06 deliverable.

---

## Cross-references

- Parent: [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
- Phase 2 commercial: [`phase-2-commercial-mvp-intake-2026-04-27.md`](./phase-2-commercial-mvp-intake-2026-04-27.md)
- Sibling intakes: Vendors, Work Orders, Leasing Pipeline, Public Listings
- ADR 0013 (provider-neutrality), ADR 0043 (threat model), ADR 0049 (audit substrate), ADR 0052 (reframed by this intake)
- Workstream #14 (provider-neutrality enforcement gate) — prerequisite

---

## Sign-off

Research session — 2026-04-28
