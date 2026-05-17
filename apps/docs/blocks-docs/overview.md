# blocks-docs

Document-attachment substrate for Sunfish. The canonical home for files uploaded into the system: invoice PDFs, lease addenda, inspection photos, signed work-order acknowledgments, scanned receipts. Owns the catalog row + the cross-cluster join table that links each blob to whichever entity in whichever cluster owns the upload.

## Why this block exists

Every cluster in Sunfish has its own "attach a file" need. Without a canonical attachments home, each cluster re-implements MIME validation, content-hash dedup, tenant isolation, sanitization, and orphan GC. Worse, the same uploaded file (e.g., an insurance certificate referenced by both a lease and a work order) ends up duplicated as two separate blob payloads with no shared lifecycle. This cluster solves all of that once.

## Domain shape

```text
Attachment (1) ── (0..N) DocumentRef ── (1) <ParentEntity in some other cluster>
Attachment (1) ── (0..1) Attachment   (Superseded chain via ReplacesAttachmentId)
Attachment (1) ── (1) StorageRef       (Inline | FoundationBlob | External — discriminated)
```

## Entities

| Entity | Shape |
|---|---|
| **`Attachment`** | `AttachmentId`, `TenantId`, `StorageRef`, `ContentHash` (sha-256 lowercase hex), `MimeType` (server-sniffed), `SizeBytes`, `OriginalFilename` (sanitized), `Sensitivity`, `Status`, `ReplacesAttachmentId?`, `ReplacedByAttachmentId?`, CRDT envelope. |
| **`DocumentRef`** | `DocumentRefId`, `TenantId`, `AttachmentId`, `ClusterCode` (e.g. `"blocks-financial-ar"`), `ParentEntityType` (e.g. `"invoice"`), `ParentEntityId`, `AttachmentRole?` (cluster-defined vocabulary), CRDT envelope. |
| **`StorageRef`** | Discriminated union: `Inline` (≤8KB blob in catalog row) / `FoundationBlob` (content-addressed in tenant CAS) / `External` (URI pointer). |

## Status lifecycle (Attachment)

| Status | Meaning |
|---|---|
| `Active` | Live, addressable, counts toward tenant quota. |
| `Superseded` | Replaced by a newer Attachment via `Attachment.SupersedeAsync`. Off the quota path. |
| `Tombstoned` | Soft-deleted by user, parent reconciler, or orphan GC. Off the quota path; bytes reclaimed by downstream FoundationBlob pass past retention. |

## Three-gate upload policy (PR 3 — security-engineering council)

Every `IAttachmentService.UploadAsync` (and `SupersedeAsync` — SE-1 council blocker) call runs through this defense-in-depth pipeline before bytes touch the catalog:

1. **`MimeSniffer`** inspects the first 512 bytes of the payload. Caller's MIME hint is dropped. The persisted MIME is always the sniffed value. Unknown sniff falls back to `application/octet-stream` (`MimeSniffer.UnknownMime`); the policy then rejects unless the tenant explicitly whitelists that sentinel.
2. **`FilenameSanitizer`** strips path components, rejects control chars + Windows reserved device names (with trailing-whitespace + multi-extension variants closed per SE-3), strips bidi-override + zero-width chars, caps at 255. Rejection → service falls back to `attachment.bin`.
3. **`IMimeTypeAndSizePolicy`** enforces, in order:
    - **System blacklist** (SE-2): 8 MIMEs no tenant whitelist can re-enable (HTML, JS, executables, octet-stream, Flash). Pre-gate, ahead of the per-tenant whitelist lookup.
    - **Per-tenant MIME whitelist**: defaults to `DefaultMimeWhitelist.Defaults` (13 entries) or per-tenant override from `BlocksDocsOptions.MimeWhitelistPerTenant`.
    - **Per-attachment size cap**: 100 MB default, per-tenant override possible.
    - **Tenant quota**: optional cumulative cap; sum of Active-row sizes only (Tombstoned + Superseded don't count).

Rejection raises `UploadRejectedException(PolicyRejection)` with the reason enum (`Mime` / `Size` / `TenantQuota`). The exception message is **scrubbed of tenant identifiers** (SE-6); the internal-only `TenantIdInternal` property carries the tenant scope for audit-log correlation.

## Cross-cluster integration contract

Consumer clusters (blocks-financial-ar, blocks-financial-ap, blocks-leases, blocks-inspections, blocks-work-orders) link to attachments via `IDocumentRefService` rather than referencing `IAttachmentRepository` directly. This keeps `Sunfish.Blocks.Docs.Services` off every consumer's import path.

The link contract is a typed tuple:

```text
(TenantId, AttachmentId, ClusterCode, ParentEntityType, ParentEntityId, AttachmentRole?)
```

- **`ClusterCode`**: lowercase, kebab-case (e.g. `"blocks-financial-ar"`). Matches the cluster's NuGet package name minus the `Sunfish.` prefix.
- **`ParentEntityType`**: lowercase, singular, cluster-defined (e.g. `"invoice"`, `"bill"`, `"lease"`, `"inspection"`).
- **`ParentEntityId`**: opaque string. The docs cluster never reflects on it; the consumer cluster owns its own ID type and serializes here.
- **`AttachmentRole`**: optional, cluster-defined vocabulary (e.g. `"primary-attachment"`, `"supporting-doc"`, `"signed-copy"`). Opaque to docs.

`LinkAsync` is **idempotent**: a second call with the same tuple returns the existing row. Role updates are applied in place with a `Version` bump. **Cross-tenant safety** is enforced — a link from tenant A to an attachment owned by tenant B is rejected at the service layer (the repository alone can't catch this because it doesn't see the attachment's tenant).

## Reconciler hooks

When a consumer cluster hard-deletes a parent entity:

```csharp
await reconciler.TombstoneParentLinksAsync(
    tenantId, clusterCode: "blocks-financial-ar",
    parentEntityType: "invoice", parentEntityId: invoiceId,
    actor: "system", reason: "invoice purged");
```

`DocumentRefReconciler` walks every live `DocumentRef` for that parent and tombstones each one. Idempotent. The underlying `Attachment`s are untouched — the periodic `AttachmentOrphanReconciler` decides whether the blob itself is now orphaned (count of live `DocumentRef`s == 0 AND past the grace window).

## Orphan-blob GC

`AttachmentOrphanReconciler.ReconcileTenantAsync` is the periodic pass:

- Walks every `Active` attachment in a tenant past the configured grace period (default 15 minutes — long enough for the upload→link round-trip).
- Asks `IDocumentRefService.CountLiveLinksToAttachmentAsync` for each.
- Tombstones any with a count of zero.

Bytes reclamation (the actual blob deletion from FoundationBlob CAS) is a downstream pass that walks tombstoned rows past retention. The catalog reconciler only flips status; it never frees bytes itself.

## What this cluster does NOT do

- **No FoundationBlob bytes lifecycle** — that's a Foundation concern. blocks-docs persists `StorageRef` discriminator + payload bytes (Inline tier) or content ID (FoundationBlob tier), and consults Foundation for retrieval.
- **No PDF / OOXML rendering** — that's a UI / sidecar concern.
- **No content-level scanning** (virus, PII detection) — explicit follow-on; the substrate logs every reject path so hosting agents can plug in their own scanners.
- **No `IsLikelyText` fallback for sniffer-unknown content** — explicit council-validated choice (PR 3 council question B). Tenants needing text/csv or application/json must whitelist them per-tenant; sniffer-unknown stays deny-by-default.

## Council follow-ons

Tracked for v2:

- **OOXML deep-sniff** — peek the ZIP central directory to distinguish `application/zip` from docx/xlsx/pptx/odt. (Stub: `blocks-docs-ooxml-deep-sniff-followon`.)
- **Java class / Mach-O / WASM sniff signatures**. v1 catches them as octet-stream + denies, but explicit signatures give clearer audit-log signal.
- **HTTP-layer pre-buffer DoS guard** — host responsibility per PR 3 docs.
- **Compressed-bomb decompression-size policy** — downstream consumer responsibility per PR 3 docs.
- **SVG sanitizer** — PR 3 council deferred to v2 with the deny-by-default fallback recommended for tenants concerned about embedded scripts.

## Tests

169 tests in `packages/blocks-docs/tests/`, covering:

- MIME sniff (12 magic-byte signatures + unknown)
- Filename sanitization (path traversal, control chars, Windows reserved names with bypass variants, bidi-override stripping)
- Three-gate policy (each gate + system blacklist + cross-tenant)
- Upload + Supersede integration (sniff → sanitize → policy on both paths)
- Cross-cluster link / unlink + idempotency + tenant isolation
- Parent-delete reconciler + orphan-blob GC reconciler (grace period, idempotency, tenant scope)
