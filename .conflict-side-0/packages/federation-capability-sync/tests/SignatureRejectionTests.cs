using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Federation.CapabilitySync.Tests;

public class SignatureRejectionTests
{
    [Fact]
    public async Task ReconcileAsync_RejectsTamperedOp()
    {
        using var h = Harness.New();
        using var groupKey = KeyPair.Generate();

        // Sign a legit MintPrincipal(Group) op, then tamper the Kind to Individual while preserving
        // the original signature. PrincipalKind serialises to a JSON enum value (number or name)
        // so canonical-JSON bytes change — the signature must no longer verify.
        var originalOp = Harness.NewMintGroup(h.AliceSigner, groupKey.PrincipalId);
        CapabilityOp tamperedPayload = new MintPrincipal(groupKey.PrincipalId, PrincipalKind.Individual, null);
        var tamperedOp = originalOp with { Payload = tamperedPayload };

        // Sanity: the tampered op must fail direct verification.
        var verifier = new Ed25519Verifier();
        Assert.True(verifier.Verify(originalOp), "original op should verify");
        Assert.False(verifier.Verify(tamperedOp), "tampered op must NOT verify");

        h.AliceStore.Put(tamperedOp);

        var result = await h.BobSyncer.ReconcileAsync(h.AlicePeer);

        Assert.Equal(1, result.OpsRejected);
        Assert.Equal(0, result.OpsTransferred);
        Assert.Contains(tamperedOp.Nonce, result.RejectedNonces);
        Assert.False(h.BobStore.Contains(tamperedOp.Nonce));
    }
}
