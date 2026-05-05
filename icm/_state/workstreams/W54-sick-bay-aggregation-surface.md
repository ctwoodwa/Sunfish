---
sort_order: 57
number: 54
slug: sick-bay-aggregation-surface
title: "**Sick Bay Aggregation Surface + IDC Role** (ADR 0082; W#35 Ship Architecture follow-on #6; `sunfish-feature-change` pipeline)"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0082 Proposed 2026-05-05 via PR #589; Stage 06 hand-off authored 2026-05-05; sunfish-PM may begin Phase 1 when H1 / W#46 Phase 1 lands `foundation-ship-common` on origin/main)"
owner: "research"
owner_cell: "research (XO) ✓"
reference_cell: "`icm/_state/handoffs/sick-bay-stage06-handoff.md` + `docs/adrs/0082-sick-bay-aggregation-surface.md` (PR #589 merged) + `icm/00_intake/output/2026-05-01_sick-bay-aggregation-intake.md`"
---

## Notes

**Hand-off authored 2026-05-05.** ~14-20h sunfish-PM / 5 phases / ~5 PRs. New packages: `foundation-sick-bay` (contracts) + `blocks-sick-bay` (Blazor UI). Key design: `PharmacyRecordCount` k-anonymity floor k=3; `IMedevacService` four-eyes self-approval rejection (`SickBayMedevacSelfApprovalRejected`); `IKeyRotationScheduler` new contract introduced by this ADR; `StretcherBearerRole` constrained enum (not `ShipRole`); `FirstAidHint.Body` plain-text-validated. 11 `AuditEventType` constants; `ShipRole.IDC` + 7 `ShipAction` (`ViewSickBay` / `ViewPharmacy` / `ManageRecoveryContacts` / `TriggerKeyRotation` / `InitiateMedevac` / `AuthorizeMedevac` / `ViewFirstAid`). WCAG 2.2 AA: 11 SCs (SC 1.3.1, 1.4.1, 1.4.3, 2.1.1, 2.2.1, 2.4.3, 2.4.7, 3.3.1, 3.3.4, 3.3.8, 4.1.3). **Halt-conditions:** H1 `foundation-ship-common` on origin/main (W#46 Phase 1; gates `ShipRole.IDC` + `ShipAction` constants); H2 `KeyFingerprint` on origin/main (W#53 Phase 1; gates `KeyFingerprintDisplay.razor` Phase 3a only); H3 ADR 0068 Status: Accepted (gates `KeyRotationTrigger` Phase 2 type-swap; Phase 1 ships `string triggerReason`); H4 security council pre-Phase-2 reflection test verifies `IFieldDecryptor` absence in `SickBayDataProvider`. Pre-merge council canonical (security-engineering mandatory Phases 2 + 3b; WCAG/a11y mandatory Phase 3a per §8). 10 enumerated operational halt-conditions in hand-off Appendix. W#35 cohort follow-on #6 of 7 (W#55 Ship's Office hand-off paired in same XO authoring batch).
