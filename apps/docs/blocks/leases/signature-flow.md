# Signature Flow

`Sunfish.Blocks.Leases` v1.x ships the multi-party signature collection per ADR 0054 + W#27 Phases 2 + 3. Every signature is a **kernel-signatures** capture event referenced by id; the lease record stores only the references + a per-tenant binding to a specific document revision.

## Two distinct signature kinds

| Kind | Where stored | Purpose |
|---|---|---|
| **Per-party signature** | `Lease.PartySignatures` (list of `LeasePartySignature`) | Each tenant signs a specific `LeaseDocumentVersion` |
| **Landlord attestation** | `Lease.LandlordAttestation` (`SignatureEventId?`) | Operator's attestation, distinct from per-party signatures |

Both reference `Sunfish.Kernel.Signatures.Models.SignatureEventId` from the kernel-signatures substrate (W#21).

## Capture flow

```text
1. Operator authors revision v1                  (AppendDocumentVersionAsync)
                ↓
2. Lease.DocumentVersions = [ v1 ]
                ↓
3. Tenant Alice signs v1 via ISignatureCapture   (kernel-signatures)
                ↓
4. RecordPartySignatureAsync(lease, alice, sigA) (binds to latest version)
                ↓
5. Tenant Bob signs v1 → RecordPartySignatureAsync
                ↓
6. Operator attests → SetLandlordAttestationAsync
                ↓
7. TransitionPhaseAsync(AwaitingSignature → Executed)  // guard passes
```

Each step emits its own audit event:

| Step | Audit event |
|---|---|
| 1 | `LeaseDocumentVersionAppended` |
| 4, 5 | `LeasePartySignatureRecorded` (one per tenant) |
| 6 | `LeaseLandlordAttestationSet` |
| 7 | `LeaseExecuted` |

## AwaitingSignature → Executed transition guard

The `Lease.TransitionPhaseAsync` method enforces, when at least one document version exists:

1. Every entry in `Lease.Tenants` has a `LeasePartySignature` whose `DocumentVersion` matches the **latest** entry in `Lease.DocumentVersions`.
2. `Lease.LandlordAttestation` is non-null.

Failures throw `InvalidOperationException` with a specific reason — naming the missing tenant or attestation.

**Legacy bypass:** when `Lease.DocumentVersions` is empty (the lease was authored without the version-tracking flow), the guard is skipped. This preserves backward compat with the pre-Phase-2 simplified flow used in tests + kitchen-sink demos.

## What happens on revision

When the operator appends a new revision v2 after a tenant has signed v1:

```text
Initial:  v1 → Alice signed v1
                ↓
Append v2 → DocumentVersions = [ v1, v2 ]
                ↓
Alice's existing signature still references v1.
Latest is v2. Guard rejects: "Alice has not signed the latest document version 'v2'."
                ↓
Alice signs v2 → RecordPartySignatureAsync(lease, alice, sigA2)
                ↓
PartySignatures contains both sigA-on-v1 + sigA-on-v2 (append-only).
                ↓
Guard now accepts (Alice's latest signature is on v2).
```

Old signatures are preserved for audit forever — the guard just looks for "any signature on the latest version."

## Code example

```csharp
var lease = await leaseService.CreateAsync(new CreateLeaseRequest { /* ... */ }, ct);

// 1. Author the document
var v1 = await leaseService.AppendDocumentVersionAsync(lease.Id, new LeaseDocumentVersion
{
    Id = default,                                   // assigned on append
    Lease = lease.Id,
    VersionNumber = 0,                              // assigned on append
    DocumentHash = ContentHash.ComputeFromUtf8Nfc(documentBody),
    DocumentBlobRef = "blob://tenant-keys/lease-v1",
    AuthoredBy = operatorId,
    AuthoredAt = DateTimeOffset.UtcNow,
    ChangeSummary = "Initial draft",
}, operatorId, ct);

// 2. Move to AwaitingSignature
await leaseService.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, operatorId, ct);

// 3. Each tenant signs via kernel-signatures ISignatureCapture upstream
foreach (var tenant in lease.Tenants)
{
    var sigEvent = await signatureCapture.CaptureAsync(/* lease-execution scope */, ct);
    await leaseService.RecordPartySignatureAsync(lease.Id, tenant, sigEvent.Id, operatorId, ct);
}

// 4. Landlord attests
var landlordSig = await signatureCapture.CaptureAsync(/* lease-execution scope */, ct);
await leaseService.SetLandlordAttestationAsync(lease.Id, landlordSig.Id, operatorId, ct);

// 5. Move to Executed (guard enforces all signatures + attestation)
var executed = await leaseService.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, operatorId, ct);
```

## Cross-package wiring

| Wiring | Provider | Consumer |
|---|---|---|
| `ISignatureCapture` | `kernel-signatures` (W#21) | callers of `RecordPartySignatureAsync` / `SetLandlordAttestationAsync` |
| `ITaxonomyResolver` (`Sunfish.Signature.Scopes@1.0.0`) | `foundation-taxonomy` (W#31) | upstream signature-capture path validates scope |
| `IAuditTrail` + `IOperationSigner` | `kernel-audit` + `foundation-crypto` | wired via `InMemoryLeaseService` 3-arg/4-arg ctor |
| `ILeaseDocumentVersionLog` | `blocks-leases` (this package) | injected via 4-arg ctor |

## See also

- [Document Versioning](./document-versioning.md)
- [Overview](./overview.md)
- [ADR 0054](../../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — Electronic signatures
- [Kernel.Signatures](../../kernel/signatures/overview.md) — Signature substrate
