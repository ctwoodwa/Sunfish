---
sort_order: 57
number: 54
slug: sick-bay-aggregation-surface
title: "**Sick Bay Aggregation Surface + IDC Role** (ADR 0082; W#35 Ship Architecture follow-on #6; `sunfish-feature-change` pipeline)"
status: "built"
status_cell: "`built` — all 5 phases shipped 2026-05-13 (PRs #628+#695+#735+#817+#819+#821); Sick Bay Blazor + React panels + MedevacServiceImpl + IDC role; PR #822 ledger flip merged; pipeline closed"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/sick-bay-stage06-handoff.md` + `docs/adrs/0082-sick-bay-aggregation-surface.md` (PR #589 merged) + `packages/foundation-sick-bay/` (P1 merged) + `icm/00_intake/output/2026-05-01_sick-bay-aggregation-intake.md`"
---

## Notes

**Phase 1 merged 2026-05-06 via PR #628.** New `Sunfish.Foundation.SickBay`
package shipped: 12 data-model types (PharmacyRecordCount k=3 floor + 4 enums
+ PharmacyInventoryEntry / LabDiagnosticResult / AtmosphereReadout /
SickBaySnapshot / FirstAidHint / StretcherBearerRole) + 6 contract
interfaces (ISickBayDataProvider / ISickBayCommandService / IMedevacService
/ IFirstAidSurface / IStretcherBearerPolicy / IKeyRotationScheduler);
SickBayOptions; AddSunfishSickBay() DI extension. PascalCase audit wire
format per cohort precedent (diverging from hand-off's kebab-case-with-dots).

**Pre-merge council** (standard 4-perspective adversarial; Opus + xhigh)
returned READY-TO-MERGE with 4 opportunistic Minor docstring items
(MedevacState.Complete terminal contract + IStretcherBearerPolicy
empty-list semantics + PharmacyRecordCount §Trust justification +
FirstAidHint `&` rejection rationale). All applied pre-merge for cohort
hygiene. **Cohort batting average: 30-of-35** substrate amendments
needed council fixes (W#54 P1 was the cleanest substrate yet — 0 Major
findings).

**Constants added**: 10 new `AuditEventType` (PascalCase per cohort:
SickBayPharmacyViewed / KeyRotationTriggered / LabDiagnosticViewed /
AtmosphereViewed / MedevacInitiated / MedevacAuthorized / MedevacCancelled
/ MedevacCompleted / MedevacSelfApprovalRejected /
RecoveryContactManaged) + 7 new `ShipAction` (kebab-case: view-sick-bay
/ view-pharmacy / manage-recovery-contacts / trigger-key-rotation /
initiate-medevac / authorize-medevac / view-first-aid).

**Resolver cohort extension**: `ActionMinimumDeck` extended (7 entries;
cardinality now 25); `MapToCapabilityAction` extended (7 entries);
`ResourceScopedActions` extended (+ ManageRecoveryContacts +
TriggerKeyRotation; cardinality now 8 — per W#50 P1 council Major M1
precedent that resource-scoped actions default-deny on null resource at
substrate tier).

**Tests**: 27/27 in foundation-sick-bay (PharmacyRecordCount k=3 floor +
FirstAidHint plain-text-validation + MedevacState 6-state + contract
surface + audit constants + ShipAction kebab-case + StretcherBearerRole
subset + Options + DI). 26/26 in foundation-ship-common still pass
(cardinality test extended).

**Phase 1 Phase-2 follow-up TODOs**:
- Concrete impls in `blocks-sick-bay` (Phase 2 + Phase 3b)
- `KeyRotationTrigger` typed enum (Phase 2 / H3 — ADR 0068 Accepted)
- `PendingTriggerLabel` field on `PharmacyInventoryEntry` (Phase 2)
- `KeyFingerprint` field on PharmacyInventoryEntry (Phase 3a; H2 gated
  on W#53 P1)
- Role-minimum enforcement (IDC / Captain / XO per §5 table) — gated on
  W#37 / `ITenantSecurityPolicy`
- Phase 2 reflection test verifying `IFieldDecryptor` absence in
  `SickBayDataProvider` (H4 council requirement)

**Remaining phases** (per hand-off + ADR 0082 A1 addendum):
- **Phase 2** (DONE): reference impls + DefaultStretcherBearerPolicy +
  DefaultFirstAidSurface + NoopKeyRotationScheduler + DI registration
  finalize. Shipped PR #695 with H4 reflection test pinned. Mission
  Envelope integration deferred to Phase 2b (resolved by ADR 0082 A1).
- **Phase 2b** (~2-3h, NEW per ADR 0082 A1): Mission Envelope
  integration — inject `IMissionEnvelopeProvider`; implement
  `BuildAtmosphereAsync` per A1.2.1 ProbeStatus → severity bucket
  projection + A1.2.2 OverallHealth derivation; add
  `AtmosphereHealth.Unknown` sentinel at ordinal 0; wire
  `IMissionEnvelopeObserver` for push-driven invalidation; gate
  `NoopKeyRotationScheduler` registration behind
  `SickBayOptions.RegisterNoopKeyRotationScheduler` opt-in flag;
  pre-merge standard-adversarial council canonical (security-engineering
  NOT required — no decryption / audit emission changes). Spec:
  `icm/_state/handoffs/sick-bay-stage06-addendum.md`.
- **Phase 3a** (~3-4h): `blocks-sick-bay` Blazor UI (Pharmacy + Lab +
  Atmosphere tabs); pre-merge WCAG/a11y subagent mandatory; H2
  (KeyFingerprint) gates KeyFingerprintDisplay.razor; A1.3 Unknown-
  rendering rules apply (text + non-color marker; aria-live announce
  on Unknown → derived transition).
- **Phase 3b** (~3-4h): SickBayCommandService + MedevacOrchestrator +
  real IKeyRotationScheduler impl; pre-merge security-engineering
  mandatory.
- **Phase 4** (~2-3h): Anchor wiring + apps/docs + ledger flip.

**W#35 cohort progress**: substrate now 5/7 on origin/main (W#46 P1 +
W#49 all + W#55 P1 + W#50 P1 + W#54 P1). Remaining: W#51 (Quarterdeck —
gated on W#46 P3) + W#52 (Tactical — gated on W#46 P3).
