# Intake Note — Electronic Signature Capture & Document Binding

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build` and a hand-off file appears in `icm/_state/handoffs/`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turn 4 — signature capture for maintenance sign-off and lease review on iPad with leaseholder present).
**Pipeline variant:** `sunfish-feature-change` (with new ADR; new kernel module)
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
**Position in cluster:** Cross-cutting #1 — load-bearing for leases, work orders, maintenance sign-off, leasing-pipeline applications, criteria documents.

---

## Problem Statement

Property operations require electronic signatures at multiple lifecycle points: lease execution and renewal, move-in/move-out checklist sign-off, maintenance work-order completion attestation, criteria-acknowledgement by applicants, vendor service agreements. Each is a legally significant act with evidentiary requirements.

A signature is *categorically different* from a receipt photo or an inspection note. A receipt is evidence of a past event; a signature **creates a binding event**. That distinction drives requirements that don't apply to other captured artifacts:

1. **Cryptographic content-binding** — the signature must bind to the exact version of the document on screen. Document edits create new versions; old signatures remain valid against their original content hash.
2. **Offline-cryptographic-self-containment** — basements have no signal. A signature event must be fully formed when captured: content hash, device clock timestamp, operator's device-key witness signature, signer's pen-stroke data, all bundled and durable. No server round-trip to be valid.
3. **Two identities per event, not one** — owner-as-witness (device key) + signer-as-signatory (pen stroke + biometric metadata).
4. **PDF output artifact** — signed leases produce a PDF the leaseholder takes with them; iPad generates on-device, ships to signer's email.
5. **UETA / E-SIGN compliance posture** — intent-to-sign, consent-to-do-business-electronically, association-with-record, retention. Items 1–4 are technical; consent is one-time UX scaffolding per signer.

This intake captures the signature event model, the iOS PencilKit + CryptoKit capture flow, the document-binding mechanic, and the new ADR work. It is consumed by Leases, Work Orders, Inspections (move-in/out), and Leasing Pipeline (criteria acknowledgement).

## Scope Statement

### In scope (this intake)

1. **`SignatureEvent` entity.** ID, tenant-scoped, document_version_hash (binds to canonical document content), signer_identity_ref (could be tenant-internal or external party), witness_identity_ref (operator who held the iPad), pen_stroke_blob_ref, captured_at, captured_at_clock_source (device clock + optional NTP attestation), device_attestation (operator's device key signature over the event payload), optional_geolocation, consent_record_ref.
2. **`SignedDocument` projection.** Latest signature → which document version → status (signed | superseded | revoked). Computed view; not authoritative.
3. **`ConsentRecord` entity.** One-time-per-signer record of "signer consented to do business electronically"; references the consent disclosure version and timestamp; required before any SignatureEvent for this signer is valid.
4. **iOS capture flow** (SwiftUI):
   - Document review pane (renders leaseable / sign-able content from a content-hash-bound source)
   - One-time consent UI (if no `ConsentRecord` exists for this signer)
   - PencilKit canvas for signature (Pencil pressure/tilt/timing if available; finger fallback)
   - CryptoKit signing of event payload using operator device key (Secure Enclave-backed)
   - Local persistence of fully-formed `SignatureEvent` (queued for sync)
   - PDF generation (PDFKit) with embedded signature image; optional email-to-signer flow
5. **`kernel-signatures` module** (or `blocks-signatures` depending on Stage 02 decision — see OQ-S1).
6. **PDF generation library** (Bridge-side parallel implementation for non-iOS-originated signature flows; e.g., owner counter-signs from Anchor desktop).
7. **Device-key issuance + rotation** for operators (extends Foundation.Recovery — uses recovery primitives for key custody and rotation).
8. **New ADR**: "Electronic signature capture & document binding."
9. **ADR 0046 amendment**: outstanding signature commitments survive operator key rotation; recovery affects only the ability to issue *new* signatures.
10. **ADR 0049 amendment** (or confirmation): SignatureEvent is a first-class audit record type.

### Out of scope (this intake — handled elsewhere)

- Document storage and versioning — domain-specific (leases live in `blocks-leases`; criteria docs in `blocks-leasing-pipeline`; etc.). This intake provides the binding *mechanism*, not document storage.
- Specific document templates (lease boilerplate, criteria docs, work-order completion forms) — content concern; lives in respective domain blocks.
- Tax / legal advice on which jurisdictions accept electronic signatures and under what conditions — out of scope; UETA + E-SIGN nationwide US baseline assumed.
- Notarization (notary-witnessed signatures with raised seal) — Phase 4+; a separate workflow class.
- Biometric authentication of signer (Face ID / Touch ID gate before signing) — optional UX enhancement; defer.

### Explicitly NOT in scope

- DocuSign / Adobe Sign / HelloSign integration — provider-neutrality applies; if external e-sign integration is ever needed it lands as `providers-esign-*` adapter, not as the primary path
- Signed PDF tampering forensics — PDF outputs are convenience artifacts; the canonical evidence is the `SignatureEvent` in the kernel ledger

---

## Affected Sunfish Areas

| Layer | Item | Change |
|---|---|---|
| Foundation | Foundation.Recovery (in-flight per workstream #15) | Used for operator device-key custody + rotation |
| Foundation | (potentially) `Foundation.Crypto` | Hash + sign + verify primitives — may already exist; confirm in Stage 01 |
| Kernel | `kernel-signatures` (new) OR `blocks-signatures` | Domain depends on Stage 02 decision |
| Kernel | `kernel-audit` (existing) | SignatureEvent emits to audit substrate |
| Blocks | `blocks-leases` (sibling intake) | Consumes for lease execution |
| Blocks | `blocks-work-orders` (sibling intake) | Consumes for completion attestation |
| Blocks | `blocks-inspections` (sibling intake) | Consumes for move-in/out sign-off |
| Blocks | `blocks-leasing-pipeline` (sibling intake) | Consumes for criteria acknowledgement |
| iOS | New `Signing` view + capture pipeline | SwiftUI + PencilKit + CryptoKit + PDFKit |
| Bridge | Server-side PDF generation library | For non-iOS-originated signatures |
| ADRs | New: "Electronic signature capture & document binding" | Primary architectural deliverable |
| ADRs | ADR 0046 | Amendment — signature survival under key rotation |
| ADRs | ADR 0049 | Confirm SignatureEvent as audit record type |

---

## Acceptance Criteria

- [ ] New ADR drafted, council-reviewed, accepted
- [ ] `SignatureEvent`, `SignedDocument`, `ConsentRecord` entities defined; full XML doc; ADR 0014 adapter parity (read-side)
- [ ] iOS capture flow end-to-end: open document → consent flow (if needed) → PencilKit canvas → CryptoKit-signed event → local persistence → PDF generation → email-to-signer
- [ ] Offline-self-contained: capture flow works in airplane mode; signature event syncs cleanly when connectivity returns
- [ ] PDF output: signed lease PDF generated on-device; embedded signature image; visually verifiable
- [ ] Document-binding integrity test: signature verified valid against original document version; signature verified invalid against modified document
- [ ] Audit trail: every SignatureEvent appears in kernel-audit log with correct event type and content
- [ ] kitchen-sink demo: lease signing flow on a simulated iPad (or Mac Catalyst); criteria acknowledgement on a Bridge web page (server-side flow)
- [ ] apps/docs entry covering signature lifecycle, document binding, PDF generation, UETA compliance posture

---

## Open Questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-S1 | `kernel-signatures` module vs. `blocks-signatures` block — kernel-tier (every accelerator gets it for free) vs block-tier (opt-in via plugin). | Stage 02 — recommend kernel-tier given how broadly it's consumed and its security-critical nature. |
| OQ-S2 | Pen-stroke storage: vector (PencilKit JSON-like) vs raster (PNG) vs both. Vector is more evidentially defensible (timing data) but larger; raster is universally compatible. | Stage 03 — both. Vector for evidence; raster for PDF rendering. |
| OQ-S3 | Pencil-vs-finger acceptance threshold: accept finger smear in Phase 2.1b? Reject as insufficient quality? | Stage 02 — accept finger; tag the event with `capture_quality: finger | pencil_basic | pencil_pro`. UX advisory only. |
| OQ-S4 | Time-source attestation: is device clock acceptable, or do we need NTP-attested timestamp service? Some compliance regimes prefer trusted timestamping (RFC 3161). | Stage 02 — Phase 2.1b ships device clock + audit log; trusted timestamp is a Phase 2.3 enhancement. |
| OQ-S5 | Identity verification of external signer (leaseholder, applicant): just email confirmation? Two-factor before allowing signature? Photo of ID + selfie? | Stage 02 — Phase 2.1b ships email confirmation only; stronger ID verification as later policy enhancement. |
| OQ-S6 | Multi-signer flow (lease with two co-tenants): all-sign-on-one-iPad, sequential email-to-each, both? | Stage 02 — Phase 2.1b ships in-person-on-one-device + email-to-second-signer flow; full DocuSign-style sequential flow Phase 2.3+. |
| OQ-S7 | PDF rendering source-of-truth: same template engine on iOS (PDFKit) and Bridge (e.g., DinkToPdf or QuestPDF)? Risk of visual drift between flows. | Stage 02 — recommend QuestPDF on Bridge, custom PDFKit on iOS, with a parity test to detect visual drift. |
| OQ-S8 | Revocation flow: a signature is revoked (e.g., signed under duress, fraudulent). Append-revocation-event to immutable signature, or hide via projection? | Stage 02 — append-only revocation event; signature event itself is immutable; projections show revoked state. |

---

## Dependencies

**Blocked by:**
- Foundation.Recovery split (workstream #15, ready-to-build) — operator device-key primitives
- ADR 0049 audit substrate (accepted; PR #190 merged) — for signature event logging
- New "Electronic signature capture & document binding" ADR

**Blocks:**
- [`property-leases-intake-2026-04-28.md`](./property-leases-intake-2026-04-28.md) — lease execution flow
- [`property-work-orders-intake-2026-04-28.md`](./property-work-orders-intake-2026-04-28.md) — completion attestations
- [`property-inspections-intake-2026-04-28.md`](./property-inspections-intake-2026-04-28.md) — move-in/out sign-off
- [`property-leasing-pipeline-intake-2026-04-28.md`](./property-leasing-pipeline-intake-2026-04-28.md) — criteria acknowledgement
- Phase 2.1b deliverable

**Cross-cutting open questions consumed:** OQ8 (criteria document versioning) from INDEX.

---

## Pipeline Variant Choice

`sunfish-feature-change` — new feature with mandatory ADR. No prior public-API contract being broken; this is additive.

---

## Cross-references

- Parent: [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
- Sibling intakes: Leases, Work Orders, Inspections, Leasing Pipeline, iOS App
- ADR 0046 (key-loss recovery) — amendment driven by this intake
- ADR 0049 (audit-trail substrate) — confirmation/amendment
- Workstream #15 (foundation-recovery split) — prerequisite

---

## Sign-off

Research session — 2026-04-28
