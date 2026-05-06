# W#57 Stage 06 Hand-Off — ADR 0065-A1 Standing Order Event-Stream Contract

**Workstream:** W#57  
**ADR:** [0065-A1 — Wayfinder System + Standing Order Contract, Amendment A1](../../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md#amendment-a1--standing-order-event-stream-contract)  
**Status at hand-off:** `design-in-flight` → flip to `ready-to-build` when this file merges  
**Package:** additive to `packages/foundation-wayfinder/` + one constant in `packages/kernel-audit/`  
**Authored:** 2026-05-06 (XO research session)  
**Build estimate:** ~2–3h / 1 PR + 1 ledger-flip PR  
**Build phases:** Phase 1 (event stream contract + DI wiring + tests) → Phase 2 (ledger flip)

---

## Critical context

ADR 0065-A1 (`StandingOrderAppliedEvent` + `IStandingOrderEventStream`) is fully authored and
council-reviewed in the ADR file. This hand-off captures the build spec so COB can execute
without re-reading the full amendment.

**Why this unblocks the cohort:**

- **W#46 Phase 3 halt-condition C** — `DefaultPermissionResolver` ships Phase 1 with a TTL
  cache fallback; subscribe-before-load cache invalidation requires `IStandingOrderEventStream`.
- **W#53 Phase 2 H8** — Helm widgets (`recent-standing-orders`, `quick-toggles`) fall back to
  periodic-refresh until `StandingOrderAppliedEvent` is on origin/main. Resume signal:
  `grep -rn "StandingOrderAppliedEvent" packages/foundation-wayfinder/` ≥1 match.
- **W#43 `WayfinderFeatureProvider`** — will subscribe to invalidate its `IFeatureManager`
  cache when Standing Orders touching `feature-management.*` are applied (Phase 2 follow-on).

---

## Prerequisites verification checklist

```bash
# H1: IStandingOrderAppliedEvent / IStandingOrderEventStream NOT yet on origin/main
grep -rn "StandingOrderAppliedEvent\|IStandingOrderEventStream\|InMemoryStandingOrderEventStream" \
  packages/foundation-wayfinder/
# EXPECT: zero hits

# H2: AuditEventType.StandingOrderApplied NOT yet on origin/main
grep -rn "StandingOrderApplied" packages/kernel-audit/AuditEventType.cs
# EXPECT: zero hits

# H3: IAuditEventStream structural template exists
grep -rn "IAuditEventStream" packages/kernel-audit/ | head -3
# EXPECT: ≥1 hit (packages/kernel-audit/IAuditEventStream.cs)

# H4: IStandingOrderIssuer exists (phase ships to same package)
grep -rn "IStandingOrderIssuer" packages/foundation-wayfinder/ | head -3
# EXPECT: ≥1 hit

# H5: DefaultStandingOrderIssuer constructor (verify current signature before editing)
grep -n "public DefaultStandingOrderIssuer" packages/foundation-wayfinder/DefaultStandingOrderIssuer.cs
# EXPECT: single public constructor with (IStandingOrderRepository, IEnumerable<...>, IOperationSigner, TimeProvider)
```

If H1 or H2 return hits a parallel session may have landed this already. STOP, write
`cob-question-[timestamp]-w57-symbols-already-present.md` to research-inbox, and halt.

---

## Phase 1 — Event-stream contract + DI wiring + tests (~2–3h, 1 PR)

**Gate:** All H1–H5 prerequisites pass.

### Files to create

#### `packages/foundation-wayfinder/StandingOrderAppliedEvent.cs`

Per ADR 0065-A1 §A1.1. Field rationale in the ADR.

```csharp
using System;
using System.Collections.Generic;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Published via <see cref="IStandingOrderEventStream"/> when a
/// <see cref="StandingOrder"/> reaches <see cref="StandingOrderState.Applied"/>
/// (post-issuance, post-CRDT-merge, post-Atlas-projection). Per ADR 0065-A1 §A1.1.
/// </summary>
/// <remarks>
/// NOT emitted for <see cref="StandingOrderState.Rejected"/>,
/// <see cref="StandingOrderState.Conflicted"/> (loser-side), or
/// <see cref="StandingOrderState.Rescinded"/> — those states fire the corresponding
/// <see cref="Sunfish.Kernel.Audit.AuditEventType"/> constants and are observed via
/// <see cref="Sunfish.Kernel.Audit.IAuditEventStream"/>.
/// </remarks>
public sealed record StandingOrderAppliedEvent(
    StandingOrderId StandingOrderId,
    TenantId TenantId,
    ActorId IssuedBy,
    DateTimeOffset AppliedAt,
    StandingOrderScope Scope,
    IReadOnlyList<StandingOrderTriple> Triples,
    AuditRecordId AuditRecordId,
    string? Rationale);
```

#### `packages/foundation-wayfinder/IStandingOrderEventStream.cs`

Per ADR 0065-A1 §A1.2. Structural parallel to `Sunfish.Kernel.Audit.IAuditEventStream`.

```csharp
using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// In-process event stream for consumers that need to react to applied
/// <see cref="StandingOrder"/>s (Helm widgets, permission resolvers,
/// feature-management cache invalidation). Per ADR 0065-A1 §A1.2.
/// </summary>
/// <remarks>
/// <para>
/// Stream is <b>in-process only</b>. Cross-process fanout (Bridge → remote-Anchor)
/// is ADR 0031 §A1 subscription-event-emitter's responsibility.
/// </para>
/// <para>
/// <b>Tenant-scope filter is mandatory</b> for tenant-scoped consumers.
/// <see cref="Subscribe"/> is all-tenant by design — callers MUST filter on
/// <see cref="StandingOrderAppliedEvent.TenantId"/>. See ADR 0065-A1 §A1.6 for
/// the recommended subscribe-then-replay idiom with <c>HashSet</c> dedup.
/// </para>
/// <para>
/// <b><see cref="ReplayAll"/> is restart-volatile.</b> For durable replay rebuild
/// from <see cref="IStandingOrderRepository.EnumerateAsync"/> filtered to
/// <see cref="StandingOrderState.Applied"/>; the event stream is in-process fanout only.
/// </para>
/// </remarks>
public interface IStandingOrderEventStream
{
    /// <summary>Replay every applied Standing Order event in append order.</summary>
    IReadOnlyList<StandingOrderAppliedEvent> ReplayAll();

    /// <summary>
    /// Subscribe a callback invoked for each newly-applied event.
    /// Returns an <see cref="IDisposable"/> that unsubscribes on dispose.
    /// </summary>
    IDisposable Subscribe(Action<StandingOrderAppliedEvent> handler);
}
```

#### `packages/foundation-wayfinder/InMemoryStandingOrderEventStream.cs`

Per ADR 0065-A1 §A1.4. Structurally mirrors `packages/kernel-audit/InMemoryAuditEventStream.cs`
(lock + list + subscriber-snapshot pattern). **Must be `internal sealed`** — not public API.

```csharp
using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Default in-process <see cref="IStandingOrderEventStream"/> backed by a list
/// and a list of subscribers. Structurally mirrors
/// <c>Sunfish.Kernel.Audit.InMemoryAuditEventStream</c>. Per ADR 0065-A1 §A1.4.
/// </summary>
internal sealed class InMemoryStandingOrderEventStream : IStandingOrderEventStream
{
    private readonly object _gate = new();
    private readonly List<StandingOrderAppliedEvent> _events = new();
    private readonly List<Action<StandingOrderAppliedEvent>> _subscribers = new();

    public IReadOnlyList<StandingOrderAppliedEvent> ReplayAll()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }

    public IDisposable Subscribe(Action<StandingOrderAppliedEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            _subscribers.Add(handler);
        }
        return new Subscription(this, handler);
    }

    internal void Publish(StandingOrderAppliedEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        Action<StandingOrderAppliedEvent>[] snapshot;
        lock (_gate)
        {
            _events.Add(evt);
            snapshot = _subscribers.ToArray();
        }
        foreach (var handler in snapshot)
        {
            handler(evt);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly InMemoryStandingOrderEventStream _owner;
        private readonly Action<StandingOrderAppliedEvent> _handler;
        private bool _disposed;

        internal Subscription(
            InMemoryStandingOrderEventStream owner,
            Action<StandingOrderAppliedEvent> handler)
        {
            _owner = owner;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_owner._gate)
            {
                _owner._subscribers.Remove(_handler);
            }
        }
    }
}
```

---

### Files to modify

#### `packages/kernel-audit/AuditEventType.cs`

Add one constant at the end of the `===== ADR 0065 =====` block (after
`StandingOrderConflictResolved` at line ~481), per ADR 0065-A1 §A1.3:

```csharp
/// <summary>
/// A <c>StandingOrder</c> reached <c>StandingOrderState.Applied</c> — the
/// projected configuration is now live. Distinct from
/// <see cref="StandingOrderIssued"/> (which fires at the validation-passed
/// grain). Per ADR 0065-A1 §A1.3.
/// </summary>
public static readonly AuditEventType StandingOrderApplied = new("StandingOrderApplied");
```

**Placement:** immediately after `StandingOrderConflictResolved`; no line changes above.

#### `packages/foundation-wayfinder/DefaultStandingOrderIssuer.cs`

Per ADR 0065-A1 §A1.5. Two changes:

1. **Constructor gains a new final parameter** `InMemoryStandingOrderEventStream eventStream`:

```csharp
public DefaultStandingOrderIssuer(
    IStandingOrderRepository repository,
    IEnumerable<IStandingOrderValidator> validators,
    IOperationSigner signer,
    TimeProvider time,
    InMemoryStandingOrderEventStream eventStream)  // ← new
```

Store as `private readonly InMemoryStandingOrderEventStream _eventStream;`.

2. **After `AppendAsync` + audit-emit** in the `Applied` path, add the publish as the **last
   step** (after persisting + auditing, never before):

```csharp
_eventStream.Publish(new StandingOrderAppliedEvent(
    order.Id,
    order.TenantId,
    order.IssuedBy,
    DateTimeOffset.UtcNow,   // use TimeProvider.GetUtcNow() if available, else DateTimeOffset.UtcNow
    order.Scope,
    order.Triples,
    auditRecordId,           // the AuditRecordId returned by IAuditTrail.AppendAsync
    order.Rationale));
```

Publish fires **only** for the `Applied` state. `Rejected` / `Conflicted` / `Rescinded`
states do NOT call `_eventStream.Publish`.

**Test fixture impact:** `packages/foundation-wayfinder/tests/DefaultStandingOrderIssuerTests.cs`
constructs `DefaultStandingOrderIssuer` directly — add `new InMemoryStandingOrderEventStream()`
as the new final argument. Estimated 1–2 lines.

#### `packages/foundation-wayfinder/WayfinderServiceExtensions.cs`

Per ADR 0065-A1 §A1.5. Add two lines to `AddSunfishWayfinder()` after the existing
`TryAddSingleton<IStandingOrderIssuer, DefaultStandingOrderIssuer>()` call:

```csharp
services.TryAddSingleton<InMemoryStandingOrderEventStream>();
services.TryAddSingleton<IStandingOrderEventStream>(
    sp => sp.GetRequiredService<InMemoryStandingOrderEventStream>());
```

---

### Tests to write

**New file:** `packages/foundation-wayfinder/tests/StandingOrderEventStreamTests.cs`

Target: 8–10 tests covering:

1. `ReplayAll()` returns empty on fresh stream.
2. `ReplayAll()` returns events in append order after publish.
3. `Subscribe` callback fires on `Publish`.
4. `Subscribe` callback NOT fired after `Dispose()` on the returned subscription.
5. Multiple subscribers all receive the same event.
6. Subscriber disposed mid-flight does not prevent other subscribers from receiving.
7. Subscribe-then-replay dedup pattern (§A1.6): subscriber sees events from `ReplayAll()` and
   new publishes without duplicates using `HashSet<StandingOrderId>`.
8. Tenant-scope filter pattern (§A1.6): event with different `TenantId` is ignored by filter.
9. (Optional) Concurrent publish + subscribe under lock does not deadlock (smoke test).

**Updated file:** `packages/foundation-wayfinder/tests/DefaultStandingOrderIssuerTests.cs`

- Pass `new InMemoryStandingOrderEventStream()` as the new constructor argument.
- Add 2–3 tests verifying that `IssueAsync` on `Applied` path fires `StandingOrderAppliedEvent`
  with correct fields, and that `Rejected` / `Rescinded` paths do NOT fire the event.

---

## Acceptance criteria

- [ ] `grep -rn "IStandingOrderEventStream" packages/foundation-wayfinder/` returns
  `IStandingOrderEventStream.cs` + `InMemoryStandingOrderEventStream.cs` + `WayfinderServiceExtensions.cs`
- [ ] `grep -rn "StandingOrderAppliedEvent" packages/foundation-wayfinder/` returns
  `StandingOrderAppliedEvent.cs` + `DefaultStandingOrderIssuer.cs` + test file(s)
- [ ] `grep "StandingOrderApplied" packages/kernel-audit/AuditEventType.cs` returns the new constant
- [ ] `InMemoryStandingOrderEventStream` is `internal sealed` — NOT public
- [ ] All existing `packages/foundation-wayfinder/tests/` tests still pass (no regressions in
  `DefaultStandingOrderIssuerTests.cs` or `StandingOrderShapeTests.cs`)
- [ ] New `StandingOrderEventStreamTests.cs` all green
- [ ] `dotnet build packages/foundation-wayfinder/ packages/kernel-audit/` clean (0 errors, 0 warnings)
- [ ] Pre-merge council dispatched (Opus 4.7; 4-perspective adversarial; cohort batting average
  ~95%+ — mandatory per ADR 0069 D1)

---

## Halt conditions

| # | Condition | Action |
|---|---|---|
| H1 | `StandingOrderAppliedEvent` / `IStandingOrderEventStream` already present on origin/main | Write `cob-question` beacon to research-inbox; halt |
| H2 | `DefaultStandingOrderIssuer` constructor signature has changed (new params before or after the expected 4) | Write `cob-question` beacon; halt — XO must update this hand-off |
| H3 | `InMemoryAuditEventStream` structural pattern has diverged from what's cited (e.g., `Publish` is public, not internal) | Align to current pattern; proceed; note divergence in commit message |
| H4 | Council returns Blocking-severity findings | Apply mechanical findings per Decision Discipline Rule 3; non-mechanical → write `cob-question` beacon and halt auto-merge |
| H5 | `DefaultStandingOrderIssuer.IssueAsync` has no `Applied` code path (state logic may have changed) | Write `cob-question` beacon; halt |

---

## §A0 — cited-symbol audit (verified 2026-05-06)

**Negative-existence (symbols introduced by this hand-off):**
- `Sunfish.Foundation.Wayfinder.StandingOrderAppliedEvent` — `grep -rn "StandingOrderAppliedEvent" packages/` = ZERO ✓
- `Sunfish.Foundation.Wayfinder.IStandingOrderEventStream` — `grep -rn "IStandingOrderEventStream" packages/` = ZERO ✓
- `Sunfish.Foundation.Wayfinder.InMemoryStandingOrderEventStream` — ZERO ✓
- `Sunfish.Kernel.Audit.AuditEventType.StandingOrderApplied` — `grep "StandingOrderApplied" packages/kernel-audit/AuditEventType.cs` = ZERO ✓

**Positive-existence (symbols this hand-off edits or depends on):**
- `Sunfish.Kernel.Audit.IAuditEventStream` — `packages/kernel-audit/IAuditEventStream.cs` ✓
- `Sunfish.Kernel.Audit.InMemoryAuditEventStream` — `packages/kernel-audit/InMemoryAuditEventStream.cs` ✓ (`internal sealed`)
- `Sunfish.Foundation.Wayfinder.DefaultStandingOrderIssuer` — `packages/foundation-wayfinder/DefaultStandingOrderIssuer.cs` ✓
- `Sunfish.Foundation.Wayfinder.WayfinderServiceExtensions` — `packages/foundation-wayfinder/WayfinderServiceExtensions.cs` ✓
- `Sunfish.Foundation.Wayfinder.StandingOrderId`, `StandingOrderScope`, `StandingOrderTriple`, `StandingOrderState` — all verified on origin/main ✓
- `Sunfish.Foundation.Assets.Common.TenantId`, `ActorId` — `packages/foundation/Assets/Common/` ✓
- `Sunfish.Foundation.Wayfinder.AuditRecordId` — `packages/foundation-wayfinder/StandingOrderId.cs:16` ✓
- `Sunfish.Kernel.Audit.AuditEventType` block `===== ADR 0065 =====` at line 466; insertion point after `StandingOrderConflictResolved` at line ~481 ✓

---

## Cohort patterns to follow

- `InMemoryStandingOrderEventStream` must be `internal sealed` (mirrors `InMemoryAuditEventStream`).
- Two-overload DI registration pattern: `TryAddSingleton<InMemoryX>()` + `TryAddSingleton<IX>(sp => sp.GetRequired...)`.
- Publish fires **last** (after persist + audit), never first.
- No `[JsonPropertyName]` on `StandingOrderAppliedEvent` — it is an in-process type, not a wire format.
- `Subscription.Dispose()` must guard against double-dispose (`if (_disposed) return`).
- Pre-merge council canonical before creating the PR (not in parallel with auto-merge).

---

## Phase 2 — Ledger flip (~15min, 1 PR)

After Phase 1 merges: flip W#57 row in `active-workstreams.md` from `building` → `built`.
Include ledger note: "`IStandingOrderEventStream` on origin/main; W#46 halt-condition C
cleared; W#53 Phase 2 H8 cleared."
