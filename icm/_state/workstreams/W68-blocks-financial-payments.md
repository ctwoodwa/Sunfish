---
sort_order: 77
number: 68
slug: blocks-financial-payments
title: "W#68 — blocks-financial-payments: Payment + PaymentApplication (Phase 2 financial cluster)"
status: "ready-to-build"
status_cell: "`ready-to-build` — gated on `blocks-financial-ar` + `blocks-financial-ap` all PRs merged; hand-off at `icm/_state/handoffs/blocks-financial-payments-stage06-handoff.md`; 4 PRs; ~8-12h; security spot-check on PR 3"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/02_architecture/blocks-financial-schema-design.md` §3.9–§3.10 (Payment + PaymentApplication) + §6.1 (posting algorithm) + §10.3 (importPaymentEntry) + `icm/_state/handoffs/blocks-financial-payments-stage06-handoff.md`"
---

## Notes

**Phase 1 critical-path position.** `blocks-financial-payments` is the final entity package in the Phase 2 financial cluster:

```
blocks-financial-ledger    (Chart + Journal core)                  ✓ shipped
blocks-financial-periods   (FiscalYear + FiscalPeriod)             ✓ shipped
blocks-financial-tax       (TaxCode + TaxRate + TaxJurisdiction)   ✓ shipped
blocks-financial-ar        (Invoice + InvoiceLine)                 ✓ shipped
blocks-financial-ap        (Bill + BillLine)                       in-progress (dev; PRs 1-4)
blocks-financial-payments  ← THIS WORKSTREAM                       gated on AP completion
```

**What it ships.** Per spec §3.9–§3.10:

- `Payment` record entity — models a cash movement (Inbound from customer; Outbound to vendor). Has `PaymentDirection`, `PaymentMethod`, `PaymentStatus`, `UnappliedAmount` invariant, references the GL via `JournalEntryId`.
- `PaymentApplication` record entity — many-to-many join between Payment and Invoice/Bill. Carries `AmountApplied`, `DiscountAmount`, `WriteoffAmount`; immutable once created (correct by unapply + re-apply).
- `IPaymentRepository` + `InMemoryPaymentRepository`
- `IPaymentApplicationRepository` + `InMemoryPaymentApplicationRepository`
- `IPaymentPostingService` (PR 2) — clear/bounce/void → GL; delegates to `IJournalPostingService`
- `IPaymentApplicationService` (PR 3) — apply/unapply; direction-matching invariant enforced; security spot-check required before auto-merge
- `IErpnextPaymentEntryImporter` (PR 4) — completes Pass 4 of the ERPNext migration importer (`importPaymentEntry`)

**Direction-matching invariant (critical financial correctness gate):** Inbound payments MUST only be applied to Invoices (AR); Outbound payments MUST only be applied to Bills (AP). Mismatches are rejected as `ApplyError.DirectionMismatch`. This is the first guard in `IPaymentApplicationService.ApplyAsync`, before any repository lookup.

**Why security spot-check on PR 3.** Direction-matching is analogous to a payment-routing invariant — bypassing it would allow a vendor payment to reduce a customer's invoice balance (silent financial corruption). XO will run a security spot-check on `DefaultPaymentApplicationService` before PR 3 auto-merges.

**Consumers unblocked.** `blocks-reports-ap-aging` (full cashflow: AR aging + AP aging + cash positions); `tooling-anchor-import` (Pass 4 complete, full ERPNext migration pipeline closes); tenant-ledger view (payment.appliedDate for monthly reconciliation).

**Attribution.** Apache OFBiz (Apache 2.0) — payment-application entity pattern; NOTICE entry required in package root.
