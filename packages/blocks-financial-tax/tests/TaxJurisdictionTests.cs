using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// PR 1 coverage for the <see cref="TaxJurisdiction"/> record per Stage
/// 02 §3.14. Contract-level only — full resolver behavior is exercised
/// by <see cref="InMemoryTaxJurisdictionResolverTests"/>.
/// </summary>
public class TaxJurisdictionTests
{
    [Fact]
    public void Create_PopulatesAllFields()
    {
        var j = TaxJurisdiction.Create(
            level: JurisdictionLevel.County,
            isoCountry: "US",
            name: "Frederick County",
            parentJurisdictionId: new TaxJurisdictionId("parent-1"),
            region: "US-VA",
            locality: "Frederick County",
            notes: "Test fixture");

        Assert.Equal(JurisdictionLevel.County, j.Level);
        Assert.Equal("US", j.IsoCountry);
        Assert.Equal("US-VA", j.Region);
        Assert.Equal("Frederick County", j.Locality);
        Assert.Equal("Frederick County", j.Name);
        Assert.Equal(new TaxJurisdictionId("parent-1"), j.ParentJurisdictionId);
        Assert.Equal("Test fixture", j.Notes);
    }

    [Fact]
    public void Create_DefaultsIsActiveTrue()
    {
        var j = TaxJurisdiction.Create(JurisdictionLevel.State, "US", "Virginia");

        Assert.True(j.IsActive);
    }

    [Fact]
    public void Create_SetsCreatedAndUpdatedToSameInstant()
    {
        var when = new Instant(new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero));
        var j = TaxJurisdiction.Create(
            level: JurisdictionLevel.Country,
            isoCountry: "US",
            name: "United States",
            createdAtUtc: when);

        Assert.Equal(when, j.CreatedAtUtc);
        Assert.Equal(when, j.UpdatedAtUtc);
        Assert.Null(j.DeletedAtUtc);
    }

    [Fact]
    public void Create_Federal_WithoutParent_Succeeds()
    {
        var j = TaxJurisdiction.Create(JurisdictionLevel.Federal, "US", "Federal");

        Assert.Null(j.ParentJurisdictionId);
        Assert.Equal(JurisdictionLevel.Federal, j.Level);
    }

    [Fact]
    public void Create_City_WithParent_RecordsParentId()
    {
        var county = TaxJurisdiction.Create(JurisdictionLevel.County, "US", "Frederick County");
        var city = TaxJurisdiction.Create(
            level: JurisdictionLevel.City,
            isoCountry: "US",
            name: "Winchester",
            parentJurisdictionId: county.Id);

        Assert.Equal(county.Id, city.ParentJurisdictionId);
    }
}
