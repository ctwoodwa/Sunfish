using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Migration;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 PR 4 — coverage for
/// <see cref="ErpnextFiscalPeriodImporter"/> period synthesis +
/// idempotency.
/// </summary>
public sealed class ErpnextFiscalPeriodImporterTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task SynthesizePeriods_EmptyFy_InsertsTwelveMonthlyPeriods()
    {
        var h = new Harness();
        var fy = await h.SeedFyAsync();

        var outcomes = await h.Sut.SynthesizePeriodsForFiscalYearAsync(fy.Id);

        Assert.Equal(12, outcomes.Count);
        Assert.All(outcomes, o => Assert.Equal(ImportAction.Inserted, o.Action));
        var stored = await h.Periods.GetByFiscalYearAsync(fy.Id);
        Assert.Equal(12, stored.Count);
    }

    [Fact]
    public async Task SynthesizePeriods_PeriodsAlreadyExist_ReturnsSkippedForAll()
    {
        var h = new Harness();
        var fy = await h.SeedFyAsync();
        await h.Sut.SynthesizePeriodsForFiscalYearAsync(fy.Id); // first pass

        var second = await h.Sut.SynthesizePeriodsForFiscalYearAsync(fy.Id);

        Assert.Equal(12, second.Count);
        Assert.All(second, o => Assert.Equal(ImportAction.Skipped, o.Action));
    }

    [Fact]
    public async Task SynthesizePeriods_QuarterlyKind_InsertsFour()
    {
        var h = new Harness();
        var fy = await h.SeedFyAsync();

        var outcomes = await h.Sut.SynthesizePeriodsForFiscalYearAsync(fy.Id, FiscalPeriodKind.Quarterly);

        Assert.Equal(4, outcomes.Count);
        Assert.All(outcomes, o => Assert.Equal(ImportAction.Inserted, o.Action));
    }

    [Fact]
    public async Task SynthesizePeriods_AnnualKind_InsertsOne()
    {
        var h = new Harness();
        var fy = await h.SeedFyAsync();

        var outcomes = await h.Sut.SynthesizePeriodsForFiscalYearAsync(fy.Id, FiscalPeriodKind.Annual);

        Assert.Single(outcomes);
        Assert.Equal(ImportAction.Inserted, outcomes[0].Action);
        Assert.Equal(FiscalPeriodKind.Annual, outcomes[0].Record.Kind);
    }

    [Fact]
    public async Task SynthesizePeriods_ProducedSetPassesCollectionValidator()
    {
        var h = new Harness();
        var fy = await h.SeedFyAsync();

        var outcomes = await h.Sut.SynthesizePeriodsForFiscalYearAsync(fy.Id);
        var validation = FiscalPeriodCollectionValidator.Validate(
            fy, outcomes.Select(o => o.Record).ToList());

        Assert.True(validation.IsValid, string.Join(" | ", validation.Errors));
    }

    [Fact]
    public async Task SynthesizePeriods_ShortYear_LastPeriodTruncatedToFyEnd()
    {
        // 9-month FY (Jan 1 → Sep 15) — FiscalPeriodFactory clips the
        // last monthly period to fy.EndDate. Validates the synthesized
        // set passes the collection validator with a non-month-aligned
        // end.
        var h = new Harness();
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026-short",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 15));
        await h.Years.InsertAsync(fy);

        var outcomes = await h.Sut.SynthesizePeriodsForFiscalYearAsync(fy.Id);

        Assert.NotEmpty(outcomes);
        Assert.All(outcomes, o => Assert.Equal(ImportAction.Inserted, o.Action));
        Assert.Equal(new DateOnly(2026, 9, 15), outcomes[^1].Record.EndDate);
        var validation = FiscalPeriodCollectionValidator.Validate(
            fy, outcomes.Select(o => o.Record).ToList());
        Assert.True(validation.IsValid, string.Join(" | ", validation.Errors));
    }

    [Fact]
    public async Task SynthesizePeriods_UnknownFy_ReturnsEmpty()
    {
        var h = new Harness();
        var outcomes = await h.Sut.SynthesizePeriodsForFiscalYearAsync(FiscalYearId.NewId());

        Assert.Empty(outcomes);
    }

    // ----- helpers ---------------------------------------------------

    private sealed class Harness
    {
        public InMemoryFiscalYearRepository Years { get; } = new();
        public InMemoryFiscalPeriodRepository Periods { get; } = new();
        public ErpnextFiscalPeriodImporter Sut { get; }

        public Harness()
        {
            Sut = new ErpnextFiscalPeriodImporter(Years, Periods);
        }

        public async Task<FiscalYear> SeedFyAsync()
        {
            var fy = FiscalYear.CreateOpen(
                FiscalYearId.NewId(), Chart, "2026",
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
            await Years.InsertAsync(fy);
            return fy;
        }
    }
}
