# ADR 0052 — Bidirectional Messaging Substrate — Council Review

**Date:** 2026-04-29
**Reviewer:** Research-session adversarial council (Universal Planning Framework Stage 1.5: six perspectives + 21-AP sweep + quality rubric)
**Subject:** [`docs/adrs/0052-bidirectional-messaging-substrate.md`](../../../../docs/adrs/0052-bidirectional-messaging-substrate.md)
**Companion intake:** [`property-messaging-substrate-intake-2026-04-28.md`](../../../00_intake/output/property-messaging-substrate-intake-2026-04-28.md)

---

## 1. Verdict

**Accept with amendments.**

The reframe from outbound-only to bidirectional substrate is correct and well-reasoned, the option analysis is honest, and the substrate compositionally re-uses ADR 0013 (provider seam), ADR 0032 (capability projection), ADR 0049 (audit substrate), and ADR 0008 (multi-tenancy) without inventing new primitives. The thread/visibility model is the strongest part of the design. However, four amendments are non-optional before any of the five cluster intakes (#18 Vendors, #19 Work Orders, #22 Leasing, #28 Public Listings, #5 Phase 2 statements) lean on this substrate at Stage 02, because the gaps below all become structural debt the moment a consumer ADR cites this one.

## 2. Anti-pattern findings (21-AP sweep)

| AP | Severity | Where it fires |
|---|---|---|
| **AP-3** Vague success criteria | Major | "Five cluster intakes unblock simultaneously" is asserted as a positive consequence, but no measurable acceptance criterion exists — no SLA on delivery, no target for unrouted-triage rate, no parity-test pass criterion between Postmark and SendGrid adapters. The implementation checklist enumerates *tasks* not *gates*. |
| **AP-1 / AP-21** Unvalidated assumption + assumed-fact-without-source | Major | "SMS thread tokens preserve ~80% of the time" is asserted in §Threading semantics with no citation. This number drives the OQ-M2 fall-through design; if it's actually 50% the unrouted-triage queue becomes the dominant ingress path, not the exception. |
| **AP-15** Premature precision | Minor | The default rate limits in the per-tenant config (`30 emails/sender/hour`, `60 SMS/sender/hour`) are presented as defaults without calibration evidence. Fine as a starting heuristic; not fine if a downstream ADR cites them as load-bearing. |
| **AP-19** Discovery amnesia | Minor | The DKIM/SPF/DMARC deferral to Phase 2.3 mentions "shared sender domain `messages.bridge.sunfish.dev`" as the Phase 2.1 default, but the §Negative consequences only flags this as "reputation implications across tenants" — the actual cross-tenant deliverability blast-radius (one tenant's spam complaint poisons the IP/domain reputation for all tenants) is not named. Future implementers will rediscover this. |
| **AP-13** Confidence without evidence | Minor | "Tokens are tenant-scoped (a leaked token from tenant A can't address tenant B's threads)" — the mechanism is named but not specified. Is it HMAC over (tenant, threadId) with a per-tenant key? Is it a lookup table? The security claim rides on the mechanism. |
| **AP-18** Unverifiable gates | Minor | Provider adapter parity (Postmark vs SendGrid → "byte-equivalent rendered output excluding provider-specific headers") is testable but the exclusion list is unspecified. What counts as "provider-specific"? Without that list, the parity test is whatever the implementer says it is. |

**Cleanly avoided:** AP-9 (Stage 0 done — three options + verdicts), AP-5 (consequences extend past Decision), AP-10 (first idea explicitly challenged — Option B beat A and C on stated grounds), AP-12 (no fantasy timelines), AP-11 (revisit triggers are observable + externally-signaled).

## 3. Top 3 risks (highest impact first)

### Risk 1 (HIGH) — Shared-sender-domain reputation contagion across tenants

`messages.bridge.sunfish.dev` is the Phase 2.1 outbound domain shared across **all** tenants. When tenant A's right-of-entry notices trigger spam complaints (or tenant B's leasing-application replies hit Gmail's bulk filter), the resulting reputation hit lands on every tenant sharing the domain. DKIM/SPF/DMARC deferral to Phase 2.3 means there's no per-tenant deliverability isolation in the meantime. **Pessimistic Risk Assessor view:** the *first* deliverability incident (a single tenant going dark for a week) becomes a multi-tenant incident, and the recovery path requires the Phase 2.3 work to be ready early. **Skeptical Implementer view:** if Phase 2.3 slips, the workaround is per-tenant Postmark "Message Streams" or SES "Configuration Sets" — not architectural changes — but the ADR doesn't name that escape hatch.

### Risk 2 (HIGH) — Inbound abuse posture is a contract-level hook only; no concrete defense for public webhook surfaces

§Threat impact correctly identifies that "inbound is a public-internet boundary" and §Decision lists rate-limit + signature + sender-block as "substrate hooks." But the hooks are abstract — there is no concrete answer to: what happens when an attacker spams the Postmark Inbound webhook with forged envelopes that pass signature verification (because they actually came from Postmark, just from a malicious sender)? The §Considered options didn't sparse this — Option B implicitly assumes provider signature verification is sufficient. It is not, for the public-listings inquiry surface. **Pedantic Lawyer view:** ADR 0043's threat-delegation contract requires this ADR to either handle the public-input-boundary threat or cite the ADR that does. Right now it does neither cleanly — it cites ADR 0043 for "trust model" but never declares which T-tier of 0043's catalog covers ingress abuse.

### Risk 3 (MEDIUM) — Visibility model has three primitives; the recommended pattern (party-pair threads) shifts complexity from the substrate to the consumer

The "use a dedicated `VendorRelation` thread instead of a per-message visibility override" recommendation is **correct** for audit clarity — owner-vendor-private content on a fully-shared work-order thread is genuinely a footgun. But the recommendation only works if the consumer code (work-orders, leasing, vendors clusters) actually creates and routes to the right party-pair thread at message-send time. There is no enforcement; nothing in the substrate prevents a consumer from setting `MessageVisibility.PartyPrivate` on a shared-thread message. **Devil's Advocate view:** the substrate ships an override the docs say "discouraged but available." That's a foot-gun that history shows will fire. Either remove `MessageVisibility.PartyPrivate` from the public surface (force the party-pair-thread pattern mechanically) or accept that some consumer ADR will use it and document the audit-projection semantics for the override case.

## 4. Top 3 strengths

1. **Provider adapter symmetry per vendor relationship is the right call.** `providers-email-postmark` (egress) + `providers-email-inbound-postmark` (ingress) as separate packages within the *same* vendor relationship resolves the Option-A/Option-C tension cleanly. The packages are separate (so a tenant can run Postmark out + SES In if they want) but the design is symmetric (Postmark-the-vendor is one mental model). This is exactly the pattern Option C tried to fragment.
2. **Capability-driven projection via Foundation.Macaroons (ADR 0032) for visibility computation is structurally elegant.** A bookkeeper whose macaroon is revoked stops seeing past messages without rewriting history — that's the right semantics for audit-substrate-compatible visibility. No static ACL, no view-rebuild, no "but who saw what when" reconstruction nightmare. The Manager's view: this is the kind of cross-ADR composition that makes the architecture pay off.
3. **Audit-substrate redaction discipline is coherent and tractable.** Body stays in `Foundation.Integrations.Messaging` persistence under tenant-key encryption; audit substrate gets sender/recipient/metadata only. That's the right side of the redaction line for ADR 0049's append-only event-sourced ledger — bodies in the ledger would balloon storage and complicate retention. Matches the §Trust impact section's PII discipline.

## 5. Required amendments (Accept-with-amendments)

| # | Severity | One-line amendment |
|---|---|---|
| 1 | Critical | Add a §"Public webhook surface threat tier" subsection citing ADR 0043's threat catalog explicitly — name which T-tier covers ingress abuse and what the per-tier defense-in-depth is (provider signature + per-sender rate-limit + content scanning + unrouted-triage gating). |
| 2 | Critical | Specify the thread-token cryptographic mechanism: HMAC-SHA-256 over `(tenantId, threadId, issuedAt)` with per-tenant key from ADR 0046's Foundation.Recovery primitives; declare TTL default; declare rotation policy. The current "tokens are tenant-scoped" claim needs the mechanism named to be auditable. |
| 3 | Major | Add measurable success criteria: target unrouted-triage rate (<5% of inbound messages over rolling 7d), Postmark↔SendGrid parity-test pass criterion (full byte-diff allowlist enumerated), and provider-adapter-onboarding gate (a new vendor adapter is "ready" when egress + ingress + delivery-status webhook all pass with their own signature scheme). |
| 4 | Major | Add a §"Shared-sender-domain risk window" subsection naming the deliverability blast-radius across tenants on `messages.bridge.sunfish.dev` until Phase 2.3, the per-tenant Message-Streams/Configuration-Sets workaround if Phase 2.3 slips, and a kill trigger ("if a single tenant's spam complaints cause the shared domain's reputation to drop below threshold X, halt outbound for the offending tenant until per-tenant DKIM lands"). |
| 5 | Major | Cite the SMS-token-preservation rate (~80% claim) or remove the number. If unsourced, replace with "preservation rate is provider+carrier dependent; substrate must handle both preserved and stripped tokens" — and make the fuzzy sender-recency matching the *primary* path, not the fallback. |
| 6 | Minor | Either remove `MessageVisibility.PartyPrivate` from the public surface (force party-pair threads mechanically) or document the audit-projection semantics for the override case so consumer ADRs can't silently pick the foot-gun. |
| 7 | Minor | Add an Assumptions table (Assumption → VALIDATE BY → IMPACT IF WRONG) covering: per-tenant credential isolation suffices for cross-tenant abuse defense; macaroon-driven visibility is performant at message-list page load; Postmark Inbound's signing scheme is sufficient for ingress trust. |

Amendments 1, 2, 4, 5 should land before any cluster intake (#18/#19/#22/#28) advances to Stage 02. Amendments 3, 6, 7 can land in parallel with Stage 02 design work on those clusters.

## 6. Quality rubric grade

**B+ (Solid, with clear A path)**

- **C floor cleared:** All 5 CORE sections present (Context, Success Criteria via consequences, Assumptions implicit, Phases via implementation checklist, Verification via parity tests + audit emission). Multiple CONDITIONAL sections (Compatibility plan, Open questions, Revisit triggers, References, Pre-acceptance audit). No critical anti-patterns fired.
- **B floor cleared:** Stage 0 evident (three options, explicit verdicts, AHA-style reframe from outbound-only to bidirectional). FAILED conditions present as Revisit triggers (seven of them, each externally-observable). Confidence Level declared (HIGH, with calibration). Cold Start Test: a fresh contributor with this ADR + the messaging intake + ADR 0013 *can* scaffold the substrate — the implementation checklist is concrete enough.
- **A ceiling missed by:** No structured Assumptions table (AP-1 partial); no Reference Library beyond the inline citations (Postmark and Twilio webhook docs are the only externals); no Knowledge Capture section naming what we expect to learn from being the first major exercise of the providers-* pattern post-enforcement-gate; success criteria not measurable; the public-webhook-boundary threat tier is named but not delegated to ADR 0043's catalog explicitly. With amendments 1–4 above, this lifts cleanly to A.

**Defensibility of deferred concerns (chat-class, real-time, WhatsApp/iMessage):** Acceptable. Each deferral has a revisit trigger naming what would force re-evaluation, and the substrate is channel-additive (adding WhatsApp later doesn't break the existing contracts). The chat-class deferral is the strongest one — durable async messaging and presence-bearing real-time chat are genuinely different substrates and conflating them would have produced a worse ADR.

---

**Bottom line for the CTO:** Accept with the four critical/major amendments listed above. The design is sound, the option analysis is honest, the cross-ADR composition is the right kind of architectural payoff. The amendments close gaps that would otherwise become structural debt the moment cluster ADRs cite this one — and at least two of them (the public-webhook threat tier and the thread-token cryptographic mechanism) are the kind of gap that a security incident, not a code review, will surface if left in.
