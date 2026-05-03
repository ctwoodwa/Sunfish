using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Federation.CapabilitySync.Tests;

public class RevocationConvergenceTests
{
    [Fact]
    public async Task AddMember_ThenRemoveMember_BothSync_PeerSeeBothOps()
    {
        using var h = Harness.New();
        using var groupKey = KeyPair.Generate();
        using var memberKey = KeyPair.Generate();

        var addOp = Harness.NewAddMember(h.AliceSigner, groupKey.PrincipalId, memberKey.PrincipalId);
        var removeOp = Harness.NewRemoveMember(h.BobSigner, groupKey.PrincipalId, memberKey.PrincipalId);

        h.AliceStore.Put(addOp);
        h.BobStore.Put(removeOp);

        await h.BobSyncer.ReconcileAsync(h.AlicePeer);
        await h.AliceSyncer.ReconcileAsync(h.BobPeer);

        Assert.Equal(2, h.AliceStore.All().Count);
        Assert.Equal(2, h.BobStore.All().Count);
        Assert.True(h.AliceStore.Contains(addOp.Nonce));
        Assert.True(h.AliceStore.Contains(removeOp.Nonce));
        Assert.True(h.BobStore.Contains(addOp.Nonce));
        Assert.True(h.BobStore.Contains(removeOp.Nonce));
    }

    [Fact]
    public async Task MultipleRevocationsConverge()
    {
        using var h = Harness.New();
        using var groupKey = KeyPair.Generate();
        using var memberKey = KeyPair.Generate();

        var mintOp = Harness.NewMintGroup(h.AliceSigner, groupKey.PrincipalId);
        var addOp = Harness.NewAddMember(h.AliceSigner, groupKey.PrincipalId, memberKey.PrincipalId);
        var removeOp = Harness.NewRemoveMember(h.BobSigner, groupKey.PrincipalId, memberKey.PrincipalId);

        h.AliceStore.Put(mintOp);
        h.AliceStore.Put(addOp);
        h.BobStore.Put(removeOp);

        await h.BobSyncer.ReconcileAsync(h.AlicePeer);
        await h.AliceSyncer.ReconcileAsync(h.BobPeer);

        Assert.Equal(3, h.AliceStore.All().Count);
        Assert.Equal(3, h.BobStore.All().Count);

        foreach (var nonce in new[] { mintOp.Nonce, addOp.Nonce, removeOp.Nonce })
        {
            Assert.True(h.AliceStore.Contains(nonce));
            Assert.True(h.BobStore.Contains(nonce));
        }
    }
}
