---
sort_order: 76
number: 71
slug: blocks-docs
title: "W#71 — blocks-docs: Document attachment substrate (Attachment, StorageRef, DocumentRef, IBlobStore)"
status: "building"
status_cell: "`building` — dev implementing; PR 1 (#968) + PR 2 (#971) merged; 4 PRs remaining; PR 3 (#974) DRAFT — **BLOCKING council verdict 2026-05-17** (3 blockers: SupersedeAsync bypass, system-blacklist override, Windows reserved-name bypass; apply amendments → re-council before merging); hand-off at `icm/_state/handoffs/blocks-docs-stage06-handoff.md`; 6 PRs total; ~10-13h"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/02_architecture/blocks-docs-schema-design.md` §3.1 (Document base scaffold / storage-ref dimensions) + §6 (storage model) + §7 (cross-cluster contracts) + `icm/_state/handoffs/blocks-docs-stage06-handoff.md`; ADR 0088 (Path II)"
---

## Notes

**Critical-path predecessor.** `blocks-docs` is the binary-attachment floor that every other cluster needs before they can attach files to records:

```
blocks-docs   ← THIS WORKSTREAM (Attachment + StorageRef + DocumentRef + IBlobStore)
  │
  ├──▶ blocks-docs-core (W#69) — Document entity layer (gated on blocks-docs completion)
  │     └──▶ blocks-docs-wiki (W#70)
  │     └──▶ blocks-docs-templates (W#72 — deferred)
  │     └──▶ blocks-docs-dam (W#73 — deferred)
  │     └──▶ blocks-docs-signing (W#74 — deferred)
  │
  ├──▶ blocks-financial-ar — invoice attachment (DocumentRef)
  ├──▶ blocks-financial-ap — bill attachment (DocumentRef)
  ├──▶ blocks-property-leases — lease contract attachment
  ├──▶ blocks-property-inspections — inspection-photo attachment
  └──▶ blocks-reports — report-artifact PDF attachment
```

**What it ships.** 6 PRs (~10-13h):

- PR 1 (MERGED #968): `Attachment` entity, `StorageRef` discriminated union (Inline/FoundationBlob/ExternalUri), `AttachmentStatus` (Active→Superseded→Tombstoned), `Sensitivity` enum, `IAttachmentRepository`, DI stub
- PR 2: `IAttachmentService` + content-hash deduplication + in-memory repo impl
- PR 3 (council required — security-engineering MANDATORY): `IBlobStore` wiring + `MimeTypeAndSizePolicy` (per-tenant whitelist + size cap + quotas) + server-side MIME sniffing; tenant-boundary defense-in-depth
- PR 4: `DocumentRef` cross-cluster join entity + `IDocumentRefService`
- PR 5: Full DI + `AddBlocksDocs()` + append-only event log + apps/docs page + idempotency-key catalog updates
- PR 6: `IPostMergeReconciler` blob GC stub

**Attribution.** Standard foundation dependency only; no third-party borrow. No NOTICE entry required.

**Note on sort_order.** Sort order 76 places this before W#69 (78) and W#70 (79) in the ledger, reflecting the correct dependency order: blocks-docs must complete before blocks-docs-core can start.
