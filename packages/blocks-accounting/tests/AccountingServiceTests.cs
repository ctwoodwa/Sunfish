using Sunfish.Blocks.Accounting.Models;
using Sunfish.Blocks.Accounting.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Accounting.Tests;

public class AccountingServiceTests
{
    private static InMemoryAccountingService CreateService() => new();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<GLAccount> AddAccount(
        InMemoryAccountingService svc,
        string code = "1000",
        string name = "Cash",
        GLAccountType type = GLAccountType.Asset)
        => await svc.CreateAccountAsync(new CreateGLAccountRequest(code, name, type));

    /// <summary>
    /// Creates a minimal two-line balanced entry (debit account A, credit account B).
    /// </summary>
    private static async Task<JournalEntry> PostBalancedEntry(
        InMemoryAccountingService svc,
        GLAccountId debitId,
        GLAccountId creditId,
        decimal amount = 100m,
        DateOnly? date = null,
        string? sourceRef = null)
    {
        var lines = new List<JournalEntryLine>
        {
            new(debitId, debit: amount, credit: 0m),
            new(creditId, debit: 0m, credit: amount),
        };
        return await svc.PostEntryAsync(new PostJournalEntryRequest(
            EntryDate: date ?? new DateOnly(2025, 6, 1),
            Memo: "Test entry",
            Lines: lines,
            SourceReference: sourceRef));
    }

    // =========================================================================
    // CreateAccountAsync
    // =========================================================================

    [Fact]
    public async Task CreateAccountAsync_RoundTrip_ReturnsAccountWithCorrectFields()
    {
        var svc = CreateService();
        var account = await svc.CreateAccountAsync(new CreateGLAccountRequest(
            Code: "4000",
            Name: "Rental Revenue",
            Type: GLAccountType.Revenue));

        Assert.NotNull(account);
        Assert.Equal("4000", account.Code);
        Assert.Equal("Rental Revenue", account.Name);
        Assert.Equal(GLAccountType.Revenue, account.Type);
        Assert.Null(account.ParentAccountId);

        // Must be retrievable by id
        var fetched = await svc.GetAccountAsync(account.Id);
        Assert.NotNull(fetched);
        Assert.Equal(account.Id, fetched!.Id);
    }

    [Fact]
    public async Task CreateAccountAsync_RejectsDuplicateCode()
    {
        var svc = CreateService();
        await svc.CreateAccountAsync(new CreateGLAccountRequest("4000", "Rental Revenue", GLAccountType.Revenue));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateAccountAsync(new CreateGLAccountRequest(
                "4000", "Another Revenue Account", GLAccountType.Revenue)).AsTask());
    }

    // =========================================================================
    // PostEntryAsync — happy path
    // =========================================================================

    [Fact]
    public async Task PostEntryAsync_AcceptsBalancedEntry()
    {
        var svc = CreateService();
        var cash    = await AddAccount(svc, "1000", "Cash");
        var revenue = await AddAccount(svc, "4000", "Revenue", GLAccountType.Revenue);

        var entry = await PostBalancedEntry(svc, cash.Id, revenue.Id, amount: 500m);

        Assert.NotNull(entry);
        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(500m, entry.Lines[0].Debit);
        Assert.Equal(500m, entry.Lines[1].Credit);

        // Round-trip via GetEntryAsync
        var fetched = await svc.GetEntryAsync(entry.Id);
        Assert.NotNull(fetched);
        Assert.Equal(entry.Id, fetched!.Id);
    }

    // =========================================================================
    // PostEntryAsync — rejection tests
    // =========================================================================

    [Fact]
    public async Task PostEntryAsync_RejectsImbalancedEntry()
    {
        var svc = CreateService();
        var cash    = await AddAccount(svc, "1000", "Cash");
        var revenue = await AddAccount(svc, "4000", "Revenue", GLAccountType.Revenue);

        var imbalancedLines = new List<JournalEntryLine>
        {
            new(cash.Id, debit: 500m, credit: 0m),
            new(revenue.Id, debit: 0m, credit: 400m),   // 500 ≠ 400
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.PostEntryAsync(new PostJournalEntryRequest(
                EntryDate: new DateOnly(2025, 6, 1),
                Memo: "Imbalanced",
                Lines: imbalancedLines)).AsTask());
    }

    [Fact]
    public void PostEntryAsync_RejectsLineWithBothDebitAndCreditNonZero()
    {
        var accountId = GLAccountId.NewId();

        Assert.Throws<ArgumentException>(() =>
            new JournalEntryLine(accountId, debit: 100m, credit: 50m));
    }

    [Fact]
    public void PostEntryAsync_RejectsLineWithBothDebitAndCreditZero()
    {
        var accountId = GLAccountId.NewId();

        Assert.Throws<ArgumentException>(() =>
            new JournalEntryLine(accountId, debit: 0m, credit: 0m));
    }

    [Fact]
    public void PostEntryAsync_RejectsNegativeDebit()
    {
        var accountId = GLAccountId.NewId();

        Assert.Throws<ArgumentException>(() =>
            new JournalEntryLine(accountId, debit: -10m, credit: 0m));
    }

    [Fact]
    public void PostEntryAsync_RejectsNegativeCredit()
    {
        var accountId = GLAccountId.NewId();

        Assert.Throws<ArgumentException>(() =>
            new JournalEntryLine(accountId, debit: 0m, credit: -10m));
    }

    [Fact]
    public async Task PostEntryAsync_RejectsUnknownAccountId()
    {
        var svc = CreateService();
        var ghost = GLAccountId.NewId();  // never added to service
        var cash  = await AddAccount(svc, "1000", "Cash");

        var lines = new List<JournalEntryLine>
        {
            new(cash.Id, debit: 100m, credit: 0m),
            new(ghost,   debit: 0m, credit: 100m),   // unknown
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.PostEntryAsync(new PostJournalEntryRequest(
                EntryDate: new DateOnly(2025, 6, 1),
                Memo: "Unknown account",
                Lines: lines)).AsTask());
    }

    // =========================================================================
    // ListEntriesAsync — date-range filter
    // =========================================================================

    [Fact]
    public async Task ListEntriesAsync_FiltersByDateRange()
    {
        var svc     = CreateService();
        var cash    = await AddAccount(svc, "1000", "Cash");
        var revenue = await AddAccount(svc, "4000", "Revenue", GLAccountType.Revenue);

        // Post three entries on different dates
        await PostBalancedEntry(svc, cash.Id, revenue.Id, date: new DateOnly(2025, 1, 15));
        await PostBalancedEntry(svc, cash.Id, revenue.Id, date: new DateOnly(2025, 3, 10));
        await PostBalancedEntry(svc, cash.Id, revenue.Id, date: new DateOnly(2025, 6, 1));

        var query   = new ListEntriesQuery(FromDate: new DateOnly(2025, 2, 1), ToDate: new DateOnly(2025, 4, 30));
        var results = new List<JournalEntry>();
        await foreach (var e in svc.ListEntriesAsync(query))
            results.Add(e);

        Assert.Single(results);
        Assert.Equal(new DateOnly(2025, 3, 10), results[0].EntryDate);
    }

    // =========================================================================
    // Concurrency
    // =========================================================================

    [Fact]
    public async Task PostEntryAsync_ConcurrentCallsAreSafe_AllEntriesAreStored()
    {
        var svc     = CreateService();
        var cash    = await AddAccount(svc, "1000", "Cash");
        var revenue = await AddAccount(svc, "4000", "Revenue", GLAccountType.Revenue);

        // Fire 20 concurrent PostEntryAsync calls.
        var tasks = Enumerable.Range(0, 20).Select(_ =>
            PostBalancedEntry(svc, cash.Id, revenue.Id)).ToArray();

        await Task.WhenAll(tasks);

        // All 20 entries must be stored.
        var allEntries = new List<JournalEntry>();
        await foreach (var e in svc.ListEntriesAsync(new ListEntriesQuery()))
            allEntries.Add(e);

        Assert.Equal(20, allEntries.Count);
    }
}
