---
sort_order: 74
number: 65
slug: kernel-security-session-signer-accessor
title: "W#65 — kernel-security `ISessionSignerAccessor` + `IBoundEd25519Signer`"
status: "built"
status_cell: "`built` — PR #868 merged 2026-05-16; `ISessionSignerAccessor` + `IBoundEd25519Signer` + `DefaultSessionSignerAccessor` + `DefaultBoundEd25519Signer` in `kernel-security/Session/`; DI wired via `AddKernelSecurity()`"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/kernel-security-session-signer-accessor-stage06-handoff.md` + `packages/kernel-security/Crypto/IEd25519Signer.cs` + `packages/kernel-security/Keys/ITeamSubkeyDerivation.cs` + `packages/kernel-security/Keys/IRootSeedProvider.cs`"
---

## Notes

Answers COB question `coordination/inbox/cob-question-2026-05-16T04-42Z-kernel-security-session-signer-accessor.md` (archived 2026-05-16).

**Problem:** `ApproveRecoveryPage.razor` (W#63 Phase 2) needs to sign recovery attestations with the session's team identity Ed25519 key. `IEd25519Signer` (kernel-security) is a raw algorithm wrapper — callers must supply private key bytes on every call. No public DI contract provides a session-bound signer without exposing the raw key bytes.

**Solution:** `ISessionSignerAccessor` resolves the current team's identity Ed25519 key pair from the existing `IRootSeedProvider` → `ITeamSubkeyDerivation` derivation chain (same pipeline used by `RoleKeyManager` + `AttestationIssuer`) and returns an `IBoundEd25519Signer` that holds the key internally.

**Derivation chain:**
```
IRootSeedProvider.GetRootSeedAsync()          → 32-byte root seed
ITeamSubkeyDerivation.DeriveSubkey(root, teamId) → 64 bytes
                                    [0..32]   → Ed25519 seed (sign key)
IEd25519Signer.GenerateFromSeed(seed)         → (PublicKey, PrivateKey)
```

**Scope:** 1 PR, ~2-3h. New files in `packages/kernel-security/Session/`. DI: `AddSingleton<ISessionSignerAccessor, DefaultSessionSignerAccessor>()`. 4-5 unit tests.

**Unblocks:** W#63 Phase 2 `ApproveRecoveryPage` — can submit attestations once this lands.
