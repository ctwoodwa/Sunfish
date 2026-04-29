# W#19 Work Orders — Stage 06 hand-off addendum (Phase 3 prerequisite resolution)

**Addendum date:** 2026-04-29
**Resolves:** COB beacon `cob-question-2026-04-29T(approx)-19-phase3-prereqs-halt.md` (PR #271)
**Original hand-off:** [`property-work-orders-stage06-handoff.md`](./property-work-orders-stage06-handoff.md)
**Workstream:** #19

---

## What this addendum does

COB shipped Phases 1+2 (TransitionTable visibility flip + WorkOrderStatus enum extension — PRs #267 + #269 in flight). Phase 3 (api-change-shape WorkOrder schema migration) hit two halts:

1. **Missing types** — `Money` + `ThreadId` + `IPaymentGateway` + `IThreadStore` referenced in the new `WorkOrder` shape don't exist on origin/main yet (ADR 0051/0052 Stage 06 not shipped)
2. **Phase ordering** — Phase 3 references the 3 child entity types (`WorkOrderEntryNotice`, `WorkOrderCompletionAttestation`, `WorkOrderAppointment`) that Phase 5 creates

Both fixable cleanly. Resolution: introduce minimal stubs for the cross-substrate types in their target foundation packages (Option A from COB's question), AND reorder phases so child entities ship before schema migration.

---

## Resolution 1 — Phase reordering (resolves Halt 2)

**Current order in original hand-off:** 1 → 2 → 3 (schema migration) → 4 (audit) → 5 (child entities) → 6 (wiring) → 7 (tests/docs) → 8 (ledger).

**New order:**

| New Phase | Subject | Was Phase |
|---|---|---|
| 1 | TransitionTable visibility flip | 1 (shipped via PR #267) |
| 2 | WorkOrderStatus enum extension | 2 (in flight via PR #269) |
| **3 (new)** | **Child entities** (EntryNotice + CompletionAttestation + Appointment) | was 5 |
| **4 (new)** | **Audit emission** (17 AuditEventType + factory) | was 4 (unchanged) |
| **5 (new)** | **WorkOrder schema migration** (api-change-shape; references types from new Phase 3) | was 3 |
| 6 | Cross-package wiring | 6 (unchanged) |
| 7 | Tests + apps/docs | 7 (unchanged) |
| 8 | Ledger flip | 8 (unchanged) |

Rationale: schema migration (new Phase 5) references `WorkOrderEntryNotice`/`WorkOrderCompletionAttestation`/`WorkOrderAppointment` as init-only fields on `WorkOrder`. Those types must exist before the migration can compile. Move child-entity creation (was Phase 5) to new Phase 3.

Audit emission (Phase 4) keeps its position — it can ship after child entities (which need their own audit event types) but before schema migration (which only needs the shape).

The 17 `AuditEventType` constants are unchanged from the original hand-off's A8 specification.

## Resolution 2 — Minimal stubs for cross-substrate types (resolves Halt 1)

Pick Option (a) from COB's question: **inline-introduce minimal stubs** in the target foundation packages, scoped so ADR 0051/0052 Stage 06 hand-offs can extend without refactor. Stubs match the ADR specs exactly, just with reduced surface for Phase 3 needs only.

### Stub 1 — `Money` + `CurrencyCode` (per ADR 0051)

Create `packages/foundation-integrations/Payments/Money.cs` and `CurrencyCode.cs`:

```csharp
namespace Sunfish.Foundation.Integrations.Payments;

/// <summary>
/// Currency-bound decimal amount. Phase 3 (W#19) introduces this stub for the
/// WorkOrder schema migration; ADR 0051 Stage 06 (W#5 substrate) will extend
/// with operators (+, -, ==), banker's-rounding helpers, and validation.
/// Phase 3 only requires the type to be constructible + comparable.
/// </summary>
public readonly record struct Money(decimal Amount, CurrencyCode Currency)
{
    public static Money Usd(decimal amount) => new(amount, CurrencyCode.USD);
}

/// <summary>
/// ISO 4217 currency code. Phase 3 stub; ADR 0051 Stage 06 adds the full
/// allow-list validation.
/// </summary>
public readonly record struct CurrencyCode(string Iso4217)
{
    public static CurrencyCode USD => new("USD");
}
```

Tests (3): construction; USD shorthand; equality.

**Cross-package compatibility:** `Money` shape matches ADR 0051's contract surface verbatim. ADR 0051 Stage 06 extends this same struct (operators, validation, NaN/Infinity rejection) without redefining it. No refactor needed.

### Stub 2 — `ThreadId` (per ADR 0052)

Create `packages/foundation-integrations/Messaging/ThreadId.cs`:

```csharp
namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// Identifier for a multi-party messaging thread. Phase 3 (W#19) introduces
/// this stub; ADR 0052 Stage 06 (W#20) ships the full Thread substrate
/// referenced by this ID.
/// </summary>
public readonly record struct ThreadId(Guid Value);
```

That's it. One file, one struct, no behavior. ADR 0052 Stage 06 will ship `IThreadStore` + `Thread` + `Message` etc.; this stub is just the FK type for `WorkOrder.PrimaryThread`.

**Don't stub `IThreadStore` or `IThreadStore.SplitAsync` in this addendum.** Phase 6 (cross-package wiring) is where those references appear; W#20 Phase 2 will have shipped `IThreadStore` by then per the W#20 hand-off's sequence note. If W#20 Phase 2 hasn't shipped when W#19 Phase 6 runs, halt-condition #3 from the original hand-off fires.

### Stub 3 — `IPaymentGateway` (per ADR 0051)

**Don't stub.** Phase 5 (schema migration) doesn't reference `IPaymentGateway` directly — it just stores `Money? TotalCost` and `Money? EstimatedCost` on the `WorkOrder` record. The gateway is consumed in Phase 6 (cross-package wiring), at which point W#5/W#20 Stage 06 work should have shipped it. If not, halt-condition fires (already named in original hand-off).

Schema migration in Phase 5 thus has only TWO new dependencies that need stubbing: `Money` + `ThreadId`. Both ship in this addendum's prerequisite Phase 0 (below).

### Phase 0 (NEW; runs before Phase 3 of new ordering) — Stub creation

Insert before child-entities phase:

**Phase 0 — Foundation stubs** (~0.5h)

- Create `packages/foundation-integrations/Payments/Money.cs` + `CurrencyCode.cs` per Stub 1
- Create `packages/foundation-integrations/Messaging/ThreadId.cs` per Stub 2
- 3 unit tests (construction + USD shorthand + equality for Money; construction for ThreadId)
- Update `packages/foundation-integrations/foundation-integrations.csproj` if new namespaces require it

**Gate:** stubs compile; `dotnet build` clean; tests pass.

**PR title:** `feat(foundation-integrations): minimal Money + ThreadId stubs for W#19 Phase 5 (ADR 0051/0052 Stage 06 will extend)`

---

## Updated Phase 5 (was Phase 3) — Schema migration

The `WorkOrder` record's new shape references the now-stubbed types + the now-existing child entities. The shape from the original hand-off is unchanged; only the dependency story is now resolved.

```csharp
public sealed record WorkOrder
{
    public required WorkOrderId Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required WorkOrderStatus Status { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public Money? EstimatedCost { get; init; }                                  // stub from Phase 0
    public Money? TotalCost { get; init; }                                      // stub from Phase 0
    public EquipmentId? Equipment { get; init; }                                // existing per A1
    public ThreadId? PrimaryThread { get; init; }                               // stub from Phase 0
    public WorkOrderAppointment? Appointment { get; init; }                     // from new Phase 3
    public WorkOrderCompletionAttestation? CompletionAttestation { get; init; } // from new Phase 3
    public IReadOnlyList<WorkOrderEntryNotice> EntryNotices { get; init; } = []; // from new Phase 3
    public IReadOnlyList<AuditId> AuditTrail { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; init; }
}
```

Everything else in the schema-migration phase per the original hand-off (positional → init-only; drop `MaintenanceRequestId RequestId`; MAJOR version bump on `Sunfish.Blocks.Maintenance`; tests + JSON serializer + apps/docs migration note) is unchanged.

---

## Updated Phase summary

Inserted Phase 0; phases 3 and 5 swapped. Total work unchanged in scope; ordering fixed.

| Phase | Subject | Hours | Status |
|---|---|---|---|
| 1 | TransitionTable visibility | 0.5 | ✅ shipped (PR #267) |
| 2 | WorkOrderStatus enum extension | 2–3 | 🟡 in flight (PR #269) |
| **0 (new)** | **Foundation stubs (Money + ThreadId)** | **0.5** | **NEW per this addendum** |
| 3 (was 5) | Child entities | 3–5 | pending |
| 4 | Audit emission | 2–3 | pending |
| 5 (was 3) | WorkOrder schema migration | 4–6 | pending |
| 6 | Cross-package wiring | 1–2 | pending |
| 7 | Tests + apps/docs | 1 | pending |
| 8 | Ledger flip | 0.5 | pending |
| **Total** | | **14.5–21h** | (was 14.5–20.5h; +0.5h for Phase 0) |

Phase 0 is short enough to bundle with the next pending PR (originally Phase 3 = child entities) if COB prefers — both can ship in one PR titled `feat(foundation-integrations,blocks-maintenance): Money/ThreadId stubs + child entities`.

---

## What this addendum does NOT change

- All other §"Acceptance criteria" in the original hand-off
- The 17 `AuditEventType` constants (Phase 4)
- The cross-package wiring scope (Phase 6) — `IPaymentGateway` + `IThreadStore.SplitAsync` + `kernel-signatures` references stay where they are; halt-conditions #2 and #3 from the original hand-off still apply if the consumed substrates haven't shipped by Phase 6 time
- The api-change pipeline classification (Phase 5 schema migration is still api-change-shape; MAJOR version bump on `Sunfish.Blocks.Maintenance` still applies)
- The estimated total effort (14.5–21h vs original 14.5–20.5h; +0.5h for Phase 0)

---

## How sunfish-PM should pick this up

1. **Resume after PR #269 (Phase 2) merges.**
2. **Next PR: Phase 0 (foundation stubs).** Bundle with new Phase 3 (child entities) if convenient.
3. **Then new Phase 3 (child entities) → Phase 4 (audit) → Phase 5 (schema migration; api-change-shape; MAJOR bump) → Phase 6 (wiring) → Phase 7 (tests/docs) → Phase 8 (ledger flip).**
4. **`git mv` the COB question beacon** `icm/_state/research-inbox/cob-question-2026-04-29-19-phase3-prereqs-halt.md` to `_archive/` in this addendum's PR (XO doing this on COB's behalf since the addendum resolves the question).

If any phase surfaces a fresh halt: write `cob-question-*` beacon — same protocol.

---

## References

- Original hand-off: [`property-work-orders-stage06-handoff.md`](./property-work-orders-stage06-handoff.md)
- COB beacon (this addendum resolves): [`../research-inbox/_archive/cob-question-2026-04-29-19-phase3-prereqs-halt.md`](../research-inbox/_archive/) (post-archive path)
- ADR 0051 (Money + IPaymentGateway spec): [`docs/adrs/0051-foundation-integrations-payments.md`](../../docs/adrs/0051-foundation-integrations-payments.md) — Stub 1 + 3 match this spec
- ADR 0052 (ThreadId + IThreadStore spec): [`docs/adrs/0052-bidirectional-messaging-substrate.md`](../../docs/adrs/0052-bidirectional-messaging-substrate.md) — Stub 2 matches this spec
- ADR 0053 amendments A4–A9: [`docs/adrs/0053-work-order-domain-model.md`](../../docs/adrs/0053-work-order-domain-model.md) §"Amendment 2026-04-29 — Council-review remediation"
- W#31 addendum pattern (precedent for this resolution shape): [`foundation-taxonomy-phase1-stage06-addendum.md`](./foundation-taxonomy-phase1-stage06-addendum.md)
