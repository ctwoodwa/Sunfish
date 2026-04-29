# Workstream #19 — Work Orders coordination spine — Stage 06 hand-off

**Workstream:** #19 (Work Orders coordination spine — EXTEND `blocks-maintenance`)
**Spec:** [ADR 0053](../../docs/adrs/0053-work-order-domain-model.md) (Accepted 2026-04-29; amendments A1–A9 landed)
**Pipeline variant:** `sunfish-api-change` (per ADR 0053 amendment A6 — schema migration is api-change-shape)
**Estimated effort:** 12–19 hours focused sunfish-PM time (per ADR 0053 amendment A7)
**Decomposition:** 8 phases shipping as ~6 separate PRs to keep review surface manageable
**Prerequisites:** W#31 Foundation.Taxonomy Phase 1 BUILT ✓ (PR #263); ADR 0053 amendments A4–A9 LANDED ✓ (PR #262)

---

## Scope summary

Extend the existing `packages/blocks-maintenance/` block (which already ships `WorkOrder` + `WorkOrderStatus` + `TransitionTable.cs` + `IMaintenanceService` + `WorkOrderListBlock.razor` for ~85% of cluster scope) with the cluster-contributed pieces:

1. **State-set extension** (5 new states; per ADR 0053 amendment A4) — `AwaitingSignOff`, `Invoiced`, `Paid`, `Disputed`, `Closed`
2. **Schema migration** (positional → init-only record + drop `RequestId` + add ~10 init-only fields; per A6) — api-change-shape
3. **Child entities** (per ADR 0053 §"Decision") — `WorkOrderEntryNotice`, `WorkOrderCompletionAttestation`, `WorkOrderAppointment`
4. **Audit emission** (17 new `AuditEventType` constants per A8) — one per state transition + one per child-entity write, payload-body factory pattern
5. **Cross-package wiring** — `IPaymentGateway` (ADR 0051) for `Invoiced`/`Paid`; `IThreadStore.SplitAsync` (ADR 0052) for thread-creation; `kernel-signatures` (ADR 0054) for completion attestation
6. **TransitionTable visibility flip** (per A5) — `internal sealed` → `public sealed`

**NOT in scope:** new package creation (everything lands in `blocks-maintenance`), UI changes to `WorkOrderListBlock.razor` beyond what's needed for the extended state-set, audiobook-pipeline coordination.

---

## Phases (binary PASS/FAIL gates; per-Phase PR pattern)

### Phase 1 — TransitionTable visibility flip (ADR 0053 A5)

**One-keyword change** at `packages/blocks-maintenance/Services/TransitionTable.cs:8`: `internal sealed class TransitionTable<TState>` → `public sealed class TransitionTable<TState>`.

- Update XML doc to clarify it's now part of the public API surface of `blocks-maintenance`
- Update existing unit tests if any assert internal access (they shouldn't but verify)
- No behavior change

**Gate:** `dotnet build` clean; existing 100% tests still pass.

**Estimated:** 0.5h
**PR title:** `chore(blocks-maintenance): expose TransitionTable<TState> as public per ADR 0053 A5`

### Phase 2 — `WorkOrderStatus` enum extension (ADR 0053 A4)

Append 5 net-new values to existing `WorkOrderStatus` enum at `packages/blocks-maintenance/Models/WorkOrderStatus.cs`:

```csharp
public enum WorkOrderStatus
{
    Draft,              // existing
    Sent,               // existing
    Accepted,           // existing
    Scheduled,          // existing
    InProgress,         // existing
    OnHold,             // existing
    Completed,          // existing
    AwaitingSignOff,    // NEW per ADR 0053 — vendor-completed, awaiting BDFL/operator signature attestation
    Invoiced,           // NEW per ADR 0053 — receipt arrived; payment not yet authorized
    Paid,               // NEW per ADR 0053 — payment authorized + captured per ADR 0051
    Disputed,           // NEW per ADR 0053 — side-branch from Invoiced or Paid
    Closed,             // NEW per ADR 0053 — final terminal; all parties settled
    Cancelled,          // existing; UK spelling preserved per A4
}
```

Append matching transitions to `TransitionTable<WorkOrderStatus>` rules (existing class is now public from Phase 1):

```text
Completed → AwaitingSignOff | Invoiced     // bypass sign-off if attestation-not-required
AwaitingSignOff → Invoiced | OnHold        // OnHold if signature-blocked
Invoiced → Paid | Disputed | OnHold
Paid → Closed | Disputed
Disputed → Invoiced | Paid | Closed        // resolution paths
```

Existing transitions unchanged. `Cancelled` remains terminal-from-anywhere-pre-Closed.

Update XML doc on `WorkOrderStatus` to list all 13 values + transition diagram.

Add 6 unit tests covering the new transitions (one per arrow above).

**Gate:** all 13 enum values present + 5 new transition rules + 6 new tests pass + existing tests still pass.

**Estimated:** 2–3h
**PR title:** `feat(blocks-maintenance): extend WorkOrderStatus with 5 post-completion states (ADR 0053 A4)`

### Phase 3 — `WorkOrder` schema migration (ADR 0053 A6)

This is the **api-change-shape** PR. `WorkOrder` is currently a positional record. Migrate to init-only record with 10+ new fields. Drop the `MaintenanceRequestId RequestId` FK; replaces with polymorphic source via the first audit event (per ADR 0053 §"Decision").

**Before:**

```csharp
public sealed record WorkOrder(
    WorkOrderId Id,
    MaintenanceRequestId RequestId,
    WorkOrderStatus Status,
    string Description,
    decimal EstimatedCost,
    decimal? ActualCost,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);
```

**After:**

```csharp
public sealed record WorkOrder
{
    public required WorkOrderId Id { get; init; }
    public required TenantId Tenant { get; init; }                              // IMustHaveTenant per ADR 0008
    public required WorkOrderStatus Status { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public Money? EstimatedCost { get; init; }                                  // ADR 0051 Money type
    public Money? TotalCost { get; init; }                                      // replaces ActualCost; per ADR 0051
    public EquipmentId? Equipment { get; init; }                                // per A1 (was Asset)
    public ThreadId? PrimaryThread { get; init; }                               // per ADR 0052
    public WorkOrderAppointment? Appointment { get; init; }                     // see Phase 5
    public WorkOrderCompletionAttestation? CompletionAttestation { get; init; } // see Phase 5
    public IReadOnlyList<WorkOrderEntryNotice> EntryNotices { get; init; } = []; // see Phase 5
    public IReadOnlyList<AuditId> AuditTrail { get; init; } = [];               // ADR 0049 ref list
    public DateTimeOffset UpdatedAt { get; init; }
}
```

**Migration tasks:**

- Rewrite `WorkOrder.cs` per the new shape
- Update every constructor call site in `blocks-maintenance` to use the init-only pattern
- Update `IMaintenanceService` and `InMemoryMaintenanceService` to construct `WorkOrder` correctly
- Update JSON serializer attributes if needed (System.Text.Json should handle init-only records natively)
- Remove `MaintenanceRequestId` references from `WorkOrder` proper; `MaintenanceRequest` linkage is now via the first emitted audit record's payload body
- Update `WorkOrderListBlock.razor` if it references the dropped `RequestId` field
- Update `apps/docs/blocks/maintenance/` page noting the breaking change
- Add a `MIGRATION.md` note in `packages/blocks-maintenance/` covering the field rename + dropped field

**Gate:** `dotnet build` clean; all existing `WorkOrder`-touching tests updated and passing; `apps/docs` page reflects new shape; MIGRATION.md present.

**Estimated:** 4–6h
**PR title:** `feat(blocks-maintenance)!: migrate WorkOrder to init-only record + Money type (ADR 0053 A6)` (note the `!` indicating breaking change per Conventional Commits)
**Version bump:** MAJOR on `Sunfish.Blocks.Maintenance` package

### Phase 4 — Audit emission (ADR 0053 A8)

Add 17 new `AuditEventType` constants to `packages/kernel-audit/AuditEventType.cs` under a new `===== ADR 0053 — Work Orders =====` divider:

```csharp
// 13 status-transition emissions
public static readonly AuditEventType WorkOrderCreated = new("WorkOrderCreated");           // Draft (initial)
public static readonly AuditEventType WorkOrderSent = new("WorkOrderSent");                 // Draft → Sent
public static readonly AuditEventType WorkOrderAccepted = new("WorkOrderAccepted");         // Sent → Accepted
public static readonly AuditEventType WorkOrderScheduled = new("WorkOrderScheduled");       // Accepted → Scheduled
public static readonly AuditEventType WorkOrderStarted = new("WorkOrderStarted");           // Scheduled → InProgress
public static readonly AuditEventType WorkOrderHeld = new("WorkOrderHeld");                 // InProgress → OnHold
public static readonly AuditEventType WorkOrderResumed = new("WorkOrderResumed");           // OnHold → InProgress
public static readonly AuditEventType WorkOrderCompleted = new("WorkOrderCompleted");       // InProgress → Completed
public static readonly AuditEventType WorkOrderSignedOff = new("WorkOrderSignedOff");       // Completed → AwaitingSignOff (or skip → Invoiced)
public static readonly AuditEventType WorkOrderInvoiced = new("WorkOrderInvoiced");         // → Invoiced
public static readonly AuditEventType WorkOrderPaid = new("WorkOrderPaid");                 // Invoiced → Paid
public static readonly AuditEventType WorkOrderDisputed = new("WorkOrderDisputed");         // → Disputed
public static readonly AuditEventType WorkOrderClosed = new("WorkOrderClosed");             // → Closed
public static readonly AuditEventType WorkOrderCancelled = new("WorkOrderCancelled");       // → Cancelled

// 4 child-entity emissions
public static readonly AuditEventType WorkOrderEntryNoticeRecorded = new("WorkOrderEntryNoticeRecorded");
public static readonly AuditEventType WorkOrderAppointmentScheduled = new("WorkOrderAppointmentScheduled");
public static readonly AuditEventType WorkOrderAppointmentConfirmed = new("WorkOrderAppointmentConfirmed");
public static readonly AuditEventType WorkOrderCompletionAttestationCaptured = new("WorkOrderCompletionAttestationCaptured");
```

Add `WorkOrderAuditPayloadFactory` at `packages/blocks-maintenance/Audit/WorkOrderAuditPayloadFactory.cs` mirroring the W#31 `TaxonomyAuditPayloadFactory` pattern. One static method per event type that returns an `AuditPayload` with the appropriate body keys (e.g., `work_order_id`, `previous_status`, `new_status`, `actor`, `correlation_id`).

Wire `InMemoryMaintenanceService` to call `IAuditTrail.AppendAsync(...)` after each status transition + child-entity write.

Add 17 unit tests verifying audit emission for each event type (test factory output round-trips through `AuditRecord`; assert event type discriminator + payload body keys + tenant + format version).

**Gate:** 17 new event types present; factory ships; service wires audit calls; 17 tests pass.

**Estimated:** 2–3h
**PR title:** `feat(blocks-maintenance): WorkOrder audit emission — 17 AuditEventType + factory (ADR 0053 A8)`

### Phase 5 — Child entities (ADR 0053 §"Decision")

Author 3 new child entity types in `packages/blocks-maintenance/Models/`:

**`WorkOrderEntryNotice.cs`** — right-of-entry notice; multiple per `WorkOrder`:

```csharp
public sealed record WorkOrderEntryNotice
{
    public required WorkOrderEntryNoticeId Id { get; init; }
    public required WorkOrderId WorkOrder { get; init; }
    public required DateTimeOffset PlannedEntryUtc { get; init; }
    public required string EntryReason { get; init; }
    public required ActorId NotifiedBy { get; init; }
    public required DateTimeOffset NotifiedAt { get; init; }
    public IReadOnlyList<PartyId> NotifiedParties { get; init; } = [];
}
```

**`WorkOrderCompletionAttestation.cs`** — signature-bound completion attestation (per ADR 0054):

```csharp
public sealed record WorkOrderCompletionAttestation
{
    public required WorkOrderCompletionAttestationId Id { get; init; }
    public required WorkOrderId WorkOrder { get; init; }
    public required SignatureEventRef Signature { get; init; } // ADR 0054 reference
    public required DateTimeOffset AttestedAt { get; init; }
    public required ActorId Attestor { get; init; }
    public string? AttestationNotes { get; init; }
}
```

**`WorkOrderAppointment.cs`** — appointment slot, CP-class with Flease lease coordination (per A9 + ADR 0028):

```csharp
public sealed record WorkOrderAppointment
{
    public required WorkOrderAppointmentId Id { get; init; }
    public required WorkOrderId WorkOrder { get; init; }
    public required DateTimeOffset SlotStartUtc { get; init; }
    public required DateTimeOffset SlotEndUtc { get; init; }
    public required AppointmentStatus Status { get; init; }    // Proposed | Confirmed | Cancelled
    public required ActorId ProposedBy { get; init; }
    public ActorId? ConfirmedBy { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
}

public enum AppointmentStatus { Proposed, Confirmed, Cancelled }
```

Repository extension — add 3 new repository interfaces or extend `IMaintenanceService`:

- `IMaintenanceService.RecordEntryNoticeAsync(WorkOrderEntryNotice notice, ActorId actor, CancellationToken ct)`
- `IMaintenanceService.ProposeAppointmentAsync(WorkOrderAppointment proposed, ActorId actor, CancellationToken ct)`
- `IMaintenanceService.ConfirmAppointmentAsync(WorkOrderAppointmentId id, ActorId actor, CancellationToken ct)`
- `IMaintenanceService.CaptureCompletionAttestationAsync(WorkOrderCompletionAttestation attestation, ActorId actor, CancellationToken ct)`

Each method emits the corresponding `AuditEventType` from Phase 4.

`WorkOrderAppointment` lease-coordination: when `ProposeAppointmentAsync` fires, acquire a Flease lease per ADR 0028 on the slot range (prevents double-booking under partition). For Phase 1 in-memory implementation, an in-process lock suffices; the lease coordination is a TODO comment for Phase 2 distributed implementation. **Important:** explicitly comment "Phase 2 will replace with Flease primitive per ADR 0028" so the in-memory shim doesn't outlive its expiration.

Add 12 unit tests (3 entity types × 4 lifecycle scenarios each, roughly).

**Gate:** 3 entity types ship; service methods wire audit emission; 12 tests pass.

**Estimated:** 3–5h
**PR title:** `feat(blocks-maintenance): child entities — EntryNotice + CompletionAttestation + Appointment (ADR 0053)`

### Phase 6 — Cross-package wiring

Wire `WorkOrder` lifecycle to consume the now-real substrates:

- **`IPaymentGateway` (ADR 0051):** when `WorkOrder.Status` transitions to `Invoiced` or `Paid`, the service calls into `IPaymentGateway` for the actual charge/capture flow. Define exactly: does `MaintenanceService` orchestrate this, or is `IPaymentGateway` injected separately and the consumer block (e.g., `blocks-rent-collection` future, or a new `blocks-property-finance`) drives this orchestration? **Recommendation: inject `IPaymentGateway` into `MaintenanceService` for Phase 1; revisit for Phase 2 if a separate orchestration block is wanted.**
- **`IThreadStore.SplitAsync` (ADR 0052):** when a `WorkOrder` is created, optionally create a `PrimaryThread` for tenant ↔ vendor coordination. `MaintenanceService.CreateAsync(...)` accepts an optional `bool createThread = true` parameter; if true, it calls `IThreadStore.CreateAsync` for a 2-party thread (tenant + vendor) and stores the resulting `ThreadId` on the `WorkOrder`.
- **`kernel-signatures` (ADR 0054):** when capturing completion attestation, the signature event is created via `kernel-signatures.ISignatureCapture.CaptureAsync(...)`. Phase 1 in-memory shim is acceptable; ADR 0054 Stage 06 work will land separately and the wiring point uses the canonical interface.

Add 6 integration tests covering the cross-package interactions.

**Gate:** wiring works end-to-end in InMemory mode; tests pass.

**Estimated:** 1–2h
**PR title:** `feat(blocks-maintenance): cross-package wiring — payments + messaging + signatures`

### Phase 7 — Tests + apps/docs migration note

Final-pass integration test suite covering a full `WorkOrder` lifecycle end-to-end (`Draft → Sent → Accepted → Scheduled → InProgress → Completed → AwaitingSignOff → Invoiced → Paid → Closed`) plus the side-branches (`OnHold` resume, `Disputed` resolution).

Update `apps/docs/blocks/maintenance/work-orders.md` (create if missing) with:
- The new state-set (13 values + transition diagram)
- The 17 `AuditEventType` constants
- The 3 child entities + their lifecycle methods
- A migration callout (BREAKING: positional → init-only; dropped `RequestId`; new `Money?` types)
- Example: full lifecycle code sample using the InMemory implementation

**Gate:** `apps/docs` builds clean; integration test passes.

**Estimated:** 1h
**PR title:** `docs(blocks-maintenance): WorkOrder Phase 1 substrate apps/docs page`

### Phase 8 — Ledger update

Update `icm/_state/active-workstreams.md` row #19 from `ready-to-build` → `built` with the merged PR list. Append entry to `## Last updated` footer.

**Gate:** ledger row reflects current state.

**Estimated:** 0.5h (lands in the same PR as Phase 7 or as a chore-class follow-up)
**PR title:** `chore(icm): flip W#19 ledger row → built`

---

## Total decomposition

| Phase | Subject | Hours | PR |
|---|---|---|---|
| 1 | TransitionTable visibility | 0.5 | `chore(blocks-maintenance): TransitionTable public` |
| 2 | WorkOrderStatus enum extension + transitions | 2–3 | `feat(blocks-maintenance): extend WorkOrderStatus` |
| 3 | WorkOrder schema migration (api-change!) | 4–6 | `feat(blocks-maintenance)!: migrate WorkOrder` |
| 4 | Audit emission (17 AuditEventType + factory) | 2–3 | `feat(blocks-maintenance): WorkOrder audit` |
| 5 | Child entities (3 types + service methods) | 3–5 | `feat(blocks-maintenance): child entities` |
| 6 | Cross-package wiring (payments + messaging + signatures) | 1–2 | `feat(blocks-maintenance): cross-package wiring` |
| 7 | Tests + apps/docs | 1 | `docs(blocks-maintenance): work-orders page` |
| 8 | Ledger flip | 0.5 | `chore(icm): W#19 → built` |
| **Total** | | **14.5–20.5h** | **8 PRs** |

Within ADR 0053 amendment A7's 12–19h estimate; the higher end accommodates the cross-package wiring (Phase 6) which depends on stub interfaces that may need debugging.

---

## Halt conditions

Per the inbox protocol, halt + write `cob-question-*` beacon if:

- ADR 0054 (`kernel-signatures`) interface signature is unclear at Phase 6 → write beacon naming the specific interface contract question
- `IPaymentGateway` injection pattern produces a circular dependency → write beacon
- `IThreadStore.SplitAsync` doesn't exist yet (it's specified in ADR 0052 amendment A2 but Stage 06 of ADR 0052 hasn't shipped) → write beacon; XO may unblock by stubbing the interface in `foundation-integrations` ahead of full ADR 0052 Stage 06
- Existing `MaintenanceRequest` callers in production blocks (search `git grep "MaintenanceRequestId\|RequestId.*=" packages/`) — there may be code outside `blocks-maintenance` that depends on the dropped FK; write beacon listing affected files
- Phase 5 Flease-vs-in-memory-lock decision needs Phase 2 commitment → write beacon

---

## Open questions

(Already-resolved in ADR 0053 amendments A1–A9; listed for completeness:)

- **OQ-1: UK Cancelled vs US Canceled** → A4 keeps UK
- **OQ-2: 13 vs 17 state union** → A4: 13 (existing 8 + 5 new)
- **OQ-3: TransitionTable composability** → A5: promote to public
- **OQ-4: api-change vs chore** → A6: api-change pipeline variant
- **OQ-5: 11 vs 17 audit event types** → A8: 17 (per state transition + per child write)
- **OQ-6: CP/AP classification** → A9: CP entity, AP thread reference

If sunfish-PM hits a question NOT covered above: write `cob-question-*` beacon. XO will respond with addendum (mirror W#31 pattern).

---

## Acceptance criteria (cumulative across all 8 phases)

- [ ] `WorkOrderStatus` has 13 values; existing 8 unchanged including UK `Cancelled`
- [ ] `TransitionTable<TState>` is `public sealed`; no internal-access regressions
- [ ] `WorkOrder` is init-only record with all required fields per A6
- [ ] `MaintenanceRequestId RequestId` removed from `WorkOrder` proper
- [ ] 3 child entity types ship with full XML doc + nullability + `required`
- [ ] 17 new `AuditEventType` constants in kernel-audit
- [ ] `WorkOrderAuditPayloadFactory` ships with one factory method per event type
- [ ] Service methods emit audit records per the cardinality rule (1 per state transition + 1 per child write)
- [ ] `IPaymentGateway` + `IThreadStore` + `kernel-signatures` wiring works end-to-end in InMemory mode
- [ ] `apps/docs/blocks/maintenance/work-orders.md` page exists; covers state-set + transitions + audit + migration
- [ ] All tests pass; build is clean; `dotnet build` ≥ existing pass rate
- [ ] MAJOR version bump on `Sunfish.Blocks.Maintenance` package
- [ ] Ledger row #19 → `built`

---

## References

- [ADR 0053](../../docs/adrs/0053-work-order-domain-model.md) — Work Order Domain Model + amendments A1–A9
- [Council review](../07_review/output/adr-audits/0053-council-review-2026-04-29.md) — surfaced A4–A9
- [Cluster intake](../00_intake/output/property-work-orders-intake-2026-04-28.md) — original scope
- [W#31 Foundation.Taxonomy substrate](https://github.com/ctwoodwa/Sunfish/pull/263) — taxonomy primitives now real
- [W#31 addendum pattern](./foundation-taxonomy-phase1-stage06-addendum.md) — established the AuditEventType + payload-factory cardinality pattern this hand-off mirrors
- ADR 0008 (multi-tenancy), 0013 (provider-neutrality), 0015 (entity-module registration), 0028 (per-record-class consistency + Flease), 0049 (audit substrate), 0051 (payments), 0052 (messaging), 0054 (signatures) — referenced throughout the schema + wiring
