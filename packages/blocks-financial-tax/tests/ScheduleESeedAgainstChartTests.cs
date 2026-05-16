using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Seeds;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Seeds;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// Cross-package integration: the Schedule E provisional seed
/// references chart-of-accounts codes that <b>must exist</b> in
/// <c>blocks-financial-ledger</c>'s
/// <c>DefaultChartTemplates.RentalRealEstate</c> template, or the
/// downstream Schedule E generator will silently drop the line.
/// Catches drift between the two seeds at unit-test time.
/// </summary>
public class ScheduleESeedAgainstChartTests
{
    [Fact]
    public void ScheduleE_AllExactCodeSelectors_ResolveAgainstRentalRealEstateChart()
    {
        var chart = FL.ChartOfAccountsId.NewId();
        var rows = DefaultTaxFormLineMap.ScheduleE(chart);

        // Build the set of codes the chart actually offers.
        var chartCodes = DefaultChartTemplates.RentalRealEstate.Accounts
            .Select(a => a.Code)
            .ToHashSet(StringComparer.Ordinal);

        // For every selector with an exact AccountCode, the code must
        // exist in the chart. Prefix and tag selectors are NOT checked
        // here — prefix is permissive by design + tags are consumer-
        // defined metadata layered on top of the chart.
        var missing = new List<(string Line, string Code)>();
        foreach (var row in rows)
        {
            foreach (var sel in row.AccountSelectors)
            {
                if (sel.AccountCode is null) continue;
                if (!chartCodes.Contains(sel.AccountCode))
                {
                    missing.Add((row.Line, sel.AccountCode));
                }
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void ScheduleE_PrefixSelectors_MatchAtLeastOneChartCode()
    {
        // A prefix selector that matches nothing in the chart is a
        // suspicious authoring error (the prefix should be narrowed
        // or the chart needs the corresponding accounts). Catches
        // the "I typoed the prefix" drift.
        var chart = FL.ChartOfAccountsId.NewId();
        var rows = DefaultTaxFormLineMap.ScheduleE(chart);

        var chartCodes = DefaultChartTemplates.RentalRealEstate.Accounts
            .Select(a => a.Code)
            .ToList();

        foreach (var row in rows)
        {
            foreach (var sel in row.AccountSelectors)
            {
                if (sel.AccountCodePrefix is null) continue;
                Assert.Contains(
                    chartCodes,
                    code => code.StartsWith(sel.AccountCodePrefix, StringComparison.Ordinal));
            }
        }
    }
}
