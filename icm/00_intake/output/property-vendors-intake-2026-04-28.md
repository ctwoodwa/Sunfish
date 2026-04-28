# Intake Note — Vendors Domain Module

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build` and a hand-off file appears in `icm/_state/handoffs/`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turn 7 — vendor coordination requirements).
**Pipeline variant:** `sunfish-feature-change`
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
**Position in cluster:** Spine #2 — vendor as new actor class; identity backbone for work-order coordination.

---

## Problem Statement

Property operations require ongoing relationships with third-party service providers — plumbers, electricians, HVAC technicians, landscapers, locksmiths, cleaners, painters, inspectors. The Phase 2 commercial intake names "contractor" as one of the multi-actor classes alongside bookkeeper, tax advisor, and leaseholder. Productizing that role surfaces a pattern that doesn't fit the existing actor model:

- A **bookkeeper** is invited via Anchor capability grant, installs Anchor, runs reconciliation in Anchor with a trimmed feature set (per ADR 0032).
- A **tax advisor** receives an annual export by email; never installs Anchor.
- A **leaseholder** stays on Rentler in Phase 2 (deferred to Phase 3 portal); receives outbound communications via email/SMS.
- A **vendor** is none of these. Vendors are not employees and won't install owner software, but they need ongoing structured interaction (multiple work orders per year, multi-party communication threads, scheduled appointments, payment delivery, 1099-NEC reporting).

The right pattern: vendors are **first-class identity records** in the kernel (with name, contact info, W-9, payment preferences, service categories, performance history) but interact via **lightweight channels** (email + SMS magic-link work-order pages) by default, with optional account upgrade to a logged-in vendor portal for high-touch vendors. This intake captures vendor identity and the lightweight interaction posture; the work-order coordination spine that *uses* vendors lives in [`property-work-orders-intake-2026-04-28.md`](./property-work-orders-intake-2026-04-28.md).

## Scope Statement

### In scope (this intake)

1. **`Vendor` entity definition.** Identity (legal name + DBA), contact info (email, SMS, mailing address), W-9 capture (TIN encrypted at rest), payment preference (check / ACH / Zelle), service categories (tag list), insurance / license metadata (optional), notes, status (active / archived).
2. **`VendorContact` child entity.** A vendor may have multiple contact people (owner of company + dispatcher + on-site tech). Each contact has its own email/SMS, role label, and "primary for this property" override.
3. **`VendorPerformanceRecord` event log.** Append-only event log: `Hired`, `JobCompleted`, `JobNoShow`, `JobLate`, `RatingAdjusted`, `Archived`. Sourced from work-order completion events; surfaced for vendor-selection UX.
4. **Lightweight onboarding flow.** Owner adds vendor in Anchor → vendor receives email asking for W-9 + ACH info via secure web form (Bridge-hosted) → form returns encrypted to kernel → vendor record activated. No Sunfish account needed.
5. **`blocks-vendors` package.** New persistent block; `ISunfishEntityModule` registration per ADR 0015; persistence via `foundation-persistence`.
6. **Capability gradient.** Anonymous (vendor before W-9 returned) → vendor (active, can receive magic-link work orders) → vendor-with-portal (optional, account-bound, can log into Bridge-hosted portal). ADR 0043 trust-model addendum captures the boundary.
7. **W-9 / TIN protection.** TIN is sensitive PII (SSN-class for sole proprietors). Encrypted at rest under tenant key. Access requires audit-logged capability (only owner + tax advisor read). Field-level encryption distinct from full-record encryption.

### Out of scope (this intake — handled elsewhere)

- Work-order assignment to vendors → [`property-work-orders-intake-2026-04-28.md`](./property-work-orders-intake-2026-04-28.md)
- Vendor communication threads (multi-party messaging) → [`property-messaging-substrate-intake-2026-04-28.md`](./property-messaging-substrate-intake-2026-04-28.md)
- Vendor magic-link web pages on Bridge → [`property-public-listings-intake-2026-04-28.md`](./property-public-listings-intake-2026-04-28.md) (same surface family — Bridge-served lightweight pages for non-account users)
- Vendor payment processing → ADR 0051 (Foundation.Integrations.Payments) in Phase 2 commercial intake
- Vendor 1099-NEC year-end report generation → Phase 2.3 (Phase 2 commercial intake's `blocks-tax-reporting`)

### Explicitly NOT in scope (deferred to later phase or different cluster)

- Vendor marketplace / vendor discovery (community-shared vendor lists across BDFL tenants) — Phase 4+
- Vendor bidding workflow (3-quote comparison) — partially covered by Phase 2 commercial intake's "Repair workflow basics"; this intake assumes vendor is already chosen
- Vendor insurance certificate verification automation — manual upload + reminder only in Phase 2

---

## Affected Sunfish Areas

| Layer | Item | Change |
|---|---|---|
| Foundation | `foundation-multitenancy` | Vendors are tenant-bound (each tenant has its own vendor list; cross-tenant vendor sharing is Phase 4+) |
| Foundation | `foundation-persistence` | New entity registration via ADR 0015 |
| Foundation | (new field-encryption helper if needed) | TIN field-level encryption — may surface a small Foundation utility addition |
| Blocks | `blocks-vendors` (new) | Primary deliverable |
| Blocks | `blocks-work-orders` (intake sibling) | FK consumer |
| Blocks | `blocks-tax-reporting` (existing) | Consumes vendor + W-9 + payment ledger for 1099-NEC |
| Bridge | Magic-link onboarding form (W-9 capture) | Bridge-hosted; same surface family as vendor work-order portal pages |
| ADRs | ADR 0008 (multi-tenancy) | Vendor is tenant-bound |
| ADRs | ADR 0015 (module-entity registration) | Vendor registers as entity module |
| ADRs | ADR 0043 (threat model) | Addendum: vendor identity boundary; capability promotion |
| ADRs | ADR 0049 (audit-trail substrate) | Vendor onboarding events, W-9 access events, performance record events are audit-logged |
| ADRs | New "Vendor onboarding posture" ADR | Lightweight (magic-link) by default; account upgrade optional |

---

## Acceptance Criteria

- [ ] `Vendor`, `VendorContact`, `VendorPerformanceRecord` entities defined; full XML doc; ADR 0014 adapter parity
- [ ] Lightweight onboarding flow end-to-end: owner adds vendor → vendor email → secure W-9 form → vendor activated
- [ ] TIN field-level encryption with audit-logged read access
- [ ] CRUD surface in Anchor; vendor picker in iOS app (read-only); vendor list view in Bridge owner cockpit
- [ ] Adapter parity tests: Blazor + React vendor list and detail
- [ ] Magic-link onboarding form passes basic security review (rate-limit, CSRF, abuse posture per ADR 0043 addendum)
- [ ] kitchen-sink demo: 3 vendors with mixed states (active without W-9, active with W-9, archived)
- [ ] apps/docs entry covering the vendor domain + onboarding flow + W-9 protection posture
- [ ] One-shot import tool from CSV (existing vendor list) under `tooling/`
- [ ] New ADR ("Vendor onboarding posture") accepted

---

## Open Questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-V1 | Vendor identity uniqueness — same plumber works on properties across BDFL's 4 tenant LLCs. Is the vendor record duplicated per tenant or shared across tenants the operator controls? | Stage 02 design. Recommend per-tenant for Phase 2 (simpler); cross-tenant vendor sharing as Phase 4+ feature. |
| OQ-V2 | TIN storage encryption: per-tenant key vs per-record key vs HSM-backed? | Stage 02. Per-tenant key consistent with rest of Foundation.Recovery posture; HSM for high-volume vendors only. |
| OQ-V3 | Optional vendor portal upgrade: same identity model as future Phase 3 leaseholder portal, or distinct? | Stage 02. Recommend shared "third-party portal" infrastructure; distinct capability sets per role. |
| OQ-V4 | Vendor performance scoring: numerical rating, free-text only, or structured criteria (timeliness, quality, communication)? | Stage 03 package design. Start free-text + on-time/no-show flags; structured scoring later. |
| OQ-V5 | Insurance / license metadata enforcement: hard block on assignment if license expired vs warning only? | Stage 02 — warning only in Phase 2 (BDFL's risk tolerance); hard-block as configurable policy in Phase 3. |
| OQ-V6 | Vendor communication channel default: email, SMS, or both? Does vendor self-select during onboarding? | Stage 03. Recommend vendor self-selects during onboarding form; owner can override per work order. |

---

## Dependencies

**Blocked by:**
- ADR 0049 (audit-trail substrate) acceptance — needed for audit-logged W-9 access and performance events. (Already drafted; PR #190 merged.)
- Foundation.Recovery split (workstream #15, ready-to-build) — needed for the encryption posture if field-level encryption uses recovery primitives.

**Blocks:**
- [`property-work-orders-intake-2026-04-28.md`](./property-work-orders-intake-2026-04-28.md) — vendors are work-order assignees
- Phase 2.3 vendor 1099-NEC reporting

**Cross-cutting open questions consumed:** OQ1 (permissions), OQ5 (vendor identity model), OQ10 (vendor 1099) from INDEX.

---

## Pipeline Variant Choice

`sunfish-feature-change` — new feature-block. Adapter parity required. kitchen-sink demo + apps/docs mandatory.

The "Vendor onboarding posture" ADR work runs in parallel; once accepted, this intake's Stage 02 references it.

---

## Cross-references

- Parent: [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
- Phase 2 commercial: [`phase-2-commercial-mvp-intake-2026-04-27.md`](./phase-2-commercial-mvp-intake-2026-04-27.md) — names contractor as actor; "Repair workflow basics" Phase 2 deliverable
- Properties: [`property-properties-intake-2026-04-28.md`](./property-properties-intake-2026-04-28.md)
- Work Orders: [`property-work-orders-intake-2026-04-28.md`](./property-work-orders-intake-2026-04-28.md)
- Messaging substrate: [`property-messaging-substrate-intake-2026-04-28.md`](./property-messaging-substrate-intake-2026-04-28.md)
- ADR 0008, ADR 0015, ADR 0043, ADR 0049
- New ADR (drafted post-this-intake): "Vendor onboarding posture"

---

## Sign-off

Research session — 2026-04-28
