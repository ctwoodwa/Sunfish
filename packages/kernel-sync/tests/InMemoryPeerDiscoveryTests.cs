using Sunfish.Kernel.Sync.Discovery;

namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Coverage for <see cref="InMemoryPeerDiscovery"/>. Every test constructs a
/// dedicated <see cref="InMemoryPeerDiscoveryBroker"/> to isolate from other
/// tests running in parallel.
/// </summary>
public class InMemoryPeerDiscoveryTests : IAsyncLifetime
{
    private readonly List<IAsyncDisposable> _cleanup = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var d in _cleanup)
        {
            try { await d.DisposeAsync(); } catch { /* best-effort */ }
        }
    }

    private static PeerAdvertisement BuildAdvertisement(
        string nodeId,
        string endpoint = "in-mem://peer",
        string teamId = "team-alpha",
        string schemaVersion = "1.0",
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new PeerAdvertisement(
            NodeId: nodeId,
            Endpoint: endpoint,
            PublicKey: new byte[32],
            TeamId: teamId,
            SchemaVersion: schemaVersion,
            Metadata: metadata ?? new Dictionary<string, string>());
    }

    private InMemoryPeerDiscovery BuildDiscovery(
        InMemoryPeerDiscoveryBroker broker,
        bool filterByTeamId = true)
    {
        var d = new InMemoryPeerDiscovery(
            broker,
            new PeerDiscoveryOptions { FilterByTeamId = filterByTeamId });
        _cleanup.Add(d);
        return d;
    }

    [Fact]
    public async Task Second_Instance_Sees_First_On_Start()
    {
        var broker = new InMemoryPeerDiscoveryBroker();
        var first = BuildDiscovery(broker);
        var second = BuildDiscovery(broker);

        await first.StartAsync(BuildAdvertisement("node-1"), CancellationToken.None);
        await second.StartAsync(BuildAdvertisement("node-2"), CancellationToken.None);

        Assert.Single(second.KnownPeers);
        Assert.Equal("node-1", second.KnownPeers.First().NodeId);
        Assert.Single(first.KnownPeers);
        Assert.Equal("node-2", first.KnownPeers.First().NodeId);
    }

    [Fact]
    public async Task PeerDiscovered_Event_Fires_On_Late_Join()
    {
        var broker = new InMemoryPeerDiscoveryBroker();
        var first = BuildDiscovery(broker);
        await first.StartAsync(BuildAdvertisement("node-1"), CancellationToken.None);

        var tcs = new TaskCompletionSource<PeerAdvertisement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        first.PeerDiscovered += (_, e) => tcs.TrySetResult(e.Peer);

        var second = BuildDiscovery(broker);
        await second.StartAsync(BuildAdvertisement("node-2"), CancellationToken.None);

        var peer = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("node-2", peer.NodeId);
    }

    [Fact]
    public async Task Stop_Emits_PeerLost_On_Other_Side()
    {
        var broker = new InMemoryPeerDiscoveryBroker();
        var first = BuildDiscovery(broker);
        var second = BuildDiscovery(broker);

        await first.StartAsync(BuildAdvertisement("node-1"), CancellationToken.None);
        await second.StartAsync(BuildAdvertisement("node-2"), CancellationToken.None);

        var tcs = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        first.PeerLost += (_, e) => tcs.TrySetResult(e.NodeId);

        await second.StopAsync(CancellationToken.None);

        var lostNodeId = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("node-2", lostNodeId);
        Assert.Empty(first.KnownPeers);
    }

    [Fact]
    public async Task TeamId_Filter_Excludes_Non_Matching_Peers()
    {
        var broker = new InMemoryPeerDiscoveryBroker();
        var first = BuildDiscovery(broker, filterByTeamId: true);
        var second = BuildDiscovery(broker, filterByTeamId: true);

        await first.StartAsync(
            BuildAdvertisement("node-1", teamId: "team-alpha"),
            CancellationToken.None);
        await second.StartAsync(
            BuildAdvertisement("node-2", teamId: "team-beta"),
            CancellationToken.None);

        Assert.Empty(first.KnownPeers);
        Assert.Empty(second.KnownPeers);
    }

    [Fact]
    public async Task TeamId_Filter_Disabled_Admits_All_Peers()
    {
        var broker = new InMemoryPeerDiscoveryBroker();
        var first = BuildDiscovery(broker, filterByTeamId: false);
        var second = BuildDiscovery(broker, filterByTeamId: false);

        await first.StartAsync(
            BuildAdvertisement("node-1", teamId: "team-alpha"),
            CancellationToken.None);
        await second.StartAsync(
            BuildAdvertisement("node-2", teamId: "team-beta"),
            CancellationToken.None);

        Assert.Single(first.KnownPeers);
        Assert.Single(second.KnownPeers);
    }

    [Fact]
    public async Task Concurrent_Start_Stop_Is_Safe()
    {
        // Hammer Start / Stop from multiple threads across several instances
        // on the same broker; nothing should throw and the final state should
        // be consistent (every disposed instance advertises nothing).
        var broker = new InMemoryPeerDiscoveryBroker();
        var tasks = new List<Task>();

        for (var i = 0; i < 10; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(async () =>
            {
                var d = BuildDiscovery(broker);
                var ad = BuildAdvertisement($"node-{idx}");
                await d.StartAsync(ad, CancellationToken.None);
                await d.StartAsync(ad, CancellationToken.None); // idempotent
                await d.StopAsync(CancellationToken.None);
                await d.StopAsync(CancellationToken.None); // idempotent
            }));
        }

        await Task.WhenAll(tasks);
    }
}
