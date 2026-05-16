using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;
using static Sunfish.Blocks.FinancialPeriods.Tests.PeriodCloseServiceSoftCloseTests;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 PR 3a — coverage for the optimistic-concurrency
/// <see cref="FiscalPeriod.Version"/> CAS at the repository layer.
/// Reproduces the cross-window admin race scenario flagged by the PR 2
/// security council and tracked in
/// <c>icm/_state/handoffs/w60-p4-pr2-addendum.md</c> § D1.
/// </summary>
public sealed class ConcurrentUpdateTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task SoftClose_BumpsVersion()
    {
        var (sut, periods, _, _) = NewHarness();
        var (_, period) = await SeedAsync(periods);
        var before = period.Version;

        var result = await sut.SoftCloseAsync(period.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(before + 1, result.Period!.Version);
    }

    [Fact]
    public async Task Reopen_BumpsVersion()
    {
        var (sut, periods, _, _) = NewHarness();
        var (_, period) = await SeedAsync(periods, FiscalPeriodStatus.SoftClosed);

        var result = await sut.ReopenAsync(period.Id, auditMemo: "fix");

        Assert.True(result.IsSuccess);
        Assert.Equal(period.Version + 1, result.Period!.Version);
    }

    [Fact]
    public async Task UpdateWithStaleVersion_ReturnsFalse_FromRepo()
    {
        // Direct repo test: simulates "two windows fetched at the same
        // version, both produced an update, second write must lose".
        var periods = new InMemoryFiscalPeriodRepository();
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var period = FiscalPeriod.CreateOpen(
            FiscalPeriodId.NewId(), Chart, fy.Id,
            FiscalPeriodKind.Monthly, "2026-M06",
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));
        await periods.InsertAsync(period);

        // Window A writes (bumps to v1).
        var aFirst = period with
        {
            Status          = FiscalPeriodStatus.SoftClosed,
            SoftClosedAtUtc = Instant.Now,
            Version         = period.Version + 1,
        };
        Assert.True(await periods.UpdateAsync(aFirst));

        // Window B tries to write with the original (v0) baseline —
        // CAS rejects.
        var bStale = period with
        {
            Status          = FiscalPeriodStatus.Locked,
            SoftClosedAtUtc = Instant.Now,
            LockedAtUtc     = Instant.Now,
            Version         = period.Version + 1, // also v1 — collides
        };
        Assert.False(await periods.UpdateAsync(bStale));
    }

    [Fact]
    public async Task SoftClose_AfterParallelWriteWonByOtherWindow_ReturnsConcurrentUpdate()
    {
        // Service-level race: wrap the repo with a stale-snapshot
        // resolver so the service reads the pre-mutation row while the
        // repo holds a fresh one. This is the only way to repro a
        // CAS-reject at the service layer (a same-process service call
        // would normally re-fetch and succeed).
        var periods = new InMemoryFiscalPeriodRepository();
        var years   = new InMemoryFiscalYearRepository();
        var events  = new CapturingEventPublisher();
        var (_, period) = await SeedAsync(periods);

        // Parallel writer bumps to Version + 1 in the repo.
        var parallelWrite = period with { Label = "edit", Version = period.Version + 1 };
        Assert.True(await periods.UpdateAsync(parallelWrite));

        // Wrap the period repo so the service sees the stale snapshot.
        var stalePeriods = new StaleSnapshotFiscalPeriodRepository(periods, staleSnapshot: period);
        var sut = new PeriodCloseService(stalePeriods, years, events, TimeProvider.System);

        var result = await sut.SoftCloseAsync(period.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(PeriodCloseError.ConcurrentUpdate, result.Error);
    }

    private sealed class StaleSnapshotFiscalPeriodRepository : IFiscalPeriodRepository
    {
        private readonly InMemoryFiscalPeriodRepository _inner;
        private readonly FiscalPeriod _stale;
        public StaleSnapshotFiscalPeriodRepository(
            InMemoryFiscalPeriodRepository inner, FiscalPeriod staleSnapshot)
        { _inner = inner; _stale = staleSnapshot; }
        public Task<FiscalPeriod?> GetAsync(FiscalPeriodId id, CancellationToken ct = default)
            => Task.FromResult<FiscalPeriod?>(id.Equals(_stale.Id) ? _stale : null);
        public Task<IReadOnlyList<FiscalPeriod>> GetByFiscalYearAsync(FiscalYearId id, CancellationToken ct = default)
            => _inner.GetByFiscalYearAsync(id, ct);
        public Task<FiscalPeriod?> FindByChartAndDateAsync(ChartOfAccountsId c, DateOnly d, CancellationToken ct = default)
            => _inner.FindByChartAndDateAsync(c, d, ct);
        public Task InsertAsync(FiscalPeriod p, CancellationToken ct = default) => _inner.InsertAsync(p, ct);
        public Task<bool> UpdateAsync(FiscalPeriod p, CancellationToken ct = default) => _inner.UpdateAsync(p, ct);
        public Task<FiscalPeriod?> GetByExternalRefAsync(string r, CancellationToken ct = default)
            => _inner.GetByExternalRefAsync(r, ct);
    }

    // ----- helpers ---------------------------------------------------

    private static (PeriodCloseService Sut,
        InMemoryFiscalPeriodRepository Periods,
        InMemoryFiscalYearRepository Years,
        CapturingEventPublisher Events) NewHarness()
    {
        var periods = new InMemoryFiscalPeriodRepository();
        var years   = new InMemoryFiscalYearRepository();
        var events  = new CapturingEventPublisher();
        var sut     = new PeriodCloseService(periods, years, events, TimeProvider.System);
        return (sut, periods, years, events);
    }

    private static async Task<(FiscalYear Year, FiscalPeriod Period)> SeedAsync(
        InMemoryFiscalPeriodRepository periods,
        FiscalPeriodStatus periodStatus = FiscalPeriodStatus.Open)
    {
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

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
            _ => basePeriod,
        };

        await periods.InsertAsync(period);
        return (fy, period);
    }
}
