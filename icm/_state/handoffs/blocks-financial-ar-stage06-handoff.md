# Hand-off — `blocks-financial-ar` Invoice + InvoiceLine + InvoiceStatus + AR aging (Phase 1 critical path)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-16
**Status:** `ready-to-build`
**Workstream:** W#60 P4 — Path II native domain, AR cluster (Phase 1 critical path)
**Spec source:** [`icm/02_architecture/blocks-financial-schema-design.md`](../../02_architecture/blocks-financial-schema-design.md) §3.5–§3.6, §5.2, §5.3, §6.2, §7, §8.1
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) (Proposed; ratified by CO 2026-05-16)
**Ratifications:** `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md` (Decision 3 — rent-collection wrapper pattern)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~10–14h sunfish-PM (5 feature PRs + 1 integration PR + ~40–45 tests + docs + attribution + rent-collection wrapper)
**PR count:** 6 PRs
**Pre-merge council:** NOT required (substrate scope; mirrors the W#34/W#35/W#36/W#60-P4 substrate-only pattern from the sibling `blocks-financial-ledger` hand-off). Standard COB self-audit applies. **EXCEPTION:** if PR 5 (the rent-collection wrapper) cannot preserve the existing `IRentCollectionService` surface verbatim, **halt and council-review** the breaking-change surface — file `cob-question-*` first.
**Audit before build:**
```bash
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-financial-(ar|ledger|tax|periods)"
```
Expected at this hand-off's start: `blocks-financial-ledger/` exists (per the sibling ledger hand-off, all 6 PRs merged); nothing matching `blocks-financial-ar/`.

---

## Context

### Phase 1 critical path position

Per ADR 0088 §1 + the sibling `blocks-financial-ledger-chart-and-journal-stage06-handoff.md`, the Phase 1 financial-cluster decomposition is:

```
blocks-financial-ledger    (Chart + Journal core — substrate)   ✓ shipped
blocks-financial-periods   (FiscalYear + FiscalPeriod)          (sibling hand-off, parallel)
blocks-financial-tax       (TaxCode + TaxRate + TaxJurisdiction)(sibling hand-off, parallel)
blocks-financial-ar        ← THIS HAND-OFF
blocks-financial-ap        (Bill + BillLine; mirror of -ar)     (follow-on)
blocks-financial-payments  (Payment + PaymentApplication)       (follow-on)
```

`blocks-financial-ar` is the **first customer-facing financial cluster unit** and the gate that unblocks:

- `blocks-financial-payments` (PaymentApplication.invoiceId → Invoice.id; cannot model without Invoice)
- `blocks-rent-collection` retrofit (the wrapper pattern per Decision 3 — see PR 5)
- `blocks-reports-*` AR aging report (consumes `IArAgingService`)
- `blocks-property-*` tenant balance display (consumes Invoice via `IPartyReadModel`-style cross-cluster contract)

It is **not** the critical-path predecessor to `blocks-financial-ap` (AP can be built in parallel because Bill mirrors Invoice but uses an AP account; this hand-off's patterns will be lifted into the AP hand-off).

### What this hand-off ships

Per `blocks-financial-schema-design.md` §3.5–§3.6, §5.2–§5.3, §6.2, §8.1:

1. **`Invoice`** record entity (header) — chartId, invoiceNumber, customerId, propertyId?, issueDate, dueDate, lines, computed totals, status, arAccountId, journalEntryId, voidedByEntryId, externalRef.
2. **`InvoiceLine`** record entity (line items) — quantity, unitPrice, amount, incomeAccountId, taxCodeId, taxAmount, propertyId?, classId?, lineNumber.
3. **`InvoiceStatus`** state machine — `Draft → Issued → PartiallyPaid → Paid → (Voided | WrittenOff)`; `Overdue` is a **derived** read-time computation.
4. **`IInvoiceNumberingService`** with per-replica-suffix monotonic scheme per `crdt-friendly-schema-conventions.md` §1 / §8.
5. **`IInvoicePostingService`** — on `issue()` transition, posts a `JournalEntry` via `IJournalPostingService` (AR debit + Revenue credit + tax-payable credits per line). Idempotent: re-posting a posted invoice is a no-op.
6. **`IArAgingService`** — AR aging bucket algorithm per `blocks-financial-schema-design.md` §6.2 (current / 0-30 / 31-60 / 61-90 / 90+); per-customer + per-property breakdowns.
7. **`blocks-rent-collection` wrapper retrofit** — `RentCollection.Invoice` becomes a thin specialization that delegates issuance + AR posting to canonical `Invoice` per Decision 3 of the 2026-05-16 ratification ruling.
8. **ERPNext importer integration** — `IErpnextSalesInvoiceImporter` consuming the migration-importer spec's Pass 2 (sales-invoice upsert).
9. **Cross-cluster events** — `Financial.InvoiceCreated`, `Financial.InvoiceIssued`, `Financial.InvoicePaid`, `Financial.InvoiceVoided`, `Financial.InvoiceWrittenOff` per `cross-cluster-event-bus-design.md` §3.1.

### What this hand-off does NOT ship

- `Receipt` (§3.11) — comes with `blocks-financial-payments`.
- `Payment` + `PaymentApplication` (§3.9–§3.10) — comes with `blocks-financial-payments`. **PR 4 + PR 5 reference these via stub interfaces (`IPaymentApplicationRepository`) only**; the in-memory stub feeds tests; the real implementation arrives in the sibling hand-off.
- `Bill` (§3.7) and `BillLine` (§3.8) — comes with `blocks-financial-ap`.
- Bank reconciliation (§6.3) — separate Phase 1 hand-off.
- Customer-statement PDF rendering — `blocks-reports-*` owns the rendering pipeline; this hand-off ships only the **query support** for the statement (a method on `IInvoiceQueryService` that returns the open + recently-paid invoices for a customer over a date range).

### Naming ratification (binding, Decision 3)

Per the 2026-05-16 ratification ruling:

> Existing `blocks-rent-collection.Invoice` + `Payment` become **non-breaking wrappers** over the canonical financial-AR `Invoice` + `Payment` once those land. Wrapper relationship lands when `blocks-financial-ar` ships in a follow-on hand-off.

**This hand-off ships that follow-on.** PR 5 implements the rent-collection wrapper. The existing `IRentCollectionService` interface remains unchanged from the consumer's perspective; internally, `InMemoryRentCollectionService.IssueInvoice(...)` delegates to `IInvoicePostingService.IssueAsync(...)` on the canonical AR Invoice, then materializes a `RentCollection.Invoice` projection from the AR Invoice for back-compat consumer use.

### Why AR is the right cluster to ship next

1. **`blocks-financial-ledger` shipped first** (per the sibling hand-off — 6 PRs + ~55 tests). The substrate is ready: `GLAccount`, `ChartOfAccounts`, `JournalEntry`, `JournalEntryLine`, `IJournalPostingService`. Invoice posting consumes that surface directly.
2. **`blocks-rent-collection` already on main carries an `Invoice` type** (rent-schedule-driven; see `packages/blocks-rent-collection/Models/Invoice.cs`). Decision 3 requires the wrapper retrofit to land before `blocks-rent-collection` can be modernized; AR is therefore the predecessor.
3. **The ERPNext migration importer's Pass 2 (sales invoices) is the next pass after Pass 1 (accounts) + Pass 3 (opening-balance JEs)** — both already shipped via the ledger hand-off. Pass 2 needs this hand-off's `IErpnextSalesInvoiceImporter`.
4. **Customer-balance display is the most-asked-for UX surface** in the property-business demo. Without AR, tenant balance displays cannot be wired.
5. **The 4-LLC / $7.6M Wave-history acceptance test** (sibling ledger hand-off §13) needs sales invoices to validate end-to-end Schedule E generation; AR is the predecessor.

### CRDT-friendly conventions applied (binding)

Per `_shared/engineering/crdt-friendly-schema-conventions.md`:

| Convention | Applied where |
|---|---|
| §1 ULID identifiers | `InvoiceId`, `InvoiceLineId` — strongly typed; ULID storage |
| §1 / §8 Monotonic numbers — per-replica-suffix scheme | `Invoice.InvoiceNumber` — format `INV-YYYY-MM-DD-{replicaId}-{seq}`; `IInvoiceNumberingService` generates the next sequence per-replica |
| §2 Soft-delete tombstones | `Invoice.deletedAt` / `deletedBy` on the record; hard-delete only allowed on `Draft` state (parallel to JournalEntry §3.3 discipline) |
| §3 version + revisionVector | `Invoice.Version` int + `Invoice.RevisionVector` Dictionary<string,long> — Loro-managed; application reads only |
| §4 Append-only sub-collections | `InvoiceLine[]` is append-only AFTER `Issued`; corrections via credit-memo invoice + reversal JE (not line mutation) |
| §5 Stable string codes | `InvoiceStatus` enum surfaces as a string code (`"Draft"`, `"Issued"`, etc.) over the wire; persistent storage as text |
| §6 Posted-then-immutable | Once `Invoice.status ∈ {Issued, PartiallyPaid, Paid}`, the header is immutable. Status transitions are allowed (`PartiallyPaid → Paid`) but field mutations are not. Voiding requires posting a reversal JE; the invoice row gains `voidedByEntryId` but the original numbers stay |
| §7 State-machine-under-CRDT pattern B — terminal wins | Per §7 cluster table: `Invoice` uses **Pattern B (terminal-wins)**: `Voided` / `WrittenOff` > `Paid` > `PartiallyPaid` > `Issued`. Implemented in `InvoiceStatusResolver` registered with `kernel-crdt`. **Designated-authority is NOT used** for Invoice — any replica can advance the state, but the resolver picks the canonical winner deterministically |
| §10 Two-tier validation | Tier-1 write-time on every Invoice persist; Tier-2 post-merge reconciler verifies `sum(applications.amountApplied) <= invoice.total` and emits `PaymentOverapplied` if violated. The reconciler ships as a stub in PR 4 and is wired into `IPostMergeReconciler` in the follow-on `blocks-financial-payments` hand-off |

The combination ensures: (a) two offline replicas can each draft + issue independent invoices and converge cleanly; (b) numbering is gap-free per-replica even under sync delay; (c) status divergence (one replica voids, another marks paid) resolves to `Voided` terminally.

### Open question Q10 (financial design) — Loro append-only constraint

Per the sibling ledger hand-off, Q10 remains **open** at this hand-off's cutoff. PR 3 posts JEs through `IJournalPostingService` (the ledger surface); the invoice row itself is updated in place (status changes after issuance), so this hand-off does NOT depend on Q10 being resolved. **The PRs in this hand-off do not touch Loro op-log integration directly**; they implement immutability at the service layer (`IInvoicePostingService` rejects any field mutation on `status != Draft` via Tier-1 validation).

**Halt condition (see §Halt-conditions):** if COB hits a Loro append-only question, file a `cob-question-*` beacon.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify sibling ledger hand-off is built.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/Services/IJournalPostingService.cs 2>&1
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-ledger/Migration/IErpnext*Importer.cs 2>&1
   ```
   Expected: all three exist. If `blocks-financial-ledger/` does not exist or doesn't carry `IJournalPostingService`, **STOP** — the sibling hand-off must land first. Drop a `cob-question-*` beacon.

2. **Verify sibling `-tax` and `-periods` packages' status.**
   ```bash
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-tax/ 2>&1
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-financial-periods/ 2>&1
   gh pr list --state open --search "blocks-financial-tax in:title,body"
   gh pr list --state open --search "blocks-financial-periods in:title,body"
   ```
   Expected: ideally exist. If not, **proceed anyway** with stub interfaces — PR 3 (posting) needs `ITaxCalculationService` and `IPeriodResolver`. Each is delivered with a stub `InMemoryTaxCalculationService` (returns zero tax — flag is acceptable for v1 single-jurisdiction USD demo) and `InMemoryPeriodResolver` (already shipped by the ledger hand-off). If the real services land later, DI swap replaces the stubs.

3. **Verify Party/Customer cross-cluster contract.**
   ```bash
   grep -rln "IPartyReadModel\|PartyId" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/foundation-* /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-* 2>/dev/null | head -5
   ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/blocks-people-* 2>&1
   ```
   Expected: most likely **no `blocks-people-*` package on main yet** (per anatomy.md). If absent, ship a local `PartyId` strong-id type in this package (mirroring `blocks-leases/Models/PartyId.cs` which already exists) and define a minimal local `IPartyReadModel` contract within `blocks-financial-ar` that the in-memory test harness implements. When `blocks-people-*` lands, the contract relocates; this hand-off's package's reference is a single `using` directive update. **DO NOT WRITE Party rows from this hand-off** — read-only via the contract per `party-model-convention.md` §4.

4. **Confirm ADR 0088 status.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: `status: Proposed` (CO ratified design 2026-05-16; status-flip is housekeeping). Hand-off is `ready-to-build` regardless — CO directive operative.

5. **Confirm `blocks-rent-collection` consumer set (for PR 5).**
   ```bash
   grep -rln "Sunfish.Blocks.RentCollection" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ /Users/christopherwood/Projects/SunfishSoftware/Sunfish/apps/ /Users/christopherwood/Projects/SunfishSoftware/Sunfish/accelerators/
   grep -rln "IRentCollectionService" /Users/christopherwood/Projects/SunfishSoftware/Sunfish/
   ```
   Capture every match; the PR 5 wrapper retrofit must preserve every public-surface call against these consumers. If the consumer set is larger than 5 projects, file `cob-question-*` for a council review of the wrapper boundary.

6. **Confirm no parallel-session PRs touch `blocks-rent-collection/` or `blocks-financial-*`.**
   ```bash
   gh pr list --state open --search "blocks-rent-collection in:title,body"
   gh pr list --state open --search "blocks-financial in:title,body"
   ```
   Expected: empty (or only this hand-off's own PRs). If anything else is open, file `cob-question-*`.

7. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler`).

8. **Read the Stage 02 design source sections.** Skim `blocks-financial-schema-design.md` §3.5, §3.6, §5.2, §5.3, §6.2, §7, §8.1. Read `crdt-friendly-schema-conventions.md` §1 + §8 (per-replica-suffix monotonic numbering). Read `cross-cluster-event-bus-design.md` §3.1 (`Financial.*` events catalog).

---

## Per-PR deliverables

This hand-off splits into **6 PRs** by responsibility:

- PR 1: Package scaffold + Invoice + InvoiceLine entities + InvoiceStatus state machine (substrate)
- PR 2: `IInvoiceNumberingService` with per-replica-suffix scheme + collision detection
- PR 3: `IInvoicePostingService` — JE posting on Draft→Issued; tax calculation; idempotent
- PR 4: `IArAgingService` — bucket algorithm + per-customer + per-property breakdowns
- PR 5: `blocks-rent-collection` wrapper retrofit — non-breaking API preservation; delegation to canonical AR
- PR 6: ERPNext importer integration (`IErpnextSalesInvoiceImporter`) per migration-importer spec

PRs 1 + 2 + 3 are sequential. PR 4 can parallelize with PR 3 once PR 1 is in. PR 5 + PR 6 sequence last (both depend on PRs 1-4).

---

### PR 1 — Package scaffold + Invoice + InvoiceLine entities + InvoiceStatus state machine

**Estimated effort:** ~2–3h
**Scope:** new package `blocks-financial-ar`; core records; status enum; status-transition validation helper; NO posting logic (that's PR 3); NO numbering (PR 2)
**Commit subject:** `feat(blocks-financial-ar): scaffold AR package with Invoice + InvoiceLine + InvoiceStatus per Stage 02 §3.5–§3.6`
**Branch:** `cob/blocks-financial-ar-scaffold`

#### Package skeleton

```
packages/blocks-financial-ar/
├── README.md
├── NOTICE.md                                       (Apache OFBiz attribution)
├── Sunfish.Blocks.FinancialAr.csproj
├── Models/
│   ├── InvoiceId.cs
│   ├── InvoiceLineId.cs
│   ├── Invoice.cs
│   ├── InvoiceLine.cs
│   ├── InvoiceStatus.cs
│   └── ReplicaId.cs                                (local placeholder; see Halt §1)
├── Services/
│   ├── (PR 1: none beyond stubs)
│   └── IInvoiceRepository.cs                       (read+write boundary)
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs
└── tests/
    ├── Sunfish.Blocks.FinancialAr.Tests.csproj
    ├── InvoiceRecordTests.cs
    ├── InvoiceLineRecordTests.cs
    └── InvoiceStatusTransitionTests.cs
```

#### New types

**`Models/InvoiceId.cs`** — ULID strongly-typed id, mirrors the existing `Sunfish.Blocks.FinancialLedger.JournalEntryId` pattern.

**`Models/InvoiceLineId.cs`** — ULID strongly-typed id.

**`Models/ReplicaId.cs`** — 2-character replica suffix, value-object.

```csharp
public readonly record struct ReplicaId
{
    public string Value { get; }
    public ReplicaId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 2)
            throw new ArgumentException("ReplicaId must be exactly 2 characters", nameof(value));
        if (!value.All(char.IsLetterOrDigit))
            throw new ArgumentException("ReplicaId must be alphanumeric", nameof(value));
        Value = value.ToUpperInvariant();
    }
    public override string ToString() => Value;
}
```

(This is a temporary local placeholder. When `foundation-localfirst` lands its canonical `ReplicaId`, relocate. Mark with a `// TODO: relocate to foundation-localfirst.ReplicaId when foundation-localfirst ships` comment. See Halt §1.)

**`Models/InvoiceStatus.cs`** per Stage 02 §3.5:

```csharp
public enum InvoiceStatus
{
    Draft,
    Issued,
    PartiallyPaid,
    Paid,
    Voided,
    WrittenOff,
}
```

`Overdue` is **NOT** an enum value — it is a derived read-time computation per Stage 02 §3.5 ("`Overdue` is a derived status: `Issued || PartiallyPaid` and `today > dueDate` and `balance > 0`. It is not a persisted state; computed at read time.").

A `static class InvoiceStatusExtensions` provides:

```csharp
public static bool IsOpen(this InvoiceStatus s)
    => s == InvoiceStatus.Issued || s == InvoiceStatus.PartiallyPaid;

public static bool IsTerminal(this InvoiceStatus s)
    => s == InvoiceStatus.Paid || s == InvoiceStatus.Voided || s == InvoiceStatus.WrittenOff;

public static bool IsOverdue(this Invoice inv, DateOnly asOf)
    => inv.Status.IsOpen() && asOf > inv.DueDate && inv.Balance > 0m;
```

#### `Invoice` record

```csharp
public sealed record Invoice
{
    public InvoiceId Id { get; init; }
    public ChartOfAccountsId ChartId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;  // populated by IInvoiceNumberingService (PR 2)
    public PartyId CustomerId { get; init; }
    public string? PropertyId { get; init; }                    // optional cost-center; opaque string in v1
    public DateOnly IssueDate { get; init; }
    public DateOnly DueDate { get; init; }
    public string Currency { get; init; } = "USD";              // ISO 4217
    public IReadOnlyList<InvoiceLine> Lines { get; init; } = Array.Empty<InvoiceLine>();

    // Computed monetary fields (cached; recomputed on line mutation while Draft):
    public decimal Subtotal { get; init; }                      // sum of Lines[].Amount before tax
    public decimal TaxTotal { get; init; }                      // sum of Lines[].TaxAmount
    public decimal Total { get; init; }                         // Subtotal + TaxTotal
    public decimal AmountPaid { get; init; }                    // running total from applied payments
    public decimal Balance { get; init; }                       // Total - AmountPaid

    public InvoiceStatus Status { get; init; } = InvoiceStatus.Draft;
    public GLAccountId ArAccountId { get; init; }               // AR account; Asset/AccountsReceivable-subtype
    public string? Notes { get; init; }
    public string? TermsId { get; init; }                       // FK to PaymentTerms; opaque in v1
    public JournalEntryId? JournalEntryId { get; init; }        // null until Issued
    public JournalEntryId? VoidedByEntryId { get; init; }       // non-null when Voided
    public JournalEntryId? WrittenOffByEntryId { get; init; }   // non-null when WrittenOff
    public string? ExternalRef { get; init; }                   // ERPNext source ref; idempotency key

    // CRDT envelope fields (per crdt-friendly-schema-conventions.md §3):
    public long Version { get; init; }                          // monotonic per-replica counter
    public IReadOnlyDictionary<string, long>? RevisionVector { get; init; } // Loro-managed

    // Tombstone (per §2):
    public Instant? DeletedAtUtc { get; init; }
    public PartyId? DeletedBy { get; init; }
    public string? DeletedReason { get; init; }

    // Audit:
    public Instant CreatedAtUtc { get; init; }
    public PartyId? CreatedBy { get; init; }
    public Instant UpdatedAtUtc { get; init; }
    public PartyId? UpdatedBy { get; init; }
}
```

**Notes on monetary representation:** per Stage 02 §7, the C# surface uses `decimal` with two-decimal rounding (back-compat with existing `blocks-rent-collection.Invoice`). Banker's rounding (`MidpointRounding.ToEven`) at every minor-unit boundary; no floats anywhere. The wire/SQLite shape is integer-minor-units; conversion is at the persistence boundary (out of scope for this hand-off — the records are in-memory v1).

#### `InvoiceLine` record

```csharp
public sealed record InvoiceLine
{
    public InvoiceLineId Id { get; init; }
    public InvoiceId InvoiceId { get; init; }
    public int LineNumber { get; init; }                        // 1..n
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }                      // permits decimal (e.g. 1.5 hours)
    public decimal UnitPrice { get; init; }
    public decimal Amount { get; init; }                        // round(quantity * unitPrice, 2, ToEven)
    public GLAccountId IncomeAccountId { get; init; }           // credited at post-time; Income/OperatingIncome subtype
    public string? TaxCodeId { get; init; }                     // FK to TaxCode (opaque v1)
    public decimal TaxAmount { get; init; }                     // computed via ITaxCalculationService at post-time
    public string? PropertyId { get; init; }                    // optional cost-center
    public string? ClassificationId { get; init; }              // user-defined classification
    public string? Notes { get; init; }
}
```

#### `IInvoiceRepository` (write boundary)

```csharp
public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken ct = default);
    Task<Invoice?> GetByExternalRefAsync(string source, string externalRefId, CancellationToken ct = default);
    Task<Invoice?> GetByNumberAsync(ChartOfAccountsId chartId, string invoiceNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> QueryOpenAsync(
        ChartOfAccountsId chartId,
        PartyId? customerId = null,
        string? propertyId = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> QueryStatementAsync(
        ChartOfAccountsId chartId,
        PartyId customerId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);
    Task UpsertAsync(Invoice invoice, CancellationToken ct = default);
    Task TombstoneAsync(InvoiceId id, PartyId by, string? reason, CancellationToken ct = default);
}

public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly ConcurrentDictionary<InvoiceId, Invoice> _byId = new();
    private readonly ConcurrentDictionary<(string, string), InvoiceId> _byExternalRef = new();
    private readonly ConcurrentDictionary<(ChartOfAccountsId, string), InvoiceId> _byNumber = new();
    // ... see InMemoryAccountResolver in blocks-financial-ledger for the precedent
}
```

#### Status-transition validation helper

```csharp
public static class InvoiceStatusTransitions
{
    private static readonly IReadOnlyDictionary<InvoiceStatus, IReadOnlySet<InvoiceStatus>> _allowed
        = new Dictionary<InvoiceStatus, IReadOnlySet<InvoiceStatus>>
        {
            [InvoiceStatus.Draft]         = new HashSet<InvoiceStatus> { InvoiceStatus.Issued },
            [InvoiceStatus.Issued]        = new HashSet<InvoiceStatus> { InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid, InvoiceStatus.Voided, InvoiceStatus.WrittenOff },
            [InvoiceStatus.PartiallyPaid] = new HashSet<InvoiceStatus> { InvoiceStatus.Paid, InvoiceStatus.Voided, InvoiceStatus.WrittenOff, InvoiceStatus.Issued /* unapply-all reverts */ },
            [InvoiceStatus.Paid]          = new HashSet<InvoiceStatus> { InvoiceStatus.PartiallyPaid /* payment-bounce or unapply */, InvoiceStatus.Voided },
            [InvoiceStatus.Voided]        = new HashSet<InvoiceStatus>(),  // terminal
            [InvoiceStatus.WrittenOff]    = new HashSet<InvoiceStatus>(),  // terminal
        };

    public static bool IsAllowed(InvoiceStatus from, InvoiceStatus to)
        => _allowed.TryGetValue(from, out var set) && set.Contains(to);

    public static void EnsureAllowed(InvoiceStatus from, InvoiceStatus to)
    {
        if (!IsAllowed(from, to))
            throw new InvalidOperationException(
                $"Invalid InvoiceStatus transition: {from} → {to}. Allowed targets from {from}: {string.Join(", ", _allowed[from])}");
    }
}
```

#### DI extension

**`DependencyInjection/ServiceCollectionExtensions.cs`**:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlocksFinancialAr(this IServiceCollection services)
    {
        services.AddSingleton<IInvoiceRepository, InMemoryInvoiceRepository>();
        return services;
    }
}
```

(Subsequent PRs extend this method.)

#### Tests (PR 1)

`tests/InvoiceRecordTests.cs`:

- `Construction_PreservesAllFields`.
- `Computed_Total_EqualsSubtotalPlusTaxTotal`.
- `Computed_Balance_EqualsTotalMinusAmountPaid`.
- `LineAmount_RoundsBankerHalfEven_AtTwoDecimals` (quantity 1.005 × unitPrice 100 → 100.50, not 100.51).
- `EmptyLines_Subtotal_IsZero`.
- `MultipleLines_SubtotalIsCorrectSum`.

`tests/InvoiceLineRecordTests.cs`:

- `Construction_PreservesAllFields`.
- `Quantity_AcceptsFractional` (e.g. 1.5 hours billed).
- `Amount_RoundsBankerHalfEven`.
- `LineNumber_Required_PositiveInt`.

`tests/InvoiceStatusTransitionTests.cs`:

- `DraftToIssued_IsAllowed`.
- `DraftToPaid_IsNotAllowed` (must go through Issued).
- `IssuedToPaid_IsAllowed`.
- `IssuedToPartiallyPaid_IsAllowed`.
- `IssuedToVoided_IsAllowed`.
- `PartiallyPaidToPaid_IsAllowed`.
- `PaidToPartiallyPaid_IsAllowed` (payment-bounce / unapply scenario).
- `VoidedToAnything_IsNotAllowed` (terminal).
- `WrittenOffToAnything_IsNotAllowed` (terminal).
- `EnsureAllowed_ThrowsWithDescriptiveMessage`.

`tests/InvoiceRepositoryTests.cs`:

- `UpsertAndGetById_RoundTrips`.
- `GetByExternalRef_ReturnsCorrectInvoice`.
- `GetByNumber_ReturnsCorrectInvoice`.
- `Tombstone_SetsDeletedAt_FiltersFromDefaultQueries`.
- `QueryOpen_FiltersByStatus_IssuedOrPartiallyPaid`.
- `QueryStatement_FiltersByDateAndCustomer`.

Total new tests this PR: ~16.

#### Verification

- `dotnet build` succeeds for the new package + adds it to the solution.
- `dotnet test packages/blocks-financial-ar/tests/` passes all ~16 tests.
- `grep -r "Sunfish.Blocks.FinancialAr" packages/blocks-financial-ar/` returns hits in every `.cs` file (sanity check on namespace).

#### Do NOT in this PR

- Do NOT post journal entries. PR 3 ships posting.
- Do NOT implement numbering. PR 2 ships it.
- Do NOT touch `blocks-rent-collection/`. PR 5 ships the wrapper retrofit.
- Do NOT introduce a `PaymentApplication` type — that ships with `blocks-financial-payments`. The `AmountPaid` field is set externally by the future `IInvoicePaymentApplicationService`; in PR 1 it's just a writable field.

---

### PR 2 — `IInvoiceNumberingService` with per-replica-suffix monotonic scheme

**Estimated effort:** ~2h
**Scope:** number generation; format `INV-YYYY-MM-DD-{ReplicaId}-{seq:D4}`; per-replica monotonic state; collision detection on first sync
**Commit subject:** `feat(blocks-financial-ar): IInvoiceNumberingService with per-replica monotonic numbering per CRDT conventions §1`
**Depends on:** PR 1 merged
**Branch:** `cob/blocks-financial-ar-numbering`

#### New types

**`Services/IInvoiceNumberingService.cs`**:

```csharp
public interface IInvoiceNumberingService
{
    /// <summary>
    /// Reserves the next invoice number for this chart on this replica.
    /// Format: INV-YYYY-MM-DD-{ReplicaId}-{seq:D4}.
    /// Numbers are monotonic per (chartId, replicaId) only — NOT globally.
    /// </summary>
    Task<string> NextNumberAsync(
        ChartOfAccountsId chartId,
        DateOnly issueDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects a numbering collision across replicas. Called by sync layer
    /// when a remote replica's op-log references a number this replica already
    /// holds. Returns the replica that must re-key (smaller replica by
    /// install-record createdAt).
    /// </summary>
    Task<ReplicaId> ResolveCollisionAsync(
        ChartOfAccountsId chartId,
        string conflictingNumber,
        ReplicaId localReplica,
        ReplicaId remoteReplica,
        Instant localReplicaCreatedAt,
        Instant remoteReplicaCreatedAt,
        CancellationToken cancellationToken = default);
}
```

#### Format spec

```
INV-YYYY-MM-DD-{ReplicaId}-{seq:D4}
       │           │          └──── per-replica sequence (gap-free per replica), zero-padded to 4 digits
       │           └─────────────── 2-character replica suffix (uppercase alphanumeric)
       └─────────────────────────── issue date in ISO-8601 (YYYY-MM-DD)
```

Examples (per `crdt-friendly-schema-conventions.md` §8): `INV-2026-05-16-CW-0124`, `INV-2026-05-16-A4-0125`.

#### Implementation

**`Services/InMemoryInvoiceNumberingService.cs`**:

```csharp
public sealed class InMemoryInvoiceNumberingService : IInvoiceNumberingService
{
    private readonly ReplicaId _localReplica;
    // (chartId, replicaId) -> next sequence value:
    private readonly ConcurrentDictionary<(ChartOfAccountsId, ReplicaId), long> _sequences = new();
    private readonly object _lock = new();

    public InMemoryInvoiceNumberingService(ReplicaId localReplica)
    {
        _localReplica = localReplica;
    }

    public Task<string> NextNumberAsync(
        ChartOfAccountsId chartId, DateOnly issueDate, CancellationToken ct)
    {
        long seq;
        lock (_lock)  // simple correctness for in-memory; SQLite version would use INSERT...RETURNING
        {
            seq = _sequences.AddOrUpdate(
                (chartId, _localReplica),
                addValue: 1L,
                updateValueFactory: (_, current) => current + 1L);
        }
        var number = $"INV-{issueDate:yyyy-MM-dd}-{_localReplica}-{seq:D4}";
        return Task.FromResult(number);
    }

    public Task<ReplicaId> ResolveCollisionAsync(
        ChartOfAccountsId chartId, string conflictingNumber,
        ReplicaId localReplica, ReplicaId remoteReplica,
        Instant localReplicaCreatedAt, Instant remoteReplicaCreatedAt,
        CancellationToken ct)
    {
        // Smaller (older) replica wins; younger re-keys.
        // Per CRDT conventions §1: "the smaller (by createdAt of the replica
        // record) re-keys."
        // Smaller = older = wins; younger = must re-key.
        var loser = localReplicaCreatedAt <= remoteReplicaCreatedAt
            ? remoteReplica  // remote is younger; remote re-keys
            : localReplica;  // local is younger; local re-keys
        return Task.FromResult(loser);
    }
}
```

#### Validation (write-time)

`Invoice.InvoiceNumber` must match the format regex on write — this is the Tier-1 validation per CRDT conventions §10:

```csharp
private static readonly Regex InvoiceNumberRegex = new(
    @"^INV-\d{4}-\d{2}-\d{2}-[A-Z0-9]{2}-\d{4,}$",
    RegexOptions.Compiled);

public static bool IsWellFormed(string invoiceNumber)
    => InvoiceNumberRegex.IsMatch(invoiceNumber);
```

The repository layer (PR 1's `InMemoryInvoiceRepository`) is extended in PR 2 to validate this on `UpsertAsync` for invoices in `Issued` or terminal states. Draft invoices may carry an empty `InvoiceNumber` (the number is assigned at `issue()` time in PR 3).

#### DI registration

Extend `ServiceCollectionExtensions.AddBlocksFinancialAr` to register the numbering service. The `ReplicaId` is taken from a `BlocksFinancialArOptions` value passed in:

```csharp
public sealed class BlocksFinancialArOptions
{
    public ReplicaId LocalReplicaId { get; init; } = new("AA"); // default; overridden at registration
}

public static IServiceCollection AddBlocksFinancialAr(
    this IServiceCollection services,
    Action<BlocksFinancialArOptions>? configure = null)
{
    var options = new BlocksFinancialArOptions();
    configure?.Invoke(options);
    services.AddSingleton(options);
    services.AddSingleton<IInvoiceRepository, InMemoryInvoiceRepository>();
    services.AddSingleton<IInvoiceNumberingService>(sp =>
        new InMemoryInvoiceNumberingService(options.LocalReplicaId));
    return services;
}
```

(Per the COB self-audit pattern from W#34/W#35: two-overload audit-disabled / audit-enabled both-or-neither is NOT required here because no audit interaction yet. If audit is later wired, retrofit per that pattern.)

#### Tests (PR 2)

`tests/InvoiceNumberingServiceTests.cs`:

- `NextNumber_FormatMatchesSpec` (regex match on `INV-YYYY-MM-DD-XX-NNNN`).
- `NextNumber_MonotonicallyIncrementsForSameReplicaAndChart` (call 5 times → seq 1..5).
- `NextNumber_IndependentSequencesPerChart` (chartA seq:0001 + chartB seq:0001 can coexist).
- `NextNumber_IndependentSequencesPerReplica` (replica CW seq:0001 + replica A4 seq:0001 can coexist on the same chart).
- `NextNumber_IncludesIssueDate` (changing issue date doesn't reset sequence — sequence is per (chart, replica), not per (chart, replica, date)).
- `NextNumber_ZeroPadsTo4Digits_AndExpandsBeyondAt10000` (seq 9999 → "0009999"; seq 10000 → "10000").
- `IsWellFormed_AcceptsValid` (positive cases).
- `IsWellFormed_RejectsInvalid` (missing replica, wrong date format, lowercase replica, non-alphanumeric replica).
- `ResolveCollision_OlderReplicaWins_YoungerReKeys`.
- `ResolveCollision_TiebreakerSimultaneousCreation` (deterministic: equal-timestamp tiebreaker by replica-id string lex order).

`tests/InvoiceRepositoryValidationTests.cs`:

- `Upsert_RejectsIssuedInvoice_WithoutValidNumber`.
- `Upsert_AllowsDraftInvoice_WithEmptyNumber`.

Total new tests this PR: ~12.

#### Verification

- `dotnet build` succeeds.
- All PR 1 tests still pass.
- New tests pass.
- A regression test demonstrates: two `InMemoryInvoiceNumberingService` instances with replicas `CW` and `A4`, both invoked 10× on the same chart with the same date, produce 20 distinct numbers without collision.

#### Do NOT in this PR

- Do NOT introduce a SQLite-backed numbering service. SQLite persistence is out of scope for v1; the in-memory service is the v1 implementation.
- Do NOT introduce a `BlocksFinancialArOptions.ReplicaId` default that hides the install-time choice — users MUST set their replica suffix. The default `"AA"` is a placeholder that should produce a warning log when used; the warning is added in PR 6 once the importer wires DI.

---

### PR 3 — `IInvoicePostingService` — atomic JE posting on Draft → Issued

**Estimated effort:** ~2–3h
**Scope:** issue() transition posts JE via `IJournalPostingService` (from ledger package); tax calculation via stub `ITaxCalculationService`; idempotent (re-posting is no-op); voiding posts reversal JE; write-off posts bad-debt JE
**Commit subject:** `feat(blocks-financial-ar): IInvoicePostingService — atomic JE posting on issue/void/write-off per Stage 02 §3.5 + §6.1`
**Depends on:** PR 2 merged
**Branch:** `cob/blocks-financial-ar-posting`

#### New service

**`Services/IInvoicePostingService.cs`**:

```csharp
public interface IInvoicePostingService
{
    Task<IssueResult> IssueAsync(
        InvoiceId invoiceId,
        CancellationToken cancellationToken = default);

    Task<VoidResult> VoidAsync(
        InvoiceId invoiceId,
        string reason,
        PartyId by,
        CancellationToken cancellationToken = default);

    Task<WriteOffResult> WriteOffAsync(
        InvoiceId invoiceId,
        GLAccountId badDebtAccountId,
        string reason,
        PartyId by,
        CancellationToken cancellationToken = default);
}

public sealed record IssueResult(
    Invoice? Invoice,
    JournalEntryId? PostedEntryId,
    IssueError Error,
    string? Detail);

public enum IssueError
{
    None,
    NotADraft,
    NoLines,
    UnknownInvoice,
    InvalidArAccount,         // arAccountId is not Asset/AccountsReceivable subtype
    InvalidIncomeAccount,     // any line.incomeAccountId is not Income type
    NumberingFailed,
    JEPostFailed,             // wraps PostError from ledger
    TaxCalculationFailed,
}

public sealed record VoidResult(Invoice? Invoice, JournalEntryId? ReversalEntryId, VoidError Error, string? Detail);
public enum VoidError { None, UnknownInvoice, NotIssuedOrPartiallyPaid, AlreadyVoided, JEPostFailed }

public sealed record WriteOffResult(Invoice? Invoice, JournalEntryId? BadDebtEntryId, WriteOffError Error, string? Detail);
public enum WriteOffError { None, UnknownInvoice, NotOpen, InvalidBadDebtAccount, JEPostFailed }
```

#### `IssueAsync` algorithm per Stage 02 §8.1 + §6.1

```text
issue(invoiceId):
  // Phase 1 — preconditions
  inv = repo.GetByIdAsync(invoiceId)
  if inv == null: return Err(UnknownInvoice)
  if inv.Status != Draft: return Err(NotADraft)    // idempotent: if Issued, return Ok with the existing JE
  if inv.Lines.Count < 1: return Err(NoLines)

  // Phase 2 — account-type validation
  arAccount = accountResolver.GetAsync(inv.ArAccountId)
  if arAccount == null || arAccount.Type != Asset || arAccount.Subtype != AccountsReceivable:
    return Err(InvalidArAccount)
  for line in inv.Lines:
    incAccount = accountResolver.GetAsync(line.IncomeAccountId)
    if incAccount == null || incAccount.Type != Revenue: return Err(InvalidIncomeAccount, line.Id)

  // Phase 3 — tax calculation (per line)
  totalTax = 0
  for line in inv.Lines:
    taxResult = await taxCalculator.ComputeLineTaxAsync(line, inv.IssueDate, inv.PropertyId)
    line.TaxAmount = taxResult.TaxAmount
    totalTax += taxResult.TaxAmount
    // record per-rate breakdown for JE lines (tax-payable account per rate)

  // Phase 4 — number reservation
  if string.IsNullOrEmpty(inv.InvoiceNumber):
    inv.InvoiceNumber = await numberer.NextNumberAsync(inv.ChartId, inv.IssueDate)

  // Phase 5 — JE construction
  arDebit       = inv.Total                          // = Subtotal + TaxTotal
  revenueCredits = group(line by line.IncomeAccountId, sum amount)
  taxCredits     = group(taxResult.Breakdown by payableAccountId, sum amount)

  je = new JournalEntry(
    Id: JournalEntryId.New(),
    EntryDate: inv.IssueDate,
    Memo: $"Invoice {inv.InvoiceNumber}",
    Lines: [
      JournalEntryLine(arAccount, Debit: arDebit, Credit: 0,    PropertyId: inv.PropertyId),
      ... for each revenueCredits: JournalEntryLine(Debit: 0, Credit: amount, PropertyId: inv.PropertyId, ClassificationId: line.ClassificationId)
      ... for each taxCredits:     JournalEntryLine(Debit: 0, Credit: amount, TaxCodeId: taxCodeId)
    ],
    ChartId: inv.ChartId,
    SourceKind: JournalEntrySource.Invoice,
    SourceReference: $"invoice:{inv.Id}",
    ExternalRef: inv.ExternalRef,
    Status: JournalEntryStatus.Draft,
    CreatedAtUtc: time.GetUtcNow())

  // Phase 6 — post JE via the ledger surface
  postResult = await jePosting.PostAsync(je)
  if postResult.Error != PostError.None:
    return Err(JEPostFailed, postResult.Detail)

  // Phase 7 — update invoice
  posted = inv with {
    Status = InvoiceStatus.Issued,
    Subtotal = sum(line.Amount),
    TaxTotal = totalTax,
    Total = Subtotal + TaxTotal,
    Balance = Total - inv.AmountPaid,
    JournalEntryId = postResult.Entry.Id,
    UpdatedAtUtc = time.GetUtcNow(),
    UpdatedBy = userContext.CurrentPartyId,
    Version = inv.Version + 1,
  }
  await repo.UpsertAsync(posted)

  // Phase 8 — emit events
  await events.PublishAsync(new InvoiceIssuedEvent(
    InvoiceId: posted.Id, CustomerId: posted.CustomerId,
    TotalAmount: posted.Total, DueDate: posted.DueDate, PropertyId: posted.PropertyId))

  return Ok(posted, postResult.Entry.Id)
```

**Idempotency:** if `inv.Status == Issued` AND `inv.JournalEntryId != null`, return `Ok` with the existing JE id rather than re-posting. This mirrors the migration-importer-spec §5.2 discipline (posted entries are not re-posted).

#### `VoidAsync` algorithm per Stage 02 §8.1

```text
void(invoiceId, reason, by):
  inv = repo.GetByIdAsync(invoiceId)
  if inv == null: return Err(UnknownInvoice)
  if !inv.Status.IsOpen() && inv.Status != InvoiceStatus.Paid: return Err(NotIssuedOrPartiallyPaid)
  if inv.Status == InvoiceStatus.Voided: return Err(AlreadyVoided)

  originalJE = jeRepo.GetByIdAsync(inv.JournalEntryId)
  reversalJE = JournalEntry.Reverse(originalJE, sourceKind: Reversal, reversalOf: originalJE.Id)
  postResult = await jePosting.PostAsync(reversalJE)
  if postResult.Error != PostError.None: return Err(JEPostFailed)

  voided = inv with {
    Status = InvoiceStatus.Voided,
    VoidedByEntryId = postResult.Entry.Id,
    UpdatedAtUtc = now, UpdatedBy = by, Version = inv.Version + 1,
    Notes = inv.Notes is null ? $"Voided: {reason}" : $"{inv.Notes}\nVoided: {reason}",
  }
  await repo.UpsertAsync(voided)
  await events.PublishAsync(new InvoiceVoidedEvent(voided.Id, postResult.Entry.Id))
  return Ok(voided, postResult.Entry.Id)
```

#### `WriteOffAsync` algorithm per Stage 02 §8.1

```text
writeOff(invoiceId, badDebtAccountId, reason, by):
  inv = repo.GetByIdAsync(invoiceId)
  if inv == null: return Err(UnknownInvoice)
  if !inv.Status.IsOpen(): return Err(NotOpen)
  badDebtAccount = accountResolver.GetAsync(badDebtAccountId)
  if badDebtAccount.Type != Expense || badDebtAccount.Subtype != OperatingExpense: return Err(InvalidBadDebtAccount)

  // Post: Dr Bad-Debt Expense, Cr AR (for inv.Balance)
  badDebtJE = JournalEntry.Create(
    EntryDate: today, Memo: $"Write-off invoice {inv.InvoiceNumber}: {reason}",
    Lines: [
      Line(badDebtAccountId, Debit: inv.Balance, Credit: 0),
      Line(inv.ArAccountId,   Debit: 0,           Credit: inv.Balance),
    ],
    ChartId: inv.ChartId, SourceKind: JournalEntrySource.Adjusting, SourceReference: $"writeoff:{inv.Id}")

  postResult = await jePosting.PostAsync(badDebtJE)
  if postResult.Error != PostError.None: return Err(JEPostFailed)

  writtenOff = inv with {
    Status = InvoiceStatus.WrittenOff,
    WrittenOffByEntryId = postResult.Entry.Id,
    AmountPaid = inv.Total,          // forced balance-zero by accounting convention
    Balance = 0,
    UpdatedAtUtc = now, UpdatedBy = by, Version = inv.Version + 1,
    Notes = inv.Notes is null ? $"Written off: {reason}" : $"{inv.Notes}\nWritten off: {reason}",
  }
  await repo.UpsertAsync(writtenOff)
  await events.PublishAsync(new InvoiceWrittenOffEvent(writtenOff.Id, postResult.Entry.Id))
  return Ok(writtenOff, postResult.Entry.Id)
```

#### Supporting stub services

**`Services/ITaxCalculationService.cs`** (local stub interface in this package; relocates to `blocks-financial-tax` when that lands):

```csharp
public interface ITaxCalculationService
{
    Task<TaxCalculationResult> ComputeLineTaxAsync(
        InvoiceLine line,
        DateOnly issueDate,
        string? propertyId,
        CancellationToken cancellationToken = default);
}

public sealed record TaxCalculationResult(
    decimal TaxAmount,
    IReadOnlyList<TaxBreakdownEntry> Breakdown);

public sealed record TaxBreakdownEntry(
    string TaxCodeId,
    string JurisdictionId,
    GLAccountId PayableAccountId,
    decimal Amount);

/// <summary>
/// Stub: returns zero tax. Real implementation lands with blocks-financial-tax.
/// </summary>
public sealed class NoOpTaxCalculationService : ITaxCalculationService
{
    public Task<TaxCalculationResult> ComputeLineTaxAsync(
        InvoiceLine line, DateOnly issueDate, string? propertyId, CancellationToken ct)
        => Task.FromResult(new TaxCalculationResult(0m, Array.Empty<TaxBreakdownEntry>()));
}
```

**`Services/IInvoiceEventPublisher.cs`** (local stub; relocates to whichever package owns the cross-cluster event bus per `cross-cluster-event-bus-design.md` §10 Q1):

```csharp
public interface IInvoiceEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class;
}

public sealed class InMemoryInvoiceEventPublisher : IInvoiceEventPublisher
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

#### New event record types

Per `cross-cluster-event-bus-design.md` §3.1, ship these as local record types in `Events/` (relocate when the foundation event-bus package lands):

```csharp
public sealed record InvoiceCreatedEvent(InvoiceId InvoiceId, ChartOfAccountsId ChartId, PartyId CustomerId);

public sealed record InvoiceIssuedEvent(
    InvoiceId InvoiceId, PartyId CustomerId,
    decimal TotalAmount, DateOnly DueDate, string? PropertyId);

public sealed record InvoicePaidEvent(InvoiceId InvoiceId, PartyId CustomerId, decimal TotalAmount, DateOnly PaidDate);

public sealed record InvoiceVoidedEvent(InvoiceId InvoiceId, JournalEntryId ReversalEntryId);

public sealed record InvoiceWrittenOffEvent(InvoiceId InvoiceId, JournalEntryId BadDebtJEId);
```

`InvoicePaidEvent` is emitted by the future `PaymentApplicationService` (in `blocks-financial-payments`), not by this hand-off. The record type ships here for shared consumption.

#### DI registration

Extend `ServiceCollectionExtensions.AddBlocksFinancialAr`:

```csharp
public static IServiceCollection AddBlocksFinancialAr(
    this IServiceCollection services,
    Action<BlocksFinancialArOptions>? configure = null)
{
    var options = new BlocksFinancialArOptions();
    configure?.Invoke(options);
    services.AddSingleton(options);
    services.AddSingleton<IInvoiceRepository, InMemoryInvoiceRepository>();
    services.AddSingleton<IInvoiceNumberingService>(sp =>
        new InMemoryInvoiceNumberingService(options.LocalReplicaId));
    services.AddSingleton<IInvoicePostingService, InvoicePostingService>();
    services.AddSingleton<ITaxCalculationService, NoOpTaxCalculationService>();
    services.AddSingleton<IInvoiceEventPublisher, InMemoryInvoiceEventPublisher>();
    return services;
}
```

#### Tests (PR 3)

`tests/InvoicePostingServiceTests.cs`:

- `Issue_RejectsAlreadyIssued_IdempotentReturn` (returns Ok with existing JE id).
- `Issue_RejectsDraftWithNoLines` → `NoLines`.
- `Issue_RejectsInvalidArAccount` (e.g. Expense type) → `InvalidArAccount`.
- `Issue_RejectsInvalidIncomeAccount` (e.g. Asset type) → `InvalidIncomeAccount`.
- `Issue_HappyPath_PostsBalancedJE_AndTransitionsToIssued`.
- `Issue_AssignsInvoiceNumber_PerNumberingService`.
- `Issue_AmountPaidUntouched_DueToZeroPaymentsAtIssue` (Balance == Total).
- `Issue_WithMultipleLines_GroupsByIncomeAccount` (3 lines, 2 distinct income accounts → JE has 2 credit lines + 1 AR debit + 0 tax).
- `Issue_WithTax_PostsTaxPayableCredit` (using a fake `ITaxCalculationService` returning $10 tax on a $100 line → JE: AR Dr $110, Income Cr $100, TaxPayable Cr $10).
- `Issue_EmitsInvoiceCreatedAndIssuedEvents`.
- `Issue_ExternalRefPropagatesToJE` (idempotency-importer compat).
- `Issue_RoundingBankerHalfEven_PreservedThroughJE` (one line with quantity 1.005, unitPrice $100 → amount $100.50 not $100.51).
- `Issue_Idempotent_OnDuplicateIssueCall_DoesNotDoubleEmitEvents`.

`tests/InvoiceVoidServiceTests.cs`:

- `Void_RejectsUnknownInvoice` → `UnknownInvoice`.
- `Void_RejectsDraft` → `NotIssuedOrPartiallyPaid`.
- `Void_RejectsAlreadyVoided` → `AlreadyVoided`.
- `Void_HappyPath_PostsReversalJE_AndTransitionsToVoided`.
- `Void_PreservesOriginalJE` (immutable; reversal is a NEW entry).
- `Void_EmitsInvoiceVoidedEvent`.

`tests/InvoiceWriteOffServiceTests.cs`:

- `WriteOff_RejectsUnknownInvoice`.
- `WriteOff_RejectsNotOpen` (Voided / WrittenOff already terminal).
- `WriteOff_RejectsInvalidBadDebtAccount` (e.g. Asset type instead of Expense).
- `WriteOff_HappyPath_PostsBadDebtJE_AndTransitionsToWrittenOff`.
- `WriteOff_ForcesBalanceToZero` (regardless of prior AmountPaid).
- `WriteOff_EmitsInvoiceWrittenOffEvent`.

Total new tests this PR: ~25.

#### Verification

- `dotnet build` succeeds.
- All PR 1 + PR 2 tests pass.
- New tests pass.
- The JE posting integrates cleanly with `IJournalPostingService` from `blocks-financial-ledger` (no duplication of double-entry validation logic — that's the ledger's job).
- Spot-check: a synthetic 3-line invoice with $0 tax produces a JE with exactly 4 lines (1 AR debit + up to 3 revenue credits depending on income-account grouping).

#### Do NOT in this PR

- Do NOT implement payment application. That's the predecessor for `Paid` / `PartiallyPaid` transitions and ships with `blocks-financial-payments`. In PR 3, manually setting `Invoice.AmountPaid` (via `IInvoiceRepository.UpsertAsync(...)` with `Status = PartiallyPaid`) is the test seam for AR aging tests in PR 4.
- Do NOT implement period gating directly. The ledger's `IJournalPostingService.PostAsync(...)` enforces period-gating via `IPeriodResolver`; PR 3 inherits that automatically.
- Do NOT mutate posted invoice headers. Status transitions are allowed; field mutations (description, customerId, etc.) on a non-Draft invoice are blocked at the repository level. Add this check as a write-time validation when `inv.Status != Draft`.

---

### PR 4 — `IArAgingService` — bucket algorithm + per-customer + per-property breakdowns

**Estimated effort:** ~2h
**Scope:** AR aging algorithm per Stage 02 §6.2 + §5.2; bucket invoices by `asOf - dueDate`; per-customer and per-property aggregations; customer-statement query support
**Commit subject:** `feat(blocks-financial-ar): IArAgingService + customer-statement query per Stage 02 §6.2`
**Depends on:** PR 3 merged (independent of PR 5+6; can parallelize with PR 5)
**Branch:** `cob/blocks-financial-ar-aging`

#### New service

**`Services/IArAgingService.cs`**:

```csharp
public interface IArAgingService
{
    Task<AgingReport> ComputeAgingAsync(
        ChartOfAccountsId chartId,
        DateOnly asOf,
        AgingGroupBy groupBy = AgingGroupBy.Customer,
        CancellationToken cancellationToken = default);
}

public enum AgingGroupBy { Customer, Property, None }

public sealed record AgingReport(
    DateOnly AsOf,
    AgingGroupBy GroupBy,
    IReadOnlyList<AgingRow> Rows,
    AgingTotals Totals);

public sealed record AgingRow(
    string GroupKey,                    // PartyId.Value or PropertyId or "All"
    decimal Current,
    decimal Days0to30,
    decimal Days31to60,
    decimal Days61to90,
    decimal Days90Plus,
    decimal TotalOpen);

public sealed record AgingTotals(
    decimal Current,
    decimal Days0to30,
    decimal Days31to60,
    decimal Days61to90,
    decimal Days90Plus,
    decimal TotalOpen);
```

#### Algorithm per Stage 02 §6.2

```text
agingReport(chartId, asOf, groupBy):
  openInvoices = await repo.QueryOpenAsync(chartId, customerId: null, propertyId: null)
  // Filter: balance > 0 AND status in {Issued, PartiallyPaid}
  openInvoices = openInvoices.Where(inv => inv.Balance > 0)

  rowsByKey = new Dictionary<string, AgingAccumulator>()
  for inv in openInvoices:
      daysOverdue = (asOf.DayNumber - inv.DueDate.DayNumber)

      bucket = daysOverdue switch {
          <= 0     => "current",
          <= 30    => "0-30",
          <= 60    => "31-60",
          <= 90    => "61-90",
          _        => "90+",
      }

      key = groupBy switch {
          Customer => inv.CustomerId.Value,
          Property => inv.PropertyId ?? "Unassigned",
          None     => "All",
      }

      rowsByKey[key].Add(bucket, inv.Balance)

  rows = rowsByKey.Select(kv => kv.ToAgingRow()).OrderBy(r => r.GroupKey).ToList()
  totals = rows.Aggregate(AgingTotals.Zero, AgingTotals.Combine)
  return new AgingReport(asOf, groupBy, rows, totals)
```

**Notes per Stage 02 §6.2:**
- `asOf` is parameterizable (not always `today`) — supports historical aging ("aging as of 2025-12-31").
- Partial payments reduce `Invoice.Balance` but don't change `Invoice.DueDate`; aging is based on the original due date.
- `Overdue` is not a queried status — it's `Issued` or `PartiallyPaid` with `asOf > DueDate` and `Balance > 0`. The repository's `QueryOpenAsync` returns the `Issued + PartiallyPaid` set; the bucket math derives overdue from that.

#### Customer-statement query

**Extend `IInvoiceRepository` (already declared in PR 1; ship the in-memory impl in PR 4):**

```csharp
// In IInvoiceRepository (declared PR 1; impl PR 4):
Task<IReadOnlyList<Invoice>> QueryStatementAsync(
    ChartOfAccountsId chartId,
    PartyId customerId,
    DateOnly fromDate,
    DateOnly toDate,
    CancellationToken ct = default);
```

`InMemoryInvoiceRepository.QueryStatementAsync` returns all invoices for the given customer where `inv.IssueDate >= fromDate AND inv.IssueDate <= toDate`, plus any open invoices (`Issued | PartiallyPaid` with `balance > 0`) that have an issue date before `fromDate` (so a brought-forward balance can be computed). The renderer (in `blocks-reports-*`) does the rest.

#### Implementation

**`Services/ArAgingService.cs`**:

```csharp
public sealed class ArAgingService : IArAgingService
{
    private readonly IInvoiceRepository _repo;

    public ArAgingService(IInvoiceRepository repo)
    {
        _repo = repo;
    }

    public async Task<AgingReport> ComputeAgingAsync(
        ChartOfAccountsId chartId, DateOnly asOf,
        AgingGroupBy groupBy = AgingGroupBy.Customer, CancellationToken ct = default)
    {
        var open = await _repo.QueryOpenAsync(chartId, customerId: null, propertyId: null, ct);
        var withBalance = open.Where(i => i.Balance > 0m);
        var groups = withBalance
            .GroupBy(i => groupBy switch
            {
                AgingGroupBy.Customer => i.CustomerId.Value,
                AgingGroupBy.Property => i.PropertyId ?? "Unassigned",
                _                     => "All",
            });

        var rows = groups
            .Select(g =>
            {
                var acc = new AgingAccumulator();
                foreach (var inv in g)
                {
                    var days = asOf.DayNumber - inv.DueDate.DayNumber;
                    acc.Add(days, inv.Balance);
                }
                return acc.ToRow(g.Key);
            })
            .OrderBy(r => r.GroupKey, StringComparer.Ordinal)
            .ToList();

        var totals = rows.Aggregate(
            new AgingTotals(0, 0, 0, 0, 0, 0),
            (acc, r) => new AgingTotals(
                acc.Current + r.Current,
                acc.Days0to30 + r.Days0to30,
                acc.Days31to60 + r.Days31to60,
                acc.Days61to90 + r.Days61to90,
                acc.Days90Plus + r.Days90Plus,
                acc.TotalOpen + r.TotalOpen));

        return new AgingReport(asOf, groupBy, rows, totals);
    }
}
```

The `AgingAccumulator` is a private mutable helper that buckets by days-overdue and exposes `ToRow(string groupKey)`.

#### DI registration

Extend `ServiceCollectionExtensions.AddBlocksFinancialAr`:

```csharp
services.AddSingleton<IArAgingService, ArAgingService>();
```

#### Tests (PR 4)

`tests/ArAgingServiceTests.cs`:

- `Aging_EmptyChart_ProducesEmptyRowsAndZeroTotals`.
- `Aging_CurrentInvoice_BucketedAsCurrent` (dueDate > asOf, daysOverdue <= 0).
- `Aging_OneDayLate_BucketedAs0to30`.
- `Aging_ThirtyDaysLate_BucketedAs0to30` (boundary; 30 days overdue stays in 0-30 bucket).
- `Aging_ThirtyOneDaysLate_BucketedAs31to60`.
- `Aging_SixtyOneDaysLate_BucketedAs61to90`.
- `Aging_NinetyOneDaysLate_BucketedAs90Plus`.
- `Aging_OneTwentyDaysLate_BucketedAs90Plus`.
- `Aging_GroupByCustomer_AggregatesAcrossInvoices` (2 invoices for customer X → 1 row).
- `Aging_GroupByProperty_AggregatesAcrossCustomers` (3 invoices on property P → 1 row).
- `Aging_GroupByNone_AggregatesAllToSingleRow`.
- `Aging_NullPropertyId_BucketsAsUnassigned`.
- `Aging_PartiallyPaidInvoice_BalanceReflectedNotTotal` (balance == 200 not total == 1000).
- `Aging_PaidInvoice_NotIncludedEvenIfPastDueDate` (terminal status excluded).
- `Aging_VoidedInvoice_NotIncluded`.
- `Aging_WrittenOffInvoice_NotIncluded`.
- `Aging_DraftInvoice_NotIncluded` (status filter).
- `Aging_HistoricalAsOf_UsesParameterizedDate` (asOf = 2026-01-01 even though "today" is later; invoice due 2025-12-31 with balance > 0 → 0-30 bucket).
- `Aging_TotalsRow_EqualsSumOfGroupedRows`.

`tests/InvoiceQueryStatementTests.cs`:

- `QueryStatement_ReturnsCustomerInvoicesInDateRange`.
- `QueryStatement_IncludesOpenInvoicesBeforeFromDate` (for brought-forward balance).
- `QueryStatement_ExcludesOtherCustomers`.
- `QueryStatement_OrdersByIssueDate`.

Total new tests this PR: ~22.

#### Verification

- `dotnet build` succeeds.
- All PR 1-3 tests pass.
- New tests pass.
- Performance acceptance: aging on 1,000 open invoices completes in < 200ms on a developer laptop (NOT the Stage 02 §13 Surface Pro 7 acceptance target — that's a cluster-level acceptance for Phase 1 close-out). Include a microbench test that generates 1,000 synthetic invoices and asserts the call returns in < 500ms locally (CI tolerance).

#### Do NOT in this PR

- Do NOT introduce a balance-cache table (per Stage 02 §9: "A balance-cache table is optional and rebuildable from primary data. Recommend not introducing it until query profiling shows AR/AP report latency > 200ms on the Surface Pro 7 target."). Direct query is acceptable for v1.
- Do NOT introduce SQLite-side indexes. SQLite persistence is out of scope for v1.
- Do NOT add a "last payment date" toggle. Per Stage 02 §6.2: "User-facing toggle for 'use last payment date' is a config option; not v1."

---

### PR 5 — `blocks-rent-collection` wrapper retrofit (non-breaking)

**Estimated effort:** ~2–3h
**Scope:** retrofit `Sunfish.Blocks.RentCollection.InMemoryRentCollectionService.IssueInvoice(...)` to delegate to `IInvoicePostingService.IssueAsync(...)` on the canonical AR Invoice; preserve the existing `IRentCollectionService` API surface; introduce a `RentCollectionToFinancialArAdapter` that materializes the rent-collection `Invoice` projection from the canonical AR `Invoice`; preserve every existing rent-collection test (zero behavior regression)
**Commit subject:** `feat(blocks-rent-collection): wrap canonical financial-AR Invoice per ratification ruling Decision 3 (non-breaking)`
**Depends on:** PR 4 merged (or parallel with PR 4 if the adapter's only AR dependency is `IInvoicePostingService`)
**Branch:** `cob/blocks-rent-collection-wrap-ar`

#### Pattern overview

Per the 2026-05-16 ratification ruling Decision 3, the existing `blocks-rent-collection.Invoice` becomes a typed-specialization wrapper. The pattern:

```
                          ┌──────────────────────────────────────┐
External consumer code    │ IRentCollectionService               │
                          │  .IssueInvoice(rentSchedule, period) │
                          │  .GetInvoice(id)                     │
                          │  .ListInvoices(...)                  │  ← unchanged
                          └──────────────────────────────────────┘
                                            │
                                            ▼
                          ┌──────────────────────────────────────┐
Wrapper layer (NEW PR 5)  │ RentCollectionToFinancialArAdapter   │
                          │  .CanonicalToProjection(arInv) →     │
                          │      RentCollection.Invoice          │
                          │  .DelegateIssue(period) →            │
                          │      IInvoicePostingService.Issue    │
                          └──────────────────────────────────────┘
                                            │
                                            ▼
                          ┌──────────────────────────────────────┐
Canonical (PR 1-4)        │ blocks-financial-ar                  │
                          │   Invoice (canonical)                │
                          │   IInvoicePostingService             │
                          └──────────────────────────────────────┘
```

External consumers continue to call `IRentCollectionService.IssueInvoice(...)`. The implementation now constructs a canonical `Invoice` (with `InvoiceStatus = Draft`), calls `IInvoicePostingService.IssueAsync(...)`, and projects the result back into a `RentCollection.Invoice` (the legacy shape). The legacy shape's `Id` (`InvoiceId`) is held identical to the canonical `Invoice.Id`'s ULID stringified.

#### Non-breaking constraints

1. **`Sunfish.Blocks.RentCollection.Models.Invoice` record signature stays identical** (positional record params unchanged).
2. **`Sunfish.Blocks.RentCollection.Models.InvoiceStatus` enum values stay identical** (`Draft`, `Open`, `PartiallyPaid`, `Paid`, `Overdue`, `Cancelled`). Maps onto canonical states:
   - `Draft` ↔ `InvoiceStatus.Draft`
   - `Open` ↔ `InvoiceStatus.Issued`
   - `PartiallyPaid` ↔ `InvoiceStatus.PartiallyPaid`
   - `Paid` ↔ `InvoiceStatus.Paid`
   - `Cancelled` ↔ `InvoiceStatus.Voided` (mapped; original `Cancelled` is preserved)
   - `Overdue` → derived (computed at projection time via `inv.IsOverdue(asOf: today)`)
3. **`IRentCollectionService` interface stays identical** (no method-signature changes; no new methods).
4. **`InMemoryRentCollectionService` is renamed internally to delegate**, but the public class name is preserved.
5. **All existing tests in `packages/blocks-rent-collection/tests/` pass unchanged.**

#### New types in `blocks-rent-collection`

**`packages/blocks-rent-collection/Adapters/RentCollectionToFinancialArAdapter.cs`**:

```csharp
internal sealed class RentCollectionToFinancialArAdapter
{
    private readonly IInvoicePostingService _arPosting;
    private readonly IInvoiceRepository _arRepo;
    private readonly IAccountResolver _accountResolver;
    private readonly RentCollectionAdapterOptions _options;

    public RentCollectionToFinancialArAdapter(
        IInvoicePostingService arPosting,
        IInvoiceRepository arRepo,
        IAccountResolver accountResolver,
        RentCollectionAdapterOptions options)
    { _arPosting = arPosting; _arRepo = arRepo; _accountResolver = accountResolver; _options = options; }

    /// <summary>
    /// Issues a rent invoice through the canonical financial-AR posting service.
    /// Returns a rent-collection-shaped Invoice projection for legacy consumers.
    /// </summary>
    public async Task<RentCollection.Models.Invoice> IssueRentInvoiceAsync(
        RentSchedule schedule, BillingPeriod period, CancellationToken ct = default)
    {
        var draft = BuildCanonicalDraft(schedule, period);
        await _arRepo.UpsertAsync(draft, ct);

        var result = await _arPosting.IssueAsync(draft.Id, ct);
        if (result.Error != IssueError.None)
            throw new InvalidOperationException(
                $"Failed to issue canonical AR invoice for rent schedule {schedule.Id}: {result.Error} {result.Detail}");

        return ProjectToLegacy(result.Invoice!);
    }

    public async Task<RentCollection.Models.Invoice?> GetByLegacyIdAsync(
        RentCollection.Models.InvoiceId legacyId, CancellationToken ct = default)
    {
        var canonical = await _arRepo.GetByIdAsync(new InvoiceId(legacyId.Value), ct);
        return canonical is null ? null : ProjectToLegacy(canonical);
    }

    private Invoice BuildCanonicalDraft(RentSchedule schedule, BillingPeriod period)
    {
        // Generate ULID; map fields from rent-schedule.
        var id = InvoiceId.New();
        return new Invoice
        {
            Id = id,
            ChartId = _options.ChartId,
            CustomerId = new PartyId(schedule.LeaseId),    // best-effort; W#23 cleaner lookup later
            PropertyId = schedule.PropertyId,              // if available
            IssueDate = period.IssueDate,
            DueDate = period.DueDate,
            Currency = "USD",
            ArAccountId = _options.ArAccountId,
            Lines = new[]
            {
                new InvoiceLine
                {
                    Id = InvoiceLineId.New(),
                    InvoiceId = id,
                    LineNumber = 1,
                    Description = $"Rent for {period.PeriodStart:yyyy-MM-dd} – {period.PeriodEnd:yyyy-MM-dd}",
                    Quantity = 1m,
                    UnitPrice = period.AmountDue,
                    Amount = period.AmountDue,
                    IncomeAccountId = _options.RentalIncomeAccountId,
                },
            },
            Status = InvoiceStatus.Draft,
            CreatedAtUtc = SystemClock.Instance.GetCurrentInstant(),
            UpdatedAtUtc = SystemClock.Instance.GetCurrentInstant(),
        };
    }

    private RentCollection.Models.Invoice ProjectToLegacy(Invoice canonical)
    {
        var legacyStatus = canonical.Status switch
        {
            InvoiceStatus.Draft         => RentCollection.Models.InvoiceStatus.Draft,
            InvoiceStatus.Issued        => RentCollection.Models.InvoiceStatus.Open,
            InvoiceStatus.PartiallyPaid => RentCollection.Models.InvoiceStatus.PartiallyPaid,
            InvoiceStatus.Paid          => RentCollection.Models.InvoiceStatus.Paid,
            InvoiceStatus.Voided        => RentCollection.Models.InvoiceStatus.Cancelled,
            InvoiceStatus.WrittenOff    => RentCollection.Models.InvoiceStatus.Cancelled,
            _                           => RentCollection.Models.InvoiceStatus.Draft,
        };
        // If Open + past due + balance>0, project as Overdue:
        if (legacyStatus == RentCollection.Models.InvoiceStatus.Open
            && canonical.Balance > 0
            && DateOnly.FromDateTime(DateTime.UtcNow) > canonical.DueDate)
            legacyStatus = RentCollection.Models.InvoiceStatus.Overdue;

        return new RentCollection.Models.Invoice(
            Id: new RentCollection.Models.InvoiceId(canonical.Id.Value),
            ScheduleId: /* read from canonical externalRef or notes; v1: keep as part of draft state */,
            LeaseId: canonical.CustomerId.Value,
            PeriodStart: /* same; v1: read from canonical notes or extension */,
            PeriodEnd: /* same */,
            DueDate: canonical.DueDate,
            AmountDue: canonical.Total,
            AmountPaid: canonical.AmountPaid,
            Status: legacyStatus,
            GeneratedAtUtc: canonical.CreatedAtUtc);
    }
}

internal sealed class RentCollectionAdapterOptions
{
    public ChartOfAccountsId ChartId { get; init; }
    public GLAccountId ArAccountId { get; init; }
    public GLAccountId RentalIncomeAccountId { get; init; }
}
```

**Implementation note on field projection:** the `RentCollection.Invoice` carries `ScheduleId`, `LeaseId`, `PeriodStart`, `PeriodEnd` — fields the canonical `Invoice` does not. The wrapper preserves them in a side-table (`Models/RentInvoiceExtension.cs`) keyed by `InvoiceId`:

```csharp
internal sealed record RentInvoiceExtension(
    InvoiceId CanonicalId,
    RentScheduleId ScheduleId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd);
```

And an `InMemoryRentInvoiceExtensionStore` holds the `ConcurrentDictionary<InvoiceId, RentInvoiceExtension>`. The adapter writes the extension on draft-build, reads it on projection.

#### `InMemoryRentCollectionService` refactor

The existing `InMemoryRentCollectionService.IssueInvoice(...)` is refactored to delegate to the adapter:

```csharp
public async Task<Invoice> IssueInvoiceAsync(
    RentScheduleId scheduleId, BillingPeriod period, CancellationToken ct = default)
{
    var schedule = await _scheduleRepo.GetByIdAsync(scheduleId, ct)
        ?? throw new InvalidOperationException(
            $"RentSchedule {scheduleId.Value} not found");
    return await _adapter.IssueRentInvoiceAsync(schedule, period, ct);
}
```

All other `IRentCollectionService` methods (`GetInvoice`, `ListInvoices`, `RecordPayment`) similarly route through the adapter — `GetInvoice` calls `_adapter.GetByLegacyIdAsync`; `ListInvoices` calls the AR repo's open-invoice query and projects each result; `RecordPayment` is **deferred** (payment-application is `blocks-financial-payments` scope; in this PR, `RecordPayment` writes `Invoice.AmountPaid` directly via `IInvoiceRepository.UpsertAsync` and recomputes `Balance` + `Status`).

#### DI extension update

`packages/blocks-rent-collection/DependencyInjection/RentCollectionServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddBlocksRentCollection(
    this IServiceCollection services,
    Action<RentCollectionAdapterOptions>? configure = null)
{
    // Existing registrations remain:
    services.AddSingleton<IRentScheduleRepository, InMemoryRentScheduleRepository>();
    services.AddSingleton<ILateFeePolicyRepository, InMemoryLateFeePolicyRepository>();

    // New: register the adapter + bridge to financial-AR.
    var options = new RentCollectionAdapterOptions();
    configure?.Invoke(options);
    services.AddSingleton(options);
    services.AddSingleton<RentCollectionToFinancialArAdapter>();
    services.AddSingleton<IRentCollectionService, InMemoryRentCollectionService>();

    return services;
}
```

The consumer's `AddBlocksFinancialAr(...)` must be called **before** `AddBlocksRentCollection(...)` in the host's DI setup. Document this in the package README + add a startup check that throws a helpful exception if `IInvoicePostingService` is not registered.

#### Tests (PR 5)

`packages/blocks-rent-collection/tests/RentCollectionAdapterTests.cs` (new):

- `IssueInvoice_DelegatesToFinancialArPosting_AndProjectsLegacyShape`.
- `IssueInvoice_PreservesScheduleId_ViaExtensionStore` (read back via `GetInvoice`).
- `IssueInvoice_LegacyStatusMapping_OpenWhenFinancialArIsIssued`.
- `GetInvoice_PaidLegacyStatus_WhenCanonicalIsPaid`.
- `GetInvoice_OverdueLegacyStatus_DerivedWhenIssuedAndPastDue`.
- `GetInvoice_CancelledLegacyStatus_WhenCanonicalIsVoided`.
- `ListInvoices_ReturnsProjectedRows`.
- `RecordPayment_UpdatesCanonicalAmountPaid_AndProjectsLegacyShape`.

`packages/blocks-rent-collection/tests/RentLedgerBlockTests.cs` (existing): all pass unchanged. **Critical halt condition:** if any existing rent-collection test fails, **STOP** and file `cob-question-*` — the wrapper must be non-breaking.

`packages/blocks-rent-collection/tests/RentCollectionServiceTests.cs` (existing): all pass unchanged.

Total new tests this PR: ~8 (plus zero regression in ~existing tests).

#### Verification

- `dotnet build` succeeds across the whole solution.
- All existing rent-collection tests pass (zero behavior regression).
- New adapter tests pass.
- A consumer-host smoke test: `AddBlocksFinancialAr(opts => { opts.LocalReplicaId = new("CW"); })` + `AddBlocksRentCollection(opts => { /* chart + AR account + rental income account */ })` builds without runtime DI errors, and a synthetic rent-issue → fetch round-trips correctly.
- `grep -r "Sunfish.Blocks.RentCollection.Models.Invoice" packages/ apps/ accelerators/` — verify all existing consumers compile against the unchanged record type.

#### Do NOT in this PR

- Do NOT add new public methods to `IRentCollectionService`. The interface remains identical.
- Do NOT delete `Sunfish.Blocks.RentCollection.Models.Invoice` or `InvoiceStatus`. These remain the legacy projection shape.
- Do NOT change `Sunfish.Blocks.RentCollection.Models.Invoice` record signature (positional params). Adding optional fields is allowed only if every existing positional caller still compiles.

---

### PR 6 — ERPNext sales-invoice importer integration

**Estimated effort:** ~2h
**Scope:** add `IErpnextSalesInvoiceImporter` consumable by the importer orchestrator; idempotent upsert on `(source, externalRefId)`; maps ERPNext `Sales Invoice` + `Sales Invoice Item` → canonical AR `Invoice` + `InvoiceLine[]`; delegates posting to `IInvoicePostingService`
**Commit subject:** `feat(blocks-financial-ar): IErpnextSalesInvoiceImporter — Pass 2 sales-invoice upsert + post`
**Depends on:** PR 5 merged
**Branch:** `cob/blocks-financial-ar-erpnext-importer`

#### New types

**`Migration/IErpnextSalesInvoiceImporter.cs`**:

```csharp
public interface IErpnextSalesInvoiceImporter
{
    /// <summary>
    /// Upserts a sales invoice from an ERPNext source record. Idempotent on
    /// (source, externalRefId). Issues a canonical AR Invoice via the
    /// IInvoicePostingService; opening-balance / migration-tagged invoices
    /// bypass the entry-date-must-be-on-or-before-today check via the
    /// posting service's Migration source kind.
    /// </summary>
    Task<ImportOutcome<Invoice>> UpsertFromErpnextAsync(
        ErpnextSalesInvoiceSource source,
        ChartOfAccountsId targetChart,
        CancellationToken cancellationToken = default);
}

public sealed record ErpnextSalesInvoiceSource(
    string Name,                       // ERPNext "name" — stable id
    string Modified,                   // ERPNext "modified" — version key
    DateOnly PostingDate,
    DateOnly DueDate,
    string CustomerName,               // ERPNext customer reference; resolve via IPartyReadModel
    string? CostCenter,                // ERPNext cost-center; mapped to PropertyId or ClassificationId
    string Status,                     // ERPNext "status" — Draft/Submitted/Paid/Cancelled
    int DocStatus,                     // 0=Draft, 1=Submitted, 2=Cancelled
    bool IsReturn,                     // credit-memo flag; treated as void-of-prior-invoice
    decimal GrandTotal,                // for crosscheck
    string Currency,
    IReadOnlyList<ErpnextSalesInvoiceItemSource> Items);

public sealed record ErpnextSalesInvoiceItemSource(
    string ItemCode,                   // ERPNext item ref; mapped to description
    string Description,
    decimal Qty,
    decimal Rate,                      // unit price
    decimal Amount,                    // qty * rate
    string IncomeAccountName,          // ERPNext income account; resolve via IAccountResolver.GetByExternalRefAsync
    string? CostCenter,
    string? ItemTaxTemplate);          // raw ERPNext tax template ref; v1 ignored (NoOp tax)
```

#### Implementation

**`Migration/ErpnextSalesInvoiceImporter.cs`** — wraps `IInvoicePostingService.IssueAsync(...)` with the §10.2 enum-mapping table from the migration-importer spec. Per-record flow:

```text
upsertFromErpnext(source, targetChart):
  // 1. Idempotency check
  existing = await arRepo.GetByExternalRefAsync(source: "erpnext", externalRefId: source.Name)
  if existing is not null:
    if existing.Status >= Issued: return Skipped (posted invoices immutable)
    if existing.UpdatedAtUtc.ToString() == source.Modified: return Skipped (same version)
    // else: existing is Draft + version moved forward → re-build + post

  // 2. Resolve customer (via party model convention: read-only IPartyReadModel)
  partyId = await partyReadModel.FindPartyByExactNameAsync(source.CustomerName)
            ?? throw new InvalidOperationException($"Unknown customer: {source.CustomerName}")

  // 3. Resolve AR account from chart (default AR account; or per-customer override)
  arAccountId = await chartConfig.GetDefaultArAccountAsync(targetChart)

  // 4. Build canonical Invoice (Draft)
  inv = new Invoice {
      Id = existing?.Id ?? InvoiceId.New(),
      ChartId = targetChart,
      CustomerId = partyId,
      PropertyId = source.CostCenter,          // best-effort cost-center map
      IssueDate = source.PostingDate,
      DueDate = source.DueDate,
      Currency = source.Currency,
      ArAccountId = arAccountId,
      Lines = source.Items.Select((item, idx) => MapItem(item, targetChart, idx + 1)).ToArray(),
      Status = InvoiceStatus.Draft,
      ExternalRef = source.Name,
      Notes = $"Imported from ERPNext sales-invoice {source.Name}",
      CreatedAtUtc = systemClock.GetCurrentInstant(),
      UpdatedAtUtc = systemClock.GetCurrentInstant(),
  }
  await arRepo.UpsertAsync(inv)

  // 5. Issue via posting service (idempotent if already issued)
  if source.DocStatus == 1: // Submitted in ERPNext
    issueResult = await arPosting.IssueAsync(inv.Id)
    if issueResult.Error != None: return Skipped + log warning (do not throw)

  // 6. Handle Cancelled (DocStatus==2) — void the canonical invoice
  if source.DocStatus == 2 && inv.Status == Issued:
    voidResult = await arPosting.VoidAsync(inv.Id, reason: "ERPNext source cancelled", by: system)
    if voidResult.Error != None: log warning

  return Inserted-or-Updated (with the resolved Invoice)
```

#### Crosscheck (post-issue)

Per the migration-importer spec §10.4, the importer verifies `inv.Total == source.GrandTotal ± $0.01`. If not, log a `RowImportWarning` (no throw — soft failure) and proceed.

#### Customer-name lookup stub

For the importer to resolve `CustomerName → PartyId`, this PR ships a local stub `IPartyReadModel` until `blocks-people-*` ships:

```csharp
public interface IPartyReadModel  // local; relocates to blocks-people-* when shipped
{
    Task<PartyId?> FindPartyByExactNameAsync(string displayName, CancellationToken ct = default);
    Task<string?> GetDisplayNameAsync(PartyId id, CancellationToken ct = default);
}

public sealed class InMemoryPartyReadModel : IPartyReadModel
{
    private readonly ConcurrentDictionary<string, PartyId> _byName = new(StringComparer.OrdinalIgnoreCase);
    // ...
}
```

When `blocks-people-*` lands, this interface relocates; the importer's namespace import changes.

#### DI registration

Extend `ServiceCollectionExtensions.AddBlocksFinancialAr`:

```csharp
services.AddSingleton<IPartyReadModel, InMemoryPartyReadModel>();
services.AddSingleton<IErpnextSalesInvoiceImporter, ErpnextSalesInvoiceImporter>();
```

#### Tests (PR 6)

`tests/ErpnextSalesInvoiceImporterTests.cs`:

- `Upsert_NewSubmittedSource_InsertsAndIssuesInvoice`.
- `Upsert_NewDraftSource_InsertsDraftNoIssue` (DocStatus==0 stays Draft).
- `Upsert_NewCancelledSource_InsertsAndVoidsImmediately`.
- `Upsert_DuplicateSubmittedSource_ReturnsSkipped` (idempotency).
- `Upsert_HigherVersionDraftSource_RebuildsAndUpsertsDraft`.
- `Upsert_HigherVersionSubmittedSource_OnAlreadyIssuedInvoice_ReturnsSkipped` (canonical issued is terminal-ish).
- `Upsert_UnknownCustomerName_ThrowsHelpfulError`.
- `Upsert_GrandTotalMismatch_LogsWarningButProceeds`.
- `Upsert_MapsCostCenterToPropertyId`.
- `Upsert_LineCount_MatchesSourceItems`.
- `Upsert_PreservesPostingDate_EvenIfBackdated` (per §10.4 — migration entries are allowed to be backdated when the posting service receives sourceKind=Invoice; the JE's entry-date is the invoice's IssueDate, which is the source's PostingDate).

Total new tests this PR: ~11.

#### Verification

- `dotnet build` succeeds.
- All PR 1-5 tests pass.
- New tests pass.
- Integration smoke: feed a small synthetic ERPNext export (1 customer, 2 sales invoices) → both invoices import + post; AR aging on the resulting chart shows the expected bucket count.
- The `apps/docs/blocks-financial-ar/overview.md` page (added in PR 5 or PR 6 — see §Docs) references the importer surface.

#### Do NOT in this PR

- Do NOT introduce the importer orchestrator (the 6-pass driver). That lives in `tooling-anchor-import` (separate hand-off). This PR ships the integration point.
- Do NOT couple to `blocks-financial-tax` for tax-template mapping. v1 ignores ERPNext tax templates and uses the NoOp tax calculator. When `blocks-financial-tax` lands, a follow-on adds the mapping.

---

## CRDT-friendly schema conventions applied

This hand-off applies the cluster's CRDT-friendly conventions. Cross-referenced summary:

### 1. Posted-then-immutable Invoice

Per `crdt-friendly-schema-conventions.md` §6: once `Invoice.Status` transitions out of `Draft`, the header's substantive fields (customerId, lines, totals, arAccountId, issueDate, dueDate, propertyId, currency) are **immutable**. Allowed mutations on a non-Draft invoice:

- Status transitions per the `InvoiceStatusTransitions` table.
- `AmountPaid` (updated by payment application — `blocks-financial-payments` scope; this hand-off treats `AmountPaid` as a writable field via the repository for test-seam purposes).
- `Balance` (derived from `Total - AmountPaid`).
- `VoidedByEntryId`, `WrittenOffByEntryId` (set during state transition).
- `UpdatedAtUtc`, `UpdatedBy`, `Version`.
- `Notes` (append-only — never truncate; only concatenate; preserves the audit trail per §4).

The repository's `UpsertAsync` enforces this at Tier-1 by comparing the incoming row against the existing row and rejecting unauthorized field mutations.

### 2. Draft-stage mutability

Per `crdt-friendly-schema-conventions.md` §6 ("Draft-stage mutability"): drafts are mutable. They are typed as scratch space not yet committed to the AR ledger. In the Loro CRDT layer (when foundation-localfirst lands), drafts are expected to be peer-local and not synced (the synchronization boundary is the `Draft → Issued` transition). Draft-side conflicts cannot arise; the user's local draft is the only draft for that ULID.

### 3. ExternalRef as the idempotency key

Every persisted invoice carries `ExternalRef` (optional; populated by the importer or other system integrations). The `(externalRef.source, externalRef.id)` tuple is the idempotency key. Re-import = look-up + skip-or-update. Applied verbatim from the ledger pattern.

### 4. Monotonic invoice numbering — per-replica-suffix scheme

Per `crdt-friendly-schema-conventions.md` §1 / §8 (binding). Format: `INV-YYYY-MM-DD-{ReplicaId}-{seq:D4}`. Detailed in PR 2.

**Open question Q10 visual ordering across replicas — partially resolved:** the format itself is the resolution — numbers are not globally monotonic but are deterministically sortable across replicas via `(createdAt, replicaId, sequence)`. The UX trade-off is accepted: `INV-2026-05-16-CW-0124` is uglier than `INV-00124`, but the alternatives (renumber-after-sync, gap-filled global numbering, conflict-UI) are worse. See §Halt-conditions #5 for the UX-acceptability halt.

### 5. State-machine-aware merge — Pattern B (terminal-wins)

Per `crdt-friendly-schema-conventions.md` §7 cluster table:

> `blocks-financial-*` Invoice lifecycle — B with terminal-wins (`Voided` / `WrittenOff` > `Paid` > `Issued`)

Implementation: `InvoiceStatusResolver` (ships in PR 3 alongside the posting service) registers with kernel-crdt's conflict-resolver registry. When two replicas concurrently transition the same Invoice, the resolver picks:

1. Terminal states win (`Voided` and `WrittenOff` are absolute terminals).
2. Among terminals, the **first-recorded** wins (Loro version-vector ordering).
3. Non-terminal upgrades win over downgrades: `PartiallyPaid → Paid` (forward) beats `Paid → PartiallyPaid` (a payment-bounce).
4. If neither side is terminal AND the transition is symmetric (`Issued → PartiallyPaid` vs `Issued → Paid` both applied), `Paid` wins (greater state).

The resolver's deterministic decision matrix is unit-tested in PR 3 against a 6×6 state-pair table.

### 6. Tier-2 post-merge validation — payment over-application

Per `crdt-friendly-schema-conventions.md` §10:

> Sum of payment applications ≤ invoice balance — `blocks-financial-*` — Overflow flagged; over-application becomes a credit; emit `PaymentOverapplied` event.

The `IPostMergeReconciler` registration ships **as a stub** in PR 4 (because PaymentApplication is `blocks-financial-payments` scope). The stub:

- Subscribes to `Financial.PaymentApplied` events (defined in the cross-cluster event bus).
- On receipt, fetches the invoice; recomputes `sum(applications.amountApplied)`; if > `Total`, emits a `Financial.PaymentOverapplied` event (defined here; consumed by the future payments cluster).

This stub's wiring is verified by a single test in PR 4; full enforcement is delivered with `blocks-financial-payments`.

---

## Event-bus catalog applied

Per `cross-cluster-event-bus-design.md` §3.1, this hand-off emits and consumes:

### Emitted (producer: `financial`)

| Event | Consumer clusters | Payload | Idempotency key |
|---|---|---|---|
| `Financial.InvoiceCreated` | (none external; internal-only — used for activity-feed on customer) | `{ invoiceId, chartId, customerId }` | `invoice-created:{invoiceId}` |
| `Financial.InvoiceIssued` | people, reports, docs | `{ invoiceId, customerId, totalAmount, dueDate, propertyId? }` | `invoice-issued:{invoiceId}` |
| `Financial.InvoiceVoided` | people, reports | `{ invoiceId, reversalEntryId }` | `invoice-voided:{invoiceId}` |
| `Financial.InvoiceWrittenOff` | people, reports | `{ invoiceId, badDebtJEId }` | `invoice-writeoff:{invoiceId}` |
| `Financial.PaymentOverapplied` (post-merge reconciler) | people, reports | `{ invoiceId, totalApplied, invoiceTotal, deltaAmount }` | `payment-overapplied:{invoiceId}:{detectedAtUtc}` |

`Financial.InvoicePaid` is **NOT** emitted by this hand-off — it's emitted by the future `blocks-financial-payments` cluster when a payment application brings the balance to zero. The event-record type is declared here so consumers can subscribe in advance.

### Consumed

Currently none — this hand-off has no event-handler subscriptions of its own. The downstream consumers listed above subscribe to the events.

### Schema versioning

All event payloads ship at `schemaVersion: "1.0.0"`. Future additive fields → minor bump; renames or breaking changes → new event type per §2 deprecation rules. Renames are forbidden.

### Envelope construction

Each emitted event is wrapped in the canonical `DomainEventEnvelope<TPayload>` per `cross-cluster-event-bus-design.md` §1. The local `IInvoiceEventPublisher` stub ships a minimal envelope (just `eventId`, `eventType`, `occurredAt`, `tenantId`, `payload`); the full envelope is populated when the foundation event-bus package lands (per §10 Q1, package home is TBD).

---

## License posture

### Borrowed-with-attribution (permissive)

- **Apache OFBiz** `accounting/Invoice + InvoiceItem` entities (Apache 2.0). The `Invoice` + `InvoiceLine` field shapes (header/line decomposition; `arAccountId` + `incomeAccountId` linkage; `subtotal/taxTotal/total/amountPaid/balance` computed-cached fields; AR-aging-as-derived-state) derive from OFBiz's `invoice` doctype pattern per `blocks-financial-schema-design.md` §11.1.

**Attribution requirements:**

1. The package's `Sunfish.Blocks.FinancialAr.csproj` carries `<PropertyGroup><NOTICEFile>NOTICE.md</NOTICEFile></PropertyGroup>`.
2. **`packages/blocks-financial-ar/NOTICE.md`** (new file in PR 1):

```markdown
# NOTICE — Sunfish.Blocks.FinancialAr

This package's entity shapes (Invoice + InvoiceLine; AR-aging-as-derived-state;
header/line decomposition; computed-cached totals) derive from Apache OFBiz's
`accounting/Invoice + InvoiceItem` entity models
(<https://ofbiz.apache.org/>, Apache 2.0 license).

OFBiz version studied: v18.12.x (as of 2026-05-16).

The Sunfish implementation is original code, distributed under the
MIT License. The OFBiz entity-shape pattern is reproduced with
attribution per Apache 2.0 §4(c) of the OFBiz License.
```

3. Source-header comments on `Invoice.cs`, `InvoiceLine.cs`, `IArAgingService.cs` reference OFBiz in a one-line comment.

### Clean-room only (copyleft)

Per `blocks-financial-schema-design.md` §11.2–§11.5, these sources were studied for understanding only and contribute NO code:

- **ERPNext + Frappe** (GPLv3) — DocType structure of the migration source side; consumed as a **data format**, not code. The `ErpnextSalesInvoiceSource` record shape mirrors the ERPNext DocType field names (Name, Modified, PostingDate, CustomerName, etc.) — these are **data-format names** (ERPNext's external API surface), not borrowed code structure.
- **Akaunting** (GPLv3) — Read for understanding of small-business AR status vocabulary and invoice-numbering UX expectations. Akaunting's `Open` status maps to our `Issued`; their `Cancelled` ↔ our `Voided`. Vocabulary inspired-by; no code borrowed.
- **GnuCash** (GPLv2) — AR aging bucket conventions (current / 30 / 60 / 90+). The bucket boundaries are also published in **IRS Schedule E instructions** (public-domain) and **AICPA practice aids** (commercial, summarized); we cite IRS as the primary reference, GnuCash as inspired-by.
- **Beancount + ledger-cli** (GPLv2) — Textbook double-entry data model. The AR posting algorithm in PR 3 derives from the AR posting equation in the AICPA *Accounting Principles* (textbook) and is independent of Beancount's representation.
- **InvoiceNinja, Akaunting** (mixed) — Modern small-business invoicing patterns; status vocabulary; UX expectations. No code borrowed.

**Discipline check before merging any PR in this hand-off:**

1. No copyleft code was opened in any editor session that produced this hand-off's PRs.
2. No identifier names from any GPL/AGPL source appear in the new code. (Spot-check by grep before merge.)
3. The clean-room schema in `blocks-financial-schema-design.md` §3.5–§3.6 is the source of truth; deviations require XO ratification.

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088 §2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary; details under each PR above)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (scaffold + records + state machine) | ~16 | record fields; line rounding; status transitions; repository round-trip |
| PR 2 (numbering) | ~12 | format spec; monotonic sequencing; collision resolution; well-formedness |
| PR 3 (posting service) | ~25 | issue happy + every failure path; void; write-off; idempotency; event emission |
| PR 4 (AR aging + statement query) | ~22 | bucket boundaries; group-by-customer/property/none; historical asOf; status filter; statement query |
| PR 5 (rent-collection wrapper) | ~8 (+ zero regression on existing) | adapter projection; status mapping; extension store; non-breaking surface |
| PR 6 (ERPNext importer) | ~11 | upsert idempotency; cancel-path; customer lookup; total crosscheck; cost-center mapping |
| **Total** | **~94 new + ~25 existing rent-collection tests preserved** | |

### Cluster-level acceptance (PASS gate at end of PR 6)

**A1.** `dotnet build` succeeds across the new `Sunfish.Blocks.FinancialAr` package + every downstream consumer (including `Sunfish.Blocks.RentCollection` after retrofit).

**A2.** `dotnet test packages/blocks-financial-ar/tests/` passes all ~94 new tests; `dotnet test packages/blocks-rent-collection/tests/` passes all existing tests unchanged.

**A3.** An AR-issuance round-trip:
- Seed a chart via `IChartSeedingService.SeedChartAsync(...)` (from ledger hand-off PR 5).
- Construct a Draft Invoice with 2 lines (rent $1,200 + late fee $50; both credit OperatingIncome).
- Call `IInvoicePostingService.IssueAsync(...)`.
- Assert: `IssueResult.Error == None`; `Invoice.Status == Issued`; `Invoice.InvoiceNumber` matches `INV-YYYY-MM-DD-XX-NNNN`; a `JournalEntry` exists with `Status == Posted`, 3 lines (AR Dr $1250, Rental Income Cr $1200, Late Fee Income Cr $50), and balanced.

**A4.** A void round-trip:
- Issue an invoice (per A3).
- Call `IInvoicePostingService.VoidAsync(...)`.
- Assert: `Invoice.Status == Voided`; `VoidedByEntryId` populated; a reversal JE exists with flipped debits/credits; the original JE is unchanged (immutable per the ledger's posted-then-immutable invariant).

**A5.** A write-off round-trip:
- Issue an invoice for $500.
- Call `IInvoicePostingService.WriteOffAsync(..., badDebtAccountId)`.
- Assert: `Invoice.Status == WrittenOff`; `Invoice.Balance == 0`; a bad-debt JE exists with Dr BadDebt $500, Cr AR $500.

**A6.** AR aging report sanity:
- Seed 5 customers × 3 invoices each (15 invoices total) at staggered due dates (today-15, today-45, today-75, today-105, future+30).
- Mark 5 of them as `PartiallyPaid` (balance = total/2) via direct repository upsert.
- Call `IArAgingService.ComputeAgingAsync(asOf: today, groupBy: Customer)`.
- Assert: each customer has a row; totals row sums to expected total open balance; bucket assignments match expectation.

**A7.** Rent-collection wrapper non-breakage:
- Pick an existing `RentCollectionServiceTests` test (e.g. `IssueInvoice_GeneratesInvoiceForBillingPeriod`).
- Run it post-retrofit.
- Assert: passes byte-identical (same `RentCollection.Invoice` shape returned; same status enum value; same `AmountDue`; only `Id`'s underlying ULID changes — that's expected).

**A8.** ERPNext importer round-trip:
- Construct an `ErpnextSalesInvoiceSource` (1 customer, 2 line items, DocStatus=1=Submitted, GrandTotal $1,500).
- Pre-seed the customer in `InMemoryPartyReadModel`.
- Call `IErpnextSalesInvoiceImporter.UpsertFromErpnextAsync(...)`.
- Assert: `ImportAction == Inserted`; `Invoice.Status == Issued`; `Invoice.Total == 1500`; ExternalRef preserved.
- Call the importer again with the SAME source.
- Assert: `ImportAction == Skipped` (idempotency).

**A9.** Performance: aging on 1,000 open invoices completes in < 500ms locally (CI tolerance; Surface Pro 7 target of < 200ms is the Phase 1 close-out acceptance, not this hand-off's).

**A10.** Replica-collision detection: simulate two `InMemoryInvoiceNumberingService` instances (`CW` + `A4`), each issues 10 invoices on the same chart with overlapping dates; assert no duplicate numbers in either set.

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*` beacon to `coordination/inbox/`:

### 1. `ReplicaId` placement (PR 1)

If `Sunfish.Foundation.LocalFirst.ReplicaId` doesn't exist yet (likely the case — `foundation-localfirst` package shape on main is partial), ship the local placeholder in `Sunfish.Blocks.FinancialAr.Models.ReplicaId` with the TODO comment for relocation. **Do not block on this.** File `cob-question-2026-05-XXTHH-MMZ-w60-p4-replica-id-placement.md` ONLY if foundation-localfirst is in active design-in-flight and a hand-off authoring its ReplicaId is queued (in which case wait for that hand-off).

### 2. `IPartyReadModel` cross-cluster contract (PR 6)

If `blocks-people-*` package doesn't exist yet on main:

**Mitigation:** Ship the local stub `IPartyReadModel` interface + `InMemoryPartyReadModel` in this package. Tests use the in-memory stub. When `blocks-people-*` lands, the interface relocates via a single `using` directive update.

**Halt condition (file `cob-question-*`):** if `blocks-people-*` IS in design-in-flight AND the people-cluster hand-off is queued, wait for that hand-off to provide a stable `IPartyReadModel` contract first. Otherwise proceed with the local stub.

**Critical: DO NOT WRITE Party rows from this hand-off.** Per `party-model-convention.md` §4: financial-AR is the consuming side of the Party model; the people cluster owns Party CRUD. Even with a local stub, only the read API surface ships here.

### 3. `blocks-financial-ledger` not built yet

**Pre-build checklist step 1** catches this. If the sibling ledger hand-off hasn't shipped, **STOP** — file `cob-question-*` requesting ledger sequence-up. The ledger hand-off is the explicit predecessor.

### 4. `blocks-financial-tax` or `blocks-financial-periods` not built (PR 3)

Mitigated via stubs in PR 3:

- `NoOpTaxCalculationService` — returns zero tax. Acceptable for v1 single-jurisdiction USD demo.
- `InMemoryPeriodResolver` (ships with the ledger hand-off PR 4) — always returns Open period.

When `blocks-financial-tax` lands, a follow-on hand-off replaces `NoOpTaxCalculationService` with the real implementation; DI swap (no code change in `IInvoicePostingService`). Same for `-periods`.

**No halt needed.** Document the stub limitation in the package README + apps/docs/blocks-financial-ar/overview.md.

### 5. Invoice numbering visual ordering across replicas — UX acceptability (PR 2)

Per CRDT conventions §1 + §8, the format is:

```
INV-2026-05-16-CW-0124
```

This is uglier than `INV-00124`. If during PR 2 review the UX is judged unacceptable (e.g., property-manager feedback on the demo highlights this as a blocker), file `cob-question-2026-05-XXTHH-MMZ-w60-p4-invoice-number-ux.md`.

**XO recommendation:** ship with the per-replica-suffix format as documented; accept the trade-off (no alternative is better under CRDT). A future ADR may revisit the display format — e.g., showing `INV-0124 (CW)` in the UI while storing the canonical form `INV-2026-05-16-CW-0124`. The canonical form must remain unambiguous for audit + search.

### 6. `blocks-rent-collection` API stability (PR 5 — CRITICAL)

PR 5's retrofit MUST preserve every existing consumer's call signature against `IRentCollectionService` + `Sunfish.Blocks.RentCollection.Models.Invoice` + `InvoiceStatus`.

**Halt conditions:**

(a) Any existing test in `packages/blocks-rent-collection/tests/` fails post-retrofit → **STOP IMMEDIATELY** + file `cob-question-*`. Do not "fix" the test to match the new behavior — that's a breaking change.

(b) Any external consumer in `apps/`, `accelerators/`, or other `packages/blocks-*` does not compile after PR 5 lands → STOP + file `cob-question-*`.

(c) The `RentCollection.Invoice.Id` field's ULID materialization differs from the legacy ULID under the existing in-memory implementation in a way that breaks consumer logic that depends on the Id format. (XO recommendation: preserve the legacy `InvoiceId`'s underlying ULID by mapping it 1:1 with the canonical `Sunfish.Blocks.FinancialAr.Models.InvoiceId`.)

If PR 5 cannot meet the non-breakage constraint, **council-review the breaking-change surface** before merging (per the §Pre-merge council exception noted in the header).

### 7. Cross-cluster event bus package home (PR 3, PR 4)

Per `cross-cluster-event-bus-design.md` §10 Q1: the canonical event-bus dispatcher's package home is TBD (`foundation-events` vs `kernel-events`). This hand-off ships local stub `IInvoiceEventPublisher` + record types. When the foundation event-bus package lands:

1. Record types relocate from `packages/blocks-financial-ar/Events/` to the foundation package.
2. `IInvoiceEventPublisher` interface relocates.
3. `InMemoryInvoiceEventPublisher` stub deletes.
4. DI registration updates.

This is a follow-on hand-off; not in scope here. **No halt.**

### 8. ERPNext import customer-lookup edge cases (PR 6)

If the ERPNext source has a customer name that maps to multiple `Party` rows in `InMemoryPartyReadModel.FindPartyByExactNameAsync`, the lookup throws. v1 acceptable behavior: throw with a helpful error message naming the conflicting parties. Real fuzzy-match / dedup is `blocks-people-*` scope per the party-model convention §5.

**No halt.** Document as a known limitation.

### 9. `Migration` JE source-kind requirements on backdated invoices (PR 6)

The ERPNext importer's Pass 2 may need to issue invoices backdated to (e.g.) 2024. The posting service rejects entries with `entryDate > today` by default (per Stage 02 §6.1); does it reject `entryDate << today`?

**Answer:** NO. Stage 02 §6.1's algorithm does not check `entryDate > today`. It checks period gating (period must exist + not Locked + not SoftClosed-for-non-admin). For backdated migration invoices:

- The target period must exist (run `blocks-financial-periods` seeding first to create historical periods).
- The period must be `Open` (or admin-overridable on `SoftClosed`).

If a historical period is `Locked`, the import fails for that invoice (correct behavior — locked means immutable). Importer logs warning; orchestrator decides whether to unlock-with-audit or skip.

**No halt.** Document. The orchestrator (`tooling-anchor-import` follow-on hand-off) handles the period-state setup.

### 10. Loro append-only constraint surfaces (any PR)

Per Stage 02 §12 Q10 (open) + the ledger hand-off's §Halt 2. **Skip Loro integration entirely in this hand-off** (v1 substrate is SQLite-only in-memory). File `cob-question-*` only if compilation fails due to a Loro op-mapping question — it shouldn't.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–6 merged to main** (sequentially or with parallelization where allowed: PR 4 may parallelize with PR 5; PRs 1–3 and PR 6 are sequential).
2. **Invoice round-trip:** acceptance tests A3 + A4 + A5 pass.
3. **AR aging works:** acceptance test A6 passes.
4. **Rent-collection wrapper non-breaking:** acceptance test A7 passes + all existing `blocks-rent-collection` tests pass unchanged.
5. **ERPNext importer round-trip:** acceptance test A8 passes (insert + idempotent re-insert = Skipped).
6. **Replica collision detection:** acceptance test A10 passes.
7. **Performance acceptance:** acceptance test A9 passes (< 500ms local; deferred to Phase 1 close-out for Surface Pro 7 target).
8. **Tests pass:** ~94 new tests across the package + ~25 existing rent-collection tests preserved.
9. **`apps/docs/blocks-financial-ar/overview.md` published** (ships in PR 5 or PR 6 — see §Docs below).
10. **`active-workstreams.md`** row for W#60 P4 / blocks-financial-ar updated with `built` status + the 6 PR numbers.
11. **`coordination/inbox/cob-status-2026-05-XXTHH-MMZ-w60-p4-financial-ar-built.md`** beacon dropped.

When the PASS gate is met, the next Phase 1 critical-path hand-offs can proceed:

- `blocks-financial-payments-stage06-handoff.md` (Payment + PaymentApplication; depends on this hand-off's `Invoice.AmountPaid` / `Balance` fields + the `IPostMergeReconciler` stub for over-application).
- `blocks-financial-ap-stage06-handoff.md` (Bill + BillLine; mirror-image of -ar; lift patterns from this hand-off).
- `blocks-reports-ar-aging-stage06-handoff.md` (consumes `IArAgingService`; renders the aging report PDF).
- `tooling-anchor-import-stage06-handoff.md` (the 6-pass orchestrator; consumes this hand-off's `IErpnextSalesInvoiceImporter`).
- `blocks-rent-collection-modernization-stage06-handoff.md` (further modernization of the rent-collection cluster atop the canonical AR — late-fee policy, prorated rent, etc.).

---

## Docs

**`apps/docs/blocks-financial-ar/overview.md`** — cluster docs page (ships in PR 5 or PR 6; XO recommendation: PR 6, alongside the importer surface mention). Cite ADR 0088 §1; cite Stage 02 schema design §3.5–§3.6, §5.2, §6.2, §7, §8.1; cite ratification ruling Decision 3; cite CRDT-conventions §1 + §8 for numbering scheme.

Structure (sketch):

```markdown
# blocks-financial-ar

Customer-facing accounts-receivable package for the Sunfish Anchor native
financial domain.

## Overview

This package is the canonical Invoice + InvoiceLine surface of the
`blocks-financial-*` cluster per ADR 0088 §1. It provides:

- `Invoice` — customer-facing demand for payment; header.
- `InvoiceLine` — composable line items; income-account + tax + cost-center linkage.
- `InvoiceStatus` — Draft → Issued → PartiallyPaid → Paid; terminal Voided / WrittenOff.
- `IInvoiceNumberingService` — per-replica-suffix monotonic; format `INV-YYYY-MM-DD-{ReplicaId}-{seq}`.
- `IInvoicePostingService` — atomic JE posting on issue; reversal on void; bad-debt JE on write-off.
- `IArAgingService` — bucket algorithm (current / 0-30 / 31-60 / 61-90 / 90+); per-customer + per-property aggregations.
- `IErpnextSalesInvoiceImporter` — ERPNext sales-invoice migration integration.

## Naming

Per the 2026-05-16 naming-ratification ruling Decision 3,
`blocks-rent-collection.Invoice` is a non-breaking wrapper over this
package's canonical `Invoice`. Consumers of `IRentCollectionService` continue
to see the legacy projection shape; under the hood, rent-collection delegates
to `IInvoicePostingService`.

## Quickstart

(~15 lines: minimal example registering DI + issuing an invoice + querying AR aging.)

## Algorithms

- Double-entry posting on issue → link to `blocks-financial-schema-design.md` §6.1 + §8.1
- AR aging buckets → link to §6.2
- Per-replica-suffix numbering → link to `crdt-friendly-schema-conventions.md` §1 + §8
- State-machine terminal-wins → link to `crdt-friendly-schema-conventions.md` §7

## Related

- `blocks-financial-ledger` (predecessor; provides `GLAccount` + `JournalEntry` + `IJournalPostingService`)
- `blocks-financial-tax` (Phase 1 sibling; provides `ITaxCalculationService` — real impl when shipped)
- `blocks-financial-periods` (Phase 1 sibling; provides `IPeriodResolver` — real impl when shipped)
- `blocks-financial-payments` (Phase 1 follow-on; provides PaymentApplication; consumes `Invoice.AmountPaid`)
- `blocks-financial-ap` (Phase 1 follow-on; mirror of -ar for Bills)
- `blocks-rent-collection` (existing; wraps canonical AR Invoice per ratification Decision 3)
- `blocks-reports-ar-aging` (Phase 1 follow-on; consumes `IArAgingService`)
```

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-16):**

- `packages/blocks-financial-ledger/` (predecessor; assumed shipped per the sibling hand-off — verify pre-build checklist step 1) ✓ (pending ledger hand-off close)
- `packages/blocks-rent-collection/Models/Invoice.cs` (target of PR 5 wrapper) ✓
- `packages/blocks-rent-collection/Models/InvoiceStatus.cs` ✓
- `packages/blocks-rent-collection/Services/InMemoryRentCollectionService.cs` ✓
- `packages/blocks-rent-collection/Services/IRentCollectionService.cs` ✓
- `packages/blocks-rent-collection/DependencyInjection/RentCollectionServiceCollectionExtensions.cs` ✓
- ADR 0088 §1 (Path II + 7-cluster decomposition) ✓
- `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md` (Decision 3) ✓
- `icm/02_architecture/blocks-financial-schema-design.md` §3.5–§3.6, §5.2, §5.3, §6.2, §7, §8.1, §10.2 ✓
- `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §3, §6, §7, §8, §10 ✓
- `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3.1, §10 Q1 ✓
- `_shared/engineering/party-model-convention.md` §3, §4 (cross-cluster contract — financial-AR ↔ people) ✓
- `_shared/engineering/erpnext-to-anchor-migration-importer-spec.md` (sibling deliverable, 2026-05-16) ✓

**Introduced by this hand-off** (ship across PRs 1–6):

- New package: `packages/blocks-financial-ar/`
- New types: `InvoiceId`, `InvoiceLineId`, `ReplicaId` (local placeholder), `Invoice`, `InvoiceLine`, `InvoiceStatus`, `InvoiceStatusTransitions`, `BlocksFinancialArOptions`, `RentCollectionAdapterOptions`, `RentInvoiceExtension`, `TaxCalculationResult`, `TaxBreakdownEntry`, `IssueResult`, `IssueError`, `VoidResult`, `VoidError`, `WriteOffResult`, `WriteOffError`, `AgingReport`, `AgingRow`, `AgingTotals`, `AgingGroupBy`, `ErpnextSalesInvoiceSource`, `ErpnextSalesInvoiceItemSource`, `InvoiceCreatedEvent`, `InvoiceIssuedEvent`, `InvoicePaidEvent`, `InvoiceVoidedEvent`, `InvoiceWrittenOffEvent`, `PaymentOverappliedEvent`
- New services: `IInvoiceRepository` + `InMemoryInvoiceRepository`, `IInvoiceNumberingService` + `InMemoryInvoiceNumberingService`, `IInvoicePostingService` + `InvoicePostingService`, `IArAgingService` + `ArAgingService`, `ITaxCalculationService` + `NoOpTaxCalculationService`, `IInvoiceEventPublisher` + `InMemoryInvoiceEventPublisher`, `IPartyReadModel` (local) + `InMemoryPartyReadModel`, `IErpnextSalesInvoiceImporter` + `ErpnextSalesInvoiceImporter`
- Refactored: `Sunfish.Blocks.RentCollection.Services.InMemoryRentCollectionService` (delegation pattern), `RentCollectionServiceCollectionExtensions` (DI wiring), new `Adapters/RentCollectionToFinancialArAdapter`
- Docs: `apps/docs/blocks-financial-ar/overview.md`
- Attribution: `packages/blocks-financial-ar/NOTICE.md`

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies each cited symbol by reading the actual file before declaring AP-21 clean. Do not rely on grep-only verification. Per `feedback_council_can_miss_spot_check_negative_existence`: spot-check negative existence too (verify `IPartyReadModel` in `blocks-people-*` is genuinely absent before shipping the local stub).

---

## Cohort discipline

This hand-off is the **second cluster implementation hand-off under ADR 0088 Path II** (after `blocks-financial-ledger`) and the **first customer-facing financial cluster unit**. The COB self-audit pattern applied to W#34 / W#35 / W#36 / W#39 / W#40 substrate hand-offs + the ledger hand-off applies here verbatim:

- **Two-overload constructor (audit-disabled / audit-enabled both-or-neither) pattern** for any DI extension that interacts with audit. Not required in this hand-off (no audit interaction); retrofit if audit is later wired.
- **`AddBlocksFinancialAr()` naming for the DI extension** — matches the cluster convention.
- **`apps/docs/{cluster}/overview.md` page convention** — applied in PR 5/6.
- **README.md at the package root** referencing Stage 02 design + ADR 0088 — ship in PR 1.
- **`ConcurrentDictionary` dedup for any cache** — applied in `InMemoryInvoiceRepository`, `InMemoryInvoiceNumberingService`, `InMemoryPartyReadModel`, `InMemoryRentInvoiceExtensionStore`.
- **Strong-typed Id records** (ULID-backed) — applied for `InvoiceId`, `InvoiceLineId`.
- **Stub interfaces for cross-cluster contracts not yet shipped** — applied for `ITaxCalculationService`, `IPartyReadModel`, `IInvoiceEventPublisher`. Each ships locally; relocates when the canonical home lands; DI swap with no public surface change.

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w60-p4-financial-ar-{slug}.md` in
  `/Users/christopherwood/Projects/SunfishSoftware/coordination/inbox/`.
- Halt the workstream + add a note in the `active-workstreams.md` row for W#60.
- `ScheduleWakeup 1800s`.

If COB completes PR 6 + the PASS gate is met:

- Update `active-workstreams.md` (via the source W*.md file, not the ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`).
- Drop `cob-status-2026-05-XXTHH-MMZ-w60-p4-financial-ar-built.md` to inbox.
- Continue with the next hand-off in the Phase 1 critical path (likely `blocks-financial-payments` or `blocks-financial-ap` — whichever XO has dropped next).

---

## Cross-references

- Spec source: `icm/02_architecture/blocks-financial-schema-design.md` §3.5–§3.6, §5.2, §5.3, §6.2, §7, §8.1, §10.2.
- CRDT conventions: `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §3, §6, §7, §8, §10.
- Party convention: `_shared/engineering/party-model-convention.md` §3 (Customer role), §4 (cross-cluster contracts), §7 (privacy).
- Event bus: `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3.1, §10.
- Migration importer spec (sibling deliverable, 2026-05-16): `_shared/engineering/erpnext-to-anchor-migration-importer-spec.md`.
- ADR 0088: `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md`.
- Ratification ruling: `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md` (Decision 3 — rent-collection wrapper pattern).
- Predecessor hand-off: `icm/_state/handoffs/blocks-financial-ledger-chart-and-journal-stage06-handoff.md` (the 6-PR ledger build that ships the GLAccount + JournalEntry + IJournalPostingService surface this hand-off consumes).
- Sibling hand-offs (Phase 1 cluster context — likely concurrent):
  - `blocks-financial-periods-stage06-handoff.md` (FiscalYear / FiscalPeriod; replaces the ledger's `InMemoryPeriodResolver` stub)
  - `blocks-financial-tax-stage06-handoff.md` (TaxCode / TaxRate; replaces this hand-off's `NoOpTaxCalculationService` stub)
- Cohort precedent hand-offs (substrate-only shape):
  - `blocks-financial-ledger-chart-and-journal-stage06-handoff.md` (direct precedent)
  - `foundation-mission-space-stage06-handoff.md` (W#40 — 5-PR shape; DI extension pattern)
  - `foundation-versioning-stage06-handoff.md` (W#34 — substrate naming)

---

**End of hand-off.**
