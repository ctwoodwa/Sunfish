# Hand-off — `blocks-financial-payments` Payment + PaymentApplication (Phase 1 critical path)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-17
**Status:** `ready-to-build` (gated — see §Gate conditions below)
**Workstream:** W#68 — blocks-financial-payments (W#60 P4 follow-on, Phase 2 financial cluster)
**Spec source:** [`icm/02_architecture/blocks-financial-schema-design.md`](../../02_architecture/blocks-financial-schema-design.md) §3.9–§3.10, §6.1, §8.3, §10 (importPaymentEntry)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) (Proposed; ratified by CO 2026-05-16)
**Ratifications:** `coordination/inbox/xo-ruling-2026-05-16T17-15Z-cob-naming-ratifications.md`
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~8–12h sunfish-PM (4 PRs; ~25–35 tests + docs)
**PR count:** 4 PRs
**Pre-merge council:** NOT required for PRs 1–2 (substrate scope; mirrors blocks-financial-ar/ap pattern). **EXCEPTION: security spot-check required for PR 3** (`IPaymentApplicationService`) — direction-matching invariant (Inbound ↔ Invoice; Outbound ↔ Bill) is a financial correctness gate; XO security agent reviews before PR 3 merges. File `cob-status-*` when PR 3 is ready for review; do NOT arm auto-merge on PR 3 until XO confirms spot-check passed.
**Attribution:** Apache OFBiz (Apache 2.0) — payment-application pattern; carry NOTICE entry in package.

---

## Gate conditions

This hand-off is gated on two predecessors. Verify both before opening PR 1:

```bash
ls /Users/christopherwood/Projects/SunfishSoftware/Sunfish/packages/ | grep -E "^blocks-financial-(ar|ap)"
# Expected: blocks-financial-ar/ AND blocks-financial-ap/ both exist (all PRs merged)
```

1. **`blocks-financial-ar` shipped** (all 6 PRs merged) — `InvoiceId` type from `Sunfish.Blocks.FinancialAr.Models` must be resolvable; `IInvoiceRepository` must exist for balance updates.

2. **`blocks-financial-ap` shipped** (all 4 PRs merged) — `BillId` type from `Sunfish.Blocks.FinancialAp.Models` must be resolvable; `IBillRepository` must exist for balance updates.

If either gate is unmet, **STOP** — drop a `cob-question-*` beacon naming the unmet gate.

---

## Context

### Phase 1 critical-path position

```
blocks-financial-ledger    (Chart + Journal core)                  ✓ shipped
blocks-financial-periods   (FiscalYear + FiscalPeriod)             ✓ shipped
blocks-financial-tax       (TaxCode + TaxRate + TaxJurisdiction)   ✓ shipped
blocks-financial-ar        (Invoice + InvoiceLine)                 ✓ shipped
blocks-financial-ap        (Bill + BillLine; mirror of -ar)        in-progress (PRs 1-4)
blocks-financial-payments  ← THIS HAND-OFF
```

`blocks-financial-payments` models the cash-movement side of the accounting cycle:
- **Inbound payments** receive money from customers and reduce Invoice balances
- **Outbound payments** send money to vendors and reduce Bill balances
- **PaymentApplication** is the many-to-many link between a Payment and its target Invoice/Bill

Completing this package unblocks:
- `blocks-reports-ap-aging`: the full cashflow picture requires both receivables (AR) and payables (AP) with applied-payment balances
- `blocks-rent-collection` retrofit completion: the `IRentCollectionService.RecordPayment()` path will delegate to `IPaymentApplicationService` instead of updating `Invoice.amountPaid` directly (Phase 2 retrofit; not in this hand-off's scope)
- `tooling-anchor-import`: Pass 4 `importPaymentEntry(pe, targetChartId)` is the final import pass for the ERPNext migration (§10.3 of the spec)
- Tenant-ledger views: CO's end-of-month reconciliation report needs Payment.appliedDate to compute collected-vs-outstanding per property

### What this hand-off ships

Per `blocks-financial-schema-design.md` §3.9–§3.10, §6.1:

**PR 1 — scaffold + Payment + PaymentApplication entities + repositories + DI**

Package: `packages/blocks-financial-payments/`
Namespace root: `Sunfish.Blocks.FinancialPayments`

Entities and value types (all in `Models/`):
- `PaymentId` — opaque ULID string; same pattern as `InvoiceId`/`BillId`; includes `NewId()` factory
- `PaymentApplicationId` — opaque ULID string
- `PaymentDirection` — `enum { Inbound, Outbound }`
- `PaymentMethod` — `enum { Cash, Check, ACH, Wire, Card, DigitalWallet, Other }`
- `PaymentStatus` — `enum { Draft, Cleared, Unapplied, Applied, PartiallyApplied, Bounced, Voided }`
- `AppliedTo` — `enum { Invoice, Bill }`
- `Payment` — record entity per §3.9; fields: `Id`, `ChartId`, `Direction`, `PaymentNumber`, `PartyId`, `BankAccountId`, `PaymentDate`, `Amount`, `Currency`, `Method`, `Reference`, `Status`, `UnappliedAmount`, `Applications`, `JournalEntryId`, `BouncedByEntryId`, `Notes`, `ExternalRef`, `CreatedAtUtc`, `UpdatedAtUtc`
- `PaymentApplication` — record entity per §3.10; fields: `Id`, `PaymentId`, `AppliedTo`, `TargetId`, `AmountApplied`, `AppliedDate`, `DiscountAmount`, `WriteoffAmount`, `CreatedAtUtc`
- `BlocksFinancialPaymentsOptions` — mirrors `BlocksFinancialArOptions` shape; has `FallbackPollingInterval`

Repository contracts and in-memory implementations (in `Services/`):
- `IPaymentRepository` — `AddAsync`, `GetAsync`, `UpdateAsync`, `ListByChartAsync`, `ListByPartyAsync`
- `InMemoryPaymentRepository` — thread-safe via `ConcurrentDictionary`
- `IPaymentApplicationRepository` — `AddAsync`, `GetAsync`, `DeleteAsync` (unapply), `ListByPaymentAsync`, `ListByTargetAsync`
- `InMemoryPaymentApplicationRepository` — thread-safe; does NOT update Payment/Invoice/Bill balances (that's `IPaymentApplicationService` in PR 3)

DI extension (in `DependencyInjection/`):
- `AddSunfishFinancialPayments()` — registers `IPaymentRepository`, `IPaymentApplicationRepository`, options binding; follows the pattern of `AddSunfishFinancialAp()`

**Tests (PR 1):** ≥10 unit tests covering:
- `Payment.UnappliedAmount` invariant (`Amount - sum(Applications[].AmountApplied) >= 0`)
- `PaymentApplication` round-trip through `InMemoryPaymentApplicationRepository`
- `PaymentStatus` transition validation helpers (if any)

**What PR 1 does NOT include:** posting logic, application logic, or ERPNext importer. Those arrive in PRs 2–4. The `IPaymentPostingService` and `IPaymentApplicationService` interfaces are declared in `Services/` as stubs (interface-only, no implementation) so downstream can reference them at compile time.

---

**PR 2 — `IPaymentPostingService` (clear / bounce / void → GL)**

Interface and implementation in `Services/`:

```csharp
// IPaymentPostingService.cs
public interface IPaymentPostingService
{
    // Transition Draft → Cleared (+ Unapplied if no applications yet).
    // Posts JE:
    //   Inbound:  Dr BankAccountId / Cr AR control account (chartId's default AR account)
    //   Outbound: Dr AP control account / Cr BankAccountId
    // Sets Payment.JournalEntryId + status. Idempotent: if already Cleared, returns existing JE id.
    Task<ClearResult> ClearAsync(PaymentId id, PartyId actor, CancellationToken ct = default);

    // Transition Cleared/PartiallyApplied/Unapplied → Bounced.
    // Posts reversing JE (flipped debits/credits of the clear entry).
    // For each prior PaymentApplication: reduces Invoice/Bill.AmountPaid + restores balance;
    // deletes the PaymentApplication records (reversal path).
    // Sets Payment.BouncedByEntryId + status.
    Task<BounceResult> BounceAsync(PaymentId id, string reason, PartyId actor, CancellationToken ct = default);

    // Transition Draft → Voided (before clearing; no GL entry).
    Task<VoidResult> VoidAsync(PaymentId id, string reason, PartyId actor, CancellationToken ct = default);
}
```

Result/error enums follow the same pattern as `IInvoicePostingService` (from `blocks-financial-ar`):
- `ClearResult(Payment?, JournalEntryId?, ClearError, string?)`
- `BounceResult(Payment?, JournalEntryId?, BounceError, string?)`
- `VoidResult(Payment?, VoidError, string?)`
- `ClearError enum { None, UnknownPayment, InvalidStatusForClear, JournalRejected, ... }`
- `BounceError enum { None, UnknownPayment, InvalidStatusForBounce, JournalRejected, ... }`
- `VoidError enum { None, UnknownPayment, InvalidStatusForVoid, ... }`

Implementation `DefaultPaymentPostingService`:
- Delegates double-entry posting to `IJournalPostingService` from `blocks-financial-ledger`
- For the BounceAsync reversal: iterates `IPaymentApplicationRepository.ListByPaymentAsync(id)` and for each application updates Invoice/Bill balance via the respective repository. This is the only place in the cluster where the posting service touches invoice/bill repositories directly — document this in a code comment.
- Does NOT implement the sequential `PaymentNumber` minting in this pass; `PaymentNumber` can be a ULID string on creation (Phase 2 sequential numbering is a follow-up)

Dependencies for PR 2 csproj additions:
- `<ProjectReference Include="..\blocks-financial-ar\Sunfish.Blocks.FinancialAr.csproj" />`
- `<ProjectReference Include="..\blocks-financial-ap\Sunfish.Blocks.FinancialAp.csproj" />`
- (blocks-financial-ledger is already in PR 1's deps)

**Tests (PR 2):** ≥8 unit tests:
- ClearAsync Inbound posts Dr Bank / Cr AR JE via mock `IJournalPostingService`
- ClearAsync Outbound posts Dr AP / Cr Bank JE
- ClearAsync idempotent (already-Cleared returns same JE id)
- ClearAsync on non-Draft status returns ClearError.InvalidStatusForClear
- BounceAsync posts reversal JE + deletes applications + restores Invoice/Bill balance
- VoidAsync on Draft succeeds; on Cleared returns VoidError.InvalidStatusForVoid

---

**PR 3 — `IPaymentApplicationService` (apply / unapply) — SECURITY SPOT-CHECK REQUIRED**

⚠ **XO security review before this PR merges.** File `cob-status-*` when PR 3 is open; XO runs a security spot-check on the direction-matching invariant. DO NOT arm auto-merge until XO confirms.

Interface:

```csharp
// IPaymentApplicationService.cs
public interface IPaymentApplicationService
{
    // Apply amountApplied of paymentId to targetId (Invoice or Bill).
    // Direction-matching invariant (§3.10 validation rule 1):
    //   Inbound payment → appliedTo == AppliedTo.Invoice  (enforced; reject otherwise)
    //   Outbound payment → appliedTo == AppliedTo.Bill    (enforced; reject otherwise)
    // On success:
    //   - Creates PaymentApplication record
    //   - Updates Invoice/Bill.AmountPaid += amountApplied; recomputes Balance
    //   - Updates Invoice/Bill status (PartiallyPaid if balance > 0; Paid if balance == 0)
    //   - Updates Payment.UnappliedAmount -= amountApplied
    //   - Updates Payment status (PartiallyApplied or Applied)
    //   - Emits Financial.PaymentApplied audit event
    Task<ApplyResult> ApplyAsync(
        PaymentId paymentId,
        AppliedTo appliedTo,
        string targetId,             // InvoiceId or BillId — string union per spec §3.10
        Money amountApplied,
        Money discountAmount,        // zero if no early-pay discount
        Money writeoffAmount,        // zero if no short-pay write-off
        PartyId actor,
        CancellationToken ct = default);

    // Remove a specific application (correction path).
    // On success:
    //   - Deletes PaymentApplication record
    //   - Restores Invoice/Bill.AmountPaid -= amountApplied; recomputes Balance + status
    //   - Restores Payment.UnappliedAmount += amountApplied; recomputes status
    //   - Emits Financial.PaymentUnapplied audit event
    Task<UnapplyResult> UnapplyAsync(
        PaymentApplicationId applicationId,
        PartyId actor,
        CancellationToken ct = default);
}
```

Result/error types follow the cluster pattern:
- `ApplyResult(PaymentApplication?, ApplyError, string?)`
- `UnapplyResult(bool Success, UnapplyError, string?)`
- `ApplyError enum { None, UnknownPayment, UnknownTarget, DirectionMismatch, InsufficientUnapplied, TargetBalanceInsufficient, CurrencyMismatch, TargetTerminal, ... }`
- `UnapplyError enum { None, UnknownApplication, ... }`

**Direction-matching MUST be the first guard** (before repository lookups) to avoid cross-cluster timing attacks (prevent deducing target existence by observing error type differences).

**Discount and writeoff GL posting:** if `discountAmount > 0` or `writeoffAmount > 0`, delegate to `IJournalPostingService` for the GL lines (Discount Allowed / Bad Debt expense lines per §6.1 notes). The application service creates the extra JE lines; it does not call `IPaymentPostingService`.

**Tests (PR 3):** ≥12 unit tests — these are the most important tests in the package:
- `ApplyAsync` Inbound→Invoice succeeds; balances updated correctly
- `ApplyAsync` Outbound→Bill succeeds; balances updated correctly
- `ApplyAsync` Inbound→Bill fails with `ApplyError.DirectionMismatch` (critical)
- `ApplyAsync` Outbound→Invoice fails with `ApplyError.DirectionMismatch` (critical)
- `ApplyAsync` with `amountApplied > Payment.UnappliedAmount` fails with `InsufficientUnapplied`
- `ApplyAsync` with `amountApplied > Invoice/Bill.Balance` fails with `TargetBalanceInsufficient`
- `ApplyAsync` on terminal Invoice (Voided/WrittenOff) fails with `TargetTerminal`
- `ApplyAsync` with currency mismatch fails with `CurrencyMismatch`
- `UnapplyAsync` restores Invoice and Payment balances correctly
- `UnapplyAsync` on unknown application fails with `UnapplyError.UnknownApplication`
- Partial application (amountApplied < balance): Invoice status → PartiallyPaid; Payment status → PartiallyApplied
- Full application (amountApplied == balance): Invoice status → Paid; Payment status → Applied

---

**PR 4 — ERPNext PaymentEntry importer + docs + ledger flip**

`IErpnextPaymentEntryImporter` in `Services/`:

```csharp
// Completes Pass 4 of the ERPNext migration importer (§10.3).
// Called after importPurchaseInvoice() for all Bills.
public interface IErpnextPaymentEntryImporter
{
    // Import a PaymentEntry from ERPNext's export.
    // pe.payment_type "Receive" → Inbound; "Pay" → Outbound.
    // pe.references[] maps to PaymentApplication rows (InvoiceId or BillId via externalRef lookup).
    // On success: creates Payment + PaymentApplication rows; calls ClearAsync to post the GL entry.
    // Idempotent by pe.name (externalRef): insert-or-skip if already imported.
    Task<ImportPaymentResult> ImportPaymentEntryAsync(
        ErpnextPaymentEntry pe,
        ChartOfAccountsId targetChartId,
        CancellationToken ct = default);
}
```

`ErpnextPaymentEntry` DTO mirrors the ERPNext PaymentEntry doctype fields used in Pass 4:
- `name`, `payment_type`, `party`, `paid_amount`, `mode_of_payment`, `reference_no`, `reference_date`, `references[]` (each with `reference_doctype`, `reference_name`, `allocated_amount`)

Implementation delegates to `IPaymentPostingService.ClearAsync` for GL posting after creating the Payment record. Applications are created via `IPaymentApplicationService.ApplyAsync` for each reference.

**Docs:** `apps/docs/blocks/financial-payments/overview.md` — key types, DI registration, direction-matching invariant, ERPNext importer usage.

**Ledger flip:** update `active-workstreams.md` W#68 row → `built`. Standard PR body with test count.

---

## Package structure

```
packages/blocks-financial-payments/
├── Sunfish.Blocks.FinancialPayments.csproj
├── NOTICE.md                          (Apache OFBiz payment-application attribution)
├── README.md
├── Models/
│   ├── Payment.cs
│   ├── PaymentId.cs
│   ├── PaymentApplication.cs
│   ├── PaymentApplicationId.cs
│   ├── PaymentDirection.cs            (enum)
│   ├── PaymentMethod.cs               (enum)
│   ├── PaymentStatus.cs               (enum)
│   ├── AppliedTo.cs                   (enum)
│   └── BlocksFinancialPaymentsOptions.cs
├── Services/
│   ├── IPaymentRepository.cs
│   ├── InMemoryPaymentRepository.cs
│   ├── IPaymentApplicationRepository.cs
│   ├── InMemoryPaymentApplicationRepository.cs
│   ├── IPaymentPostingService.cs      (PR 2)
│   ├── DefaultPaymentPostingService.cs (PR 2)
│   ├── IPaymentApplicationService.cs  (PR 3)
│   ├── DefaultPaymentApplicationService.cs (PR 3)
│   └── IErpnextPaymentEntryImporter.cs (PR 4)
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs
└── tests/
    └── Sunfish.Blocks.FinancialPayments.Tests/
        └── Sunfish.Blocks.FinancialPayments.Tests.csproj
```

**csproj dependencies:**

```xml
<ItemGroup>
  <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
  <ProjectReference Include="..\foundation-events\Sunfish.Foundation.Events.csproj" />
  <ProjectReference Include="..\blocks-financial-ledger\Sunfish.Blocks.FinancialLedger.csproj" />
  <ProjectReference Include="..\blocks-financial-ar\Sunfish.Blocks.FinancialAr.csproj" />
  <ProjectReference Include="..\blocks-financial-ap\Sunfish.Blocks.FinancialAp.csproj" />
  <ProjectReference Include="..\blocks-people-foundation\Sunfish.Blocks.People.Foundation.csproj" />
</ItemGroup>
```

Note: `blocks-financial-ar` + `blocks-financial-ap` are only required from PR 2 onward (for balance updates and JE routing). PR 1 can omit them; add in PR 2 when `DefaultPaymentPostingService` calls the respective repositories.

---

## Audit invariants

These MUST hold after every operation and are verified by the test suite:

**Payment invariants:**
1. `Payment.UnappliedAmount = Payment.Amount - sum(Applications[].AmountApplied)`, always `>= 0`
2. `Payment.Status` is always consistent with `UnappliedAmount`: if `== Amount` → Unapplied; if `> 0 and < Amount` → PartiallyApplied; if `== 0` → Applied

**PaymentApplication invariants:**
1. Direction-matching: Inbound ↔ Invoice; Outbound ↔ Bill. No exceptions.
2. `amountApplied + discountAmount + writeoffAmount <= target.Balance` at time of application (no overapplication)
3. `sum_over_applications(amountApplied) <= payment.Amount`
4. Currency match: `payment.Currency == invoice/bill.Currency == application.AmountApplied.Currency`

**NOTICE content (Apache OFBiz attribution):**
```
The payment-application entity model in this package (Payment,
PaymentApplication, the direction-matching invariant, and the many-to-many
apply/unapply pattern) was informed by the Apache OFBiz 'accounting' package
(entity 'PaymentApplication', package version 18.12). Apache OFBiz is
licensed under the Apache License, Version 2.0.

Source: https://github.com/apache/ofbiz-framework
```

---

## Halt conditions

Stop and file `cob-question-*` if any of these arise:

1. **Gate not met** — `blocks-financial-ar` or `blocks-financial-ap` package is missing on disk. File naming the unmet gate. Do not proceed.
2. **Direction-matching conflict** — any scenario where an Inbound payment must be applied to a Bill (or vice versa) for the ERPNext importer to work correctly. The §3.10 spec is unambiguous; if ERPNext export data has mismatched types, treat as import data quality issue and log a warning rather than bypassing the invariant.
3. **PR 3 SecurityReview** — do NOT arm auto-merge until XO confirms security spot-check passed. File `cob-status-*` when PR 3 is ready.
4. **Balance invariant violation** — if implementing `DefaultPaymentApplicationService` reveals an edge case where the four audit invariants cannot all hold simultaneously (e.g. concurrent application race), halt and ask XO. The correct fix is a serialization strategy, not a relaxation of the invariant.
5. **csproj dependency cycle** — if adding `blocks-financial-ar` + `blocks-financial-ap` to the payments csproj introduces a circular dependency (they both reference `blocks-financial-ledger`; payments references all three; no cycle since the direction is always leaf → ledger). If you discover a cycle, halt + file question.

---

## PR commit message templates

```
feat(blocks-financial-payments): PR 1 — scaffold + Payment + PaymentApplication + repositories + DI
feat(blocks-financial-payments): PR 2 — IPaymentPostingService (clear/bounce/void + GL posting)
feat(blocks-financial-payments): PR 3 — IPaymentApplicationService (apply/unapply + direction guard)
feat(blocks-financial-payments): PR 4 — ERPNext PaymentEntry importer + docs + ledger flip
```
