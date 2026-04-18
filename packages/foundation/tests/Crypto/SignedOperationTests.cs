using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Crypto;

public class SignedOperationTests
{
    private sealed record TestPayload(string Name, int Count);

    [Fact]
    public async Task SignedOperation_RoundTripsAllFields()
    {
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);
        var payload = new TestPayload("hello", 42);
        var issuedAt = new DateTimeOffset(2026, 4, 17, 12, 0, 0, TimeSpan.Zero);
        var nonce = Guid.NewGuid();

        var op = await signer.SignAsync(payload, issuedAt, nonce);

        Assert.Equal(payload, op.Payload);
        Assert.Equal(kp.PrincipalId, op.IssuerId);
        Assert.Equal(issuedAt, op.IssuedAt);
        Assert.Equal(nonce, op.Nonce);
    }

    [Fact]
    public async Task SignedOperation_WithExpression_ProducesMutatedCopy()
    {
        // Documents the semantics used by the tamper tests: `with` produces a
        // new SignedOperation<T> value that shares the original signature but
        // carries different non-signature fields. The verifier must reject it.
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);
        var op = await signer.SignAsync(new TestPayload("a", 1), DateTimeOffset.UtcNow, Guid.NewGuid());

        var mutated = op with { Payload = new TestPayload("b", 2) };

        Assert.NotEqual(op.Payload, mutated.Payload);
        Assert.Equal(op.IssuerId, mutated.IssuerId);
        Assert.Equal(op.IssuedAt, mutated.IssuedAt);
        Assert.Equal(op.Nonce, mutated.Nonce);
        Assert.Equal(op.Signature, mutated.Signature);
    }
}
