---
type: question
workstream: 19
last-pr: 269
---

W#19 Phases 1+2 shipped (PRs #267 + #269 in flight). Phase 3 (api-change-shape WorkOrder migration per A6) blocked: spec references types that don't exist in source on origin/main — `Money` (ADR 0051; nullable in 2 fields), `ThreadId` + `IThreadStore` (ADR 0052; halt-condition explicitly named), `IPaymentGateway` (halt-condition), and the 3 Phase 5 child entity types (`WorkOrderAppointment`, `WorkOrderCompletionAttestation`, `WorkOrderEntryNotice` — Phase 3 references them but hand-off creates them in Phase 5).

What would unblock me: addendum picking shape for each — (a) inline-introduce minimal stubs in foundation for Money/ThreadId, (b) defer (use `decimal?` for Money + omit/string ThreadId), or (c) wait for ADR 0051/0052 Stage 06; AND order-of-construction for Phase 3 vs Phase 5 child types (does Phase 3 declare them as forward references or are they Phase 5-only?). Same Option-A pattern as W#31's IdentityRef → ActorId resolution.
