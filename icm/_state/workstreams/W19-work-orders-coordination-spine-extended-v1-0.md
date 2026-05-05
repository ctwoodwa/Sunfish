---
sort_order: 18
number: 19
slug: work-orders-coordination-spine-extended-v1-0
title: "Work Orders coordination spine (cluster #3 spine) — **EXTENDED `blocks-maintenance` v1.0**"
status: "built"
status_cell: "`built` (all 8 phases shipped 2026-04-30; `Sunfish.Blocks.Maintenance` v1.0 released)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM ✓"
reference_cell: "`icm/00_intake/output/property-work-orders-intake-2026-04-28.md` + `docs/adrs/0053-work-order-domain-model.md` + `icm/_state/handoffs/property-work-orders-stage06-handoff.md` + `icm/_state/handoffs/property-work-orders-stage06-addendum.md` (Phase 3 prereqs) + `icm/_state/handoffs/property-work-orders-stage06-phase5-addendum.md` (Phase 5.1 source-display via audit-query)"
---

## Notes

**Built 2026-04-30.** Shipped via 8 PRs: Phase 1 (#267, TransitionTable visibility) → Phase 2 (#269, WorkOrderStatus enum +5 states) → Phase 0+3 bundled (#281, foundation-integrations Money/ThreadId stubs + 3 child entities) → Phase 4 (#284, 18 AuditEventType + emission) → Phase 5 (#301, schema migration + RequestId drop, MAJOR version bump v0.x→v1.0) → Phase 5.1 (#304, WorkOrderListBlock source-display via IAuditTrail.QueryAsync per Option-A addendum) → Phase 6 (#314, cross-package wiring: IThreadStore opens 2-party thread on Create + IPaymentGateway authorizes on Invoiced/captures on Paid + ISignatureCapture available) → Phase 7+8 (this PR, apps/docs work-orders page + ledger flip). 18 AuditEventType emitted. 3 child entities: WorkOrderEntryNotice + WorkOrderCompletionAttestation + WorkOrderAppointment. 69/69 maintenance tests passing. `MIGRATION.md` documents v0.x→v1.0 breaking changes.
