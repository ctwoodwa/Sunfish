# W#48 Phase 1.5 Hand-Off — Cycle-Break Prerequisites for Atlas Integration-Config (ADR 0067)

**Workstream:** W#48  
**Parent hand-off:** `icm/_state/handoffs/atlas-integration-config-stage06-handoff.md`  
**Phase:** 1.5 (prerequisite moves before Phase 1 cycle-blocked deliverables)  
**Status:** `ready-to-build`  
**Authored:** 2026-05-06 (XO ruling on `cob-question-2026-05-06T17-30Z-w48-p1-cycle-halt.md`)  
**Packages modified:** `packages/foundation/` + `packages/foundation-wayfinder/` +
  `packages/foundation-recovery/` + `packages/foundation-ship-common/`  
**Build estimate:** ~2–3h / 2 PRs

---

## Context

W#48 Phase 1 hand-off specified all integration-config contract types as additive to
`packages/ui-core/Wayfinder/Integrations/`. COB (PR #636) identified three confirmed
dependency cycles blocking a subset of Phase 1:

| Chain | Cycle | Blocked types |
|---|---|---|
| #1 | `ui-core → foundation-wayfinder → kernel-crdt → ui-core` | `StandingOrderId` in `IntegrationAtlasView` |
| #2 | `ui-core → foundation-catalog → foundation-featuremanagement → foundation-wayfinder → kernel-crdt → ui-core` | `IntegrationCategoryMapping` (deferred to Phase 2) |
| #3 | `ui-core → foundation-recovery → kernel-security → ui-core` | `IDecryptCapability` in `IDecryptCapabilityProvider` |

**Cycle-safe Phase 1a** (already shippable per COB investigation): enums, value types,
constants — see §Phase 1a below.

**This hand-off (Phase 1.5)** resolves Chains #1 and #3 by moving two leaf value types
into `foundation` (which `ui-core` already references). Chain #2 is deferred — 
`IntegrationCategoryMapping` is an adapter/mapping concern that belongs in Phase 2.

---

## Phase 1a — Ship now (cycle-safe; no prerequisites)

COB may ship Phase 1a independently of this hand-off. These types have no foreign-package
deps:

| Type | Package | Status |
|---|---|---|
| `CredentialAutocompleteHint` (enum) | `ui-core/Wayfinder/Integrations/` | cycle-safe |
| `CredentialFieldKind` (enum) | `ui-core/Wayfinder/Integrations/` | cycle-safe |
| `IntegrationCategory` (enum, 6 values) | `ui-core/Wayfinder/Integrations/` | cycle-safe |
| `ProviderValidationStatus` (enum) | `ui-core/Wayfinder/Integrations/` | cycle-safe |
| `CredentialFieldSpec` (record) | `ui-core/Wayfinder/Integrations/` | cycle-safe |
| `IntegrationProviderSchema` (record) | `ui-core/Wayfinder/Integrations/` | cycle-safe |
| `IntegrationCapabilityPurposes` (constants) | `ui-core/Wayfinder/Integrations/` | cycle-safe |
| `IIntegrationAtlasContext` | `ui-core/Wayfinder/Integrations/` | cycle-safe (uses only `TenantId` + `ActorId` from `foundation`) |
| `IIntegrationProviderValidator` | `ui-core/Wayfinder/Integrations/` | cycle-safe (takes only `CredentialFieldSpec[]`; no foreign deps) |
| `ICustomIntegrationRenderer` | `ui-core/Wayfinder/Integrations/` | cycle-safe (escape hatch; no foreign deps) |
| `IValidationStatusStore` | `ui-core/Wayfinder/Integrations/` | cycle-safe if scoped to `ProviderValidationStatus` (no `IDecryptCapability` dep) |
| `IntegrationCapabilityPurposes` | `ui-core/Wayfinder/Integrations/` | cycle-safe |

**Phase 1a GATE:** None — ship immediately.

---

## Phase 1.5 — Cycle-break moves (2 PRs, ~2–3h)

**GATE:** Phase 1a merged.

### PR 1 — Move `StandingOrderId` + `AuditRecordId` to `foundation`

**Breaks:** Cycle Chain #1 (`ui-core → foundation-wayfinder → kernel-crdt → ui-core`).

After this move, `IntegrationAtlasView` in `ui-core/Wayfinder/Integrations/` can reference
`StandingOrderId` via `foundation` (which `ui-core` already depends on) without pulling in
`foundation-wayfinder`.

#### File 1: `packages/foundation/Assets/Common/StandingOrderId.cs` — new file

```csharp
using System;

namespace Sunfish.Foundation.Assets.Common;

/// <summary>
/// Stable identifier for a StandingOrder. Per ADR 0065 §1.
/// Moved from <c>Sunfish.Foundation.Wayfinder</c> to break a
/// <c>ui-core → foundation-wayfinder → kernel-crdt → ui-core</c>
/// cycle at the W#48 Phase 1.5 boundary.
/// </summary>
public readonly record struct StandingOrderId(Guid Value);

/// <summary>
/// Stable identifier referencing an AuditRecord emitted at the time
/// a Standing Order was issued, amended, rescinded, rejected, or
/// conflict-resolved. Audit-record-id round-trips with
/// <c>Sunfish.Kernel.Audit.AuditRecord.AuditId</c>. Per ADR 0065 §1.
/// Moved from <c>Sunfish.Foundation.Wayfinder</c> alongside StandingOrderId.
/// </summary>
public readonly record struct AuditRecordId(Guid Value);
```

#### File 2: `packages/foundation-wayfinder/StandingOrderId.cs` — delete and redirect

**Option A (recommended):** Delete the file. Update `using` statements in all call sites
within `foundation-wayfinder` to `using Sunfish.Foundation.Assets.Common;`.

**Call sites to update in `foundation-wayfinder/`:**
```bash
grep -rn "StandingOrderId\|AuditRecordId" packages/foundation-wayfinder/ --include="*.cs" \
  | grep -v "StandingOrderId\.cs"
# EXPECT: StandingOrder.cs, CrdtStandingOrderRepository.cs, DefaultStandingOrderIssuer.cs,
#         IStandingOrderIssuer.cs, IStandingOrderRepository.cs, AtlasSettingSnapshot.cs
```

For each: change `using Sunfish.Foundation.Wayfinder;` to add
`using Sunfish.Foundation.Assets.Common;` (or just add the second using; both are fine).

**Also update `packages/foundation-ship-common/ShipRoleAssignment.cs`:**
```bash
grep -n "StandingOrderId\|AuditRecordId" packages/foundation-ship-common/ShipRoleAssignment.cs
```
Add `using Sunfish.Foundation.Assets.Common;` as needed.

**Note on `W#57`:** The `StandingOrderAppliedEvent` hand-off references `StandingOrderId` +
`AuditRecordId`. If W#57 has not yet been built when this PR lands, its hand-off will need
`using Sunfish.Foundation.Assets.Common;` instead of `using Sunfish.Foundation.Wayfinder;`.
If W#57 has already been built, update the existing file.

#### Acceptance criteria for PR 1

```bash
# Types in foundation
grep -n "StandingOrderId\|AuditRecordId" packages/foundation/Assets/Common/StandingOrderId.cs
# EXPECT: both definitions

# Old file gone
find packages/foundation-wayfinder -name "StandingOrderId.cs" | head -1
# EXPECT: empty

# Build clean
dotnet build packages/foundation/ packages/foundation-wayfinder/ \
  packages/foundation-ship-common/ --no-incremental
# EXPECT: 0 errors
```

---

### PR 2 — Move `IDecryptCapability` to `foundation`

**Breaks:** Cycle Chain #3 (`ui-core → foundation-recovery → kernel-security → ui-core`).

After this move, `IDecryptCapabilityProvider` in `ui-core/Wayfinder/Integrations/` can
reference `IDecryptCapability` via `foundation` without pulling in `foundation-recovery`.

**Namespace decision:** Use `Sunfish.Foundation.Crypto` (consistent with `KeyFingerprint`
at `packages/foundation/Crypto/KeyFingerprint.cs`). The old namespace
`Sunfish.Foundation.Recovery.Crypto` is left behind in `foundation-recovery` as a
type-alias for one release cycle to reduce blast radius (optional; see below).

#### File 1: `packages/foundation/Crypto/IDecryptCapability.cs` — new file

```csharp
using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// A capability granting decrypt access to one or more encrypted field values
/// for a specific tenant.
/// Moved from <c>Sunfish.Foundation.Recovery.Crypto</c> to break a
/// <c>ui-core → foundation-recovery → kernel-security → ui-core</c> cycle
/// at the W#48 Phase 1.5 boundary. Per ADR 0046-A2 §Implementation.
/// </summary>
public interface IDecryptCapability
{
    /// <summary>
    /// Stable identifier for this capability, logged in denial audit records.
    /// </summary>
    string CapabilityId { get; }

    /// <summary>
    /// Returns <c>null</c> when the capability is valid for
    /// <paramref name="targetTenant"/> at <paramref name="now"/>;
    /// otherwise returns a short rejection reason (e.g. <c>"expired"</c>,
    /// <c>"wrong-tenant"</c>).
    /// </summary>
    string? ValidateForDecrypt(TenantId targetTenant, DateTimeOffset now);
}
```

#### File 2: `packages/foundation-recovery/Crypto/IDecryptCapability.cs` — replace with type alias

```csharp
// Moved to packages/foundation/Crypto/IDecryptCapability.cs (W#48 Phase 1.5).
// Namespace is now Sunfish.Foundation.Crypto.
// This using alias lets existing callers in foundation-recovery migrate at their pace.
global using IDecryptCapability = Sunfish.Foundation.Crypto.IDecryptCapability;
```

> **Alternative:** Delete the file and update all call sites within `foundation-recovery`
> directly. Call sites: `FixedDecryptCapability.cs`, `TenantKeyProviderFieldDecryptor.cs`,
> `IFieldDecryptor.cs`. Either approach is valid; the alias approach is lower-blast-radius.

#### File 3: Update `blocks-maintenance` call site

```bash
grep -n "IDecryptCapability\|Foundation.Recovery.Crypto" \
  packages/blocks-maintenance/Services/IW9DocumentService.cs \
  packages/blocks-maintenance/Services/InMemoryW9DocumentService.cs
```

Change `using Sunfish.Foundation.Recovery.Crypto;` to `using Sunfish.Foundation.Crypto;`
in each file that references `IDecryptCapability`.

#### Acceptance criteria for PR 2

```bash
# Type in foundation
grep -n "IDecryptCapability" packages/foundation/Crypto/IDecryptCapability.cs
# EXPECT: interface definition

# Build clean
dotnet build packages/foundation/ packages/foundation-recovery/ \
  packages/blocks-maintenance/ --no-incremental
# EXPECT: 0 errors

# Cycle check: ui-core must NOT reference foundation-recovery
grep "ProjectReference" packages/ui-core/Sunfish.UICore.csproj
# EXPECT: no foundation-recovery reference
```

---

## Phase 1b — Remaining cycle-blocked Phase 1 deliverables (after Phase 1.5 PRs merged)

Once Phase 1.5 PRs are on origin/main, ship the remaining Phase 1 deliverables in
`ui-core/Wayfinder/Integrations/`:

| Type | Previous blocker | Now cycle-safe because |
|---|---|---|
| `IIntegrationAtlasProvider` | `StandingOrderId` in `ActiveProviderSnapshot` | `StandingOrderId` now in `foundation` |
| `IntegrationAtlasView` | `ActiveProviderSnapshot.StandingOrderId` | same |
| `ActiveProviderSnapshot` | `StandingOrderId` field | same |
| `IDecryptCapabilityProvider` | `IDecryptCapability` return type | `IDecryptCapability` now in `foundation` |
| `AddSunfishIntegrationAtlas()` DI extension | `IDecryptCapabilityProvider` dep | same |
| 4 `AuditEventType` constants in `kernel-audit` | none | always cycle-safe |
| `ContractSurfaceTests.NoMethodReturnsDecryptedBytes` | needs full Phase 1 surface | now available |

**Phase 1b GATE:** Both Phase 1.5 PRs on origin/main.

> **`IntegrationCategoryMapping` — deferred to Phase 2.** This type bridges
> `IntegrationCategory` (in `ui-core`) with `ProviderCategory` (in `foundation-catalog`).
> It's a mapping/adapter concern; the contract surface is complete without it. Phase 2
> can house it in whatever package best fits (`blocks-integrations` or a dedicated adapter
> package). Chain #2 does NOT block Phase 1b.

---

## §A0 — Cited-symbol audit (2026-05-06)

**Positive-existence (existing symbols moved by this phase):**
- `StandingOrderId` at `packages/foundation-wayfinder/StandingOrderId.cs` ✓
- `AuditRecordId` at `packages/foundation-wayfinder/StandingOrderId.cs` ✓
- `IDecryptCapability` at `packages/foundation-recovery/Crypto/IDecryptCapability.cs` ✓

**Positive-existence (call sites to update):**
- `packages/foundation-wayfinder/StandingOrder.cs` — uses `StandingOrderId` ✓
- `packages/foundation-wayfinder/CrdtStandingOrderRepository.cs` — uses `StandingOrderId` ✓
- `packages/foundation-wayfinder/DefaultStandingOrderIssuer.cs` — uses `StandingOrderId` + `AuditRecordId` ✓
- `packages/foundation-wayfinder/IStandingOrderIssuer.cs` — uses `StandingOrderId` ✓
- `packages/foundation-wayfinder/IStandingOrderRepository.cs` — uses `StandingOrderId` ✓
- `packages/foundation-wayfinder/AtlasSettingSnapshot.cs` — uses `StandingOrderId` ✓
- `packages/foundation-ship-common/ShipRoleAssignment.cs` — uses `StandingOrderId` ✓
- `packages/foundation-recovery/Crypto/FixedDecryptCapability.cs` — implements `IDecryptCapability` ✓
- `packages/foundation-recovery/Crypto/TenantKeyProviderFieldDecryptor.cs` — uses `IDecryptCapability` ✓
- `packages/foundation-recovery/Crypto/IFieldDecryptor.cs` — uses `IDecryptCapability` ✓
- `packages/blocks-maintenance/Services/IW9DocumentService.cs` — uses `IDecryptCapability` ✓
- `packages/blocks-maintenance/Services/InMemoryW9DocumentService.cs` — uses `IDecryptCapability` ✓

**Negative-existence (confirmed absent; introduced by this phase):**
- `foundation/Assets/Common/StandingOrderId.cs` — ZERO on origin/main ✓
- `foundation/Crypto/IDecryptCapability.cs` — ZERO on origin/main ✓

---

## Pre-merge council

**Not required** for Phase 1.5 — these are mechanical moves (type relocation, not new API
design). However, COB should:
1. Verify `dotnet build` across all modified packages passes
2. Verify no test breakage in `foundation-recovery`, `foundation-wayfinder`, `foundation-ship-common`,
   `blocks-maintenance` test suites
3. Note both moves in the PR description with the cycle-break rationale
