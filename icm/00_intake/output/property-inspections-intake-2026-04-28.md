# Intake Note — Inspections Domain Module (revised: extension to `blocks-inspections`)

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build`.**
**Status owner:** research session
**Date:** 2026-04-28 (revised 2026-04-28 per cluster-vs-existing reconciliation)
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turns 3, 5, 6 — inspection photos, asset condition assessments, move-in/move-out checklists).
**Pipeline variant:** `sunfish-feature-change` (extension; not new package)
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)

> **Revision note 2026-04-28:** Disposition reframed from "new block" to **"extension to existing `packages/blocks-inspections/`"**. Audit revealed `blocks-inspections` already ships `Inspection` + `InspectionTemplate` + `InspectionChecklistItem` + `InspectionResponse` + `InspectionReport` + `Deficiency` (severity + status) + `InspectionPhase` + `InspectionItemKind` + `IInspectionsService` + `InMemoryInspectionsService` + `ScheduleInspectionRequest`/`CreateTemplateRequest`/`RecordDeficiencyRequest`. Cluster contribution (move-in/move-out triggers + EquipmentConditionAssessment children + iOS walkthrough wizard) becomes an **extension** to that block: add `InspectionItemKind` values for move-in/move-out; model EquipmentConditionAssessment as a `Deficiency` variant or new child entity. **Note: cluster's "AssetConditionAssessment" renames to `EquipmentConditionAssessment` per UPF Rule 4** (the cluster physical-equipment entity is `Equipment`, not `Asset`). See [`../../07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md`](../../07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md) workstream #25 row for full rationale.

---

## Problem Statement

Inspections happen at multiple property lifecycle points: annual condition reviews, move-in walkthroughs, move-out walkthroughs, post-repair verifications, insurance-driven inspections. Each is a structured event capturing observations + photos + (often) asset-by-asset condition assessments + (often) tenant or vendor sign-off.

Without a structured inspection module, observations live in spreadsheets or photo-roll memory. Move-in vs move-out condition deltas — the basis for security-deposit reconciliation — become contestable rather than documented.

This module provides the inspection event, its child condition assessments, the trigger types (annual / move-in / move-out / post-repair / on-demand), and the iOS walkthrough wizard. Sign-off uses the Signatures intake mechanism. The condition assessments link to assets (per Assets intake).

## Scope Statement

### In scope

1. **`Inspection` entity.** Tenant + property + (optional) PropertyUnit; trigger type; scheduled_at + completed_at; inspector_identity_ref; narrative_notes; photos; status (scheduled | in_progress | completed | signed_off).
2. **`AssetConditionAssessment` child entity.** FK Inspection + FK Asset; condition_rating (good | fair | poor | failed); expected_remaining_life_years (nullable); observations; photos; recommendations.
3. **`MoveInChecklist` / `MoveOutChecklist` polymorphic specialization.** Inspection trigger=move-in or move-out; child condition assessments cover all assets in the unit; security-deposit-reconciliation hook on move-out (delta vs move-in baseline).
4. **`InspectionFinding` event.** Inspector identifies a deficiency → emits InspectionFinding event → optionally opens a Work Order (per Work Orders intake's `WorkOrderSource` polymorphic FK).
5. **iOS walkthrough wizard.** Structured form per trigger type; per-asset prompts; photo capture; signature capture for move-in/out (Signatures intake).
6. **`blocks-inspections` package.**
7. **Recurring annual cadence.** Per-property next-inspection-due calc; reminder via messaging substrate.

### Out of scope

- Asset entity → [`property-assets-intake-2026-04-28.md`](./property-assets-intake-2026-04-28.md)
- Sign-off mechanism → [`property-signatures-intake-2026-04-28.md`](./property-signatures-intake-2026-04-28.md)
- Work-order creation from findings → [`property-work-orders-intake-2026-04-28.md`](./property-work-orders-intake-2026-04-28.md)
- Security-deposit reconciliation calculation → ADR 0051 / Phase 2 commercial intake (this intake provides the *delta data*, not the calc)

---

## Affected Sunfish Areas

- `blocks-inspections` (new)
- `foundation-persistence`, ADR 0015, ADR 0049
- iOS walkthrough wizard (depends on iOS App intake)
- Signatures (consumer), Work Orders (consumer), Assets (FK)

## Acceptance Criteria

- [ ] All entities defined with XML doc + ADR 0014 adapter parity
- [ ] Trigger-type polymorphism + per-trigger required fields
- [ ] iOS wizard end-to-end for all trigger types
- [ ] Move-in baseline + move-out delta computed and exposed as projection
- [ ] InspectionFinding emits cleanly to Work Orders source FK
- [ ] kitchen-sink demo: annual + move-in + move-out flows on a sample property
- [ ] apps/docs entry covering inspection lifecycle + trigger types

## Open Questions

| ID | Question | Resolution |
|---|---|---|
| OQ-IN1 | Free-text condition vs structured rating: just rating + notes, or per-component checklist (kitchen has stove, fridge, dishwasher, etc.)? | Stage 03 — both; per-property-class checklist templates configurable |
| OQ-IN2 | Inspection schema versioning: when checklist template changes mid-tenancy, do prior inspections re-render against old template? | Stage 02 — versioned templates; prior inspections preserve original template ref |
| OQ-IN3 | Multiple inspectors on one inspection (you + spouse walk together) | Stage 02 — collaborative-edit; CRDT-on-mobile constraint per OQ2 means LWW for now |
| OQ-IN4 | Photo redaction (tenant personal items, etc.) prior to sharing inspection report | Stage 03 — manual redaction tool; defer auto-detect |

## Dependencies

**Blocked by:** Properties, Assets, Signatures, iOS App
**Blocks:** Work Orders (findings → work-order source), Phase 2 security-deposit reconciliation

## Cross-references

- Sibling intakes: Assets, Signatures, Work Orders, iOS App, Properties
- ADR 0015, ADR 0049

## Sign-off

Research session — 2026-04-28
