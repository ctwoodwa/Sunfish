---
sort_order: 0
number: 1
slug: multi-tenancy-type-surface-convention
title: "Multi-tenancy type surface convention (WS-A + WS-B)"
status: "ready-to-build"
status_cell: "`ready-to-build` (ADR 0084 **Accepted** + ADR 0085 **Accepted** 2026-05-06 PR #672; Stage 06 hand-offs pre-authored PR #637; WS-A unblocked; WS-B requires WS-A built first)"
owner: "research"
owner_cell: "research"
reference_cell: "`docs/adrs/0084-tenant-selection-and-sentinel-governance.md` (WS-A) + `docs/adrs/0085-tenant-selection-query-migration.md` (WS-B) + `icm/_state/handoffs/tenant-selection-wsa-stage06-handoff.md` (WS-A hand-off PR #637) + `icm/_state/handoffs/tenant-selection-wsb-stage06-handoff.md` (WS-B hand-off PR #637)"
---

## Notes

**WS-A ADR 0084 Proposed 2026-05-05 (PR #597 merged by CO — authoring commit on main; CO acceptance flip pending).** Verify `status:` in ADR file. `TenantId.System`
sentinel + `TenantSelection` DU (`ForSingle`/`ForMultiple`/`AllAccessible`) + implicit cast
on TenantSelection + `IMayHaveTenant` [Obsolete] — all in `foundation` + `foundation-multitenancy`.
**WS-B (sunfish-api-change):** ADR 0085 Proposed 2026-05-05. Migrates
`AuditQuery.Tenant` + `EntityQuery.Tenant` + `ExportRequest.TenantId` from `TenantId?`
→ `TenantSelection?`. Adds `TenantSelection.Matches(TenantId)` helper to
`foundation-multitenancy`. Source-compatible (implicit cast at call sites). ~3–5h / 1 PR.
Council complete (6 BLOCKING resolved 2026-05-05). **Stage 06 hand-offs pre-authored
2026-05-06 (pending CO acceptance).** WS-A: `icm/_state/handoffs/tenant-selection-wsa-stage06-handoff.md`;
WS-B: `icm/_state/handoffs/tenant-selection-wsb-stage06-handoff.md`.
**Flip to `ready-to-build` once BOTH ADR 0084 AND ADR 0085 Status: Accepted AND WS-A built.**
