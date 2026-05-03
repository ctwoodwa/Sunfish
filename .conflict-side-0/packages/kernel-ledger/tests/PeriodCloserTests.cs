using Sunfish.Kernel.Events;
using Sunfish.Kernel.Ledger.CQRS;
using Sunfish.Kernel.Ledger.Exceptions;
using Sunfish.Kernel.Ledger.Periods;

namespace Sunfish.Kernel.Ledger.Tests;

public class PeriodCloserTests
{
    private static (PostingEngine engine, PeriodCloser closer, PeriodCloseState state, BalanceProjection balances) NewStack()
    {
        var state = new PeriodCloseState();
        var engine = new PostingEngine(
            new FakeLeaseCoordinator(),
            new InMemoryEventLog(),
            state);
        var closer = new PeriodCloser(engine, state, engine);
        var balances = new BalanceProjection(engine);
        return (engine, closer, state, balances);
    }

    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jan31 = new(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
    private static readonly DateTimeOffset Feb1 = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Feb28 = new(2026, 2, 28, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public async Task CloseAsync_EmptyLedger_ReturnsEmptySnapshot()
    {
        var (_, closer, _, _) = NewStack();

        var result = await closer.CloseAsync(Jan31, default);

        Assert.Equal(Jan31, result.PeriodEnd);
        Assert.Equal(0UL, result.AccountCount);
        Assert.Empty(result.ClosingBalances);
    }

    [Fact]
    public async Task CloseAsync_WithPostings_CapturesBalances()
    {
        var (engine, closer, _, _) = NewStack();
        await engine.PostAsync(TxBuilder.Simple("k1", "cash", "rev", 100m, Jan1), default);

        var result = await closer.CloseAsync(Jan31, default);

        Assert.Equal(2UL, result.AccountCount);
        Assert.Equal(+100m, result.ClosingBalances["cash"]);
        Assert.Equal(-100m, result.ClosingBalances["rev"]);
    }

    [Fact]
    public async Task CloseAsync_UpdatesLastClosedPeriodEnd()
    {
        var (_, closer, state, _) = NewStack();
        Assert.Null(state.LastClosedPeriodEnd);

        await closer.CloseAsync(Jan31, default);

        Assert.Equal(Jan31, state.LastClosedPeriodEnd);
        Assert.Equal(Jan31, await closer.LastClosedPeriodEndAsync(default));
    }

    [Fact]
    public async Task CloseAsync_DoubleClose_Throws()
    {
        var (_, closer, _, _) = NewStack();
        await closer.CloseAsync(Jan31, default);

        await Assert.ThrowsAsync<ClosedPeriodException>(
            () => closer.CloseAsync(Jan31, default));
    }

    [Fact]
    public async Task CloseAsync_EarlierPeriod_Throws()
    {
        var (_, closer, _, _) = NewStack();
        await closer.CloseAsync(Feb28, default);

        await Assert.ThrowsAsync<ClosedPeriodException>(
            () => closer.CloseAsync(Jan31, default));
    }

    [Fact]
    public async Task PostAsync_DatedIntoClosedPeriod_RoutesToAdjustments()
    {
        var (engine, closer, _, balances) = NewStack();

        // Close January.
        await closer.CloseAsync(Jan31, default);

        // Post a transaction dated into January (closed) — must be redirected.
        var backDated = TxBuilder.Simple("late", "cash", "rev", 10m, Jan1);
        var result = await engine.PostAsync(backDated, default);
        Assert.True(result.Success);

        // Original 'cash' and 'rev' accounts should NOT reflect the back-dated
        // posting at all — the postings have been renamed to the adjustments
        // account in the next open period (paper §12.4).
        Assert.Equal(0m, await balances.GetBalanceAsync("cash", null, default));
        Assert.Equal(0m, await balances.GetBalanceAsync("rev", null, default));

        // Adjustments account receives BOTH sides — net balance is therefore
        // zero, but there is an observable audit trail on the account.
        var adjKey = $"adjustments-{Jan31.UtcDateTime:yyyyMMdd}";
        Assert.Equal(0m, await balances.GetBalanceAsync(adjKey, null, default));
        var postings = new List<Posting>();
        await foreach (var p in balances.GetPostingsAsync(adjKey, default))
        {
            postings.Add(p);
        }
        Assert.Equal(2, postings.Count);
        Assert.All(postings, p => Assert.True(p.PostedAt > Jan31));
    }
}
