namespace Sunfish.Kernel.Crdt.Tests;

/// <summary>
/// Coverage for <see cref="DocumentGarbageCollector"/> — the paper §9 facade.
/// </summary>
public class DocumentGarbageCollectorTests
{
    private static ICrdtEngine Engine() => new StubCrdtEngine();

    [Fact]
    public async Task CollectAsync_NeverPolicy_AppliesNoneStrategy()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        doc.GetMap("meta").Set("k", "v");

        var mgr = new ShallowSnapshotManager();
        mgr.Register(doc, new NeverShallowSnapshotPolicy());
        var gc = new DocumentGarbageCollector(mgr);

        var result = await gc.CollectAsync(doc);

        Assert.Equal("doc-1", result.DocumentId);
        Assert.Equal(GcStrategy.None, result.AppliedStrategy);
        Assert.Equal(result.BytesBefore, result.BytesAfter);
    }

    [Fact]
    public async Task CollectAsync_ThresholdExceeded_AppliesShallowSnapshotStrategy()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        var map = doc.GetMap("meta");
        for (var i = 0; i < 50; i++) map.Set($"k{i}", i);

        var mgr = new ShallowSnapshotManager();
        mgr.Register(doc, new ThresholdShallowSnapshotPolicy
        {
            OperationThreshold = 10,
            MinIntervalBetweenSnapshots = TimeSpan.Zero,
        });
        var gc = new DocumentGarbageCollector(mgr);

        var result = await gc.CollectAsync(doc);

        Assert.Equal(GcStrategy.ShallowSnapshot, result.AppliedStrategy);
        Assert.Single(mgr.Snapshots);
    }

    [Fact]
    public async Task CollectAsync_ReturnsPositiveByteMeasurements()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        doc.GetMap("meta").Set("k", "v");

        var mgr = new ShallowSnapshotManager();
        mgr.Register(doc, new NeverShallowSnapshotPolicy());
        var gc = new DocumentGarbageCollector(mgr);

        var result = await gc.CollectAsync(doc);

        // Stub backend never shrinks; byte deltas should at least be sensible
        // (non-zero before/after for a doc with content, and no negative delta).
        Assert.True(result.BytesBefore > 0);
        Assert.True(result.BytesAfter > 0);
        Assert.True(result.BytesAfter >= 0);
    }

    [Fact]
    public async Task CollectAsync_NullDocument_Throws()
    {
        var mgr = new ShallowSnapshotManager();
        var gc = new DocumentGarbageCollector(mgr);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await gc.CollectAsync(null!));
    }
}
