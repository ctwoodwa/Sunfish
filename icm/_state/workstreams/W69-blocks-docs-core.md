---
sort_order: 78
number: 69
slug: blocks-docs-core
title: "W#69 — blocks-docs-core: Document management core entities (§3.1)"
status: "ready-to-build"
status_cell: "`ready-to-build` — gated on `blocks-docs` (attachment substrate, 6 PRs) all PRs merged — `DocumentVersion.StorageRef` depends on `blocks-docs.StorageRef` type; hand-off at `icm/_state/handoffs/blocks-docs-core-stage06-handoff.md`; 3 PRs; ~6-9h; no mandatory council (no auth-adjacent surface)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/02_architecture/blocks-docs-schema-design.md` §1 + §2 + §3.1 (7 entities: Document, DocumentVersion, DocumentRevisionHistory, DocumentTag, DocumentFolder, DocumentPermission, RetentionPolicy) + `icm/_state/handoffs/blocks-docs-core-stage06-handoff.md`"
---

## Notes

**Gate condition.** `blocks-docs` (attachment substrate) all 6 PRs merged — `DocumentVersion` references `StorageRef` from `blocks-docs`; the type must exist before this package compiles. csproj must add `<ProjectReference Include="..\blocks-docs\blocks-docs.csproj" />`.

**Phase 1 critical-path position.** `blocks-docs-core` is the Document entity layer above the `blocks-docs` attachment substrate, per `blocks-docs-schema-design.md` §3.1.

**Sub-domain packages (build order):**

```
blocks-docs              (attachment substrate — Attachment, StorageRef, DocumentRef; 6 PRs; PREDECESSOR)
  └──▶ blocks-docs-core  ← THIS WORKSTREAM  (§3.1 — Document entity layer; 7 entities; 3 PRs)
        └──▶ blocks-docs-wiki       (W#70 — WikiSpace + WikiPage + Policy; 4 PRs)
        └──▶ blocks-docs-templates  (W#71 — ContractTemplate + render-job; deferred)
        └──▶ blocks-docs-dam        (W#72 — MarketingAsset + BrandKit; deferred)
        └──▶ blocks-docs-signing    (W#73 — SigningWorkflow + Signature; deferred)
```

**What it ships.** Per spec §3.1:

- `Document` — polymorphic base entity; `DocumentType` discriminator (Contract, Invoice, Receipt, Inspection, Report, Generic); `DocumentStatus` (Draft → Published → Archived → Superseded); `DocumentSensitivity` (Public, Internal, Confidential, Restricted)
- `DocumentVersion` — content versions; `StorageRef? ContentStorageRef` using `blocks-docs.StorageRef` type (discriminated union: Inline / FoundationBlob / ExternalUri); `DiffKind` enum (Added, Removed, Modified, Moved, Unchanged)
- `DocumentRevisionHistory` — append-only log of version transitions; immutable once written
- `DocumentTag` — free-form keyword tags; N:M to Document via join table
- `DocumentFolder` — materialized-path hierarchy (`/policies/hr/`); depth ≤ 8; efficient subtree queries on SQLite
- `DocumentPermission` — per-document actor grants; `DocumentScope` (Read, Annotate, Edit, Manage, Owner)
- `RetentionPolicy` — `min-retention-days`, `disposalAction` (Delete, Archive, CryptoShred); CryptoShred throws `NotSupportedException` until `blocks-docs-retention` ships

- `IDocumentRepository` + `InMemoryDocumentRepository`
- `IDocumentVersionRepository` + `InMemoryDocumentVersionRepository`
- `IDocumentTagRepository` + `InMemoryDocumentTagRepository`
- `IDocumentFolderRepository` + `InMemoryDocumentFolderRepository`
- `IDocumentCommandService` (PR 2) — `CreateAsync`, `SaveRevisionAsync`, `PublishAsync`, `ArchiveAsync`, `UnarchiveAsync`, `SupersedeAsync`
- `IDocumentTagService` (PR 2) — `AddTagAsync`, `RemoveTagAsync`, `GetTagsAsync`
- `IDocumentPermissionService` (PR 2) — `GrantAsync`, `RevokeAsync`, `GetPermissionsAsync`

**Attribution.** Apache OFBiz `Content` entity (Apache 2.0) + Mayan EDMS (Apache 2.0) — document hierarchy and version model. NOTICE entry required in package root.

**Consumers unblocked.** `blocks-docs-wiki` (W#70 — WikiPage.documentId FK); `blocks-docs-templates` (W#71 — ContractTemplate.documentId FK); property-operations cluster document management views (inspection reports as Documents, lease contracts, receipts); wiki policy publishing flows.

**No mandatory council.** No auth-adjacent surface and no background sweep — standard security spot-check rules apply at PR 2 (`IDocumentPermissionService`), but no council gate required before auto-merge.
