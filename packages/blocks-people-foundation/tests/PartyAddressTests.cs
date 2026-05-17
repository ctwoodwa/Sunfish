using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Validation;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.People.Foundation.Tests;

public class PartyAddressTests
{
    private static PartyAddress NewAddr(Address inner, Instant? from = null, Instant? to = null) =>
        PartyAddress.Create(
            tenantId: new TenantId("acme"),
            partyId: PartyId.NewId(),
            address: inner,
            isPrimary: true,
            createdBy: PartyId.NewId(),
            validFrom: from,
            validTo: to);

    private static Address WellFormed() => new(
        Line1: "100 Main St",
        City: "Austin",
        Region: "TX",
        PostalCode: "78701",
        Country: "US");

    [Fact]
    public void Validate_AlphaTwoCountry_Passes()
    {
        var r = PartyAddressValidator.Validate(NewAddr(WellFormed()));
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
    }

    [Theory]
    [InlineData("us")]    // lowercase
    [InlineData("USA")]   // alpha-3
    [InlineData("U")]     // single letter
    [InlineData("")]
    [InlineData("U1")]    // digit
    public void Validate_NonAlpha2Country_Fails(string country)
    {
        var addr = WellFormed() with { Country = country };
        var r = PartyAddressValidator.Validate(NewAddr(addr));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Validate_MissingRequiredFields_Fails()
    {
        var addr = new Address(Line1: "", City: "", Region: "", PostalCode: "", Country: "US");
        var r = PartyAddressValidator.Validate(NewAddr(addr));
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("Line1"));
        Assert.Contains(r.Errors, e => e.Contains("City"));
        Assert.Contains(r.Errors, e => e.Contains("Region"));
        Assert.Contains(r.Errors, e => e.Contains("PostalCode"));
    }

    [Fact]
    public void Validate_ValidToBeforeValidFrom_Fails()
    {
        var from = new Instant(DateTimeOffset.Parse("2026-06-01T00:00:00Z"));
        var to = new Instant(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var r = PartyAddressValidator.Validate(NewAddr(WellFormed(), from, to));
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("ValidTo"));
    }

    [Fact]
    public void Validate_OmittedValidFromAndValidTo_Passes()
    {
        var r = PartyAddressValidator.Validate(NewAddr(WellFormed())); // both null
        Assert.True(r.IsValid);
    }
}
