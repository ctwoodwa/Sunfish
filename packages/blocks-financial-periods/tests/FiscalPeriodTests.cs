using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="FiscalPeriod"/> per Stage 02 §3.16.
/// </summary>
public sealed class FiscalPeriodTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();
    private static readonly FiscalYearId Fy = FiscalYearId.NewId();

    [Fact]
    public void CreateOpen_PopulatesStatusOpen_AndNullCloseFields()
    {
        var p = FiscalPeriod.CreateOpen(
            FiscalPeriodId.NewId(), Chart, Fy, FiscalPeriodKind.Monthly, "2026-M01",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        Assert.Equal(FiscalPeriodStatus.Open, p.Status);
        Assert.Null(p.SoftClosedAtUtc);
        Assert.Null(p.LockedAtUtc);
        Assert.Null(p.ClosingJournalEntryId);
    }

    [Fact]
    public void Contains_ReturnsTrueOnBoundaries()
    {
        var p = FiscalPeriod.CreateOpen(
            FiscalPeriodId.NewId(), Chart, Fy, FiscalPeriodKind.Monthly, "2026-M01",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        Assert.True(p.Contains(new DateOnly(2026, 1, 1)));
        Assert.True(p.Contains(new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public void Contains_ReturnsFalseOutsideRange()
    {
        var p = FiscalPeriod.CreateOpen(
            FiscalPeriodId.NewId(), Chart, Fy, FiscalPeriodKind.Monthly, "2026-M01",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        Assert.False(p.Contains(new DateOnly(2025, 12, 31)));
        Assert.False(p.Contains(new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void Validate_RejectsStartAfterEnd()
    {
        var p = FiscalPeriod.CreateOpen(
            FiscalPeriodId.NewId(), Chart, Fy, FiscalPeriodKind.Monthly, "bad",
            new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1));
        Assert.NotEmpty(p.Validate());
    }

    [Fact]
    public void Validate_RejectsSoftClosedWithNullTimestamp()
    {
        var p = new FiscalPeriod(
            Id: FiscalPeriodId.NewId(), ChartId: Chart, FiscalYearId: Fy,
            Kind: FiscalPeriodKind.Monthly, Label: "2026-M01",
            StartDate: new DateOnly(2026, 1, 1), EndDate: new DateOnly(2026, 1, 31),
            Status: FiscalPeriodStatus.SoftClosed,
            SoftClosedAtUtc: null, LockedAtUtc: null,
            ClosingJournalEntryId: null, CreatedAtUtc: Instant.Now);
        Assert.NotEmpty(p.Validate());
    }

    [Fact]
    public void Validate_RejectsLockedWithNullSoftClosedTimestamp()
    {
        var p = new FiscalPeriod(
            Id: FiscalPeriodId.NewId(), ChartId: Chart, FiscalYearId: Fy,
            Kind: FiscalPeriodKind.Monthly, Label: "2026-M01",
            StartDate: new DateOnly(2026, 1, 1), EndDate: new DateOnly(2026, 1, 31),
            Status: FiscalPeriodStatus.Locked,
            SoftClosedAtUtc: null, LockedAtUtc: Instant.Now,
            ClosingJournalEntryId: null, CreatedAtUtc: Instant.Now);
        Assert.NotEmpty(p.Validate());
    }
}
