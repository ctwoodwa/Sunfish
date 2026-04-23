using Microsoft.Extensions.Options;

using Sunfish.Kernel.Sync.Discovery;

namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// mDNS smoke tests. Require a multicast-capable network segment and
/// Windows Defender Firewall inbound-UDP/5353 permission on Windows. Skipped
/// unless <c>SUNFISH_MDNS_TESTS=1</c> is set in the environment.
/// </summary>
public class MdnsPeerDiscoveryTests
{
    private static bool MdnsEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable("SUNFISH_MDNS_TESTS"),
            "1",
            StringComparison.Ordinal);

    private static PeerAdvertisement BuildAdvertisement(string nodeId, string teamId = "team-alpha")
    {
        return new PeerAdvertisement(
            NodeId: nodeId,
            Endpoint: $"tcp://127.0.0.1:8765",
            PublicKey: new byte[32],
            TeamId: teamId,
            SchemaVersion: "1.0",
            Metadata: new Dictionary<string, string>());
    }

    private static MdnsPeerDiscovery BuildDiscovery(string serviceTypeSuffix)
    {
        // Randomise service type per test so parallel runs don't cross-talk.
        var opts = Options.Create(new PeerDiscoveryOptions
        {
            // Use an ephemeral service type to isolate from anything else on
            // the segment.
            ServiceType = $"_sfnode-{serviceTypeSuffix}._tcp.local",
            DiscoveryIntervalSeconds = 1,
            PeerTtlSeconds = 10,
        });
        return new MdnsPeerDiscovery(opts);
    }

    [SkippableFact]
    public async Task Start_Stop_Roundtrip_On_Loopback()
    {
        Skip.IfNot(MdnsEnabled, "Requires multicast-capable network. Set SUNFISH_MDNS_TESTS=1 to run.");

        await using var discovery = BuildDiscovery(Guid.NewGuid().ToString("N").Substring(0, 8));
        await discovery.StartAsync(BuildAdvertisement("node-a"), CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1));
        await discovery.StopAsync(CancellationToken.None);
    }

    [SkippableFact]
    public async Task Two_Instances_Discover_Each_Other()
    {
        Skip.IfNot(MdnsEnabled, "Requires multicast-capable network. Set SUNFISH_MDNS_TESTS=1 to run.");

        var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        await using var first = BuildDiscovery(suffix);
        await using var second = BuildDiscovery(suffix);

        var firstSawSecond = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        first.PeerDiscovered += (_, e) =>
        {
            if (e.Peer.NodeId == "node-b") firstSawSecond.TrySetResult(e.Peer.NodeId);
        };

        await first.StartAsync(BuildAdvertisement("node-a"), CancellationToken.None);
        await second.StartAsync(BuildAdvertisement("node-b"), CancellationToken.None);

        var seen = await firstSawSecond.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal("node-b", seen);
    }
}

// SkippableFact / Skip.IfNot come from the Xunit.SkippableFact package — the
// standard xUnit 2 idiom for runtime-conditional skips.
