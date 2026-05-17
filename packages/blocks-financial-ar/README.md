# Sunfish.Blocks.FinancialAr

Accounts-receivable substrate for Sunfish. Builds on canonical Party (`blocks-people-foundation`) and the GL ledger (`blocks-financial-ledger`).

## PR 1 scope (this commit)

Entities + repository contract. No numbering, no posting, no aging, no event emission — those land in PRs 2–4 of the hand-off.

| File | What ships |
|---|---|
| `Models/InvoiceId.cs`, `InvoiceLineId.cs` | Strongly-typed Guid-backed identifiers with JSON converters. Same pattern as `blocks-people-foundation` and `blocks-financial-tax`; ULID deferred pending a repo-wide sweep. |
| `Models/InvoiceStatus.cs` | `enum { Draft, Issued, PartiallyPaid, Paid, Voided, WrittenOff }` + lowercase-camelCase JSON converter + `IsOpen` / `IsTerminal` predicates. `Overdue` is intentionally **not** an enum member — it's derived from `IsOpen() && today > DueDate && Balance > 0`. |
| `Models/InvoiceStatusTransitions.cs` | Static `IsAllowed(from, to)` over the full transition graph. Forbids un-issuing a Draft, rewinding terminals, and skipping `Issued`. |
| `Models/InvoiceLine.cs` | Per-line entity with banker's-rounded `Amount = round(Quantity * UnitPrice, 2)` materialized on `Create`. Carries opaque `TaxCodeId: string?` rather than a hard dep on `blocks-financial-tax`. |
| `Models/Invoice.cs` | Invoice aggregate with cached monetary totals (Subtotal/TaxTotal/Total/AmountPaid/Balance), GL pointers (`JournalEntryId`, `VoidedByEntryId`, `WrittenOffByEntryId`), and the standard CRDT envelope. Static `Create` factory materializes the totals from supplied lines. |
| `Services/IInvoiceRepository.cs`, `InMemoryInvoiceRepository.cs` | CRUD + `GetByNumber` + `ListByChart` / `ListByCustomer` + `SoftDeleteAsync` (idempotent). Tombstoned rows excluded from all reads. |
| `DependencyInjection/FinancialArServiceCollectionExtensions.cs` | `services.AddBlocksFinancialAr()` — `TryAddSingleton<IInvoiceRepository, InMemoryInvoiceRepository>()`. Persistence-backed implementations registered earlier by the host shadow this default. |

**Tests:** ~22 across `InvoiceRecordTests`, `InvoiceLineRecordTests`, `InvoiceStatusTransitionTests`, `InvoiceRepositoryTests`.

## What's NOT in PR 1

- **PR 2:** `IInvoiceNumberingService` — per-replica monotonic `INV-YYYY-MM-DD-XX-NNNN` minting + collision detection.
- **PR 3:** `IInvoicePostingService` — issue / void / write-off operations, tax computation via `ITaxCalculationService`, idempotent journal-entry posting + canonical-event emission via `Sunfish.Foundation.Events.IDomainEventPublisher`.
- **PR 4:** `IArAgingService` — current / 0–30 / 31–60 / 61–90 / 90+ bucket algorithm, per-customer + per-property roll-ups.
- **PR 5:** Non-breaking retrofit of existing `blocks-rent-collection.Invoice` to delegate to canonical AR.
- **PR 6:** `IErpnextSalesInvoiceImporter` — ERPNext `Sales Invoice` doctype upsert.

## Cross-cluster types

| Origin | Used here |
|---|---|
| `Sunfish.Blocks.FinancialLedger.Models` | `GLAccountId`, `ChartOfAccountsId`, `JournalEntryId` |
| `Sunfish.Blocks.People.Foundation.Models` | `PartyId` (the customer FK) |
| `Sunfish.Foundation.Assets.Common` | `TenantId`, `Instant` |

`TaxCodeId` is intentionally a `string?` rather than `Sunfish.Blocks.FinancialTax.Models.TaxCodeId` — keeping AR independent of the tax cluster's exact ID type until PR 3 wires the calculation engine.

## Build + test

```bash
dotnet build packages/blocks-financial-ar/Sunfish.Blocks.FinancialAr.csproj
dotnet test  packages/blocks-financial-ar/tests/Sunfish.Blocks.FinancialAr.Tests.csproj
```
