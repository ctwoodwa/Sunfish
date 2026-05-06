# W#46 Phase 1b Follow-Up — `DefaultPermissionResolver` Subscribe-Before-Load Cache Invalidation

**Amends:** [`shared-design-system-stage06-handoff.md`](shared-design-system-stage06-handoff.md) Phase 1 §halt-condition C  
**Reason:** `IStandingOrderEventStream` shipped W#57 (PR #662 2026-05-06); halt-C is now implementable  
**Status:** XO spec — ship as a standalone PR BEFORE Phase 4 (adapter implementations)  
**Estimated size:** ~1h, 1 PR  

---

## Problem

`DefaultPermissionResolver` Phase 1 (PR #622) ships with a 60-second TTL cache of role
assignments (per ADR 0077 §2.5). The xmldoc explicitly notes "halt-condition C" — subscribe-
before-load cache invalidation was deferred pending `IStandingOrderEventStream`, which was
not yet built at Phase 1 time. W#57 (PR #662) ships `IStandingOrderEventStream`. This
addendum implements halt-C.

**The risk without this fix:** A tenant administrator issues a permission-related
Standing Order (e.g., revokes a role assignment). `DefaultPermissionResolver`'s cached
decisions remain stale for up to 60 seconds. With subscribe-before-load, the cache is
invalidated immediately when the Standing Order is applied.

---

## What to Build

**Single file change: `packages/foundation-ship-common/DefaultPermissionResolver.cs`**

### 1. Add optional `IStandingOrderEventStream` constructor parameter

Add `IStandingOrderEventStream? eventStream = null` as the LAST parameter in the existing
constructor. This is a non-breaking optional injection (DI resolves it if registered; tests
that don't register it get the 60s TTL behavior they already test).

```csharp
// Add to using directives (already has Sunfish.Foundation.Wayfinder for ShipAction / ShipLocation):
// No new usings needed — IStandingOrderEventStream + StandingOrderAppliedEvent already in
// Sunfish.Foundation.Wayfinder (foundation-wayfinder already ProjectReference'd in
// packages/foundation-ship-common/Sunfish.Foundation.Ship.Common.csproj).

// Add two fields after _gate:
private readonly IDisposable? _eventStreamSubscription;

// In constructor body (AFTER all other field assignments):
_eventStreamSubscription = eventStream?.Subscribe(OnStandingOrderApplied);
```

### 2. Add the cache-invalidation handler

```csharp
private void OnStandingOrderApplied(StandingOrderAppliedEvent e)
{
    lock (_gate)
    {
        switch (e.Scope)
        {
            case StandingOrderScope.Platform:
                // Platform-wide: all tenants' caches are stale
                _cache.Clear();
                _inflightLoads.Clear();
                break;
            case StandingOrderScope.Integration:
                // Integration config only — permission graph unaffected
                break;
            default:
                // User / Tenant / Security: invalidate the affected tenant's cache
                _cache.Remove(e.TenantId);
                _inflightLoads.Remove(e.TenantId);
                break;
        }
    }
}
```

**Scope reasoning:**
- `User` — only the issuing actor's assignments change; but the cache is per-tenant (not
  per-actor), so the entire tenant's entry must be evicted.
- `Tenant` — role assignments for all actors in the tenant may be affected.
- `Security` — high-privilege scope that may reclassify role-assignment sources; invalidate tenant.
- `Platform` — spans tenants; invalidate all.
- `Integration` — integration-connector config change; does NOT affect `IShipRoleAssignmentSource`
  or `ICapabilityGraph`; safe to skip.

### 3. Add `IDisposable` to the class

```csharp
// Change class declaration from:
public sealed class DefaultPermissionResolver : IPermissionResolver
// to:
public sealed class DefaultPermissionResolver : IPermissionResolver, IDisposable

// Add at end of class body:
public void Dispose() => _eventStreamSubscription?.Dispose();
```

The DI container (Microsoft.Extensions.DependencyInjection) calls `Dispose()` on singleton
services when the application shuts down. No extra wiring needed — adding `IDisposable` is
sufficient.

### 4. Update the xmldoc on the cache paragraph

Remove the "not-yet-shipped" caveat and replace with a note that subscribe-before-load is now active:

```csharp
/// <b>Cache (per ADR 0077 §2.5):</b> Per-tenant 60-second TTL cache of role assignments.
/// When <see cref="IStandingOrderEventStream"/> is provided (registered via
/// <c>AddSunfishHelm()</c> or equivalent), the cache for the affected tenant is
/// invalidated immediately on each <see cref="StandingOrderAppliedEvent"/>
/// (subscribe-before-load; halt-condition C resolved by W#57). When the event stream
/// is NOT provided (e.g., in isolated unit tests), the TTL behaviour applies.
```

---

## Tests

Add to `packages/foundation-ship-common/tests/DefaultPermissionResolverTests.cs`
(or a new `DefaultPermissionResolverCacheTests.cs`):

**4 new tests:**

```csharp
[Fact]
public async Task Cache_InvalidatedOnStandingOrderApplied_TenantScope()
{
    // Arrange: resolve once to populate cache, then publish a Tenant-scoped event
    // Act: resolve again — must call IShipRoleAssignmentSource a second time
    // Assert: IShipRoleAssignmentSource called twice (not cache-served on second call)
}

[Fact]
public async Task Cache_InvalidatedOnStandingOrderApplied_PlatformScope()
{
    // Arrange: populate caches for two different tenants
    // Act: publish Platform-scoped event
    // Assert: IShipRoleAssignmentSource called for both tenants on next resolve
}

[Fact]
public async Task Cache_NotInvalidatedOnStandingOrderApplied_IntegrationScope()
{
    // Arrange: populate cache, then publish Integration-scoped event
    // Act: resolve — should be cache-served
    // Assert: IShipRoleAssignmentSource called only once (initial population)
}

[Fact]
public void Dispose_UnsubscribesFromEventStream()
{
    // Arrange: create resolver with event stream; subscribe count tracked
    // Act: Dispose() the resolver
    // Assert: subsequent events do NOT trigger cache invalidation
    //         (event stream subscription was released)
}
```

Use `InMemoryStandingOrderEventStream` (from `foundation-wayfinder`) as the test double —
it's already in `origin/main` (W#57 PR #662) and is the canonical in-process test double.

---

## DI note

When `AddSunfishHelm()` (registered in `WayfinderServiceExtensions`) is called before
`AddSunfishSharedDesignSystem()` (Phase 5), `IStandingOrderEventStream` will already be
in the container. Microsoft.Extensions.DependencyInjection resolves optional params
(non-nullable in the constructor) when available; since the new param is nullable
(`IStandingOrderEventStream? eventStream = null`), the DI constructor-injection uses
the registered implementation if present, null otherwise. No call-site change needed.

**Recommended startup sequence:**
```csharp
services.AddSunfishHelm();                   // registers IStandingOrderEventStream
services.AddSunfishSharedDesignSystem();     // registers DefaultPermissionResolver (Phase 5)
// DefaultPermissionResolver gets IStandingOrderEventStream injected automatically
```

---

## Halt conditions

- **(H-C1)** `DefaultPermissionResolver` constructor signature includes
  `IStandingOrderEventStream? eventStream = null` as its last parameter.
  Run: `grep -n "IStandingOrderEventStream" packages/foundation-ship-common/DefaultPermissionResolver.cs`
  Expected: ≥1 match.

- **(H-C2)** Class declaration includes `IDisposable`.
  Run: `grep "IPermissionResolver, IDisposable" packages/foundation-ship-common/DefaultPermissionResolver.cs`
  Expected: 1 match.

- **(H-C3)** All 4 new cache-invalidation tests pass.
  Run: `dotnet test packages/foundation-ship-common/tests/ --filter Category=Cache`
  (or without filter: full test run must pass).

- **(H-C4)** Build clean — no new warnings.
  Run: `dotnet build packages/foundation-ship-common/Sunfish.Foundation.Ship.Common.csproj`

---

## PR title and strategy

**PR title:**
`fix(foundation-ship-common): W#46 halt-C — DefaultPermissionResolver subscribe-before-load cache invalidation`

**Sequence:** Ship this BEFORE Phase 4 (adapter implementations). It changes only
`packages/foundation-ship-common/DefaultPermissionResolver.cs` and its test file.
Phase 4 touches only adapter packages — no overlap.

**Pre-merge council:** standard adversarial review (Opus + xhigh). The cache-invalidation
logic has concurrency implications (`lock (_gate)` path) and subscription lifecycle
(IDisposable) — council must verify no deadlock path exists (event handler takes `_gate`;
check that `_gate` is not held during any call that could fire events back into the handler).
