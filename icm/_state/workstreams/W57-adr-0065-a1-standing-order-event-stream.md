---
sort_order: 66
number: 57
slug: adr-0065-a1-standing-order-event-stream
title: "**ADR 0065-A1 — Standing Order event-stream contract** (`sunfish-feature-change` pipeline; addendum to W#42) — adds `StandingOrderAppliedEvent` + `IStandingOrderEventStream` + `InMemoryStandingOrderEventStream` to `packages/foundation-wayfinder/` + one new `AuditEventType.StandingOrderApplied` constant in `packages/kernel-audit/`; clears W#46 Phase-3 halt-condition C + W#53 Phase-2 H8"
status: "built"
status_cell: "`built` (shipped 2026-05-06 PR #662 — StandingOrderAppliedEvent + IStandingOrderEventStream + InMemoryStandingOrderEventStream in foundation-wayfinder + AuditEventType.StandingOrderApplied; clears W#46 halt-C + W#53 H8)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/wayfinder-adr-0065-a1-event-stream-handoff.md` + `docs/adrs/0065-wayfinder-system-and-standing-order-contract.md` §Amendment A1"
---

## Notes

**Hand-off authored 2026-05-06.** Additive to `packages/foundation-wayfinder/` — no new package. ~2-3h / 1 PR + 1 ledger-flip PR. All prerequisite symbols verified on origin/main; all introduced symbols confirmed absent. **Unblocks:** (1) W#46 Phase 3 halt-condition C (subscribe-before-load cache invalidation in `DefaultPermissionResolver`); (2) W#53 Phase 2 H8 (Helm widget reactive refresh for `recent-standing-orders` + `quick-toggles`); (3) future W#43 `WayfinderFeatureProvider` cache-invalidation path (`feature-management.*` Standing Order consumer). Pre-merge council mandatory.
