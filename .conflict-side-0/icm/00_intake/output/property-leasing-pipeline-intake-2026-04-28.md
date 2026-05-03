# Intake Note — Leasing Pipeline + Fair Housing Compliance

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build` and a hand-off file appears in `icm/_state/handoffs/`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turn 6 — public-facing inquiry intake, criteria sending, showings, applications).
**Pipeline variant:** `sunfish-feature-change` (with new ADR — Fair Housing posture is foundational)
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
**Position in cluster:** Cross-cutting #2 — public-input boundary, capability promotion, jurisdiction-aware compliance posture.

---

## Problem Statement

When a property is vacant, the leasing pipeline is the workflow that gets it filled. Today the BDFL operates this manually + via Rentler. Phase 2 commercial intake explicitly defers Rentler replacement to Phase 3, but the *non-portal* leasing-pipeline pieces — public listings, inquiry intake, criteria sending, showing scheduling, application receipt, accept/decline — are operational gaps the field-app conversation surfaced as in-scope.

Critically, this is the first Sunfish workflow that **accepts input from anonymous public actors**. Up to now, every actor has been known: BDFL, spouse, bookkeeper, contractor, leaseholder, vendor (with W-9). A prospect filling out a public inquiry form is untrusted, anonymous, and capability-bounded. That's a different trust posture and requires explicit threat-model treatment.

Two compounding compliance concerns:

1. **Fair Housing Act (FHA) and state-equivalent laws** require pre-screening criteria to be applied uniformly across applicants. Selectively screening applicants by protected class is the legal exposure that ends a property-management business. The defense is documentation: the *exact criteria document version* sent to *each prospect* on *what date*, plus *uniform application*. That is a content-binding mechanic identical to signed leases.
2. **State-specific tenant screening law** — California, New York, Washington, others have specific regulations on application fees, screening criteria disclosure, adverse-action letters, and credit/background-check consent. This is jurisdiction-aware policy.

This intake captures the leasing pipeline state machine, the FHA-compliant criteria-document mechanism, the public-input boundary, and the new ADR work.

## Scope Statement

### In scope (this intake)

1. **`Inquiry` entity.** Public-facing form submission: prospect contact info, property of interest, message, source (listing page | direct email | phone-logged), received_at, status (new | criteria-sent | applied | declined | abandoned).
2. **`CriteriaDocument` entity.** Versioned, content-hash-bound (same mechanic as signed leases). Per-property or per-LLC-tenant defaults. Contains: income multiple, credit score floor, eviction-history policy, rental-history requirement, smoking/pet policy, occupancy limit, application-fee amount, jurisdiction-required disclosures.
3. **`CriteriaAcknowledgement` entity.** Prospect's acknowledgement of receiving criteria + intent to proceed. Lightweight signature (per Signatures intake) capturing prospect identity + criteria document version. Required before application accepted.
4. **`Application` entity.** Full rental application: applicant info, employment, income, references, prior addresses, consent to credit/background check, application-fee receipt link. Multi-section form; partial-save supported.
5. **`Showing` entity.** Scheduled time slot + property + prospect + (optional) tenant-coordination if currently occupied + status (proposed | confirmed | completed | no-show | rescheduled). iCal export.
6. **Leasing-pipeline state machine:**
   ```
   Inquiry → CriteriaSent → CriteriaAcknowledged →
     ApplicationStarted → ApplicationSubmitted → ApplicationReviewed →
     Approved → LeaseDrafted → LeaseSigned → MoveInScheduled → TenancyStart
   (side branches: Declined-with-AdverseActionLetter, Withdrawn, Stalled)
   ```
7. **Showing scheduling logic.** Owner publishes 3 candidate slots → prospect picks one → owner confirms → both parties get iCal invitation. If property is occupied (rare; usually only between leases), tenant gets right-of-entry notice per Work Orders intake mechanism.
8. **Public inquiry-form Bridge surface.** Anonymous-accessible inquiry form per public listing; rate-limited; CAPTCHA-gated; abuse-posture per ADR 0043 addendum.
9. **Capability-promotion flow.** Anonymous (filled inquiry form) → Prospect (criteria sent and acknowledged; verified email) → Applicant (started application; can save partial state). ADR 0043 addendum captures the boundaries.
10. **Adverse-action letter generation.** When declining based on credit/background report contents, FCRA-compliant letter generation + delivery via messaging substrate.
11. **`blocks-leasing-pipeline` package.** New persistent block; entity registration per ADR 0015.
12. **New ADR**: "Leasing pipeline + Fair Housing compliance posture."
13. **ADR 0043 addendum**: public-input boundary; capability promotion (anonymous → prospect → applicant); inquiry-form abuse posture.
14. **Jurisdiction-policy module.** Pluggable jurisdiction policies (US-state-level) for: tenant-screening law specifics, allowed application fees, required disclosures in criteria, FCRA adverse-action requirements. Phase 2.1d ships BDFL's property states only; broader coverage in Phase 2.3.

### Out of scope (this intake — handled elsewhere)

- Public listing surface (the actual property pages) → [`property-public-listings-intake-2026-04-28.md`](./property-public-listings-intake-2026-04-28.md)
- Lease document storage and execution → [`property-leases-intake-2026-04-28.md`](./property-leases-intake-2026-04-28.md)
- Move-in checklist execution (after MoveInScheduled state) → [`property-inspections-intake-2026-04-28.md`](./property-inspections-intake-2026-04-28.md)
- Outbound email/SMS delivery → [`property-messaging-substrate-intake-2026-04-28.md`](./property-messaging-substrate-intake-2026-04-28.md)
- Signature mechanism for criteria acknowledgement → [`property-signatures-intake-2026-04-28.md`](./property-signatures-intake-2026-04-28.md)
- Credit/background check provider integration → ADR 0013 follow-on; `providers-screening-*` package family

### Explicitly NOT in scope (deferred)

- Tenant portal (lease-holder login, online rent pay, maintenance requests via portal) — Phase 3 per Phase 2 commercial intake decision
- Two-way calendar integration with prospect's Google/Apple/Outlook — Phase 2.3+
- AI-assisted application screening / risk scoring — Phase 4+
- Multi-applicant household collaborative application — Phase 2.3+

---

## Affected Sunfish Areas

| Layer | Item | Change |
|---|---|---|
| Foundation | `foundation-persistence` | New entity registration |
| Foundation | Jurisdiction-policy framework | New plug-in surface; reusable for right-of-entry policy in Work Orders |
| Blocks | `blocks-leasing-pipeline` (new) | Primary deliverable |
| Blocks | `blocks-properties` (sibling) | FK consumer |
| Blocks | `blocks-leases` (sibling) | LeaseDrafted state hands off to `blocks-leases` |
| Bridge | Public inquiry form | Anonymous-accessible, rate-limited |
| Bridge | Application form (multi-section, partial-save) | Authenticated as prospect/applicant via short-lived macaroon |
| Bridge | Showing-scheduling page | Prospect-facing; macaroon-authenticated |
| Bridge | Owner cockpit pipeline view | Authenticated owner |
| iOS | (minimal) | Showing entry creation; lease draft start trigger from on-site visit |
| ADRs | New "Leasing pipeline + Fair Housing" | Primary architectural deliverable |
| ADRs | ADR 0043 addendum | Public-input boundary |
| ADRs | ADR 0049 | Inquiry receipt, criteria sent, acknowledgement, application submitted, decline events all audit-logged |
| ADRs | ADR 0052 (reframed) | Outbound criteria, inbound application replies, adverse-action letters |

---

## Acceptance Criteria

- [ ] New ADR (Leasing pipeline + FHA) accepted
- [ ] ADR 0043 addendum accepted
- [ ] All entities defined with XML doc + adapter parity
- [ ] State machine implemented; invalid transitions rejected; each transition emits to audit
- [ ] Public inquiry form on Bridge with CAPTCHA + rate limit + signature verification per provider
- [ ] CriteriaDocument versioning + content-hash binding integrated with Signatures
- [ ] CriteriaAcknowledgement flow: send → prospect receives email → clicks link → reviews criteria → acknowledges (lightweight signature) → application unlocked
- [ ] Application form: multi-section, partial-save, attachment upload, FCRA-compliant credit/background-check consent
- [ ] Showing scheduling: 3-slot proposal → prospect pick → confirmation → iCal export
- [ ] Adverse-action letter generation tied to FCRA-compliant template; delivered via messaging substrate
- [ ] Jurisdiction-policy framework: BDFL's property states pinned; policy DSL or config-file format documented
- [ ] kitchen-sink demo: full pipeline from inquiry to MoveInScheduled
- [ ] apps/docs entry covering pipeline + FHA posture + jurisdiction-policy authoring

---

## Open Questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-L1 | Criteria document format: PDF (one canonical), HTML (renders responsively), or both? | Stage 02 — both; HTML for in-page display + PDF for download/email. Same content hash drives both. |
| OQ-L2 | CriteriaAcknowledgement signature: in-page click-to-acknowledge with consent UX vs full Signatures-intake signature event? | Stage 02 — recommend full SignatureEvent with `capture_quality: web_click` to keep one canonical mechanism. |
| OQ-L3 | Application abandonment policy: prospects often start applications and don't finish. Auto-archive after N days? Notify with reminder? | Stage 02 — auto-archive after 14 days; one reminder at 7 days. |
| OQ-L4 | Jurisdiction-policy authoring: code (TypeScript/C# class per state), config DSL (JSON/YAML schema), or content-managed (database-driven)? | Stage 02 — recommend C# class per jurisdiction implementing `IJurisdictionPolicy`; config DSL for thresholds within. |
| OQ-L5 | FHA uniformity audit: how does the system *prove* uniform application post-hoc? Daily audit job comparing decline reasons against criteria? | Stage 02 — periodic audit query + manual review surface; not auto-blocking. |
| OQ-L6 | FCRA adverse-action letter template versioning + multi-state customization | Stage 03 — template-per-jurisdiction with shared base; CriteriaDocument-style content-hashing. |
| OQ-L7 | Application fee handling: collect on submission (Stripe via ADR 0051) or on acceptance? Per-state law varies. | Stage 02 — defer to jurisdiction policy + Phase 2 payments work. Default: collect on submission with state-policy override. |
| OQ-L8 | Showing scheduling conflict resolution: same property, two showings in overlapping windows. CP-class lease per Work Orders OQ-W1? | Stage 02 — yes, reuse CP appointment-slot lease primitive from Work Orders. |
| OQ-L9 | Public inquiry form spam vs legitimate. Heuristics? Honeypot? reCAPTCHA v3 score threshold? | Stage 02 — recommend reCAPTCHA v3 + per-IP rate limit + honeypot field. |

---

## Dependencies

**Blocked by:**
- Properties (sibling) — FK
- Messaging substrate (sibling) — outbound criteria + inbound application reply routing
- Signatures (sibling) — CriteriaAcknowledgement + LeaseSigned state transitions
- Public Listings (sibling) — inquiry-form delivery surface
- New "Leasing pipeline + FHA" ADR
- ADR 0043 addendum

**Blocks:**
- [`property-leases-intake-2026-04-28.md`](./property-leases-intake-2026-04-28.md) — LeaseDrafted state hands off
- [`property-inspections-intake-2026-04-28.md`](./property-inspections-intake-2026-04-28.md) — MoveInScheduled triggers move-in inspection workflow
- Phase 2.1d-e deliverables

**Cross-cutting open questions consumed:** OQ7 (jurisdiction policy), OQ8 (criteria versioning), OQ11 (public-listing abuse), OQ12 (calendar integration depth) from INDEX.

---

## Pipeline Variant Choice

`sunfish-feature-change` with mandatory new ADR. Stage 02 + 03 mandatory; jurisdiction-policy framework requires careful design before Stage 06.

---

## Cross-references

- Parent: [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
- Sibling intakes: Public Listings, Properties, Leases, Inspections, Messaging Substrate, Signatures
- ADR 0008 (multi-tenancy), ADR 0013 (provider neutrality — for screening adapters), ADR 0043 (threat model — addendum), ADR 0049 (audit substrate), ADR 0052 (reframed messaging)

---

## Sign-off

Research session — 2026-04-28
