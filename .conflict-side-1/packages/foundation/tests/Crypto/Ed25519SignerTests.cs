using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Crypto;

public class Ed25519SignerTests
{
    private sealed record TestPayload(string Name);

    [Fact]
    public async Task IssuerId_MatchesKeyPairPrincipal()
    {
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);

        Assert.Equal(kp.PrincipalId, signer.IssuerId);

        var op = await signer.SignAsync(new TestPayload("x"), DateTimeOffset.UtcNow, Guid.NewGuid());
        Assert.Equal(kp.PrincipalId, op.IssuerId);
    }

    [Fact]
    public async Task Sign_ProducesSignatureOfExpectedLength()
    {
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);

        var op = await signer.SignAsync(new TestPayload("x"), DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal(Signature.LengthInBytes, op.Signature.AsSpan().Length);
    }

    [Fact]
    public async Task Sign_IsDeterministic_ForIdenticalInputs()
    {
        // Ed25519 is deterministic (RFC 8032): for a fixed key + fixed message,
        // the signature is exactly the same every time. This property is load-bearing
        // for reproducible tests and offline-replay equivalence checks.
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);
        var payload = new TestPayload("same");
        var issuedAt = new DateTimeOffset(2026, 4, 17, 0, 0, 0, TimeSpan.Zero);
        var nonce = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var a = await signer.SignAsync(payload, issuedAt, nonce);
        var b = await signer.SignAsync(payload, issuedAt, nonce);

        Assert.Equal(a.Signature, b.Signature);
    }
}
