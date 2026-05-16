using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

public class TaxFormLineMapTests
{
    private static ChartOfAccountsId NewChart() => ChartOfAccountsId.NewId();

    [Fact]
    public void Create_DefaultsIsActiveTrueIsProvisionalAsGiven()
    {
        var map = TaxFormLineMap.Create(
            chartId: NewChart(),
            formKind: TaxFormKind.ScheduleE,
            taxYear: 2026,
            line: "Line5",
            description: "Advertising",
            selectors: new[] { new TaxAccountSelector(AccountCode: "5100") },
            perPropertyDimension: true,
            isProvisional: true);

        Assert.True(map.IsActive);
        Assert.True(map.IsProvisional);
        Assert.Equal(1, map.Version);
        Assert.Null(map.DeletedAtUtc);

        var ratified = TaxFormLineMap.Create(
            chartId: NewChart(),
            formKind: TaxFormKind.ScheduleE,
            taxYear: 2026,
            line: "Line5",
            description: "Advertising",
            selectors: new[] { new TaxAccountSelector(AccountCode: "5100") },
            perPropertyDimension: true,
            isProvisional: false);

        Assert.False(ratified.IsProvisional);
    }

    [Fact]
    public void Create_PreservesAllFields()
    {
        var chart = NewChart();
        var selectors = new[]
        {
            new TaxAccountSelector(AccountCode: "5100"),
            new TaxAccountSelector(AccountCodePrefix: "61"),
        };
        var map = TaxFormLineMap.Create(
            chartId: chart,
            formKind: TaxFormKind.Form1099Nec,
            taxYear: 2027,
            line: "Box1",
            description: "Nonemployee compensation",
            selectors: selectors,
            perPropertyDimension: false,
            isProvisional: true,
            provisionalRationale: "Test rationale",
            citationSource: "Test citation");

        Assert.Equal(chart, map.ChartId);
        Assert.Equal(TaxFormKind.Form1099Nec, map.FormKind);
        Assert.Equal(2027, map.TaxYear);
        Assert.Equal("Box1", map.Line);
        Assert.Equal("Nonemployee compensation", map.Description);
        Assert.Equal(selectors, map.AccountSelectors);
        Assert.False(map.PerPropertyDimension);
        Assert.Equal("Test rationale", map.ProvisionalRationale);
        Assert.Equal("Test citation", map.CitationSource);
    }
}
