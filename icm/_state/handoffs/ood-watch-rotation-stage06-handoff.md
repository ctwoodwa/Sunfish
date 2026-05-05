# W#49 Stage 06 Hand-off — OOD Watch Rotation Primitive

**ADR:** [0078 — OOD Watch Rotation Primitive](../../../docs/adrs/0078-ood-watch-rotation.md)
**Status:** ADR 0078 Accepted 2026-05-05 via PR #571
**Workstream row:** W#49 (`design-in-flight` → flip to `ready-to-build` with this hand-off)
**Assigned to:** sunfish-PM (COB)
**Estimated effort:** ~6–9h / 3 PRs
**Pipeline variant:** `sunfish-feature-change`
**Prerequisite state (verify before starting):**
- [ ] `origin/main` has `packages/foundation-wayfinder/StandingOrder.cs` ✓ (verified 2026-05-05)
- [ ] `origin/main` has `packages/kernel-audit/AuditEventType.cs` ✓ (ADR 0049 substrate)
- [ ] `origin/main` has `packages/foundation/Crypto/IOperationSigner.cs` ✓ (ADR 0046)
- [ ] `origin/main` has `packages/foundation/Crypto/IOperationVerifier.cs` ✓ (ADR 0046)
- [ ] No `OodWatch*.cs` or `IOodWatch*.cs` files in `packages/foundation-wayfinder/` (none on
  origin/main 2026-05-05 — symbols are net-new; confirm before Phase 1)
- [ ] `foundation-wayfinder` has NOT shipped a public NuGet binary yet (pre-v1; if this changes,
  the `StandingOrder.IssuedDuringWatchId` addition requires an ADR 0065 amendment first)

**Downstream unblocks:**
- W#50 (Engine Room Observability) Phase 3b: `IOodWatchService.GetActiveWatchAsync(OodRole.EngineeringOfficerOfTheWatch)` wiring for EOOW-check panel
- W#51 (Quarterdeck Entry-Point) Phase 3a: WatchBanner component consumes `IOodWatchService`; Phase 3a is gated on W#49 Phase 1 per ADR 0080 §Prerequisites

---

## Substrate verification (pre-start)

Run before writing a single line of Phase 1 code:

```bash
# 1. StandingOrder constructor shape (10 params; no OodWatchId yet)
grep -n "OodWatchId\|IssuedDuringWatchId" packages/foundation-wayfinder/StandingOrder.cs
# Expected: 0 matches (field does not yet exist)

# 2. Existing StandingOrder construction call-sites (all in foundation-wayfinder)
grep -rn "new StandingOrder(" packages/
# Expected: exactly 3 lines:
#   packages/foundation-wayfinder/DefaultStandingOrderIssuer.cs:90
#   packages/foundation-wayfinder/tests/StandingOrderShapeTests.cs:28
#   packages/foundation-wayfinder/tests/DefaultAtlasProjectorTests.cs:35

# 3. AuditEventType for OOD (none yet)
grep -n "OodWatch" packages/kernel-audit/AuditEventType.cs
# Expected: 0 matches

# 4. foundation-wayfinder builds clean
dotnet build packages/foundation-wayfinder/ -c Release --no-restore
```

If any check fails unexpectedly, stop and write a `cob-question-*.md` to the research-inbox.

---

## Phase 1 — OOD substrate types + audit constants

**Effort:** ~2–3h | **PR:** 1 | **Review:** pre-merge council mandatory (security-engineering
subagent required — OOD authority intersects Standing Order approval paths; signing req in §Trust)

### Deliverables

**New files in `packages/foundation-wayfinder/`:**

**`OodWatchId.cs`**
```csharp
namespace Sunfish.Foundation.Wayfinder;

public readonly record struct OodWatchId(Guid Value)
{
    public static OodWatchId NewId() => new(Guid.NewGuid());
}
```

**`OodRole.cs`**
```csharp
namespace Sunfish.Foundation.Wayfinder;

public enum OodRole
{
    OfficerOfTheDeck,
    EngineeringOfficerOfTheWatch
}
```

**`OodWatchState.cs`**
```csharp
namespace Sunfish.Foundation.Wayfinder;

public enum OodWatchState
{
    Active,
    Relieved,
    Expired
}
```

**`OodWatch.cs`**
```csharp
using System;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

public sealed record OodWatch(
    OodWatchId Id,
    TenantId TenantId,
    ActorId OnWatchActor,
    OodRole Role,
    NodaTime.Instant StartedAt,
    NodaTime.Instant? RelievedAt,
    ActorId StartedBy,
    ActorId? RelievedBy,
    TimeSpan MaxWatchDuration,
    OodWatchState State);
```

**`OodWatchConflictException.cs`**
```csharp
using System;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

public sealed class OodWatchConflictException(
    OodWatchId existingWatchId,
    TenantId tenantId,
    OodRole role)
    : InvalidOperationException(
        $"An active watch already exists for tenant {tenantId.Value} / role {role}: {existingWatchId.Value}")
{
    public OodWatchId ExistingWatchId { get; } = existingWatchId;
    public TenantId TenantId { get; } = tenantId;
    public OodRole Role { get; } = role;
}
```

**`IOodWatchRepository.cs`**
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

public interface IOodWatchRepository
{
    ValueTask<OodWatch?> GetCurrentWatchAsync(
        TenantId tenantId, OodRole role, CancellationToken ct = default);

    ValueTask<OodWatch> StartWatchAsync(
        TenantId tenantId, ActorId onWatchActor, OodRole role,
        TimeSpan? maxDuration, ActorId startedBy, CancellationToken ct = default);

    ValueTask<OodWatch> RelieveWatchAsync(
        OodWatchId watchId, ActorId relievedBy, CancellationToken ct = default);

    ValueTask<OodWatch> ExpireWatchAsync(
        OodWatchId watchId, CancellationToken ct = default);

    IAsyncEnumerable<OodWatch> GetWatchHistoryAsync(
        TenantId tenantId, OodRole role,
        NodaTime.Instant from, NodaTime.Instant to,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all Active watches whose StartedAt + MaxWatchDuration &lt;= cutoff.
    /// Used by the expiry sweep to drive TTL across all tenants.
    /// </summary>
    IAsyncEnumerable<OodWatch> GetExpiredCandidatesAsync(
        NodaTime.Instant cutoff, CancellationToken ct = default);
}
```

**`IOodWatchService.cs`**
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

public interface IOodWatchService
{
    /// <summary>
    /// Start a new watch. OodWatchId is server-generated; callers MUST NOT supply it.
    /// </summary>
    /// <exception cref="OodWatchConflictException">
    /// Thrown when an Active watch already exists for the given (TenantId, OodRole) pair.
    /// </exception>
    ValueTask<OodWatch> StartWatchAsync(
        TenantId tenantId, ActorId onWatchActor, OodRole role,
        TimeSpan? maxDuration, ActorId requestedBy, CancellationToken ct = default);

    /// <summary>
    /// Formal handover: relieves the current watch and starts a new one atomically.
    /// </summary>
    /// <exception cref="OodWatchConflictException">
    /// Thrown if currentWatchId is not in Active state at the time of the call.
    /// </exception>
    ValueTask<(OodWatch Relieved, OodWatch Started)> HandoverWatchAsync(
        OodWatchId currentWatchId, ActorId incomingActor,
        ActorId requestedBy, string? reason, CancellationToken ct = default);

    /// <summary>
    /// Returns the Active watch for (tenant, role), or null if none.
    /// Returns null for Relieved or Expired watches.
    /// </summary>
    ValueTask<OodWatch?> GetActiveWatchAsync(
        TenantId tenantId, OodRole role, CancellationToken ct = default);
}
```

**Extend `StandingOrder.cs`** — add `IssuedDuringWatchId` as an optional positional parameter
(last position; default null). Update the 3 call-sites:

1. `DefaultStandingOrderIssuer.cs` line ~90: add `IssuedDuringWatchId: null` (named arg; pass
   null in Phase 1, OodWatchService wiring in Phase 2)
2. `tests/StandingOrderShapeTests.cs` line ~28: add trailing `, null` or named arg
3. `tests/DefaultAtlasProjectorTests.cs` line ~35: add trailing `, null` or named arg

New `StandingOrder` last two lines of the positional constructor:
```csharp
    [property: JsonPropertyName("state")] StandingOrderState State,
    [property: JsonPropertyName("issuedDuringWatchId")] OodWatchId? IssuedDuringWatchId = null
) : IMustHaveTenant;
```

**New constants in `packages/kernel-audit/AuditEventType.cs`:**
```csharp
public static readonly AuditEventType OodWatchStarted  = new("OodWatchStarted");
public static readonly AuditEventType OodWatchRelieved = new("OodWatchRelieved");
public static readonly AuditEventType OodWatchExpired  = new("OodWatchExpired");
```

**DI registration stub in existing `AddSunfishWayfinder()` extension** — add
`services.AddScoped<IOodWatchService, DefaultOodWatchService>()` (stub impl introduced in
Phase 2; Phase 1 can register a `NotImplementedException`-throwing stub or defer registration
to Phase 2 — prefer deferral to avoid DI resolution errors at startup).

### Phase 1 acceptance gate

```
PASS: dotnet build packages/foundation-wayfinder/ packages/kernel-audit/ -c Release
PASS: dotnet test packages/foundation-wayfinder/ -c Release (existing 0-delta; new types have no tests yet)
PASS: pre-merge council (security-engineering subagent) returns APPROVED or NEEDS-AMENDMENT-MECHANICAL-ONLY
FAIL: any new type not matching §1 spec in ADR 0078
FAIL: IssuedDuringWatchId not null-defaulted (compilation errors at existing call-sites)
```

---

## Phase 2 — `IOodWatchService` implementation + audit emission

**Effort:** ~3–4h | **PR:** 1 | **Review:** pre-merge council mandatory (security-engineering
subagent re-review — signing implementation is the critical path)

### Deliverables

**New file `DefaultOodWatchService.cs`** in `packages/foundation-wayfinder/`:

> **H4 resolved (XO 2026-05-05):** `IOperationSigner.VerifyAsync` does not exist and will not
> be added — signing and verification are separate concerns in Sunfish.Foundation.Crypto.
> `IOperationVerifier` (at `packages/foundation/Crypto/IOperationVerifier.cs`) is the correct
> verification interface, but the domain service does NOT call it directly. The ADR 0078 §Trust
> attesting-signature requirement is enforced at the **API/gateway layer** (capability check +
> principal authentication); `DefaultOodWatchService` trusts the authenticated `requestedBy`
> `ActorId` that arrives through the already-validated call path — consistent with every other
> domain service in the Sunfish.Foundation tier. `IClock` does not exist in this codebase; use
> `System.TimeProvider` throughout (constructor-injected; `TimeProvider.GetUtcNow()` returns
> `DateTimeOffset`). Council-approved precedent: W#49 P1 was already amended from NodaTime to
> DateTimeOffset by council in PR #610.

Key behavior per ADR 0078 §2 and §Trust:

- `StartWatchAsync`:
  1. Check for existing `Active` watch via `IOodWatchRepository.GetCurrentWatchAsync`; throw
     `OodWatchConflictException` if one exists.
  2. Generate `OodWatchId` server-side via `OodWatchId.NewId()` — callers MUST NOT supply it.
     Set `StartedAt = _timeProvider.GetUtcNow()`.
  3. If `maxDuration` is provided, set `ExpiresAt = StartedAt + maxDuration`.
  4. Persist via `IOodWatchRepository.StartWatchAsync`.
  5. Emit `AuditEventType.OodWatchStarted` with payload:
     `{ "watchId": id.Value, "tenantId": tenantId.Value, "role": role.ToString(),
        "actor": onWatchActor.Value, "startedBy": requestedBy.Value, "severity": "High" }`.

- `HandoverWatchAsync`:
  1. Load current watch; throw `OodWatchConflictException` if not `Active`.
  3. Atomically: `IOodWatchRepository.RelieveWatchAsync` + `IOodWatchRepository.StartWatchAsync`
     for the incoming actor.
  4. Emit `AuditEventType.OodWatchRelieved` for the relieved watch (severity `"Normal"`).
  5. Emit `AuditEventType.OodWatchStarted` for the new watch (severity `"High"`).
  6. **Standing Order notification** (issue via `IStandingOrderIssuer` — requires W#42 Phase 2
     to be built before this step can be wired; if `IStandingOrderIssuer` is not yet available,
     leave a `// TODO(W#49-P2): emit watch-transfer notification once IStandingOrderIssuer
     is available` comment and ship Phase 2 without it; no blocking halt).
     Path: `coordination/ood-watch/{role.ToString().ToLowerInvariant()}/transfer`
     Scope: `StandingOrderScope.Tenant`
     Triples: one triple with `OldValue = JsonValue.Create(currentWatchId.Value.ToString())`
     and `NewValue = JsonValue.Create(newWatchId.Value.ToString())`.
  7. Populate `StandingOrder.IssuedDuringWatchId` in the notification Standing Order using the
     new watch's `OodWatchId`.
  8. Return `(relieved, started)`.

- `GetActiveWatchAsync`:
  Delegates to `IOodWatchRepository.GetCurrentWatchAsync`. Returns null for non-Active watches.

**New file `OodWatchExpiryService.cs`** (hosted service):

```csharp
// Polls GetExpiredCandidatesAsync at ConfiguredInterval (default 5 min).
// Calls ExpireWatchAsync for each expired candidate.
// Emits AuditEventType.OodWatchExpired with payload:
//   { "watchId": id.Value, "tenantId": tenantId.Value, "role": role.ToString(),
//     "expiredAt": now.ToString(), "maxWatchDuration": duration.ToString(),
//     "severity": "High" }
// Inject TimeProvider (System.TimeProvider); use _timeProvider.GetUtcNow() for clock reads.
// Unit tests MUST pass a FakeTimeProvider (from Microsoft.Extensions.TimeProvider.Testing)
// or a subclass to override interval; avoids Thread.Sleep in tests.
```

**Update DI extension** to register `DefaultOodWatchService` and `OodWatchExpiryService`.

**Required unit tests (8 minimum) in `packages/foundation-wayfinder/tests/`:**

| Test name | Scenario |
|---|---|
| `StartWatch_Succeeds_EmitsAuditEvent` | Happy path; audit record emitted |
| `StartWatch_ConflictThrows_WhenActiveExists` | Second concurrent start on same (tenant, role) |
| `HandoverWatch_AtomicRelieveAndStart` | Repository calls in order; both watches returned |
| `HandoverWatch_EmitsBothAuditEvents` | OodWatchRelieved + OodWatchStarted both emitted |
| `GetActiveWatch_ReturnsNull_WhenNoneActive` | No watch ever started |
| `GetActiveWatch_ReturnsNull_ForExpiredWatch` | Expired watch state treated as null |
| `ExpiryService_SetsExpiredState_EmitsAuditEvent` | Mocked clock; TTL exceeded |
| `StandingOrder_IssuedDuringWatchId_PopulatedWhenActive` | IssuedDuringWatchId != null when watch active |

All tests use NSubstitute for `IOodWatchRepository`, `IAuditTrail`.
`TimeProvider` is injected via `FakeTimeProvider` (from `Microsoft.Extensions.TimeProvider.Testing`)
— do NOT mock it with NSubstitute; use the real test double from the BCL testing package.
`IOperationSigner` and `IClock` are NOT injected into the service (H4 resolved — see note above).

### Phase 2 acceptance gate

```
PASS: dotnet build packages/foundation-wayfinder/ -c Release
PASS: dotnet test packages/foundation-wayfinder/ -c Release (all 8 new + all existing)
PASS: pre-merge council (security-engineering subagent) returns APPROVED or
      NEEDS-AMENDMENT-MECHANICAL-ONLY
FAIL: any signing requirement from §Trust not implemented (clock-skew check, server-side OodWatchId)
FAIL: OodWatchConflictException not thrown on concurrent-active scenario
FAIL: audit emission missing for any of the 3 event types
```

---

## Phase 3 — Wiring, docs, ledger flip

**Effort:** ~1–2h | **PR:** 1 | **Review:** standard (no council required for docs-only)

### Deliverables

- **`apps/docs/foundation/wayfinder/ood-watch.md`** — usage guide for Quarterdeck/Engine Room
  implementors. Must cover:
  - Starting a watch, handling `OodWatchConflictException`
  - Formal handover pattern
  - Consuming `GetActiveWatchAsync` (null means "no active watch")
  - WCAG live-region contract (§7 of ADR 0078): watch-banner as `aria-live="polite"`;
    expiry announcement as `aria-live="assertive"`; handover confirmation dialog §7.1
  - TTL failsafe behavior
- **XML docs** on all public types and interface members in `foundation-wayfinder/` introduced
  by W#49 (all the new files from Phase 1 + Phase 2)
- **`CHANGELOG.md` entry** (user-facing: "Added OOD Watch Rotation substrate for multi-actor
  tenant watch management; `IOodWatchService` + `IOodWatchRepository` in
  `Sunfish.Foundation.Wayfinder`")
- **Ledger flip** — update `icm/_state/active-workstreams.md` W#49 row from `design-in-flight`
  (Accepted) to `built`

### Phase 3 acceptance gate

```
PASS: docs/foundation/wayfinder/ood-watch.md exists and covers WCAG live-region contract
PASS: XML docs present on all new public API members
PASS: CHANGELOG.md updated
PASS: W#49 ledger row says `built`
```

---

## Cross-phase halt-conditions

| # | Condition | Action |
|---|---|---|
| H1 | `foundation-wayfinder` has already shipped a public NuGet binary (v1+) | STOP — file ADR 0065 amendment for `IssuedDuringWatchId` before any W#49 work |
| H2 | Unexpected `OodWatch*` symbols already exist on origin/main | STOP — write `cob-question-*.md` to research-inbox |
| H3 | Pre-merge council returns NEEDS-AMENDMENT with non-mechanical findings | Apply; re-run council; do NOT enable auto-merge until APPROVED |
| H4 | ~~`IOperationSigner.VerifyAsync` does not exist (only `SignAsync`)~~ | **RESOLVED 2026-05-05** — signing validation is API-layer responsibility; service trusts authenticated `requestedBy`. Use `TimeProvider.GetUtcNow()` for clock reads. See Phase 2 note above. |
| H5 | `StandingOrder` positional constructor has changed shape since this hand-off was authored | Verify call-site count; adapt `IssuedDuringWatchId` addition accordingly |

---

## Cohort precedents

- **Signing pattern**: `IOperationSigner.SignAsync` at `packages/foundation/Crypto/IOperationSigner.cs`. Verification is `IOperationVerifier.Verify<T>(SignedOperation<T>)` at `packages/foundation/Crypto/IOperationVerifier.cs`. Neither is called in the domain service — both live at API/gateway layer.
- **AuditEventType addition pattern**: W#45 P1 (`packages/kernel-audit/AuditEventType.cs`) — add static-readonly constant; zero-impact on existing callers.
- **Hosted service timer pattern**: W#45 P3 `SessionExpiryService` — inject `TimeProvider`; use `FakeTimeProvider` (from `Microsoft.Extensions.TimeProvider.Testing`) in tests to avoid `Thread.Sleep`. Note: W#45 used `IClock` (NodaTime) but that type does not exist in this codebase; `TimeProvider` is the canonical BCL equivalent used across `foundation-transport`, `kernel-security`, `foundation-mission-space`.
- **NSubstitute test style**: established in W#42, W#43, W#45 across `packages/foundation-wayfinder/tests/`.
- **Pre-merge council before auto-merge**: mandatory per `feedback_council_before_automerge.md`; cohort batting average 29-of-30 substrate amendments needing council fixes.

---

## Downstream consumers (do not implement in W#49)

The following workstreams consume W#49 types after this hand-off ships:

| Workstream | Consumer | Dependency |
|---|---|---|
| W#50 Engine Room | `OodRole.EngineeringOfficerOfTheWatch` via `IOodWatchService.GetActiveWatchAsync` | W#49 Phase 1 must land |
| W#51 Quarterdeck | `IOodWatchService` for WatchBanner; `IOodWatchRepository` for watch history | W#49 Phase 1 must land (Phase 3a of W#51 gated) |
| ~ADR 0068 (W#37) | `IOodWatchService.GetActiveWatchAsync` in Standing Order approval routing | W#49 Phase 1 must land |
