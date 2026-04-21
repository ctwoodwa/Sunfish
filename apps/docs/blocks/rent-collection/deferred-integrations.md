---
uid: block-rent-collection-deferred-integrations
title: Rent Collection — Deferred Integrations
description: What rent-collection does not do yet — late fees, credit memos, banking, lease linking.
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

## Related

- [Overview](overview.md)
- [Ledger Service](ledger-service.md)
