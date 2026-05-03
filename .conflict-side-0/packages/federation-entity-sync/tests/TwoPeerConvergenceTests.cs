using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Federation.EntitySync.Tests;

public class TwoPeerConvergenceTests
{
    private sealed class Harness : IDisposable
    {
        public KeyPair AliceKey { get; }
        public Ed25519Signer AliceSigner { get; }
        public InMemoryChangeStore AliceStore { get; }
        public InMemoryEntitySyncer AliceSyncer { get; }
        public PeerDescriptor AliceDescriptor { get; }

        public KeyPair BobKey { get; }
        public Ed25519Signer BobSigner { get; }
        public InMemoryChangeStore BobStore { get; }
        public InMemoryEntitySyncer BobSyncer { get; }
        public PeerDescriptor BobDescriptor { get; }

        public InMemorySyncTransport Transport { get; }

        public Harness()
        {
            var verifier = new Ed25519Verifier();
            Transport = new InMemorySyncTransport();

            AliceKey = KeyPair.Generate();
            AliceSigner = new Ed25519Signer(AliceKey);
            AliceStore = new InMemoryChangeStore();
            AliceSyncer = new InMemoryEntitySyncer(AliceStore, Transport, AliceSigner, verifier);
            AliceDescriptor = new PeerDescriptor(
                PeerId.From(AliceKey.PrincipalId),
                new Uri("inmem://alice"));

            BobKey = KeyPair.Generate();
            BobSigner = new Ed25519Signer(BobKey);
            BobStore = new InMemoryChangeStore();
            BobSyncer = new InMemoryEntitySyncer(BobStore, Transport, BobSigner, verifier);
            BobDescriptor = new PeerDescriptor(
                PeerId.From(BobKey.PrincipalId),
                new Uri("inmem://bob"));
        }

        public void Dispose()
        {
            AliceSyncer.Dispose();
            BobSyncer.Dispose();
            AliceKey.Dispose();
            BobKey.Dispose();
        }
    }

    [Fact]
    public async Task PullFromPeer_TransfersMissingChanges()
    {
        using var h = new Harness();
        var entity = TestData.NewEntity("item-1");

        // Alice has a chain of 3 changes.
        var c1 = TestData.NewSigned(h.AliceSigner, entity, sequence: 1);
        var c2 = TestData.NewSigned(h.AliceSigner, entity, sequence: 2, parent: c1.Payload.VersionId);
        var c3 = TestData.NewSigned(h.AliceSigner, entity, sequence: 3, parent: c2.Payload.VersionId);
        h.AliceStore.Put(c1);
        h.AliceStore.Put(c2);
        h.AliceStore.Put(c3);

        // Bob pulls from Alice.
        var result = await h.BobSyncer.PullFromAsync(h.AliceDescriptor, scope: null, CancellationToken.None);

        Assert.Equal(3, result.ChangesTransferred);
        Assert.Equal(0, result.ChangesRejected);
        Assert.Empty(result.Rejections);
        Assert.True(h.BobStore.Contains(c1.Payload.VersionId));
        Assert.True(h.BobStore.Contains(c2.Payload.VersionId));
        Assert.True(h.BobStore.Contains(c3.Payload.VersionId));
    }

    [Fact]
    public async Task BidirectionalSync_ConvergesBothPeers()
    {
        using var h = new Harness();
        var entity = TestData.NewEntity("item-2");

        // Alice has two independent heads; Bob has two different independent heads.
        var a1 = TestData.NewSigned(h.AliceSigner, entity, sequence: 1);
        var a2 = TestData.NewSigned(h.AliceSigner, entity, sequence: 2);
        h.AliceStore.Put(a1);
        h.AliceStore.Put(a2);

        var b1 = TestData.NewSigned(h.BobSigner, entity, sequence: 3);
        var b2 = TestData.NewSigned(h.BobSigner, entity, sequence: 4);
        h.BobStore.Put(b1);
        h.BobStore.Put(b2);

        // Bob pulls from Alice (Bob learns a1, a2).
        var bobPull = await h.BobSyncer.PullFromAsync(h.AliceDescriptor, scope: null, CancellationToken.None);
        Assert.Equal(2, bobPull.ChangesTransferred);

        // Alice pulls from Bob (Alice learns b1, b2).
        var alicePull = await h.AliceSyncer.PullFromAsync(h.BobDescriptor, scope: null, CancellationToken.None);
        Assert.Equal(2, alicePull.ChangesTransferred);

        foreach (var v in new[] { a1.Payload.VersionId, a2.Payload.VersionId, b1.Payload.VersionId, b2.Payload.VersionId })
        {
            Assert.True(h.AliceStore.Contains(v), $"Alice missing {v}");
            Assert.True(h.BobStore.Contains(v), $"Bob missing {v}");
        }
    }

    [Fact]
    public async Task AlreadyPresent_ChangesNotRetransferred()
    {
        using var h = new Harness();
        var entity = TestData.NewEntity("item-3");

        var c1 = TestData.NewSigned(h.AliceSigner, entity, sequence: 1);
        var c2 = TestData.NewSigned(h.AliceSigner, entity, sequence: 2, parent: c1.Payload.VersionId);
        h.AliceStore.Put(c1);
        h.AliceStore.Put(c2);

        // First push transfers both.
        var first = await h.AliceSyncer.PushToAsync(h.BobDescriptor, scope: null, CancellationToken.None);
        Assert.Equal(2, first.ChangesTransferred);

        // Second push should transfer nothing — peer has everything already.
        var second = await h.AliceSyncer.PushToAsync(h.BobDescriptor, scope: null, CancellationToken.None);
        Assert.Equal(0, second.ChangesTransferred);
        Assert.Equal(0, second.ChangesRejected);
    }
}
