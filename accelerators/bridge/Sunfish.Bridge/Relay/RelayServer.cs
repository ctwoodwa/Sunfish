using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Sunfish.Kernel.Sync.Handshake;
using Sunfish.Kernel.Sync.Protocol;

namespace Sunfish.Bridge.Relay;

/// <summary>
/// Default <see cref="IRelayServer"/>. Listens on an
/// <see cref="ISyncDaemonTransport"/>, runs the
/// <see cref="HandshakeProtocol.RespondAsync"/> ladder against each inbound
/// connection, and then fans out <see cref="DeltaStreamMessage"/> /
/// <see cref="GossipPingMessage"/> frames to co-tenant peers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Team-id derivation.</b> The handshake itself does not negotiate a
/// team-id (team scoping lives in the discovery layer and in per-stream
/// attestations). For relay fan-out we take the first entry of
/// <see cref="CapabilityResult.Granted"/> — stream names in the paper
/// §6.2 model are team-scoped — and use it as the peer's effective team
/// identifier. Peers that propose no streams land in an empty-team bucket
/// and only exchange with other empty-team peers, which is the safe
/// default (no cross-team leakage).
/// </para>
/// <para>
/// <b>Statelessness.</b> The relay holds only the in-memory
/// <see cref="_connections"/> map; nothing is persisted. Crash = peers
/// reconnect.
/// </para>
/// </remarks>
public sealed class RelayServer : IRelayServer
{
    private readonly ISyncDaemonTransport _transport;
    private readonly RelayOptions _options;
    private readonly ILogger<RelayServer> _logger;

    private readonly ConcurrentDictionary<string, RelayConnection> _connections =
        new(StringComparer.Ordinal);

    private readonly Lock _stateLock = new();
    private CancellationTokenSource? _acceptCts;
    private Task? _acceptTask;
    private bool _disposed;

    public RelayServer(
        ISyncDaemonTransport transport,
        IOptions<BridgeOptions> options,
        ILogger<RelayServer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);

        _transport = transport;
        _options = options.Value.Relay ?? new RelayOptions();
        _logger = logger ?? NullLogger<RelayServer>.Instance;
    }

    public IReadOnlyCollection<ConnectedNode> ConnectedNodes =>
        _connections.Values.Select(c => c.Node).ToList();

    public int ConnectedCount => _connections.Count;

    public event EventHandler<NodeConnectedEventArgs>? NodeConnected;
    public event EventHandler<NodeDisconnectedEventArgs>? NodeDisconnected;

    public Task StartAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_stateLock)
        {
            if (_acceptTask is not null)
            {
                return Task.CompletedTask; // idempotent
            }
            _acceptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _acceptTask = Task.Run(() => AcceptLoopAsync(_acceptCts.Token));
        }

        _logger.LogInformation(
            "Relay server started (max={Max}, listen='{Listen}', teams={Teams}).",
            _options.MaxConnectedNodes,
            _options.ListenEndpoint ?? "(transport-default)",
            _options.AllowedTeamIds.Length == 0 ? "(any)" : string.Join(",", _options.AllowedTeamIds));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (_stateLock)
        {
            cts = _acceptCts;
            task = _acceptTask;
            _acceptCts = null;
            _acceptTask = null;
        }

        if (cts is null)
        {
            return; // idempotent
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException) { /* race with Dispose — fine */ }

        if (task is not null)
        {
            try
            {
                await task.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Relay accept loop ended with error.");
            }
        }

        // Drop all connections.
        foreach (var (nodeId, conn) in _connections.ToArray())
        {
            _connections.TryRemove(nodeId, out _);
            await conn.DisposeQuietlyAsync().ConfigureAwait(false);
            NodeDisconnected?.Invoke(this, new NodeDisconnectedEventArgs(nodeId, "relay-stopping"));
        }

        cts.Dispose();

        _logger.LogInformation("Relay server stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during relay dispose.");
        }
    }

    // ---------------------------------------------------------------------

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var connection in _transport.ListenAsync(ct).ConfigureAwait(false))
            {
                // Fire-and-forget per-connection handshake. Any handshake
                // failure closes the connection without affecting the loop.
                _ = Task.Run(() => HandleConnectionAsync(connection, ct), ct);
            }
        }
        catch (OperationCanceledException) { /* shutdown path */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Relay accept loop crashed.");
        }
    }

    private async Task HandleConnectionAsync(ISyncDaemonConnection connection, CancellationToken ct)
    {
        // Enforce MaxConnectedNodes before the handshake commits any work.
        if (_connections.Count >= _options.MaxConnectedNodes)
        {
            _logger.LogWarning(
                "Relay rejecting new connection from {Remote}: max {Max} reached.",
                connection.RemoteEndpoint, _options.MaxConnectedNodes);
            await SendErrorAndCloseAsync(
                connection,
                ErrorCode.RateLimitExceeded,
                "Relay at capacity.",
                recoverable: false,
                ct).ConfigureAwait(false);
            return;
        }

        // Relay identity is a deterministic zero-id; the handshake's peer-verification
        // work is downstream of Wave 1.6 (attestation) — see HandshakeProtocol.cs remarks.
        // PrivateKey: null — the relay does not sign; Signer is already null.
        var identity = new LocalIdentity(
            NodeId: new byte[16],
            PublicKey: new byte[32],
            Signer: null,
            PrivateKey: null,
            SchemaVersion: HandshakeProtocol.DefaultSchemaVersion,
            SupportedVersions: HandshakeProtocol.DefaultSupportedVersions);

        CapabilityResult result;
        try
        {
            result = await HandshakeProtocol.RespondAsync(
                connection,
                identity,
                // Relay is a pass-through: grant everything the peer proposes.
                // Attestation evaluation is a later wave; relay-side policy
                // belongs with the per-tenant revenue SKU in paper §17.2.
                policy: proposal => new AckMessage(
                    GrantedSubscriptions: proposal.ProposedStreams,
                    Rejected: Array.Empty<Rejection>()),
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Handshake failed with {Remote}; closing.", connection.RemoteEndpoint);
            await connection.DisposeAsync().ConfigureAwait(false);
            return;
        }

        var teamId = result.Granted.Count > 0 ? result.Granted[0] : string.Empty;

        // Enforce team allowlist AFTER handshake; the allowlist is an
        // operator-policy gate, not a transport decision.
        if (_options.AllowedTeamIds.Length > 0
            && Array.IndexOf(_options.AllowedTeamIds, teamId) < 0)
        {
            _logger.LogWarning(
                "Relay refusing connection from {Remote}: team '{Team}' not allowlisted.",
                connection.RemoteEndpoint, teamId);
            await SendErrorAndCloseAsync(
                connection,
                ErrorCode.RateLimitExceeded,
                "Team not allowlisted on this relay.",
                recoverable: false,
                ct).ConfigureAwait(false);
            return;
        }

        var nodeId = Convert.ToHexString(result.PeerNodeId);
        var node = new ConnectedNode(
            NodeId: nodeId,
            RemoteEndpoint: connection.RemoteEndpoint,
            TeamId: teamId,
            ConnectedAt: DateTimeOffset.UtcNow);

        var relayConnection = new RelayConnection(node, connection);
        if (!_connections.TryAdd(nodeId, relayConnection))
        {
            _logger.LogWarning("Relay refusing duplicate connection from {NodeId}.", nodeId);
            await connection.DisposeAsync().ConfigureAwait(false);
            return;
        }

        _logger.LogInformation(
            "Peer connected: nodeId={NodeId} team='{Team}' remote={Remote} count={Count}.",
            nodeId, teamId, connection.RemoteEndpoint, _connections.Count);
        NodeConnected?.Invoke(this, new NodeConnectedEventArgs(node));

        string reason = "peer-closed";
        try
        {
            await RelayLoopAsync(relayConnection, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            reason = "relay-stopping";
        }
        catch (Exception ex)
        {
            reason = $"error:{ex.GetType().Name}";
            _logger.LogInformation(ex, "Relay loop for {NodeId} ended with error.", nodeId);
        }
        finally
        {
            _connections.TryRemove(nodeId, out _);
            await relayConnection.DisposeQuietlyAsync().ConfigureAwait(false);
            NodeDisconnected?.Invoke(this, new NodeDisconnectedEventArgs(nodeId, reason));
            _logger.LogInformation(
                "Peer disconnected: nodeId={NodeId} reason={Reason} count={Count}.",
                nodeId, reason, _connections.Count);
        }
    }

    private async Task RelayLoopAsync(RelayConnection sender, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var inbound = await sender.Connection.ReceiveAsync(ct).ConfigureAwait(false);
            switch (inbound)
            {
                case DeltaStreamMessage delta:
                    await FanOutAsync(sender, delta, ct).ConfigureAwait(false);
                    break;
                case GossipPingMessage ping:
                    await FanOutAsync(sender, ping, ct).ConfigureAwait(false);
                    break;
                case ErrorMessage err:
                    _logger.LogInformation(
                        "Peer {NodeId} reported error {Code}: {Msg}",
                        sender.Node.NodeId, err.Code, err.Message);
                    // Peer-initiated error → close on non-recoverable.
                    if (!err.Recoverable) return;
                    break;
                default:
                    // Out-of-protocol frames are dropped. The relay is not a
                    // lease/ack authority — LEASE_* / ACK / CAPABILITY_NEG are
                    // silently ignored rather than forwarded. See paper §6.1
                    // tier-3 scope (fan-out-only).
                    break;
            }
        }
    }

    private async Task FanOutAsync<TMessage>(
        RelayConnection sender,
        TMessage message,
        CancellationToken ct)
        where TMessage : class
    {
        foreach (var peer in _connections.Values)
        {
            if (ReferenceEquals(peer, sender)) continue;
            if (!string.Equals(peer.Node.TeamId, sender.Node.TeamId, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                await peer.Connection.SendAsync(message, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Fan-out is best-effort — a failing peer must not bring down
                // the sender's loop.
                _logger.LogDebug(ex,
                    "Relay fan-out to {PeerNodeId} failed; dropping frame.",
                    peer.Node.NodeId);
            }
        }
    }

    private static async Task SendErrorAndCloseAsync(
        ISyncDaemonConnection connection,
        ErrorCode code,
        string message,
        bool recoverable,
        CancellationToken ct)
    {
        try
        {
            var err = new ErrorMessage(code, message, recoverable);
            await connection.SendAsync(err, ct).ConfigureAwait(false);
        }
        catch { /* best-effort — peer may already be gone */ }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class RelayConnection
    {
        public ConnectedNode Node { get; }
        public ISyncDaemonConnection Connection { get; }
        private int _disposed;

        public RelayConnection(ConnectedNode node, ISyncDaemonConnection connection)
        {
            Node = node;
            Connection = connection;
        }

        public async ValueTask DisposeQuietlyAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try
            {
                await Connection.DisposeAsync();
            }
            catch { /* quiet */ }
        }
    }
}
