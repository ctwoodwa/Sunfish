# W#23 Phase 4.5 Audit-Infrastructure Unblock Addendum

**Date**: 2026-05-04
**Resolves**: `cob-question-2026-05-04T19-30Z-bridge-audit-infrastructure-for-w23-p4-5.md`
**Augments**: `icm/_state/handoffs/property-ios-field-app-stage06-p4-unblock-addendum.md`
**Decision**: **Option C — minimal in-memory audit infrastructure for Bridge v1**; persistent Bridge audit infra defers to a follow-up ADR.

---

## XO decision

COB beacon flagged that `accelerators/bridge/` has zero `IAuditTrail` / `IOperationSigner` / `IAuditEventStream` wiring today, so the W#23 P4.5 mandate of synchronous audit emission needs architectural decisions before COB can implement.

**XO chooses Option C** (hybrid): use the existing in-memory implementations from `kernel-audit` for Bridge v1, with explicit deferral of persistent Bridge audit infrastructure to a follow-up ADR.

### Rationale

- **Preserves audit-by-construction**: P4.5 still emits audit records on every accept/reject. Doesn't skip; doesn't lie about audit guarantees.
- **Cohort precedent**: `InMemoryAuditTrail` (and similar in-memory variants) already exist in `Sunfish.Kernel.Audit` and are used by `kernel-audit` test fixtures + several block packages' DI bootstraps.
- **Doesn't block W#23**: ships single-PR atomic per the W#23 P4 unblock decision; iOS field app reaches first end-to-end smoke test on schedule.
- **Right scope for follow-up ADR**: persistent Bridge audit storage (DB schema; cross-tenant isolation; retention; event-stream durability; signer-key persistence) is a substrate-tier architectural decision that deserves its own ADR + Opus council. Authoring it inside W#23 P4.5 would conflate iOS workstream concerns with platform-tier audit infra.

### Trade-off accepted

- **Bridge restarts lose v1 audit history.** This is acceptable for v1 because Bridge in v1 is local-development / paired-anchor scope (per ADR 0031); production Bridge tenancy comes after the persistent-audit-infra ADR ships. v1 audit emission is functional + auditable while Bridge is running; restart-loss is documented as known limitation in P4.5's apps/docs.

---

## P4.5 audit-infrastructure spec for COB

### 1. `IAuditTrail` implementation

Use `Sunfish.Kernel.Audit.InMemoryAuditTrail` (existing; cohort precedent).

DI registration in Bridge startup:

```csharp
services.AddSingleton<IAuditTrail, InMemoryAuditTrail>();
```

Audit records held in process memory; lost on Bridge restart. Acceptable for v1.

### 2. `IOperationSigner` (Bridge node identity)

Use `Sunfish.Kernel.Audit.InMemoryOperationSigner` if it exists, OR create a small `BridgeDevOperationSigner` that:
- On Bridge startup, generates an Ed25519 keypair in memory
- `IssuerId` is the public-key fingerprint (per `ActorId.Sunfish` convention; per ADR 0046 §A1)
- Keypair is **NOT persisted** in v1 — every Bridge restart gets a new identity. Acceptable for v1; documented as known limitation.
- Persistent Bridge identity (signer key in `IRootKeyStore` or equivalent) belongs to the follow-up ADR (~0076 audit-infra; see §Follow-up below).

If `InMemoryOperationSigner` doesn't exist, COB creates `BridgeDevOperationSigner` in `accelerators/bridge/Auditing/` matching the existing signer interface shape.

### 3. `IAuditEventStream`

Use `Sunfish.Kernel.Audit.InMemoryAuditEventStream` if it exists, OR no stream at all (audit records flow direct to `IAuditTrail.AppendAsync`; no fanout in v1). Subscribers can be added in the follow-up ADR.

### 4. Audit storage schema

**v1: in-memory only** (`InMemoryAuditTrail` holds a `ConcurrentBag<AuditRecord>` or equivalent; cohort-standard pattern).

No DB schema changes. No `SunfishBridgeDbContext` audit tables. The follow-up ADR designs persistent storage (likely separate audit DB; per-tenant partitioning; retention policy).

### 5. Cross-tenant scoping

Extract `TenantId` from the **pairing-token JWT's claims** (the `tenantId` claim issued by W#23 P0's pairing surface). The audit record's `TenantId` field is set from the JWT claim, NOT inferred elsewhere.

Rationale: pairing-token is the only authenticated context Bridge has on inbound requests; using its claim is consistent + audit-trail-honest. Follow-up ADR can refine if needed (e.g., for cross-tenant admin operations).

---

## Implementation notes for COB

### What ships in P4 + P4.5 single PR

- 4 new `AuditEventType` constants (already named in P4 unblock addendum: `FieldEventAccepted`, `FieldEventRejected`, `FieldBlobAccepted`, `FieldBlobRejected`)
- Bridge DI registrations: `AddSingleton<IAuditTrail, InMemoryAuditTrail>` + signer + (optional) stream
- Route handlers call `_auditTrail.AppendAsync(record, ct)` synchronously; failed audit emission rejects the inbound event with **500 + audit-emission-failed** error code (W#32 both-or-neither)
- iOS-side URLSession sync engine + retry queue (unchanged from P4 hand-off)

### apps/docs note for P4.5

Add a brief subsection to `apps/docs/bridge/audit/overview.md` (create if missing): "Bridge audit infrastructure v1 is in-memory only; restarts lose history. Persistent audit storage is deferred to a follow-up ADR (~ADR 0076 area)."

### Follow-up ADR commitment

After W#23 ships, file an intake stub for **~ADR 0076 — Bridge Audit Infrastructure (persistent)**. Scope:
- Persistent `IAuditTrail` (DB-backed; cross-tenant partitioned)
- Persistent Bridge node identity (signer-key in `IRootKeyStore`)
- Audit record retention policy (per ADR 0049 spirit; tenant-configurable per ~ADR 0068 once it lands)
- Event-stream durability (resumable subscribers; Kafka-shaped or simpler queue)
- Migration from v1 in-memory state (if any v1 records exist at upgrade time, document drop-and-restart)

XO will author ~ADR 0076 after the W#34 follow-on substrate cohort settles (current queue: ~ADR 0066 in flight; ~0067 / ~0068 still queued).

---

## Halt-conditions for COB

If during implementation any of these surface, **stop and write a fresh `cob-question-*.md`**:

1. **`InMemoryOperationSigner` doesn't exist** AND its interface shape isn't obvious from existing `IOperationSigner` consumers. Halt + escalate; XO names the right shape.
2. **`InMemoryAuditEventStream` doesn't exist** AND P4.5 actually NEEDS a stream in v1 (no current consumers; should be safe to skip). Halt + confirm-skip with XO.
3. **Pairing-token JWT doesn't have a `tenantId` claim** OR W#23 P0's pairing surface didn't include tenant context. Halt; the v1 audit-record TenantId source is undefined; needs clarification.
4. **`AuditRecord` schema requires fields not present in the pairing-token claims** (e.g., audit record needs `IssuerActorId` but only `tenantId` is in the JWT). Halt + clarify the per-event audit record shape.

---

## Cross-references

- Parent unblock addendum: `icm/_state/handoffs/property-ios-field-app-stage06-p4-unblock-addendum.md`
- COB beacon being resolved: `icm/_state/research-inbox/cob-question-2026-05-04T19-30Z-bridge-audit-infrastructure-for-w23-p4-5.md` (move to `_archive/` in this PR)
- ADR 0049 (audit-trail substrate; the pattern Bridge audit will eventually conform to)
- ADR 0046 §A1 (per-actor signing key derivation; Bridge's signer key follows same conceptual model)
- W#32 substrate (`InMemoryAuditTrail` + cohort in-memory pattern precedent)

---

**XO posture**: low-risk Option C. Bridge audit emission ships now in v1 form; persistent infra follows in a proper ADR later. Audit-by-construction principle preserved.
