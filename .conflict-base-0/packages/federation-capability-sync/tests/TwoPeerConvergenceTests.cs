using Sunfish.Federation.CapabilitySync.Sync;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Federation.CapabilitySync.Tests;

public class TwoPeerConvergenceTests
{
    [Fact]
    public async Task ReconcileAsync_PullsMissingOpsFromPeer()
    {
        using var h = Harness.New();
        for (int i = 0; i < 3; i++)
            h.AliceStore.Put(Harness.NewMint(h.AliceSigner));

        var result = await h.BobSyncer.ReconcileAsync(h.AlicePeer);

        Assert.Equal(3, result.OpsTransferred);
        Assert.Equal(0, result.OpsRejected);
        Assert.Equal(3, h.BobStore.All().Count);
    }

    [Fact]
    public async Task BidirectionalReconcile_Converges()
    {
        using var h = Harness.New();
        h.AliceStore.Put(Harness.NewMint(h.AliceSigner));
        h.AliceStore.Put(Harness.NewMint(h.AliceSigner));
        h.BobStore.Put(Harness.NewMint(h.BobSigner));
        h.BobStore.Put(Harness.NewMint(h.BobSigner));

        await h.BobSyncer.ReconcileAsync(h.AlicePeer);
        await h.AliceSyncer.ReconcileAsync(h.BobPeer);

        Assert.Equal(4, h.AliceStore.All().Count);
        Assert.Equal(4, h.BobStore.All().Count);

        var aliceNonces = new HashSet<Guid>(h.AliceStore.AllNonces());
        var bobNonces = new HashSet<Guid>(h.BobStore.AllNonces());
        Assert.True(aliceNonces.SetEquals(bobNonces));
    }

    [Fact]
    public async Task AlreadyPresent_OpsNotRetransferred()
    {
        using var h = Harness.New();
        for (int i = 0; i < 3; i++)
            h.AliceStore.Put(Harness.NewMint(h.AliceSigner));

        var first = await h.BobSyncer.ReconcileAsync(h.AlicePeer);
        var second = await h.BobSyncer.ReconcileAsync(h.AlicePeer);

        Assert.Equal(3, first.OpsTransferred);
        Assert.Equal(0, second.OpsTransferred);
        // The second pass converges via RIBLT-fast-path (both sides match) so wanted is empty;
        // "already-present" counters only increment when RIBLT reports RemoteOnly nonces the
        // local store happens to hold. Either way, no double-transfer.
        Assert.Equal(3, h.BobStore.All().Count);
    }
}

internal sealed class Harness : IDisposable
{
    public InMemoryCapabilityOpStore AliceStore { get; }
    public InMemoryCapabilityOpStore BobStore { get; }
    public InMemoryCapabilitySyncer AliceSyncer { get; }
    public InMemoryCapabilitySyncer BobSyncer { get; }
    public PeerDescriptor AlicePeer { get; }
    public PeerDescriptor BobPeer { get; }
    public Ed25519Signer AliceSigner { get; }
    public Ed25519Signer BobSigner { get; }
    private readonly KeyPair _aliceKey;
    private readonly KeyPair _bobKey;

    private Harness(
        KeyPair aliceKey, KeyPair bobKey,
        Ed25519Signer aliceSigner, Ed25519Signer bobSigner,
        InMemoryCapabilityOpStore aliceStore, InMemoryCapabilityOpStore bobStore,
        InMemoryCapabilitySyncer aliceSyncer, InMemoryCapabilitySyncer bobSyncer,
        PeerDescriptor alicePeer, PeerDescriptor bobPeer)
    {
        _aliceKey = aliceKey;
        _bobKey = bobKey;
        AliceSigner = aliceSigner;
        BobSigner = bobSigner;
        AliceStore = aliceStore;
        BobStore = bobStore;
        AliceSyncer = aliceSyncer;
        BobSyncer = bobSyncer;
        AlicePeer = alicePeer;
        BobPeer = bobPeer;
    }

    public static Harness New()
    {
        var verifier = new Ed25519Verifier();
        var aliceKey = KeyPair.Generate();
        var bobKey = KeyPair.Generate();
        var aliceSigner = new Ed25519Signer(aliceKey);
        var bobSigner = new Ed25519Signer(bobKey);

        var aliceStore = new InMemoryCapabilityOpStore();
        var bobStore = new InMemoryCapabilityOpStore();

        var alicePeer = new PeerDescriptor(PeerId.From(aliceKey.PrincipalId), new Uri("inmem://alice"));
        var bobPeer = new PeerDescriptor(PeerId.From(bobKey.PrincipalId), new Uri("inmem://bob"));

        Func<PeerId, ICapabilityOpStore?> aliceResolver = id => id == bobPeer.Id ? bobStore : null;
        Func<PeerId, ICapabilityOpStore?> bobResolver = id => id == alicePeer.Id ? aliceStore : null;

        var aliceSyncer = new InMemoryCapabilitySyncer(aliceStore, aliceResolver, verifier);
        var bobSyncer = new InMemoryCapabilitySyncer(bobStore, bobResolver, verifier);

        return new Harness(
            aliceKey, bobKey, aliceSigner, bobSigner,
            aliceStore, bobStore, aliceSyncer, bobSyncer,
            alicePeer, bobPeer);
    }

    public static SignedOperation<CapabilityOp> NewMint(Ed25519Signer signer)
    {
        using var newPrincipalKey = KeyPair.Generate();
        CapabilityOp payload = new MintPrincipal(newPrincipalKey.PrincipalId, PrincipalKind.Individual, null);
        return signer.SignAsync(payload, DateTimeOffset.UtcNow, Guid.NewGuid()).AsTask().GetAwaiter().GetResult();
    }

    public static SignedOperation<CapabilityOp> NewMintGroup(Ed25519Signer signer, PrincipalId groupId)
    {
        CapabilityOp payload = new MintPrincipal(groupId, PrincipalKind.Group, Array.Empty<PrincipalId>());
        return signer.SignAsync(payload, DateTimeOffset.UtcNow, Guid.NewGuid()).AsTask().GetAwaiter().GetResult();
    }

    public static SignedOperation<CapabilityOp> NewAddMember(Ed25519Signer signer, PrincipalId group, PrincipalId member)
    {
        CapabilityOp payload = new AddMember(group, member);
        return signer.SignAsync(payload, DateTimeOffset.UtcNow, Guid.NewGuid()).AsTask().GetAwaiter().GetResult();
    }

    public static SignedOperation<CapabilityOp> NewRemoveMember(Ed25519Signer signer, PrincipalId group, PrincipalId member)
    {
        CapabilityOp payload = new RemoveMember(group, member);
        return signer.SignAsync(payload, DateTimeOffset.UtcNow, Guid.NewGuid()).AsTask().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _aliceKey.Dispose();
        _bobKey.Dispose();
    }
}
