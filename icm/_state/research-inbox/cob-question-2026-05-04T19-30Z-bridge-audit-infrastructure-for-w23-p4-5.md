---
type: question
workstream-or-chapter: W#23 P4+P4.5 (atomic Bridge field-event/blob endpoints)
last-pr: "(WIP shelf) feat/w23-p4-and-p4-5-field-endpoints — 4 AuditEventType constants + endpoint stubs returning 501"
---

W#23 P4+P4.5 unblock addendum mandates synchronous audit emission from the
Bridge field-event + field-blob route handlers (W#32 both-or-neither: failed
audit emission must reject the event with 500). But `accelerators/bridge/`
has **zero IAuditTrail / IOperationSigner / IAuditEventStream wiring** —
this would be the first audit-emitting flow on Bridge.

Architectural decisions XO needs to make before COB can implement:
(1) which IAuditTrail implementation (in-memory / persistent / per-tenant);
(2) which IOperationSigner (Bridge node identity key — where stored, how
issued); (3) which IAuditEventStream (event bus or inline); (4) audit
storage schema (separate DB, embedded in SunfishBridgeDbContext, or in-memory
only); (5) cross-tenant scoping (Bridge is multi-tenant — TenantId resolution).

What would unblock me: a small Bridge-audit-infrastructure ADR or hand-off
addendum specifying the 5 decisions above. Or, alternatively, narrow the
P4.5 scope to skip audit emission for substrate v1 + file a follow-up to
add audit emission once Bridge audit infra ships.
