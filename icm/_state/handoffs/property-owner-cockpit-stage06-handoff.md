# Hand-off — Owner Web Cockpit Phase 1 (W#29)

**From:** XO (research session)
**To:** sunfish-PM (COB)
**Created:** 2026-05-15
**Pipeline variant:** `sunfish-feature-change`
**Workstream:** W#29

---

## Context

The Owner Web Cockpit is a consumer-only workstream — it introduces no new domain entities.
All data comes from existing domain packages already on `main`:

| Domain | Package | Status |
|---|---|---|
| Properties | `Sunfish.Blocks.Properties` | built |
| Equipment | `Sunfish.Blocks.PropertyEquipment` | built |
| Inspections | `blocks-inspections` | built |
| Leases | `blocks-leases` | built |
| Work Orders | `blocks-maintenance` | built |
| Vendors | `blocks-maintenance` (Vendor types) | built |
| Leasing Pipeline | `blocks-property-leasing-pipeline` | built |
| Public Listings | `blocks-property-public-listings` | built |
| Transport | `Sunfish.Foundation.Transport` | built |
| Signatures | `Sunfish.Kernel.Signatures` | built |

**NOT in Phase 1 scope (dependencies not yet built):**
- Receipts / reconciliation — W#26 blocked on ADR 0055 (dynamic forms)
- Messaging/thread inbox — W#20 still building
- Full reporting / tax export — Phase 3+

---

## Architecture decisions (Stage 02 resolution)

### OQ-OC1 — Package structure
**Decision: Distributed, no new `blocks-*` package.**
Cockpit views live in the accelerators:
- Anchor Blazor: `accelerators/anchor/Components/Pages/` (new files per feature area)
- Bridge React: `accelerators/bridge/Sunfish.Bridge/Cockpit/` (new Minimal API handler family + React TSX components via `apps/anchor-react/`)

Reason: cockpit is a composition surface, not a domain. A new `blocks-owner-cockpit-shell` package would only wrap DI registrations the accelerators already perform. Follow the W#50-W#55 pattern.

### OQ-OC2 — Bridge web auth
**Decision: Existing Bridge identity model.**
The cockpit in Bridge uses the same session mechanism as the rest of Bridge. When CO accesses Bridge in browser, the existing cookie-based session (with macaroon caveats on role) gates the cockpit routes. No new auth mechanism needed for Phase 1.

### OQ-OC3 — Real-time updates
**Decision: Poll-on-focus (Phase 1).**
React side uses TanStack Query `refetchOnWindowFocus: true` + `staleTime: 30_000`. Blazor side uses `ITimerService` or component `OnParametersSet` refresh. No SignalR for Phase 1. Real-time (SignalR / WebSocket) deferred to Phase 2+.

### OQ-OC4 — Capability trimming
**Decision: Single Anchor, ADR 0032 feature flags.**
One Anchor binary. Visible pages are gated by the capability token's role caveat:
- `owner` / `spouse` — full cockpit
- `bookkeeper` — Properties (read) + Work Orders (read) + Vendor list + receipt path (Phase 3 only)
- `tax-advisor` — read-only reports tab only (Phase 3)
- `contractor` — no cockpit access (use Bridge work-order portal)

Phase 1 ships owner + spouse visibility. Other role trims are coded but lead to a "you don't have access" page until Phase 3 routes exist.

---

## Multi-actor permissions matrix (resolves cluster OQ1)

This table is the canonical OQ1 resolution. Implement as compile-time constants in a
`CockpitPermissions` static class in the Anchor/Bridge cockpit namespace.

| Role | Properties | Equipment | Leases | Leasing Pipeline | Work Orders | Vendors | Inspections | Receipts (Ph3) | Reports (Ph3) |
|---|---|---|---|---|---|---|---|---|---|
| `owner` | Full CRUD | Full CRUD | Full | Full | Full | Full | Full | Full | Full |
| `spouse` | Full CRUD | Full CRUD | Full | Full | Full | Full | Full | Full | Full |
| `bookkeeper` | Read | Read | Read | None | Read | Read + W9 | Read | Full | Export CSV |
| `tax-advisor` | Read (summary) | None | None | None | None | Read (1099) | None | Export | Full |
| `contractor` | None | Own WO only | None | None | Own WO | Own profile | Own assigned | None | None |
| `leaseholder` | Own unit | None | Own | None | Own WO | None | None | Own | None |
| `prospect` | None | None | None | Own pipeline | None | None | None | None | None |

---

## Phase 1 deliverables (these PRs only)

### PR 1 — Cockpit navigation shell

**Anchor Blazor** — `accelerators/anchor/Components/Pages/`:
- `CockpitLayout.razor` — sidebar nav with links to all cockpit sections; shows current property context selector (TopNavBar pattern from existing `Layout/`)
- `CockpitPropertySelectorPage.razor` — landing page: list all properties CO manages; clicking one sets the active-property context used by all other cockpit pages

**Bridge React** — new folder `accelerators/bridge/Sunfish.Bridge/Cockpit/`:
- `CockpitEndpoints.cs` — `MapCockpit()` extension; maps `/cockpit` and `/cockpit/{propertyId}/*` route family; guards all routes with `RequireAuthorization("CockpitPolicy")`
- `apps/anchor-react/src/cockpit/` — React cockpit root: `CockpitLayout.tsx`, `PropertySelector.tsx`

**DI / Auth:**
- `CockpitPolicy` = authenticated + role in `{owner, spouse}` for Phase 1
- `CockpitPermissions.cs` — static class encoding the matrix above (used by both Anchor and Bridge cockpit pages)

**Tests:** Route-guard test: unauthenticated → 401; wrong-role → 403; owner → 200.

---

### PR 2 — Properties + Equipment + Inspections views

**Anchor Blazor** — `accelerators/anchor/Components/Pages/`:
- `PropertyDetailPage.razor` — `@page "/cockpit/properties/{PropertyId}"` — renders:
  - Property card (address, kind, status)
  - Equipment list (kind, condition, last inspection date) via `IPropertyEquipmentRepository`
  - Active lease summary (current tenant name, rent amount, expiry) via `ILeaseRepository`
  - Open work-order count badge via `IWorkOrderService`
  - Last inspection date + result via `IInspectionsService`

**Bridge React** — `apps/anchor-react/src/cockpit/properties/`:
- `PropertyDetailView.tsx` — same composite data, fetched via TanStack Query with `refetchOnWindowFocus`
- `PropertyDetailEndpoint.cs` in Bridge: `GET /cockpit/{propertyId}/detail` — aggregates from `IPropertyRepository`, `IPropertyEquipmentRepository`, `ILeaseRepository`, `IWorkOrderService`, `IInspectionsService`; returns `PropertyDetailDto`

**DTO:**
```csharp
public record PropertyDetailDto(
    string PropertyId,
    string DisplayAddress,
    string Kind,
    IReadOnlyList<EquipmentSummary> Equipment,
    LeaseSummary? ActiveLease,
    int OpenWorkOrderCount,
    DateOnly? LastInspectionDate,
    string? LastInspectionResult
);
```

**Tests:** At least 1 Bridge integration test: property with equipment + lease + open WO returns populated `PropertyDetailDto`.

---

### PR 3 — Work Orders management view

**Anchor Blazor** — `accelerators/anchor/Components/Pages/WorkOrders/`:
- `WorkOrderListPage.razor` — `@page "/cockpit/work-orders"` — filterable list (status, property, vendor, date range); shows vendor name, appointment date if set, status chip
- `WorkOrderDetailPage.razor` — `@page "/cockpit/work-orders/{WorkOrderId}"` — full detail: entry notice, completion attestation, appointment, vendor contact, linked inspection if any, audit trail (last 10 events)

**Bridge React** — `apps/anchor-react/src/cockpit/work-orders/`:
- `WorkOrderListView.tsx` + `WorkOrderDetailView.tsx`
- Bridge endpoints: `GET /cockpit/work-orders` (paginated, filter params) + `GET /cockpit/work-orders/{id}` (detail)

**Tests:** List endpoint returns paginated results filtered by `status=open`; detail endpoint 404s on unknown ID.

---

### PR 4 — Vendors list + 1099 readiness

**Anchor Blazor** — `accelerators/anchor/Components/Pages/`:
- `VendorListPage.razor` — `@page "/cockpit/vendors"` — table: vendor name, specialty badge, W-9 status chip (`Filed` / `Awaiting` / `Overdue`), YTD payments, 1099 readiness badge (⚠ if W-9 missing + payments > $600)
- `VendorDetailPage.razor` — `@page "/cockpit/vendors/{VendorId}"` — vendor profile, contact list, performance log (last 5 entries), work-order history

**Bridge React** — `apps/anchor-react/src/cockpit/vendors/`:
- `VendorListView.tsx` + `VendorDetailView.tsx`
- Bridge endpoints: `GET /cockpit/vendors` + `GET /cockpit/vendors/{id}`

**1099 readiness logic** (in a `VendorTaxReadinessProjection` class in the Bridge handler):
```
needsForm1099 = vendor.OnboardingState == Completed
    && vendor.W9Document == null
    && ytdPayments > 600.00m
```

**Tests:** Vendor with no W-9 + $650 in payments is flagged `needsForm1099 = true`.

---

### PR 5 — Property dashboard (metrics + calendar strip)

**Anchor Blazor** — `accelerators/anchor/Components/Pages/`:
- `PropertyDashboardPage.razor` — `@page "/cockpit"` (default cockpit landing after property selected):
  - Vacancy rate: `vacantUnits / totalUnits * 100`
  - Renewal radar: leases expiring in ≤30 / 31-60 / 61-90 days (grouped)
  - Open work-order count by status (open / in-progress / blocked)
  - Overdue inspections: last inspection > 12 months ago by unit

**Bridge React** — `apps/anchor-react/src/cockpit/`:
- `DashboardView.tsx` — same 4 metrics widgets
- Bridge endpoint: `GET /cockpit/{propertyId}/dashboard` — returns `DashboardDto`

**DTO:**
```csharp
public record DashboardDto(
    int TotalUnits,
    int VacantUnits,
    IReadOnlyList<RenewalBucket> UpcomingRenewals,
    WorkOrderSummary WorkOrders,
    IReadOnlyList<string> OverdueInspectionUnitIds
);
```

**Tests:** Dashboard endpoint with 2 units (1 vacant, 1 with lease expiring in 25 days) returns correct bucket counts.

---

### PR 6 — docs + ledger flip

- `apps/docs/cockpit/overview.md` — multi-actor permissions matrix (from this hand-off), architecture decisions, phase plan
- `apps/docs/cockpit/permissions.md` — full role table
- W#29 workstream source flip to `built`; re-run `render-ledger.py`

---

## Acceptance criteria

- [ ] Cockpit nav sidebar appears in Anchor with links to Properties, Work Orders, Vendors, Dashboard
- [ ] Property list shows all properties for the authenticated tenant
- [ ] Property detail aggregates equipment + lease + WO count + inspection date
- [ ] Work order list filters by status; detail shows full audit trail
- [ ] Vendor list renders 1099 readiness badge correctly
- [ ] Dashboard shows vacancy rate + renewal radar for selected property
- [ ] Blazor (Anchor) and React (Bridge) parity: same data, same structure
- [ ] CockpitPolicy blocks unauthenticated access (401)
- [ ] CockpitPermissions matrix encoded as compile-time constants (not magic strings)
- [ ] `apps/docs/cockpit/` docs page live

---

## Halt conditions

- **PR 5 (Dashboard) gates on W#62.** `IPropertyUnitRepository.ListByPropertyAsync` (units for a property), `WorkOrder.PropertyId` FK, and cross-property inspection lookup all come from W#62 (`blocks-properties-property-unit-substrate-stage06-handoff.md`). Do NOT start PR 5 until W#62 PR 1+2+3 are merged. PRs 3 and 4 are unaffected; ship them while W#62 builds. The existing halt condition below remains for any other missing service method that is NOT covered by W#62.
- If `ILeaseRepository`, `IWorkOrderService`, or `IInspectionsService` lack cross-property query methods needed for the dashboard **beyond what W#62 provides**, STOP and file a `cob-question-*.md` naming the missing method + the interface file — do not add it without a research ruling.
- If existing Anchor layout/nav pattern changes are needed beyond adding pages, STOP and confirm with research session — avoid breaking the existing pages (Home, TeamSwitcher, CrewChat, Onboarding).
- Receipts integration: defer entirely — W#26 is blocked. Do not stub receipt views.

---

## PR order

1 (shell) → 2 (properties) → 3 (work orders) → 4 (vendors) → 5 (dashboard) → 6 (docs + flip). PRs 2-5 can be interleaved once PR 1 lands. Do NOT bundle more than 2 in one PR.
