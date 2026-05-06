# `IActorPrincipalResolver` Stage 06 Hand-off

**Scope:** `packages/foundation-ship-common/` — 2 new files + 1 partial extension + 3–5 tests
**ADR:** ADR 0077 §1.2 (identity model); no new ADR required — additive to existing substrate
**XO ruling date:** 2026-05-06 (resolves COB question PR #671)
**Effort:** ~1h | **PR:** 1 | **Pre-merge council:** mandatory (seam design + canonical invariant)

---

## Background and ruling

`DefaultPermissionResolver.ResolveAsync` takes `Principal subject` (line ~250). Every Phase 2
data provider (W#51 / W#52 / W#48 / W#54 / W#55) has `ActorId actor` in its data-provider
interfaces and must bridge `ActorId → Principal` to call the resolver.

**Canonical Sunfish invariant** (confirmed by `DefaultPermissionResolver.cs` comment at line 250):

> `ActorId.Value = PrincipalId.ToBase64Url()` — the 43-character base64url (RFC 4648 §5,
> unpadded) encoding of the 32-byte Ed25519 public key.

This is the ONLY sanctioned derivation. **SHA-256 of `ActorId.Value` is EXPLICITLY FORBIDDEN** —
it produces a byte array that looks like a valid `PrincipalId` but is NOT an Ed25519 public key
and will NOT match the canonical ActorId encoding used by `DefaultPermissionResolver`.

`DefaultPermissionResolver` line 250 is **CORRECT — do not change it**.

`IActorPrincipalResolver` is a seam for:
1. Phase 2 data providers that need `Principal` from an `ActorId`.
2. Production hosts whose `ActorId` may be a UUID or tenant-assigned identifier rather than
   the raw base64url key (replace `InMemoryActorPrincipalResolver` with a key-store impl).
3. Test fixtures that use non-canonical `ActorId` values like `"alice"` (explicit registration).

---

## Deliverables

### File 1 — `packages/foundation-ship-common/IActorPrincipalResolver.cs`

```csharp
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Resolves an <see cref="ActorId"/> to its canonical <see cref="Principal"/>
/// (Ed25519 public-key identity). Used by Phase 2 data providers that receive
/// <see cref="ActorId"/> from standing-order context and need to call
/// <see cref="IPermissionResolver"/> (which takes <see cref="Principal"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Canonical invariant.</b> In Sunfish's self-sovereign identity model,
/// <c>ActorId.Value</c> is the 43-character base64url (RFC 4648 §5, unpadded)
/// encoding of the 32-byte Ed25519 public key. <see cref="InMemoryActorPrincipalResolver"/>
/// enforces this invariant as the fallback path.
/// </para>
/// <para>
/// <b>Null means fail-closed.</b> When <see cref="ResolveAsync"/> returns
/// <c>null</c>, the actor cannot be resolved to a principal. Callers MUST treat
/// <c>null</c> as a deny / skip — never assume an unresolvable actor is permitted.
/// </para>
/// </remarks>
public interface IActorPrincipalResolver
{
    /// <summary>
    /// Resolves <paramref name="actorId"/> to its <see cref="Principal"/> within
    /// <paramref name="tenantId"/>. Returns <c>null</c> if the actor cannot be
    /// resolved — callers must treat <c>null</c> as fail-closed.
    /// </summary>
    ValueTask<Principal?> ResolveAsync(
        TenantId tenantId,
        ActorId actorId,
        CancellationToken ct = default);
}
```

---

### File 2 — `packages/foundation-ship-common/InMemoryActorPrincipalResolver.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// In-process default implementation of <see cref="IActorPrincipalResolver"/>.
/// </summary>
/// <remarks>
/// <para>
/// Falls back to the canonical Sunfish ActorId invariant:
/// <c>ActorId.Value = PrincipalId.ToBase64Url()</c> — the 43-character base64url
/// encoding of the 32-byte Ed25519 public key. Returns <c>null</c> if the value
/// is not a valid base64url-encoded 32-byte key.
/// </para>
/// <para>
/// Use <see cref="Register"/> to add explicit <c>ActorId → Principal</c> mappings
/// for test fixtures that use non-canonical <see cref="ActorId"/> values (e.g.
/// <c>ActorId("alice")</c>). Registered mappings take precedence over the derivation.
/// </para>
/// <para>
/// <b>Thread safety:</b> <see cref="Register"/> is synchronized via a lock.
/// <see cref="ResolveAsync"/> holds the lock only for the registered-mapping
/// lookup; the canonical derivation runs lock-free.
/// </para>
/// </remarks>
public sealed class InMemoryActorPrincipalResolver : IActorPrincipalResolver
{
    private readonly Dictionary<ActorId, Principal> _overrides = new();
    private readonly object _gate = new();

    /// <summary>
    /// Registers an explicit <paramref name="actorId"/> → <paramref name="principal"/>
    /// mapping. Takes precedence over the canonical base64url derivation.
    /// </summary>
    public void Register(ActorId actorId, Principal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        lock (_gate)
            _overrides[actorId] = principal;
    }

    /// <inheritdoc />
    public ValueTask<Principal?> ResolveAsync(
        TenantId tenantId,
        ActorId actorId,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_overrides.TryGetValue(actorId, out var registered))
                return ValueTask.FromResult<Principal?>(registered);
        }

        // Canonical invariant: ActorId.Value = PrincipalId.ToBase64Url()
        try
        {
            var id = PrincipalId.FromBase64Url(actorId.Value);
            return ValueTask.FromResult<Principal?>(new Individual(id));
        }
        catch (FormatException)
        {
            return ValueTask.FromResult<Principal?>(null);
        }
    }
}
```

---

### DI registration

**Do NOT** create `ShipCommonServiceExtensions.cs` in this PR — Phase 5 of W#46 authors that file
and adds `AddSunfishSharedDesignSystem()`. Instead, each Phase 2 data provider registers the
resolver in its own `AddSunfish*()` extension via `TryAddSingleton`:

```csharp
services.TryAddSingleton<IActorPrincipalResolver, InMemoryActorPrincipalResolver>();
```

This is the pattern for all five cohort Phase 2 packages:
- `foundation-quarterdeck` (`AddSunfishQuarterdeck()` — Phase 2 DI additions)
- `foundation-tactical` (`AddSunfishTactical()` — Phase 2 DI additions)
- `blocks-integrations` (`AddSunfishIntegrationAtlasDefaults()` — new in W#48 Phase 2)
- `foundation-sick-bay` (`AddSunfishSickBay()` — Phase 2 DI additions)
- `foundation-ships-office` (`AddSunfishShipsOffice()` — Phase 2 DI additions)

Phase 5 of W#46 will also call `services.TryAddSingleton<IActorPrincipalResolver, InMemoryActorPrincipalResolver>()` inside `AddSunfishSharedDesignSystem()` so hosts that use the umbrella extension get it automatically.

---

### Tests — `packages/foundation-ship-common/tests/ActorPrincipalResolverTests.cs`

Minimum 4 tests:

1. `Resolve_CanonicalActorId_ReturnsDerivedIndividual`
   — `Register()` nothing; pass a valid 43-char base64url string as `actorId.Value`;
   expect `Individual` with matching `PrincipalId`.

2. `Resolve_RegisteredOverride_ReturnsOverridePrincipal`
   — `Register(actorId, principal)` with a test `ActorId("alice")`;
   verify returned principal == registered.

3. `Resolve_InvalidBase64UrlActorId_ReturnsNull`
   — pass `ActorId("not-a-key")` with no override;
   expect `null`.

4. `Resolve_OverrideByActorId_IsNotAffectedByTenantId`
   — register override once; resolve with two different `TenantId` values;
   expect same principal both times (override is actor-scoped, not tenant-scoped).

5. `Resolve_CanonicalRoundTrip_PrincipalIdMatchesOriginal` *(optional but recommended)*
   — generate `PrincipalId.FromBase64Url(key)`, derive `ActorId(principalId.ToBase64Url())`,
   resolve; expect `Individual(principalId)` with byte-identical `Id`.

---

### Acceptance gate

```
PASS: dotnet test packages/foundation-ship-common/ -c Release
PASS: pre-merge council (seam design + canonical invariant doc)
FAIL: SHA-256 derivation appears anywhere in the implementation
FAIL: TenantId used as override-map key in InMemoryActorPrincipalResolver
FAIL: DefaultPermissionResolver.cs modified by this PR (MUST NOT change)
```

---

## How Phase 2 data providers use the resolver

Pattern (all five Phase 2 providers follow the same shape):

```csharp
// Constructor injection — non-optional:
public DefaultQuarterdeckDataProvider(
    IPermissionResolver permissionResolver,
    IActorPrincipalResolver actorPrincipalResolver,
    // ... other deps)

// In GetSnapshotAsync / TryIssueAsync / GetAtlasViewAsync:
var principal = await _actorPrincipalResolver.ResolveAsync(tenantId, actorId, ct)
    ?? throw new InvalidOperationException(
        $"Cannot resolve principal for actor '{actorId}' — " +
        "host must register IActorPrincipalResolver or use canonical base64url ActorId.");

var decision = await _permissionResolver.ResolveAsync(
    tenantId, principal, location, deck, action, resource, ct);
```

**Fail-closed rule:** if `ResolveAsync` returns null, the operation MUST be denied or skipped —
never assume unresolvable == allowed.

---

## Halt conditions

| # | Condition | Consequence |
|---|---|---|
| H1 | `foundation-ship-common` not on `origin/main` (W#46 P1) | Blocked — this PR depends on `IPermissionResolver`, `ActorId` |
| H2 | PR ships without pre-merge council | Not allowed — seam design is substrate; council mandatory |

Both are cleared as of 2026-05-06 (W#46 Phase 1 on origin/main).

---

## PR title

`feat(foundation-ship-common): IActorPrincipalResolver seam — ActorId→Principal bridge for Phase 2 data providers`
