# W#67 Stage 06 Hand-off — G6-A: Social Recovery Seed-Delivery Protocol

**ADR:** `docs/adrs/0046-a6-social-recovery-seed-delivery-protocol.md` (Accepted 2026-05-16)
**Pipeline:** api-change (`IRecoveryCoordinator` is a public interface)
**Effort:** ~18-22h / 5-6 PRs
**Unblocks:** Full G6 closure — `AnchorRecoveryCompletionHandler.HandleAsync` real rekey path

---

## Pre-build checklist

- [ ] `active-workstreams.md` row shows `ready-to-build` ✓
- [ ] ADR 0046-A6 `status: Accepted` ✓
- [ ] No in-flight PRs touching `foundation-recovery` or `kernel-security/Keys/` (`gh pr list --state open`)
- [ ] `git log --all --oneline -10` — confirm no parallel-session work since this hand-off

---

## Context

Phase 1 social recovery (ADR 0046) proves device identity via trustee attestations but does not deliver the root seed to the recovering device. After the grace period, `AnchorRecoveryCompletionHandler.HandleAsync` stubs the SQLCipher rekey:

```csharp
// TODO (post-W#63): IEncryptedStore.RotateKeyAsync(...)
//                   IAuditTrail.AppendAsync(new AuditRecord(...))
//                   ISyncDaemon.AnnounceIdentityRotation(...)
```

`IEncryptedStore.RotateKeyAsync` EXISTS at `packages/foundation-localfirst/Encryption/IEncryptedStore.cs:73`. The gap is protocol: `RecoveryCompleted` carries no key material. This workstream implements the seed-delivery protocol.

**SQLCipher key derivation (critical):** The production SQLCipher key is `HKDF-Expand(root_seed, "sunfish:sqlcipher:v1:" + teamId, 32)` via `ISqlCipherKeyDerivation` — NOT Argon2id. Recovering the root seed enables full key reconstruction.

---

## Implementation order (6 PRs)

### PR 1 — New key interfaces in `kernel-security` (~3h)

**Files to create:**

`packages/kernel-security/Keys/IX25519SubkeyDerivation.cs`
```csharp
namespace Sunfish.Kernel.Security.Keys;

public interface IX25519SubkeyDerivation
{
    /// <summary>
    /// Derives a 32-byte X25519 private key for <paramref name="teamId"/> from <paramref name="rootSeed"/>
    /// using HKDF-Expand with info prefix "sunfish-x25519-team-v1:".
    /// NSec applies RFC 7748 clamping internally on Key.Import — callers do NOT clamp manually.
    /// </summary>
    byte[] DeriveX25519PrivateKey(ReadOnlyMemory<byte> rootSeed, string teamId);

    /// <summary>
    /// Derives the corresponding X25519 public key (Curve25519 scalar mult over base point).
    /// </summary>
    byte[] DeriveX25519PublicKey(ReadOnlyMemory<byte> rootSeed, string teamId);
}
```

`packages/kernel-security/Keys/HkdfX25519SubkeyDerivation.cs`
- HKDF-Expand: `info = Encoding.UTF8.GetBytes("sunfish-x25519-team-v1:" + teamId)`
- `Key.Import(KeyAgreementAlgorithm.X25519, derivedBytes, KeyBlobFormat.RawPrivateKey)` — NSec handles RFC 7748 clamping
- Export public key: `key.Export(KeyBlobFormat.RawPublicKey)`

`packages/kernel-security/Keys/IRootSeedRestorer.cs`
```csharp
namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// Single-use write path for restoring a root seed during social recovery.
/// Only <see cref="AnchorRecoveryCompletionHandler"/> should inject this.
/// </summary>
public interface IRootSeedRestorer
{
    Task RestoreRootSeedAsync(ReadOnlyMemory<byte> recoveredSeed, CancellationToken ct);
}
```

**Files to modify:**

`packages/kernel-security/Keys/KeystoreRootSeedProvider.cs`
- Implement `IRootSeedRestorer` alongside existing `IRootSeedProvider`
- `RestoreRootSeedAsync`: write to keystore slot, reset internal `Lazy<>` cache so next `GetRootSeedAsync` returns the new seed

`packages/kernel-security/KernelSecurityServiceCollectionExtensions.cs`
- `AddSingleton<IX25519SubkeyDerivation, HkdfX25519SubkeyDerivation>()`
- `AddSingleton<IRootSeedRestorer>(sp => sp.GetRequiredService<KeystoreRootSeedProvider>())`

**Tests:** `packages/kernel-security/tests/` — 5-6 unit tests:
- `HkdfX25519SubkeyDerivation` produces distinct keys for different team IDs
- Same root + team → same private key (deterministic)
- Domain separation: X25519 key ≠ Ed25519 key for same root + team (verify prefix `"sunfish-x25519-team-v1:"` ≠ `"sunfish-team-subkey-v1:"`)
- `IX25519KeyAgreement.Box` + `OpenBox` round-trip using derived keys (integration test via existing `IX25519KeyAgreement`)

---

### PR 2 — Protocol type changes in `foundation-recovery` (~3-4h)

**Files to modify:**

`packages/foundation-recovery/RecoveryRequest.cs`
- Add field: `byte[] EphemeralDHPublicKey` (X25519, 32 bytes) — positioned after `EphemeralPublicKey`
- Add constant: `public const int EphemeralDHPublicKeyLength = 32;`
- Update `CanonicalBytesForSigning`:
  ```
  "sunfish-recovery-request-v1\n" || NodeId || EphEd25519Pub || EphX25519Pub || RequestedAt
  ```
  (append `EphemeralDHPublicKey` bytes after `EphemeralPublicKey` bytes)
- Update `Create()` factory: generate both Ed25519 keypair AND X25519 keypair; return `EphemeralDHPublicKey` as out param or include in result record

`packages/foundation-recovery/TrusteeAttestation.cs`
- Add fields:
  ```csharp
  byte[] TrusteeDHPublicKey,              // X25519, 32 bytes
  byte[] EncryptedSeedEnvelopeCiphertext, // 48 bytes (32 seed + 16 auth tag)
  byte[] EncryptedSeedEnvelopeNonce,      // 24 bytes
  ```
- Add constants: `TrusteeDHPublicKeyLength = 32`, `SeedEnvelopeCiphertextLength = 48`, `SeedEnvelopeNonceLength = 24`
- Update `CanonicalBytesForSigning` to include ALL three new fields after `AttestedAt`:
  ```
  "sunfish-trustee-attestation-v1\n" || TrusteeNodeId || RequestHash || AttestedAt
    || TrusteeDHPublicKey || SeedEnvelopeCiphertext || SeedEnvelopeNonce
  ```
  Order matters — verify test vectors after updating.

**Breaking change note:** No live devices use this flow (repo scan confirmed callers only in `packages/foundation-recovery/tests/`). Update those tests to match new constructors.

**Tests:** Update existing tests; add round-trip canonical-bytes tests for both types.

---

### PR 3 — Coordinator changes in `foundation-recovery` (~4-5h)

**Files to modify:**

`packages/foundation-recovery/RecoveryCoordinatorState.cs`
- Add: `ImmutableDictionary<string, TrusteeEncryptedSeed> TrusteeEncryptedSeeds`

New file: `packages/foundation-recovery/TrusteeEncryptedSeed.cs`
```csharp
public sealed record TrusteeEncryptedSeed(
    string TrusteeNodeId,
    byte[] OwnerEphX25519PublicKey,  // 32 bytes — trustee needs this to OpenBox
    byte[] Ciphertext,               // 48 bytes
    byte[] Nonce);                   // 24 bytes
```

`packages/foundation-recovery/IRecoveryCoordinator.cs`
- Add: `Task SetupTrusteeAsync(string trusteeNodeId, TrusteeEncryptedSeed encryptedSeed, CancellationToken ct)`
- Change return type: `EvaluateGracePeriodAsync` → `Task<RecoveryCompletionResult?>`

New file: `packages/foundation-recovery/RecoveryCompletionResult.cs`
```csharp
public sealed record RecoveryCompletionResult(
    RecoveryEvent Event,
    IReadOnlyList<TrusteeAttestation> Attestations);
```

`packages/foundation-recovery/RecoveryCoordinator.cs`
- Implement `SetupTrusteeAsync`: persist `TrusteeEncryptedSeed` to `RecoveryCoordinatorState.TrusteeEncryptedSeeds`
- Update `EvaluateGracePeriodAsync`: after quorum + grace check, return `RecoveryCompletionResult` with the collected attestations (currently discarded after counting — retain them)

**Tests:** 3-4 tests covering `SetupTrusteeAsync` persistence and `EvaluateGracePeriodAsync` result includes attestations.

---

### PR 4 — Completion handler: real rekey path (~2-3h)

**File:** `accelerators/anchor/Services/AnchorRecoveryCompletionHandler.cs`

Inject: `IX25519KeyAgreement`, `IRootSeedRestorer`, `ISqlCipherKeyDerivation`, `IEncryptedStore`, `IAuditTrail`, `ISecureStorage` (MAUI), `ISyncDaemon`

Replace the TODO stub with:

```csharp
// 1. Retrieve ephemeral DH private key from SecureStorage
var ephPrivB64 = await SecureStorage.GetAsync("recovery:dh-priv");
if (ephPrivB64 is null) { /* log + return — key lost (device wipe scenario) */ return; }
var ephX25519Priv = Convert.FromBase64String(ephPrivB64);

// 2. Decrypt each attestation envelope
var decodedSeeds = new List<byte[]>();
foreach (var att in result.Attestations)
{
    var seed = _keyAgreement.OpenBox(
        att.EncryptedSeedEnvelopeCiphertext,
        att.EncryptedSeedEnvelopeNonce,
        att.TrusteeDHPublicKey,
        ephX25519Priv);
    if (seed is null) { _auditTrail.AppendAsync(...); continue; }
    decodedSeeds.Add(seed);
}

// 3. Minimum 1 successful decryption
if (decodedSeeds.Count == 0) { /* log failure audit event + return */ return; }

// 4. Divergence check
if (decodedSeeds.Select(s => Convert.ToBase64String(s)).Distinct().Count() > 1)
{
    // Log SHA-256 hashes of distinct seeds (NOT raw seeds) + return
    return;
}

var recoveredSeed = decodedSeeds[0].AsMemory();

// 5. Restore root seed
await _rootSeedRestorer.RestoreRootSeedAsync(recoveredSeed, ct);

// 6. Derive + rotate SQLCipher key
var sqlCipherKey = _sqlCipherKeyDerivation.DeriveSqlCipherKey(recoveredSeed, teamId);
await _encryptedStore.RotateKeyAsync(sqlCipherKey, ct);

// 7. Emit success audit event
await _auditTrail.AppendAsync(new AuditRecord(AuditEventType.RecoveryRekey,
    new RecoveryRekeyPayload(...ReKeySucceeded: true...)), ct);

// 8. Clear ephemeral key
SecureStorage.Remove("recovery:dh-priv");

// 9. Signal sync
await _syncDaemon.AnnounceIdentityRotation(ct);
```

**Also:** at `RecoveryRequest.Create()` call site in the recovery initiation flow, generate and store the ephemeral DH private key:
```csharp
await SecureStorage.SetAsync("recovery:dh-priv", Convert.ToBase64String(ephX25519Priv));
```

**Tests:** 3-4 integration tests covering success path, null `OpenBox` skip, divergent seeds abort, missing SecureStorage key graceful return.

---

### PR 5 — Trustee setup flow + identity bundle (~5-7h including MAJOR-2)

#### MAJOR-2 binding fix (council finding — `TrusteeDesignation.DHPublicKey`)

**`packages/foundation-recovery/TrusteeDesignation.cs`** — add X25519 DH key field:
```csharp
// Add positional field (32 bytes, X25519 public key):
public byte[] DHPublicKey { get; init; } = Array.Empty<byte>();
public const int DHPublicKeyLength = 32;
```

**`packages/foundation-recovery/IRecoveryCoordinator.cs`** — widen `DesignateTrusteeAsync`:
```diff
-Task<RecoveryEvent> DesignateTrusteeAsync(
-    string trusteeNodeId,
-    ReadOnlyMemory<byte> trusteePublicKey,
-    CancellationToken ct);
+Task<RecoveryEvent> DesignateTrusteeAsync(
+    string trusteeNodeId,
+    ReadOnlyMemory<byte> trusteePublicKey,
+    ReadOnlyMemory<byte> trusteeDHPublicKey,
+    CancellationToken ct);
```

**`packages/foundation-recovery/RecoveryCoordinator.cs`** — cross-check in `SubmitAttestationAsync`:
- After verifying attestation signature, retrieve `state.Trustees[nodeId].DHPublicKey`
- Use `CryptographicOperations.FixedTimeEquals(attestation.TrusteeDHPublicKey, stored.DHPublicKey)`
- If mismatch: drop attestation (emit no event, log at Warn — use the ≤8-char fingerprint pattern
  from MAJOR-2's sibling fix M-4, i.e., `Convert.ToHexString(SHA256.HashData(key))[..8]`)

**Tests (`packages/foundation-recovery.tests/RecoveryCoordinatorTests.cs` + `TrusteeRecordTests.cs`):**
- Update all `DesignateTrusteeAsync` call sites to pass `trusteeDHPublicKey` argument
- Add: `SubmitAttestation_DropsWhenTrusteeDHKeyMismatch` — ensure no event emitted when
  `attestation.TrusteeDHPublicKey` ≠ `state.Trustees[id].DHPublicKey`

**Note on callers:** `RecoveryAttestationSubmitter` (W#65 session signer) already passes
`signer.PublicKey.ToArray()` as trustee DH — no change needed there. Only the owner-side
`TrusteeSetupPage.razor` changes (see below).

---

#### Identity bundle + page changes

**Identity bundle** (`accelerators/anchor/Services/Pairing/` — locate identity bundle type):
- Add `DHPublicKey: byte[]` (32 bytes, from `IX25519SubkeyDerivation.DeriveX25519PublicKey`)
- Populate during bundle generation; include in QR-code payload

**`TrusteeSetupPage.razor`** (`accelerators/anchor/Components/Pages/Recovery/TrusteeSetupPage.razor`):
- **Designation step:** collect trustee's X25519 DH public key alongside Ed25519 + NodeId
  (trustee presents both via identity-bundle QR + paste flow). Pass to widened
  `DesignateTrusteeAsync(trusteeNodeId, trusteeEdPublicKey, trusteeDHPublicKey, ct)`.
- **After designation, add seed distribution step:**
  1. `IRootSeedProvider.GetRootSeedAsync()` → root seed
  2. For each trustee: `IX25519KeyAgreement.Box(rootSeed, trustee.DHPublicKey, ownerEphPriv)` → `(Ciphertext, Nonce)`
  3. `IRecoveryCoordinator.SetupTrusteeAsync(trusteeNodeId, new TrusteeEncryptedSeed(...))`
- UX note: trustees must be online. Add a clear "Trustees must be reachable during setup" warning.

**New service:** `TrusteeSetupService.cs` — helper extracting the encryption logic out of the page code-behind. Keeps the page thin.

**`ApproveRecoveryPage.razor`** (`accelerators/anchor/Components/Pages/Recovery/ApproveRecoveryPage.razor`):
- After session signing via `ISessionSignerAccessor`, add seed re-encryption step:
  1. Decrypt own stored seed via `IX25519KeyAgreement.OpenBox` (using trustee's X25519 private key from `IX25519SubkeyDerivation`)
  2. Re-encrypt to recovering device's `request.EphemeralDHPublicKey` via `Box` using trustee ephemeral keypair
  3. Include `TrusteeDHPublicKey`, `EncryptedSeedEnvelopeCiphertext`, `EncryptedSeedEnvelopeNonce` in `TrusteeAttestation`
  4. Pass updated attestation to `RecoveryAttestationSubmitter`

---

### PR 6 — Audit event + ledger flip (~1h)

**`packages/kernel-audit/`** (locate `AuditEventType` enum):
- Add `RecoveryRekey` constant

New file: `packages/kernel-audit/Payloads/RecoveryRekeyPayload.cs`
```csharp
public sealed record RecoveryRekeyPayload(
    string TargetNodeId,
    DateTimeOffset CompletedAt,
    int AttestationCount,
    int SuccessfulDecryptions,
    bool ReKeySucceeded,
    string? FailureReason);  // null on success; "divergent_seeds" or "min_decryption_failed"
```

**Ledger flip:** update `W67-g6a-social-recovery-seed-delivery-protocol.md` → `status: "built"`.

---

## Acceptance criteria

- [ ] `AnchorRecoveryCompletionHandler.HandleAsync` no longer contains any TODO comments; real rekey path executes
- [ ] `IX25519SubkeyDerivation` domain separation tests pass (distinct prefix from Ed25519 + SQLCipher paths)
- [ ] `RecoveryRequest.CanonicalBytesForSigning` includes `EphemeralDHPublicKey`
- [ ] `TrusteeAttestation.CanonicalBytesForSigning` includes `TrusteeDHPublicKey` + `SeedEnvelopeCiphertext` + `SeedEnvelopeNonce`
- [ ] `IRootSeedRestorer` is injected only into `AnchorRecoveryCompletionHandler` (grep DI registrations)
- [ ] `TrusteeDesignation.DHPublicKey` populated at designation time; `SubmitAttestationAsync` cross-checks via `FixedTimeEquals` (MAJOR-2 closed)
- [ ] `SubmitAttestation_DropsWhenTrusteeDHKeyMismatch` test passes
- [ ] Divergent-seed audit path logs SHA-256 hashes, NOT raw seed bytes
- [ ] Ephemeral DH private key is cleared from `SecureStorage` after successful completion
- [ ] G6-A gap CLOSED in G7 conformance baseline (flip `g7-conformance-baseline-2026-Q2.md` G6 from PARTIAL → CLOSED in PR 6)

---

## Files for reference (do not re-read unless modifying)

- `packages/kernel-security/Crypto/IX25519KeyAgreement.cs` — `Box` + `OpenBox` signatures
- `packages/kernel-security/Keys/ISqlCipherKeyDerivation.cs` — HKDF derivation, verified 2026-05-16
- `packages/kernel-security/Keys/IRootSeedProvider.cs` — read-only interface
- `packages/foundation-localfirst/Encryption/IEncryptedStore.cs:73` — `RotateKeyAsync` exists
- `packages/foundation-localfirst/Encryption/SqlCipherEncryptedStore.cs:209` — `PRAGMA rekey` implementation
- `accelerators/anchor/Services/AnchorRecoveryCompletionHandler.cs` — current stub
- `accelerators/anchor/Services/RecoveryAttestationSubmitter.cs` — W#66 pattern to follow
- `docs/adrs/0046-a6-social-recovery-seed-delivery-protocol.md` — full protocol spec
