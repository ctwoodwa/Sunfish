using Sunfish.Kernel.Events;
using Sunfish.Kernel.Ledger.CQRS;
using Sunfish.Kernel.Ledger.Periods;

namespace Sunfish.Kernel.Ledger.Tests;

public class StatementProjectionTests
{
    private static (PostingEngine engine, StatementProjection statements) NewStack()
    {
        var engine = new PostingEngine(
            new FakeLeaseCoordinator(),
            new InMemoryEventLog(),
            new PeriodCloseState());
        var statements = new StatementProjection(engine);
        return (engine, statements);
    }

    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jan31 = new(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
    private static readonly DateTimeOffset Feb1 = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Feb28 = new(2026, 2, 28, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public async Task GetStatement_OpeningBalance_IsSumBeforePeriodStart()
    {
        var (engine, statements) = NewStack();
        await engine.PostAsync(TxBuilder.Simple("k1", "cash", "rev", 50m, Jan1), default);
        await engine.PostAsync(TxBuilder.Simple("k2", "cash", "rev", 25m, Feb1), default);

        var stmt = await statements.GetStatementAsync("cash", Feb1, Feb28, default);
        Assert.Equal(+50m, stmt.OpeningBalance); // pre-period posting of +50
    }

    [Fact]
    public async Task GetStatement_ClosingBalance_IsSumThroughPeriodEnd()
    {
        var (engine, statements) = NewStack();
        await engine.PostAsync(TxBuilder.Simple("k1", "cash", "rev", 50m, Jan1), default);
        await engine.PostAsync(TxBuilder.Simple("k2", "cash", "rev", 25m, Feb1), default);
        // Post dated AFTER the period end — must not be included in closing.
        await engine.PostAsync(
            TxBuilder.Simple("k3", "cash", "rev", 999m, new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)),
            default);

        var stmt = await statements.GetStatementAsync("cash", Feb1, Feb28, default);
        Assert.Equal(+75m, stmt.ClosingBalance);
    }

    [Fact]
    public async Task GetStatement_PostingsList_OnlyIncludesInPeriod_SortedOldestFirst()
    {
        var (engine, statements) = NewStack();
        await engine.PostAsync(TxBuilder.Simple("before", "cash", "rev", 10m, Jan1), default);
        await engine.PostAsync(TxBuilder.Simple("mid1", "cash", "rev", 20m,
            new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero)), default);
        await engine.PostAsync(TxBuilder.Simple("mid2", "cash", "rev", 30m,
            new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero)), default);
        await engine.PostAsync(TxBuilder.Simple("after", "cash", "rev", 40m,
            new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)), default);

        var stmt = await statements.GetStatementAsync("cash", Feb1, Feb28, default);
        Assert.Equal(2, stmt.Postings.Count);
        Assert.True(stmt.Postings[0].PostedAt < stmt.Postings[1].PostedAt);
    }

    [Fact]
    public async Task GetStatement_EmptyAccount_IsZerosAndEmptyList()
    {
        var (_, statements) = NewStack();

        var stmt = await statements.GetStatementAsync("nothing", Jan1, Feb28, default);
        Assert.Equal(0m, stmt.OpeningBalance);
        Assert.Equal(0m, stmt.ClosingBalance);
        Assert.Empty(stmt.Postings);
    }
}
