namespace Sunfish.Kernel.Crdt.Tests;

/// <summary>
/// Coverage for <see cref="ICrdtText"/>: basic insert/delete, local change events, and
/// concurrent-edit convergence across two peers exchanging deltas.
/// </summary>
public class CrdtTextTests
{
    private static ICrdtEngine Engine() => new StubCrdtEngine();

    [Fact]
    public async Task Insert_AppendsText()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var text = doc.GetText("body");

        text.Insert(0, "Hello");
        text.Insert(5, ", world");

        Assert.Equal("Hello, world", text.Value);
        Assert.Equal(12, text.Length);
    }

    [Fact]
    public async Task Delete_RemovesRange()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var text = doc.GetText("body");

        text.Insert(0, "Hello, world");
        text.Delete(5, 7); // remove ", world"

        Assert.Equal("Hello", text.Value);
    }

    [Fact]
    public async Task Insert_RaisesChangedEvent()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var text = doc.GetText("body");

        var fired = false;
        text.Changed += (_, _) => fired = true;

        text.Insert(0, "hi");

        Assert.True(fired);
    }

    [Fact]
    public async Task ConcurrentInserts_ConvergeAfterDeltaExchange()
    {
        // Paper §2.2: concurrent writes to AP-class data must converge.
        // Scenario: two peers start from the same snapshot, each edits locally,
        // then they exchange deltas. Both must see the same final Value.
        var engine = Engine();
        await using var alice = engine.CreateDocument("note-1");
        var aliceText = alice.GetText("body");
        aliceText.Insert(0, "shared ");
        var baseSnapshot = alice.ToSnapshot();

        await using var bob = engine.OpenDocument("note-1", baseSnapshot);
        var bobText = bob.GetText("body");

        // Concurrent edits.
        aliceText.Insert(aliceText.Length, "[alice]");
        bobText.Insert(bobText.Length, "[bob]");

        // Exchange deltas both ways.
        var aliceClock = alice.VectorClock;
        var bobClock = bob.VectorClock;
        var aliceDelta = alice.EncodeDelta(bobClock);
        var bobDelta = bob.EncodeDelta(aliceClock);

        bob.ApplyDelta(aliceDelta);
        alice.ApplyDelta(bobDelta);

        // Convergence: both replicas agree.
        Assert.Equal(aliceText.Value, bobText.Value);
    }

    [Fact]
    public async Task ApplyDelta_IsIdempotent()
    {
        var engine = Engine();
        await using var alice = engine.CreateDocument("doc-1");
        alice.GetText("body").Insert(0, "abc");
        await using var bob = engine.CreateDocument("doc-1");

        var delta = alice.EncodeDelta(ReadOnlyMemory<byte>.Empty);
        bob.ApplyDelta(delta);
        bob.ApplyDelta(delta); // second application must be a no-op.

        Assert.Equal("abc", bob.GetText("body").Value);
    }

    [Fact]
    public async Task SnapshotRoundTrip_PreservesState()
    {
        var engine = Engine();
        await using var source = engine.CreateDocument("doc-1");
        source.GetText("body").Insert(0, "persistent");

        var snapshot = source.ToSnapshot();
        await using var restored = engine.OpenDocument("doc-1", snapshot);

        Assert.Equal("persistent", restored.GetText("body").Value);
    }

    [Fact]
    public async Task Insert_OutOfRange_Throws()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var text = doc.GetText("body");

        Assert.Throws<ArgumentOutOfRangeException>(() => text.Insert(5, "x"));
    }
}
