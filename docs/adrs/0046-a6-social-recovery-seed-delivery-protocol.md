---
id: 46
title: "Social Recovery Seed-Delivery Protocol (Phase 2 key-transport for #48a)"
status: Proposed
date: 2026-05-16
tier: foundation
concern:
  - security
  - recovery
composes:
  - 49
extends: []
supersedes: []
superseded_by: null
amendments:
  - A2
  - A3
  - A4
  - A5
---
# ADR-0046-A6 — Social Recovery Seed-Delivery Protocol

**Status:** Proposed
**Date:** 2026-05-16
**Amends:** [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — Key-loss recovery scheme for Business MVP Phase 1 (Accepted 2026-04-26)
**Driven by:** G7 conformance baseline scan 2026-05-16 — G6-A gap: `AnchorRecoveryCompletionHandler.HandleAsync` stubs the SQLCipher rekey because `RecoveryCompleted` carries no key material; Phase 1 sub-pattern #48a implements identity proof only, not seed delivery. See W#67 (`icm/_state/workstreams/W67-g6a-social-recovery-seed-delivery-protocol.md`).

---

## Context

Phase 1 ADR 0046 implemented three components of the social recovery stack:
- **#48a identity proof** — trustees attest a device's identity via signed `TrusteeAttestation`
- **#48e grace period** — 7-day dispute window before recovery finalizes
- **#48f audit trail** — `RecoveryEvent` log persisted to per-tenant store

Missing: after the grace period expires and `RecoveryCompleted` fires, the recovering device has no way to obtain its root seed. `IEncryptedStore.RotateKeyAsync(newKey)` exists and is implemented by `SqlCipherEncryptedStore`, but `AnchorRecoveryCompletionHandler` has no `newKey` bytes to supply.

The `RecoveryRequest` type already carries `EphemeralPublicKey` (Ed25519, for signing the request) but the Phase 1 trustee attestation flow does not include an encrypted seed payload — trustees sign only a hash of the request.

## Decision drivers

1. **Cryptographic correctness** — the recovering device must receive the root seed via an authenticated, confidential channel so it can re-derive its SQLCipher key.
2. **Minimal protocol churn** — the new types must be additive amendments to `RecoveryRequest` and `TrusteeAttestation`; the existing signature scheme must not be invalidated.
3. **Use existing primitives** — `IX25519KeyAgreement` (NSec-backed; X25519 + HKDF-SHA256 + ChaCha20-Poly1305) already exists in `kernel-security`; no new cryptographic dependencies.
4. **Full-copy model for Phase 2 simplicity** — Shamir Secret Sharing (k-of-n threshold) is the correct long-term design but adds implementation complexity. Phase 2 of A6 uses full-copy-per-trustee (each trustee holds a complete encrypted copy of the root seed). Shamir upgrade is Phase 3.

## Decisions

### A6.1 — Recover via per-trustee full seed copy (Phase 2 of A6)

Each designated trustee holds a complete encrypted copy of the owner's 32-byte root seed. The copy is encrypted using a sealed-box constructed with the **owner's ephemeral X25519 key × the trustee's X25519 DH key**. Trustees store their copy in their local RecoveryCoordinatorState.

When attesting a recovery request, the trustee:
1. Decrypts their own seed copy using their X25519 DH private key
2. Re-encrypts the seed using a sealed-box addressed to the **recovering device's X25519 DH ephemeral key** (from `RecoveryRequest.EphemeralDHPublicKey` — A6.2)
3. Includes the sealed box in `TrusteeAttestation.EncryptedSeedEnvelope` (A6.3)

The recovering device decrypts using the corresponding ephemeral X25519 private key (held in memory only; never persisted).

**Threat model:** A single compromised trustee exposes the full root seed encrypted under the trustee's X25519 key. This is acceptable in Phase 2 (trustees are already trusted to hold the full seed in the existing model — they can attest and authorize any device). Shamir (k-of-n threshold cryptography) restricts seed exposure to k-of-n colluding trustees but adds ~300 lines of new crypto. Deferred to Phase 3 of A6. Trustee compromise risk is mitigated by the 7-day grace period (#48e): the owner can dispute on any device that still holds the original keystore.

### A6.2 — `RecoveryRequest` adds `EphemeralDHPublicKey`

`RecoveryRequest` gains one new field:
```csharp
public sealed record RecoveryRequest(
    string RequestingNodeId,
    byte[] EphemeralPublicKey,         // Ed25519, 32 bytes — for signing (unchanged)
    byte[] EphemeralDHPublicKey,       // X25519, 32 bytes — for seed delivery (NEW)
    DateTimeOffset RequestedAt,
    byte[] Signature)
```

`CanonicalBytesForSigning` is updated to include `EphemeralDHPublicKey` AFTER `EphemeralPublicKey` in the byte buffer (fully backwards-incompatible change — existing signed requests are invalid; Phase 1 had no live devices so no migration needed):
```
"sunfish-recovery-request-v1\n" || NodeId || EphEd25519Pub || EphX25519Pub || RequestedAt
```

The recovering device generates both keypairs at `RecoveryRequest.Create()` time. The `EphemeralDHPrivateKey` is held in memory on the recovering device for the duration of the grace period and used at completion to decrypt attestation seed envelopes. It must NOT be persisted.

`RecoveryRequest.EphemeralDHPublicKeyLength = 32`.

### A6.3 — `TrusteeAttestation` adds `EncryptedSeedEnvelope`

`TrusteeAttestation` gains two new fields:
```csharp
public sealed record TrusteeAttestation(
    string TrusteeNodeId,
    byte[] TrusteePublicKey,               // Ed25519, 32 bytes — for signature verification (unchanged)
    byte[] TrusteeDHPublicKey,             // X25519, 32 bytes — for OpenBox (NEW)
    byte[] RecoveryRequestHash,
    DateTimeOffset AttestedAt,
    byte[] EncryptedSeedEnvelopeCiphertext, // ChaCha20-Poly1305 ciphertext (seed 32b + 16b auth tag = 48b) (NEW)
    byte[] EncryptedSeedEnvelopeNonce,      // 24-byte nonce (NEW)
    byte[] Signature)
```

`CanonicalBytesForSigning` is updated to include `EncryptedSeedEnvelopeCiphertext` in the signed payload (to prevent tampering with the sealed seed after the trustee signs):
```
"sunfish-trustee-attestation-v1\n" || TrusteeNodeId || RequestHash || AttestedAt || SeedEnvelopeCiphertext
```

Nonce is NOT included in the signed payload (nonce is authenticated by the AEAD tag; including it would be redundant; the trustee signs over the ciphertext+auth-tag which implicitly authenticates the nonce).

`TrusteeAttestation.SeedEnvelopeCiphertextLength = 48` (32-byte seed + 16-byte auth tag).
`TrusteeAttestation.SeedEnvelopeNonceLength = 24`.

### A6.4 — Trustee X25519 DH key derivation path

The trustee's X25519 DH private key is derived from the team root seed via a separate path from the Ed25519 identity key:

```
IRootSeedProvider.GetRootSeedAsync()          → 32-byte root seed
ITeamSubkeyDerivation.DeriveSubkey(root, teamId + "-dh") → 64 bytes
                                  [0..32]      → X25519 private key (seed bytes)
```

The X25519 private key is then passed to `IX25519KeyAgreement.GenerateKeyPair()` (or equivalently: the 32-byte seed IS the X25519 private key; public key = scalar multiplication on Curve25519 base point, which NSec handles automatically from the private key).

`TrusteeDHPublicKey` in the attestation is the 32-byte X25519 public key derived from this path.

**Domain separation:** the `-dh` suffix on the team ID ensures the X25519 key is derived independently from the Ed25519 identity key (`DeriveSubkey(root, teamId)` → [0..32] → Ed25519 seed). Same root seed, different derivation inputs, different keys.

### A6.5 — Trustee setup: seed copy distribution

During `TrusteeSetupPage` trustee designation, the owner's device:
1. Retrieves the root seed via `IRootSeedProvider.GetRootSeedAsync()`
2. For each designated trustee, obtains their X25519 DH public key via gossip/sync (the trustee's DH key is included in their identity bundle — A6.6)
3. Generates an owner ephemeral X25519 keypair per trustee
4. Calls `IX25519KeyAgreement.Box(rootSeed, trusteeX25519Pub, ownerEphPriv)` → `(Ciphertext, Nonce)`
5. Stores `(TrusteeNodeId, OwnerEphX25519Pub, Ciphertext, Nonce)` in `RecoveryCoordinatorState.TrusteeEncryptedSeeds`
6. Syncs the updated state to the trustee's node (via gossip)

The trustee receives the encrypted seed copy and stores it locally. On startup, the trustee's node can re-derive its X25519 DH private key and decrypt the copy to verify it received a valid 32-byte seed (integrity check only; the trustee's node should not hold the plaintext seed in long-term storage — see the `IBoundEd25519Signer` pattern for session-scoped key handling).

### A6.6 — Identity bundle extension

The trustee's identity bundle (the payload exchanged during QR-code pairing) gains `DHPublicKey: byte[]` (32-byte X25519 public key derived per A6.4). This allows the owner's device to obtain the trustee's DH public key without requiring the trustee to be online during seed distribution.

### A6.7 — `RecoveryCoordinatorState` and `IRecoveryCoordinator` changes

`RecoveryCoordinatorState` adds:
- `TrusteeEncryptedSeeds: ImmutableDictionary<string, TrusteeEncryptedSeed>` — maps trustee NodeId → their encrypted seed copy (set during trustee setup, A6.5)

New record:
```csharp
public sealed record TrusteeEncryptedSeed(
    string TrusteeNodeId,
    byte[] OwnerEphX25519PublicKey,   // so trustee can decrypt with their X25519 private key
    byte[] Ciphertext,               // 48 bytes (32 seed + 16 auth tag)
    byte[] Nonce);                   // 24 bytes
```

`IRecoveryCoordinator` gains:
- `Task SetupTrusteeAsync(string trusteeNodeId, TrusteeEncryptedSeed encryptedSeed, CancellationToken ct)` — called during `TrusteeSetupPage` trustee designation to store the encrypted seed copy

`EvaluateGracePeriodAsync` return value changes: instead of returning a bare `RecoveryEvent?`, it returns `RecoveryCompletionResult?` which includes:
```csharp
public sealed record RecoveryCompletionResult(
    RecoveryEvent Event,
    IReadOnlyList<TrusteeAttestation> Attestations); // attestations with EncryptedSeedEnvelopes
```

This allows `AnchorRecoveryCompletionHandler` to access the sealed seed envelopes from the attestations.

### A6.8 — Completion handler: seed reconstruction and rekey

`AnchorRecoveryCompletionHandler.HandleAsync` is updated to:
1. Collect the `TrusteeAttestation` records from `RecoveryCompletionResult.Attestations`
2. For each attestation: call `IX25519KeyAgreement.OpenBox(ciphertext, nonce, trusteeDHPub, ephX25519Priv_recovering)` using the ephemeral X25519 private key generated at `RecoveryRequest.Create()` time
3. Verify all successful decryptions return identical 32-byte seeds (if they diverge, a trustee submitted a bad seed — escalate via audit log, do not proceed)
4. Take the first successfully-decrypted seed as the recovered root seed
5. Derive the new SQLCipher key: `SqlCipherKeyDerivation.DeriveKey(recoveredSeed, teamId)` (using the same path as the original key)
6. Call `IEncryptedStore.RotateKeyAsync(newSqlCipherKey, ct)`
7. Emit a `RecoveryRekey` kernel-audit record (A6.9)
8. Clear the ephemeral X25519 private key from memory

The ephemeral X25519 private key must be held in the `IHostedService` or a scoped service that lives for the duration of the recovery session. It must not be persisted (ADR 0046's key-exposure-minimization principle).

### A6.9 — `RecoveryRekey` audit event

A new `AuditEventType.RecoveryRekey` constant is added with a typed payload:
```csharp
public sealed record RecoveryRekeyPayload(
    string TargetNodeId,
    DateTimeOffset CompletedAt,
    int AttestationCount,
    bool ReKeySucceeded);
```

---

## Alternatives rejected

**Shamir Secret Sharing (threshold k-of-n)**: Stronger threat model (requires k trustees to collude to expose seed) but adds ~300 lines of crypto + test complexity. Each trustee holds only a SHARE of the seed. Deferred to A6 Phase 3.

**Ed25519→X25519 key conversion**: Mathematically valid (same underlying curve; different forms). Avoids needing a separate X25519 identity key per trustee. Rejected because: (a) .NET 11 System.Security.Cryptography does not expose this conversion; (b) NSec supports it but it's a non-obvious API path; (c) adding a explicit `-dh` derived key is cleaner and auditable.

**Coordinator-held encrypted seed**: A coordinator (Bridge relay) holds the seed, releases it after quorum of "release" signatures from trustees. Rejected: violates local-first P7 (no privileged server); contradicts the Phase 1 design goal of trustee-only social recovery.

---

## Impacts on existing types

| Type | Change | Backward compat |
|---|---|---|
| `RecoveryRequest` | +`EphemeralDHPublicKey`; updated `CanonicalBytesForSigning` | Breaking — existing requests recompute their canonical bytes differently. Phase 1 had no live devices; acceptable. |
| `TrusteeAttestation` | +`TrusteeDHPublicKey`, +`EncryptedSeedEnvelopeCiphertext`, +`EncryptedSeedEnvelopeNonce`; updated canonical bytes | Breaking — existing attestations have no seed envelope. Phase 1 attestations are coordinator-state only; no serialized wire format shipped to users yet. |
| `RecoveryCoordinatorState` | +`TrusteeEncryptedSeeds` | Additive |
| `IRecoveryCoordinator` | +`SetupTrusteeAsync`; `EvaluateGracePeriodAsync` → returns `RecoveryCompletionResult?` | Breaking on the interface; api-change pipeline required |
| `AnchorRecoveryCompletionHandler` | Rewrite stub to real rekey path | Internal; no contract change |

---

## Open questions

**OQ-A6.1:** The `EphemeralDHPrivateKey` on the recovering device must survive the grace period (up to 30 days). Where is it stored? Options: (a) encrypted in `RecoveryCoordinatorState` under a PIN-derived key; (b) in the MAUI SecureStorage; (c) re-derived from a user-supplied PIN at completion time. **XO recommendation: MAUI SecureStorage** (existing Anchor pattern; used for the keystore root seed). This unblocks the Phase 2 implementation; Phase 3 can add hardware-backed key storage.

**OQ-A6.2:** At trustee setup time, if a trustee is offline, the owner cannot obtain their X25519 DH public key from gossip. Should the owner pre-compute the seed copy and store it, then deliver it when the trustee comes online? Or require trustees to be online for setup? **XO recommendation: require online trustee during setup ceremony** (matches the UX of QR-code pairing; not a significant practical constraint since trustee setup is a deliberate one-time act). Owner retains the unencrypted root seed in memory (via `IRootSeedProvider`) only for the duration of the setup ceremony.

**OQ-A6.3:** The `SqlCipherKeyDerivation.DeriveKey(recoveredSeed, teamId)` path must produce the SAME key as the original SQLCipher key. This works if the original key was also derived from the root seed via the same path. But if the original device used a directly-generated random SQLCipher key (not seed-derived), recovery cannot reconstruct it. Is the current SQLCipher key derivation seed-derived? **Action: verify that `SqlCipherKeyDerivation` in `foundation-localfirst` uses `IRootSeedProvider` + `ITeamSubkeyDerivation` and does not accept a user-supplied random key.** If it does accept a random key (e.g., from `AddKernelSecurity()` MAUI initialization), A6 must also define how the original random key is distributed to trustees or replaced with a deterministic derivation.

---

## Implementation scope

| Phase | Work | Effort | Files |
|---|---|---|---|
| A6.1 — Protocol types | `RecoveryRequest` + `TrusteeAttestation` field additions; updated `CanonicalBytesForSigning`; `TrusteeEncryptedSeed` new record | ~3-4h | `packages/foundation-recovery/{RecoveryRequest,TrusteeAttestation,TrusteeEncryptedSeed}.cs` |
| A6.2 — Coordinator changes | `RecoveryCoordinatorState` + `IRecoveryCoordinator.SetupTrusteeAsync` + `EvaluateGracePeriodAsync` → `RecoveryCompletionResult?` | ~4-5h | `packages/foundation-recovery/{RecoveryCoordinatorState,IRecoveryCoordinator,RecoveryCoordinator}.cs` |
| A6.3 — Identity bundle + DH key derivation | Trustee X25519 key derivation in `ITeamSubkeyDerivation`; identity bundle extension | ~2-3h | `packages/kernel-security/Keys/ITeamSubkeyDerivation.cs`; identity bundle type |
| A6.4 — Completion handler | `AnchorRecoveryCompletionHandler`: decrypt seed envelopes + verify + rekey + audit | ~2-3h | `accelerators/anchor/Services/AnchorRecoveryCompletionHandler.cs` |
| A6.5 — Setup flow | `TrusteeSetupPage.razor` seed distribution step; new `TrusteeSetupService` helper | ~3-4h | `accelerators/anchor/Components/Pages/Recovery/TrusteeSetupPage.razor`; new service |
| A6.6 — Audit event | `AuditEventType.RecoveryRekey` + `RecoveryRekeyPayload` | ~1h | `packages/kernel-audit/AuditEventType.cs` |
| **Total** | **~15-20h / ~4-5 PRs** | — | api-change pipeline (IRecoveryCoordinator is a public interface) |

**Pre-build requirement:** Resolve OQ-A6.3 before Phase A6.2. If `SqlCipherKeyDerivation` accepts a random key, that must be fixed first (or A6.4 must define a separate seed→SQLCipher key transport path that doesn't depend on the current derivation).

---

*This amendment resolves the G6-A gap from the G7 conformance baseline scan (2026-05-16).*
