# Hand-off — `blocks-financial-ap` Bill + BillLine + BillStatus + AP posting (Phase 1 critical path)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-16
**Status:** `ready-to-build` (gated — see §Gate conditions below)
**Workstream:** W#60 P4 — Path II native domain, AP cluster (Phase 1 critical path)
**Spec source:** [`icm/02_architecture/blocks-financial-schema-design.md`](../../02_architecture/blocks-financial-schema-design.md) §3.7–§3.8, §6.4, §7, §8.2, §10.3
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) (Proposed; ratified by CO 2026-05-16)
**Ratifications:** `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md`
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~8–10h sunfish-PM (4 PRs; ~20–25 tests + docs)
**PR count:** 4 PRs
**Pre-merge council:** NOT required (substrate scope; mirrors the W#34/W#35/W#36/W#60-P4 substrate-only pattern from sibling packages). Standard COB self-audit applies. **EXCEPTION:** if `IBillService` interface would materially break the `blocks-financial-payments` dependency contract (e.g. removing or renaming methods that `PaymentApplicationService` calls on `IBillRepository`), **halt and council-review** the breaking change — file `cob-question-*` first.
**Audit before build:**
```bash
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-financial-(ap|ar|tax|ledger)"
```
Expected at this hand-off's start: `blocks-financial-ar/`, `blocks-financial-tax/`, and `blocks-financial-ledger/` all exist (all gates cleared); nothing matching `blocks-financial-ap/`.

---

## Gate conditions

This hand-off is **gated on two predecessors**. COB must verify both before opening PR 1:

1. **`blocks-financial-ar` shipped** (all 6 PRs merged). The AR hand-off establishes the AR pattern that AP mirrors: record shapes, ULID-id convention, InMemory repositories, tax-calculation stub delegation, posting algorithm discipline, event bus stubs, and the ERPNext importer contract. AP lifts all of these patterns directly.

2. **`blocks-financial-tax` shipped** (all PRs merged). `BillLine.taxCodeId` is an FK into the tax cluster; the AP tax helper (`computeLineTax`) delegates to `ITaxCalculationService` from `blocks-financial-tax`. Without the real tax service, the AP package ships with the same `NoOpTaxCalculationService` stub used by AR — which is acceptable — but the `TaxCodeId` strong-type from `blocks-financial-tax` must be resolvable at compile time.

If either gate is unmet, **STOP** — drop a `cob-question-*` beacon naming the unmet gate.

---

## Context

### Phase 1 critical-path position

Per ADR 0088 §1 + the sibling `blocks-financial-ar-stage06-handoff.md`, the Phase 1 financial-cluster decomposition is:

```
blocks-financial-ledger    (Chart + Journal core — substrate)   ✓ shipped
blocks-financial-periods   (FiscalYear + FiscalPeriod)          (sibling hand-off, parallel)
blocks-financial-tax       (TaxCode + TaxRate + TaxJurisdiction)(sibling hand-off, parallel — gate for AP)
blocks-financial-ar        (Invoice + InvoiceLine — AR side)    (predecessor — gate for AP)
blocks-financial-ap        ← THIS HAND-OFF
blocks-financial-payments  (Payment + PaymentApplication)       (follow-on; consumes Bill.Id via PaymentApplication.targetId)
```

`blocks-financial-ap` is the **mirror image of AR on the credit side** — it models what we owe suppliers rather than what customers owe us. Its completion unblocks:

- `blocks-financial-payments`: `PaymentApplication.appliedTo = "Bill"` cannot be modeled without `BillId` available on main.
- `blocks-reports-ap-aging`: consumes `IApAgingService` to render the AP aging report.
- `tooling-anchor-import`: the ERPNext Pass 4 `importPurchaseInvoice(bill, targetChartId)` call (per schema design §10.3) requires this hand-off's `IErpnextPurchaseInvoiceImporter`.
- Schedule-E / cashflow-projection: per schema design §6.7, the cashflow model queries open Bills by due date; without AP entities on main, the projection cannot query the payables side.

### What this hand-off ships

Per `blocks-financial-schema-design.md` §3.7–§3.8, §8.2, §10.3:

1. **`Bill`** record entity (header) — mirror of `Invoice` on the payables side; `vendorId`, `billDate`, `dueDate`, `receivedDate`, `apAccountId`, per-line debit accounts, optional approval gate.
2. **`BillLine`** record entity (line items) — mirror of `InvoiceLine` with `debitAccountId` instead of `incomeAccountId`; accepts Expense or Asset account subtypes.
3. **`BillStatus`** state machine — `Draft → Received → Approved? → PartiallyPaid → Paid`; `Disputed` hold status; `Voided` with reversal JE. `Overdue` is derived (not persisted), identical discipline to AR's `Overdue`.
4. **`IBillPostingService`** — on `record()` transition (`Draft → Received`), posts a `JournalEntry` via `IJournalPostingService` (Dr Expense/Asset, Cr AP); on `void()`, posts a reversing JE. Idempotent.
5. **`IApAgingService`** — AP aging bucket algorithm (mirror of `IArAgingService`); by vendor and by property; parameterizable `asOf` date.
6. **ERPNext importer integration** — `IErpnextPurchaseInvoiceImporter` consuming the migration-importer spec's Pass 4 (`importPurchaseInvoice`).

### What this hand-off does NOT ship

- `Payment` and `PaymentApplication` — comes with `blocks-financial-payments`. The `applyPayment(partial/full)` transitions in the AP state machine update `Bill.AmountPaid` + `Bill.Balance` via direct repository upsert (test seam only), exactly as AR does for invoice payment seeding in its aging tests.
- Approval workflow UI — the `approve()` transition is a service method; the UI shell lives in the Anchor accelerator (separate workstream).
- Vendor-statement query — analogous to AR's customer-statement query; deferred to `blocks-reports-ap-aging`. Ship only the `IApAgingService` query method in this hand-off.
- Bank reconciliation — separate Phase 1 hand-off.
- `BidProposal` / purchase-order → bill conversion (schema design §3.12) — Phase 2+.

### Why AP ships after AR (not in parallel)

The decision to sequence AP after AR (rather than building them in parallel) is intentional:

1. **Pattern lift.** AR establishes the posting algorithm, CRDT discipline, importer contract, and repository conventions. AP is a near-identical mirror; COB builds it by lifting the AR pattern, not designing from scratch. Parallelism would require designing both in parallel — higher cognitive load, higher risk of divergence.
2. **Tax delegation.** Both AR and AP call `ITaxCalculationService`. If the AR package's `NoOpTaxCalculationService` stub shape changes during AR authoring, AP inherits the final shape rather than a draft shape.
3. **`blocks-financial-tax` gate alignment.** The tax gate applies to both; by the time AR is done, tax is also expected to be on main.

### CRDT-friendly conventions applied

AP applies the same CRDT-friendly schema conventions as AR:

| Convention | Applied where |
|---|---|
| §1 ULID identifiers | `BillId`, `BillLineId` — strongly typed; ULID storage |
| §2 Soft-delete tombstones | `Bill.DeletedAtUtc` / `DeletedBy`; hard-delete only on `Draft` state |
| §3 version + revisionVector | `Bill.Version` int + `Bill.RevisionVector` Dictionary<string,long> — Loro-managed |
| §4 Append-only sub-collections | `BillLine[]` is append-only AFTER `Received`; corrections via credit-bill + reversal JE |
| §5 Stable string codes | `BillStatus` surfaces as a string code over the wire; persistent storage as text |
| §6 Posted-then-immutable | Once `Bill.status ∈ {Received, Approved, PartiallyPaid, Paid}`, header fields are immutable; status transitions allowed |
| §7 State-machine-under-CRDT pattern B — terminal wins | `Voided` > `Paid` > `PartiallyPaid` > `Received`; `Disputed` does not win over terminal states |
| §10 Two-tier validation | Tier-1 write-time; Tier-2 post-merge reconciler stub — `sum(applications.amountApplied) <= bill.total`; emits `PaymentOverapplied` if violated (lands with `blocks-financial-payments`) |

`Disputed` status handling: `Disputed` is a **hold** — it does not advance the bill toward payment and does not affect the GL. The CRDT terminal-wins rule ignores `Disputed`; if one replica marks the bill Disputed while another marks it Received/Approved, the non-Disputed state wins (the bill must be actively paid out; Disputed is a user-flow gate, not a terminal).

### Bill numbering — no per-replica scheme

Unlike Invoices, Bills carry `billNumber` set to **the vendor's own invoice number** (not a Sunfish-generated sequence). This is a key difference from AR:

- `Bill.BillNumber` stores whatever the vendor prints on their invoice (e.g. `VND-2026-001`).
- Uniqueness is enforced per-chart + per-vendor (not globally): `(chartId, vendorId, billNumber)` is the natural unique key.
- Sunfish does not auto-generate bill numbers; there is no `IBillNumberingService`.
- The ERPNext importer populates `BillNumber` from `Purchase Invoice.bill_no` (the supplier's reference); falls back to the ERPNext docname if `bill_no` is null.

This eliminates the `ReplicaId` complexity from AR's PR 2. AP is simpler: 4 PRs, not 6.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify `blocks-financial-ar` is built.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ar/
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ar/Services/IBillPostingService.cs 2>&1
   ```
   Wait — `IBillPostingService` lives in `blocks-financial-ap`, not `blocks-financial-ar`. Check instead:
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ar/Services/IInvoicePostingService.cs 2>&1
   ```
   Expected: file exists. If `blocks-financial-ar/` does not exist, **STOP**.

2. **Verify `blocks-financial-tax` is built.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-tax/ 2>&1
   grep -rn "ITaxCalculationService" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-tax/ 2>/dev/null | head -5
   ```
   If `blocks-financial-tax/` is absent, proceed with the `NoOpTaxCalculationService` stub from AR (see Halt §3). If present, import and use its `ITaxCalculationService` directly — do not re-define the stub locally.

3. **Verify `blocks-financial-ledger` is built.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/Services/IJournalPostingService.cs 2>&1
   ```
   Expected: exists. `IBillPostingService` consumes `IJournalPostingService` from the ledger.

4. **Confirm ADR 0088 status.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: `status: Proposed` (CO ratified design 2026-05-16; status-flip is housekeeping). Hand-off is `ready-to-build` regardless — CO directive operative.

5. **Confirm no parallel-session PRs touch `blocks-financial-ap` or `blocks-financial-payments`.**
   ```bash
   gh pr list --state open --search "blocks-financial-ap in:title,body"
   gh pr list --state open --search "blocks-financial-payments in:title,body"
   ```
   Expected: empty (or only this hand-off's own PRs). If anything else is open, file `cob-question-*`.

6. **Read the Stage 02 design source sections.** Skim `blocks-financial-schema-design.md` §3.7, §3.8, §6.4, §7, §8.2, §10.3. Read the AR hand-off's PR 1–3 patterns if uncertain about the C# shape conventions — AP mirrors them directly.

7. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler`).

---

## Per-PR deliverables

This hand-off splits into **4 PRs** by responsibility:

- PR 1: Package scaffold + `Bill` entity + `BillLine` entity + `BillStatus` enum + `IBillRepository`
- PR 2: `IBillPostingService` — `record()` (Draft→Received) + `void()` + `dispute()` + approval gate
- PR 3: `IApAgingService` — bucket algorithm + per-vendor + per-property breakdowns
- PR 4: ERPNext `IErpnextPurchaseInvoiceImporter` + DI extension `AddBlocksFinancialAp()` + docs

PRs 1 + 2 are strictly sequential. PR 3 depends on PR 1 (can parallelize with PR 2 once PR 1 is in). PR 4 requires PRs 1–3 merged.

---

### PR 1 — Package scaffold + `Bill` + `BillLine` entities + `BillStatus` state machine

**Estimated effort:** ~2–3h
**Scope:** new package `blocks-financial-ap`; core record types; status enum; status-transition helper; repository interface; NO posting logic (PR 2)
**Commit subject:** `feat(blocks-financial-ap): scaffold AP package with Bill + BillLine + BillStatus per Stage 02 §3.7–§3.8`
**Branch:** `cob/blocks-financial-ap-scaffold`

#### Package skeleton

```
packages/blocks-financial-ap/
├── README.md
├── NOTICE.md                                       (Apache OFBiz attribution — see §License)
├── Sunfish.Blocks.FinancialAp.csproj
├── Models/
│   ├── BillId.cs
│   ├── BillLineId.cs
│   ├── Bill.cs
│   ├── BillLine.cs
│   └── BillStatus.cs
├── Services/
│   └── IBillRepository.cs
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs              (stub — filled in PR 4)
└── tests/
    ├── Sunfish.Blocks.FinancialAp.Tests.csproj
    ├── BillRecordTests.cs
    ├── BillLineRecordTests.cs
    └── BillStatusTransitionTests.cs
```

#### New types

**`Models/BillId.cs`** — ULID strongly-typed id, mirrors `Sunfish.Blocks.FinancialAr.Models.InvoiceId`:

```csharp
public readonly record struct BillId
{
    public Ulid Value { get; }
    public BillId(Ulid value) => Value = value;
    public static BillId New() => new(Ulid.NewUlid());
    public override string ToString() => Value.ToString();
}
```

**`Models/BillLineId.cs`** — same pattern for `BillLineId`.

**`Models/BillStatus.cs`** per Stage 02 §3.7:

```csharp
public enum BillStatus
{
    Draft,
    Received,    // entered + posted to GL (Expense/Asset Dr, AP Cr)
    Approved,    // approved for payment (optional gate)
    PartiallyPaid,
    Paid,
    Voided,
    Disputed,    // hold; do not pay; ledger trail retained
}
```

`Overdue` is **NOT** an enum value — it is a derived read-time computation, identical discipline to AR's `Overdue`:

```
Overdue = (status ∈ {Received, Approved, PartiallyPaid}) AND (today > dueDate) AND (balance > 0)
```

A `static class BillStatusExtensions` provides:

```csharp
public static bool IsOpen(this BillStatus s)
    => s is BillStatus.Received or BillStatus.Approved or BillStatus.PartiallyPaid;

public static bool IsTerminal(this BillStatus s)
    => s is BillStatus.Paid or BillStatus.Voided;

public static bool IsPayable(this BillStatus s)
    => s is BillStatus.Received or BillStatus.Approved or BillStatus.PartiallyPaid;

public static bool IsOverdue(this Bill bill, DateOnly asOf)
    => bill.Status.IsOpen() && asOf > bill.DueDate && bill.Balance > 0m;

// Approval gate: if chart policy requires approval, PartiallyPaid/Paid transitions from
// Received are blocked until status reaches Approved first.
public static bool CanApplyPayment(this BillStatus s, bool approvalRequired)
    => approvalRequired
        ? s == BillStatus.Approved || s == BillStatus.PartiallyPaid
        : s.IsPayable();
```

**Status-transition validation helper** — `BillStatusTransitions.CanTransitionTo(BillStatus from, BillStatus to) -> bool`, per §8.2:

| From | Allowed To |
|---|---|
| Draft | Received, (deleted) |
| Received | Approved, PartiallyPaid (if no-gate), Voided, Disputed |
| Approved | PartiallyPaid, Paid, Voided, Disputed |
| PartiallyPaid | Paid, Voided |
| Disputed | Received, Approved (clears back to prior state) |
| Paid | (terminal; no forward transitions) |
| Voided | (terminal; no forward transitions) |

#### `Bill` record

```csharp
public sealed record Bill
{
    public BillId Id { get; init; }
    public ChartOfAccountsId ChartId { get; init; }
    public string BillNumber { get; init; } = string.Empty;   // vendor's invoice number
    public PartyId VendorId { get; init; }
    public string? PropertyId { get; init; }                   // optional cost-center
    public DateOnly BillDate { get; init; }                    // date on the vendor's invoice
    public DateOnly DueDate { get; init; }
    public DateOnly ReceivedDate { get; init; }                // when we received it (may differ from BillDate)
    public string Currency { get; init; } = "USD";             // ISO 4217
    public IReadOnlyList<BillLine> Lines { get; init; } = Array.Empty<BillLine>();

    // Computed monetary fields (cached; recomputed on line mutation while Draft):
    public decimal Subtotal { get; init; }                     // sum of Lines[].Amount before tax
    public decimal TaxTotal { get; init; }                     // sum of Lines[].TaxAmount
    public decimal Total { get; init; }                        // Subtotal + TaxTotal
    public decimal AmountPaid { get; init; }                   // running total from applied payments
    public decimal Balance { get; init; }                      // Total - AmountPaid

    public BillStatus Status { get; init; } = BillStatus.Draft;
    public GLAccountId ApAccountId { get; init; }              // AP account; Liability/AccountsPayable-subtype

    public string? Notes { get; init; }
    public string? TermsId { get; init; }                      // FK to PaymentTerms; opaque in v1
    public JournalEntryId? JournalEntryId { get; init; }       // null until Received
    public JournalEntryId? VoidedByEntryId { get; init; }      // non-null when Voided

    // Approval gate (per §3.7 §Validation — controlled by chart policy):
    public string? ApprovedByUserId { get; init; }
    public Instant? ApprovedAtUtc { get; init; }

    public string? ExternalRef { get; init; }                  // ERPNext source ref; idempotency key

    // CRDT envelope fields (per crdt-friendly-schema-conventions.md §3):
    public long Version { get; init; }
    public IReadOnlyDictionary<string, long>? RevisionVector { get; init; }

    // Tombstone (per §2):
    public Instant? DeletedAtUtc { get; init; }
    public string? DeletedReason { get; init; }

    // Audit:
    public Instant CreatedAtUtc { get; init; }
    public Instant UpdatedAtUtc { get; init; }
}
```

**Notes on monetary representation:** identical discipline to AR — `decimal` with banker's rounding (`MidpointRounding.ToEven`) at every minor-unit boundary; no floats.

#### `BillLine` record

```csharp
public sealed record BillLine
{
    public BillLineId Id { get; init; }
    public BillId BillId { get; init; }
    public int LineNumber { get; init; }                       // 1..n
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Amount { get; init; }                       // round(quantity * unitPrice, 2, ToEven)
    public GLAccountId DebitAccountId { get; init; }           // Expense or Asset; subtype-validated
    public string? TaxCodeId { get; init; }                    // FK to TaxCode (opaque in v1 stub)
    public decimal TaxAmount { get; init; }                    // computed via ITaxCalculationService at record-time
    public string? PropertyId { get; init; }                   // optional cost-center
    public string? ClassificationId { get; init; }
    public string? Notes { get; init; }
}
```

**`DebitAccountId` subtype validation:** at `record()` time, each `BillLine.DebitAccountId` must resolve to an account with `Type ∈ {Expense, Asset}`. Any other account type (Income, Liability, Equity) is rejected with a typed error (`InvalidDebitAccount`). This is the key AP-vs-AR distinction: AR debits AR (Asset), AP's lines debit Expense or Asset.

**`amount` rounding:** `round(quantity * unitPrice.amount, 2, MidpointRounding.ToEven)` — banker's rounding, same as `InvoiceLine.amount`.

#### `IBillRepository` (write boundary)

```csharp
public interface IBillRepository
{
    Task<Bill?> GetByIdAsync(BillId id, CancellationToken ct = default);
    Task<Bill?> GetByExternalRefAsync(string source, string externalRefId, CancellationToken ct = default);
    Task<Bill?> GetByVendorBillNumberAsync(
        ChartOfAccountsId chartId, PartyId vendorId, string billNumber,
        CancellationToken ct = default);
    Task<IReadOnlyList<Bill>> QueryOpenAsync(
        ChartOfAccountsId chartId,
        PartyId? vendorId = null,
        string? propertyId = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<Bill>> QueryByStatusAsync(
        ChartOfAccountsId chartId,
        IReadOnlyList<BillStatus> statuses,
        CancellationToken ct = default);
    Task UpsertAsync(Bill bill, CancellationToken ct = default);
    Task<bool> ExistsByExternalRefAsync(string source, string externalRefId, CancellationToken ct = default);
}
```

**`InMemoryBillRepository`** — mirrors `InMemoryInvoiceRepository`; keyed by `BillId`; secondary indices on `(chartId + vendorId + billNumber)` and `(source + externalRefId)`. Enforces posted-then-immutable at `UpsertAsync` (rejects field mutations on `status != Draft`).

#### Tests (PR 1)

`tests/BillRecordTests.cs`:

- `Bill_DefaultStatus_IsDraft`.
- `Bill_Balance_EqualsTotalMinusAmountPaid`.
- `Bill_Lines_DefaultToEmpty`.

`tests/BillLineRecordTests.cs`:

- `BillLine_Amount_RoundedWithBankersRounding` (quantity 1.005, unitPrice 100 → amount 100.50 not 100.51).
- `BillLine_Amount_MultipleLines_SummedToSubtotal`.
- `BillLine_UnitPriceZero_AmountIsZero`.

`tests/BillStatusTransitionTests.cs`:

- `Transition_Draft_ToReceived_IsAllowed`.
- `Transition_Draft_ToApproved_IsNotAllowed`.
- `Transition_Received_ToApproved_IsAllowed`.
- `Transition_Received_ToPartiallyPaid_WithNoGate_IsAllowed`.
- `Transition_Received_ToPartiallyPaid_WithApprovalRequired_IsNotAllowed` (use `CanApplyPayment`).
- `Transition_Approved_ToPartiallyPaid_IsAllowed`.
- `Transition_Approved_ToDisputedAndBack_IsAllowed`.
- `Transition_Voided_ForwardTransition_IsNotAllowed`.
- `Transition_Paid_ForwardTransition_IsNotAllowed`.
- `IsOverdue_OpenAndPastDue_ReturnsTrue`.
- `IsOverdue_OpenAndNotPastDue_ReturnsFalse`.
- `IsOverdue_Paid_ReturnsFalse`.
- `IsOverdue_Voided_ReturnsFalse`.
- `IsOverdue_Disputed_ReturnsFalse` (Disputed is on hold; treat as not-overdue for aging purposes — see PR 3).

`tests/InMemoryBillRepositoryTests.cs`:

- `UpsertAndGet_RoundTrip_SingleBill`.
- `GetByVendorBillNumber_ReturnsCorrectBill`.
- `GetByVendorBillNumber_DifferentVendorSameBillNumber_ReturnsDifferentBills`.
- `QueryOpen_ReturnsOnlyReceivedApprovedPartiallyPaid`.
- `Upsert_OnPostedBill_RejectsFieldMutation` (status != Draft; field mutation attempt → throws `InvalidOperationException`).

Total new tests this PR: ~20.

#### Verification

- `dotnet build` succeeds on the new package.
- All new tests pass.
- `BillStatus` string values over the wire match schema design §3.7 exactly: `"Draft"`, `"Received"`, `"Approved"`, `"PartiallyPaid"`, `"Paid"`, `"Voided"`, `"Disputed"`.

#### Do NOT in this PR

- Do NOT add posting logic — that's PR 2.
- Do NOT define the `ITaxCalculationService` locally — it ships with `blocks-financial-tax`. If the tax package is on main, import it; if not, defer to PR 2 which defines the stub interface scoped to that PR.
- Do NOT define the AP aging service — that's PR 3.

---

### PR 2 — `IBillPostingService` — `record()` + `void()` + `dispute()` + approval gate

**Estimated effort:** ~3–4h
**Scope:** posting algorithm for `Draft → Received` (GL: Dr Expense/Asset, Cr AP); idempotency; `void()` reversal; `dispute()` + `resolve()` status transitions; `approve()` gate; approval-threshold policy; CRDT status-resolver registration
**Commit subject:** `feat(blocks-financial-ap): IBillPostingService — record + void + dispute + approval gate per Stage 02 §8.2`
**Depends on:** PR 1 merged
**Branch:** `cob/blocks-financial-ap-posting`

#### New service interface

**`Services/IBillPostingService.cs`**:

```csharp
public interface IBillPostingService
{
    Task<RecordResult> RecordAsync(BillId billId, CancellationToken ct = default);
    Task<ApproveResult> ApproveAsync(BillId billId, string approvedByUserId, CancellationToken ct = default);
    Task<DisputeResult> DisputeAsync(BillId billId, string reason, CancellationToken ct = default);
    Task<ResolveDisputeResult> ResolveDisputeAsync(BillId billId, CancellationToken ct = default);
    Task<VoidResult> VoidAsync(BillId billId, string reason, CancellationToken ct = default);
}

// Result types:
public sealed record RecordResult(Bill? Bill, JournalEntryId? EntryId, RecordError Error, string? Detail);
public enum RecordError
{
    None,
    UnknownBill,
    NotADraft,
    NoLines,
    InvalidApAccount,
    InvalidDebitAccount,
    TaxCalculationFailed,
    JEPostFailed,
}

public sealed record ApproveResult(Bill? Bill, ApproveError Error, string? Detail);
public enum ApproveError { None, UnknownBill, NotReceived, AlreadyApproved }

public sealed record DisputeResult(Bill? Bill, DisputeError Error, string? Detail);
public enum DisputeError { None, UnknownBill, NotPayable, AlreadyDisputed }

public sealed record ResolveDisputeResult(Bill? Bill, ResolveDisputeError Error, string? Detail);
public enum ResolveDisputeError { None, UnknownBill, NotDisputed }

public sealed record VoidResult(Bill? Bill, JournalEntryId? ReversalEntryId, VoidError Error, string? Detail);
public enum VoidError { None, UnknownBill, NotVoidable, AlreadyVoided, JEPostFailed }
```

#### `RecordAsync` algorithm per Stage 02 §8.2

```text
record(billId):
  // Phase 1 — preconditions
  bill = repo.GetByIdAsync(billId)
  if bill == null: return Err(UnknownBill)
  if bill.Status != Draft: return Err(NotADraft)   // idempotent: if Received, return Ok with existing JE
  if bill.Lines.Count < 1: return Err(NoLines)

  // Phase 2 — account-type validation
  apAccount = accountResolver.GetAsync(bill.ApAccountId)
  if apAccount == null || apAccount.Type != Liability || apAccount.Subtype != AccountsPayable:
    return Err(InvalidApAccount)
  for line in bill.Lines:
    debitAccount = accountResolver.GetAsync(line.DebitAccountId)
    if debitAccount == null || (debitAccount.Type != Expense && debitAccount.Type != Asset):
      return Err(InvalidDebitAccount, line.Id)

  // Phase 3 — tax calculation (per line)
  totalTax = 0
  for line in bill.Lines:
    txnContext = { address: resolveVendorAddress(bill.VendorId), date: bill.BillDate, propertyId: bill.PropertyId }
    taxResult = await taxCalculator.ComputeLineTaxAsync(line, bill.BillDate, bill.PropertyId)
    line.TaxAmount = taxResult.TaxAmount
    totalTax += taxResult.TaxAmount

  // Phase 4 — JE construction (Dr Expense/Asset, Cr AP)
  apCredit = bill.Total
  debitLines = group(line by line.DebitAccountId, sum amount + taxAmount)
    // Note: tax amounts debit the expense/asset account (not a separate tax-receivable line)
    // because vendor-side tax is embedded in the bill cost, not separately recoverable in US context.
    // Exception: if bill.TaxCodeId maps to a recoverable input-tax jurisdiction, a separate
    // Dr input-tax-receivable, Cr AP adjustment would apply. V1 ships without this;
    // the NoOp tax service returns zero tax so this edge case does not arise.

  je = new JournalEntry(
    Id: JournalEntryId.New(),
    EntryDate: bill.BillDate,
    Memo: $"Bill {bill.BillNumber} from {bill.VendorId}",
    Lines: [
      JournalEntryLine(apAccount, Debit: 0, Credit: apCredit, PropertyId: bill.PropertyId),
      ... for each debitLines: JournalEntryLine(
            debitAccount, Debit: amount, Credit: 0,
            PropertyId: bill.PropertyId, ClassificationId: line.ClassificationId)
    ],
    ChartId: bill.ChartId,
    SourceKind: JournalEntrySource.Bill,   // new SourceKind enum member; add to ledger if absent
    SourceReference: $"bill:{bill.Id}",
    ExternalRef: bill.ExternalRef,
    Status: JournalEntryStatus.Draft,
    CreatedAtUtc: time.GetUtcNow())

  // Phase 5 — post JE
  postResult = await jePosting.PostAsync(je)
  if postResult.Error != PostError.None:
    return Err(JEPostFailed, postResult.Detail)

  // Phase 6 — update bill
  recorded = bill with {
    Status = BillStatus.Received,
    Subtotal = sum(line.Amount),
    TaxTotal = totalTax,
    Total = Subtotal + TaxTotal,
    Balance = Total - bill.AmountPaid,
    JournalEntryId = postResult.Entry.Id,
    UpdatedAtUtc = time.GetUtcNow(),
    Version = bill.Version + 1,
  }
  await repo.UpsertAsync(recorded)

  // Phase 7 — emit events
  await events.PublishAsync(new BillRecordedEvent(
    BillId: recorded.Id, VendorId: recorded.VendorId,
    TotalAmount: recorded.Total, DueDate: recorded.DueDate, PropertyId: recorded.PropertyId))

  return Ok(recorded, postResult.Entry.Id)
```

**Idempotency:** if `bill.Status == Received` AND `bill.JournalEntryId != null`, return `Ok` with the existing JE id. Mirrors AR's invoice issuance idempotency discipline.

**JE balance check:** the posting service must produce a balanced JE. The CR AP credit equals exactly the sum of all DR debit lines. Validate this before calling `IJournalPostingService.PostAsync` (the ledger will also enforce it, but early detection improves error messages).

**`SourceKind.Bill` enum member:** if `JournalEntrySource` in `blocks-financial-ledger` does not include `Bill`, add it in this PR as an additive change to the ledger package (source-kind additions are non-breaking per the ledger hand-off's extensibility discipline). Check first:
```bash
grep -n "Bill" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/Models/JournalEntrySource.cs 2>/dev/null
```
If absent, add `Bill` to the enum and include the ledger change in this PR's commit. The ledger csproj is a project-reference from AP; the enum extension is safe.

#### `VoidAsync` algorithm per Stage 02 §8.2

```text
void(billId, reason):
  bill = repo.GetByIdAsync(billId)
  if bill == null: return Err(UnknownBill)
  if bill.Status == BillStatus.Voided: return Err(AlreadyVoided)
  if bill.Status is not (Draft or Received or Approved or Disputed):
    return Err(NotVoidable)  // PartiallyPaid or Paid bills cannot be voided without prior payment reversal

  if bill.JournalEntryId != null:
    // Bill was recorded; need reversal JE
    originalJE = jeRepo.GetByIdAsync(bill.JournalEntryId)
    reversalJE = JournalEntry.Reverse(originalJE, sourceKind: Reversal, reversalOf: originalJE.Id)
    postResult = await jePosting.PostAsync(reversalJE)
    if postResult.Error != PostError.None: return Err(JEPostFailed)
    voidedByEntryId = postResult.Entry.Id
  else:
    // Bill was still Draft when voided; no GL effect
    voidedByEntryId = null

  voided = bill with {
    Status = BillStatus.Voided,
    VoidedByEntryId = voidedByEntryId,
    UpdatedAtUtc = now, Version = bill.Version + 1,
    Notes = bill.Notes is null ? $"Voided: {reason}" : $"{bill.Notes}\nVoided: {reason}",
  }
  await repo.UpsertAsync(voided)
  await events.PublishAsync(new BillVoidedEvent(voided.Id, voidedByEntryId))
  return Ok(voided, voidedByEntryId)
```

#### `DisputeAsync` and `ResolveDisputeAsync` algorithms

```text
dispute(billId, reason):
  bill = repo.GetByIdAsync(billId)
  if bill == null: return Err(UnknownBill)
  if !bill.Status.IsPayable(): return Err(NotPayable)
  if bill.Status == BillStatus.Disputed: return Err(AlreadyDisputed)

  disputed = bill with {
    Status = BillStatus.Disputed,
    Notes = bill.Notes is null ? $"Disputed: {reason}" : $"{bill.Notes}\nDisputed: {reason}",
    UpdatedAtUtc = now, Version = bill.Version + 1,
  }
  await repo.UpsertAsync(disputed)
  await events.PublishAsync(new BillDisputedEvent(disputed.Id, reason))
  return Ok(disputed)

resolveDispute(billId):
  // Clears Disputed status back to Received (or Approved if the prior approved gate was set).
  // The prior status is inferred: if ApprovedAtUtc != null → restore Approved; else → Received.
  bill = repo.GetByIdAsync(billId)
  if bill == null: return Err(UnknownBill)
  if bill.Status != BillStatus.Disputed: return Err(NotDisputed)

  priorStatus = bill.ApprovedAtUtc.HasValue ? BillStatus.Approved : BillStatus.Received
  resolved = bill with {
    Status = priorStatus,
    UpdatedAtUtc = now, Version = bill.Version + 1,
  }
  await repo.UpsertAsync(resolved)
  await events.PublishAsync(new BillDisputeResolvedEvent(resolved.Id, priorStatus))
  return Ok(resolved)
```

#### Tax-calculation stub (if `blocks-financial-tax` not yet on main)

If `blocks-financial-tax` is absent, define a local `IApTaxCalculationService` stub scoped to this package, mirroring AR's `NoOpTaxCalculationService`:

```csharp
// Services/IApTaxCalculationService.cs — local stub; relocates to blocks-financial-tax when available
public interface IApTaxCalculationService
{
    Task<ApTaxCalculationResult> ComputeLineTaxAsync(
        BillLine line, DateOnly billDate, string? propertyId,
        CancellationToken ct = default);
}

public sealed record ApTaxCalculationResult(decimal TaxAmount, IReadOnlyList<ApTaxBreakdownEntry> Breakdown);
public sealed record ApTaxBreakdownEntry(string TaxCodeId, string JurisdictionId, GLAccountId TaxReceivableAccountId, decimal Amount);

/// Stub: returns zero tax. Real implementation lands with blocks-financial-tax.
public sealed class NoOpApTaxCalculationService : IApTaxCalculationService
{
    public Task<ApTaxCalculationResult> ComputeLineTaxAsync(
        BillLine line, DateOnly billDate, string? propertyId, CancellationToken ct)
        => Task.FromResult(new ApTaxCalculationResult(0m, Array.Empty<ApTaxBreakdownEntry>()));
}
```

If `blocks-financial-tax` IS on main, import its `ITaxCalculationService` and `TaxCalculationResult` directly; do not redefine. Update the `BillLine.TaxCodeId` field type from `string?` to `TaxCodeId?` if the tax package defines a strong type.

#### Event types

Ship local event record types in `Events/` (relocate when the foundation event-bus package lands):

```csharp
public sealed record BillCreatedEvent(BillId BillId, ChartOfAccountsId ChartId, PartyId VendorId);

public sealed record BillRecordedEvent(
    BillId BillId, PartyId VendorId,
    decimal TotalAmount, DateOnly DueDate, string? PropertyId);

public sealed record BillVoidedEvent(BillId BillId, JournalEntryId? ReversalEntryId);

public sealed record BillDisputedEvent(BillId BillId, string Reason);

public sealed record BillDisputeResolvedEvent(BillId BillId, BillStatus RestoredStatus);
```

`BillPaidEvent` is NOT emitted by this hand-off — it's emitted by `blocks-financial-payments` when a payment application brings the balance to zero.

**`IApEventPublisher.cs`** — local stub matching AR's `IInvoiceEventPublisher` pattern:

```csharp
public interface IApEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class;
}

public sealed class InMemoryApEventPublisher : IApEventPublisher
{
    private readonly List<object> _events = new();
    private readonly object _lock = new();
    public IReadOnlyList<object> PublishedEvents { get { lock (_lock) return _events.ToList(); } }
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct) where TEvent : class
    {
        lock (_lock) _events.Add(@event);
        return Task.CompletedTask;
    }
}
```

#### CRDT `BillStatusResolver` registration

Per `crdt-friendly-schema-conventions.md` §7 + the AR hand-off's `InvoiceStatusResolver` pattern:

- `BillStatusResolver` implements Pattern B (terminal-wins): `Voided` > `Paid` > `PartiallyPaid` > `Approved` > `Received`. `Disputed` does NOT win over non-Disputed payable states.
- Registers with `kernel-crdt`'s conflict-resolver registry in the same manner as `InvoiceStatusResolver`.
- If `kernel-crdt` is not on main yet (consistent with how AR handled this), ship the resolver as a registered-but-dormant service with a TODO comment.

#### Tests (PR 2)

`tests/BillPostingServiceTests.cs`:

- `Record_RejectsUnknownBill` → `UnknownBill`.
- `Record_RejectsAlreadyReceived_IdempotentReturn` (status Received → returns Ok with existing JE).
- `Record_RejectsDraftWithNoLines` → `NoLines`.
- `Record_RejectsInvalidApAccount` (e.g. Income type) → `InvalidApAccount`.
- `Record_RejectsInvalidDebitAccount_IncomeType` → `InvalidDebitAccount`.
- `Record_RejectsInvalidDebitAccount_LiabilityType` → `InvalidDebitAccount`.
- `Record_AcceptsExpenseDebitAccount` — happy path.
- `Record_AcceptsAssetDebitAccount` (fixed-asset purchase) — happy path.
- `Record_HappyPath_PostsBalancedJE_AndTransitionsToReceived`.
- `Record_WithMultipleLines_GroupsByDebitAccount` (2 lines on same expense account → 1 debit JE line; 2 lines on different accounts → 2 debit JE lines).
- `Record_JEMemo_ContainsBillNumberAndVendorId`.
- `Record_ExternalRefPropagates_ToJE`.
- `Record_EmitsBillCreatedAndRecordedEvents`.
- `Record_RoundingBankerHalfEven_PreservedThroughJE`.
- `Record_Idempotent_OnDuplicateCall_DoesNotDoubleEmitEvents`.

`tests/BillVoidServiceTests.cs`:

- `Void_RejectsUnknownBill` → `UnknownBill`.
- `Void_RejectsAlreadyVoided` → `AlreadyVoided`.
- `Void_OnDraft_VoidsWithoutReversalJE` (Draft bills have no JE; void is a status-only change).
- `Void_OnReceived_PostsReversalJE`.
- `Void_PreservesOriginalJE` (reversal is a new JE; original is unchanged).
- `Void_EmitsBillVoidedEvent`.
- `Void_RejectsPartiallyPaid` → `NotVoidable`.

`tests/BillDisputeServiceTests.cs`:

- `Dispute_RejectsUnknownBill`.
- `Dispute_RejectsAlreadyDisputed`.
- `Dispute_RejectsDraftBill` (Draft is not payable) → `NotPayable`.
- `Dispute_OnReceived_TransitionsToDisputed`.
- `Dispute_OnApproved_TransitionsToDisputed`.
- `ResolveDispute_OnDisputed_RestoresToReceived_WhenNoApproval`.
- `ResolveDispute_OnDisputed_RestoresToApproved_WhenPriorlyApproved`.
- `ResolveDispute_RejectsNonDisputed`.

`tests/BillApprovalTests.cs`:

- `Approve_RejectsNonReceived`.
- `Approve_RejectsAlreadyApproved`.
- `Approve_OnReceived_TransitionsToApproved_WithUserAndTimestamp`.
- `ApplyPayment_WithApprovalRequired_BlocksOnReceivedStatus` (using `CanApplyPayment`).
- `ApplyPayment_WithApprovalRequired_AllowsOnApprovedStatus`.

Total new tests this PR: ~30.

#### Verification

- `dotnet build` succeeds.
- All PR 1 + PR 2 tests pass.
- Spot-check: a 2-line bill (Expense line $800 + Supplies Asset line $200) produces a JE with exactly 3 lines (AP Cr $1,000; Expense Dr $800; Asset Dr $200), balanced, `SourceKind == Bill`.
- If `SourceKind.Bill` was added to the ledger enum, verify the ledger's own tests still pass.

#### Do NOT in this PR

- Do NOT implement payment application. The `PartiallyPaid → Paid` transitions come from `blocks-financial-payments`. Direct `Bill.AmountPaid` mutation via `IBillRepository.UpsertAsync` is the test seam for the AP aging tests in PR 3.
- Do NOT mutate posted bill headers. Field mutations (description, vendorId, lines, totals) on a non-Draft bill are blocked at the repository level.

---

### PR 3 — `IApAgingService` — bucket algorithm + per-vendor + per-property breakdowns

**Estimated effort:** ~1.5–2h
**Scope:** AP aging algorithm per Stage 02 §6.2 (mirror of AR aging); bucket open bills by `asOf - dueDate`; per-vendor and per-property aggregations; vendor-balance query method
**Commit subject:** `feat(blocks-financial-ap): IApAgingService + vendor-balance query per Stage 02 §6.2`
**Depends on:** PR 1 merged (parallel with PR 2)
**Branch:** `cob/blocks-financial-ap-aging`

#### New service interface

**`Services/IApAgingService.cs`** — mirrors `IArAgingService` exactly, substituting `ApAgingGroupBy.Vendor` for `AgingGroupBy.Customer`:

```csharp
public interface IApAgingService
{
    Task<ApAgingReport> ComputeAgingAsync(
        ChartOfAccountsId chartId,
        DateOnly asOf,
        ApAgingGroupBy groupBy = ApAgingGroupBy.Vendor,
        CancellationToken ct = default);
}

public enum ApAgingGroupBy { Vendor, Property, None }

public sealed record ApAgingReport(
    DateOnly AsOf,
    ApAgingGroupBy GroupBy,
    IReadOnlyList<ApAgingRow> Rows,
    ApAgingTotals Totals);

public sealed record ApAgingRow(
    string GroupKey,
    decimal Current,
    decimal Days0to30,
    decimal Days31to60,
    decimal Days61to90,
    decimal Days90Plus,
    decimal TotalOpen);

public sealed record ApAgingTotals(
    decimal Current,
    decimal Days0to30,
    decimal Days31to60,
    decimal Days61to90,
    decimal Days90Plus,
    decimal TotalOpen);
```

#### Algorithm (mirror of `IArAgingService`)

```text
apAgingReport(chartId, asOf, groupBy):
  // Retrieve open bills: status in {Received, Approved, PartiallyPaid}
  // Note: Disputed bills are NOT included in the aging report — they are on hold
  // and excluded from cash-outflow projections per the dispute-hold semantics.
  openBills = await repo.QueryOpenAsync(chartId)
  openBills = openBills.Where(b => b.Balance > 0 && b.Status != BillStatus.Disputed)

  rowsByKey = group openBills by:
      key = groupBy switch {
          Vendor   => bill.VendorId.Value,
          Property => bill.PropertyId ?? "Unassigned",
          None     => "All",
      }

  for each group:
      for bill in group:
          daysOverdue = asOf.DayNumber - bill.DueDate.DayNumber
          bucket = daysOverdue switch {
              <= 0   => current,
              <= 30  => 0-30,
              <= 60  => 31-60,
              <= 90  => 61-90,
              _      => 90+,
          }
          accumulate bill.Balance into the bucket

  rows = sorted by GroupKey (ordinal)
  totals = aggregate rows
  return ApAgingReport(asOf, groupBy, rows, totals)
```

**Key AP difference from AR:** `Disputed` bills are excluded from the aging report (AR's `IArAgingService` includes all open invoices regardless of disputed state because AR has no `Disputed` status). This reflects the AP business rule: a disputed payable is not a current obligation until the dispute resolves.

#### Vendor-balance query method

Extend `IBillRepository` (declared PR 1; impl PR 3):

```csharp
// In IBillRepository (declared PR 1; impl PR 3):
Task<IReadOnlyList<Bill>> QueryVendorBalanceAsync(
    ChartOfAccountsId chartId,
    PartyId vendorId,
    DateOnly fromDate,
    DateOnly toDate,
    CancellationToken ct = default);
```

`InMemoryBillRepository.QueryVendorBalanceAsync` returns all bills for the given vendor where `bill.BillDate >= fromDate AND bill.BillDate <= toDate`, plus any open bills (`Received | Approved | PartiallyPaid` with `balance > 0`) that have a bill date before `fromDate` (for brought-forward balance). Mirrors AR's `QueryStatementAsync`.

#### Tests (PR 3)

`tests/ApAgingServiceTests.cs`:

- `Aging_EmptyChart_ProducesEmptyRowsAndZeroTotals`.
- `Aging_CurrentBill_BucketedAsCurrent` (dueDate > asOf).
- `Aging_OneDayLate_BucketedAs0to30`.
- `Aging_ThirtyDaysLate_BucketedAs0to30` (boundary).
- `Aging_ThirtyOneDaysLate_BucketedAs31to60`.
- `Aging_SixtyOneDaysLate_BucketedAs61to90`.
- `Aging_NinetyOneDaysLate_BucketedAs90Plus`.
- `Aging_GroupByVendor_AggregatesAcrossBills`.
- `Aging_GroupByProperty_AggregatesAcrossVendors`.
- `Aging_GroupByNone_AggregatesAllToSingleRow`.
- `Aging_NullPropertyId_BucketsAsUnassigned`.
- `Aging_PartiallyPaidBill_BalanceReflectedNotTotal`.
- `Aging_PaidBill_NotIncluded`.
- `Aging_VoidedBill_NotIncluded`.
- `Aging_DraftBill_NotIncluded`.
- `Aging_DisputedBill_ExcludedFromAging` (key AP difference from AR).
- `Aging_HistoricalAsOf_UsesParameterizedDate`.
- `Aging_TotalsRow_EqualsSumOfGroupedRows`.

`tests/BillVendorBalanceQueryTests.cs`:

- `QueryVendorBalance_ReturnsBillsInDateRange`.
- `QueryVendorBalance_IncludesOpenBillsBeforeFromDate`.
- `QueryVendorBalance_ExcludesOtherVendors`.

Total new tests this PR: ~21.

#### Verification

- `dotnet build` succeeds.
- All PR 1 + PR 3 tests pass (PR 2 may be concurrent but is not required).
- `ApAgingReport.Totals` fields are sums of the individual row fields — add an assertion to the aggregate test.

#### Do NOT in this PR

- Do NOT introduce a balance-cache table (per Stage 02 §9 guidance — identical to AR).
- Do NOT add the AP-specific cash-flow projection algorithm (schema design §6.7). That is a `blocks-reports-*` scope; AP only ships the open-bill query that the projection calls.

---

### PR 4 — ERPNext `IErpnextPurchaseInvoiceImporter` + DI extension `AddBlocksFinancialAp()` + docs

**Estimated effort:** ~2h
**Scope:** ERPNext Purchase Invoice → Bill importer per schema design §10.3 Pass 4; field-by-field mapping per §10.2; idempotency; `AddBlocksFinancialAp()` DI extension method; `apps/docs/blocks-financial-ap/overview.md`
**Commit subject:** `feat(blocks-financial-ap): ERPNext importer + DI extension + docs per Stage 02 §10.3`
**Depends on:** PRs 1–3 merged
**Branch:** `cob/blocks-financial-ap-importer`

#### ERPNext source DTO

**`Migration/ErpnextPurchaseInvoiceSource.cs`** — mirrors AR's `ErpnextSalesInvoiceSource` but for Purchase Invoice fields:

```csharp
/// <summary>
/// Deserialized shape of an ERPNext Purchase Invoice document.
/// Field names use ERPNext's snake_case as-published (data-format names, not borrowed code).
/// </summary>
public sealed record ErpnextPurchaseInvoiceSource
{
    public string Name { get; init; } = string.Empty;     // ERPNext docname (idempotency key)
    public string Supplier { get; init; } = string.Empty; // supplier name (maps to Party.Name)
    public string? BillNo { get; init; }                  // vendor's invoice number; falls back to Name
    public string Company { get; init; } = string.Empty;  // maps to ChartOfAccounts via legalEntity
    public string PostingDate { get; init; } = string.Empty; // ISO 8601 date string
    public string? DueDate { get; init; }
    public int DocStatus { get; init; }                   // 0=Draft, 1=Submitted, 2=Cancelled
    public decimal GrandTotal { get; init; }
    public decimal TotalTaxesAndCharges { get; init; }
    public string? Modified { get; init; }                // ERPNext last-modified ISO timestamp
    public string? CostCenter { get; init; }              // maps to propertyId if property-linked
    public IReadOnlyList<ErpnextPurchaseInvoiceItem> Items { get; init; } = Array.Empty<ErpnextPurchaseInvoiceItem>();
}

public sealed record ErpnextPurchaseInvoiceItem
{
    public string ItemName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Qty { get; init; }
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
    public string? ExpenseAccount { get; init; }           // maps to DebitAccountId (Expense)
    public string? CostCenter { get; init; }
    public string? ItemTaxTemplate { get; init; }
}
```

#### Importer interface and implementation

**`Migration/IErpnextPurchaseInvoiceImporter.cs`**:

```csharp
public interface IErpnextPurchaseInvoiceImporter
{
    Task<PurchaseImportResult> UpsertFromErpnextAsync(
        ErpnextPurchaseInvoiceSource source,
        ChartOfAccountsId targetChartId,
        CancellationToken ct = default);
}

public sealed record PurchaseImportResult(
    BillId? BillId, PurchaseImportAction Action, string? Detail);

public enum PurchaseImportAction { Inserted, Updated, Skipped, Failed }
```

#### Mapping logic

Per schema design §10.2 + §10.3:

| ERPNext `Purchase Invoice` field | → `Bill` field | Notes |
|---|---|---|
| `name` | `ExternalRef` | idempotency key; format `"erpnext:{name}"` |
| `bill_no` ?? `name` | `BillNumber` | vendor's invoice number; fall back to ERPNext docname |
| `supplier` | `VendorId` | lookup `Party.Name` via `IPartyReadModel`; throw if not found |
| `company` | `ChartId` | caller provides `targetChartId`; verify match |
| `posting_date` | `BillDate` | parse ISO date |
| `due_date` | `DueDate` | parse ISO date; fall back to `BillDate + 30d` if null |
| `posting_date` | `ReceivedDate` | v1: use posting_date as received_date |
| `doc_status == 0` | `Status = Draft` | do not post |
| `doc_status == 1` | `Status = Received` | call `RecordAsync()` to post JE |
| `doc_status == 2` | `Status = Voided` | insert as Draft then call `VoidAsync()` |
| `grand_total` | `Total` (cross-check) | verify computed Total matches within $0.01; log warning if not |
| `cost_center` | `PropertyId` | if cost-center maps to property in `IPropertyCostCenterMap`; otherwise null |
| items | `Lines` | map each item → `BillLine` (see below) |

Per-line mapping:

| ERPNext `Purchase Invoice Item` | → `BillLine` field | Notes |
|---|---|---|
| (auto) | `LineNumber` | 1..n, ordinal |
| `item_name` / `description` | `Description` | prefer description; fall back to item_name |
| `qty` | `Quantity` | |
| `rate` | `UnitPrice` | |
| `amount` | `Amount` (cross-check) | verify `round(qty * rate, 2, ToEven) ≈ amount`; log mismatch |
| `expense_account` | `DebitAccountId` | resolve account by name in `IAccountResolver`; must be Expense or Asset |
| `cost_center` | `PropertyId` | same mapping as header |
| `item_tax_template` | `TaxCodeId` | lookup tax code by ERPNext template name; null if not found |

**Vendor lookup fallback:** identical to AR's customer lookup. `IPartyReadModel.FindPartyByExactNameAsync(supplierName)` must return exactly one match; throw `ImportVendorNotFoundException` otherwise with the supplier name in the message.

**Idempotency:** before inserting, call `IBillRepository.ExistsByExternalRefAsync("erpnext", source.Name)`. If already exists, return `Skipped`. On upsert of a higher-`Modified` draft, rebuild and upsert.

#### `AddBlocksFinancialAp()` DI extension

**`DependencyInjection/ServiceCollectionExtensions.cs`**:

```csharp
public static IServiceCollection AddBlocksFinancialAp(
    this IServiceCollection services,
    Action<BlocksFinancialApOptions>? configure = null)
{
    var options = new BlocksFinancialApOptions();
    configure?.Invoke(options);
    services.AddSingleton(options);
    services.AddSingleton<IBillRepository, InMemoryBillRepository>();
    services.AddSingleton<IBillPostingService, BillPostingService>();
    services.AddSingleton<IApAgingService, ApAgingService>();
    services.AddSingleton<IApEventPublisher, InMemoryApEventPublisher>();
    // If blocks-financial-tax is on main, register its ITaxCalculationService here.
    // If not, register the local NoOpApTaxCalculationService stub:
    services.AddSingleton<IApTaxCalculationService, NoOpApTaxCalculationService>();
    services.AddSingleton<IErpnextPurchaseInvoiceImporter, ErpnextPurchaseInvoiceImporter>();
    return services;
}

public sealed class BlocksFinancialApOptions
{
    /// Optional: AP approval threshold. Bills below this amount bypass the Approved gate.
    /// Null = no approval required (all bills can be paid immediately upon Received).
    public decimal? ApprovalThreshold { get; set; } = null;
}
```

#### Tests (PR 4)

`tests/ErpnextPurchaseInvoiceImporterTests.cs`:

- `Upsert_NewSubmittedSource_InsertsAndRecordsBill` (DocStatus=1 → `Status = Received`; JE posted).
- `Upsert_NewDraftSource_InsertsDraftNoRecord` (DocStatus=0 → `Status = Draft`; no JE).
- `Upsert_NewCancelledSource_InsertsAndVoidsImmediately` (DocStatus=2 → Voided with no JE since no prior record).
- `Upsert_DuplicateSubmittedSource_ReturnsSkipped` (idempotency).
- `Upsert_UnknownVendorName_ThrowsHelpfulError`.
- `Upsert_GrandTotalMismatch_LogsWarningButProceeds`.
- `Upsert_MapsCostCenterToPropertyId`.
- `Upsert_LineCount_MatchesSourceItems`.
- `Upsert_BillNo_UsedWhenPresent` (`BillNumber == source.BillNo`).
- `Upsert_FallsBackToDocname_WhenBillNoNull` (`BillNumber == source.Name`).
- `Upsert_PreservesPostingDate_EvenIfBackdated`.

`tests/AddBlocksFinancialApTests.cs`:

- `AddBlocksFinancialAp_RegistersAllServices` (verify `IServiceProvider` resolves `IBillRepository`, `IBillPostingService`, `IApAgingService`, `IErpnextPurchaseInvoiceImporter`).
- `AddBlocksFinancialAp_WithApprovalThreshold_SetsOption`.

Total new tests this PR: ~13.

#### Verification

- `dotnet build` succeeds across the entire solution.
- All PR 1–3 tests pass.
- New tests pass.
- Integration smoke: feed a synthetic ERPNext Purchase Invoice export (1 vendor, 2 line items, DocStatus=1, GrandTotal $2,500) → bill imports, posts a balanced JE (Cr AP $2,500, Dr Expense1 $N + Dr Expense2 $M); AP aging on the result shows the bill in the correct bucket.
- `apps/docs/blocks-financial-ap/overview.md` rendered without broken links.

#### Docs

**`apps/docs/blocks-financial-ap/overview.md`** — cluster docs page (ships in this PR). Structure:

```markdown
# blocks-financial-ap

Vendor-facing accounts-payable package for the Sunfish Anchor native
financial domain (ADR 0088 §1; Stage 02 §3.7–§3.8).

## Overview

Provides:
- `Bill` — vendor-facing payable obligation; header.
- `BillLine` — composable line items; debit-account (Expense or Asset) + tax + cost-center linkage.
- `BillStatus` — state machine: Draft → Received → [Approved →] PartiallyPaid → Paid; Disputed hold; Voided.
- `IBillPostingService` — double-entry GL posting: Dr Expense/Asset, Cr AP on `record()`.
- `IApAgingService` — AP aging report by vendor and by property.
- `IErpnextPurchaseInvoiceImporter` — migration importer for ERPNext Purchase Invoices.

## Key differences from blocks-financial-ar

| Aspect | AR (Invoice) | AP (Bill) |
|---|---|---|
| Debits | AR asset account | Expense or Asset account per line |
| Credits | Income + Tax-payable | AP liability account |
| Numbering | Sunfish-generated (INV-YYYY-...) | Vendor's own number (bill_no) |
| Posting trigger | `issue()` Draft→Issued | `record()` Draft→Received |
| Dispute status | None | `Disputed` hold (excludes from AP aging) |
| Approval gate | None | Optional; per `BlocksFinancialApOptions.ApprovalThreshold` |
| CRDT terminal | Voided > Paid > PartiallyPaid > Issued | Voided > Paid > PartiallyPaid > Approved > Received |

## Registration

services.AddBlocksFinancialAp(opts => { opts.ApprovalThreshold = 500m; });

Requires `AddBlocksFinancialLedger()` (for `IJournalPostingService`).
Optionally requires `AddBlocksFinancialTax()` (for `ITaxCalculationService`); ships with NoOp stub until the tax package is available.

## ERPNext migration

Pass 4 of the ERPNext import process (`importPurchaseInvoice`) calls
`IErpnextPurchaseInvoiceImporter.UpsertFromErpnextAsync(source, chartId)`.
See `icm/02_architecture/blocks-financial-schema-design.md §10.3`.
```

#### Do NOT in this PR

- Do NOT implement the importer orchestrator (the 6-pass driver). That lives in `tooling-anchor-import` (separate hand-off). This PR ships the integration point.
- Do NOT couple to `blocks-financial-payments` — `PaymentApplication` is out of scope.
- Do NOT rename or remove `IBillRepository` or `IBillPostingService` methods. These are the surfaces that `blocks-financial-payments` will consume. Any change requires Halt §4 (see below).

---

## Entity shapes (C# summary)

Full shapes are specified in the per-PR sections above. Summary for COB's quick reference:

### `BillId` and `BillLineId`

```csharp
public readonly record struct BillId { public Ulid Value { get; } /* ... */ }
public readonly record struct BillLineId { public Ulid Value { get; } /* ... */ }
```

### `Bill` (key fields)

```csharp
public sealed record Bill
{
    public BillId Id { get; init; }
    public ChartOfAccountsId ChartId { get; init; }
    public string BillNumber { get; init; }                 // vendor's invoice number
    public PartyId VendorId { get; init; }
    public string? PropertyId { get; init; }
    public DateOnly BillDate { get; init; }
    public DateOnly DueDate { get; init; }
    public DateOnly ReceivedDate { get; init; }
    public string Currency { get; init; }
    public IReadOnlyList<BillLine> Lines { get; init; }
    public decimal Subtotal { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal Total { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal Balance { get; init; }
    public BillStatus Status { get; init; }
    public GLAccountId ApAccountId { get; init; }           // Liability/AccountsPayable-subtype
    public string? Notes { get; init; }
    public string? TermsId { get; init; }
    public JournalEntryId? JournalEntryId { get; init; }
    public JournalEntryId? VoidedByEntryId { get; init; }
    public string? ApprovedByUserId { get; init; }
    public Instant? ApprovedAtUtc { get; init; }
    public string? ExternalRef { get; init; }
    public long Version { get; init; }
    public Instant CreatedAtUtc { get; init; }
    public Instant UpdatedAtUtc { get; init; }
}
```

### `BillLine` (key fields)

```csharp
public sealed record BillLine
{
    public BillLineId Id { get; init; }
    public BillId BillId { get; init; }
    public int LineNumber { get; init; }
    public string Description { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Amount { get; init; }                    // banker's rounding
    public GLAccountId DebitAccountId { get; init; }        // Expense or Asset; subtype-validated
    public string? TaxCodeId { get; init; }
    public decimal TaxAmount { get; init; }
    public string? PropertyId { get; init; }
    public string? ClassificationId { get; init; }
    public string? Notes { get; init; }
}
```

---

## DI wiring (complete)

```csharp
// Host composition root:
services
    .AddBlocksFinancialLedger(/* ... */)      // prerequisite
    .AddBlocksFinancialTax(/* ... */)          // prerequisite (or NoOp stub auto-registered by AP)
    .AddBlocksFinancialAp(opts =>
    {
        opts.ApprovalThreshold = null;          // null = no approval gate required (default)
    });

// Resolved by IBillPostingService (via IBillRepository + IJournalPostingService + IApTaxCalculationService):
var billPostingSvc = host.Services.GetRequiredService<IBillPostingService>();

// Resolved by IApAgingService (via IBillRepository):
var apAgingSvc = host.Services.GetRequiredService<IApAgingService>();

// Resolved by IErpnextPurchaseInvoiceImporter (via IBillRepository + IBillPostingService + IPartyReadModel):
var importer = host.Services.GetRequiredService<IErpnextPurchaseInvoiceImporter>();
```

---

## ERPNext mapping table (Purchase Invoice → Bill)

Per schema design §8.2 + §10.2. Authoritative reference for COB when implementing the importer.

| ERPNext DocType | Sunfish entity |
|---|---|
| `Purchase Invoice` | `Bill` |
| `Purchase Invoice Item` | `BillLine` |

| ERPNext `Purchase Invoice` field | `Bill` field | Transformation |
|---|---|---|
| `name` | `ExternalRef` | `"erpnext:{name}"` |
| `bill_no` ?? `name` | `BillNumber` | prefer `bill_no`; fall back to `name` |
| `supplier` | `VendorId` | `IPartyReadModel.FindPartyByExactNameAsync(supplier)` |
| `company` | (inferred) | caller provides `targetChartId`; verify company resolves to it |
| `posting_date` | `BillDate` | parse ISO date string |
| `due_date` | `DueDate` | parse ISO date; default to `BillDate + 30d` if null |
| `posting_date` | `ReceivedDate` | v1 approximation; migration source lacks a "received" field |
| `grand_total` | `Total` (cross-check only) | computed from lines; log warning if mismatch > $0.01 |
| `total_taxes_and_charges` | `TaxTotal` (cross-check only) | |
| `cost_center` | `PropertyId` | via `IPropertyCostCenterMap.TryMapToPropertyId(costCenter)` |
| `doc_status == 0` | `Status = Draft` | no GL posting |
| `doc_status == 1` | `Status = Received` | call `IBillPostingService.RecordAsync()` |
| `doc_status == 2` | `Status = Voided` | insert Draft → call `IBillPostingService.VoidAsync()` |
| `modified` | version-order tie-breaker | used to decide whether to skip or update an existing draft |

| ERPNext `Purchase Invoice Item` field | `BillLine` field | Transformation |
|---|---|---|
| (ordinal 1..n) | `LineNumber` | |
| `description` ?? `item_name` | `Description` | |
| `qty` | `Quantity` | |
| `rate` | `UnitPrice` | |
| `amount` | `Amount` (cross-check) | verify `round(qty * rate, 2, ToEven) ≈ amount`; log mismatch |
| `expense_account` | `DebitAccountId` | `IAccountResolver.ResolveByNameAsync(expenseAccount)` |
| `cost_center` | `PropertyId` | via `IPropertyCostCenterMap` |
| `item_tax_template` | `TaxCodeId` | `ITaxCodeResolver.FindByErpnextTemplateNameAsync(template)`; null if not found |

---

## License posture

### Borrowed-with-attribution (permissive)

- **Apache OFBiz** `accounting/Invoice + InvoiceItem` entities (Apache 2.0). The `Bill` + `BillLine` field shapes derive from OFBiz's `purchase-invoice` doctype pattern (header/line decomposition; `apAccountId` + `debitAccountId` linkage; computed-cached totals; AP-aging-as-derived-state) — the mirror-image of the AR attribution in the sibling `blocks-financial-ar` package.

**Attribution requirements:**

1. The package's `Sunfish.Blocks.FinancialAp.csproj` carries `<PropertyGroup><NOTICEFile>NOTICE.md</NOTICEFile></PropertyGroup>`.
2. **`packages/blocks-financial-ap/NOTICE.md`** (new file in PR 1):

```markdown
# NOTICE — Sunfish.Blocks.FinancialAp

This package's entity shapes (Bill + BillLine; AP-aging-as-derived-state;
header/line decomposition; computed-cached totals) derive from Apache OFBiz's
`accounting/Invoice + InvoiceItem` entity models — specifically the purchase-invoice
(Accounts Payable) variant (<https://ofbiz.apache.org/>, Apache 2.0 license).

OFBiz version studied: v18.12.x (as of 2026-05-16).

The Sunfish implementation is original code, distributed under the
MIT License. The OFBiz entity-shape pattern is reproduced with
attribution per Apache 2.0 §4(c) of the OFBiz License.
```

3. Source-header comments on `Bill.cs`, `BillLine.cs`, `IApAgingService.cs` reference OFBiz in a one-line comment.

### Clean-room only (copyleft)

Same discipline as AR. Per schema design §11.2–§11.5, these were studied for understanding only:

- **ERPNext + Frappe** (GPLv3) — Purchase Invoice DocType field names are consumed as a **data format** (the importer's `ErpnextPurchaseInvoiceSource` DTO); no code borrowed.
- **GnuCash** (GPLv2) — AP aging bucket conventions; cite IRS guidance as primary reference.
- **Beancount** (GPLv2) — double-entry posting pattern; the `record()` algorithm is from the textbook AR posting equation (AICPA *Accounting Principles*), not Beancount's representation.

**Discipline check before merging any PR:**
1. No copyleft code was opened in any editor session that produced this hand-off's PRs.
2. No identifier names from GPL/AGPL sources appear in the new code.
3. The clean-room schema in `blocks-financial-schema-design.md` §3.7–§3.8 is the source of truth.

---

## Test plan

### Per-PR minima (summary)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (scaffold + records + state machine) | ~20 | record fields; line rounding; status transitions; repository round-trip; immutability enforcement |
| PR 2 (posting service) | ~30 | record happy + every failure path; void (Draft + Received paths); dispute + resolve; approval gate; idempotency; event emission |
| PR 3 (AP aging + vendor-balance query) | ~21 | bucket boundaries; group-by-vendor/property/none; Disputed exclusion; historical asOf; paid/voided exclusion; vendor-balance query |
| PR 4 (ERPNext importer + DI extension) | ~13 | upsert idempotency; DocStatus routing; vendor lookup; bill_no fallback; cross-check warning; DI registration |
| **Total** | **~84 new** | |

### Cluster-level acceptance (PASS gate at end of PR 4)

**A1.** `dotnet build` succeeds across `Sunfish.Blocks.FinancialAp` + every downstream consumer.

**A2.** `dotnet test packages/blocks-financial-ap/tests/` passes all ~84 new tests.

**A3.** A bill recording round-trip:
- Seed a chart via `IChartSeedingService.SeedChartAsync(...)`.
- Construct a Draft Bill with 2 lines (office supplies $300 to Supplies Expense account; equipment $700 to Equipment Asset account; both credit the chart's default AP account).
- Call `IBillPostingService.RecordAsync(...)`.
- Assert: `RecordResult.Error == None`; `Bill.Status == Received`; `Bill.BillNumber` populated (from `BillNumber` field, not generated); a `JournalEntry` exists with `Status == Posted`, 3 lines (AP Cr $1,000; Supplies Dr $300; Equipment Dr $700), balanced.

**A4.** A void round-trip (Received bill):
- Record a bill (per A3).
- Call `IBillPostingService.VoidAsync(...)`.
- Assert: `Bill.Status == Voided`; `VoidedByEntryId` populated; a reversal JE exists with flipped debits/credits; original JE unchanged.

**A5.** A void round-trip (Draft bill):
- Create a Draft bill (do NOT record).
- Call `IBillPostingService.VoidAsync(...)`.
- Assert: `Bill.Status == Voided`; `VoidedByEntryId == null` (no JE for a Draft bill); no GL entries created.

**A6.** AP aging report sanity:
- Seed 3 vendors × 3 bills each (9 bills total) at staggered due dates.
- Mark 2 bills as `Disputed` (should be excluded from aging).
- Mark 3 bills as `PartiallyPaid` (balance = total/2) via direct repository upsert.
- Call `IApAgingService.ComputeAgingAsync(asOf: today, groupBy: Vendor)`.
- Assert: 3 vendor rows; Disputed bills absent from all buckets; totals sum to expected open balance.

**A7.** ERPNext importer round-trip:
- Construct an `ErpnextPurchaseInvoiceSource` (1 vendor, 2 line items, DocStatus=1, GrandTotal $1,800, BillNo="VND-2026-042").
- Pre-seed the vendor in `InMemoryPartyReadModel`.
- Call `IErpnextPurchaseInvoiceImporter.UpsertFromErpnextAsync(...)`.
- Assert: `ImportAction == Inserted`; `Bill.Status == Received`; `Bill.BillNumber == "VND-2026-042"`; `Bill.Total == 1800`; ExternalRef preserved.
- Call the importer again with the SAME source.
- Assert: `ImportAction == Skipped` (idempotency).

**A8.** DI registration smoke:
- Build a minimal `IServiceCollection` → `IServiceProvider` using `AddBlocksFinancialLedger()` + `AddBlocksFinancialAp()`.
- Assert: `IBillRepository`, `IBillPostingService`, `IApAgingService`, `IErpnextPurchaseInvoiceImporter` all resolve without error.

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*` beacon to `coordination/inbox/`:

### H1. `blocks-financial-ar` or `blocks-financial-tax` gate not cleared

Pre-build checklist steps 1–2 catch this. If either gate is unmet, **STOP** — file `cob-question-2026-05-XXTHH-MMZ-w60-p4-ap-gate-unmet.md` naming which predecessor is missing. Do not proceed with stubs-only beyond the tax stub (the AR patterns are required, not just additive).

### H2. `GLAccountId` type disambiguation (PR 1)

`Bill.ApAccountId` uses `GLAccountId` from `blocks-financial-ledger` (the canonical type for all account references in this cluster). If `GLAccountId` is aliased differently in the ledger hand-off (e.g., renamed to `AccountId` in a ledger refactor), update the AP package's field type to match. This is a compile-time catch; no logic change needed.

**No halt needed unless the type is missing entirely.** If `blocks-financial-ledger` doesn't export any account-ID strong type, file `cob-question-*`.

### H3. `ITaxCalculationService` stub shape conflict (PR 2)

If `blocks-financial-tax` IS on main and its `ITaxCalculationService` interface differs from the stub shape used in AR (`ComputeLineTaxAsync(InvoiceLine line, DateOnly, string?)`) — specifically if the parameter type is `InvoiceLine` rather than a protocol type — the AP stub cannot reuse it directly (AP passes `BillLine`, not `InvoiceLine`).

**Mitigation (no halt):** define a local `IApTaxCalculationService` with `BillLine` as the parameter (as shown in PR 2). When `blocks-financial-tax` stabilizes a generic interface (e.g., `IGenericLineTaxCalculationService<TLine>`), a follow-on hand-off wires the real implementation.

**Halt condition:** if the tax package exports a protocol that explicitly forbids extension to `BillLine` (e.g., sealed types with no generic surface), file `cob-question-*`. This is unlikely but document if discovered.

### H4. `IBillService` / `IBillRepository` contract would break `blocks-financial-payments`

Per the §Pre-merge council exception in the header: if any method on `IBillRepository` or `IBillPostingService` that `blocks-financial-payments` would logically call (e.g., `QueryOpenAsync`, `UpsertAsync`, `RecordAsync`) cannot be implemented without changing its signature, **halt and file `cob-question-*`** naming the specific signature conflict.

The `blocks-financial-payments` hand-off (not yet authored) will rely on:
- `IBillRepository.QueryOpenAsync(chartId, vendorId?, propertyId?)` — to find bills eligible for payment application.
- `IBillRepository.UpsertAsync(Bill)` — to update `AmountPaid` + `Balance` + `Status` when a payment is applied.
- `IBillPostingService.VoidAsync(billId, reason)` — for payment-bounce reversal scenarios.

Do not rename or remove these. Additive changes (new overloads, new query methods) are fine.

### H5. `JournalEntrySource.Bill` enum member conflict (PR 2)

If `blocks-financial-ledger`'s `JournalEntrySource` enum already has a `Bill` member (unlikely but possible if a ledger refactor added it), confirm the existing member's semantics match before using it. If it means something different, file `cob-question-*`.

### H6. Loro append-only constraint surfaces (any PR)

Per the AR hand-off §Halt 10. **Skip Loro integration entirely in this hand-off** (v1 in-memory substrate). File `cob-question-*` only if compilation fails due to a Loro op-mapping question.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–4 merged to main** (PRs 1 + 2 sequential; PR 3 may parallelize with PR 2 once PR 1 is in; PR 4 requires all three prior PRs).
2. **Bill recording round-trip:** acceptance tests A3 + A4 + A5 pass.
3. **AP aging works:** acceptance test A6 passes.
4. **ERPNext importer round-trip:** acceptance test A7 passes (insert + idempotent re-insert = Skipped).
5. **DI registration:** acceptance test A8 passes.
6. **Tests pass:** ~84 new tests across the package.
7. **`apps/docs/blocks-financial-ap/overview.md` published** (ships in PR 4).
8. **`active-workstreams.md`** row for W#60 P4 / blocks-financial-ap updated with `built` status + the 4 PR numbers.
9. **`coordination/inbox/cob-status-2026-05-XXTHH-MMZ-w60-p4-financial-ap-built.md`** beacon dropped.

When the PASS gate is met, the next Phase 1 critical-path hand-off can proceed:

- `blocks-financial-payments-stage06-handoff.md` (Payment + PaymentApplication; the `PaymentApplication.appliedTo = "Bill"` path now has `BillId` + `IBillRepository` available).
