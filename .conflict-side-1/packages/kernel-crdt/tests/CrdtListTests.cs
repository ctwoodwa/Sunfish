namespace Sunfish.Kernel.Crdt.Tests;

/// <summary>
/// Coverage for <see cref="ICrdtList"/>: insert, push, remove, and concurrent-insert
/// convergence. The stub backend picks a deterministic total order; both peers MUST
/// agree on the order after delta exchange.
/// </summary>
public class CrdtListTests
{
    private static ICrdtEngine Engine() => new StubCrdtEngine();

    [Fact]
    public async Task Push_AppendsItems()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var list = doc.GetList("items");

        list.Push("a");
        list.Push("b");
        list.Push("c");

        Assert.Equal(3, list.Count);
        Assert.Equal("a", list.Get<string>(0));
        Assert.Equal("c", list.Get<string>(2));
    }

    [Fact]
    public async Task Insert_InsertsAtIndex()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var list = doc.GetList("items");

        list.Push("a");
        list.Push("c");
        list.Insert(1, "b");

        Assert.Equal(new[] { "a", "b", "c" },
            Enumerable.Range(0, list.Count).Select(i => list.Get<string>(i)));
    }

    [Fact]
    public async Task RemoveAt_RemovesItem()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var list = doc.GetList("items");

        list.Push("a");
        list.Push("b");
        list.Push("c");

        Assert.True(list.RemoveAt(1));
        Assert.Equal(2, list.Count);
        Assert.Equal("a", list.Get<string>(0));
        Assert.Equal("c", list.Get<string>(1));
    }

    [Fact]
    public async Task RemoveAt_OutOfRange_ReturnsFalse()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var list = doc.GetList("items");

        Assert.False(list.RemoveAt(0));
        list.Push("x");
        Assert.False(list.RemoveAt(5));
    }

    [Fact]
    public async Task Insert_OutOfRange_Throws()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var list = doc.GetList("items");

        Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(2, "x"));
    }

    [Fact]
    public async Task Get_OutOfRange_ReturnsDefault()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var list = doc.GetList("items");

        Assert.Null(list.Get<string>(-1));
        Assert.Null(list.Get<string>(0));
        list.Push("x");
        Assert.Null(list.Get<string>(5));
    }

    [Fact]
    public async Task ConcurrentInsertsAtSameIndex_Converge()
    {
        // Two peers push to the end concurrently; both should agree on final order.
        var engine = Engine();
        await using var alice = engine.CreateDocument("doc-1");
        await using var bob = engine.CreateDocument("doc-1");

        alice.GetList("items").Push("seed");
        bob.ApplyDelta(alice.EncodeDelta(ReadOnlyMemory<byte>.Empty));

        alice.GetList("items").Push("from-alice");
        bob.GetList("items").Push("from-bob");

        var ac = alice.VectorClock;
        var bc = bob.VectorClock;
        alice.ApplyDelta(bob.EncodeDelta(ac));
        bob.ApplyDelta(alice.EncodeDelta(bc));

        var aliceItems = Enumerable.Range(0, alice.GetList("items").Count)
            .Select(i => alice.GetList("items").Get<string>(i))
            .ToList();
        var bobItems = Enumerable.Range(0, bob.GetList("items").Count)
            .Select(i => bob.GetList("items").Get<string>(i))
            .ToList();

        Assert.Equal(aliceItems, bobItems);
        Assert.Equal(3, aliceItems.Count);
    }

    [Fact]
    public async Task Insert_RaisesChangedEventOnRemoteApply()
    {
        var engine = Engine();
        await using var alice = engine.CreateDocument("doc-1");
        await using var bob = engine.CreateDocument("doc-1");

        var bobList = bob.GetList("items");
        var fired = false;
        bobList.Changed += (_, _) => fired = true;

        alice.GetList("items").Push("remote");
        bob.ApplyDelta(alice.EncodeDelta(ReadOnlyMemory<byte>.Empty));

        Assert.True(fired);
    }
}
