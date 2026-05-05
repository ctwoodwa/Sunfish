---
sort_order: 20
number: 21
slug: signatures-document-binding-v1-0
title: "Signatures + Document Binding (cluster cross-cutting) — **`Sunfish.Kernel.Signatures` v1.0**"
status: "built"
status_cell: "`built` (W#23 iOS native PencilKit/CryptoKit deferred per scope)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM ✓"
reference_cell: "`icm/00_intake/output/property-signatures-intake-2026-04-28.md` + `docs/adrs/0054-electronic-signature-capture-and-document-binding.md` + `icm/_state/handoffs/property-signatures-stage06-handoff.md` + `icm/_state/handoffs/property-signatures-stage06-addendum.md`"
---

## Notes

**Built 2026-04-30.** Shipped via 6 PRs: Phase 0+1 (#348, `SignatureEnvelope` stub + kernel-signatures substrate scaffold; 11 model types + 3 service contracts + InMemory impls + DI extension) → Phase 2 (#350, canonicalization — RFC 8785 pragmatic via `Foundation.Crypto.CanonicalJson` + UTF-8 NFC + PDF/A stub) → Phase 3 (#352, last-revocation-wins merge per A4+A5; pure `RevocationProjection` extracted) → Phase 4 (#353, scope validation via `Foundation.Taxonomy` per A7) → Phase 5 (#355, 5 `AuditEventType` + factory + emission across all 3 services) → Phase 6+7 (this PR; cross-package wiring verification + apps/docs/kernel/signatures/ + end-to-end integration test + ledger flip). 70/70 kernel-signatures tests pass. **Native iOS PencilKit + CryptoKit integration deferred to W#23 iOS Field-Capture App** per the original hand-off scope — kernel-signatures provides the substrate that the iOS app will attach to via the `SignatureEnvelope` algorithm-agility container + `DeviceAttestation` payload.
