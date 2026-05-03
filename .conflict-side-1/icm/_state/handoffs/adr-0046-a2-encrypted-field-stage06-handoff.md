# Workstream #32 — Foundation.Recovery field-encryption substrate (ADR 0046-A2) — Stage 06 hand-off

**Workstream:** #32 (`EncryptedField` + `IFieldDecryptor` field-level encryption substrate in `Sunfish.Foundation.Recovery`)
**Spec:** [ADR 0046-A2](../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md#a2-required--encryptedfield--ifielddecryptor-field-level-encryption-substrate) (Accepted; PR #325 merged 2026-04-30)
**Pipeline variant:** `sunfish-feature-change` (net-new substrate types; zero existing callers)
**Estimated effort:** 3–5h focused sunfish-PM time
**Decomposition:** 5 phases shipping as ~3 separate PRs
**Prerequisites:** ✓ ADR 0046 Accepted (2026-04-26); ✓ ADR 0046-A2 amendment merged (PR #325, 2026-04-30); ✓ existing `KeystoreRootSeedProvider` in `kernel-security` (Phase 1 G6 complete); ✓ existing `kernel-audit` substrate (`AuditEventType` + `IAuditTrail` + `AuditPayload`)

Implementation specification is explicit in ADR 0046-A2 sub-amendments A2.1 through A2.6. This hand-off decomposes the build into binary-PASS-gate phases per the Stage 06 template (`icm/_templates/handoff-stage06.md`).

---

## Scope summary

1. **`EncryptedField` value type** — `readonly record struct` wrapping AES-GCM ciphertext + nonce + key-version + JSON converter
2. **`IFieldEncryptor` / `IFieldDecryptor` interfaces** — capability-checked decrypt-on-read; encrypt is unrestricted
3. **`IDecryptCapability` + `FixedDecryptCapability`** — capability envelope; macaroon integration deferred
4. **Per-tenant DEK derivation** — HKDF-SHA256 from existing `KeystoreRootSeedProvider` root seed; tenant-scoped via salt
5. **Reference impls** — `RecoveryRootSeedFieldEncryptor` / `RecoveryRootSeedFieldDecryptor` / `RecoveryRootSeedFieldEncryptionKeyRotator`
6. **Audit emission** — 3 new `AuditEventType` constants in `kernel-audit/AuditEventType.cs` + `FieldEncryptionAuditPayloads` factory class
7. **DI registration** — extend existing `AddSunfishRecoveryCoordinator()` (no new top-level extension)
8. **Tests** — ~20-25 covering round-trip, capability gating, multi-tenant isolation, multi-version decrypt, audit emission shape, JSON serialization, EFCore mapping smoke

**NOT in scope** (deferred to follow-up hand-offs):
- Macaroon-bound capabilities (ADR 0032 work; `FixedDecryptCapability` is the Phase 1 reference impl)
- Hardware-backed DEK storage (Secure Enclave / TPM)
- Per-record DEKs (Phase 1 is per-tenant)
- Forward secrecy across rotations (rotation primitive lands; old DEK destruction is post-MVP)
- Streaming decrypt for large blobs (Phase 1 targets short-string fields)
- `TaxonomyClassification` typing on `EncryptedField` (OQ-A2.1; deferred per ADR text)

---

## Phases

### Phase 1 — `EncryptedField` value type + JSON converter (~30 min)

Net-new types only; no DI changes; no behavior.

**Files to create:**
- `packages/foundation-recovery/EncryptedField.cs` — `readonly record struct EncryptedField(ReadOnlyMemory<byte> Ciphertext, ReadOnlyMemory<byte> Nonce, int KeyVersion)`
- `packages/foundation-recovery/EncryptedFieldJsonConverter.cs` — `internal sealed class EncryptedFieldJsonConverter : JsonConverter<EncryptedField>` (serializes as `{ "ct": "<base64url>", "nonce": "<base64url>", "kv": <int> }`)
- `packages/foundation-recovery/tests/EncryptedFieldTests.cs` — round-trip JSON serialization; equality semantics; ToString() doesn't leak ciphertext

**Gate:** PASS iff `dotnet build packages/foundation-recovery/Sunfish.Foundation.Recovery.csproj` clean + 3+ JSON serialization tests pass.

**PR title:** `feat(foundation-recovery): EncryptedField value type + JSON converter (W#32 Phase 1, ADR 0046-A2.1)`

### Phase 2 — `IFieldEncryptor` + `IFieldDecryptor` + capability surface + reference impls (~90 min)

Substrate interfaces + AES-GCM reference impls + capability shape.

**Files to create:**
- `packages/foundation-recovery/Crypto/IFieldEncryptor.cs` — interface per ADR 0046-A2.2
- `packages/foundation-recovery/Crypto/IFieldDecryptor.cs` — interface per ADR 0046-A2.2
- `packages/foundation-recovery/Crypto/IDecryptCapability.cs` — interface per ADR 0046-A2.2
- `packages/foundation-recovery/Crypto/FixedDecryptCapability.cs` — `(actor, tenant, validUntil)` reference impl; `ValidateForDecrypt(now)` returns null if now ≤ validUntil + actor matches the tenant scope
- `packages/foundation-recovery/Crypto/FieldDecryptionDeniedException.cs` — `Exception` subclass; ctor takes `(capabilityId, reason)`
- `packages/foundation-recovery/Crypto/RecoveryRootSeedFieldEncryptor.cs` — implementation:
  - Constructor: `(IRootSeedProvider rootSeedProvider, IFieldEncryptionKeyVersionStore versionStore)`
  - `EncryptAsync` derives per-tenant DEK via HKDF-SHA256 (current key-version from versionStore); uses `System.Security.Cryptography.AesGcm`; generates random 12-byte nonce; concatenates ciphertext + 16-byte tag in `EncryptedField.Ciphertext`
- `packages/foundation-recovery/Crypto/RecoveryRootSeedFieldDecryptor.cs` — implementation:
  - Constructor: `(IRootSeedProvider rootSeedProvider, IAuditTrail auditTrail, IRecoveryClock clock)`
  - `DecryptAsync` first calls `capability.ValidateForDecrypt(clock.UtcNow)`; if null returned (valid) AND `capability.Tenant == tenant`: derive DEK at `field.KeyVersion`; AES-GCM decrypt; emit `FieldDecrypted` audit; return plaintext
  - On rejection: emit `FieldDecryptionDenied` audit; throw `FieldDecryptionDeniedException`
- `packages/foundation-recovery/Crypto/IFieldEncryptionKeyRotator.cs` — interface; method `Task RotateAsync(TenantId tenant, ActorId rotator, CancellationToken ct)`
- `packages/foundation-recovery/Crypto/RecoveryRootSeedFieldEncryptionKeyRotator.cs` — bumps version in `IFieldEncryptionKeyVersionStore`; emits `FieldEncryptionKeyRotated` audit
- `packages/foundation-recovery/Crypto/IFieldEncryptionKeyVersionStore.cs` — interface; methods `int GetCurrentVersion(TenantId)`, `int IncrementVersion(TenantId)` (in-process counter for Phase 1; durable storage post-MVP)
- `packages/foundation-recovery/Crypto/InMemoryFieldEncryptionKeyVersionStore.cs` — `ConcurrentDictionary<TenantId, int>` reference impl

**HKDF derivation (canonical):**
```csharp
// In RecoveryRootSeedFieldEncryptor / Decryptor
using System.Security.Cryptography;

private byte[] DerivePerTenantDek(TenantId tenant, int keyVersion)
{
    var rootSeed = _rootSeedProvider.GetSeed();    // 32 bytes
    var salt = Encoding.UTF8.GetBytes(tenant.Value);
    var info = Encoding.UTF8.GetBytes($"sunfish-encrypted-field-v1|version={keyVersion}");
    var dek = new byte[32];
    HKDF.DeriveKey(HashAlgorithmName.SHA256, rootSeed, dek, salt, info);
    return dek;
}
```

Verify `IRootSeedProvider` is the actual interface name in `kernel-security` BEFORE writing — `git grep -n "interface IRootSeedProvider\|class KeystoreRootSeedProvider" packages/kernel-security/`. The ADR text says `KeystoreRootSeedProvider` is the existing primitive; if the interface is named differently, adjust.

**Gate:** PASS iff
- `dotnet build` clean
- Round-trip test: encrypt then decrypt yields original plaintext (in test, supply a `FixedDecryptCapability` with valid scope)
- Capability rejection test: invalid capability throws `FieldDecryptionDeniedException`

**PR title:** `feat(foundation-recovery): IFieldEncryptor + IFieldDecryptor + reference impls (W#32 Phase 2, ADR 0046-A2.2-A2.3)`

### Phase 3 — Audit emission (3 AuditEventType + factories) (~30 min)

**Files to modify/create:**
- `packages/kernel-audit/AuditEventType.cs` — add 3 new constants:
  ```csharp
  public static readonly AuditEventType FieldDecrypted = new("FieldDecrypted");
  public static readonly AuditEventType FieldDecryptionDenied = new("FieldDecryptionDenied");
  public static readonly AuditEventType FieldEncryptionKeyRotated = new("FieldEncryptionKeyRotated");
  ```
- `packages/foundation-recovery/Audit/FieldEncryptionAuditPayloads.cs` — static factory class with 3 methods matching ADR 0046-A2.4 schemas (`FieldDecrypted` / `FieldDecryptionDenied` / `FieldEncryptionKeyRotated`)
- `packages/foundation-recovery/tests/FieldEncryptionAuditPayloadsTests.cs` — schema snapshot tests (alphabetized keys per audit record type)

Wire emission into Phase 2 `Decryptor` + `KeyRotator`. (Phase 2's gate just verifies the throw; this phase verifies the audit envelope shape.)

**Gate:** PASS iff
- 3 schema-snapshot tests pass
- `RecoveryRootSeedFieldDecryptor` emits `AuditRecord` (verify via `IAuditTrail` test double — NSubstitute per Decision Discipline industry defaults) on success AND on denial
- `RecoveryRootSeedFieldEncryptionKeyRotator` emits `AuditRecord` on rotation

**PR title:** `feat(kernel-audit,foundation-recovery): field-encryption audit emission (W#32 Phase 3, ADR 0046-A2.4)`

### Phase 4 — DI extension + integration tests (~30 min)

**Files to modify:**
- `packages/foundation-recovery/DependencyInjection/ServiceCollectionExtensions.cs` — extend `AddSunfishRecoveryCoordinator()` per ADR 0046-A2.5:
  ```csharp
  services.AddSingleton<IFieldEncryptor, RecoveryRootSeedFieldEncryptor>();
  services.AddSingleton<IFieldDecryptor, RecoveryRootSeedFieldDecryptor>();
  services.AddSingleton<IFieldEncryptionKeyRotator, RecoveryRootSeedFieldEncryptionKeyRotator>();
  services.AddSingleton<IFieldEncryptionKeyVersionStore, InMemoryFieldEncryptionKeyVersionStore>();
  ```

**Tests to add** (covering A2.6 acceptance criteria not yet covered):
- Different tenants produce different ciphertext for same plaintext (DEK is tenant-scoped via salt)
- Different key-versions: encrypt at v1, rotate to v2, decrypt v1-encrypted field still works
- EFCore `OwnsOne<EncryptedField>` mapping smoke test (in-memory provider with a wrapper entity)

**Gate:** PASS iff
- Test count meets ~20-25 target across Phases 1-4
- All A2.6 acceptance-criteria checkboxes pass

**PR title:** `feat(foundation-recovery): DI registration + integration tests (W#32 Phase 4, ADR 0046-A2.5-A2.6)`

### Phase 5 — Ledger flip (~5 min)

Update `icm/_state/active-workstreams.md` row #32 → `built`. Append last-updated entry crediting the PRs.

**PR title:** `chore(icm): flip W#32 ledger row → built`

---

## Total decomposition

| Phase | Subject | Hours |
|---|---|---|
| 1 | EncryptedField value type + JSON converter | 0.5 |
| 2 | IFieldEncryptor + IFieldDecryptor + reference impls | 1.5 |
| 3 | Audit emission (3 AuditEventType + factories) | 0.5 |
| 4 | DI extension + integration tests | 0.5 |
| 5 | Ledger flip | 0.1 |
| **Total** | | **~3.1 h** |

(ADR estimate was 3-5h; phase decomposition lands at low end since ADR A2.1-A2.6 are unusually concrete.)

---

## Halt conditions

Per `feedback_decision_discipline` Rule 6 + the no-destructive-git rule, name SPECIFIC scenarios that should trigger a `cob-question-*` beacon instead of attempting to resolve in-session:

- **`IRootSeedProvider` interface signature has drifted** (e.g., now returns `Task<byte[]>` instead of synchronous `byte[]`) → write `cob-question-*-w32-rootseed-shape.md`; halt; XO updates A2 spec
- **`KeystoreRootSeedProvider` doesn't exist in `kernel-security/Keys/`** (e.g., renamed during recent kernel work) → halt; XO confirms current substrate type name
- **`HKDF.DeriveKey` behavior differs across .NET 11 preview revisions** (verified working as of 2026-04-30 baseline) → halt only if test fails; XO investigates
- **`AesGcm` requires .NET surface change** (e.g., constructor signature changed in preview) → halt; document the actual API in beacon
- **`IFieldEncryptionKeyVersionStore` durability surfaces a real consumer need** mid-build (e.g., a provider asks for cross-process version coordination) → halt; XO adds OQ + a follow-up hand-off

---

## Acceptance criteria (cumulative)

Mirror ADR 0046-A2.6 + Stage 06 close-conditions:

- [ ] `EncryptedField` record struct + JSON converter
- [ ] `IFieldEncryptor` + `IFieldDecryptor` + `IDecryptCapability` interfaces
- [ ] `RecoveryRootSeedFieldEncryptor` + `RecoveryRootSeedFieldDecryptor` + `RecoveryRootSeedFieldEncryptionKeyRotator` reference impls
- [ ] `FixedDecryptCapability` Phase 1 reference impl
- [ ] `IFieldEncryptionKeyRotator` + `IFieldEncryptionKeyVersionStore` + `InMemoryFieldEncryptionKeyVersionStore`
- [ ] `FieldDecryptionDeniedException`
- [ ] 3 `AuditEventType` constants in `kernel-audit/AuditEventType.cs`
- [ ] `FieldEncryptionAuditPayloads` factory class with 3 methods
- [ ] DI registration extended on `AddSunfishRecoveryCoordinator()`
- [ ] ~20-25 tests:
  - [ ] Round-trip encrypt+decrypt yields original
  - [ ] Capability invalid/expired/wrong-tenant: throws + emits denied audit
  - [ ] Different tenants get different ciphertext
  - [ ] Different key-versions decrypt correctly (rotation backward-compat)
  - [ ] Audit emission shape: keys + values match A2.4 schema
  - [ ] JSON serialization round-trip
  - [ ] EFCore `OwnsOne` mapping smoke test
- [ ] All tests pass; build clean (no analyzer warnings)
- [ ] Ledger row #32 → `built`

---

## References

- **Spec:** [ADR 0046-A2](../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md#a2-required--encryptedfield--ifielddecryptor-field-level-encryption-substrate) — substrate spec
- **Driver:** [ADR 0058 amendment A1](../../docs/adrs/0058-vendor-onboarding-posture.md) — explicit halt-condition resolved by this build
- **Companion:** [ADR 0049](../../docs/adrs/0049-foundation-audit.md) — audit emission pattern; `AuditEventType` extensibility
- **Companion:** [ADR 0032](../../docs/adrs/0032-multi-team-anchor-workspace-switching.md) — macaroon capability pattern (deferred for `FixedDecryptCapability`)
- **Cluster siblings consuming `EncryptedField` post-build:** W#18 Vendors (next XO hand-off); W#22 Leasing Pipeline (FCRA tenant SSN); W#23 iOS Field-Capture App (offline PII); ADR 0051 Payments (potentially)
