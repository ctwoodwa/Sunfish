---
sort_order: 31
number: 32
slug: foundation-recovery-field-encryption-substrate
title: "Foundation.Recovery field-encryption substrate (ADR 0046-A2/A3/A4/A5)"
status: "built"
status_cell: "`built` (4 phases shipped 2026-04-30)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM ✓"
reference_cell: "https://github.com/ctwoodwa/Sunfish/pull/370 (Phase 1 — `EncryptedField` + JSON converter) + https://github.com/ctwoodwa/Sunfish/pull/371 (Phase 2+3 — substrate + audit emission) + Phase 4 (this PR — DI + ledger flip)"
---

## Notes

**Built 2026-04-30.** P1: `EncryptedField(Ciphertext, Nonce, KeyVersion)` readonly record struct + base64url-shape JSON converter + ToString() redaction. P2+P3 (combined; audit emission inseparable from decryptor body): `IFieldEncryptor`/`IFieldDecryptor`/`IDecryptCapability`/`FixedDecryptCapability`/`FieldDecryptionDeniedException`; `TenantKeyProviderFieldEncryptor` (delegates to `ITenantKeyProvider`; AES-GCM + 12-byte nonce + 16-byte tag; KeyVersion=1 invariant per A5.5); `TenantKeyProviderFieldDecryptor` two-overload constructor (audit-disabled / audit-enabled, both-or-neither per A5.7); `FieldEncryptionAuditPayloadFactory` (alphabetized bodies); `AuditEventType.FieldDecrypted` + `AuditEventType.FieldDecryptionDenied`. P4: DI factory delegate on `AddSunfishRecoveryCoordinator()` throwing `InvalidOperationException` on mid-state misconfiguration. **93/93 foundation-recovery tests pass** (66 prior + 6 P1 + 15 P2/P3 + 6 P4 DI). Rotation primitive deferred per A4.3 (Phase 1 fixed `KeyVersion=1`; future ADR amendment with durable storage). Compounding consumer set now unblocked: W#18 Phase 4 (W9Document + EncryptedField TIN), W#22 Phase 6 (FCRA tenant SSN), W#23 (offline PII), ADR 0051 (potentially card-on-file).
