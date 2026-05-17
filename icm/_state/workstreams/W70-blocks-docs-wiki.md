---
sort_order: 79
number: 70
slug: blocks-docs-wiki
title: "W#70 — blocks-docs-wiki: Wiki, policies, and procedures (§3.2)"
status: "ready-to-build"
status_cell: "`ready-to-build` — gated on W#69 `blocks-docs-core` all PRs merged; hand-off at `icm/_state/handoffs/blocks-docs-wiki-stage06-handoff.md`; 4 PRs; ~8-12h; security spot-check on PR 3 (IPolicyCommandService + PolicyAcknowledgment)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/02_architecture/blocks-docs-schema-design.md` §3.2 (9 entities: WikiSpace, WikiBook, WikiChapter, WikiPage, Policy, Procedure, PolicyVersion, PolicyEffectiveDate, PolicyAcknowledgment) + `icm/_state/handoffs/blocks-docs-wiki-stage06-handoff.md`"
---

## Notes

**Gate condition.** `blocks-docs-core` (W#69) all PRs merged — `WikiPage.documentId` is a foreign key to `Document`. Dev should start W#70 PR 1 only after W#69 PR 1 merges (the Document entity must exist before WikiPage can reference it).

**What it ships.** Per spec §3.2:

- `WikiSpace` — top-level organization container; slug-unique per tenant; `requiresApproval` flag gates page publishing
- `WikiBook` — organized page collection within a space; slug-unique per space; optional cover image (`StorageRef?`)
- `WikiChapter` — grouping layer within a book; sort-ordered
- `WikiPage` — IS a `Document` (FK to `blocks-docs-core.Document`); supports nested pages (depth ≤ 4, cycle-detected); maintains forward/backlinks via `IWikiLinkIntegrityService`; `markdownBody` + cached `renderedHtml`
- `Policy` overlay — wiki page with formal publishing/approval workflow; policy number unique per tenant; review cadence + approver list
- `Procedure` overlay — wiki page implementing a parent policy; estimated duration; tooling requirements
- `PolicyVersion` — versioned publish record; links `PolicyEffectiveDate` + `DocumentVersion`; `acknowledgmentRequired` flag
- `PolicyEffectiveDate` — effective date range with open-ended support; superseded tracking
- `PolicyAcknowledgment` — per-employee acknowledgment record; status transitions (Pending → Acknowledged | Declined); channel tracking; `(PolicyVersionId, EmployeeId)` unique

- `IWikiCommandService` (CreatePage, PublishPage, ArchivePage, RestorePage) — depth + cycle invariants enforced
- `IPolicyCommandService` (PublishVersion, RequireAcknowledgment, RecordAcknowledgment) — transition rules enforced
- `IWikiLinkIntegrityService` — forward/backlink registry; updated on page save (no background sweep)

**Attribution.** Bookstack (MIT) — `WikiSpace → WikiBook → WikiPage` hierarchy (clean-room shape adaptation). NOTICE entry required in package root.

**Consumers unblocked.** `blocks-docs-templates` (W#71 — lease/notice/NDA template overlays over Document); property-operations policy management (maintenance procedures, emergency protocols, tenant handbooks); `blocks-crew-comms` knowledge base integration.
