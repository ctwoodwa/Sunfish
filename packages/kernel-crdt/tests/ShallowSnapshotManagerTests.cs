using Microsoft.Extensions.DependencyInjection;

using Sunfish.Kernel.Crdt.DependencyInjection;

namespace Sunfish.Kernel.Crdt.Tests;

/// <summary>
/// Coverage for <see cref="ShallowSnapshotManager"/> — registration, snapshots, eval pass,
/// event, serialized-per-document access. Paper §9 mitigation 3.
/// </summary>
public class ShallowSnapshotManagerTests
{
    private static ICrdtEngine Engine() => new StubCrdtEngine();

    [Fact]
    public async Task Register_ThenTakeSnapshot_RecordsSnapshot()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        doc.GetMap("meta").Set("k", "v");

        var mgr = new ShallowSnapshotManager();
        mgr.Register(doc, new NeverShallowSnapshotPolicy());

        var record = await mgr.TakeSnapshotAsync("doc-1");

        Assert.Equal("doc-1", record.DocumentId);
        Assert.False(record.SnapshotBytes.IsEmpty);
        Assert.Single(mgr.Snapshots);
    }

    [Fact]
    public async Task TakeSnapshotAsync_UnregisteredDocument_Throws()
    {
        var mgr = new ShallowSnapshotManager();
        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await mgr.TakeSnapshotAsync("never-registered"));
    }

    [Fact]
    public async Task RunEvaluation_NeverPolicy_TakesNoSnapshots()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        doc.GetMap("meta").Set("k", "v");

        var mgr = new ShallowSnapshotManager();
        mgr.Register(doc, new NeverShallowSnapshotPolicy());

        var taken = await mgr.RunEvaluationAsync();

        Assert.Empty(taken);
        Assert.Empty(mgr.Snapshots);
    }

    [Fact]
    public async Task RunEvaluation_ThresholdPolicyExceeded_TakesSnapshot()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        // Generate many ops so the estimated op-count from the vector clock exceeds the threshold.
        var map = doc.GetMap("meta");
        for (var i = 0; i < 50; i++) map.Set($"k{i}", i);

        var mgr = new ShallowSnapshotManager();
        mgr.Register(doc, new ThresholdShallowSnapshotPolicy
        {
            OperationThreshold = 10,
            MinIntervalBetweenSnapshots = TimeSpan.Zero,
        });

        var taken = await mgr.RunEvaluationAsync();

        Assert.Single(taken);
        Assert.Equal("doc-1", taken[0].DocumentId);
        Assert.Single(mgr.Snapshots);
    }

    [Fact]
    public async Task SnapshotTaken_Event_Fires()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        doc.GetMap("meta").Set("k", "v");

        var mgr = new ShallowSnapshotManager();
        mgr.Register(doc, new NeverShallowSnapshotPolicy());

        ShallowSnapshotRecord? observed = null;
        mgr.SnapshotTaken += (_, args) => observed = args.Record;

        var record = await mgr.TakeSnapshotAsync("doc-1");

        Assert.NotNull(observed);
        Assert.Equal(record.DocumentId, observed!.DocumentId);
        Assert.Equal(record.TakenAt, observed.TakenAt);
    }

    [Fact]
    public async Task ConcurrentSnapshots_SameDocument_AreSerialized()
    {
        var engine = Engine();
        await using var doc = engine.CreateDocument("doc-1");
        doc.GetMap("meta").Set("k", "v");

        var mgr = new ShallowSnapshotManager();
        mgr.Register(doc, new NeverShallowSnapshotPolicy());

        // 8 concurrent snapshot calls — should all complete and produce 8 records in order.
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => mgr.TakeSnapshotAsync("doc-1"))
            .ToArray();
        var records = await Task.WhenAll(tasks);

        Assert.Equal(8, records.Length);
        Assert.Equal(8, mgr.Snapshots.Count);
        // Each record should have a monotonically non-decreasing TakenAt timestamp since
        // the manager holds the gate for the duration of each snapshot.
        for (var i = 1; i < mgr.Snapshots.Count; i++)
        {
            Assert.True(mgr.Snapshots[i].TakenAt >= mgr.Snapshots[i - 1].TakenAt);
        }
    }

    [Fact]
    public void DI_AddSunfishCrdtGarbageCollection_RegistersSingletons()
    {
        var services = new ServiceCollection();
        services.AddSunfishCrdtGarbageCollection();

        using var sp = services.BuildServiceProvider();
        var mgr1 = sp.GetRequiredService<IShallowSnapshotManager>();
        var mgr2 = sp.GetRequiredService<IShallowSnapshotManager>();
        Assert.Same(mgr1, mgr2);

        var policy = sp.GetRequiredService<IShallowSnapshotPolicy>();
        Assert.IsType<NeverShallowSnapshotPolicy>(policy);

        var gc = sp.GetRequiredService<IDocumentGarbageCollector>();
        Assert.NotNull(gc);
    }
}
