using Microsoft.Extensions.Options;

namespace Sunfish.Integration.KernelSyncRoundtrip;

/// <summary>
/// mDNS discovery integration tests. Skipped unless
/// <c>SUNFISH_MDNS_INTEGRATION_TESTS=1</c> is set, because mDNS requires a
/// multicast-capable segment and on Windows the Defender firewall has to
/// permit inbound UDP/5353. Uses ephemeral service-type names so parallel
/// CI runs do not cross-talk.
/// </summary>
public class PeerDiscoveryIntegrationTests
{
    private static bool MdnsEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable("SUNFISH_MDNS_INTEGRATION_TESTS"),
            "1",
            StringComparison.Ordinal);

    private static MdnsPeerDiscovery BuildDiscovery(string serviceSuffix, int ttlSeconds = 15)
    {
        var opts = Options.Create(new PeerDiscoveryOptions
        {
            ServiceType = $"_sfit-{serviceSuffix}._tcp.local",
            DiscoveryIntervalSeconds = 1,
            PeerTtlSeconds = ttlSeconds,
            FilterByTeamId = true,
        });
        return new MdnsPeerDiscovery(opts);
    }

    private static PeerAdvertisement BuildAdvertisement(string nodeId, string teamId = "team-integration")
    {
        return new PeerAdvertisement(
            NodeId: nodeId,
            Endpoint: "tcp://127.0.0.1:8765",
            PublicKey: new byte[32],
            TeamId: teamId,
            SchemaVersion: "1.0.0",
            Metadata: new Dictionary<string, string>());
    }

    [SkippableFact]
    public async Task Two_Nodes_On_Localhost_Discover_Each_Other_Within_Thirty_Seconds()
    {
        Skip.IfNot(MdnsEnabled,
            "Requires mDNS-capable segment. Set SUNFISH_MDNS_INTEGRATION_TESTS=1 to run.");

        var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        await using var first = BuildDiscovery(suffix);
        await using var second = BuildDiscovery(suffix);

        var firstSawSecond = new TaskCompletionSource<PeerAdvertisement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSawFirst = new TaskCompletionSource<PeerAdvertisement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        first.PeerDiscovered += (_, e) =>
        {
            if (e.Peer.NodeId == "integration-b") firstSawSecond.TrySetResult(e.Peer);
        };
        second.PeerDiscovered += (_, e) =>
        {
            if (e.Peer.NodeId == "integration-a") secondSawFirst.TrySetResult(e.Peer);
        };

        await first.StartAsync(BuildAdvertisement("integration-a"), CancellationToken.None)
            ;
        await second.StartAsync(BuildAdvertisement("integration-b"), CancellationToken.None)
            ;

        var bPeer = await firstSawSecond.Task.WaitAsync(TimeSpan.FromSeconds(30))
            ;
        var aPeer = await secondSawFirst.Task.WaitAsync(TimeSpan.FromSeconds(30))
            ;

        Assert.Equal("integration-b", bPeer.NodeId);
        Assert.Equal("integration-a", aPeer.NodeId);
    }

    [SkippableFact]
    public async Task Disappearing_Node_Is_Marked_Lost_Within_PeerTtlSeconds()
    {
        Skip.IfNot(MdnsEnabled,
            "Requires mDNS-capable segment. Set SUNFISH_MDNS_INTEGRATION_TESTS=1 to run.");

        var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        // Short TTL so the test does not run forever; real deployments
        // pick 30 s per sync-daemon-protocol §3.1.
        await using var stayer = BuildDiscovery(suffix, ttlSeconds: 10);
        var leaver = BuildDiscovery(suffix, ttlSeconds: 10);

        var stayerSawLeaver = new TaskCompletionSource<PeerAdvertisement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stayerLostLeaver = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        stayer.PeerDiscovered += (_, e) =>
        {
            if (e.Peer.NodeId == "integration-leaver")
                stayerSawLeaver.TrySetResult(e.Peer);
        };
        stayer.PeerLost += (_, e) =>
        {
            if (e.NodeId == "integration-leaver")
                stayerLostLeaver.TrySetResult(e.NodeId);
        };

        await stayer.StartAsync(BuildAdvertisement("integration-stayer"), CancellationToken.None)
            ;
        await leaver.StartAsync(BuildAdvertisement("integration-leaver"), CancellationToken.None)
            ;

        await stayerSawLeaver.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // Leaver stops advertising. mDNS implementations generally emit a
        // goodbye packet; if that is missed the TTL sweeps after
        // PeerTtlSeconds. Allow a generous wall-clock budget to cover both
        // paths plus CI jitter.
        await leaver.DisposeAsync();

        var lost = await stayerLostLeaver.Task.WaitAsync(TimeSpan.FromSeconds(45))
            ;
        Assert.Equal("integration-leaver", lost);
    }
}
