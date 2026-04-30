# Workstream #32 — Stage 06 hand-off addendum (post-A4 + post-A5 council)

**Supersedes:** [`adr-0046-a2-encrypted-field-stage06-handoff.md`](./adr-0046-a2-encrypted-field-stage06-handoff.md) Phases 2–5 + halt-conditions
**Effective:** 2026-04-30 (after PR #333 + #335 merge)
**Spec source:** ADR 0046 amendments A4 (substantive resolution of A2 council F2/F4/F5) + A5 (mechanical fixes from A4 council). See `docs/adrs/0046-key-loss-recovery-scheme-phase-1.md` §"Amendments (post-acceptance, 2026-04-30)".

The original hand-off authored 2026-04-30 was written against the A2/A3 spec. Two council passes (PRs #329 + #335) reshaped the substrate. This addendum documents the deltas; the original hand-off's Phase 1 + Phase 5 (ledger flip) are unchanged.

---

## Changed scope summary

| Aspect | A2/A3 (original) | A4/A5 (revised) |
|---|---|---|
| Encryptor reference impl | `RecoveryRootSeedFieldEncryptor` (HKDF in package) | `TenantKeyProviderFieldEncryptor` (delegates to existing `ITenantKeyProvider`) |
| Decryptor reference impl | `RecoveryRootSeedFieldDecryptor` | `TenantKeyProviderFieldDecryptor` (explicit two-overload constructor; structural both-or-neither audit invariant) |
| Per-tenant DEK derivation | Manual HKDF-SHA256 in package | Delegated to `ITenantKeyProvider.DeriveKeyAsync(tenant, "encrypted-field-aes", ct)` (existing W#20 Phase 0 stub) |
| Audit emission | Raw `Dictionary<string, object?>` factories | `AuditPayload`-returning factories (matches W#31 `TaxonomyAuditPayloadFactory`) + `IOperationSigner.SignAsync` envelope |
| Audit DI | A4.6 `services.AddOptions().Validate(...)` (broken — wrong timing) | A5.3 factory delegate; mid-state throws `InvalidOperationException` at first resolution |
| Rotation primitive | `IFieldEncryptionKeyRotator` + `IFieldEncryptionKeyVersionStore` + `InMemoryFieldEncryptionKeyVersionStore` + `AuditEventType.FieldEncryptionKeyRotated` | **DEFERRED** — Phase 1 fixed `KeyVersion = 1`; rotation primitive lands in Phase 2 amendment |
| Purpose-label format | `encrypted-field-aes-v{n}` (with version suffix) | `encrypted-field-aes` (no suffix; matches existing `ITenantKeyProvider` xmldoc) |
| `IRecoveryClock.UtcNow` | called as property (would not compile) | called as method `UtcNow()` (matches actual interface) |
| Total phases | 5 | **4** (rotation Phase 3 dropped) |
| Total effort | ~3-5h | **~2-3h** (rotation work removed) |

---

## Revised phase decomposition

### Phase 1 — `EncryptedField` value type + JSON converter (~30 min) [UNCHANGED]

Original hand-off Phase 1 stands as written. `EncryptedField(ReadOnlyMemory<byte> Ciphertext, ReadOnlyMemory<byte> Nonce, int KeyVersion)` + `EncryptedFieldJsonConverter` ship per A2.1.

**Phase 1 invariant clarification (per A5.5):** Phase 1 encrypt MUST write `KeyVersion = 1`; decrypt accepts `KeyVersion >= 1`; decrypting `KeyVersion <= 0` throws `FieldDecryptionDeniedException("unsupported key version")`.

### Phase 2 — `IFieldEncryptor` + `IFieldDecryptor` + reference impls (~60 min) [REVISED]

**Files to create:**
- `packages/foundation-recovery/Crypto/IFieldEncryptor.cs` (interface per A2.2 — unchanged)
- `packages/foundation-recovery/Crypto/IFieldDecryptor.cs` (interface per A2.2 — unchanged)
- `packages/foundation-recovery/Crypto/IDecryptCapability.cs` (interface per A3.3 — `ValidateForDecrypt(TenantId targetTenant, DateTimeOffset now)`)
- `packages/foundation-recovery/Crypto/FixedDecryptCapability.cs` (reference impl — `(actor, tenant, validUntil)`)
- `packages/foundation-recovery/Crypto/FieldDecryptionDeniedException.cs`

**Reference implementations** (A4.1 + A5.1; replace original `RecoveryRootSeedField*`):

```csharp
namespace Sunfish.Foundation.Recovery.Crypto;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery.TenantKey;

public sealed class TenantKeyProviderFieldEncryptor : IFieldEncryptor
{
    private readonly ITenantKeyProvider _tenantKeys;

    public TenantKeyProviderFieldEncryptor(ITenantKeyProvider tenantKeys)
    {
        ArgumentNullException.ThrowIfNull(tenantKeys);
        _tenantKeys = tenantKeys;
    }

    public async Task<EncryptedField> EncryptAsync(
        ReadOnlyMemory<byte> plaintext,
        TenantId tenant,
        CancellationToken ct)
    {
        const string purpose = "encrypted-field-aes";
        const int keyVersion = 1;       // A5.5: Phase 1 invariant
        var dek = await _tenantKeys.DeriveKeyAsync(tenant, purpose, ct).ConfigureAwait(false);

        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(dek.Span, tagSizeInBytes: 16);
        aes.Encrypt(nonce, plaintext.Span, ciphertext, tag);

        var packed = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, packed, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, packed, ciphertext.Length, tag.Length);
        return new EncryptedField(packed, nonce, keyVersion);
    }
}

public sealed class TenantKeyProviderFieldDecryptor : IFieldDecryptor
{
    private readonly ITenantKeyProvider _tenantKeys;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly IRecoveryClock _clock;

    /// <summary>Audit-disabled overload (test/bootstrap).</summary>
    public TenantKeyProviderFieldDecryptor(ITenantKeyProvider tenantKeys, IRecoveryClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(tenantKeys);
        _tenantKeys = tenantKeys;
        _clock = clock ?? new SystemRecoveryClock();
    }

    /// <summary>Audit-enabled overload.</summary>
    public TenantKeyProviderFieldDecryptor(
        ITenantKeyProvider tenantKeys,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        IRecoveryClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(tenantKeys);
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        _tenantKeys = tenantKeys;
        _auditTrail = auditTrail;
        _signer = signer;
        _clock = clock ?? new SystemRecoveryClock();
    }

    public async Task<ReadOnlyMemory<byte>> DecryptAsync(
        EncryptedField field,
        IDecryptCapability capability,
        TenantId tenant,
        CancellationToken ct)
    {
        if (field.KeyVersion < 1)
            throw new FieldDecryptionDeniedException(capability.CapabilityId, "unsupported key version");

        var now = _clock.UtcNow();    // A5.2: method call
        var rejection = capability.ValidateForDecrypt(tenant, now);
        if (rejection is not null)
        {
            await EmitAuditAsync(
                AuditEventType.FieldDecryptionDenied,
                FieldEncryptionAuditPayloadFactory.DecryptionDenied(capability, tenant, rejection),
                tenant,
                ct).ConfigureAwait(false);
            throw new FieldDecryptionDeniedException(capability.CapabilityId, rejection);
        }

        const string purpose = "encrypted-field-aes";   // A5.4: no -v{n} suffix in Phase 1
        var dek = await _tenantKeys.DeriveKeyAsync(tenant, purpose, ct).ConfigureAwait(false);

        var packed = field.Ciphertext.Span;
        if (packed.Length < 16)
            throw new FieldDecryptionDeniedException(capability.CapabilityId, "ciphertext too short");

        var ciphertextLen = packed.Length - 16;
        var ciphertext = packed[..ciphertextLen];
        var tag = packed[ciphertextLen..];
        var plaintext = new byte[ciphertextLen];
        using var aes = new AesGcm(dek.Span, tagSizeInBytes: 16);
        try
        {
            aes.Decrypt(field.Nonce.Span, ciphertext, tag, plaintext);
        }
        catch (CryptographicException ex)
        {
            await EmitAuditAsync(
                AuditEventType.FieldDecryptionDenied,
                FieldEncryptionAuditPayloadFactory.DecryptionDenied(capability, tenant, $"crypto: {ex.Message}"),
                tenant,
                ct).ConfigureAwait(false);
            throw new FieldDecryptionDeniedException(capability.CapabilityId, "AES-GCM tag verification failed");
        }

        await EmitAuditAsync(
            AuditEventType.FieldDecrypted,
            FieldEncryptionAuditPayloadFactory.Decrypted(capability, tenant, field.KeyVersion),
            tenant,
            ct).ConfigureAwait(false);
        return plaintext;
    }

    private async Task EmitAuditAsync(
        AuditEventType eventType,
        AuditPayload payload,
        TenantId tenant,
        CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null) return;
        var occurredAt = _clock.UtcNow();
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenant,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }
}
```

**Gate:** PASS iff `dotnet build` clean + round-trip + capability-rejection tests pass.

**PR title:** `feat(foundation-recovery): TenantKeyProviderField{En,De}cryptor + capability surface (W#32 Phase 2, ADR 0046-A4/A5)`

### Phase 3 — Audit emission (~30 min) [REVISED]

**Files:**
- `packages/kernel-audit/AuditEventType.cs` — add `FieldDecrypted` + `FieldDecryptionDenied` (NOT `FieldEncryptionKeyRotated` — deferred per A4.3)
- `packages/foundation-recovery/Audit/FieldEncryptionAuditPayloadFactory.cs` — 2 factory methods returning `AuditPayload` (per A4.2 / A5; matches `TaxonomyAuditPayloadFactory` pattern)
- `packages/foundation-recovery/tests/FieldEncryptionAuditPayloadsTests.cs` — schema snapshot tests on alphabetized keys

**Gate:** PASS iff 2 schema tests pass + `TenantKeyProviderFieldDecryptor` emits `AuditRecord` (verified via NSubstitute test double per Decision Discipline industry defaults) on success + denial.

**PR title:** `feat(kernel-audit,foundation-recovery): field-encryption audit emission (W#32 Phase 3, ADR 0046-A5)`

### Phase 4 — DI registration + integration tests + ledger flip (~60 min) [REVISED]

**DI extension** (per A5.3):

```csharp
public static IServiceCollection AddSunfishRecoveryCoordinator(this IServiceCollection services)
{
    // ... existing recovery-coordinator registrations ...

    services.TryAddSingleton<IFieldEncryptor, TenantKeyProviderFieldEncryptor>();
    services.TryAddSingleton<IFieldDecryptor>(sp =>
    {
        var tenantKeys = sp.GetRequiredService<ITenantKeyProvider>();
        var clock = sp.GetService<IRecoveryClock>();
        var auditTrail = sp.GetService<IAuditTrail>();
        var signer = sp.GetService<IOperationSigner>();
        return (auditTrail, signer) switch
        {
            (null, null) => new TenantKeyProviderFieldDecryptor(tenantKeys, clock),
            (not null, not null) => new TenantKeyProviderFieldDecryptor(tenantKeys, auditTrail, signer, clock),
            _ => throw new InvalidOperationException(
                "Field-encryption decryptor requires both IAuditTrail and IOperationSigner registered, or neither. " +
                $"Mid-state misconfiguration: IAuditTrail={(auditTrail is null ? "null" : "registered")}, " +
                $"IOperationSigner={(signer is null ? "null" : "registered")}.")
        };
    });

    return services;
}
```

**Tests** (target ~30-40 across all phases per A4.4 + A5.7 update):

- Round-trip encrypt+decrypt yields original (with `IDecryptCapability` valid for tenant + actor)
- Capability rejection paths × 4: expired, wrong-tenant, null capability, revoked — each throws + emits denied audit
- Different tenants get different ciphertext for same plaintext (`ITenantKeyProvider` tenant-scoped)
- HMAC-tag-tampering: corrupt ciphertext or nonce → throws + emits denied audit
- AES-GCM nonce uniqueness sanity (random per encrypt)
- Audit emission shape: alphabetized payload-body keys snapshot test
- Audit emission can be disabled: audit-disabled overload + `null` deps → no audit emission
- **Audit emission misconfiguration (per A5.7):**
  - Resolution-time test: register exactly one of (`IAuditTrail`, `IOperationSigner`) without the other → first `sp.GetRequiredService<IFieldDecryptor>()` throws `InvalidOperationException` with mid-state message
  - Constructor-guard test: directly constructing `TenantKeyProviderFieldDecryptor` with mid-state is impossible (overload selection prevents it; verify via reflection on the type's public constructors)
- Phase 1 invariants (per A5.5):
  - Encrypt always writes `KeyVersion = 1` (snapshot test on output)
  - Decrypt accepts `KeyVersion = 1` (round-trip)
  - Decrypt rejects `KeyVersion = 0` and `KeyVersion = -1` with `unsupported key version` reason
- JSON serialization round-trip
- EFCore three-column property mapping smoke test (in-memory provider)

**Ledger flip:** update `icm/_state/active-workstreams.md` row #32 → `built` after PR merges.

**PR title:** `feat(foundation-recovery): DI + integration tests + ledger flip (W#32 Phase 4, ADR 0046-A5)`

### Phase 5 (DROPPED — was rotation; deferred per A4.3)

The original hand-off's Phase 5 (rotation primitive) is **deleted**. Phase 1 ships fixed `KeyVersion = 1`; rotation lands in a future ADR amendment with durable storage substrate.

### Halt-condition addition

In addition to the original 5 halt-conditions, add:

- **Phase 1 caller needs key rotation** (e.g., trustee-driven security event invalidates current DEK) → HALT and write `cob-question-*-w32-rotation-needed.md` to `icm/_state/research-inbox/`. A future ADR amendment introduces the rotation primitive backed by durable storage; do NOT improvise rotation in Phase 1.

---

## Updated total decomposition

| Phase | Subject | Hours |
|---|---|---|
| 1 | `EncryptedField` value type + JSON converter | 0.5 |
| 2 | `TenantKeyProvider`-based reference impls + capability surface | 1.0 |
| 3 | Audit emission (2 `AuditEventType` + factories) | 0.5 |
| 4 | DI + integration tests + ledger flip | 1.0 |
| **Total** | | **~3.0 h** |

(Down from ~3-5h to ~3h after rotation deferred.)

---

## Decision-class

Session-class per `feedback_decision_discipline` Rule 1. Authority: XO; this addendum is mechanical mapping of A4 + A5 changes onto the original phase decomposition. No business judgment introduced.

---

## References

- **Spec:** ADR 0046 §A4 (substantive resolution of council F2/F4/F5) + §A5 (mechanical fixes from A4 council)
- **Council reviews:** PR #329 (A2 review) + PR #335 (A4 review)
- **Original hand-off:** [`adr-0046-a2-encrypted-field-stage06-handoff.md`](./adr-0046-a2-encrypted-field-stage06-handoff.md) (Phases 1 + 5 only retained; Phases 2-4 superseded by this addendum; original Phase 5 = ledger flip is now Phase 4)
- **Pattern reference:** `packages/foundation-taxonomy/Services/InMemoryTaxonomyRegistry.cs` (canonical two-overload constructor) + `packages/foundation-taxonomy/Audit/TaxonomyAuditPayloadFactory.cs` (canonical audit factory)
