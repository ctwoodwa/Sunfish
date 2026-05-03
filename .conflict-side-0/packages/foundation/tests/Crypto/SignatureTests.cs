using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Crypto;

public class SignatureTests
{
    [Fact]
    public void FromBytes_WithWrongLength_Throws()
    {
        var tooShort = new byte[32];
        Assert.Throws<ArgumentException>(() => Signature.FromBytes(tooShort));
    }

    [Fact]
    public void Base64Url_RoundTrip_ProducesEqualSignature()
    {
        var bytes = new byte[Signature.LengthInBytes];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i * 3);

        var sig = Signature.FromBytes(bytes);
        var roundTripped = Signature.FromBase64Url(sig.ToBase64Url());

        Assert.Equal(sig, roundTripped);
    }
}
