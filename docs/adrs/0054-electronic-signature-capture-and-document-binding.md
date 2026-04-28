# ADR 0054 — Electronic Signature Capture & Document Binding

**Status:** Proposed
**Date:** 2026-04-28
**Resolves:** Property-ops cluster intake [`property-signatures-intake-2026-04-28.md`](../../icm/00_intake/output/property-signatures-intake-2026-04-28.md); cluster workstream #21. Specifies the cryptographic substrate for legally-binding electronic signatures on leases, work-order completion, inspection move-in/out sign-off, and prospect criteria-acknowledgement.

---

## Context

The property-operations cluster requires electronic signatures at multiple lifecycle points: lease execution and renewal (`blocks-leases`), maintenance work-order completion attestation (`blocks-work-orders` per ADR 0053), move-in/move-out checklist sign-off (`blocks-inspections`), prospect criteria acknowledgement (`blocks-leasing-pipeline`), vendor service agreements. Each is a legally significant act with evidentiary requirements that are categorically different from the photo-blob and form-data captures handled elsewhere.

A signature is *not data about a past event*; it **creates a binding event**. That distinction drives requirements that don't apply to other captured artifacts:

1. **Cryptographic content-binding** — the signature must bind to the exact version of the document on screen at the moment of signing. Document edits later create new versions; the original signature remains valid against the original content hash but is not transferable to the edited version. The defense is a deterministic, content-addressed hash that anyone can recompute from the document content.
2. **Offline-cryptographic-self-containment** — basements have no signal. Property managers visit on-site, hand the iPad to a leaseholder, capture a signature, and walk away — possibly hours before connectivity returns. A signature event must be **fully formed when captured**: content hash, device clock timestamp, operator's device-key witness signature, signer's pen-stroke biometric data, all bundled and durable. Any dependency on a server round-trip to make the signature valid breaks the workflow.
3. **Two identities per event, not one** — every signature event has *both* an operator (the property manager holding the iPad, witnessing-via-device-key) and a signer (the leaseholder applying ink). Receipt capture has one identity; signatures have two cryptographically distinct ones. The operator's device-key signs the event payload; the signer's pen stroke is the act of signing.
4. **PDF output artifact** — signed leases produce a PDF the leaseholder takes with them. Generated on-device (iPad uses PDFKit; Bridge uses QuestPDF for non-iOS-originated flows). The PDF is *convenience evidence*; the canonical record is the SignatureEvent in the kernel ledger.
5. **UETA / E-SIGN compliance posture** — US baseline requires (a) intent to sign, (b) consent to do business electronically, (c) association with the record, (d) record retention. Items (a), (c), (d) are technical (signature-event capture + content-hash binding + audit substrate). Item (b) is one-time UX scaffolding per signer.

Concurrent dependencies: ADR 0046 (key-loss recovery) defines the device-key custody primitives the operator-as-witness signature uses. Workstream #15 (Foundation.Recovery package split) is in flight to consolidate those primitives. ADR 0049 (audit-trail substrate) is the persistence pattern. ADR 0028 (CRDT engine) determines AP/CP classification. ADR 0053 (work-order domain model) already references signatures as `SignatureEventId`. The cluster intake INDEX explicitly names this ADR as #3 in the cluster drafting order (after 0052 messaging and 0053 work-order).

---

## Decision drivers

- **Cluster cross-cutting consumer.** Five cluster modules consume signatures: Leases (#27), Work Orders (#19, ADR 0053), Inspections (#25, move-in/out), Leasing Pipeline (#22, criteria acknowledgement), iOS App (#23, capture surface).
- **Legal defensibility is binary.** Either UETA/E-SIGN-compliant or not. Substrate must structurally support compliance, not just enable it.
- **Phase 2 is on-iPad in-person.** BDFL's property business signs in-person with the leaseholder present (lease execution, maintenance sign-off, move-in/out). Async / out-of-band signing (DocuSign-style email-the-doc-and-wait) is Phase 2.3+.
- **Provider-neutrality applies even here.** ADR 0013 enforcement gate is active. Any external e-sign service (DocuSign, Adobe Sign, HelloSign) lands as a `providers-esign-*` adapter, not as the primary path. First-class signatures are Sunfish-owned.
- **iOS capture is canonical.** PencilKit + CryptoKit + PDFKit are best-in-class. SwiftUI native iOS app per cluster intake #23 is the primary capture surface.
- **Foundation.Recovery is the dependency.** Operator device-key issuance + rotation lives there (workstream #15 in flight). Signature substrate composes those primitives.
- **Audit-substrate emission is non-negotiable.** Every signature event is a first-class audit record per ADR 0049.

---

## Considered options

### Option A — Kernel-tier `kernel-signatures` substrate [RECOMMENDED]

Place the signature substrate in `packages/kernel-signatures/`, sibling to `packages/kernel-audit/` and `packages/kernel-security/`. Every accelerator gets it for free; no plugin opt-in.

- **Pro:** Cross-cutting consumer set (5 cluster modules + future); kernel-tier reflects that.
- **Pro:** Security-critical primitive — kernel-tier matches the threat-model posture of `kernel-security` and `kernel-audit`.
- **Pro:** Foundation-recovery (workstream #15) is the dependency; sibling kernel placement is architecturally clean.
- **Pro:** ADR 0053 (work-order) already assumes a `SignatureEventId` from somewhere; kernel-tier resolves the location.
- **Con:** Slight kernel-tier weight; one more package contributors must understand.

**Verdict:** Recommended.

### Option B — Block-tier `blocks-signatures` (opt-in plugin)

Place the substrate in `packages/blocks-signatures/`, opt-in per accelerator via plugin registration.

- **Pro:** Lighter kernel; clearer plugin boundary.
- **Con:** Cross-cutting nature (5 cluster modules) makes opt-in a foot-gun — accelerators that miss the registration get inconsistent UX.
- **Con:** Doesn't match the kernel-tier weight of comparable substrates (audit, security, recovery).
- **Con:** Plugin registration is overhead for what is structurally a cross-cutting concern.

**Verdict:** Rejected. Mismatched layer for a cross-cutting security-critical primitive.

### Option C — `foundation-signatures` (between kernel and blocks)

Place in `packages/foundation-signatures/`, foundation-tier.

- **Pro:** Avoids kernel-tier weight.
- **Pro:** Foundation-tier is where orchestration lives (per kernel-vs-foundation discussion in workstream #15).
- **Con:** Kernel-tier siblings (`kernel-audit`, `kernel-security`, future `kernel-signatures`) are the pattern; placing signature in a different tier from its siblings creates inconsistency.
- **Con:** Future `foundation-recovery` (workstream #15) will reference signature primitives; this would be a foundation→foundation dependency.

**Verdict:** Rejected on inconsistency grounds. The kernel-tier siblings argument wins.

---

## Decision

**Adopt Option A.** Place the signature substrate in `packages/kernel-signatures/`, sibling to `kernel-audit` and `kernel-security`. Foundation-recovery references `kernel-security` for crypto primitives and `kernel-signatures` for signature emission. The signature substrate is consumed by all 5 cluster modules without plugin opt-in.

### Initial contract surface

```csharp
namespace Sunfish.Kernel.Signatures;

// Primary entity — represents a single act of signing
public sealed record SignatureEvent
{
    public required SignatureEventId Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required ContentHash DocumentVersionHash { get; init; }      // cryptographic binding to document content
    public required IdentityRef SignerIdentity { get; init; }            // the leaseholder / vendor / prospect
    public required IdentityRef WitnessIdentity { get; init; }           // operator (BDFL / spouse / contractor) holding the device
    public required PenStrokeBlobRef PenStroke { get; init; }            // PencilKit data (vector + timing) + raster fallback
    public required DateTimeOffset CapturedAt { get; init; }             // device-clock at capture
    public required ClockSource CapturedAtSource { get; init; }
    public required DeviceAttestation DeviceAttestation { get; init; }   // operator device-key signature over event payload
    public required CaptureQuality CaptureQuality { get; init; }
    public required ConsentRecordId ConsentReference { get; init; }      // proves UETA/E-SIGN consent was granted prior
    public Geolocation? OptionalGeolocation { get; init; }               // GPS at capture (with permission)
    public required string DocumentScopeRef { get; init; }                // opaque ref to lease/work-order/inspection/criteria-doc
    public required SignatureScope Scope { get; init; }                   // type discriminator
}

public readonly record struct SignatureEventId(Guid Value);

public enum SignatureScope
{
    Lease,                    // blocks-leases
    WorkOrderCompletion,      // blocks-work-orders (ADR 0053)
    InspectionMoveIn,         // blocks-inspections
    InspectionMoveOut,        // blocks-inspections
    CriteriaAcknowledgement,  // blocks-leasing-pipeline
    Other                     // future scopes; tag in DocumentScopeRef
}

// Content-addressed binding
public readonly record struct ContentHash(string HexValue)
{
    // SHA-256 over the canonical bytes of the document content.
    // Canonical bytes are deterministic — same content always produces the same hash.
    // Hash is recomputable by any party with access to the document content.
    public static ContentHash Compute(ReadOnlySpan<byte> content) => /* SHA-256 wrapper */;
    public static ContentHash Compute(string utf8Content) => /* UTF-8 then SHA-256 */;
}

// Pen-stroke storage (vector + raster)
public sealed record PenStrokeBlobRef
{
    public required string VectorBlobRef { get; init; }       // PencilKit JSON (timing + pressure + tilt for Apple Pencil)
    public required string RasterBlobRef { get; init; }        // PNG of the rendered signature
    public required PenStrokeFidelity Fidelity { get; init; }
}

public enum PenStrokeFidelity
{
    Finger,                   // capacitive finger smear; lowest fidelity but legally sufficient
    PencilBasic,              // any stylus; basic timing data
    PencilPro                 // Apple Pencil with pressure + tilt + timing
}

public enum CaptureQuality
{
    Acceptable,               // any captured signature; passes UETA baseline
    HighFidelity              // Pencil-pro + multi-stroke + timing-coherent
}

// Operator-as-witness attestation
public sealed record DeviceAttestation
{
    public required string OperatorDeviceKeyId { get; init; }            // resolves to operator identity via Foundation.Recovery
    public required string SignatureBytes { get; init; }                  // operator's Ed25519 signature over canonical event payload
    public required string CanonicalPayloadHash { get; init; }            // SHA-256 of canonical bytes; what was signed
    public required string Algorithm { get; init; }                       // "Ed25519" for Phase 2; algorithm-agility per ADR 0004
}

// Consent record (UETA/E-SIGN compliance)
public sealed record ConsentRecord
{
    public required ConsentRecordId Id { get; init; }
    public required TenantId Tenant { get; init; }
    public required IdentityRef SignerIdentity { get; init; }
    public required ContentHash ConsentDisclosureVersionHash { get; init; }   // versioned consent text content-bound
    public required DateTimeOffset GrantedAt { get; init; }
    public required string GrantingChannel { get; init; }                      // "in-person-tap-on-iPad", "web-checkbox", "email-confirm-link"
}

public readonly record struct ConsentRecordId(Guid Value);

// Clock source (for time-attestation rigor)
public enum ClockSource
{
    DeviceClock,                          // iPad device clock; default Phase 2.1b
    DeviceClockWithNtpAttestation,        // device clock + recent NTP sync verification
    TrustedTimestamp                      // RFC 3161 trusted-timestamp service; Phase 2.3+ enhancement
}

// Geolocation (optional metadata; PII)
public sealed record Geolocation
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double AccuracyMeters { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
}

// Primary service contract
public interface IDocumentSigningService
{
    Task<SignatureCaptureResult> CaptureAsync(
        SignatureCaptureRequest request,
        CancellationToken ct);

    Task<SignatureVerificationResult> VerifyAsync(
        SignatureEventId signature,
        ContentHash currentDocumentHash,
        CancellationToken ct);

    Task<ConsentRecord> RecordConsentAsync(
        ConsentGrantRequest request,
        CancellationToken ct);
}

public sealed record SignatureCaptureRequest
{
    public required TenantId Tenant { get; init; }
    public required ContentHash DocumentVersionHash { get; init; }
    public required IdentityRef SignerIdentity { get; init; }
    public required IdentityRef WitnessIdentity { get; init; }
    public required PenStrokeBlobRef PenStroke { get; init; }
    public required SignatureScope Scope { get; init; }
    public required string DocumentScopeRef { get; init; }
    public ConsentRecordId? ExistingConsent { get; init; }                // if signer already consented
    public Geolocation? OptionalGeolocation { get; init; }
}

public sealed record SignatureCaptureResult
{
    public required SignatureEventId SignatureId { get; init; }
    public required SignatureCaptureOutcome Outcome { get; init; }
    public ConsentRecordId? PendingConsentRecord { get; init; }           // if consent must be captured first
}

public enum SignatureCaptureOutcome
{
    Captured,                                   // signature event persisted; ready
    ConsentRequired,                            // signer has no ConsentRecord yet; capture flow paused
    DocumentVersionMismatch,                    // hash didn't match; document changed mid-flow
    OperatorDeviceKeyUnavailable,               // operator's device can't sign (Foundation.Recovery state)
    PenStrokeQualityInsufficient                // fingertip on a low-DPI device; reject
}

public sealed record SignatureVerificationResult
{
    public required bool IsValid { get; init; }
    public required SignatureValidityReason Reason { get; init; }
    public required IdentityRef VerifiedSigner { get; init; }
    public required IdentityRef VerifiedWitness { get; init; }
    public required ContentHash BoundDocumentHash { get; init; }          // hash the signature actually binds
}

public enum SignatureValidityReason
{
    SignatureValid,
    DocumentHashMismatch,                       // current document differs from signed version
    WitnessKeyRevoked,                          // operator's device key was rotated/revoked AFTER signing
    SignatureCorrupted,                         // event payload tampered
    Revoked                                     // signature explicitly revoked (see Revocation section)
}
```

(Schema sketch only; XML doc + nullability + `required` enforced at Stage 06.)

### Substrate / layering notes

```
blocks-leases          blocks-work-orders          blocks-inspections          blocks-leasing-pipeline
        ↓ depends on (contracts only)
    Sunfish.Kernel.Signatures
        ↑ implemented by
    InMemoryDocumentSigningService (kernel default for tests/demos)
    iOS PencilKit-backed implementation (in accelerators/anchor-mobile-ios)
    Bridge QuestPDF-backed implementation (for non-iOS-originated flows)
        ↓ uses
    Sunfish.Kernel.Security (Ed25519 signing primitives)
    Sunfish.Foundation.Recovery (operator device-key custody — workstream #15)
    Sunfish.Kernel.Audit (audit emission per ADR 0049)
```

### Audit-substrate integration (ADR 0049)

Every signature lifecycle action emits a typed audit record:

| Audit record type | Emitted on |
|---|---|
| `ConsentRecorded` | `RecordConsentAsync` succeeds |
| `SignatureCaptured` | `CaptureAsync` succeeds (`Outcome = Captured`) |
| `SignatureCaptureRejected` | `CaptureAsync` returns Reject outcome |
| `SignatureVerified` | `VerifyAsync` succeeds (logs result) |
| `SignatureRevoked` | Revocation event |

Audit records carry: SignatureEventId + SignerIdentity (redacted) + WitnessIdentity + DocumentScopeRef + DocumentVersionHash. Pen-stroke blob is referenced, not stored in audit; raw blob is in tenant-key-encrypted blob store.

### Revocation flow (append-only)

A signature is revoked (signed under duress, fraudulent, post-hoc agreement to nullify) via an append-only `SignatureRevokedEvent` written to the audit substrate. The original SignatureEvent record is **immutable** — never deleted, never modified. Projections compute current validity by reading the audit stream. This preserves the legal trail of what was signed when, plus the legal trail of what was later revoked and why.

### Compliance posture: UETA / E-SIGN baseline

Federal E-SIGN (15 U.S.C. § 7001) + state-level UETA (adopted in 49 of 50 states) require four elements; substrate maps:

| Required element | Substrate provides |
|---|---|
| **Intent to sign** | `PenStroke` capture in PencilKit canvas with explicit "Tap to sign" affordance — the act of stroking the canvas after a tap is the intent |
| **Consent to do business electronically** | `ConsentRecord` with content-hash-bound disclosure version + grant timestamp |
| **Association with the record** | `DocumentVersionHash` cryptographically binds the signature to the exact document content; `DocumentScopeRef` identifies the lease/work-order/etc. |
| **Record retention** | ADR 0049 audit substrate (immutable append-only) + tenant-key-encrypted blob store; retention policy per tenant config (default 10 years for legal artifacts) |

Compliance is structural, not policy — the substrate cannot accidentally produce a non-compliant signature because every required element is a non-nullable contract field.

### What this ADR does NOT do

- **Does not define notarization** (notary-witnessed signatures with raised seal). Phase 4+; separate workflow class.
- **Does not define DocuSign/Adobe Sign/HelloSign integration.** If external e-sign integration becomes a requirement, it lands as `providers-esign-*` adapter — not the primary path.
- **Does not define multi-signer sequential flows** (lease with two co-tenants signing on different days via email link). Phase 2.3+. First-slice handles in-person + email-to-second-signer only.
- **Does not define biometric authentication of signer** (Face ID before allowing signature). UX enhancement; defer.
- **Does not define forensic PDF tampering detection.** PDFs are convenience artifacts; canonical evidence is the SignatureEvent.
- **Does not define document storage.** Each consuming block (`blocks-leases`, `blocks-work-orders`, etc.) owns its document storage. This ADR provides the binding mechanism, not the documents.
- **Does not define jurisdiction-specific signature requirements** (e.g., mandatory wet-signature for real-estate transfers in some states). Out of property-management scope; flag at consumer-block design if relevant.

---

## Consequences

### Positive

- 5 cluster modules unblock simultaneously on acceptance (Leases, Work Orders Phase 2, Inspections move-in/out, Leasing Pipeline, iOS App).
- UETA/E-SIGN compliance is structural — substrate cannot produce non-compliant signatures.
- Content-binding is cryptographic — a leaseholder cannot dispute "I didn't sign *this version*" because the hash is recomputable.
- Operator-as-witness device attestation provides a second cryptographic identity per signature; stronger evidence than single-signer schemes.
- Offline-self-contained capture — basement scenarios work; sync when connectivity returns.
- Append-only revocation preserves the legal trail without erasing history.
- iOS PencilKit + CryptoKit composition is best-in-class; substrate matches what iOS provides natively.
- Foundation.Recovery integration (workstream #15) provides operator device-key primitives without bespoke crypto.

### Negative

- Kernel-tier weight: one additional package every accelerator inherits.
- Pen-stroke blob storage adds blob-store load; PencilKit vector data can be 5–50KB per signature.
- 5 audit record types add to ADR 0049 substrate's vocabulary.
- Foundation.Recovery dependency means signature substrate ships *after* workstream #15 — sequencing constraint.
- iOS-first capture means non-iOS accelerators (Anchor desktop MAUI on Mac/Windows, web-only Bridge) need their own capture-flow implementation; first-slice ships iOS only, others get fallback "type your name" web flow.
- Multi-signer sequential flows are explicitly out-of-scope for first-slice; some lease workflows (co-tenants on different schedules) need the Phase 2.3+ enhancement.

### Trust impact / Security & privacy

- **Pen stroke is biometric data.** Stroked-on-glass + timing/pressure metadata is identifying. Tenant-key-encrypted at rest; never in audit projections.
- **Content-hash binding is cryptographic.** Document mutation breaks signature validity by design.
- **Operator device key is the second identity.** Loss of the operator's device-key invalidates *future* signing capability but does NOT invalidate already-captured signatures (per ADR 0046 amendment below).
- **Geolocation is sensitive PII.** Optional, with one-time-permission UX. Captured at signature time + signature time only; not continuous tracking.
- **Consent record is per-signer, one-time.** Signer's first signature event triggers consent capture; subsequent signatures reference the existing ConsentRecord by ID.
- **Revocation is append-only.** A signature event is never deleted; revocation creates a new audit-trail event. Preserves legal history.

---

## Compatibility plan

### Existing callers / consumers

No production code references signatures today. ADR 0053 (work-order, Proposed) references `SignatureEventId` as a forward-compat reservation; this ADR ships the type. Updates to ADR 0053 references happen automatically once both are Accepted.

### Affected packages (new + modified)

| Package | Change |
|---|---|
| `packages/kernel-signatures` (new) | **Created** — primary deliverable: SignatureEvent + ConsentRecord + IDocumentSigningService + ContentHash + audit emission |
| `packages/kernel-audit` (existing) | **Modified** — adds 5 typed audit record subtypes (ConsentRecorded, SignatureCaptured, SignatureCaptureRejected, SignatureVerified, SignatureRevoked) |
| `packages/kernel-security` (existing) | **Modified** — exposes Ed25519 primitives the signature substrate uses (already exposed; verify) |
| `packages/foundation-recovery` (workstream #15) | **Eventual integration** — operator device-key issuance + rotation. Not blocking; signature substrate ships with placeholder device-key resolver until #15 lands. |
| `packages/blocks-leases` (cluster intake #27) | **Eventual consumer** |
| `packages/blocks-work-orders` (ADR 0053) | **Eventual consumer** — completion attestation |
| `packages/blocks-inspections` (cluster intake #25) | **Eventual consumer** — move-in/out sign-off |
| `packages/blocks-leasing-pipeline` (cluster intake #22) | **Eventual consumer** — criteria acknowledgement |
| `accelerators/anchor-mobile-ios` (cluster intake #23) | **Eventual consumer** — primary capture surface (PencilKit + CryptoKit + PDFKit) |
| `accelerators/bridge` (existing) | **Modified** — server-side signature flow (QuestPDF; non-iOS-originated paths) |

### ADR amendments triggered by this ADR

1. **ADR 0046 (key-loss recovery) amendment.** Outstanding signature commitments survive operator device-key rotation. Recovery affects only the ability to issue *new* signatures going forward; historical signatures remain valid against the original (now-rotated) device key. Amendment adds a `historical_keys[]` projection to recovery state for verification of old signatures.
2. **ADR 0049 (audit-trail substrate) confirmation.** 5 new audit record subtypes added per the table above. No structural change to audit substrate; new vocabulary only.
3. **ADR 0053 (work-order) implicit confirmation.** This ADR provides the `SignatureEventId` ADR 0053 reserves; no edit needed to ADR 0053 once both are Accepted.

---

## Implementation checklist

- [ ] `packages/kernel-signatures/` scaffolded; references `kernel-security` + `kernel-audit`
- [ ] `SignatureEvent`, `SignatureEventId`, `ConsentRecord`, `ConsentRecordId`, `ContentHash`, `PenStrokeBlobRef`, `DeviceAttestation`, `Geolocation`, `SignatureScope`, `PenStrokeFidelity`, `CaptureQuality`, `ClockSource` types defined; full XML doc; nullability + `required` enforced
- [ ] `IDocumentSigningService` + `SignatureCaptureRequest` + `SignatureCaptureResult` + `SignatureCaptureOutcome` + `SignatureVerificationResult` + `SignatureValidityReason` types
- [ ] `InMemoryDocumentSigningService` reference implementation in `kernel-signatures` for tests/demos (uses in-memory storage; mock device-key)
- [ ] 5 audit record types added to `Sunfish.Kernel.Audit` per ADR 0049
- [ ] ADR 0046 amendment authored + accepted (signature survival under key rotation)
- [ ] ADR 0049 audit subtype additions accepted
- [ ] iOS PencilKit-backed implementation in `accelerators/anchor-mobile-ios/` (gated on workstream #23 hand-off)
- [ ] Bridge server-side QuestPDF-backed implementation (for non-iOS-originated flows; e.g., criteria acknowledgement on a web page)
- [ ] kitchen-sink demo: lease signing flow simulated (Mac Catalyst or WebView shim showing the iOS flow); criteria acknowledgement on a Bridge web page
- [ ] PDF generation: signed PDF generated on-device with embedded signature image; visual rendering parity with Bridge-side
- [ ] Document-binding integrity test: SignatureEvent verified valid against original document content; verified invalid against modified content
- [ ] Audit trail: every SignatureEvent appears in kernel-audit log
- [ ] apps/docs entry covering signature lifecycle, document binding, PDF generation, UETA/E-SIGN compliance posture, and consumer integration patterns

---

## Open questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-S1 | Pen stroke vector format. PencilKit JSON is iOS-specific; cross-platform interchange? | Stage 02. Recommend: store PencilKit JSON when captured on iOS; rely on raster for cross-platform render; Bridge web-flow uses HTML5 Canvas + custom JSON serialization. |
| OQ-S2 | Pencil-vs-finger acceptance. Reject finger smear (CaptureQuality = `PenStrokeQualityInsufficient`)? | Stage 02. Recommend ACCEPT finger; tag `PenStrokeFidelity.Finger`; UX advisory only ("for stronger evidence, use a stylus"). UETA accepts finger smears. |
| OQ-S3 | Time-source attestation. Device clock vs NTP-attested vs trusted-timestamp service (RFC 3161). | Stage 02 — Phase 2.1b ships `ClockSource.DeviceClock`; trusted timestamp Phase 2.3+ enhancement. |
| OQ-S4 | Identity verification of external signer (leaseholder, applicant, vendor). Email-confirm vs 2FA vs photo-of-ID + selfie. | Stage 02 — Phase 2.1b ships email-confirmation only; stronger ID verification (KYC-class) is Phase 2.3+. |
| OQ-S5 | Multi-signer sequential flow (lease with 2 co-tenants on different schedules). | Phase 2.3 follow-on ADR. First-slice handles only in-person + email-to-second-signer. |
| OQ-S6 | PDF rendering parity between iOS PDFKit and Bridge QuestPDF. Visual drift detection. | Stage 02 — recommend Stage 06 parity test comparing rendered byte streams (with provider-specific headers excluded). |
| OQ-S7 | Multi-signer cardinality: single SignatureEvent with multiple signers, or N SignatureEvents per document version. | Stage 02 — recommend N events per signer; consumer block (Leases) computes "all required signers signed" projection. |
| OQ-S8 | Revocation governance: who can revoke a signature? | Stage 02 — recommend: signer can revoke their own; tenant-admin can revoke any; revocation is permanent (no un-revoke); audit trail captures revoking-actor. |
| OQ-S9 | Disaster recovery: if entire tenant DB is lost but PDFs are preserved, can signatures be re-verified? | No — substrate is the source of truth; PDF is convenience evidence. Disaster recovery uses backup of audit substrate + blob store. |

---

## Revisit triggers

This ADR should be re-evaluated when any of the following fire:

- **Multi-signer sequential / async flows** become a real requirement (Phase 2.3+).
- **Notarization workflow** required (raised seal, notary-witnessed).
- **Algorithm-agility crisis** — Ed25519 deprecated or compromised; substrate must support algorithm rotation per ADR 0004.
- **Jurisdiction-specific signature requirements** (e.g., wet-signature mandatory for real-estate transfers) become in-scope.
- **External e-sign integration** required (DocuSign / Adobe Sign / HelloSign) — `providers-esign-*` adapter ADR.
- **Forensic dispute** in production reveals a signature evidence gap not captured by current substrate.
- **Revocation flow** abused as un-do (governance failure) — tighten governance per OQ-S8 resolution.
- **PDF rendering drift** between iOS PDFKit and Bridge QuestPDF degrades evidence portability.

---

## References

### Predecessor and sister ADRs

- [ADR 0004](./0004-post-quantum-signature-migration.md) — Algorithm-agility for crypto primitives this substrate uses.
- [ADR 0008](./0008-foundation-multitenancy.md) — Per-tenant scope.
- [ADR 0013](./0013-foundation-integrations.md) — Provider-neutrality (governs future `providers-esign-*` adapters).
- [ADR 0028](./0028-crdt-engine-selection.md) — AP/CP classification (signature events are AP append-only).
- [ADR 0043](./0043-unified-threat-model-public-oss-chain-of-permissiveness.md) — Threat model.
- [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — Key-loss recovery; **amendment driven by this ADR** for signature survival.
- [ADR 0049](./0049-audit-trail-substrate.md) — Audit substrate; 5 new event types.
- [ADR 0051](./0051-foundation-integrations-payments.md) — Sibling Proposed ADR.
- [ADR 0052](./0052-bidirectional-messaging-substrate.md) — Sibling Proposed ADR; criteria-document delivery uses messaging substrate.
- [ADR 0053](./0053-work-order-domain-model.md) — Sibling Proposed ADR; references `SignatureEventId` shipped here.

### Roadmap and specifications

- [Property-ops cluster INDEX](../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md) — pins ADR drafting order; signatures is #3.
- [Signatures cluster intake](../../icm/00_intake/output/property-signatures-intake-2026-04-28.md) — Stage 00 spec source.
- [Phase 2 commercial intake](../../icm/00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md) — vendor service agreements consume substrate.

### Existing code / substrates

- `packages/kernel-audit/` — audit substrate consumer.
- `packages/kernel-security/` — Ed25519 primitives (sibling kernel-tier).
- `packages/foundation-recovery/` — operator device-key custody (workstream #15 in flight).

### External

- E-SIGN Act (15 U.S.C. § 7001) — federal baseline.
- UETA — state-level adoption (49 of 50 states).
- RFC 3161 — Time-Stamp Protocol (Phase 2.3+ trusted timestamping).
- iOS PencilKit framework — primary capture surface.
- Apple CryptoKit — Ed25519 + Secure Enclave-backed signing on iOS.
- iOS PDFKit framework — on-device PDF generation.
- QuestPDF — server-side PDF generation library for Bridge.

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options considered: kernel-tier (A), block-tier (B), foundation-tier (C). Option A chosen with explicit rejection rationale for B (mismatched layer) and C (inconsistency with kernel-tier siblings).
- [x] **FAILED conditions / kill triggers.** 8 revisit triggers explicit; tied to externally-observable signals.
- [x] **Rollback strategy.** No production code consumes signatures today (placeholder reference in ADR 0053 only). Rollback = revert this ADR + revert `kernel-signatures` package + revert 5 audit subtype additions in kernel-audit.
- [x] **Confidence level.** **HIGH.** Composes well-understood substrates (audit + security + recovery). UETA/E-SIGN compliance is well-established. Risk surface is in long-tail UX (multi-signer flows, multi-jurisdiction wet-signature exceptions) — covered by revisit triggers + Phase 2.3+ deferred work.
- [x] **Anti-pattern scan.** None of AP-1, AP-3, AP-9, AP-12, AP-21 from `.claude/rules/universal-planning.md` apply. Substrate composition explicit; phases observable; sources cited (E-SIGN, UETA, RFC 3161).
- [x] **Revisit triggers.** 8 conditions with externally-observable signals.
- [x] **Cold Start Test.** Implementation checklist is 13 specific tasks. Fresh contributor reading this ADR + signatures cluster intake + ADR 0049 + ADR 0046 + ADR 0028 should be able to scaffold `kernel-signatures` without asking for clarification.
- [x] **Sources cited.** ADR 0004, 0008, 0013, 0028, 0043, 0046, 0049, 0051, 0052, 0053 referenced. E-SIGN + UETA cited. PencilKit + CryptoKit + PDFKit + QuestPDF cited as concrete impl targets. RFC 3161 for trusted-timestamp (deferred).
