using Sunfish.Blocks.FinancialTax.Models;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// PR 1 coverage for <see cref="JurisdictionLevelExtensions.OrderIndex"/>.
/// The compound-tax engine in PR 3 walks jurisdictions outermost-first
/// using this ordering, so the relationships below are load-bearing.
/// </summary>
public class JurisdictionLevelExtensionsTests
{
    [Fact]
    public void OrderIndex_Federal_BeforeState()
    {
        Assert.True(JurisdictionLevel.Federal.OrderIndex() < JurisdictionLevel.State.OrderIndex());
    }

    [Fact]
    public void OrderIndex_State_BeforeCounty()
    {
        Assert.True(JurisdictionLevel.State.OrderIndex() < JurisdictionLevel.County.OrderIndex());
    }

    [Fact]
    public void OrderIndex_County_BeforeCity()
    {
        Assert.True(JurisdictionLevel.County.OrderIndex() < JurisdictionLevel.City.OrderIndex());
    }

    [Fact]
    public void OrderIndex_City_BeforeDistrict()
    {
        Assert.True(JurisdictionLevel.City.OrderIndex() < JurisdictionLevel.District.OrderIndex());
    }

    [Fact]
    public void OrderIndex_UnknownEnumValue_Returns99()
    {
        var unknown = (JurisdictionLevel)9999;
        Assert.Equal(99, unknown.OrderIndex());
    }
}
