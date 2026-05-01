# EncryptedField substrate (W#32)

The W#32 substrate is the canonical Sunfish primitive for at-rest encrypted scalar values: TINs, SSNs, payout-account numbers, demographic data, signature payloads. Every field that compliance, regulation, or threat-model requires to be opaque outside an explicit access boundary lives behind this seam.

Implements ADR 0046-A2/A3/A4/A5.

## The envelope

```csharp
public readonly record struct EncryptedField(
    ReadOnlyMemory<byte> Ciphertext,   // AES-GCM ciphertext + 16-byte tag, packed
    ReadOnlyMemory<byte> Nonce,        // 12-byte random nonce
    int KeyVersion);                    // Phase 1 fixed at 1; rotation deferred
```

`ToString()` deliberately omits Ciphertext + Nonce bytes — log-leak defense in depth.

JSON shape via the bundled converter:

```json
{ "ct": "<base64url>", "nonce": "<base64url>", "kv": 1 }
```

Base64url (no padding) keeps the on-the-wire form URL-safe.

## The encrypt path

```csharp
public interface IFieldEncryptor
{
    Task<EncryptedField> EncryptAsync(
        ReadOnlyMemory<byte> plaintext,
        TenantId tenant,
        CancellationToken ct);
}
```

Reference impl `TenantKeyProviderFieldEncryptor`:

1. Derive a per-tenant DEK via `ITenantKeyProvider.DeriveKeyAsync(tenant, "encrypted-field-aes", ct)`.
2. AES-GCM-encrypt with a fresh 12-byte random nonce + 16-byte tag.
3. Always write `KeyVersion = 1` (Phase 1 invariant per ADR 0046-A5.5).

Encryption is **unrestricted** — any caller with an `IFieldEncryptor` can write ciphertext. The access boundary lives entirely on the decrypt path.

## The decrypt path

```csharp
public interface IFieldDecryptor
{
    Task<ReadOnlyMemory<byte>> DecryptAsync(
        EncryptedField field,
        IDecryptCapability capability,
        TenantId tenant,
        CancellationToken ct);
}
```

Capability shape:

```csharp
public interface IDecryptCapability
{
    string CapabilityId { get; }
    string? ValidateForDecrypt(TenantId targetTenant, DateTimeOffset now);  // null → valid
}
```

Phase 1 reference: `FixedDecryptCapability(capabilityId, actor, tenant, validUntil)`. Macaroon-bound flavor (ADR 0032) is the follow-up.

`TenantKeyProviderFieldDecryptor` ships **two-overload constructor** (per ADR 0046-A5.7):

- `(ITenantKeyProvider tenantKeys, IRecoveryClock? clock = null)` — audit-disabled (test / bootstrap)
- `(ITenantKeyProvider tenantKeys, IAuditTrail auditTrail, IOperationSigner signer, IRecoveryClock? clock = null)` — audit-enabled; both `IAuditTrail` and `IOperationSigner` are required together

There is **no mid-state**. The DI factory delegate in `AddSunfishRecoveryCoordinator()` throws `InvalidOperationException` at first resolution if exactly one of `IAuditTrail` / `IOperationSigner` is registered.

## Audit emission

When the audit-enabled overload is wired, every decrypt call emits one of:

- `AuditEventType.FieldDecrypted` — successful decrypt (capability + tenant + key-version in body)
- `AuditEventType.FieldDecryptionDenied` — any rejection path (capability-id + reason)

Rejection reasons (all surfaced as `FieldDecryptionDeniedException.Reason`):

| Reason | Cause |
|---|---|
| `unsupported key version` | `EncryptedField.KeyVersion < 1` (Phase 1 invariant guard) |
| `expired` | `IDecryptCapability.ValidateForDecrypt` returned `"expired"` |
| `wrong-tenant` | Capability bound to a different tenant than the decrypt request |
| `ciphertext too short` | Packed bytes < tag length (16) — corruption or truncation |
| `AES-GCM tag verification failed` | HMAC tag mismatch — tampering or wrong key |

## Consumer pattern

W#18 Phase 4 (`W9Document` TIN), W#22 Phase 9 (`DemographicProfile`), and ADR 0051 (Payments) all consume the substrate via the same boundary pattern:

```csharp
// At write — the service encrypts at the SubmitX boundary
var encrypted = await _encryptor.EncryptAsync(plaintextBytes, tenant, ct);

// Persist `encrypted` in the entity record; plaintext never lives past this scope.

// At authorized read (HUD reporting / SAR / etc.)
var decrypted = await _decryptor.DecryptAsync(field, capability, tenant, ct);
```

For records with multiple encrypted fields (e.g., `DemographicProfile`'s 8 protected-class fields), the encryption helper iterates and encrypts each non-null field; null in the submission stays null in the encrypted record (declined-to-disclose semantics).

## Rotation (deferred)

`KeyVersion = 1` is fixed in Phase 1. Rotation requires a durable version-store substrate that doesn't exist yet — deferred per ADR 0046-A4.3 to a future amendment. The decryptor accepts `KeyVersion >= 1` so the deferred rotation primitive can introduce v2/v3 without breaking forward compatibility.

## See also

- [ADR 0046](../../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md) — substrate spec + amendments
- [Foundation.Recovery overview](./overview.md)
- [Foundation.Recovery README](../../../packages/foundation-recovery/README.md)
