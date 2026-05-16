using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;
using Xunit;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="FiscalPeriodFactory"/>.
/// </summary>
public sealed class FiscalPeriodFactoryTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public void BuildMonthlyPeriods_ProducesTwelveForCalendarYear()
    {
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var periods = FiscalPeriodFactory.BuildMonthlyPeriods(fy);
        Assert.Equal(12, periods.Count);
    }

    [Fact]
    public void BuildMonthlyPeriods_ProducesContiguousNonOverlappingSet()
    {
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var periods = FiscalPeriodFactory.BuildMonthlyPeriods(fy);
        var result = FiscalPeriodCollectionValidator.Validate(fy, periods);
        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }

    [Fact]
    public void BuildMonthlyPeriods_LabelsAreFyLabelPlusMonthOrdinal()
    {
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var periods = FiscalPeriodFactory.BuildMonthlyPeriods(fy);
        Assert.Equal("2026-M01", periods[0].Label);
        Assert.Equal("2026-M12", periods[11].Label);
    }

    [Fact]
    public void BuildQuarterlyPeriods_ProducesFour()
    {
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var periods = FiscalPeriodFactory.BuildQuarterlyPeriods(fy);
        Assert.Equal(4, periods.Count);
        var result = FiscalPeriodCollectionValidator.Validate(fy, periods);
        Assert.True(result.IsValid, string.Join(" | ", result.Errors));
    }

    [Fact]
    public void BuildAnnualPeriod_ProducesOne()
    {
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var periods = FiscalPeriodFactory.BuildAnnualPeriod(fy);
        Assert.Single(periods);
        Assert.Equal(FiscalPeriodKind.Annual, periods[0].Kind);
        Assert.Equal("2026", periods[0].Label);
    }
}
