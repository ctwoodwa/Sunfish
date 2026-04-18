using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Sunfish.Foundation.Macaroons;

namespace Sunfish.Foundation.Tests.Macaroons;

public class MacaroonWireFormatTests
{
    private const string Location = "https://sunfish.local/";
    private const string Identifier = "kid-wire";

    private static DefaultMacaroonIssuer BuildIssuer()
    {
        var store = new InMemoryRootKeyStore();
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        store.Set(Location, key);
        return new DefaultMacaroonIssuer(store);
    }

    [Fact]
    public async Task Encode_Decode_RoundTripsCorrectly()
    {
        var issuer = BuildIssuer();
        var original = await issuer.MintAsync(
            Location,
            Identifier,
            new[]
            {
                new Caveat("time <= \"2099-01-01T00:00:00Z\""),
                new Caveat("subject == \"urn:sunfish:alice\"")
            });

        var encoded = MacaroonCodec.EncodeBase64Url(original);
        var decoded = MacaroonCodec.DecodeBase64Url(encoded);

        Assert.Equal(original.Location, decoded.Location);
        Assert.Equal(original.Identifier, decoded.Identifier);
        Assert.Equal(original.Caveats, decoded.Caveats);
        Assert.Equal(original.Signature, decoded.Signature);
    }

    [Fact]
    public void Decode_RejectsMalformedInput()
    {
        // "!!!" is not valid base64url; should surface as FormatException.
        Assert.Throws<FormatException>(() => MacaroonCodec.DecodeBase64Url("!!!***not-base64url***!!!"));
    }

    [Fact]
    public void Decode_RejectsMissingSignature()
    {
        // Build wire bytes with NO 0x1F sentinel: just location + separator + identifier.
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.UTF8.GetBytes("loc"));
        bytes.Add(MacaroonCodec.RecordSeparator);
        bytes.AddRange(Encoding.UTF8.GetBytes("id"));
        var encoded = Base64Url.EncodeToString(bytes.ToArray());

        Assert.Throws<FormatException>(() => MacaroonCodec.DecodeBase64Url(encoded));
    }
}
