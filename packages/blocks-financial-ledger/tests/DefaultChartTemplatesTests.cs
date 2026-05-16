using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Seeds;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

/// <summary>
/// W#60 P4 PR 5 — coverage for the <see cref="DefaultChartTemplates"/>
/// catalogue. Verifies invariants the seeding service relies on.
/// </summary>
public sealed class DefaultChartTemplatesTests
{
    [Fact]
    public void RentalRealEstate_HasExpectedCount()
    {
        // Sanity check — 37 accounts per the hand-off § PR 5 spec.
        Assert.Equal(37, DefaultChartTemplates.RentalRealEstate.Accounts.Count);
    }

    [Fact]
    public void RentalRealEstate_AllAccountsHavePostableTrueExceptGroups()
    {
        // Group / header / rollup nodes (those that have children OR
        // are clearly top-level group containers like "Assets",
        // "Liabilities") MUST be IsPostable=false to prevent direct
        // postings. Leaves MUST be IsPostable=true.
        var byCode = DefaultChartTemplates.RentalRealEstate.Accounts
            .ToDictionary(a => a.Code);
        var hasChildren = DefaultChartTemplates.RentalRealEstate.Accounts
            .Where(a => a.ParentCode is not null)
            .Select(a => a.ParentCode!)
            .ToHashSet();

        foreach (var a in DefaultChartTemplates.RentalRealEstate.Accounts)
        {
            var shouldBeGroup = hasChildren.Contains(a.Code);
            if (shouldBeGroup)
            {
                Assert.False(a.IsPostable, $"Group node '{a.Code} {a.Name}' must be IsPostable=false.");
            }
            else
            {
                Assert.True(a.IsPostable, $"Leaf node '{a.Code} {a.Name}' must be IsPostable=true.");
            }
        }
    }

    [Fact]
    public void RentalRealEstate_ParentCodesAllResolve()
    {
        var codes = DefaultChartTemplates.RentalRealEstate.Accounts
            .Select(a => a.Code)
            .ToHashSet();
        foreach (var a in DefaultChartTemplates.RentalRealEstate.Accounts)
        {
            if (a.ParentCode is { } parent)
            {
                Assert.Contains(parent, codes);
            }
        }
    }

    [Fact]
    public void RentalRealEstate_ScheduleELineCoverage()
    {
        // Every entry in the Schedule E line map MUST point to an
        // account that exists in the template (otherwise the future
        // blocks-reports-tax TaxFormLineMap seeding will produce
        // dangling refs).
        var codes = DefaultChartTemplates.RentalRealEstate.Accounts
            .Select(a => a.Code)
            .ToHashSet();
        foreach (var (line, code) in DefaultChartTemplates.RentalRealEstateScheduleELineMap)
        {
            Assert.True(codes.Contains(code),
                $"Schedule E line {line} maps to '{code}' which is missing from the template.");
        }
    }

    [Fact]
    public void RentalRealEstate_NoDuplicateCodes()
    {
        var grouped = DefaultChartTemplates.RentalRealEstate.Accounts
            .GroupBy(a => a.Code)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.Empty(grouped);
    }
}
