# W#48 Phase 2 Addendum — `DefaultIntegrationAtlasProvider` lives in `blocks-integrations`

**Amends:** [`atlas-integration-config-stage06-handoff.md`](atlas-integration-config-stage06-handoff.md) Phase 2  
**Reason:** Package-cycle constraint prevents Phase 2 implementations from living in `ui-core`  
**Status:** XO ruling — apply before starting Phase 2  

---

## Problem

`DefaultIntegrationAtlasProvider` (Phase 2) consumes `IFieldEncryptor` and `IFieldDecryptor`,
both of which live in `packages/foundation-recovery/`. However, `ui-core` **cannot** reference
`foundation-recovery` — a `foundation-recovery → kernel-security → ui-core` cycle already exists
(the same reason `KeyFingerprint` and `IDecryptCapabilityProvider` were placed in
`packages/foundation/Crypto/` rather than `foundation-recovery` during W#53 P1b and W#48 P1.5).

Verify the cycle:
```
packages/foundation-recovery/Sunfish.Foundation.Recovery.csproj
  → kernel-security → Sunfish.Kernel.Security.csproj
    → ui-core → Sunfish.UICore.csproj       ← Phase 2 implementations cannot live here
```

Adding a `<ProjectReference>` to `foundation-recovery` from `ui-core` would close this cycle
and break the build.

---

## XO Ruling: Option A — New `packages/blocks-integrations/` package

`DefaultIntegrationAtlasProvider` and all Phase 2 implementation types go into a new
**`packages/blocks-integrations/`** package at the `blocks-*` composition tier.

**Precedent:** `blocks-maintenance` already references both `foundation-recovery` and `ui-core`
without a cycle (see `packages/blocks-maintenance/Sunfish.Blocks.Maintenance.csproj`). The
`blocks-*` tier sits above `ui-core` in the dependency graph; it may consume both contract
packages and implementation packages freely.

---

## Phase 1b Amendment — `AddSunfishIntegrationAtlas()` scope

Phase 1b's `AddSunfishIntegrationAtlas()` in `packages/ui-core/Wayfinder/Integrations/` ships
**contracts and stores only** — it does NOT register `DefaultIntegrationAtlasProvider` (that
type does not exist in `ui-core`).

**Corrected `ServiceCollectionExtensions.cs` for Phase 1b (`ui-core`):**

```csharp
// packages/ui-core/Wayfinder/Integrations/ServiceCollectionExtensions.cs
// Namespace: Sunfish.UICore.Wayfinder.Integrations

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers integration-atlas contracts + in-memory transient stores.
    /// Call <c>AddSunfishIntegrationAtlasDefaults()</c> from
    /// <c>Sunfish.Blocks.Integrations</c> to also register the reference
    /// implementation.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="IDecryptCapabilityProvider"/> (registered by
    /// <c>AddSunfishRecoveryCoordinator()</c>) to be present in the
    /// container — guards that the decrypt-capability chain is wired before
    /// validation flows are available.
    /// </remarks>
    public static IServiceCollection AddSunfishIntegrationAtlas(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!services.Any(d => d.ServiceType == typeof(IDecryptCapabilityProvider)))
            throw new InvalidOperationException(
                "AddSunfishRecoveryCoordinator() must be called before " +
                "AddSunfishIntegrationAtlas(). " +
                "IDecryptCapabilityProvider is required by DefaultIntegrationAtlasProvider.");
        services.TryAddSingleton<IValidationStatusStore, InMemoryValidationStatusStore>();
        return services;
    }
}
```

**Key difference from original hand-off spec:**  
The original spec called `services.AddSingleton<IIntegrationAtlasProvider, DefaultIntegrationAtlasProvider>()` —
this is WRONG for Phase 1b because `DefaultIntegrationAtlasProvider` lives in `blocks-integrations`,
not in `ui-core`. Remove that line. The registration happens in `blocks-integrations` instead.

**Companion step — Phase 1b still requires:**  
Add `IDecryptCapabilityProvider` implementation registration to `foundation-recovery`:

```csharp
// packages/foundation-recovery/DependencyInjection/ServiceCollectionExtensions.cs
// ADD to the existing AddSunfishRecoveryCoordinator() method body:
services.TryAddSingleton<IDecryptCapabilityProvider, TenantKeyDecryptCapabilityProvider>();
```

Where `TenantKeyDecryptCapabilityProvider` is a new class in
`packages/foundation-recovery/Crypto/TenantKeyDecryptCapabilityProvider.cs`:

```csharp
// Namespace: Sunfish.Foundation.Recovery.Crypto
// Implements: Sunfish.Foundation.Crypto.IDecryptCapabilityProvider
internal sealed class TenantKeyDecryptCapabilityProvider : IDecryptCapabilityProvider
{
    private readonly ITenantKeyProvider _keyProvider;

    public TenantKeyDecryptCapabilityProvider(ITenantKeyProvider keyProvider)
        => _keyProvider = keyProvider;

    public async Task<IDecryptCapability?> AcquireAsync(
        TenantId tenantId, string purpose, TimeSpan ttl,
        CancellationToken ct = default)
    {
        // Acquire the tenant's active key for the given purpose+ttl.
        // Return null (fail-closed) if no key is available for this tenant.
        var key = await _keyProvider.GetActiveKeyAsync(tenantId, ct);
        if (key is null) return null;
        return new TenantKeyDecryptCapability(key, purpose, ttl);
    }
}
```

`TenantKeyDecryptCapability` (also in `foundation-recovery/Crypto/`) is an `IDecryptCapability`
wrapper around the tenant key with TTL tracking. Implementation details are at COB's discretion
(wrap `IFieldDecryptor` scoped to the key).

---

## Phase 2 — Corrected package location

### New package: `packages/blocks-integrations/`

**Create `packages/blocks-integrations/Sunfish.Blocks.Integrations.csproj`:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Blocks.Integrations</PackageId>
    <Description>
      Reference implementations for the Atlas Integration-Config UI surface (ADR 0067).
      Provides DefaultIntegrationAtlasProvider, audit payload factories, and the
      AddSunfishIntegrationAtlasDefaults() DI extension that wires the full stack.
    </Description>
    <PackageTags>sunfish;blocks;integrations;atlas;ui</PackageTags>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="tests/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\foundation-wayfinder\Sunfish.Foundation.Wayfinder.csproj" />
    <ProjectReference Include="..\foundation-recovery\Sunfish.Foundation.Recovery.csproj" />
    <ProjectReference Include="..\kernel-audit\Sunfish.Kernel.Audit.csproj" />
    <ProjectReference Include="..\ui-core\Sunfish.UICore.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Sunfish.Blocks.Integrations.Tests" />
  </ItemGroup>
</Project>
```

**No Blazor adapter reference at Phase 2** — the Blazor/React rendering blocks come in Phase 4.
Phase 2 is implementation + audit; it has no `.razor` files.

### Namespace: `Sunfish.Blocks.Integrations`

All Phase 2 types use this namespace (not `Sunfish.UICore.*`).

### Phase 2 deliverables — corrected locations

| Original spec location | Corrected location |
|---|---|
| `packages/ui-core/Wayfinder/Integrations/DefaultIntegrationAtlasProvider.cs` | `packages/blocks-integrations/DefaultIntegrationAtlasProvider.cs` |
| `packages/ui-core/Wayfinder/Integrations/InMemoryIntegrationAtlasProvider.cs` | `packages/blocks-integrations/InMemoryIntegrationAtlasProvider.cs` |
| `packages/ui-core/Wayfinder/Integrations/DefaultValidationStatusStore.cs` | `packages/blocks-integrations/DefaultValidationStatusStore.cs` |
| `packages/ui-core/Wayfinder/Integrations/InMemoryValidationStatusStore.cs` | `packages/blocks-integrations/InMemoryValidationStatusStore.cs` |
| `packages/ui-core/Wayfinder/Integrations/ServiceCollectionExtensions.cs` (impl registration) | `packages/blocks-integrations/DependencyInjection/ServiceCollectionExtensions.cs` |
| `packages/ui-core/tests/Wayfinder/Integrations/Default*Tests.cs` | `packages/blocks-integrations/tests/*.cs` |

**What STAYS in `ui-core`** (contracts only — no change needed):
- `IIntegrationAtlasProvider.cs` — Phase 1b deliverable
- `IntegrationAtlasView.cs` — Phase 1b deliverable
- `ActiveProviderSnapshot.cs` — Phase 1b deliverable
- `IntegrationAuditPayloads.cs` — Phase 2 typed payload factories (only uses `ui-core` + `kernel-audit` types)
- All Phase 1a files (enums, `CredentialFieldSpec`, etc.) — already shipped

### `AddSunfishIntegrationAtlasDefaults()` in `blocks-integrations`

```csharp
// packages/blocks-integrations/DependencyInjection/ServiceCollectionExtensions.cs
// Namespace: Sunfish.Blocks.Integrations

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full integration-atlas stack: contracts (via
    /// <c>AddSunfishIntegrationAtlas()</c>) plus the reference
    /// <see cref="DefaultIntegrationAtlasProvider"/> implementation.
    /// </summary>
    /// <remarks>
    /// Requires <c>AddSunfishRecoveryCoordinator()</c> to have been called first
    /// (enforced by the inner <c>AddSunfishIntegrationAtlas()</c> guard).
    /// </remarks>
    public static IServiceCollection AddSunfishIntegrationAtlasDefaults(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSunfishIntegrationAtlas(); // registers IValidationStatusStore + guard
        services.TryAddSingleton<IIntegrationAtlasProvider, DefaultIntegrationAtlasProvider>();
        return services;
    }
}
```

**Host wiring pattern (replaces the original `AddSunfishIntegrationAtlas()` call):**
```csharp
// In host startup (Bridge/Anchor):
services.AddSunfishRecoveryCoordinator();          // foundation-recovery
services.AddSunfishIntegrationAtlasDefaults();     // blocks-integrations (registers impl)
// Note: AddSunfishIntegrationAtlas() alone registers only contracts — use Defaults() for full stack.
```

### `DefaultIntegrationAtlasProvider.cs` — implementation notes

All behavior from the main hand-off §7.1 applies, with ONE interface divergence (see below).
Only the namespace and csproj change:

```csharp
// packages/blocks-integrations/DefaultIntegrationAtlasProvider.cs
namespace Sunfish.Blocks.Integrations;  // ← was Sunfish.UICore.Wayfinder.Integrations

public sealed class DefaultIntegrationAtlasProvider : IIntegrationAtlasProvider
{
    // constructor injects:
    // IStandingOrderIssuer         (foundation-wayfinder)
    // IAtlasProjector              (foundation-wayfinder)
    // IAuditTrail                  (kernel-audit)
    // IFieldEncryptor              (foundation-recovery)  ← available here
    // IFieldDecryptor              (foundation-recovery)  ← available here
    // IDecryptCapabilityProvider   (foundation/Crypto)
    // IValidationStatusStore       (ui-core contracts — resolved from DI)
    // IIntegrationAtlasContext     (host — resolved from DI)
    // IEnumerable<IIntegrationSchemaProvider>     (adapter packages)
    // IEnumerable<IIntegrationProviderValidator>  (adapter packages)
    // ... (all other behavior per §7.1 is unchanged)
}
```

### CRITICAL — `IssueXxxAsync` return type divergence (Phase 1b shipping note)

**`IIntegrationAtlasProvider.IssueXxxAsync` returns `Task<StandingOrderId>`, NOT `Task<StandingOrder>`.**

This divergence was introduced by COB during Phase 1b (PR #660) to avoid a second dependency cycle:

```
ui-core → foundation-wayfinder → kernel-crdt → ui-core
```

`StandingOrder` lives in `kernel-crdt` (via `foundation-wayfinder`). Returning it from an
interface defined in `ui-core` would close the cycle. COB adapted by returning `StandingOrderId`
instead — a cycle-safe identifier in `foundation/Assets/Common/`.

**Impact on `DefaultIntegrationAtlasProvider` (Phase 2):**

`IStandingOrderIssuer.IssueAsync` returns the full `StandingOrder`. In the Phase 2
implementation, extract the `StandingOrderId` and return it directly:

```csharp
// In DefaultIntegrationAtlasProvider.IssueProviderChangeAsync, IssueRoutingAsync, etc.:
var standingOrder = await _standingOrderIssuer.IssueAsync(command, ct);
// Do NOT return standingOrder — return its Id only (interface contract)
return standingOrder.Id;   // StandingOrderId
```

The `StandingOrder` object is NOT passed out of the provider; callers who need to observe
the standing order do so via `IAtlasProjector` (GetAtlasViewAsync) or an event stream.

**The main hand-off §7.1 shows `Task<StandingOrder>` return types — treat those as
`Task<StandingOrderId>` for all IssueXxxAsync method signatures.**

### Tests — `packages/blocks-integrations/tests/`

**Create `packages/blocks-integrations/tests/Sunfish.Blocks.Integrations.Tests.csproj`:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Sunfish.Blocks.Integrations.csproj" />
    <ProjectReference Include="..\..\foundation-recovery\Sunfish.Foundation.Recovery.csproj" />
    <ProjectReference Include="..\..\kernel-audit\Sunfish.Kernel.Audit.csproj" />
  </ItemGroup>
</Project>
```

All test file names and test cases from the main hand-off (§ "Tests") apply unchanged.
Move to `packages/blocks-integrations/tests/` — not `packages/ui-core/tests/`.

---

## SUNFISH_INTEGRATION_AUDIT001 analyzer — location unchanged

The Roslyn analyzer goes in **`packages/foundation-wayfinder-analyzers/`** as originally
specified. Analyzers run at compile time and do not add runtime `ProjectReference` dependencies.
No change needed to that deliverable.

---

## `IntegrationAuditPayloads.cs` — location unchanged

Stays in **`packages/ui-core/Wayfinder/Integrations/IntegrationAuditPayloads.cs`** — it only
references `AuditRecord` + `AuditEventType` from `kernel-audit` and value types from `ui-core`.
No cycle concern.

---

## Halt conditions (Phase 2 — additional)

These supplement the H5–H8 halt conditions in the main hand-off:

- **(H9) `blocks-integrations` project builds clean before opening PR.**
  Run: `dotnet build packages/blocks-integrations/Sunfish.Blocks.Integrations.csproj`
  Expected: zero errors, zero warnings.

- **(H10) `ui-core` project still builds clean after Phase 1b.**
  Run: `dotnet build packages/ui-core/Sunfish.UICore.csproj`
  Expected: zero errors. This verifies no `foundation-recovery` reference crept in.

- **(H11) `IDecryptCapabilityProvider` registered via `AddSunfishRecoveryCoordinator()`.**
  Run: `grep -rn "IDecryptCapabilityProvider" packages/foundation-recovery/` ≥1 match.
  If zero → HALT; the Phase 1b companion step (register `TenantKeyDecryptCapabilityProvider`)
  must be complete before Phase 2 starts.

---

## ADR amendment note

ADR 0067 §A0.5 states "No new package is introduced by ADR 0067." This addendum constitutes a
**post-acceptance architectural amendment** (equivalent to an A1 amendment) for Phase 2 only.
The "no new package" ruling applied to Phase 1 contracts; Phase 2 implementations require a
`blocks-*` tier home due to the encryption-dependency cycle. Phase 1 contracts remain in
`ui-core` as specified.

XO has ruled; no separate ADR amendment PR is required for this cycle-resolution. The
constraint is documented here and in the main hand-off HALT block. If CO disagrees with
Option A, escalate before starting Phase 2.
