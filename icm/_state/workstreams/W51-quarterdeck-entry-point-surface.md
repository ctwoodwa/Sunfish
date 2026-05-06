---
sort_order: 53
number: 51
slug: quarterdeck-entry-point-surface
title: "**Quarterdeck Entry-Point Surface** (ADR 0080; W#35 Ship Architecture follow-on; `sunfish-feature-change` pipeline)"
status: "building"
status_cell: "`building` (Phase 1 merged 2026-05-06 PR #651 — `foundation-quarterdeck` substrate + `IQuarterdeckAlertSource` + `QuarterdeckAlert` on origin/main; Phase 2 unblocked W#46 P1 ✓ W#49 P1 ✓; Phase 3a unblocked W#46 P3 ✓; **Phase 2 additional halt: `IActorPrincipalResolver` must be on origin/main first** — see `actor-principal-resolver-stage06-handoff.md`; Phases 2–4 pending)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`docs/adrs/0080-quarterdeck-entry-point.md` (PR #574 merged)"
---

## Notes

Hard prerequisites: ADR 0077 W#46 Phase 1 (ShipAction catalog) must land before Phase 2; ADR 0078 W#49 Phase 1 (IOodWatchService) must land before Phase 3a; ADR 0077 W#46 Phase 3 (ILiveAnnouncer + IFocusTrap) must land before Phase 3a. Key types: `IQuarterdeckDataProvider` (`GetSnapshotAsync` / `SubscribeSnapshotAsync`) + `IQuarterdeckCommandService` (`AcknowledgeAlertAsync`) + `IQuarterdeckAlertSource` (SourceName + `GetAlertsAsync`) + `IDepartmentKpiSource` (SourceName + `GetKpisAsync`); `QuarterdeckOptions` (HeartbeatInterval + ProviderTimeout + PerSourceTimeout); `QuarterdeckSnapshot` aggregates 6 sources (OOD watch + mission envelope + standing orders + alerts + KPIs + permission-pre-resolved dept links); `AlertVisibilityPolicy` enum; 3 new `AuditEventType`: WatchHandoverRequested + AlertAcknowledgementRequested + AlertAcknowledged; split `ShipAction`: ViewQuarterdeck (any ShipRole) + ViewQuarterdeckAlerts (DivisionOfficer+) + AcknowledgeAlert. Two new packages: `foundation-quarterdeck` + `blocks-quarterdeck`. 4-phase build: ~14-20h / ~5 PRs. Pre-merge council canonical (WCAG/a11y + security subagents mandatory for Phases 2/3a/3b/4).
