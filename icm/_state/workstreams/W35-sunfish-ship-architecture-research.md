---
sort_order: 35
number: 35
slug: sunfish-ship-architecture-research
title: "**Sunfish Ship Architecture research** (`sunfish-gap-analysis` pipeline)"
status: "built"
status_cell: "`built` + **CO Approved Gap 2026-05-01** (pipeline closed; 7 follow-on intakes queued at CO discretion)"
owner: "research"
owner_cell: "research (XO)"
reference_cell: "`icm/00_intake/output/2026-05-01_ship-architecture-intake.md` + `icm/01_discovery/output/2026-05-01_ship-architecture.md` (8,002 words; WCAG/a11y hardened) + 7 follow-on intake stubs in `icm/00_intake/output/` + `~/.claude/plans/sunfish-ship-architecture-research.md` (UPF v1.2 meta-plan, Grade A post-meta-UPF) + `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_35_ship_architecture_naming.md`"
---

## Notes

**Built 2026-05-01 across 5 phases.** Consolidated operator/admin/dev experience research (Aspire-shaped). **Two-layer architecture**: locations × responsibilities × deck-depth, composing as permission tuple `(role, location, deck, action)`. **7 v1 locations**: Quarterdeck, Wayfinder (W#34), Engine Room, Tactical, Sick Bay, Ship's Office, Supply Office *(deferred Phase 2)*. **8 v1 roles**: Captain/XO + Department Heads ENG/NAV/TAC + Division Officers (rotate) + specialized IDC/Scribe/SUPPO + cross-cutting watch OOD/EOOW. **Coverage gradient:** 1 Specified location (Wayfinder via W#34) + 3 Partial (Engine Room / Sick Bay / Ship's Office) + 3 Gap (Quarterdeck / Tactical / Supply Office); 1 Partial role (Captain/XO) + 5 Gap + 2 deferred. **7 follow-on intake stubs filed:** `ood-watch-rotation-intake` + `shared-design-system-intake` (load-bearing for all others) + `quarterdeck-entry-point-intake` + `engine-room-observability-intake` + `tactical-anomaly-detection-intake` + `sick-bay-aggregation-intake` (overlaps W#34 ~ADR 0066 — disambiguate at authoring) + `ships-office-content-aggregation-intake`. WCAG/a11y hardening pass surfaced 8 P0/P1 fixes including: 10 mandatory accessibility topics for Shared Design System; first-aid baseline made auditable contract; permission-tuple denial-accessibility (`PermissionDecision`); Aspire-shaped-NOT-Aspire-equivalent for a11y in Engine Room; native platform a11y APIs (UIA / NSAccessibility / UIAccessibility / AccessibilityNodeInfo) per ADR 0048; council posture extended to dispatch WCAG/a11y subagent for ALL UI-bearing follow-on ADRs. Pipeline closure gated on CO "Approved Gap" decision in discovery-doc Status field.
