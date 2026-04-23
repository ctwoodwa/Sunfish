using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Sunfish.Bridge;
using Sunfish.Bridge.Relay;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Sync.Handshake;
using Sunfish.Kernel.Sync.Protocol;

using Xunit;

namespace Sunfish.Bridge.Tests.Unit;

/// <summary>
/// Covers the paper §6.1 tier-3 managed-relay semantics in <see cref="RelayServer"/>:
/// start/stop lifecycle, handshake acceptance, cross-team isolation,
/// MaxConnectedNodes enforcement, and per-connection crash isolation.
/// Uses <see cref="InMemorySyncDaemonTransport"/> so the suite stays
/// out-of-process-free.
/// </summary>
public class RelayServerTests
{
    // Shared Ed25519 signer for the HELLO signature path. Wave 6.1 made
    // LocalIdentity.Signer + PrivateKey mandatory for BuildHello; we derive a
    // deterministic keypair per-peer from a seed expanded out of nodeId so
    // tests stay reproducible and each peer still gets its own keypair.
    private static readonly IEd25519Signer TestSigner = new Ed25519Signer();

    private static string NewEndpoint() => $"relay-test-{Guid.NewGuid():N}";

    private static (byte[] PublicKey, byte[] PrivateKey) DeriveKeyPair(byte[] nodeId)
    {
        // Expand the short test nodeId into a 32-byte Ed25519 seed by padding
        // with a fixed byte pattern. Deterministic => reproducible failures.
        var seed = new byte[32];
        for (var i = 0; i < seed.Length; i++)
        {
            seed[i] = (byte)(nodeId[i % nodeId.Length] ^ (byte)(0xA5 + i));
        }
        return TestSigner.GenerateFromSeed(seed);
    }

    private static RelayServer BuildRelay(
        InMemorySyncDaemonTransport listen,
        int maxConnections = 100,
        string[]? allowedTeams = null)
    {
        var options = Options.Create(new BridgeOptions
        {
            Mode = BridgeMode.Relay,
            Relay = new RelayOptions
            {
                MaxConnectedNodes = maxConnections,
                AllowedTeamIds = allowedTeams ?? Array.Empty<string>(),
            },
        });
        return new RelayServer(listen, options, NullLogger<RelayServer>.Instance);
    }

    private static LocalIdentity PeerIdentity(
        byte[] nodeId,
        IReadOnlyList<string>? proposedStreams = null)
    {
        var (publicKey, privateKey) = DeriveKeyPair(nodeId);
        return new LocalIdentity(
            NodeId: nodeId,
            PublicKey: publicKey,
            Signer: TestSigner,
            PrivateKey: privateKey,
            SchemaVersion: HandshakeProtocol.DefaultSchemaVersion,
            SupportedVersions: HandshakeProtocol.DefaultSupportedVersions,
            ProposedStreams: proposedStreams ?? Array.Empty<string>());
    }

    private static async Task<(ISyncDaemonConnection conn, CapabilityResult result)> ConnectPeerAsync(
        InMemorySyncDaemonTransport client,
        string endpoint,
        byte[] nodeId,
        string teamId)
    {
        var conn = await client.ConnectAsync(endpoint, CancellationToken.None);
        var result = await HandshakeProtocol.InitiateAsync(
            conn,
            PeerIdentity(nodeId, new[] { teamId }),
            CancellationToken.None);
        return (conn, result);
    }

    [Fact]
    public async Task Start_And_Stop_Are_Idempotent()
    {
        var ep = NewEndpoint();
        await using var listen = new InMemorySyncDaemonTransport(ep);
        await using var relay = BuildRelay(listen);

        await relay.StartAsync(CancellationToken.None);
        await relay.StartAsync(CancellationToken.None); // no-op
        Assert.Equal(0, relay.ConnectedCount);

        await relay.StopAsync(CancellationToken.None);
        await relay.StopAsync(CancellationToken.None); // no-op
    }

    [Fact]
    public async Task Accepts_Peer_Connection_And_Increments_ConnectedCount()
    {
        var ep = NewEndpoint();
        await using var listen = new InMemorySyncDaemonTransport(ep);
        await using var client = new InMemorySyncDaemonTransport();
        await using var relay = BuildRelay(listen);

        var connectedSignal = new TaskCompletionSource<ConnectedNode>();
        relay.NodeConnected += (_, e) => connectedSignal.TrySetResult(e.Node);

        await relay.StartAsync(CancellationToken.None);

        var (conn, _) = await ConnectPeerAsync(client, ep, new byte[] { 1 }, "team-A");
        await using (conn)
        {
            var node = await connectedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("team-A", node.TeamId);
            Assert.Equal(1, relay.ConnectedCount);
        }
    }

    [Fact]
    public async Task Two_Peers_Same_Team_Fan_Out_Delta_To_Each_Other()
    {
        var ep = NewEndpoint();
        await using var listen = new InMemorySyncDaemonTransport(ep);
        await using var client = new InMemorySyncDaemonTransport();
        await using var relay = BuildRelay(listen);

        var connCount = 0;
        var bothConnected = new TaskCompletionSource<bool>();
        relay.NodeConnected += (_, _) =>
        {
            if (Interlocked.Increment(ref connCount) == 2) bothConnected.TrySetResult(true);
        };

        await relay.StartAsync(CancellationToken.None);

        var (connA, _) = await ConnectPeerAsync(client, ep, new byte[] { 0xA }, "team-X");
        var (connB, _) = await ConnectPeerAsync(client, ep, new byte[] { 0xB }, "team-X");
        await using var _a = connA;
        await using var _b = connB;

        await bothConnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var delta = new DeltaStreamMessage("team-X", 1ul, new byte[] { 0xFF });
        await connA.SendAsync(delta, CancellationToken.None);

        var received = await connB.ReceiveAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        var relayed = Assert.IsType<DeltaStreamMessage>(received);
        Assert.Equal("team-X", relayed.StreamId);
        Assert.Equal(1ul, relayed.OpSequence);
    }

    [Fact]
    public async Task Peers_In_Different_Teams_Are_Not_Fanned_Out()
    {
        var ep = NewEndpoint();
        await using var listen = new InMemorySyncDaemonTransport(ep);
        await using var client = new InMemorySyncDaemonTransport();
        await using var relay = BuildRelay(listen);

        var connCount = 0;
        var bothConnected = new TaskCompletionSource<bool>();
        relay.NodeConnected += (_, _) =>
        {
            if (Interlocked.Increment(ref connCount) == 2) bothConnected.TrySetResult(true);
        };

        await relay.StartAsync(CancellationToken.None);

        var (connA, _) = await ConnectPeerAsync(client, ep, new byte[] { 0xA }, "team-X");
        var (connB, _) = await ConnectPeerAsync(client, ep, new byte[] { 0xB }, "team-Y");
        await using var _a = connA;
        await using var _b = connB;

        await bothConnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var delta = new DeltaStreamMessage("team-X", 1ul, new byte[] { 0xFF });
        await connA.SendAsync(delta, CancellationToken.None);

        // connB should receive nothing within the timeout — cross-team isolation.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await connB.ReceiveAsync(timeoutCts.Token));
    }

    [Fact]
    public async Task MaxConnectedNodes_Is_Enforced()
    {
        var ep = NewEndpoint();
        await using var listen = new InMemorySyncDaemonTransport(ep);
        await using var client = new InMemorySyncDaemonTransport();
        await using var relay = BuildRelay(listen, maxConnections: 1);

        var firstConnected = new TaskCompletionSource<bool>();
        relay.NodeConnected += (_, _) => firstConnected.TrySetResult(true);

        await relay.StartAsync(CancellationToken.None);

        var (connA, _) = await ConnectPeerAsync(client, ep, new byte[] { 0xA }, "team-A");
        await using var _a = connA;
        await firstConnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, relay.ConnectedCount);

        // Second connection should be rejected at accept time with an ERROR frame.
        var connB = await client.ConnectAsync(ep, CancellationToken.None);
        await using var _b = connB;

        var inbound = await connB.ReceiveAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        var err = Assert.IsType<ErrorMessage>(inbound);
        Assert.Equal(ErrorCode.RateLimitExceeded, err.Code);
        Assert.False(err.Recoverable);
    }

    [Fact]
    public async Task AllowedTeamIds_Filter_Rejects_Non_Allowlisted_Team()
    {
        var ep = NewEndpoint();
        await using var listen = new InMemorySyncDaemonTransport(ep);
        await using var client = new InMemorySyncDaemonTransport();
        await using var relay = BuildRelay(listen, allowedTeams: new[] { "team-allowed" });

        await relay.StartAsync(CancellationToken.None);

        // Handshake completes, then the team allowlist rejects.
        var conn = await client.ConnectAsync(ep, CancellationToken.None);
        _ = await HandshakeProtocol.InitiateAsync(
            conn,
            PeerIdentity(new byte[] { 0x01 }, new[] { "team-blocked" }),
            CancellationToken.None);

        var inbound = await conn.ReceiveAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        var err = Assert.IsType<ErrorMessage>(inbound);
        Assert.False(err.Recoverable);
        Assert.Contains("not allowlisted", err.Message, StringComparison.OrdinalIgnoreCase);

        await conn.DisposeAsync();
    }

    [Fact]
    public async Task Graceful_Peer_Disconnect_Decrements_Count_And_Fires_Event()
    {
        var ep = NewEndpoint();
        await using var listen = new InMemorySyncDaemonTransport(ep);
        await using var client = new InMemorySyncDaemonTransport();
        await using var relay = BuildRelay(listen);

        var connected = new TaskCompletionSource<bool>();
        var disconnected = new TaskCompletionSource<NodeDisconnectedEventArgs>();
        relay.NodeConnected += (_, _) => connected.TrySetResult(true);
        relay.NodeDisconnected += (_, e) => disconnected.TrySetResult(e);

        await relay.StartAsync(CancellationToken.None);

        var (conn, _) = await ConnectPeerAsync(client, ep, new byte[] { 0x01 }, "team-A");
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, relay.ConnectedCount);

        await conn.DisposeAsync();

        var evt = await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(evt.NodeId);
        // Give the relay a tick to remove the connection from the dictionary.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (relay.ConnectedCount != 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
        Assert.Equal(0, relay.ConnectedCount);
    }

    [Fact]
    public async Task Crash_In_One_Connection_Does_Not_Disturb_Other_Peers()
    {
        var ep = NewEndpoint();
        await using var listen = new InMemorySyncDaemonTransport(ep);
        await using var client = new InMemorySyncDaemonTransport();
        await using var relay = BuildRelay(listen);

        var connCount = 0;
        var bothConnected = new TaskCompletionSource<bool>();
        relay.NodeConnected += (_, _) =>
        {
            if (Interlocked.Increment(ref connCount) == 2) bothConnected.TrySetResult(true);
        };

        await relay.StartAsync(CancellationToken.None);

        var (connA, _) = await ConnectPeerAsync(client, ep, new byte[] { 0xA }, "team-A");
        var (connB, _) = await ConnectPeerAsync(client, ep, new byte[] { 0xB }, "team-A");
        await bothConnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Kill peer A abruptly.
        await connA.DisposeAsync();

        // Wait for relay to notice.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (relay.ConnectedCount > 1 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
        Assert.Equal(1, relay.ConnectedCount);

        // Peer B should still be able to send (even if no fan-out destinations).
        var ping = new GossipPingMessage(
            VectorClock: new Dictionary<string, ulong> { ["B"] = 1ul },
            PeerMembershipDelta: new MembershipDelta(Array.Empty<byte[]>(), Array.Empty<byte[]>()),
            MonotonicNonce: 1);
        await connB.SendAsync(ping, CancellationToken.None);

        await connB.DisposeAsync();
    }

    [Fact]
    public async Task GossipPing_Is_Also_Fanned_Out_To_Co_Tenant_Peers()
    {
        var ep = NewEndpoint();
        await using var listen = new InMemorySyncDaemonTransport(ep);
        await using var client = new InMemorySyncDaemonTransport();
        await using var relay = BuildRelay(listen);

        var connCount = 0;
        var bothConnected = new TaskCompletionSource<bool>();
        relay.NodeConnected += (_, _) =>
        {
            if (Interlocked.Increment(ref connCount) == 2) bothConnected.TrySetResult(true);
        };

        await relay.StartAsync(CancellationToken.None);

        var (connA, _) = await ConnectPeerAsync(client, ep, new byte[] { 0xA }, "team-P");
        var (connB, _) = await ConnectPeerAsync(client, ep, new byte[] { 0xB }, "team-P");
        await using var _a = connA;
        await using var _b = connB;
        await bothConnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var ping = new GossipPingMessage(
            VectorClock: new Dictionary<string, ulong> { ["A"] = 42ul },
            PeerMembershipDelta: new MembershipDelta(Array.Empty<byte[]>(), Array.Empty<byte[]>()),
            MonotonicNonce: 7);
        await connA.SendAsync(ping, CancellationToken.None);

        var inbound = await connB.ReceiveAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        var relayed = Assert.IsType<GossipPingMessage>(inbound);
        Assert.Equal(7ul, relayed.MonotonicNonce);
        Assert.Equal(42ul, relayed.VectorClock["A"]);
    }
}
