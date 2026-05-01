# Sunfish.Kernel.Signatures

Sunfish kernel signature-capture substrate per [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md). Captures legally-binding signature events with content-hash binding, append-only revocation log, UETA/E-SIGN consent gate, and pluggable device-attestation surface.

Sibling to `kernel-audit` + `kernel-security`. **W#21 Phase 1 ships the contract surface + InMemory implementations;** native iOS PencilKit + CryptoKit integration deferred to W#23 (iOS Field-Capture App).

## What this ships

### Contracts

- **`ISignatureCapture`** — capture flow: signer identity + consent gate + signature payload + content-hash binding to the signed document.
- **`ISignatureValidityResolver`** — current-validity projection: signed signature + revocation log + key rotation + `GetCurrentValidityAsync(signatureEventId)` returns Active / Revoked / KeyRotated / etc.
- **`ISignatureRevocationLog`** — append-only revocation entries (per ADR 0054 A4+A5; last-revocation-wins with total-order Guid tie-break).
- **`SignatureEvent`** — captured signature record (signer ID, content hash, device attestation, consent timestamp, signature payload).
- **`ConsentRecord`** — UETA / E-SIGN consent gate envelope.

### Reference impls

- **`InMemorySignatureCapture`** — in-process capture path.
- **`InMemorySignatureRevocationLog`** — in-process revocation log.
- **`DefaultSignatureValidityResolver`** — composes capture + revocation log + key store into the validity projection.

### Audit emission

- 5 `AuditEventType` constants in `Sunfish.Kernel.Audit` (`SignatureCaptured`, `SignatureRevoked`, `SignatureValidityProjected`, `ConsentRecorded`, plus 1 reserved for the iOS path).

### Cross-substrate

- **`SignatureEventRef`** — opaque reference type lives in `Sunfish.Foundation.Integrations.Signatures` (so consumer blocks can take a typed FK without referencing this kernel package directly).

## ADR map

- [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — substrate spec + 7 amendments
- [ADR 0046-A1](../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md) — historical-keys amendment (signature survival under key rotation)

## Future scope (W#23)

Native iOS PencilKit (handwritten signature capture) + CryptoKit (Secure Enclave-backed signing). `Sunfish.Kernel.Signatures` provides the contract surface; the iOS path implements the platform-native capture side.

## See also

- `Sunfish.Foundation.Integrations.Signatures` — `SignatureEventRef` typed-FK seam (consumer-side)
- `Sunfish.Blocks.Maintenance` — `WorkOrderCompletionAttestation.Signature` consumer
- `Sunfish.Blocks.Leases` — per-party lease-signature consumer
- `Sunfish.Blocks.PropertyLeasingPipeline` — application-signature consumer
