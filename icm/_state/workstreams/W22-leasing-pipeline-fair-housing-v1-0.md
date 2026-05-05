---
sort_order: 21
number: 22
slug: leasing-pipeline-fair-housing-v1-0
title: "Leasing Pipeline + Fair Housing (cluster cross-cutting) — **`blocks-property-leasing-pipeline` v1.0**"
status: "built"
status_cell: "`built` (Phase 6 compliance half deferred to ADR 0060 Stage 06)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM ✓"
reference_cell: "`icm/00_intake/output/property-leasing-pipeline-intake-2026-04-28.md` + `docs/adrs/0057-leasing-pipeline-fair-housing.md` + `icm/_state/handoffs/property-leasing-pipeline-stage06-handoff.md`"
---

## Notes

**Built 2026-04-30.** Shipped via 7 PRs: Phase 1 (#322, substrate + FHA-defense layout) → Phase 2 (#327, state machine + capability promotion) → Phase 3 (#328, FCRA workflow + AdverseActionNotice generator with §615(a) mandatory statement + 60-day dispute window) → Phase 4 (#332, Sunfish.Leasing.JurisdictionRules@1.0.0 charter + seed; 30 nodes / 7 root jurisdictions) → Phase 5 (#334, public-input boundary + IInquiryValidator) → Phase 6 audit half (#336, 12 AuditEventType + LeasingPipelineAuditPayloadFactory + reflection-based audit-leak invariant for DemographicProfile) → Phase 7 (#339, IPaymentGateway wiring + 4 apps/docs pages). 56/56 leasing-pipeline tests pass. **Phase 6 compliance half (showing-compliance + IEntryComplianceChecker + IJurisdictionPolicyResolver) deferred** pending ADR 0060 Stage 06; halt-condition explicit in hand-off. **3 forward-compat audit events** (BackgroundCheckRequested, AdverseActionNoticeIssued, LeasingPipelineCapabilityRevoked) declared in kernel-audit but unwired pending service-level kickoff/issuance/revocation operations.
