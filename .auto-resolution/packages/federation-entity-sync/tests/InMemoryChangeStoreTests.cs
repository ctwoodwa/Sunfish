using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Federation.EntitySync.Tests;

public class InMemoryChangeStoreTests
{
    [Fact]
    public void Put_ThenContains_IsTrue()
    {
        using var key = KeyPair.Generate();
        var signer = new Ed25519Signer(key);
        var entity = TestData.NewEntity("p-1");
        var store = new InMemoryChangeStore();

        var change = TestData.NewSigned(signer, entity, sequence: 1);
        store.Put(change);

        Assert.True(store.Contains(change.Payload.VersionId));
        Assert.Equal(change, store.TryGet(change.Payload.VersionId));
    }

    [Fact]
    public void GetHeads_NoParents_AllChangesAreHeads()
    {
        using var key = KeyPair.Generate();
        var signer = new Ed25519Signer(key);
        var entity = TestData.NewEntity("p-2");
        var store = new InMemoryChangeStore();

        var a = TestData.NewSigned(signer, entity, sequence: 1);
        var b = TestData.NewSigned(signer, entity, sequence: 2);
        var c = TestData.NewSigned(signer, entity, sequence: 3);
        store.Put(a);
        store.Put(b);
        store.Put(c);

        var heads = store.GetHeads(entity);

        Assert.Equal(3, heads.Count);
        Assert.Contains(a.Payload.VersionId, heads);
        Assert.Contains(b.Payload.VersionId, heads);
        Assert.Contains(c.Payload.VersionId, heads);
    }

    [Fact]
    public void GetHeads_WithChain_OnlyTipIsHead()
    {
        using var key = KeyPair.Generate();
        var signer = new Ed25519Signer(key);
        var entity = TestData.NewEntity("p-3");
        var store = new InMemoryChangeStore();

        // Chain: a → b → c (a is root, c is tip)
        var a = TestData.NewSigned(signer, entity, sequence: 1);
        var b = TestData.NewSigned(signer, entity, sequence: 2, parent: a.Payload.VersionId);
        var c = TestData.NewSigned(signer, entity, sequence: 3, parent: b.Payload.VersionId);
        store.Put(a);
        store.Put(b);
        store.Put(c);

        var heads = store.GetHeads(entity);

        var head = Assert.Single(heads);
        Assert.Equal(c.Payload.VersionId, head);
    }

    [Fact]
    public void GetReachableFrom_StopsAtStopSet()
    {
        using var key = KeyPair.Generate();
        var signer = new Ed25519Signer(key);
        var entity = TestData.NewEntity("p-4");
        var store = new InMemoryChangeStore();

        // Chain: a → b → c → d
        var a = TestData.NewSigned(signer, entity, sequence: 1);
        var b = TestData.NewSigned(signer, entity, sequence: 2, parent: a.Payload.VersionId);
        var c = TestData.NewSigned(signer, entity, sequence: 3, parent: b.Payload.VersionId);
        var d = TestData.NewSigned(signer, entity, sequence: 4, parent: c.Payload.VersionId);
        store.Put(a);
        store.Put(b);
        store.Put(c);
        store.Put(d);

        // Starting from [d], stopping at [b], should return d and c but NOT b or a.
        var reachable = store.GetReachableFrom(
            new[] { d.Payload.VersionId },
            new[] { b.Payload.VersionId });

        var ids = reachable.Select(x => x.Payload.VersionId).ToHashSet();
        Assert.Equal(2, ids.Count);
        Assert.Contains(d.Payload.VersionId, ids);
        Assert.Contains(c.Payload.VersionId, ids);
        Assert.DoesNotContain(b.Payload.VersionId, ids);
        Assert.DoesNotContain(a.Payload.VersionId, ids);
    }
}
