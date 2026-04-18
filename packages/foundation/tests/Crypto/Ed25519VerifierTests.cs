using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Crypto;

public class Ed25519VerifierTests
{
    private sealed record TestPayload(string Name, int Count);

    [Fact]
    public async Task Verify_ValidSignature_ReturnsTrue()
    {
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);
        var verifier = new Ed25519Verifier();

        var op = await signer.SignAsync(new TestPayload("hello", 1), DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.True(verifier.Verify(op));
    }

    [Fact]
    public async Task Verify_TamperedPayload_ReturnsFalse()
    {
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);
        var verifier = new Ed25519Verifier();
        var op = await signer.SignAsync(new TestPayload("hello", 1), DateTimeOffset.UtcNow, Guid.NewGuid());

        var tampered = op with { Payload = new TestPayload("hello", 2) };

        Assert.False(verifier.Verify(tampered));
    }

    [Fact]
    public async Task Verify_TamperedIssuedAt_ReturnsFalse()
    {
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);
        var verifier = new Ed25519Verifier();
        var op = await signer.SignAsync(new TestPayload("hello", 1), DateTimeOffset.UtcNow, Guid.NewGuid());

        var tampered = op with { IssuedAt = op.IssuedAt.AddSeconds(1) };

        Assert.False(verifier.Verify(tampered));
    }

    [Fact]
    public async Task Verify_TamperedNonce_ReturnsFalse()
    {
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);
        var verifier = new Ed25519Verifier();
        var op = await signer.SignAsync(new TestPayload("hello", 1), DateTimeOffset.UtcNow, Guid.NewGuid());

        var tampered = op with { Nonce = Guid.NewGuid() };

        Assert.False(verifier.Verify(tampered));
    }

    [Fact]
    public async Task Verify_TamperedIssuer_ReturnsFalse()
    {
        using var kp1 = KeyPair.Generate();
        using var kp2 = KeyPair.Generate();
        var signer = new Ed25519Signer(kp1);
        var verifier = new Ed25519Verifier();
        var op = await signer.SignAsync(new TestPayload("hello", 1), DateTimeOffset.UtcNow, Guid.NewGuid());

        var tampered = op with { IssuerId = kp2.PrincipalId };

        Assert.False(verifier.Verify(tampered));
    }

    [Fact]
    public async Task Verify_Succeeds_AcrossFreshVerifierInstances()
    {
        // A fresh verifier constructed after signing must still verify — the verifier holds
        // no per-signing state; the public key travels on the envelope.
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);
        var op = await signer.SignAsync(new TestPayload("roundtrip", 7), DateTimeOffset.UtcNow, Guid.NewGuid());

        var freshVerifier = new Ed25519Verifier();

        Assert.True(freshVerifier.Verify(op));
    }
}
