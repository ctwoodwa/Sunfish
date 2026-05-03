---
uid: block-rent-collection-ledger-service
title: Rent Collection — Ledger Service
description: IRentCollectionService contract — schedules, invoices, and payments.
keywords:
  - rent-collection
  - service-contract
  - invoice-lifecycle
  - payment-recording
  - concurrency
---

# Rent Collection — Ledger Service

## Overview

`IRentCollectionService` is the core contract for the rent-collection domain. It captures the full write and read surface the block needs: creating schedules, generating invoices from them, recording payments against invoices, and querying invoice state.

Namespace: `Sunfish.Blocks.RentCollection.Services`.

## Methods

| Method | Purpose |
|---|---|
| `CreateScheduleAsync(CreateScheduleRequest, ct)` | Creates and persists a new `RentSchedule`. Throws `ArgumentException` when `DueDayOfMonth` is outside 1–28. |
| `GenerateInvoiceAsync(RentScheduleId, DateOnly periodStart, ct)` | Creates an `Invoice` in `InvoiceStatus.Open` for the given period. Period end and due date are computed from the schedule's `Frequency` and `DueDayOfMonth`. Throws `KeyNotFoundException` when the schedule is unknown. |
| `RecordPaymentAsync(RecordPaymentRequest, ct)` | Appends a `Payment`, updates the invoice's `AmountPaid` and `Status` atomically. Per-invoice locking serialises concurrent payments. Throws `KeyNotFoundException` when the invoice is unknown. |
| `GetInvoiceAsync(InvoiceId, ct)` | Returns a single invoice or `null`. |
| `ListInvoicesAsync(ListInvoicesQuery, ct)` | Streams invoices matching the optional filter. All filter fields are additive and `null` means "no constraint". |

## Request and query DTOs

**`CreateScheduleRequest`**

```csharp
public sealed record CreateScheduleRequest(
    string LeaseId,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal MonthlyAmount,
    int DueDayOfMonth,
    BillingFrequency Frequency = BillingFrequency.Monthly);
```

`DueDayOfMonth` is capped at 28 to avoid ambiguity across February and 30-day months. `BillingFrequency` supports `Monthly`, `BiMonthly`, `Quarterly`, and `Annually`.

**`RecordPaymentRequest`**

```csharp
public sealed record RecordPaymentRequest(
    InvoiceId InvoiceId,
    decimal Amount,
    Instant? PaidAtUtc,
    string Method,
    string? Reference = null);
```

`PaidAtUtc` defaults to `Instant.Now` when `null`. `Method` is a free-form convention string (`"cash"`, `"check"`, `"ach"`, `"card"`); there is no enum.

**`ListInvoicesQuery`** — optional filter with `LeaseId`, `ScheduleId`, `Status`, `DueBefore`, `DueAfter`. An empty query returns every invoice.

## Invoice lifecycle

`InvoiceStatus` progresses through:

- `Draft` — generated but not issued. The in-memory implementation creates invoices directly in `Open` today.
- `Open` — awaiting payment.
- `PartiallyPaid` — at least one payment received, balance not cleared.
- `Paid` — `AmountPaid >= AmountDue`. Overpayments are absorbed silently in this pass.
- `Overdue` — past due with no full payment. Not set automatically — consumers may promote `Open` → `Overdue` during queries.
- `Cancelled` — voided; do not collect.

Status is recomputed inside `RecordPaymentAsync` via `ComputeStatus(AmountDue, AmountPaid)` and clamps to `Paid` on overpayment.

## Typical workflow

```csharp
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
    PaidAtUtc: null,
    Method: "ach"));
```

## Concurrency

`InMemoryRentCollectionService` uses a `ConcurrentDictionary` per entity type plus a per-invoice `SemaphoreSlim` to serialise concurrent `RecordPaymentAsync` calls on the same invoice. Different invoices proceed fully in parallel.

## Precision note

All monetary fields (`MonthlyAmount`, `AmountDue`, `AmountPaid`, `Amount`, fee amounts) are `decimal` with a two-decimal-place assumption. The service does not round or truncate — callers must pre-round values if strict two-decimal precision matters to downstream code.

## Period math

`GenerateInvoiceAsync` computes the period end and due date from the schedule's `Frequency` and `DueDayOfMonth`:

- **Monthly** — `PeriodEnd = last day of PeriodStart's month` (e.g. March 2025 → `2025-03-31`).
- **BiMonthly** — covers two calendar months from `PeriodStart`.
- **Quarterly** — covers three calendar months.
- **Annually** — covers twelve calendar months.

`DueDate` is always `new DateOnly(PeriodStart.Year, PeriodStart.Month, schedule.DueDayOfMonth)` — aligned to the schedule's due day, in the first month of the period. Because `DueDayOfMonth` is capped at 28 in `CreateScheduleAsync`, the `DateOnly` construction is safe for every month including February.

## Blazor-side usage — `RentLedgerBlock`

The `RentLedgerBlock` is purely read-display. It takes a `[Parameter] ListInvoicesQuery Query` and calls `IRentCollectionService.ListInvoicesAsync` on init, presenting the results as a table with columns for period, due date, amount due, amount paid, status, and aging days (for open/overdue invoices). Payment entry is not in the UI yet; consumers who want to add a payment from the ledger should wire their own form or button into an `ItemTemplate`-like hook (planned follow-up).

```razor
@using Sunfish.Blocks.RentCollection.Services

<RentLedgerBlock Query="@_query" />

@code {
    private ListInvoicesQuery _query = new()
    {
        LeaseId = "lease-123",
        Status = null,   // no status filter
    };
}
```

## Error model

| Call | Condition | Exception |
|---|---|---|
| `CreateScheduleAsync` | `DueDayOfMonth < 1 \|\| > 28` | `ArgumentException` |
| `GenerateInvoiceAsync` | schedule id not found | `KeyNotFoundException` |
| `RecordPaymentAsync` | invoice id not found | `KeyNotFoundException` |
| `RecordPaymentAsync` | cancelled via `CancellationToken` | `OperationCanceledException` |

There are no business-rule exceptions beyond the ones above — overpayment is silently absorbed, zero-amount payments are recorded as-is, and late-fee evaluation is not performed. Consumers that want those behaviours layer them on top.

## Concurrency test (from `tests/RentCollectionServiceTests.cs`)

```csharp
[Fact]
public async Task RecordPaymentAsync_ConcurrentPayments_SerializedCorrectly()
{
    var svc = CreateService();
    var (_, invoice) = await CreateScheduleAndInvoice(svc, amountDue: 1000m);

    // Fire 10 concurrent payments of 100 each — total should be exactly 1000.
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => svc.RecordPaymentAsync(new RecordPaymentRequest(
            InvoiceId: invoice.Id,
            Amount: 100m,
            PaidAtUtc: null,
            Method: "cash")).AsTask())
        .ToArray();

    await Task.WhenAll(tasks);

    var updated = await svc.GetInvoiceAsync(invoice.Id);
    Assert.Equal(1000m, updated!.AmountPaid);
    Assert.Equal(InvoiceStatus.Paid, updated.Status);
}
```

The per-invoice `SemaphoreSlim` prevents lost-update races. Invoices for different leases proceed entirely in parallel.

## Troubleshooting

- **"DueDayOfMonth must be between 1 and 28"** — supply a value in range; there is no "last day of the month" sentinel.
- **Overpayment stayed on the invoice** — expected. `AmountPaid` is preserved verbatim so audit trails are not lost; credit-memo issuance is a follow-up.
- **`ListInvoicesAsync` returned stale data** — the in-memory implementation reads from a `ConcurrentDictionary` snapshot; if you just wrote via another service instance, ensure you share the singleton.

## Related

- [Overview](overview.md)
- [Deferred Integrations](deferred-integrations.md)
