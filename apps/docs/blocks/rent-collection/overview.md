---
uid: block-rent-collection-overview
title: Rent Collection — Overview
description: Introduction to the blocks-rent-collection package — rent schedules, invoices, and payment tracking.
---

# Rent Collection — Overview

## Overview

The `blocks-rent-collection` package provides a self-contained building block for tracking recurring rent obligations, generating invoices from those schedules, and recording payments against them. It sits at the composition layer of the Sunfish stack: the core service contract (`IRentCollectionService`) is framework-agnostic, while the Blazor-shaped `RentLedgerBlock` renders a read-only ledger over the same data.

The block is designed to be usable the moment it is referenced — an `AddInMemoryRentCollection()` call wires a thread-safe in-memory service that is suitable for demos, kitchen-sink pages, and tests. Replace the registration with a persistence-backed implementation when you are ready to ship.

## Package path

`packages/blocks-rent-collection` — assembly `Sunfish.Blocks.RentCollection`.

## When to use it

- You need a minimal, opinionated "rent schedule → invoice → payment" ledger without pulling in a full accounting block.
- You are standing up a property-management demo or a real landlord app and want an off-the-shelf invoice model with due-date tracking, aging, and status transitions.
- You want a service contract that decouples the domain (rent schedules, billing frequency, payments) from whichever UI or framework you ship.

If you need journal entries, double-entry accounting, or deep reconciliation, see `blocks-accounting` instead — rent-collection writes *invoices* and *payments*, not ledger transactions.

## Key entities

- **`RentSchedule`** — the recurring billing contract for a lease: monthly amount, due day (1–28), start/end dates, billing frequency.
- **`Invoice`** — one billing event generated from a schedule, with `AmountDue`, `AmountPaid`, a period window, and an `InvoiceStatus`.
- **`Payment`** — a single payment event applied to an invoice; method is a free-form string (`"cash"`, `"check"`, `"ach"`, `"card"`).
- **`LateFeePolicy`** — a passive data record describing grace period, flat/percentage fees, and a cap. Late-fee application is deferred.
- **`BankAccount`** — display-safe metadata only (masked account number, holder name). Real ACH/Plaid integration is deferred.

## Key services

- **`IRentCollectionService`** — the core contract: `CreateScheduleAsync`, `GenerateInvoiceAsync`, `RecordPaymentAsync`, `GetInvoiceAsync`, `ListInvoicesAsync`.
- **`InMemoryRentCollectionService`** — thread-safe in-memory implementation with per-invoice locking around payment recording; suitable for testing and demos.

## Key UI components

- **`RentLedgerBlock`** — a read-only ledger table showing invoices for a given `ListInvoicesQuery`, with computed aging days for unpaid invoices. Payment entry UI is intentionally deferred.

## DI wiring

```csharp
services.AddInMemoryRentCollection();
```

Registers a singleton `IRentCollectionService` backed by `InMemoryRentCollectionService`. Swap the binding for a persistence-backed implementation in production.

## Status and deferred items

Several pieces are intentionally stubbed in this pass:

- `LeaseId` is stored as an opaque `string` until `blocks-leases` (G14) is on main; the two blocks can ship independently.
- Late-fee calculation is *not* applied — `LateFeePolicy` is a passive record.
- Credit-memo logic is not implemented; overpayments are silently absorbed with `InvoiceStatus.Paid`.
- Decimal rounding enforcement is deferred — callers should round values they pass in.
- ACH / Plaid / Stripe integration is deferred.

## Related

- [Ledger Service](ledger-service.md)
- [Deferred Integrations](deferred-integrations.md)
