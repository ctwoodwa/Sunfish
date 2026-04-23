using Sunfish.Kernel.Events;
using Sunfish.Kernel.Ledger.Periods;

namespace Sunfish.Kernel.Ledger.Tests;

public class PostingEngineTests
{
    private static PostingEngine NewEngine(
        out FakeLeaseCoordinator leases,
        out InMemoryEventLog eventLog,
        out PeriodCloseState periodState)
    {
        leases = new FakeLeaseCoordinator();
        eventLog = new InMemoryEventLog();
        periodState = new PeriodCloseState();
        return new PostingEngine(leases, eventLog, periodState);
    }

    [Fact]
    public async Task PostAsync_BalancedTransaction_Succeeds()
    {
        var engine = NewEngine(out _, out _, out _);

        var tx = TxBuilder.Simple("key-1", "cash", "revenue", 100m);
        var result = await engine.PostAsync(tx, default);

        Assert.True(result.Success);
        Assert.Equal(tx.TransactionId, result.TransactionId);
        Assert.NotNull(result.LogSequence);
        Assert.Equal(1UL, result.LogSequence);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public async Task PostAsync_UnbalancedTransaction_Rejected()
    {
        var engine = NewEngine(out _, out _, out _);
        var tx = TxBuilder.Unbalanced("bad-key");

        var result = await engine.PostAsync(tx, default);

        Assert.False(result.Success);
        Assert.Equal("UNBALANCED", result.RejectionReason);
        Assert.Null(result.LogSequence);
    }

    [Fact]
    public async Task PostAsync_IsBalanced_UsesDecimalExactEquality()
    {
        // Three-way split: 0.10 = 0.07 + 0.02 + 0.01 — decimal is exact; floats
        // would carry epsilon.
        var engine = NewEngine(out _, out _, out _);
        var txId = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;
        var tx = new Transaction(
            txId,
            "split-key",
            new[]
            {
                new Posting(Guid.NewGuid(), txId, "a", +0.10m, "USD", at, "s", TxBuilder.EmptyMetadata),
                new Posting(Guid.NewGuid(), txId, "b", -0.07m, "USD", at, "s", TxBuilder.EmptyMetadata),
                new Posting(Guid.NewGuid(), txId, "c", -0.02m, "USD", at, "s", TxBuilder.EmptyMetadata),
                new Posting(Guid.NewGuid(), txId, "d", -0.01m, "USD", at, "s", TxBuilder.EmptyMetadata),
            },
            at);

        Assert.True(tx.IsBalanced);
        Assert.Equal(0m, tx.Sum);

        var result = await engine.PostAsync(tx, default);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task PostAsync_DuplicateIdempotencyKey_ReturnsExistingTransactionId()
    {
        var engine = NewEngine(out _, out var eventLog, out _);

        var first = TxBuilder.Simple("dedupe-key", "cash", "revenue", 50m);
        var second = TxBuilder.Simple("dedupe-key", "cash", "revenue", 50m);
        Assert.NotEqual(first.TransactionId, second.TransactionId);

        var r1 = await engine.PostAsync(first, default);
        var r2 = await engine.PostAsync(second, default);

        Assert.True(r1.Success);
        Assert.True(r2.Success);
        Assert.Equal(first.TransactionId, r2.TransactionId);
        // r2 hit the dedupe path — no new log entry.
        Assert.Null(r2.LogSequence);
        Assert.Equal(1UL, eventLog.CurrentSequence);
    }

    [Fact]
    public async Task PostAsync_ConcurrentSameAccount_SerializesViaLease()
    {
        var engine = NewEngine(out var leases, out _, out _);

        // Fire 20 concurrent posts on the same account pair.
        var tasks = Enumerable.Range(0, 20).Select(async i =>
        {
            var tx = TxBuilder.Simple($"k-{i}", "cash", "revenue", 1m);
            await engine.PostAsync(tx, default);
        }).ToArray();

        await Task.WhenAll(tasks);

        // We can't guarantee max-concurrency == 1 exactly (FakeLease is free to
        // hand out overlapping leases for different LeaseIds on the same
        // resource if called in parallel), but every transaction should still
        // have acquired its leases — the deadlock-safe ordering means no hang.
        Assert.Equal(20 * 2, leases.AcquireCount); // two accounts per tx
    }

    [Fact]
    public async Task PostAsync_QuorumUnavailable_Rejected()
    {
        var leases = new UnreachableLeaseCoordinator();
        var eventLog = new InMemoryEventLog();
        var engine = new PostingEngine(leases, eventLog, new PeriodCloseState());

        var tx = TxBuilder.Simple("q-key", "cash", "revenue", 10m);
        var result = await engine.PostAsync(tx, default);

        Assert.False(result.Success);
        Assert.Equal("QUORUM_UNAVAILABLE", result.RejectionReason);
        Assert.Null(result.LogSequence);
        Assert.Equal(0UL, eventLog.CurrentSequence);
    }

    [Fact]
    public async Task PostAsync_AppendsKernelEvent_WithPostingsAppliedKind()
    {
        var engine = NewEngine(out _, out var eventLog, out _);
        var tx = TxBuilder.Simple("env-key", "cash", "revenue", 10m);

        await engine.PostAsync(tx, default);

        var entries = new List<LogEntry>();
        await foreach (var e in eventLog.ReadAfterAsync(0, default))
        {
            entries.Add(e);
        }
        Assert.Single(entries);
        Assert.Equal("ledger.postings-applied", entries[0].Event.Kind);
    }

    [Fact]
    public async Task CompensateAsync_ProducesReversingTransaction()
    {
        var engine = NewEngine(out _, out _, out _);
        var original = TxBuilder.Simple("orig-key", "cash", "revenue", 100m);
        await engine.PostAsync(original, default);

        var result = await engine.CompensateAsync(original.TransactionId, "reverse", default);

        Assert.True(result.Success);
        var events = engine.ReplayAll();
        // One PostingsAppliedEvent for original, one PostingsAppliedEvent for
        // compensating tx, one CompensationAppliedEvent.
        Assert.Equal(3, events.Count);
        var originalApplied = Assert.IsType<PostingsAppliedEvent>(events[0]);
        var compensatingApplied = Assert.IsType<PostingsAppliedEvent>(events[1]);
        var compEvent = Assert.IsType<CompensationAppliedEvent>(events[2]);

        Assert.Equal(0m, compensatingApplied.Transaction.Sum);
        Assert.True(compensatingApplied.Transaction.IsBalanced);
        Assert.Equal(original.TransactionId, compEvent.OriginalTransactionId);

        // Net balance after compensation == 0 for each account.
        var cashNet = originalApplied.Transaction.Postings.Where(p => p.AccountId == "cash").Sum(p => p.Amount)
                    + compensatingApplied.Transaction.Postings.Where(p => p.AccountId == "cash").Sum(p => p.Amount);
        Assert.Equal(0m, cashNet);
    }

    [Fact]
    public async Task CompensateAsync_NonexistentTransaction_Throws()
    {
        var engine = NewEngine(out _, out _, out _);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.CompensateAsync(Guid.NewGuid(), "why", default));
    }

    [Fact]
    public async Task PostAsync_RejectsCancellation()
    {
        var engine = NewEngine(out _, out _, out _);
        var tx = TxBuilder.Simple("c-key", "cash", "revenue", 1m);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => engine.PostAsync(tx, cts.Token));
    }
}
