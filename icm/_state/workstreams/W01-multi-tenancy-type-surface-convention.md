---
sort_order: 0
number: 1
slug: multi-tenancy-type-surface-convention
title: "Multi-tenancy type surface convention (WS-A + WS-B)"
status: "built"
status_cell: "`built` (WS-A PR #688 + security follow-up PR #692 + WS-B PR #739; all 3 sub-works complete 2026-05-13)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`docs/adrs/0084-tenant-selection-and-sentinel-governance.md` (WS-A) + `docs/adrs/0085-tenant-selection-query-migration.md` (WS-B) + `icm/_state/handoffs/tenant-selection-wsa-stage06-handoff.md` (WS-A hand-off PR #637) + `icm/_state/handoffs/tenant-selection-wsb-stage06-handoff.md` (WS-B hand-off PR #637)"
---

## Notes

**WS-A BUILT 2026-05-06 PR #688** — `TenantId.System` sentinel + `TenantSelection` DU
(`ForSingle`/`ForMultiple`/`AllAccessible`) + implicit cast + `IMayHaveTenant` [Obsolete].
ADR 0084 + ADR 0085 both Accepted 2026-05-06 PR #672.

**Security follow-up SHIPPED 2026-05-06 PR #692** — all 6 MFs resolved (AllAccessible
sentinel exclusion, null-context throw, crew-comms placeholder, JSON test, ForMultiple
empty guard, TenantSelection.All). WS-B gate cleared.

**WS-B `ready-to-build`:** ADR 0084 ✅ ADR 0085 ✅ WS-A ✅ security follow-up ✅.
Hand-off at `tenant-selection-wsb-stage06-handoff.md`. MF-1 (`TenantSelection.Matches`)
is on origin/main; WS-B can start immediately.

**WS-B scope:** ADR 0085 — migrates `AuditQuery.Tenant` + `EntityQuery.Tenant` +
`DataExport.TenantId` from `TenantId?` → `TenantSelection?`. ~3-5h / 1 PR. Source-compatible
(implicit cast at call sites). Hand-off: `icm/_state/handoffs/tenant-selection-wsb-stage06-handoff.md`.
