# OOD Watch Rotation

`Sunfish.Foundation.Wayfinder` ships an authority-rotation primitive
modeled on naval Officer-of-the-Deck (OOD) watch turnover. One Active
watch per `(TenantId, OodRole)` pair; explicit handover is the only way
to transfer authority; a TTL failsafe expires watches whose holder fails
to relieve. Per [ADR 0078 — OOD Watch Rotation Primitive](../../../../docs/adrs/0078-ood-watch-rotation.md).

## Surface

| Type | Role |
|---|---|
| `OodWatch` | Authority assignment record (`: IMustHaveTenant`) |
| `OodWatchId` | Server-generated stable identifier |
| `OodRole` | `OfficerOfTheDeck`, `EngineeringOfficerOfTheWatch` |
| `OodWatchState` | `Active`, `Relieved`, `Expired` |
| `OodWatchConflictException` | Single-Active-per-(tenant, role) invariant violation |
| `IOodWatchRepository` | Persistence boundary; owns transactional atomicity |
| `IOodWatchService` | Public service surface (`StartWatchAsync`, `HandoverWatchAsync`, `GetActiveWatchAsync`) |
| `DefaultOodWatchService` | Reference implementation |
| `OodWatchExpiryService` | Background sweep (default 5-min cadence) |
| `StandingOrder.IssuedDuringWatchId` | Optional `OodWatchId?` field correlating Standing Order issuances with the OOD watch active at issuance time |

## DI registration

```csharp
services.AddSunfishWayfinder();
```

`AddSunfishWayfinder` registers `IOodWatchService → DefaultOodWatchService`
and the `OodWatchExpiryService` hosted service. Hosts MUST separately
register an `IOodWatchRepository` (no in-memory default ships in W#49 Phase 2;
Phase 3 will add one).

`IAuditTrail` and `IOperationSigner` are **mandatory** for OOD authority
operations per ADR 0078 §Trust. The service throws
`InvalidOperationException` at first emit attempt when either is missing —
fail loudly rather than run authority operations with zero audit trail.

## Starting a watch

```csharp
try
{
    var watch = await oodWatchService.StartWatchAsync(
        tenantId: TenantId.Default,
        onWatchActor: alice,
        role: OodRole.OfficerOfTheDeck,
        maxDuration: TimeSpan.FromHours(8),
        requestedBy: bob,
        ct);
}
catch (OodWatchConflictException ex)
{
    // Another Active watch already exists for (tenantId, role).
    // ex.ExistingWatchId, ex.TenantId, ex.Role.
}
```

The repository owns server-side `OodWatchId` minting; callers MUST NOT
supply one. `requestedBy` is trusted as authenticated (signature
enforcement is API/gateway-layer responsibility per the ADR 0078
§Trust resolution).

## Formal handover

```csharp
var (relieved, started) = await oodWatchService.HandoverWatchAsync(
    currentWatchId: prior.Id,
    incomingActor: carol,
    requestedBy: alice,
    reason: "shift change at 16:00",
    ct);
```

Handover is **atomic**: the repository's `HandoverWatchAsync`
implementation enforces transactional rollback if the start-leg fails,
so the `(TenantId, OodRole)` pair never enters an authority-vacuum
state. The new watch inherits the relieved watch's `MaxWatchDuration`;
callers needing a fresh TTL must `RelieveWatchAsync` (via direct
repository call) and then `StartWatchAsync` separately.

## Consuming `GetActiveWatchAsync`

```csharp
var active = await oodWatchService.GetActiveWatchAsync(
    tenantId, OodRole.OfficerOfTheDeck, ct);

if (active is null)
{
    // No Active watch — the (tenant, role) pair is in authority vacuum.
    // Quarterdeck banner SHOULD show "no watch in effect"; downstream
    // operations gated on OOD authority should refuse.
}
else
{
    // active.OnWatchActor, active.StartedAt, active.MaxWatchDuration
}
```

`null` means **no Active watch** — Relieved and Expired watches are not
returned. Quarterdeck and Engine Room consumers MUST treat null as
authority-absent.

## TTL failsafe behavior

`OodWatchExpiryService` runs as an `IHostedService` (registered
automatically by `AddSunfishWayfinder`). Default sweep interval is 5
minutes. Each tick:

1. Calls `IOodWatchRepository.GetExpiredCandidatesAsync(now)` to
   enumerate Active watches across all tenants whose
   `StartedAt + MaxWatchDuration ≤ now`.
2. For each candidate: calls `ExpireWatchAsync(watch.Id)` and emits
   `AuditEventType.OodWatchExpired` (severity `"High"`).

Tests inject a `FakeTimeProvider` subclass via the constructor's
`timeProvider` parameter to exercise the sweep deterministically.

> **Cross-tenant warning.** `GetExpiredCandidatesAsync` is the **only**
> non-tenant-scoped method on `IOodWatchRepository`. The single
> legitimate caller is `OodWatchExpiryService`. Application code MUST
> NOT call this method directly. A future phase will move the sweep
> contract to a separate internal interface to enforce this at the
> type system.

## WCAG live-region contract (per ADR 0078 §7)

UI consumers (Quarterdeck WatchBanner, Engine Room dashboards, etc.)
MUST implement the following live-region behavior:

| UI surface | ARIA contract |
|---|---|
| **Watch banner** (top-of-Quarterdeck banner showing the current OOD) | `aria-live="polite"` — non-disruptive when the on-watch actor changes via handover |
| **Expiry announcement** (when `OodWatchState` flips to `Expired`) | `aria-live="assertive"` — interrupts the screen reader because authority has lapsed and operator MUST act |
| **Handover confirmation dialog** (per ADR 0078 §7.1) | Modal `<dialog>` with `aria-labelledby` pointing at the dialog heading; focus moves to the confirm button on open; `Escape` cancels |

These are **renderer-side** responsibilities; `Sunfish.Foundation.Wayfinder`
emits the state changes (audit events + service-layer return values) and
the renderer translates them into the appropriate live-region
announcements.

## Audit emission

Every OOD authority operation emits exactly one of:

| Event type | Triggered by |
|---|---|
| `OodWatchStarted` | `StartWatchAsync` succeeded; payload includes severity `"High"` |
| `OodWatchRelieved` | `HandoverWatchAsync` relieved-leg; payload includes severity `"Normal"` + optional reason |
| `OodWatchExpired` | `OodWatchExpiryService` tick advanced a watch past its TTL; payload includes severity `"High"` + maxWatchDuration |

A successful `HandoverWatchAsync` emits both `OodWatchRelieved` (for
the prior watch) and `OodWatchStarted` (for the new watch) with an
**identical** `OccurredAt` so audit-log replay surfaces them as a
correlated pair.

Audit emission is best-effort for transient transport hiccups —
`AuditSignatureException` (cryptographic integrity failure) and
`OperationCanceledException` propagate to the caller; other backend
errors are swallowed so authority operations are not denied by audit
backend issues.
