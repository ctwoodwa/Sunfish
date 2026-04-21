---
uid: block-accounting-entity-model
title: Accounting — Entity Model
description: Chart of accounts, journal entries with double-entry invariants, and depreciation schedule records exposed by Sunfish.Blocks.Accounting.
keywords:
  - sunfish
  - accounting
  - gl-account
  - journal-entry
  - depreciation-schedule
  - entity-model
---

# Accounting — Entity Model

## Overview

The accounting block exposes three aggregate-like shapes: `GLAccount`, `JournalEntry` (with
its child `JournalEntryLine`), and `DepreciationSchedule`. All are immutable records; mutation
happens by replacing the record via `IAccountingService` methods.

## GLAccount

A single node in the general ledger chart of accounts.

| Field             | Type                | Notes |
|-------------------|---------------------|-------|
| `Id`              | `GLAccountId`       | Unique identifier. |
| `Code`            | `string`            | Human-readable code (e.g. `"4000"`). Unique within a chart. |
| `Name`            | `string`            | Display name (e.g. `"Rental Revenue"`). |
| `Type`            | `GLAccountType`     | Asset, Liability, Equity, Revenue, Expense. |
| `ParentAccountId` | `GLAccountId?`      | Optional parent for hierarchical charts; `null` for roots. |

`GLAccountType` is a closed enum:

- `Asset` — resources owned by the entity.
- `Liability` — obligations owed to creditors.
- `Equity` — residual interest of owners.
- `Revenue` — income earned from operations.
- `Expense` — costs incurred in generating revenue.

## JournalEntry

An immutable, double-entry accounting record composed of one or more `JournalEntryLine`
children. The constructor enforces the core invariant: **sum of all debits must equal sum
of all credits**. Imbalanced entries are rejected with `ArgumentException`.

| Field             | Type                              | Notes |
|-------------------|-----------------------------------|-------|
| `Id`              | `JournalEntryId`                  | Unique identifier. |
| `EntryDate`       | `DateOnly`                        | Accounting-effective date. |
| `Memo`            | `string`                          | Free-form description. |
| `Lines`           | `IReadOnlyList<JournalEntryLine>` | Ordered, balanced lines. |
| `CreatedAtUtc`    | `Instant`                         | Posting instant. |
| `SourceReference` | `string?`                         | Optional opaque back-reference (e.g. `"rent-payment:INV-123"`). |

### JournalEntryLine

A single debit or credit line. Invariants enforced at construction:

- Exactly one of `Debit` or `Credit` is non-zero.
- Both are non-negative.

| Field       | Type          | Notes |
|-------------|---------------|-------|
| `AccountId` | `GLAccountId` | Target account for this line. |
| `Debit`     | `decimal`     | Debit amount; zero on credit lines. |
| `Credit`    | `decimal`     | Credit amount; zero on debit lines. |
| `Notes`     | `string?`     | Optional per-line annotation. |

## DepreciationSchedule

Passive configuration record for a fixed-asset depreciation schedule. Schedule-line
computation (generating periodic journal entries from the schedule) is intentionally
deferred to a follow-up pass.

| Field              | Type                 | Notes |
|--------------------|----------------------|-------|
| `Id`               | `DepreciationScheduleId` | Unique identifier. |
| `AssetId`          | `EntityId`           | Reference to the asset (e.g. `asset:acme-realty/building-42`). |
| `StartDate`        | `DateOnly`           | First day of the first depreciation period. |
| `OriginalCost`     | `decimal`            | Acquisition cost. Non-negative. |
| `SalvageValue`     | `decimal`            | Residual value. Non-negative and ≤ `OriginalCost`. |
| `UsefulLifeMonths` | `int`                | Positive. |
| `Method`           | `DepreciationMethod` | `StraightLine`, `DecliningBalance`, `UnitsOfProduction`. |

## Relationships

```
GLAccount  <──  JournalEntryLine.AccountId
GLAccount  <──  GLAccount.ParentAccountId   (self-reference)

JournalEntry  1 ─── N  JournalEntryLine    (owned collection, balanced)

DepreciationSchedule  ── references ──>  EntityId (external asset)
```

There are no direct relationships between `DepreciationSchedule` and the JE engine in this
pass; the follow-up `ComputeScheduleAsync` would emit JEs that reference the schedule via
`SourceReference`.

## Strong-typed identifiers

Every top-level record has a dedicated identifier struct:

- `GLAccountId` — wraps a `string` code; the value is the same human-readable code the
  caller supplies to `CreateGLAccountRequest.Code`. This makes GL codes round-trip through
  the journal entries and the IIF export without a secondary lookup table.
- `JournalEntryId` — GUID-based; `JournalEntryId.NewId()` generates a fresh identifier.
- `DepreciationScheduleId` — GUID-based; `DepreciationScheduleId.NewId()` generates a fresh
  identifier.

Because records are immutable and identifiers are opaque to callers, any persistence-backed
implementation can re-use the same shapes without layering extra DTOs.

## Invariant catalogue

The constructors on `JournalEntry` and `JournalEntryLine` are the sole place where balance
and single-side invariants are enforced. Every persistence-backed implementation must call
those constructors (not bypass them) to retain the guarantees:

| Type | Invariant | Constructor behaviour |
|---|---|---|
| `JournalEntryLine` | Exactly one of `Debit` / `Credit` is non-zero. | `ArgumentException`. |
| `JournalEntryLine` | Both `Debit` and `Credit` are `>= 0`. | `ArgumentException`. |
| `JournalEntry` | `Lines` is non-empty. | `ArgumentException`. |
| `JournalEntry` | `Sum(l.Debit) == Sum(l.Credit)` across `Lines`. | `ArgumentException` with a message reporting the actual totals (formatted `F2`). |

`DepreciationSchedule` enforces non-negative `OriginalCost`, non-negative `SalvageValue`,
`SalvageValue <= OriginalCost`, and `UsefulLifeMonths > 0`.

## Usage example (drawn from tests)

```csharp
var accountId = GLAccountId.NewId();

// Rejected: debit and credit both non-zero
Assert.Throws<ArgumentException>(() =>
    new JournalEntryLine(accountId, debit: 100m, credit: 50m));

// Rejected: imbalanced across lines
var lines = new List<JournalEntryLine>
{
    new(cashId,    debit: 500m, credit: 0m),
    new(revenueId, debit: 0m,   credit: 400m),   // 500 ≠ 400
};
await Assert.ThrowsAsync<ArgumentException>(
    () => svc.PostEntryAsync(new PostJournalEntryRequest(
        EntryDate: new DateOnly(2025, 6, 1),
        Memo: "Imbalanced",
        Lines: lines)).AsTask());
```

## Related pages

- [Overview](overview.md)
- [Service Contract](service-contract.md)
- [QuickBooks IIF Export](quickbooks-iif-export.md)
