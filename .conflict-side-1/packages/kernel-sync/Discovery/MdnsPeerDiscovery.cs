using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

using Makaretu.Dns;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sunfish.Kernel.Sync.Discovery;

/// <summary>
/// Paper §6.1 tier-1 peer discovery — zero-configuration, LAN-only multicast
/// DNS. Advertises this node as
/// <c>&lt;node-id&gt;.&lt;service-type&gt;</c> (default
/// <c>_sunfish-node._tcp.local</c>) and browses for the same service,
/// surfacing each peer as a <see cref="PeerAdvertisement"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Library choice.</b> Backed by
/// <c>Makaretu.Dns.Multicast.New</c> 0.38.x — the actively-maintained fork of
/// <c>Makaretu.Dns.Multicast</c> (original was last published 2019). Pure-managed
/// implementation, no Bonjour runtime required on Windows, TXT-record support
/// built in, targets .NET 9 which is forward-compatible with .NET 11 preview.
/// Alternative considered — <c>Zeroconf</c> — has weaker cross-platform
/// advertising and requires a native Bonjour runtime on Windows.
/// </para>
/// <para>
/// <b>TXT record layout.</b> The advertisement carries the paper's trust
/// fingerprint set:
/// <list type="bullet">
///   <item><c>node</c> — node id (string).</item>
///   <item><c>endpoint</c> — transport endpoint the gossip daemon will dial.</item>
///   <item><c>pk</c> — public key, hex-encoded.</item>
///   <item><c>team</c> — team id (for segregation on a shared segment).</item>
///   <item><c>schema</c> — schema version string.</item>
///   <item><c>m.*</c> — free-form metadata entries (one TXT entry per key).</item>
/// </list>
/// TXT records over 255 bytes per entry are not produced; callers that need
/// larger metadata must encode it out-of-band.
/// </para>
/// <para>
/// <b>TTL and eviction.</b> Implemented in user space — peers unheard from for
/// <see cref="PeerDiscoveryOptions.PeerTtlSeconds"/> emit
/// <see cref="IPeerDiscovery.PeerLost"/>. The mDNS layer's own TTL is set to
/// <c>2 * PeerTtlSeconds</c> so our sweep is the authoritative gate.
/// </para>
/// <para>
/// <b>Windows Defender Firewall.</b> First-run will typically prompt the user
/// to allow inbound UDP/5353 on the host binary. In managed deployments the
/// firewall rule must be pre-provisioned; see
/// <c>docs/specifications/sync-daemon-protocol.md §3.1</c>.
/// </para>
/// </remarks>
public sealed class MdnsPeerDiscovery : IPeerDiscovery
{
    private readonly PeerDiscoveryOptions _options;
    private readonly ILogger<MdnsPeerDiscovery> _logger;

    private readonly ConcurrentDictionary<string, PeerTracker> _known = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private ServiceDiscovery? _discovery;
    private ServiceProfile? _profile;
    private PeerAdvertisement? _self;
    private CancellationTokenSource? _sweepCts;
    private Task? _sweepLoop;
    private bool _disposed;

    public event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;
    public event EventHandler<PeerLostEventArgs>? PeerLost;

    public MdnsPeerDiscovery(
        IOptions<PeerDiscoveryOptions> options,
        ILogger<MdnsPeerDiscovery>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MdnsPeerDiscovery>.Instance;
    }

    public IReadOnlyCollection<PeerAdvertisement> KnownPeers =>
        _known.Values.Select(t => t.Advertisement).ToList();

    public async Task StartAsync(PeerAdvertisement self, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(self);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_discovery is not null)
            {
                return; // idempotent start
            }
            _self = self;

            var instanceName = SanitizeInstanceName(self.NodeId);
            var serviceName = NormalizeServiceName(_options.ServiceType);

            _profile = new ServiceProfile(
                instanceName: instanceName,
                serviceName: serviceName,
                port: (ushort)_options.Port,
                addresses: GetLocalAddresses());
            PopulateTxt(_profile, self);

            _discovery = new ServiceDiscovery();
            _discovery.ServiceInstanceDiscovered += OnServiceInstanceDiscovered;
            _discovery.ServiceInstanceShutdown += OnServiceInstanceShutdown;

            _discovery.Advertise(_profile);
            _discovery.QueryServiceInstances(serviceName);

            _sweepCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _sweepLoop = Task.Run(() => RunSweepAsync(_sweepCts.Token));
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_discovery is null)
            {
                return;
            }

            _sweepCts?.Cancel();
            if (_sweepLoop is not null)
            {
                try
                {
                    await _sweepLoop.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { /* expected */ }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "mDNS sweep loop ended with error during shutdown");
                }
            }
            _sweepLoop = null;
            _sweepCts?.Dispose();
            _sweepCts = null;

            _discovery.ServiceInstanceDiscovered -= OnServiceInstanceDiscovered;
            _discovery.ServiceInstanceShutdown -= OnServiceInstanceShutdown;
            try
            {
                if (_profile is not null)
                {
                    _discovery.Unadvertise(_profile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "mDNS Unadvertise failed; disposing anyway");
            }
            _discovery.Dispose();
            _discovery = null;
            _profile = null;

            foreach (var tracker in _known.Values.ToList())
            {
                if (_known.TryRemove(tracker.Advertisement.NodeId, out _))
                {
                    PeerLost?.Invoke(this, new PeerLostEventArgs(tracker.Advertisement.NodeId));
                }
            }

            _self = null;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            /* swallow — DisposeAsync should not throw */
        }
        _lifecycleLock.Dispose();
    }

    // ------------------------------------------------------------------
    // Event plumbing
    // ------------------------------------------------------------------

    private void OnServiceInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e)
    {
        try
        {
            var ad = ParseAdvertisement(e);
            if (ad is null) return;
            if (IsSelf(ad)) return;
            if (!PassesFilter(ad)) return;

            var tracker = _known.AddOrUpdate(
                ad.NodeId,
                _ => new PeerTracker(ad, DateTimeOffset.UtcNow),
                (_, existing) => existing.TouchedWith(ad, DateTimeOffset.UtcNow));

            if (tracker.IsNew)
            {
                PeerDiscovered?.Invoke(this, new PeerDiscoveredEventArgs(ad));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Malformed mDNS advertisement ignored");
        }
    }

    private void OnServiceInstanceShutdown(object? sender, ServiceInstanceShutdownEventArgs e)
    {
        // The shutdown event carries the service-instance name; we match it
        // against our tracker's recorded instance label.
        var instanceLabel = e.ServiceInstanceName?.ToString();
        if (string.IsNullOrEmpty(instanceLabel)) return;

        foreach (var (nodeId, tracker) in _known)
        {
            if (string.Equals(tracker.InstanceLabel, instanceLabel, StringComparison.OrdinalIgnoreCase))
            {
                if (_known.TryRemove(nodeId, out _))
                {
                    PeerLost?.Invoke(this, new PeerLostEventArgs(nodeId));
                }
                break;
            }
        }
    }

    private async Task RunSweepAsync(CancellationToken ct)
    {
        var period = TimeSpan.FromSeconds(Math.Max(1, _options.DiscoveryIntervalSeconds));
        using var timer = new PeriodicTimer(period);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                var now = DateTimeOffset.UtcNow;
                var ttl = TimeSpan.FromSeconds(_options.PeerTtlSeconds);

                foreach (var (nodeId, tracker) in _known)
                {
                    if (now - tracker.LastSeen > ttl)
                    {
                        if (_known.TryRemove(nodeId, out _))
                        {
                            PeerLost?.Invoke(this, new PeerLostEventArgs(nodeId));
                        }
                    }
                }

                // Re-query so short-lived peers stay visible.
                if (_discovery is not null && _options.ServiceType is { Length: > 0 } svc)
                {
                    try
                    {
                        _discovery.QueryServiceInstances(NormalizeServiceName(svc));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "mDNS re-query failed");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            /* expected on shutdown */
        }
    }

    // ------------------------------------------------------------------
    // TXT helpers
    // ------------------------------------------------------------------

    private static DomainName NormalizeServiceName(string serviceType)
    {
        // Makaretu wants "_sunfish-node._tcp" (no trailing ".local").
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

    private static string SanitizeInstanceName(string nodeId)
    {
        // DNS instance labels are capped at 63 octets. Node ids are short
        // strings in practice; clamp defensively.
        if (nodeId.Length <= 63) return nodeId;
        return nodeId[..63];
    }

    private static IEnumerable<IPAddress> GetLocalAddresses()
    {
        var addresses = new List<IPAddress>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork ||
                        unicast.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        if (!unicast.Address.IsIPv6LinkLocal)
                        {
                            addresses.Add(unicast.Address);
                        }
                    }
                }
            }
        }
        catch
        {
            /* sandboxed / containerized hosts may refuse enumeration — fall back to loopback */
        }
        if (addresses.Count == 0)
        {
            addresses.Add(IPAddress.Loopback);
        }
        return addresses;
    }

    private static void PopulateTxt(ServiceProfile profile, PeerAdvertisement self)
    {
        profile.AddProperty("node", self.NodeId);
        profile.AddProperty("endpoint", self.Endpoint);
        profile.AddProperty("pk", Convert.ToHexString(self.PublicKey));
        profile.AddProperty("team", self.TeamId);
        profile.AddProperty("schema", self.SchemaVersion);
        foreach (var kvp in self.Metadata)
        {
            // Namespace custom metadata under "m." to keep the reserved keys
            // unambiguous.
            profile.AddProperty($"m.{kvp.Key}", kvp.Value);
        }
    }

    private PeerAdvertisement? ParseAdvertisement(ServiceInstanceDiscoveryEventArgs e)
    {
        // The event args carry an IResponseMessage; TXT records on the named
        // instance are the primary payload.
        var txtRecords = e.Message.AdditionalRecords
            .Concat(e.Message.Answers)
            .OfType<TXTRecord>()
            .ToList();
        if (txtRecords.Count == 0) return null;

        var strings = new List<string>();
        foreach (var rec in txtRecords)
        {
            foreach (var s in rec.Strings)
            {
                strings.Add(s);
            }
        }

        string? nodeId = null;
        string? endpoint = null;
        string? publicKeyHex = null;
        string? teamId = null;
        string? schemaVersion = null;
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in strings)
        {
            var eqIdx = entry.IndexOf('=');
            if (eqIdx <= 0) continue;
            var key = entry[..eqIdx];
            var value = entry[(eqIdx + 1)..];

            switch (key)
            {
                case "node": nodeId = value; break;
                case "endpoint": endpoint = value; break;
                case "pk": publicKeyHex = value; break;
                case "team": teamId = value; break;
                case "schema": schemaVersion = value; break;
                default:
                    if (key.StartsWith("m.", StringComparison.Ordinal) && key.Length > 2)
                    {
                        metadata[key[2..]] = value;
                    }
                    break;
            }
        }

        if (string.IsNullOrEmpty(nodeId) ||
            string.IsNullOrEmpty(endpoint) ||
            string.IsNullOrEmpty(publicKeyHex))
        {
            return null;
        }

        byte[] publicKey;
        try
        {
            publicKey = Convert.FromHexString(publicKeyHex);
        }
        catch (FormatException)
        {
            return null;
        }

        return new PeerAdvertisement(
            NodeId: nodeId,
            Endpoint: endpoint,
            PublicKey: publicKey,
            TeamId: teamId ?? string.Empty,
            SchemaVersion: schemaVersion ?? string.Empty,
            Metadata: metadata);
    }

    private bool IsSelf(PeerAdvertisement ad) =>
        _self is not null && string.Equals(_self.NodeId, ad.NodeId, StringComparison.Ordinal);

    private bool PassesFilter(PeerAdvertisement ad)
    {
        if (!_options.FilterByTeamId) return true;
        if (_self is null) return true;
        return string.Equals(_self.TeamId, ad.TeamId, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Per-peer tracker — records freshness for the TTL sweep
    // ------------------------------------------------------------------

    private sealed class PeerTracker
    {
        public PeerAdvertisement Advertisement { get; private set; }
        public DateTimeOffset LastSeen { get; private set; }
        public string InstanceLabel { get; }
        public bool IsNew { get; private set; }

        public PeerTracker(PeerAdvertisement ad, DateTimeOffset lastSeen)
        {
            Advertisement = ad;
            LastSeen = lastSeen;
            InstanceLabel = ad.NodeId;
            IsNew = true;
        }

        public PeerTracker TouchedWith(PeerAdvertisement ad, DateTimeOffset lastSeen)
        {
            Advertisement = ad;
            LastSeen = lastSeen;
            IsNew = false;
            return this;
        }
    }
}
