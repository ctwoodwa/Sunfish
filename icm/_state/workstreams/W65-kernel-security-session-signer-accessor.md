---
sort_order: 73
number: 65
slug: kernel-security-session-signer-accessor
title: "**Kernel-security `ISessionSignerAccessor` + `IBoundEd25519Signer`** (additive; `sunfish-feature-change` pipeline) — provides a key-bound signer over the active team's identity key without exposing raw private-key bytes; unblocks W#63 `ApproveRecoveryPage`"
status: "built"
status_cell: "`built` — Single PR per hand-off. `ISessionSignerAccessor` + `IBoundEd25519Signer` interfaces in `kernel-security/Session/`; `DefaultBoundEd25519Signer` (public) in kernel-security; `DefaultSessionSignerAccessor` in `kernel-runtime/Session/` (DI implementation lives in kernel-runtime, not kernel-security, to avoid an upward dependency on `IActiveTeamAccessor`). 5/5 contract tests pass."
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/kernel-security-session-signer-accessor-stage06-handoff.md` + cob-question 2026-05-16T04-42Z (W#63 ApproveRecoveryPage signer-retrieval gap)"
---

## Notes

**Answers the W#63 cob-question** on the missing key-storage API. Provides:

- `ISessionSignerAccessor.GetCurrentAsync()` — returns an `IBoundEd25519Signer` for the active team's identity key
- `IBoundEd25519Signer` — exposes `PublicKey` (32 bytes) + `SignAsync(data)` returning a 64-byte Ed25519 signature. **Private key never exposed.**

**Derivation chain** (verified against existing kernel-security primitives):

```
IRootSeedProvider.GetRootSeedAsync()         → 32-byte install root seed
ITeamSubkeyDerivation.DeriveTeamKeypair(root, teamId)
                                              → (PublicKey, PrivateKey) Ed25519 pair
DefaultBoundEd25519Signer(signer, privKey, pubKey)
                                              → IBoundEd25519Signer
```

**Layering correction (hand-off amendment).** The hand-off proposed putting `DefaultSessionSignerAccessor` in kernel-security, but that would have required kernel-security to depend on kernel-runtime (for `IActiveTeamAccessor`) — kernel-runtime → kernel-security today, not the reverse. The PR keeps the interfaces in kernel-security (so consumers like the Anchor pages depend only on kernel-security) and moves `DefaultSessionSignerAccessor` to `kernel-runtime/Session/`. DI registration lives in `AddSunfishKernelRuntime`. `DefaultBoundEd25519Signer` is upgraded from `internal sealed` to `public sealed` so cross-assembly construction works.

**Tests:** 5 unit tests in `kernel-runtime/tests/SessionSignerAccessorTests.cs`:

1. `GetCurrentAsync_WhenNoActiveTeam_ThrowsInvalidOperation`
2. `GetCurrentAsync_ReturnsSignerWithDerivedPublicKey` — matches the public key from a direct `TeamSubkeyDerivation.DeriveTeamKeypair` call
3. `GetCurrentAsync_SignedBytes_VerifyWithPublicKey` — sign-then-verify round-trip via `Ed25519Signer.Verify`
4. `GetCurrentAsync_DifferentTeams_ReturnDifferentPublicKeys` — per-team isolation
5. `BoundSigner_SignAsync_IsDeterministic` — Ed25519 determinism (same data + key → identical signature)

Hand-rolled fakes (`FakeRootSeedProvider`, `FakeActiveTeamAccessor`) since `kernel-runtime/tests/tests.csproj` has no mocking framework.

**Pre-existing macOS failures unrelated to W#65:** 5 `DefaultTeamServiceRegistrarSyncTests` fail locally on macOS with `System.ArgumentOutOfRangeException` on the Unix-domain-socket path (>104 chars due to `$TMPDIR` length). These pre-date the W#65 change and pass on CI Linux/Windows.

**Unblocks:** W#63 `ApproveRecoveryPage.razor` upgrade — can now inject `ISessionSignerAccessor`, call `GetCurrentAsync()`, and use the returned `IBoundEd25519Signer.SignAsync` to produce trustee attestation signatures.
