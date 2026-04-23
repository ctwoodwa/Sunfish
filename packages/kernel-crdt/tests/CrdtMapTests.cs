namespace Sunfish.Kernel.Crdt.Tests;

/// <summary>
/// Coverage for <see cref="ICrdtMap"/>: set/get/remove, LWW semantics on concurrent
/// writes, and concurrent set-vs-delete convergence.
/// </summary>
public class CrdtMapTests
{
    private static ICrdtEngine Engine() => new StubCrdtEngine();

    [Fact]
    public async Task Set_ThenGet_ReturnsValue()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var map = doc.GetMap("meta");

        map.Set("title", "Hello");
        map.Set("count", 42);

        Assert.Equal("Hello", map.Get<string>("title"));
        Assert.Equal(42, map.Get<int>("count"));
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public async Task Remove_RemovesKey()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var map = doc.GetMap("meta");

        map.Set("k", "v");
        Assert.True(map.ContainsKey("k"));
        Assert.True(map.Remove("k"));
        Assert.False(map.ContainsKey("k"));
        Assert.False(map.Remove("k"));
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsDefault()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var map = doc.GetMap("meta");

        Assert.Null(map.Get<string>("absent"));
        Assert.Equal(0, map.Get<int>("absent"));
    }

    [Fact]
    public async Task Set_RaisesChangedEventOnReplay()
    {
        var engine = Engine();
        await using var source = engine.CreateDocument("doc-1");
        source.GetMap("meta").Set("k", "v");

        await using var peer = engine.CreateDocument("doc-1");
        var peerMap = peer.GetMap("meta");
        var fired = false;
        peerMap.Changed += (_, args) =>
        {
            if (args.Key == "k" && !args.IsDeleted) fired = true;
        };

        peer.ApplyDelta(source.EncodeDelta(ReadOnlyMemory<byte>.Empty));

        Assert.True(fired);
    }

    [Fact]
    public async Task ConcurrentSet_LastWriterWins()
    {
        // Two peers set the same key concurrently. After delta exchange,
        // both must agree on a single winner chosen by the backend's
        // conflict rule. (Stub: (lamport, actor) order replay — the later
        // op in sort order wins.)
        var engine = Engine();
        await using var alice = engine.CreateDocument("doc-1");
        await using var bob = engine.CreateDocument("doc-1");

        alice.GetMap("meta").Set("color", "red");
        bob.GetMap("meta").Set("color", "blue");

        var aliceClock = alice.VectorClock;
        var bobClock = bob.VectorClock;
        alice.ApplyDelta(bob.EncodeDelta(aliceClock));
        bob.ApplyDelta(alice.EncodeDelta(bobClock));

        var a = alice.GetMap("meta").Get<string>("color");
        var b = bob.GetMap("meta").Get<string>("color");
        Assert.Equal(a, b); // convergence — same winner on both sides
        Assert.Contains(a, new[] { "red", "blue" });
    }

    [Fact]
    public async Task ConcurrentSetAndDelete_Converge()
    {
        var engine = Engine();
        await using var alice = engine.CreateDocument("doc-1");
        await using var bob = engine.CreateDocument("doc-1");

        // Both start with the same key.
        alice.GetMap("meta").Set("k", "v");
        bob.ApplyDelta(alice.EncodeDelta(ReadOnlyMemory<byte>.Empty));

        // Alice updates, Bob deletes — concurrently.
        alice.GetMap("meta").Set("k", "v2");
        bob.GetMap("meta").Remove("k");

        // Exchange.
        var ac = alice.VectorClock;
        var bc = bob.VectorClock;
        alice.ApplyDelta(bob.EncodeDelta(ac));
        bob.ApplyDelta(alice.EncodeDelta(bc));

        // Convergence: whatever the outcome, both agree.
        Assert.Equal(
            alice.GetMap("meta").ContainsKey("k"),
            bob.GetMap("meta").ContainsKey("k"));
        Assert.Equal(
            alice.GetMap("meta").Get<string>("k"),
            bob.GetMap("meta").Get<string>("k"));
    }

    [Fact]
    public async Task Keys_EnumeratesPresent()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var map = doc.GetMap("meta");

        map.Set("a", 1);
        map.Set("b", 2);
        map.Set("c", 3);
        map.Remove("b");

        var keys = map.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "a", "c" }, keys);
    }
}
