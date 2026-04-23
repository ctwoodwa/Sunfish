using Sunfish.Kernel.Events;
using Sunfish.Kernel.Ledger.CQRS;
using Sunfish.Kernel.Ledger.Periods;

namespace Sunfish.Kernel.Ledger.Tests;

public class BalanceProjectionTests
{
    private static (PostingEngine engine, BalanceProjection balances) NewStack()
    {
        var engine = new PostingEngine(
            new FakeLeaseCoordinator(),
            new InMemoryEventLog(),
            new PeriodCloseState());
        var balances = new BalanceProjection(engine);
        return (engine, balances);
    }

    [Fact]
    public async Task GetBalance_AfterSinglePost_ReturnsCorrectSignedAmount()
    {
        var (engine, balances) = NewStack();
        await engine.PostAsync(TxBuilder.Simple("k1", "cash", "revenue", 100m), default);

        Assert.Equal(+100m, await balances.GetBalanceAsync("cash", asOf: null, default));
        Assert.Equal(-100m, await balances.GetBalanceAsync("revenue", asOf: null, default));
    }

    [Fact]
    public async Task GetBalance_AfterMultiplePosts_IsSum()
    {
        var (engine, balances) = NewStack();
        await engine.PostAsync(TxBuilder.Simple("k1", "cash", "revenue", 100m), default);
        await engine.PostAsync(TxBuilder.Simple("k2", "cash", "revenue", 250m), default);
        await engine.PostAsync(TxBuilder.Simple("k3", "cash", "revenue", 50m), default);

        Assert.Equal(+400m, await balances.GetBalanceAsync("cash", null, default));
        Assert.Equal(-400m, await balances.GetBalanceAsync("revenue", null, default));
    }

    [Fact]
    public async Task GetBalance_AsOfDate_IgnoresLaterPostings()
    {
        var (engine, balances) = NewStack();
        var jan = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var feb = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        var mar = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);

        await engine.PostAsync(TxBuilder.Simple("k1", "cash", "rev", 10m, jan), default);
        await engine.PostAsync(TxBuilder.Simple("k2", "cash", "rev", 20m, feb), default);
        await engine.PostAsync(TxBuilder.Simple("k3", "cash", "rev", 40m, mar), default);

        var endJan = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
        Assert.Equal(+10m, await balances.GetBalanceAsync("cash", endJan, default));

        var endFeb = new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero);
        Assert.Equal(+30m, await balances.GetBalanceAsync("cash", endFeb, default));
    }

    [Fact]
    public async Task GetBalance_UnknownAccount_IsZero()
    {
        var (_, balances) = NewStack();
        Assert.Equal(0m, await balances.GetBalanceAsync("ghost", null, default));
    }

    [Fact]
    public async Task GetPostingsAsync_ReturnsNewestFirst()
    {
        var (engine, balances) = NewStack();
        var jan = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var feb = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        await engine.PostAsync(TxBuilder.Simple("k1", "cash", "rev", 1m, jan, "first"), default);
        await engine.PostAsync(TxBuilder.Simple("k2", "cash", "rev", 2m, feb, "second"), default);

        var list = new List<Posting>();
        await foreach (var p in balances.GetPostingsAsync("cash", default))
        {
            list.Add(p);
        }

        Assert.Equal(2, list.Count);
        Assert.Equal(feb, list[0].PostedAt);
        Assert.Equal(jan, list[1].PostedAt);
    }

    [Fact]
    public async Task RebuildAsync_RecomputesStateFromEventStream()
    {
        var (engine, balances) = NewStack();
        await engine.PostAsync(TxBuilder.Simple("k1", "cash", "rev", 10m), default);
        await engine.PostAsync(TxBuilder.Simple("k2", "cash", "rev", 20m), default);

        // Rebuild should yield the same balance (idempotent).
        await balances.RebuildAsync(default);
        Assert.Equal(+30m, await balances.GetBalanceAsync("cash", null, default));
    }
}
