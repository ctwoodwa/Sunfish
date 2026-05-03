---
uid: block-rent-collection-overview
title: Rent Collection — Overview
description: Introduction to the blocks-rent-collection package — rent schedules, invoices, and payment tracking.
keywords:
  - rent
  - invoice
  - payment
  - ledger
  - landlord
  - property-management
  - sunfish-blocks
---

# Rent Collection — Overview

## Overview

The `blocks-rent-collection` package provides a self-contained building block for tracking recurring rent obligations, generating invoices from those schedules, and recording payments against them. It sits at the composition layer of the Sunfish stack: the core service contract (`IRentCollectionService`) is framework-agnostic, while the Blazor-shaped `RentLedgerBlock` renders a read-only ledger over the same data.

The block is designed to be usable the moment it is referenced — an `AddInMemoryRentCollection()` call wires a thread-safe in-memory service that is suitable for demos, kitchen-sink pages, and tests. Replace the registration with a persistence-backed implementation when you are ready to ship.

## Positioning within Sunfish

`blocks-rent-collection` is one of the property-management-flavoured composition blocks in the Sunfish catalog. It depends on `foundation` (for the `Instant` primitive and strong-typed id generator) and nothing else — no accounting block, no leasing block, no external billing gateway. Consumers that want a richer posture combine it with:

- `blocks-accounting` — when journal-entry bookkeeping should accompany the invoice/payment trail.
- `blocks-leases` (G14, forthcoming) — when `LeaseId` should be strong-typed rather than an opaque `string`.
- `blocks-tax-reporting` — when the year's rent receipts should feed into Schedule E line items.

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

See [Deferred Integrations](deferred-integrations.md) for the follow-up notes.

## End-to-end example

```csharp
using Sunfish.Blocks.RentCollection.Models;
using Sunfish.Blocks.RentCollection.Services;

// Host wiring
services.AddInMemoryRentCollection();

// Later, inside a handler or page
var svc = sp.GetRequiredService<IRentCollectionService>();

var schedule = await svc.CreateScheduleAsync(new CreateScheduleRequest(
    LeaseId: "lease-123",
    StartDate: new DateOnly(2026, 1, 1),
    EndDate: null,
    MonthlyAmount: 1500m,
    DueDayOfMonth: 1,
    Frequency: BillingFrequency.Monthly));

var invoice = await svc.GenerateInvoiceAsync(
    schedule.Id,
    periodStart: new DateOnly(2026, 1, 1));

await svc.RecordPaymentAsync(new RecordPaymentRequest(
    InvoiceId: invoice.Id,
    Amount: 1500m,
    PaidAtUtc: null,   // defaults to Instant.Now
    Method: "ach",
    Reference: "ACH-2026-001"));

var reload = await svc.GetInvoiceAsync(invoice.Id);
// reload.Status is now InvoiceStatus.Paid
```

## Where things live in the package

| Path (under `packages/blocks-rent-collection/`) | Purpose |
|---|---|
| `Models/RentSchedule.cs` | Recurring billing contract entity. |
| `Models/Invoice.cs` | Per-period billing record. |
| `Models/Payment.cs` | Payment event entity. |
| `Models/LateFeePolicy.cs` | Passive policy record. |
| `Models/BankAccount.cs` | Display-safe bank metadata only. |
| `Services/IRentCollectionService.cs` | Core framework-agnostic contract. |
| `Services/InMemoryRentCollectionService.cs` | Thread-safe in-memory implementation. |
| `DependencyInjection/RentCollectionServiceCollectionExtensions.cs` | `AddInMemoryRentCollection` extension. |
| `RentLedgerBlock.razor` | Read-only ledger table (Blazor). |
| `tests/RentCollectionServiceTests.cs` | Service behaviour including concurrency fixture. |
| `tests/RentLedgerBlockTests.cs` | bUnit component tests. |

## Tests as executable spec

The service tests double as executable specification for edge cases. Notable fixtures in `tests/RentCollectionServiceTests.cs`:

- `CreateScheduleAsync_RejectsDueDayOfMonthOutOfRange` — 0, 29, −1, 100 all throw `ArgumentException`.
- `CreateScheduleAsync_AcceptsDueDayOfMonthInRange` — 1, 15, 28 pass.
- `GenerateInvoiceAsync_MonthlyFrequency_ComputesCorrectPeriodDates` — period-end and due-date math for 31-day months.
- `RecordPaymentAsync_PartialPayment_SetsPartiallyPaidStatus` — status transitions mid-balance.
- `RecordPaymentAsync_Overpayment_SetsPaidStatus` — overpayments clamp to `Paid` with no credit-memo.
- `RecordPaymentAsync_ConcurrentPayments_SerializedCorrectly` — 10 parallel `$100` payments total exactly `$1000`.
- `LateFeePolicy_RejectsNeitherFlatNorPercentage` — the policy constructor rejects empty policies.

## Related

- [Ledger Service](ledger-service.md)
- [Deferred Integrations](deferred-integrations.md)
- ADR 0022 — `docs/adrs/0022-example-catalog-and-docs-taxonomy.md` (canonical docs taxonomy)
- Sibling block: [blocks-accounting](../accounting/overview.md)
