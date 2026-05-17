# Hand-off — `blocks-docs-core` Document substrate (Phase 2 document cluster)

**From:** XO (research session)
**To:** dev (galley primary; Sunfish overflow)
**Created:** 2026-05-17
**Status:** `ready-to-build` — **gated on `blocks-docs` (attachment substrate) all 6 PRs merged** — `DocumentVersion.ContentStorageRef` depends on `blocks-docs.StorageRef` type
**Workstream:** W#69 — blocks-docs-core (Phase 2 document cluster substrate)
**Spec source:** [`icm/02_architecture/blocks-docs-schema-design.md`](../../02_architecture/blocks-docs-schema-design.md) §3.1 (all sub-sections: Document + DocumentVersion + DocumentRevisionHistory + DocumentTag + DocumentFolder + DocumentPermission + RetentionPolicy) + §6 (storage model, IBlobStore contract)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~6–9h dev (3 PRs; ~25–35 tests + docs)
**PR count:** 3 PRs
**Pre-merge council:** NOT required (substrate scope; mirrors blocks-financial-ar/ap pattern). Standard self-audit applies.
**Attribution required:** Apache OFBiz `Content` entity (Apache 2.0 — Document base shape + OFBiz ContentRole → DocumentPermission); Mayan EDMS (Apache 2.0 — version-pointer pattern + revision history); carry NOTICE entry.

---

## Context

### Phase 2 document cluster position

`blocks-docs-core` is the substrate that all other `blocks-docs-*` packages extend. It defines:
- The `Document` base entity (type-polymorphic via `DocumentType` discriminator)
- Versioning and revision history (append-only)
- Folder hierarchy (materialized-path)
- Permission model (per-document ACL overlay over cluster RBAC)
- Retention policy (legal hold + disposal action + crypto-shred hook)

**Follow-on packages** (each is a future workstream; none are in scope for this hand-off):
- `blocks-docs-wiki` (§3.2): WikiSpace + WikiBook + WikiPage + Policy/Procedure overlays + acknowledgment
- `blocks-docs-templates` (§3.3): ContractTemplate + fields + clauses + render jobs + ContractInstance
- `blocks-docs-dam` (§3.4): MarketingAsset + AssetTag + AssetCollection + AssetUsage + BrandKit
- `blocks-docs-signing` (§3.5): SigningWorkflow + SigningStep + SigningParty + Signature + audit

All follow-on packages depend on the `Document` entity from `blocks-docs-core`; none can ship until this substrate lands.

**Gate condition.** Start after `blocks-docs` (attachment substrate) PR 1 merges — `DocumentVersion.ContentStorageRef` uses `StorageRef` from `blocks-docs`. Add csproj dep: `<ProjectReference Include="..\blocks-docs\blocks-docs.csproj" />`. No financial dependency; can run in parallel with any remaining financial PRs.

### What this hand-off ships

Per `blocks-docs-schema-design.md` §3.1:

**7 entity types** (all in `Models/`):

| Type | Description |
|---|---|
| `Document` | Base entity — name, slug, `DocumentType` discriminator, version pointers, status, sensitivity, storageRef, soft-delete |
| `DocumentVersion` | Append-only version row — `ContentStorageRef: StorageRef?` (from `blocks-docs`), contentHash, versionNumber, changeSummary |
| `DocumentRevisionHistory` | Fine-grained revision journal — supports `full-snapshot`, `json-patch`, and `crdt-op` diff kinds |
| `DocumentTag` | Tag taxonomy — name, slug, color; many-to-many with documents |
| `DocumentFolder` | Hierarchical folders — materialized-path (`/policies/hr/`), depth ≤ 8 |
| `DocumentPermission` | Per-document ACL row — principalKind, scope (`read`/`comment`/`edit`/`approve`/`manage`), revocable |
| `RetentionPolicy` | Retention rules — period days, legal hold flag, disposalAction (`archive`/`soft-delete`/`crypto-shred`) |

**5 supporting enums** (in `Models/`):

| Enum | Values |
|---|---|
| `DocumentType` | `WikiPage`, `Policy`, `Procedure`, `ContractTemplate`, `ContractInstance`, `MarketingAsset`, `BrandKitEntry`, `SignedPdf`, `Generic` |
| `DocumentStatus` | `Draft`, `InReview`, `Approved`, `Published`, `Archived`, `Superseded` |
| `DocumentSensitivity` | `Public`, `Internal`, `Confidential`, `Restricted` |
| `DiffKind` | `FullSnapshot`, `JsonPatch`, `CrdtOp` |
| `DocumentScope` | `Read`, `Comment`, `Edit`, `Approve`, `Manage` |

**Strongly-typed IDs** (ULID-string pattern, same as financial cluster):
`DocumentId`, `DocumentVersionId`, `DocumentRevisionId`, `DocumentTagId`, `DocumentFolderId`, `DocumentPermissionId`, `RetentionPolicyId`

**4 repository contracts + in-memory implementations** (in `Services/`):
- `IDocumentRepository` + `InMemoryDocumentRepository`
- `IDocumentVersionRepository` + `InMemoryDocumentVersionRepository`
- `IDocumentFolderRepository` + `InMemoryDocumentFolderRepository`
- `IRetentionPolicyRepository` + `InMemoryRetentionPolicyRepository`

`IDocumentTagRepository` and `IDocumentPermissionRepository` are declared as interfaces (stubs); in-memory implementations arrive in PR 2 alongside the services that need them.

**What this does NOT ship:** wiki/policy/template/DAM/signing overlays; EFCore configurations; `IAttachmentService` or `IBlobStore` wiring (those are in `blocks-docs`). `StorageRef` is consumed from `blocks-docs` — do NOT redefine it here.

---

## PR breakdown

### PR 1 — Scaffold + 7 entity types + 4 repositories + DI

Package: `packages/blocks-docs-core/`
Namespace root: `Sunfish.Blocks.DocsCore`

```
packages/blocks-docs-core/
├── Sunfish.Blocks.DocsCore.csproj
├── NOTICE.md                          (OFBiz + Mayan EDMS attribution)
├── README.md
├── Models/
│   ├── Document.cs
│   ├── DocumentId.cs
│   ├── DocumentVersion.cs
│   ├── DocumentVersionId.cs
│   ├── DocumentRevisionHistory.cs
│   ├── DocumentRevisionId.cs
│   ├── DocumentTag.cs
│   ├── DocumentTagId.cs
│   ├── DocumentFolder.cs
│   ├── DocumentFolderId.cs
│   ├── DocumentPermission.cs
│   ├── DocumentPermissionId.cs
│   ├── RetentionPolicy.cs
│   ├── RetentionPolicyId.cs
│   ├── DocumentType.cs                (enum)
│   ├── DocumentStatus.cs              (enum)
│   ├── DocumentSensitivity.cs         (enum)
│   ├── DiffKind.cs                    (enum)
│   ├── DocumentScope.cs               (enum)
│   └── BlocksDocsCoreOptions.cs       (FallbackPollingInterval)
├── Services/
│   ├── IDocumentRepository.cs
│   ├── InMemoryDocumentRepository.cs
│   ├── IDocumentVersionRepository.cs
│   ├── InMemoryDocumentVersionRepository.cs
│   ├── IDocumentFolderRepository.cs
│   ├── InMemoryDocumentFolderRepository.cs
│   ├── IRetentionPolicyRepository.cs
│   └── InMemoryRetentionPolicyRepository.cs
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs
└── tests/
    └── Sunfish.Blocks.DocsCore.Tests/
        └── Sunfish.Blocks.DocsCore.Tests.csproj
```

**csproj dependencies:**
```xml
<ItemGroup>
  <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
  <ProjectReference Include="..\foundation-events\Sunfish.Foundation.Events.csproj" />
  <ProjectReference Include="..\blocks-people-foundation\Sunfish.Blocks.People.Foundation.csproj" />
</ItemGroup>
```

Note: `blocks-people-foundation` provides `PartyId` for `Document.ownerId` / `DocumentPermission.principalId` cross-cluster typing. No dependency on any blocks-financial-* package.

**Key invariants in the entity models:**
1. `Document.slug` unique within `(tenantId, folderId)` — enforce in repository
2. `DocumentVersion.versionNumber` strictly monotonic within `documentId` — enforce in repository
3. `DocumentFolder.depth ≤ 8` — enforce in `IDocumentFolderRepository.AddAsync`
4. `Document.sensitivity ∈ {Confidential, Restricted}` requires ≥1 `DocumentPermission` row — enforce in `IDocumentCommandService` (PR 2); not at repository level
5. `RetentionPolicy.retentionPeriodDays` is null when `legalHold == true` (legal hold has no expiry) — enforce in `RetentionPolicy` constructor

**Tests (PR 1):** ≥10 unit tests:
- `InMemoryDocumentRepository.AddAsync` + `GetAsync` round-trip
- `InMemoryDocumentVersionRepository` monotonic `versionNumber` check
- `InMemoryDocumentFolderRepository` depth guard (≥9 → rejects)
- `DocumentFolder` materialized-path generation (root, 2-deep, 4-deep)
- `RetentionPolicy` `legalHold` + `retentionPeriodDays` consistency invariant
- Tenant-isolation: two tenants can have documents with identical slugs

---

### PR 2 — `IDocumentCommandService` + `IDocumentTagService` + `IDocumentPermissionService`

Services and their in-memory implementations:

**`IDocumentCommandService`** — the core mutation surface:
```csharp
public interface IDocumentCommandService
{
    // Creates Document (Draft) + initial DocumentVersion (Draft).
    // Assigns slug from name if not provided; enforces uniqueness within (tenantId, folderId).
    Task<CreateDocumentResult> CreateAsync(CreateDocumentCommand cmd, CancellationToken ct = default);

    // Saves a new DocumentRevisionHistory row (fine-grained edit).
    Task<SaveRevisionResult> SaveRevisionAsync(SaveRevisionCommand cmd, CancellationToken ct = default);

    // Promotes current draft version to Published; updates Document.currentVersionId + status.
    // Enforces sensitivity → permission requirement.
    // Emits Docs.DocumentPublished audit event.
    Task<PublishResult> PublishAsync(DocumentId id, PartyId actor, CancellationToken ct = default);

    // Transitions Published → Archived; sets Document.archivedAt; emits Docs.DocumentArchived.
    Task<ArchiveResult> ArchiveAsync(DocumentId id, string reason, PartyId actor, CancellationToken ct = default);

    // Restores Archived → Published; clears archivedAt.
    Task<UnarchiveResult> UnarchiveAsync(DocumentId id, PartyId actor, CancellationToken ct = default);

    // Supersedes a document: creates a successor DocumentVersion from a source document; marks prior Superseded.
    Task<SupersedeResult> SupersedeAsync(DocumentId oldId, DocumentId newId, PartyId actor, CancellationToken ct = default);
}
```

Result types follow the financial cluster pattern (record with IsSuccess + error enum + Detail string).

**`IDocumentTagService`** — tag CRUD + assignment:
```csharp
public interface IDocumentTagService
{
    Task<DocumentTag> CreateTagAsync(string name, string color, PartyId actor, CancellationToken ct = default);
    Task AddTagToDocumentAsync(DocumentId documentId, DocumentTagId tagId, PartyId actor, CancellationToken ct = default);
    Task RemoveTagFromDocumentAsync(DocumentId documentId, DocumentTagId tagId, PartyId actor, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentTag>> GetTagsForDocumentAsync(DocumentId documentId, CancellationToken ct = default);
}
```

**`IDocumentPermissionService`** — per-document ACL:
```csharp
public interface IDocumentPermissionService
{
    Task GrantAsync(DocumentId documentId, string principalKind, string principalId, DocumentScope scope, PartyId grantedBy, CancellationToken ct = default);
    Task RevokeAsync(DocumentPermissionId permissionId, PartyId revokedBy, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentPermission>> GetPermissionsAsync(DocumentId documentId, CancellationToken ct = default);
}
```

DI: `AddSunfishDocsCore()` registers all repositories + services (Scoped); `BlocksDocsCoreOptions` binding.

**Tests (PR 2):** ≥12 unit tests:
- `CreateAsync` with duplicate slug in same folder → `CreateError.SlugConflict`
- `PublishAsync` on Confidential document with no permissions → `PublishError.SensitivityRequiresPermission`
- `PublishAsync` on Draft → succeeds; `Document.currentVersionId` updated
- `PublishAsync` on non-Draft → `PublishError.InvalidStatus`
- `ArchiveAsync` + `UnarchiveAsync` round-trip
- `SupersedeAsync` sets old document status to Superseded
- `AddTagToDocumentAsync` + `GetTagsForDocumentAsync` round-trip
- `GrantAsync` + `RevokeAsync` permission lifecycle
- Tenant isolation on `IDocumentCommandService` (cannot archive another tenant's document)

---

### PR 3 — apps/docs page + EFCore entity configurations + ledger flip

**EFCore configurations** — `Sunfish.Blocks.DocsCore.EntityFrameworkCore/` (if the blocks pattern separates EFCore from the main package):

Check whether the blocks-financial-* packages use a separate EFCore package or include the `EntityFrameworkCore/` directory in the main package. Mirror whichever pattern exists on main. If no EFCore pattern exists yet in blocks-financial-*, defer EFCore to a follow-up (PR 3 ships without it; note the deferral in the PR description).

**apps/docs page:**
```
apps/docs/blocks/docs-core/
└── overview.md        (key types table, DI registration, IDocumentCommandService API surface)
```

Follow the `apps/docs/blocks/financial-ap/overview.md` pattern (from blocks-financial-ap PR 4 on main).

**Ledger flip:** `active-workstreams.md` W#69 row → `built`. Standard PR body with test count.

---

## Commit message templates

```
feat(blocks-docs-core): PR 1 — scaffold + Document + DocumentVersion + DocumentFolder + repositories + DI
feat(blocks-docs-core): PR 2 — IDocumentCommandService (publish/archive/supersede) + tag + permission services
chore(blocks-docs-core): PR 3 — EFCore entity config + apps/docs overview + ledger flip
```

---

## Halt conditions

Stop and file `dev-question-*` if any of these arise:

1. **EFCore pattern unclear**: if blocks-financial-* packages on main have NO EFCore config (all in-memory only), confirm whether blocks-docs-core should also omit EFCore in this hand-off. If yes, remove EFCore from PR 3 scope and note in PR description.
2. **`StorageRef` type not found**: `Document.storageRef: StorageRef | null` references a type from the future `blocks-docs-storage` package. For now, model it as `string? StorageRef` (a URI/path) in C#; the strongly-typed `StorageRef` value type can be added when `blocks-docs-storage` ships.
3. **`blocks-people-foundation` missing on main**: run `ls packages/ | grep people-foundation` to confirm. If missing, `Document.OwnerId` is `string?` (untyped) for now.
4. **Wiki/signing types leaking in**: `blocks-docs-core` is substrate only. Do not include `WikiPage`, `Policy`, `ContractTemplate`, or `SigningWorkflow` — those are future workstreams.
5. **Retention + crypto-shred**: `DisposalAction.CryptoShred` requires kernel-security envelope keys. Model the enum value but do NOT implement the crypto logic here — the shred path throws `NotSupportedException` until `blocks-docs-retention` wires the key-destruction path.
