# Document Versioning

`Sunfish.Blocks.Leases` ships an append-only document-revision log per ADR 0054 amendment A1 + W#27 Phase 2. Every revision of a lease document is a new immutable entry; old revisions stay in the log forever for audit + signature-binding integrity.

## Why versioned

Per ADR 0054, signatures bind to **canonical document bytes** via SHA-256. If the document changes, prior signatures stop applying — they continue to attest to the bytes they were captured against, but no longer attest to the current document. A version log makes this explicit:

- Each revision has a stable `LeaseDocumentVersionId` + monotonically-increasing `VersionNumber` (1, 2, 3, …)
- Each revision carries the SHA-256 `DocumentHash` of its canonical bytes
- Each `LeasePartySignature` references the specific revision it was captured against
- The execution-transition guard requires every tenant to have signed the **latest** revision

## Entity shape

```csharp
public sealed record LeaseDocumentVersion
{
    public required LeaseDocumentVersionId Id { get; init; }
    public required LeaseId Lease { get; init; }
    public required int VersionNumber { get; init; }     // assigned on append
    public required ContentHash DocumentHash { get; init; }
    public required string DocumentBlobRef { get; init; } // tenant-key encrypted blob ref
    public required ActorId AuthoredBy { get; init; }
    public required DateTimeOffset AuthoredAt { get; init; }
    public required string ChangeSummary { get; init; }
}
```

`DocumentBlobRef` is opaque — the actual document bytes live in tenant-key-encrypted blob storage; the lease record points at them via this string.

## Append-only invariant

`ILeaseDocumentVersionLog` exposes only `AppendAsync` (no Update, no Delete). The implementation:

- Assigns a fresh `LeaseDocumentVersionId` (overwriting whatever the caller passed)
- Assigns the next per-lease `VersionNumber` (overwriting whatever the caller passed)
- Locks per-lease while assigning to prevent races

Two concurrent calls for the same lease produce two distinct revisions with sequential version numbers — the lock ensures monotonicity.

## Reading the log

Three operations:

| Method | Purpose |
|---|---|
| `ListAsync(leaseId, ct)` | Streams every revision, oldest first |
| `GetLatestAsync(leaseId, ct)` | Returns the highest-version revision (or null if no append yet) |
| `GetAsync(versionId, ct)` | Looks up a specific revision by id (cross-lease search) |

The lease record's `Lease.DocumentVersions` is an ordered list of `LeaseDocumentVersionId` references — the last entry is the latest.

## Append flow + audit

```csharp
var v1 = await leaseService.AppendDocumentVersionAsync(lease.Id, new LeaseDocumentVersion
{
    Id = default,                                     // assigned on append
    Lease = lease.Id,                                 // overwritten on append
    VersionNumber = 0,                                // assigned on append
    DocumentHash = ContentHash.ComputeFromUtf8Nfc(canonicalDocument),
    DocumentBlobRef = "blob://tenant-keys/lease-{tenant}/{lease-id}/v1",
    AuthoredBy = operatorId,
    AuthoredAt = DateTimeOffset.UtcNow,
    ChangeSummary = "Initial draft",
}, operatorId, ct);

// v1.VersionNumber is now 1
// v1.Id is a fresh Guid
// Lease.DocumentVersions now ends with v1.Id
// LeaseDocumentVersionAppended audit event was emitted
```

## ContentHash determinism

The hash is computed over **canonical bytes**, not arbitrary serialization. Use `ContentHash.ComputeFromUtf8Nfc(string)` for plain-text documents or `ContentHash.ComputeFromJson(JsonNode)` / `ContentHash.ComputeFromJsonObject<T>(T)` for structured documents — these apply Unicode NFC normalization or RFC 8785 canonicalization respectively. See the [kernel-signatures canonicalization page](../../kernel/signatures/overview.md#canonicalization-rule) for the full rule set.

PDF/A canonicalization is deferred to a downstream PDF-rendering package.

## Idempotence + concurrency

The log is **not** idempotent on `LeaseDocumentVersionId` — each append generates a fresh id. Callers wanting idempotence MUST track their own external id mapping (e.g., a per-author revision counter).

Concurrent appends for the same lease serialize via the per-lease lock + each gets a distinct version number. There is no SHA-256-based dedup — two appends of byte-identical documents produce two separate revisions (this is intentional; it preserves authoring intent in the audit trail).

## Cross-references

- [Signature Flow](./signature-flow.md) — How signatures bind to specific revisions
- [Overview](./overview.md)
- [ADR 0054 amendment A1](../../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — Canonicalization rule
