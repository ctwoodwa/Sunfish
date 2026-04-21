---
uid: block-accounting-service-contract
title: Accounting — Service Contract
description: The IAccountingService public surface — GL accounts, balanced journal-entry posting, and depreciation-schedule registration.
keywords:
  - sunfish
  - accounting
  - service-contract
  - iaccountingservice
  - journal-entry
  - chart-of-accounts
---

# Accounting — Service Contract

## Overview

`IAccountingService` is the single public write/read contract for the accounting block.
It is framework-agnostic and has no Blazor, React, or UI coupling — the same implementation
can be consumed from ASP.NET Core endpoints, background workers, test harnesses, or the
kitchen-sink demo.

The service is deliberately narrow. Three bounded concerns are surfaced:

1. GL account management.
2. Journal-entry posting (balanced, double-entry).
3. Depreciation-schedule registration (passive; no computation).

Explicitly deferred (follow-up passes): automatic JE generation from payment events,
schedule-line computation, Xero export, and a ledger UI block.

## GL account methods

```csharp
ValueTask<GLAccount> CreateAccountAsync(CreateGLAccountRequest request, CancellationToken ct = default);
ValueTask<GLAccount?> GetAccountAsync(GLAccountId id, CancellationToken ct = default);
IAsyncEnumerable<GLAccount> ListAccountsAsync(CancellationToken ct = default);
```

`CreateAccountAsync` throws `InvalidOperationException` when an account with the requested
`Code` already exists. `GetAccountAsync` returns `null` for unknown IDs.

### `CreateGLAccountRequest`

| Parameter         | Type             | Notes |
|-------------------|------------------|-------|
| `Code`            | `string`         | Unique within the service instance. |
| `Name`            | `string`         | Display name. |
| `Type`            | `GLAccountType`  | Account category. |
| `ParentAccountId` | `GLAccountId?`   | Optional; referenced account must exist. |

## Journal-entry methods

```csharp
ValueTask<JournalEntry> PostEntryAsync(PostJournalEntryRequest request, CancellationToken ct = default);
ValueTask<JournalEntry?> GetEntryAsync(JournalEntryId id, CancellationToken ct = default);
IAsyncEnumerable<JournalEntry> ListEntriesAsync(ListEntriesQuery query, CancellationToken ct = default);
```

`PostEntryAsync` validates and persists a journal entry. It throws:

- `ArgumentException` when the entry is imbalanced or a line violates single-side invariants
  (both non-zero, both zero, or negative).
- `KeyNotFoundException` when any `JournalEntryLine.AccountId` references an unknown account.

### `PostJournalEntryRequest`

| Parameter          | Type                              | Notes |
|--------------------|-----------------------------------|-------|
| `EntryDate`        | `DateOnly`                        | Accounting-effective date. |
| `Memo`             | `string`                          | Free-form description. |
| `Lines`            | `IReadOnlyList<JournalEntryLine>` | Must be non-empty and balanced. |
| `SourceReference`  | `string?`                         | Optional back-reference. |

### `ListEntriesQuery`

| Parameter   | Type        | Notes |
|-------------|-------------|-------|
| `FromDate`  | `DateOnly?` | Inclusive lower bound on `EntryDate`. |
| `ToDate`    | `DateOnly?` | Inclusive upper bound on `EntryDate`. |

An empty query returns all entries.

## Depreciation-schedule method

```csharp
ValueTask<DepreciationSchedule> RegisterScheduleAsync(
    RegisterDepreciationRequest request, CancellationToken ct = default);
```

Stores the schedule configuration. Schedule-line generation is deferred — the method
does not emit JEs or compute period expense.

### `RegisterDepreciationRequest`

| Parameter           | Type                  | Notes |
|---------------------|-----------------------|-------|
| `AssetId`           | `EntityId`            | Reference to the asset (e.g. `asset:acme-realty/building-42`). |
| `StartDate`         | `DateOnly`            | First day of the first depreciation period. |
| `OriginalCost`      | `decimal`             | Non-negative. |
| `SalvageValue`      | `decimal`             | `≤ OriginalCost`. |
| `UsefulLifeMonths`  | `int`                 | Positive. |
| `Method`            | `DepreciationMethod`  | Cost-allocation method. |

## Typical workflow

1. **Seed the chart**: call `CreateAccountAsync` for each account (`"4000" Rental Revenue`,
   `"1100" Bank`, etc.).
2. **Post rent receipt**: build a balanced `PostJournalEntryRequest` with two `JournalEntryLine`s
   — a debit to cash/bank and a credit to rental revenue — and call `PostEntryAsync`.
3. **Register depreciation** for a capitalised asset via `RegisterScheduleAsync`.
4. **List for the period**: `ListEntriesAsync(new ListEntriesQuery(from, to))` to enumerate
   all entries in a reporting window.
5. **Export to QuickBooks**: feed the enumerated entries to
   `IQuickBooksJournalEntryExporter.Export` — see
   [QuickBooks IIF Export](quickbooks-iif-export.md).

## Concurrency

`InMemoryAccountingService` protects both the chart and the journal-entry store with a lock,
so concurrent `PostEntryAsync` calls never lose entries. The regression is pinned by the
`PostEntryAsync_ConcurrentCallsAreSafe_AllEntriesAreStored` test, which fires 20 parallel
posts and asserts that all 20 land in the ledger. Persistence-backed implementations should
provide equivalent guarantees (database transactions typically do this for free).

## Cancellation semantics

All methods accept an optional `CancellationToken`. Cancellation is checked before each store
mutation; once a mutation has been committed the method will not abort mid-way. Callers that
need hard deadlines should wrap the call in their own timeout — cancellation is cooperative.

## Default implementation

`InMemoryAccountingService` is registered by `AddInMemoryAccounting`. It is suitable for
testing, prototyping, and kitchen-sink demos. Replace it with a persistence-backed
implementation for production workloads. The in-memory service keeps two `Dictionary`
instances — one keyed by `GLAccountId`, one keyed by `JournalEntryId` — and a simple list
for depreciation schedules; no other state is stored.

### Registering a replacement

A production host that provides its own implementation should still register the IIF
exporter from `AddInMemoryAccounting`, because the exporter is framework-agnostic and
stateless:

```csharp
services.AddSingleton<IAccountingService, EfAccountingService>();
services.AddSingleton<IQuickBooksJournalEntryExporter, QuickBooksIifExporter>();
```

## Related pages

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- [QuickBooks IIF Export](quickbooks-iif-export.md)
