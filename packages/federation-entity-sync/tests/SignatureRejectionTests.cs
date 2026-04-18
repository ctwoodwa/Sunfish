using Sunfish.Federation.Common;
using Sunfish.Federation.EntitySync.Protocol;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Federation.EntitySync.Tests;

public class SignatureRejectionTests
{
    [Fact]
    public async Task PullFromPeer_RejectsTamperedChange()
    {
        using var aliceKey = KeyPair.Generate();
        using var bobKey = KeyPair.Generate();
        var aliceSigner = new Ed25519Signer(aliceKey);
        var bobSigner = new Ed25519Signer(bobKey);
        var verifier = new Ed25519Verifier();

        var transport = new InMemorySyncTransport();
        var aliceStore = new InMemoryChangeStore();
        var bobStore = new InMemoryChangeStore();
        using var aliceSyncer = new InMemoryEntitySyncer(aliceStore, transport, aliceSigner, verifier);
        using var bobSyncer = new InMemoryEntitySyncer(bobStore, transport, bobSigner, verifier);
        var aliceDescriptor = new PeerDescriptor(PeerId.From(aliceKey.PrincipalId), new Uri("inmem://alice"));

        var entity = TestData.NewEntity("tampered-1");

        // Start with a valid signed change.
        var original = TestData.NewSigned(aliceSigner, entity, sequence: 1);

        // Tamper with the payload while keeping the original signature. `with` produces a new record
        // that has a different Diff value but carries the old (now-invalid) signature.
        var tamperedPayload = original.Payload with { Diff = new byte[] { 0xFF, 0xFE, 0xFD } };
        var tampered = original with { Payload = tamperedPayload };

        // Place the tampered op directly in Alice's store so it will be advertised and returned.
        aliceStore.Put(tampered);

        // Bob pulls — should reject the tampered change.
        var result = await bobSyncer.PullFromAsync(aliceDescriptor, scope: null, CancellationToken.None);

        Assert.Equal(0, result.ChangesTransferred);
        Assert.Equal(1, result.ChangesRejected);
        var rejection = Assert.Single(result.Rejections);
        Assert.Equal(tampered.Payload.VersionId, rejection.VersionId);
        Assert.Contains("signature", rejection.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(bobStore.Contains(tampered.Payload.VersionId));
    }

    [Fact]
    public async Task Sync_IncludesFullChain_ForMultiStepLineage()
    {
        using var aliceKey = KeyPair.Generate();
        using var bobKey = KeyPair.Generate();
        var aliceSigner = new Ed25519Signer(aliceKey);
        var bobSigner = new Ed25519Signer(bobKey);
        var verifier = new Ed25519Verifier();

        var transport = new InMemorySyncTransport();
        var aliceStore = new InMemoryChangeStore();
        var bobStore = new InMemoryChangeStore();
        using var aliceSyncer = new InMemoryEntitySyncer(aliceStore, transport, aliceSigner, verifier);
        using var bobSyncer = new InMemoryEntitySyncer(bobStore, transport, bobSigner, verifier);
        var aliceDescriptor = new PeerDescriptor(PeerId.From(aliceKey.PrincipalId), new Uri("inmem://alice"));

        var entity = TestData.NewEntity("chain-1");

        // Build a 3-step lineage: a1 → a2 → a3 on Alice.
        var a1 = TestData.NewSigned(aliceSigner, entity, sequence: 1);
        var a2 = TestData.NewSigned(aliceSigner, entity, sequence: 2, parent: a1.Payload.VersionId);
        var a3 = TestData.NewSigned(aliceSigner, entity, sequence: 3, parent: a2.Payload.VersionId);
        aliceStore.Put(a1);
        aliceStore.Put(a2);
        aliceStore.Put(a3);

        var result = await bobSyncer.PullFromAsync(aliceDescriptor, scope: null, CancellationToken.None);

        Assert.Equal(3, result.ChangesTransferred);
        Assert.Equal(0, result.ChangesRejected);
        Assert.True(bobStore.Contains(a1.Payload.VersionId));
        Assert.True(bobStore.Contains(a2.Payload.VersionId));
        Assert.True(bobStore.Contains(a3.Payload.VersionId));
    }
}
