---
sort_order: 28
number: 29
slug: owner-web-cockpit
title: "**Owner Web Cockpit** (cluster module; `sunfish-feature-change` pipeline) — Anchor Blazor + Bridge React + cockpit endpoints over all property-ops blocks; multi-actor permissions matrix"
status: "built"
status_cell: "`built` — All 5 ship phases + Phase 1.5 merged. PR 1 (#853) nav shell + property selector + CockpitPolicy. PR 2 (#857) property detail (stubbed). Phase 1.5 / W#62 PR 2 (#861) real lease + inspection aggregation. PR 3 (#858) work-orders list + detail. PR 4 (#859) vendors list + detail + 1099 readiness. PR 5 (#863) per-property dashboard. PR 6 docs + ledger flip (this PR)."
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/property-owner-cockpit-stage06-handoff.md` + `apps/docs/cockpit/overview.md` + `apps/docs/cockpit/permissions.md`"
---

## Notes

**Built across 6 cockpit PRs + 4 W#62 PRs (PropertyUnit substrate that unblocked Phase 5).** Anchor Blazor + Bridge React + shared cockpit endpoints, walking `Property → PropertyUnit → {Lease, Inspection, WorkOrder}` via the W#62 substrate.

**Multi-actor permissions matrix (cluster OQ1 resolution):** `CockpitPermissions.cs` static class encoded in both `accelerators/anchor/Cockpit/` and `accelerators/bridge/Sunfish.Bridge/Cockpit/`. Phase 1 enforces `CanEnterCockpit(role)` (owner/spouse only); the `Resolve(role, area)` matrix is pre-encoded for Phase 3 role expansion.

**Endpoints (all guarded by `CockpitPolicy`):**

| Endpoint | Purpose |
|---|---|
| `GET /api/v1/cockpit/properties` | Property selector list |
| `GET /api/v1/cockpit/{propertyId}/detail` | Property card + equipment + active lease + last inspection + open-WO count |
| `GET /api/v1/cockpit/work-orders[/{id}]` | Work-order list (status / vendor / date / pagination) + detail |
| `GET /api/v1/cockpit/vendors[/{id}]` | Vendor list with W-9 + YTD + 1099 readiness + detail (contact + performance log + WO history) |
| `GET /api/v1/cockpit/{propertyId}/dashboard` | Vacancy + renewal radar (30/60/90d) + WO rollup + overdue inspections |

**Tests:** 31 cockpit unit tests in `accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/Cockpit/` (5 Phase 1 policy/handler + 3 Phase 2 detail + 3 Phase 1.5 aggregation + 1 PR 3 WO count + 5 Phase 3 work orders + 9 Phase 4 vendors + 5 Phase 5 dashboard).

**Deferred:** Resolution of `VendorContactId` → display name (IVendorContactService has no list-by-vendor accessor today). W-9 status chip is binary on-file/awaiting (full document resolution requires the encryption substrate, out of scope for read-only cockpit).
