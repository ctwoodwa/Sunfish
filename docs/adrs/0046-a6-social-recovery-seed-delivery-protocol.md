---
id: 46
title: "Social Recovery Seed-Delivery Protocol (Phase 2 key-transport for #48a)"
status: Accepted
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
**Date:** 2026-05-16 (council review amendments applied 2026-05-16)
**Amends:** [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — Key-loss recovery scheme for Business MVP Phase 1 (Accepted 2026-04-26)
**Driven by:** G7 conformance baseline scan 2026-05-16 — G6-A gap: `AnchorRecoveryCompletionHandler.HandleAsync` stubs the SQLCipher rekey because `RecoveryCompleted` carries no key material; Phase 1 sub-pattern #48a implements identity proof only, not seed delivery. See W#67 (`icm/_state/workstreams/W67-g6a-social-recovery-seed-delivery-protocol.md`).

---

## Context

Phase 1 ADR 0046 implemented three components of the social recovery stack:
- **#48a identity proof** — trustees attest a device's identity via signed `TrusteeAttestation`
- **#48e grace period** — 7-day dispute window before recovery finalizes
- **#48f audit trail** — `RecoveryEvent` log persisted to per-tenant store

Missing: after the grace period expires and `RecoveryCompleted` fires, the recovering device has no way to obtain its root seed. `IEncryptedStore.RotateKeyAsync(newKey)` exists and is implemented by `SqlCipherEncryptedStore`, but `AnchorRecoveryCompletionHandler` has no `newKey` bytes to supply. The `RecoveryRequest` type carries `EphemeralPublicKey` (Ed25519, for signing) as the design hook for key transport, but the Phase 1 `TrusteeAttestation` includes no encrypted seed payload — trustees sign only a hash of the request.

**SQLCipher key derivation (verified 2026-05-16):** The production SQLCipher key is NOT Argon2id-derived from a user password. `ISqlCipherKeyDerivation` in `packages/kernel-security/Keys/` specifies `HKDF-Expand(prk = root_seed, info = "sunfish:sqlcipher:v1:" + teamId, L = 32)` — fully deterministic from the root seed. `Argon2idKeyDerivation` in `foundation-localfirst` exists for other use cases (e.g. paper-key passphrase hardening) but is NOT in the primary SQLCipher key path. Delivering the root seed to the recovering device therefore enables both identity restoration AND SQLCipher re-keying.

## Decision drivers

1. **Cryptographic correctness** — recovering device must receive the root seed via an authenticated, confidential channel to re-derive its team identity keys and SQLCipher key.
2. **Minimal protocol churn** — new types must be additive amendments to `RecoveryRequest` and `TrusteeAttestation`; existing signature scheme must not be invalidated.
3. **Use existing primitives** — `IX25519KeyAgreement` (NSec-backed; X25519 + HKDF-SHA256 + ChaCha20-Poly1305) already exists in `kernel-security`; no new cryptographic dependencies beyond a new `IX25519SubkeyDerivation` interface (A6.4).
4. **Full-copy model for Phase 2 simplicity** — Shamir Secret Sharing is the correct long-term design but adds ~300 lines of crypto. Phase 2 uses full-copy-per-trustee with explicit threat-model documentation. Shamir is Phase 3 of A6.

## Decisions

### A6.1 — Per-trustee full seed copy; threat model expansions vs. Phase 1

Each designated trustee holds a complete encrypted copy of the owner's 32-byte root seed. When attesting a recovery request, the trustee decrypts their copy and re-encrypts it addressed to the recovering device's X25519 ephemeral public key.

**Threat model expansions vs. Phase 1 #48a (council amendment A6-A4):**
- **Per-install, not per-team.** The seed delivered is the 32-byte install root seed, from which ALL team subkeys are derived. A single trustee compromise for ANY team where they hold a seed copy exposes the root seed and thereby every team on that install. In Phase 1, trustees could only attest/authorize a device; they could not derive the root seed. Phase 2 materially expands trustee trust: they become capable of impersonating the owner indefinitely if their keystore is compromised.
- **No revocation.** Once a trustee's encrypted seed copy is gossiped to their node, it is retained locally until their node is wiped. De-designating a trustee does NOT revoke their copy. Owners who de-designate a trustee for security reasons should assume the trustee retains a usable seed copy until they rotate their root seed (which is not a Phase 2 primitive — see Phase 3).
- **Grace period is one-directional.** The 7-day grace period (#48e) mitigates "attacker initiates fake recovery" by giving the original device time to dispute. It does NOT mitigate "compromised trustee passively decrypts their seed copy offline and stores the root seed for later use." There is no observable signal for this attack path.
- **Shamir upgrade (Phase 3)** is the structural fix: k-of-n threshold cryptography means no single trustee can reconstruct the root seed alone. Phase 2 explicitly accepts the full-copy trade-offs above; they are documented here as known obligations, not gaps.

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

`CanonicalBytesForSigning` is updated to include `EphemeralDHPublicKey` AFTER `EphemeralPublicKey`:
```
"sunfish-recovery-request-v1\n" || NodeId || EphEd25519Pub || EphX25519Pub || RequestedAt
```

Breaking change — existing signed requests are invalid. The repo scan (2026-05-16) confirmed no callers of `RecoveryRequest.Create()` exist outside `packages/foundation-recovery/tests/`; no live devices use this flow. Migration is not needed.

`RecoveryRequest.EphemeralDHPublicKeyLength = 32`.

The recovering device generates both keypairs (`EphemeralPublicKey` Ed25519 + `EphemeralDHPublicKey` X25519) at `RecoveryRequest.Create()` time. The corresponding `EphemeralDHPrivateKey` is held securely on the recovering device (see OQ-A6.1 resolution, A6.11) and used at completion to decrypt attestation seed envelopes.

### A6.3 — `TrusteeAttestation` adds `EncryptedSeedEnvelope`

`TrusteeAttestation` gains three new fields (council amendment A6-A2):
```csharp
public sealed record TrusteeAttestation(
    string TrusteeNodeId,
    byte[] TrusteePublicKey,               // Ed25519, 32 bytes — signature verification (unchanged)
    byte[] TrusteeDHPublicKey,             // X25519, 32 bytes — for OpenBox (NEW)
    byte[] RecoveryRequestHash,
    DateTimeOffset AttestedAt,
    byte[] EncryptedSeedEnvelopeCiphertext, // ChaCha20-Poly1305 ciphertext (seed 32b + 16b auth tag = 48b) (NEW)
    byte[] EncryptedSeedEnvelopeNonce,      // 24-byte nonce (NEW)
    byte[] Signature)
```

`CanonicalBytesForSigning` is updated to include ALL three new fields (council amendment A6-A2: nonce and DH public key must be in signed payload so the recovering device does not need a subtle AEAD-binding argument):
```
"sunfish-trustee-attestation-v1\n" || TrusteeNodeId || RequestHash || AttestedAt || TrusteeDHPublicKey || SeedEnvelopeCiphertext || SeedEnvelopeNonce
```

Breaking change — existing attestations are invalid. Same verification as A6.2: no callers outside tests; safe to break.

`TrusteeAttestation.SeedEnvelopeCiphertextLength = 48` (32-byte seed + 16-byte auth tag).
`TrusteeAttestation.SeedEnvelopeNonceLength = 24`.
`TrusteeAttestation.TrusteeDHPublicKeyLength = 32`.

### A6.4 — Trustee X25519 DH key derivation path (council amendment A6-A1)

**`ITeamSubkeyDerivation.DeriveSubkey(root, teamId + "-dh")` is NOT the correct derivation.** String-suffix manipulation is not domain separation — a team whose `teamId` ends in `-dh` would collide with another team's DH-derived key. The info prefix must change, not the suffix.

**Correct design:** introduce a dedicated `IX25519SubkeyDerivation` interface in `packages/kernel-security/Keys/` with its own HKDF info prefix:

```
IRootSeedProvider.GetRootSeedAsync()                               → 32-byte root seed
IX25519SubkeyDerivation.DeriveX25519PrivateKey(root, teamId)      → 32 bytes
                         HKDF-Expand(root, "sunfish-x25519-team-v1:" + teamId, 32)
→ X25519 private key (imported via IX25519KeyAgreement via NSec Key.Import; NSec applies RFC 7748 clamping internally)
→ X25519 public key = Curve25519 scalar multiplication of private key over base point
```

Info prefix `"sunfish-x25519-team-v1:"` is distinct from:
- Ed25519 signing path: uses `ITeamSubkeyDerivation` with prefix `"sunfish-team-subkey-v1:"`
- SQLCipher key path: uses `ISqlCipherKeyDerivation` with prefix `"sunfish:sqlcipher:v1:"`

**RFC 7748 clamping note:** NSec's `Key.Import(KeyAgreementAlgorithm.X25519, bytes, KeyBlobFormat.RawPrivateKey)` applies the mandatory bit clamping (low 3 bits cleared; bit 254 set; bit 255 cleared) internally. Callers do NOT apply clamping manually; passing raw bytes through this import path is correct.

`TrusteeDHPublicKey` in the attestation is the 32-byte X25519 public key derived from this path.

### A6.5 — Trustee setup: seed copy distribution

During `TrusteeSetupPage` trustee designation, the owner's device:
1. Retrieves the root seed via `IRootSeedProvider.GetRootSeedAsync()`
2. Obtains each trustee's X25519 DH public key from their identity bundle (A6.6); **trustees must be online for setup** — the owner cannot encrypt without the trustee's DH public key
3. For each trustee: generates an owner ephemeral X25519 keypair; calls `IX25519KeyAgreement.Box(rootSeed, trusteeX25519Pub, ownerEphPriv)` → `(Ciphertext, Nonce)`
4. Stores `(TrusteeNodeId, OwnerEphX25519Pub, Ciphertext, Nonce)` in `RecoveryCoordinatorState.TrusteeEncryptedSeeds`
5. Syncs to the trustee's node via gossip

The trustee stores their encrypted copy locally. The plaintext root seed is held in memory only during the setup ceremony — once `Box()` completes, the plaintext is cleared.

**Trustee-online requirement for setup** is an acceptable Phase 2 UX constraint: trustee setup is a deliberate one-time ceremony (analogous to the QR-code pairing flow). Future automation (e.g. deferred distribution via gossip when the trustee next connects) is Phase 3.

### A6.6 — Identity bundle extension

The trustee's identity bundle (exchanged during QR-code pairing) gains `DHPublicKey: byte[]` (32-byte X25519 public key derived per A6.4). This allows the owner to obtain the trustee's DH public key without a separate round-trip.

### A6.7 — `RecoveryCoordinatorState` and `IRecoveryCoordinator` changes

`RecoveryCoordinatorState` adds:
- `TrusteeEncryptedSeeds: ImmutableDictionary<string, TrusteeEncryptedSeed>` — set during trustee setup (A6.5)

New record:
```csharp
public sealed record TrusteeEncryptedSeed(
    string TrusteeNodeId,
    byte[] OwnerEphX25519PublicKey,   // so trustee can decrypt with their X25519 private key
    byte[] Ciphertext,               // 48 bytes (32 seed + 16 auth tag)
    byte[] Nonce);                   // 24 bytes
```

`IRecoveryCoordinator` gains:
- `Task SetupTrusteeAsync(string trusteeNodeId, TrusteeEncryptedSeed encryptedSeed, CancellationToken ct)` — called during `TrusteeSetupPage`

`EvaluateGracePeriodAsync` return type changes: `RecoveryEvent?` → `RecoveryCompletionResult?`:
```csharp
public sealed record RecoveryCompletionResult(
    RecoveryEvent Event,
    IReadOnlyList<TrusteeAttestation> Attestations); // includes EncryptedSeedEnvelopes
```

### A6.8 — `IRootSeedRestorer` — write-back interface (council amendment A6-A3)

`IRootSeedProvider` is read-only (`GetRootSeedAsync` only). A6.8 step 5 requires writing the recovered root seed back to the keystore. This is a **high-risk, single-use write path** that must be isolated from the normal read surface.

New interface in `packages/kernel-security/Keys/`:
```csharp
/// <summary>
/// Single-use interface for restoring a root seed to the keystore during social recovery.
/// Only <see cref="AnchorRecoveryCompletionHandler"/> (or equivalent per-accelerator handler) should inject this.
/// </summary>
public interface IRootSeedRestorer
{
    /// <summary>
    /// Overwrites the existing root seed slot with <paramref name="recoveredSeed"/>.
    /// Invalidates the <see cref="IRootSeedProvider"/> cache so subsequent calls reflect the new seed.
    /// Emits a <see cref="AuditEventType.RecoveryRekey"/> record via <see cref="IAuditTrail"/>.
    /// </summary>
    Task RestoreRootSeedAsync(ReadOnlyMemory<byte> recoveredSeed, CancellationToken ct);
}
```

`KeystoreRootSeedProvider` implements both `IRootSeedProvider` (existing read path) and `IRootSeedRestorer` (new write path). `RestoreRootSeedAsync` overwrites the keystore slot and resets the internal `Lazy<>` cache so the next `GetRootSeedAsync` call returns the new seed.

**Lifecycle after seed restore (council amendment A6-A3 §A6.10):**
1. `IRootSeedRestorer.RestoreRootSeedAsync(recovered)` — writes seed, resets cache
2. All consumers of derived keys (`ITeamSubkeyDerivation`, `ISqlCipherKeyDerivation`, `IX25519SubkeyDerivation`) that cache derived material in memory must be restarted or invalidated — on a fresh recovery device (Scenario A, first run) no caches exist; on a same-device re-recovery (Scenario B) the app restart that follows SQLCipher re-keying serves as the cache reset
3. Trigger sync subsystem to begin receiving gossip

**DI scoping:** `IRootSeedRestorer` is registered as a Scoped or Singleton implementation (same as `KeystoreRootSeedProvider`) but injected only into `AnchorRecoveryCompletionHandler`. No other DI consumer should reference it.

### A6.9 — Completion handler: seed reconstruction and SQLCipher re-keying

`AnchorRecoveryCompletionHandler.HandleAsync` is updated to:

1. Retrieve `TrusteeAttestation` records from `RecoveryCompletionResult.Attestations`
2. Retrieve the `EphemeralDHPrivateKey` from secure platform storage (see OQ-A6.1 resolution, A6.11)
3. For each attestation: call `IX25519KeyAgreement.OpenBox(Ciphertext, Nonce, TrusteeDHPublicKey, ephX25519Priv)` — returns `null` on AEAD failure; log audit event and skip that attestation (do NOT abort — the set of successful decryptions drives the next step)
4. Collect all non-null results. **Minimum quorum: 1 successful decryption is sufficient to proceed** (rationale: the 3-of-5 trustee quorum already established identity trust; a single valid seed delivery is adequate for deterministic key reconstruction; divergence detection below provides the compromised-trustee safety net)
5. Compare all successful decryptions: if any decoded seeds diverge, log `AuditEventType.RecoveryRekey` with `ReKeySucceeded: false` and the SHA-256 hashes of each distinct decoded value (do not log raw seeds); do NOT proceed — owner must investigate
6. All decoded seeds are identical: take the first as `recoveredSeed`
7. Call `IRootSeedRestorer.RestoreRootSeedAsync(recoveredSeed, ct)` — writes seed to keystore, invalidates cache
8. Derive the team's SQLCipher key: `ISqlCipherKeyDerivation.DeriveSqlCipherKey(recoveredSeed, teamId)` → 32 bytes
9. Call `IEncryptedStore.RotateKeyAsync(sqlCipherKey, ct)` — re-keys the encrypted store (both new-device Scenario A where the store is empty, and same-device Scenario B where it holds old data)
10. Emit `AuditEventType.RecoveryRekey` with `ReKeySucceeded: true`
11. Clear `EphemeralDHPrivateKey` from platform storage (SecureStorage delete)
12. Signal sync subsystem to begin gossip

**Scenario A (new device):** steps 8–9 produce a fresh empty SQLCipher store keyed correctly from the recovered seed. Data flows in via sync. No pre-existing data to rotate.
**Scenario B (same device, keystore corruption):** IF the SQLCipher store was previously keyed from the same root seed via `ISqlCipherKeyDerivation`, step 9 (`RotateKeyAsync`) re-keys it from old to new with the same derived key (no data change). IF the store was orphaned (old key from a different seed), step 9 produces a key mismatch — the store must be wiped and re-populated via sync. The handler should catch `NotSupportedException` from `RotateKeyAsync` (meaning the store implementation doesn't support rekey), log, and fallback to wipe-and-resync.

### A6.10 — `RecoveryRekey` audit event

New `AuditEventType.RecoveryRekey` constant with typed payload:
```csharp
public sealed record RecoveryRekeyPayload(
    string TargetNodeId,
    DateTimeOffset CompletedAt,
    int AttestationCount,
    int SuccessfulDecryptions,
    bool ReKeySucceeded,
    string? FailureReason);  // null on success; "divergent_seeds" or "min_decryption_failed" on failure
```

### A6.11 — Ephemeral DH private key storage (OQ-A6.1 resolved)

The `EphemeralDHPrivateKey` (32 bytes) must survive the grace period (7 days per ADR 0046 Phase 1; the earlier draft's "30 days" was a typo). It is stored using platform-managed encryption at rest:
- **MAUI Anchor:** `Microsoft.Maui.Storage.SecureStorage.SetAsync("recovery:dh-priv", base64Key)` — backed by DPAPI (Windows), Keychain (macOS/iOS), Android Keystore (Android); all are encrypted at rest
- **Requirement:** storage must be encrypted at rest under a platform-managed or user-credential-derived key

**Device-wipe between request and completion:** the ephemeral private key is lost; the existing attestations (bound to the original `RecoveryRequestHash`) cannot be decrypted. The user must initiate a fresh recovery request (generating a new `EphemeralPublicKey` + `EphemeralDHPublicKey` pair). This is correct behaviour — it forces trustees to re-attest the new device identity — and must be documented in the recovery UX.

**Compromise window:** while the ephemeral key is in platform storage, an attacker with OS-level access to the recovering device can extract it and decrypt any submitted attestation envelopes to recover the root seed. This is mitigated by the OS platform protections (biometrics / PIN required for SecureStorage extraction on most platforms) and the existing physical-device threat model.

---

## Alternatives rejected

**Shamir Secret Sharing (threshold k-of-n)**: Stronger — requires k trustees to collude to expose seed. Deferred to Phase 3 of A6 due to implementation complexity (~300 lines new crypto + test coverage).

**Ed25519→X25519 key conversion**: Mathematically valid. Rejected: .NET 11 `System.Security.Cryptography` does not expose this conversion; NSec supports it but the API is non-obvious; introducing `IX25519SubkeyDerivation` is cleaner and auditable.

**Coordinator-held encrypted seed**: Violates local-first P7; contradicts ADR 0046's design.

**`ITeamSubkeyDerivation.DeriveSubkey(root, teamId + "-dh")`**: Rejected (council amendment A6-A1) — string-suffix manipulation is not domain separation; team IDs ending in `-dh` would collide.

---

## Impacts on existing types

| Type | Change | Backward compat |
|---|---|---|
| `RecoveryRequest` | +`EphemeralDHPublicKey`; updated `CanonicalBytesForSigning` | Breaking. No live devices (repo scan 2026-05-16). |
| `TrusteeAttestation` | +`TrusteeDHPublicKey`, +`EncryptedSeedEnvelopeCiphertext`, +`EncryptedSeedEnvelopeNonce`; updated canonical bytes (all three fields included) | Breaking. No live devices. |
| `RecoveryCoordinatorState` | +`TrusteeEncryptedSeeds` | Additive |
| `IRecoveryCoordinator` | +`SetupTrusteeAsync`; `EvaluateGracePeriodAsync` → `RecoveryCompletionResult?` | Breaking on interface; api-change pipeline |
| `IRootSeedProvider` | No change | — |
| `IRootSeedRestorer` | NEW interface + `KeystoreRootSeedProvider` implementation | Additive |
| `IX25519SubkeyDerivation` | NEW interface + `HkdfX25519SubkeyDerivation` implementation | Additive |
| `AnchorRecoveryCompletionHandler` | Rewrite stub to real rekey path (A6.9) | Internal |
| `TrusteeSetupPage.razor` | Add seed distribution step | Internal |

---

## Open questions (remaining after council amendments)

**OQ-A6.2:** Online-trustee requirement for setup — future automation of deferred distribution (trustee receives their encrypted seed copy next time they connect, not during the ceremony). Phase 3 scope; document in TrusteeSetupPage UX as "Trustees must be reachable during setup."

---

## Implementation scope (updated)

| Phase | Work | Effort | Key files |
|---|---|---|---|
| A6.1 — New key interfaces | `IX25519SubkeyDerivation` + `HkdfX25519SubkeyDerivation` + `IRootSeedRestorer` + `KeystoreRootSeedProvider` extension | ~3h | `packages/kernel-security/Keys/` |
| A6.2 — Protocol types | `RecoveryRequest` + `TrusteeAttestation` field additions; updated `CanonicalBytesForSigning` for both | ~3-4h | `packages/foundation-recovery/` |
| A6.3 — Coordinator changes | `RecoveryCoordinatorState` + `IRecoveryCoordinator.SetupTrusteeAsync` + `EvaluateGracePeriodAsync` → `RecoveryCompletionResult?` | ~4-5h | `packages/foundation-recovery/` |
| A6.4 — Identity bundle + DH key inclusion | Identity bundle `DHPublicKey` field; QR-code pairing extension | ~2h | `accelerators/anchor/Services/Pairing/` |
| A6.5 — Completion handler | `AnchorRecoveryCompletionHandler`: decrypt + verify + restore seed + SQLCipher rekey + audit | ~2-3h | `accelerators/anchor/Services/` |
| A6.6 — Setup flow | `TrusteeSetupPage.razor` seed distribution step; `TrusteeSetupService` helper | ~3-4h | `accelerators/anchor/Components/Pages/Recovery/` |
| A6.7 — Audit event | `AuditEventType.RecoveryRekey` + `RecoveryRekeyPayload` | ~1h | `packages/kernel-audit/` |
| **Total** | **~18-22h / ~5-6 PRs** | — | api-change pipeline (`IRecoveryCoordinator` is public interface) |

**Pre-build gate:** Council review of this amendment (complete 2026-05-16 — see `icm/_state/workstreams/W67-g6a-social-recovery-seed-delivery-protocol.md`). CO acceptance flip required before COB implementation.

---

## Council review applied 2026-05-16

Security-engineering council review identified 3 BLOCKING + 4 MAJOR items. All resolved in this revised draft:

| Finding | Check | Resolution |
|---|---|---|
| A6.4 domain-separation unsound (`teamId + "-dh"` suffix) | BLOCKING 1 | Replaced with `IX25519SubkeyDerivation` + info prefix `"sunfish-x25519-team-v1:"` |
| `TrusteeDHPublicKey` + `EncryptedSeedEnvelopeNonce` not in canonical bytes | BLOCKING 2 | Both added to `TrusteeAttestation.CanonicalBytesForSigning` |
| `IRootSeedProvider` read-only; A6.8 step 5 not implementable | BLOCKING 3 | `IRootSeedRestorer` interface introduced; A6.9 revised |
| Full-copy blast radius understated (per-install, not per-team; no revocation) | MAJOR 1 | A6.1 expanded with explicit threat-model expansions section |
| OQ-A6.1 unresolved; 7d vs 30d grace period mismatch | MAJOR 2 | A6.11 resolves OQ-A6.1; grace period corrected to 7 days |
| `OpenBox` null-handling and minimum decryption count undefined | MAJOR 3 | A6.9 steps 3–4 specify null-handling and 1-successful-decryption minimum |
| Per-install seed cross-team blast radius; trustee de-designation gap | MAJOR 4 | A6.1 threat model + A6.1 "no revocation" documented |
| OQ-A6.3: Argon2id claim incorrect — SQLCipher is HKDF root-seed-derived | Cross-cutting | A6.9 corrected: both Scenario A and B use `ISqlCipherKeyDerivation.DeriveSqlCipherKey(recoveredSeed, teamId)` |

---

*This amendment resolves the G6-A gap from the G7 conformance baseline scan (2026-05-16).*
