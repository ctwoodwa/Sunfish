# Sunfish.Blocks.Accounting

Accounting block — GL accounts, journal entries, depreciation schedules, in-memory service, and a QuickBooks IIF exporter.

**First pass — export-only (Option B from the intake).** Defers: ledger UI, Xero export, automatic JE generation, depreciation computation.

## What this ships

### Models

- **`GLAccount`** + `GLAccountId` + `GLAccountType` — chart-of-accounts entries; type discriminator (Asset / Liability / Equity / Revenue / Expense).
- **`JournalEntry`** + `JournalEntryId` — double-entry ledger posting (date, description, debit/credit lines).
- **`DepreciationSchedule`** + `DepreciationScheduleId` — asset depreciation records.
- **`DepreciationMethod`** — enum (StraightLine / DecliningBalance / SumOfYearsDigits / Units).

### Services

- **`IAccountingService`** + `InMemoryAccountingService` — CRUD over GL + JE + depreciation; balance lookups; trial-balance projection.
- **`QuickBooksIifExporter`** — exports JE history to QuickBooks IIF format for hand-off into existing QB workflows.

## DI

```csharp
services.AddInMemoryAccounting();
```

## Deferred follow-ups (per intake)

- Ledger UI (read-only viewer + JE entry forms)
- Xero export adapter (parallel to QuickBooks IIF)
- Automatic JE generation from rent collection + maintenance + payment events
- Depreciation computation engine (currently the schedule is recorded; computation is caller-supplied)

## See also

- [apps/docs Overview](../../apps/docs/blocks/accounting/overview.md)
- [QuickBooks IIF Export](../../apps/docs/blocks/accounting/quickbooks-iif-export.md)
- [Entity Model](../../apps/docs/blocks/accounting/entity-model.md)
- [Service Contract](../../apps/docs/blocks/accounting/service-contract.md)
