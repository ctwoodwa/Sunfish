using Microsoft.Extensions.Options;

using Sunfish.Kernel.Crdt.Backends;

namespace Sunfish.Integration.KernelSyncRoundtrip.Harness;

/// <summary>
/// Two-node composition helper for the kernel-sync round-trip integration
/// tests. Spins up two <see cref="NodeContext"/>s, each backed by a real
/// <see cref="UnixSocketSyncDaemonTransport"/> listening on a unique,
/// per-test endpoint (Unix domain socket on POSIX, named pipe on Windows).
/// </summary>
/// <remarks>
/// <para>
/// <b>Platform split:</b> <see cref="UnixSocketSyncDaemonTransport"/> already
/// handles the Windows / POSIX switch — we merely need to produce a valid
/// endpoint string for each platform. See <see cref="NewEndpoint"/>.
/// </para>
/// <para>
/// <b>Peer wiring:</b> after construction each node's gossip daemon has the
/// other node's endpoint registered, and each <see cref="FleaseLeaseCoordinator"/>
/// sees the other node through the same gossip daemon. The gossip loop is
/// NOT auto-started — tests opt in by calling
/// <c>Node.Gossip.StartAsync</c> when they want ticks to fire.
/// </para>
/// <para>
/// <b>Lifetime:</b> <see cref="DisposeAsync"/> tears down gossip daemons,
/// lease coordinators, and transports in dependency order, then best-effort
/// deletes any stray socket files. Named pipes are reclaimed by the OS when
/// the server handle disposes.
/// </para>
/// </remarks>
internal sealed class TwoNodeHarness : IAsyncDisposable
{
    public NodeContext NodeA { get; }
    public NodeContext NodeB { get; }
    public string SharedSocketPathA => NodeA.ListenEndpoint;
    public string SharedSocketPathB => NodeB.ListenEndpoint;

    private bool _disposed;

    private TwoNodeHarness(NodeContext nodeA, NodeContext nodeB)
    {
        NodeA = nodeA;
        NodeB = nodeB;
    }

    /// <summary>
    /// Build a two-node harness with real transports on fresh endpoints.
    /// Each node's gossip daemon knows the other node as a peer, and each
    /// lease coordinator's gossip peer list includes the other node. Gossip
    /// is stopped; tests start it explicitly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Listener ownership:</b> the <see cref="UnixSocketSyncDaemonTransport"/>
    /// <see cref="ISyncDaemonTransport.ListenAsync"/> surface is single-consumer.
    /// When <paramref name="enableLeaseResponder"/> is true, the
    /// <see cref="FleaseLeaseCoordinator"/> owns the listener on each node
    /// and answers inbound <c>LEASE_REQUEST</c> / <c>LEASE_RELEASE</c> frames
    /// (the lease tests use this mode). When false, the lease coordinator
    /// is built without a listen endpoint, leaving the transport's
    /// <see cref="ISyncDaemonTransport.ListenAsync"/> free for the test's
    /// own responder loop (the gossip and handshake tests use this mode).
    /// </para>
    /// </remarks>
    public static Task<TwoNodeHarness> StartAsync(
        CancellationToken ct,
        bool enableLeaseResponder = false)
    {
        _ = ct; // reserved for future async wiring
        var endpointA = NewEndpoint(suffix: "a");
        var endpointB = NewEndpoint(suffix: "b");

        var nodeA = NodeContext.Create(
            nodeIdLabel: "node-a",
            listenEndpoint: endpointA,
            enableLeaseResponder: enableLeaseResponder);
        var nodeB = NodeContext.Create(
            nodeIdLabel: "node-b",
            listenEndpoint: endpointB,
            enableLeaseResponder: enableLeaseResponder);

        // Wire bidirectional peering on the gossip daemons so lease
        // proposals route through the known-peers list.
        nodeA.Gossip.AddPeer(endpointB, nodeB.PublicKey);
        nodeB.Gossip.AddPeer(endpointA, nodeA.PublicKey);

        return Task.FromResult(new TwoNodeHarness(nodeA, nodeB));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Tear down B first then A; order does not matter for correctness
        // but keeps logs deterministic.
        try { await NodeB.DisposeAsync(); } catch { /* best-effort */ }
        try { await NodeA.DisposeAsync(); } catch { /* best-effort */ }

        // Best-effort socket cleanup for POSIX. Named pipes are invisible
        // on the filesystem; the OS reclaims the name when the server
        // handle disposes, so nothing to do on Windows.
        TryDeleteSocket(SharedSocketPathA);
        TryDeleteSocket(SharedSocketPathB);
    }

    /// <summary>
    /// Produce a platform-appropriate endpoint string. On POSIX we put the
    /// socket under the system temp dir so we avoid colliding with other
    /// tests and so our own cleanup can wipe the file. On Windows we use
    /// the pipe-name form that <see cref="UnixSocketSyncDaemonTransport"/>
    /// normalises internally.
    /// </summary>
    private static string NewEndpoint(string suffix)
    {
        var token = Guid.NewGuid().ToString("N").Substring(0, 12);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Pipe names are a flat namespace; we stay inside the
            // sunfish-test prefix so stray pipes are easy to spot in
            // Sysinternals / PipeList during debugging.
            return $"sunfish-test-{token}-{suffix}";
        }
        else
        {
            var tempDir = Path.GetTempPath();
            return Path.Combine(tempDir, $"sunfish-test-{token}-{suffix}.sock");
        }
    }

    private static void TryDeleteSocket(string endpoint)
    {
        if (string.IsNullOrEmpty(endpoint)) return;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return; // named pipes — nothing to delete
        try
        {
            if (File.Exists(endpoint))
            {
                File.Delete(endpoint);
            }
        }
        catch
        {
            /* best-effort */
        }
    }
}

/// <summary>
/// A single node's composition root. Owns one real transport, one gossip
/// daemon, one lease coordinator, one event log, one CRDT engine, and an
/// Ed25519 identity. Disposed together.
/// </summary>
internal sealed class NodeContext : IAsyncDisposable
{
    public string NodeId { get; }
    public byte[] NodeIdBytes { get; }
    public byte[] PublicKey { get; }
    public byte[] PrivateKey { get; }
    public string ListenEndpoint { get; }
    public IEd25519Signer Signer { get; }
    public NodeIdentity Identity { get; }
    public IGossipDaemon Gossip { get; }
    public ISyncDaemonTransport Transport { get; }
    public IEventLog EventLog { get; }
    public ILeaseCoordinator Leases { get; }
    public ICrdtEngine Crdt { get; }

    private readonly UnixSocketSyncDaemonTransport _concreteTransport;
    private readonly GossipDaemon _concreteGossip;
    private readonly FleaseLeaseCoordinator _concreteLeases;
    private bool _disposed;

    private NodeContext(
        string nodeId,
        byte[] nodeIdBytes,
        byte[] publicKey,
        byte[] privateKey,
        string listenEndpoint,
        IEd25519Signer signer,
        NodeIdentity identity,
        UnixSocketSyncDaemonTransport transport,
        GossipDaemon gossip,
        FleaseLeaseCoordinator leases,
        IEventLog eventLog,
        ICrdtEngine crdt)
    {
        NodeId = nodeId;
        NodeIdBytes = nodeIdBytes;
        PublicKey = publicKey;
        PrivateKey = privateKey;
        ListenEndpoint = listenEndpoint;
        Signer = signer;
        Identity = identity;
        _concreteTransport = transport;
        Transport = transport;
        _concreteGossip = gossip;
        Gossip = gossip;
        _concreteLeases = leases;
        Leases = leases;
        EventLog = eventLog;
        Crdt = crdt;
    }

    public static NodeContext Create(
        string nodeIdLabel,
        string listenEndpoint,
        bool enableLeaseResponder = true)
    {
        var signer = new Ed25519Signer();
        var (publicKey, privateKey) = signer.GenerateKeyPair();
        // The kernel-sync wire form requires a 16-byte node_id that is
        // distinct from the 32-byte public key. Following the
        // TestIdentityFactory convention, take the first 16 bytes of the
        // public key — stable + debuggable without dragging in a UUID
        // dependency.
        var nodeIdBytes = new byte[16];
        Buffer.BlockCopy(publicKey, 0, nodeIdBytes, 0, 16);
        var nodeIdHex = Convert.ToHexString(nodeIdBytes).ToLowerInvariant();

        var identity = new NodeIdentity(nodeIdHex, publicKey, privateKey);
        var identityProvider = new InMemoryNodeIdentityProvider(identity);

        var transport = new UnixSocketSyncDaemonTransport(listenEndpoint);

        var gossipOpts = Options.Create(new GossipDaemonOptions
        {
            RoundIntervalSeconds = 1,
            PeerPickCount = 1,
            ConnectTimeoutSeconds = 2,
            DeadPeerBackoffSeconds = 5,
        });
        var gossip = new GossipDaemon(
            transport, new VectorClock(), gossipOpts, identityProvider, signer);

        var leaseOpts = Options.Create(new LeaseCoordinatorOptions
        {
            DefaultLeaseDuration = TimeSpan.FromSeconds(30),
            ProposalTimeout = TimeSpan.FromSeconds(2),
            ExpiryPruneInterval = TimeSpan.FromMilliseconds(200),
        });
        // When the lease responder is off, pass a null listen endpoint so
        // the coordinator does not contend with the test's own
        // ListenAsync consumer for the transport. The coordinator is still
        // fully functional as a *proposer* in this mode.
        var leases = new FleaseLeaseCoordinator(
            transport,
            gossip,
            leaseOpts,
            localNodeId: nodeIdLabel,
            localListenEndpoint: enableLeaseResponder ? listenEndpoint : null);

        var eventLog = new InMemoryEventLog();
        var crdt = new StubCrdtEngine();

        return new NodeContext(
            nodeId: nodeIdLabel,
            nodeIdBytes: nodeIdBytes,
            publicKey: publicKey,
            privateKey: privateKey,
            listenEndpoint: listenEndpoint,
            signer: signer,
            identity: identity,
            transport: transport,
            gossip: gossip,
            leases: leases,
            eventLog: eventLog,
            crdt: crdt);
    }

    /// <summary>
    /// Build a <see cref="LocalIdentity"/> for this node using its real
    /// Ed25519 signer + private key. Wave 2.5 wired real HELLO signature
    /// verification, so the returned identity signs HELLO with the full
    /// canonical signing payload and the peer's <see cref="HandshakeProtocol"/>
    /// verifies against <see cref="PublicKey"/>.
    /// </summary>
    public LocalIdentity BuildLocalIdentity(string schemaVersion = HandshakeProtocol.DefaultSchemaVersion)
    {
        return new LocalIdentity(
            NodeId: NodeIdBytes,
            PublicKey: PublicKey,
            Signer: Signer,
            PrivateKey: PrivateKey,
            SchemaVersion: schemaVersion,
            SupportedVersions: HandshakeProtocol.DefaultSupportedVersions);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try { await _concreteGossip.DisposeAsync(); } catch { /* best-effort */ }
        try { await _concreteLeases.DisposeAsync(); } catch { /* best-effort */ }
        try { await _concreteTransport.DisposeAsync(); } catch { /* best-effort */ }
    }
}
