---
sort_order: 54
number: 52
slug: tactical-anomaly-detection-threat-trigger-surface
title: "**Tactical Anomaly Detection + Threat-Trigger Surface** (ADR 0081; W#35 Ship Architecture follow-on; `sunfish-feature-change` pipeline)"
status: "built"
status_cell: "`built` — all phases shipped 2026-05-13 (PR #826 ledger flip; PR #825 Phase 3b Fire Control + Emergency Standing Order; 28/28 tests); pipeline closed"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`docs/adrs/0081-tactical-anomaly-detection.md` (PR #578 merged) + `icm/_state/handoffs/tactical-anomaly-detection-stage06-handoff.md` + `icm/_state/handoffs/tactical-p2-system-principal-authority-addendum.md` (ShipRole.System gap ruling)"
---

## Notes

Hard prerequisites: ADR 0077 W#46 Accepted ✓ + ADR 0065 W#42 built ✓. Soft cross-reference: ADR 0080 W#51 (`LookoutQuarterdeckAlertSource` supplies Quarterdeck ticker); ADR 0068 W#37 (security policy). Key types: `ITacticalRuleEngine` + `ITacticalRule` + `IAlertRouter` + `ISonarStore` + `ILookout` + `ITacticalDataProvider` + `ITacticalCommandService` + `IThreatTriggerService` + `ISystemPrincipalProvider` + `LookoutQuarterdeckAlertSource`; `TacticalOptions` (7 fields with normative bounds); 13 `AuditEventType` constants; 7 `ShipAction` constants (IssueEmergencyStandingOrder = System-only; ManageThreatTriggers = reserved v1). Two new packages: `foundation-tactical` + `blocks-tactical`. 4–5 phase build: ~16-22h / ~6 PRs. Pre-merge council canonical (WCAG/a11y + security subagents mandatory for all UI-bearing and authority-chain phases per §Trust + §8.5).
