# Integration Guide — `kernel-signatures`

How a consumer block (Lease, WorkOrder, Inspection, Adverse-Action Notice) integrates with the kernel-signatures substrate.

## Storage shape — what a consumer record holds

Consumers do NOT store the full `SignatureEvent`. They store a **reference** + delegate fact-of-the-matter to the substrate. Two reference shapes exist today:

| Reference type | Where defined | Used by |
|---|---|---|
| `SignatureEventRef` | `Sunfish.Foundation.Integrations.Signatures` (W#19 Phase 0 stub) | `WorkOrderCompletionAttestation`, `Application.ApplicationSignature`, `AdverseActionNotice.NoticeIssuanceSignature`, `LeaseOffer` (future) |
| `SignatureEventId` | `Sunfish.Kernel.Signatures.Models` (W#21 Phase 1) | Internal to kernel-signatures; the canonical identifier |

Both wrap a `Guid`. The foundation-integrations stub (`SignatureEventRef`) was introduced before kernel-signatures shipped + is preserved for backward-compat. New consumer types should use `SignatureEventRef` for the foundation-integrations stub OR consume kernel-signatures directly when they need richer functionality (validity projection, revocation lookup, scope inspection).

## Capture flow at a consumer block

```csharp
// 1. Consumer collects the canonical document bytes for the thing being
//    signed — this is the binding proof.
var documentHash = ContentHash.ComputeFromJsonObject(leaseDocument);

// 2. Consumer chooses one or more signature scopes from
//    Sunfish.Signature.Scopes@1.0.0.
var scopes = new[]
{
    new TaxonomyClassification
    {
        Definition = new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes"),
        Code = "lease-execution",
        Version = TaxonomyVersion.V1_0_0,
    },
};

// 3. Consumer chooses an algorithm-agility envelope. Phase 0 stub
//    accepts any algorithm string; ADR 0004 Stage 06 will tighten.
var envelope = new SignatureEnvelope(
    Algorithm: "ed25519",
    Signature: signedBytes,
    Headers: new Dictionary<string, string> { ["kid"] = keyId });

// 4. Capture.
var captured = await capture.CaptureAsync(new SignatureCaptureRequest
{
    Tenant = tenant,
    Signer = signer,
    Consent = currentConsentId,    // upstream consent flow
    DocumentHash = documentHash,
    Scope = scopes,
    Envelope = envelope,
    Quality = new CaptureQuality { /* clock + device + UETA review flags */ },
}, ct);

// 5. Consumer stores ONLY the reference.
var leaseOffer = leaseOffer with { ApplicationSignature = new SignatureEventRef(captured.Id.Value) };
```

## Verifying a stored signature

When a consumer needs to verify that a stored reference still represents a valid signature (e.g., before honoring a lease at execution time):

```csharp
var status = await revocationLog.GetCurrentValidityAsync(new SignatureEventId(leaseOffer.ApplicationSignature.Value.SignatureEventId), ct);
if (!status.IsValid)
{
    // Refuse the operation; surface status.RevokedBy for audit-trail diagnostics.
}
```

The substrate emits `SignatureValidityProjected` on every query — production callers may filter at the audit-trail layer if the volume is too noisy.

## Revoking a signature

```csharp
await revocationLog.AppendAsync(new SignatureRevocation
{
    Id = new RevocationEventId(Guid.NewGuid()),
    SignatureEvent = capturedSignature.Id,
    RevokedAt = DateTimeOffset.UtcNow,
    RevokedBy = operatorActorId,
    Reason = RevocationReason.SignerRequest,
    Note = "Pre-execution withdrawal during cooling-off window",
}, ct);
```

Revocations are append-only + idempotent on duplicate `RevocationEventId`. Concurrent revocations from offline devices converge deterministically when their logs sync (last-revocation-wins; ties broken by total-order on Guid).

## Composition example

Full wiring of all pieces — consent + scope-validation + audit:

```csharp
var auditTrail = serviceProvider.GetRequiredService<IAuditTrail>();
var signer = serviceProvider.GetRequiredService<IOperationSigner>();
var taxonomyResolver = serviceProvider.GetRequiredService<ITaxonomyResolver>();
var tenantId = ...;

var emitter = new SignatureAuditEmitter(auditTrail, signer, tenantId);
var consents = new InMemoryConsentRegistry(emitter, time: null);
var scopeValidator = new InMemorySignatureScopeValidator(taxonomyResolver);
var capture = new InMemorySignatureCapture(
    consents,
    scopeValidator,
    emitter,
    time: null);
var revocationLog = new InMemorySignatureRevocationLog(emitter, time: null);
```

## Forensic reconstruction

Auditors needing to reconstruct a signing event reach for the following:

| Fact | Source |
|---|---|
| Who signed | `SignatureEvent.Signer` |
| When | `SignatureEvent.SignedAt` + `Quality.ClockSource` (assurance level) |
| What was signed | `SignatureEvent.DocumentHash` (compare against canonical re-hash of the stored document) |
| What scope | `SignatureEvent.Scope` (taxonomy classifications) |
| Algorithm | `SignatureEvent.Envelope.Algorithm` |
| Where (optional) | `SignatureEvent.Location` |
| Device proof (optional) | `SignatureEvent.Attestation` |
| Currently valid? | `ISignatureRevocationLog.GetCurrentValidityAsync` |
| Audit chain | `IAuditTrail.QueryAsync` filtered to `SignatureCaptured` / `SignatureRevoked` |

## See also

- [Overview](./overview.md)
- [ADR 0054](../../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md)
