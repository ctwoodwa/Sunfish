---
sort_order: 26
number: 27
slug: leases-domain-extended
title: "Leases domain (cluster module) — **EXTENDED `blocks-leases`**"
status: "built"
status_cell: "`built` (all 7 phases shipped 2026-04-29 → 30; Party canonical retrofit follow-on shipped 2026-05-17 via PR #949 — local Party/PartyId/PartyKind [Obsolete]; canonical type swap + GetLeaseholderDisplaysAsync via IPartyReadModel)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM ✓"
reference_cell: "`icm/00_intake/output/property-leases-intake-2026-04-28.md` + `icm/_state/handoffs/property-leases-stage06-handoff.md`"
---

## Notes

**Built 2026-04-30.** Shipped via 5 PRs: Phase 1 (#293, LeasePhase transition table + state-machine guards via blocks-maintenance TransitionTable<TState>) → Phase 4 (#305, LeaseHolderRole + LeasePartyRole join per UPF Rule 5) → Phase 5 (#318, 9 LeaseXxx AuditEventType + LeaseAuditPayloadFactory + emission for 5 events; 3 Phase 2/3 events declared forward-compat at the time) → Phase 2+3 bundled (#365, LeaseDocumentVersion append-only versioning + per-party signatures + landlord attestation + AwaitingSignature → Executed transition guard with legacy bypass; wires the 3 forward-compat audit events) → Phase 6+7 (this PR, apps/docs/blocks/leases/document-versioning.md + signature-flow.md + ledger flip). 52/52 lease tests passing. All 9 LeaseXxx AuditEventType emit. Cross-package wiring: kernel-signatures.SignatureEventId (W#21 ✓) + Foundation.Taxonomy ITaxonomyResolver (W#31 ✓) + IAuditTrail/IOperationSigner via 4-arg ctor + ILeaseDocumentVersionLog optional. W#22 LeaseOffer → Lease.Draft boundary contract is the inbound integration point on the W#22 side.
