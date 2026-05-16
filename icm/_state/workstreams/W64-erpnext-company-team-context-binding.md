---
sort_order: 73
number: 64
slug: erpnext-company-team-context-binding
title: "WS-A — ERPNext Company ↔ Sunfish Team Context Binding (multi-entity cockpit)"
status: "design-in-flight"
status_cell: "`design-in-flight` — scope identified 2026-05-16; research session needed to audit W#60 P2 React cockpit ERPNext API calls + verify company-filter gap; hand-off pending"
owner: "research"
owner_cell: "research"
reference_cell: "`icm/_state/MASTER-PLAN.md` WS-A + `docs/adrs/0032-multi-team-anchor-workspace-switching.md` (Accepted) + `packages/kernel-runtime/Teams/`"
---

## Notes

**Context:** CO has 6 entities in ERPNext (4 LLCs + holding co + mgmt co). Each ERPNext entity is a "Company" doctype. The W#29 Owner Web Cockpit fetches from ERPNext via Bridge, but does not yet filter by ERPNext company — the current cockpit may show mixed-entity data.

**ADR 0032 substrate is built:** `TeamContext` + `ITeamContextFactory` + `IActiveTeamAccessor` + `TeamSwitcherPage.razor` + `TeamServiceRegistrar` all exist in `packages/kernel-runtime/Teams/` and `accelerators/anchor/`.

**What's missing:** The binding between Sunfish `TeamId` and ERPNext `company` name (the filter parameter on all ERPNext REST API list calls). Also, the React cockpit team-switcher component.

**Research needed before hand-off:**
1. Audit W#60 P2 React cockpit (`apps/anchor-react/src/`) — do the ERPNext API calls include a `company` filter? If not, this is a correctness gap.
2. Check if Bridge proxy adds a tenant-scoped `company` header or filter automatically.
3. Verify the ADR 0032 team config storage mechanism — can `TeamContext` persist a `ErpNextCompanyName` mapping in its encrypted SQLite store?
4. Scope: per-team ERPNext company config screen (one-time setup) + cockpit query filter wiring = 2-3 PRs.

**Gate:** No prerequisites; immediately researchable. W#60 P2 must be on main (it is) so the React source files are accessible.

**Downstream:** WS-H (spouse co-ownership) depends on WS-A being wired (spouse needs team-level capability grants across all 6 entities).
