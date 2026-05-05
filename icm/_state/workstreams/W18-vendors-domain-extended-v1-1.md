---
sort_order: 17
number: 18
slug: vendors-domain-extended-v1-1
title: "Vendors domain (cluster #2 spine) — **EXTENDED `blocks-maintenance` v1.1**"
status: "built"
status_cell: "`built` (Phases 1, 2, 3, 6, 7, 8 shipped; Phase 4 deferred to W#32 build; Phase 5 deferred to W#20 magic-link contracts)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM ✓"
reference_cell: "`icm/00_intake/output/property-vendors-intake-2026-04-28.md` + `docs/adrs/0058-vendor-onboarding-posture.md` + `icm/_state/handoffs/property-vendor-onboarding-stage06-handoff.md`"
---

## Notes

**Built 2026-04-30 (Phases 1, 2, 3, 6, 7, 8 shipped).** Shipped via 6 PRs: Phase 6 (#346, Sunfish.Vendor.Specialties@1.0.0 charter + seed; 30 nodes / 11 root anchors preserving every legacy enum value) → Phase 1 (#360, Vendor record api-change v1.0→v1.1: positional→init-only + Specialty enum→Specialties taxonomy-list + new OnboardingState/W9/PaymentPreference/Contacts fields + 4 auxiliary id types + VendorOnboardingState enum) → Phase 2 (#361, VendorContact + per-property primary with at-most-one-primary-per-vendor invariant) → Phase 3 (#362, VendorPerformanceRecord append-only event log with 9 event categories + ProjectFromWorkOrderAsync) → Phase 7 (#363, 7 vendor-onboarding AuditEventType + VendorAuditPayloadFactory + TIN-PII discipline invariant test) → Phase 8 (this PR, apps/docs/blocks/maintenance/vendor-onboarding.md + ledger flip). 108/108 maintenance tests pass. **Phase 4 (W9Document + EncryptedField TIN) deferred** pending W#32 substrate build (W#32 ledger row tracks). **Phase 5 (VendorMagicLink + Bridge onboarding flow + providers-postmark first email adapter) deferred** pending W#20 magic-link delivery contracts. Both deferrals named as halt-conditions in the original hand-off; substrate is "ready for vendor management; W-9 + magic-link follow when prereqs land".
