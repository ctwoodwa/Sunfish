# Intake Index — Property-Operations Vertical (Phase 2 Cluster)

**Status:** `design-in-flight` — Stage 00 cluster index. **sunfish-PM: do not implement against any cluster intake until its individual `Status:` flips to `ready-to-build` and a hand-off file appears in `icm/_state/handoffs/`.**
**Status owner:** research session
**Date:** 2026-04-28 (revised 2026-04-28 — naming convention codified + disposition reconciled)
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (cross-network transport → iOS access → field capture → signatures → assets → mileage → leasing pipeline → vendor coordination)
**Pipeline variant:** N/A (index file; per-domain intakes select their own variants)
**Parent:** [`phase-2-commercial-mvp-intake-2026-04-27.md`](./phase-2-commercial-mvp-intake-2026-04-27.md) — this cluster is a deepening of Phase 2 commercial scope.

> **Revision note 2026-04-28:** UPF review of cluster naming conventions ([`../../07_review/output/property-ops-cluster-naming-upf-review-2026-04-28.md`](../../07_review/output/property-ops-cluster-naming-upf-review-2026-04-28.md)) + cluster-vs-existing reconciliation report ([`../../07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md`](../../07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md)) materially reshape this index. Read both review documents before drafting any new hand-off. Key shifts: (1) cluster's "Asset" entity renames to "Equipment" per Rule 4 (avoids overload of `Sunfish.Foundation.Assets.Common.EntityId`); (2) workstreams #18 Vendors, #19 Work Orders, #25 Inspections, #27 Leases reframe from "new block" to "extension to existing block"; (3) ADR 0053 (work-order) amended with state-machine composition note; (4) `blocks-property-assets` rename to `blocks-property-equipment` queued (sunfish-PM hand-off at [`../../_state/handoffs/property-equipment-rename-handoff.md`](../../_state/handoffs/property-equipment-rename-handoff.md)).

---

## Naming convention (canonical 5 rules per UPF review)

1. **Audit before naming** — `ls packages/ | grep -E "^blocks-|^foundation-"` collision check
2. **Property-ops siblings use `blocks-property-*` prefix** when collision exists (root `blocks-properties` unprefixed; already shipped)
3. **Extend over parallel** when existing block covers ≥50% of scope
4. **Cluster physical-equipment entity must not overload foundation-tier "Asset"** — use **`Equipment`** (default) or namespace-qualify (alternative)
5. **"Tenant" disambiguation** — multi-tenancy `Tenant` ≠ property-management lease-holder; use `Party` + `PartyKind.Tenant`

Full rationale in the UPF review document.

## Disposition table (per-workstream; revised 2026-04-28)

| WS# | Module | Original | Revised | Existing block |
|---|---|---|---|---|
| 17 | Properties | NEW | **NEW ✅ shipped** | none |
| 18 | Vendors | NEW | **EXTEND** | `blocks-maintenance.Vendor` |
| 19 | Work Orders | NEW | **EXTEND** | `blocks-maintenance.WorkOrder` |
| 20 | Messaging Substrate | NEW SUBSTRATE | NEW SUBSTRATE | none |
| 21 | Signatures | NEW SUBSTRATE | NEW SUBSTRATE | none |
| 22 | Leasing Pipeline | NEW | NEW + COMPOSE | composes `blocks-leases` + `blocks-scheduling` |
| 23 | iOS App | NEW | NEW | none |
| 24 | Property-Equipment (was Property-Assets) | NEW | **NEW (rename per Rule 4)** | naming-only collision (different domain) |
| 25 | Inspections | NEW | **EXTEND** | `blocks-inspections` |
| 26 | Property-Receipts | NEW | NEW | none |
| 27 | Leases | NEW | **EXTEND** | `blocks-leases.Lease` |
| 28 | Public Listings | NEW | NEW | none |
| 29 | Owner Cockpit | NEW | COMPOSE | distributes across all blocks |
| 30 | Mesh-VPN | NEW SUBSTRATE | NEW SUBSTRATE | none |

---

## Purpose

The Phase 2 commercial intake (2026-04-27) scoped the BDFL's accounting / payment / reconciliation / tax-prep cycle. A subsequent multi-turn architectural conversation on 2026-04-28 surfaced an entire **field-operations and leasing-pipeline vertical** that sits *alongside* the accounting cycle and is equally Phase 2-bound. This cluster captures that vertical as 14 per-domain intakes plus this index.

The cluster delivers, end-to-end:

- An iOS field-capture app for the BDFL (and spouse / contractor / bookkeeper as their roles expand) to inventory assets, capture receipts, conduct inspections, sign leases on location, log mileage, run move-in/move-out walkthroughs.
- A public-facing leasing pipeline (listings, inquiries, criteria, applications, showings, accept/decline) hosted on Bridge.
- A vendor-coordination spine (work orders, multi-party messaging threads, magic-link vendor portal) that ties maintenance, inspections, signatures, payments, and audit-trail together.
- Bidirectional messaging substrate (egress + ingress, email + SMS, thread/work-order routing) replacing the original outbound-only ADR 0052 scope.

---

## Cluster contents (14 per-domain intakes)

| # | Intake | File | Status |
|---|---|---|---|
| **Spine** |
| 1 | Properties | [`property-properties-intake-2026-04-28.md`](./property-properties-intake-2026-04-28.md) | drafted |
| 2 | Vendors | [`property-vendors-intake-2026-04-28.md`](./property-vendors-intake-2026-04-28.md) | drafted |
| 3 | Work Orders (coordination spine) | [`property-work-orders-intake-2026-04-28.md`](./property-work-orders-intake-2026-04-28.md) | drafted |
| 4 | Bidirectional Messaging Substrate | [`property-messaging-substrate-intake-2026-04-28.md`](./property-messaging-substrate-intake-2026-04-28.md) | drafted |
| **Cross-cutting** |
| 5 | Signatures + Document Binding | [`property-signatures-intake-2026-04-28.md`](./property-signatures-intake-2026-04-28.md) | drafted |
| 6 | Leasing Pipeline + Fair Housing | [`property-leasing-pipeline-intake-2026-04-28.md`](./property-leasing-pipeline-intake-2026-04-28.md) | drafted |
| 7 | iOS Field-Capture App | [`property-ios-field-app-intake-2026-04-28.md`](./property-ios-field-app-intake-2026-04-28.md) | drafted |
| **Domain modules** |
| 8 | Assets (incl. Vehicle subtype + mileage) | [`property-assets-intake-2026-04-28.md`](./property-assets-intake-2026-04-28.md) | drafted |
| 9 | Inspections (incl. move-in/out + condition assessments) | [`property-inspections-intake-2026-04-28.md`](./property-inspections-intake-2026-04-28.md) | drafted |
| 10 | Receipts | [`property-receipts-intake-2026-04-28.md`](./property-receipts-intake-2026-04-28.md) | drafted |
| 11 | Leases | [`property-leases-intake-2026-04-28.md`](./property-leases-intake-2026-04-28.md) | drafted |
| 12 | Public Listings surface | [`property-public-listings-intake-2026-04-28.md`](./property-public-listings-intake-2026-04-28.md) | drafted |
| 13 | Owner Web Cockpit | [`property-owner-cockpit-intake-2026-04-28.md`](./property-owner-cockpit-intake-2026-04-28.md) | drafted |

**Adjacent (separate from cluster but same conversation):**
| Mesh VPN / Cross-Network Transport | [`mesh-vpn-cross-network-transport-intake-2026-04-28.md`](./mesh-vpn-cross-network-transport-intake-2026-04-28.md) | drafted — kernel-tier transport ADR; benefits whole architecture, not just property-ops; consumed by iOS App in Phase 2.3 |

---

## ADR cluster (new + amended)

The cluster requires the following ADR work. Many are flagged in individual intakes; pinned here for cross-reference.

### New ADRs

| Working ID | Title | Driven by intake | Notes |
|---|---|---|---|
| ADR 0051 | Foundation.Integrations.Payments | Receipts, Work Orders | Existing Phase 2 intake placeholder — promote to drafted |
| ADR 0052 (reframe) | Bidirectional messaging substrate | Messaging Substrate | Reframes from outbound-only to egress + ingress + thread model |
| ADR 005X | Electronic signature capture & document binding | Signatures | UETA-aligned; PencilKit + content-hash; CryptoKit / Foundation.Recovery dependency |
| ADR 005X | Vendor onboarding posture | Vendors | Lightweight (magic-link / SMS) by default; account upgrade optional |
| ADR 005X | Work-order domain model | Work Orders | Coordination unit; CP/AP classification per record class |
| ADR 005X | Leasing pipeline + Fair Housing compliance | Leasing Pipeline | Pipeline state machine; criteria document versioning; jurisdiction-policy hook |
| ADR 005X | Public listing surface (Bridge-served) | Public Listings | Trust posture for anonymous public input; rate-limiting; SEO; capability promotion |
| ADR 005X | Right-of-entry compliance | Work Orders / Leasing | Multi-state notice rules, audit-trail format, template versioning |
| ADR 005X | Mobile location capture posture (deferred) | iOS App (Phase 2.1c) | Only if GPS auto-tracked mileage proceeds |

### Amendments

| ADR | Amendment | Driven by intake |
|---|---|---|
| ADR 0028 (CRDT engine selection) | "Mobile reality check" — YDotNet has no Swift port; Phase 2.1 ships append-only events, not full CRDT, on iOS | iOS App |
| ADR 0043 (unified threat model) | Public-input boundary; capability promotion (anonymous → prospect → applicant); vendor identity boundary | Public Listings, Vendors |
| ADR 0046 (key-loss recovery) | Outstanding signature commitments survive operator key rotation; recovery affects new-signature capability only | Signatures |
| ADR 0049 (audit-trail substrate) | Confirm signature events, vendor entry notices, criteria-sent events are first-class audit records | Signatures, Work Orders |

---

## Phase mapping

Phase 2 commercial scope (parent intake) is the umbrella. This cluster defines internal phase ordering for the property-operations vertical:

| Sub-phase | Scope | Gates |
|---|---|---|
| **2.0 (architectural)** | ADRs 0051, 0052 (reframed), signature ADR, leasing/FHA ADR, public-listing ADR, vendor onboarding ADR, work-order ADR, ADR 0043 addendum drafted + accepted. ADR 0028 mobile amendment. ADR 0046 amendment. | Workstreams #2 (kernel-audit Tier 1), #14 (provider-neutrality gate), #15 (foundation-recovery split) closed |
| **2.1a** | iOS field-capture: receipts + asset capture (OCR) + inspection photos + manual mileage | 2.0 ADRs in flight; Bridge blob ingest API spec |
| **2.1b** | Signatures + structured inspections (asset condition assessments) | Signature ADR accepted; Foundation.Recovery shipped |
| **2.1c (optional)** | GPS-tracked mileage | Mobile location ADR drafted |
| **2.1d** | Public listings + inquiry intake + outbound criteria sending | Public-listing ADR + ADR 0052 reframe accepted |
| **2.1e** | Showing scheduling + leasing pipeline state machine | Leasing/FHA ADR accepted |
| **2.1f** | Move-in/out checklist wizards + security-deposit reconciliation hooks | Inspections + payments domains live |
| **2.1g** | Vendor coordination: work orders, multi-party threads, magic-link portal, vendor inbound parsing | Messaging substrate + work-order ADR accepted |
| **2.2** | Read flows + dashboards + tax-advisor views | All 2.1 modules functional |
| **2.3** | Reporting (annual tax package, maintenance forecast, inspection cadence, vendor 1099) | 2.2 dashboards stable |

---

## Cross-cutting open questions

These cut across multiple per-domain intakes; tracked once here, referenced by individual intakes.

| ID | Question | Affects |
|---|---|---|
| OQ1 | Multi-actor permissions matrix — full RBAC at fidelity Sunfish doesn't currently model in detail; bookkeeper, tax advisor, contractor/vendor, leaseholder, prospect/applicant, spouse/co-owner each have different read+write surfaces. Capability graph + macaroon-driven, but the *catalog* of permissions per role per record-class needs explicit listing. | All cluster intakes |
| OQ2 | CRDT-on-mobile — YDotNet has no Swift port. Phase 2.1 ships append-only events on iOS (no rich CRDT merge); Phase 3+ is full-peer. Confirm append-only is sufficient for receipt/asset/inspection/signature/mileage flows. | iOS App, all field-captured domains |
| OQ3 | Bridge blob ingest API spec — resumable uploads (tus.io vs S3 multipart), idempotency by content hash, virus scanning hook, quota tracking. | iOS App, Receipts, Inspections, Assets, Signatures |
| OQ4 | MAUI-iOS vs SwiftUI lock-in — SwiftUI native recommended for camera/PencilKit/background URLSession, but locks Anchor and iOS field-app to two codebases. Confirm before Phase 2.1a starts. | iOS App, ADR 0048 |
| OQ5 | Vendor identity: account-required vs anonymous-magic-link. Recommended lightweight default; specify upgrade path for high-touch vendors. | Vendors, Messaging Substrate |
| OQ6 | Inbound channel parsing: Mailgun vs Postmark Inbound vs SES Inbound for email; Twilio for SMS reply. Selection criteria, abstraction layer. | Messaging Substrate |
| OQ7 | Right-of-entry jurisdiction policy: which states do we ship policy for in Phase 2.1g? BDFL's properties are in {state(s) — to confirm}; broader coverage in 2.3. | Leasing Pipeline, Work Orders |
| OQ8 | Criteria document versioning: PDF generation on Bridge or on iPad? Where does the canonical version live? Same content-binding mechanic as signatures. | Leasing Pipeline, Signatures |
| OQ9 | Work-order CP/AP classification — `WorkOrder` itself is likely AP (multiple actors update concurrently); `appointment slot booking` is CP (no double-booking). Confirm split per record-class per paper §6.3. | Work Orders |
| OQ10 | Vendor 1099-NEC year-end reporting: W-9 capture during onboarding, vendor TIN storage posture (sensitive PII), payment ledger aggregation by vendor by tax year. | Vendors, Receipts, Phase 2.3 |
| OQ11 | Public-listing rate-limiting and abuse posture: what's the inquiry submission rate cap? Captcha? Email-verification gate before "applicant" capability is granted? | Public Listings, ADR 0043 |
| OQ12 | Calendar integration depth: iCal export only (Phase 2.1e MVP) vs two-way Google/Apple/Outlook integration (later). | Showings (in Leasing Pipeline) |
| OQ13 | Existing toolchain replacement scope: Phase 2 commercial intake names Wave Accounting, Rentler.com, bank shared-access PDFs as targets. This cluster also implies replacing any current property-management spreadsheet, vendor-list document, lease-document folder, etc. Confirm BDFL's current tooling baseline before 2.1a starts. | All cluster intakes |

---

## Drafting status

All 14 cluster intakes + INDEX + adjacent mesh-VPN intake **drafted** in user-requested flush 2026-04-28. Each follows the format established by `phase-2-commercial-mvp-intake-2026-04-27.md` and `tenant-id-sentinel-pattern-intake-2026-04-28.md`.

---

## Next steps (research session, post-flush)

1. **User review** — full review of all 15 drafts. Edits land in-place; cluster cross-references update if scope shifts.
2. **Stage 01 Discovery** — once user approves the cluster, pick one or two intakes to advance to `01_discovery`. Recommended starting points: **Properties** (spine #1, no upstream blockers) and **Bidirectional Messaging Substrate** (its provider-neutrality exercise validates the in-flight ADR-0013 enforcement gate). Both can run in parallel.
3. **ADR drafting** — the ADR cluster section above lists 8 new ADRs and 4 amendments. These should land before the corresponding intakes reach Stage 06. Recommended drafting order: messaging substrate ADR (0052 reframe), work-order ADR, signatures ADR, leasing/FHA ADR, vendor onboarding ADR, public listing ADR, right-of-entry ADR. Mesh-VPN ADR is independent (not on Phase 2.1 critical path).
4. **Sequence with workstreams already in flight** — Workstreams #2 (kernel-audit Tier 1), #14 (provider-neutrality gate), #15 (foundation-recovery split) are all prerequisites for various cluster intakes; their closure unblocks the cluster's Phase 2.0 architectural sub-phase.

---

## Sign-off

Research session — 2026-04-28

Cluster captured from multi-turn conversation. All sessions: treat every intake here as `design-in-flight` — sunfish-PM does not implement any of them until individually flipped to `ready-to-build` with a hand-off file in `icm/_state/handoffs/`.
