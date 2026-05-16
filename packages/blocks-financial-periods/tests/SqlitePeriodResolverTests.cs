using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="SqlitePeriodResolver"/>: maps the
/// authoritative <see cref="FiscalPeriod"/> rows from the repository
/// into the ledger's minimal <see cref="IPeriodResolver.PeriodSnapshot"/>
/// shape, including the status enum projection.
/// </summary>
public sealed class SqlitePeriodResolverTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task Resolve_DateWithinOpenPeriod_ReturnsOpenSnapshot()
    {
        var h = new Harness();
        var period = await h.SeedMonthlyAsync(2026, 6, FiscalPeriodStatus.Open);

        var snapshot = await h.Sut.ResolveAsync(Chart, new DateOnly(2026, 6, 15));

        Assert.NotNull(snapshot);
        Assert.Equal(period.Id.Value, snapshot!.Value.PeriodId);
        Assert.Equal(Chart.Value, snapshot.Value.ChartId);
        Assert.Equal(IPeriodResolver.Status.Open, snapshot.Value.Status);
    }

    [Fact]
    public async Task Resolve_DateOutsideAllPeriods_ReturnsNull()
    {
        var h = new Harness();
        await h.SeedMonthlyAsync(2026, 6, FiscalPeriodStatus.Open);

        var snapshot = await h.Sut.ResolveAsync(Chart, new DateOnly(2026, 7, 15));

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Resolve_DateOnStartBoundary_ReturnsContainingPeriod()
    {
        var h = new Harness();
        var period = await h.SeedMonthlyAsync(2026, 6, FiscalPeriodStatus.Open);

        var snapshot = await h.Sut.ResolveAsync(Chart, period.StartDate);

        Assert.NotNull(snapshot);
        Assert.Equal(period.Id.Value, snapshot!.Value.PeriodId);
    }

    [Fact]
    public async Task Resolve_DateOnEndBoundary_ReturnsContainingPeriod()
    {
        var h = new Harness();
        var period = await h.SeedMonthlyAsync(2026, 6, FiscalPeriodStatus.Open);

        var snapshot = await h.Sut.ResolveAsync(Chart, period.EndDate);

        Assert.NotNull(snapshot);
        Assert.Equal(period.Id.Value, snapshot!.Value.PeriodId);
    }

    [Fact]
    public async Task Resolve_TranslatesSoftClosedStatusCorrectly()
    {
        var h = new Harness();
        await h.SeedMonthlyAsync(2026, 6, FiscalPeriodStatus.SoftClosed);

        var snapshot = await h.Sut.ResolveAsync(Chart, new DateOnly(2026, 6, 15));

        Assert.NotNull(snapshot);
        Assert.Equal(IPeriodResolver.Status.SoftClosed, snapshot!.Value.Status);
    }

    [Fact]
    public async Task Resolve_TranslatesLockedStatusCorrectly()
    {
        var h = new Harness();
        await h.SeedMonthlyAsync(2026, 6, FiscalPeriodStatus.Locked);

        var snapshot = await h.Sut.ResolveAsync(Chart, new DateOnly(2026, 6, 15));

        Assert.NotNull(snapshot);
        Assert.Equal(IPeriodResolver.Status.Locked, snapshot!.Value.Status);
    }

    // ----- harness ---------------------------------------------------

    private sealed class Harness
    {
        public InMemoryFiscalPeriodRepository Periods { get; } = new();
        public SqlitePeriodResolver Sut { get; }

        public Harness() { Sut = new SqlitePeriodResolver(Periods); }

        public async Task<FiscalPeriod> SeedMonthlyAsync(int year, int month, FiscalPeriodStatus status)
        {
            var start = new DateOnly(year, month, 1);
            var end   = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            var basePeriod = FiscalPeriod.CreateOpen(
                FiscalPeriodId.NewId(), Chart, FiscalYearId.NewId(),
                FiscalPeriodKind.Monthly, $"{year}-M{month:00}", start, end);

            var period = status switch
            {
                FiscalPeriodStatus.SoftClosed => basePeriod with
                {
                    Status          = FiscalPeriodStatus.SoftClosed,
                    SoftClosedAtUtc = Instant.Now,
                },
                FiscalPeriodStatus.Locked => basePeriod with
                {
                    Status          = FiscalPeriodStatus.Locked,
                    SoftClosedAtUtc = Instant.Now,
                    LockedAtUtc     = Instant.Now,
                },
                _ => basePeriod,
            };

            await Periods.InsertAsync(period);
            return period;
        }
    }
}
