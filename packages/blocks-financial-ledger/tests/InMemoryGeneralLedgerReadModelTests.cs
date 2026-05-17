using System;
using System.Linq;
using System.Threading.Tasks;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

public sealed class InMemoryGeneralLedgerReadModelTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();
    private static readonly GLAccountId AccountA = GLAccountId.NewId();
    private static readonly GLAccountId AccountB = GLAccountId.NewId();

    private static JournalEntry Entry(DateOnly entryDate, JournalEntryStatus status, decimal amount, ChartOfAccountsId? chartId = null)
    {
        var entry = new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: entryDate,
            memo: "test",
            lines: new[]
            {
                new JournalEntryLine(AccountA, debit: amount, credit: 0m),
                new JournalEntryLine(AccountB, debit: 0m,    credit: amount),
            },
            createdAtUtc: Instant.Now);
        return entry with { Status = status, ChartId = chartId ?? Chart };
    }

    [Fact]
    public async Task EmptyStore_ReturnsEmptyDictionary()
    {
        var store = new InMemoryJournalStore();
        var sut = new InMemoryGeneralLedgerReadModel(store);
        var result = await sut.GetAccountBalancesAsOfAsync(Chart, new DateOnly(2026, 12, 31), "marker:1");
        Assert.Empty(result);
    }

    [Fact]
    public async Task PostedEntry_ProducesSignedBalance()
    {
        var store = new InMemoryJournalStore();
        await store.SaveAtomicAsync(Entry(new DateOnly(2026, 5, 1), JournalEntryStatus.Posted, 100m));
        var sut = new InMemoryGeneralLedgerReadModel(store);

        var result = await sut.GetAccountBalancesAsOfAsync(Chart, new DateOnly(2026, 12, 31), "marker:1");

        Assert.Equal(2, result.Count);
        Assert.Equal(100m, result[AccountA]);    // debit-positive
        Assert.Equal(-100m, result[AccountB]);   // credit-negative (signed)
    }

    [Fact]
    public async Task MultipleEntries_AggregatePerAccount()
    {
        var store = new InMemoryJournalStore();
        await store.SaveAtomicAsync(Entry(new DateOnly(2026, 5, 1), JournalEntryStatus.Posted, 100m));
        await store.SaveAtomicAsync(Entry(new DateOnly(2026, 5, 15), JournalEntryStatus.Posted, 50m));
        var sut = new InMemoryGeneralLedgerReadModel(store);

        var result = await sut.GetAccountBalancesAsOfAsync(Chart, new DateOnly(2026, 12, 31), "marker:1");

        Assert.Equal(150m, result[AccountA]);
        Assert.Equal(-150m, result[AccountB]);
    }

    [Fact]
    public async Task EntryAfterAsOf_ExcludedFromResults()
    {
        var store = new InMemoryJournalStore();
        await store.SaveAtomicAsync(Entry(new DateOnly(2026, 5, 1), JournalEntryStatus.Posted, 100m));
        await store.SaveAtomicAsync(Entry(new DateOnly(2026, 6, 1), JournalEntryStatus.Posted, 50m));
        var sut = new InMemoryGeneralLedgerReadModel(store);

        var result = await sut.GetAccountBalancesAsOfAsync(Chart, new DateOnly(2026, 5, 31), "marker:1");

        // Only May 1 entry counted; June 1 excluded.
        Assert.Equal(100m, result[AccountA]);
        Assert.Equal(-100m, result[AccountB]);
    }

    [Fact]
    public async Task DraftEntry_ExcludedFromResults()
    {
        var store = new InMemoryJournalStore();
        await store.SaveAtomicAsync(Entry(new DateOnly(2026, 5, 1), JournalEntryStatus.Draft, 100m));
        var sut = new InMemoryGeneralLedgerReadModel(store);

        var result = await sut.GetAccountBalancesAsOfAsync(Chart, new DateOnly(2026, 12, 31), "marker:1");
        Assert.Empty(result);
    }

    [Fact]
    public async Task DifferentChart_ExcludedFromResults()
    {
        var otherChart = ChartOfAccountsId.NewId();
        var store = new InMemoryJournalStore();
        await store.SaveAtomicAsync(Entry(new DateOnly(2026, 5, 1), JournalEntryStatus.Posted, 100m, chartId: otherChart));
        var sut = new InMemoryGeneralLedgerReadModel(store);

        var result = await sut.GetAccountBalancesAsOfAsync(Chart, new DateOnly(2026, 12, 31), "marker:1");
        Assert.Empty(result);
    }
}
