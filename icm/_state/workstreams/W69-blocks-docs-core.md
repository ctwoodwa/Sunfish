---
sort_order: 78
number: 69
slug: blocks-docs-core
title: "W#69 — blocks-docs-core: Document management core entities (§3.1)"
status: "ready-to-build"
status_cell: "`ready-to-build` — no gate conditions; hand-off at `icm/_state/handoffs/blocks-docs-core-stage06-handoff.md`; 3 PRs; ~6-9h; no mandatory council (no auth-adjacent surface)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/02_architecture/blocks-docs-schema-design.md` §1 + §2 + §3.1 (7 entities: Document, DocumentVersion, DocumentRevisionHistory, DocumentTag, DocumentFolder, DocumentPermission, RetentionPolicy) + `icm/_state/handoffs/blocks-docs-core-stage06-handoff.md`"
---

## Notes

**Phase 1 critical-path position.** `blocks-docs-core` is the first of 6 document-management packages defined in `blocks-docs-schema-design.md`. It establishes the shared entity contracts that all subsequent doc packages build on.

**Sub-domain packages (schema §1):**

```
blocks-docs-core         ← THIS WORKSTREAM   (§3.1 — 7 entities)
blocks-docs-storage      (§3.2 — blob + StorageRef wiring; unblocks crypto-shred)
blocks-docs-search       (§3.3 — full-text index)
blocks-docs-workflow     (§3.4 — approval + signing flows)
blocks-docs-retention    (§3.5 — lifecycle enforcement sweep)
blocks-docs-sharing      (§3.6 — link-share + guest access)
```

**What it ships.** Per spec §3.1:

- `Document` — polymorphic base entity; `DocumentType` discriminator (Contract, Invoice, Receipt, Inspection, Report, Generic); `DocumentStatus` (Draft → Published → Archived → Superseded); `DocumentSensitivity` (Public, Internal, Confidential, Restricted)
- `DocumentVersion` — content versions; `StorageRef: string?` placeholder until `blocks-docs-storage` ships; `DiffKind` enum (Added, Removed, Modified, Moved, Unchanged)
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

**Consumers unblocked.** `blocks-docs-storage` (StorageRef wiring + blob upload/download); `blocks-docs-search` (full-text index over Document.Title/Tags); property-operations cluster document attachments (inspection reports, lease PDFs, receipts).

**No mandatory council.** No auth-adjacent surface and no background sweep — standard security spot-check rules apply at PR 2 (`IDocumentPermissionService`), but no council gate required before auto-merge.
