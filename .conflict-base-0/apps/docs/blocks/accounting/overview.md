---
uid: block-accounting-overview
title: Accounting — Overview
description: Double-entry GL, journal entries, depreciation schedules, and QuickBooks IIF export for Sunfish-hosted property and business apps.
keywords:
  - sunfish
  - blocks
  - accounting
  - general-ledger
  - journal-entry
  - double-entry
  - quickbooks
  - iif
  - depreciation
---

# Accounting — Overview

## What this block is

`Sunfish.Blocks.Accounting` is a framework-agnostic, adapter-free accounting domain block.
It provides a minimal, strict double-entry general ledger suitable for property-management,
small-business, and SaaS-billing scenarios, along with a QuickBooks-compatible export path for
back-office reconciliation.

The block is deliberately narrow. It is the canonical place for shape-of-the-ledger types
(`GLAccount`, `JournalEntry`, `DepreciationSchedule`) and for the posting primitive that
enforces balance. It is not an accounting UI, not a billing engine, and not a reconciliation
tool — those are expected to sit above the block in consumer apps.

The block ships four things:

- A **chart of accounts** (`GLAccount`) classified by `GLAccountType` (Asset, Liability, Equity,
  Revenue, Expense), with optional parent references for hierarchical charts.
- A **journal-entry engine** that enforces double-entry invariants at construction time —
  imbalanced entries are rejected, mixed debit/credit lines are rejected, and zero-both lines
  are rejected.
- A **depreciation-schedule registration** surface for fixed assets. The configuration is stored
  this pass; schedule-line computation is deferred.
- A **QuickBooks IIF exporter** (`IQuickBooksJournalEntryExporter`) that produces a textual
  `TRNS` / `SPL` / `ENDTRNS` stream suitable for QuickBooks Desktop's **File → Utilities →
  Import → IIF Files** flow.

## Package

- Package: `Sunfish.Blocks.Accounting`
- Source: `packages/blocks-accounting/`
- Namespace roots: `Sunfish.Blocks.Accounting.Models`, `Sunfish.Blocks.Accounting.Services`,
  `Sunfish.Blocks.Accounting.DependencyInjection`

## When to use it

Use `Sunfish.Blocks.Accounting` when your application needs:

- A canonical shape for `GLAccount`, `JournalEntry`, and `DepreciationSchedule` records to
  store locally or persist into any host database.
- A balance-enforcing journal-posting primitive you can call from higher-level domain code
  (rent collection, invoicing, payroll).
- A one-way export path to QuickBooks that does not require the QuickBooks SDK or an OAuth
  connection to QuickBooks Online.

Do **not** use this block when you need a full ledger UI, Xero export, or automatic
event-driven JE generation — those are explicitly deferred follow-ups called out in
`IAccountingService` remarks.

## Key entities and services

- `GLAccount` / `GLAccountType` — chart of accounts.
- `JournalEntry` / `JournalEntryLine` — balanced double-entry records.
- `DepreciationSchedule` / `DepreciationMethod` — passive asset-depreciation configuration.
- `IAccountingService` — the core write/read contract.
- `IQuickBooksJournalEntryExporter` / `QuickBooksIifExporter` — the IIF export path.

See [entity-model.md](entity-model.md), [service-contract.md](service-contract.md), and
[quickbooks-iif-export.md](quickbooks-iif-export.md) for details.

## DI wiring

Register the default in-memory implementation plus the IIF exporter from your host's
composition root:

```csharp
using Sunfish.Blocks.Accounting.DependencyInjection;

services.AddInMemoryAccounting();
```

This registers `InMemoryAccountingService` as the singleton `IAccountingService` and
`QuickBooksIifExporter` as the singleton `IQuickBooksJournalEntryExporter`. Swap the service
registration for a persistence-backed implementation in production; the IIF exporter is
stateless and can be kept as-is.

## Minimal end-to-end example

The following round-trip mirrors the happy-path flow covered by `AccountingServiceTests` and
`QuickBooksIifExporterTests`:

```csharp
using Sunfish.Blocks.Accounting.Models;
using Sunfish.Blocks.Accounting.Services;

var accounting = serviceProvider.GetRequiredService<IAccountingService>();
var exporter   = serviceProvider.GetRequiredService<IQuickBooksJournalEntryExporter>();

// 1. Seed the chart
var cash = await accounting.CreateAccountAsync(
    new CreateGLAccountRequest("1000", "Cash", GLAccountType.Asset));
var revenue = await accounting.CreateAccountAsync(
    new CreateGLAccountRequest("4000", "Rental Revenue", GLAccountType.Revenue));

// 2. Post a balanced rent receipt
var lines = new List<JournalEntryLine>
{
    new(cash.Id,    debit: 1200m, credit: 0m),
    new(revenue.Id, debit: 0m,    credit: 1200m),
};
await accounting.PostEntryAsync(new PostJournalEntryRequest(
    EntryDate: new DateOnly(2026, 4, 15),
    Memo: "April rent — unit 3B",
    Lines: lines,
    SourceReference: "rent-payment:INV-007"));

// 3. Export the month to QuickBooks
var entries = new List<JournalEntry>();
await foreach (var e in accounting.ListEntriesAsync(
    new ListEntriesQuery(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30))))
{
    entries.Add(e);
}
var iif = exporter.Export(entries, new QuickBooksExportOptions());
```

## Operational invariants at a glance

| Invariant | Enforced by | On violation |
|---|---|---|
| Total debits equal total credits per entry. | `JournalEntry` constructor. | `ArgumentException`. |
| Each line has exactly one non-zero side. | `JournalEntryLine` constructor. | `ArgumentException`. |
| Amounts are non-negative. | `JournalEntryLine` constructor. | `ArgumentException`. |
| Account codes are unique per service instance. | `CreateAccountAsync`. | `InvalidOperationException`. |
| Referenced accounts exist when posting. | `PostEntryAsync`. | `KeyNotFoundException`. |

## Related ADRs

- ADR 0015 — Module-Entity Registration (future: when a persistence-backed accounting service
  lands, its entity module follows this pattern).
- ADR 0013 — Foundation.Integrations (QuickBooks is an external integration surface; IIF is
  file-based and so bypasses the credential/webhook path).
- ADR 0022 — Example catalog + docs taxonomy (this block's docs live under the canonical
  `block-accounting-*` UID prefix).

## Deferred follow-ups

Explicit non-goals for this first pass, tracked for later work:

- Automatic JE generation from payment events (requires event-bus wiring).
- Schedule-line computation (`ComputeScheduleAsync` stub documented on
  `IAccountingService.RegisterScheduleAsync`).
- Xero export (parallel to the QuickBooks path).
- A ledger UI Blazor block over this service — the kitchen-sink demo uses the raw service surface.

## Related pages

- [Entity Model](entity-model.md)
- [Service Contract](service-contract.md)
- [QuickBooks IIF Export](quickbooks-iif-export.md)
