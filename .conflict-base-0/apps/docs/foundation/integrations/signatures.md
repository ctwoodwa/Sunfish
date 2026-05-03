# Signatures Contracts

`Sunfish.Foundation.Integrations.Signatures` ships the cross-substrate reference type for the eventual electronic-signatures substrate (per [ADR 0054 — Electronic Signature Capture and Document Binding](../../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md)).

**Phase 1 stub status:** This namespace ships `SignatureEventRef` only — an opaque FK shape. The full ADR 0054 substrate (`SignatureEvent`, `ContentHash`, `PenStrokeBlobRef`, `IConsentRegistry`, `ISignatureCapture`, etc.) lands in `kernel-signatures` when W#21 Stage 06 is hand-offed.

## Phase 1 stub

Introduced by W#19 Phase 0 (mirroring the addendum's Money / ThreadId stub pattern) so W#19 Phase 3's `WorkOrderCompletionAttestation` child entity could compile with the FK shape without forcing W#21 Stage 06 to ship first.

```csharp
public readonly record struct SignatureEventRef(Guid SignatureEventId);
```

## Cross-substrate consumers

- **W#19 Work Orders** — `WorkOrderCompletionAttestation.Signature: SignatureEventRef` (Phase 3 child entity).
- **W#27 Leases** — eventual `LeasePartySignature.SignatureEvent` (Phase 3 — currently halted on `ContentHash`).
- **W#22 Leasing Pipeline** — `Application.ApplicationSignature` (Phase 1 — currently halted).

## What W#21 Stage 06 will replace

When `kernel-signatures` lands, the canonical `SignatureEventId` lives there alongside the full event record (capture metadata, device attestation, content hash, consent record). Consumers point at the same FK shape; the stub becomes a no-op forwarder until W#21 ships.

## See also

- [ADR 0054](../../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md)
- [W#21 hand-off](../../../icm/_state/handoffs/property-signatures-stage06-handoff.md) (currently halted on `Foundation.Crypto.SignatureEnvelope`)
- [W#19 hand-off addendum](../../../icm/_state/handoffs/property-work-orders-stage06-addendum.md) — minimal-stub pattern this follows
