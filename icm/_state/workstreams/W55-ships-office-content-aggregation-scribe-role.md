---
sort_order: 58
number: 55
slug: ships-office-content-aggregation-scribe-role
title: "**Ship's Office Content Aggregation + Scribe Role** (ADR 0083; W#35 Ship Architecture follow-on #7; `sunfish-feature-change` pipeline)"
status: "design-in-flight"
status_cell: "`design-in-flight` (ADR 0083 Proposed 2026-05-05 via PR #591; council complete; pending CO acceptance)"
owner: "research"
owner_cell: "research (XO)"
reference_cell: "`docs/adrs/0083-ships-office-content-aggregation.md` (PR #591 merged) + `icm/00_intake/output/2026-05-01_ships-office-content-aggregation-intake.md`"
---

## Notes

**ADR 0083 Proposed 2026-05-05 via PR #591; pre-merge council complete; pending CO acceptance.** Stage 06 hand-off gated on ADR 0083 Status: Accepted. W#35 cohort COMPLETE (7/7 follow-on ADRs authored). New packages: `foundation-ships-office` (contracts) + `blocks-ships-office` (Blazor UI). Key design decisions: `IDocumentDiffService` lives in `blocks-ships-office` only (foundation cannot reference ui-core for diffing primitives); W9 TIN always redacted (no Phase 2 opt-in); `RequireSecondActorPublish` opt-in per document class; `IExternalPublicationService` interface (integration point for future Bridge publishing targets). 6 `AuditEventType` constants; 6 `ShipAction` constants; `ShipRole.Scribe` + `ShipLocation.ShipsOffice`. **Halt-conditions:** H1 `foundation-ship-common` on origin/main (W#46 Phase 1); H2 `RequireSecondActorPublish` validation gated on CO acceptance of per-document second-actor policy; H3 security council required before Phase 2 ship; H4 `IDocumentDiffService` must NOT be added to `foundation-ships-office` (ui-core dep constraint). W#35 cohort follow-on #7 of 7 (final; cohort complete).
