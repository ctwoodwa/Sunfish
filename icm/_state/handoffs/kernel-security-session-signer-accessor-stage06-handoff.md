# W#65 — kernel-security `ISessionSignerAccessor` + `IBoundEd25519Signer`

**Owner:** sunfish-PM
**Workstream:** W#65 (`icm/_state/workstreams/W65-kernel-security-session-signer-accessor.md`)
**Estimated effort:** ~2-3h / 1 PR
**Unblocks:** W#63 Phase 2 `ApproveRecoveryPage` (can submit attestations once this lands)

---

## Context

`IEd25519Signer` (in `kernel-security/Crypto/`) is a raw algorithm wrapper — callers must supply the private key bytes on every `Sign()` call. The W#63 `ApproveRecoveryPage` needs to sign recovery attestations with the session's identity key, but there is no public DI contract that provides a key-bound signer without exposing raw key bytes to the caller.

`IOperationSigner` (in `foundation/Crypto/`) wraps `SignedOperation<T>` envelopes — not suitable for raw-byte signing needed by the recovery flow.

**This workstream adds the missing seam:** `ISessionSignerAccessor` resolves the current team's identity Ed25519 key pair from `IRootSeedProvider` + `ITeamSubkeyDerivation` and returns an `IBoundEd25519Signer` that holds the key internally, never surfacing the raw key bytes.

---

## Derivation chain (verified)

```
IRootSeedProvider.GetRootSeedAsync()          → 32-byte root Ed25519 seed
ITeamSubkeyDerivation.DeriveSubkey(root, teamId) → 64-byte derived material
                                    [0..32]   → team's Ed25519 seed (sign key)
                                    [32..64]  → reserved
IEd25519Signer.GenerateFromSeed(seed)         → (PublicKey, PrivateKey)
```

This derivation is already used by `RoleKeyManager` and `AttestationIssuer` for attestation issuance; this workstream wraps it as a DI service.

---

## PR 1 — `ISessionSignerAccessor` + `IBoundEd25519Signer` (1 PR, ~2-3h)

### New files

**`packages/kernel-security/Session/IBoundEd25519Signer.cs`**
```csharp
namespace Sunfish.Kernel.Security.Session;

/// <summary>
/// An Ed25519 signer pre-loaded with a session identity private key.
/// The private key is never exposed to callers.
/// </summary>
public interface IBoundEd25519Signer
{
    /// <summary>The Ed25519 public key (32 bytes) matching the held private key.</summary>
    ReadOnlyMemory<byte> PublicKey { get; }

    /// <summary>Signs <paramref name="data"/> with the held private key. Returns a 64-byte signature.</summary>
    ValueTask<byte[]> SignAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
}
```

**`packages/kernel-security/Session/ISessionSignerAccessor.cs`**
```csharp
namespace Sunfish.Kernel.Security.Session;

/// <summary>
/// Resolves an <see cref="IBoundEd25519Signer"/> pre-loaded with the current team's
/// device-identity private key, derived via ADR 0032's team-subkey pipeline.
/// </summary>
public interface ISessionSignerAccessor
{
    /// <summary>
    /// Returns a signer bound to the active team's identity key.
    /// </summary>
    /// <exception cref="InvalidOperationException">No team is currently active.</exception>
    ValueTask<IBoundEd25519Signer> GetCurrentAsync(CancellationToken ct = default);
}
```

**`packages/kernel-security/Session/DefaultBoundEd25519Signer.cs`**
```csharp
namespace Sunfish.Kernel.Security.Session;

internal sealed class DefaultBoundEd25519Signer : IBoundEd25519Signer
{
    private readonly IEd25519Signer _signer;
    private readonly byte[] _privateKeySeed; // held internally; never returned

    internal DefaultBoundEd25519Signer(
        IEd25519Signer signer,
        ReadOnlySpan<byte> privateKeySeed,
        byte[] publicKey)
    {
        _signer = signer;
        _privateKeySeed = privateKeySeed.ToArray();
        PublicKey = publicKey;
    }

    public ReadOnlyMemory<byte> PublicKey { get; }

    public ValueTask<byte[]> SignAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var sig = _signer.Sign(data.Span, _privateKeySeed);
        return ValueTask.FromResult(sig);
    }
}
```

**`packages/kernel-security/Session/DefaultSessionSignerAccessor.cs`**
```csharp
namespace Sunfish.Kernel.Security.Session;

public sealed class DefaultSessionSignerAccessor : ISessionSignerAccessor
{
    private readonly IRootSeedProvider _rootSeed;
    private readonly ITeamSubkeyDerivation _subkeyDerivation;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly IEd25519Signer _ed25519;

    public DefaultSessionSignerAccessor(
        IRootSeedProvider rootSeed,
        ITeamSubkeyDerivation subkeyDerivation,
        IActiveTeamAccessor activeTeam,
        IEd25519Signer ed25519)
    {
        _rootSeed = rootSeed;
        _subkeyDerivation = subkeyDerivation;
        _activeTeam = activeTeam;
        _ed25519 = ed25519;
    }

    public async ValueTask<IBoundEd25519Signer> GetCurrentAsync(CancellationToken ct = default)
    {
        var active = _activeTeam.Active
            ?? throw new InvalidOperationException(
                "ISessionSignerAccessor.GetCurrentAsync: no active team. " +
                "Call IActiveTeamAccessor.SetActiveAsync before requesting a session signer.");

        var root = await _rootSeed.GetRootSeedAsync(ct).ConfigureAwait(false);
        var subkey = _subkeyDerivation.DeriveSubkey(root.Span, active.TeamId.ToString());
        var seed = subkey.AsSpan(0, 32);
        var (publicKey, _) = _ed25519.GenerateFromSeed(seed);
        return new DefaultBoundEd25519Signer(_ed25519, seed, publicKey);
    }
}
```

### Modified file

**`packages/kernel-security/DependencyInjection/ServiceCollectionExtensions.cs`**

Add to `AddKernelSecurity()` (or a new extension `AddSessionSignerAccessor()`):

```csharp
services.AddSingleton<ISessionSignerAccessor, DefaultSessionSignerAccessor>();
```

`IActiveTeamAccessor` is from `Sunfish.Kernel.Runtime` — verify it's already registered by the host (Anchor wires it in `MauiProgram.cs`). If not present in the host's DI, the `DefaultSessionSignerAccessor` constructor will throw at resolution time — which is the correct behavior (missing dep = clear error).

### Tests (4–5 tests in `kernel-security/tests/SessionSignerAccessorTests.cs`)

1. `GetCurrentAsync_WhenNoActiveTeam_ThrowsInvalidOperation` — null `_activeTeam.Active` → throws
2. `GetCurrentAsync_ReturnsSignerWithCorrectPublicKey` — signer's `PublicKey` matches `ITeamSubkeyDerivation`-derived public key
3. `GetCurrentAsync_SignedBytesVerifyWithPublicKey` — sign a known message; verify with `IEd25519Signer.Verify`
4. `GetCurrentAsync_DifferentTeams_ReturnDifferentPublicKeys` — switching teams changes the public key
5. `BoundSigner_SignAsync_IsDeterministic` — same data + same key → same signature (Ed25519 is deterministic)

---

## Acceptance criteria

- [ ] `ISessionSignerAccessor` + `IBoundEd25519Signer` in `kernel-security/Session/`
- [ ] `DefaultSessionSignerAccessor` resolves the team's identity key via the `IRootSeedProvider` → `ITeamSubkeyDerivation` chain
- [ ] `DefaultBoundEd25519Signer` never exposes the raw private key bytes
- [ ] DI registered as `Singleton` in `ServiceCollectionExtensions.AddKernelSecurity()`
- [ ] 4–5 unit tests pass; no regressions in existing kernel-security suite

---

## Halt conditions

- `IActiveTeamAccessor` is not in the DI graph when the accessor is resolved → stop; check if `kernel-runtime` DI extensions need to be called before `kernel-security` extensions in the host
- The `ITeamSubkeyDerivation.DeriveSubkey` signature changes (breaking) → halt; check kernel-security version

---

## Consuming workstreams

After W#65 lands, W#63 Phase 2 `ApproveRecoveryPage.razor` can be completed: inject `ISessionSignerAccessor`, call `GetCurrentAsync()`, use the returned `IBoundEd25519Signer.SignAsync` to produce the trustee attestation bytes for `IRecoveryCoordinator.SubmitTrusteeApprovalAsync`.
