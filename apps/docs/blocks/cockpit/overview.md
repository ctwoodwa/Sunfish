---
uid: block-cockpit-overview
title: Owner Cockpit — Overview
description: The W#29 owner-cockpit surface — Anchor Blazor + Bridge React + cockpit endpoints that roll up property-ops data per property.
keywords:
  - sunfish
  - cockpit
  - owner
  - property-operations
  - dashboard
---

# Owner Cockpit — Overview

The **owner cockpit** is a read-mostly surface for property-management owners, spouses, bookkeepers, tax advisors, and (later) contractors. It composes data from `blocks-properties`, `blocks-property-equipment`, `blocks-leases`, `blocks-inspections`, and `blocks-maintenance` into per-property views: a property card with equipment, a work-order operations queue, a vendor list with 1099 readiness, and a four-widget dashboard.

The cockpit is **not** a new block. It lives in the accelerators (`accelerators/anchor/Components/Pages/` and `accelerators/bridge/Sunfish.Bridge/Cockpit/`) and consumes existing domain services.

## Surfaces

| URL (cockpit) | Bridge endpoint | Purpose |
|---|---|---|
| `/cockpit` | `GET /api/v1/cockpit/properties` | Property selector — landing |
| `/cockpit/{propertyId}` | `GET /api/v1/cockpit/{propertyId}/detail` | Property card + equipment + active lease + last inspection + open-WO count |
| `/cockpit/{propertyId}/dashboard` | `GET /api/v1/cockpit/{propertyId}/dashboard` | Vacancy + renewal radar (30/60/90 day) + WO rollup + overdue inspections |
| `/cockpit/work-orders` | `GET /api/v1/cockpit/work-orders` | Tenant-wide WO list with status/vendor/date filters |
| `/cockpit/work-orders/{id}` | `GET /api/v1/cockpit/work-orders/{id}` | WO detail (vendor + entry notices + appointment + completion attestation + audit trail) |
| `/cockpit/vendors` | `GET /api/v1/cockpit/vendors` | Vendor list with W-9 status + YTD payments + 1099 readiness |
| `/cockpit/vendors/{id}` | `GET /api/v1/cockpit/vendors/{id}` | Vendor detail (contact + performance log + WO history) |

All routes are guarded by `CockpitPolicy` (authenticated + role ∈ `{owner, spouse}` per the W#29 Phase 1 permissions matrix).

## Architecture decisions

### OQ-OC1 — Package structure: distributed, no new block

Cockpit views live in the accelerators rather than a new `blocks-owner-cockpit` package. Rationale: the cockpit is a composition surface, not a domain. A new block would only wrap DI registrations the accelerators already perform.

### OQ-OC2 — Auth: existing Bridge identity model

The cockpit reuses Bridge's session mechanism. `CockpitPolicy` adds a role assertion (`owner` or `spouse`) on top of `RequireAuthenticatedUser`. No new auth model.

### OQ-OC3 — Real-time updates: poll-on-focus

React side uses TanStack Query with `refetchOnWindowFocus: true` and `staleTime: 30_000`. No SignalR; real-time is a Phase 2+ concern.

### OQ-OC4 — Single Anchor binary

One Anchor binary serves all roles. Page-level rendering reads `CockpitPermissions.Resolve(role, area)` to gate UI; routes still return data unless the role lacks `CanEnterCockpit`.

## Property aggregation

The detail page and dashboard both walk:

```text
Property
  ├─ IPropertyUnitRepository.ListByPropertyAsync  ──▶ PropertyUnit[]
  │     └─ UnitId[] used to filter Lease.UnitId / Inspection.UnitId
  └─ ListWorkOrdersAsync(PropertyId = …)
```

See the [property-aggregation guide](xref:block-properties-property-aggregation) for the join pattern.

## 1099 readiness rule

The vendor list flags vendors that need a 1099:

```text
needsForm1099 = OnboardingState == Active
              AND vendor.W9 is null
              AND ytdPayments > $600
```

(Hand-off named "Completed"; the actual `VendorOnboardingState` enum's closest equivalent is `Active`.) Extracted as `VendorsEndpoint.NeedsForm1099(Vendor, decimal)` for testability.

## What's not in scope

- Real-time push updates (SignalR / WebSocket) — Phase 2+
- Bookkeeper / tax-advisor / contractor / leaseholder / prospect role surfaces — Phase 3+
- Receipts integration — gated on W#26 (blocked on ADR 0055 dynamic forms)
- Vendor contact display-name resolution — `IVendorContactService` needs a list-by-vendor accessor
- Full W-9 document inspection — requires the tenant-key + encryption substrate

## See also

- [Cockpit permissions matrix](xref:block-cockpit-permissions) — full role × area table
- [PropertyUnit reference](xref:block-properties-property-unit) — the substrate cockpit aggregation walks
