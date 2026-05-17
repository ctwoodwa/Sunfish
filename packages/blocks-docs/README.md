# Sunfish.Blocks.Docs

Document-attachment substrate for Sunfish. Cross-cluster surface — AR invoices, AP bills, leases, inspections, and work orders all attach documents through this cluster. PR 1 ships the entity primitives; PRs 2–6 build the service surface, IBlobStore wiring, and cross-cluster DocumentRef.

## PR 1 scope (this commit)

Entity primitives + repository contract. No services, no IBlobStore wiring, no DocumentRef cross-cluster API.

| File | What ships |
|---|---|
| `Models/AttachmentId.cs` | Guid-backed strongly-typed identifier with JSON converter. |
| `Models/Sensitivity.cs` | `enum { Internal, Pii, Financial, Confidential }` + lowercase JSON. Drives sharing / export policy in later PRs. |
| `Models/StorageRefKind.cs`, `StorageRef.cs` | Discriminated-union over the three storage tiers: `Inline` (≤8KB bytes in the catalog row), `FoundationBlob` (content-addressed CID via the foundation Blobs primitive), `ExternalUrl` (S3-equivalent). Constructor helpers (`ForInline` / `ForFoundationBlob` / `ForExternalUrl`) validate inputs. |
| `Models/AttachmentStatus.cs` | `enum { Active, Superseded, Tombstoned }` + lowercase JSON. Attachments are immutable post-upload; a new version is a fresh row with `ReplacesAttachmentId` set + the prior row flipped to `Superseded`. |
| `Validation/AttachmentStatusTransitions.cs` | Static `IsAllowed(from, to)` over the lifecycle. Forbids un-supersede + resurrection. |
| `Models/Attachment.cs` | Aggregate with `ContentHash` (sha-256 lowercase-hex), `MimeType` (server-sniffed, not from filename), `SizeBytes`, `OriginalFilename` (display-only — service path-sanitizes on the way in), `ThumbnailRef?`, `Sensitivity`, replacement chain, CRDT envelope. Static `Create` factory. |
| `Services/IAttachmentRepository.cs` | CRUD + `FindByContentHashAsync` (supports PR 2's dedup) + `ListByTenantAsync` + idempotent `SoftDeleteAsync`. **Interface only — impl ships in PR 2.** |
| `DependencyInjection/DocsServiceCollectionExtensions.cs` | `services.AddBlocksDocs()` — **no-op stub** in PR 1. Hosts can wire the call today; PRs 2–5 fill in the registrations. |

**Tests:** ~26 across `AttachmentTests` (10), `StorageRefTests` (8), `AttachmentStatusTransitionTests` (8).

## What's NOT in PR 1

- **PR 2:** `IAttachmentService` + content-hash dedup + `InMemoryAttachmentRepository`.
- **PR 3 (council required):** `IBlobStore` wiring + `BlocksDocsOptions` (MIME whitelist + size cap + tenant quotas) + server-side MIME sniffer. Security-engineering council.
- **PR 4:** `DocumentRef` cross-cluster join entity + `IDocumentRefService` (lets a single attachment carry multiple cluster references with different role labels).
- **PR 5:** Full `AddBlocksDocs()` DI extension + `apps/docs/blocks-docs/overview.md` + cross-cluster event-bus editorial.
- **PR 6:** `IPostMergeReconciler` stub for blob GC against the replacement chain.

## Design notes

- **Filename is for display only.** Server-side MIME sniffing is the source of truth (lands in PR 3). The `OriginalFilename` field exists for user-visible UI rendering.
- **Inline tier is small only** (≤ 8 KB). Anything bigger goes to FoundationBlob (canonical local-first path) or ExternalUrl (when the host opts in).
- **Replacement chain, not in-place mutation.** A new version of a doc is a fresh `Attachment` row; the old row flips to `Superseded` and back-fills `ReplacedByAttachmentId`. Auditable history.
- **Content-hash deduplication is at the service layer** (PR 2), not the repository. Different `AttachmentId`s can point at the same blob — preserving per-upload context (who uploaded, when, what filename, what role) while keeping blob storage O(unique-content).

## Cross-cluster types

| Origin | Used here |
|---|---|
| `Sunfish.Foundation.Assets.Common` | `TenantId`, `Instant` |

**No other cluster deps.** AR / AP / Leases / Inspections / Work-Orders are downstream consumers of this cluster; this cluster has no upstream cluster deps.

## Build + test

```bash
dotnet build packages/blocks-docs/Sunfish.Blocks.Docs.csproj
dotnet test  packages/blocks-docs/tests/Sunfish.Blocks.Docs.Tests.csproj
```
