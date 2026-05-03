# Sunfish.Kernel.Ledger

Event-sourced double-entry ledger — kernel subsystem for financial and CP-class value records.

## Paper cross-reference

Implements [local-node-architecture-paper.md](../../_shared/product/local-node-architecture-paper.md) §12:

- **§12.1 Double-Entry Ledger as a First-Class Subsystem** — `Posting`, `Transaction` (invariants: ≥2 postings, sum to zero, immutable).
- **§12.2 Posting Engine and Idempotency** — `IPostingEngine` / `PostingEngine`, lease-coordinated via `ILeaseCoordinator`.
- **§12.3 CQRS Read Models** — `IBalanceProjection` / `BalanceProjection`, `IStatementProjection` / `StatementProjection`, rebuildable from the event stream.
- **§12.4 Closing the Books and Period Snapshots** — `IPeriodCloser` / `PeriodCloser`, adjustment-account routing.

## Dependencies

- [`Sunfish.Kernel.EventBus`](../kernel-event-bus/README.md) — `IEventLog` durability receipts.
- [`Sunfish.Kernel.Lease`](../kernel-lease/README.md) — per-account CP-class serialization.

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Kernel.Events.DependencyInjection;
using Sunfish.Kernel.Lease.DependencyInjection;
using Sunfish.Kernel.Ledger;
using Sunfish.Kernel.Ledger.CQRS;
using Sunfish.Kernel.Ledger.DependencyInjection;
using Sunfish.Kernel.Ledger.Periods;

var services = new ServiceCollection()
    .AddSunfishEventLog()
    .AddSunfishKernelLease(localNodeId: "node-a")
    .AddSunfishKernelLedger()
    .BuildServiceProvider();

var engine = services.GetRequiredService<IPostingEngine>();

// Post a balanced transaction (sum must equal 0m).
var txId = Guid.NewGuid();
var tx = new Transaction(
    TransactionId: txId,
    IdempotencyKey: "invoice-42",
    Postings: new[]
    {
        new Posting(Guid.NewGuid(), txId, "cash",     +100m, "USD", DateTimeOffset.UtcNow, "invoice 42", new Dictionary<string,string>()),
        new Posting(Guid.NewGuid(), txId, "revenue",  -100m, "USD", DateTimeOffset.UtcNow, "invoice 42", new Dictionary<string,string>()),
    },
    CreatedAt: DateTimeOffset.UtcNow);

var result = await engine.PostAsync(tx, default);
// result.Success == true, result.LogSequence non-null.

// Query balance via the CQRS projection.
var balances = services.GetRequiredService<IBalanceProjection>();
var cashBalance = await balances.GetBalanceAsync("cash", asOf: null, default);

// Close a period.
var closer = services.GetRequiredService<IPeriodCloser>();
await closer.CloseAsync(new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.Zero), default);
```

## Design notes

- **Sum-to-zero invariant:** enforced with `decimal` equality (no floating-point tolerance).
- **Idempotency:** in-memory key→tx-id index; replays the ledger event stream on construction.
- **Lease scope:** one lease per *affected account* (sorted ordinal), not one global lease per transaction — lets disjoint-account transactions commit in parallel while preserving CP-class serialization on the contested account.
- **Compensations:** never mutate the original; emit a reversing `Transaction` via `CompensateAsync`.
- **Closed-period writes:** rewritten to `adjustments-yyyyMMdd` in the next open period.

## Out of scope for this wave

- Refactoring `packages/blocks-accounting/` onto this kernel — separate follow-up task.
- Durable / distributed event backend for the ledger event stream — currently in-process only; `IEventLog` is used for the durability receipt.
