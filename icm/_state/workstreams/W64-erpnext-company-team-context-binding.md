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

**Architectural finding (2026-05-16 audit of origin/main):**

The W#29 cockpit does NOT call ERPNext directly. It calls Bridge `/api/v1/cockpit/properties` which reads from Sunfish's own `blocks-properties` repository (`IPropertyRepository.ListByTenantAsync(tenant)`). The `blocks-properties` data model has no `Company` or `EntityTag` field — all CO's properties across 6 entities are co-mingled under a single `TenantId`.

The `apps/anchor-react/src/api/erpnext.ts` file (W#60 P2) calls ERPNext directly and does have a `company` field on `Property` + `Lease` — but those calls are a **separate data path** from the cockpit (which uses the Bridge `/api/v1/cockpit/*` routes).

**CO-class design decision required:**

| Option | Approach | Tradeoff |
|---|---|---|
| **A (Blocks-first)** | Add `CompanyId` (or `EntityTag`) to `Property` entity in `blocks-properties`; entity-switcher filters by this field | Sunfish controls the data model; works offline; adds a migration |
| **B (ERPNext-first)** | Cockpit entity-switcher reads ERPNext Company list; cockpit screens use the ERPNext data path (`erpnext.ts`) instead of `blocks-properties` | ERPNext is source of truth; simpler; no blocks-properties migration; loses offline capability until W#60 P3 |
| **C (Hybrid)** | Cockpit uses `blocks-properties` for offline display, synced from ERPNext with company tag; ERPNext is write source | Most complex; deferred to W#60 P3 local cache design |

**XO recommendation:** Option A for the current cockpit (blocks-properties already built; simple `EntityTag` string property added + migration); Option C as the W#60 P3 offline sync target. CO input needed before hand-off.

**ADR 0032 substrate is built:** `TeamContext` + `ITeamContextFactory` + `IActiveTeamAccessor` + `TeamSwitcherPage.razor` + `TeamServiceRegistrar` all in `packages/kernel-runtime/Teams/` and `accelerators/anchor/`. The team switcher UI exists; it just needs the entity/company binding.

**Gate:** CO-class design decision on A vs B needed. No code prerequisites.

**Downstream:** WS-H (spouse co-ownership) depends on WS-A being wired (spouse needs team-level capability grants across all 6 entities).
