---
sort_order: 53
number: 51
slug: quarterdeck-entry-point-surface
title: "**Quarterdeck Entry-Point Surface** (ADR 0080; W#35 Ship Architecture follow-on; `sunfish-feature-change` pipeline)"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0080 Accepted 2026-05-05 via PR #574; Stage 06 hand-off at `icm/_state/handoffs/quarterdeck-entry-point-stage06-handoff.md`; sunfish-PM may begin Phase 1 when W#46 Phase 1 lands)"
owner: "research"
owner_cell: "research (XO) ✓"
reference_cell: "`docs/adrs/0080-quarterdeck-entry-point.md` (PR #574 merged)"
---

## Notes

Hard prerequisites: ADR 0077 W#46 Phase 1 (ShipAction catalog) must land before Phase 2; ADR 0078 W#49 Phase 1 (IOodWatchService) must land before Phase 3a; ADR 0077 W#46 Phase 3 (ILiveAnnouncer + IFocusTrap) must land before Phase 3a. Key types: `IQuarterdeckDataProvider` (`GetSnapshotAsync` / `SubscribeSnapshotAsync`) + `IQuarterdeckCommandService` (`AcknowledgeAlertAsync`) + `IQuarterdeckAlertSource` (SourceName + `GetAlertsAsync`) + `IDepartmentKpiSource` (SourceName + `GetKpisAsync`); `QuarterdeckOptions` (HeartbeatInterval + ProviderTimeout + PerSourceTimeout); `QuarterdeckSnapshot` aggregates 6 sources (OOD watch + mission envelope + standing orders + alerts + KPIs + permission-pre-resolved dept links); `AlertVisibilityPolicy` enum; 3 new `AuditEventType`: WatchHandoverRequested + AlertAcknowledgementRequested + AlertAcknowledged; split `ShipAction`: ViewQuarterdeck (any ShipRole) + ViewQuarterdeckAlerts (DivisionOfficer+) + AcknowledgeAlert. Two new packages: `foundation-quarterdeck` + `blocks-quarterdeck`. 4-phase build: ~14-20h / ~5 PRs. Pre-merge council canonical (WCAG/a11y + security subagents mandatory for Phases 2/3a/3b/4).
