using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Transport;
using Xunit;

namespace Sunfish.Providers.Mesh.Headscale.Tests;

public sealed class HeadscaleMeshAdapterTests
{
    private static readonly PeerId PeerA = new("peer-a-base64url");
    private static readonly PeerId PeerB = new("peer-b-base64url");
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static (HeadscaleMeshAdapter adapter, FakeHeadscaleClient client, FakeTimeProvider time) NewAdapter()
    {
        var options = new HeadscaleOptions
        {
            BaseUrl = new Uri("https://headscale.example/"),
            ApiKey = "test-api-key",
        };
        var client = new FakeHeadscaleClient(options);
        var time = new FakeTimeProvider(Now);
        var adapter = new HeadscaleMeshAdapter(client, options, time);
        return (adapter, client, time);
    }

    [Fact]
    public void AdapterName_IsHeadscale()
    {
        var (adapter, _, _) = NewAdapter();
        Assert.Equal("headscale", adapter.AdapterName);
    }

    [Fact]
    public void Tier_IsMeshVpn()
    {
        var (adapter, _, _) = NewAdapter();
        Assert.Equal(TransportTier.MeshVpn, adapter.Tier);
    }

    [Fact]
    public void IsAvailable_QueriesHealthCheck_AndCachesResult()
    {
        var (adapter, client, time) = NewAdapter();
        client.HealthValue = true;

        Assert.True(adapter.IsAvailable);
        Assert.Equal(1, client.HealthCheckCallCount);

        // Within cache window — no new call.
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.True(adapter.IsAvailable);
        Assert.Equal(1, client.HealthCheckCallCount);

        // Past cache window — re-checks.
        time.Advance(TimeSpan.FromSeconds(10));
        Assert.True(adapter.IsAvailable);
        Assert.Equal(2, client.HealthCheckCallCount);
    }

    [Fact]
    public void IsAvailable_ReportsFalseWhenHealthCheckFails()
    {
        var (adapter, client, _) = NewAdapter();
        client.HealthValue = false;

        Assert.False(adapter.IsAvailable);
    }

    [Fact]
    public async Task ResolvePeerAsync_PeerRegistered_ReturnsMeshEndpoint()
    {
        var (adapter, client, _) = NewAdapter();
        client.Nodes = new[]
        {
            new HeadscaleNode
            {
                Id = "headscale-node-1",
                Name = "anchor-laptop",
                IpAddresses = new[] { "100.64.0.5" },
                Online = true,
                LastSeen = Now,
                ForcedTags = new[] { "tag:env-prod", HeadscaleMeshAdapter.TagForPeer(PeerA) },
            },
        };

        var endpoint = await adapter.ResolvePeerAsync(PeerA, CancellationToken.None);

        Assert.NotNull(endpoint);
        Assert.Equal(TransportTier.MeshVpn, endpoint!.Tier);
        Assert.Equal("100.64.0.5", endpoint.Endpoint.Address.ToString());
        Assert.Equal(HeadscaleMeshAdapter.DefaultMeshPort, endpoint.Endpoint.Port);
        Assert.Equal(Now, endpoint.LastSeenAt);
    }

    [Fact]
    public async Task ResolvePeerAsync_PeerNotRegistered_ReturnsNull()
    {
        var (adapter, client, _) = NewAdapter();
        client.Nodes = Array.Empty<HeadscaleNode>();

        var endpoint = await adapter.ResolvePeerAsync(PeerA, CancellationToken.None);

        Assert.Null(endpoint);
    }

    [Fact]
    public async Task ResolvePeerAsync_PeerRegisteredButNoMeshIp_ReturnsNull()
    {
        var (adapter, client, _) = NewAdapter();
        client.Nodes = new[]
        {
            new HeadscaleNode
            {
                Id = "headscale-node-1",
                Name = "anchor-laptop",
                IpAddresses = Array.Empty<string>(), // not yet assigned
                ForcedTags = new[] { HeadscaleMeshAdapter.TagForPeer(PeerA) },
            },
        };

        var endpoint = await adapter.ResolvePeerAsync(PeerA, CancellationToken.None);

        Assert.Null(endpoint);
    }

    [Fact]
    public async Task ConnectAsync_PeerNotResolvable_Throws()
    {
        var (adapter, client, _) = NewAdapter();
        client.Nodes = Array.Empty<HeadscaleNode>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.ConnectAsync(PeerA, CancellationToken.None));
    }

    [Fact]
    public async Task GetMeshStatusAsync_HealthyControlPlane_ReturnsPeers()
    {
        var (adapter, client, _) = NewAdapter();
        client.HealthValue = true;
        client.Nodes = new[]
        {
            new HeadscaleNode
            {
                Id = "n1", Name = "a", IpAddresses = new[] { "100.64.0.5" }, Online = true, LastSeen = Now,
                ForcedTags = new[] { HeadscaleMeshAdapter.TagForPeer(PeerA) },
            },
            new HeadscaleNode
            {
                Id = "n2", Name = "b", IpAddresses = new[] { "100.64.0.6" }, Online = true, LastSeen = Now.AddMinutes(-2),
                ForcedTags = new[] { HeadscaleMeshAdapter.TagForPeer(PeerB) },
            },
            new HeadscaleNode // No Sunfish tag → ignored
            {
                Id = "n3", Name = "non-sunfish", IpAddresses = new[] { "100.64.0.7" },
                ForcedTags = new[] { "tag:non-sunfish" },
            },
        };

        var status = await adapter.GetMeshStatusAsync(CancellationToken.None);

        Assert.True(status.IsConnected);
        Assert.Equal(2, status.Peers.Count);
        Assert.Contains(status.Peers, p => p.Peer == PeerA);
        Assert.Contains(status.Peers, p => p.Peer == PeerB);
        Assert.Equal(Now, status.LastHandshakeAt); // most recent across the two
    }

    [Fact]
    public async Task GetMeshStatusAsync_UnhealthyControlPlane_ReturnsEmpty()
    {
        var (adapter, client, _) = NewAdapter();
        client.HealthValue = false;
        client.Nodes = new[]
        {
            new HeadscaleNode { Id = "n1", IpAddresses = new[] { "100.64.0.5" }, ForcedTags = new[] { HeadscaleMeshAdapter.TagForPeer(PeerA) } },
        };

        var status = await adapter.GetMeshStatusAsync(CancellationToken.None);

        Assert.False(status.IsConnected);
        Assert.Empty(status.Peers);
        Assert.Null(status.LastHandshakeAt);
    }

    [Fact]
    public async Task RegisterDeviceAsync_EncodesPeerIdAsForcedTag()
    {
        var (adapter, client, _) = NewAdapter();
        var registration = new MeshDeviceRegistration
        {
            DeviceId = "ignored-by-adapter", // Headscale issues its own
            Peer = PeerA,
            DeviceName = "anchor-laptop-01",
            Tags = new[] { "tag:env-prod" },
        };

        await adapter.RegisterDeviceAsync(registration, CancellationToken.None);

        Assert.Equal(1, client.RegisterCallCount);
        var lastReq = Assert.Single(client.RegisterRequests);
        Assert.Equal("anchor-laptop-01", lastReq.Name);
        Assert.Contains("tag:env-prod", lastReq.ForcedTags);
        Assert.Contains(HeadscaleMeshAdapter.TagForPeer(PeerA), lastReq.ForcedTags);
    }

    [Fact]
    public async Task RegisterDeviceAsync_NullRegistration_Throws()
    {
        var (adapter, _, _) = NewAdapter();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            adapter.RegisterDeviceAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void TagForPeer_RoundTripsThroughTryDecode()
    {
        var tag = HeadscaleMeshAdapter.TagForPeer(PeerA);
        Assert.StartsWith("tag:sunfish-peer-", tag);

        var decoded = HeadscaleMeshAdapter.TryDecodePeerFromTags(new[] { "tag:env-prod", tag, "tag:other" });
        Assert.Equal(PeerA, decoded);
    }

    [Fact]
    public void TryDecodePeerFromTags_NoMatch_ReturnsNull()
    {
        var decoded = HeadscaleMeshAdapter.TryDecodePeerFromTags(new[] { "tag:env-prod", "tag:other" });
        Assert.Null(decoded);
    }

    [Fact]
    public void Constructor_NullClient_Throws()
    {
        var options = new HeadscaleOptions { BaseUrl = new Uri("https://h.example/"), ApiKey = "k" };
        Assert.Throws<ArgumentNullException>(() => new HeadscaleMeshAdapter(null!, options));
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var options = new HeadscaleOptions { BaseUrl = new Uri("https://h.example/"), ApiKey = "k" };
        var http = new HttpClient();
        var client = new HeadscaleClient(http, options);
        Assert.Throws<ArgumentNullException>(() => new HeadscaleMeshAdapter(client, null!));
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    /// <summary>
    /// Test double for <see cref="HeadscaleClient"/>. The base class
    /// methods are virtual; this overrides them without touching real
    /// HTTP.
    /// </summary>
    private sealed class FakeHeadscaleClient : HeadscaleClient
    {
        public FakeHeadscaleClient(HeadscaleOptions options)
            : base(new HttpClient { BaseAddress = options.BaseUrl }, options) { }

        public bool HealthValue { get; set; } = true;
        public int HealthCheckCallCount { get; private set; }
        public IReadOnlyList<HeadscaleNode> Nodes { get; set; } = Array.Empty<HeadscaleNode>();
        public int RegisterCallCount { get; private set; }
        public List<HeadscaleRegisterRequest> RegisterRequests { get; } = new();

        public override Task<bool> HealthCheckAsync(CancellationToken ct)
        {
            HealthCheckCallCount++;
            return Task.FromResult(HealthValue);
        }

        public override Task<IReadOnlyList<HeadscaleNode>> ListNodesAsync(CancellationToken ct) =>
            Task.FromResult(Nodes);

        public override Task<HeadscaleNode> RegisterNodeAsync(HeadscaleRegisterRequest request, CancellationToken ct)
        {
            RegisterCallCount++;
            RegisterRequests.Add(request);
            return Task.FromResult(new HeadscaleNode
            {
                Id = $"headscale-issued-{RegisterCallCount}",
                Name = request.Name,
                ForcedTags = request.ForcedTags,
            });
        }
    }
}
