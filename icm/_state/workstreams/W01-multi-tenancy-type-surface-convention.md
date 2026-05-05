---
sort_order: 0
number: 1
slug: multi-tenancy-type-surface-convention
title: "Multi-tenancy type surface convention (WS-A + WS-B)"
status: "design-in-flight"
status_cell: "`design-in-flight` (WS-A ADR 0084 **Proposed** 2026-05-05 via PR #597 — CO acceptance flip pending; WS-B ADR 0085 Proposed 2026-05-05 via PR #606 — council complete; both pending CO acceptance)"
owner: "research"
owner_cell: "research (WS-B council pending)"
reference_cell: "`docs/adrs/0084-tenant-selection-and-sentinel-governance.md` (WS-A) + `docs/adrs/0085-tenant-selection-query-migration.md` (WS-B, PR pending)"
---

## Notes

**WS-A ADR 0084 Proposed 2026-05-05 (PR #597 merged by CO — authoring commit on main; CO acceptance flip pending).** Verify `status:` in ADR file. `TenantId.System`
sentinel + `TenantSelection` DU (`ForSingle`/`ForMultiple`/`AllAccessible`) + implicit cast
on TenantSelection + `IMayHaveTenant` [Obsolete] — all in `foundation` + `foundation-multitenancy`.
**WS-B (sunfish-api-change):** ADR 0085 Proposed 2026-05-05. Migrates
`AuditQuery.Tenant` + `EntityQuery.Tenant` + `ExportRequest.TenantId` from `TenantId?`
→ `TenantSelection?`. Adds `TenantSelection.Matches(TenantId)` helper to
`foundation-multitenancy`. Source-compatible (implicit cast at call sites). ~3–5h / 1 PR.
Council complete (6 BLOCKING resolved 2026-05-05); hand-off to be authored once ADR 0085
Status: Accepted. **Flip to `ready-to-build` once BOTH ADR 0084 AND ADR 0085 Status: Accepted.**
