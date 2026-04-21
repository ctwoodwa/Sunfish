---
uid: block-rent-collection-ledger-service
title: Rent Collection — Ledger Service
description: IRentCollectionService contract — schedules, invoices, and payments.
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

## Related

- [Overview](overview.md)
- [Deferred Integrations](deferred-integrations.md)
