using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Seeds;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

public class DefaultTaxFormLineMapTests
{
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();

    [Fact]
    public void ScheduleE_2026_Returns20RowsAcrossLines3Through22()
    {
        // Hand-off prose said "19 rows" but its example seed enumerates
        // Lines 3-22 inclusive = 20 rows. Off-by-one in the prose; the
        // seed enumeration is correct (3..22 = 20 lines).
        var rows = DefaultTaxFormLineMap.ScheduleE(Chart());

        Assert.Equal(20, rows.Count);
        var actualLines = rows.Select(r => r.Line).ToHashSet(StringComparer.Ordinal);
        for (int n = 3; n <= 22; n++)
        {
            Assert.Contains($"Line{n}", actualLines);
        }
    }

    [Fact]
    public void ScheduleE_AllRowsAreProvisional()
    {
        var rows = DefaultTaxFormLineMap.ScheduleE(Chart());
        Assert.All(rows, r => Assert.True(r.IsProvisional, $"{r.Line} should be provisional pending ONR."));
        Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.ProvisionalRationale)));
    }

    [Fact]
    public void ScheduleE_AllRowsHaveCitationSource()
    {
        var rows = DefaultTaxFormLineMap.ScheduleE(Chart());
        Assert.All(rows, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.CitationSource));
            Assert.Contains("IRS Pub 527", r.CitationSource);
            Assert.Contains("Schedule E", r.CitationSource);
        });
    }

    [Fact]
    public void ScheduleE_PerPropertyDimensionTrueForAllIncomeAndExpenseLines()
    {
        var rows = DefaultTaxFormLineMap.ScheduleE(Chart());
        foreach (var r in rows)
        {
            Assert.True(r.PerPropertyDimension, $"{r.Line} should be per-property.");
        }
    }

    [Fact]
    public void ScheduleE_Line5_AdvertisingMapsToAccountCode5100()
    {
        var rows = DefaultTaxFormLineMap.ScheduleE(Chart());
        var line5 = rows.Single(r => r.Line == "Line5");
        Assert.Equal("Advertising", line5.Description);
        Assert.Contains(line5.AccountSelectors, s => s.AccountCode == "5100");
    }

    [Fact]
    public void ScheduleE_Line14_RepairsMapsToAccountCode5600()
    {
        var rows = DefaultTaxFormLineMap.ScheduleE(Chart());
        var line14 = rows.Single(r => r.Line == "Line14");
        Assert.Equal("Repairs", line14.Description);
        Assert.Contains(line14.AccountSelectors, s => s.AccountCode == "5600");
    }

    [Fact]
    public void ScheduleE_Line16_TaxesMapsToAccountCode6100_PropertyTax()
    {
        // Regression for the hand-off Halt 5 clarification: property tax
        // is NOT a TaxCode — it's an AP recurring bill mapped here via
        // chart code 6100.
        var rows = DefaultTaxFormLineMap.ScheduleE(Chart());
        var line16 = rows.Single(r => r.Line == "Line16");
        Assert.Equal("Taxes", line16.Description);
        Assert.Contains(line16.AccountSelectors, s => s.AccountCode == "6100");
    }

    [Fact]
    public void ScheduleE_Line18_DepreciationMapsToAccountCode7200()
    {
        var rows = DefaultTaxFormLineMap.ScheduleE(Chart());
        var line18 = rows.Single(r => r.Line == "Line18");
        Assert.Equal("Depreciation expense or depletion", line18.Description);
        Assert.Contains(line18.AccountSelectors, s => s.AccountCode == "7200");
    }

    [Fact]
    public void ScheduleE_Line20And21And22_HaveEmptyAccountSelectors_BecauseComputed()
    {
        var rows = DefaultTaxFormLineMap.ScheduleE(Chart());
        foreach (var line in new[] { "Line20", "Line21", "Line22" })
        {
            var r = rows.Single(x => x.Line == line);
            Assert.Empty(r.AccountSelectors);
        }
    }
}
