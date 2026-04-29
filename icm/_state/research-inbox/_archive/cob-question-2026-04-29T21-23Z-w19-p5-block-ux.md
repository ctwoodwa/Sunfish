---
type: question
workstream: 19
last-pr: 284
---

W#19 Phases 1, 2, 0+3, 4 shipped (PRs #267 + #269 + #281 + #284 in flight). Phase 5 (api-change-shape WorkOrder schema migration) ready to start — but `WorkOrderListBlock.razor` currently does `wo.RequestId` then `MaintenanceService.GetRequestAsync(wo.RequestId)` to render the originating maintenance request alongside the work order. Dropping `RequestId` per the addendum breaks this UI feature.

What would unblock me: pick the UX strategy for the block during Phase 5 — (A) **drop the request-fetching feature** (block shows work-order info only; operator deduces source from elsewhere; lowest churn, matches "polymorphic source via first audit event" but UX regression); (B) **synthesize source from audit query** (block calls `IAuditTrail.QueryAsync` to find the first `WorkOrderCreated` audit record's payload and resolves source from there; correct long-term but substantial UI refactor); (C) **defer dropping `RequestId` to a follow-up PR**; keep the field through Phase 5 and hand-off a separate cleanup phase post-Phase 6 wiring. Recommend Option A for Phase 5 + a TODO comment pointing at a follow-up Phase 5.1 if Option B is wanted.
