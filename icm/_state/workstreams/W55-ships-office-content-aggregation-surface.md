---
sort_order: 58
number: 55
slug: ships-office-content-aggregation-surface
title: "**Ship's Office Content Aggregation Surface + Scribe Role** (ADR 0083; W#35 Ship Architecture follow-on #7 — FINAL cohort ADR; `sunfish-feature-change` pipeline)"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0083 Proposed 2026-05-05 via PR #591; Stage 06 hand-off authored 2026-05-05; sunfish-PM may begin Phase 1 when H1 / W#46 Phase 1 lands `foundation-ship-common` + `ShipRole.Scribe` + `ShipLocation.ShipsOffice` on origin/main)"
owner: "research"
owner_cell: "research (XO) ✓"
reference_cell: "`icm/_state/handoffs/ships-office-stage06-handoff.md` + `docs/adrs/0083-ships-office-content-aggregation.md` (PR #591 merged) + `icm/00_intake/output/2026-05-01_ships-office-content-aggregation-intake.md`"
---

## Notes

**Hand-off authored 2026-05-05.** ~16-22h sunfish-PM / 6 phases (Phase 5 conditional on ADR 0055) / ~5-6 PRs. New packages: `foundation-ships-office` (contracts + `IContentEditorSurface`) + `blocks-ships-office` (Blazor UI + `IDocumentDiffService` + `SUNFISH_SHIPSOFFICE_PERM001` analyzer). Key design decisions per ADR 0083 council resolutions: `IDocumentDiffService` declared in blocks-tier ONLY (B-1 foundation→ui-core dep prevented); `PublishAsync` audit-before-operation ordering with explicit pre-op + rejected-on-denial paths (B-2); SC 1.4.12 noted as WCAG 2.1 carried forward (B-3); W9 TIN ALWAYS redacted in browse view (`ShipsOfficeDocumentView` excludes the TIN field); `RequireSecondActorPublish` opt-in default false (Phase 1) / true (Bridge regulated default); 6 `AuditEventType` constants; 4 `ShipAction` (`ViewShipsOffice` / `EditShipsOfficeDocument` / `PublishShipsOfficeDocument` / `ArchiveShipsOfficeDocument`). WCAG 2.2 AA: 12 SCs (SC 1.3.1, 1.3.3, 1.4.1, 1.4.3, 1.4.4, 1.4.12, 2.1.1, 2.4.3, 2.4.5, 2.4.6, 3.3.1, 4.1.3). **Halt-conditions:** H1 `foundation-ship-common` (W#46 Phase 1; gates `ShipAction` constants); H2 `DiffPreviewView` (W#46 Phase 3; gates Phase 2 real `IDocumentDiffService` impl + Phase 3 `DocumentDiffPanel.razor`; Phase 1 stub OK); H3 `ISearchAsYouType` (W#46 Phase 1; gates Phase 3 `ShipsOfficeSearchBar.razor`); H4 ADR 0055 Accepted (gates Phase 5 `DynamicTemplate` kind — Phase 5 is CONDITIONAL; skip if not cleared); H5 ADR 0065-A1 (no hard gate; polling fallback); H6 ADR 0004 Stage 06 (no hard gate; empty-list stub for `SignatureEnvelope` kind). Pre-merge council canonical (security-engineering mandatory Phase 2 — `IFieldDecryptor` prohibition reflection test + audit-emission ordering + analyzer; WCAG/a11y mandatory Phase 3 — long-form reading + diff UX per W#35 §9.5). 14 enumerated operational halt-conditions in hand-off Appendix. **W#35 Ship Architecture cohort follow-on #7 of 7 — the FINAL cohort ADR.** When W#55 ships `built`, the W#35 cohort is fully closed (W#46/49/50/51/52/54/55 all built).
