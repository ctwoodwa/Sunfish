---
uid: block-rent-collection-deferred-integrations
title: Rent Collection — Deferred Integrations
description: What rent-collection does not do yet — late fees, credit memos, banking, lease linking.
keywords:
  - rent-collection
  - deferred
  - late-fees
  - credit-memo
  - lease-linking
  - ach
  - plaid
  - stripe
---

# Rent Collection — Deferred Integrations

## Overview

This page is the honest list of integrations that are modelled but not yet wired, and of features that are intentionally out of scope for the current pass of `blocks-rent-collection`. Every item below is called out in code (XML docs or `TODO` comments) so a future implementer has a trail to follow.

## Late-fee application

`LateFeePolicy` exists as a passive record with `GracePeriodDays`, `FlatFee`, `PercentageFee`, and `CapAmount`. The constructor enforces that at least one of `FlatFee` or `PercentageFee` is provided; beyond that, nothing in the service *applies* a policy to an overdue invoice. There is no scheduled job, no automatic status promotion to `Overdue`, and no generated fee-line invoice.

**Follow-up:** a late-fee processor that walks overdue invoices, applies the policy, and either mutates `AmountDue` or produces sibling fee invoices.

## Credit memos and overpayments

When `AmountPaid >= AmountDue`, the invoice transitions to `InvoiceStatus.Paid` and any overpayment is silently retained in `AmountPaid`. No credit-memo document is produced and there is no mechanism to apply the surplus against a later invoice.

**Follow-up:** implement credit-memo issuance in `RecordPaymentAsync` and a matching credit balance on the schedule or tenant.

## Banking integration (ACH, Plaid, Stripe)

`BankAccount` stores display-safe metadata only — the masked account number (e.g. `"****1234"`), the holder name, and an account kind. There are no raw account or routing numbers, no tokenised Plaid or Stripe identifiers, and no payment-initiation flow. `Payment.Method` is a free-form string by convention (`"cash"`, `"check"`, `"ach"`, `"card"`) because there is no external gateway to validate against.

**Follow-up:** introduce a payment-provider abstraction alongside `IRentCollectionService`, store tokens (not PANs) on `BankAccount`, and make `Method` an enum once the set of supported gateways stabilises.

## Lease linkage (block G14)

`RentSchedule.LeaseId` and `Invoice.LeaseId` are plain `string` fields so that `blocks-rent-collection` and `blocks-leases` (G14) can ship and merge independently. The xmldoc carries a `TODO` to migrate to `Sunfish.Blocks.Leases.Models.LeaseId` once G14 is on main.

**Follow-up:** introduce a strong-typed `LeaseId` reference once `blocks-leases` exposes it.

## Payment entry UI

`RentLedgerBlock.razor` is read-only. There is no form for recording a payment from the ledger UI — consumers must call `IRentCollectionService.RecordPaymentAsync` directly (or build their own UI on top of it).

**Follow-up:** add a "Record payment" affordance to the ledger row, backed by a reusable form block.

## Decimal rounding enforcement

All monetary fields are `decimal` with a two-decimal-place assumption. The service does not round or truncate caller input — pass a value with more decimal places and it will be stored as-is.

**Follow-up:** decide whether to enforce two-decimal precision at the boundary (throw) or round silently (which introduces its own rounding-mode choice).

## Overdue status promotion

`InvoiceStatus.Overdue` is part of the enum but the service does not set it automatically. A consumer that wants an overdue view today needs to filter on `Status == Open` combined with `DueDate < today`, or promote the status in their own read model.

Example projection:

```csharp
static InvoiceStatus PromoteOverdue(Invoice inv)
    => inv.Status == InvoiceStatus.Open && inv.DueDate < DateOnly.FromDateTime(DateTime.UtcNow)
        ? InvoiceStatus.Overdue
        : inv.Status;
```

A scheduled job doing this in bulk is a known follow-up.

## Persistence

The block does not currently ship an `ISunfishEntityModule` (ADR 0015) — there is no EF Core configuration for `RentSchedule`, `Invoice`, or `Payment`. The in-memory service is the only backing store today. Consumers that need durability have two paths:

1. **Fork the service** — implement `IRentCollectionService` against your own persistence layer and replace the DI binding.
2. **Wait for the persistence pass** — an EF Core-backed implementation with an entity module is planned, aligning with the rest of the Sunfish blocks that contribute to the shared Bridge `DbContext`.

**Follow-up:** ship `RentCollectionEntityModule : ISunfishEntityModule` plus `IEntityTypeConfiguration<T>` implementations for the four entities, mirroring the pattern used by `blocks-subscriptions` and `blocks-tenant-admin`.

## Migration plan to strong-typed LeaseId

```csharp
// Today
public sealed record RentSchedule(
    ...
    string LeaseId,          // opaque, documented TODO
    ...);

// After G14 (blocks-leases)
using Sunfish.Blocks.Leases.Models;

public sealed record RentSchedule(
    ...
    LeaseId LeaseId,         // strong-typed reference
    ...);
```

The migration is a breaking change for consumers. It will be routed through `sunfish-api-change` per the ICM pipeline variants and accompanied by a migration note in the release notes.

## Rounding mode decision

The "rounding enforcement is deferred" line in the XML docs is deliberately non-committal. Two paths are viable:

- **Enforce at boundary (throw)** — reject any `decimal` with more than two decimal places in `CreateScheduleRequest.MonthlyAmount`, `RecordPaymentRequest.Amount`, etc. Simple and safe; might surprise callers who passed `1500.000m` from a UI binding.
- **Round silently (banker's rounding)** — accept arbitrary precision and round to two decimals internally using `MidpointRounding.ToEven`. Forgiving, but the rounded value is what ends up persisted and the caller loses the original.

A future decision surfaced in an ADR or release note will lock this in. Until then, callers should pre-round.

## Summary table

| Area | Current pass | Future pass |
|---|---|---|
| Late fees | `LateFeePolicy` record only; no application | Scheduled processor walks overdue invoices; generates fee invoices |
| Credit memos | Overpayment retained in `AmountPaid`, status clamps to `Paid` | Emit credit-memo document; apply surplus to later invoices |
| Banking | Display-safe metadata on `BankAccount`; `Method` is `string` | Payment provider abstraction; tokenised accounts; `Method` enum |
| Lease linkage | `LeaseId` as opaque `string` | Strong-typed reference to `blocks-leases` (G14) |
| Payment entry UI | `RentLedgerBlock` is read-only | Record-payment row affordance + reusable form block |
| Rounding | Unenforced two-decimal assumption | Boundary enforcement (throw) or silent banker's rounding |
| Overdue promotion | Enum value exists; not auto-set | Scheduled job or query-time promotion |
| Persistence | In-memory only; no `ISunfishEntityModule` | EF Core entity module per ADR 0015 |

## Related

- [Overview](overview.md)
- [Ledger Service](ledger-service.md)
- ADR 0015 — `docs/adrs/0015-module-entity-registration.md` (persistence-follow-up target)
