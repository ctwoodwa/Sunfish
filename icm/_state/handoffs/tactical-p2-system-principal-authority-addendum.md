# W#52 Phase 2 Addendum — `IssueEmergencyStandingOrder` authority via identity, not ShipRole

**Amends:** [`tactical-anomaly-detection-stage06-handoff.md`](tactical-anomaly-detection-stage06-handoff.md) Phase 2 §2.3  
**Reason:** `ShipRole.System` does not exist in the v1 enum; hand-off §2.3 incorrectly assumed it would  
**Status:** XO ruling — apply before starting Phase 2  
**Filed by COB:** PR #658 divergence note "ShipRole.System gap — filed for council disposition"

---

## Problem

The hand-off Phase 2 §2.3 states:
> `ISystemPrincipalProvider` wiring: ... IPermissionResolver check for `IssueEmergencyStandingOrder`
> against the system principal.

And the `ISystemPrincipalProvider` xmldoc in the hand-off stated:
> The returned Principal carries `ShipRole.System` — not assignable to human actors.
> IPermissionResolver MUST grant `IssueEmergencyStandingOrder` to `ShipRole.System`.

**`ShipRole.System` does NOT exist.** The v1 `ShipRole` enum (ADR 0077 §1, shipped PR #622)
has 11 values: `Captain / XO / EngineerOfficer / Navigator / TacticalOfficer / DivisionOfficer /
IDC / Scribe / SUPPO / OOD / EOOW`. No `System` value.

Adding `ShipRole.System` would require an ADR 0077 api-change amendment. XO ruling: do NOT add it.
The `IPermissionResolver` is a role-based policy gate for human actors; system authority is a
different concern resolved via identity, not role hierarchy.

---

## XO Ruling: Identity-based authority check (no IPermissionResolver for system actions)

`TryIssueAsync` in `DefaultThreatTriggerService` (Phase 2) checks system-principal authority
as follows — **replace the hand-off §2.3 "IPermissionResolver check" with this**:

```csharp
// In DefaultThreatTriggerService.TryIssueAsync, before the rate-limit checks:

// Step 0 — Verify system-principal identity (authority check, not role check):
var systemPrincipal = _systemPrincipalProvider.GetSystemPrincipal();
if (systemPrincipal is null)
{
    await _auditTrail.RecordAsync(new AuditRecord(
        AuditEventType.TacticalAuthorizationDenied,
        JsonNode.Parse($"{{\"denialReason\":\"no-system-principal-registered\",\"action\":\"IssueEmergencyStandingOrder\"}}")));
    return null;
}

// The system principal identity is authoritative — no IPermissionResolver check.
// DefaultThreatTriggerService is only called by system-owned code paths
// (rule engine, scheduled triggers). If the system principal is registered,
// authority is granted. Tenant binding is enforced separately in §2.5.
```

**What this means:**
- Do NOT call `IPermissionResolver.ResolveAsync(...)` for `IssueEmergencyStandingOrder`
- The `IssueEmergencyStandingOrder` ShipAction constant is **informational/reserved** — documents
  the system boundary in ADR terms, but NOT processed through `IPermissionResolver`
- Audit on the resolved identity uses `systemPrincipal.ActorId` (for traceability)
- The existing tenant-binding check (§2.5) still applies: `alert.TenantId == ambient.TenantId`

---

## Phase 3a startup registration check — EXCLUDE system-only ShipActions

The hand-off §2.4 says "validate all 7 ShipAction values at startup." This includes
`IssueEmergencyStandingOrder` and `ManageThreatTriggers`.

**Corrected Phase 3a startup check:** register only the 5 human-actor ShipActions with
`IPermissionResolver`; explicitly SKIP the 2 system-reserved ones:

```csharp
// In blocks-tactical DI extension, at startup:
var humanActorActions = new[]
{
    ShipAction.ViewTactical,
    ShipAction.ViewFireControl,
    ShipAction.AcknowledgeTacticalAlert,
    ShipAction.OpenIncident,
    ShipAction.CloseIncident,
};
// IssueEmergencyStandingOrder → system-principal identity check (NOT IPermissionResolver)
// ManageThreatTriggers        → reserved v1, not yet registered

foreach (var action in humanActorActions)
{
    if (!permissionResolver.IsRegistered(action))
        throw new InvalidOperationException(
            $"IPermissionResolver must register ShipAction '{action.Value}' before blocks-tactical starts.");
}
```

If `IPermissionResolver` does not expose an `IsRegistered` check: follow the W#49 cohort
pattern and perform a smoke-test resolution with a sentinel actor at startup instead.

---

## `ISystemPrincipalProvider` xmldoc correction

COB shipped the interface in Phase 1 (PR #658) with a xmldoc referencing `ShipRole.System`.
Phase 2 should update the xmldoc to remove the `ShipRole.System` reference:

```csharp
// packages/foundation-tactical/ISystemPrincipalProvider.cs — UPDATE in Phase 2

/// <summary>
/// Provides the system-owned principal used by automated processes that act
/// outside the human-actor role hierarchy (ADR 0081 §4.1).
/// </summary>
/// <remarks>
/// <para>
/// The system principal is an automation identity, not a human actor — it does
/// not carry a <see cref="Sunfish.Foundation.Ship.Common.ShipRole"/>. Authority for
/// <c>IssueEmergencyStandingOrder</c> is enforced via identity check
/// (presence of a registered system principal + tenant binding), not via
/// <see cref="Sunfish.Foundation.Ship.Common.IPermissionResolver"/>.
/// </para>
/// <para>
/// Implementations MUST be registered at DI bootstrap before any
/// <c>DefaultThreatTriggerService</c> invocation. If <see cref="GetSystemPrincipal"/>
/// returns null at call time, the service fails closed (returns null; emits
/// <c>TacticalAuthorizationDenied</c>).
/// </para>
/// </remarks>
public interface ISystemPrincipalProvider
{
    /// <summary>
    /// Returns the system-owned principal for this node, or <c>null</c>
    /// if no system principal is registered.
    /// </summary>
    Principal? GetSystemPrincipal();
}
```

---

## Halt conditions (Phase 2 additional)

- **(H-SP1) `ISystemPrincipalProvider` is on origin/main** (shipped in Phase 1 PR #658 ✓).
  Verify: `grep -rn "ISystemPrincipalProvider" packages/foundation-tactical/` ≥1 match.

- **(H-SP2) No `ShipRole.System` reference in Phase 2 code.**
  After building Phase 2, run:
  `grep -rn "ShipRole.System" packages/foundation-tactical/ packages/blocks-tactical/`
  Expected: ZERO matches. If any found, remove — `ShipRole.System` is a hand-off authoring error.

- **(H-SP3) `TryIssueAsync_returns_null_when_no_system_principal_registered` test passes.**
  Add to `DefaultThreatTriggerServiceTests.cs`:
  ```csharp
  [Fact]
  public async Task TryIssueAsync_returns_null_when_no_system_principal_registered()
  {
      var provider = Substitute.For<ISystemPrincipalProvider>();
      provider.GetSystemPrincipal().Returns((Principal?)null);
      // ... build sut with this provider
      var result = await sut.TryIssueAsync(signal, template, ct);
      Assert.Null(result);
      // Verify TacticalAuthorizationDenied emitted
  }
  ```
