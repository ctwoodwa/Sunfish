using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Financial;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;
using static Sunfish.Blocks.FinancialPeriods.Tests.PeriodCloseServiceSoftCloseTests;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 PR 3a — coverage for <see cref="PeriodCloseService.LockAsync"/>
/// per Stage 02 §8.5 row 3. Lock is reachable from Open
/// (auto-soft-close inline) or SoftClosed; rejects already-Locked +
/// unknown.
/// </summary>
public sealed class PeriodCloseServiceLockTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task Lock_SoftClosedPeriod_TransitionsToLocked()
    {
        var h = new LockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.SoftClosed);

        var result = await h.Sut.LockAsync(period.Id);

        Assert.True(result.IsSuccess, result.Detail);
        Assert.Equal(FiscalPeriodStatus.Locked, result.Period!.Status);
        Assert.NotNull(result.Period.LockedAtUtc);
        // SoftClosedAtUtc is preserved from the prior state.
        Assert.NotNull(result.Period.SoftClosedAtUtc);
    }

    [Fact]
    public async Task Lock_OpenPeriod_AutoSoftClosesInlineAndLocks()
    {
        // Convenience for year-end close helpers (PR 3b) that pass
        // Open periods rather than pre-soft-closing them.
        var h = new LockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.Open);

        var result = await h.Sut.LockAsync(period.Id);

        Assert.True(result.IsSuccess, result.Detail);
        Assert.Equal(FiscalPeriodStatus.Locked, result.Period!.Status);
        Assert.NotNull(result.Period.SoftClosedAtUtc);
        Assert.NotNull(result.Period.LockedAtUtc);
    }

    [Fact]
    public async Task Lock_AlreadyLocked_ReturnsPeriodAlreadyLocked()
    {
        var h = new LockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.Locked);

        var result = await h.Sut.LockAsync(period.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(PeriodCloseError.PeriodAlreadyLocked, result.Error);
    }

    [Fact]
    public async Task Lock_UnknownPeriod_ReturnsPeriodNotFound()
    {
        var h = new LockHarness();
        var unknown = FiscalPeriodId.NewId();

        var result = await h.Sut.LockAsync(unknown);

        Assert.False(result.IsSuccess);
        Assert.Equal(PeriodCloseError.PeriodNotFound, result.Error);
        Assert.Equal(unknown.Value, result.Detail);
    }

    [Fact]
    public async Task Lock_EmitsPeriodLockedEvent()
    {
        var h = new LockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.SoftClosed);

        await h.Sut.LockAsync(period.Id);

        var evt = Assert.Single(h.Events.Published.OfType<PeriodLocked>().ToList());
        Assert.Equal(period.Id, evt.PeriodId);
        Assert.Equal(period.ChartId, evt.ChartId);
        // Direct SoftClosed → Locked does NOT also emit PeriodSoftClosed.
        Assert.Empty(h.Events.Published.OfType<PeriodSoftClosed>().ToList());
    }

    [Fact]
    public async Task Lock_AutoSoftClosesOpen_EmitsBothEventsInOrder()
    {
        // M1 (PR 3a council): the auto-soft-close convenience must
        // still emit PeriodSoftClosed BEFORE PeriodLocked so observers
        // gating on soft-close (AR aging snapshots, reports cluster)
        // see the intermediate transition.
        var h = new LockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.Open);

        await h.Sut.LockAsync(period.Id);

        Assert.Collection(
            h.Events.Published,
            e => Assert.IsType<PeriodSoftClosed>(e),
            e => Assert.IsType<PeriodLocked>(e));
    }

    [Fact]
    public async Task Lock_BumpsVersion()
    {
        var h = new LockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.SoftClosed);
        var versionBefore = period.Version;

        var result = await h.Sut.LockAsync(period.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(versionBefore + 1, result.Period!.Version);
    }

    // ----- harness ---------------------------------------------------

    private sealed class LockHarness
    {
        public InMemoryFiscalPeriodRepository Periods { get; } = new();
        public InMemoryFiscalYearRepository Years { get; } = new();
        public CapturingEventPublisher Events { get; } = new();
        public PeriodCloseService Sut { get; }

        public LockHarness()
        {
            Sut = new PeriodCloseService(Periods, Years, Events, TimeProvider.System);
        }

        public async Task<(FiscalYear Year, FiscalPeriod Period)> SeedAsync(
            FiscalPeriodStatus periodStatus = FiscalPeriodStatus.SoftClosed)
        {
            var fy = FiscalYear.CreateOpen(
                FiscalYearId.NewId(), Chart, "2026",
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
            await Years.InsertAsync(fy);

            var basePeriod = FiscalPeriod.CreateOpen(
                FiscalPeriodId.NewId(), fy.ChartId, fy.Id,
                FiscalPeriodKind.Monthly, "2026-M06",
                new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

            var period = periodStatus switch
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
            return (fy, period);
        }
    }
}
