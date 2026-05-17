using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Validation;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.People.Foundation.Tests;

public class EmailAddressTests
{
    private static EmailAddress NewEmail(string addr) => EmailAddress.Create(
        tenantId: new TenantId("acme"),
        partyId: PartyId.NewId(),
        address: addr,
        isPrimary: true,
        createdBy: PartyId.NewId());

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("first.last+tag@sub.example.co.uk")]
    [InlineData("a@b.io")]
    public void Validate_RFC5322Address_Passes(string addr)
    {
        var r = EmailAddressValidator.Validate(NewEmail(addr));
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
    }

    [Theory]
    [InlineData("not-an-email")]   // no @ at all
    [InlineData("@no-local.com")]  // missing local-part
    [InlineData("missing@")]       // missing domain
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MalformedAddress_Fails(string addr)
    {
        var r = EmailAddressValidator.Validate(NewEmail(addr));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Validate_AddressWithDisplayName_Fails()
    {
        // RFC 5322 accepts "Display Name <a@b.com>" — we reject those because
        // EmailAddress.Address is the bare addr-spec; display lives in Label.
        var r = EmailAddressValidator.Validate(NewEmail("Jane Doe <jane@example.com>"));
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Contains("bare address", StringComparison.OrdinalIgnoreCase));
    }
}
