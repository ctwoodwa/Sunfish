# blocks-financial-ap

Accounts-payable substrate for Sunfish. Mirror of `blocks-financial-ar`'s shape with vendor-side differences: single AP control account, vendor-supplied bill numbers, optional approval gate, Disputed hold.

## Why this block exists

Every property-management install needs a place to record bills from vendors (utilities, maintenance crews, supply purchases), track which ones are approved + paid + outstanding, and project cash-flow obligations. Without canonical AP, every workflow (bill capture, approval, payment) re-invents the same entity model and the data never reconciles to the GL. This cluster is that canonical home.

## Domain shape

```text
Bill (1) ── (N) BillLine ── (1) GLAccount (Expense / Asset)
Bill (1) ── (1) Party (vendor role)
Bill (1) ── (1) GLAccount (AP control account — credits the total)
Bill (1) ── (0..1) JournalEntry (record)
Bill (1) ── (0..1) JournalEntry (void reversal)
```

## Entities

| Entity | Shape |
|---|---|
| **`Bill`** | `BillId`, `TenantId`, `ChartId`, `BillNumber` (vendor's own), `VendorId: PartyId`, `BillDate`, `DueDate`, `ReceivedDate`, `Currency`, `Lines`, cached totals, `Status`, `ApAccountId`, `ApprovedByUserId?`, `ApprovedAtUtc?`, `JournalEntryId?`, `VoidedByEntryId?`, `ExternalRef`, CRDT envelope. |
| **`BillLine`** | `Id`, `BillId`, `LineNumber`, `Description`, `Quantity`, `UnitPrice`, `Amount` (banker's-rounded), `DebitAccountId` (Expense/Asset), `TaxCodeId?` (opaque), `TaxAmount`, `PropertyId?`, `ClassificationId?`, `Notes?`. |

## Status lifecycle

| Status | Meaning |
|---|---|
| `Draft` | Editable; not yet acknowledged by AP. |
| `Received` | Recorded in AP — `Dr Expense / Cr AP` posted to the GL. |
| `Approved` | Approved for payment per workflow policy. Required when `ApprovalThreshold` is set and the bill is at/over threshold. |
| `PartiallyPaid` | One or more payments applied; `Balance > 0`. |
| `Paid` | Fully paid; terminal. |
| `Voided` | Reversed via a void JE; terminal. |
| `Disputed` | Hold — excluded from `IsOpen` / `IsPayable` / AP aging. Resolves back to `Received` or `Approved`. |

`Overdue` is **derived** at read-time from `(IsOpen() ∧ today > DueDate ∧ Balance > 0)` — not stored.

## Transitions

```text
Draft         → Received | Voided
Received      → Approved | PartiallyPaid | Paid | Voided | Disputed
Approved      → PartiallyPaid | Paid | Voided | Disputed
PartiallyPaid → Paid | Voided | Disputed
Disputed      → Received | Approved          (dispute resolves back)
Paid, Voided                                  (terminal)
```

Notably:
- **Disputed → Paid is forbidden** — the dispute must resolve back to a normal payable state first.
- **Received → PartiallyPaid is allowed** without going through `Approved` (when approval policy doesn't gate).

## Services

```csharp
public interface IBillRepository { … }     // CRUD + vendor-scoped + external-ref + open-only
public interface IBillPostingService { … } // Record / Void / Approve / Dispute / ResolveDispute
public interface IApAgingService { … }     // Per-chart / Vendor / Property aging snapshots
public interface IErpnextPurchaseInvoiceImporter { … } // ERPNext Pass 2
```

All in-memory implementations ship in v1. Persistence-backed implementations land in the follow-on substrate hand-off.

## Posting algorithm

`IBillPostingService.RecordAsync` (Draft → Received):

1. Validate Draft + non-empty lines.
2. Compute per-line tax via `ITaxCalculator`.
3. Build balanced JE: **Debit each line's `DebitAccountId`** for the line amount, **Credit `ApAccountId`** for the total. Tax (when present) routes a zero-net placeholder pair on the AP account until the tax-bridge adapter ships.
4. Call `IJournalPostingService.PostAsync`.
5. Persist updated bill (Status, JE id, totals), emit `Financial.BillRecorded`.

Idempotent: re-recording an already-Received bill with a `JournalEntryId` returns the existing record without re-posting or duplicate event.

`VoidAsync` builds a reversal (debits/credits swapped) and emits `Financial.BillVoided`.

`DisputeAsync` **does not touch the GL** — it's a hold. Bill is flipped to `Disputed`; aging + payment-applicable queries exclude it.

`ResolveDisputeAsync` flips back to `Received` or `Approved` (caller chooses). Other targets are rejected with `InvalidResolutionTarget`.

`ApproveAsync` stamps `ApprovedByUserId` + `ApprovedAtUtc` and emits `Financial.BillApproved`. Only Received bills are approvable.

## Aging

`IApAgingService` mirrors `IArAgingService` — five buckets (`current` / `0-30` / `31-60` / `61-90` / `90+`), per-chart/vendor/property scopes. **Disputed bills are excluded** since they don't satisfy `IsOpen()`.

## Events

`InvoicePostingService` (well, `BillPostingService`) emits five canonical event types via `IDomainEventPublisher`:

| Event | Triggered by |
|---|---|
| `Financial.BillRecorded` | `RecordAsync` (Draft → Received) |
| `Financial.BillVoided` | `VoidAsync` |
| `Financial.BillApproved` | `ApproveAsync` (Received → Approved) |
| `Financial.BillDisputed` | `DisputeAsync` |
| `Financial.DisputeResolved` | `ResolveDisputeAsync` |

Idempotency keys: `{action}:{billId}`.

## ERPNext migration importer

`IErpnextPurchaseInvoiceImporter` upserts ERPNext `Purchase Invoice` records into the canonical AP substrate. Idempotent on `(Name, Modified)`:

- ExternalRef: `erpnext:pinv:{Name}`
- `erpnextModified:{Modified}` marker in `Notes`

`BillNumber` comes from ERPNext's `bill_no` (vendor's own number); falls back to `name` when blank. Line `expense_account` falls back to the importer's `defaultExpenseAccountId` when null. Cost-center maps to canonical `PropertyId`.

Status mapping mirrors the AR sales-invoice importer (`Submitted` / `Overdue` / `Return` / `Debit Note Issued` → `Received`; `Partly Paid` → `PartiallyPaid`; `Paid` → `Paid`; `Cancelled` → `Voided`).

Customer / supplier resolution is **upstream**: the importer takes `vendorPartyId` as a parameter, assuming `blocks-people-foundation.IErpnextPartyImporter` (Pass 1) already ran.

## DI

```csharp
services.AddBlocksFinancialAp(options => options.ApprovalThreshold = 5000m);
```

Registers `BlocksFinancialApOptions`, `IBillRepository`, `ITaxCalculator` (NoOp), `IDomainEventPublisher` (Noop default), `IBillPostingService`, `IApAgingService`, `IErpnextPurchaseInvoiceImporter`. All via `TryAddSingleton` — hosts that register a real publisher / persistence layer / tax-bridge upstream are unaffected.

## What's not here (deferred)

| Concern | Where |
|---|---|
| Persistence backend | substrate hand-off; in-memory is plenty for v1 desktop seed |
| Real tax calculation | tax-bridge adapter that delegates to `Sunfish.Blocks.FinancialTax.Services.ITaxCalculationService` |
| Vendor 1099 tracking | future workstream |
| Recurring bills | future workstream |
| Payment application | future `blocks-financial-payments` cluster — emits to `Bill` via `Financial.PaymentApplied` events |
