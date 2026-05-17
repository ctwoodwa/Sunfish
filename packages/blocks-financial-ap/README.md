# Sunfish.Blocks.FinancialAp

Accounts-payable substrate for Sunfish. Mirror of `blocks-financial-ar`'s shape with vendor-side differences (single AP control account, vendor-supplied bill numbers, optional approval gate, Disputed hold).

## PR 1 scope (this commit)

Entities + repository contract. No posting, no aging, no event emission — those land in PRs 2–4 of the hand-off.

| File | What ships |
|---|---|
| `Models/BillId.cs`, `BillLineId.cs` | Guid-backed strongly-typed identifiers with JSON converters. |
| `Models/BillStatus.cs` | `enum { Draft, Received, Approved, PartiallyPaid, Paid, Voided, Disputed }` + JSON converter + `IsOpen` / `IsTerminal` / `IsPayable` predicates. `Disputed` is a hold (excluded from Open and Payable; resolvable back to Received/Approved). |
| `Models/BillStatusTransitions.cs` | Static `IsAllowed(from, to)` over the AP transition graph. Allows Received → PartiallyPaid without an Approved step (when policy doesn't gate); forbids Disputed → Paid directly. |
| `Models/BillLine.cs` | Per-line entity. `Amount = banker's-round(Quantity * UnitPrice, 2)` materialized on `Create`. `DebitAccountId` (Expense / Asset) on the line. Opaque `TaxCodeId: string?`. |
| `Models/Bill.cs` | Bill aggregate with cached totals (Subtotal/TaxTotal/Total/AmountPaid/Balance), GL pointers (JournalEntryId / VoidedByEntryId), approval fields (ApprovedByUserId / ApprovedAtUtc), CRDT envelope. |
| `Models/BlocksFinancialApOptions.cs` | Host config: `ApprovalThreshold` (nullable decimal — null = no approval gate). |
| `Services/IBillRepository.cs`, `InMemoryBillRepository.cs` | CRUD + `GetByVendorBillNumberAsync` (composite key) + `GetByExternalRefAsync` + `ListByChart` / `ListByVendor` + `QueryOpenAsync` (with optional vendor/property filter) + idempotent `SoftDeleteAsync`. |
| `DependencyInjection/FinancialApServiceCollectionExtensions.cs` | `services.AddBlocksFinancialAp(configure?)` — `TryAddSingleton<IBillRepository, InMemoryBillRepository>()` + options. |

**Tests:** ~28 across `BillRecordTests`, `BillLineRecordTests`, `BillStatusTransitionTests`, `BillRepositoryTests`.

## Key differences from AR

- **No `IBillNumberingService`.** Bills carry the vendor's own number; uniqueness is `(ChartId, VendorId, BillNumber)`. Lookup via `GetByVendorBillNumberAsync` catches duplicate vendor submissions.
- **Single AP credit account.** AR has one Income account per line + a tax-payable credit. AP has one Expense/Asset account per line + a single AP control credit for the total.
- **Approval gate (optional).** `BlocksFinancialApOptions.ApprovalThreshold` policy gates Received → payment transitions. Below threshold (or null threshold): bills can move Received → PartiallyPaid without approval. PR 2's posting service consumes this option.
- **Disputed hold.** Bill on hold while the dispute resolves; excluded from `IsOpen` / `IsPayable` / AP aging without flipping the bill terminal. Resolves back to Received or Approved.

## Cross-cluster types

| Origin | Used here |
|---|---|
| `Sunfish.Blocks.FinancialLedger.Models` | `GLAccountId`, `ChartOfAccountsId`, `JournalEntryId` |
| `Sunfish.Blocks.People.Foundation.Models` | `PartyId` (the vendor FK) |
| `Sunfish.Foundation.Assets.Common` | `TenantId`, `Instant` |
| `Sunfish.Foundation.Events` | `IDomainEventPublisher` (PR 2's event emission) |

## What's NOT in PR 1

- **PR 2:** `IBillPostingService` (record / void / dispute / resolve-dispute) + `Financial.Bill*` events + tax stub.
- **PR 3:** `IApAgingService` — same bucket algorithm as AR per-vendor / per-property.
- **PR 4:** `IErpnextPurchaseInvoiceImporter` + DI assembly wiring + `apps/docs/blocks-financial-ap/overview.md`.

## Build + test

```bash
dotnet build packages/blocks-financial-ap/Sunfish.Blocks.FinancialAp.csproj
dotnet test  packages/blocks-financial-ap/tests/Sunfish.Blocks.FinancialAp.Tests.csproj
```
