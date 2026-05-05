---
sort_order: 0
number: 1
slug: multi-tenancy-type-surface-convention
title: "Multi-tenancy type surface convention"
status: "design-in-flight"
status_cell: "`design-in-flight`"
owner: "research"
owner_cell: "research"
reference_cell: "`icm/01_discovery/output/2026-05-05_multi-tenancy-type-surface.md` (Stage 01 complete) + `icm/00_intake/output/tenant-id-sentinel-pattern-intake-2026-04-28.md`"
---

## Notes

**Stage 01 Discovery complete 2026-05-05 (PR #595).** Discovery resolves all 8 intake sub-items. Two-workstream resolution: **WS-A** (`sunfish-feature-change`) — add `TenantId.System` sentinel + reserve `"__"` prefix + mark `TenantId.Default`/`IMayHaveTenant` `[Obsolete]` + add `TenantSelection` discriminated union (`ForSingle`/`ForMultiple`/`AllAccessible`). **WS-B** (`sunfish-api-change`, gated on WS-A Accepted) — migrate `foundation/Assets/Audit/AuditQuery.Tenant` + `EntityQuery.Tenant` + `DataExport.TenantId` from `TenantId?` → `TenantSelection?`. IMayHaveTenant has 0 production implementations (safe to obsolete). kernel-audit `AuditQuery.TenantId` is required non-nullable per ADR 0049 v0 — NOT a migration target. Implicit `TenantId → TenantSelection` conversion recommended for migration ergonomics. **Stage 02 ADR authoring (WS-A) is next.**
