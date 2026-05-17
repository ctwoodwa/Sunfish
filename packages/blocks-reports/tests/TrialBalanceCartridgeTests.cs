using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Blocks.Reports.Cartridges.TrialBalance;
using Sunfish.Blocks.Reports.Exceptions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

public sealed class TrialBalanceCartridgeTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();
    private static readonly FiscalYearId FY = FiscalYearId.NewId();
    private static readonly TenantId Tenant = new("tenant-tb");
    private static readonly PrincipalId Principal = PrincipalId.FromBytes(new byte[32]);

    private static GLAccount Acct(string code, GLAccountType type, bool isActive = true)
        => GLAccount.Create(GLAccountId.NewId(), Chart, code, $"Account {code}",
            type, type == GLAccountType.Asset ? AccountSubtype.BankAccount : AccountSubtype.OperatingIncome, "USD")
            with { IsActive = isActive };

    private static JournalEntry PostedEntry(DateOnly date, GLAccountId debit, GLAccountId credit, decimal amount)
        => new JournalEntry(
                id: JournalEntryId.NewId(),
                entryDate: date,
                memo: "test",
                lines: new[]
                {
                    new JournalEntryLine(debit, debit: amount, credit: 0m),
                    new JournalEntryLine(credit, debit: 0m, credit: amount),
                },
                createdAtUtc: Instant.Now)
            with { Status = JournalEntryStatus.Posted, ChartId = Chart };

    private sealed class StubChartRepo : IChartRepository
    {
        public ChartOfAccounts? Chart { get; set; }
        public Task<ChartOfAccounts?> GetAsync(ChartOfAccountsId chartId, CancellationToken ct = default)
            => Task.FromResult(Chart);
    }

    private static ChartOfAccounts MakeChart()
        => new ChartOfAccounts(Chart, LegalEntityId.NewId(), "Test", "USD", 1, 1, null, true, Instant.Now, Instant.Now);

    private static ReportExecutionContext Context()
        => new ReportExecutionContext(Tenant, "marker:1", new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero), Principal);

    private static (TrialBalanceCartridge Cartridge, StubChartRepo Charts, InMemoryAccountResolver Accounts,
                    InMemoryFiscalPeriodRepository Periods, InMemoryJournalStore Journals)
        Build(IEnumerable<GLAccount>? seedAccounts = null)
    {
        var charts = new StubChartRepo { Chart = MakeChart() };
        var accounts = new InMemoryAccountResolver(seedAccounts ?? Array.Empty<GLAccount>());
        var periods = new InMemoryFiscalPeriodRepository();
        var journals = new InMemoryJournalStore();
        var ledger = new InMemoryGeneralLedgerReadModel(journals);
        var cartridge = new TrialBalanceCartridge(charts, accounts, periods, ledger);
        return (cartridge, charts, accounts, periods, journals);
    }

    [Fact]
    public async Task TrialBalance_EmptyChart_ReturnsZeroTotalsAndEmptyRows()
    {
        var (sut, _, _, _, _) = Build();
        var result = await sut.ExecuteAsync(Context(),
            new TrialBalanceParameters { ChartId = Chart, AsOfDate = new DateOnly(2026, 12, 31) });
        Assert.Empty(result.Rows);
        Assert.Equal(0m, result.TotalDebit);
        Assert.Equal(0m, result.TotalCredit);
        Assert.True(result.IsBalanced);
    }

    [Fact]
    public async Task TrialBalance_SingleAccountWithBalance_AppearsInDebitColumn()
    {
        var cash = Acct("1000", GLAccountType.Asset);
        var revenue = Acct("4000", GLAccountType.Revenue);
        var (sut, _, _, _, journals) = Build(new[] { cash, revenue });
        await journals.SaveAtomicAsync(PostedEntry(new DateOnly(2026, 5, 1), cash.Id, revenue.Id, 100m));

        var result = await sut.ExecuteAsync(Context(),
            new TrialBalanceParameters { ChartId = Chart, AsOfDate = new DateOnly(2026, 12, 31) });

        var cashRow = result.Rows.Single(r => r.AccountId == cash.Id);
        Assert.Equal(100m, cashRow.DebitBalance);
        Assert.Equal(0m, cashRow.CreditBalance);
        var revRow = result.Rows.Single(r => r.AccountId == revenue.Id);
        Assert.Equal(0m, revRow.DebitBalance);
        Assert.Equal(100m, revRow.CreditBalance);   // Revenue is credit-normal; raw is -100 → projects to credit 100
    }

    [Fact]
    public async Task TrialBalance_BalancedChart_IsBalancedTrue()
    {
        var cash = Acct("1000", GLAccountType.Asset);
        var revenue = Acct("4000", GLAccountType.Revenue);
        var (sut, _, _, _, journals) = Build(new[] { cash, revenue });
        await journals.SaveAtomicAsync(PostedEntry(new DateOnly(2026, 5, 1), cash.Id, revenue.Id, 100m));
        await journals.SaveAtomicAsync(PostedEntry(new DateOnly(2026, 5, 15), cash.Id, revenue.Id, 50m));

        var result = await sut.ExecuteAsync(Context(),
            new TrialBalanceParameters { ChartId = Chart, AsOfDate = new DateOnly(2026, 12, 31) });

        Assert.True(result.IsBalanced);
        Assert.Equal(result.TotalDebit, result.TotalCredit);
        Assert.Equal(150m, result.TotalDebit);
    }

    [Fact]
    public async Task TrialBalance_NeitherPeriodNorAsOfDate_ThrowsValidationException()
    {
        var (sut, _, _, _, _) = Build();
        await Assert.ThrowsAsync<ReportParameterValidationException>(() =>
            sut.ExecuteAsync(Context(), new TrialBalanceParameters { ChartId = Chart }));
    }

    [Fact]
    public async Task TrialBalance_BothPeriodAndAsOfDate_ThrowsValidationException()
    {
        var (sut, _, _, _, _) = Build();
        await Assert.ThrowsAsync<ReportParameterValidationException>(() =>
            sut.ExecuteAsync(Context(), new TrialBalanceParameters
            {
                ChartId = Chart,
                FiscalPeriodId = FiscalPeriodId.NewId(),
                AsOfDate = new DateOnly(2026, 12, 31),
            }));
    }

    [Fact]
    public async Task TrialBalance_UnknownChart_ThrowsValidationException()
    {
        var (sut, charts, _, _, _) = Build();
        charts.Chart = null;
        await Assert.ThrowsAsync<ReportParameterValidationException>(() =>
            sut.ExecuteAsync(Context(),
                new TrialBalanceParameters { ChartId = Chart, AsOfDate = new DateOnly(2026, 12, 31) }));
    }

    [Fact]
    public async Task TrialBalance_IncludeInactiveFalse_OmitsInactiveAccounts()
    {
        var active = Acct("1000", GLAccountType.Asset);
        var inactive = Acct("1001", GLAccountType.Asset, isActive: false);
        var (sut, _, _, _, journals) = Build(new[] { active, inactive });
        await journals.SaveAtomicAsync(PostedEntry(new DateOnly(2026, 5, 1), active.Id, inactive.Id, 100m));

        var result = await sut.ExecuteAsync(Context(),
            new TrialBalanceParameters { ChartId = Chart, AsOfDate = new DateOnly(2026, 12, 31), IncludeZeroBalanceAccounts = true });

        Assert.DoesNotContain(result.Rows, r => r.AccountId == inactive.Id);
    }

    [Fact]
    public async Task TrialBalance_IncludeInactiveTrue_IncludesInactiveAccounts()
    {
        var active = Acct("1000", GLAccountType.Asset);
        var inactive = Acct("1001", GLAccountType.Asset, isActive: false);
        var (sut, _, _, _, journals) = Build(new[] { active, inactive });
        await journals.SaveAtomicAsync(PostedEntry(new DateOnly(2026, 5, 1), active.Id, inactive.Id, 100m));

        var result = await sut.ExecuteAsync(Context(),
            new TrialBalanceParameters { ChartId = Chart, AsOfDate = new DateOnly(2026, 12, 31), IncludeInactiveAccounts = true });

        Assert.Contains(result.Rows, r => r.AccountId == inactive.Id);
    }

    [Fact]
    public async Task TrialBalance_IncludeZeroFalse_OmitsZeroBalanceAccounts()
    {
        var withBalance = Acct("1000", GLAccountType.Asset);
        var zero = Acct("1001", GLAccountType.Asset);
        var revenue = Acct("4000", GLAccountType.Revenue);
        var (sut, _, _, _, journals) = Build(new[] { withBalance, zero, revenue });
        await journals.SaveAtomicAsync(PostedEntry(new DateOnly(2026, 5, 1), withBalance.Id, revenue.Id, 100m));

        var result = await sut.ExecuteAsync(Context(),
            new TrialBalanceParameters { ChartId = Chart, AsOfDate = new DateOnly(2026, 12, 31), IncludeZeroBalanceAccounts = false });

        Assert.DoesNotContain(result.Rows, r => r.AccountId == zero.Id);
    }

    [Fact]
    public async Task TrialBalance_IncludeZeroTrue_IncludesZeroBalanceAccounts()
    {
        var withBalance = Acct("1000", GLAccountType.Asset);
        var zero = Acct("1001", GLAccountType.Asset);
        var revenue = Acct("4000", GLAccountType.Revenue);
        var (sut, _, _, _, journals) = Build(new[] { withBalance, zero, revenue });
        await journals.SaveAtomicAsync(PostedEntry(new DateOnly(2026, 5, 1), withBalance.Id, revenue.Id, 100m));

        var result = await sut.ExecuteAsync(Context(),
            new TrialBalanceParameters { ChartId = Chart, AsOfDate = new DateOnly(2026, 12, 31), IncludeZeroBalanceAccounts = true });

        Assert.Contains(result.Rows, r => r.AccountId == zero.Id);
    }

    [Fact]
    public async Task TrialBalance_PeriodLocked_IsProvisionalFalse()
    {
        var (sut, _, _, periods, _) = Build();
        var period = FiscalPeriod.CreateOpen(FiscalPeriodId.NewId(), Chart, FY, FiscalPeriodKind.Monthly, "2026-05",
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), Instant.Now)
            with { Status = FiscalPeriodStatus.Locked };
        await periods.InsertAsync(period);

        var result = await sut.ExecuteAsync(Context(),
            new TrialBalanceParameters { ChartId = Chart, FiscalPeriodId = period.Id });

        Assert.False(result.IsProvisional);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task TrialBalance_PeriodOpen_IsProvisionalTrue_WithWarning()
    {
        var (sut, _, _, periods, _) = Build();
        var period = FiscalPeriod.CreateOpen(FiscalPeriodId.NewId(), Chart, FY, FiscalPeriodKind.Monthly, "2026-05",
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), Instant.Now);
        await periods.InsertAsync(period);

        var result = await sut.ExecuteAsync(Context(),
            new TrialBalanceParameters { ChartId = Chart, FiscalPeriodId = period.Id });

        Assert.True(result.IsProvisional);
        Assert.Contains(result.Warnings, w => w.Contains("Open"));
    }

    [Fact]
    public async Task TrialBalance_AsOfDateWithoutPeriod_IsProvisionalFalse()
    {
        var (sut, _, _, _, _) = Build();
        var result = await sut.ExecuteAsync(Context(),
            new TrialBalanceParameters { ChartId = Chart, AsOfDate = new DateOnly(2026, 5, 31) });
        Assert.False(result.IsProvisional);
    }

    [Fact]
    public async Task TrialBalance_Rows_AreOrderedByCodeThenIdStable()
    {
        var a3 = Acct("3000", GLAccountType.Equity);
        var a1 = Acct("1000", GLAccountType.Asset);
        var a2 = Acct("2000", GLAccountType.Liability);
        var rev = Acct("4000", GLAccountType.Revenue);
        var (sut, _, _, _, journals) = Build(new[] { a3, a1, a2, rev });
        await journals.SaveAtomicAsync(PostedEntry(new DateOnly(2026, 5, 1), a1.Id, rev.Id, 100m));
        await journals.SaveAtomicAsync(PostedEntry(new DateOnly(2026, 5, 2), a2.Id, a3.Id, 50m));

        var result = await sut.ExecuteAsync(Context(),
            new TrialBalanceParameters { ChartId = Chart, AsOfDate = new DateOnly(2026, 12, 31) });

        var codes = result.Rows.Select(r => r.AccountCode).ToArray();
        Assert.Equal(codes.OrderBy(c => c, StringComparer.Ordinal).ToArray(), codes);
    }

    [Fact]
    public async Task TrialBalance_PeriodFromDifferentChart_ThrowsValidationException()
    {
        var (sut, _, _, periods, _) = Build();
        var otherChart = ChartOfAccountsId.NewId();
        var period = FiscalPeriod.CreateOpen(FiscalPeriodId.NewId(), otherChart, FY, FiscalPeriodKind.Monthly, "2026-05",
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), Instant.Now);
        await periods.InsertAsync(period);

        await Assert.ThrowsAsync<ReportParameterValidationException>(() =>
            sut.ExecuteAsync(Context(),
                new TrialBalanceParameters { ChartId = Chart, FiscalPeriodId = period.Id }));
    }
}
