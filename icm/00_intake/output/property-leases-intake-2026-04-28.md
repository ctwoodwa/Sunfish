# Intake Note — Leases Domain Module (revised: extension to `blocks-leases`)

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build`.**
**Status owner:** research session
**Date:** 2026-04-28 (revised 2026-04-28 per cluster-vs-existing reconciliation)
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turns 4, 6 — lease execution, leasing-pipeline handoff).
**Pipeline variant:** `sunfish-feature-change` (extension; not new package)
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)

> **Revision note 2026-04-28:** Disposition reframed from "new block" to **"extension to existing `packages/blocks-leases/`"**. Audit revealed `blocks-leases` already ships `Lease` + `LeaseId` + `LeasePhase` + `Document` + `DocumentId` + `Party` + `PartyId` + `PartyKind` (tenant/landlord/manager/guarantor) + `Unit` (with `EntityId`). Existing block self-describes as "thin first pass; full workflow surface (signature, execution, renewal, termination) deferred" — **which is exactly the cluster's contribution.** Cluster's deltas (`LeaseDocumentVersion` for content-hash-bound versioning + `Lease.SignatureEventRef` per ADR 0054 + renewal/termination state-machine transitions + LeaseHolderRole multi-tenant lease support) become **extensions** to that block, picking up where the original author left off. See [`../../07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md`](../../07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md) workstream #27 row + Stage 02 implementation must read existing `LeasePhase` enum values before drafting state-machine extensions. Existing `Party` + `PartyKind.Tenant` is the canonical lease-holder representation per UPF Rule 5; cluster artifacts must NOT introduce a top-level `Tenant` entity for property-management lease-holders.

---

## Problem Statement

A lease is the legal anchor of every tenancy: term, parties, rent, deposit, rules, signatures. It's also a versioned document — addenda, amendments, renewals all produce new versions. The current state (Phase 2 baseline) is that the BDFL's leases live in PDF form on Rentler; Phase 2 commercial intake explicitly defers Rentler portal replacement to Phase 3, but the **lease document itself** as a structured Sunfish entity is in scope for Phase 2 because:

- Leasing-pipeline state machine ends at LeaseSigned → handoff requires a Lease entity
- Move-in/move-out condition deltas are reconciled against the lease's deposit amount
- Vendor right-of-entry compliance varies based on lease terms (some leases pre-authorize entry for routine inspection)
- Tax-advisor reporting needs lease-by-lease rent income classification

This module provides the Lease entity, lease versioning, signature binding (per Signatures intake), term tracking, renewal events, and termination events.

## Scope Statement

### In scope

1. **`Lease` entity.** Tenant + FK Property + FK PropertyUnit + lease_holder_identities[] + start_date + end_date + monthly_rent + security_deposit + payment_due_day + late_fee_policy + lease_type (fixed-term | month-to-month) + status (drafted | signed | active | expired | terminated | renewed).
2. **`LeaseDocumentVersion` entity.** Versioned PDF + content-hash + section markers + signed_via_signature_event_ref. Each amendment / addendum / renewal creates a new LeaseDocumentVersion linked to the same Lease.
3. **`LeaseLifecycleEvent` log.** `Drafted`, `Signed`, `Activated`, `Amended`, `Renewed`, `Terminated`, `Expired`. Audit-logged per ADR 0049.
4. **`LeaseHolderRole` entity.** Multi-tenant lease support — primary tenant, co-tenant, occupant (non-tenant adult), guarantor. Each role's identity ref + role-specific terms.
5. **Lease document storage.** PDF-as-blob via foundation-persistence blob primitives; not stored as structured form fields. Lease *terms* (rent, deposit, dates) extracted as first-class fields; full document is canonical.
6. **`blocks-leases` package.**
7. **Renewal workflow.** N days before end_date → reminder → owner triggers renewal flow → new LeaseDocumentVersion created → routed through Signatures flow.
8. **Termination workflow.** Owner-initiated (per cause) or tenant-initiated; triggers move-out inspection (per Inspections intake); security-deposit reconciliation hook.
9. **Lease library (Phase 2 commercial intake's existing scope).** Standard lease templates per jurisdiction; instantiated when LeaseDrafted state reached in Leasing Pipeline.

### Out of scope

- Lease execution flow / signature mechanism → [`property-signatures-intake-2026-04-28.md`](./property-signatures-intake-2026-04-28.md)
- Rent collection / payment processing → ADR 0051 / Phase 2 commercial intake
- Move-in / move-out inspections → [`property-inspections-intake-2026-04-28.md`](./property-inspections-intake-2026-04-28.md)
- Lease holder portal (online rent pay, maintenance requests) → Phase 3 per Phase 2 commercial intake
- Eviction workflow → Phase 4+
- Lease language / clause customization UI → uses jurisdiction-aware templates from leasing-pipeline ADR

---

## Affected Sunfish Areas

- `blocks-leases` (new)
- `foundation-persistence`, ADR 0015, ADR 0049
- Signatures (consumer), Inspections (move-in/out trigger), Leasing Pipeline (LeaseDrafted handoff)

## Acceptance Criteria

- [ ] All entities defined; XML doc + adapter parity
- [ ] LeaseDocumentVersion content-hash binding integrates with Signatures intake
- [ ] Renewal workflow: reminder → renewal → new version → resign → activate
- [ ] Termination workflow: trigger → move-out inspection → deposit reconciliation hook
- [ ] kitchen-sink demo: full lease lifecycle on a sample property (drafted → signed → renewed → terminated)
- [ ] apps/docs entry covering lease lifecycle + multi-version model

## Open Questions

| ID | Question | Resolution |
|---|---|---|
| OQ-LE1 | Lease template authoring: code-as-template (Razor) vs content-managed templates per jurisdiction | Stage 02 — content-managed (Markdown + variable interpolation); jurisdiction picker; legal-reviewed |
| OQ-LE2 | Lease holder identity: shared identity model with prospects/applicants from Leasing Pipeline (carry forward Application data into Lease) | Stage 02 — yes; LeaseHolderRole references LeasingApplication on creation |
| OQ-LE3 | Co-tenant signature ordering / requirements | Stage 02 — all-required; signature event captures each tenant separately |
| OQ-LE4 | Lease amendment vs new version: what triggers a new LeaseDocumentVersion vs an in-place edit? | Stage 02 — any change requires new version + new signature(s); no in-place edits to signed leases |

## Dependencies

**Blocked by:** Properties, Signatures, Leasing Pipeline (handoff source)
**Blocks:** Inspections (move-in/out triggered by lease lifecycle), Phase 2 rent collection, Phase 3 portal

## Cross-references

- Sibling intakes: Properties, Signatures, Inspections, Leasing Pipeline, Owner Cockpit
- ADR 0015, ADR 0049

## Sign-off

Research session — 2026-04-28
