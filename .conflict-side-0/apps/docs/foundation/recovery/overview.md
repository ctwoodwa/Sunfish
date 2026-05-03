# Foundation.Recovery substrate

`Sunfish.Foundation.Recovery` is the foundation-tier substrate for two related concerns:

1. **Multi-sig social recovery** of a tenant's primary key (Phase 1 G6 — ADR 0046's recovery coordinator + trustee attestation + dispute machinery).
2. **Field-level encryption** of at-rest scalar values (W#32 — `EncryptedField`, `IFieldEncryptor`, `IFieldDecryptor`).

Both are foundation-tier (cross-cutting, no business semantics) and consume kernel-tier crypto primitives (Ed25519, X25519, AES-GCM) from `Sunfish.Kernel.Security`.

## What's here

| Type | Role |
|---|---|
| `IRecoveryCoordinator` + `RecoveryCoordinator` | The Phase 1 G6 multi-sig social-recovery state machine (ADR 0046 sub-pattern #48a/#48e/#48f). |
| `RecoveryRequest` / `TrusteeAttestation` / `RecoveryDispute` / `RecoveryEvent` | Append-only message envelopes flowing through the coordinator. |
| `PaperKeyDerivation` | BIP-39 paper-key orchestration for trustee key material. |
| `IRecoveryStateStore` + `InMemoryRecoveryStateStore` | Persistence seam for the coordinator's working state. |
| `IRecoveryClock` + `SystemRecoveryClock` | Wall-clock abstraction for the 7-day grace window. |
| `ITenantKeyProvider` + `InMemoryTenantKeyProvider` | W#20 Phase 0 stub for per-tenant key derivation. ADR 0046 Stage 06 will replace with real KEK-hierarchy backing. |
| `EncryptedField` | The W#32 envelope: `(Ciphertext, Nonce, KeyVersion)` value-record + base64url JSON converter. |
| `IFieldEncryptor` + `TenantKeyProviderFieldEncryptor` | Per-field AES-GCM encrypt path; delegates DEK derivation to `ITenantKeyProvider`. |
| `IFieldDecryptor` + `TenantKeyProviderFieldDecryptor` | Per-field decrypt path; capability-gated; both-or-neither audit-emission overload pair. |
| `IDecryptCapability` + `FixedDecryptCapability` | Capability envelope; macaroon-bound flavor deferred (ADR 0032). |
| `FieldDecryptionDeniedException` | Typed denial with capability-id + reason; emitted to audit + thrown to caller. |

## ADR map

- [ADR 0046](../../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md) — multi-sig social recovery + the field-encryption substrate (A2/A3/A4/A5 amendments)
- [ADR 0049](../../../docs/adrs/0049-foundation-audit.md) — audit emission pattern consumed by the decryptor

## See also

- [Encrypted Field](./encrypted-field.md) — focused walkthrough of the W#32 substrate
- [Foundation.Recovery package README](../../../packages/foundation-recovery/README.md)
