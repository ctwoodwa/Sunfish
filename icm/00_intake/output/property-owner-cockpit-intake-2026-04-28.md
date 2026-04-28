# Intake Note — Owner Web Cockpit

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (cumulative — owner needs a desk-grade view of everything the field app captures + the leasing pipeline).
**Pipeline variant:** `sunfish-feature-change`
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)

---

## Problem Statement

Field-grade iOS captures + Bridge-served public surfaces require a desk-grade owner cockpit to triage, manage, and report. The cockpit is where the BDFL (and spouse) spend hours per week running the business: reviewing inquiries, dispatching work orders, monitoring vendor performance, reconciling receipts, drafting leases, generating tax-advisor exports.

This intake captures the cockpit's surface, package structure, and integration with all cluster modules. It is a *consumer* of every other intake; it does not introduce its own domain entities.

## Scope Statement

### In scope

1. **Anchor desktop cockpit views.** Property list, property detail (with assets, leases, work orders, inspections, receipts, mileage rolling up); inquiry inbox; pipeline kanban (Inquiry → CriteriaSent → … → TenancyStart); work-order list with vendor + appointment + status; receipt inbox with reconciliation; vendor list + W-9 status + 1099 readiness.
2. **Bridge web cockpit views.** Same domain coverage, web-rendered, for use when away from the Anchor desktop. Multi-actor: owner, spouse, bookkeeper, tax advisor each see their permitted slice (capability-driven UI per ADR 0032).
3. **Multi-actor permissions matrix.** Captures cluster OQ1 — explicit catalog of permissions per role per record class.
4. **Dashboards.** Per-property P&L (Phase 2 commercial intake feeds revenue/expense; this surfaces); next-inspection-due calendar; lease expiry calendar; vacancy days running total; vendor 1099 progress (gate to year-end task).
5. **Reporting (Phase 2.3).** Annual tax-advisor export, mileage summary, depreciation schedule, vendor 1099 export, maintenance forecast.
6. **`blocks-owner-cockpit-views` package** OR cockpit views distributed across each domain block. Stage 02 decides.
7. **Adapter parity.** Blazor + React both render every cockpit view via the existing ui-adapters infrastructure.

### Out of scope

- Domain entities — every sibling intake
- Lease holder portal (Phase 3)
- Mobile cockpit views — iOS app handles field-grade flows; on-the-road cockpit access is via Bridge web on iPad/phone (deferred to Phase 2.2 as a quality pass)
- AI-assisted insights / anomaly detection — Phase 4+

---

## Affected Sunfish Areas

- Anchor (existing accelerator) — new property-ops cockpit module
- Bridge (existing accelerator) — new property-ops cockpit module
- `blocks-owner-cockpit-views` (new, possibly) — or distributed across domain blocks
- ADR 0032 (multi-team workspace switching) — the cockpit is the canonical multi-tenant cockpit demonstrating per-tenant feature trimming

## Acceptance Criteria

- [ ] Multi-actor permissions matrix documented (resolves OQ1)
- [ ] Property detail view aggregating all cluster modules' data
- [ ] Inquiry inbox + pipeline kanban
- [ ] Work-order list + detail
- [ ] Receipt inbox + reconciliation triage
- [ ] Vendor list + 1099 readiness
- [ ] Per-property dashboard with P&L slice
- [ ] Calendar views (inspection-due, lease-expiry, showing-schedule)
- [ ] Adapter parity tests across all views
- [ ] Capability-driven UI trimming demonstrated (bookkeeper sees receipts only; tax advisor sees reports only)
- [ ] kitchen-sink demo: full cockpit populated for 2 properties + 1 vacant unit + 3 vendors
- [ ] apps/docs entry covering cockpit + multi-actor permissions

## Open Questions

| ID | Question | Resolution |
|---|---|---|
| OQ-OC1 | One package vs distributed cockpit views per domain block | Stage 02 — recommend distributed; each domain block owns its cockpit view; cockpit shell composes |
| OQ-OC2 | Bridge cockpit auth: same identity model as Anchor (paired device + token) or web SSO? | Stage 02 — share identity, web session via macaroon + secure cookie |
| OQ-OC3 | Real-time updates: poll vs SignalR / WebSocket | Stage 02 — poll-on-focus for Phase 2.1; SignalR for Phase 2.3 |
| OQ-OC4 | Bookkeeper-specific Anchor variant per ADR 0032 capability trimming, or single Anchor with feature flags? | Stage 02 — single Anchor, feature flags per ADR 0032 |

## Dependencies

**Blocked by:** Every sibling intake (consumer)
**Blocks:** Nothing directly (cockpit ships incrementally as siblings ship)

## Cross-references

- Parent: [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
- Every sibling intake
- ADR 0032 (multi-team workspace switching)

## Sign-off

Research session — 2026-04-28
