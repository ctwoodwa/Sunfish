using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Capabilities;

public class CapabilityOpSignatureTests
{
    [Fact]
    public async Task Delegate_CanBeSignedAndVerified()
    {
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);
        var verifier = new Ed25519Verifier();

        using var subjectKp = KeyPair.Generate();
        var payload = new Sunfish.Foundation.Capabilities.Delegate(
            Subject: subjectKp.PrincipalId,
            Resource: new Resource("urn:doc:1"),
            Action: CapabilityAction.Read);

        var op = await signer.SignAsync<CapabilityOp>(payload, DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.True(verifier.Verify(op));
    }

    [Fact]
    public async Task AddMember_CanonicalJsonIsDeterministic()
    {
        // Ed25519 (NSec) signatures are deterministic — identical inputs produce identical
        // signature bytes. This guards the canonical-JSON + signing pipeline end to end.
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);

        using var groupKp = KeyPair.Generate();
        using var memberKp = KeyPair.Generate();
        CapabilityOp payload = new AddMember(Group: groupKp.PrincipalId, Member: memberKp.PrincipalId);

        var issuedAt = new DateTimeOffset(2026, 4, 17, 12, 0, 0, TimeSpan.Zero);
        var nonce = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var first = await signer.SignAsync(payload, issuedAt, nonce);
        var second = await signer.SignAsync(payload, issuedAt, nonce);

        Assert.Equal(first.Signature, second.Signature);
    }

    [Fact]
    public async Task MintPrincipal_WithoutInitialMembers_SerializesWithNullMembers()
    {
        // A MintPrincipal with a null InitialMembers must canonicalize stably and verify.
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);
        var verifier = new Ed25519Verifier();

        using var newKp = KeyPair.Generate();
        CapabilityOp payload = new MintPrincipal(NewId: newKp.PrincipalId, Kind: PrincipalKind.Individual);

        var op = await signer.SignAsync(payload, DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.True(verifier.Verify(op));
        var mint = Assert.IsType<MintPrincipal>(op.Payload);
        Assert.Null(mint.InitialMembers);
    }
}
