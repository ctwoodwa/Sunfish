using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;

namespace Sunfish.Foundation.Transport.Relay;

/// <summary>
/// Tier-3 (managed-relay) <see cref="IPeerTransport"/> per ADR 0061
/// §"Decision". Wraps the existing Bridge ciphertext-only relay
/// (ADR 0031) as the last-resort failover transport. Always-tried
/// per the selection algorithm — Tier-3 is the floor below T1 and T2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolution semantics.</b> The Tier-3 relay multiplexes all
/// peers through a single Bridge endpoint. <see cref="ResolvePeerAsync"/>
/// always returns a <see cref="PeerEndpoint"/> pointing at the
/// configured relay (resolved to its first IPv4/IPv6 address) — the
/// peer is identified inside the application protocol that runs on
/// top of the connected stream, not at the transport layer.
/// </para>
/// <para>
/// <b>Connect semantics.</b> <see cref="ConnectAsync"/> opens a
/// <see cref="ClientWebSocket"/> session to <see cref="BridgeRelayOptions.RelayUrl"/>
/// and returns a <see cref="WebSocketDuplexStream"/> wrapping it. The
/// kernel-sync gossip daemon's HELLO ladder + role-key handshake run
/// on top of this stream — see <c>Sunfish.Kernel.Sync.HandshakeProtocol</c>
/// for the framing. Ciphertext-only posture is preserved: the
/// transport never sees plaintext payload bytes (per ADR 0031).
/// </para>
/// <para>
/// <b>Availability flag.</b> <see cref="IsAvailable"/> reports
/// transport-level reachability. The default is <c>true</c> when a
/// relay URL is configured; hosts that detect prolonged Bridge outage
/// (via audit events from <see cref="ConnectAsync"/> failures) MAY
/// flip <see cref="MarkUnavailable"/>; a follow-up <see cref="MarkAvailable"/>
/// re-engages the transport. Per ADR 0061 §"Decision", Tier 3 is the
/// always-tried fallback so callers usually leave it available.
/// </para>
/// </remarks>
public sealed class BridgeRelayPeerTransport : IPeerTransport
{
    private readonly BridgeRelayOptions _options;
    private readonly TimeProvider _time;
    private int _availableFlag = 1;

    public BridgeRelayPeerTransport(BridgeRelayOptions options, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public TransportTier Tier => TransportTier.ManagedRelay;

    /// <inheritdoc />
    public bool IsAvailable => Volatile.Read(ref _availableFlag) == 1;

    /// <summary>Marks the transport unavailable so the selector skips it.</summary>
    public void MarkUnavailable() => Volatile.Write(ref _availableFlag, 0);

    /// <summary>Restores availability after <see cref="MarkUnavailable"/>.</summary>
    public void MarkAvailable() => Volatile.Write(ref _availableFlag, 1);

    /// <inheritdoc />
    public Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!TryResolveRelayEndpoint(out var ipEndpoint))
        {
            return Task.FromResult<PeerEndpoint?>(null);
        }
        return Task.FromResult<PeerEndpoint?>(new PeerEndpoint
        {
            Peer = peer,
            Endpoint = ipEndpoint,
            Tier = TransportTier.ManagedRelay,
            DiscoveredAt = _time.GetUtcNow(),
        });
    }

    /// <inheritdoc />
    public async Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct)
    {
        var ws = new ClientWebSocket();
        try
        {
            await ws.ConnectAsync(_options.RelayUrl, ct).ConfigureAwait(false);
        }
        catch
        {
            ws.Dispose();
            throw;
        }
        return new WebSocketDuplexStream(ws);
    }

    private bool TryResolveRelayEndpoint(out IPEndPoint endpoint)
    {
        endpoint = null!;
        var host = _options.RelayUrl.Host;
        var port = _options.RelayUrl.IsDefaultPort ? GetDefaultPortForScheme(_options.RelayUrl.Scheme) : _options.RelayUrl.Port;
        if (port < 0)
        {
            return false;
        }
        if (IPAddress.TryParse(host, out var literal))
        {
            endpoint = new IPEndPoint(literal, port);
            return true;
        }
        try
        {
            var addresses = System.Net.Dns.GetHostAddresses(host);
            if (addresses.Length == 0) return false;
            endpoint = new IPEndPoint(addresses[0], port);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetDefaultPortForScheme(string scheme) => scheme.ToLowerInvariant() switch
    {
        "https" or "wss" => 443,
        "http" or "ws" => 80,
        _ => -1,
    };
}
