using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;
using Xunit;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// PR 1 coverage for <see cref="InMemoryTaxJurisdictionResolver"/>.
/// Validates the address-matching algorithm + the most-local-first
/// ordering contract. Property-id look-aside path lands in a later
/// hand-off and is out of scope here.
/// </summary>
public class InMemoryTaxJurisdictionResolverTests
{
    [Fact]
    public async Task Resolve_USVAFrederick_ReturnsCityCountyStateFederal()
    {
        var store = new InMemoryTaxJurisdictionStore();
        var federal = TaxJurisdiction.Create(JurisdictionLevel.Federal, "US", "Federal");
        var state = TaxJurisdiction.Create(
            JurisdictionLevel.State, "US", "Virginia",
            parentJurisdictionId: federal.Id, region: "US-VA");
        var county = TaxJurisdiction.Create(
            JurisdictionLevel.County, "US", "Frederick County",
            parentJurisdictionId: state.Id, region: "US-VA", locality: "Frederick County");
        var city = TaxJurisdiction.Create(
            JurisdictionLevel.City, "US", "Winchester",
            parentJurisdictionId: county.Id, region: "US-VA", locality: "Frederick County");
        await store.UpsertAsync(federal);
        await store.UpsertAsync(state);
        await store.UpsertAsync(county);
        await store.UpsertAsync(city);

        var resolver = new InMemoryTaxJurisdictionResolver(store);
        var result = await resolver.ResolveAsync(new TaxLocationContext(
            IsoCountry: "US",
            Region: "US-VA",
            Locality: "Frederick County"));

        // Most-local-first per the contract.
        Assert.Collection(
            result,
            j => Assert.Equal(JurisdictionLevel.City, j.Level),
            j => Assert.Equal(JurisdictionLevel.County, j.Level),
            j => Assert.Equal(JurisdictionLevel.State, j.Level),
            j => Assert.Equal(JurisdictionLevel.Federal, j.Level));
    }

    [Fact]
    public async Task Resolve_USVAWithoutCity_OmitsCity()
    {
        var store = new InMemoryTaxJurisdictionStore();
        var federal = TaxJurisdiction.Create(JurisdictionLevel.Federal, "US", "Federal");
        var state = TaxJurisdiction.Create(
            JurisdictionLevel.State, "US", "Virginia",
            parentJurisdictionId: federal.Id, region: "US-VA");
        var county = TaxJurisdiction.Create(
            JurisdictionLevel.County, "US", "Frederick County",
            parentJurisdictionId: state.Id, region: "US-VA", locality: "Frederick County");
        var winchester = TaxJurisdiction.Create(
            JurisdictionLevel.City, "US", "Winchester",
            parentJurisdictionId: county.Id, region: "US-VA", locality: "Frederick County");
        await store.UpsertAsync(federal);
        await store.UpsertAsync(state);
        await store.UpsertAsync(county);
        await store.UpsertAsync(winchester);

        var resolver = new InMemoryTaxJurisdictionResolver(store);
        // Same county, but context has no locality — Winchester should be excluded.
        var result = await resolver.ResolveAsync(new TaxLocationContext(
            IsoCountry: "US",
            Region: "US-VA",
            Locality: null));

        Assert.DoesNotContain(result, j => j.Level == JurisdictionLevel.City);
        Assert.Contains(result, j => j.Level == JurisdictionLevel.State);
        Assert.Contains(result, j => j.Level == JurisdictionLevel.Federal);
    }

    [Fact]
    public async Task Resolve_NonUSCountry_ReturnsCountryOnly_WhenNoSubdivisionsSeeded()
    {
        var store = new InMemoryTaxJurisdictionStore();
        await store.UpsertAsync(TaxJurisdiction.Create(JurisdictionLevel.Country, "DE", "Germany"));
        await store.UpsertAsync(TaxJurisdiction.Create(JurisdictionLevel.Federal, "US", "Federal"));

        var resolver = new InMemoryTaxJurisdictionResolver(store);
        var result = await resolver.ResolveAsync(new TaxLocationContext(
            IsoCountry: "DE",
            Region: "DE-BY"));

        // Only DE Country is in scope. US Federal does not match DE.
        Assert.Single(result);
        Assert.Equal(JurisdictionLevel.Country, result[0].Level);
        Assert.Equal("DE", result[0].IsoCountry);
    }

    [Fact]
    public async Task Resolve_SoftDeletedRow_Excluded()
    {
        var store = new InMemoryTaxJurisdictionStore();
        var federal = TaxJurisdiction.Create(JurisdictionLevel.Federal, "US", "Federal");
        await store.UpsertAsync(federal);
        // Soft-delete it.
        await store.UpsertAsync(federal with { DeletedAtUtc = federal.UpdatedAtUtc });

        var resolver = new InMemoryTaxJurisdictionResolver(store);
        var result = await resolver.ResolveAsync(new TaxLocationContext("US"));

        Assert.Empty(result);
    }
}
