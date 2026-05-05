---
sort_order: 0
number: 1
slug: multi-tenancy-type-surface-convention
title: "Multi-tenancy type surface convention (WS-A + WS-B)"
status: "design-in-flight"
status_cell: "`design-in-flight` (WS-A complete — ADR 0084 Accepted 2026-05-05 via PR #597; WS-B ADR 0085 Proposed 2026-05-05; council pending)"
owner: "research"
owner_cell: "research (WS-B council pending)"
reference_cell: "`docs/adrs/0084-tenant-selection-and-sentinel-governance.md` (WS-A) + `docs/adrs/0085-tenant-selection-query-migration.md` (WS-B, PR pending)"
---

## Notes

**WS-A complete — ADR 0084 Accepted 2026-05-05 (PR #597 merged by CO).** `TenantId.System`
sentinel + `TenantSelection` DU (`ForSingle`/`ForMultiple`/`AllAccessible`) + implicit cast
on TenantSelection + `IMayHaveTenant` [Obsolete] — all in `foundation` + `foundation-multitenancy`.
**WS-B (sunfish-api-change):** ADR 0085 Proposed 2026-05-05. Migrates
`AuditQuery.Tenant` + `EntityQuery.Tenant` + `ExportRequest.TenantId` from `TenantId?`
→ `TenantSelection?`. Adds `TenantSelection.Matches(TenantId)` helper to
`foundation-multitenancy`. Source-compatible (implicit cast at call sites). ~3–5h / 1 PR.
Council pending; hand-off authored on Accepted. **Flip to `ready-to-build` once ADR 0085
Status: Accepted.**
