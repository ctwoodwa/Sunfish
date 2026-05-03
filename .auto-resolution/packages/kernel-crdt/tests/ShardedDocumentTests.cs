namespace Sunfish.Kernel.Crdt.Tests;

/// <summary>
/// Coverage for <see cref="ShardedDocument"/> — paper §9 mitigation 2
/// (application-level document sharding).
/// </summary>
public class ShardedDocumentTests
{
    private static ICrdtEngine Engine() => new StubCrdtEngine();

    [Fact]
    public async Task NewShardedDocument_ActiveShardKeys_IsEmpty()
    {
        await using var sharded = new ShardedDocument(Engine(), "doc-1");
        Assert.Empty(sharded.ActiveShardKeys);
    }

    [Fact]
    public async Task GetOrCreateShard_CreatesAndTracksShard()
    {
        await using var sharded = new ShardedDocument(Engine(), "doc-1");

        var shard = sharded.GetOrCreateShard("foo");

        Assert.NotNull(shard);
        Assert.Contains("foo", sharded.ActiveShardKeys);
    }

    [Fact]
    public async Task GetOrCreateShard_ReturnsSameInstanceOnRepeatCall()
    {
        await using var sharded = new ShardedDocument(Engine(), "doc-1");

        var a = sharded.GetOrCreateShard("foo");
        var b = sharded.GetOrCreateShard("foo");

        Assert.Same(a, b);
    }

    [Fact]
    public async Task RetireShard_RemovesFromActiveKeys()
    {
        await using var sharded = new ShardedDocument(Engine(), "doc-1");
        sharded.GetOrCreateShard("foo");
        sharded.GetOrCreateShard("bar");

        Assert.True(sharded.RetireShard("foo"));

        Assert.DoesNotContain("foo", sharded.ActiveShardKeys);
        Assert.Contains("bar", sharded.ActiveShardKeys);
    }

    [Fact]
    public async Task RetireShard_UnknownKey_ReturnsFalse()
    {
        await using var sharded = new ShardedDocument(Engine(), "doc-1");

        Assert.False(sharded.RetireShard("never-created"));
    }

    [Fact]
    public async Task ToSnapshot_ApplySnapshot_RoundTripsShards()
    {
        // Populate sharded doc A with two shards, each carrying distinct data.
        await using var a = new ShardedDocument(Engine(), "doc-1");
        var s1 = a.GetOrCreateShard("one");
        s1.GetMap("meta").Set("who", "alice");
        var s2 = a.GetOrCreateShard("two");
        s2.GetText("body").Insert(0, "hello");

        var snapshot = a.ToSnapshot();
        Assert.False(snapshot.IsEmpty);

        // Apply to a fresh sharded doc B.
        await using var b = new ShardedDocument(Engine(), "doc-1");
        b.ApplySnapshot(snapshot);

        // Active keys restored.
        var keys = b.ActiveShardKeys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "one", "two" }, keys);

        // Sub-document state restored.
        var restoredOne = b.GetOrCreateShard("one");
        Assert.Equal("alice", restoredOne.GetMap("meta").Get<string>("who"));

        var restoredTwo = b.GetOrCreateShard("two");
        Assert.Equal("hello", restoredTwo.GetText("body").Value);
    }

    [Fact]
    public async Task RetiredShard_IsRemovedFromSnapshot()
    {
        await using var a = new ShardedDocument(Engine(), "doc-1");
        var s1 = a.GetOrCreateShard("keep");
        s1.GetMap("meta").Set("k", "v");
        var s2 = a.GetOrCreateShard("drop");
        s2.GetMap("meta").Set("k", "trash");

        Assert.True(a.RetireShard("drop"));
        var snapshot = a.ToSnapshot();

        await using var b = new ShardedDocument(Engine(), "doc-1");
        b.ApplySnapshot(snapshot);

        Assert.Contains("keep", b.ActiveShardKeys);
        Assert.DoesNotContain("drop", b.ActiveShardKeys);
    }

    [Fact]
    public async Task DisposeAsync_PreventsFurtherOperations()
    {
        var sharded = new ShardedDocument(Engine(), "doc-1");
        sharded.GetOrCreateShard("foo");
        await sharded.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => sharded.GetOrCreateShard("bar"));
    }
}
