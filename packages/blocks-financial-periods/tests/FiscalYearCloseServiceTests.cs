using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPeriods.Financial;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;
using static Sunfish.Blocks.FinancialPeriods.Tests.PeriodCloseServiceSoftCloseTests;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 PR 3c — coverage for
/// <see cref="FiscalYearCloseService.CloseFiscalYearAsync"/> per
/// Stage 02 §6.5(b).
/// </summary>
public sealed class FiscalYearCloseServiceTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task CloseFY_AlreadyClosed_ReturnsFiscalYearAlreadyClosed()
    {
        var h = new Harness();
        await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();

        // First close succeeds.
        var first = await h.Sut.CloseFiscalYearAsync(fy.Id);
        Assert.True(first.IsSuccess, first.Detail);

        // Second close returns already-closed.
        var second = await h.Sut.CloseFiscalYearAsync(fy.Id);
        Assert.False(second.IsSuccess);
        Assert.Equal(FiscalYearCloseError.FiscalYearAlreadyClosed, second.Error);
    }

    [Fact]
    public async Task CloseFY_RetainedEarningsAccountUnset_ReturnsConfigurationError()
    {
        var h = new Harness();
        // Seed chart WITHOUT retained earnings.
        h.Charts.Upsert(new ChartOfAccounts(
            Id: Chart, LegalEntityId: LegalEntityId.NewId(),
            Name: "Test Chart", BaseCurrency: "USD",
            FiscalYearStartMonth: 1, FiscalYearStartDay: 1,
            RetainedEarningsAccountId: null,
            IsActive: true,
            CreatedAtUtc: Instant.Now, UpdatedAtUtc: Instant.Now));
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();

        var result = await h.Sut.CloseFiscalYearAsync(fy.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalYearCloseError.RetainedEarningsAccountNotConfigured, result.Error);
    }

    [Fact]
    public async Task CloseFY_FiscalYearNotFound_Returns()
    {
        var h = new Harness();
        var result = await h.Sut.CloseFiscalYearAsync(FiscalYearId.NewId());

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalYearCloseError.FiscalYearNotFound, result.Error);
    }

    [Fact]
    public async Task CloseFY_AutoSoftClosesAnyRemainingOpenPeriods()
    {
        var h = new Harness();
        await h.SeedChartWithRetainedEarningsAsync();
        var (fy, periods) = await h.SeedYearWithMonthlyPeriodsAsync();

        var result = await h.Sut.CloseFiscalYearAsync(fy.Id);

        Assert.True(result.IsSuccess);
        // All periods end Locked (post-Lock).
        foreach (var pid in periods.Select(p => p.Id))
        {
            var stored = await h.Periods.GetAsync(pid);
            Assert.NotNull(stored);
            Assert.Equal(FiscalPeriodStatus.Locked, stored!.Status);
        }
    }

    [Fact]
    public async Task CloseFY_ZeroActivityYear_FlipsFy_NoClosingEntry()
    {
        var h = new Harness();
        await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();
        // No balances seeded → all accounts balance = 0 → no closing JE.

        var result = await h.Sut.CloseFiscalYearAsync(fy.Id);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ClosingEntryId);
        Assert.Equal(FiscalYearStatus.Closed, result.FiscalYear!.Status);
        Assert.NotNull(result.FiscalYear.ClosedAtUtc);
        Assert.Empty(h.Store.Snapshot()); // no JE posted
    }

    [Fact]
    public async Task CloseFY_NetProfit_PostsClosingEntry_CreditingRetainedEarnings()
    {
        var h = new Harness();
        var (rentRev, supplyExp, retained) = await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();

        // Revenue: $1000 credit balance → IBalanceComputer returns -1000.
        h.Balances.Seed(rentRev.Id, -1000m);
        // Expense: $300 debit balance → IBalanceComputer returns +300.
        h.Balances.Seed(supplyExp.Id, 300m);

        var result = await h.Sut.CloseFiscalYearAsync(fy.Id);

        Assert.True(result.IsSuccess, result.Detail);
        Assert.NotNull(result.ClosingEntryId);
        var closing = h.Store.Snapshot().Single();
        Assert.Equal(JournalEntryStatus.Posted, closing.Status);
        Assert.Equal(JournalEntrySource.Closing, closing.SourceKind);
        // Retained earnings line: credit 700 (net income).
        var retainedLine = closing.Lines.Single(l => l.AccountId.Equals(retained.Id));
        Assert.Equal(0m, retainedLine.Debit);
        Assert.Equal(700m, retainedLine.Credit);
    }

    [Fact]
    public async Task CloseFY_NetLoss_PostsClosingEntry_DebitingRetainedEarnings()
    {
        var h = new Harness();
        var (rentRev, supplyExp, retained) = await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();

        h.Balances.Seed(rentRev.Id, -200m);     // $200 revenue
        h.Balances.Seed(supplyExp.Id, 500m);    // $500 expense → net loss $300

        var result = await h.Sut.CloseFiscalYearAsync(fy.Id);

        Assert.True(result.IsSuccess);
        var closing = h.Store.Snapshot().Single();
        var retainedLine = closing.Lines.Single(l => l.AccountId.Equals(retained.Id));
        Assert.Equal(300m, retainedLine.Debit);
        Assert.Equal(0m, retainedLine.Credit);
    }

    [Fact]
    public async Task CloseFY_PostsBalancedClosingEntry()
    {
        var h = new Harness();
        var (rentRev, supplyExp, _) = await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();

        h.Balances.Seed(rentRev.Id, -1234.56m);
        h.Balances.Seed(supplyExp.Id, 234.56m);

        var result = await h.Sut.CloseFiscalYearAsync(fy.Id);
        Assert.True(result.IsSuccess);

        var closing = h.Store.Snapshot().Single();
        Assert.Equal(closing.Lines.Sum(l => l.Debit), closing.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task CloseFY_AfterClose_FyEndPointsAtClosingEntry()
    {
        var h = new Harness();
        var (rentRev, _, _) = await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();
        h.Balances.Seed(rentRev.Id, -100m);

        var result = await h.Sut.CloseFiscalYearAsync(fy.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(result.ClosingEntryId, result.FiscalYear!.ClosingJournalEntryId);
        var storedFy = await h.Years.GetAsync(fy.Id);
        Assert.Equal(result.ClosingEntryId, storedFy!.ClosingJournalEntryId);
    }

    [Fact]
    public async Task CloseFY_EmitsYearClosedAndYearEndRolloverCompletedEvents()
    {
        var h = new Harness();
        var (rentRev, supplyExp, _) = await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();
        h.Balances.Seed(rentRev.Id, -1000m);
        h.Balances.Seed(supplyExp.Id, 300m);

        await h.Sut.CloseFiscalYearAsync(fy.Id);

        var yearClosed = Assert.Single(h.Events.Published.OfType<YearClosed>().ToList());
        Assert.Equal(fy.Id, yearClosed.FiscalYearId);
        var rollover = Assert.Single(h.Events.Published.OfType<YearEndRolloverCompleted>().ToList());
        Assert.Equal(700m, rollover.NetIncome);
        Assert.Equal(1, rollover.IncomeAccountsClosed);
        Assert.Equal(1, rollover.ExpenseAccountsClosed);
    }

    [Fact]
    public async Task ReopenFY_ClosedFY_FlipsToOpen()
    {
        var h = new Harness();
        await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();
        await h.Sut.CloseFiscalYearAsync(fy.Id);

        var result = await h.Sut.ReopenFiscalYearAsync(fy.Id, auditMemo: "audit fix");

        Assert.True(result.IsSuccess, result.Detail);
        Assert.Equal(FiscalYearStatus.Open, result.FiscalYear!.Status);
        Assert.Null(result.FiscalYear.ClosedAtUtc);
    }

    [Fact]
    public async Task ReopenFY_OpenFY_ReturnsFiscalYearAlreadyOpen()
    {
        var h = new Harness();
        await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();

        var result = await h.Sut.ReopenFiscalYearAsync(fy.Id, auditMemo: "x");

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalYearCloseError.FiscalYearAlreadyOpen, result.Error);
    }

    [Fact]
    public async Task ReopenFY_EmptyAuditMemo_ReturnsAuditMemoRequired()
    {
        var h = new Harness();
        await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();
        await h.Sut.CloseFiscalYearAsync(fy.Id);

        var result = await h.Sut.ReopenFiscalYearAsync(fy.Id, auditMemo: "   ");

        Assert.False(result.IsSuccess);
        Assert.Equal(FiscalYearCloseError.AuditMemoRequired, result.Error);
    }

    [Fact]
    public async Task ReopenFY_WithClosingJE_ReturnsReversalEntryFailed()
    {
        // B1 (PR 3c council): until PR 3d ships the sibling-ledger
        // reverse-by-id helper, reopen cannot post the reversal — it
        // must reject loudly with closingId in Detail so the operator
        // doesn't end up with a phantom zero-out in the ledger.
        var h = new Harness();
        var (rentRev, _, _) = await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();
        h.Balances.Seed(rentRev.Id, -500m);
        var closeResult = await h.Sut.CloseFiscalYearAsync(fy.Id);
        Assert.True(closeResult.IsSuccess, closeResult.Detail);
        Assert.NotNull(closeResult.ClosingEntryId);

        var reopen = await h.Sut.ReopenFiscalYearAsync(fy.Id, auditMemo: "needs reversal");

        Assert.False(reopen.IsSuccess);
        Assert.Equal(FiscalYearCloseError.ReversalEntryFailed, reopen.Error);
        Assert.NotNull(reopen.Detail);
        Assert.Contains(closeResult.ClosingEntryId!.Value.Value, reopen.Detail!);
    }

    [Fact]
    public async Task CloseFY_RetryAfterPartialFailure_DoesNotDoublePostClosingEntry()
    {
        // M2 (PR 3c council): if a prior invocation posted the closing
        // JE but failed mid-lock-loop, the second invocation must
        // reuse the stored ClosingJournalEntryId instead of posting a
        // second JE. Simulated here by pre-seeding the FY with a
        // fake-but-set ClosingJournalEntryId and proving the retry
        // doesn't post a second entry.
        var h = new Harness();
        var (rentRev, _, _) = await h.SeedChartWithRetainedEarningsAsync();
        var (fy, _) = await h.SeedYearWithMonthlyPeriodsAsync();
        h.Balances.Seed(rentRev.Id, -200m);
        var phantomJEId = JournalEntryId.NewId();
        await h.Years.UpdateAsync(fy with
        {
            ClosingJournalEntryId = phantomJEId,
            Version               = fy.Version + 1,
        });

        var result = await h.Sut.CloseFiscalYearAsync(fy.Id);

        Assert.True(result.IsSuccess, result.Detail);
        Assert.Equal(phantomJEId, result.ClosingEntryId);
        Assert.Empty(h.Store.Snapshot()); // no NEW JE posted
    }

    [Fact]
    public async Task ReopenFY_FlipsPeriodsLockedToSoftClosed()
    {
        var h = new Harness();
        await h.SeedChartWithRetainedEarningsAsync();
        var (fy, periods) = await h.SeedYearWithMonthlyPeriodsAsync();
        await h.Sut.CloseFiscalYearAsync(fy.Id);

        await h.Sut.ReopenFiscalYearAsync(fy.Id, auditMemo: "y-e adjustment");

        foreach (var pid in periods.Select(p => p.Id))
        {
            var stored = await h.Periods.GetAsync(pid);
            Assert.NotNull(stored);
            Assert.Equal(FiscalPeriodStatus.SoftClosed, stored!.Status);
            Assert.Null(stored.LockedAtUtc);
        }
    }

    // ----- harness ---------------------------------------------------

    private sealed class Harness
    {
        public InMemoryFiscalPeriodRepository Periods { get; } = new();
        public InMemoryFiscalYearRepository Years { get; } = new();
        public InMemoryChartRepository Charts { get; } = new();
        public InMemoryAccountTypeQuery Accounts { get; } = new();
        public InMemoryBalanceComputer Balances { get; } = new();
        public InMemoryAccountResolver AccountResolver { get; } = new();
        public InMemoryJournalStore Store { get; } = new();
        public CapturingEventPublisher Events { get; } = new();
        public PeriodCloseService PeriodClose { get; }
        public JournalPostingService Posting { get; }
        public FiscalYearCloseService Sut { get; }

        public Harness()
        {
            PeriodClose = new PeriodCloseService(Periods, Years, Events, TimeProvider.System);
            // FinancialAdmin role required so the closing JE can post
            // even when periods are SoftClosed.
            var user = new StaticUserContext("test-admin", new[] { "FinancialAdmin" });
            // Periods are Open at the start; we always SoftClose-then-Lock
            // them inside the algorithm, so the period-gating uses the
            // SoftClosed bypass for admin posts.
            var periodResolver = new InMemoryPeriodResolver()
                .WithStatus(IPeriodResolver.Status.SoftClosed);
            Posting = new JournalPostingService(AccountResolver, periodResolver, Store, user, TimeProvider.System);
            Sut = new FiscalYearCloseService(
                Years, Periods, PeriodClose, Charts, Accounts, Balances, Posting, Events,
                TimeProvider.System);
        }

        public Task<(GLAccount RentRevenue, GLAccount SupplyExpense, GLAccount RetainedEarnings)>
            SeedChartWithRetainedEarningsAsync()
        {
            var retained = GLAccount.Create(
                id: GLAccountId.NewId(), chartId: Chart, code: "3900",
                name: "Retained Earnings",
                type: GLAccountType.Equity, subtype: AccountSubtype.RetainedEarnings, currency: "USD");
            var rentRev = GLAccount.Create(
                id: GLAccountId.NewId(), chartId: Chart, code: "4100",
                name: "Rent Revenue",
                type: GLAccountType.Revenue, subtype: AccountSubtype.OperatingIncome, currency: "USD");
            var supplyExp = GLAccount.Create(
                id: GLAccountId.NewId(), chartId: Chart, code: "6100",
                name: "Supplies Expense",
                type: GLAccountType.Expense, subtype: AccountSubtype.OperatingExpense, currency: "USD");

            Charts.Upsert(new ChartOfAccounts(
                Id: Chart, LegalEntityId: LegalEntityId.NewId(),
                Name: "Test Chart", BaseCurrency: "USD",
                FiscalYearStartMonth: 1, FiscalYearStartDay: 1,
                RetainedEarningsAccountId: retained.Id,
                IsActive: true,
                CreatedAtUtc: Instant.Now, UpdatedAtUtc: Instant.Now));

            Accounts.Upsert(retained);
            Accounts.Upsert(rentRev);
            Accounts.Upsert(supplyExp);
            AccountResolver.Upsert(retained);
            AccountResolver.Upsert(rentRev);
            AccountResolver.Upsert(supplyExp);
            return Task.FromResult((rentRev, supplyExp, retained));
        }

        public async Task<(FiscalYear Fy, IReadOnlyList<FiscalPeriod> Periods)>
            SeedYearWithMonthlyPeriodsAsync()
        {
            var fy = FiscalYear.CreateOpen(
                FiscalYearId.NewId(), Chart, "2026",
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
            await Years.InsertAsync(fy);
            var periods = FiscalPeriodFactory.BuildMonthlyPeriods(fy);
            foreach (var p in periods) await Periods.InsertAsync(p);
            return (fy, periods);
        }
    }
}
