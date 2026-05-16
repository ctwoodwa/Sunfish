using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Financial;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;
using static Sunfish.Blocks.FinancialPeriods.Tests.PeriodCloseServiceSoftCloseTests;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 PR 3a — coverage for <see cref="PeriodCloseService.UnlockAsync"/>
/// per Stage 02 §8.5 row 3 reverse path.
/// </summary>
public sealed class PeriodCloseServiceUnlockTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task Unlock_LockedPeriod_TransitionsToSoftClosed()
    {
        var h = new UnlockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.Locked);

        var result = await h.Sut.UnlockAsync(period.Id, auditMemo: "audit reopen");

        Assert.True(result.IsSuccess, result.Detail);
        Assert.Equal(FiscalPeriodStatus.SoftClosed, result.Period!.Status);
        Assert.Null(result.Period.LockedAtUtc);
    }

    [Fact]
    public async Task Unlock_EmptyAuditMemo_ReturnsAuditMemoRequired()
    {
        var h = new UnlockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.Locked);

        var result = await h.Sut.UnlockAsync(period.Id, auditMemo: "   ");

        Assert.False(result.IsSuccess);
        Assert.Equal(PeriodCloseError.AuditMemoRequired, result.Error);
    }

    [Fact]
    public async Task Unlock_OpenPeriod_ReturnsPeriodNotLocked()
    {
        var h = new UnlockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.Open);

        var result = await h.Sut.UnlockAsync(period.Id, auditMemo: "x");

        Assert.False(result.IsSuccess);
        Assert.Equal(PeriodCloseError.PeriodNotLocked, result.Error);
    }

    [Fact]
    public async Task Unlock_SoftClosedPeriod_ReturnsPeriodNotLocked()
    {
        var h = new UnlockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.SoftClosed);

        var result = await h.Sut.UnlockAsync(period.Id, auditMemo: "x");

        Assert.False(result.IsSuccess);
        Assert.Equal(PeriodCloseError.PeriodNotLocked, result.Error);
    }

    [Fact]
    public async Task Unlock_PeriodInClosedFy_ReturnsFiscalYearAlreadyClosed()
    {
        var h = new UnlockHarness();
        var (fy, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.Locked);
        await h.Years.UpdateAsync(fy with
        {
            Status      = FiscalYearStatus.Closed,
            ClosedAtUtc = Instant.Now,
            Version     = fy.Version + 1,
        });

        var result = await h.Sut.UnlockAsync(period.Id, auditMemo: "year-end-after");

        Assert.False(result.IsSuccess);
        Assert.Equal(PeriodCloseError.FiscalYearAlreadyClosed, result.Error);
    }

    [Fact]
    public async Task Unlock_EmitsPeriodOpenedEvent_WithUnlockReason()
    {
        var h = new UnlockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.Locked);

        await h.Sut.UnlockAsync(period.Id, auditMemo: "SEC audit clarification");

        var evt = Assert.Single(h.Events.Published.OfType<PeriodOpened>().ToList());
        Assert.Equal(period.Id, evt.PeriodId);
        Assert.NotNull(evt.Reason);
        Assert.StartsWith("Unlocked by admin:", evt.Reason!);
        Assert.Contains("SEC audit clarification", evt.Reason!);
    }

    [Fact]
    public async Task Unlock_BumpsVersion()
    {
        var h = new UnlockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.Locked);
        var before = period.Version;

        var result = await h.Sut.UnlockAsync(period.Id, "memo");

        Assert.True(result.IsSuccess);
        Assert.Equal(before + 1, result.Period!.Version);
    }

    [Fact]
    public async Task Unlock_ReStampsSoftClosedAtUtc_ToUnlockInstant()
    {
        // M2 (PR 3a council): the preserved-from-original SoftClosedAtUtc
        // would be stale by the time we unlock; re-stamping reflects the
        // new soft-close start.
        var h = new UnlockHarness();
        var (_, period) = await h.SeedAsync(periodStatus: FiscalPeriodStatus.Locked);
        var prior = period.SoftClosedAtUtc!.Value;
        await Task.Delay(10);

        var result = await h.Sut.UnlockAsync(period.Id, "audit");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Period!.SoftClosedAtUtc);
        Assert.True(result.Period.SoftClosedAtUtc!.Value.Value > prior.Value);
    }

    // ----- harness ---------------------------------------------------

    private sealed class UnlockHarness
    {
        public InMemoryFiscalPeriodRepository Periods { get; } = new();
        public InMemoryFiscalYearRepository Years { get; } = new();
        public CapturingEventPublisher Events { get; } = new();
        public PeriodCloseService Sut { get; }

        public UnlockHarness()
        {
            Sut = new PeriodCloseService(Periods, Years, Events, TimeProvider.System);
        }

        public async Task<(FiscalYear Year, FiscalPeriod Period)> SeedAsync(
            FiscalPeriodStatus periodStatus = FiscalPeriodStatus.Locked)
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
