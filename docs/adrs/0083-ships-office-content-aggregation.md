---
id: 83
title: Ship's Office Content Aggregation Surface + Scribe Role
status: Accepted
date: 2026-05-05
tier: ui-core
pipeline_variant: sunfish-feature-change
composes: [7, 21, 22, 27, 46, 54, 62, 65, 77]
amendments: []
---

# ADR 0083 — Ship's Office Content Aggregation Surface + Scribe Role

**Status:** Proposed
**Date:** 2026-05-05

**Authors:** XO research session (W#35 Ship Architecture follow-on #7; final cohort ADR)

---

## Context

W#35 Ship Architecture discovery §5.6 identifies Ship's Office as **Partial coverage**:
per-document-type substrates exist (`SignatureEnvelope` via W#21; `LeaseDocumentVersion` + `ILeaseDocumentVersionLog`
via W#22; `W9Document` + `IW9DocumentService` via W#18; `BusinessCaseBundleManifest` via ADR 0007;
form primitives via ADR 0055 + ADR 0056). What is missing is the **cross-document-type aggregation surface**:
a unified view where the Scribe operates across all document kinds in one location.

This is the **7th and final follow-on ADR** in the W#35 Ship Architecture cohort
(ADR 0077 Shared Design System → ADR 0078 OOD Watch → ADR 0079 Engine Room → ADR 0080 Quarterdeck
→ ADR 0081 Tactical → ADR 0082 Sick Bay → **ADR 0083 Ship's Office**).

**Disambiguation:** ADR 0083 specifies the *aggregation surface* — the cross-document-type browse,
search, diff, and status-management layer for the Scribe. It does NOT:
- Author or manage per-document-type substrates (those are W#18/W#21/W#22 + ADR 0007/0055/0056)
- Provide plaintext TIN access (that is `IW9DocumentService.GetWithDecryptedTinAsync` via explicit capability)
- Implement a markdown editor (adapter-specific; declared as an interface contract in §3)
- Implement ADR 0055 dynamic template authoring (deferred to Phase 2; gated on ADR 0055 Accepted)

---

## Status

`Proposed` — awaiting CO acceptance

---

## Predecessor ADRs

- [ADR 0007](./0007-business-case-bundles.md) — `BusinessCaseBundleManifest` + `BundleStatus`
- [ADR 0021](./0021-lease-document-signatures.md) — `SignatureEnvelope` in `Sunfish.Foundation.Crypto`
- [ADR 0054](./0054-electronic-signature-capture-and-document-binding.md) — `LeaseDocumentVersion` + `ILeaseDocumentVersionLog`
- [ADR 0058](./0058-vendor-onboarding.md) — `W9Document` + `IW9DocumentService`; TIN encrypted via
  `IFieldEncryptor`; redacted in Ship's Office browse per §Trust impact
- [ADR 0062](./0062-mission-space-negotiation-protocol.md) — `IMissionEnvelopeObserver` for
  subscription-push fallback
- [ADR 0065](./0065-wayfinder-system-and-standing-order-contract.md) — `IStandingOrderIssuer`;
  Ship's Office docs referenced in Standing Orders
- [ADR 0077](./0077-shared-design-system.md) — `ShipRole.Scribe` + `ShipLocation.ShipsOffice` +
  `IPermissionResolver` + `ShipAction` + `IDiffPreview<TPath, TValue>` + `ISearchAsYouType<THit>` +
  `DiffPreviewView`
- [ADR 0082](./0082-sick-bay-aggregation-surface.md) — cohort sibling; structural pattern reference

---

## Decision drivers

1. The Scribe role (`ShipRole.Scribe`) is defined in ADR 0077 and assigned to `ShipLocation.ShipsOffice`
   but currently has no implementation surface — no place to go.
2. Per-document-type substrates (leases, W9s, bundle manifests, signatures) are scattered across
   multiple packages with no cross-document browse or search.
3. Document version diff is a first-class Scribe workflow (compare lease redlines; audit bundle
   manifest changes) and must be accessible per W#35 §9.5.
4. The Ship's Office is the administrative record-keeping anchor for the tenant; its absence is
   a launch-blocking gap for regulated-industry tenants (property management, healthcare admin).

---

## Considered options

### Option A — Scribe surface embedded in existing blocks (no new package)

Add Ship's Office views to `blocks-leases`, `blocks-maintenance`, and `foundation-catalog` each
showing only their own document kind.

**Pro:** minimal new packages.
**Con:** there is no unified cross-document browse. The Scribe must navigate to three separate
  blocks, losing the "single place for document management" premise of `ShipLocation.ShipsOffice`.
  `IPermissionResolver` permission check cannot be de-duplicated across blocks.
**Verdict:** rejected.

### Option B — `foundation-ships-office` + `blocks-ships-office` **[RECOMMENDED]**

`foundation-ships-office` owns: `ShipsOfficeDocumentView` view-record + `ShipsOfficeDocumentKind` enum +
`ShipsOfficeDocumentId` opaque identifier + `ShipsOfficeSnapshot` aggregate + `IShipsOfficeDataProvider` +
`IShipsOfficeCommandService` + `IDocumentDiffService` contract + `IContentEditorSurface` adapter contract +
`ShipsOfficeOptions` DI configuration.

`blocks-ships-office` owns: Blazor rendering + reference implementation of `IShipsOfficeDataProvider`
(aggregating `ILeaseDocumentVersionLog`, `IW9DocumentService`, `BundleCatalog`) + `ShipsOfficeBlock.razor`
+ component tree.

**Pro:** clean separation; `foundation-ships-office` contracts are reusable by iOS / Bridge without
  Blazor dependency; permission check is centralized in the data provider; single DI registration.
**Con:** one additional package pair.
**Verdict:** selected.

---

## Decision

`foundation-ships-office` (contracts) + `blocks-ships-office` (UI + reference impl). New packages;
no changes to existing foundation-tier packages beyond additive `ShipAction` constants (foundation-ship-common)
and `AuditEventType` constants (kernel-audit).

---

## §1 Observable data model

```csharp
// Opaque identifier; format is internal to IShipsOfficeDataProvider implementations.
// Do not parse or construct outside of IShipsOfficeDataProvider.
public readonly record struct ShipsOfficeDocumentId(string Value);

public enum ShipsOfficeDocumentKind
{
    BundleManifest,     // BusinessCaseBundleManifest (foundation-catalog)
    LeaseDocument,      // LeaseDocumentVersion (blocks-leases; append-only)
    VendorW9,           // W9Document (blocks-maintenance) — TIN ALWAYS redacted in browse
    SignatureEnvelope,  // SignatureEnvelope (foundation-crypto)
    // Phase 2 addition (requires ADR 0055 Status: Accepted; halt-condition H4):
    // DynamicTemplate,
}

public enum DocumentStatus
{
    Draft,
    Published,
    Archived,
    PendingSignature,  // used for SignatureEnvelope + LeaseDocument awaiting sign-off
}

/// <summary>
/// Framework-agnostic browse view over any document in Ship's Office.
/// ALL sensitive fields are pre-redacted by IShipsOfficeDataProvider.
/// VersionLabel is populated for LeaseDocument (e.g., "v3"); null for other kinds.
/// </summary>
public sealed record ShipsOfficeDocumentView
{
    public required ShipsOfficeDocumentId   Id             { get; init; }
    public required ShipsOfficeDocumentKind Kind           { get; init; }
    public required string                  Title          { get; init; }
    public required DocumentStatus          Status         { get; init; }
    public required NodaTime.Instant        UpdatedAt      { get; init; }
    public required ActorId                 LastModifiedBy { get; init; }
    public required string?                 VersionLabel   { get; init; }
}

public sealed record ShipsOfficeSnapshot
{
    public required IReadOnlyList<ShipsOfficeDocumentView> Documents { get; init; }
    public required int                                    TotalCount { get; init; }
    public required NodaTime.Instant                       AsOf      { get; init; }
}

public sealed record ShipsOfficeSearchQuery
{
    public string?                                    TextQuery   { get; init; }  // null = all
    public IReadOnlyList<ShipsOfficeDocumentKind>?    KindFilter  { get; init; }  // null = all kinds
    public DocumentStatus?                            StatusFilter { get; init; } // null = all statuses
    public int                                        PageSize    { get; init; } = 50;
    public string?                                    PageToken   { get; init; } // null = first page
}
```

---

## §2 Provider + command interfaces

```csharp
/// <summary>
/// Aggregates cross-document-type browse + search + subscription for Ship's Office.
/// Implementations MUST honor ct. Recommended completion: ≤2s per GetSnapshotAsync call.
/// Return a partial snapshot (empty documents list) rather than throwing on timeout.
/// FORBIDDEN: no IFieldDecryptor call anywhere in this interface's implementations —
/// W9Document TIN is always redacted; see §Trust impact.
/// </summary>
public interface IShipsOfficeDataProvider
{
    /// <summary>
    /// Returns a paged snapshot of all documents visible to the caller.
    /// Permission pre-filtering (ViewShipsOffice) is the caller's responsibility;
    /// the data provider returns all documents for the tenant without role filtering.
    /// CALLER CONTRACT: callers MUST verify ShipAction.ViewShipsOffice via
    /// IPermissionResolver before calling this method. The data provider does not
    /// re-verify role — it is the caller's (UI block's) responsibility to gate access.
    /// This contract is enforced by a Roslyn analyzer (SUNFISH_SHIPSOFFICE_PERM001)
    /// in Phase 2 that warns on GetSnapshotAsync/SearchAsync calls not preceded by
    /// an IPermissionResolver.AuthorizeAsync(ShipAction.ViewShipsOffice) call.
    /// </summary>
    Task<ShipsOfficeSnapshot> GetSnapshotAsync(
        TenantId tenant, CancellationToken ct = default);

    IAsyncEnumerable<ShipsOfficeDocumentView> SearchAsync(
        TenantId tenant, ShipsOfficeSearchQuery query, CancellationToken ct);

    /// <summary>
    /// Push-subscription for document-change events. Falls back to periodic polling
    /// until ADR 0065-A1 IStandingOrderEventStream ships (halt-condition H5).
    /// </summary>
    IAsyncEnumerable<ShipsOfficeDocumentView> SubscribeChangesAsync(
        TenantId tenant, CancellationToken ct);
}

/// <summary>
/// Publish + archive commands for Ship's Office documents.
/// Every method emits its AuditEventType BEFORE performing the state change
/// (audit-before-operation invariant per ADR 0046-A2 cohort pattern).
/// </summary>
public interface IShipsOfficeCommandService
{
    /// <summary>
    /// Transitions a Draft or Archived document to Published.
    /// Requires ShipAction.PublishShipsOfficeDocument + Captain/XO permission.
    /// </summary>
    Task PublishAsync(
        TenantId tenant, ShipsOfficeDocumentId id, CancellationToken ct = default);

    /// <summary>
    /// Transitions a Published document to Archived. Requires XO+ permission.
    /// </summary>
    Task ArchiveAsync(
        TenantId tenant, ShipsOfficeDocumentId id, CancellationToken ct = default);
}
```

---

## §3 Content editor contract (foundation-ships-office) + diff contract (blocks-ships-office)

**Note on tier placement.** `IDocumentDiffService` returns `DiffPreviewView` from `Sunfish.UICore`
(ADR 0077 §6.4). The architecture rule is `foundation → ui-core → blocks`; a foundation package
MUST NOT depend on `Sunfish.UICore`. Therefore `IDocumentDiffService` is declared in
`blocks-ships-office`, not `foundation-ships-office`. Only `IContentEditorSurface` and
`ContentEditorResult` live in `foundation-ships-office`.

```csharp
// ── foundation-ships-office ────────────────────────────────────────────────

/// <summary>
/// Adapter contract for framework-specific content editors (Blazor / React / MAUI).
/// Adapters implement this in ui-adapters-blazor / ui-adapters-react;
/// consumers depend on this interface, not on the concrete editor.
/// Phase 1: stub no-op implementation returns document content as read-only.
/// Phase 2: full markdown editor with preview + publish workflow.
/// </summary>
public interface IContentEditorSurface
{
    /// <summary>
    /// Mounts an editor for the given document. Returns a Task that completes
    /// when the editor session ends (user saves, discards, or navigates away).
    /// </summary>
    Task<ContentEditorResult> EditAsync(
        TenantId tenant,
        ShipsOfficeDocumentId id,
        CancellationToken ct = default);
}

public sealed record ContentEditorResult
{
    public required bool WasSaved { get; init; }
    /// Non-null when WasSaved = true and the document kind supports versioning.
    public required string? NewVersionLabel { get; init; }
}

// ── blocks-ships-office (ONLY — NOT in foundation-ships-office) ────────────

/// <summary>
/// Computes an accessible diff between two versions of a document.
/// Declared in blocks-ships-office (depends on DiffPreviewView from Sunfish.UICore).
/// Composes IDiffPreview&lt;string, string&gt; from ADR 0077 §6.4.
/// Halt H2: requires DiffPreviewView in Sunfish.UICore (W#46 Phase 3 build).
/// Phase 1: stub returns DiffPreviewView with AccessibleRows = ["Diff not yet available"].
/// </summary>
public interface IDocumentDiffService  // declared in blocks-ships-office
{
    /// <summary>
    /// Returns an accessible diff-table view for the two document ids.
    /// Both documents must share the same ShipsOfficeDocumentKind.
    /// Throws ArgumentException when kinds differ (cross-type diff not supported).
    /// </summary>
    Task<DiffPreviewView> ComputeDiffAsync(
        TenantId tenant,
        ShipsOfficeDocumentId baseId,
        ShipsOfficeDocumentId compareId,
        CancellationToken ct = default);
}
```

---

## §4 ShipAction constants (additive to `foundation-ship-common`)

Four new constants extend the `ShipAction` readonly-record-struct value set in ADR 0077:

```csharp
public static readonly ShipAction ViewShipsOffice             = new("view-ships-office");
public static readonly ShipAction EditShipsOfficeDocument     = new("edit-ships-office-doc");
public static readonly ShipAction PublishShipsOfficeDocument  = new("publish-ships-office-doc");
public static readonly ShipAction ArchiveShipsOfficeDocument  = new("archive-ships-office-doc");
```

Permission rules (for `DefaultPermissionResolver`):

| Action | Minimum role | Minimum deck |
|---|---|---|
| `ViewShipsOffice` | `Scribe` | `TopDeck` |
| `EditShipsOfficeDocument` | `Scribe` | `TopDeck` |
| `PublishShipsOfficeDocument` | `XO` | `MainDeck` |
| `ArchiveShipsOfficeDocument` | `XO` | `MainDeck` |

---

## §5 Trust impact

- **`IFieldDecryptor` prohibition.** `IShipsOfficeDataProvider` MUST NOT call `IFieldDecryptor`
  anywhere in its implementation chain. `W9Document` TIN is always surfaced redacted in
  `ShipsOfficeDocumentView` (the `Title` field for `VendorW9` shows only the vendor name,
  not the TIN). Plaintext TIN requires an explicit `IDecryptCapability` passed to
  `IW9DocumentService.GetWithDecryptedTinAsync` — this is outside the scope of Ship's Office.
  **Test:** `[Fact] ShipsOfficeDataProvider_DoesNotReference_IFieldDecryptor()` using reflection;
  mandatory in Phase 2 council security review.

- **Publish permission is XO+.** `ShipAction.PublishShipsOfficeDocument` requires `XO` or
  higher. A Scribe can draft and edit; only XO/Captain can flip to Published. This prevents
  a document authoring mistake from becoming an officially-published record without oversight.

- **Diff output is plain text only.** `IDocumentDiffService.ComputeDiffAsync` returns a
  `DiffPreviewView` (ADR 0077 §6.4 type) composed of `IReadOnlyList<string> AccessibleRows` —
  plain text rows. The implementation MUST NOT embed raw document content (which may contain PII)
  in the `DiffPreviewView`; only structural metadata (field path / old value summary / new value
  summary) is included. Redaction policy mirrors `ShipsOfficeDocumentView` field redaction.

- **Audit emission ordering for command methods.** For `PublishAsync` and `ArchiveAsync`:
  (1) call `IPermissionResolver` FIRST; (2a) on permission PASS: emit the success
  `AuditEventType` pre-op (before state change) then execute; (2b) on permission FAIL:
  emit the rejected `AuditEventType` (e.g., `ShipsOfficePublishRejected`) then throw.
  This sequence ensures: the audit trail records rejections; no phantom "success" events
  for rejected attempts; the success audit fires before the mutation (audit-before-operation
  per ADR 0046-A2 cohort pattern).

---

## §6 AuditEventType constants (additive to `kernel-audit`)

Six new static-readonly fields on `AuditEventType`:

```csharp
public static readonly AuditEventType ShipsOfficeDocumentViewed      = new("ships-office.doc.viewed");
public static readonly AuditEventType ShipsOfficeDocumentSearched    = new("ships-office.doc.searched");
public static readonly AuditEventType ShipsOfficeDocumentDiffViewed  = new("ships-office.doc.diff-viewed");
public static readonly AuditEventType ShipsOfficeDocumentPublished   = new("ships-office.doc.published");
public static readonly AuditEventType ShipsOfficeDocumentArchived    = new("ships-office.doc.archived");
public static readonly AuditEventType ShipsOfficePublishRejected     = new("ships-office.doc.publish-rejected");
```

`ShipsOfficeDocumentSearched` fires on every `SearchAsync` call that returns ≥1 result (suppressed
for zero-result background polls to avoid audit noise). `ShipsOfficePublishRejected` fires when
`PublishAsync` is called by a non-XO+ principal and is rejected by `IPermissionResolver`.

`ShipsOfficeDocumentViewed` fires when the Scribe opens a document detail view (not on every
snapshot poll — that would be too noisy for the audit trail). Implementation convention:
fire on first view per document-per-session (one event per document-open action, not per render).

---

## §7 DI registration

```csharp
public sealed class ShipsOfficeOptions
{
    /// Polling interval for SubscribeChangesAsync fallback before ADR 0065-A1 ships.
    public TimeSpan FallbackPollingInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// Maximum document count per GetSnapshotAsync call.
    public int SnapshotPageSize { get; set; } = 500;

    /// Phase 2 opt-in: require a second actor (not the document's LastModifiedBy)
    /// for PublishAsync. Intended for regulated-industry tenants.
    /// Default false; A1 amendment governs activation.
    public bool RequireSecondActorPublish { get; set; } = false;
}

// Extension method in foundation-ships-office:
public static class ShipsOfficeServiceCollectionExtensions
{
    public static IServiceCollection AddSunfishShipsOffice(
        this IServiceCollection services,
        Action<ShipsOfficeOptions>? configure = null) { ... }
}
```

Registration in `accelerators/anchor/MauiProgram.cs` (Phase 4):
```csharp
services.AddSunfishShipsOffice(opts => {
    opts.SnapshotPageSize = 200;
});
```

---

## §8 WCAG 2.2 AA coverage

Ship's Office surfaces long-form reading, diff UX, and search — requiring dedicated review per W#35 §9.5.

| SC | Criterion | Application |
|---|---|---|
| SC 1.3.1 | Info and Relationships | Document kind badges, status badges: markup conveys structure, not just visual style |
| SC 1.3.3 | Sensory Characteristics | Diff added/removed: never color-only; each row labeled "Added:" / "Removed:" in `AccessibleRows` |
| SC 1.4.1 | Use of Color | Status badge + diff highlighting: secondary text/icon differentiator required |
| SC 1.4.3 | Contrast | Document body text ≥ 4.5:1; large text ≥ 3:1 |
| SC 1.4.4 | Resize Text | Document body reflows at 200% zoom; no horizontal scroll for single-column content |
| SC 1.4.12 | Text Spacing | Line-height, letter-spacing adjustable without loss of content per WCAG 2.1 (carried forward in WCAG 2.2 baseline) |
| SC 2.1.1 | Keyboard | All document actions (publish, archive, open, search) keyboard-accessible |
| SC 2.4.3 | Focus Order | Tab order in search results follows document list order |
| SC 2.4.5 | Multiple Ways | Documents reachable via: search + kind filter + direct navigation; ≥2 paths |
| SC 2.4.6 | Headings and Labels | Document list section, filter panel, diff panel: each has a visible heading |
| SC 3.3.1 | Error Identification | Search error (unavailable, timeout) surfaced with text description |
| SC 4.1.3 | Status Messages | Search result count ("N documents found") via `aria-live="polite"` region; operation confirmations (Published, Archived) via polite live region (user-initiated expected outcomes). Permission-rejected publish denial uses `aria-live="assertive"` (unexpected outcome mid-task per ADR 0077 §3 convention). |

---

## Compatibility plan

No existing packages are modified beyond additive changes:

| Package | Action |
|---|---|
| `packages/foundation-ships-office/` | NEW — contracts only |
| `packages/blocks-ships-office/` | NEW — Blazor UI + reference impl |
| `packages/foundation-ship-common/` | ADDITIVE — 4 `ShipAction` additions (binary-compat safe) |
| `kernel-audit` | ADDITIVE — 6 `AuditEventType` constants |
| `accelerators/anchor/` | MauiProgram.cs: `services.AddSunfishShipsOffice(...)` |
| `accelerators/bridge/` | Program.cs: `services.AddSunfishShipsOffice(...)` (Phase 4) |

---

## Implementation checklist

**Phase 1 — `foundation-ships-office` scaffold + contracts**

- [ ] Scaffold `packages/foundation-ships-office/Sunfish.Foundation.ShipsOffice.csproj`
  — deps on `foundation`, `foundation-ship-common`, `foundation-catalog`
  — NOT on `blocks-leases` or `blocks-maintenance` (those are consumed in blocks-ships-office)
- [ ] Implement §1 data model:
  `ShipsOfficeDocumentId`, `ShipsOfficeDocumentKind`, `DocumentStatus`,
  `ShipsOfficeDocumentView`, `ShipsOfficeSnapshot`, `ShipsOfficeSearchQuery`
- [ ] Implement §2 interfaces: `IShipsOfficeDataProvider`, `IShipsOfficeCommandService`
- [ ] Implement §3 contracts: `IDocumentDiffService`, `IContentEditorSurface`, `ContentEditorResult`
- [ ] Add 4 `ShipAction` constants to `foundation-ship-common`
- [ ] Add 6 `AuditEventType` constants to `kernel-audit`
- [ ] Implement `AddSunfishShipsOffice()` DI extension (stubs only for Phase 1)
- [ ] Unit tests: `ShipsOfficeDocumentKind` enum round-trip; `ShipsOfficeSearchQuery` defaults;
  `ShipsOfficeSnapshot` factory; `ContentEditorResult` WasSaved = false path
- [ ] Pre-merge council: standard adversarial (4 perspectives)

**Phase 2 — Reference implementation + DefaultShipsOfficeDataProvider**

- [ ] Implement `ShipsOfficeDataProvider : IShipsOfficeDataProvider` in `blocks-ships-office/`
  - `BundleManifest`: enumerate from `BundleCatalog.GetAllAsync(tenant)`
  - `LeaseDocument`: enumerate from `ILeaseDocumentVersionLog.ListAsync` (latest version per lease)
  - `VendorW9`: enumerate from `IW9DocumentService.GetAsync` — NEVER call `GetWithDecryptedTinAsync`
    here; title = vendor name (redacted TIN field is excluded from the view)
  - `SignatureEnvelope`: enumerate from kernel-signatures store (Phase 2 stub acceptable if
    kernel-signatures store interface not yet available — see halt H6)
  - `SubscribeChangesAsync`: use `IMissionEnvelopeObserver` + polling fallback
  - FORBIDDEN: no `IFieldDecryptor` call anywhere — verified by reflection test
- [ ] Implement `ShipsOfficeCommandService : IShipsOfficeCommandService`
  - `PublishAsync` ordering: (1) call `IPermissionResolver`; (2a) pass → emit
    `ShipsOfficeDocumentPublished` pre-op → execute state change; (2b) fail → emit
    `ShipsOfficePublishRejected` → throw (no state change). No phantom success events.
  - `ArchiveAsync` ordering: (1) call `IPermissionResolver`; (2a) pass → emit
    `ShipsOfficeDocumentArchived` pre-op → execute; (2b) fail → throw (no audit event
    needed for ArchiveAsync rejection — informational-only path).
- [ ] Implement `DocumentDiffService : IDocumentDiffService`
  — depends on `DiffPreviewView` in `Sunfish.UICore` (halt H2: W#46 Phase 3 build)
  — Phase 1 stub returns empty `DiffPreviewView` with `AccessibleRows = ["Diff not yet available"]`
- [ ] Implement `SUNFISH_SHIPSOFFICE_PERM001` Roslyn analyzer — warns on any call to
  `GetSnapshotAsync` or `SearchAsync` not preceded by a verifiable
  `IPermissionResolver.AuthorizeAsync(ShipAction.ViewShipsOffice, ...)` call site
  (per §2 caller-contract; mirrors W#48 SUNFISH_INTEGRATION_AUDIT001 pattern)
- [ ] Unit tests: `ShipsOfficeDataProvider` W9 TIN redaction (verify no `IFieldDecryptor` via reflection);
  `ShipsOfficeCommandService.PublishAsync` permission rejection flow;
  `ShipsOfficeCommandService.PublishAsync` audit pre-emission order
- [ ] Pre-merge council: **security-engineering subagent mandatory**
  (verify no decryption path; audit-before-operation; permission gate ordering)

**Phase 3 — `blocks-ships-office` Blazor UI**

- [ ] Scaffold `packages/blocks-ships-office/Sunfish.Blocks.ShipsOffice.csproj`
  — deps on `foundation-ships-office`, `foundation-ship-common`, `ui-core`,
  `blocks-leases`, `blocks-maintenance`, `foundation-catalog`
- [ ] Implement `ShipsOfficeBlock.razor` — root; search bar (`ISearchAsYouType`) + kind-filter
  chips + document list (paged); error state with retry
- [ ] Implement `DocumentListItem.razor` — kind badge (icon + text; SC 1.4.1) + title + status
  badge + updated-at; keyboard-operable row (Enter = open)
- [ ] Implement `DocumentDetailDrawer.razor` — slide-in panel; document metadata; action buttons
  (Publish / Archive per role); read-only content view
- [ ] Implement `DocumentDiffPanel.razor` — diff table (Path | Prior | New headers per ADR 0077
  §6.4 `DiffPreviewView.AccessibleRows`); added/removed labels (SC 1.3.3);
  color + text dual-encoding (SC 1.4.1); `<caption>` on diff table
- [ ] Implement `ShipsOfficeSearchBar.razor` — ARIA APG combobox;
  `role="combobox"` + `aria-expanded` + `aria-activedescendant`;
  `aria-live="polite"` result count region (SC 4.1.3)
- [ ] Unit tests: search bar `aria-expanded` state; document list item keyboard navigation;
  diff panel SC 1.3.3 text labels; permission-gated Publish button hidden when role ≠ XO+
- [ ] Pre-merge council: **WCAG/a11y subagent mandatory**
  (SC 1.3.3, SC 1.4.1, SC 1.4.4, SC 1.4.12, SC 2.1.1, SC 2.4.3, SC 2.4.5, SC 2.4.6, SC 4.1.3)

**Phase 4 — Anchor + Bridge wiring + apps/docs**

- [ ] Wire `services.AddSunfishShipsOffice(...)` in `accelerators/anchor/MauiProgram.cs`
- [ ] Wire `services.AddSunfishShipsOffice(...)` in `accelerators/bridge/Program.cs`
- [ ] Kitchen-sink demo: Ship's Office tab in Anchor demo shell
- [ ] `apps/docs/blocks/ships-office/overview.md`
- [ ] `apps/docs/foundation/ships-office/overview.md`
- [ ] `apps/docs/design-system/ships-office-wcag.md` — WCAG 2.2 AA declaration for SC 1.4.4 + SC 1.4.12

**Phase 5 — Phase 2 DynamicTemplate kind + ADR 0055 integration (gated on H4)**

- [ ] Add `ShipsOfficeDocumentKind.DynamicTemplate` (uncomment Phase 2 addition in §1)
- [ ] Wire `IFormSchemaStore` (ADR 0055 type) into `ShipsOfficeDataProvider`
- [ ] Wire full `IContentEditorSurface` implementation with ADR 0055 editor
- [ ] Pre-merge council: standard 4-perspective

**Phase 6 — Ledger flip + close**

- [ ] Update `icm/_state/active-workstreams.md`: W#55 row → `built`
- [ ] Write XO project memory update
- [ ] W#35 Ship Architecture cohort: mark fully complete (all 7 follow-on ADRs shipped)

---

## Open questions

1. **Kernel-signatures store interface.** The `SignatureEnvelope` type lives in
   `packages/foundation/Crypto/SignatureEnvelope.cs` as a Phase 0 stub. There is no
   `ISignatureEnvelopeStore` or equivalent query interface yet — ADR 0004 Stage 06 hand-off
   is not yet authored. In Phase 2, `ShipsOfficeDataProvider` MUST stub the
   `SignatureEnvelope` kind as an empty list until a queryable store interface ships.
   **Decision deferred.** The `SignatureEnvelope` kind is included in `ShipsOfficeDocumentKind`
   for forward-compat; Phase 2 implementation maps to empty list (not an error) until
   ADR 0004 Stage 06 lands.

2. **Markdown editor adapter surface.** `IContentEditorSurface` is declared in Phase 1
   contracts; the Phase 1 implementation is read-only stub (no edit capability).
   Full markdown editing with preview + publish requires an adapter-specific component
   (Blazor `Microsoft.AspNetCore.Components.Forms.InputTextArea` + preview engine;
   MAUI native text editor; React monaco-lite). **Decision deferred.** Phase 2 build
   wires the adapter implementations. ADR 0077 §6 component primitive contracts
   (`IFormControlContract`) are the composition target.

4. **Second-actor publish requirement.** Phase 1 allows any XO or Captain to publish a
   document they themselves authored (`LastModifiedBy == currentActor`). Cohort sibling
   ADR 0082 enforces a four-eyes constraint on `IMedevacService.AuthorizeAsync`.
   Should `PublishAsync` reject self-publishing? For regulated-industry tenants (§Decision
   drivers item 4), self-publish without a second reviewer is a governance gap.
   **Decision deferred.** Phase 1 ships without a four-eyes constraint on publish.
   `ShipsOfficeOptions` will expose `RequireSecondActorPublish: bool` (default `false`)
   as an opt-in flag for regulated tenants in Phase 2. A1 amendment when Phase 2 ships.

3. **Search index.** Phase 1 `ShipsOfficeDataProvider.SearchAsync` is an in-memory
   linear scan (acceptable for ≤500 documents per `SnapshotPageSize`). Phase 2+ tenants
   with large document corpora need a search index (Foundation.Taxonomy + full-text search
   extension per ADR 0056). **Decision deferred.** Phase 1 in-memory scan is explicit;
   the interface signature supports pagination via `PageToken` for Phase 2 backend switch.

---

## Revisit triggers

- ADR 0055 `Status: Accepted` → add `ShipsOfficeDocumentKind.DynamicTemplate` + wire ADR 0055
  `IFormSchemaStore` into Phase 5 build.
- ADR 0004 Stage 06 ships `ISignatureEnvelopeStore` → wire `SignatureEnvelope` kind in
  `ShipsOfficeDataProvider` (remove empty-list stub).
- ADR 0065-A1 `IStandingOrderEventStream` ships → remove polling fallback in
  `ShipsOfficeDataProvider.SubscribeChangesAsync`.
- `ShipRole` enum exceeds 32 values → evaluate flags pattern for `IPermissionResolver`
  permission set.
- Phase 2 commercial scope: Bridge-side multi-tenant document management needs cross-tenant
  Scribe isolation (tenant-admin Scribe MUST NOT browse another tenant's documents).
  Current `TenantId` scoping is sufficient for Phase 1 Anchor use case.

---

## References

### Predecessor and sister ADRs

- [ADR 0007](./0007-business-case-bundles.md) — `BusinessCaseBundleManifest` + `BundleStatus`
- [ADR 0021](./0021-lease-document-signatures.md) — W#21 kernel-signatures
- [ADR 0054](./0054-electronic-signature-capture-and-document-binding.md) — `LeaseDocumentVersion` + `ILeaseDocumentVersionLog`
- [ADR 0058](./0058-vendor-onboarding.md) — `W9Document` + `IW9DocumentService` + `EncryptedField` TIN
- [ADR 0062](./0062-mission-space-negotiation-protocol.md) — `IMissionEnvelopeObserver` subscription push
- [ADR 0065](./0065-wayfinder-system-and-standing-order-contract.md) — `IStandingOrderIssuer`
- [ADR 0077](./0077-shared-design-system.md) — `ShipRole.Scribe` + `ShipLocation.ShipsOffice` +
  `IDiffPreview<TPath, TValue>` + `ISearchAsYouType<THit>` + `DiffPreviewView`

### Intake + discovery

- Ship's Office intake: `icm/00_intake/output/2026-05-01_ships-office-content-aggregation-intake.md`
- W#35 Ship Architecture discovery: `icm/01_discovery/output/2026-05-01_ship-architecture.md` §5.6 + §8.7

---

## Pre-acceptance audit

- [x] **UPF Stage 0 check.** Predecessor substrates verified present on origin/main:
  `ILeaseDocumentVersionLog` (`packages/blocks-leases/Services/ILeaseDocumentVersionLog.cs` ✓);
  `W9Document` + `IW9DocumentService` (`packages/blocks-maintenance/Models/W9Document.cs` +
  `.../Services/IW9DocumentService.cs` ✓); `SignatureEnvelope`
  (`packages/foundation/Crypto/SignatureEnvelope.cs` ✓);
  `BusinessCaseBundleManifest` (`packages/foundation-catalog/Bundles/BusinessCaseBundleManifest.cs` ✓).
  30-day decay check (packages last modified April-May 2026; green).

- [x] **Risk assessment.** LOW for Phase 1 (contracts only; additive to existing packages).
  MEDIUM for Phase 2 (W9 redaction invariant — security council mandatory; IFieldDecryptor
  prohibition reflection test required). MEDIUM for Phase 3 (WCAG/a11y council mandatory;
  long-form reading + diff accessibility).

- [x] **Cited-symbol verification.**
  - `ShipRole.Scribe` — introduced by ADR 0077 (PR #543 merged; `ready-to-build`; NOT yet built —
    halt-condition H1: build begins only after W#46 Phase 1 lands `ShipRole` on origin/main).
  - `ShipLocation.ShipsOffice` — introduced by ADR 0077 (same halt H1).
  - `IDiffPreview<TPath, TValue>` + `DiffPreviewView` — introduced by ADR 0077 §6.4;
    `Sunfish.UICore` package; NOT yet built — halt-condition H2. `IDocumentDiffService`
    is declared in `blocks-ships-office` (NOT `foundation-ships-office`) to avoid
    foundation→ui-core dependency violation (B-1 council finding). Phase 1 stub returns
    placeholder rows; full implementation requires W#46 Phase 3 build.
  - `ISearchAsYouType<THit>` — introduced by ADR 0077 §6.5; NOT yet built — halt-condition H3:
    `ShipsOfficeSearchBar.razor` Phase 3 uses this interface; acceptable stub if W#46 Phase 1
    precedes Phase 3 build.
  - `ILeaseDocumentVersionLog` — in `Sunfish.Blocks.Leases.Services` (built W#22/W#27; verified ✓).
  - `IW9DocumentService` — in `Sunfish.Blocks.Maintenance.Services` (built W#18 Phase 4; verified ✓).
  - `W9Document`, `W9DocumentId` — in `Sunfish.Blocks.Maintenance.Models` (built W#18; verified ✓).
  - `SignatureEnvelope` — in `Sunfish.Foundation.Crypto` (Phase 0 stub; verified ✓); NO queryable
    store yet — see Open Question 1 for Phase 2 stub policy.
  - `BusinessCaseBundleManifest` — in `Sunfish.Foundation.Catalog.Bundles` (built; verified ✓).
  - `IMissionEnvelopeObserver` — in `Sunfish.Foundation.MissionSpace` (ADR 0062; built W#40; verified ✓).
  - `IAuditTrail`, `AuditEventType` — existing kernel-audit (built; verified ✓).
  - `IFieldDecryptor`, `IFieldEncryptor`, `EncryptedField` — in `foundation-recovery`
    (ADR 0046-A2; built W#32; verified ✓). Note: these types are explicitly FORBIDDEN from
    `IShipsOfficeDataProvider` implementations; the prohibition is enforced by reflection test.
  - `DynamicTemplate` kind and `IFormSchemaStore` — deferred to Phase 5; gated on ADR 0055
    reaching Status: Accepted (halt H4). NOT referenced in Phase 1-4.
  - `ISignatureEnvelopeStore` — does NOT exist (no ADR 0004 Stage 06 yet); Phase 2 stub
    returns empty list for `SignatureEnvelope` kind. See Open Question 1.

- [x] **Anti-pattern scan.**
  AP-1 (unvalidated assumptions): Open Questions §1–§3 explicit; halt-conditions listed.
  AP-3 (vague phases): Phase 1 has 9 discrete checklist items.
  AP-11 (zombie project): Revisit triggers named; ADR 0055 + ADR 0004 Stage 06 are explicit triggers.
  AP-21 (cited-symbol drift): all symbols verified above.
  AP-15 (premature precision): W9 TIN redaction prohibition is load-bearing (security) — intentional.

- [x] **Council review posture.** Standard adversarial (4 perspectives) for Phases 1/4.
  Security-engineering subagent mandatory for Phase 2 (IFieldDecryptor prohibition + audit
  pre-emission + permission gate ordering). WCAG/a11y subagent mandatory for Phase 3
  (SC 1.3.3, SC 1.4.1, SC 1.4.4, SC 1.4.12, SC 2.4.5, SC 4.1.3 — long-form document surfaces).
  Pre-merge canonical for every phase.

- [x] **Halt conditions (H1-H6).**
  - H1: `ShipRole.Scribe` + `ShipLocation.ShipsOffice` on origin/main — W#46 Phase 1 build.
  - H2: `IDiffPreview<TPath, TValue>` + `DiffPreviewView` in `Sunfish.UICore` — W#46 Phase 3
    build; Phase 1 `IDocumentDiffService` stub acceptable.
  - H3: `ISearchAsYouType<THit>` in `Sunfish.UICore` — W#46 Phase 1; Phase 3 Blazor component
    stub acceptable if W#46 Phase 1 precedes Phase 3.
  - H4: ADR 0055 Status: Accepted — `DynamicTemplate` kind + `IFormSchemaStore` gate.
  - H5: ADR 0065-A1 `IStandingOrderEventStream` — polling fallback until A1 ships; no hard gate.
  - H6: ADR 0004 Stage 06 `ISignatureEnvelopeStore` — empty-list stub for `SignatureEnvelope` kind
    until store interface ships; no hard gate.
