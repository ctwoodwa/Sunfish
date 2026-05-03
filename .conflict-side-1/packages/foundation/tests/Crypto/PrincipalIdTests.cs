using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Crypto;

public class PrincipalIdTests
{
    [Fact]
    public void FromBytes_WithWrongLength_Throws()
    {
        var tooShort = new byte[16];
        Assert.Throws<ArgumentException>(() => PrincipalId.FromBytes(tooShort));
    }

    [Fact]
    public void Equals_ComparesByContent_NotReference()
    {
        var bytes = new byte[PrincipalId.LengthInBytes];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = (byte)i;

        var a = PrincipalId.FromBytes(bytes);
        var b = PrincipalId.FromBytes(bytes);

        // Two independent PrincipalId values backed by distinct arrays must be equal
        // because equality is by content (override intentionally replaces the default
        // record-struct reference equality over byte[]).
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Base64Url_RoundTrip_ProducesEqualPrincipal()
    {
        using var kp = KeyPair.Generate();
        var original = kp.PrincipalId;

        var encoded = original.ToBase64Url();
        var decoded = PrincipalId.FromBase64Url(encoded);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void FromBase64Url_WithInvalidValue_Throws()
    {
        Assert.Throws<FormatException>(() => PrincipalId.FromBase64Url("not-a-valid-key"));
    }
}
