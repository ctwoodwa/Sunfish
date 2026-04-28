# Intake Note — Work Orders Coordination-Spine Domain Module

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build` and a hand-off file appears in `icm/_state/handoffs/`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turn 7 — vendor coordination crystallized work-order as the missing coordination unit).
**Pipeline variant:** `sunfish-feature-change` (escalates to `sunfish-api-change` because work-order touches and reshapes prior maintenance-item scope from Phase 2 commercial intake)
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
**Position in cluster:** Spine #3 — coordination unit that ties the rest together. **The architectural keystone of the cluster.**

---

## Problem Statement

The property-operations vertical needs a coordination unit that lives at the intersection of nearly every other domain. Without it, every domain has to invent its own ad-hoc coordination affordances:

- "This water heater (Asset) was inspected (Inspection event) and found failing; needs replacement (Maintenance request); we hired Acme Plumbing (Vendor) to replace it on 2026-05-12 09:00 (Appointment); we notified the tenant 48 hours prior (Right-of-entry compliance); the tenant confirmed (Communication thread); the work was done; vendor signed off and uploaded a photo (Signature + blob); we received an invoice (Receipt); paid via ACH (Payment)."

That is a **single business process** with one atomic identity (the work order). Without the work-order entity, eight modules each hold a fragment of the truth and reconciliation becomes impossible.

The work order is the spine. Maintenance items become work-order *sources*; vendors become *assignees*; appointments become *coordinated time slots*; communication threads become *work-order-scoped*; receipts become *completion artifacts*; signatures become *sign-off attestations*; right-of-entry notices become *audit-logged events on the work order*.

This intake captures the work-order entity, its state machine, its coordination affordances, and the integration contracts with surrounding modules. The deepest open question is **CP/AP classification per record class** — the work-order itself, its appointment slot, and its message thread have different concurrency semantics and need to be modeled per paper §6.3.

## Scope Statement

### In scope (this intake)

1. **`WorkOrder` entity definition.** ID, tenant-scoped, FK to property + (often) asset + (often) maintenance source, vendor assignee, status (state machine below), priority, opened_at, expected_completion, actual_completion, total_cost, completion_notes.
2. **`WorkOrder` state machine.**
   ```
   Draft → Scoped → AssignedToVendor → AppointmentProposed →
     AppointmentConfirmed → InProgress → AwaitingSignOff →
     Completed → Invoiced → Paid → Closed
   (with side branches: Canceled, OnHold, Disputed)
   ```
   State transitions are first-class events; each emits to kernel-audit per ADR 0049.
3. **`WorkOrderAppointment` child entity.** Time slot, location (which property/unit), tenant-access-coordination state (notified, confirmed, declined, no-response), iCal export representation. Appointment slot booking is **CP-class** (no double-booking the same vendor at the same time).
4. **`WorkOrderSource` polymorphic FK.** A work order is created from one of: `MaintenanceRequest` (tenant-submitted), `InspectionFinding` (annual inspection deficiency), `Manual` (owner-initiated), `RecurringSchedule` (HVAC service every 6mo).
5. **`WorkOrderEntryNotice` entity.** Right-of-entry notice record bound to an appointment; captures notification sent_at, channel, recipient, content (templated, versioned, content-hashed per same mechanic as signed leases), tenant-acknowledgement state. Audit-logged via ADR 0049.
6. **`WorkOrderCompletionAttestation` entity.** Vendor sign-off (vendor's attestation: "I completed the work as scoped"); optional owner counter-sign (owner's attestation: "I verify completion"); optional tenant counter-sign (rare; only for tenant-acknowledged repairs). Each attestation is a signature event per signatures intake.
7. **`blocks-work-orders` package.** New persistent block; `ISunfishEntityModule` registration; persistence via `foundation-persistence`.
8. **Anchor + Bridge + iOS surfaces.** Owner cockpit view (work-order list + detail); vendor magic-link page (status updates + photo upload + sign-off); iOS field-app entry creation (open work order from on-site inspection finding).
9. **CP/AP classification per record class.** `WorkOrder` itself is AP (multi-actor concurrent updates safe). `WorkOrderAppointment` slot booking is CP (lease-protected to prevent double-booking). `WorkOrderEntryNotice` is append-only AP.

### Out of scope (this intake — handled elsewhere)

- Vendor identity → [`property-vendors-intake-2026-04-28.md`](./property-vendors-intake-2026-04-28.md)
- Multi-party communication threads → [`property-messaging-substrate-intake-2026-04-28.md`](./property-messaging-substrate-intake-2026-04-28.md)
- Signature mechanism (PencilKit + content-hash binding) → [`property-signatures-intake-2026-04-28.md`](./property-signatures-intake-2026-04-28.md)
- Right-of-entry **policy** (which states require what notice) → captured in new "Right-of-entry compliance" ADR; this intake just consumes the policy
- Receipt / invoice ingestion → [`property-receipts-intake-2026-04-28.md`](./property-receipts-intake-2026-04-28.md)
- Payment processing → ADR 0051 in Phase 2 commercial intake
- Public listing / inquiry → [`property-leasing-pipeline-intake-2026-04-28.md`](./property-leasing-pipeline-intake-2026-04-28.md)

### Explicitly NOT in scope (deferred)

- Bidding workflow with multi-vendor quote comparison — Phase 2 commercial intake's "Repair workflow basics" handles 3-quote flow; this intake assumes a vendor is already chosen
- Recurring scheduled maintenance (HVAC service every 6mo) automation — `RecurringSchedule` source type is reserved but the scheduler itself is Phase 2.3
- Vendor performance scoring / vendor leaderboard — `VendorPerformanceRecord` events are emitted from work-order completion (per vendors intake) but UX surface is Phase 2.2

---

## Affected Sunfish Areas

| Layer | Item | Change |
|---|---|---|
| Foundation | `foundation-persistence` | New entity registration |
| Foundation | `Foundation.Macaroons` | Capability: vendor magic-link work-order page authenticates via short-lived macaroon |
| Foundation | `kernel-cp` (lease coordination) | Appointment slot booking uses distributed lease per ADR 0028 / paper §6.3 |
| Blocks | `blocks-work-orders` (new) | Primary deliverable |
| Blocks | `blocks-vendors` (sibling) | FK consumer |
| Blocks | `blocks-properties` (sibling) | FK consumer |
| Blocks | `blocks-assets` (sibling) | FK consumer (often) |
| Blocks | `blocks-maintenance` | Reframed as a *source* of work orders, not a peer entity |
| Blocks | `blocks-inspections` (sibling) | Inspection findings emit work-order draft events |
| Bridge | Vendor magic-link work-order page | Same Bridge surface family as vendor onboarding form, leasing-pipeline pages |
| ADRs | New "Work-order domain model" ADR | CP/AP classification, state machine, source polymorphism |
| ADRs | New "Right-of-entry compliance" ADR | Entry-notice rules per state, template versioning |
| ADRs | ADR 0049 (audit-trail substrate) | Work-order state transitions, entry notices, completion attestations are first-class audit records |
| ADRs | ADR 0028 (CRDT engine) | Confirms AP work-order with CP appointment slot |
| ADRs | ADR 0043 (threat model) | Vendor identity boundary for magic-link page access |

---

## Acceptance Criteria

- [ ] `WorkOrder`, `WorkOrderAppointment`, `WorkOrderSource` (polymorphic), `WorkOrderEntryNotice`, `WorkOrderCompletionAttestation` entities defined; full XML doc; ADR 0014 adapter parity
- [ ] State machine implemented with explicit transition events; invalid transitions rejected; each transition emits to ADR 0049 audit trail
- [ ] Appointment slot booking uses CP-class lease per ADR 0028; double-booking prevented; lease timeout / release behavior tested
- [ ] Right-of-entry notice generated from versioned template; content-hashed; sent via messaging substrate; tenant-acknowledgement tracked
- [ ] Vendor magic-link work-order page (Bridge-served): vendor sees work-order detail, can update status, upload photos, request signature, sign-off
- [ ] Owner cockpit work-order list + detail (Blazor + React adapter parity)
- [ ] iOS field-app: create work order from on-site inspection finding
- [ ] kitchen-sink demo: full lifecycle (draft → completed → paid) with vendor + tenant + owner participants
- [ ] apps/docs entry covering work-order lifecycle + state machine
- [ ] New ADR ("Work-order domain model") accepted
- [ ] New ADR ("Right-of-entry compliance") accepted

---

## Open Questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-W1 | Is `WorkOrder` itself AP or CP? Concurrent updates by owner + vendor + tenant — likely AP with LWW for non-state fields and explicit lease for state transitions. | Stage 02. Research recommends: AP for entity, CP-via-lease for state transitions, CP-via-lease for appointment slot. |
| OQ-W2 | Polymorphic source FK: discriminator column + nullable FKs vs. event-sourced "OpenedFrom" event? | Stage 03 package design. Event-sourced is more aligned with kernel-audit substrate; recommend that. |
| OQ-W3 | Recurring schedules — `RecurringSchedule` source reserved here, scheduler in Phase 2.3. Does the work-order entity need any forward-compatible fields now (parent_recurring_id, etc.)? | Stage 02 — yes; reserve `parent_recurrence_id` nullable to avoid an api-change later. |
| OQ-W4 | Tenant access coordination state machine — needs to handle "no response" (tenant didn't reply to notice). What's the auto-advance policy after legally-required notice period elapses? | Stage 02 — depends on right-of-entry policy ADR. Recommend: state goes "notice-given" → after legal-period elapses → "right-to-enter" without requiring tenant ack. |
| OQ-W5 | Magic-link page security: macaroon TTL, refresh policy, abuse rate-limiting. | Stage 02. Cross-reference Vendors intake OQ-V3. |
| OQ-W6 | Owner counter-sign on completion: required for all work orders, or only over a threshold ($500+, capital improvements)? | Stage 02 — config-driven; default required for all in Phase 2.1g. |
| OQ-W7 | Disputed state — what triggers it, who can transition into/out of it? Tenant dispute (work not done) vs vendor dispute (scope creep) vs owner dispute (quality). | Stage 02. Recommend: any party can flag; only owner can resolve. |
| OQ-W8 | Per-jurisdiction right-of-entry rules — Phase 2 commercial intake doesn't pin BDFL's property states. Confirm scope before Phase 2.1g. | Cluster INDEX OQ7. |

---

## Dependencies

**Blocked by:**
- Properties (sibling intake) — work-order FK
- Vendors (sibling intake) — work-order assignee FK
- Messaging substrate (sibling intake) — multi-party threads, entry notice delivery
- Signatures (sibling intake) — completion attestations
- ADR 0049 (already drafted/scaffolded) — audit substrate
- ADR 0028 (already accepted) — CP-class lease primitive
- New "Work-order domain model" ADR
- New "Right-of-entry compliance" ADR

**Blocks:**
- Phase 2 commercial intake's "Repair workflow basics" — work-order is the canonical container for repair workflow
- Inspections intake's "InspectionFinding emits work-order draft event" integration
- Maintenance request intake's "tenant maintenance request opens work order" integration

**Cross-cutting open questions consumed:** OQ1, OQ7, OQ9 from INDEX.

---

## Pipeline Variant Choice

`sunfish-feature-change` for the entity work, but **escalates to `sunfish-api-change`** because:
- Reframes existing maintenance-item modeling from a peer entity into a work-order *source* (breaking for any consumer)
- Introduces a CP-class lease usage on appointment slots that touches kernel-cp contracts
- Phase 2 commercial intake's "Repair workflow basics" deliverable now flows through this entity

Stage 02 (architecture) and Stage 03 (package design) are mandatory and not skippable. ADRs land before Stage 06 (build).

---

## Cross-references

- Parent: [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
- Phase 2 commercial: [`phase-2-commercial-mvp-intake-2026-04-27.md`](./phase-2-commercial-mvp-intake-2026-04-27.md)
- Sibling intakes: Properties, Vendors, Messaging Substrate, Signatures, Inspections, Receipts, iOS App, Owner Cockpit
- ADR 0008 (multi-tenancy), ADR 0015 (entity registration), ADR 0028 (CRDT engine), ADR 0043 (threat model), ADR 0049 (audit substrate)
- Paper §6.3 (Distributed Lease Coordination) for CP-class semantics
- New ADRs (drafted post-this-intake): "Work-order domain model", "Right-of-entry compliance"

---

## Sign-off

Research session — 2026-04-28
