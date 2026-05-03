using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Makaretu.Dns;
using Sunfish.Federation.Common;

namespace Sunfish.Foundation.Transport.Mdns;

/// <summary>
/// Tier-1 (link-local) <see cref="IPeerTransport"/> per ADR 0061 §"Decision".
/// Advertises the local Sunfish peer as a mDNS service instance and
/// browses for peers under the same service type, populating an
/// in-memory <see cref="PeerId"/> → <see cref="PeerEndpoint"/> cache.
/// <see cref="ResolvePeerAsync"/> is a synchronous cache lookup (no
/// network I/O); the cache is refreshed continuously in the
/// background by <c>Makaretu.Dns</c>'s service-discovery callbacks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Library choice.</b> Backed by <c>Makaretu.Dns.Multicast.New</c> 0.38.x
/// (pure-managed; no Bonjour runtime required on Windows; same library
/// used by <c>Sunfish.Kernel.Sync.Discovery.MdnsPeerDiscovery</c>).
/// </para>
/// <para>
/// <b>TXT layout.</b> Two reserved entries on the service profile:
/// <list type="bullet">
///   <item><c>peer</c> — base64url-encoded peer id (Ed25519 public key per <see cref="PeerId.Value"/>).</item>
///   <item><c>port</c> — the TCP port <see cref="ConnectAsync"/> dials, as a string-encoded integer.</item>
/// </list>
/// Other TXT keys are accepted in the wire format and ignored on parse;
/// future amendments may augment the layout without breaking older peers.
/// </para>
/// <para>
/// <b>Lifecycle.</b> <see cref="StartAsync"/> begins advertising +
/// browsing; <see cref="StopAsync"/> tears them down. <see cref="IsAvailable"/>
/// reports whether the service-discovery handle is live. The transport
/// is single-start: re-calling <see cref="StartAsync"/> after
/// <see cref="StopAsync"/> requires a fresh instance.
/// </para>
/// <para>
/// <b>CI testing.</b> mDNS depends on host networking semantics that
/// don't hold in containerized CI runners. The integration test for
/// this transport is gated on <c>SUNFISH_MDNS_TESTS=1</c> per the
/// existing convention in <c>kernel-sync</c>.
/// </para>
/// </remarks>
public sealed class MdnsPeerTransport : IPeerTransport, IAsyncDisposable
{
    private readonly MdnsPeerTransportOptions _options;
    private readonly TimeProvider _time;

    private readonly ConcurrentDictionary<PeerId, CachedPeer> _cache = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);

    private ServiceDiscovery? _discovery;
    private ServiceProfile? _profile;
    private PeerId _localPeer;
    private CancellationTokenSource? _sweepCts;
    private Task? _sweepLoop;
    private bool _disposed;

    /// <summary>Builds a transport with the supplied options.</summary>
    public MdnsPeerTransport(MdnsPeerTransportOptions? options = null, TimeProvider? time = null)
    {
        _options = options ?? new MdnsPeerTransportOptions();
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public TransportTier Tier => TransportTier.LocalNetwork;

    /// <inheritdoc />
    public bool IsAvailable => _discovery is not null;

    /// <summary>
    /// Begins advertising the local peer + browsing for remotes. Idempotent
    /// — repeated calls while running are no-ops.
    /// </summary>
    public async Task StartAsync(PeerId localPeer, IPEndPoint localEndpoint, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(localEndpoint);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_discovery is not null) return;

            _localPeer = localPeer;
            var serviceName = NormalizeServiceName(_options.ServiceType);
            var instanceName = SanitizeInstanceName(localPeer.Value);

            _profile = new ServiceProfile(
                instanceName: instanceName,
                serviceName: serviceName,
                port: (ushort)localEndpoint.Port,
                addresses: GetLocalAddresses(localEndpoint));
            _profile.AddProperty("peer", localPeer.Value);
            _profile.AddProperty("port", localEndpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));

            _discovery = new ServiceDiscovery();
            _discovery.ServiceInstanceDiscovered += OnInstanceDiscovered;
            _discovery.ServiceInstanceShutdown += OnInstanceShutdown;
            _discovery.Advertise(_profile);
            _discovery.QueryServiceInstances(serviceName);

            _sweepCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _sweepLoop = Task.Run(() => RunSweepAsync(_sweepCts.Token));
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <summary>Stops advertising + browsing. Cache is cleared.</summary>
    public async Task StopAsync(CancellationToken ct)
    {
        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_discovery is null) return;

            _sweepCts?.Cancel();
            if (_sweepLoop is not null)
            {
                try { await _sweepLoop.WaitAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { /* expected */ }
                catch { /* swallow on shutdown */ }
            }
            _sweepLoop = null;
            _sweepCts?.Dispose();
            _sweepCts = null;

            _discovery.ServiceInstanceDiscovered -= OnInstanceDiscovered;
            _discovery.ServiceInstanceShutdown -= OnInstanceShutdown;
            try { if (_profile is not null) _discovery.Unadvertise(_profile); }
            catch { /* best-effort */ }
            _discovery.Dispose();
            _discovery = null;
            _profile = null;
            _cache.Clear();
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { await StopAsync(CancellationToken.None).ConfigureAwait(false); }
        catch { /* DisposeAsync must not throw */ }
        _lifecycle.Dispose();
    }

    /// <inheritdoc />
    public Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_cache.TryGetValue(peer, out var entry))
        {
            if (_time.GetUtcNow() - entry.LastSeen > TimeSpan.FromSeconds(_options.PeerCacheTtlSeconds))
            {
                _cache.TryRemove(peer, out _);
                return Task.FromResult<PeerEndpoint?>(null);
            }
            return Task.FromResult<PeerEndpoint?>(entry.Endpoint);
        }
        return Task.FromResult<PeerEndpoint?>(null);
    }

    /// <inheritdoc />
    public async Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct)
    {
        var endpoint = await ResolvePeerAsync(peer, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"MdnsPeerTransport: peer {peer.Value} is not in the link-local cache. Call ResolvePeerAsync first or wait for the next mDNS sweep.");

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
        return new TcpDuplexStream(client);
    }

    // ------------------------------------------------------------------
    // Internal: event plumbing + sweep loop
    // ------------------------------------------------------------------

    private void OnInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e)
    {
        if (TryParseAdvertisement(e, out var peer, out var endpoint))
        {
            if (peer == _localPeer) return; // self
            _cache[peer] = new CachedPeer(endpoint, _time.GetUtcNow());
        }
    }

    private void OnInstanceShutdown(object? sender, ServiceInstanceShutdownEventArgs e)
    {
        var label = e.ServiceInstanceName?.ToString();
        if (string.IsNullOrEmpty(label)) return;
        // The instance label is the SanitizeInstanceName(PeerId.Value)
        // prefix; remove any cached peer whose id matches the label.
        foreach (var (cachedPeer, _) in _cache.ToArray())
        {
            if (label.StartsWith(SanitizeInstanceName(cachedPeer.Value), StringComparison.OrdinalIgnoreCase))
            {
                _cache.TryRemove(cachedPeer, out _);
            }
        }
    }

    private async Task RunSweepAsync(CancellationToken ct)
    {
        var period = TimeSpan.FromSeconds(Math.Max(1, _options.SweepIntervalSeconds));
        var ttl = TimeSpan.FromSeconds(_options.PeerCacheTtlSeconds);
        using var timer = new PeriodicTimer(period);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                var now = _time.GetUtcNow();
                foreach (var (peer, entry) in _cache.ToArray())
                {
                    if (now - entry.LastSeen > ttl) _cache.TryRemove(peer, out _);
                }
                if (_discovery is not null)
                {
                    try { _discovery.QueryServiceInstances(NormalizeServiceName(_options.ServiceType)); }
                    catch { /* best-effort re-query */ }
                }
            }
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
    }

    // ------------------------------------------------------------------
    // Internal helpers (visible to tests via InternalsVisibleTo)
    // ------------------------------------------------------------------

    internal static bool TryParseAdvertisement(ServiceInstanceDiscoveryEventArgs e, out PeerId peer, out PeerEndpoint endpoint)
    {
        peer = default;
        endpoint = default!;

        var txtRecords = e.Message.AdditionalRecords
            .Concat(e.Message.Answers)
            .OfType<TXTRecord>()
            .ToList();
        if (txtRecords.Count == 0) return false;

        string? peerValue = null;
        int? port = null;
        foreach (var rec in txtRecords)
        {
            foreach (var entry in rec.Strings)
            {
                var eqIdx = entry.IndexOf('=');
                if (eqIdx <= 0) continue;
                var key = entry[..eqIdx];
                var value = entry[(eqIdx + 1)..];
                switch (key)
                {
                    case "peer": peerValue = value; break;
                    case "port":
                        if (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var p)) port = p;
                        break;
                }
            }
        }
        if (string.IsNullOrEmpty(peerValue) || port is null) return false;

        var address = e.Message.AdditionalRecords
            .Concat(e.Message.Answers)
            .OfType<AddressRecord>()
            .Select(r => r.Address)
            .FirstOrDefault();
        if (address is null) return false;

        peer = new PeerId(peerValue);
        endpoint = new PeerEndpoint
        {
            Peer = peer,
            Endpoint = new IPEndPoint(address, port.Value),
            Tier = TransportTier.LocalNetwork,
            DiscoveredAt = DateTimeOffset.UtcNow,
        };
        return true;
    }

    internal void SeedCacheForTest(PeerId peer, PeerEndpoint endpoint, DateTimeOffset lastSeen)
    {
        _cache[peer] = new CachedPeer(endpoint, lastSeen);
    }

    private static DomainName NormalizeServiceName(string serviceType)
    {
        var trimmed = serviceType;
        if (trimmed.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^".local".Length];
        }
        if (trimmed.EndsWith(".", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1];
        }
        return new DomainName(trimmed);
    }

    private static string SanitizeInstanceName(string id) =>
        id.Length <= 63 ? id : id[..63];

    private static IEnumerable<IPAddress> GetLocalAddresses(IPEndPoint preferred)
    {
        var addresses = new List<IPAddress>();
        if (!preferred.Address.Equals(IPAddress.Any) && !preferred.Address.Equals(IPAddress.IPv6Any))
        {
            addresses.Add(preferred.Address);
            return addresses;
        }
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6 &&
                        !unicast.Address.IsIPv6LinkLocal)
                    {
                        addresses.Add(unicast.Address);
                    }
                }
            }
        }
        catch { /* sandboxed hosts may refuse; fall back */ }
        if (addresses.Count == 0) addresses.Add(IPAddress.Loopback);
        return addresses;
    }

    private readonly record struct CachedPeer(PeerEndpoint Endpoint, DateTimeOffset LastSeen);
}
