using Sunfish.Federation.CapabilitySync.Riblt;
using Sunfish.Federation.CapabilitySync.Sync;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Federation.CapabilitySync.Tests;

public class RibltFallbackTests
{
    [Fact]
    public void Decode_WithLargeDifference_Returns_NeedMoreSymbols_AfterSmallBatch()
    {
        var remoteItems = Enumerable.Range(0, 50)
            .Select(_ => RibltItem.FromIdentity(unchecked((ulong)Random.Shared.NextInt64())))
            .ToArray();

        var encoder = new RibltEncoder(remoteItems);
        var tinyBatch = encoder.Batch(0, 8);

        var result = RibltDecoder.TryDecode(tinyBatch, Array.Empty<RibltItem>());

        Assert.NotEqual(RibltDecodeOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task FullSetFallback_ConvergesWhenRibltGivesUp()
    {
        // 50 disjoint ops on each side guarantees the 3-batch RIBLT fast path cannot converge
        // within 48 symbols (3 * 16). Full-set fallback must still deliver every missing op.
        using var aliceKey = KeyPair.Generate();
        using var bobKey = KeyPair.Generate();
        var aliceSigner = new Ed25519Signer(aliceKey);
        var bobSigner = new Ed25519Signer(bobKey);
        var verifier = new Ed25519Verifier();

        var aliceStore = new InMemoryCapabilityOpStore();
        var bobStore = new InMemoryCapabilityOpStore();

        for (int i = 0; i < 50; i++)
            aliceStore.Put(NewMintOp(aliceSigner));
        for (int i = 0; i < 50; i++)
            bobStore.Put(NewMintOp(bobSigner));

        var alicePeer = new PeerDescriptor(PeerId.From(aliceKey.PrincipalId), new Uri("inmem://alice"));
        var bobPeer = new PeerDescriptor(PeerId.From(bobKey.PrincipalId), new Uri("inmem://bob"));

        Func<PeerId, ICapabilityOpStore?> bobResolver = id => id == alicePeer.Id ? aliceStore : null;
        var bobSyncer = new InMemoryCapabilitySyncer(bobStore, bobResolver, verifier);

        var result = await bobSyncer.ReconcileAsync(alicePeer);

        Assert.True(result.UsedFullSetFallback);
        Assert.Equal(50, result.OpsTransferred);
        Assert.Equal(0, result.OpsRejected);
        Assert.Equal(100, bobStore.All().Count);
    }

    private static SignedOperation<CapabilityOp> NewMintOp(Ed25519Signer signer)
    {
        using var newPrincipalKey = KeyPair.Generate();
        CapabilityOp payload = new MintPrincipal(newPrincipalKey.PrincipalId, PrincipalKind.Individual, null);
        return signer.SignAsync(payload, DateTimeOffset.UtcNow, Guid.NewGuid()).AsTask().GetAwaiter().GetResult();
    }
}
