using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Validation;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.People.Foundation.Tests;

public class PhoneNumberTests
{
    private static PhoneNumber NewPhone(string e164) => PhoneNumber.Create(
        tenantId: new TenantId("acme"),
        partyId: PartyId.NewId(),
        e164: e164,
        isPrimary: true,
        createdBy: PartyId.NewId());

    [Theory]
    [InlineData("+14155551234")]   // US
    [InlineData("+442071234567")]  // UK
    [InlineData("+819012345678")]  // Japan
    [InlineData("+12")]            // bare minimum the regex allows: 1 country digit + 1 subscriber digit
    public void Validate_E164Number_Passes(string e164)
    {
        var r = PhoneNumberValidator.Validate(NewPhone(e164));
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
    }

    [Theory]
    [InlineData("4155551234")]     // no leading +
    [InlineData("+04155551234")]   // leading zero on country code
    [InlineData("+1 415 555 1234")] // spaces
    [InlineData("+1-415-555-1234")] // dashes
    [InlineData("")]
    public void Validate_NonE164_Fails(string e164)
    {
        var r = PhoneNumberValidator.Validate(NewPhone(e164));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Validate_TooLong_Fails()
    {
        // E.164 maxes at 15 digits including country code.
        var r = PhoneNumberValidator.Validate(NewPhone("+1234567890123456")); // 16 digits
        Assert.False(r.IsValid);
    }
}
