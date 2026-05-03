using System.Security.Cryptography;
using Sunfish.Foundation.Macaroons;

namespace Sunfish.Foundation.Tests.Macaroons;

public class MacaroonAttenuationTests
{
    private const string Location = "https://sunfish.local/";
    private const string Identifier = "kid-002";

    private static (DefaultMacaroonIssuer issuer, DefaultMacaroonVerifier verifier) BuildHarness()
    {
        var store = new InMemoryRootKeyStore();
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        store.Set(Location, key);
        return (new DefaultMacaroonIssuer(store), new DefaultMacaroonVerifier(store));
    }

    [Fact]
    public async Task Attenuate_AddsCaveat_ExtendsSignatureChain()
    {
        var (issuer, _) = BuildHarness();
        var original = await issuer.MintAsync(Location, Identifier, Array.Empty<Caveat>());

        var attenuated = await issuer.AttenuateAsync(
            original,
            new[] { new Caveat("action in [\"read\"]") });

        Assert.Equal(original.Caveats.Count + 1, attenuated.Caveats.Count);
        Assert.False(attenuated.Signature.AsSpan().SequenceEqual(original.Signature));
    }

    [Fact]
    public async Task Attenuate_PreservesLocationAndIdentifier()
    {
        var (issuer, _) = BuildHarness();
        var original = await issuer.MintAsync(Location, Identifier, Array.Empty<Caveat>());

        var attenuated = await issuer.AttenuateAsync(
            original,
            new[] { new Caveat("subject == \"urn:sunfish:bob\"") });

        Assert.Equal(original.Location, attenuated.Location);
        Assert.Equal(original.Identifier, attenuated.Identifier);
    }

    [Fact]
    public async Task AttenuatedMacaroon_VerifiesWithOriginalRootKey()
    {
        var (issuer, verifier) = BuildHarness();
        var original = await issuer.MintAsync(Location, Identifier, Array.Empty<Caveat>());

        var attenuated = await issuer.AttenuateAsync(
            original,
            new[] { new Caveat("time <= \"2099-01-01T00:00:00Z\"") });

        var result = await verifier.VerifyAsync(
            attenuated,
            new MacaroonContext(DateTimeOffset.UtcNow, null, null, null, null));

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
    }
}
