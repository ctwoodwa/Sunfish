# Payments Contracts

`Sunfish.Foundation.Integrations.Payments` is the contract surface for the payments substrate (per [ADR 0051 — Foundation.Integrations.Payments](../../../docs/adrs/0051-foundation-integrations-payments.md)).

**Phase 1 stub status:** This namespace ships `Money` + `CurrencyCode` only. The full ADR 0051 payment-orchestration surface (`IPaymentGateway`, `Charge`, `Capture`, `Refund`, dispute handling, etc.) lands when ADR 0051 Stage 06 is hand-offed.

## Phase 1 stub

Introduced by W#19 Phase 0 (per the W#19 hand-off addendum) so the W#19 Phase 5 `WorkOrder` schema migration could compile with `Money?` cost fields without forcing ADR 0051 Stage 06 to ship first.

| Type | Purpose |
|---|---|
| `Money` | Currency-bound decimal amount (`Amount` + `Currency`). |
| `CurrencyCode` | ISO 4217 currency code (3-letter). |
| `Money.Usd(amount)` | USD shorthand factory. |
| `CurrencyCode.USD` | The US dollar singleton. |

```csharp
var rent = Money.Usd(2000m);
// rent.Amount == 2000m, rent.Currency == CurrencyCode.USD
```

ADR 0051 Stage 06 will extend `Money` with operator overloads (+, -, ==), banker's-rounding helpers, validation (NaN / Infinity rejection), and the full ISO 4217 allow-list. The struct's shape is forward-compatible — Stage 06 only **extends**.

## Cross-substrate consumers

- **W#19 Work Orders** — `WorkOrder.EstimatedCost: Money?` + `WorkOrder.TotalCost: Money?` (Phase 5 schema migration).
- **W#28 Public Listings** — `PublicListing.AskingRent: Money?`.
- **W#22 Leasing Pipeline** — `Application.ApplicationFee: Money` (when it ships).
- **W#27 Leases** — eventual `Lease.MonthlyRent: Money` migration (current type is `decimal`; not yet migrated).

## See also

- [ADR 0051](../../../docs/adrs/0051-foundation-integrations-payments.md)
- [W#19 hand-off addendum](../../../icm/_state/handoffs/property-work-orders-stage06-addendum.md) — defines the Phase 0 stub
