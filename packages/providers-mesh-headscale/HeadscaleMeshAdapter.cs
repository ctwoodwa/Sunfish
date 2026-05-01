using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Transport;

namespace Sunfish.Providers.Mesh.Headscale;

/// <summary>
/// Tier-2 (mesh-VPN) <see cref="IMeshVpnAdapter"/> implementation backed by a
/// self-hosted (or organization-hosted) Headscale control plane. The
/// adapter only talks to the Headscale REST API; the WireGuard data
/// plane is the host OS's responsibility (kernel module, wireguard-go,
/// or a sibling adapter such as <c>tailscaled</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>PeerId ↔ node mapping.</b> The Sunfish <see cref="PeerId"/> is
/// encoded as a Headscale ACL tag of the form
/// <c>tag:sunfish-peer-{base64url-encoded-peer-id}</c>. <see cref="RegisterDeviceAsync"/>
/// writes the tag; <see cref="ResolvePeerAsync"/> + <see cref="GetMeshStatusAsync"/>
/// match on it. The two-field <c>(DeviceId, PeerId)</c> shape from
/// <see cref="MeshDeviceRegistration"/> per ADR 0061 A1 is preserved:
/// <c>DeviceId</c> is the Headscale-issued node id; the Sunfish
/// <see cref="PeerId"/> is independent + addressable via the tag.
/// </para>
/// <para>
/// <b>ConnectAsync semantics.</b> The selector calls
/// <see cref="ResolvePeerAsync"/> first; on success it then calls
/// <see cref="ConnectAsync"/>. This adapter assumes the host OS has
/// brought up the WireGuard interface (Headscale is a control-plane;
/// data-plane setup is out of scope per the ADR). When the interface
/// is up, <see cref="ConnectAsync"/> opens a raw TCP socket to the
/// resolved mesh IP + port — same model as the Tier-1 mDNS adapter
/// (<c>Sunfish.Foundation.Transport.Mdns.MdnsPeerTransport</c>).
/// When the interface is down,
/// the connect attempt fails inside the per-tier budget and the
/// selector falls through to Tier 3.
/// </para>
/// <para>
/// <b>License posture.</b> Headscale is BSD-3 + actively maintained; no
/// SSPL/BSL-licensed dependencies are pulled by this adapter.
/// </para>
/// </remarks>
public sealed class HeadscaleMeshAdapter : IMeshVpnAdapter
{
    /// <summary>Prefix for the Headscale ACL tag that encodes the Sunfish PeerId.</summary>
    public const string SunfishPeerTagPrefix = "tag:sunfish-peer-";

    /// <summary>Default WireGuard mesh-data-plane port the adapter dials on <see cref="ConnectAsync"/>.</summary>
    public const int DefaultMeshPort = 51820;

    private readonly HeadscaleClient _client;
    private readonly HeadscaleOptions _options;
    private readonly TimeProvider _time;

    private DateTimeOffset _availabilityCheckedAt = DateTimeOffset.MinValue;
    private bool _availabilityCachedValue;
    private readonly object _availabilityLock = new();

    public HeadscaleMeshAdapter(HeadscaleClient client, HeadscaleOptions options, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _options = options;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string AdapterName => "headscale";

    /// <inheritdoc />
    public TransportTier Tier => TransportTier.MeshVpn;

    /// <inheritdoc />
    /// <remarks>
    /// Cached for <see cref="HeadscaleOptions.AvailabilityCacheDuration"/>
    /// (default 5s). The selector calls this before every
    /// <see cref="ResolvePeerAsync"/> per ADR 0061 §"Tier selection
    /// algorithm" — without caching, every selection round would hit
    /// <c>GET /health</c>.
    /// </remarks>
    public bool IsAvailable
    {
        get
        {
            lock (_availabilityLock)
            {
                if (_time.GetUtcNow() - _availabilityCheckedAt < _options.AvailabilityCacheDuration)
                {
                    return _availabilityCachedValue;
                }
            }
            // Cache miss: probe synchronously. We wrap in Task.Run to
            // avoid blocking on a sync-over-async deadlock on UI/Web
            // SynchronizationContexts.
            var probed = Task.Run(() => _client.HealthCheckAsync(CancellationToken.None)).GetAwaiter().GetResult();
            lock (_availabilityLock)
            {
                _availabilityCheckedAt = _time.GetUtcNow();
                _availabilityCachedValue = probed;
            }
            return probed;
        }
    }

    /// <inheritdoc />
    public async Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct)
    {
        var nodes = await _client.ListNodesAsync(ct).ConfigureAwait(false);
        var match = FindNodeForPeer(nodes, peer);
        if (match is null) return null;
        if (!TryPickIpEndpoint(match, out var ip)) return null;
        return new PeerEndpoint
        {
            Peer = peer,
            Endpoint = new IPEndPoint(ip, DefaultMeshPort),
            Tier = TransportTier.MeshVpn,
            DiscoveredAt = _time.GetUtcNow(),
            LastSeenAt = match.LastSeen,
        };
    }

    /// <inheritdoc />
    public async Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct)
    {
        var endpoint = await ResolvePeerAsync(peer, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"HeadscaleMeshAdapter: peer {peer.Value} is not registered in the Headscale control plane (or has no mesh IP).");
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(endpoint.Endpoint.Address, endpoint.Endpoint.Port, ct).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            throw;
        }
        return new HeadscaleTcpDuplexStream(client);
    }

    /// <inheritdoc />
    public async Task<MeshNodeStatus> GetMeshStatusAsync(CancellationToken ct)
    {
        bool isConnected;
        try
        {
            isConnected = await _client.HealthCheckAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            isConnected = false;
        }

        var nodes = isConnected
            ? await _client.ListNodesAsync(ct).ConfigureAwait(false)
            : Array.Empty<HeadscaleNode>();

        var peers = new List<MeshPeer>();
        foreach (var node in nodes)
        {
            var peer = TryDecodePeerFromTags(node.ForcedTags);
            if (peer is null) continue;
            if (!TryPickIpEndpoint(node, out var ip)) continue;
            peers.Add(new MeshPeer
            {
                Peer = peer.Value,
                MeshEndpoint = new IPEndPoint(ip, DefaultMeshPort),
                LastHandshakeAt = node.LastSeen ?? _time.GetUtcNow(),
            });
        }

        return new MeshNodeStatus
        {
            IsConnected = isConnected,
            Peers = peers,
            LastHandshakeAt = peers.Count == 0 ? null : peers.Max(p => p.LastHandshakeAt),
        };
    }

    /// <inheritdoc />
    public async Task RegisterDeviceAsync(MeshDeviceRegistration registration, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var tags = new List<string>(registration.Tags) { TagForPeer(registration.Peer) };
        await _client.RegisterNodeAsync(new HeadscaleRegisterRequest
        {
            Name = registration.DeviceName,
            User = _options.User,
            ForcedTags = tags,
        }, ct).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------
    // Internal helpers (visible to tests via InternalsVisibleTo)
    // ------------------------------------------------------------------

    internal static string TagForPeer(PeerId peer) => SunfishPeerTagPrefix + peer.Value;

    internal static PeerId? TryDecodePeerFromTags(IEnumerable<string> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.StartsWith(SunfishPeerTagPrefix, StringComparison.Ordinal))
            {
                var value = tag[SunfishPeerTagPrefix.Length..];
                if (!string.IsNullOrEmpty(value)) return new PeerId(value);
            }
        }
        return null;
    }

    internal static HeadscaleNode? FindNodeForPeer(IEnumerable<HeadscaleNode> nodes, PeerId peer)
    {
        var target = TagForPeer(peer);
        foreach (var node in nodes)
        {
            foreach (var tag in node.ForcedTags)
            {
                if (string.Equals(tag, target, StringComparison.Ordinal))
                {
                    return node;
                }
            }
        }
        return null;
    }

    internal static bool TryPickIpEndpoint(HeadscaleNode node, out IPAddress ip)
    {
        foreach (var raw in node.IpAddresses)
        {
            if (IPAddress.TryParse(raw, out var parsed))
            {
                ip = parsed;
                return true;
            }
        }
        ip = null!;
        return false;
    }

    private sealed class HeadscaleTcpDuplexStream : IDuplexStream
    {
        private readonly TcpClient _client;
        private readonly System.IO.Stream _stream;
        private bool _disposed;

        public HeadscaleTcpDuplexStream(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
        }

        public System.IO.Stream Stream => _stream;
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct) =>
            await _stream.ReadAsync(buffer, ct).ConfigureAwait(false);
        public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct) =>
            await _stream.WriteAsync(buffer, ct).ConfigureAwait(false);
        public Task FlushAsync(CancellationToken ct) => _stream.FlushAsync(ct);
        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            try { _stream.Dispose(); } catch { /* best-effort */ }
            try { _client.Dispose(); } catch { /* best-effort */ }
            return ValueTask.CompletedTask;
        }
    }
}
