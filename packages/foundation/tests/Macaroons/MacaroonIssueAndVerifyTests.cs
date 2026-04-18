using System.Security.Cryptography;
using Sunfish.Foundation.Macaroons;

namespace Sunfish.Foundation.Tests.Macaroons;

public class MacaroonIssueAndVerifyTests
{
    private const string Location = "https://sunfish.local/";
    private const string Identifier = "kid-001";

    private static byte[] NewRootKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    private static (InMemoryRootKeyStore store, byte[] key, DefaultMacaroonIssuer issuer, DefaultMacaroonVerifier verifier) BuildHarness()
    {
        var store = new InMemoryRootKeyStore();
        var key = NewRootKey();
        store.Set(Location, key);
        return (store, key, new DefaultMacaroonIssuer(store), new DefaultMacaroonVerifier(store));
    }

    private static MacaroonContext OpenContext() => new(
        Now: DateTimeOffset.UtcNow,
        SubjectUri: null,
        ResourceSchema: null,
        RequestedAction: null,
        DeviceIp: null);

    [Fact]
    public async Task Mint_ProducesMacaroonWithSignatureMatchingHmacChain()
    {
        var (_, key, issuer, _) = BuildHarness();
        var caveats = new[] { new Caveat("time <= \"2099-01-01T00:00:00Z\"") };

        var macaroon = await issuer.MintAsync(Location, Identifier, caveats);

        var expected = MacaroonCodec.ComputeChain(key, Identifier, macaroon.Caveats);
        Assert.Equal(expected, macaroon.Signature);
        Assert.Equal(Location, macaroon.Location);
        Assert.Equal(Identifier, macaroon.Identifier);
        Assert.Single(macaroon.Caveats);
    }

    [Fact]
    public async Task Verify_SucceedsForCorrectlyConstructedMacaroon()
    {
        var (_, _, issuer, verifier) = BuildHarness();
        var macaroon = await issuer.MintAsync(
            Location,
            Identifier,
            new[] { new Caveat("time <= \"2099-01-01T00:00:00Z\"") });

        var result = await verifier.VerifyAsync(macaroon, OpenContext());

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task Verify_FailsWhenSignatureTampered()
    {
        var (_, _, issuer, verifier) = BuildHarness();
        var macaroon = await issuer.MintAsync(Location, Identifier, Array.Empty<Caveat>());

        var tampered = new byte[32];
        RandomNumberGenerator.Fill(tampered);
        var bad = macaroon with { Signature = tampered };

        var result = await verifier.VerifyAsync(bad, OpenContext());

        Assert.False(result.IsValid);
        Assert.Equal("Signature mismatch", result.Reason);
    }

    [Fact]
    public async Task Verify_FailsWhenCaveatsTampered()
    {
        var (_, _, issuer, verifier) = BuildHarness();
        var macaroon = await issuer.MintAsync(
            Location,
            Identifier,
            new[] { new Caveat("subject == \"urn:sunfish:alice\"") });

        // Same signature, different caveat list — should detect the tamper.
        var forged = new Macaroon(
            macaroon.Location,
            macaroon.Identifier,
            new[] { new Caveat("subject == \"urn:sunfish:mallory\"") },
            macaroon.Signature);

        var result = await verifier.VerifyAsync(forged, OpenContext() with { SubjectUri = "urn:sunfish:mallory" });

        Assert.False(result.IsValid);
        Assert.Equal("Signature mismatch", result.Reason);
    }
}
