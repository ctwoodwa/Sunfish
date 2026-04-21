---
uid: block-accounting-overview
title: Accounting — Overview
description: Double-entry GL, journal entries, depreciation schedules, and QuickBooks IIF export for Sunfish-hosted property and business apps.
---

# Accounting — Overview

## What this block is

`Sunfish.Blocks.Accounting` is a framework-agnostic, adapter-free accounting domain block.
It provides a minimal, strict double-entry general ledger suitable for property-management,
small-business, and SaaS-billing scenarios, along with a QuickBooks-compatible export path for
back-office reconciliation.

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
`QuickBooksIifExporter` as `IQuickBooksJournalEntryExporter`. Swap the service registration
for a persistence-backed implementation in production.

## Related ADRs

- ADR 0015 — Module-Entity Registration (future: when a persistence-backed accounting service
  lands, its entity module follows this pattern).
- ADR 0013 — Foundation.Integrations (QuickBooks is an external integration surface; IIF is
  file-based and so bypasses the credential/webhook path).

## Related pages

- [Entity Model](entity-model.md)
- [Service Contract](service-contract.md)
- [QuickBooks IIF Export](quickbooks-iif-export.md)
