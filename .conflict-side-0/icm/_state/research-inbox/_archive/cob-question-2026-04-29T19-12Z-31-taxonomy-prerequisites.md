---
type: question
workstream: 31
last-pr: 248
---

Workstream #31 Stage 06 build halted at pre-build audit on two prerequisite gaps explicitly listed in the hand-off's halt-conditions: (1) `IdentityRef` type doesn't exist anywhere in repo (referenced 11x in spec; shape undefined; OQ-4 vs halt-condition contradict each other on whether COB or XO introduces it); (2) `IAuditRecord` interface doesn't exist in `kernel-audit` — `AuditRecord` is sealed and extensibility is via `AuditEventType` discriminator + opaque payload-body, per ADR 0049. Full analysis + recommended Option A on each in user memory `project_workstream_31_taxonomy_handoff_halts.md`.

What would unblock me: revised hand-off (or addendum) that picks the IdentityRef shape (recommend reuse `ActorId` with new `ActorId.Sunfish` sentinel) AND replaces the `IAuditRecord` types with 8 new `AuditEventType` constants + payload-body factories per existing kernel-audit pattern. Also resolve OQ-4 / halt-condition wording conflict.
