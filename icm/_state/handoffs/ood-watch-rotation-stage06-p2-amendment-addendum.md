# W#49 Stage 06 — Phase 2 Amendment Addendum

**Issued:** 2026-05-06 (XO post-merge council review of PR #614)
**Status:** REQUIRED — apply before starting Phase 3

PR #614 (`DefaultOodWatchService` + `OodWatchExpiryService`) merged 2026-05-05T22:58Z. XO's
security-engineering council review ran after merge (comment #4383601224 on PR #614) and found
4 issues that survived COB's own council pass. These must be fixed before Phase 3 (docs +
changelog) to avoid shipping Phase 3 with known ADR 0078 §Trust gaps.

---

## Pre-Phase-3 fixes (ship as a single "P2 amendment" PR)

**PR title suggestion:** `fix(foundation-wayfinder): W#49 P2 amendment — TOCTOU, ILogger, OodHandoverKind, sweep interface`

---

### Fix R1 — Remove TOCTOU pre-check from `StartWatchAsync`

**File:** `packages/foundation-wayfinder/DefaultOodWatchService.cs`

Remove these 3 lines from `StartWatchAsync`:
```csharp
// DELETE:
var existing = await _repository.GetCurrentWatchAsync(tenantId, role, ct).ConfigureAwait(false);
if (existing is not null)
    throw new OodWatchConflictException(existing.Id, tenantId, role);
```

**Rationale:** ADR 0078 §1 assigns the single-Active invariant to the persistence layer via
DB-level unique index. The service pre-check is:
- Redundant (`_repository.StartWatchAsync` already throws `OodWatchConflictException`)
- TOCTOU — two concurrent callers can both observe `null`, then the second caller gets a
  repository-level throw instead of the pre-check. Outcome is correct but the pre-check
  creates a false impression the service owns the invariant.
- Adds an extra round-trip per start operation

The `IOodWatchRepository.StartWatchAsync` contract guarantees `OodWatchConflictException`
on violation. Trust it.

**Test update:** `StartWatch_ConflictThrows_WhenActiveExists` — verify the repo mock is the
one throwing `OodWatchConflictException` (not a pre-check path). The test behaviour should
be identical; only the call path changes.

---

### Fix R2 — Add `ILogger` on audit swallow

**Files:** `packages/foundation-wayfinder/DefaultOodWatchService.cs` and
`packages/foundation-wayfinder/OodWatchExpiryService.cs`

Add `ILogger<DefaultOodWatchService>` (non-nullable) and `ILogger<OodWatchExpiryService>` to
each constructor. Log before the best-effort swallow:

```csharp
// DefaultOodWatchService constructor addition:
private readonly ILogger<DefaultOodWatchService> _logger;

public DefaultOodWatchService(
    IOodWatchRepository repository,
    ILogger<DefaultOodWatchService> logger,       // ADD — non-nullable
    IAuditTrail? auditTrail = null,
    IOperationSigner? signer = null,
    TimeProvider? timeProvider = null)
{
    _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    ...
}

// In EmitAuditAsync catch block — replace the comment with:
catch (Exception ex) when (ex is not AuditSignatureException && ex is not OperationCanceledException)
{
    _logger.LogError(ex,
        "OOD audit write failed for {EventType} on watch {WatchId}; continuing best-effort",
        eventType, watchId);
}
```

Same pattern in `OodWatchExpiryService.EmitExpiredAuditAsync`.

**DI registration update:** `WayfinderServiceExtensions.AddSunfishWayfinder` passes
`ILoggerFactory` or uses the `ILogger<T>` DI overload:
```csharp
services.AddScoped<IOodWatchService, DefaultOodWatchService>();
// ILogger<DefaultOodWatchService> is auto-resolved from the DI logger infrastructure
```

No explicit logger registration needed — DI provides `ILogger<T>` automatically when
`services.AddLogging()` is called (which it is in any ASP.NET Core or Aspire host).

**Test update:** inject `NullLogger<DefaultOodWatchService>.Instance` in test constructors.

---

### Fix R3 — Add `OodHandoverKind` enum + `handoverKind` to audit payload

**New file:** `packages/foundation-wayfinder/OodHandoverKind.cs`

```csharp
namespace Sunfish.Foundation.Wayfinder;

/// <summary>Distinguishes a routine watch-change from an authority-ordered relief.</summary>
public enum OodHandoverKind
{
    /// <summary>Outgoing watch-keeper voluntarily transfers; both parties are present.</summary>
    Voluntary,
    /// <summary>Commanding authority relieves the watch-keeper (incapacitation, emergency,
    /// disciplinary).</summary>
    CommandRelieved,
}
```

**`IOodWatchService.HandoverWatchAsync` — add `handoverKind` parameter:**
```csharp
ValueTask<(OodWatch Relieved, OodWatch Started)> HandoverWatchAsync(
    OodWatchId currentWatchId,
    ActorId incomingActor,
    ActorId requestedBy,
    OodHandoverKind handoverKind,   // ADD
    string? reason,
    CancellationToken ct = default);
```

**`IOodWatchRepository.HandoverWatchAsync` — add `handoverKind` (pass-through for audit):**
```csharp
ValueTask<(OodWatch Relieved, OodWatch Started)> HandoverWatchAsync(
    OodWatchId currentWatchId,
    ActorId incomingActor,
    ActorId requestedBy,
    CancellationToken ct = default);
// NOTE: repository doesn't need handoverKind — it's an audit discriminator, not a
// persistence variant. DefaultOodWatchService passes it to EmitRelievedAuditAsync.
```

**`DefaultOodWatchService.EmitRelievedAuditAsync` — include in payload:**
```csharp
private ValueTask EmitRelievedAuditAsync(
    OodWatch watch, ActorId requestedBy,
    OodHandoverKind handoverKind,  // ADD
    string? reason,
    DateTimeOffset occurredAt, CancellationToken ct)
{
    var body = new Dictionary<string, object?>
    {
        ["actor"]        = watch.OnWatchActor.Value,
        ["relievedBy"]   = requestedBy.Value,
        ["handoverKind"] = handoverKind.ToString(),   // ADD
        ["role"]         = watch.Role.ToString(),
        ["severity"]     = handoverKind == OodHandoverKind.CommandRelieved ? "High" : "Normal",
        ["tenantId"]     = watch.TenantId.Value,
        ["watchId"]      = watch.Id.Value,
    };
    if (reason is not null) body["reason"] = reason;
    return EmitAuditAsync(AuditEventType.OodWatchRelieved, watch.TenantId,
        new AuditPayload(body), occurredAt, ct);
}
```

**Test update:** update `HandoverWatch_EmitsBothAuditEvents` to pass `OodHandoverKind.Voluntary`
and assert the payload contains `["handoverKind"] = "Voluntary"`.

---

### Fix R4 — Extract `internal IOodWatchSweepRepository`

**New file:** `packages/foundation-wayfinder/IOodWatchSweepRepository.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Cross-tenant sweep interface for the expiry background service ONLY.
/// Internal to prevent application code from enumerating watches across tenants.
/// </summary>
internal interface IOodWatchSweepRepository
{
    /// <summary>
    /// Returns all Active watches whose <c>StartedAt + MaxWatchDuration &lt; cutoff</c>
    /// across ALL tenants. Called exclusively by <see cref="OodWatchExpiryService"/>.
    /// </summary>
    IAsyncEnumerable<OodWatch> GetExpiredCandidatesAsync(
        DateTimeOffset cutoff, CancellationToken ct = default);
}
```

**`IOodWatchRepository.cs` — remove `GetExpiredCandidatesAsync`** from the public interface.
The public interface should only expose per-tenant operations.

**`OodWatchExpiryService` — change constructor parameter:**
```csharp
// Replace: IOodWatchRepository _repository
// With: IOodWatchRepository _repository + IOodWatchSweepRepository _sweepRepository
// OodWatchExpiryService.SweepOnceAsync calls _sweepRepository.GetExpiredCandidatesAsync
```

**`WayfinderServiceExtensions.AddSunfishWayfinder` — register sweep repo:**
The concrete repository (typically added by the application) should implement both
`IOodWatchRepository` and `IOodWatchSweepRepository`. The DI extension registers:
```csharp
// Concrete repo registered by the host implements both:
// services.AddScoped<ConcreteOodWatchRepository>();
// services.AddScoped<IOodWatchRepository>(sp => sp.GetRequiredService<ConcreteOodWatchRepository>());
// services.AddScoped<IOodWatchSweepRepository>(sp => sp.GetRequiredService<ConcreteOodWatchRepository>());
```
Document this pattern in the DI extension XML doc.

**Test update:** inject a separate `IOodWatchSweepRepository` mock in
`OodWatchExpiryServiceTests`.

---

## Acceptance gate (P2 amendment PR)

- [ ] `StartWatchAsync` has no pre-check (R1)
- [ ] `DefaultOodWatchService` has `ILogger` + logs on audit swallow (R2)
- [ ] `OodWatchExpiryService` has `ILogger` + logs on audit swallow (R2)
- [ ] `OodHandoverKind` enum exists; `HandoverWatchAsync` takes it; payload includes `handoverKind` (R3)
- [ ] `IOodWatchSweepRepository` is `internal`; `GetExpiredCandidatesAsync` removed from public `IOodWatchRepository` (R4)
- [ ] All existing tests pass; updated test reflects new call signatures
- [ ] `dotnet build packages/foundation-wayfinder/` clean
- [ ] Pre-merge council: security-engineering subagent (R4 interface separation is a security boundary)

---

## References

- XO follow-up council comment: PR #614 comment #4383601224 (2026-05-06)
- ADR 0078 §Trust + §2 — signing/audit requirements + atomicity contract
- PR #614 — P2 implementation (amendment commit: c7a35f8)
