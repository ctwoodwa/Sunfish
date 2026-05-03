# Sunfish.Foundation.Recovery

Foundation-tier substrate for two related concerns: (1) **multi-sig social recovery** of a tenant's primary key (Phase 1 G6 per [ADR 0046](../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md)), and (2) **field-level encryption** of at-rest scalar values (W#32 / ADR 0046-A2/A3/A4/A5).

Cross-cutting; no business semantics. Consumes kernel-tier crypto primitives (Ed25519, X25519, AES-GCM) from `Sunfish.Kernel.Security`.

## What this ships

### Multi-sig social recovery (Phase 1 G6)

- **`IRecoveryCoordinator`** + `RecoveryCoordinator` — the state machine for recovery (request → trustee attestation quorum → 7-day grace window → SqlCipher rekey).
- **Message envelopes:** `RecoveryRequest`, `TrusteeAttestation`, `RecoveryDispute`, `RecoveryEvent` (append-only).
- **`PaperKeyDerivation`** — BIP-39 wordlist + paper-key derivation orchestration.
- **`IRecoveryStateStore`** + `InMemoryRecoveryStateStore` — coordinator-state persistence seam.
- **`IRecoveryClock`** + `SystemRecoveryClock` — controllable clock for the 7-day grace window.

### Per-tenant key derivation

- **`ITenantKeyProvider`** + `InMemoryTenantKeyProvider` — W#20 Phase 0 stub for per-tenant key material. Tenant + purpose-label → 32-byte derived key (HMAC-SHA256). ADR 0046 Stage 06 will replace with real KEK-hierarchy backing.

### Field-level encryption (W#32 — `Crypto/`)

- **`EncryptedField(Ciphertext, Nonce, KeyVersion)`** — value-record envelope + base64url JSON converter. Phase 1 invariant: `KeyVersion = 1`; rotation deferred per A4.3.
- **`IFieldEncryptor`** — encrypt-on-write seam (unrestricted; per-tenant DEK).
- **`IFieldDecryptor`** — capability-gated decrypt; emits `FieldDecrypted` / `FieldDecryptionDenied` audit per call.
- **`IDecryptCapability`** + `FixedDecryptCapability` — Phase 1 capability shape; macaroon-bound flavor (ADR 0032) is the follow-up.
- **`FieldDecryptionDeniedException`** — typed denial carrying capability-id + reason.

### Audit emission (`Audit/`)

- **`FieldEncryptionAuditPayloadFactory`** — alphabetized `AuditPayload` bodies for the W#32 events. Mirrors the `TaxonomyAuditPayloadFactory` convention.

## Two-overload constructor pattern

`TenantKeyProviderFieldDecryptor` ships **two constructors** (per ADR 0046-A5.7):

```csharp
// Audit-disabled (test / bootstrap)
new TenantKeyProviderFieldDecryptor(tenantKeys);

// Audit-enabled — both audit trail and signer required together
new TenantKeyProviderFieldDecryptor(tenantKeys, auditTrail, signer);
```

There is no mid-state. The `AddSunfishRecoveryCoordinator()` DI factory throws `InvalidOperationException` at first resolution if exactly one of `IAuditTrail` / `IOperationSigner` is registered. This makes "encryption substrate is wired but audit isn't" a fail-fast misconfiguration rather than a silent log-leak.

## DI

```csharp
services.AddSunfishRecoveryCoordinator();
```

Registers `IRecoveryClock`, `IRecoveryStateStore`, `IRecoveryCoordinator`, `IFieldEncryptor`, and `IFieldDecryptor` (with the both-or-neither audit factory). Caller adds an `ICapabilityPromoter` and a `IDisputerValidator` separately.

## Consumer set

- W#18 Phase 4 — `W9Document` TIN encryption (`blocks-maintenance`)
- W#22 Phase 9 — `DemographicProfile` FHA-defense encryption (`blocks-property-leasing-pipeline`)
- W#23 — iOS Field-Capture App offline PII (post-substrate)
- ADR 0051 — Payments (potential card-on-file)

## ADR map

- [ADR 0046](../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md) — substrate spec + A1–A5 amendments
- [ADR 0049](../../docs/adrs/0049-foundation-audit.md) — audit-emission pattern consumed by the decryptor

## See also

- [Foundation.Recovery overview](../../apps/docs/foundation/recovery/overview.md) — apps/docs walkthrough
- [Encrypted Field substrate](../../apps/docs/foundation/recovery/encrypted-field.md) — W#32-focused walkthrough
