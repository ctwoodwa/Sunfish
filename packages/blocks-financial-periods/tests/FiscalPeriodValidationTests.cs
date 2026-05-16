using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 — collection-level coverage for
/// <see cref="FiscalPeriodCollectionValidator"/> per Stage 02 §3.16
/// rules 1–3.
/// </summary>
public sealed class FiscalPeriodValidationTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public void Validate_AcceptsContiguousMonthlyPeriods_FullCalendarYear()
    {
        var fy = NewYear();
        var periods = FiscalPeriodFactory.BuildMonthlyPeriods(fy);
        var result = FiscalPeriodCollectionValidator.Validate(fy, periods);
        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }

    [Fact]
    public void Validate_RejectsGapBetweenPeriods()
    {
        var fy = NewYear();
        var jan = NewPeriod(fy, "2026-M01", new(2026, 1, 1), new(2026, 1, 31));
        var mar = NewPeriod(fy, "2026-M03", new(2026, 3, 1), new(2026, 3, 31));
        var result = FiscalPeriodCollectionValidator.Validate(fy, new[] { jan, mar });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("gap or overlap"));
    }

    [Fact]
    public void Validate_RejectsOverlappingPeriods()
    {
        var fy = NewYear();
        var jan = NewPeriod(fy, "2026-M01", new(2026, 1, 1), new(2026, 1, 31));
        var janFeb = NewPeriod(fy, "2026-M01b", new(2026, 1, 15), new(2026, 2, 14));
        var result = FiscalPeriodCollectionValidator.Validate(fy, new[] { jan, janFeb });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsPeriodSetStartingAfterFyStart()
    {
        var fy = NewYear();
        var feb = NewPeriod(fy, "2026-M02", new(2026, 2, 1), new(2026, 12, 31));
        var result = FiscalPeriodCollectionValidator.Validate(fy, new[] { feb });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("starts"));
    }

    [Fact]
    public void Validate_RejectsPeriodSetEndingBeforeFyEnd()
    {
        var fy = NewYear();
        var partial = NewPeriod(fy, "2026-partial", new(2026, 1, 1), new(2026, 11, 30));
        var result = FiscalPeriodCollectionValidator.Validate(fy, new[] { partial });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ends"));
    }

    [Fact]
    public void Validate_RejectsLockedPeriodWhenFyOpen()
    {
        var fy = NewYear();
        var locked = NewPeriod(fy, "2026-M01", new(2026, 1, 1), new(2026, 12, 31)) with
        {
            Status = FiscalPeriodStatus.Locked,
            SoftClosedAtUtc = Instant.Now,
            LockedAtUtc = Instant.Now,
        };
        var result = FiscalPeriodCollectionValidator.Validate(fy, new[] { locked });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Locked"));
    }

    // ----- helpers ---------------------------------------------------

    private static FiscalYear NewYear() =>
        FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

    private static FiscalPeriod NewPeriod(FiscalYear fy, string label, DateOnly start, DateOnly end) =>
        FiscalPeriod.CreateOpen(
            FiscalPeriodId.NewId(), fy.ChartId, fy.Id,
            FiscalPeriodKind.Monthly, label, start, end);
}
