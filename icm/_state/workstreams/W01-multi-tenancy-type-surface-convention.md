---
sort_order: 0
number: 1
slug: multi-tenancy-type-surface-convention
title: "Multi-tenancy type surface convention (WS-A + WS-B)"
status: "building"
status_cell: "`building` (WS-A merged 2026-05-06 PR #688 — **⚠️ security follow-up required** before WS-B starts; 6 must-fix items on origin/main; WS-B `held` until follow-up ships)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`docs/adrs/0084-tenant-selection-and-sentinel-governance.md` (WS-A) + `docs/adrs/0085-tenant-selection-query-migration.md` (WS-B) + `icm/_state/handoffs/tenant-selection-wsa-stage06-handoff.md` (WS-A hand-off PR #637) + `icm/_state/handoffs/tenant-selection-wsb-stage06-handoff.md` (WS-B hand-off PR #637)"
---

## Notes

**WS-A BUILT 2026-05-06 PR #688** — `TenantId.System` sentinel + `TenantSelection` DU
(`ForSingle`/`ForMultiple`/`AllAccessible`) + implicit cast + `IMayHaveTenant` [Obsolete].
ADR 0084 + ADR 0085 both Accepted 2026-05-06 PR #672.

**⚠️ SECURITY FOLLOW-UP REQUIRED before WS-B starts.** 6 must-fix items on origin/main
(XO security council returned after PR #688 auto-merged). Full spec with code snippets at
PR #688 comment (https://github.com/ctwoodwa/Sunfish/pull/688#issuecomment-4388668618)
and in auto-memory `project_workstream_01_adr_0084.md` (MF-1..MF-6). Summary:
- MF-1: `AllAccessible.Matches` must exclude TenantId.System sentinels
- MF-2: `InMemorySubscriptionService` null context → throw (not TenantId.System)
- MF-3: `NativeChannelProvider` → dedicated sentinel (not TenantId.System)
- MF-4: JSON sentinel deserialization rejection test
- MF-5: `ForMultiple` primary positional ctor must reject empty arrays
- MF-6: `TenantSelection.All` static field (ADR spec gap)

**WS-B gate:** ADR 0084 ✅ ADR 0085 ✅ WS-A ✅ — but HOLD until security follow-up merges.
MF-1 changes `TenantSelection.Matches` API which WS-B consumes.

**WS-B scope:** ADR 0085 — migrates `AuditQuery.Tenant` + `EntityQuery.Tenant` +
`DataExport.TenantId` from `TenantId?` → `TenantSelection?`. ~3-5h / 1 PR. Source-compatible
(implicit cast at call sites). Hand-off: `icm/_state/handoffs/tenant-selection-wsb-stage06-handoff.md`.
