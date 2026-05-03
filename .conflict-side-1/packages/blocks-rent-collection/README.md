# Sunfish.Blocks.RentCollection

Rent-collection block — entities + ledger service + `RentLedgerBlock` Razor component.

**First pass — defers Plaid / Stripe / event-bus integrations.**

## What this ships

### Models

- **`Invoice`** + `InvoiceId` + `InvoiceStatus` — rent invoice with billing period + amount due + status (Draft / Issued / Paid / PartiallyPaid / Overdue / Voided).
- **`Payment`** — payment-application record (which invoice(s) the payment satisfied).
- **`BankAccount`** + `BankAccountId` + `BankAccountKind` — payer/payee bank-account stub (production wires through `Sunfish.Foundation.Integrations.Payments`).
- **`BillingFrequency`** — enum (Monthly / Quarterly / Annual / Custom).
- **`LateFeePolicy`** — late-fee schedule (grace period + flat fee + percentage cap).
- **`RentLedgerEntry`** — append-only ledger entry tying invoices + payments + adjustments.

### Services

- **`IRentLedgerService`** + `InMemoryRentLedgerService` — invoice generation + payment application + ledger projection + balance computation.

### UI

- **`RentLedgerBlock.razor`** — read-display ledger view per tenant + per lease.

## DI

```csharp
services.AddInMemoryRentCollection();
```

## Deferred follow-ups

- Plaid integration for ACH bank-account verification + auto-debit
- Stripe integration for card-on-file rent payments
- Event-bus integration (currently in-process; future will dispatch `RentInvoiceIssued` / `RentPaymentApplied` events)
- Late-fee auto-application engine (currently the policy is recorded; application is caller-supplied)

## ADR map

- [ADR 0051](../../docs/adrs/0051-foundation-integrations-payments.md) — payments substrate (production rent collection wires through this)
- [ADR 0015](../../docs/adrs/0015-module-entity-registration.md) — module-entity registration

## See also

- [apps/docs Overview](../../apps/docs/blocks/rent-collection/overview.md)
- [Sunfish.Blocks.Accounting](../blocks-accounting/README.md) — GL/JE downstream consumer (rent payments roll up to JEs)
- [Sunfish.Blocks.Leases](../blocks-leases/README.md) — `Lease.RentAmount` is the upstream amount source
