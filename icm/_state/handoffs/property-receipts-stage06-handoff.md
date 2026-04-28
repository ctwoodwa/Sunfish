# Hand-off — Property-Receipts domain block (first-slice, kernel-only)

**From:** research session
**To:** sunfish-PM session
**Created:** 2026-04-28 (revised 2026-04-28 for cluster naming consistency)
**Status:** `ready-to-build` (gated on Property-Assets first-slice merging — Receipt FK to Asset)
**Revision note:** Renamed from `packages/blocks-receipts/` → `packages/blocks-property-receipts/` for cluster-level naming consistency. No existing collision; rename adopts the convention after Assets discovered the `blocks-assets` collision.
**Spec source:** Cluster intake [`property-receipts-intake-2026-04-28.md`](../../00_intake/output/property-receipts-intake-2026-04-28.md) (Stage 00) + cluster INDEX [`property-ops-INDEX-intake-2026-04-28.md`](../../00_intake/output/property-ops-INDEX-intake-2026-04-28.md)
**Approval:** Cluster intake names Receipts as a domain module after Properties + Assets land. This hand-off compresses Stages 01–05 for the kernel-only first-slice scope. iOS Vision OCR capture, email-attachment ingestion, and typed FKs to Vendor/WorkOrder are deferred to follow-up hand-offs (those modules don't exist yet).
**Estimated cost:** ~3–5 hours sunfish-PM (similar shape to Properties first-slice; no lifecycle event log; opaque-string FK reservations)
**Pipeline:** `sunfish-feature-change`
**Blocked by:** Properties first-slice (workstream #17) + Assets first-slice (workstream #24) merging — Receipt.Property + Receipt.Asset FK targets

---

## Context (one paragraph)

Receipts are evidence of past payment events with multiple roles: cost-basis evidence (asset acquisition), payment evidence (vendor 1099 supporting documentation), expense categorization (P&L line item), tax-deduction evidence (tax-advisor consumes annually). The same captured artifact threads through all of these. First-slice scope is **kernel-only persistence**: ship the Receipt entity + categorization + CRUD + tests. **The two ingestion paths surfaced in the cluster intake (iOS Vision OCR + email-attachment via messaging substrate) are explicitly out-of-scope here** — they're gated on iOS App intake (#23) and ADR 0052 acceptance (workstream #20) respectively. Persistence first; capture flows when the substrates they depend on ship. This pattern intentionally mirrors Properties + Assets: ship the kernel root, defer surfaces.

---

## Phases (binary gates)

### Phase 1 — Scaffold `packages/blocks-property-receipts/`

**Files:**

- **NEW** `packages/blocks-property-receipts/Sunfish.Blocks.PropertyReceipts.csproj` — references `Sunfish.Foundation`, `Sunfish.Foundation.MultiTenancy`, `Sunfish.Foundation.Persistence`, `Sunfish.Blocks.Properties` (FK target), `Sunfish.Blocks.Assets` (optional FK target), `Sunfish.Kernel.Audit` (event emission per ADR 0049 if Receipt-create emits)
- **NEW** Add to `Sunfish.slnx` under `/blocks/receipts/`

**PASS gate:** `dotnet build` green; provider-neutrality analyzer passes.

### Phase 2 — `ReceiptId` + `Receipt` entity + `ReceiptCategory`

**Files:**

- **NEW** `packages/blocks-property-receipts/Models/ReceiptId.cs` — mirror `PropertyId` / `AssetId` shape (record struct + JSON converter + `NewId()`)
- **NEW** `packages/blocks-property-receipts/Models/ReceiptCategory.cs`

```csharp
public enum ReceiptCategory
{
    Depreciable,        // capital improvements; feeds asset cost basis
    RepairAndMaintenance,
    Supplies,
    Utilities,
    Insurance,
    Tax,                // property tax, license fees
    Professional,       // legal, accounting, consulting
    Travel,             // mileage-related
    Other
}
```

(More structured taxonomy is Phase 2.3+ once tax-advisor reporting requirements are firm.)

- **NEW** `packages/blocks-property-receipts/Models/Receipt.cs`

```csharp
public sealed record Receipt
{
    public required ReceiptId Id { get; init; }
    public required TenantId Tenant { get; init; }

    // FK reservations (typed where target exists; opaque string where target hasn't shipped)
    public required PropertyId Property { get; init; }                  // FK; receipts are property-scoped
    public AssetId? Asset { get; init; }                                // optional; set when receipt evidences asset acquisition
    public string? VendorRef { get; init; }                             // opaque string; converts to VendorId? when Vendors module ships
    public string? WorkOrderRef { get; init; }                          // opaque string; converts to WorkOrderId? when Work Orders module ships
    public string? PaymentRef { get; init; }                            // opaque string; converts to PaymentId? / ChargeId? when blocks-rent-collection.Payment is migrated to ADR 0051

    // Money — placeholder until ADR 0051 (Money struct) is Accepted
    public required decimal Amount { get; init; }                       // TODO: Money — gated on ADR 0051 acceptance
    public required string CurrencyCode { get; init; }                  // ISO 4217; "USD" for Phase 2; replaces with CurrencyCode struct from ADR 0051

    public required DateTimeOffset TransactionDate { get; init; }       // when the receipt was issued
    public required DateTimeOffset CaptureDate { get; init; }           // when Sunfish ingested it
    public required ReceiptCategory Category { get; init; }
    public string? PaymentMethod { get; init; }                         // free-text "cash"|"check"|"ach"|"card-visa-1234"|...
    public string? VendorName { get; init; }                            // free-text vendor name when no Vendor record yet
    public string? Description { get; init; }                           // free-text purchase description
    public string? PrimaryPhotoBlobRef { get; init; }                   // FK reservation (blob ingest gated on cluster OQ3)
    public string? ExtractedTextOcr { get; init; }                      // populated by future iOS Vision OCR flow; null in kernel-only first-slice
    public required ReceiptSource Source { get; init; }                 // tracks ingestion path
    public required ReconciliationStatus Reconciliation { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public IdentityRef? CapturedBy { get; init; }                       // who created the record (owner / bookkeeper / future iOS app)
}

public enum ReceiptSource
{
    ManualEntry,        // typed in Anchor / Bridge cockpit
    MobileCapture,      // future: iOS Vision OCR (workstream #23)
    EmailAttachment,    // future: messaging substrate inbound (workstream #20)
    Imported            // bulk import (CSV / spreadsheet migration tool)
}

public enum ReconciliationStatus
{
    Pending,            // not yet matched to a payment
    Matched,            // matched to a payment record
    Unmatched,          // payment expected but not found
    Manual              // owner explicitly resolved without auto-match
}
```

**PASS gate:** Compiles; XML doc on every public member; round-trip JSON test on `ReceiptId` + `Receipt`.

### Phase 3 — `ReceiptLineItem` (optional child entity, included for forward-compat)

**Files:**

- **NEW** `packages/blocks-property-receipts/Models/ReceiptLineItem.cs`

```csharp
public sealed record ReceiptLineItem
{
    public required Guid Id { get; init; }
    public required ReceiptId Receipt { get; init; }
    public required string Description { get; init; }
    public required decimal Amount { get; init; }                       // TODO: Money on ADR 0051
    public required string CurrencyCode { get; init; }                  // matches parent receipt
    public int? Quantity { get; init; }
    public ReceiptCategory? Category { get; init; }                     // optional override per line; null = inherit from parent
    public AssetId? AssetRef { get; init; }                             // optional per-line asset assignment for split receipts
}
```

(Phase-2.1a workflow may not need line-items — owner often categorizes the whole receipt under one category. Field reserved for cases where one Home Depot receipt covers a roof repair AND a fridge replacement.)

**PASS gate:** Compiles; one round-trip JSON test.

### Phase 4 — `IReceiptRepository` + in-memory CRUD + ISunfishEntityModule

**Files:**

- **NEW** `packages/blocks-property-receipts/IReceiptRepository.cs`

```csharp
public interface IReceiptRepository
{
    Task<Receipt?> GetByIdAsync(TenantId tenant, ReceiptId id, CancellationToken ct);
    Task<IReadOnlyList<Receipt>> ListByPropertyAsync(TenantId tenant, PropertyId property, CancellationToken ct);
    Task<IReadOnlyList<Receipt>> ListByAssetAsync(TenantId tenant, AssetId asset, CancellationToken ct);
    Task<IReadOnlyList<Receipt>> ListByVendorRefAsync(TenantId tenant, string vendorRef, CancellationToken ct);
    Task<IReadOnlyList<Receipt>> ListByCategoryAsync(TenantId tenant, ReceiptCategory category, DateTimeOffset? fromDate, DateTimeOffset? toDate, CancellationToken ct);
    Task<IReadOnlyList<Receipt>> ListByReconciliationStatusAsync(TenantId tenant, ReconciliationStatus status, CancellationToken ct);
    Task UpsertAsync(Receipt receipt, CancellationToken ct);
    Task DeleteAsync(TenantId tenant, ReceiptId id, CancellationToken ct);  // hard-delete: receipts are user-correctable; no soft-delete here
}

public interface IReceiptLineItemRepository
{
    Task<IReadOnlyList<ReceiptLineItem>> ListByReceiptAsync(TenantId tenant, ReceiptId receipt, CancellationToken ct);
    Task UpsertAsync(ReceiptLineItem lineItem, CancellationToken ct);
    Task DeleteAsync(Guid lineItemId, CancellationToken ct);
}
```

- **NEW** `packages/blocks-property-receipts/InMemoryReceiptRepository.cs` — thread-safe; all queries; tenant isolation
- **NEW** `packages/blocks-property-receipts/InMemoryReceiptLineItemRepository.cs`
- **NEW** `packages/blocks-property-receipts/ReceiptEntityModule.cs` — ISunfishEntityModule registration per ADR 0015 (registers Receipt + ReceiptLineItem entities)
- **NEW** `packages/blocks-property-receipts/ServiceCollectionExtensions.cs` — `AddReceiptBlock(this IServiceCollection services)`

**PASS gate:** All files compile; runtime DI resolves both repositories + entity module.

### Phase 5 — Audit emission for receipt lifecycle

Receipts are not append-only events themselves (a typo in amount or category needs fixing). But create / update / delete events should audit-log per ADR 0049.

**Files:**

- **EDIT** `packages/blocks-property-receipts/InMemoryReceiptRepository.cs` — `UpsertAsync` and `DeleteAsync` emit to `IAuditTrail` (existing kernel-audit substrate). Audit record types:
  - `ReceiptCreated`
  - `ReceiptUpdated`
  - `ReceiptDeleted`
  - `ReceiptReconciled` (when ReconciliationStatus transitions)

  Pattern: mirror existing kernel-audit subtype emission patterns from PR #190 + #198 (Tier 1 retrofit) + workstream #2 patterns.

**PASS gate:** Audit emission verified by existing kernel-audit test harness; new audit subtypes registered.

### Phase 6 — Tests + kitchen-sink demo seed

**Files:**

- **NEW** `packages/blocks-property-receipts/tests/Sunfish.Blocks.PropertyReceipts.Tests.csproj`
- **NEW** `packages/blocks-property-receipts/tests/ReceiptTests.cs` — record equality + JSON round-trip + enum coverage
- **NEW** `packages/blocks-property-receipts/tests/InMemoryReceiptRepositoryTests.cs` — Get/List/Upsert/Delete; tenant isolation; per-property + per-asset + per-vendor-ref + per-category + per-reconciliation-status filters; date-range filter on category list
- **NEW** `packages/blocks-property-receipts/tests/ReceiptAuditEmissionTests.cs` — verify Create/Update/Delete/Reconciled audit emission
- **NEW** `packages/blocks-property-receipts/tests/ReceiptLineItemTests.cs` + repository tests
- **NEW** seed data in `apps/kitchen-sink/`:
  - For Property "123 Main St" + the seed water heater asset: receipt for water heater purchase ($1,200; category Depreciable; asset FK set); receipt for plumber labor ($450; category RepairAndMaintenance; vendorRef opaque string "plumber-acme")
  - For Property "456 Oak Ave": receipt for HVAC service ($380; category RepairAndMaintenance); receipt for property tax ($4,200; category Tax)
  - One receipt with line-items demonstrating the split-receipt case (Home Depot trip with roof patch supplies + fridge replacement)

**PASS gate:** `dotnet test packages/blocks-property-receipts/tests/` returns 0 failures; kitchen-sink boots and seed receipts render/log alongside seed properties + assets.

### Phase 7 — Documentation

**Files:**

- **NEW** `apps/docs/blocks/receipts.md` — block summary, field reference for `Receipt` + `ReceiptLineItem`, ReceiptSource + ReceiptCategory + ReconciliationStatus enum tables, "what's not in this slice" deferred list (iOS Vision OCR; email-attachment ingestion; typed FKs to Vendor/WorkOrder/Payment; Money struct migration), cross-link to cluster intake.

**PASS gate:** apps/docs builds; new doc page renders.

### Phase 8 — Workstream ledger flip

**Files:**

- **EDIT** `icm/_state/active-workstreams.md` row #26 (Receipts domain): Status → `built` (merged); reference merged PR; notes append "First-slice (Receipt entity + ReceiptLineItem + categorization + CRUD + audit emission) shipped. iOS Vision OCR capture + email-attachment ingestion + typed FK conversions queued as separate hand-offs."

**PASS gate:** Ledger updated; PR ready to merge.

---

## Out of scope (explicit deferred to follow-up hand-offs)

- **iOS Vision OCR capture flow** — gated on iOS Field App intake (#23). Receipt has `ExtractedTextOcr: string?` field reservation; first-slice leaves it null. Capture path is a separate cluster intake.
- **Email-attachment ingestion path** — gated on ADR 0052 (Bidirectional Messaging Substrate) acceptance + `blocks-messaging` first-slice. Receipt has `Source.EmailAttachment` enum value reserved; first-slice never produces it.
- **Money struct migration** — gated on ADR 0051 (Foundation.Integrations.Payments) acceptance. First-slice uses `decimal Amount` + `string CurrencyCode` placeholders. One-line follow-up commit when ADR 0051 lands.
- **Typed FK conversions** — when Vendors / Work Orders / Payments modules ship:
  - `string? VendorRef` → `VendorId?`
  - `string? WorkOrderRef` → `WorkOrderId?`
  - `string? PaymentRef` → `PaymentId?` (or `ChargeId?` per ADR 0051)
  Each conversion is a small api-change follow-up.
- **Reconciliation auto-matching** — `IReceiptRepository.ListByReconciliationStatusAsync` exposes the query surface; the actual auto-match pipeline (matching receipts to bank-line ledger entries) lives in Phase 2 commercial intake's `blocks-accounting` reconciliation deliverable.
- **CSV / spreadsheet bulk import** — Phase 2 onboarding work; separate hand-off.
- **Tax-advisor depreciation projection** — `blocks-tax-reporting` consumer; aggregation reads receipts but not receipts module's job.

---

## What sunfish-PM should NOT touch

- `packages/blocks-properties/` (consumer only; Property FK)
- `packages/blocks-assets/` (consumer only; Asset FK)
- `packages/blocks-rent-collection/` (sibling block; receipts here are property-expense-side, not rent-collection-side; no overlap)
- `accelerators/` (consumer integration deferred)
- iOS app (doesn't exist)
- ADR documents (no new or amended ADRs at this slice)

---

## Open questions sunfish-PM should flag back to research

1. **Soft-delete vs hard-delete.** Properties + Assets first-slices use soft-delete (DisposedAt + DisposalReason). Receipts could go either way. **Recommend hard-delete** — receipts are correctable artifacts, not lifecycle records. Deletion fires `ReceiptDeleted` audit event so the trail is preserved even when the row isn't. Flag if disagree.
2. **PaymentRef vs PaymentId<T> migration scope.** When `blocks-rent-collection.Payment` migrates to `Money Amount` + `PaymentMethodReference Method` per ADR 0051 implementation, does Receipt's PaymentRef converge to `ChargeId?` (the new payment ID per ADR 0051) or stay as `PaymentId?` (block-level rent-collection ID)? Probably `ChargeId?` for cross-block uniformity. **Flag at the conversion follow-up hand-off; first-slice opaque string is fine.**
3. **Reconciled-status transitions and state-machine.** Should ReconciliationStatus have a guarded state machine (Pending → Matched / Unmatched / Manual) or is it a free-set field? **Recommend free-set with audit emission on every change** — receipts are user-correctable; constraints get in the way.
4. **kitchen-sink rendering pattern.** Match whatever Properties + Assets first-slices used (UI page or startup logging).

---

## Acceptance criteria (research-session sign-off)

- [ ] All 8 phases complete with PASS gates
- [ ] `dotnet build` + `dotnet test` repo-wide green
- [ ] Provider-neutrality analyzer passes on `blocks-property-receipts`
- [ ] kitchen-sink demo seed renders/logs receipts per property + asset
- [ ] `apps/docs/blocks/receipts.md` exists with deferred-list
- [ ] Workstream #26 ledger row flipped to `built` (merged) with PR link
- [ ] PR description names Phase 1 (this hand-off) as the slice scope; flags iOS OCR + email-attachment + Money migration + typed FK conversions as deferred to follow-up hand-offs
- [ ] No code outside `packages/blocks-property-receipts/`, `Sunfish.slnx`, `apps/kitchen-sink/<seed>`, `apps/docs/blocks/receipts.md`, `icm/_state/active-workstreams.md` is touched

---

## After this hand-off ships

Research session will write the next two hand-offs to keep the queue full:

- `property-receipts-ios-capture-handoff.md` — iOS Vision OCR capture flow (gated on iOS App intake/ADR + Bridge blob-ingest API spec)
- `property-receipts-email-attachment-handoff.md` — Email-attachment ingestion (gated on ADR 0052 acceptance + `blocks-messaging` first-slice)
- `property-receipts-money-migration-handoff.md` — Money struct + typed FK conversions (gated on ADR 0051 acceptance + Vendors/WorkOrders modules shipping)

Plus other cluster module first-slices (Inspections is naturally next; depends on Assets first-slice + Signatures ADR for move-in/out sign-off).

---

## Sign-off

Research session — 2026-04-28
